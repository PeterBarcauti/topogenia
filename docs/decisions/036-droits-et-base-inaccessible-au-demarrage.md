<!-- 036-droits-et-base-inaccessible-au-demarrage.md -->
<!-- Contrôle du schéma au démarrage : droits insuffisants et base inaccessible distingués d'une base en retard (A-5-10) -->

# 036 - Droits insuffisants et base inaccessible au démarrage

Statut : acceptée (date 23/10/2026), révisée (23/10/2026, porte P3 round 2 ; 24/10/2026, round 3)

## Contexte

La 1.x traduisait au démarrage les erreurs de connexion et de droits (18456, 262, 1802, annexe A 5) en message clair
(A-5-10). En 2.0, `SchemaMigrations.CheckAsync` lit les deux historiques des migrations : sous un compte sans droit
sur le schéma `topogenia` (membre de `topogenia_bi` seulement, ou d'aucun rôle), SQL Server cache leurs métadonnées,
EF Core et le catalogue les voient absents, et le contrôle concluait « en retard » (constat de
`docs/couverture-p3/portage-b.md`). L'hôte aurait alors demandé `--migrate` au lieu de signaler les droits.

## Décision

- Deux codes de plus dans `SchemaCodes` (Application) : `DROITS_INSUFFISANTS` et `BASE_INACCESSIBLE`. Le message
  et l'arrêt restent à l'hôte (P4-T06).
- `SchemaCatalog.CheckAccessAsync`, appelé par `CheckAsync` dès la connexion ouverte (avant le verrou et la lecture
  des historiques) : en SQL Server, une lecture sans ligne (`SELECT TOP (0)`) de `__EFMigrationsHistory`, de
  `schema_history` et de `project`. SQL Server refuse (229) une table que le compte ne peut pas lire, même quand il
  en cache les métadonnées ; une table absente (208) n'est pas un refus : la base est neuve ou en retard. SQLite : rien
  (un fichier n'a pas de compte).
- `SchemaCatalog.RefusalOf` : le fournisseur SQL Server lit les numéros de toutes les erreurs du premier
  `SqlException` de la chaîne : une erreur passagère d'abord (`BASE_OCCUPEE`, révision ci-dessous), puis 4060 ou
  18456 `BASE_INACCESSIBLE`, puis 229 ou 262 `DROITS_INSUFFISANTS`, et 1802 seulement s'il est seul.
  `CheckAsync` et `MigrateAsync` rendent ces échecs en `SchemaRefusedException` avec le code, l'erreur du moteur en
  exception interne (journaux de l'hôte, sans pile technique pour l'utilisateur).
- `SchemaMigrations` accepte des intercepteurs ajoutés à chacun de ses contextes (connexions d'un compte, journaux).

## Conséquences

- Un compte de rapports ou sans rôle est refusé pour ses droits, jamais pris pour une base neuve ; le compte de
  l'application (`topogenia_app`) est accepté.
- Une base absente reste « en retard » (EF Core la dit inexistante quand elle ne peut pas l'ouvrir, 4060) : en mode
  Serveur l'hôte répond `BASE_EN_RETARD` et la création appartient à `--migrate` (révisé au round 2 : le contrôle la
  refuse `BASE_INACCESSIBLE`, voir la dernière révision). Une base vide sous un compte sans
  droit reste aussi « en retard » : aucune table à lire, rien ne distingue les deux cas.
- 1802 (« CREATE DATABASE failed ») n'est pas un refus de droit en soi : SQL Server l'ajoute à la cause de l'échec
  (révision ci-dessous). La première version de cette ADR le disait « non reproductible sans droits sur le serveur » :
  c'est faux, l'instance locale le produit sous charge avec 1807 (verrou de `model`), constat de la porte P3.

## Vérification

`tests/Topogenia.Persistence.Tests/Startup/StartupErrorTests.cs`, SQL Server, sous `EXECUTE AS USER` (utilisateurs
sans login dans une base `Topogenia_Test_*`) : contrôle et migration refusés `DROITS_INSUFFISANTS` (membre de
`topogenia_bi`, utilisateur sans rôle), contrôle accepté sous `topogenia_app`, erreurs réelles 4060/18456, 229, 262
traduites. Mutations : sans `CheckAccessAsync` (le contrôle conclut « en retard »), sans traduction (codes absents).

## Révision (23/10/2026) : 1802 accompagné d'une erreur passagère

Contexte : sous la charge de plusieurs suites de tests, `CREATE DATABASE` échoue avec 1807 (« Could not obtain
exclusive lock on database 'model' », verrou passager tenu par une autre création) suivi de 1802 (« CREATE DATABASE
failed ») ; la lecture de tous les numéros classait l'échec `DROITS_INSUFFISANTS` et un compte `sysadmin` s'est vu
refuser ses droits. 1802 accompagne aussi un disque plein (1101) ou un chemin invalide (5133). La 1.x lisait le seul
premier numéro (`SqlException.Number`, `carto/api/Program.cs` l. 110-113) : 1807 y donnait « SQL Server ne répond
pas ».

Décision :

- Nouveau code `SchemaCodes.DatabaseBusy` (`BASE_OCCUPEE`), seul code passager
  (`SchemaRefusedException.IsTransient`) : le contrôle ou la migration a rencontré une contention ; rien n'est faux
  dans la base ni dans les droits, la même exécution réussit plus tard. L'hôte (P4-T06) le dit et peut rejouer :
  le contrôle ne change rien, la migration est rejouable (ADR 027).
- Numéros passagers, lus avant tout autre : -2 (délai du client), 64, 233, 10053, 10054, 10060 (connexion rompue),
  1204 (plus de verrou disponible), 1205 (victime d'interblocage), 1222 (délai d'un verrou), 1807 (verrou de
  `model`), 5061 (verrou de la base pour `ALTER DATABASE`, qu'accompagne 5069), 40197, 40501, 40613, 49918, 49919,
  49920 (base ou service occupé en Azure SQL).
- 1802 n'est un refus de droit que seul ; avec toute autre erreur, il suit cette erreur (passagère : `BASE_OCCUPEE` ;
  autre : l'échec reste celui du moteur, journalisé par l'hôte). Le vrai refus de droit de création est 262.

Vérification : `tests/Topogenia.Persistence.Tests/Startup/TransientStartupTests.cs`. Le vrai 1807 demande de tenir le
verrou exclusif de `model` le temps d'un `CREATE DATABASE` : toute création de l'instance échouerait alors, celles des
autres processus de tests comprises, et aucun test ne pose de verrou sur une base système (prompt 4-2). Les échecs
sont donc construits tels que le serveur les donne (numéros dans l'ordre du serveur, fabrique interne du pilote) ;
leur lecture est éprouvée sur 14 combinaisons, et un intercepteur envoie 1807 + 1802 à la place d'une vraie commande
du contrôle puis de la migration : refus `BASE_OCCUPEE`, passager, erreur du moteur gardée en exception interne, puis
nouvelle exécution acceptée. Mutations : lecture des passagères neutralisée (`BASE_OCCUPEE` jamais rendu : 8 tests
rouges, dont le contrôle et la migration réels) ; 1802 lu avec toute autre erreur (`Contains` au lieu de « seul » :
1802 + 5133 et 1802 + 1101 redeviennent `DROITS_INSUFFISANTS`, 2 rouges).

## Révision (23/10/2026) : mode Poste (SQLite)

Contexte : le catalogue SQLite n'avait pas de `RefusalOf` (celui de base rend null). Un fichier occupé (`SQLITE_BUSY` 5,
`SQLITE_LOCKED` 6) rencontré par le contrôle ou la migration remontait en `SqliteException` brute au lieu de
`BASE_OCCUPEE`, seul code passager (constat de la porte P3, ADR 041). La spec ne dit rien des fichiers illisibles du mode
Poste ; la 1.x n'avait pas de SQLite, et rendait en « base non disponible » toute erreur de SQL Server autre que les
droits (`carto/api/Program.cs`, branche par défaut), en « pas le droit de créer la base » 262 et 1802. Défaut retenu,
le plus proche de la 1.x (prompt 6.4) : même découpage que SQL Server, par cause.

Décision :

- `SqliteSchemaCatalog.RefusalOf` lit le premier `SqliteException` de la chaîne, par son code principal et par l'octet
  bas de son code étendu (`SQLITE_BUSY_RECOVERY` 261, `SQLITE_BUSY_SNAPSHOT` 517, `SQLITE_BUSY_TIMEOUT` 773,
  `SQLITE_LOCKED_SHAREDCACHE` 262... sont des fichiers occupés), dans cet ordre :
  - 5, 6 : `BASE_OCCUPEE` (passager : un autre processus tient le fichier, la même exécution réussit plus tard) ;
  - 14 (`SQLITE_CANTOPEN` : dossier absent, chemin qui désigne un dossier, fichier que le compte ne peut pas ouvrir),
    26 (`SQLITE_NOTADB` : fichier qui n'est pas une base) : `BASE_INACCESSIBLE` ;
  - 3 (`SQLITE_PERM`), 8 (`SQLITE_READONLY` : fichier en lecture seule, compte sans droit d'écriture) :
    `DROITS_INSUFFISANTS` ;
  - tout autre code (`SQLITE_CORRUPT` 11, `SQLITE_FULL` 13, `SQLITE_IOERR` 10...) reste l'échec du moteur, journalisé
    par l'hôte, comme 1802 avec 1101 ou 5133 en SQL Server.
- `CheckAccessAsync` reste sans effet en SQLite (aucun compte à lire) ; un fichier absent reste « en retard ».
- Défaut trouvé en écrivant le test réel : EF Core ouvre une connexion en lecture seule pour savoir si le fichier
  existe (`SqliteDatabaseCreator.Exists`), et `SqliteSettingsInterceptor` y posait `journal_mode = WAL`. Sur un fichier
  en journal `DELETE`, c'est-à-dire une sauvegarde restaurée (ADR 029, restauration par `RestoreAsync` depuis le round 2), SQLite
  refusait (`SQLITE_READONLY` 8) : le contrôle échouait sur toute sauvegarde restaurée, et la traduction ci-dessus
  l'aurait dite `DROITS_INSUFFISANTS`. Une connexion ouverte en lecture seule (`Mode=ReadOnly`) reçoit désormais tous
  les réglages sauf le mode de journal (écrit dans le fichier, qu'elle ne peut pas changer) ; la première connexion qui
  peut écrire le passe en `WAL`, comme avant. Les connexions de l'application, en lecture et écriture, sont inchangées
  (ADR 029, A-5-28).

Conséquences :

- L'hôte (P4-T05, P4-T06) reçoit les mêmes codes dans les deux modes ; en mode Poste, `BASE_OCCUPEE` veut dire « une
  autre instance ou un autre programme tient le fichier », `BASE_INACCESSIBLE` « le fichier ne s'ouvre pas ou n'est pas
  une base Topogenia », `DROITS_INSUFFISANTS` « le compte ne peut pas écrire le fichier ».
- Une sauvegarde restaurée est acceptée au démarrage et repasse en `WAL` à la première ouverture en écriture.
- Une sauvegarde copiée à la main à la place du fichier de données, à côté du `-wal` de l'ancien fichier, n'est pas une
  sauvegarde restaurée : SQLite lirait ce journal avec elle (travail postérieur à la sauvegarde, ou fichier corrompu).
  Elle est refusée avant l'ouverture, `BASE_INACCESSIBLE` (`SqliteJournalCheck`, code 26), et la restauration passe par
  `IBackupService.RestoreAsync`, qui remplace le fichier et son journal ensemble (ADR 029, révision du round 2, porte P3
  round 2, Données B1). `SqliteStartupErrorTests.A_restored_backup_in_the_rollback_journal_is_accepted_by_the_check_and_taken_to_the_write_ahead_log`
  ouvre la sauvegarde sur place, sans journal à côté ; le cas du journal périmé est dans `Sqlite/RestoreTests`.
- Un fichier occupé pendant les réglages de l'ouverture (passage en `WAL` d'un fichier restauré qu'un autre programme
  tient) échoue au bout de 6,15 s au plus : ces réglages ont le délai ordinaire, posé avant celui des migrations
  (ADR 041, révision).

Vérification : `tests/Topogenia.Persistence.Tests/Startup/SqliteStartupErrorTests.cs`, sur de vrais fichiers : sauvegarde
restaurée acceptée et repassée en `WAL` ; la même tenue par une autre connexion (`BEGIN IMMEDIATE`, verrou réservé) :
contrôle et migration refusés `BASE_OCCUPEE`, passagers, `SqliteException` 5 gardée en exception interne, puis nouvelle
exécution acceptée ; fichier texte : `BASE_INACCESSIBLE` (26) ; dossier à la place du fichier : migration refusée
`BASE_INACCESSIBLE` (14) ; fichier marqué en lecture seule : `DROITS_INSUFFISANTS` (8) ; lecture des codes éprouvée sur
19 combinaisons (codes étendus, codes laissés au moteur). Tests écrits avant la correction : 19 rouges sur 25.
Mutations (chacune constatée rouge, code remis) : `RefusalOf` neutralisé (18 rouges, dont les quatre fichiers réels) ;
code étendu non lu (annoncé « 1 rouge : 261 », en fait vert : corrigé par la révision du round 2) ; occupation non lue (9 rouges, dont le fichier tenu) ; mode de journal posé sur la
connexion en lecture seule (2 rouges : sauvegarde restaurée, fichier tenu lu `DROITS_INSUFFISANTS`).

## Révision (23/10/2026, porte P3 round 2) : base absente, exception interne sans message, verrou laissé

Contexte :

- `SchemaMigrations.CheckAsync` demandait d'abord à EF Core si la base existe. `SqlServerDatabaseCreator.ExistsAsync`
  (EF Core 10.0.13) ouvre une connexion à la base et répond « n'existe pas » quand l'ouverture échoue avec 4060 (aussi
  1832, 5120) ; il ne lit aucune autre base. Or un login sans utilisateur dans une base existante reçoit exactement la
  même réponse (4060 puis 18456, même message ; seul le journal du serveur les distingue). Le contrôle disait donc « en
  retard » une base qu'il ne pouvait pas ouvrir, alors que `RefusalOf` lit la même erreur `BASE_INACCESSIBLE` : l'hôte
  aurait demandé `--migrate`, qui ne change rien à une base existante. La 1.x (`MiseAJourBase.Verifier`) échouait à
  l'ouverture et affichait « la base de données n'est pas disponible » (`carto/api/Program.cs`, premier numéro 4060,
  branche par défaut). Distinguer les deux cas demanderait de lire `sys.databases` depuis une autre base (`master` ou la
  base par défaut du login) : interdit par A-5-12, et sans garantie (`VIEW ANY DATABASE` peut être retiré à `public`).
- L'exception interne d'un refus était l'exception du moteur : son message nomme la base, l'objet et, pour 18456, le
  login. La spec 5.2 limite les journaux aux identifiants et aux codes.

Options pour la base absente : (a) la garder « en retard » ; (b) la refuser `BASE_INACCESSIBLE` au contrôle, la
migration la créant toujours ; (c) lire `master`. Choix : (b), le défaut de la 1.x (prompt 6.4), qui ne trompe pas
l'administrateur dans le cas d'un compte sans accès.

Décision :

- `SchemaCatalog.AbsentDatabaseRefusal` : SQL Server `BASE_INACCESSIBLE`, SQLite aucun (un fichier absent reste « en
  retard », migré au démarrage du mode Poste). Le contrôle (`CheckAsync`) refuse avec ce code une base qu'EF Core dit
  absente ; `MigrateAsync` (`--migrate`) ne l'applique pas et crée la base comme avant. Le refus n'a pas d'exception
  interne (EF Core a absorbé l'erreur). Message de l'hôte (P4-T06) : « la base n'existe pas, ou le compte ne peut pas
  l'ouvrir : la créer par `--migrate`, ou donner au compte un utilisateur dans la base ».
- L'exception interne d'un refus traduit par `RefusalOf` est une `EngineFailureException` (Application) qui ne porte
  que les numéros du moteur (`SchemaCatalog.NumbersOf` : toutes les erreurs du premier `SqlException`, dans l'ordre du
  serveur ; le code principal puis le code étendu du premier `SqliteException`). Le délai du verrou garde sa
  `TimeoutException`, qui ne donne que le délai.
- Règle pour l'hôte (P4-T05, P4-T06) : journaliser `Code`, `Migration`, `IsTransient` et les numéros de
  l'`EngineFailureException`, jamais le `Message` ni le `ToString()` d'une exception du moteur ; une erreur que
  `RefusalOf` laisse au moteur remonte telle quelle : l'hôte n'en journalise que le type et les numéros
  (`SqlException.Errors[*].Number`, `SqliteException.SqliteErrorCode` et `SqliteExtendedErrorCode`) ; les journaux
  d'EF Core restent sous le niveau qui porte les messages, et `EnableSensitiveDataLogging` n'est jamais posé.
- Verrou des migrations SQLite laissé : nouveau code `VERROU_MIGRATIONS`, non passager (ADR 041, révision).

Conséquences :

- Mode Serveur : une base absente ou inaccessible au compte donne `BASE_INACCESSIBLE` au démarrage ; `--migrate` crée
  une base absente ; sous un compte qui ne peut pas ouvrir une base existante, `--migrate` échoue à la création (1801 ou
  262), comme avant.
- Correction du contrat, pas affaiblissement : les tests qui lisaient l'exception du moteur gardée en exception interne
  (`TransientStartupTests`, `SqliteStartupErrorTests`, `StartupErrorTests`) lisent désormais les mêmes numéros dans
  l'`EngineFailureException`, et vérifient que le refus ne contient plus le nom de la base ;
  `SchemaMigrationTests.A_new_database_is_behind_on_sqlite_and_refused_as_unreachable_on_sql_server_and_the_check_never_creates_it` doit
  attendre, pour SQL Server, le refus `BASE_INACCESSIBLE` (fichier hors du périmètre de cette correction, signalé).
- Correction de la révision « mode Poste » : la mutation « code étendu non lu » (`SqliteExtendedErrorCode` remplacé par
  `SqliteErrorCode`) restait verte, aucun cas n'ayant un code principal muet et un code étendu parlant ; trois cas
  construits l'ont désormais (1 avec 261, 526, 1032).

Vérification : `Startup.StartupErrorTests.A_missing_database_is_refused_as_unreachable_by_the_check_never_created_and_refused_to_a_connection_with_4060_and_18456`
(refus du contrôle et lecture de la connexion identiques) ; `Startup.StartupErrorTests.The_check_and_the_migration_run_by_an_account_that_cannot_read_the_histories_are_refused_for_its_rights`
(numéros seuls, nom de la base absent du refus) ; `Startup.StartupErrorTests.The_failures_of_a_start_are_read_as_an_unreachable_database_or_as_missing_rights`
(ouverture de la base absente rouverte, bornée, sur un échec sans réponse du serveur, comme le test voisin, ADR 040) ;
`Startup.SqliteStartupErrorTests.The_result_code_of_sqlite_and_its_extended_code_are_read_as_one_refusal` (22 cas).
Mutations (chacune constatée rouge, code remis) `AbsentDatabaseRefusal` nul en SQL Server (1 rouge : la base absente redevient « en retard ») ; refus gardant l'exception du moteur en exception interne (6 rouges : les quatre fichiers SQLite réels, le refus de droits SQL Server, la contention 1807 + 1802) ; code étendu non lu (3 rouges : les trois cas construits).

## Révision (24/10/2026, porte P3 round 3) : connexion refusée par l'instance, délai du verrou sans l'échec du moteur

Contexte :

- Fidélité m2 : 18456 seul (connexion refusée par l'instance : connexion inconnue, mot de passe faux, compte Windows
  sans connexion) donnait `BASE_INACCESSIBLE`, comme une base absente ; le message prévu pour l'hôte (« la créer par
  `--migrate`... ») aurait été faux. La 1.x distinguait ce cas par le premier numéro : « SQL Server refuse la connexion
  de ce compte Windows : il faut un compte autorisé sur l'instance » (`carto/api/Program.cs`). La spec ne tranche pas.
- Sécurité m4 / architecture m5 : la `TimeoutException` du délai du verrou gardait l'échec du moteur en exception
  interne (`SqlException`, dont `SqlError.Server` nomme le serveur), alors que la révision du round 2 disait « ne donne
  que le délai ».

Options pour 18456 seul : (a) garder `BASE_INACCESSIBLE` et documenter les numéros pour l'hôte ; (b)
`DROITS_INSUFFISANTS` ; (c) un code nouveau. Choix : (b), le plus proche de la 1.x (prompt 6.4) : c'est le compte, non
la base, qui est en faute, et la 1.x le disait ainsi ; (c) ajouterait un code pour un cas que (b) couvre déjà. 4060,
seul ou avec 18456 (base absente, ou connexion sans utilisateur dans la base), reste `BASE_INACCESSIBLE`.

Décision :

- `SqlServerSchemaCatalog.RefusalOf` : une erreur passagère d'abord ; puis 4060 présent : `BASE_INACCESSIBLE` ; puis
  229, 262, 18456 (sans 4060), 1802 seul : `DROITS_INSUFFISANTS`.
- Message de l'hôte (P4-T06) pour `DROITS_INSUFFISANTS` dont l'`EngineFailureException` porte 18456 : « SQL Server refuse
  la connexion de ce compte : il faut un compte autorisé sur l'instance » ; pour les autres numéros : « le compte n'a
  pas les droits nécessaires sur la base ».
- La `TimeoutException` du délai du verrou des migrations n'a plus d'exception interne ; son message ne donne que le
  délai (`SchemaMigrations.AcquireLockAsync`).

Limite constatée : sur l'instance locale, le contrôle et la migration d'une connexion refusée (18456 seul) mettent
chacun environ 60 s avant d'échouer (mesuré : 2 min pour les deux ; une base absente, 4060 avec 18456, échoue en
quelques secondes). L'attente ne vient pas du délai de connexion (inchangée avec `Connect Timeout = 10`) ; elle a lieu
pendant la vérification d'existence de la base par EF Core, avant toute commande du produit. Cause exacte non établie ;
aucun code du produit ne rejoue l'erreur, et le refus final est le bon.

Vérification : `Startup.StartupErrorTests.A_login_refused_by_the_instance_is_refused_for_its_rights_by_the_check_and_the_migration_not_as_unreachable`
(vraie connexion SQL inconnue de l'instance, aucune connexion créée : contrôle et migration `DROITS_INSUFFISANTS`, non
passagers, numéros `[18456]`, nom de connexion absent du refus) ; `Startup.TransientStartupTests.The_errors_of_one_failure_are_read_together_a_contention_first`
(cas `[18456]` et `[4060]` ajoutés) ; `Schema.MigrationTimeoutTests` (`TimeoutException` sans exception interne, sur
les deux moteurs). Mutations (constatées rouges, code remis) : 18456 relu `BASE_INACCESSIBLE` (2 rouges : le cas
construit, la vraie connexion) ; échec du moteur gardé dans la `TimeoutException` (4 rouges : le délai sur les deux
moteurs, les deux verrous SQLite vivants).

## Complément (porte P3, round 3, restauration SQLite)

En SQLite, `BASE_INACCESSIBLE` couvre aussi un fichier séparé de son journal (`SqliteException` 26 avec un code étendu) :
26/26 en-tête en journal de retour arrière à côté d'un journal, 26/282 journal d'un autre fichier, 26/538 journal sans
page 1, 26/794 fichier abîmé à la première ouverture, 26/1050 fichier remplacé en marche (ADR 029, révision du round 3).
