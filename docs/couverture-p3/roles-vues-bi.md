<!-- roles-vues-bi.md -->
<!-- Couverture de la tâche P3-T06 : rôles de base, schéma bi et vues de rapport du mode Serveur -->

# Couverture phase 3, P3-T06 : rôles `topogenia_app` et `topogenia_bi`, vues `bi`

Lignes couvertes par la migration SQL Server `20261023083307_RolesAndReportViews`, le catalogue
`SqlServerSchemaCatalog` (empreinte étendue aux droits) et les tests de
`tests/Topogenia.Persistence.Tests/Bi/`. Préfixe des tests : `Topogenia.Persistence.Tests.`. Ces tests
tournent sur SQL Server seulement (rôles et vues propres au mode Serveur, D04, ADR 032), dans des bases
`Topogenia_Test_*`, avec des utilisateurs sans connexion (`CREATE USER ... WITHOUT LOGIN`, `EXECUTE AS
USER`). À reporter dans `docs/couverture.md` : la colonne « Test 2.0 » reçoit les tests ci-dessous en
plus de ceux déjà cités, la colonne « État » la valeur indiquée.

## Annexe A

| ID | État | Test 2.0 | Remarque |
|---|---|---|---|
| A-4-40 | couvert | `Bi.RoleTests.The_application_role_holds_exactly_the_data_permissions_of_its_rules_and_none_on_the_report_views` ; `Bi.RoleTests.The_application_role_reads_the_histories_of_the_migrations_and_cannot_write_them` ; `Bi.RoleTests.The_application_role_writes_reads_changes_and_removes_the_rows_of_a_project` ; `Bi.RoleTests.The_schema_check_run_by_the_application_role_sees_the_schema_the_migrations_recorded` | Part persistance (phase 3) ; l'hôte qui se connecte sous ce rôle relève de la phase 4. `DENY INSERT, UPDATE, DELETE` sur `schema_history` et `__EFMigrationsHistory` ; `VIEW DEFINITION` pour le contrôle du démarrage (ADR 032) |
| A-4-41 | couvert | `Bi.RoleTests.The_report_role_holds_the_select_of_the_report_views_and_nothing_on_the_tables` ; `Bi.RoleTests.The_report_role_reads_every_report_view_and_no_table` ; `Bi.ReportViewTests.Nothing_of_a_project_in_the_trash_reaches_the_report_views` ; `Bi.ReportViewTests.Each_report_view_gives_the_live_content_of_a_project_and_nothing_deleted_or_hidden` | Vues sans objets supprimés ni orphelins ; projets à la corbeille exclus (1.x `db/006`) |
| A-5-29 | couvert | `Bi.AccessMigrationTests.The_migration_creates_two_roles_the_report_schema_and_eleven_views_all_described_by_the_fingerprint` ; `Bi.ReportViewTests.Each_report_view_has_the_columns_of_the_1x_view_in_their_order` ; `Bi.ReportViewTests.Each_report_view_gives_the_live_content_of_a_project_and_nothing_deleted_or_hidden` ; `Bi.AccessMigrationTests.A_database_at_the_initial_schema_is_behind_and_migrates_to_the_roles_and_the_report_views` | Équivalent de `Les_vues_des_rapports_se_lisent` (1.x), renforcé : colonnes et contenu de chacune des 11 vues |
| A-5-17 | couvert | (déjà cités) ; `Bi.AccessMigrationTests.The_migration_creates_two_roles_the_report_schema_and_eleven_views_all_described_by_the_fingerprint` | Les seules vues sont celles du schéma `bi`, en SQL Server |
| A-2.10-17 | couvert | (part domaine déjà citée) ; `Bi.ReportViewTests.The_completeness_of_the_report_view_is_the_one_of_the_domain_for_every_step_with_content` ; `Bi.ReportViewTests.Each_report_view_gives_the_live_content_of_a_project_and_nothing_deleted_or_hidden` (`step_sheet`) | La vue calcule en SQL ; son résultat est comparé, étape par étape, à `SheetCompleteness` (le calcul unique du domaine), avec un document d'une étape supprimée qui ne compte pas |
| A-6-06 | couvert | comme A-2.10-17 | Même valeur dans l'API (domaine) et dans `bi.step_sheet` |
| A-2.12-03 | couvert | (part domaine déjà citée) ; `Bi.RoleTests.The_application_role_appends_to_the_histories_of_the_data_and_purges_them_but_never_rewrites_them` ; `Bi.RoleTests.The_application_role_holds_exactly_the_data_permissions_of_its_rules_and_none_on_the_report_views` | `DENY UPDATE` sur `pain_point_status_history` pour `topogenia_app` (aussi sur `step_sheet_version` et `audit_event`) ; `DELETE` permis pour la purge en C# (ADR 032) |
| A-2.13-13 | couvert (part phase 3) | `Bi.ReportViewTests.The_raci_of_the_report_view_is_the_visible_raci_of_the_domain` | Part `bi.step_raci` ; l'export relève de la phase 5 |
| A-6-14 | couvert (part phase 3) | `Bi.ReportViewTests.The_raci_of_the_report_view_is_the_visible_raci_of_the_domain` ; `Bi.ReportViewTests.Each_report_view_gives_the_live_content_of_a_project_and_nothing_deleted_or_hidden` (`step_raci`) | Rôles masqués : porte, couloir supprimé, couloir d'une autre carte, couloir de l'étape ; comparé à `RaciRules.Visible` |
| A-4-36 | couvert | (déjà cités) ; `Bi.AccessMigrationTests.A_table_granted_by_hand_to_the_report_role_is_refused_as_a_changed_schema` ; `Bi.AccessMigrationTests.A_deny_removed_by_hand_from_the_application_role_is_refused_as_a_changed_schema` ; `Bi.AccessMigrationTests.The_report_role_made_a_reader_of_every_table_by_hand_is_refused_as_a_changed_schema` ; `Bi.AccessMigrationTests.Users_added_to_the_roles_by_the_administrator_leave_the_schema_accepted` | L'empreinte du schéma couvre les droits des rôles (ADR 032) ; les membres ajoutés par l'administrateur ne la changent pas |
| A-4-33 | couvert | (déjà cités) ; `Bi.AccessMigrationTests.Migrating_again_changes_neither_the_roles_nor_their_rights_nor_the_views` ; `Bi.AccessMigrationTests.Reverting_the_migration_gives_back_exactly_the_initial_schema` | Seconde migration SQL Server ; `Down` rend exactement le schéma initial |

## Exigences 2.0

| ID | État | Test 2.0 | Remarque |
|---|---|---|---|
| S-D04-03 | couvert | `Topogenia.Architecture.Tests.ExtraRulesTests.Views_exist_only_in_the_bi_schema_of_the_sql_server_provider` ; `Schema.SchemaMigrationTests.The_migrations_of_each_provider_match_the_model_and_embed_their_source` (SQLite : la seule migration initiale) | Aucune migration SQLite pour les vues (ADR 032 : la 1.x n'avait pas de mode fichier) |
| S-3.1-05 | couvert | `Bi.AccessMigrationTests.The_migration_creates_two_roles_the_report_schema_and_eleven_views_all_described_by_the_fingerprint` | Migration, rôles et vues dans `Persistence.SqlServer` (`SqlServerAccess`, `Migrations/`) |
| S-5.3-04 | à faire | `Bi.RoleTests.The_application_role_reads_the_histories_of_the_migrations_and_cannot_write_them` (part persistance) | Le compte de l'application ne peut pas migrer ; la séparation des comptes est une revue d'exploitation (phase 7) |
