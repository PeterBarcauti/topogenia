<!-- occupation-sqlite.md -->
<!-- Couverture des corrections de la porte P3 : fichier SQLite occupé ou illisible au démarrage, migrations des tests sous charge -->

# Couverture phase 3, fichier SQLite au démarrage et migrations des tests (ADR 036, 040, 041 révisées)

Constats traités (ADR 041, `docs/couverture-p3/delai-migrations.md`) :

1. `SqliteSchemaCatalog` n'avait pas de `RefusalOf` : un `SQLITE_BUSY` ou `SQLITE_LOCKED` rencontré par le contrôle ou
   la migration remontait en `SqliteException` brute au lieu de `BASE_OCCUPEE`. En écrivant le test réel, un second
   défaut est apparu : une sauvegarde restaurée (journal `DELETE`, ADR 029) était refusée au contrôle par
   `SQLITE_READONLY` 8, la connexion en lecture seule d'EF Core (`SqliteDatabaseCreator.Exists`) recevant
   `journal_mode = WAL`.
2. `Collation.CollationMigrationTests.A_database_in_use_migrates_to_binary_text_columns_and_reverts_to_its_previous_schema`
   créait ses bases par `IMigrator.MigrateAsync` sur un contexte ordinaire (`Execution Timeout Expired` dans
   `SqlServerDatabaseCreator.CreateAsync` sous trois suites simultanées).

Préfixe des tests : `Topogenia.Persistence.Tests.`. À reporter dans `docs/couverture.md` : la colonne « Test 2.0 »
reçoit les tests ci-dessous (en plus de ceux déjà cités), la colonne « État » la valeur indiquée.

| ID | État | Test 2.0 | Remarque |
|---|---|---|---|
| A-5-10 | couvert (persistance) | `Topogenia.Persistence.Tests.Startup.SqliteStartupErrorTests.The_result_code_of_sqlite_and_its_extended_code_are_read_as_one_refusal` ; `Topogenia.Persistence.Tests.Startup.SqliteStartupErrorTests.A_failure_that_is_not_one_of_sqlite_is_no_refusal` ; `Topogenia.Persistence.Tests.Startup.SqliteStartupErrorTests.A_file_that_is_not_a_database_is_refused_as_unreachable_by_the_check_and_the_migration` ; `Topogenia.Persistence.Tests.Startup.SqliteStartupErrorTests.A_data_file_that_cannot_be_opened_is_refused_as_unreachable_by_the_migration` ; `Topogenia.Persistence.Tests.Startup.SqliteStartupErrorTests.A_data_file_that_the_account_may_not_write_is_refused_for_its_rights_by_the_check_and_the_migration` | Mode Poste (ADR 036, révision) : le catalogue SQLite lit le premier `SqliteException` de la chaîne, code principal et octet bas du code étendu : 5, 6 `BASE_OCCUPEE` ; 14 (`CANTOPEN`), 26 (`NOTADB`) `BASE_INACCESSIBLE` ; 3 (`PERM`), 8 (`READONLY`) `DROITS_INSUFFISANTS` ; tout autre code (corrompu, disque plein, entrée-sortie) laissé au moteur. Fichiers réels : texte (26), dossier à la place du fichier (14), fichier marqué en lecture seule (8). Mutations : `RefusalOf` neutralisé (18 rouges), code étendu non lu (annoncé « 1 rouge », en fait vert : aucun cas n'avait un code principal muet et un code étendu parlant ; corrigé au round 2 par trois cas construits, `docs/couverture-p3/r2-a.md`). SQL Server : `Startup.StartupErrorTests.A_missing_database_is_reported_behind_never_created_and_refused_to_a_connection_with_4060_and_18456` rouvre seulement sur un échec sans réponse du serveur (233 seul sous charge), assertion inchangée (ADR 040, révision) |
| A-5-09 | couvert | `Topogenia.Persistence.Tests.Startup.SqliteStartupErrorTests.A_file_held_by_another_connection_is_refused_as_busy_by_the_check_and_the_migration_and_a_new_run_succeeds` (en plus des tests déjà cités) | Base occupée au démarrage : sauvegarde restaurée dont une autre connexion tient le verrou réservé (`BEGIN IMMEDIATE`) ; contrôle et migration refusés `BASE_OCCUPEE`, passagers, `SqliteException` 5 en exception interne ; une fois le verrou rendu, le contrôle et la migration réussissent. Mutation : occupation non lue (9 rouges, dont ce test) |
| A-4-35 | couvert | (déjà cités) | Le verrou des migrations SQLite tenu ailleurs donnait déjà `BASE_OCCUPEE` par l'attente bornée (ADR 041) ; un `SQLITE_BUSY` rencontré hors de cette attente (réglages de l'ouverture, instructions de la migration) est désormais traduit lui aussi |
| A-5-28 | couvert | `Topogenia.Persistence.Tests.Startup.SqliteStartupErrorTests.A_restored_backup_in_the_rollback_journal_is_accepted_by_the_check_and_taken_to_the_write_ahead_log` (en plus des tests déjà cités) | Une connexion ouverte en lecture seule (`Mode=ReadOnly`, celle par laquelle EF Core regarde si le fichier existe) reçoit tous les réglages sauf le mode de journal ; la première connexion qui peut écrire passe le fichier en `WAL` (réglages relus par `SqliteSettingsProbe` : ceux de la spec 4.3-6). Mutation : mode de journal posé sur la connexion en lecture seule (2 rouges) |
| A-4-29 | couvert | `Topogenia.Persistence.Tests.Startup.SqliteStartupErrorTests.A_restored_backup_in_the_rollback_journal_is_accepted_by_the_check_and_taken_to_the_write_ahead_log` (en plus des tests déjà cités) | La sauvegarde du mode Poste se restaure par simple copie (ADR 029) : avant correction, le contrôle du démarrage la refusait (`SQLITE_READONLY` 8) |
| A-5-05 | couvert | `Topogenia.Persistence.Tests.Collation.CollationMigrationTests.A_database_in_use_migrates_to_binary_text_columns_and_reverts_to_its_previous_schema` (déjà cité) | Test rendu sûr sous charge sans changer son assertion (ADR 040, révision) : bases créées par `TestDatabase.PreparedAsync` (sémaphore, nouvel essai), migrations par `DatabaseEngine.CreateMigrationContext()`. Même traitement pour `Bi.AccessMigrationTests` (base à la migration initiale, retour à l'initiale) et `Schema.SnapshotIsolationTests` (base aux rôles, retour aux rôles) |

## Mesures

| Exécution | Tests | Échecs | Durée |
|---|---|---|---|
| Nouveaux tests SQLite avant correction | 25 | 19 | 7 s |
| Nouveaux tests SQLite après correction | 25 | 0 | 7 s |
| Solution entière après les deux corrections | 2724 | 0 | 339 s |
| Trois suites de persistance simultanées, premier essai | 893 × 3 | 0, 0, 1 | 757, 745, 758 s |
| Trois suites de persistance simultanées, second essai | 893 × 3 | 0, 0, 1 | 740, 805, 834 s |
| Solution entière après la correction de `StartupErrorTests` | 2729 | 0 | 341 s |
| Trois suites de persistance simultanées, troisième essai | 898 × 3 | 0, 0, 0 | 734, 707, 749 s |

L'échec des deux premiers essais était dans un test que la mission ne visait pas :
`Startup.StartupErrorTests.A_missing_database_is_reported_behind_never_created_and_refused_to_a_connection_with_4060_and_18456`
(SQL Server, 44 s puis 76 s) : la connexion à la base absente a rendu 233 seul (connexion coupée par le serveur avant
sa réponse) au lieu de 4060 et 18456. Correction (ADR 040, révision, écart nommé) : seul un échec sans aucune réponse du
serveur (`TransientFailures.Unanswered`) fait rouvrir, borné ; l'assertion est inchangée. Test ajouté :
`Infrastructure.TestInfrastructureTests.Only_a_failure_made_of_cut_connections_is_without_an_answer_of_the_server`
(mutation `Any` au lieu de `All` : rouge).

Aucune base `Topogenia_*` ni aucun dossier `topogenia-test-*` ne reste après les exécutions.
