<!-- 040-bases-de-test-sous-charge.md -->
<!-- Création, ouverture et suppression des bases de test sous la charge de plusieurs exécutions, nettoyage des bases laissées -->

# 040 - Bases de test sous charge et nettoyage au démarrage

Statut : acceptée (date 23/10/2026)

## Contexte

La porte P3 round 1 a fait tourner la suite de persistance par cinq contrôleurs à la fois sur la même instance
SQL Server : 70 à 313 échecs, alors que `-maxThreads 2` passait. Trois exécutions simultanées refaites avant
correction : 173, 146 et 142 échecs (environ 270 s chacune). Causes :

1. `CREATE DATABASE` prend le verrou exclusif de `model` ; les créations et suppressions de toutes les exécutions
   se mettent en file (`LCK_M_X`), d'où 1807 (accompagné de 1802), des délais de 30 s dépassés et des connexions
   rompues (233, 10054) ;
2. `MigratedDatabases.OfAsync` rangeait la base **avant** sa migration : une création échouée faisait tomber
   toute la classe en 4060 (environ 130 échecs en cascade sur 139) ;
3. `EnsureDeleted` vide **tous** les pools du processus (`SqlConnection.ClearAllPools`), ce qui coupe les
   connexions des autres tests, et une ouverture échouée bloque le pool de sa base quelques secondes
   (`PoolBlockingPeriod`) ;
4. les fabriques de bases des tests de schéma laissaient la base quand leur préparation échouait, la suppression
   des bases d'une classe s'arrêtait au premier échec, et rien ne retirait les bases `Topogenia_Test_<pid>_*` ni
   les dossiers SQLite d'un processus arrêté.

La règle du prompt 4-2 reste : seules les bases `Topogenia_*` sont créées ou supprimées, et un test n'est jamais
affaibli. Le code de production n'est pas en cause : la 1.x, comme la 2.0, crée une seule base par démarrage.

## Décision

- **`Infrastructure/TestServer.cs`** : l'instance des tests (`TEST_SQLSERVER_CONNECTION` ou locale). Délai de
  commande 120 s (au lieu de 30) et d'ouverture 60 s (au lieu de 15), `PoolBlockingPeriod=NeverBlock`. Création
  et suppression par les opérations du générateur d'EF Core (`SqlServerCreateDatabaseOperation`,
  `SqlServerDropDatabaseOperation` : les tests n'écrivent pas de SQL, spec 3.2), exécutées sur la base par défaut
  du compte ; la suppression vide le pool de **cette** base seulement. Au plus **2** créations ou suppressions à la
  fois dans un processus (sémaphore) ; chacune est rejouée sur une panne passagère.
- **Pannes passagères** (`Infrastructure/TransientFailures.cs`), lues dans toutes les erreurs de toutes les
  `SqlException` de la chaîne : création 1807, -2 (délai), 1205, connexion coupée (233, 10053, 10054, 64, 121) ;
  ouverture juste après la création 4060, -2, connexion coupée ; suppression 3702, 1807, -2, 1205, connexion
  coupée. **1802 seul n'est jamais passager** (il signale un vrai refus : droits, disque, chemin). Au plus 8 essais,
  250 ms doublés à chaque fois, 8 s au plus (environ 30 s en tout). Seules la création, la première ouverture et la
  migration de préparation d'une base neuve sont rejouées, **jamais une instruction d'un test** : une panne d'un
  test reste un échec.
- **Préparation** (`TestDatabase.CreatedAsync`, `MigratedAsync`, `PreparedAsync`) : la base est créée vide, puis
  amenée à l'état de départ du test par le code sous test (migrations) ; une préparation qui échoue supprime sa base
  avant de remonter l'échec ; en SQL Server, une panne passagère recommence sur une base neuve. Les tests dont le
  sujet est la création par la migration (`SchemaMigrationTests`) passent par `CreatingAsync`, qui rejoue la
  création du code sous test sur une panne passagère après avoir supprimé ce qu'elle a créé.
- **Fixture de classe** (`MigratedDatabases`) : une base n'est rangée **qu'une fois migrée** ; un échec fait
  échouer le test qui l'a demandée, le suivant repart d'une base neuve ; toutes les bases sont supprimées à la
  fin même si l'une échoue (échecs levés ensemble ensuite).
- **Nettoyage au démarrage** (`Infrastructure/StaleDatabases.cs`) : avant sa première création, chaque processus
  supprime les bases dont le nom est exactement `Topogenia_Test_<pid>_<n>` (chiffres ASCII) et dont le processus ne
  tourne pas sur cette machine ; et, avant sa première base SQLite, les dossiers
  `%TEMP%\topogenia-test-<pid>-<aléa>` d'un processus arrêté (le numéro du processus est désormais dans le nom du
  dossier). Aucune autre base ni aucun autre dossier n'est touché ; une base d'un processus vivant (autre exécution)
  est gardée ; le nettoyage des bases ne se fait que sur une instance locale (`.`, `(local)`, `localhost`, boucle
  locale, nom de la machine), jamais sur une instance distante partagée, où un numéro de processus ne désigne rien.
- **Tests de course** : `Rendezvous` attend 60 s en SQL Server (les unités s'y rencontrent toujours) et 2 s en
  SQLite (les transactions immédiates ne les laissent jamais se rencontrer) ; son message dit combien d'unités sont
  arrivées et se sont rencontrées. Les pauses des tests de lecture cohérente et de verrou attendent l'événement
  qu'elles prouvent (validation de l'écriture concurrente, demande du verrou) et vérifient qu'il a eu lieu.

## Conséquences

- La suite de persistance reste verte avec plusieurs exécutions complètes simultanées sur une instance (mesures
  dans `docs/couverture-p3/r1-c3.md`).
- Un test plus lent sous charge ne devient pas rouge pour autant ; un test bloqué échoue après 120 s au lieu de 30.
- Une base ou un dossier laissé par un processus tué disparaît à l'exécution suivante sur la même machine.

## Vérification

`tests/Topogenia.Persistence.Tests/Infrastructure/TestInfrastructureTests.cs` (classification des pannes, 1802
seul refusé, nouvel essai borné, base supprimée après une préparation échouée, nouvelle base après une panne
passagère, fixture qui ne garde qu'une base migrée, nettoyage des bases et des dossiers) ; mutations dans
`docs/couverture-p3/r1-c3.md` ; trois exécutions simultanées de la suite de persistance et deux de la solution.

## Révision (23/10/2026) : migrations des tests

Le délai de commande de 120 s de `TestServer` ne borne plus les migrations ni l'attente de leur verrou : les contextes
de `SchemaMigrations` ont le délai des migrations (10 minutes par défaut, ADR 041). Sous la charge de trois
exécutions de la solution, `Two_processes_that_migrate_at_once_apply_each_migration_once` (SQL Server) avait échoué
une fois sur trois, le second processus attendant plus de 120 s la fin des huit migrations du premier. Le test n'est
pas modifié.

## Révision (23/10/2026) : bases préparées par le migrateur d'EF Core

Contexte : trois suites de persistance simultanées, `CollationMigrationTests.A_database_in_use_migrates_to_binary_text_columns_and_reverts_to_its_previous_schema`
a échoué une fois en `Execution Timeout Expired` dans `SqlServerDatabaseCreator.CreateAsync`. Le test créait ses deux
bases par `IMigrator.MigrateAsync` sur un contexte ordinaire : création hors du sémaphore et sans nouvel essai (décision
ci-dessus), migrations avec le délai ordinaire des tests (120 s) au lieu de celui des migrations (ADR 041). Trois autres
préparations migraient vers une migration donnée sur un contexte ordinaire : `AccessMigrationTests` (base à la migration
initiale, retour à la migration initiale) et `SnapshotIsolationTests` (base aux rôles et vues, retour aux rôles et vues),
leurs bases déjà créées par `PreparedAsync`.

Décision : toute migration d'un test par le migrateur d'EF Core ouvre son contexte par
`DatabaseEngine.CreateMigrationContext()` ; une base qu'un test prépare ainsi est créée par `TestDatabase.PreparedAsync`
(création bornée et rejouée, puis migration de préparation rejouée sur une base neuve en cas de panne passagère). Les
migrations qui sont le sujet du test (vers la dernière, retour à la précédente, puis de nouveau la collation) restent
des instructions du test, jamais rejouées. Aucune assertion n'est changée.

Écart nommé, une seule ouverture d'un test : `StartupErrorTests.A_missing_database_is_refused_as_unreachable_by_the_check_never_created_and_refused_to_a_connection_with_4060_and_18456`
a échoué deux fois sur deux essais de trois suites simultanées (une suite sur trois à chaque fois) : la connexion à la
base absente rendait 233 seul (connexion coupée par le serveur avant que sa réponse arrive) au lieu de 4060 et 18456,
après les trois essais de l'unité. Le sujet du test est la réponse du serveur à cette ouverture ; un échec sans aucune
réponse du serveur (tous ses numéros sont des connexions coupées : `TransientFailures.Unanswered`) ne dit rien de
cette réponse. Ce seul échec fait rouvrir, au plus `TransientFailures.Attempts` fois avec les pauses de la préparation ;
la première réponse du serveur, quelle qu'elle soit, est celle vérifiée, et l'assertion (4060 et 18456) est inchangée.
Toute autre panne d'un test reste un échec. Vérification : `TestInfrastructureTests.Only_a_failure_made_of_cut_connections_is_without_an_answer_of_the_server`
(mutation : une seule connexion coupée suffit, `Any` au lieu de `All` : 4060 + 233 redevient sans réponse, rouge).

Vérification : trois suites de persistance simultanées, chacune avec son dossier d'artefacts
(`docs/couverture-p3/occupation-sqlite.md`).

## Révision (23/10/2026) : nettoyage, budget des essais, Query Store, options et attentes mesurées

Contexte : porte P3 round 2, plusieurs exécutions simultanées (une suite de persistance a duré 1 526 s). Constats et
causes, vérifiées sur l'instance locale :

1. **Nettoyage au démarrage.** `TestServer` gardait dans un `Lazy<Task>` l'échec de `StaleDatabases.DropAsync` pour tout
   le processus : 400 échecs sur 898, la même exception relancée à chaque création. La liste des bases
   (`GetSchemaAsync("Databases")`, lue dans `master..sysdatabases`) attend en `LCK_M_S` la fin de toute création en
   cours : un `CREATE DATABASE` garde la ligne de sa base jusqu'à sa fin (reproduit : création avec un journal de
   3 Go, lecture bloquée 1,9 s puis libérée à la fin exacte de la création ; `sys.databases` attend aussi, même avec
   `NOLOCK` ou en `READ UNCOMMITTED` : erreur 1222 sous `LOCK_TIMEOUT`). Seule la lecture non validée de
   `sysdatabases` répond aussitôt.
2. **Suppression de l'isolation d'instantané à la main.** `ALTER DATABASE CURRENT SET ALLOW_SNAPSHOT_ISOLATION OFF`
   attend que se terminent toutes les transactions qui écrivent, au moment où il commence, dans une base versionnée
   **de l'instance** (autre base comprise ; une transaction d'une base non versionnée ne le retient pas, une transaction
   commencée après non plus). Ni le pool ni une autre connexion du test : ce sont les transactions des autres tests et
   des autres exécutions. `WITH ROLLBACK IMMEDIATE` est refusé par SQL Server sur ce changement (5083 « l'option de fin
   n'est pas prise en charge lorsque des modifications d'état de versioning sont apportées »), et il ne servirait à
   rien : les sessions de la base ne sont pas celles qu'il attend. Le passage à `ON` n'attend pas les autres bases.
3. **Suppression d'une base.** 12408 (« une opération de lecture/écriture dans le Magasin des requêtes a échoué »,
   avec 5069) pendant le `SET SINGLE_USER WITH ROLLBACK IMMEDIATE` de l'opération d'EF Core. Le journal de l'instance
   montre, sur de nombreuses bases de test, « The Query Store ... has an invalid structure for internal table
   plan_persist_plan » écrit par une tâche de fond, suivi de la mise en `SINGLE_USER` puis de la suppression de la base
   au passage suivant ; aucune base de test n'est restée sur l'instance. La panne est passagère : un nouvel essai
   supprime la base.
4. **Nouvel essai de l'opération d'un test.** `CreatingAsync` (`SchemaMigrationTests.Migrating_creates_...`,
   `Migrating_an_up_to_date_...`) rejouait toute la migration sous test sur une panne passagère : une migration
   intermittente aurait été cachée. Et les essais s'imbriquaient (`PreparedAsync` : 8 essais, chacun avec 8 essais de
   création, d'ouverture et de suppression), loin des « environ 30 s » écrits plus haut.
5. **Attentes de SQLite mesurées à l'horloge** (`RealBusyTests`) : 8,797 s pour une borne de 8,15 s, 26,31 s pour 20,6 s.
   Les 5 s de `busy_timeout` sont la somme des pauses que demande le gestionnaire d'occupation de SQLite sous Windows
   (`sqliteDefaultBusyCallback` : 1, 2, 5, 10, 15, 20, 25, 25, 25, 50, 50 ms puis 100 ms, la dernière coupée, 59 pauses
   pour 5 s) : SQLite compte le temps demandé, jamais le temps dormi. Sur une machine chargée chaque pause finit plus
   tard, au réveil de son thread : 8,8 / 5 et 26,3 / 15,2 donnent le même étirement d'environ 1,75.
6. Les dossiers `topogenia-sqlite-*` (`SqliteFiles`) et `topogenia-files-*` (`DataFileResolutionTests`) ne portaient
   pas le numéro du processus : un processus tué les laissait pour toujours.

Décision :

- **Nettoyage sans échec** : `TestServer.QuietlyAsync` lance le nettoyage borné à `CleaningBound` (60 s) ; un échec, une
  annulation ou un nettoyage encore en cours à la borne (annulé, plus attendu) est laissé à l'exécution suivante et ne
  fait échouer aucun test. La liste des bases à nettoyer est lue sans attendre les créations
  (`TestServer.DatabasesWithoutWaitingAsync`) : `GetSchemaAsync("Databases")` sur une connexion hors du pool dont la
  session est passée en `READ UNCOMMITTED` (le niveau d'une transaction reste celui de la session après elle). Aucun
  texte SQL dans les tests (ADR 025) ; la règle A-5-12 (`SourceRulesTests.The_application_needs_neither_the_system_database_nor_the_server_procedures`)
  ne lit que `src`, et rien de ceci n'y est. La vérification d'existence de `CreateAsync` et `DropAsync` garde la
  lecture validée : elle doit voir la fin d'une création ou d'une suppression annulée.
- **Isolation d'instantané retirée à la main** (`SnapshotIsolationTests`) : sur un contexte des migrations, avec leur
  délai (10 minutes, ADR 041), comme la migration qui la pose ; la sonde ne change pas (5083).
- **12408 passager à la suppression** seulement (`TransientFailures.OfRemoval`). Couper le Query Store des bases de
  test à leur création a été écarté : le Query Store est actif par défaut sur SQL Server 2022 et rien ne le coupe en
  production, une base de test doit rester une base ordinaire ; et les bases que le code sous test crée lui-même
  (`CreatingAsync`) y échapperaient.
- **Seule la création est rejouée** dans `CreatingAsync` : sur une panne passagère de création
  (`TransientFailures.OfCreation`) qui n'a laissé aucune base (lecture validée), le pas recommence ; dès que la base
  existe, l'opération que le test vérifie a commencé et toute panne remonte telle quelle.
- **Budget commun des essais** : les essais imbriqués (`RetriedAsync`, `PreparedAsync`, `CreatingAsync`) partagent le
  budget du plus externe, `TransientFailures.Budget` = 10 minutes (le délai des migrations, ADR 041) ; aucun essai ne
  commence quand sa pause finirait après ce budget. Une préparation dure au plus ce budget et son dernier essai. Les
  pauses d'une série de 8 essais font 23,75 s (et non « environ 30 s »).
- **Attentes de SQLite** : la borne haute de `RealBusyTests` est la même formule que l'ADR 029, où les 5 s de SQLite
  sont remplacées par la durée des mêmes pauses dormies au même moment sur un thread du test (`BusySleeps`,
  `SleptAsync`), plus la part du fournisseur (1,15 s par instruction) et la marge de 2 s. La borne basse (5 s, 15,15 s)
  reste : les pauses demandées font au moins le délai. Sur une machine au repos la borne vaut celle de l'ADR 029 ; elle
  reste toujours très au-dessous des 34 s et 103 s de la configuration d'avant l'ADR 029, et le nombre d'essais, leur
  erreur et leurs pauses (50 et 100 ms) sont vérifiés exactement. `The_sleeps_of_the_busy_handler_of_sqlite_sum_to_its_busy_timeout`
  fixe le plan des pauses.
- **Dossiers temporaires** : `topogenia-test-<pid>-sqlite-<aléa>` et `topogenia-test-<pid>-files-<aléa>`, nettoyés comme
  ceux des bases SQLite.

Vérification : `TestInfrastructureTests` (12408, budget imbriqué, création rejouée seulement sans base, nettoyage qui
échoue ou dure, liste sans attente, dossier des fichiers SQLite), `SnapshotIsolationTests.Removing_the_snapshot_isolation_by_hand_waits_for_the_writes_in_progress_in_another_versioned_database`,
`RealBusyTests` ; mutations et mesures sous charge dans `docs/couverture-p3/r2-b.md`.

## Révision (24/10/2026) : pannes traduites par les migrations, existence d'une base, bases suffixées, attentes de SQLite

Contexte : porte P3 round 3, cinq contrôleurs en même temps sur l'instance locale. Constats et causes :

1. **Pannes traduites.** `SchemaMigrations` remplace une panne passagère du moteur par
   `SchemaRefusedException(BASE_OCCUPEE)`, dont l'exception interne `EngineFailureException` ne garde que les numéros
   (ADR 036). `TransientFailures.Numbers` ne lisait que les `SqlException` : `PreparedAsync` et `CreatingAsync` ne
   rejouaient plus aucune panne venue des migrations, et leurs tests simulaient une `SqlException` brute que le code ne
   produit plus (sonde P21 du contrôleur : rouge).
2. **Lecture de l'existence d'une base.** `ExistsAsync` lisait la liste validée (`GetSchemaAsync("Databases")`) et
   dépassait son délai de 120 s sous charge (`CreatingAsync`, nettoyage). La requête du fournisseur est
   `select … from master..sysdatabases where (name = @Name or (@Name is null))` : un parcours qui attend en `LCK_M_S` la
   ligne de toute création en cours sur l'instance, avec un nom ou sans. Mesuré derrière une création lente : 11,1 s
   pour cette requête, 10,8 s avec `LIKE`, 54 ms pour une recherche `name = @n`, qu'aucune API sans texte SQL n'offre
   (ADR 025). Sous les créations continues de plusieurs exécutions, le parcours rencontre une nouvelle ligne après l'autre.
3. **Base `_kept`.** Le test du nettoyage créait `Topogenia_Test_<disparu>_<pid>_kept`, qu'aucun nettoyage ne reconnaissait :
   un processus arrêté pendant le test la laissait pour toujours (deux bases restées), et le `finally` ne la supprimait pas
   quand la suppression de l'autre échouait.
4. **Attentes de SQLite sous charge.** `RealBusyTests` a mesuré 33,3 s pour une borne de 24,0 s, et 11,7 s pour
   10,3 s. Les pauses dormies par le test n'étaient étirées que de 1,23 et 1,42 : le reste n'est pas l'attente de SQLite.
   Entre deux instructions, la suite asynchrone d'une unité attend un thread libre du pool, que les instructions
   synchrones de SQLite des autres tests occupent, 5 s chacune quand elles attendent un verrou. De plus, la borne haute
   lisait `SqliteConnectionSettings.MaximumBusyWait`, une constante du produit : avec `CommandTimeoutSeconds = 30`, le
   test restait vert.
5. **Logins de test.** `ForeignKeyTests` dérivait un utilisateur par `login + "2"` : `Owner1` devient `Owner12`, le
   propriétaire du graphe 12, écrit dans la même base de classe (`UNIQUE constraint failed: app_user.login_normalized`).

Décision :

- **Pannes traduites lues sous `BASE_OCCUPEE` seulement.** `TransientFailures.Numbers` lit les numéros de
  `EngineFailureException` quand le refus est `BASE_OCCUPEE`. Sous tout autre refus (`VERROU_MIGRATIONS`,
  `BASE_INACCESSIBLE`, `DROITS_INSUFFISANTS`, refus du schéma), il ne lit aucun numéro : le code dit ce qu'est la panne,
  et aucun refus n'est rejoué. Les numéros décident ensuite comme avant : par exemple, 1222 sous `BASE_OCCUPEE` n'est pas
  une panne de création. Les tests produisent la panne comme le code la rencontre. `FailingOpenings` fait échouer
  l'ouverture des connexions des migrations avec les numéros de l'instance, et c'est la vraie `SchemaMigrations` qui la
  traduit. Le catalogue (`SqlServerSchemaCatalog.RefusalOf`, `NumbersOf`) donne le même refus.
- **Existence d'une base par son ouverture.** `TestServer.ExistsAsync` ouvre la base elle-même, hors du pool et sans
  le nouvel essai du pilote. 4060 dit qu'elle n'existe pas ; toute autre réponse dit qu'elle existe, 924 compris (base
  gardée par son seul utilisateur). L'ouverture n'attend que la création ou la suppression de cette base, jamais celle
  d'une autre. Mesuré pendant une création lente : 22 ms pour ouvrir une autre base ; la base en création s'ouvre à la
  fin de sa création ; après une création annulée, 4060. L'ouverture voit donc la fin d'une création annulée, comme la
  lecture validée. Une lecture qui échoue pour un instant (ouverture ou liste ; `-2` ou connexion coupée :
  `TransientFailures.OfReading`) est refaite, dans la limite des essais et du budget. La liste validée
  (`DatabasesAsync`) ne sert plus qu'au test qui la compare à la liste lue sans attendre.
- **Bases suffixées.** Un nom de base de test peut porter une partie de plus, en lettres et chiffres ASCII
  (`Topogenia_Test_<pid>_<n>_kept`). Le nettoyage la reconnaît par son numéro de processus, comme les autres.
  `Topogenia_Test_<disparu>_1_2` est donc désormais nettoyée : la règle change, ce cas de la théorie aussi. Le test du
  nettoyage crée deux bases d'un processus disparu, avec et sans suffixe. Il vérifie que les deux sont supprimées, que la
  base de son processus reste, et que les bases qu'aucun test ne touche (hors `Topogenia_*`) restent. Son `finally`
  supprime chaque base, quoi qu'il arrive à l'autre. Les deux bases laissées ont disparu au lancement suivant, par ce
  nettoyage.
- **Attentes de SQLite.** Les valeurs de l'ADR 029 sont fixées dans le test : 5 s, 1 s et 150 ms. Le test prouve
  exactement ce que SQLite et le fournisseur garantissent : le délai posé sur chaque connexion de l'unité (1 s), le
  nombre d'essais, leur erreur et leurs pauses (50 et 100 ms), et le plan des 59 pauses. L'attente d'un essai est mesurée
  là où SQLite attend, de l'ouverture de la connexion de l'essai à sa fermeture après le refus de `BEGIN IMMEDIATE`. Cela
  se passe sur un seul thread (intercepteur de connexion), sans les attentes du pool. Elle est bornée par les mêmes
  pauses dormies au même moment par le test, plus 1,15 s par instruction et 2 s de marge ; la borne basse reste 5 s par
  essai.
- **Borne de mur large.** Les durées lues à l'horloge du test comprennent les attentes du pool : on ne peut pas les
  borner de façon fiable sous charge. Elles gardent une borne large mais significative : les 30 s par instruction du
  fournisseur d'avant l'ADR 029, soit 30 s pour un essai et 90,15 s pour trois. La configuration d'avant les dépassait
  toujours (34 s et 103 s mesurés).
- **Logins dérivés.** `ProjectGraph.OtherUser` dérive un utilisateur par `_<étiquette>`. Un login de graphe finit par le
  numéro de son graphe : un chiffre ajouté donnerait le login d'un autre graphe.

Vérification : `TestInfrastructureTests` (panne refusée par les migrations, numéros lus sous `BASE_OCCUPEE` seulement,
création rejouée, préparation recommencée, existence après une création annulée, nettoyage des bases suffixées, logins
dérivés) et `RealBusyTests`. Les mutations et les mesures sous charge (trois suites de persistance simultanées, deux
fois) sont dans `docs/couverture-p3/r3-b.md`.

## Révision (25/10/2026, round 2) : attentes de SQLite sous la charge de la porte P4

Contexte : porte P4, rounds 1 et 2, cinq contrôleurs en même temps (parfois une compilation ou une suite de mutation en
plus). `RealBusyTests` a échoué à chaque round sur sa borne haute, mesurée à l'intercepteur de connexion, puis passé à la
relance seule. Mesures, rapportées à l'attente documentée d'un essai (ADR 029 : 5 s + 1 s + 150 ms = 6,15 s) :

| Porte | Un essai (borne) | Trois essais (borne) | Étirement au pire |
| --- | --- | --- | --- |
| P3 round 2 | 8,80 s (8,15 s) | 26,31 s (20,6 s) | 1,43 |
| P3 round 3 | 11,7 s (10,3 s) | 33,3 s (24,0 s) | 1,90 |
| P4 round 1 | 9,58 s (9,12 s) | 35,6 s (24,7 s) ; 45,32 s (23,87 s) | 2,46 |
| P4 round 2 | 15,12 s (9,68 s) | 34,27 s (25,16 s) ; 31,69 s (26,66 s) ; 41,60 s (34,58 s) | 2,46 |

La borne de la révision du 24/10 (pauses dormies au même moment par le test, plus 1,15 s par instruction et 2 s) suit
l'étirement des pauses, pas l'attente d'un thread libre : les deux causes nommées le 24/10 (point 4) restent, la seconde
venant des autres tests du même processus comme des autres exécutions.

Décision :

- **Classe seule** : `RealBusyTests` est dans la collection `RealBusyCollection` (`DisableParallelization = true`). xUnit
  la lance après les collections parallèles de son processus, sans aucun autre test : les instructions synchrones de
  SQLite des autres tests de la suite n'occupent plus le pool pendant la mesure. La charge des autres processus reste.
- **Borne haute en multiple du délai réglé** : le temps ouvert de n essais est au plus **4 × n × 6,15 s** (24,6 s pour un
  essai, 73,8 s pour trois). Le facteur 4 vient de deux bornes : l'étirement le plus fort mesuré, 2,46 (P4 rounds 1 et 2,
  un et trois essais), laisse une marge de 1,63 ; et 4 × 6,15 s reste sous les 30 s par instruction du fournisseur
  d'avant l'ADR 029 (34 s et 103 s mesurés), que la borne doit toujours refuser. Le facteur 5 (30,75 s) ne les
  séparerait plus ; 3 (18,45 s) ne laisserait que 1,22 de marge sur le pire mesuré. Les pauses dormies au même moment
  par le test restent mesurées et sont écrites dans le message d'échec, pour dire si un dépassement vient des pauses ou
  de l'attente d'un thread.
- **Borne basse inchangée** : au moins 5 s par essai (chaque essai, et 15 s pour les trois) ; les bornes à l'horloge du
  test (5 s à 30 s pour un essai, 15,15 s à 90,15 s pour trois) aussi.

Écart nommé : la borne haute est plus large que celle du 24/10 (6,15 s + 2 s au repos devient 24,6 s). Ce qu'elle ne
voit plus est prouvé exactement ailleurs : `busy_timeout = 5000` est relu dans SQLite par `ConnectionSettingsTests`, le
délai de commande de 1 s est vérifié sur chaque connexion de l'unité, le nombre d'essais, leur erreur et leurs pauses
(50 et 100 ms) le sont dans ce test. La borne haute ne garde que l'ordre de grandeur : aucune attente du fournisseur par
défaut (30 s par instruction) ne passe.

Vérification : `RealBusyTests` lancé seul plusieurs fois, puis avec les autres classes de la suite de persistance en
même temps.
