<!-- r2-c.md -->
<!-- Couverture de la correction C de la porte de la phase 3, round 2 : SQLite, sauvegarde, restauration, fichier du mode Poste -->

# Couverture phase 3, porte round 2, correction C : SQLite, sauvegarde, restauration, fichier du mode Poste

Constats traités : Données B1 (restauration par copie à côté d'un journal périmé), Données M2 (`Down` SQLite et verrou
des migrations), Données m4 (réglages de la connexion en lecture seule), Données m5 (noms du § 5.3 bis), Sécurité m2
(droits NTFS hors du profil, documenté), Sécurité m3 (chemin non pris en charge, `GLOBALROOT`), Architecture m6
(`MaximumBusyWait` interne), Architecture m7 (`DesktopDatabaseStartup`, écart nommé). Décisions : ADR 029 (deux
révisions), 035 et 036 complétées ; `docs/modele-donnees.md` § 7.1 et § 5.3 bis. À reporter dans `docs/couverture.md` :
les tests ci-dessous s'ajoutent à ceux déjà cités ; dans A-4-29, la remarque « La sauvegarde du mode Poste se restaure
par simple copie (ADR 029) » devient « La sauvegarde du mode Poste se restaure par `IBackupService.RestoreAsync` (ADR
029, révision du round 2) ; ouverte sur place, sans journal à côté, elle est acceptée » ; dans S-5.2-09 et S-5.2-10, le
test `DesktopDataFileTests.A_relative_path_is_refused_as_a_mistake_of_the_caller` est renommé
`DesktopDataFileTests.A_relative_path_or_a_form_the_check_cannot_read_is_refused_with_a_code` s'il y est cité.

| ID | État | Test 2.0 | Remarque |
|---|---|---|---|
| A-4-29 | couvert | `Topogenia.Persistence.Tests.Sqlite.RestoreTests.A_backup_copied_by_hand_next_to_the_journal_of_the_replaced_file_is_refused_by_the_check_and_restored_by_the_service` ; `Topogenia.Persistence.Tests.Sqlite.RestoreTests.A_data_file_stopped_with_work_in_its_journal_is_replaced_by_the_backup_and_the_journal_goes_with_it` ; `Topogenia.Persistence.Tests.Sqlite.RestoreTests.The_idle_connections_of_the_engine_are_released_and_a_connection_open_elsewhere_refuses_the_restoration` ; `Topogenia.Persistence.Tests.Sqlite.RestoreTests.A_missing_damaged_or_refused_backup_leaves_the_data_file_as_it_was` (en plus des tests déjà cités) | Les deux variantes de la sonde de la porte (même disposition ; disposition changée) : copie à la main à côté du `-wal` de l'ancien fichier refusée `BASE_INACCESSIBLE` (26) avant toute lecture, puis `RestoreAsync` : premier projet seul, journal absent, `integrity_check` et `foreign_key_check` vides, contrôle `UpToDate`. Rouges avant correction (contrôle `UpToDate`, second projet présent ou fichier corrompu). Mutations : contrôle du journal neutralisé, restauration = copie, journal non supprimé, fichier ouvert non vu, pool non vidé, sauvegarde sans migration acceptée : rouges (ADR 029). L'appel par l'hôte relève de P4-T05 |
| A-4-34 | couvert | `Topogenia.Persistence.Tests.Sqlite.MigrationRollbackTests.A_migration_that_fails_half_way_leaves_no_lock_and_the_next_run_succeeds` (en plus du test déjà cité) | Les deux moteurs : une instruction de création refusée pendant la migration ; le contrôle suivant répond sans attendre le délai des migrations (20 s ici), « en retard », puis la migration réussit. Vert avant correction : EF Core retire son verrou sur un échec d'exécution (modele-donnees § 7.1) |
| A-4-35 | couvert | `Topogenia.Persistence.Tests.Sqlite.MigrationRollbackTests.Every_migration_goes_down_to_an_empty_schema_and_the_database_is_migrated_again` ; `Topogenia.Persistence.Tests.Sqlite.MigrationRollbackTests.A_database_with_a_detailed_map_goes_down_on_sql_server_and_is_refused_by_sqlite_with_its_lock_given_back` ; `Topogenia.Persistence.Tests.Sqlite.MigrationRollbackTests.The_script_of_every_migration_up_and_down_is_generated_by_the_provider` (en plus des tests déjà cités) | Les deux moteurs : descente à zéro par les outils d'EF Core, schéma laissé égal à celui d'une base vide (SQL Server garde le schéma `topogenia` vide), contrôle immédiat « en retard », remontée `UpToDate` ; avec des données, SQL Server descend, SQLite refuse une carte détaillée (`SQLITE_CONSTRAINT` 19, migration initiale annulée, lignes gardées) et rend le verrou. Rouges avant correction en SQLite (3 : `NotSupportedException` de `DropForeignKey`, ligne de `__EFMigrationsLock` laissée). Mutation (`DropForeignKey` remis, `Down` d'origine) : rouge |
| A-5-28 | couvert | `Topogenia.Persistence.Tests.Sqlite.ConnectionSettingsTests.A_connection_opened_read_only_has_every_setting_but_the_journal_mode_which_stays_the_one_of_the_file` (en plus des tests déjà cités) | Connexion `Mode=ReadOnly` (celle par laquelle EF Core regarde si le fichier existe) : `foreign_keys`, `busy_timeout`, `synchronous`, délai de commande malgré la chaîne. Mutation « `if (ReadOnly(connection)) return;` avant les réglages » : rouge |
| S-5.2-09 | couvert (persistance) | `Topogenia.Persistence.Tests.Files.DesktopDataFileTests.A_file_reached_by_a_network_path_is_refused_with_its_share` (renforcé) ; `Topogenia.Persistence.Tests.Files.DesktopDataFileTests.A_relative_path_or_a_form_the_check_cannot_read_is_refused_with_a_code` ; `Topogenia.Persistence.Tests.Sqlite.BackupRetentionTests.A_backup_folder_through_a_device_of_windows_is_refused_by_its_code_and_never_thrown` | `\\?\GLOBALROOT\Device\Mup\...`, `\\.\globalroot\device\mup\...`, `\??\GLOBALROOT\Device\LanmanRedirector\...` refusés `FICHIER_SUR_PARTAGE` avec leur partage ; chemin relatif, volume, périphérique refusés `FICHIER_CHEMIN_NON_PRIS_EN_CHARGE`, sans exception (le message recopiait le chemin) ; dossier des sauvegardes de même. Mutations : périphérique réseau non lu, exception rendue : rouges. Niveaux : Hôte ; Phases : 4 (P4-T05) |
| S-5.2-12 | couvert (persistance) | `Topogenia.Persistence.Tests.Sqlite.BackupRetentionTests.A_backup_folder_through_a_device_of_windows_is_refused_by_its_code_and_never_thrown` (en plus des tests déjà cités) | Renvoi S-5.2-09 |

## Points sans test

- Sécurité m2 (droits NTFS d'un dossier hors de `%LOCALAPPDATA%`) : non contrôlés, limite documentée dans l'ADR 035
  (défaut de la spec 5.2), question pour le porteur du projet.
- Architecture m6 (`SqliteConnectionSettings.MaximumBusyWait` interne) : sans effet observable, les tests le lisent par
  `InternalsVisibleTo`.
- Architecture m7 (`DesktopDatabaseStartup` dans l'adaptateur) : écart nommé dans l'ADR 029, déplacement en P4-T05.
