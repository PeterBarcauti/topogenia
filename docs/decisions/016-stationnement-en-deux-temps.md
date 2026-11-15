<!-- 016-stationnement-en-deux-temps.md -->
<!-- C3 : le stationnement en deux temps des étapes est fait par l'unité de travail -->

# 016 - Stationnement en deux temps

Statut : acceptée (date 23/10/2026)

## Contexte

SQLite vérifie l'unicité ligne par ligne : déplacer un bloc d'étapes dont deux s'échangent
une case échoue sur l'index unique « une étape vivante par case », alors que SQL Server ne
vérifie qu'en fin d'instruction (spec 4.3-1). La 1.x contourne le problème par un
stationnement sur la colonne 1 000 000 000 + i, recopié à quatre endroits (annexe A 6-4).

## Décision

L'unité de travail de `Topogenia.Persistence` traite le piège en un seul endroit : toute
étape dont la case change est d'abord posée hors bornes (colonne 1 000 000 000 + i),
enregistrée, puis posée à sa case finale, dans la même transaction.

## Conséquences

- Le domaine ignore le piège de l'unicité : le `ChangeSet` qu'il remet ne contient que des
  cases finales. Il stationne pourtant, en mémoire et pour une autre raison, les étapes d'un
  bloc qu'il déplace (`SelectionChecks.Park`, colonne 1 000 000 000 + i, utilisé par le
  regroupement et le transfert) : une étape du bloc ne doit pas être décalée par
  le RG-C04 qui libère la case d'une autre étape du bloc. Ce stationnement ne sort jamais de
  l'opération (chaque étape stationnée est reposée avant la fin) ; il ne remplace pas celui de
  l'unité de travail, qui reste seul à traiter l'unicité en base (porte de contrôle round 2).
- La quadruple duplication 1.x disparaît.
- Les colonnes provisoires n'apparaissent jamais hors de la transaction. En base, le CHECK
  porte seulement sur `column_index >= 0` (comme la 1.x, `db/001_schema.sql:221`) ; la borne
  de 10 000 colonnes est contrôlée par le domaine (couverture A-2.4-07).

## Vérification

- Tests paramétrés SQLite / SQL Server dans `tests/Topogenia.Persistence.Tests/` (P3-T07) :
  échange de deux étapes, décalage d'un bloc contigu, déplacement groupé (AR-44).
- Porte de contrôle Données et portabilité.
