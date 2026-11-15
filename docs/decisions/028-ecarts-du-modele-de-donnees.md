<!-- 028-ecarts-du-modele-de-donnees.md -->
<!-- Écarts du modèle de données 2.0 au schéma 1.x qui ne sont pas écrits dans la spécification -->

# 028 - Écarts du modèle de données

Statut : acceptée (date 23/10/2026)

## Contexte

La spec 4.1 conserve le modèle logique 1.x avec une liste de changements. En le traduisant en EF Core
sur deux moteurs, d'autres points se posent, que la spec ne tranche pas ; la règle du prompt 6.4
s'applique (défaut de la spec, sinon le plus proche de la 1.x).

## Décisions

| Point | Options | Choix | Raison |
|---|---|---|---|
| Tables `language` et `app_setting` | (a) gardées ; (b) supprimées | (b) : langues et réglages viennent de la configuration (`LanguageSettings`, `AppSettings`) ; colonnes de langue `varchar(10)` sans clé étrangère | AR-29, NF12 ; le domaine ne lit aucun réglage en base |
| Précision des dates | (a) `datetime2(3)` de la 1.x ; (b) `datetime2(7)` | (b) | L'annulation compare l'enregistrement relu à celui du journal (AR-81) : une troncature à la milliseconde la ferait refuser à tort |
| Petits entiers (`smallint`, `tinyint`) | (a) gardés avec un convertisseur ; (b) `int` | (b), bornes par CHECK | ADR 026 : pas de convertisseur pour un type simple du domaine |
| Index filtrés non uniques | (a) filtrés comme en 1.x ; (b) simples | (b) | Seuls les index uniques portent une règle (spec 4.2) ; volumes faibles ; aucun filtre hors des fournisseurs |
| Index des clés étrangères | (a) un par clé (convention d'EF Core) ; (b) ceux de la 1.x | (b), convention retirée | Les colonnes d'audit ne sont pas cherchées ; moins d'écritures |
| Identifiants sans clé étrangère | (a) clé étrangère partout ; (b) identifiant seul | (b) pour `last_project_id`, `operation_id`, `origin_operation_id`, `redone_from_id`, `audit_event.project_id` (en plus des références sans clé de la 1.x) | Purge, journal taillé à 50, audit qui survit au projet (`docs/modele-donnees.md` § 5.4) |
| Unicité du rang d'une opération | (a) aucune ; (b) `(map_id, sequence)` unique | (b) | `sequence` ordonne les opérations d'une carte (spec 4.6) |
| Décimaux en SQLite | (a) `REAL` ; (b) texte exact d'EF Core | (b), comparés comme nombres dans les CHECK | Aucune perte ; une comparaison de texte refuserait 12,5 face à 100 000 |
| Nom du contexte EF | (a) `TopogeniaDbContext` (plan) ; (b) `AppDbContext` | (b) | D13 : l'en-tête du fichier écrirait le nom du produit (test `The_product_name_is_written_only_in_its_constant`) |
| Analyse des migrations | (a) code du dépôt ; (b) code généré | (b) : `generated_code = true` pour `src/Topogenia.Persistence.{SqlServer,Sqlite}/Migrations/` dans `.editorconfig` | Les migrations sont générées puis jamais modifiées (spec 4.4) ; les règles d'architecture les excluent déjà |

## Conséquences

- La migration des données 1.x (phase 5) ignore `language` et `app_setting` (reportés en configuration
  si besoin) et convertit les codes de langue tels quels.
- `docs/modele-donnees.md` § 8 tient la liste complète des écarts, avec ceux qu'écrit la spec 4.1.

## Vérification

`tests/Topogenia.Persistence.Tests/Model/` : allers-retours exacts (dates au tick, en UTC), index
uniques, CHECK et bornes, références, sur SQLite et SQL Server.
