<!-- 033-creations-concurrentes-par-cle-naturelle.md -->
<!-- Nouvel essai de l'unité de travail quand une création par clé naturelle (utilisateur, traduction, usage) perd la course -->

# 033 - Créations concurrentes par clé naturelle

Statut : acceptée (date 23/10/2026)

## Contexte

La 1.x écrivait trois enregistrements par `MERGE ... WITH (HOLDLOCK)` (annexe A 5, A-5-14) : l'utilisateur invité
(`Utilisateurs.cs`, par son login), la traduction (`Traductions.cs`, par objet, champ et langue ; sans `HOLDLOCK`
dans `Translations.Ecrire`, d'où la faiblesse 11, A-4-52) et l'usage d'un document (`ServiceFiches.Documents.cs`,
par document et étape). Le `MERGE` est propre à SQL Server et la spec 4.3 interdit toute indication de verrou.

En 2.0 le domaine crée l'enregistrement quand l'espace de projet ne le contient pas et le met à jour sinon ; le
`ChangeSet` est écrit tel quel (ADR 030). Deux unités qui le créent en même temps (chacune l'a cherché avant que
l'autre écrive) se heurtent à la clé : la seconde est refusée par `CONFLIT_UNICITE`. Constat de P3-T07 :

- sous le verrou de projet (spec 4.7, 5.6-11), les traductions et les usages d'un même projet ne se rencontrent
  jamais : la seconde unité attend le verrou, charge ce que la première a validé et met à jour ;
- un utilisateur n'appartient à aucun projet : deux créations de projet (ou deux remplacements de membres) dans
  deux projets différents qui invitent le même login nouveau ne prennent pas le même verrou ; en SQL Server (lecture
  validée) elles se rencontrent et la seconde échoue sur `UQ_app_user_login_normalized` ;
- une traduction ou un usage écrits hors du verrou (gestionnaire qui l'oublierait) ont le même défaut ;
- en SQLite les transactions d'écriture sont immédiates : deux unités ne se rencontrent jamais.

Options :

1. laisser le refus `CONFLIT_UNICITE` remonter (409) : un utilisateur verrait échouer une invitation sans conflit réel ;
2. verrou applicatif global sur les utilisateurs : indication propre au moteur ou table de verrou, interdits ou
   lourds ;
3. rejouer toute l'unité quand la clé refusée est la clé naturelle d'un de ces trois enregistrements : le nouvel
   essai recharge, trouve l'enregistrement et le met à jour ; c'est le comportement du `MERGE` sans indication de
   verrou.

## Décision

- Option 3. `Writing/CreationRaces` reconnaît un refus `CONFLIT_UNICITE` dont la contrainte est
  `UQ_app_user_login_normalized`, `PK_translation` ou `PK_document_use` (noms lus dans le modèle) et le classe
  `StoreErrorKind.CreationRace`, passager comme l'interblocage et la base occupée (`StoreError.IsTransient`).
  `UnitOfWork.RunAsync` rejoue alors toute l'unité, dans la limite des trois essais de la spec 4.3-5 (50 puis
  100 ms) ; au troisième échec le refus `CONFLIT_UNICITE` est levé tel quel.
- Toute autre clé unique garde son refus : code de projet (`UQ_project_code`, A-2.1-04 : une seule création
  réussit), case, flèche, codes lisibles, numéro de version de fiche.
- Contrat pour la phase 4 (P4-T01, P4-T02) : le gestionnaire lit dans son unité, à chaque essai, ce dont le domaine
  a besoin pour décider (utilisateurs connus par login, espace avec ses traductions et ses usages), ce que le
  gabarit fait déjà en chargeant l'espace après le verrou ; il ne fait rien hors de la base avant la validation
  (ADR 030).

## Conséquences

- A-5-14 et A-4-52 : écritures portables et sûres sous concurrence sur les deux moteurs, sans `MERGE` ni indication
  de verrou ; sous le verrou de projet aucun nouvel essai n'a lieu.
- Le commentaire du port `IUnitOfWork` (Application) cite la création concurrente par clé naturelle parmi les cas
  de nouvel essai.
- Une opération de structure écrite sans le verrou de projet sur un espace chargé avant une suppression logique
  concurrente peut écrire une étape dans un couloir supprimé ou une flèche vers une étape supprimée : aucune clé ne
  le refuse (D04, pas de déclencheur). Le verrou de projet est donc obligatoire pour toute opération de structure
  (spec 4.7) ; `StructureConcurrencyTests.A_placement_that_waits_for_the_lock_while_its_lane_is_deleted_is_refused_as_lane_deleted`
  montre le refus `COULOIR_SUPPRIME` obtenu sous le verrou.

## Vérification

`tests/Topogenia.Persistence.Tests/Concurrency/CreationRaceTests.cs` : deux unités réellement simultanées, qui se
rencontrent après leur chargement, créent le même utilisateur, la même traduction, le même usage ; toutes deux
réussissent, une seule ligne, un seul nouvel essai `CreationRace` en SQL Server, aucun en SQLite. Mutation vérifiée :
classer la course comme un refus ordinaire fait échouer les trois tests en SQL Server.
