<!-- unite-de-travail-chargeur.md -->
<!-- Couverture des tâches P3-T04 et P3-T05 : unité de travail, verrou, écriture du ChangeSet, erreurs, nouvel essai, chargeur -->

# Couverture phase 3, P3-T04 et P3-T05 : unité de travail et chargeur de l'espace

Lignes de phase 3 couvertes par l'unité de travail (`src/Topogenia.Persistence/Writing/`), la traduction des
erreurs (`src/Topogenia.Persistence/Errors/`, `SqlServerDialect`, `SqliteDialect`) et le chargeur
(`src/Topogenia.Persistence/Loading/`). Préfixe des tests : `Topogenia.Persistence.Tests.` ; chaque test est une
théorie exécutée sur SQLite **et** SQL Server (`EngineKind`), sauf ceux dont le nom dit le moteur
(`On_sql_server_...`, `On_sqlite_...`, `Sqlite_...`) et les deux tests sans base (`Chunks`). À reporter dans
`docs/couverture.md` : la colonne « Test 2.0 » reçoit les tests ci-dessous en plus de ceux déjà cités, la colonne
« État » la valeur indiquée. La colonne « Mutation » de la remarque donne la mutation du code qui fait échouer le
test (vérifiée).

## Annexe A, 2.16 (concurrence)

| ID | État | Test 2.0 | Remarque |
|---|---|---|---|
| A-2.16-01 | couvert | `Writing.VersionConflictTests.A_record_changed_since_the_loading_is_refused_with_the_author_and_time_of_the_change` ; `Writing.OperationRoundTripTests.Steps_and_flows_created_by_the_domain_are_read_back_as_written` (versions relues égales) ; `Model.ColumnTests.Every_modifiable_record_has_an_integer_version_as_its_concurrency_token_and_none_has_a_rowversion` | Version entière tenue par `ProjectWorkspace.Commit`, contrôlée par l'écriture (ADR 030). Mutation : conflit avalé |
| A-2.16-06 | couvert (persistance) | `Writing.VersionConflictTests.A_record_changed_since_the_loading_is_refused_with_the_author_and_time_of_the_change` | `CONFLIT_VERSION` avec `object`, `updated_by`, `updated_at` ; la mise en forme `{par, le, le_iso}` relève de l'hôte (phase 4) |
| A-2.16-08 | couvert | `Writing.UnitOfWorkTests.Locking_moves_the_structure_version_on_by_one_in_the_transaction_and_returns_it` ; `Writing.UnitOfWorkTests.Structure_operations_on_one_project_run_at_the_same_time_never_interleave` ; `Writing.UnitOfWorkTests.Locking_a_project_that_does_not_exist_is_refused_as_not_found` | `UPDATE project SET structure_version = structure_version + 1` écrit par EF Core (`ExecuteUpdateAsync`), sans indication de verrou. Mutation : verrou retiré |
| A-2.16-09 | couvert | `Writing.VersionConflictTests.*` (4 tests : modifié, seulement touché, supprimé, sans version) ; `Writing.UnitOfWorkTests.Structure_operations_on_one_project_run_at_the_same_time_never_interleave` | Jamais d'écriture perdue : la seconde écriture est refusée entière |
| A-2.16-10 | couvert | `Writing.RetryTests.Two_transient_failures_run_the_whole_unit_again_after_50_then_100_ms` ; `Writing.RetryTests.A_third_transient_failure_is_thrown_and_leaves_nothing_written` ; `Writing.RetryTests.A_business_refusal_is_never_retried` ; `Writing.RetryTests.A_loading_outside_a_unit_is_retried_whole_on_a_transient_failure` ; `Writing.RetryTests.On_sql_server_a_real_deadlock_is_retried_and_both_units_commit` ; `Writing.RetryTests.On_sqlite_a_real_busy_database_is_retried_once_its_write_lock_is_released` | Échecs simulés à la validation (les deux moteurs, les deux genres) ; vrai interblocage 1205 et vraie base occupée `SQLITE_BUSY`. Mutations : 2 essais, délais 50/50, 1205 non lu, `SQLITE_BUSY` non lu |
| A-2.16-11 | couvert (persistance) | `Writing.ErrorTranslationTests.A_step_written_in_a_cell_taken_since_is_refused_as_a_unique_conflict_naming_the_index` ; `Writing.ErrorTranslationTests.A_counter_left_out_of_the_workspace_is_refused_by_its_key_and_never_gives_a_code_twice` | `CONFLIT_UNICITE` avec `constraint` ; le 409 relève de la phase 4 |

## Annexe A, sections 4, 5 et 6

| ID | État | Test 2.0 | Remarque |
|---|---|---|---|
| A-4-51 | couvert | `Loading.ConsistentReadTests.A_structure_operation_committed_during_a_loading_is_never_half_seen` | Transaction différée en SQLite, sérialisable en SQL Server (ADR 031, écart à « instantané »). Mutations : lecture sans transaction, `READ COMMITTED` |
| A-4-53 | couvert (persistance) | `Writing.ErrorTranslationTests.A_row_out_of_a_check_constraint_is_refused_as_an_integrity_conflict_naming_the_constraint` (en plus des tests du domaine et de P3-T01) | Un `CHECK` violé donne `CONFLIT_INTEGRITE` (ADR 030), jamais une erreur interne ; l'absence de 500 relève de la phase 4. Mutation : `CHECK` non traduit |
| A-5-06 | couvert | `Writing.ChunkTests.A_change_set_of_more_than_2100_records_without_version_is_checked_and_written` ; `Writing.ChunkTests.A_list_is_cut_into_packets_of_500_distinct_values_in_their_order` | 2 201 enregistrements relus par paquets de 500 ; le chargeur n'envoie aucune liste (jointures). Mutation : paquets de 5 000 (tuée par le test unitaire seulement : EF Core 10 passe ici la liste en un paramètre, la limite de 2 100 n'est pas atteinte) |
| A-5-07 | couvert | `Writing.ErrorTranslationTests.A_step_written_in_a_cell_taken_since_is_refused_as_a_unique_conflict_naming_the_index` ; `Writing.ErrorTranslationTests.A_counter_left_out_of_the_workspace_is_refused_by_its_key_and_never_gives_a_code_twice` ; `Writing.ErrorTranslationTests.Sqlite_names_a_unique_index_or_key_by_its_columns_and_a_check_by_its_name` | SQL Server 2601, 2627 ; SQLite 2067, 1555 (nom retrouvé par le modèle). Mutations : unique traduit en intégrité, 2601 non lu, nom SQLite perdu |
| A-5-08 | couvert | `Writing.ErrorTranslationTests.A_flow_to_a_step_that_does_not_exist_is_refused_as_an_integrity_conflict` ; `Writing.ErrorTranslationTests.A_row_out_of_a_check_constraint_is_refused_as_an_integrity_conflict_naming_the_constraint` ; `Writing.ErrorTranslationTests.The_failure_of_a_foreign_key_and_of_a_check_is_read_as_its_own_kind` | SQL Server 547 (`FK_` ou `CK_` selon le nom) ; SQLite 787 (sans nom), 275. Mutations : 547 toujours lu comme `CHECK`, 787 non lu, `CHECK` non traduit |
| A-5-09 | couvert | voir A-2.16-10 | |
| A-5-11 | couvert | `Loading.ConsistentReadTests.A_structure_operation_committed_during_a_loading_is_never_half_seen` ; `Loading.WorkspaceLoaderTests.Inside_a_unit_of_work_the_loading_and_the_counters_see_what_the_unit_wrote` | RCSI non supposé (ADR 031) |
| A-5-16 | couvert | `Writing.UnitOfWorkTests.Structure_operations_on_one_project_run_at_the_same_time_never_interleave` | Quatre opérations simultanées réelles par moteur : sections critiques disjointes, codes distincts, `structure_version` et compteur à 4 |
| A-5-18 | couvert | `Writing.OperationRoundTripTests.A_move_that_turns_three_steps_round_their_cells_is_written_through_the_parking` ; `Writing.OperationRoundTripTests.Two_steps_that_exchange_their_cells_in_one_change_set_are_written` ; `Writing.OperationRoundTripTests.A_detail_dissolved_into_its_map_is_read_back_as_written` ; `Writing.OperationRoundTripTests.A_step_deleted_logically_frees_its_cell_for_a_step_moved_into_it_in_the_same_change_set` | Stationnement en colonne 1 000 000 000 + i et libération des places des index partiels d'abord (ADR 016, 030). Mutations : stationnement retiré (10 échecs), suppression logique non écrite d'abord (2 échecs). Survit : retrait physique d'un rôle A ou d'un usage écrit d'abord (EF Core ordonne déjà les suppressions avant ; code gardé par prudence) |
| A-5-19 | couvert (persistance) | `Writing.UnitOfWorkTests.Structure_operations_on_one_project_run_at_the_same_time_never_interleave` ; `Writing.ErrorTranslationTests.A_counter_left_out_of_the_workspace_is_refused_by_its_key_and_never_gives_a_code_twice` ; `Writing.VersionConflictTests.A_record_without_version_changed_since_the_loading_is_refused` | Le compteur avance sous le verrou de projet ; un compteur oublié ou relu périmé est refusé, jamais un code redonné |
| A-6-16 | couvert | `Writing.OperationRoundTripTests.A_move_that_turns_three_steps_round_their_cells_is_written_through_the_parking` (en plus des tests du domaine déjà cités) | Part phase 3 : un seul stationnement en base, dans `Writing/WritePlan` |

## Exigences 2.0 (spec 3.4, 4.3, 4.7)

| ID | État | Test 2.0 | Remarque |
|---|---|---|---|
| S-4.3-01 | couvert | `Writing.ErrorTranslationTests.*` ; `Writing.RetryTests.*` | Interface commune `Errors/EngineDialect` (classement, transactions), une implémentation par fournisseur ; le SQL brut reste dans les fournisseurs (aucun ici) |
| S-3.4-03 | couvert (persistance) | `Writing.OperationRoundTripTests.*` (10 théories, 20 cas) | Le `ChangeSet` du domaine écrit par `IWorkspaceStore.SaveAsync` ; le gestionnaire relève de la phase 4 |
| S-3.4-02 | à faire | `Infrastructure.StoreScenario.RunAsync` (gabarit verrou, chargement, domaine, écriture, sans autorisation ni journal) | Le gabarit complet relève de la phase 4 |

## Lignes restées à faire

| ID | Raison |
|---|---|
| A-4-52, A-5-14 | Écriture idempotente des traductions, usages et utilisateurs sous concurrence : relève des gestionnaires (phase 4) ; la persistance refuse déjà une création concurrente par la clé (`CONFLIT_UNICITE`) |
