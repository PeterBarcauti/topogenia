<!-- 051-format-du-paquet-topogenia.md -->
<!-- Format 2.0 du paquet d'échange : références par les codes, écriture canonique, portée, personnes, schéma et son validateur -->

# 051 - Format 2.0 du paquet `.topogenia`

Statut : acceptée (date 25/10/2026)

## Contexte

La spec 7.2 fixe le contenu du paquet (un ZIP, un manifeste, sept entrées JSON) et deux exigences fortes : une écriture
déterministe (« deux exports du même état donnent le même contenu ») et l'aller-retour exact de la 7.4,
`export(import(export(P))) == export(P)` hors métadonnées du manifeste, alors que l'import en nouveau projet donne de
nouveaux identifiants (7.3). Elle laisse ouverts : la façon dont les entrées se désignent les unes les autres, le sens de
`schemaVersion`, le sort d'une remontée ouverte dont l'étape dissoute est dans la corbeille, la portée exacte de l'option
« sans membres ni auteurs », le nom technique du format et l'outil de validation du schéma publié.

## Décision

- **Aucun identifiant de la base dans le paquet.** Les entrées se désignent par les codes lisibles, uniques et jamais
  redonnés (D07) : une étape, un couloir, un irritant par son code ; une carte par le code de son étape parente (aucun
  pour la carte racine, unique) ; une flèche par ses deux extrémités (une seule flèche vivante par couple, annexe A 2.6) ;
  un usage, une ligne du déroulé, une version validée, un rôle RACI et un historique d'irritant sont rangés sous leur
  objet. Seuls les codes portent le préfixe du projet ; l'import en nouveau projet les réécrit (7.3). Un test refuse tout
  identifiant (`PackageScopeTests`).
- **Écriture canonique** (`Json/CanonicalJson`) : clés de chaque objet dans l'ordre ordinal, listes dans l'ordre de
  lecture (cartes : la racine puis les sous-cartes par code de l'étape parente ; étapes : colonne, couloir, rangée ;
  flèches : place de la source puis de la cible ; le reste par code ou par numéro), indentation de deux espaces, fins de
  ligne LF, UTF-8 sans BOM, textes écrits tels quels (seuls le guillemet, la barre oblique inverse et les caractères de
  contrôle sont échappés : le fichier n'est jamais inséré dans une page). Les valeurs absentes sont omises, les listes
  toujours écrites. Dates UTC à sept décimales (D08) ; décimaux sans zéros de fin, pour que l'échelle gardée par un moteur
  ne change pas les octets. Une liste sans clé naturelle (documents d'une même étape créés au même instant, historique
  d'un irritant au même instant) est départagée par son texte canonique : l'ordre ne dépend jamais de l'ordre de lecture
  de la base.
- **Archive** : `manifest.json` d'abord, puis les entrées dans l'ordre de la spec ; chaque entrée datée du
  01/01/1980 à 00:00, décalage nul (`Packages/ZipEntryTime`) : l'archive ne révèle aucune heure de la machine et le même
  contenu donne la même archive. Le framework type cette date en `DateTimeOffset` : c'est la seule exception à D08 du
  code livré, limitée à ce fichier par `SourceRulesTests.Production_code_uses_utc_dates_only`.
- **Manifeste** : noms de la spec 7.2 tels qu'écrits (`formatVersion`, `appVersion`...), et donc toutes les clés du paquet
  en camelCase (les valeurs des listes fermées gardent leurs codes, `month_end_close`). `schemaVersion` est la version du
  schéma publié que suivent les entrées (fichier `schemas/topogenia-package-<version>.schema.json`), égale à
  `formatVersion` en 2.0 : la version du schéma de la base n'a pas de sens hors d'elle (les migrations diffèrent d'un
  moteur à l'autre) et la montée de version des paquets se règle sur `formatVersion` (7.3). `entries[]` donne le chemin,
  la taille et le SHA-256 en hexadécimal minuscule de chaque entrée, le manifeste exclu.
- **Portée** (`Packages/ExportScope`) : les cartes vivantes sous la racine (sous-cartes vides comprises, AR-09), leurs
  couloirs, étapes et flèches vivants, et tout ce qui pend à ces étapes (fiches, documents, usages, irritants, rôles RACI
  sur un couloir vivant, traductions). Une **remontée ouverte** est exportée avec son étape dissoute, supprimée mais lue
  par le cadre et son reliquat (fiche, documents, usages, irritants) ; l'étape dissoute est écrite sans case (elle n'en a
  plus), les étapes du cadre, les flèches et les couloirs créés par la remontée seulement s'ils sont encore vivants, et
  les cases d'origine du détail ne le sont pas (elles désignent des couloirs de la sous-carte dissoute, dans la
  corbeille, et rien ne les relit en 2.0). Ne sont pas exportés : la corbeille et ce qui pend à ses objets, les remontées
  fermées, les lots de suppression, le journal des opérations, les versions des enregistrements (jetons de concurrence)
  et la version de structure du projet.
- **Personnes** (option `withoutPeople`, cochée par défaut, AR-34) : sans les personnes, le paquet n'a ni `members.json`,
  ni aucun login (auteurs `createdBy`, `updatedBy`, `validatedBy`, `changedBy`, acteur d'un couloir, assigné d'une étape,
  `exportedBy`) ; les dates restent. Avec elles, chaque utilisateur est écrit par son login, anciens membres compris ;
  `PackageWriter.UsersNamedBy` donne à l'appelant les utilisateurs dont il doit fournir le login, et un login manquant
  est une faute de l'appelant, jamais un auteur vide. Les membres portent login, nom affiché, rôle, parrain, contributeur
  clé et ordre ; pas l'e-mail.
- **Noms techniques figés** : `topogenia-package`, l'extension `.topogenia` et le nom du fichier du schéma dérivent de
  l'identifiant racine de la solution (`nameof(Topogenia)`), comme les espaces de noms et le schéma SQL Server (ADR 013),
  et non du nom affiché : renommer le produit ne doit pas rendre illisibles les paquets déjà écrits.
- **Schéma et validateur** : `schemas/topogenia-package-2.0.schema.json` (JSON Schema 2020-12) décrit le manifeste à sa
  racine et chaque entrée par la définition de `$defs` qui porte son nom ; il reprend les bornes du domaine (longueurs,
  bornes des cases, durées, heures) et les règles des textes (annexe A 2.18 : rogné, sans balise, sans caractère de
  contrôle). Il est embarqué dans `Topogenia.Interchange` et validé par `Schemas/SchemaValidator`, écrit dans la solution
  pour le sous-ensemble de mots-clés que le schéma emploie ; tout autre mot-clé est refusé au chargement, si bien que le
  schéma ne peut pas annoncer un contrôle que personne ne fait. Options écartées : JsonSchema.Net, sous licence MIT
  jusqu'à la 8.0.5 (28/12/2025) puis sous une licence qui demande d'être acceptée à partir de la 9.0, donc une branche
  permissive qui ne sera bientôt plus maintenue (spec 0.2). Le sous-ensemble employé est petit et figé par le schéma : le
  valider dans la solution évite une dépendance de plus dans le chemin d'un fichier reçu de l'extérieur.

## Conséquences

- Deux exports du même état, sur l'un ou l'autre moteur, donnent les mêmes entrées octet pour octet ; un `diff` entre
  deux paquets compare deux états d'un projet.
- L'import (P5-T02) lit les références par les codes, recrée des identifiants, ramène l'étape dissoute d'une remontée
  ouverte en supprimée dans un lot neuf, et pose les cases d'origine des étapes du cadre sur leur case actuelle.
- Le validateur ne connaît qu'un sous-ensemble : un schéma futur qui voudrait un autre mot-clé devra l'y ajouter, avec
  ses tests (`SchemaValidatorTests`).

## Vérification

- `tests/Topogenia.Interchange.Tests/Packages/` : `PackageArchiveTests` (archive, manifeste, empreintes),
  `StructureEntryTests` et `ContentEntryTests` (chaque entrée), `PackageScopeTests` (corbeille, journal, lots,
  identifiants, personnes), `DeterminismTests`, `SamplePackageTests` (`samples/conges.topogenia`).
- `tests/Topogenia.Interchange.Tests/Schemas/` : `PackageSchemaTests` (les entrées écrites suivent le schéma, le schéma
  embarqué est le fichier publié, les bornes sont celles du domaine), `SchemaValidatorTests` (chaque mot-clé, refus d'un
  mot-clé inconnu).
- `tests/Topogenia.Architecture.Tests/SourceRulesTests.cs` (`Production_code_uses_utc_dates_only`, exception unique).
