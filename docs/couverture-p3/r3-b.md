<!-- r3-b.md -->
<!-- Couverture des corrections de la porte P3 round 3, agent B : infrastructure de test sous charge -->

# Couverture phase 3, porte P3 round 3 : infrastructure de test (ADR 040 et 029 révisées)

Constats traités (intégrité MAJ-1, m1, m2, m3 ; charge vue par les contrôleurs Architecture, Fidélité et Intégrité) :

1. `TransientFailures.Numbers` ne lisait pas les numéros que garde un refus `BASE_OCCUPEE` des migrations
   (`EngineFailureException`) : `PreparedAsync` et `CreatingAsync` ne rejouaient plus rien venant de `SchemaMigrations` ;
   les tests simulaient une `SqlException` brute.
2. La borne haute de `RealBusyTests` lisait `SqliteConnectionSettings.MaximumBusyWait` (constante du produit).
3. `RealBusyTests` hors borne sous la charge de cinq contrôleurs (33,3 s > 24,0 s ; 11,7 s > 10,3 s).
4. Base `<disparu>_<pid>_kept` jamais nettoyée (`Topogenia_Test_44032_15700_kept`, `Topogenia_Test_44032_42728_kept`).
5. `TestServer.ExistsAsync` (liste validée) en `Execution Timeout Expired` sous charge.
6. `ForeignKeyTests.Maps_and_steps_that_reference_each_other...(Sqlite)` : `UNIQUE constraint failed: app_user.login_normalized`.
7. Anciens noms de tests dans l'ADR 040 et trois fichiers de ce dossier.
8. Comptage de la persistance dans `dotnet test --solution`.

Préfixe des tests : `Topogenia.Persistence.Tests.`. À reporter dans `docs/couverture.md` : la colonne « Test 2.0 »
reçoit les tests ci-dessous (en plus de ceux déjà cités), la colonne « État » la valeur indiquée.

| ID | État | Test 2.0 | Remarque |
|---|---|---|---|
| A-5-31 | couvert | `Topogenia.Persistence.Tests.Infrastructure.TestInfrastructureTests.A_failure_of_the_instance_refused_by_the_migrations_is_passing_only_when_busy_with_a_passing_number` ; `Topogenia.Persistence.Tests.Infrastructure.TestInfrastructureTests.The_numbers_that_a_refusal_keeps_are_read_under_a_busy_database_only` ; `Topogenia.Persistence.Tests.Infrastructure.TestInfrastructureTests.A_base_exists_once_created_and_no_longer_once_a_creation_cancelled_at_any_moment_is_rolled_back` ; `Topogenia.Persistence.Tests.Infrastructure.TestInfrastructureTests.A_user_made_from_a_user_of_a_graph_never_takes_the_login_of_another_graph` (en plus des tests déjà cités ; `A_step_of_a_test_that_creates_its_base_runs_again_only_on_a_passing_failure_of_the_creation_that_left_no_base`, `A_step_of_a_test_that_creates_its_base_never_runs_again_once_the_base_exists`, `On_sql_server_a_preparation_that_fails_for_a_moment_starts_again_on_a_new_database`, `On_sql_server_the_bases_of_a_gone_process_are_removed_and_every_other_base_is_kept` et `Only_a_test_database_whose_process_is_gone_is_stale` réécrits) | Bases de test sous charge (ADR 040, révision du round 3) : numéros de `EngineFailureException` lus sous `BASE_OCCUPEE` seulement, jamais sous `VERROU_MIGRATIONS` ni un autre refus ; pannes produites par `FailingOpenings` et traduites par la vraie `SchemaMigrations` (et par `SqlServerSchemaCatalog.RefusalOf` / `NumbersOf`) ; existence d'une base lue par son ouverture (4060), qui n'attend que cette base, lectures rejouées bornées (`OfReading`) ; bases `Topogenia_Test_<pid>_<n>_<suffixe>` nettoyées, `finally` qui supprime chaque base ; logins dérivés par `_<étiquette>` (`ProjectGraph.OtherUser`). Mutations : numéros de `EngineFailureException` non lus, comportement d'avant (9 rouges, dont les deux sondes P21 reprises) ; code `BASE_OCCUPEE` ignoré (5 rouges) ; `ProcessOf` sans la partie ajoutée (3 rouges) ; 4060 lu comme base existante (rouge) ; utilisateur dérivé par un chiffre ajouté, `+ "2"` (rouge) |
| A-5-09 | couvert | `Topogenia.Persistence.Tests.Sqlite.RealBusyTests.On_sqlite_a_real_busy_database_is_retried_once_its_write_lock_is_released` ; `Topogenia.Persistence.Tests.Sqlite.RealBusyTests.A_write_lock_held_for_good_fails_the_unit_as_busy_after_three_attempts_within_the_documented_wait` (réécrits) | Valeurs de l'ADR 029 fixées dans le test (5 s, 1 s, 150 ms) ; délai de 1 s vérifié sur chaque connexion de l'unité ; attente de chaque essai mesurée sur sa connexion (ouverture, refus de `BEGIN IMMEDIATE`, fermeture, un seul thread), bornée par les pauses dormies en même temps + 1,15 s + 2 s ; borne de mur sous les 30 s par instruction d'avant l'ADR 029 (30 s, 90,15 s) ; essais, erreurs et pauses exacts. Mutations : `CommandTimeoutSeconds = 30` dans le produit (les deux tests rouges : délai 30 au lieu de 1 ; le contrôleur l'avait vu vert) ; `busy_timeout` 10 s dans le produit (les deux rouges : 11,1 s pour 8,7 s ; 33,3 s pour 22,2 s) |

## Mesures

| Exécution | Tests | Échecs | Durée |
|---|---|---|---|
| Classes visées (`TestInfrastructureTests`, `RealBusyTests`, `ForeignKeyTests`) | 89 | 0 | 245 s |
| Trois suites de persistance simultanées, premier essai | 1 018 × 3 | 0, 0, 0 | 2 318, 2 091, 1 865 s |
| Trois suites de persistance simultanées, second essai | 1 018 × 3 | 0, 0, 0 | 1 607, 1 746, 1 645 s |
| Solution entière (`dotnet test --solution`) | 2 996 | 2, hors périmètre | persistance 598 s |

Les deux échecs de la solution sont `Schema.MigrationDescentTests` (SQL Server, `SCHEMA_MODIFIE: 20261023174530_TextGuardsAndIntegerBounds`), une classe nouvelle d'un autre agent.

Comptage (point 8) : dans cette solution, la persistance compte tous ses tests. On a 2 996 exécutés pour 2 993
découverts par `--list-tests` dans les trois projets qui en déclarent (persistance 1 046, domaine 1 298,
architecture 649) ; les trois de plus sont des cas du domaine que la découverte ne détaille pas (le domaine en exécute
1 301), les petits projets (application, hôte, échange) n'en exécutent aucun (correction de la porte P3 round 4,
Intégrité m3). La construction du contrôleur Architecture (23/10, 22 h 13)
découvre 976 tests de persistance par `--list-tests` ; son exécution n'en a compté que 792, sans plantage signalé. Ce n'est pas
reproduit, le code de ce périmètre n'y est pour rien, et la cause n'est pas établie.

Bases laissées : `Topogenia_Test_44032_15700_kept` et `Topogenia_Test_44032_42728_kept` ont disparu au premier
lancement de la suite corrigée, par le nettoyage du démarrage, sans commande à la main. Aucune base `Topogenia_*` ni
aucun dossier `topogenia-test-*` de ces exécutions ne reste.

Noms corrigés (m3) : ADR 040 (révision des bases préparées) et `portage-b.md` (A-5-10, A-5-30, S-5.2-09) ;
`instantane.md` (A-2.2-28, A-5-10) ; `delai-migrations.md` (A-4-35).
