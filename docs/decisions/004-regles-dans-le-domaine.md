<!-- 004-regles-dans-le-domaine.md -->
<!-- D04 : règles métier en C# dans le Domaine, la base ne garde que les garde-fous structurels -->

# 004 - Règles métier dans le Domaine

Statut : acceptée (date 23/10/2026)

## Contexte

En 1.x, une même règle peut vivre à la fois en C#, dans un déclencheur
(`TR_flow_same_map`, `TR_step_flow_same_map`, `TR_step_raci_lane`), une contrainte ou une
procédure (`purge_*`). Ces objets ne se portent pas vers SQLite et une erreur de CHECK
remonte en erreur 500 (faiblesse 12 de la spec 5.6).

## Décision

- Les règles métier vivent dans `Topogenia.Domain`, en C# testable sans base (services de la
  spec 3.3 : `GridRules`, `FlowRules`, `RaciRules`, `Stitcher`, `TextRules`...). Les erreurs
  sont des codes stables, jamais des messages.
- La base ne garde que les garde-fous structurels portables (spec 4.2) : clés primaires et
  étrangères, CHECK simples, index uniques partiels.
- Aucun déclencheur, aucune procédure stockée, aucune vue métier. Seule exception : les vues
  de rapport du schéma `bi`, propres au mode Serveur.

## Conséquences

- Une seule source pour chaque règle ; toute règle est contrôlée en amont par le domaine, si
  bien qu'un garde-fou de la base qui se déclenche signale un défaut, pas un cas normal.
- La purge devient du C# (`Maintenance/PurgeTrash`), par lots, en transactions courtes.
- Les tests unitaires du domaine portent l'essentiel de la preuve de l'annexe A.

## Vérification

- `tests/Topogenia.Domain.Tests/` (phase 2) : chaque règle de l'annexe A a au moins un test.
- `tests/Topogenia.Architecture.Tests/SourceRulesTests.cs` :
  `No_trigger_procedure_or_function_anywhere_including_migrations` (aucun `CREATE TRIGGER`,
  `PROCEDURE` ni `FUNCTION` dans `src`, migrations comprises) et
  `No_raw_sql_outside_the_two_providers` (le SQL ne vit que dans les deux fournisseurs).
- Porte de contrôle Données et portabilité : relecture des migrations (vues seulement dans le
  schéma `bi` du fournisseur SQL Server).
