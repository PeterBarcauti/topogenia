<!-- modele-donnees.md -->
<!-- Physical data model of Topogenia 3.0 on SQL Server and SQLite: tables, columns, keys, guards, history of the schema, settings of the assistant, roles and report views, deviations from 1.x -->

# Modèle de données de Topogenia 3.0

Ce document décrit les tables de la base de Topogenia 3.0 (celles de la 2.0, plus les paramètres de l'assistant,
§ 6.25), sur ses deux moteurs (SQL Server en mode
Serveur, SQLite en mode Poste), et leur correspondance avec les enregistrements du domaine
(`src/Topogenia.Domain/Model/*.cs`) et avec le schéma 1.x (`43_Outil_Collaboratif\carto\db\`,
`MODELE_DONNEES.md`, lu sans modification). Il fait foi pour le modèle EF Core
(`src/Topogenia.Persistence/Model/`) et pour les migrations des deux fournisseurs
(`src/Topogenia.Persistence.SqlServer/Migrations/`, douze, dont celles des rôles et des vues `bi` du § 10 ;
`src/Topogenia.Persistence.Sqlite/Migrations/`, cinq ; liste au § 7.1) ; les tests
`tests/Topogenia.Persistence.Tests` le vérifient sur les deux bases.

Sources : spécification 4.1 à 4.4, 4.6, 5.5 ; ADR 002, 006, 007, 008, 013, 016, 025, 026 ; arbitrages
AR-07, AR-49, AR-64, AR-70, AR-86 ; écarts décidés pendant cette tâche : ADR 027 (historique du schéma)
et ADR 028 (écarts du modèle) ; rôles et vues `bi` du mode Serveur : ADR 032 (§ 10) ; collation binaire des
colonnes texte : ADR 038 ; décimaux et rendu commun des garde-fous : ADR 039. Le contenu des colonnes de chaque table du § 6 est extrait du modèle
compilé des deux fournisseurs ; il n'est pas recopié à la main.

## 1. Organisation

| Élément | Où | Rôle |
|---|---|---|
| `AppDbContext` | `src/Topogenia.Persistence/AppDbContext.cs` | Le contexte commun : un seul modèle pour les deux moteurs (D02). Le nom `TopogeniaDbContext` du plan écrirait le nom du produit dans l'en-tête du fichier, ce que D13 interdit (test `The_product_name_is_written_only_in_its_constant`) |
| Configurations | `src/Topogenia.Persistence/Model/*Configurations.cs` | Une configuration par table : clés, colonnes, références, index, sans aucun fragment SQL (ADR 025) |
| Garde-fous décrits | `src/Topogenia.Persistence/Model/ModelGuards.cs`, `GuardConditions.cs`, `GuardWriter.cs` | La liste unique des index uniques partiels et des CHECK, garde-fous de texte compris, décrits par des conditions sans SQL (`IsNull`, `InList`, `Between`, `TextLength`...) ; parcours commun sans SQL (colonnes, décimaux, ordre des conditions), ADR 025, 039 |
| Rendu SQL Server | `src/Topogenia.Persistence.SqlServer/SqlServerGuards.cs`, `SqlServerModelCustomizer.cs` | Schéma technique, SQL de chaque garde-fou (opérateurs, délimiteur `[colonne]`, `LEN`, `DATALENGTH`, `LIKE`), filtres et CHECK posés dans le modèle ; collation `Latin1_General_100_BIN2` sur toute colonne texte (ADR 038) |
| Rendu SQLite | `src/Topogenia.Persistence.Sqlite/SqliteGuards.cs`, `SqliteModelCustomizer.cs` | SQL de chaque garde-fou (opérateurs, délimiteur `"colonne"`, `length`, `GLOB`, décimaux comparés par `CAST(... AS REAL)`), filtres et CHECK posés dans le modèle, sans schéma |
| Moteurs | `src/Topogenia.Persistence/Schema/DatabaseEngine.cs`, `SqlServerEngine`, `SqliteEngine` | Options du contexte par moteur, catalogue, assemblage des migrations |
| Historique du schéma | `src/Topogenia.Persistence/Schema/` (`SchemaMigrations`, `SchemaVerification`, `SchemaSealInterceptor`, `MigrationSources`, `Fingerprints`), `SqlServerSchemaCatalog`, `SqliteSchemaCatalog` | Port `ISchemaMigrations` de l'Application (§ 7) |
| Accès du mode Serveur | `src/Topogenia.Persistence.SqlServer/SqlServerAccess.cs`, migrations `RolesAndReportViews` et `ApplicationViewDefinition` | Rôles `topogenia_app` et `topogenia_bi`, schéma `bi` et ses 11 vues (§ 10, ADR 032) |
| Sondes de test | `src/Topogenia.Persistence.SqlServer/SqlServerAccessProbe.cs`, `SqlServerOptionsProbe.cs`, `src/Topogenia.Persistence.Sqlite/Connection/SqliteSettingsProbe.cs` (classes statiques internes, visibles du seul projet de tests de persistance) | Le SQL dont les tests ont besoin, gardé dans le fournisseur (spec 3.2) : utilisateurs sans connexion, `EXECUTE AS USER`, droits effectifs, lecture d'une vue, droits, membres et objets changés à la main (`CatalogChange`) ; options de lecture retirées ou posées à la main (`ALTER DATABASE CURRENT SET ALLOW_SNAPSHOT_ISOLATION` ou `READ_COMMITTED_SNAPSHOT`, seules formes permises par A-5-12), inscription d'une migration refusée par une contrainte ajoutée à un historique ; réglages d'une connexion SQLite relus dans SQLite même. Aucun type de l'application n'en dépend, un seul `InternalsVisibleTo` par fournisseur, dans une sonde (`ProbeRulesTests`, ADR 037) |

## 2. Conventions

| Sujet | Règle 2.0 | 1.x |
|---|---|---|
| Noms | Tables au singulier et colonnes en `snake_case` (passe de nommage `ModelNaming` sur le nom de la propriété C#, sauf les noms 1.x gardés explicitement : `windows_login`, `column_index`, `row_index`, `occurred_at`, `touched`) | Identiques |
| Contraintes | `PK_table`, `FK_table_rôle` (rôle : première colonne de la clé sans `_id`), `UQ_` (unique sur toutes les lignes), `UX_` (unique partiel), `IX_` (index), `CK_table_règle` ; aucune valeur par défaut (`DF_`) ni déclencheur (`TR_`) | `PK_`, `FK_`, `UQ_`, `UX_`, `IX_`, `CK_`, `DF_`, `TR_` |
| Schéma | SQL Server : `topogenia`, nom technique figé comme les espaces de noms (`TechnicalNames.Schema`, `SqlServerEngine.Schema`), indépendant du nom affiché du produit ; SQLite : aucun préfixe (spec 4.1). Les migrations figent `topogenia` (ADR 013) | `ariane` |
| Clés | `Guid` donné par l'application (Guid v7, D07) : `ValueGeneratedNever` sur toute clé, aucune valeur par défaut de base | `uniqueidentifier` + `NEWSEQUENTIALID()` |
| Version | `version int`, jeton de concurrence EF Core (`IsConcurrencyToken`), incrémenté par le domaine (`Versioning`), CHECK `version BETWEEN 1 AND 2147483647` (borné à `int.MaxValue`, ADR 038), sur tout enregistrement modifiable (`IVersionedRecord`) ; aucune colonne `rowversion` (S-4.1-02) | `row_version rowversion` |
| Dates | `DateTime` UTC (D08), convertisseur `UtcDateTimeConverter` sur chaque date : une date locale est refusée à l'écriture, une date relue est marquée UTC. Précision complète des deux côtés (`datetime2` = `datetime2(7)` en SQL Server, texte ISO 8601 en SQLite) : un enregistrement relu est égal à l'enregistrement écrit, ce que compare l'annulation (AR-81) | `datetime2(3)`, `SYSUTCDATETIME()` |
| Listes fermées | Stockées par leur code stable (`Codes.Of` : `task`, `month_end_close`, `A`...), convertisseur `CodeConverter<T>`, colonne ASCII de la largeur 1.x ; chaque liste a son CHECK `IN (...)`, construit depuis les codes du domaine | `varchar(n)` + CHECK |
| Textes saisis | Unicode, longueur 1.x (`nvarchar(n)`, `TEXT` en SQLite) ; textes techniques (codes, genres, langues, empreintes) en ASCII (`varchar`) | Identiques |
| Garde de texte | Toute colonne stockée en texte, codes compris, a son CHECK `CK_<table>_<colonne>_text` (`ModelGuards.TextChecks`, 69 colonnes) : pas d'espace de fin (SQL Server ignore les espaces de fin d'une comparaison), au plus sa largeur et exactement sa largeur pour un `char(n)` (SQLite garde toute longueur), ASCII imprimable pour une colonne `varchar`. Le convertisseur `StoredTextConverter` refuse avant toute écriture, et dans une requête, un texte à espace de fin, un texte qui contient le caractère NUL (SQLite mesure et compare un texte jusqu'à son premier NUL) et un texte hors ASCII d'une colonne `varchar` (SQL Server le convertirait avant tout contrôle). Un texte trop long est `CONFLIT_INTEGRITE` sur les deux moteurs (CHECK en SQLite, 2628 en SQL Server, lu comme la même garde) (ADR 038, compléments des rounds 2 et 3). Limite : une ligne écrite à la main avec un NUL échappe en SQLite à la largeur et à l'espace de fin au-delà du NUL | Aucune garde ; `varchar` converti par SQL Server |
| Comparaison de texte | Toute colonne texte, codes compris, compare en binaire sur les deux moteurs : `Latin1_General_100_BIN2` posé par `SqlServerModelCustomizer.BinaryText` en SQL Server (et non la collation de la base), `BINARY` par défaut en SQLite ; aucun tri ni aucune égalité ne dépend de la langue de l'instance ; un tri pour l'affichage se fait en mémoire (spec 4.3-3, ADR 038). Hors du plan de base, `BIN2` trie par unité UTF-16 et SQLite par point de code : aucune requête ne trie par libellé. Recherches : `LIKE` est sensible à la casse en SQLite (`PRAGMA case_sensitive_like = ON` sur chaque connexion), donc `StartsWith` et `EndsWith` aussi ; `Contains` prend en SQLite le même texte cherché qu'en SQL Server (`SqliteTextSearch`, espace de fin admise) ; `ToUpper`, `ToLower`, `EF.Functions.Like`, `IndexOf`, `Substring` et `Length` sont refusés dans une requête sur les deux moteurs (`TextSearchParity`, ADR 038, complément du round 3) | `Latin1_General_100_CI_AS` sur `windows_login`, collation de la base ailleurs |
| Décimaux | Précision 1.x (`decimal(9,2)`, `decimal(6,1)`) en SQL Server, texte en SQLite ; convertisseur `ScaledDecimalConverter` sur chaque colonne : arrondi à l'échelle au plus loin de zéro à l'écriture, refus d'une valeur hors précision, zéros de fin retirés à la relecture : même valeur stockée, même CHECK (`CAST(... AS REAL)` en SQLite), même valeur relue et même JSON sur les deux moteurs (ADR 039) | `decimal` |
| Suppression | Logique sur `process_map`, `lane`, `step`, `flow` (`deleted_at`, `deletion_batch_id`, CHECK de cohérence) ; projet à la corbeille (`deleted_at`, `deleted_by`) ; physique pour le reste, par le code C# (purge livrée en phase 3 : `Persistence/Purge`, ADR 034) | Identique, purge par procédures |
| Références | Toute clé étrangère est `ON DELETE NO ACTION` en SQL Server et `RESTRICT` en SQLite (EF `DeleteBehavior.Restrict`) : aucune cascade, le code efface d'abord ce qui dépend (spec 4.1) | Aucune cascade |
| Ordre | Aucune requête ne trie par identifiant : SQL Server trie un `uniqueidentifier` par ses six derniers octets d'abord, SQLite le texte de l'identifiant ; un ordre stable se prend sur une colonne qui a un sens (`created_at`, `sequence`, `sort_order`), puis en mémoire (ADR 038, test `OrderParityTests`) | Sans objet |
| Entiers | Les compteurs et versions bornés à `int.MaxValue` par leur CHECK : SQL Server refuse un dépassement (8115), SQLite le garderait dans son entier de 64 bits, que la lecture d'un `int` ne relit plus ; les deux donnent `CONFLIT_INTEGRITE` (ADR 038) | `int` |
| Index | Ceux des configurations seulement : la convention d'EF Core qui indexe chaque clé étrangère est retirée (`ForeignKeyIndexConvention`) ; les colonnes d'audit (`created_by`...) ne sont pas indexées, comme en 1.x | Index explicites |

### Types par fournisseur

| Type C# | SQL Server | SQLite |
|---|---|---|
| `Guid` | `uniqueidentifier` | `TEXT` |
| `DateTime` | `datetime2` (7 décimales) | `TEXT` (`yyyy-MM-dd HH:mm:ss.FFFFFFF`) |
| `bool` | `bit` | `INTEGER` |
| `int` | `int` | `INTEGER` |
| `decimal` | `decimal(p,s)` | `TEXT` |
| texte saisi | `nvarchar(n)` ou `nvarchar(max)` | `TEXT` |
| texte technique, code d'une liste | `varchar(n)` ou `char(n)` | `TEXT` |

## 3. Vue d'ensemble

```
app_user ─┬─< project_member >── project ──< process_map ──< lane ──< step ──< flow (source, cible)
          │                         │              ▲                  │
          │                         │              └── parent_step_id ┘   (carte détaillée d'une étape)
          │                         ├──< project_counter (compteurs des codes lisibles)
          │                         ├──< deletion_batch (lots de suppression)
          │                         ├──< pain_point >── step (étape productrice)
          │                         └──< operation_log >── process_map, app_user (journal des opérations)
          │
          step ──1:1── step_sheet ──< step_operation (déroulé)
                           └───────< step_sheet_version (versions validées)
          step ──< step_raci >── lane
          step ──< document ──< document_use >── step, pain_point
          process_map ──< dissolution ──< dissolution_step >── step ; dissolution ──< dissolution_object
          translation (polymorphe), audit_event, schema_history (historique du schéma)
          assistant_settings (une ligne, 3.0) >── app_user (updated_by)
```

25 tables : 23 pour les 23 enregistrements du domaine (`IRecord`), plus `schema_history` et `assistant_settings`. EF Core y
ajoute ses deux tables techniques : `__EFMigrationsHistory` (identifiants des migrations appliquées)
et, en SQLite seulement, `__EFMigrationsLock` (verrou des migrations ; en SQL Server, le verrou est un
`sp_getapplock` d'EF Core, aucune table).

## 4. Correspondance des enregistrements du domaine

| Enregistrement (`Domain.Model`) | Table | Clé | Version |
|---|---|---|---|
| `AppUser` | `app_user` | `id` | oui |
| `Project` | `project` | `id` | oui |
| `ProjectCounter` | `project_counter` | `project_id`, `counter` | non |
| `ProjectMember` | `project_member` | `project_id`, `user_id` | oui |
| `DeletionBatch` | `deletion_batch` | `id` | non |
| `ProcessMap` | `process_map` | `id` | oui |
| `Lane` | `lane` | `id` | oui |
| `Step` | `step` | `id` | oui |
| `Flow` | `flow` | `id` | oui |
| `StepRaci` | `step_raci` | `step_id`, `lane_id` | oui |
| `StepSheet` | `step_sheet` | `id` | oui |
| `SheetOperation` | `step_operation` | `sheet_id`, `position` | non |
| `StepSheetVersion` | `step_sheet_version` | `id` | non |
| `Document` | `document` | `id` | oui |
| `PainPoint` | `pain_point` | `id` | oui |
| `PainPointStatusChange` | `pain_point_status_history` | `id` | non |
| `DocumentUse` | `document_use` | `document_id`, `step_id` | oui |
| `Translation` | `translation` | `object_type`, `object_id`, `field`, `language` | oui |
| `Dissolution` | `dissolution` | `id` | oui |
| `DissolutionStep` | `dissolution_step` | `dissolution_id`, `step_id` | non |
| `DissolutionObject` | `dissolution_object` | `dissolution_id`, `object_type`, `object_id` | non |
| `OperationLogEntry` | `operation_log` | `id` | non |
| `AuditEvent` | `audit_event` | `id` | non |
| (`SchemaHistoryEntry`, persistance) | `schema_history` | `migration_id` | non |
| (`AssistantSettingsRow`, persistance, 3.0) | `assistant_settings` | `id` | oui |

Les clés reprennent celles de `KeyOf` dans le domaine. Les enregistrements immuables sont projetés
tels quels : EF Core les construit par leur constructeur (paramètres de même nom que les
propriétés) et renseigne les propriétés `init` (`Version`, `UpdatedAt`, `UpdatedBy` de `StepRaci`,
`Dissolution`, `ProjectMember`, `DocumentUse`, `Translation`) ; les propriétés calculées (`Key`,
`Cell`, `IsRoot`, `IsInTrash`, `IsOpen`) ne sont pas stockées. Les objets valeur (ADR 026) produisent
des types simples : seules les listes fermées et les dates ont une conversion.

## 5. Garde-fous gardés dans la base (spec 4.2)

### 5.1 Index uniques partiels (ce qui est vivant)

| Index | Table (colonnes) | Filtre | Règle |
|---|---|---|---|
| `UX_step_lane_id_column_index_row_index` | `step` (lane_id, column_index, row_index) | `deleted_at IS NULL` | Une étape vivante par case (RG-C04) |
| `UX_flow_source_step_id_target_step_id` | `flow` (source_step_id, target_step_id) | `deleted_at IS NULL` | Une flèche vivante par couple (RG-C06) |
| `UX_process_map_parent_step_id` | `process_map` (parent_step_id) | `parent_step_id IS NOT NULL AND deleted_at IS NULL` | Une sous-carte vivante par étape (RG-C09) |
| `UX_process_map_project_id_root` | `process_map` (project_id) | `parent_step_id IS NULL` | Une carte racine par projet |
| `UX_step_raci_step_id_approver` | `step_raci` (step_id) | `role = 'A'` | Un seul A par étape |
| `UX_dissolution_map_id_open` | `dissolution` (map_id) | `closed_at IS NULL` | Une seule remontée ouverte par carte |
| `UX_document_use_pain_point_id` | `document_use` (pain_point_id) | `pain_point_id IS NOT NULL` | Un irritant par usage |

Chaque filtre est écrit une fois (`ModelGuards.PartialIndexes`) et rendu par chaque fournisseur. Les
filtres de SQL Server n'acceptent ni `OR` ni `NOT` : aucun filtre n'en a besoin. Un index unique sans
filtre sur une colonne qui peut être vide n'existe pas : SQL Server n'y admet qu'un seul `NULL`, SQLite
plusieurs, et les deux moteurs divergeraient.

### 5.2 Unicité sur toutes les lignes

`UQ_project_code` (code réservé tant que le projet est à la corbeille, spec 4.5), `UQ_lane_code`,
`UQ_step_code`, `UQ_pain_point_code` (un code n'est jamais redonné, D07, suppressions comprises),
`UQ_app_user_login_normalized` (S-4.1-03), `UQ_step_sheet_step_id` (une fiche par étape),
`UQ_step_sheet_version_sheet_id_version_number`, `UQ_operation_log_map_id_sequence`, et la clé
alternative `UQ_lane_id_map_id`.

### 5.3 CHECK simples

Listes fermées (un CHECK par colonne de code), bornes (`row_index` 0 à 50, `column_index` 0 à `int.MaxValue`,
`position` 1 à 50, `duration_minutes` 0 à 100 000, `percent_complete_accurate` 0 à 100, attentes et
heures perdues `>= 0`, compteurs, `structure_version` et `sequence` de 0 à `int.MaxValue`, `version` et
`version_number` de 1 à `int.MaxValue` (ADR 038)), cohérences (`deleted_at` avec `deletion_batch_id` ou `deleted_by`, `restored_at`
avec `restored_by`, `closed_at` avec `closed_by`, adresse et type d'un document, attente d'une entrée
seulement, carte racine jamais supprimée, flèche non réflexive, champ traduit selon le type d'objet), et, sur chaque colonne texte, la garde `CK_<table>_<colonne>_text` (§ 2).
Pas de `LIKE '[A-Z]'` ni d'`ISJSON` : le domaine contrôle les formats (A-5-25). La liste complète,
par table, est au § 6 ; chaque CHECK a un test qui le viole et un test de ses bornes
(`CheckConstraintTests`).

### 5.3 bis Enregistrements sans version

Un enregistrement sans version (`project_counter`, `deletion_batch`, `step_operation`, `step_sheet_version`,
`dissolution_step`, `dissolution_object`, `pain_point_status_history`, `operation_log`, `audit_event`) n'est modifié ou
supprimé que sous le verrou de son projet (`structure_version + 1`, spec 4.7) ; l'écriture le relit à travers les
projets verrouillés par l'unité et refuse sinon (`UnlockedWriteException`, erreur interne) ; une ligne changée depuis
le chargement donne `CONFLIT_VERSION` (ADR 030).

### 5.4 Références sans clé étrangère

| Colonne | Raison |
|---|---|
| `app_user.last_project_id` | Préférence d'affichage (AR-49) : un projet purgé ne doit pas bloquer la purge ; l'accueil retombe sur le premier projet |
| `deletion_batch.operation_id`, `step.origin_operation_id` | Une opération peut n'être pas journalisée, ou avoir été taillée au-delà de 50 (spec 4.6) |
| `operation_log.redone_from_id` | Idem ; et SQL Server refuse une action en cascade sur une clé auto-référencée |
| `lane.source_lane_id`, `dissolution_step.origin_lane_id` | Le couloir d'origine peut être purgé avant (1.x `db/007`, `db/008`) |
| `dissolution_object.object_id`, `translation.object_id` | Références polymorphes (1.x) ; les traductions d'un objet supprimé restent pour sa restauration |
| `audit_event.project_id` | L'audit d'une purge survit au projet purgé (spec 5.5) |
| colonnes de langue | La table `language` de la 1.x disparaît (§ 8) |

## 6. Tables

Pour chaque table : son rôle, l'enregistrement du domaine, la table 1.x, les écarts, puis les
colonnes (types par fournisseur), clés, index et CHECK tels que le modèle compilé les donne. Les
CHECK sont écrits en syntaxe SQL Server ; la variante SQLite n'est donnée que lorsqu'elle diffère
autrement que par les guillemets. La garde de texte de chaque colonne texte (`CK_<table>_<colonne>_text`, § 2,
ADR 038) n'est pas répétée par table : elle se lit dans la colonne (largeur, `varchar` ou `nvarchar`).

### 6.1 `app_user`

Personne connue par son login Windows, créée à sa première requête (annexe A 2.1).

- Domaine : `AppUser` (`Model/ProjectRecords.cs`).
- 1.x : `app_user` (`db/001`).
- Écart : `windows_login` garde la casse saisie (affichage, S-4.1-04) ; l'unicité porte sur la nouvelle colonne `login_normalized` (majuscules invariantes, `Login.Normalize`), en collation binaire (`Latin1_General_100_BIN2` en SQL Server, ADR 038 ; spec 4.1, 4.3-3) : la contrainte `UQ_app_user_windows_login` et la collation `Latin1_General_100_CI_AS` de la 1.x disparaissent, le CHECK de format (`LIKE`) aussi (format contrôlé par l'objet valeur `Login`).
- Écart : Nouvelles colonnes : `last_project_id` (dernier projet ouvert, AR-49, sans clé étrangère), `updated_by` et `version` (le profil est versionné, AR-07).
- Écart : `language` : `varchar(10)` sans clé étrangère (la table `language` n'existe plus, voir § 8).
- Écart : `display_name` et `email` venus de l'annuaire sont rognés, coupés à 128 et 256 caractères, puis rognés à nouveau (`UserRules.NewUser`), sans couper un caractère hors du plan de base ; la 1.x les enregistrait tels quels, coupés, éventuellement terminés par une espace. La migration 1.x (phase 5, P5-T05) rogne ces deux colonnes et signale chaque ligne rognée en anomalie (S-7.6-11) : aucune autre colonne texte de la 1.x ne peut finir par une espace (ADR 038, complément du round 3).

| Colonne | Propriété | SQL Server | SQLite | Null |
|---|---|---|---|---|
| `id` | `Id` | `uniqueidentifier` | `TEXT` | non |
| `created_at` | `CreatedAt` | `datetime2` | `TEXT` | non |
| `display_name` | `DisplayName` | `nvarchar(128)` | `TEXT` | non |
| `email` | `Email` | `nvarchar(256)` | `TEXT` | oui |
| `first_visit_done` | `FirstVisitDone` | `bit` | `INTEGER` | non |
| `is_admin` | `IsAdmin` | `bit` | `INTEGER` | non |
| `language` | `Language` | `varchar(10)` | `TEXT` | oui |
| `last_project_id` | `LastProjectId` | `uniqueidentifier` | `TEXT` | oui |
| `windows_login` | `Login` | `nvarchar(128)` | `TEXT` | non |
| `login_normalized` | `LoginNormalized` | `nvarchar(128)` | `TEXT` | non |
| `updated_at` | `UpdatedAt` | `datetime2` | `TEXT` | oui |
| `updated_by` | `UpdatedBy` | `uniqueidentifier` | `TEXT` | oui |
| `version` | `Version` | `int` | `INTEGER` | non |

- Clé : `PK_app_user` (id)
- FK `FK_app_user_updated_by` : (updated_by) -> `app_user` (id), sans cascade
- Index `UQ_app_user_login_normalized` (login_normalized) unique
- CHECK `CK_app_user_version` : `[version] BETWEEN 1 AND 2147483647`

### 6.2 `project`

Processus étudié ; son code (3 à 6 majuscules) préfixe les codes lisibles et reste réservé tant que le projet est à la corbeille (spec 4.5).

- Domaine : `Project`.
- 1.x : `project` (`db/001`, `db/006` pour `deleted_at`/`deleted_by`).
- Écart : Nouvelles colonnes : `classification` (spec 4.1 ; `internal`, `confidential`, `strictly_confidential`), `sharing_group_label` (AR-64, 100 caractères), `structure_version` (verrou de projet, spec 4.7 ; CHECK de 0 à `int.MaxValue`), `version` (au lieu de `row_version rowversion`, D06).
- Écart : Le CHECK de format du code (`LIKE '%[^A-Z]%'`) est porté par l'objet valeur `ProjectCode` (A-5-25) ; l'unicité du code reste en base (`UQ_project_code`, sur toutes les lignes, corbeille comprise).

| Colonne | Propriété | SQL Server | SQLite | Null |
|---|---|---|---|---|
| `id` | `Id` | `uniqueidentifier` | `TEXT` | non |
| `classification` | `Classification` | `varchar(24)` | `TEXT` | non |
| `code` | `Code` | `varchar(6)` | `TEXT` | non |
| `created_at` | `CreatedAt` | `datetime2` | `TEXT` | non |
| `created_by` | `CreatedBy` | `uniqueidentifier` | `TEXT` | non |
| `deleted_at` | `DeletedAt` | `datetime2` | `TEXT` | oui |
| `deleted_by` | `DeletedBy` | `uniqueidentifier` | `TEXT` | oui |
| `description` | `Description` | `nvarchar(4000)` | `TEXT` | oui |
| `fte_hours_per_month` | `FteHoursPerMonth` | `decimal(5,2)` | `TEXT` | non |
| `name` | `Name` | `nvarchar(200)` | `TEXT` | non |
| `shared_folder` | `SharedFolder` | `nvarchar(400)` | `TEXT` | oui |
| `sharing_group_label` | `SharingGroupLabel` | `nvarchar(100)` | `TEXT` | oui |
| `source_language` | `SourceLanguage` | `varchar(10)` | `TEXT` | non |
| `structure_version` | `StructureVersion` | `int` | `INTEGER` | non |
| `updated_at` | `UpdatedAt` | `datetime2` | `TEXT` | oui |
| `updated_by` | `UpdatedBy` | `uniqueidentifier` | `TEXT` | oui |
| `version` | `Version` | `int` | `INTEGER` | non |
| `workday_hours` | `WorkdayHours` | `decimal(4,2)` | `TEXT` | non |

- Clé : `PK_project` (id)
- FK `FK_project_created_by` : (created_by) -> `app_user` (id), sans cascade
- FK `FK_project_deleted_by` : (deleted_by) -> `app_user` (id), sans cascade
- FK `FK_project_updated_by` : (updated_by) -> `app_user` (id), sans cascade
- Index `UQ_project_code` (code) unique
- CHECK `CK_project_classification` : `[classification] IN ('internal', 'confidential', 'strictly_confidential')`
- CHECK `CK_project_deletion` : `(([deleted_at] IS NULL AND [deleted_by] IS NULL) OR ([deleted_at] IS NOT NULL AND [deleted_by] IS NOT NULL))`
- CHECK `CK_project_fte_hours_per_month` : `[fte_hours_per_month] BETWEEN 1 AND 744` (3.0, ADR 085)
- CHECK `CK_project_structure_version` : `[structure_version] BETWEEN 0 AND 2147483647`
- CHECK `CK_project_version` : `[version] BETWEEN 1 AND 2147483647`
- CHECK `CK_project_workday_hours` : `[workday_hours] BETWEEN 1 AND 24` (3.0, ADR 085)

### 6.3 `project_counter`

Compteurs des codes lisibles d'un projet, un par genre, avancés sous le verrou de projet et jamais redonnés (D07).

- Domaine : `ProjectCounter` (sans version : il n'avance que sous le verrou).
- 1.x : `project_counter`.
- Écart : Liste fermée étendue : `step`, `lane`, `pain_point`, plus `solution` (compteur S de l'annexe A 6-9, absent du CHECK 1.x).
- Écart : La valeur par défaut `DF_project_counter_last_value` disparaît : l'application écrit toujours la valeur (aucune valeur par défaut de base, ADR 007).

| Colonne | Propriété | SQL Server | SQLite | Null |
|---|---|---|---|---|
| `project_id` | `ProjectId` | `uniqueidentifier` | `TEXT` | non |
| `counter` | `Counter` | `varchar(10)` | `TEXT` | non |
| `last_value` | `LastValue` | `int` | `INTEGER` | non |

- Clé : `PK_project_counter` (project_id, counter)
- FK `FK_project_counter_project` : (project_id) -> `project` (id), sans cascade
- CHECK `CK_project_counter_counter` : `[counter] IN ('step', 'lane', 'pain_point', 'solution')`
- CHECK `CK_project_counter_last_value` : `[last_value] BETWEEN 0 AND 2147483647`

### 6.4 `project_member`

Rôle d'un utilisateur dans un projet.

- Domaine : `ProjectMember`.
- 1.x : `project_member`.
- Écart : Nouvelle colonne `version` : un membre est modifiable (rôle, mentions, rang), donc versionné (D06, AR-70).

| Colonne | Propriété | SQL Server | SQLite | Null |
|---|---|---|---|---|
| `project_id` | `ProjectId` | `uniqueidentifier` | `TEXT` | non |
| `user_id` | `UserId` | `uniqueidentifier` | `TEXT` | non |
| `created_at` | `CreatedAt` | `datetime2` | `TEXT` | non |
| `created_by` | `CreatedBy` | `uniqueidentifier` | `TEXT` | non |
| `display_order` | `DisplayOrder` | `int` | `INTEGER` | non |
| `is_key_contributor` | `IsKeyContributor` | `bit` | `INTEGER` | non |
| `is_sponsor` | `IsSponsor` | `bit` | `INTEGER` | non |
| `role` | `Role` | `varchar(12)` | `TEXT` | non |
| `updated_at` | `UpdatedAt` | `datetime2` | `TEXT` | oui |
| `updated_by` | `UpdatedBy` | `uniqueidentifier` | `TEXT` | oui |
| `version` | `Version` | `int` | `INTEGER` | non |

- Clé : `PK_project_member` (project_id, user_id)
- FK `FK_project_member_created_by` : (created_by) -> `app_user` (id), sans cascade
- FK `FK_project_member_project` : (project_id) -> `project` (id), sans cascade
- FK `FK_project_member_updated_by` : (updated_by) -> `app_user` (id), sans cascade
- FK `FK_project_member_user` : (user_id) -> `app_user` (id), sans cascade
- Index `IX_project_member_user_id` (user_id)
- CHECK `CK_project_member_role` : `[role] IN ('reader', 'contributor', 'facilitator')`
- CHECK `CK_project_member_version` : `[version] BETWEEN 1 AND 2147483647`

### 6.5 `deletion_batch`

Lot de suppression : tout ce qu'une opération a supprimé logiquement, restaurable tant qu'il n'est pas purgé (spec 4.5).

- Domaine : `DeletionBatch` (sans version).
- 1.x : `deletion_batch`.
- Écart : Nouvelle colonne `operation_id` : l'opération qui a supprimé (spec 4.6 ; identifiant sans clé étrangère, une opération peut ne pas être journalisée ou avoir été taillée).

| Colonne | Propriété | SQL Server | SQLite | Null |
|---|---|---|---|---|
| `id` | `Id` | `uniqueidentifier` | `TEXT` | non |
| `deleted_at` | `DeletedAt` | `datetime2` | `TEXT` | non |
| `deleted_by` | `DeletedBy` | `uniqueidentifier` | `TEXT` | non |
| `operation_id` | `OperationId` | `uniqueidentifier` | `TEXT` | oui |
| `project_id` | `ProjectId` | `uniqueidentifier` | `TEXT` | non |
| `restored_at` | `RestoredAt` | `datetime2` | `TEXT` | oui |
| `restored_by` | `RestoredBy` | `uniqueidentifier` | `TEXT` | oui |

- Clé : `PK_deletion_batch` (id)
- FK `FK_deletion_batch_deleted_by` : (deleted_by) -> `app_user` (id), sans cascade
- FK `FK_deletion_batch_project` : (project_id) -> `project` (id), sans cascade
- FK `FK_deletion_batch_restored_by` : (restored_by) -> `app_user` (id), sans cascade
- Index `IX_deletion_batch_project_id` (project_id)
- CHECK `CK_deletion_batch_restored` : `(([restored_at] IS NULL AND [restored_by] IS NULL) OR ([restored_at] IS NOT NULL AND [restored_by] IS NOT NULL))`

### 6.6 `process_map`

Carte en couloirs ; une carte détaillée a une étape parente (annexe A 2.2). Référence circulaire avec `step.map_id` (A-5-27).

- Domaine : `ProcessMap`.
- 1.x : `process_map`.
- Écart : `version` remplace `row_version`. L'index non unique filtré `IX_process_map_project (WHERE deleted_at IS NULL)` devient un index simple `IX_process_map_project_id` (voir § 8).

| Colonne | Propriété | SQL Server | SQLite | Null |
|---|---|---|---|---|
| `id` | `Id` | `uniqueidentifier` | `TEXT` | non |
| `created_at` | `CreatedAt` | `datetime2` | `TEXT` | non |
| `created_by` | `CreatedBy` | `uniqueidentifier` | `TEXT` | non |
| `deleted_at` | `DeletedAt` | `datetime2` | `TEXT` | oui |
| `deletion_batch_id` | `DeletionBatchId` | `uniqueidentifier` | `TEXT` | oui |
| `name` | `Name` | `nvarchar(200)` | `TEXT` | non |
| `parent_step_id` | `ParentStepId` | `uniqueidentifier` | `TEXT` | oui |
| `project_id` | `ProjectId` | `uniqueidentifier` | `TEXT` | non |
| `source_language` | `SourceLanguage` | `varchar(10)` | `TEXT` | non |
| `updated_at` | `UpdatedAt` | `datetime2` | `TEXT` | oui |
| `updated_by` | `UpdatedBy` | `uniqueidentifier` | `TEXT` | oui |
| `version` | `Version` | `int` | `INTEGER` | non |

- Clé : `PK_process_map` (id)
- FK `FK_process_map_created_by` : (created_by) -> `app_user` (id), sans cascade
- FK `FK_process_map_deletion_batch` : (deletion_batch_id) -> `deletion_batch` (id), sans cascade
- FK `FK_process_map_parent_step` : (parent_step_id) -> `step` (id), sans cascade
- FK `FK_process_map_project` : (project_id) -> `project` (id), sans cascade
- FK `FK_process_map_updated_by` : (updated_by) -> `app_user` (id), sans cascade
- Index `IX_process_map_deletion_batch_id` (deletion_batch_id)
- Index `IX_process_map_project_id` (project_id)
- Index `UX_process_map_parent_step_id` (parent_step_id) unique, filtre SQL Server `([parent_step_id] IS NOT NULL AND [deleted_at] IS NULL)`, SQLite `("parent_step_id" IS NOT NULL AND "deleted_at" IS NULL)`
- Index `UX_process_map_project_id_root` (project_id) unique, filtre SQL Server `[parent_step_id] IS NULL`, SQLite `"parent_step_id" IS NULL`
- CHECK `CK_process_map_deletion` : `(([deleted_at] IS NULL AND [deletion_batch_id] IS NULL) OR ([deleted_at] IS NOT NULL AND [deletion_batch_id] IS NOT NULL))`
- CHECK `CK_process_map_root_alive` : `([parent_step_id] IS NOT NULL OR [deleted_at] IS NULL)`
- CHECK `CK_process_map_version` : `[version] BETWEEN 1 AND 2147483647`

### 6.7 `lane`

Couloir (acteur : personne, service ou système), ordonné sans trou (annexe A 2.3).

- Domaine : `Lane`.
- 1.x : `lane` (`db/001`, `db/008` pour `source_lane_id`).
- Écart : Les CHECK de format du code (`LIKE '[A-Z][A-Z][A-Z]%-C[0-9][0-9]%'`) et de la couleur (`LIKE '#[0-9A-F]...'`) passent dans le domaine (`ReadableCode`, `LaneColor`, A-5-25).
- Écart : Clé alternative `UQ_lane_id_map_id` (id, map_id), cible de la clé composée de `step` ; un couloir ne change jamais de carte (aucune règle du domaine ne le fait), ce qu'EF Core exige d'une clé alternative.
- Écart : `source_lane_id` sans clé étrangère, comme en 1.x (le couloir d'origine peut être purgé).

| Colonne | Propriété | SQL Server | SQLite | Null |
|---|---|---|---|---|
| `id` | `Id` | `uniqueidentifier` | `TEXT` | non |
| `actor_user_id` | `ActorUserId` | `uniqueidentifier` | `TEXT` | oui |
| `code` | `Code` | `varchar(20)` | `TEXT` | non |
| `color` | `Color` | `char(7)` | `TEXT` | non |
| `created_at` | `CreatedAt` | `datetime2` | `TEXT` | non |
| `created_by` | `CreatedBy` | `uniqueidentifier` | `TEXT` | non |
| `deleted_at` | `DeletedAt` | `datetime2` | `TEXT` | oui |
| `deletion_batch_id` | `DeletionBatchId` | `uniqueidentifier` | `TEXT` | oui |
| `headcount` | `Headcount` | `int` | `INTEGER` | oui |
| `lane_type` | `LaneType` | `varchar(10)` | `TEXT` | non |
| `map_id` | `MapId` | `uniqueidentifier` | `TEXT` | non |
| `name` | `Name` | `nvarchar(150)` | `TEXT` | non |
| `sort_order` | `SortOrder` | `int` | `INTEGER` | non |
| `source_lane_id` | `SourceLaneId` | `uniqueidentifier` | `TEXT` | oui |
| `source_language` | `SourceLanguage` | `varchar(10)` | `TEXT` | non |
| `updated_at` | `UpdatedAt` | `datetime2` | `TEXT` | oui |
| `updated_by` | `UpdatedBy` | `uniqueidentifier` | `TEXT` | oui |
| `version` | `Version` | `int` | `INTEGER` | non |

- Clé : `PK_lane` (id)
- Clé alternative : `UQ_lane_id_map_id` (id, map_id)
- FK `FK_lane_actor_user` : (actor_user_id) -> `app_user` (id), sans cascade
- FK `FK_lane_created_by` : (created_by) -> `app_user` (id), sans cascade
- FK `FK_lane_deletion_batch` : (deletion_batch_id) -> `deletion_batch` (id), sans cascade
- FK `FK_lane_map` : (map_id) -> `process_map` (id), sans cascade
- FK `FK_lane_updated_by` : (updated_by) -> `app_user` (id), sans cascade
- Index `UQ_lane_code` (code) unique
- Index `IX_lane_deletion_batch_id` (deletion_batch_id)
- Index `IX_lane_map_id` (map_id)
- CHECK `CK_lane_deletion` : `(([deleted_at] IS NULL AND [deletion_batch_id] IS NULL) OR ([deleted_at] IS NOT NULL AND [deletion_batch_id] IS NOT NULL))`
- CHECK `CK_lane_lane_type` : `[lane_type] IN ('person', 'department', 'system')`
- CHECK `CK_lane_headcount` : `([headcount] IS NULL OR ([headcount] BETWEEN 0 AND 100000 AND [lane_type] IN ('person', 'department')))` (3.0, ADR 085)
- CHECK `CK_lane_version` : `[version] BETWEEN 1 AND 2147483647`

### 6.8 `step`

Étape posée dans une case (couloir, colonne, rangée) de sa carte (annexe A 2.4).

- Domaine : `Step` (`Column` et `Row` dans `column_index` et `row_index`, noms 1.x ; `Cell` est calculée, non stockée).
- 1.x : `step`.
- Écart : Clé étrangère composée `FK_step_lane` (lane_id, map_id) vers `lane` (id, map_id) : une étape et son couloir sont sur la même carte (RG-C02, spec 4.2) ; la clé simple `FK_step_map` reste.
- Écart : Nouvelle colonne `origin_operation_id` : l'opération qui a créé l'étape (spec 4.6, fin de l'heuristique des ±60 s), identifiant sans clé étrangère.
- Écart : `CK_step_column_index` borne `column_index` de 0 à `int.MaxValue` (ADR 038) : le stationnement provisoire en colonne 1 000 000 000 + i (ADR 016) doit passer ; la borne de 10 000 est celle du domaine (`Cell.MaxColumn`).
- Écart : Le déclencheur `TR_step_flow_same_map` disparaît (RG-C05 dans `Workspace/FlowLinks`, spec 4.1).

| Colonne | Propriété | SQL Server | SQLite | Null |
|---|---|---|---|---|
| `id` | `Id` | `uniqueidentifier` | `TEXT` | non |
| `assignee_user_id` | `AssigneeUserId` | `uniqueidentifier` | `TEXT` | oui |
| `automation_status` | `AutomationStatus` | `varchar(12)` | `TEXT` | non |
| `code` | `Code` | `varchar(20)` | `TEXT` | non |
| `column_index` | `Column` | `int` | `INTEGER` | non |
| `created_at` | `CreatedAt` | `datetime2` | `TEXT` | non |
| `created_by` | `CreatedBy` | `uniqueidentifier` | `TEXT` | non |
| `deleted_at` | `DeletedAt` | `datetime2` | `TEXT` | oui |
| `deletion_batch_id` | `DeletionBatchId` | `uniqueidentifier` | `TEXT` | oui |
| `label` | `Label` | `nvarchar(150)` | `TEXT` | non |
| `lane_id` | `LaneId` | `uniqueidentifier` | `TEXT` | non |
| `map_id` | `MapId` | `uniqueidentifier` | `TEXT` | non |
| `origin_operation_id` | `OriginOperationId` | `uniqueidentifier` | `TEXT` | oui |
| `row_index` | `Row` | `int` | `INTEGER` | non |
| `source_language` | `SourceLanguage` | `varchar(10)` | `TEXT` | non |
| `step_type` | `StepType` | `varchar(10)` | `TEXT` | non |
| `updated_at` | `UpdatedAt` | `datetime2` | `TEXT` | oui |
| `updated_by` | `UpdatedBy` | `uniqueidentifier` | `TEXT` | oui |
| `version` | `Version` | `int` | `INTEGER` | non |
| `volume_period` | `VolumePeriod` | `varchar(8)` | `TEXT` | oui |
| `volume_quantity` | `VolumeQuantity` | `decimal(9,2)` | `TEXT` | oui |

- Clé : `PK_step` (id)
- FK `FK_step_assignee_user` : (assignee_user_id) -> `app_user` (id), sans cascade
- FK `FK_step_created_by` : (created_by) -> `app_user` (id), sans cascade
- FK `FK_step_deletion_batch` : (deletion_batch_id) -> `deletion_batch` (id), sans cascade
- FK `FK_step_map` : (map_id) -> `process_map` (id), sans cascade
- FK `FK_step_updated_by` : (updated_by) -> `app_user` (id), sans cascade
- FK `FK_step_lane` : (lane_id, map_id) -> `lane` (id, map_id), sans cascade
- Index `IX_step_assignee_user_id` (assignee_user_id)
- Index `UQ_step_code` (code) unique
- Index `IX_step_deletion_batch_id` (deletion_batch_id)
- Index `IX_step_map_id` (map_id)
- Index `UX_step_lane_id_column_index_row_index` (lane_id, column_index, row_index) unique, filtre SQL Server `[deleted_at] IS NULL`, SQLite `"deleted_at" IS NULL`
- CHECK `CK_step_automation_status` : `[automation_status] IN ('manual', 'in_progress', 'automated')`
- CHECK `CK_step_column_index` : `[column_index] BETWEEN 0 AND 2147483647`
- CHECK `CK_step_deletion` : `(([deleted_at] IS NULL AND [deletion_batch_id] IS NULL) OR ([deleted_at] IS NOT NULL AND [deletion_batch_id] IS NOT NULL))`
- CHECK `CK_step_row_index` : `[row_index] BETWEEN 0 AND 50`
- CHECK `CK_step_step_type` : `[step_type] IN ('task', 'decision', 'parallel', 'inclusive', 'wait', 'start', 'end')`
- CHECK `CK_step_version` : `[version] BETWEEN 1 AND 2147483647`
- CHECK `CK_step_volume` : `(([volume_quantity] IS NULL AND [volume_period] IS NULL) OR ([volume_quantity] IS NOT NULL AND [volume_period] IS NOT NULL))` (3.0, ADR 085)
- CHECK `CK_step_volume_period` : `[volume_period] IN ('day', 'week', 'month', 'year')`, et sa garde de texte `CK_step_volume_period_text`
- CHECK `CK_step_volume_quantity` : `([volume_quantity] IS NULL OR ([volume_quantity] BETWEEN 0 AND 1000000 AND [step_type] = 'start'))`

### 6.9 `flow`

Flèche entre deux étapes vivantes d'une même carte (annexe A 2.6).

- Domaine : `Flow`.
- 1.x : `flow`.
- Écart : Le déclencheur `TR_flow_same_map` disparaît (RG-C05 dans le domaine) ; l'index unique filtré perd son `INCLUDE (map_id)` (propre à SQL Server, annexe A 5) ; `IX_flow_target_step` perd aussi son `INCLUDE`.

| Colonne | Propriété | SQL Server | SQLite | Null |
|---|---|---|---|---|
| `id` | `Id` | `uniqueidentifier` | `TEXT` | non |
| `created_at` | `CreatedAt` | `datetime2` | `TEXT` | non |
| `created_by` | `CreatedBy` | `uniqueidentifier` | `TEXT` | non |
| `deleted_at` | `DeletedAt` | `datetime2` | `TEXT` | oui |
| `deletion_batch_id` | `DeletionBatchId` | `uniqueidentifier` | `TEXT` | oui |
| `label` | `Label` | `nvarchar(60)` | `TEXT` | oui |
| `map_id` | `MapId` | `uniqueidentifier` | `TEXT` | non |
| `share_percent` | `SharePercent` | `decimal(5,2)` | `TEXT` | oui |
| `source_step_id` | `SourceStepId` | `uniqueidentifier` | `TEXT` | non |
| `target_step_id` | `TargetStepId` | `uniqueidentifier` | `TEXT` | non |
| `updated_at` | `UpdatedAt` | `datetime2` | `TEXT` | oui |
| `updated_by` | `UpdatedBy` | `uniqueidentifier` | `TEXT` | oui |
| `version` | `Version` | `int` | `INTEGER` | non |

- Clé : `PK_flow` (id)
- FK `FK_flow_created_by` : (created_by) -> `app_user` (id), sans cascade
- FK `FK_flow_deletion_batch` : (deletion_batch_id) -> `deletion_batch` (id), sans cascade
- FK `FK_flow_map` : (map_id) -> `process_map` (id), sans cascade
- FK `FK_flow_source_step` : (source_step_id) -> `step` (id), sans cascade
- FK `FK_flow_target_step` : (target_step_id) -> `step` (id), sans cascade
- FK `FK_flow_updated_by` : (updated_by) -> `app_user` (id), sans cascade
- Index `IX_flow_deletion_batch_id` (deletion_batch_id)
- Index `IX_flow_map_id` (map_id)
- Index `IX_flow_target_step_id` (target_step_id)
- Index `UX_flow_source_step_id_target_step_id` (source_step_id, target_step_id) unique, filtre SQL Server `[deleted_at] IS NULL`, SQLite `"deleted_at" IS NULL`
- CHECK `CK_flow_deletion` : `(([deleted_at] IS NULL AND [deletion_batch_id] IS NULL) OR ([deleted_at] IS NOT NULL AND [deletion_batch_id] IS NOT NULL))`
- CHECK `CK_flow_not_reflexive` : `[source_step_id] <> [target_step_id]`
- CHECK `CK_flow_share_percent` : `[share_percent] BETWEEN 0 AND 100` (3.0, ADR 085 ; une part seulement sur une sortie de décision ou de OU inclusif : règle du domaine, contrôlée à chaque écriture et à l'import)
- CHECK `CK_flow_version` : `[version] BETWEEN 1 AND 2147483647`

### 6.10 `step_raci`

Rôle RACI tenu par un couloir sur une étape ; le R est implicite (couloir de l'étape, annexe A 2.13).

- Domaine : `StepRaci`.
- 1.x : `step_raci`.
- Écart : Nouvelles colonnes `updated_at`, `updated_by`, `version` : un rôle qui change garde sa création (AR-86), donc il est versionné (D06).
- Écart : Le déclencheur `TR_step_raci_lane` disparaît (règle dans `RaciRules`).

| Colonne | Propriété | SQL Server | SQLite | Null |
|---|---|---|---|---|
| `step_id` | `StepId` | `uniqueidentifier` | `TEXT` | non |
| `lane_id` | `LaneId` | `uniqueidentifier` | `TEXT` | non |
| `created_at` | `CreatedAt` | `datetime2` | `TEXT` | non |
| `created_by` | `CreatedBy` | `uniqueidentifier` | `TEXT` | non |
| `role` | `Role` | `char(1)` | `TEXT` | non |
| `updated_at` | `UpdatedAt` | `datetime2` | `TEXT` | oui |
| `updated_by` | `UpdatedBy` | `uniqueidentifier` | `TEXT` | oui |
| `version` | `Version` | `int` | `INTEGER` | non |

- Clé : `PK_step_raci` (step_id, lane_id)
- FK `FK_step_raci_created_by` : (created_by) -> `app_user` (id), sans cascade
- FK `FK_step_raci_lane` : (lane_id) -> `lane` (id), sans cascade
- FK `FK_step_raci_step` : (step_id) -> `step` (id), sans cascade
- FK `FK_step_raci_updated_by` : (updated_by) -> `app_user` (id), sans cascade
- Index `IX_step_raci_lane_id` (lane_id)
- Index `UX_step_raci_step_id_approver` (step_id) unique, filtre SQL Server `[role] = 'A'`, SQLite `"role" = 'A'`
- CHECK `CK_step_raci_role` : `[role] IN ('A', 'S', 'C', 'I')`
- CHECK `CK_step_raci_version` : `[version] BETWEEN 1 AND 2147483647`

### 6.11 `step_sheet`

Fiche d'une étape, créée au premier enregistrement (annexe A 2.10).

- Domaine : `StepSheet`.
- 1.x : `step_sheet`.
- Écart : `version` remplace `row_version`.

| Colonne | Propriété | SQL Server | SQLite | Null |
|---|---|---|---|---|
| `id` | `Id` | `uniqueidentifier` | `TEXT` | non |
| `actor` | `Actor` | `nvarchar(4000)` | `TEXT` | oui |
| `clarification_request` | `ClarificationRequest` | `nvarchar(1000)` | `TEXT` | oui |
| `created_at` | `CreatedAt` | `datetime2` | `TEXT` | non |
| `created_by` | `CreatedBy` | `uniqueidentifier` | `TEXT` | non |
| `duration_minutes` | `DurationMinutes` | `decimal(9,2)` | `TEXT` | oui |
| `lead_time` | `LeadTime` | `decimal(9,2)` | `TEXT` | oui |
| `lead_time_unit` | `LeadTimeUnit` | `varchar(8)` | `TEXT` | oui |
| `free_text` | `FreeText` | `nvarchar(max)` | `TEXT` | oui |
| `frequency` | `Frequency` | `varchar(16)` | `TEXT` | oui |
| `purpose` | `Purpose` | `nvarchar(4000)` | `TEXT` | oui |
| `source_language` | `SourceLanguage` | `varchar(10)` | `TEXT` | non |
| `status` | `Status` | `varchar(12)` | `TEXT` | non |
| `step_id` | `StepId` | `uniqueidentifier` | `TEXT` | non |
| `tools` | `Tools` | `nvarchar(4000)` | `TEXT` | oui |
| `updated_at` | `UpdatedAt` | `datetime2` | `TEXT` | oui |
| `updated_by` | `UpdatedBy` | `uniqueidentifier` | `TEXT` | oui |
| `version` | `Version` | `int` | `INTEGER` | non |

- Clé : `PK_step_sheet` (id)
- FK `FK_step_sheet_created_by` : (created_by) -> `app_user` (id), sans cascade
- FK `FK_step_sheet_step` : (step_id) -> `step` (id), sans cascade
- FK `FK_step_sheet_updated_by` : (updated_by) -> `app_user` (id), sans cascade
- Index `UQ_step_sheet_step_id` (step_id) unique
- CHECK `CK_step_sheet_duration_minutes` : `[duration_minutes] BETWEEN 0 AND 100000` ; SQLite `CAST("duration_minutes" AS REAL) BETWEEN 0 AND 100000`
- CHECK `CK_step_sheet_frequency` : `[frequency] IN ('daily', 'weekly', 'monthly', 'month_end_close', 'quarterly', 'yearly', 'occasional', 'per_case')` (`per_case` en 3.0, ADR 085)
- CHECK `CK_step_sheet_status` : `[status] IN ('empty', 'draft', 'validated', 'needs_review')`
- CHECK `CK_step_sheet_version` : `[version] BETWEEN 1 AND 2147483647`

### 6.12 `step_operation`

Ligne du déroulé d'une fiche (positions 1 à 50).

- Domaine : `SheetOperation` (sans version : la fiche porte la version).
- 1.x : `step_operation`.
- Écart : `position` passe de `smallint` à `int` (le domaine porte un `int` ; aucun convertisseur, ADR 026).

| Colonne | Propriété | SQL Server | SQLite | Null |
|---|---|---|---|---|
| `sheet_id` | `SheetId` | `uniqueidentifier` | `TEXT` | non |
| `position` | `Position` | `int` | `INTEGER` | non |
| `nature` | `Nature` | `varchar(12)` | `TEXT` | oui |
| `text` | `Text` | `nvarchar(1000)` | `TEXT` | non |
| `tool` | `Tool` | `nvarchar(200)` | `TEXT` | oui |

- Clé : `PK_step_operation` (sheet_id, position)
- FK `FK_step_operation_sheet` : (sheet_id) -> `step_sheet` (id), sans cascade
- CHECK `CK_step_operation_nature` : `[nature] IN ('extract', 'copy_paste', 're_enter', 'format', 'calculate', 'check', 'chase', 'file', 'send', 'wait')`
- CHECK `CK_step_operation_position` : `[position] BETWEEN 1 AND 50`

### 6.13 `step_sheet_version`

Version validée d'une fiche, figée en JSON.

- Domaine : `StepSheetVersion` (ajout seul).
- 1.x : `step_sheet_version`.
- Écart : Nouvelles colonnes dénormalisées `frequency` et `duration_minutes` (spec 4.1, A-5-22) : lues sans fonction JSON de la base (fin de `JSON_VALUE`, `TRY_CAST`).
- Écart : Le CHECK `ISJSON(content_json) = 1` passe dans le domaine (`SheetJson`, A-5-25).

| Colonne | Propriété | SQL Server | SQLite | Null |
|---|---|---|---|---|
| `id` | `Id` | `uniqueidentifier` | `TEXT` | non |
| `content_json` | `ContentJson` | `nvarchar(max)` | `TEXT` | non |
| `duration_minutes` | `DurationMinutes` | `decimal(9,2)` | `TEXT` | oui |
| `lead_time` | `LeadTime` | `decimal(9,2)` | `TEXT` | oui |
| `lead_time_unit` | `LeadTimeUnit` | `varchar(8)` | `TEXT` | oui |
| `frequency` | `Frequency` | `varchar(16)` | `TEXT` | oui |
| `sheet_id` | `SheetId` | `uniqueidentifier` | `TEXT` | non |
| `validated_at` | `ValidatedAt` | `datetime2` | `TEXT` | non |
| `validated_by` | `ValidatedBy` | `uniqueidentifier` | `TEXT` | non |
| `version_number` | `VersionNumber` | `int` | `INTEGER` | non |

- Clé : `PK_step_sheet_version` (id)
- FK `FK_step_sheet_version_sheet` : (sheet_id) -> `step_sheet` (id), sans cascade
- FK `FK_step_sheet_version_validated_by` : (validated_by) -> `app_user` (id), sans cascade
- Index `UQ_step_sheet_version_sheet_id_version_number` (sheet_id, version_number) unique
- CHECK `CK_step_sheet_version_duration_minutes` : `[duration_minutes] BETWEEN 0 AND 100000` ; SQLite `CAST("duration_minutes" AS REAL) BETWEEN 0 AND 100000`
- CHECK `CK_step_sheet_version_frequency` : `[frequency] IN ('daily', 'weekly', 'monthly', 'month_end_close', 'quarterly', 'yearly', 'occasional', 'per_case')` (`per_case` en 3.0, ADR 085)
- CHECK `CK_step_sheet_version_version_number` : `[version_number] BETWEEN 1 AND 2147483647`

### 6.14 `document`

Document produit par une étape (sortie) ou reçu de l'extérieur (entrée), annexe A 2.11.

- Domaine : `Document`.
- 1.x : `document`.
- Écart : Le CHECK 1.x `CK_document_location` (adresse et type ensemble, type dans la liste) est écrit en deux : `CK_document_location` (ensemble) et `CK_document_location_type` (liste fermée).

| Colonne | Propriété | SQL Server | SQLite | Null |
|---|---|---|---|---|
| `id` | `Id` | `uniqueidentifier` | `TEXT` | non |
| `created_at` | `CreatedAt` | `datetime2` | `TEXT` | non |
| `created_by` | `CreatedBy` | `uniqueidentifier` | `TEXT` | non |
| `direction` | `Direction` | `varchar(6)` | `TEXT` | non |
| `location` | `Location` | `nvarchar(1000)` | `TEXT` | oui |
| `location_type` | `LocationType` | `varchar(4)` | `TEXT` | oui |
| `name` | `Name` | `nvarchar(200)` | `TEXT` | non |
| `share_confirmed` | `ShareConfirmed` | `bit` | `INTEGER` | non |
| `source_language` | `SourceLanguage` | `varchar(10)` | `TEXT` | non |
| `step_id` | `StepId` | `uniqueidentifier` | `TEXT` | non |
| `updated_at` | `UpdatedAt` | `datetime2` | `TEXT` | oui |
| `updated_by` | `UpdatedBy` | `uniqueidentifier` | `TEXT` | oui |
| `version` | `Version` | `int` | `INTEGER` | non |
| `wait_hours` | `WaitHours` | `decimal(9,2)` | `TEXT` | oui |

- Clé : `PK_document` (id)
- FK `FK_document_created_by` : (created_by) -> `app_user` (id), sans cascade
- FK `FK_document_step` : (step_id) -> `step` (id), sans cascade
- FK `FK_document_updated_by` : (updated_by) -> `app_user` (id), sans cascade
- Index `IX_document_step_id_direction` (step_id, direction)
- CHECK `CK_document_direction` : `[direction] IN ('input', 'output')`
- CHECK `CK_document_location` : `(([location] IS NULL AND [location_type] IS NULL) OR ([location] IS NOT NULL AND [location_type] IS NOT NULL))`
- CHECK `CK_document_location_type` : `[location_type] IN ('url', 'path')`
- CHECK `CK_document_version` : `[version] BETWEEN 1 AND 2147483647`
- CHECK `CK_document_wait_hours` : `([wait_hours] IS NULL OR ([wait_hours] >= 0 AND [direction] = 'input'))` ; SQLite `("wait_hours" IS NULL OR (CAST("wait_hours" AS REAL) >= 0 AND "direction" = 'input'))`

### 6.15 `pain_point`

Irritant relevé sur l'étape productrice, ou sur la carte quand `step_id` est vide (annexe A 2.12).

- Domaine : `PainPoint`.
- 1.x : `pain_point`.
- Écart : Le CHECK de format du code (`LIKE '...-I[0-9]...'`) passe dans le domaine (`ReadableCode`).

| Colonne | Propriété | SQL Server | SQLite | Null |
|---|---|---|---|---|
| `id` | `Id` | `uniqueidentifier` | `TEXT` | non |
| `code` | `Code` | `varchar(20)` | `TEXT` | non |
| `created_at` | `CreatedAt` | `datetime2` | `TEXT` | non |
| `created_by` | `CreatedBy` | `uniqueidentifier` | `TEXT` | non |
| `description` | `Description` | `nvarchar(2000)` | `TEXT` | non |
| `lost_hours_per_month` | `LostHoursPerMonth` | `decimal(6,1)` | `TEXT` | oui |
| `project_id` | `ProjectId` | `uniqueidentifier` | `TEXT` | non |
| `source_language` | `SourceLanguage` | `varchar(10)` | `TEXT` | non |
| `status` | `Status` | `varchar(12)` | `TEXT` | non |
| `step_id` | `StepId` | `uniqueidentifier` | `TEXT` | oui |
| `updated_at` | `UpdatedAt` | `datetime2` | `TEXT` | oui |
| `updated_by` | `UpdatedBy` | `uniqueidentifier` | `TEXT` | oui |
| `version` | `Version` | `int` | `INTEGER` | non |

- Clé : `PK_pain_point` (id)
- FK `FK_pain_point_created_by` : (created_by) -> `app_user` (id), sans cascade
- FK `FK_pain_point_project` : (project_id) -> `project` (id), sans cascade
- FK `FK_pain_point_step` : (step_id) -> `step` (id), sans cascade
- FK `FK_pain_point_updated_by` : (updated_by) -> `app_user` (id), sans cascade
- Index `UQ_pain_point_code` (code) unique
- Index `IX_pain_point_project_id` (project_id)
- Index `IX_pain_point_step_id` (step_id)
- CHECK `CK_pain_point_lost_hours_per_month` : `[lost_hours_per_month] >= 0` ; SQLite `CAST("lost_hours_per_month" AS REAL) >= 0`
- CHECK `CK_pain_point_status` : `[status] IN ('reported', 'scored', 'under_review', 'in_progress', 'implemented', 'adopted', 'dropped')`
- CHECK `CK_pain_point_version` : `[version] BETWEEN 1 AND 2147483647`

### 6.16 `pain_point_status_history`

Historique des statuts d'un irritant (ajout seul).

- Domaine : `PainPointStatusChange`.
- 1.x : `pain_point_status_history`.

| Colonne | Propriété | SQL Server | SQLite | Null |
|---|---|---|---|---|
| `id` | `Id` | `uniqueidentifier` | `TEXT` | non |
| `changed_at` | `ChangedAt` | `datetime2` | `TEXT` | non |
| `changed_by` | `ChangedBy` | `uniqueidentifier` | `TEXT` | non |
| `comment` | `Comment` | `nvarchar(1000)` | `TEXT` | oui |
| `pain_point_id` | `PainPointId` | `uniqueidentifier` | `TEXT` | non |
| `status_after` | `StatusAfter` | `varchar(12)` | `TEXT` | non |
| `status_before` | `StatusBefore` | `varchar(12)` | `TEXT` | oui |

- Clé : `PK_pain_point_status_history` (id)
- FK `FK_pain_point_status_history_changed_by` : (changed_by) -> `app_user` (id), sans cascade
- FK `FK_pain_point_status_history_pain_point` : (pain_point_id) -> `pain_point` (id), sans cascade
- Index `IX_pain_point_status_history_pain_point_id_changed_at` (pain_point_id, changed_at)
- CHECK `CK_pain_point_status_history_status_after` : `[status_after] IN ('reported', 'scored', 'under_review', 'in_progress', 'implemented', 'adopted', 'dropped')`
- CHECK `CK_pain_point_status_history_status_before` : `[status_before] IN ('reported', 'scored', 'under_review', 'in_progress', 'implemented', 'adopted', 'dropped')`

### 6.17 `document_use`

Une étape utilise la sortie d'une autre (coche), avec attente, %C&A et irritant (annexe A 2.11).

- Domaine : `DocumentUse`.
- 1.x : `document_use`.
- Écart : Nouvelle colonne `version` (D06). `percent_complete_accurate` passe de `tinyint` à `int` (type du domaine, ADR 026).

| Colonne | Propriété | SQL Server | SQLite | Null |
|---|---|---|---|---|
| `document_id` | `DocumentId` | `uniqueidentifier` | `TEXT` | non |
| `step_id` | `StepId` | `uniqueidentifier` | `TEXT` | non |
| `created_at` | `CreatedAt` | `datetime2` | `TEXT` | non |
| `created_by` | `CreatedBy` | `uniqueidentifier` | `TEXT` | non |
| `pain_point_id` | `PainPointId` | `uniqueidentifier` | `TEXT` | oui |
| `percent_complete_accurate` | `PercentCompleteAccurate` | `int` | `INTEGER` | oui |
| `updated_at` | `UpdatedAt` | `datetime2` | `TEXT` | oui |
| `updated_by` | `UpdatedBy` | `uniqueidentifier` | `TEXT` | oui |
| `version` | `Version` | `int` | `INTEGER` | non |
| `wait_hours` | `WaitHours` | `decimal(9,2)` | `TEXT` | oui |

- Clé : `PK_document_use` (document_id, step_id)
- FK `FK_document_use_created_by` : (created_by) -> `app_user` (id), sans cascade
- FK `FK_document_use_document` : (document_id) -> `document` (id), sans cascade
- FK `FK_document_use_pain_point` : (pain_point_id) -> `pain_point` (id), sans cascade
- FK `FK_document_use_step` : (step_id) -> `step` (id), sans cascade
- FK `FK_document_use_updated_by` : (updated_by) -> `app_user` (id), sans cascade
- Index `IX_document_use_step_id` (step_id)
- Index `UX_document_use_pain_point_id` (pain_point_id) unique, filtre SQL Server `[pain_point_id] IS NOT NULL`, SQLite `"pain_point_id" IS NOT NULL`
- CHECK `CK_document_use_percent_complete_accurate` : `[percent_complete_accurate] BETWEEN 0 AND 100`
- CHECK `CK_document_use_version` : `[version] BETWEEN 1 AND 2147483647`
- CHECK `CK_document_use_wait_hours` : `[wait_hours] >= 0` ; SQLite `CAST("wait_hours" AS REAL) >= 0`

### 6.18 `translation`

Traduction d'un texte saisi ; référence polymorphe sans clé étrangère (annexe A 2.14).

- Domaine : `Translation`.
- 1.x : `translation` (`db/001`, `db/005` pour les libellés de flèches).
- Écart : Nouvelle colonne `version` (D06). La clé étrangère vers `language` disparaît (§ 8) ; `updated_at` n'a plus de valeur par défaut de base : l'opération l'écrit toujours (le domaine l'exige non vide).

| Colonne | Propriété | SQL Server | SQLite | Null |
|---|---|---|---|---|
| `object_type` | `ObjectType` | `varchar(10)` | `TEXT` | non |
| `object_id` | `ObjectId` | `uniqueidentifier` | `TEXT` | non |
| `field` | `Field` | `varchar(10)` | `TEXT` | non |
| `language` | `Language` | `varchar(10)` | `TEXT` | non |
| `source` | `Source` | `varchar(6)` | `TEXT` | non |
| `text` | `Text` | `nvarchar(4000)` | `TEXT` | non |
| `updated_at` | `UpdatedAt` | `datetime2` | `TEXT` | non |
| `updated_by` | `UpdatedBy` | `uniqueidentifier` | `TEXT` | oui |
| `version` | `Version` | `int` | `INTEGER` | non |

- Clé : `PK_translation` (object_type, object_id, field, language)
- FK `FK_translation_updated_by` : (updated_by) -> `app_user` (id), sans cascade
- CHECK `CK_translation_object_field` : `(([object_type] IN ('project', 'map', 'lane') AND [field] = 'name') OR ([object_type] IN ('step', 'flow') AND [field] = 'label'))`
- CHECK `CK_translation_source` : `[source] IN ('auto', 'manual')`
- CHECK `CK_translation_version` : `[version] BETWEEN 1 AND 2147483647`

### 6.19 `dissolution`

Remontée d'un détail dans le flux : son cadre et son lot (annexe A 2.9). Une seule ouverte par carte.

- Domaine : `Dissolution`.
- 1.x : `dissolution` (`db/007`).
- Écart : Nouvelles colonnes `updated_at`, `updated_by`, `version` : un cadre se ferme et se rouvre (D06).

| Colonne | Propriété | SQL Server | SQLite | Null |
|---|---|---|---|---|
| `id` | `Id` | `uniqueidentifier` | `TEXT` | non |
| `closed_at` | `ClosedAt` | `datetime2` | `TEXT` | oui |
| `closed_by` | `ClosedBy` | `uniqueidentifier` | `TEXT` | oui |
| `created_at` | `CreatedAt` | `datetime2` | `TEXT` | non |
| `created_by` | `CreatedBy` | `uniqueidentifier` | `TEXT` | non |
| `deletion_batch_id` | `DeletionBatchId` | `uniqueidentifier` | `TEXT` | non |
| `map_id` | `MapId` | `uniqueidentifier` | `TEXT` | non |
| `step_id` | `StepId` | `uniqueidentifier` | `TEXT` | non |
| `updated_at` | `UpdatedAt` | `datetime2` | `TEXT` | oui |
| `updated_by` | `UpdatedBy` | `uniqueidentifier` | `TEXT` | oui |
| `version` | `Version` | `int` | `INTEGER` | non |

- Clé : `PK_dissolution` (id)
- FK `FK_dissolution_closed_by` : (closed_by) -> `app_user` (id), sans cascade
- FK `FK_dissolution_created_by` : (created_by) -> `app_user` (id), sans cascade
- FK `FK_dissolution_deletion_batch` : (deletion_batch_id) -> `deletion_batch` (id), sans cascade
- FK `FK_dissolution_map` : (map_id) -> `process_map` (id), sans cascade
- FK `FK_dissolution_step` : (step_id) -> `step` (id), sans cascade
- FK `FK_dissolution_updated_by` : (updated_by) -> `app_user` (id), sans cascade
- Index `IX_dissolution_deletion_batch_id` (deletion_batch_id)
- Index `IX_dissolution_step_id` (step_id)
- Index `UX_dissolution_map_id_open` (map_id) unique, filtre SQL Server `[closed_at] IS NULL`, SQLite `"closed_at" IS NULL`
- CHECK `CK_dissolution_closed` : `(([closed_at] IS NULL AND [closed_by] IS NULL) OR ([closed_at] IS NOT NULL AND [closed_by] IS NOT NULL))`
- CHECK `CK_dissolution_version` : `[version] BETWEEN 1 AND 2147483647`

### 6.20 `dissolution_step`

Étape d'un cadre, avec sa case d'origine dans la carte détaillée.

- Domaine : `DissolutionStep`.
- 1.x : `dissolution_step` (`db/007`).
- Écart : `origin_lane_id` sans clé étrangère, comme en 1.x (couloir à la corbeille, purgé avant la remontée).

| Colonne | Propriété | SQL Server | SQLite | Null |
|---|---|---|---|---|
| `dissolution_id` | `DissolutionId` | `uniqueidentifier` | `TEXT` | non |
| `step_id` | `StepId` | `uniqueidentifier` | `TEXT` | non |
| `origin_column` | `OriginColumn` | `int` | `INTEGER` | non |
| `origin_lane_id` | `OriginLaneId` | `uniqueidentifier` | `TEXT` | non |
| `origin_row` | `OriginRow` | `int` | `INTEGER` | non |

- Clé : `PK_dissolution_step` (dissolution_id, step_id)
- FK `FK_dissolution_step_dissolution` : (dissolution_id) -> `dissolution` (id), sans cascade
- FK `FK_dissolution_step_step` : (step_id) -> `step` (id), sans cascade
- Index `IX_dissolution_step_step_id` (step_id)

### 6.21 `dissolution_object`

Flèche ou couloir créé par une remontée.

- Domaine : `DissolutionObject`.
- 1.x : `dissolution_object` (`db/007`).
- Écart : `object_id` polymorphe, sans clé étrangère ; liste fermée `flow`, `lane` (`DissolutionObjectTypes`).

| Colonne | Propriété | SQL Server | SQLite | Null |
|---|---|---|---|---|
| `dissolution_id` | `DissolutionId` | `uniqueidentifier` | `TEXT` | non |
| `object_type` | `ObjectType` | `varchar(10)` | `TEXT` | non |
| `object_id` | `ObjectId` | `uniqueidentifier` | `TEXT` | non |

- Clé : `PK_dissolution_object` (dissolution_id, object_type, object_id)
- FK `FK_dissolution_object_dissolution` : (dissolution_id) -> `dissolution` (id), sans cascade
- CHECK `CK_dissolution_object_object_type` : `[object_type] IN ('flow', 'lane')`

### 6.22 `operation_log`

Journal des opérations de structure : aller, inverse et objets touchés de chaque opération, pour annuler et rétablir (spec 4.6, D05).

- Domaine : `OperationLogEntry` (`Model/OperationLogRecords.cs`, sans version).
- 1.x : aucune, table nouvelle en 2.0 (spec 4.6).
- Écart : Colonnes de la spec 4.6 : `id`, `project_id`, `map_id`, `user_id`, `kind`, `sequence`, `forward_json`, `inverse_json`, `touched` (propriété `TouchedJson`), `created_at`, `undone_at`, `redone_from_id`.
- Écart : `UQ_operation_log_map_id_sequence` : `sequence` ordonne les opérations d'une carte, une fois chacune (S-4.6-03) ; `IX_operation_log_map_id_user_id` sert la pile d'un utilisateur sur une carte.
- Écart : `redone_from_id` est un identifiant sans clé étrangère : les plus anciennes entrées sont effacées au-delà de 50 (spec 4.6), et SQL Server refuse une action en cascade sur une clé qui pointe sur sa propre table.

| Colonne | Propriété | SQL Server | SQLite | Null |
|---|---|---|---|---|
| `id` | `Id` | `uniqueidentifier` | `TEXT` | non |
| `created_at` | `CreatedAt` | `datetime2` | `TEXT` | non |
| `forward_json` | `ForwardJson` | `nvarchar(max)` | `TEXT` | non |
| `inverse_json` | `InverseJson` | `nvarchar(max)` | `TEXT` | non |
| `kind` | `Kind` | `varchar(60)` | `TEXT` | non |
| `map_id` | `MapId` | `uniqueidentifier` | `TEXT` | non |
| `project_id` | `ProjectId` | `uniqueidentifier` | `TEXT` | non |
| `redone_from_id` | `RedoneFromId` | `uniqueidentifier` | `TEXT` | oui |
| `sequence` | `Sequence` | `int` | `INTEGER` | non |
| `touched` | `TouchedJson` | `nvarchar(max)` | `TEXT` | non |
| `undone_at` | `UndoneAt` | `datetime2` | `TEXT` | oui |
| `user_id` | `UserId` | `uniqueidentifier` | `TEXT` | non |

- Clé : `PK_operation_log` (id)
- FK `FK_operation_log_map` : (map_id) -> `process_map` (id), sans cascade
- FK `FK_operation_log_project` : (project_id) -> `project` (id), sans cascade
- FK `FK_operation_log_user` : (user_id) -> `app_user` (id), sans cascade
- Index `IX_operation_log_project_id` (project_id)
- Index `UQ_operation_log_map_id_sequence` (map_id, sequence) unique
- Index `IX_operation_log_map_id_user_id` (map_id, user_id)
- CHECK `CK_operation_log_sequence` : `[sequence] BETWEEN 0 AND 2147483647`

### 6.23 `audit_event`

Journal de sécurité : qui, quand, quoi, identifiants seulement (spec 5.5).

- Domaine : `AuditEvent` (ajout seul ; `At` dans `occurred_at`, convention `*_at`).
- 1.x : aucune, table nouvelle en 2.0 (spec 5.5).
- Écart : `project_id` sans clé étrangère : l'audit d'une purge survit au projet purgé. `IX_audit_event_occurred_at` sert la conservation paramétrable (1 an par défaut).

| Colonne | Propriété | SQL Server | SQLite | Null |
|---|---|---|---|---|
| `id` | `Id` | `uniqueidentifier` | `TEXT` | non |
| `occurred_at` | `At` | `datetime2` | `TEXT` | non |
| `details` | `Details` | `nvarchar(4000)` | `TEXT` | oui |
| `kind` | `Kind` | `varchar(60)` | `TEXT` | non |
| `project_id` | `ProjectId` | `uniqueidentifier` | `TEXT` | oui |
| `user_id` | `UserId` | `uniqueidentifier` | `TEXT` | oui |

- Clé : `PK_audit_event` (id)
- FK `FK_audit_event_user` : (user_id) -> `app_user` (id), sans cascade
- Index `IX_audit_event_occurred_at` (occurred_at)
- Index `IX_audit_event_project_id` (project_id)

### 6.24 `schema_history`

Historique des migrations appliquées, avec leurs empreintes (spec 4.4), voir § 7.

- Domaine : `SchemaHistoryEntry` (`src/Topogenia.Persistence/Schema/`, type de la persistance, pas du domaine).
- 1.x : remplace `schema_version` (`MiseAJourBase.cs` : version, script, `applied_at`, `applied_by`, `checksum`).
- Écart : La version n'est plus un numéro de script mais l'identifiant de la migration EF Core ; l'empreinte de la source reste une SHA-256 normalisée LF, à laquelle s'ajoute l'empreinte du schéma ; `applied_by` (`SUSER_SNAME()`, propre à SQL Server) est remplacé par `application_version`.

| Colonne | Propriété | SQL Server | SQLite | Null |
|---|---|---|---|---|
| `migration_id` | `MigrationId` | `varchar(150)` | `TEXT` | non |
| `application_version` | `ApplicationVersion` | `varchar(32)` | `TEXT` | non |
| `applied_at` | `AppliedAt` | `datetime2` | `TEXT` | non |
| `migration_fingerprint` | `MigrationFingerprint` | `char(64)` | `TEXT` | non |
| `schema_fingerprint` | `SchemaFingerprint` | `char(64)` | `TEXT` | oui |

- Clé : `PK_schema_history` (migration_id)

### 6.25 `assistant_settings`

Les paramètres de l'assistant de l'installation (3.0, spec 3.0 section 4, ADR 077) : une seule ligne.

- Domaine : aucun ; `AssistantSettingsRow` (`src/Topogenia.Persistence/Assistant/`, type de la persistance, hors du cœur fermé, comme `schema_history`) ; l'application lit et écrit `AssistantSettings` par le port `IAssistantSettingsStore`.
- 1.x : aucune, table nouvelle en 3.0.
- Une seule ligne (`CK_assistant_settings_single` : `id` = 1), absente tant que l'administrateur n'a rien enregistré (les valeurs par défaut sont alors celles de `AssistantSettings.Default` : aucun connecteur, plafond de 64 000 jetons, projets Internes seulement). La version est le jeton de concurrence, comme pour un enregistrement versionné du domaine (D06) : deux administrateurs qui changent les paramètres en même temps ne s'écrasent pas (`CONFLIT_VERSION`).
- `allowed_classifications` : les codes des classifications qui peuvent partir vers l'IA, séparés par des virgules.
- `protected_secret` : la clé de l'API Anthropic telle que la protection des données d'ASP.NET Core l'a chiffrée (ADR 077) ; jamais en clair, jamais rendue par l'API. `secret_hint` : ses quatre derniers caractères, montrés à l'administrateur. Bedrock n'a aucune clé (profil AWS ou identité de l'hôte).
- L'écriture se fait dans l'unité de la requête, avec l'événement d'audit du changement (`assistant.settings_changed`, noms des champs changés, jamais leurs valeurs).

| Colonne | Propriété | SQL Server | SQLite | Null |
|---|---|---|---|---|
| `id` | `Id` | `int` | `INTEGER` | non |
| `allowed_classifications` | `AllowedClassifications` | `varchar(60)` | `TEXT` | non |
| `aws_profile` | `AwsProfile` | `varchar(100)` | `TEXT` | oui |
| `connector` | `Connector` | `varchar(20)` | `TEXT` | non |
| `max_output_tokens` | `MaxOutputTokens` | `int` | `INTEGER` | non |
| `model` | `Model` | `varchar(200)` | `TEXT` | oui |
| `protected_secret` | `ProtectedSecret` | `varchar(2000)` | `TEXT` | oui |
| `region` | `Region` | `varchar(40)` | `TEXT` | oui |
| `secret_hint` | `SecretHint` | `varchar(4)` | `TEXT` | oui |
| `updated_at` | `UpdatedAt` | `datetime2` | `TEXT` | oui |
| `updated_by` | `UpdatedBy` | `uniqueidentifier` | `TEXT` | oui |
| `version` | `Version` | `int` | `INTEGER` | non |

- Clé : `PK_assistant_settings` (id)
- FK `FK_assistant_settings_updated_by` : (updated_by) -> `app_user` (id), sans cascade
- CHECK `CK_assistant_settings_single` : `id` = 1 ; `CK_assistant_settings_connector` : `none`, `copy_paste`, `anthropic`, `bedrock` ; `CK_assistant_settings_max_output_tokens` : 256 à 128 000 ; `CK_assistant_settings_version` : 1 à `int.MaxValue` ; la garde `CK_assistant_settings_<colonne>_text` sur chaque colonne texte (§ 2)
- Tests : une ligne refusée par CHECK (`CheckConstraintTests`) ; les bornes 256 et 128 000 acceptées par les deux bases à travers les routes (`AssistantRoutesTests`), la table n'ayant qu'une ligne

## 7. Migrations et historique du schéma (spec 4.4, ADR 027)

### 7.1 Un jeu de migrations par fournisseur

| Fournisseur | Migration initiale | Contenu |
|---|---|---|
| SQL Server | `src/Topogenia.Persistence.SqlServer/Migrations/20261023074436_Initial.cs` | `EnsureSchema("topogenia")`, les 24 tables, leurs clés, index, filtres et CHECK ; 13 clés étrangères ajoutées après la création des tables (références circulaires `process_map` et `step`) |
| SQLite | `src/Topogenia.Persistence.Sqlite/Migrations/20261023074737_Initial.cs` | Les 24 tables sans préfixe ; EF Core écrit toutes les clés étrangères dans le `CREATE TABLE` (SQLite accepte une référence vers une table créée plus loin) ; une seule transaction |
| SQL Server | `src/Topogenia.Persistence.SqlServer/Migrations/20261023083307_RolesAndReportViews.cs` | Deuxième migration, sans changement du modèle (générée vide par `dotnet ef`, puis écrite à la main) : propriétaire `dbo` du schéma `topogenia`, rôles `topogenia_app` et `topogenia_bi` et leurs droits, schéma `bi` et ses 11 vues (§ 10). Aucune migration SQLite correspondante (ADR 032) |
| SQL Server | `src/Topogenia.Persistence.SqlServer/Migrations/20261023092342_SnapshotIsolation.cs` | Troisième migration, sans changement du modèle (1.x `db/003_concurrence.sql`, ADR 031) : `ALTER DATABASE CURRENT SET ALLOW_SNAPSHOT_ISOLATION ON` puis `SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE`, chacune hors transaction et seulement si l'option n'est pas posée (rejouable) ; `Down` les retire. EF Core l'enregistre hors transaction (§ 7.3) |
| SQL Server | `src/Topogenia.Persistence.SqlServer/Migrations/20261023093315_SnapshotIsolationCheck.cs` | Quatrième migration, en transaction : vérifie que les deux options sont posées (`THROW` sinon) ; sa validation inscrit les troisième et quatrième migrations dans `schema_history` ; les options sont décrites par l'empreinte à partir d'elle (§ 7.2, § 7.3). Aucune migration SQLite correspondante (la transaction de lecture SQLite n'a besoin d'aucune option) |
| SQL Server | `src/Topogenia.Persistence.SqlServer/Migrations/20261023102021_ReportMapDepthBound.cs` | Cinquième migration, sans changement du modèle : `bi.process_map` supprimée puis recréée avec la récursion bornée à 50 niveaux (`WHERE t.depth < 50`, borne du domaine, AR-67) ; `Down` rend la définition de `RolesAndReportViews` (§ 10.2) ; remplacée par la septième (101 niveaux, AR-87) |
| SQL Server | `src/Topogenia.Persistence.SqlServer/Migrations/20261023113221_BinaryTextCollation.cs` | Sixième migration (ADR 038) : les 69 colonnes texte en `Latin1_General_100_BIN2` (`ALTER COLUMN` générés par `dotnet ef`) ; avant, écrits à la main d'après le modèle, suppression des 4 clés primaires (`translation`, `project_counter`, `dissolution_object`, `schema_history`), 7 index et 21 CHECK qui nomment une colonne texte, et des statistiques sur colonnes texte (celles d'un administrateur bloquent le changement, erreur 5074) ; après, recréation à l'identique et `sp_refreshview` des 11 vues `bi` ; `Down` inverse. Aucune migration SQLite |
| SQL Server | `src/Topogenia.Persistence.SqlServer/Migrations/20261023113557_ReportMapDepthOf101.cs` | Septième migration, sans changement du modèle : `bi.process_map` recréée avec la récursion bornée à 101 niveaux (`WHERE t.depth < 101`, AR-87) ; `Down` rend la borne 50 de `ReportMapDepthBound` |
| SQL Server | `src/Topogenia.Persistence.SqlServer/Migrations/20261023124304_ApplicationViewDefinition.cs` | Huitième migration, sans changement du modèle : `GRANT VIEW DEFINITION TO topogenia_app` sur la base (révision de l'ADR 032) ; `Down` : `REVOKE` |
| SQL Server | `src/Topogenia.Persistence.SqlServer/Migrations/20261023174530_TextGuardsAndIntegerBounds.cs` | Neuvième migration (ADR 038, complément du round 2), générée par `dotnet ef` : 69 gardes de texte `CK_<table>_<colonne>_text` ajoutées, 19 CHECK d'entier bornés à `int.MaxValue` ; `Down` inverse |
| SQL Server | `src/Topogenia.Persistence.SqlServer/Migrations/20261024200000_ReportViewsClassificationAndValidatedVersion.cs` | Dixième migration, sans changement du modèle (générée vide, écrite à la main) : `bi.project` gagne `classification` (AR-100), `bi.step_sheet` gagne `validated_version` (AR-101), colonnes ajoutées à la fin ; vues supprimées puis recréées ; `Down` rend les vues de `RolesAndReportViews` et retire sa ligne d'historique. Aucune migration SQLite (pas de vues en SQLite) |
| SQLite | `src/Topogenia.Persistence.Sqlite/Migrations/20261023174259_TextGuardsAndIntegerBounds.cs` | Deuxième migration, `Up` générée : mêmes gardes ; SQLite reconstruit chaque table d'après le modèle cible (`.Designer.cs`), dans la seule transaction de la migration (`AtomicRebuildSqlGenerator`, porte P3 round 3, Données B2 : clés étrangères actives, contrôle reporté à la validation) ; `Down` écrit à la main : chaque table recréée par les instructions de la migration initiale (`SqliteTableRebuild.BackTo`), ligne d'historique retirée (§ 7.1, retour en arrière) |
| SQLite | `src/Topogenia.Persistence.Sqlite/Migrations/20261023174605_TextGuardsCheck.cs` | Troisième migration, écrite à la main : vérifie dans une transaction qu'aucune clé étrangère n'est violée ; elle inscrivait aussi la deuxième, exécutée hors transaction jusqu'au round 3 ; chacune s'inscrit désormais elle-même ; `Down` : ligne d'historique retirée |
| SQL Server | `src/Topogenia.Persistence.SqlServer/Migrations/20261028132453_TranslationGlossary.cs` | Onzième migration (ADR 065), générée par `dotnet ef` : colonne `project.translation_glossary` (`nvarchar(4000)`) et sa garde de texte ; `Down` : colonne retirée, ligne d'historique retirée |
| SQLite | `src/Topogenia.Persistence.Sqlite/Migrations/20261028132425_TranslationGlossary.cs` | Quatrième migration (ADR 065), la même colonne et sa garde ; `Down` : garde et colonne retirées, ligne d'historique retirée |
| SQL Server | `src/Topogenia.Persistence.SqlServer/Migrations/20261029220635_AssistantSettings.cs` | Douzième migration (3.0, ADR 077), générée par `dotnet ef` : la table `assistant_settings` (§ 6.25), ses CHECK et sa clé étrangère vers `app_user` ; `Down` : table supprimée, ligne d'historique retirée |
| SQLite | `src/Topogenia.Persistence.Sqlite/Migrations/20261029220606_AssistantSettings.cs` | Cinquième migration (3.0, ADR 077), la même table ; `Down` : table supprimée, ligne d'historique retirée |
| SQL Server | `src/Topogenia.Persistence.SqlServer/Migrations/20261031001313_ValueStream.cs` | Treizième migration (3.0, ADR 085), générée par `dotnet ef` : les colonnes des saisies du flux de valeur (§ 6.2, 6.7, 6.8, 6.9, 6.11, 6.13), le temps de travail du projet encore facultatif, les CHECK ; `Down` : colonnes et CHECK retirés, ligne d'historique retirée |
| SQL Server | `src/Topogenia.Persistence.SqlServer/Migrations/20261031001327_ValueStreamWorkTime.cs` | Quatorzième migration (3.0, ADR 085), complétée à la main : les projets existants prennent 7 h et 151,67 h, puis les deux colonnes deviennent obligatoires, sans valeur par défaut en base (§ 2) ; `Down` : colonnes de nouveau facultatives, ligne d'historique retirée |
| SQL Server | `src/Topogenia.Persistence.SqlServer/Migrations/20261031001333_ValueStreamReportViews.cs` | Quinzième migration (3.0, ADR 085), écrite à la main : les vues `bi` gagnent les saisies en fin de vue (§ 10.3) ; `Down` : les vues d'avant, texte pour texte |
| SQLite | `src/Topogenia.Persistence.Sqlite/Migrations/20261031001307_ValueStream.cs` | Sixième migration (3.0, ADR 085), les mêmes colonnes et CHECK (tables reconstruites, § 7.1) ; `Down` : tables d'avant, ligne d'historique retirée |
| SQLite | `src/Topogenia.Persistence.Sqlite/Migrations/20261031001321_ValueStreamWorkTime.cs` | Septième migration (3.0, ADR 085), comme celle de SQL Server : valeurs par défaut écrites dans les projets existants, puis colonnes obligatoires |

SQL Server a donc douze migrations, SQLite cinq.

Les migrations sont générées par l'outil `dotnet-ef` 10.0.13, outil local au dépôt
(`.config/dotnet-tools.json`), puis relues ; elles ne sont plus jamais modifiées une fois publiées
(une correction est une nouvelle migration). `.editorconfig` les analyse comme du code généré
(`generated_code = true`). Une migration avec du SQL écrit à la main (`migrationBuilder.Sql`) suit les règles de
forme (en-tête de deux lignes, commentaires en anglais) ; les `*.Designer.cs`, l'instantané et une migration écrite
en entier par l'outil en sont exclus (ADR 037).
Chaque migration doit correspondre au modèle : `HasPendingModelChanges` est faux (test
`The_migrations_of_each_provider_match_the_model_and_embed_their_source`), et EF Core refuse d'appliquer
des migrations en retard sur le modèle.

**Retour en arrière** (`Down`, porte P3 round 2, Données M2 ; round 3, Données M2 et m5). L'application ne descend jamais une migration ; les outils
d'EF Core le font (`dotnet ef database update 0`, `IMigrator.MigrateAsync("0")`). Le `Down` généré de la migration
initiale SQLite commençait par 20 `DropForeignKey`, que le fournisseur SQLite refuse (`NotSupportedException`) : l'échec
survenait après la prise du verrou des migrations d'EF Core, dont la ligne de `__EFMigrationsLock` restait ; chaque
contrôle suivant attendait alors le délai des migrations (10 minutes) avant d'être refusé (`BASE_OCCUPEE`, puis
`VERROU_MIGRATIONS`, ADR 041). Le `Down` SQLite (seule méthode modifiée de la migration, qui n'a jamais été publiée)
supprime les 24 tables sans `DropForeignKey`, chacune après les tables qui la référencent (`step`, `lane`, `process_map`,
`deletion_batch`, `project`, `app_user` en dernier) ; il reste écrit avec les seules opérations d'EF Core, sans SQL à la
main (la migration initiale reste un fichier généré, ADR 037). Avec `foreign_keys = ON`, SQLite vide une table avant de
la supprimer et contrôle les références : la seule boucle qui reste est celle d'une carte détaillée
(`process_map.parent_step_id` vers l'étape détaillée, qui est sur une carte) ; un fichier qui en contient une est refusé
(`SQLITE_CONSTRAINT` 19), la transaction de la migration initiale est annulée (ses tables et ses lignes restent), le
verrou est rendu ; les descentes des migrations suivantes, chacune dans sa propre transaction, ont pu passer avant, et
`schema_history` (que seul le `Down` initial supprime) les garde : le démarrage refuse alors le fichier par son code,
sans attendre. La descente complète d'un fichier avec des cartes détaillées n'est pas offerte : vider ces données avant demanderait
du SQL écrit à la main dans la migration initiale (`PRAGMA defer_foreign_keys`, essayé et vérifié, écarté pour cette
raison) ; la restauration d'une sauvegarde d'avant migration (ADR 029) est le retour arrière du mode Poste. Ce changement
change l'empreinte de la source de la migration (§ 7.2) : un fichier migré avec l'ancienne source serait refusé
`MIGRATION_MODIFIEE` ; aucun fichier hors des tests n'existe avant la publication de la 2.0. Le `Down` SQL Server, qui
supprime les clés étrangères puis les tables, descend données comprises et laisse le schéma `topogenia` vide (EF Core ne
défait pas `EnsureSchema`), recréé tel quel à la remontée (`MigrationRollbackTests`) ; le script de chaque migration,
montée et descente, est généré par son fournisseur, ce qui tient les migrations ajoutées plus tard à la même
règle. Un échec pendant l'exécution d'une migration (une instruction refusée par le moteur) ne laisse pas le verrou :
EF Core retire la ligne, vérifié sur les deux moteurs ; un échec de génération d'une montée est levé par le contrôle
(`MigrationTransactions.OutsideTransaction` génère toutes les montées sous le verrou de `SchemaMigrations`, rendu à la
sortie), avant le verrou d'EF Core.

Révision du round 3 (Données M2) : EF Core retire sa propre ligne avec chaque `Down`, jamais celle de `schema_history` ;
après la descente d'une seule migration, le contrôle lisait la ligne restée comme un historique changé à la main et
refusait la base pour toujours (`SCHEMA_MODIFIE`), sur les deux moteurs. Le `Down` de chaque migration écrite depuis
retire sa ligne (`DeleteData` d'EF Core sur `migration_id`, dans la transaction du `Down`) : SQLite `TextGuardsCheck` et
`TextGuardsAndIntegerBounds`, SQL Server `TextGuardsAndIntegerBounds` ; leur empreinte change (aucune n'a été publiée).
Le `Down` SQLite de `TextGuardsAndIntegerBounds` recrée chaque table par les instructions de la migration initiale
(`SqliteTableRebuild.BackTo`) : celui de l'outil la recréait d'après le modèle, colonnes dans un autre ordre, et
l'empreinte du schéma n'était plus celle de la migration initiale. Une base descendue d'une migration, ou jusqu'à la
migration d'avant les gardes de texte, est donc « en retard » et remonte (`MigrationDescentTests`, deux moteurs). Le
fichier SQLite dont la descente complète est refusée par une carte détaillée garde sa migration initiale, les deux
suivantes descendues et retirées de l'historique : le démarrage le lit aussitôt « en retard » (migration initiale
appliquée, deux en attente) et le remigre, données intactes (`MigrationRollbackTests`, Fidélité m5). Les huit migrations
SQL Server antérieures ne retirent pas leur ligne : descendre en dessous de `ApplicationViewDefinition` reste refusé
`SCHEMA_MODIFIE` (le retour arrière du mode Serveur est la restauration par le plan de SQL Server). Règle pour toute
migration à venir : son `Down` retire sa ligne d'historique (ADR 027, révision du round 3).

### 7.2 Les deux historiques

- `__EFMigrationsHistory` (EF Core) : `MigrationId`, `ProductVersion`, écrit par EF Core dans la
  transaction de chaque migration.
- `schema_history` (Topogenia) : `migration_id`, `migration_fingerprint` (SHA-256 de la source de la
  migration, fins de ligne normalisées en LF, comme les scripts 1.x), `schema_fingerprint` (SHA-256 de
  la description du schéma lue dans le catalogue juste après la dernière migration de la
  transaction), `application_version`, `applied_at`.

La source de chaque migration (`Migrations/<identifiant>.cs`, hors `Designer` et instantané) est
embarquée dans l'assemblage de son fournisseur (`EmbeddedResource`) ; `MigrationSources` en calcule
l'empreinte. La description du schéma est faite par le catalogue de chaque fournisseur :

- SQL Server : tables, colonnes (type, longueur, précision, échelle, nullabilité, collation),
  index (unicité, filtre, colonnes et ordre), clés étrangères (colonnes, actions), CHECK, valeurs par
  défaut et vues (définition complète) des schémas `topogenia` et `bi`, lus dans `sys.*` ; tout autre objet de la base
  que Microsoft ne livre pas, quel que soit son schéma (`X|schéma.objet|type` : fonction, synonyme, déclencheur,
  procédure, séquence, table ou vue hors des deux schémas...), les déclencheurs de base (`TR`) et les types définis
  (`Y`) : une base des migrations n'en a aucun (D04, ADR 037) ; un objet marqué livré par Microsoft compte hors des
  schémas `sys` et `INFORMATION_SCHEMA` (files `SQ` de `dbo` à part) ; seule la table `topogenia.__EFMigrationsHistory`
  est écartée, à la casse exacte (une vue ou une table de ce nom ailleurs est décrite) ; le propriétaire posé sur un
  objet (`W|schéma.objet|numéro du principal`) ; les options `TRUSTWORTHY` et `DB_CHAINING` quand elles sont posées
  (`B`) ; et la couche d'accès (ADR 032, 037) : chaque schéma hors système et son propriétaire (`S`), les deux rôles et leur propriétaire (`R`), chaque droit de la base, à qui que ce soit, sauf
  le droit de connexion d'un utilisateur (celui de `guest` est décrit) et les droits sur les objets système (`P` :
  classe, objet, schéma, principal ou autre cible visés, droit, `GRANT`/`DENY`, bénéficiaire), les rôles dont les
  deux rôles sont membres (`M`) et le nombre de membres de `db_datareader`, `db_datawriter`, `db_ddladmin`,
  `db_securityadmin` et `db_backupoperator` (`MF`). Les membres des deux rôles et de `db_owner` (comptes de chaque
  installation, administrateurs) n'en font pas partie. Les options de base des lectures (ADR 031) : `O|<état de
  ALLOW_SNAPSHOT_ISOLATION>|<READ_COMMITTED_SNAPSHOT>` (`O|ON|True`), lu dans `sys.databases` pour la base
  courante, décrit dès que la migration `20261023093315_SnapshotIsolationCheck` figure dans `__EFMigrationsHistory` :
  une option retirée à la main fait refuser la base au démarrage (`SCHEMA_MODIFIE`) au lieu de faire échouer
  chaque lecture (erreur 3952), et l'empreinte d'une base antérieure à cette migration ne dépend pas de ses
  options. Depuis la migration `ApplicationViewDefinition` (`VIEW DEFINITION` sur la base), le compte de
  l'application lit la même description qu'un administrateur : les déclencheurs de base, les objets hors des deux
  schémas sans droit visible et les droits donnés à un autre utilisateur sont refusés dès le contrôle du démarrage,
  et non plus seulement à `--migrate` (ADR 032, révision ; ADR 037) ;
- SQLite : l'instruction `sql` de chaque table, index, vue et déclencheur de `sqlite_master`, hors noms internes
  (`sqlite_` suivi d'autre chose, le `_` échappé dans le `LIKE`) et hors des deux tables d'EF Core, mais pas ce qui
  s'y attache.

Les deux tables d'EF Core sont hors de la description : leur forme appartient à EF Core ; un déclencheur, un index
ou une contrainte qu'une personne y ajoute est décrit (ADR 037), sauf une contrainte de table en SQL Server.

### 7.3 Écriture de l'historique : dans la transaction de la migration

`SchemaMigrations.MigrateAsync` applique les migrations par `Database.MigrateAsync` d'EF Core, qui
crée la base au besoin et prend son propre verrou (deux processus ne migrent jamais en même temps,
A-4-35 : `sp_getapplock` en SQL Server, table `__EFMigrationsLock` en SQLite). Un intercepteur de
transaction (`SchemaSealInterceptor`), présent sur le seul contexte qui migre, écrit au moment où EF
Core valide une transaction de migration la ligne `schema_history` de chaque migration que
`__EFMigrationsHistory` liste sans ligne correspondante, avec l'empreinte du schéma sur la dernière :
la migration, sa ligne d'EF Core et sa ligne d'historique sont écrites ensemble ou pas du tout
(A-4-34), comme la 1.x écrivait `schema_version` avec son script.

Délai des migrations (ADR 041) : le contrôle et la migration ouvrent leurs contextes par
`DatabaseEngine.CreateMigrationContext`. Chaque commande de ces contextes (instructions des migrations, verrou
`sp_getapplock` ou `__EFMigrationsLock`, catalogue, `BEGIN` des transactions SQLite) a le délai
`DatabaseEngine.MigrationTimeout`, réglage du moteur passé par l'hôte (`--migrate`, P4-T09) : 10 minutes par défaut,
de 1 s à 1 jour, en secondes entières. Les autres contextes gardent le délai ordinaire (chaîne de connexion en SQL
Server, 1 s en SQLite, ADR 029). L'attente du verrou dans le contrôle est bornée par ce délai sur les deux moteurs
(EF Core attend sans fin sur SQLite tant que la ligne de `__EFMigrationsLock` existe, même laissée par une migration
arrêtée) : au-delà, `BASE_OCCUPEE` en SQL Server, `VERROU_MIGRATIONS` (non passager) en SQLite (ADR 041, révision).

Aucune migration SQLite ne s'exécute hors transaction (porte P3 round 3, Données B2) : le fournisseur génère toute
reconstruction de table dans la transaction de sa migration (`AtomicRebuildSqlGenerator`) ; un arrêt à n'importe quelle
instruction ou validation laisse le fichier tel que la migration précédente l'a laissé, et le démarrage suivant le migre
(`AtomicMigrationTests`, qui l'arrête avant chacune). Seule `SnapshotIsolation`, en SQL Server, suit la règle qui suit.

Migration avec une instruction hors transaction (`suppressTransaction`, ADR 031, ADR 027 révisée) : EF Core 10
exécute alors aussi sa ligne de `__EFMigrationsHistory` hors de toute transaction ; l'intercepteur ne voit aucune
validation pour elle. `MigrationTransactions.OutsideTransaction` la désigne (même règle qu'EF Core, sur les
commandes générées) ; elle est toujours suivie d'une migration en transaction (`SnapshotIsolationCheck`), dont la
validation l'inscrit, et ses effets n'entrent dans la description du schéma qu'avec cette migration suivante.
Reprise sans intervention :

- arrêt avant l'enregistrement par EF Core : rien dans les historiques, base en retard, la migration se rejoue
  (instructions rejouables) ;
- arrêt après cet enregistrement, avant la migration suivante : `SchemaVerification` accepte cette seule forme
  incomplète (dernière migration appliquée, hors transaction, sans ligne ; celle d'avant inscrite ; la suivante
  connue, en transaction, en attente) et rend « en retard », le schéma étant comparé à l'empreinte de la
  migration d'avant ; `MigrateAsync` l'inscrit sous le verrou des migrations, à la première transaction validée
  (création de l'historique d'EF Core ou migration suivante), puis applique et inscrit la suivante.

Restent refusées (`EMPREINTE_ABSENTE`) une migration en transaction sans ligne, et une migration hors transaction
sans ligne qui n'est pas la dernière appliquée ; un schéma changé pendant l'arrêt reste `SCHEMA_MODIFIE`.

### 7.4 Contrôle avant tout usage de la base

`SchemaMigrations.CheckAsync` lit, sous le verrou des migrations (une migration en cours ailleurs est
vue entière ou pas du tout ; le contrôle attend sa fin jusqu'au délai des migrations, § 7.3), les migrations appliquées, l'historique et le catalogue, puis
`SchemaVerification` refuse, dans l'ordre de la 1.x (`MiseAJourBase.Verifier`) :

| Code (`SchemaCodes`) | Cas |
|---|---|
| `BASE_PLUS_RECENTE` | Une migration appliquée (dans l'un ou l'autre historique) est inconnue de l'application : la base est plus récente qu'elle |
| `MIGRATION_MODIFIEE` | L'empreinte de la source d'une migration appliquée n'est plus celle enregistrée |
| `EMPREINTE_ABSENTE` | Une migration appliquée n'a pas de ligne d'historique (appliquée hors de l'application, par exemple par `dotnet ef database update`), ou la dernière n'a pas l'empreinte de son schéma |
| `SCHEMA_MODIFIE` | Le schéma lu n'est plus celui que les migrations ont laissé (colonne, index, contrainte changés à la main), ou une ligne d'historique n'a plus sa migration dans `__EFMigrationsHistory` |
| `DROITS_INSUFFISANTS` | Le compte ne peut pas lire les historiques des migrations ou les données (`SchemaCatalog.CheckAccessAsync`, avant toute lecture des historiques : en SQL Server, `SELECT TOP (0)` refusé par 229), ou une erreur de droits du moteur (SQL Server 229, 262, 1802 seul, ou 18456 sans 4060 : connexion refusée par l'instance, la 1.x disait « SQL Server refuse la connexion de ce compte » ; SQLite `SQLITE_PERM` 3, `SQLITE_READONLY` 8 : fichier que le compte ne peut pas écrire) pendant le contrôle ou la migration (ADR 036) |
| `BASE_INACCESSIBLE` | La base ne s'ouvre pas pour le compte (SQL Server 4060, que 18456 accompagne ; SQLite `SQLITE_CANTOPEN` 14, `SQLITE_NOTADB` 26 : fichier qui ne s'ouvre pas ou n'est pas une base) pendant le contrôle ou la migration ; en SQL Server, aussi une base absente au contrôle, qu'EF Core ne distingue pas d'une base que le compte ne peut pas ouvrir (ADR 036, révision du round 2) ; en SQLite, aussi 26 avec un code étendu pour un fichier séparé de son journal : 26/26 en-tête en journal de retour arrière à côté d'un journal, 26/282 journal d'un autre fichier, 26/538 journal sans page 1, 26/794 fichier abîmé à la première ouverture, 26/1050 fichier remplacé en marche (ADR 029, révision du round 3) |
| `VERROU_MIGRATIONS` | Mode Poste : le verrou des migrations (ligne de `__EFMigrationsLock`) n'a pas été obtenu dans le délai des migrations, la même ligne est restée pendant tout le délai et personne n'écrit le fichier (`BEGIN IMMEDIATE` accepté) ; laissé par une migration arrêtée, il ne disparaît pas seul : non passager. Une ligne reprise pendant l'attente ou un fichier en cours d'écriture (porteur vivant) donne `BASE_OCCUPEE` (ADR 041, révisions) |
| `BASE_OCCUPEE` | Seul code passager (`SchemaRefusedException.IsTransient`) : une contention du moteur (SQL Server 1807 avec 1802, 1205, 1222, 5061, délai, connexion rompue... ; SQLite `SQLITE_BUSY` 5, `SQLITE_LOCKED` 6, codes étendus compris : fichier tenu par une autre connexion) a arrêté le contrôle ou la migration, ou le verrou des migrations n'a pas été obtenu dans le délai des migrations, en SQL Server toujours, en SQLite quand son porteur est vivant (ADR 041) ; elle se rejoue telle quelle (ADR 036, révision). Le refus du délai garde une `TimeoutException` qui ne donne que le délai, sans l'échec du moteur |

Une base sans migration appliquée (nouvelle, ou fichier absent) est « en retard » avec toutes les
migrations à appliquer ; le contrôle ne la crée pas. En SQL Server, une base qu'EF Core ne peut pas ouvrir (4060) est
vue absente et le contrôle la refuse `BASE_INACCESSIBLE` (ADR 036, révision du round 2) ; `MigrateAsync` la crée. Une
base vide reste « en retard » sous un compte sans droit (aucune table à lire). Un refus traduit garde en exception
interne une `EngineFailureException` (numéros du moteur, sans message). `SchemaCodes.DatabaseBehind`
(`BASE_EN_RETARD`) est le code que l'hôte rend en mode Serveur pour une base en retard (refus de
démarrer, `Topogenia.exe --migrate`, spec 4.4) ; en mode Poste, l'hôte sauvegarde le fichier puis migre
(`DesktopDatabaseStartup`, § 7.6 ; l'appel par l'hôte relève de P4-T05). `MigrateAsync` refuse les mêmes bases que
`CheckAsync` avant de rien appliquer, et contrôle de nouveau après. Les échecs du moteur sont lus par
`SchemaCatalog.RefusalOf` de chaque fournisseur (premier `SqlException` ou `SqliteException` de la chaîne) ; un code
qui n'est dans aucune ligne du tableau (SQLite : fichier corrompu, disque plein, erreur d'entrée-sortie) reste l'échec du
moteur, journalisé par l'hôte (ADR 036, révisions).

### 7.5 Générer une migration

La migration est générée avec l'outil local du dépôt, sans ajouter le paquet
`Microsoft.EntityFrameworkCore.Design` aux projets du dépôt (il tire Roslyn et des paquets qui
entrent en conflit avec la version de `Microsoft.CodeAnalysis.CSharp` figée pour les tests
d'architecture) :

1. `dotnet tool restore` à la racine du dépôt (`dotnet-ef` 10.0.13, outil local) ;
2. hors du dépôt (dossier temporaire), un projet de démarrage jetable qui référence le projet du
   fournisseur et `Microsoft.EntityFrameworkCore.Design` 10.0.13, avec une fabrique
   `IDesignTimeDbContextFactory<AppDbContext>` qui rend `new SqlServerEngine(...).CreateContext()`
   (ou `SqliteEngine`) ; aucune connexion n'est ouverte ;
3. `dotnet ef migrations add <Nom> --project src/Topogenia.Persistence.<Fournisseur>
   --startup-project <projet jetable> --output-dir Migrations --context AppDbContext` (sans
   `--namespace`, qui déplacerait l'instantané du modèle) ;
4. relire la migration, la normaliser en UTF-8 sans BOM et LF, l'ajouter aux tests ;
5. faire de même pour l'autre fournisseur.

### 7.6 Réglages et sauvegarde SQLite (ADR 029)

Chaque connexion ouverte par un contexte reçoit `busy_timeout = 5000`, `foreign_keys = ON`,
`synchronous = NORMAL` et `journal_mode = WAL` (vérifié) par `SqliteSettingsInterceptor`, sauf le mode de journal sur une
connexion ouverte en lecture seule (`Mode=ReadOnly`, celle par laquelle EF Core regarde si le fichier existe) : elle ne
peut pas le changer, et une sauvegarde restaurée (journal `DELETE`) passe en `WAL` à la première connexion qui peut
écrire (ADR 036, révision du mode Poste) ; le SQL est dans
`Connection/SqlitePragmas.cs`. Le délai de commande du fournisseur est fixé à 1 s
(`SqliteConnectionSettings.CommandTimeoutSeconds`, option `CommandTimeout` d'EF Core et `DefaultTimeout` posé par
l'intercepteur) : une instruction attend un verrou au plus `MaximumBusyWait` = 6,15 s, une unité de travail environ
15 s (18,6 s au pire) avant `SQLITE_BUSY` (ADR 029). L'intercepteur remplace aussi la collation `EF_DECIMAL` d'EF
Core par une collation en culture invariante (`SqliteDecimalCollation`). Les tests relisent les réglages par la sonde
interne `SqliteSettingsProbe` (ADR 037). Le dossier des sauvegardes suit la règle du fichier de données
(`SAUVEGARDE_DOSSIER_REFUSE`) ; les fichiers d'avant migration sont gardés au plus deux, 30 jours au plus ; un échec
ne porte que des codes, l'exception d'origine est dans `BackupResult.Cause`. `SqliteBackupService` copie le fichier par l'API de sauvegarde en ligne vers
`<dossier>/<fichier>_<n>_<jour>.db` (quotidienne, jour UTC, sautée si celle du jour existe) ou
`<fichier>_before_<dernière migration>.db` ; la copie est contrôlée (`integrity_check`,
`foreign_key_check`) dans un fichier `.partial`, passée en journal `DELETE`, puis remplace l'ancienne.
`DesktopDatabaseStartup` : contrôle, sauvegarde avant migration si la base est en retard et non vide (échec :
`MigrationBackupFailedException`, pas de migration), migration, quotidienne (échec non bloquant).
La restauration passe par `SqliteBackupService.RestoreAsync`, application arrêtée (copie contrôlée `.restoring`, journal
supprimé avec le fichier remplacé, codes `RESTAURATION_*`). Un fichier en journal `DELETE` posé à côté d'un `-wal` non
vide est refusé à l'ouverture (`SqliteJournalCheck`, 26, `BASE_INACCESSIBLE`) : le journal périmé d'un autre fichier
n'est jamais rejoué (ADR 029, révision du round 2).

Fichier et journal liés par un tampon (ADR 029, révision du round 3, Données B1 et M3) : le fichier n'est réécrit depuis
son journal que par le moteur (`wal_autocheckpoint = 0`, `journal_size_limit = 0`, `SQLITE_DBCONFIG_NO_CKPT_ON_CLOSE`),
chaque fois sous un nouveau tampon (`user_version`) écrit avant, puis de nouveau comme première trame du journal
suivant (`SqliteDataFileGuard`). À la première ouverture du fichier par le processus : un journal à trames validées doit
contenir une page 1 au tampon du fichier (sinon 26/282, ou 26/538 sans page 1), puis `integrity_check` et
`foreign_key_check` avant toute écriture (26/794), puis un point de contrôle complet. En marche : point de contrôle à
la fermeture d'une connexion au-delà de 4 Mio de journal, après le même test ; un fichier remplacé n'est jamais
réécrit (26/1050 dans le processus). Arrêt propre : `SqliteEngine.CloseAsync` (P4-T05), plus aucun journal. Tous ces
refus sont `BASE_INACCESSIBLE`. La restauration copie la sauvegarde par l'API de sauvegarde en ligne (journal de la
sauvegarde compris), refuse le fichier de données lui-même par son identité sur le disque et un chemin d'une forme non
prise en charge (`RESTAURATION_CHEMIN_NON_PRIS_EN_CHARGE`), et ne laisse aucun fichier partiel.

## 8. Écarts décidés par rapport au schéma 1.x (ADR 028)

| Écart | 1.x | 2.0 | Raison |
|---|---|---|---|
| Tables `language` et `app_setting` | Référentiel des langues, paramètres en base | Supprimées : langues (`LanguageSettings`, AR-29) et réglages (`AppSettings`, rétention) viennent de la configuration de l'hôte ; colonnes de langue sans clé étrangère, `varchar(10)` au lieu de `char(2)` | NF12 : ajouter une langue = un fichier et une ligne de configuration |
| `schema_version` | Numéro et empreinte de script | `__EFMigrationsHistory` + `schema_history` (§ 7) | Migrations EF Core (spec 4.4) |
| `rowversion` | `row_version` | `version int` (D06), sur plus de tables (membre, rôle RACI, usage, traduction, remontée, profil) | Portabilité, AR-07, AR-70, AR-86 |
| Dates | `datetime2(3)` | `datetime2(7)` | Un enregistrement relu doit être égal à l'enregistrement écrit (AR-81) ; une troncature à la milliseconde ferait refuser une annulation légitime |
| Valeurs par défaut | `DF_*`, `NEWSEQUENTIALID()`, `SYSUTCDATETIME()` | Aucune | L'application écrit chaque valeur (D07, D08) |
| Déclencheurs | `TR_flow_same_map`, `TR_step_flow_same_map`, `TR_step_raci_lane` | Aucun | Règles dans le domaine (spec 4.1, D04) |
| CHECK de format | `LIKE '[A-Z]...'`, `ISJSON` | Retirés | Objets valeur du domaine (A-5-25, ADR 026) |
| Collation du login | `Latin1_General_100_CI_AS` | `login_normalized` unique, en collation binaire comme toute colonne texte (ADR 038) | Aucune comparaison sans casse dans la base (spec 4.3-3) |
| `INCLUDE` des index | `UX_flow_...`, `IX_flow_target_step` | Retirés | Propres à SQL Server (annexe A 5) |
| Index filtrés non uniques | `IX_* WHERE deleted_at IS NULL` | Index simples | Seuls les index uniques portent une règle ; volumes faibles (5 000 étapes par projet) |
| Petits entiers | `smallint` (`position`), `tinyint` (`percent_complete_accurate`) | `int` | Types du domaine, aucun convertisseur (ADR 026) ; bornes par CHECK |
| Nouvelles colonnes | - | `login_normalized`, `last_project_id`, `classification`, `sharing_group_label`, `structure_version`, `deletion_batch.operation_id`, `step.origin_operation_id`, `step_sheet_version.frequency` et `duration_minutes`, `updated_*` de `step_raci` et `dissolution` | Spec 4.1, 4.6, 4.7, AR-49, AR-64 |
| Nouvelles tables | - | `operation_log`, `audit_event`, `schema_history` | Spec 4.6, 5.5, 4.4 |
| Compteurs | `step`, `lane`, `pain_point` | plus `solution` | Annexe A 6-9 |
| Clés étrangères | `audit`, `last_project_id`... (n'existaient pas) | Colonnes d'identifiant sans clé étrangère (§ 5.4) | Purge, journal taillé |
| Décimaux SQLite | - | Texte de la valeur arrondie à l'échelle de la colonne (`ScaledDecimalConverter`), CHECK par `CAST(... AS REAL)` | Une comparaison de texte refuserait 12,5 face à 100 000 ; même valeur que SQL Server (ADR 039) |
| Nom du contexte | - | `AppDbContext` au lieu de `TopogeniaDbContext` (plan) | D13 : le nom du produit n'est écrit que dans sa constante |
| Rôles | `ariane_app`, `ariane_bi` | `topogenia_app`, `topogenia_bi` (ADR 013) ; `DENY` aussi sur `__EFMigrationsHistory` ; `DENY UPDATE` (et non plus `DELETE`) sur `step_sheet_version`, plus `audit_event` ; `VIEW DEFINITION` pour le contrôle du démarrage ; plus de `GRANT EXECUTE` (aucune procédure) | Purge en C# sous le compte de l'application ; contrôle du schéma au démarrage (§ 10.1, ADR 032) |
| Vues `bi` | SQL Server | SQL Server seulement, aucune en SQLite ; mêmes 11 vues et colonnes, types de la 2.0 (§ 10.2) | D04, ADR 032 |
| Options de base des lectures | `db/003` : `READ_COMMITTED_SNAPSHOT` et `ALLOW_SNAPSHOT_ISOLATION`, script sans transaction inscrit hors transaction dans `schema_version` | Mêmes options et mêmes conditions, SQL Server seulement ; deux migrations (`SnapshotIsolation` hors transaction, `SnapshotIsolationCheck` en transaction, qui l'inscrit) ; état des options dans l'empreinte (§ 7.2, § 7.3) | Lecture en instantané (spec 3.4) ; historique écrit en transaction (ADR 027, ADR 031) |

## 9. Points d'extension pour les tâches suivantes

- **P3-T03** (fait) : voir § 7.6. Pour P4-T05 : construire `SqliteBackupService(engine, dossier, clock)` (dossier
  `%LOCALAPPDATA%\Topogenia\backups`), appeler `DesktopDatabaseStartup.RunAsync` avant de servir,
  arrêter avec un message clair sur `MigrationBackupFailedException`, journaliser `Daily.Failure`.
- **P3-T04 et P3-T05** (faits) : ADR 030 (écriture du `ChangeSet`, unité de travail) et ADR 031 (lecture
  cohérente). Le stationnement admis par `CK_step_column_index` sert aussi aux étapes qui changent de couloir
  ou de carte ; une erreur de garde est classée par le nom de la contrainte de ce document.
- **P3-T06** (fait) : rôles, schéma `bi` et vues, § 10 et ADR 032.
- **Phase 4** (hôte, mode Serveur) : le contrôle du démarrage tourne sous le compte de l'application,
  membre de `topogenia_app` ; il lit les historiques, prend le verrou `sp_getapplock` d'EF Core (ouvert à
  tous) et décrit le catalogue grâce à `VIEW DEFINITION` (§ 10.1). `Topogenia.exe --migrate` tourne sous
  un compte distinct, propriétaire de la base (`db_owner`).
- **Purge (P3-T07, ADR 034)** : `TrashPurge` sous `topogenia_app` efface les versions de fiche, l'historique des
  statuts et les entrées du journal liées, jamais l'audit ; elle n'envoie que des `DELETE`. Pour P4-T03 :
  `PurgePlanner` puis `ITrashPurge.PurgeAsync`.
- **Mode Poste (P4-T05)** : `DesktopDataFile.DefaultPathIn(<dossier du produit>)`, appelé par `DesktopPaths.Of`, et
  `DesktopDataFile.Check(chemin, DataFileEnvironment.Current())` (chemin résolu : `..`, noms courts, liens) avant
  `DesktopDatabaseStartup` ; `SqliteBackupService(engine, dossier, horloge, DataFileEnvironment.Current())`.
- **Démarrage (P4-T06, ADR 036)** : `CheckAsync` rend `DROITS_INSUFFISANTS`, `BASE_INACCESSIBLE` ou `BASE_OCCUPEE` (passager, à rejouer) (avec
  l'erreur du moteur en exception interne) ; le message clair et l'arrêt appartiennent à l'hôte.
- **Créations par clé naturelle (ADR 033)** : `app_user` (`UQ_app_user_login_normalized`), `translation`
  (`PK_translation`) et `document_use` (`PK_document_use`) remplacent les `MERGE ... HOLDLOCK` de la 1.x. Le
  domaine crée ou met à jour ; une création perdue face à une unité concurrente fait rejouer toute l'unité, qui
  met à jour la ligne trouvée. Aucune indication de verrou.

## 10. Rôles et vues `bi` du mode Serveur (ADR 032)

Reprise de `db/004_acces.sql` de la 1.x, avec les vues telles que `db/006_corbeille.sql` les a
modifiées (projets à la corbeille exclus). Migration SQL Server `20261023083307_RolesAndReportViews`, puis
`ReportMapDepthBound` et `ReportMapDepthOf101` (`bi.process_map`) et `ApplicationViewDefinition` (droit de
`topogenia_app`), § 7.1 ;
rien en SQLite (D04 : vues propres au mode Serveur ; la 1.x n'avait pas de mode fichier). Les noms sont
écrits tels quels dans la migration et dérivés de `TechnicalNames.Schema` dans le code
(`SqlServerAccess.ApplicationRole`, `SqlServerAccess.ReportRole`, ADR 013). Les deux schémas et les deux
rôles appartiennent à `dbo` : les vues lisent les tables par chaînage de propriété, sans droit des
rapports sur les tables.

### 10.1 Rôles

| Rôle | Droits | 1.x |
|---|---|---|
| `topogenia_app` (compte du pool IIS) | `SELECT, INSERT, UPDATE, DELETE` sur le schéma `topogenia` ; `DENY INSERT, UPDATE, DELETE` sur `schema_history` et `__EFMigrationsHistory` ; `DENY UPDATE` sur `step_sheet_version`, `pain_point_status_history` et `audit_event` ; `VIEW DEFINITION` sur la base (migration `ApplicationViewDefinition`), en plus de celui sur les schémas `topogenia` et `bi` et sur le rôle `topogenia_bi` (`RolesAndReportViews`) ; rien sur les vues `bi` | `ariane_app` : mêmes droits sur `ariane`, `DENY` sur `schema_version`, `DENY UPDATE, DELETE` sur `step_sheet_version`, `DENY UPDATE` sur `pain_point_status_history`, `EXECUTE` sur les procédures de purge |
| `topogenia_bi` (rapports Power BI) | `SELECT` sur le schéma `bi` ; rien d'autre | `ariane_bi` : identique |

Droits effectifs de `topogenia_app`, table par table (vérifiés par le serveur, `RoleTests`) :

| Tables | Lire | Ajouter | Modifier | Effacer |
|---|---|---|---|---|
| Les 20 autres tables de données | oui | oui | oui | oui |
| `step_sheet_version`, `pain_point_status_history`, `audit_event` (historiques de données, ajout seul) | oui | oui | non | oui (purge, conservation de l'audit) |
| `schema_history`, `__EFMigrationsHistory` (historiques des migrations) | oui | non | non | non |
| Vues `bi` | non | - | - | - |

Raisons des écarts (ADR 032) : la purge de la corbeille est écrite en C# sous le compte de l'application
(la procédure 1.x effaçait les versions de fiche par chaînage de propriété), d'où `DELETE` permis sur les
versions ; l'audit, nouveau en 2.0, est un historique en ajout seul ; le contrôle du démarrage décrit le
catalogue (§ 7.4) et SQL Server cache au compte sans `VIEW DEFINITION` la définition des CHECK et des
vues et les droits de l'autre rôle, ce qui ferait refuser une base saine. `VIEW DEFINITION` ne donne
accès à aucune donnée. Sans `VIEW DEFINITION` sur la base, ce compte ne voyait ni les déclencheurs de base, ni les
objets hors des deux schémas sans droit visible, ni les droits donnés à un autre utilisateur : le contrôle du
démarrage les acceptait, seul `--migrate` les refusait (ADR 032, révision ; `Schema/AppAccountCatalogTests`).

Lecture en instantané (ADR 031) : `topogenia_app` ouvre ses transactions `SNAPSHOT` sans aucun droit de plus
(l'option est celle de la base) et voit les options de la base courante dans `sys.databases` (ligne `O` du
catalogue) ; vérifié sous `EXECUTE AS USER` (`ConsistentReadTests.On_sql_server_the_application_role_loads_in_a_snapshot`,
`RoleTests.The_schema_check_run_by_the_application_role_sees_the_schema_the_migrations_recorded`).

Installation (mode Serveur, spec 5.3) : l'administrateur lance `Topogenia.exe --migrate` sous un compte
`db_owner` distinct, crée l'utilisateur du compte du pool (`CREATE USER ... FROM LOGIN`) et l'ajoute à
`topogenia_app`, puis ajoute les comptes des rapports à `topogenia_bi`. Ces membres (des utilisateurs) ne font pas
partie de l'empreinte ; un membre qui n'est pas un utilisateur (rôle de base, rôle d'application) de l'un des deux
rôles, et tout rôle d'application de la base, en font partie (lignes `MN`, `AR`, ADR 032, révision du round 3) : un
rôle d'application membre de `topogenia_app` donne `app_user` à toute session qui connaît son mot de passe
(`sp_setapprole`), un compte de rapport compris. Mettre `topogenia_bi` ou un compte de rapport dans `db_datareader` (ou `db_datawriter`, `db_ddladmin`,
`db_securityadmin`, `db_backupoperator`), donner un droit à la main (à `public` compris), retirer un `DENY` ou
ajouter un objet (fonction, synonyme, déclencheur...) changent l'empreinte et font refuser la base au démarrage
(`SCHEMA_MODIFIE`, ADR 037). Un administrateur ajouté à `db_owner` ne change rien.

### 10.2 Vues

Toutes les vues ne montrent que des objets vivants de projets vivants, sans orphelin. Les « étapes avec
contenu » sont celles de type `task`, `wait` et `decision` (`StepTypes.HasNoContent` est faux).

| Vue | Colonnes | Contenu |
|---|---|---|
| `bi.project` | `project_id`, `code`, `name`, `source_language`, `description`, `created_at`, `classification` | Projets hors corbeille ; `classification` : niveau de confidentialité (`internal`, `confidential`, `strictly_confidential`), ajouté en 2.0 (AR-100, migration `ReportViewsClassificationAndValidatedVersion`) |
| `bi.process_map` | `map_id`, `project_id`, `parent_step_id`, `name`, `depth`, `path` | Cartes vivantes, de la racine (`depth` 1) vers les cartes détaillées par les étapes vivantes, 101 niveaux au plus (tout ce que la 1.x montrait, AR-87, migration `ReportMapDepthOf101`) ; `path` : noms depuis la racine séparés par ` > ` (CTE récursive) |
| `bi.lane` | `lane_id`, `map_id`, `project_id`, `code`, `name`, `lane_type`, `sort_order` | Couloirs vivants des cartes de `bi.process_map` |
| `bi.step` | `step_id`, `map_id`, `project_id`, `lane_id`, `code`, `label`, `step_type`, `column_index`, `row_index`, `automation_status`, `assignee_user_id`, `created_at`, `updated_at` | Étapes vivantes des cartes de `bi.process_map` |
| `bi.flow` | `flow_id`, `map_id`, `project_id`, `source_step_id`, `target_step_id`, `label` | Flèches vivantes dont l'étape source est dans `bi.step` |
| `bi.step_sheet` | `step_id`, `status`, `actor`, `purpose`, `tools`, `frequency`, `duration_minutes`, `updated_at`, `completeness_pct`, `validated_version` | Une ligne par étape avec contenu (`empty` sans fiche) ; contenu en cours, brouillon compris, signalé par `status` = `draft` ; `validated_version` : numéro de la dernière version validée, nul si aucune, ajouté en 2.0 pour distinguer un premier brouillon d'un brouillon sur une fiche validée (AR-101) ; complétude sur 7 rubriques, même calcul que `SheetCompleteness` (A-2.10-17) |
| `bi.step_operation` | `step_id`, `position`, `text`, `tool`, `nature` | Déroulé des fiches des étapes avec contenu |
| `bi.document` | `document_id`, `step_id`, `direction`, `name`, `location`, `location_type`, `wait_hours` | Documents des étapes avec contenu |
| `bi.document_use` | `document_id`, `step_id`, `wait_hours`, `percent_complete_accurate`, `pain_point_id` | Usages par une étape avec contenu d'un document produit par une étape vivante avec contenu |
| `bi.pain_point` | `pain_point_id`, `project_id`, `step_id`, `code`, `description`, `lost_hours_per_month`, `status`, `created_at` | Irritants des projets vivants, sur la carte ou sur une étape vivante |
| `bi.step_raci` | `step_id`, `lane_id`, `role` | RACI visible, comme `RaciRules.IsVisible` : étape avec contenu, couloir vivant de la même carte, autre que celui de l'étape (A-6-14) |

Écarts à la 1.x (mêmes vues, mêmes filtres, mêmes colonnes plus deux ajoutées à la fin) :

| Écart | 1.x | 2.0 |
|---|---|---|
| Schéma lu | `ariane` | `topogenia` |
| `bi.step_operation.position` | `smallint` | `int` (§ 6.12) |
| `bi.document_use.percent_complete_accurate` | `tinyint` | `int` (§ 6.17) |
| Dates (`created_at`, `updated_at`) | `datetime2(3)` | `datetime2(7)` (ADR 028) |
| `source_language` | `char(2)` | `varchar(10)` (§ 8) |
| `completeness_pct` | `tinyint` | `tinyint` (inchangé) |
| Colonnes nouvelles des tables (`sharing_group_label`, `share_confirmed`, `origin_operation_id`, `version`...) | - | Non exposées : la 1.x ne les avait pas |
| `bi.project.classification`, `bi.step_sheet.validated_version` | - | Ajoutées à la fin des vues (AR-100, AR-101, décisions du 24/10/2026) : niveau de confidentialité du projet, dernière version validée d'une fiche |
| Nom de la CTE de `bi.process_map`, commentaires | `arbre`, français | `tree`, anglais (Q1) |
| Profondeur de `bi.process_map` | Récursion sans borne : au-delà de 101 niveaux, toute lecture de la vue et des vues construites dessus échoue (100 récursions de SQL Server, qu'une vue ne peut pas relever) | 101 niveaux au plus (AR-87) : tout ce que la 1.x montrait, et la vue ne peut plus échouer ; les parcours du domaine restent bornés à 50 (AR-67) |
| Collation des colonnes | Collation de la base (`windows_login` en `Latin1_General_100_CI_AS`) | `Latin1_General_100_BIN2` sur toute colonne texte (ADR 038) : un outil qui compare ou trie dans la base compare en binaire |

Donnée personnelle exposée au rôle des rapports (ADR 035, ADR 032 point 8) : `bi.step.assignee_user_id`, seule colonne
des vues qui rattache une personne (à une étape). Elle est gardée, sans écart à la 1.x : la vue `bi.step` de
`db/004_acces.sql` l'exposait déjà, et la spec 4.4 demande le même contenu fonctionnel. C'est un identifiant opaque
(GUID) : `topogenia_bi` ne lit aucune table, `app_user` comprise
(`RoleTests.The_report_role_reads_every_report_view_and_no_table`), et aucune vue ne donne le nom affiché, le login ni
l'e-mail ; la colonne a le même contenu qu'en 1.x. Un rapport peut compter ou regrouper les étapes par personne, jamais
la nommer. La retirer serait un écart que la spec ne demande pas : question posée au porteur du projet.

### 10.3 Le flux de valeur (3.0, ADR 085)

Les saisies du flux de valeur sont ajoutées **en fin** des vues qu'elles concernent, les colonnes existantes gardant leur
ordre (migration SQL Server `ValueStreamReportViews`, écrite à la main ; sa descente rend les vues d'avant, texte pour
texte) : `bi.project` (`workday_hours`, `fte_hours_per_month`), `bi.lane` (`headcount`), `bi.step` (`volume_quantity`,
`volume_period`), `bi.flow` (`share_percent`), `bi.step_sheet` (`lead_time`, `lead_time_unit`). Les chiffres calculés
(débits, charge, délais, chemin principal) ne sont pas dans la base : un rapport qui les veut lit l'export Excel du
calque ou les recalcule.

Les CHECK des fréquences (`CK_step_sheet_frequency`, `CK_step_sheet_version_frequency`) admettent `per_case` ; ceux du
délai (`CK_step_sheet_lead_time`, `_lead_time_unit`, `_lead_time_unit_text`, `_lead_time_value`, et les mêmes sur
`step_sheet_version`) exigent la valeur et l'unité ensemble, l'unité parmi `minute`, `hour`, `day`, la valeur de 0 à
100 000.
