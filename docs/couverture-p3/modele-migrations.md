<!-- modele-migrations.md -->
<!-- Couverture des tâches P3-T01 et P3-T02 : modèle physique, garde-fous, migrations et historique du schéma -->

# Couverture phase 3, P3-T01 et P3-T02 : modèle, garde-fous, migrations

Lignes de phase 3 couvertes par le modèle EF Core, les migrations initiales des deux fournisseurs et
l'historique du schéma. Préfixe des tests : `Topogenia.Persistence.Tests.` ; chaque test est une théorie
exécutée sur SQLite **et** SQL Server (`EngineKind`), sauf les deux tests propres à un moteur
(`On_sql_server_...`, `On_sqlite_...`). À reporter dans `docs/couverture.md` : la colonne « Test 2.0 »
reçoit les tests ci-dessous en plus de ceux déjà cités, la colonne « État » la valeur indiquée.

## Annexe A, section 5 (points SQL Server, portabilité)

| ID | État | Test 2.0 | Remarque |
|---|---|---|---|
| A-5-01 | couvert | `Schema.SchemaMigrationTests.On_sql_server_the_tables_live_in_the_schema_named_after_the_product` ; `Schema.SchemaMigrationTests.On_sqlite_the_tables_have_no_schema_prefix` ; toute la suite `Topogenia.Persistence.Tests` sur les deux moteurs | Schéma dérivé de `ProductIdentity.Name` (`SqlServerEngine.Schema`, D13) |
| A-5-02 | couvert | `Model.ColumnTests.Every_key_is_given_by_the_application_and_never_by_the_database` (en plus des tests du domaine déjà cités) | Part phase 3 : `ValueGeneratedNever` sur toute clé, aucune valeur par défaut de base |
| A-5-03 | couvert | `Model.ColumnTests.A_change_written_on_a_version_read_before_another_change_is_refused_as_a_conflict` ; `Model.ColumnTests.Every_modifiable_record_has_an_integer_version_as_its_concurrency_token_and_none_has_a_rowversion` | Le conflit est vu par EF Core sur les deux moteurs ; sa traduction en code métier relève de P3-T04 et de la phase 4 |
| A-5-04 | couvert | `Model.ColumnTests.Dates_compare_and_sort_the_same_on_both_engines` ; `Model.RoundTripTests.Every_date_comes_back_utc_to_the_tick` ; `Model.ColumnTests.A_local_date_is_refused_before_anything_is_written` ; `Model.ColumnTests.Every_code_of_a_closed_list_fits_its_column_and_every_date_is_kept_in_utc` | Dates au tick (`datetime2(7)`, ADR 028) |
| A-5-05 | couvert | `Model.UniqueIndexTests.The_login_keeps_its_case_and_its_normalised_form_is_the_unique_one` ; `Model.ColumnTests.No_column_has_a_collation_of_its_own` | Aucune collation propre ; casse traitée par `login_normalized` |
| A-5-12 | couvert (revue) | `src/Topogenia.Persistence*/` : ni `master`, ni `sp_getapplock`, ni `CREATE DATABASE` | La création de la base et le verrou des migrations sont ceux d'EF Core (`Database.MigrateAsync`) ; un test d'architecture dédié reste possible (phase 1) |
| A-5-22 | couvert | `Model.ColumnTests.The_frequency_and_duration_of_a_validated_version_are_read_from_their_own_columns` ; `Model.RoundTripTests.Every_record_is_read_back_equal_to_the_record_written` (`StepSheetVersion`) | Colonnes dénormalisées `frequency`, `duration_minutes` |
| A-5-24 | couvert | `Model.UniqueIndexTests.Two_live_rows_are_refused_by_the_partial_unique_index` ; `Model.UniqueIndexTests.A_row_outside_the_filter_of_the_partial_unique_index_is_accepted` ; `Model.UniqueIndexTests.Every_partial_unique_index_of_the_model_has_its_cases` ; `Schema.SchemaMigrationTests.Every_guard_of_the_model_exists_in_the_migrated_database` | Les 7 index (case, flèche, sous-carte, racine, A, cadre, irritant par usage), un cas refusé et un accepté chacun |
| A-5-26 | couvert | `Model.ForeignKeyTests.A_step_on_a_lane_of_another_map_is_refused_by_the_composite_key` ; `Model.ForeignKeyTests.A_step_moved_to_another_map_must_take_a_lane_of_that_map` | Clé `FK_step_lane` (lane_id, map_id) vers `UQ_lane_id_map_id` |
| A-5-27 | couvert | `Model.ForeignKeyTests.Maps_and_steps_that_reference_each_other_are_created_in_one_save_and_removed_from_the_detail_up` | Références croisées `process_map.parent_step_id` / `step.map_id` |
| A-5-30 | à faire | - | Aucune migration ne change encore une contrainte ; le test viendra avec la première (reconstruction de table SQLite) |
| A-5-31 | couvert | `Infrastructure.TestDatabase` (base `Topogenia_Test_<processus>_<n>` créée par les migrations, supprimée par le test) ; théories `Engines.All` de toute la suite | Part persistance ; la part API relève de la phase 4. Aucun `master` dans le code des tests : la création est celle d'EF Core |
| A-5-32 | couvert | `Model.UniqueIndexTests.A_value_unique_over_every_row_is_refused_twice` (`UQ_*_code`, `UQ_app_user_login_normalized`) | Codes et login indexés sur les deux moteurs, sans collation |

## Annexe A, section 4 (mise à jour du schéma)

| ID | État | Test 2.0 | Remarque |
|---|---|---|---|
| A-4-33 | couvert | `Schema.SchemaMigrationTests.Migrating_creates_the_database_and_records_each_migration_with_its_fingerprints` ; `Schema.SchemaMigrationTests.The_migrations_of_each_provider_match_the_model_and_embed_their_source` | Un jeu de migrations EF Core par fournisseur |
| A-4-34 | couvert | `Schema.SchemaMigrationTests.A_migration_that_fails_leaves_the_database_as_it_was_and_records_nothing` | L'historique est écrit dans la transaction de la migration (ADR 027) |
| A-4-35 | couvert | `Schema.SchemaMigrationTests.Two_processes_that_migrate_at_once_apply_each_migration_once` | Verrou des migrations d'EF Core ; le contrôle le prend aussi |
| A-4-36 | couvert | `Schema.SchemaRefusalTests.A_migration_changed_since_it_was_applied_is_refused` ; `Schema.SchemaRefusalTests.A_migration_applied_without_its_record_is_refused` ; `Schema.SchemaRefusalTests.A_last_migration_recorded_without_the_fingerprint_of_its_schema_is_refused` ; `Schema.SchemaRefusalTests.A_column_added_outside_the_migrations_is_refused_as_a_changed_schema` ; `Schema.SchemaRefusalTests.An_index_dropped_outside_the_migrations_is_refused_as_a_changed_schema` ; `Schema.SchemaRefusalTests.A_migration_removed_from_the_history_of_ef_core_is_refused_as_a_changed_schema` ; `Schema.SchemaRefusalTests.A_database_left_as_the_migrations_made_it_is_accepted` | Codes `MIGRATION_MODIFIEE`, `EMPREINTE_ABSENTE`, `SCHEMA_MODIFIE` ; l'appel au démarrage relève de l'hôte (phase 4) |
| A-4-37 | couvert | `Schema.SchemaRefusalTests.A_database_whose_history_holds_a_migration_unknown_to_the_application_is_refused_as_newer` ; `Schema.SchemaRefusalTests.A_database_migrated_by_a_newer_application_is_refused_as_newer` | Code `BASE_PLUS_RECENTE` ; le contrôle et la migration refusent, rien n'est changé |
| A-4-38 | à faire | `Schema.SchemaMigrationTests.A_new_database_is_behind_on_sqlite_and_refused_as_unreachable_on_sql_server_and_the_check_never_creates_it` ; `Schema.SchemaMigrationTests.Migrating_an_up_to_date_database_changes_nothing` (part persistance) | La persistance donne l'état « en retard » et migre ; la sauvegarde avant migration (P3-T03), le refus du mode Serveur (`BASE_EN_RETARD`) et `--migrate` (phase 4) restent |
| A-4-39 | couvert | `Schema.SchemaMigrationTests.Migrating_creates_the_database_and_records_each_migration_with_its_fingerprints` (SQLite : le fichier est créé par la migration) | Part persistance ; l'emplacement du fichier relève de l'hôte (phase 4) |
| A-4-53 | à faire | `Model.CheckConstraintTests.A_row_that_breaks_a_check_constraint_is_refused_by_it` ; `Model.CheckConstraintTests.A_row_on_the_bound_of_a_check_constraint_is_accepted` (part P3-T01) | Les CHECK sont construits sur les codes et les bornes du domaine (`Codes.All`, `Cell.MaxRow`, `Duration.MaxMinutes`, `SheetContentReader.ProcedureMax`, `DocumentRules.PercentMax`) ; la traduction d'une violation en `CONFLIT_INTEGRITE` relève de P3-T04, l'absence de 500 de la phase 4 |

## Exigences 2.0 (spec 4.1, 4.2, 4.6)

| ID | État | Test 2.0 | Remarque |
|---|---|---|---|
| S-4.1-01 | couvert (revue) | `docs/modele-donnees.md` ; `Model.ColumnTests.Every_table_and_column_is_named_in_snake_case` ; `Model.RoundTripTests.The_model_maps_every_record_of_the_domain_and_the_history_of_the_schema` ; `Model.RoundTripTests.Every_record_is_read_back_equal_to_the_record_written` | Correspondance table par table avec la 1.x et écarts (§ 6, § 8, ADR 028) |
| S-4.1-02 | couvert | `Model.ColumnTests.Every_modifiable_record_has_an_integer_version_as_its_concurrency_token_and_none_has_a_rowversion` | |
| S-4.1-03 | couvert | `Model.UniqueIndexTests.A_value_unique_over_every_row_is_refused_twice` (`UQ_app_user_login_normalized`) ; `Model.UniqueIndexTests.The_login_keeps_its_case_and_its_normalised_form_is_the_unique_one` | |
| S-4.1-04 | couvert | `Model.UniqueIndexTests.The_login_keeps_its_case_and_its_normalised_form_is_the_unique_one` | |
| S-4.1-09 | couvert | `Model.RoundTripTests.Every_record_is_read_back_equal_to_the_record_written` (`AppUser`, `last_project_id`) | Part persistance ; l'accueil relève de la phase 4 |
| S-4.2-01 | couvert | `Model.CheckConstraintTests.A_row_that_breaks_a_check_constraint_is_refused_by_it` ; `Model.CheckConstraintTests.A_row_on_the_bound_of_a_check_constraint_is_accepted` ; `Model.CheckConstraintTests.Every_check_constraint_of_the_model_has_a_refused_row` ; `Schema.SchemaMigrationTests.Every_guard_of_the_model_exists_in_the_migrated_database` | 56 CHECK, chacun refusé par son nom sur les deux moteurs |
| S-4.6-01 | couvert | `Model.RoundTripTests.Every_record_is_read_back_equal_to_the_record_written` (`OperationLogEntry`) | Colonnes de la spec 4.6 (`docs/modele-donnees.md` § 6.22) |
| S-4.6-03 | couvert | `Model.UniqueIndexTests.A_value_unique_over_every_row_is_refused_twice` (`UQ_operation_log_map_id_sequence`) | Part persistance : un rang par carte ; l'ordre de la pile relève de la phase 4 |
