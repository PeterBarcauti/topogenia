<!-- 031-lecture-coherente.md -->
<!-- Lecture cohérente du chargement : instantané en SQL Server (options de base posées par migration, comme la 1.x), transaction différée en SQLite -->

# 031 - Lecture cohérente du chargement

Statut : acceptée (date 23/10/2026, révisée le même jour : décision du chef de projet, prompt 6.4, « défaut de la
spec, au plus près de la 1.x » ; la version précédente retenait le sérialisable)

## Contexte

La spec 3.4 demande que les lectures composées (carte, espace de projet) se fassent « dans une transaction en
lecture cohérente (instantané en SQL Server, transaction de lecture en SQLite) » ; A-4-51 et A-5-11 corrigent la
faiblesse 10 de la 1.x (`EnLecture` n'ouvrait aucune transaction : chaque requête lisait l'état validé du moment,
grâce à RCSI).

En SQL Server, une transaction `SNAPSHOT` exige l'option de base `ALLOW_SNAPSHOT_ISOLATION ON` ; sans elle, toute
lecture échoue (erreur 3952). La 1.x posait cette option et `READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE`
dans `db/003_concurrence.sql`, script marqué `-- sans-transaction` (une option de base ne change pas dans une
transaction), rejouable (`IF ... = 0`), inscrit dans `schema_version` hors transaction (`MiseAJourBase.cs`).

La première version de cette ADR retenait `SERIALIZABLE`, parce que la règle d'architecture
`The_application_needs_neither_the_system_database_nor_the_server_procedures` refusait tout `ALTER DATABASE`. Or la
ligne A-5-12 n'interdit que la dépendance à `master` et aux procédures système (`sp_getapplock`, `CREATE DATABASE`) :
changer une option de la base courante (`ALTER DATABASE CURRENT`) ne nomme aucune autre base et ne demande aucun droit
sur le serveur. Le sérialisable gardait les verrous partagés de la lecture jusqu'à sa fin : une écriture sur ce que
lisait le chargement attendait.

## Décision

- **SQL Server : instantané.** `SqlServerDialect.BeginReadAsync` ouvre une transaction `IsolationLevel.Snapshot`
  pour le chargement hors unité de travail. Elle voit la base telle qu'à sa première lecture (le projet, lu en
  premier), ne prend aucun verrou partagé : elle ne fait attendre aucune écriture et n'en attend aucune.
- **SQLite : transaction différée** (`BEGIN` simple, `SqliteDialect.BeginReadAsync`) : elle voit la base telle
  qu'à sa première lecture (instantané du mode WAL).
- **Dans une unité de travail**, le chargement lit dans la transaction d'écriture : `READ COMMITTED` en SQL Server
  (comme la 1.x), immédiate en SQLite ; la structure y est stable sous le verrou de projet (spec 4.7, ADR 015).
- **Options posées par migration**, au plus près de `db/003` : migration SQL Server
  `20261023092342_SnapshotIsolation`, deux instructions hors transaction (`suppressTransaction`), chacune seulement
  si l'option n'est pas déjà posée (rejouable) :
  `ALTER DATABASE CURRENT SET ALLOW_SNAPSHOT_ISOLATION ON` puis
  `ALTER DATABASE CURRENT SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE`. Le `Down` les retire.
- **RCSI aussi posé, comme la 1.x.** Mesure faite avant de choisir (suite de persistance complète, 450 tests, avec
  et sans RCSI) : aucun écart, en particulier le verrou de projet (`UPDATE` du projet, verrou exclusif que RCSI ne
  change pas), le contrôle de version (`UPDATE ... WHERE version = @v`, qui lit toujours la dernière version
  validée) et l'interblocage réel de `RetryTests` (deux `UPDATE` croisés) restent identiques. RCSI apporte ce que la
  1.x en attendait : une instruction qui lit (rapports Power BI sur les vues `bi`, lectures d'une unité de travail
  en dehors des lignes verrouillées) n'attend plus un écrivain et ne le fait pas attendre ; une donnée lue dans une
  unité puis modifiée par une autre est refusée par le contrôle de version (ADR 006). La justesse ne repose pas sur
  RCSI (A-5-11) : les lectures composées sont en instantané, les écritures sous verrou et version.
- **Enregistrement de la migration.** EF Core 10 exécute l'enregistrement d'une migration qui contient une
  instruction hors transaction hors de toute transaction lui aussi (`Migrator.GenerateUpSql` : la ligne de
  `__EFMigrationsHistory` est hors transaction dès qu'une commande l'est) ; `SchemaSealInterceptor` n'écrit
  `schema_history` qu'à la validation d'une transaction de migration (ADR 027). D'où une seconde migration,
  `20261023093315_SnapshotIsolationCheck`, en transaction : elle vérifie que les deux options sont posées (`THROW`
  sinon) ; sa validation inscrit dans `schema_history` les deux migrations, l'empreinte du schéma sur la seconde.
- **Catalogue.** `SqlServerSchemaCatalog` décrit l'état des deux options (ligne `O|<état instantané>|<RCSI>`, lue
  dans `sys.databases` pour la base courante, toujours visible de ses utilisateurs) dès que la migration
  `SnapshotIsolationCheck` figure dans `__EFMigrationsHistory`. Une base où l'on a retiré une option à la main est
  refusée au démarrage (`SCHEMA_MODIFIE`) au lieu d'échouer à chaque lecture (3952). Avant cette migration, la
  ligne est absente : l'empreinte des bases de la version précédente ne change pas, quelles que soient leurs
  options, et une exécution arrêtée entre les deux migrations laisse un schéma décrit comme avant elles.
- **Règle A-5-12 resserrée** (`SourceDetectors.SystemDatabase`, révisée le 23/10/2026 après la porte P3) : seules
  deux instructions sont permises, une option chacune : `ALTER DATABASE CURRENT SET ALLOW_SNAPSHOT_ISOLATION` et
  `ALTER DATABASE CURRENT SET READ_COMMITTED_SNAPSHOT`, `ON` ou `OFF` (écrit, ou trou d'une chaîne interpolée),
  suivies au plus de `WITH ROLLBACK IMMEDIATE` et de la fin de l'instruction. Tout autre `ALTER DATABASE`
  (`CURRENT` compris : `TRUSTWORTHY`, `DB_CHAINING`, `MODIFY NAME`, `SINGLE_USER`, `ADD FILE`, `ENCRYPTION`,
  `COLLATE`, une seconde option après une virgule), `ALTER DATABASE <nom>` (y compris `[CURRENT]`,
  `SCOPED CONFIGURATION`), `CREATE DATABASE`, `DROP DATABASE`, `sp_getapplock`, `USE master` et toute référence à
  `master` restent refusés, migrations et sondes comprises. `sp_getapplock` appelé par EF Core lui-même
  (`IHistoryRepository.AcquireDatabaseLockAsync`, verrou des migrations, ADR 027) n'est pas du code de l'application :
  la règle lit les sources.
- **Sonde des options** (`SqlServerOptionsProbe`, interne, pour les tests seulement, ADR 037) : lit les deux options
  de la base courante (`sys.databases`), les retire ou les pose à la main comme un administrateur (mêmes deux
  instructions, `OFF` compris), et ajoute puis retire sur l'un des deux historiques des migrations une contrainte
  `CK_probe_refused_migration` qui refuse l'inscription d'une migration, pour arrêter une migration après ses
  instructions hors transaction. Aucun type de l'application n'en dépend (`ProbeRulesTests`).

## Conséquences

- Un chargement ne fait plus attendre les écritures et n'attend plus personne ; il ne voit pas une écriture validée
  après sa première lecture. Coût : la version des lignes dans `tempdb`, comme la 1.x.
- `--migrate` (compte `db_owner`, spec 5.3) annule les transactions ouvertes des autres sessions de la base
  lorsqu'il pose RCSI (`ROLLBACK IMMEDIATE`, comme la 1.x) : la migration se fait l'application arrêtée. Une fois
  RCSI posé, l'instruction n'est plus exécutée (rejouable).
- Le compte de l'application (`topogenia_app`) lit en instantané sans aucun droit de plus (aucun changement du § 10
  de `docs/modele-donnees.md`).
- Une exécution arrêtée après les deux instructions hors transaction se reprend seule (ADR 027, révision) :
  - arrêt avant l'enregistrement de `SnapshotIsolation` par EF Core : les options restent posées, aucun des deux
    historiques ne liste la migration ; la base est « en retard » et la migration suivante reprend tout
    (instructions sautées) ;
  - arrêt après cet enregistrement (hors transaction), avant la validation de `SnapshotIsolationCheck` : la base est
    « en retard », `SnapshotIsolation` en attente d'inscription ; la migration suivante l'inscrit dans
    `schema_history` sous le verrou des migrations, puis applique et inscrit `SnapshotIsolationCheck`. Aucun
    historique faux, aucune intervention manuelle.
- `SnapshotIsolationCheck` reste nécessaire : elle porte la transaction qui inscrit `SnapshotIsolation` dans une
  exécution sans panne, elle refuse d'inscrire des options absentes, et les options n'entrent dans la description
  qu'avec elle, ce qui rend la reprise contrôlable (schéma comparé à celui de `RolesAndReportViews`).

## Vérification

- `tests/Topogenia.Persistence.Tests/Loading/ConsistentReadTests.cs`, sur SQLite et SQL Server : une opération de
  structure validée pendant un chargement (juste après la lecture du projet) n'est jamais vue à moitié ; une écriture
  attendue jusqu'au bout pendant le chargement n'attend pas et n'est pas vue ; en SQL Server, le même chargement sous
  le compte `topogenia_app` (`EXECUTE AS USER`, intercepteur `RoleImpersonation`). Mutations : `READ COMMITTED`
  (échec de la cohérence), `SERIALIZABLE` (l'écriture attend : délai dépassé).
- `tests/Topogenia.Persistence.Tests/Schema/SnapshotIsolationTests.cs` : options posées et décrites, historique des
  deux migrations ; refus d'une base sans instantané (et erreur 3952 au chargement) ou sans RCSI ; empreinte d'une
  base de la version précédente inchangée ; les deux arrêts ci-dessus et leur reprise automatique, refus maintenus
  (schéma changé à la main pendant l'arrêt, migration hors transaction sans ligne avant une migration inscrite) ;
  `Down`. Mutations : ligne `O` figée (refus non détecté), ligne `O` décrite sans condition ou dès
  `SnapshotIsolation` (reprise impossible), acceptation « en attente d'inscription » retirée (reprise impossible).
- `tests/Topogenia.Architecture.Tests/DetectorSamplesTests.cs` : les deux options acceptées (écrites, en minuscules,
  interpolées, sur plusieurs lignes, `OFF WITH ROLLBACK IMMEDIATE`) ; refusés `ALTER DATABASE Topogenia ...` et, sur
  la base courante, `TRUSTWORTHY`, `DB_CHAINING`, `MODIFY NAME`, `SINGLE_USER`, `ADD FILE`, `ENCRYPTION`,
  `COLLATE`, `RECOVERY`, une seconde option après une virgule, une seconde instruction. Mutations : ancienne
  expression (`ALTER DATABASE CURRENT` sous toute forme : les échantillons `TRUSTWORTHY`... passent, rouge),
  expression sans la fin d'instruction (la virgule passe, rouge).
