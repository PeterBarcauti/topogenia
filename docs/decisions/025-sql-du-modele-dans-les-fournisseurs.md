<!-- 025-sql-du-modele-dans-les-fournisseurs.md -->
<!-- Les fragments SQL du modèle EF Core (filtres d'index, contraintes) vivent dans les projets de fournisseur -->

# 025 - Fragments SQL du modèle dans les fournisseurs

Statut : acceptée (date 23/10/2026), révisée le 23/10/2026 (porte P3, round 2 : aucun SQL dans le parcours commun)

## Contexte

La spec 4.2 impose des index uniques partiels (`HasFilter`) et des contraintes `CHECK`
(`HasCheckConstraint`). Dans EF Core, ces deux méthodes prennent un fragment SQL. La spec
3.2 interdit toute chaîne SQL hors de `Persistence.SqlServer` et `Persistence.Sqlite`. De
plus, la syntaxe diffère : `[deleted_at] IS NULL` en SQL Server, `"deleted_at" IS NULL` en
SQLite.

## Décision

Le modèle commun (`Topogenia.Persistence`) décrit entités, clés, relations et index sans
fragment SQL. Les garde-fous y sont décrits une fois, sans SQL (`Model/ModelGuards`,
`Model/GuardConditions`). Le parcours commun `Model/GuardWriter` trouve l'index ou la table de
chaque garde-fou, la colonne de chaque propriété, reconnaît une colonne décimale et parcourt
les conditions dans leur ordre ; il n'écrit aucun SQL. Chaque fournisseur en dérive son
écrivain (`SqlServerGuards`, `SqliteGuards`) : délimiteurs, littéraux, opérateurs (`IS NULL`,
`IN`, `=`, `BETWEEN`, `>=`, `<>`, `AND`, `OR`), fonctions des garde-fous de texte (`LEN`,
`DATALENGTH`, `LIKE` ; `length`, `GLOB`, ADR 038) et comparaison d'une colonne décimale
(ADR 039), puis pose filtres et `CHECK` dans son modèle. Le détecteur de SQL brut refuse
`HasFilter`, `HasCheckConstraint`, `HasDefaultValueSql`, `HasComputedColumnSql`,
`ToSqlQuery` et `CommandText` hors des deux fournisseurs, ainsi que les instructions SQL et,
depuis le round 2 de la porte P3, les fragments de condition (`IS NULL`, `IN (`, `BETWEEN`,
` AND `, ` OR `, ` <> `, `CAST(`, `LIKE '`, `COLLATE x`, jointures, `WHERE` d'une comparaison,
guillemet doublé d'un littéral échappé).

Options pesées pour ne pas écrire deux fois le même rendu (constat m2 du round 1) sans SQL
dans le commun (constat MAJ-2 du round 2) : (a) un rendu commun paramétré par les délimiteurs,
retenu au round 1, écrivait les opérateurs dans `Topogenia.Persistence`, contraire à la spec
3.2 ; (b) un fichier de rendu lié dans les deux projets de fournisseur, ou un projet de plus,
sort de l'organisation de la spec 3.1 et range dans un fournisseur un fichier compilé par
l'autre ; (c) retenue : parcours commun sans SQL, vocabulaire SQL par fournisseur. Seules
les quelques lignes d'opérateurs sont écrites deux fois, comme le sont déjà les deux dialectes
de texte ; une règle vérifie que les deux écritures restent d'accord.

## Conséquences

- Les deux bases ont les mêmes garde-fous, écrits chacun dans leur syntaxe.
- Le parcours des garde-fous est commun (`Model/GuardWriter.cs`, sans SQL) ; le SQL de chaque condition est écrit par
  son fournisseur (`SqlServerGuards`, `SqliteGuards`). Le texte écrit est celui du rendu du round 1 : le modèle et les
  migrations existantes ne bougent pas (`HasPendingModelChanges` faux sur les deux moteurs).
- Les garde-fous de texte (`NoTrailingSpace`, `TextLength`, `PrintableAscii`, ADR 038) passent par le même parcours et
  les mêmes écrivains (`GuardWriter.TextChecks`) ; les personnalisateurs de modèle n'écrivent plus de SQL de garde-fou,
  ils appellent `SqlServerGuards.Apply` ou `SqliteGuards.Apply`. Textes inchangés : migrations non modifiées.
- Un nouveau type de condition s'ajoute à `GuardConditions`, au parcours commun (une branche) et à chacun des deux
  écrivains (une méthode abstraite à écrire) : le compilateur refuse un écrivain incomplet.
- Les tests de persistance vérifient, sur les deux bases, que chaque index partiel et chaque
  contrainte existe et agit (phase 3).

## Vérification

`SourceRulesTests.No_raw_sql_outside_the_two_providers`, cas fautifs `HasFilter`,
`HasCheckConstraint`, `HasDefaultValueSql` et fragments de condition (`IS NULL`, `IN (`,
`BETWEEN`, ` AND `, ` OR `, ` <> `, `CAST(`, `LIKE '`, guillemet doublé) dans
`DetectorSamplesTests.cs` ; `GuardWritingRulesTests` : le modèle de chaque fournisseur contient,
pour chaque garde-fou (index partiels, `CHECK`, garde-fous de texte), ce que son écrivain écrit,
et les deux fournisseurs écrivent la même condition aux délimiteurs et à la comparaison d'un
décimal près. Mutations (constatées rouges, code remis) : l'ancien `GuardRenderer.cs` remis
dans `Topogenia.Persistence` (règle du SQL brut rouge) ; `BETWEEN` remplacé par `>=` dans
l'écrivain SQLite (les deux règles de `GuardWritingRulesTests` rouges).
