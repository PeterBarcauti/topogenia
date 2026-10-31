<!-- r2-b.md -->
<!-- Couverture des corrections de la porte P3 round 2, agent B : infrastructure de test sous charge -->

# Couverture phase 3, porte P3 round 2 : infrastructure de test sous charge (ADR 040 et 029 révisées)

Constats traités (intégrité MAJ-1, MAJ-2, MAJ-3, m3, m4, m5 ; données m6) :

1. Le nettoyage des bases d'un processus mort gardait son échec pour tout le processus (`Lazy<Task>`), et sa liste
   (`GetSchemaAsync("Databases")`) attendait les `CREATE DATABASE` en cours (`LCK_M_S`).
2. `ALTER DATABASE CURRENT SET ALLOW_SNAPSHOT_ISOLATION OFF` attendait 120 s ou plus.
3. 12408 (Query Store) à la suppression d'une base n'était pas passager.
4. `CreatingAsync` rejouait la migration sous test ; essais imbriqués 8 × 8 au-delà de l'ADR 040.
5. Dossiers `topogenia-sqlite-*` et `topogenia-files-*` sans numéro de processus ; `RealBusyTests.CreateAsync` sans `try`.
6. Bornes d'horloge de `RealBusyTests` dépassées sous charge.
7. Ligne S-3.1-11 incomplète.

Préfixe des tests : `Topogenia.Persistence.Tests.`. À reporter dans `docs/couverture.md` : la colonne « Test 2.0 »
reçoit les tests ci-dessous (en plus de ceux déjà cités), la colonne « État » la valeur indiquée ; la ligne S-3.1-11
est remplacée par celle-ci.

| ID | État | Test 2.0 | Remarque |
|---|---|---|---|
| S-3.1-11 | couvert | `Topogenia.Persistence.Tests.Guards.DatabaseShapeTests.Every_theory_of_the_persistence_that_takes_an_engine_runs_on_both_engines` | Toute théorie qui prend un `EngineKind` reçoit les deux moteurs. Propres à un moteur, par conception. **SQL Server seul** : `Bi/` (`AccessMigrationTests`, `ReportDepthTests`, `ReportViewTests`, `RoleTests` : rôles et vues de rapport, D04) ; `Collation/CollationMigrationTests` (collation binaire) ; `Schema/AppAccountCatalogTests`, `Schema/CatalogChangeTests` (droits et catalogue) ; les faits de `Schema/SnapshotIsolationTests` (options de la base, ADR 031) ; `Startup/StartupErrorTests` (4060, 18456, 229, 262) ; `Startup/TransientStartupTests.A_contention_of_the_check_and_of_the_migration_is_transient_and_a_new_run_succeeds` (1807, 1802) ; `Loading/ConsistentReadTests.On_sql_server_the_application_role_loads_in_a_snapshot` ; `Purge/PurgeRightsTests` (rôles) ; `Writing/RetryTests.On_sql_server_a_real_deadlock_is_retried_and_both_units_commit` ; `Schema/SchemaMigrationTests.On_sql_server_*` ; les faits SQL Server de `Infrastructure/TestInfrastructureTests` (préparation des bases de test). **SQLite seul** : `Sqlite/` (fichier du mode Poste : sauvegarde, restauration, réglages, culture décimale, démarrage, occupation réelle) ; `Schema/SqliteCatalogChangeTests` ; `Startup/SqliteRebuildTests` ; `Startup/SqliteStartupErrorTests` ; `Writing/ErrorTranslationTests.Sqlite_names_a_unique_index_or_key_by_its_columns_and_a_check_by_its_name` ; `Schema/SchemaMigrationTests.On_sqlite_*`. **Sans base** : `Files/` (chemin du fichier du mode Poste), `Decimals/DecimalScaleTests.The_conversion_rounds_half_away_from_zero_and_drops_the_trailing_zeros`, `Writing/ChunkTests`, `Writing/RetryTests.The_policy_retries_every_passing_kind_and_no_other`, `Startup/TransientStartupTests.Only_a_contention_is_transient`, `Guards/DatabaseShapeTests.Both_providers_map_one_model_with_the_same_tables_columns_keys_indexes_and_guards` (les deux modèles comparés). Mutation : une théorie sur SQLite seul |
| A-5-31 | couvert | `Topogenia.Persistence.Tests.Infrastructure.TestInfrastructureTests.A_cleaning_that_fails_or_lasts_is_left_to_the_next_run_and_never_fails_a_test` ; `Topogenia.Persistence.Tests.Infrastructure.TestInfrastructureTests.The_list_read_without_waiting_names_the_bases_as_the_committed_one_does` ; `Topogenia.Persistence.Tests.Infrastructure.TestInfrastructureTests.Nested_retries_share_the_budget_of_the_outermost_one_and_none_starts_once_it_is_spent` ; `Topogenia.Persistence.Tests.Infrastructure.TestInfrastructureTests.A_step_of_a_test_that_creates_its_base_runs_again_only_on_a_passing_failure_of_the_creation_that_left_no_base` ; `Topogenia.Persistence.Tests.Infrastructure.TestInfrastructureTests.A_step_of_a_test_that_creates_its_base_never_runs_again_once_the_base_exists` ; `Topogenia.Persistence.Tests.Infrastructure.TestInfrastructureTests.The_folder_of_the_sqlite_files_of_a_test_carries_its_process_number_as_every_cleaned_folder` ; `Topogenia.Persistence.Tests.Infrastructure.TestInfrastructureTests.Only_the_passing_failures_of_the_instance_are_retried_and_1802_alone_never_is` (cas 12408) (en plus des tests déjà cités) | Bases de test sous charge (ADR 040, révision du round 2) : nettoyage sans échec et borné à 60 s, liste lue sans attendre les créations (session `READ UNCOMMITTED`, hors pool), 12408 passager à la suppression seulement, seule la création rejouée quand elle n'a laissé aucune base, budget de 10 minutes commun aux essais imbriqués, dossiers `topogenia-test-<pid>-sqlite-*` et `topogenia-test-<pid>-files-*` nettoyables. Mutations : nettoyage attendu tel quel (rouge) ; transaction non validée retirée (liste 2 645 ms derrière une création, sonde temporaire rouge ; 241 ms contre 2 069 ms pour la lecture validée sans mutation) ; 12408 retiré (rouge) ; création rejouée une fois la base créée (rouge) ; nouvel essai sans condition de création (rouge) ; budget ignoré (64 essais au lieu de 1, rouge, 218 s) ; dossier sans numéro de processus (rouge) |
| A-5-11 | couvert | `Topogenia.Persistence.Tests.Schema.SnapshotIsolationTests.Removing_the_snapshot_isolation_by_hand_waits_for_the_writes_in_progress_in_another_versioned_database` (en plus des tests déjà cités) | Cause de l'attente de `ALLOW_SNAPSHOT_ISOLATION OFF` : toute transaction qui écrit dans une base versionnée de l'instance au moment où il commence (reproduit : transaction d'une autre base versionnée, 8 s d'attente ; base non versionnée, aucune ; passage à `ON`, aucune). `WITH ROLLBACK IMMEDIATE` refusé par SQL Server (5083). Le retrait à la main se fait avec le délai des migrations (ADR 041). Mutation : contexte ordinaire (délai 1 s dans le test) : rouge |
| A-5-09 | couvert | `Topogenia.Persistence.Tests.Sqlite.RealBusyTests.The_sleeps_of_the_busy_handler_of_sqlite_sum_to_its_busy_timeout` (en plus des tests déjà cités) | Les 5 s de SQLite sont 59 pauses demandées (1 à 100 ms), pas un temps d'horloge ; la borne haute de `RealBusyTests` est la formule de l'ADR 029 où les 5 s sont remplacées par les mêmes pauses dormies au même moment par le test, plus 1,15 s par instruction et 2 s de marge ; bornes basses, nombre d'essais, erreurs et pauses inchangés (ADR 029 et 040 révisées). Mutation : délai du fournisseur de 30 s (EF Core et connexion) : les deux tests rouges. `CreateAsync` supprime le fichier si sa préparation échoue |

## Mesures

| Exécution | Tests | Échecs | Durée |
|---|---|---|---|
| Classes visées (`TestInfrastructureTests`, `RealBusyTests`, `SnapshotIsolationTests`) | 71 | 0 dans le périmètre | 74 s |
| Solution entière | 2 860 | 10, tous hors du périmètre | 1 112 s |
| Trois suites de persistance simultanées, premier essai | 976 × 3 | 9, 9, 9 | 1 671, 1 550, 1 631 s |
| Trois suites de persistance simultanées, second essai | 976 × 3 | 3, 4, 4 | 1 341, 1 369, 1 364 s |

Les échecs sont identiques d'une suite à l'autre et viennent des travaux en cours des autres agents (nouvelles
migrations, catalogue, base absente) : `Schema.SchemaMigrationTests.A_new_database_is_behind_...(SqlServer)`
(`BASE_INACCESSIBLE`), `Guards.DatabaseShapeTests.The_database_keeps_structural_guards_only_...`,
`Startup.SqliteRebuildTests`, `Collation.TextParityTests` (espace final), `Schema.CatalogChangeTests` (`SchemaOwnedByTheReports`),
`Sqlite.MigrationRollbackTests` (SQL Server, `Execution Timeout`) ; au premier essai aussi
`Schema.SnapshotIsolationTests.Every_migration_recorded_outside_a_transaction_is_followed_by_one_in_a_transaction(Sqlite)`,
dont l'attente a été mise à jour ensuite (nouvelle migration SQLite `20261023174259_TextGuardsAndIntegerBounds`
exécutée hors transaction par EF Core, suivie de `20261023174605_TextGuardsCheck` en transaction). Aucun échec de
nettoyage, de suppression (12408) ni de délai dans les tests du périmètre.

Attente de `ALLOW_SNAPSHOT_ISOLATION OFF` relevée pendant les essais (`sys.dm_exec_requests`, attente
`DISABLE_VERSIONING`) : jusqu'à 107 s, derrière des transactions d'autres bases de test ouvertes depuis 106 à 121 s
(`sys.dm_tran_active_snapshot_database_transactions`) ; 47 bases concernées au premier essai. Le délai de 120 s
ne suffisait donc pas, celui des migrations (10 minutes) suffit.

Aucune base `Topogenia_*` ni aucun dossier `topogenia-test-*` créé par ces exécutions ne reste.
