<!-- 039-decimaux-a-l-echelle-de-leur-colonne.md -->
<!-- Décimaux arrondis, contrôlés et relus à l'échelle de leur colonne sur les deux moteurs ; parcours commun des garde-fous -->

# 039 - Décimaux à l'échelle de leur colonne et parcours commun des garde-fous

Statut : acceptée (date 23/10/2026)

## Contexte

Les colonnes décimales gardent la précision de la 1.x : `decimal(9,2)` (durées des fiches et de leurs versions,
attentes des documents et des usages) et `decimal(6,1)` (heures perdues d'un irritant). SQL Server arrondit une valeur
au-delà de l'échelle de la colonne et la rend avec ses zéros de fin ; EF Core écrit un décimal en texte exact dans
SQLite, qui rend ce qu'il a reçu. Constat de la porte P3 (round 1, données M5 et m4) :

- `100000.004` minutes : refusé par `CK_step_sheet_duration_minutes` en SQLite (le CHECK compare le texte exact,
  `CAST(... AS REAL)`), arrondi à `100000.00` et accepté en SQL Server ;
- `9.755` relu `9.755` en SQLite, `9.76` en SQL Server ; `12.5` relu `12.5` et `12.50` : le JSON d'un enregistrement
  relu dépend du moteur ;
- une valeur hors de la précision (`10000000` dans un `decimal(9,2)`) : gardée par SQLite, échec 8115 en SQL Server.

L'ADR 025 veut que les deux bases refusent les mêmes lignes. Le domaine arrondit déjà durées et heures à leur échelle,
au plus loin de zéro (`Duration`, `DocumentRules`), et borne les valeurs ; la base reste le dernier garde-fou.

## Décision

- Toute colonne décimale reçoit, dans le modèle commun (`ConfigurationExtensions.Decimal`), le convertisseur
  `ScaledDecimalConverter(précision, échelle)` :
  - écriture : arrondi à l'échelle de la colonne au plus loin de zéro (`MidpointRounding.AwayFromZero`, la règle de SQL
    Server et du domaine), refus (`ArgumentOutOfRangeException`, avant toute écriture, comme une date locale) d'une valeur
    que la colonne ne peut pas tenir (dix puissance précision moins échelle), puis zéros de fin retirés ;
  - lecture : zéros de fin retirés (`12.50` devient `12.5`, `100000.00` devient `100000`, zéro sans signe).
- Ainsi SQLite stocke le texte de la valeur arrondie, la valeur que SQL Server stocke ; le CHECK SQLite
  (`CAST("colonne" AS REAL)`) compare ce nombre, exact en double pour neuf chiffres significatifs, aux mêmes bornes que
  SQL Server : les deux bases acceptent et refusent les mêmes lignes écrites par l'application, et rendent la même
  valeur, donc le même JSON. Le CHECK SQLite ne change pas : un `ROUND` dans le CHECK demanderait de reconstruire cinq
  tables SQLite pour le seul cas d'une ligne écrite à la main, que SQLite n'aligne de toute façon pas sur SQL Server
  (il ne contrôle pas le type d'une colonne et garderait le texte non arrondi). Aucune migration, ni SQLite ni SQL
  Server : le convertisseur ne change pas le schéma.
- Parcours commun des garde-fous (constat d'architecture m2 du round 1, révisé par le constat MAJ-2 du round 2) :
  `Model/GuardWriter.cs` parcourt une fois les conditions de `ModelGuards` (colonnes, colonnes décimales, ordre des
  conditions, nombres invariants) sans écrire de SQL ; chaque fournisseur écrit les opérateurs, les délimiteurs, les
  littéraux et sa comparaison d'une colonne décimale (`SqlServerGuards.Writer` : la colonne telle quelle ;
  `SqliteGuards.Writer` : `CAST("colonne" AS REAL)`), et pose lui-même filtres et CHECK dans son modèle (spec 3.2,
  ADR 025). Le texte écrit est inchangé : le modèle et les migrations existantes ne bougent pas. Révisé le 23/10/2026
  (porte P3, round 2) : le rendu commun du round 1 écrivait ces opérateurs dans `Topogenia.Persistence`, ce que la
  spec 3.2 interdit.

## Conséquences

- Une valeur relue est égale (`decimal.Equals`) à la valeur écrite arrondie à l'échelle de sa colonne, sur les deux
  moteurs ; sa représentation n'a pas de zéro de fin. Le domaine compare des décimaux par leur valeur : rien ne change
  pour l'annulation (AR-81).
- Une requête qui compare une colonne décimale à une constante passe la constante par le même convertisseur.
- Les textes et les entiers suivent le même principe (ADR 038, complément du round 2) : une garde de texte par colonne
  et des bornes d'entier dans les deux bases, et un convertisseur commun (`StoredTextConverter`) qui refuse avant toute
  écriture ce que SQL Server changerait avant tout contrôle (espace de fin, caractère hors ASCII d'un `varchar`).
- Le tri d'une colonne décimale en SQLite (collation `EF_DECIMAL` d'EF Core, sensible à la culture) reste traité à part
  (constat données M3, réglages de connexion SQLite).
- L'ADR 025 garde sa règle : aucun fragment SQL dans le modèle commun, ni par les API (`HasFilter`, `HasCheckConstraint`)
  ni dans une chaîne ; les opérateurs des conditions sont écrits par chaque fournisseur, et `GuardWritingRulesTests`
  vérifie que les deux écritures restent d'accord.

## Vérification

`tests/Topogenia.Persistence.Tests/Decimals/DecimalScaleTests.cs`, deux moteurs : durées `9.755`, `12.5`, `12.50`,
`0.005`, `0.004`, `-0.004`, `100000`, `100000.004`, `99999.995` relues et sérialisées en JSON à l'identique
(`9.76`, `12.5`, `12.5`, `0.01`, `0`, `0`, `100000`, `100000`, `100000`) et retrouvées par égalité ; `100000.005`,
`100000.01` et `-0.005` refusées par `CK_step_sheet_duration_minutes` ; `decimal(6,1)` (`9.95` relu `10`, `0.05` relu
`0.1`) ; valeur hors précision refusée avant toute écriture, borne `9999999.99` acceptée ; arrondi et normalisation du
convertisseur. Les CHECK existants (`Model/CheckConstraintTests`) passent par le parcours commun et l'écrivain de chaque fournisseur. Mutations, toutes rouges :
sans convertisseur (18 échecs : SQLite refuse `100000.004` et relit `9.755`, SQL Server relit `12.50`), arrondi au pair
au lieu du plus loin de zéro (8 échecs, `0.005` relu `0`), sans retrait des zéros de fin (11 échecs), sans refus hors
précision (3 échecs), SQLite sans `CAST(... AS REAL)` (80 échecs).
