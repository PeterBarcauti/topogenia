<!-- 041-delai-des-migrations.md -->
<!-- Délai propre aux migrations et à l'attente de leur verrou, indépendant du délai ordinaire des commandes -->

# 041 - Délai des migrations

Statut : acceptée (date 23/10/2026), révisée (23/10/2026, porte P3 round 2 ; 24/10/2026, round 3)

## Contexte

Trois exécutions simultanées de la solution sur l'instance locale : `Two_processes_that_migrate_at_once_apply_each_migration_once`
(SQL Server) a échoué une fois sur trois après 5 min 20 s, `BASE_OCCUPEE` ← `SqlException : Execution Timeout Expired`
dans `SqlServerHistoryRepository.AcquireDatabaseLockAsync`, appelé par le contrôle de `SchemaMigrations`. Le second
processus attendait le verrou des migrations pendant que le premier migrait (huit migrations SQL Server, dont
`BinaryTextCollation` qui modifie 69 colonnes) ; son attente a été coupée par le délai ordinaire d'une commande.

Délais constatés dans le code et dans EF Core 10.0.13 (lecture des piles d'appel et sondes jetables, avant correction) :

| Point | SQL Server | SQLite |
|---|---|---|
| Instructions des migrations (`Database.MigrateAsync`) | Délai de commande du contexte, sinon celui de la chaîne de connexion (30 s par défaut, 120 s dans les tests, ADR 040) | Délai de commande du contexte, 1 s (ADR 029) : il ne coupe pas une instruction qui s'exécute, il borne l'attente d'un verrou (5 s de `busy_timeout`, relancées tant que la commande a moins que son délai, soit 6,15 s au plus) |
| `BEGIN` d'une transaction de migration | - | `DefaultTimeout` de la connexion, 1 s (posé par `SqliteSettingsInterceptor`) : même attente bornée à 6,15 s |
| Verrou des migrations (`AcquireDatabaseLockAsync`) | `sp_getapplock` sans `@LockTimeout` : attente sans fin côté serveur, bornée par le seul délai de la commande (30 s) | Boucle d'EF Core : `INSERT OR IGNORE` dans `__EFMigrationsLock`, puis pause de 1 s doublée jusqu'à environ une minute, **sans fin** tant que la ligne existe, même laissée par une migration arrêtée (sonde : annulée au bout de 20 s, rien d'autre ne l'arrête) ; l'insertion elle-même échoue au bout de 6,15 s quand une migration ailleurs tient le verrou d'écriture (sonde : `SqliteException` 5 au bout de 5,7 s) |
| Contrôle `CheckAsync` et catalogue (`SchemaCatalog`) | Commandes créées sur la connexion : délai de la chaîne de connexion | `DefaultTimeout` de la connexion : 1 s |

En production, le délai ordinaire s'appliquait donc aux migrations et à l'attente de leur verrou : une migration
longue sur une vraie base (collation sur des milliers de lignes, reconstruction de table SQLite) ou un second
démarrage pendant une migration échouaient sans panne réelle, et un démarrage en mode Poste après une migration
arrêtée attendait sans fin.

Options :

1. allonger le délai ordinaire : toucherait toutes les commandes de l'application, contraire à l'ADR 029 (une
   instruction SQLite bloquée attendrait des minutes au lieu de 6,15 s) ;
2. aucune limite pour les migrations (délai 0) : un processus bloqué ailleurs bloquerait le démarrage sans fin ;
3. un délai propre aux contextes des migrations, long, borné, réglable par l'hôte.

## Décision

Option 3.

- `DatabaseEngine.MigrationTimeout` : réglage du moteur (propriété `init`), de 1 s à `MaximumMigrationTimeout`
  (1 jour), compté en secondes entières arrondies au-dessus (`MigrationTimeoutSeconds`) ; par défaut
  `DefaultMigrationTimeout` = **10 minutes**. L'hôte le passe à la construction du moteur (`--migrate`, P4-T09 ; le
  mode Poste garde le défaut). Justification de la borne : volumes faibles (D07) ; sous la charge de trois exécutions
  simultanées, la migration complète d'une base de test SQL Server a dépassé 120 s (constat ci-dessus) ; la 1.x
  attendait son verrou 120 s (`sp_getapplock`, A-4-35). Dix minutes couvrent une migration sur une base réelle et l'attente de la
  migration complète d'un autre processus, et rendent la main en cas de blocage véritable.
- `DatabaseEngine.CreateMigrationContext` : contexte des migrations. SQL Server : délai posé dans les options d'EF
  Core **et** dans la chaîne de connexion (`Command Timeout`), pour les commandes d'EF Core (instructions,
  `sp_getapplock`) comme pour celles que le catalogue crée sur la connexion ; ces connexions forment un second pool.
  SQLite : délai dans les options d'EF Core et `DefaultTimeout` de la connexion, posé par un intercepteur placé juste
  après `SqliteSettingsInterceptor` (commandes du catalogue, `BEGIN` des transactions de migration) : une instruction
  attend le verrou d'écriture tenu par une migration ailleurs jusqu'au délai des migrations.
- `SchemaMigrations` : le contrôle (`CheckAsync`, et celui qui précède et suit `MigrateAsync`) et la migration
  utilisent ce contexte. L'attente du verrou dans le contrôle est en plus bornée par le délai des migrations sur les
  deux moteurs (annulation liée) : au-delà, refus `BASE_OCCUPEE` (passager), avec une `TimeoutException` interne qui
  donne le délai ; une annulation par l'appelant reste une annulation.
- `DatabaseEngine.CreateContext` et tout le reste de l'application gardent le délai ordinaire (ADR 029 inchangée pour
  SQLite, chaîne de connexion pour SQL Server).

## Conséquences

- Un second démarrage ou un second `--migrate` pendant une migration attend sa fin (jusqu'à 10 minutes par défaut)
  au lieu d'échouer au bout de 30 s (SQL Server) ou 6,15 s (SQLite).
- Mode Poste : une ligne de `__EFMigrationsLock` laissée par une migration arrêtée (processus tué) donne
  `BASE_OCCUPEE` au bout du délai au lieu d'un démarrage bloqué (révisé au round 2 : `VERROU_MIGRATIONS`, non
  passager, voir la dernière révision) ; elle ne disparaît pas seule (EF Core ne la retire
  pas). La conduite à tenir (fermer l'autre instance, puis retirer la ligne) relève du message de l'hôte (P4-T05).
  Limite connue : si un autre processus prend le verrou entre le contrôle et `Database.MigrateAsync` puis s'arrête
  sans le rendre, l'attente interne d'EF Core sur SQLite n'est bornée que par l'annulation de l'appelant.
- SQL Server : une instruction de migration bloquée par un verrou ou très longue échoue au bout du délai des
  migrations, en `BASE_OCCUPEE` (délai, ADR 036) ; la transaction de la migration est annulée, rien n'est inscrit.
- Les tests : le délai ordinaire des tests SQL Server (120 s, ADR 040) ne borne plus les migrations des tests.

## Vérification

`tests/Topogenia.Persistence.Tests/Schema/MigrationTimeoutTests.cs`, sur SQLite et SQL Server :

- `A_migration_waits_for_a_migration_elsewhere_longer_than_the_ordinary_command_timeout` : un autre contexte tient
  le verrou des migrations (et, en SQLite, le verrou d'écriture comme une migration en cours) ; la migration d'un
  moteur dont le délai ordinaire est court (2 s en SQL Server ; 6,15 s en SQLite) attend au-delà de ce délai plus
  3 s sans échouer, puis migre une fois le verrou rendu ;
- `Every_command_of_the_migration_and_of_the_check_has_the_delay_of_the_migrations_and_an_ordinary_context_keeps_its_own` :
  chaque commande et chaque connexion de la migration et du contrôle a le délai des migrations (1234,2 s, arrondi à
  1235), un contexte ordinaire garde le sien (120 s des tests en SQL Server, 1 s en SQLite) ;
- `A_lock_of_the_migrations_held_beyond_the_delay_of_the_migrations_is_refused_as_busy` : verrou tenu par une
  session (SQL Server) ou ligne laissée (SQLite), délai des migrations de 2 s : `BASE_OCCUPEE` au bout de 2 s.

Mutations (chacune constatée rouge, code remis) : contextes ordinaires dans `SchemaMigrations` (les deux premiers
tests rouges sur les deux moteurs, avec l'erreur du constat en SQL Server : `BASE_OCCUPEE` ← `Execution Timeout
Expired`) ; attente du verrou non bornée (troisième test rouge en SQLite, attente annulée au bout de 60 s) ;
intercepteur SQLite du délai de connexion retiré (deuxième test rouge en SQLite). Trois exécutions simultanées de la
suite de persistance : `docs/couverture-p3/delai-migrations.md`.

## Révision (23/10/2026) : réglages de l'ouverture et migrations des tests

- SQLite : les réglages de l'ouverture (`SqliteSettingsInterceptor`, dont le passage en `WAL`) s'exécutent avant que
  l'intercepteur du délai des migrations pose `DefaultTimeout` : ils gardent le délai ordinaire (6,15 s au plus d'attente
  d'un verrou). Le cas se présente pour une sauvegarde restaurée (journal `DELETE`) qu'un autre programme tient : le
  contrôle est refusé `BASE_OCCUPEE` au bout de 6,15 s, passager, au lieu d'attendre le délai des migrations ; un fichier
  déjà en `WAL` n'est pas concerné. Avant la révision de l'ADR 036 (mode Poste), cet échec remontait en `SqliteException`
  brute ; les échecs de SQLite du contrôle et de la migration sont désormais traduits par `SqliteSchemaCatalog.RefusalOf`.
- Tests : les migrations faites par le migrateur d'EF Core dans les tests ont elles aussi le contexte des migrations
  (ADR 040, révision).

## Révision (23/10/2026, porte P3 round 2) : verrou SQLite laissé par une migration arrêtée

Contexte : le refus du délai du verrou était `BASE_OCCUPEE` sur les deux moteurs, seul code passager
(`IsTransient`). Sur SQL Server, le verrou (`sp_getapplock`) appartient à une session et disparaît avec elle : rejouer
réussit. Sur SQLite, c'est une ligne de `__EFMigrationsLock` qui survit au processus qui l'a posée ; EF Core ne la
retire jamais seul. Un hôte qui rejoue un refus passager bouclait donc (constat de la porte P3, round 2). La spec ne
dit rien ; la 1.x n'avait pas de SQLite (prompt 6.4 : décision consignée, question pour le porteur du projet).

Options : (a) un code distinct, non passager ; (b) garder `BASE_OCCUPEE` avec `IsTransient` faux dans ce seul cas ;
(c) retirer la ligne laissée. (c) n'est pas sûr : la ligne ne dit ni le processus ni la machine qui la tient (EF Core
n'écrit qu'un identifiant et une date), une migration réelle en cours ailleurs (`--migrate`, autre instance sur le même
fichier) ne se distingue pas d'une ligne laissée, et la retirer laisserait deux migrations courir ensemble (A-4-35).
(b) donne à l'hôte un code qui ne dit pas quoi faire. Choix : (a).

Décision :

- `SchemaCodes.MigrationLockHeld` (`VERROU_MIGRATIONS`), non passager. `SchemaCatalog.LockOutlivesItsHolder` : vrai en
  SQLite. Une attente du verrou au-delà du délai des migrations donne `VERROU_MIGRATIONS` en SQLite, `BASE_OCCUPEE`
  (passager) en SQL Server ; la `TimeoutException` interne reste.
- Message de l'hôte (P4-T05) : « une migration est en cours ou a été arrêtée : fermer l'autre instance ; si aucune ne
  tourne, retirer la ligne de `__EFMigrationsLock` (ou restaurer la sauvegarde d'avant la migration) ». L'hôte ne rejoue
  pas ce code.

Correction du contrat, pas affaiblissement : `MigrationTimeoutTests.A_lock_of_the_migrations_held_beyond_the_delay_of_the_migrations_is_refused_as_busy`
devient `..._is_refused_as_busy_or_as_left_where_it_outlives_its_holder` ; l'assertion `IsTransient` vraie reste pour
SQL Server, devient fausse pour SQLite, avec le code `VERROU_MIGRATIONS` ; le délai mesuré est inchangé.
Mutation : `LockOutlivesItsHolder` faux en SQLite (le cas SQLite du test rouge). Constatée rouge (1 : `MigrationTimeoutTests`, SQLite), code remis.

## Révision (24/10/2026, porte P3 round 3) : verrou SQLite tenu par un porteur vivant, attente bornée des tests

Contexte :

- Données m4 : `VERROU_MIGRATIONS` (non passager, conseil « retirer la ligne ») était rendu même quand une migration
  **vivante** tenait le verrou SQLite plus longtemps que le délai : retirer la ligne aurait laissé deux migrations
  courir ensemble (A-4-35).
- Charge : `A_migration_waits_for_a_migration_elsewhere_longer_than_the_ordinary_command_timeout(SqlServer)` attendait la
  fin de la migration au plus `Rendezvous * 5` (5 min) ; une migration SQL Server complète d'une base de test, sous la
  charge de plusieurs suites, dépasse 5 min (vu par deux contrôleurs).

Décision pour le verrou :

- `SchemaCatalog.LockOutlivesItsHolder` est remplacé par deux méthodes : `LockMarkAsync` (lue juste avant l'attente) et
  `LockLeftByAStoppedHolderAsync` (après une attente au-delà du délai). SQL Server : aucune marque, toujours faux
  (`BASE_OCCUPEE`, verrou de session).
- SQLite : la marque est la date qu'EF Core écrit dans la ligne de `__EFMigrationsLock` en prenant le verrou. Le verrou
  est dit laissé (`VERROU_MIGRATIONS`) seulement si la même ligne est restée pendant tout le délai (marque inchangée) et
  si personne n'écrit le fichier à cet instant (`BEGIN IMMEDIATE` accepté, aussitôt annulé ; une migration tient le
  verrou d'écriture pendant ses transactions). Une ligne retirée ou reprise pendant l'attente, ou un fichier en cours
  d'écriture, montrent un porteur vivant : `BASE_OCCUPEE`, passager. L'essai d'écriture attend au plus le
  `busy_timeout` de la connexion (5 s), le délai de sa commande étant fixé à 1 s. La comparaison de la date au délai
  seul a été écartée : elle dépend de l'horloge et du format d'EF Core, la comparaison de la marque avant et après n'en
  dépend pas.
- Limite nommée : une migration vivante qui tient la ligne depuis avant l'attente, plus longtemps que tout le délai (10
  min par défaut), et se trouve entre deux transactions à l'instant du constat, est prise pour arrêtée. Le message de
  l'hôte reste donc prudent (P4-T05) : « une migration est en cours ou a été arrêtée : fermer toute autre instance ; si
  aucune ne tourne, retirer la ligne de `__EFMigrationsLock` (ou restaurer la sauvegarde d'avant la migration) ».

Décision pour le test sous charge : l'attente de la migration, une fois le verrou rendu, est bornée par
`engine.MigrationTimeout` (10 min, le délai que le produit lui-même donne à l'attente du verrou et à chaque
instruction de la migration) au lieu d'une borne propre au test (5 min), plus courte que ce que le produit accorde.
Ce n'est pas un affaiblissement : les assertions « la migration n'a pas fini après le délai ordinaire plus 3 s » puis
« elle a migré, `UpToDate`, chaque migration inscrite une fois » sont inchangées.

Vérification : `Schema.MigrationTimeoutTests.On_sqlite_a_lock_held_by_a_migration_that_writes_is_refused_as_busy_not_as_left`
(ligne et verrou d'écriture tenus ailleurs : `BASE_OCCUPEE`, passager) ;
`On_sqlite_a_lock_taken_again_during_the_wait_is_refused_as_busy_not_as_left` (ligne laissée, retirée puis reprise par
un autre processus pendant l'attente : `BASE_OCCUPEE`) ; le cas de la ligne laissée (`VERROU_MIGRATIONS`) est inchangé ;
dans ces trois cas et sur SQL Server, la `TimeoutException` ne garde que le délai (ADR 036, révision du round 3). Les
deux nouveaux tests rendaient `VERROU_MIGRATIONS` avant la correction. Mutations (constatées rouges, code remis) : marque
ignorée (1 rouge : ligne reprise) ; écriture en cours ignorée (1 rouge : verrou d'écriture tenu).

## Révision du 24/10/2026, porte P3 round 4 (données MAJ-1) : le contrôle ne laisse jamais la ligne du verrou

Constat : le contrôle du démarrage (`CheckAsync`, lancé à chaque démarrage et après chaque restauration) prenait le verrou
des migrations d'EF Core, une ligne écrite dans `__EFMigrationsLock`. Un arrêt brutal pendant ce contrôle laissait la
ligne : le démarrage suivant refusait une base saine en `VERROU_MIGRATIONS` (non passager) au bout du délai des
migrations (603 s mesurés), avec une conduite inadaptée (retirer la ligne, ou restaurer et perdre le travail depuis la
sauvegarde, alors qu'aucune migration n'avait eu lieu). La 1.x utilisait `sp_getapplock`, lié à la session.

Décision : `SchemaCatalog.CheckLockAsync`. Sur SQLite, le contrôle tient le verrou d'écriture de SQLite
(`BEGIN IMMEDIATE`, rien d'écrit, annulé à la fin), que SQLite relâche de lui-même quand le processus s'arrête, et ne
prend la ligne d'EF Core que si une migration est là : ligne présente (une migration l'écrit avant sa première étape) ou
fichier en cours d'écriture (busy après 5 s, le fournisseur ne réessaie pas au-delà de 1 s) ; il attend alors comme
avant, avec les mêmes refus (`BASE_OCCUPEE`, `VERROU_MIGRATIONS`). Une migration qui démarre pendant le contrôle attend
le verrou d'écriture pour écrire sa ligne : elle n'est jamais vue en partie. SQL Server inchangé (verrou applicatif de
la session).

Limites : la ligne laissée par une migration arrêtée reste refusée en `VERROU_MIGRATIONS` après le délai (inchangé,
conduite P4-T05) ; le contrôle bloque les écritures des autres connexions le temps de sa lecture (au démarrage, aucune).
Un contrôle qui rencontre un écrivain attend jusqu'à environ 6 s de plus avant l'attente du verrou d'EF Core.

Vérification : `Schema/CheckLockTests` (à chaque instruction du contrôle, aucune ligne dans `__EFMigrationsLock` ; rouge
quand le catalogue SQLite ne redéfinit pas `CheckLockAsync`), `MigrationTimeoutTests` inchangés et verts (ligne laissée,
migration qui écrit, ligne reprise pendant l'attente).
