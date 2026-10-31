<!-- r2-a.md -->
<!-- Couverture de la correction A de la porte P3, round 2 : empreinte du schéma SQL Server et refus au démarrage -->

# Couverture phase 3, porte P3 round 2, correction A

Constats traités : Sécurité MAJ-1, MAJ-2, m1, m4 ; Fidélité MAJ-1, m1, m3 ; Intégrité m1, m2 ; Architecture m5. Plus,
à la demande du chef de projet : `Startup.SqliteRebuildTests` réécrit sur la vraie reconstruction (migration SQLite
`TextGuardsAndIntegerBounds`). Décisions : révisions des ADR 036, 037, 041. Préfixe : `Topogenia.Persistence.Tests.`,
écrit en entier ci-dessous. À reporter dans `docs/couverture.md` : la colonne « Test 2.0 » reçoit les tests ci-dessous
en plus de ceux déjà cités ; un test renommé remplace l'ancien nom partout.

Renommages :

- `Startup.StartupErrorTests.A_missing_database_is_reported_behind_never_created_and_refused_to_a_connection_with_4060_and_18456`
  devient `Startup.StartupErrorTests.A_missing_database_is_refused_as_unreachable_by_the_check_never_created_and_refused_to_a_connection_with_4060_and_18456` ;
- `Schema.MigrationTimeoutTests.A_lock_of_the_migrations_held_beyond_the_delay_of_the_migrations_is_refused_as_busy`
  devient `Schema.MigrationTimeoutTests.A_lock_of_the_migrations_held_beyond_the_delay_of_the_migrations_is_refused_as_busy_or_as_left_where_it_outlives_its_holder` ;
- `Startup.SqliteRebuildTests.A_change_of_a_constraint_of_the_steps_rebuilds_the_table_and_keeps_every_row_index_and_guard`
  devient `Startup.SqliteRebuildTests.The_migrations_after_the_initial_one_rebuild_the_tables_and_keep_every_row_index_and_foreign_key_with_the_guards_of_the_model`.

| ID | État | Test 2.0 | Remarque |
|---|---|---|---|
| A-4-36 | couvert | `Topogenia.Persistence.Tests.Schema.CatalogChangeTests.A_view_of_the_report_schema_named_as_the_history_of_ef_core_reads_the_users_for_the_reports_and_is_refused` ; `Topogenia.Persistence.Tests.Schema.CatalogChangeTests.The_users_given_to_the_report_role_as_their_owner_are_read_by_the_reports_and_refused` ; `Topogenia.Persistence.Tests.Schema.CatalogChangeTests.An_object_or_a_right_added_by_hand_is_refused_as_a_changed_schema` (six cas de plus) | Ajouter : « Seule la table `topogenia.__EFMigrationsHistory` est écartée, à la casse exacte (une vue `bi.__efmigrationshistory` qui lit `app_user` était acceptée et lue par les rapports) ; propriétaire posé sur un objet (`W`), tout schéma hors système et son propriétaire (`S`), objet marqué livré hors de `sys` (`X`, `P`), `TRUSTWORTHY` et `DB_CHAINING` posés (`B`, preuve à la main : la règle A-5-12 interdit l'instruction dans les sondes) décrits ; tous les changements refusés aussi sous le compte de l'application, qui tient `VIEW DEFINITION` sur la base (ADR 037 révisée). Mutations : ancienne exclusion (3 rouges), ligne `W` (3), ligne `S` (1), filtre `is_ms_shipped` dans `X` (1), dans `X` et `P` (2) » |
| A-5-10 | couvert (persistance) | `Topogenia.Persistence.Tests.Startup.StartupErrorTests.A_missing_database_is_refused_as_unreachable_by_the_check_never_created_and_refused_to_a_connection_with_4060_and_18456` (renommé) ; `Topogenia.Persistence.Tests.Startup.StartupErrorTests.The_check_and_the_migration_run_by_an_account_that_cannot_read_the_histories_are_refused_for_its_rights` ; `Topogenia.Persistence.Tests.Startup.StartupErrorTests.The_failures_of_a_start_are_read_as_an_unreachable_database_or_as_missing_rights` | Remplacer « Une base absente reste « en retard » » par : « En SQL Server, le contrôle refuse une base absente `BASE_INACCESSIBLE` : EF Core répond « absente » sur 4060, réponse identique pour un login sans utilisateur dans une base existante, que rien ne distingue sans lire `master` ; défaut de la 1.x (« base non disponible », ADR 036 révisée) ; `--migrate` crée toujours la base. L'exception interne d'un refus ne garde que les numéros du moteur (`EngineFailureException`), jamais le message, qui nomme la base et le login (spec 5.2) ; règle de journalisation de l'hôte dans l'ADR 036. Corrigé : les commentaires prêtaient à la 1.x la lecture de 229, 4060 et de 1802 seul ; elle lisait 18456, 262, 1802, par le premier numéro. L'ouverture de la base absente du dernier test est rouverte, bornée, sur un échec sans réponse du serveur (`TransientFailures.Unanswered`), comme le test voisin. Mutations : `AbsentDatabaseRefusal` nul (1 rouge), exception du moteur gardée (6 rouges) » |
| A-5-10 | couvert (persistance) | `Topogenia.Persistence.Tests.Startup.SqliteStartupErrorTests.The_result_code_of_sqlite_and_its_extended_code_are_read_as_one_refusal` (22 cas) | Remplacer « code étendu non lu (1 rouge) » par « code étendu non lu (3 rouges : code principal `SQLITE_ERROR` 1 avec 261, 526, 1032 ; l'ancienne annonce « 1 rouge » était fausse, la mutation restait verte) » |
| A-4-35 | couvert | `Topogenia.Persistence.Tests.Schema.MigrationTimeoutTests.A_lock_of_the_migrations_held_beyond_the_delay_of_the_migrations_is_refused_as_busy_or_as_left_where_it_outlives_its_holder` (renommé) ; `Topogenia.Persistence.Tests.Startup.TransientStartupTests.Only_a_contention_is_transient` | Ajouter : « Mode Poste : un verrou des migrations (ligne de `__EFMigrationsLock`) non obtenu dans le délai donne `VERROU_MIGRATIONS`, non passager : la ligne laissée par une migration arrêtée ne disparaît pas seule, un hôte qui rejouait `BASE_OCCUPEE` bouclait. SQL Server garde `BASE_OCCUPEE`, passager (verrou de session). Correction du contrat nommée (ADR 041 révisée). Mutation : `LockOutlivesItsHolder` faux en SQLite (1 rouge) » |
| A-5-30 | couvert | `Topogenia.Persistence.Tests.Startup.SqliteRebuildTests.The_migrations_after_the_initial_one_rebuild_the_tables_and_keep_every_row_index_and_foreign_key_with_the_guards_of_the_model` (renommé) | Remplacer la remarque du test de reconstruction par : « Fichier à la migration initiale (inscrit comme par la version précédente), projet complet écrit, migration jusqu'au bout par le code testé : chaque ligne relue égale, chaque table garde ses index (partiels compris), sa clé et ses clés étrangères, ses CHECK sont exactement ceux du modèle ; la table `step` a été reconstruite ; gardes unique, clé étrangère et CHECK toujours actives. La phrase « aucune migration ne change encore une contrainte » est périmée (migration `TextGuardsAndIntegerBounds`) » |

## Ce qui reste hors de ce périmètre

- `Schema.SchemaMigrationTests.A_new_database_is_behind_on_sqlite_and_refused_as_unreachable_on_sql_server_and_the_check_never_creates_it`
  (SQL Server) attend « en retard » pour une base absente : avec la décision de l'ADR 036 révisée, le contrôle la
  refuse `BASE_INACCESSIBLE`. Correction du contrat à reporter dans ce test (cas SQL Server : refus `BASE_INACCESSIBLE`,
  base toujours non créée ; cas SQLite inchangé).
- `src/Topogenia.Application/Ports/ISchemaMigrations.cs` : la documentation de `SchemaStatus` (« behind: ... a new
  database too ») et de `MigrateAsync` (« after the same refusals as CheckAsync ») ne dit pas que le contrôle SQL Server
  refuse une base absente que la migration crée.
