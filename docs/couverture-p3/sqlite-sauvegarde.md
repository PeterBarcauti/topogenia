<!-- sqlite-sauvegarde.md -->
<!-- Couverture de la tâche P3-T03 : réglages de chaque connexion SQLite, sauvegarde en ligne, démarrage du mode Poste -->

# Couverture phase 3, P3-T03 : réglages SQLite et sauvegarde

Lignes couvertes par les réglages de connexion (`SqliteSettingsInterceptor`), la sauvegarde en ligne
(`SqliteBackupService`) et le démarrage du fichier du mode Poste (`DesktopDatabaseStartup`), ADR 029.
Préfixe des tests : `Topogenia.Persistence.Tests.Sqlite.`. Tests SQLite seulement : ces comportements
n'ont pas de côté SQL Server (le mode Serveur est sauvegardé par le plan de SQL Server, spec 6.2). À
reporter dans `docs/couverture.md` : la colonne « Test 2.0 » reçoit les tests ci-dessous (en plus de ceux
déjà cités), la colonne « État » la valeur indiquée.

## Annexe A, section 4 (sauvegarde et mise à jour du schéma)

| ID | État | Test 2.0 | Remarque |
|---|---|---|---|
| A-4-29 | couvert | `BackupServiceTests.The_daily_backup_is_an_online_copy_that_holds_the_rows_still_in_the_write_ahead_log` ; `BackupServiceTests.Seven_daily_files_rotate_one_per_day_of_the_week` ; `BackupServiceTests.A_copy_that_fails_the_integrity_check_is_not_kept_and_the_previous_file_stays` ; `DesktopDatabaseStartupTests.A_new_data_file_is_created_by_the_migration_with_no_backup_before_and_gets_its_daily_backup` | API de sauvegarde en ligne, 7 fichiers `<fichier>_1_monday.db` à `_7_sunday.db`, `integrity_check` de la copie ; l'appel au lancement par l'hôte relève de P4-T05 |
| A-4-30 | couvert | `BackupServiceTests.The_daily_backup_is_skipped_when_the_backup_of_the_day_exists` ; `DesktopDatabaseStartupTests.An_up_to_date_database_is_not_migrated_and_its_daily_backup_is_made_once_a_day` | Jour UTC de l'horloge (ADR 029, D08) |
| A-4-31 | couvert | `BackupServiceTests.The_backup_before_a_migration_is_made_in_its_own_file_even_when_the_daily_backup_exists` ; `DesktopDatabaseStartupTests.A_database_behind_is_backed_up_before_it_is_migrated` | Fichier `<fichier>_before_<dernière migration>.db` (1.x `Ariane_avant_vN.bak`), écrit avant que la migration commence |
| A-4-32 | couvert | `DesktopDatabaseStartupTests.A_failed_backup_before_a_migration_stops_the_migration` ; `DesktopDatabaseStartupTests.A_failed_daily_backup_does_not_stop_the_start` ; `BackupServiceTests.A_backup_folder_that_cannot_be_created_fails_the_backup` ; `BackupServiceTests.A_missing_data_file_fails_the_backup_and_creates_nothing` | AR-14 : `MigrationBackupFailedException`, migration non lancée ; le message clair et l'arrêt de l'hôte relèvent de P4-T05 |
| A-4-38 | à faire | `DesktopDatabaseStartupTests.A_database_behind_is_backed_up_before_it_is_migrated` ; `DesktopDatabaseStartupTests.A_new_data_file_is_created_by_the_migration_with_no_backup_before_and_gets_its_daily_backup` ; `DesktopDatabaseStartupTests.A_refused_database_is_neither_backed_up_nor_migrated` (en plus des tests P3-T02) | Part persistance couverte (mode Poste : sauvegarde puis migration) ; restent le refus du mode Serveur (`BASE_EN_RETARD`), `--migrate` et l'appel par l'hôte (phase 4) |

## Annexe A, section 5 (portabilité)

| ID | État | Test 2.0 | Remarque |
|---|---|---|---|
| A-5-13 | couvert | `BackupServiceTests.The_daily_backup_is_an_online_copy_that_holds_the_rows_still_in_the_write_ahead_log` | `SqliteConnection.BackupDatabase` (sqlite3_backup) ; la copie contient les lignes encore dans le `-wal` (une copie de fichier les perdrait : vérifié par mutation), passe en journal `DELETE` ; ni `BACKUP DATABASE` ni `msdb` |
| A-5-28 | couvert | `ConnectionSettingsTests.Every_connection_opened_by_the_context_has_the_settings_of_spec_4_3` ; `ConnectionSettingsTests.A_connection_given_back_by_the_pool_gets_the_settings_again` ; `ConnectionSettingsTests.Foreign_keys_are_on_even_when_the_connection_string_turns_them_off_and_ef_core_opens_the_connection` ; `ConnectionSettingsTests.The_settings_read_from_a_raw_connection_follow_what_sqlite_holds` | Valeurs relues dans SQLite : `foreign_keys = 1`, `journal_mode = wal`, `busy_timeout = 5000`, `synchronous = 1` ; connexion du pool réutilisée (même poignée native) |

## Exigences 2.0

| ID | État | Test 2.0 | Remarque |
|---|---|---|---|
| S-3.1-06 | couvert | `ConnectionSettingsTests.*` ; `BackupServiceTests.*` ; `Schema.SchemaMigrationTests.On_sqlite_the_tables_have_no_schema_prefix` | `Persistence.Sqlite` porte les migrations, les réglages (`Connection/`) et la sauvegarde (`Backup/`) ; la ligne est de phase 1 (Architecture) : à confirmer par la porte |
| S-5.2-12 | à faire | `BackupServiceTests.Seven_daily_files_rotate_one_per_day_of_the_week` | Un fichier tournant par jour de la semaine : couvert ; le dossier `%LOCALAPPDATA%\Topogenia\backups\` est donné par l'hôte (P4-T05) au constructeur de `SqliteBackupService` |

## Points de la spec 4.3 et 5.2 sans ligne propre (déjà couverts ailleurs)

- 4.3-6 réglages SQLite : renvoi A-5-28 (ci-dessus).
- 5.2 sauvegarde quotidienne, 7 fichiers, avant migration, `integrity_check` : renvoi A-4-29 à A-4-32, A-5-13
  (ci-dessus). Le contrôle `foreign_key_check` de la copie est en plus (ADR 029) :
  `BackupServiceTests.A_copy_with_a_broken_reference_fails_the_foreign_key_check`.

## Vérification par mutation

Chaque exigence a été cassée dans le code puis remise ; les tests nommés sont passés au rouge :

| Mutation | Tests rouges |
|---|---|
| Intercepteur retiré de `SqliteEngine` | `Every_connection_opened...`, `A_connection_given_back_by_the_pool...`, `Foreign_keys_are_on_even...` |
| `synchronous` FULL, `busy_timeout` 0, journal `DELETE`, clés étrangères coupées dans `Default` | les tests de `ConnectionSettingsTests` (et le test en ligne pour le journal) |
| Copie de fichier au lieu de l'API de sauvegarde | `The_daily_backup_is_an_online_copy...`, `..._skipped_...`, `..._foreign_key_check` |
| Quotidienne jamais sautée / sautée quelle que soit la date | `..._skipped_...`, `An_up_to_date_database...` / `Seven_daily_files_rotate...`, `..._integrity_check...` |
| Un seul nom de fichier quotidien | `Seven_daily_files_rotate...` et cinq autres |
| `integrity_check` retiré ; `foreign_key_check` retiré | `A_copy_that_fails_the_integrity_check...` ; `A_copy_with_a_broken_reference...` |
| Copie laissée en WAL | `The_daily_backup_is_an_online_copy...` |
| Copie corrompue gardée | `..._integrity_check...`, `..._foreign_key_check` |
| Fichier avant migration nommé comme la quotidienne | `The_backup_before_a_migration...`, `A_database_behind...` |
| Contrôle « fichier de données absent » retiré | `A_missing_data_file_fails_the_backup_and_creates_nothing` |
| Migration lancée malgré l'échec ; pas de sauvegarde avant migration ; pas de quotidienne au démarrage | `A_failed_backup_before_a_migration...` ; `A_database_behind...` ; `A_new_data_file...` et trois autres |
