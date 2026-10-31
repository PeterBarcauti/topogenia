<!-- delai-migrations.md -->
<!-- Couverture de la correction du délai des migrations : attente du verrou et instructions hors du délai ordinaire (ADR 041) -->

# Couverture phase 3, délai des migrations (ADR 041)

Constat traité : `Schema.SchemaMigrationTests.Two_processes_that_migrate_at_once_apply_each_migration_once(kind: SqlServer)`
rouge une fois sur trois exécutions simultanées de la solution (`BASE_OCCUPEE` ← `Execution Timeout Expired` dans
`AcquireDatabaseLockAsync`). Décisions : ADR 041 (nouvelle), ADR 027 et ADR 040 révisées. Préfixe des tests :
`Topogenia.Persistence.Tests.` ; chaque test est une théorie exécutée sur SQLite et SQL Server.

| ID | État | Test 2.0 | Remarque |
|---|---|---|---|
| A-4-35 | couvert | `Topogenia.Persistence.Tests.Schema.SchemaMigrationTests.Two_processes_that_migrate_at_once_apply_each_migration_once` ; `Topogenia.Persistence.Tests.Schema.MigrationTimeoutTests.A_migration_waits_for_a_migration_elsewhere_longer_than_the_ordinary_command_timeout` ; `Topogenia.Persistence.Tests.Schema.MigrationTimeoutTests.A_lock_of_the_migrations_held_beyond_the_delay_of_the_migrations_is_refused_as_busy_or_as_left_where_it_outlives_its_holder` | Verrou des migrations d'EF Core, pris aussi par le contrôle ; l'attente du verrou a le délai des migrations (10 min par défaut, réglable par l'hôte) et non plus le délai ordinaire ; au-delà, `BASE_OCCUPEE` sur les deux moteurs (sur SQLite, EF Core attendait sans fin une ligne de verrou laissée par une migration arrêtée). Mutations : contextes ordinaires (rouge sur les deux moteurs, avec l'erreur du constat en SQL Server) ; attente non bornée (rouge en SQLite) |
| A-4-33 | couvert | `Topogenia.Persistence.Tests.Schema.MigrationTimeoutTests.Every_command_of_the_migration_and_of_the_check_has_the_delay_of_the_migrations_and_an_ordinary_context_keeps_its_own` (en plus des tests déjà cités dans `modele-migrations.md`) | Chaque commande et chaque connexion de la migration et du contrôle (instructions, verrou, catalogue, `BEGIN` SQLite) a le délai des migrations ; un contexte ordinaire garde le sien (ADR 029 inchangée). Mutations : contextes ordinaires (rouge sur les deux moteurs) ; délai de connexion SQLite retiré (rouge en SQLite) |
| A-4-38 | à faire | `Topogenia.Persistence.Tests.Schema.MigrationTimeoutTests.A_migration_waits_for_a_migration_elsewhere_longer_than_the_ordinary_command_timeout` (part persistance, en plus des tests déjà cités) | Un second démarrage pendant une migration attend sa fin au lieu d'échouer. Le passage du délai par l'hôte (`--migrate`) et le message de `BASE_OCCUPEE` restent. Niveaux : Persistance, Hôte ; Phases : 3, 4 (P4-T05, P4-T09) |

## Mesures

| Exécution | Tests | Échecs | Durée |
|---|---|---|---|
| Nouveaux tests avant correction | 6 | 6 | - |
| Solution entière après correction | 2699 (architecture 532, domaine 1299, persistance 868) | 0 | 341 s |
| Trois suites de persistance simultanées (trois processus), premier essai | 868 × 3 | 0, 1, 0 | 689, 699, 727 s |
| Trois suites de persistance simultanées (trois processus), second essai | 868 × 3 | 0, 0, 0 | 724, 735, 739 s |

L'échec du premier essai est hors du périmètre de cette correction :
`Topogenia.Persistence.Tests.Collation.CollationMigrationTests.A_database_in_use_migrates_to_binary_text_columns_and_reverts_to_its_previous_schema`,
`Execution Timeout Expired` dans `SqlServerDatabaseCreator.CreateAsync` (création de base par `IMigrator.MigrateAsync`
sur un contexte ordinaire, délai de 120 s des tests). Ce test crée ses deux bases par le migrateur d'EF Core sans
passer par `TestDatabase.CreatingAsync` (ni sémaphore de création ni nouvel essai sur panne passagère, ADR 040) et sans
le contexte des migrations. Correction proposée (fichier hors de cette mission) : dans `CollationMigrationTests.MigrateAsync`,
ouvrir le contexte par `database.Engine.CreateMigrationContext()` et faire passer la première migration de chaque base
par `database.CreatingAsync`.

Aucune base `Topogenia_*` ni aucun dossier `topogenia-test-*` ne reste après les exécutions.
