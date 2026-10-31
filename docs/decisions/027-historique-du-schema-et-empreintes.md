<!-- 027-historique-du-schema-et-empreintes.md -->
<!-- Historique des migrations avec empreinte de la source et du schéma, écrit dans la transaction de la migration -->

# 027 - Historique du schéma et empreintes

Statut : acceptée (date 23/10/2026)

## Contexte

La spec 4.4 exige une table d'historique des migrations avec empreinte : l'application refuse une base
plus récente qu'elle ou dont une migration appliquée a changé. La 1.x stockait dans
`ariane.schema_version`, dans la transaction de chaque script, l'empreinte SHA-256 normalisée LF du
script (`MiseAJourBase.cs`). EF Core 10 tient sa propre table (`__EFMigrationsHistory` : identifiant et
version d'EF Core) sans empreinte ; y ajouter une colonne demande de remplacer son `HistoryRepository`
par une classe interne du fournisseur (API marquée interne, avertissement EF1001). La mission demande
aussi de refuser une base modifiée hors des migrations.

Options :

1. empreinte calculée sur le SQL généré par chaque migration : change à chaque version d'EF Core qui
   modifie la génération, et refuserait une base saine après une mise à jour du paquet ;
2. empreinte de la source de la migration, comme la 1.x l'avait de ses scripts : stable, et c'est la
   règle de la spec (« jamais modifiées une fois publiées ») ;
3. écriture de l'historique après `Migrate` dans une transaction séparée : une panne entre les deux
   laisserait une migration appliquée sans empreinte ;
4. écriture dans la transaction même de la migration, par un intercepteur de transaction (API publique
   d'EF Core).

## Décision

- Table `schema_history` dans le modèle et la migration initiale : `migration_id`,
  `migration_fingerprint` (option 2 : SHA-256 de la source `Migrations/<identifiant>.cs`, LF, sans BOM,
  embarquée dans l'assemblage du fournisseur), `schema_fingerprint` (SHA-256 de la description du
  schéma lue dans le catalogue après la dernière migration de la transaction), `application_version`,
  `applied_at`. `__EFMigrationsHistory` reste celle d'EF Core.
- Écriture par `SchemaSealInterceptor` (option 4), au moment où EF Core valide une transaction de
  migration : la migration et son historique sont écrits ensemble ou pas du tout.
- Contrôle (`SchemaMigrations.CheckAsync`, sous le verrou des migrations d'EF Core) et refus par codes
  stables (`SchemaCodes`) : `BASE_PLUS_RECENTE`, `MIGRATION_MODIFIEE`, `EMPREINTE_ABSENTE`,
  `SCHEMA_MODIFIE` ; `BASE_EN_RETARD` pour l'hôte en mode Serveur. Le SQL du catalogue (`sys.*`,
  `sqlite_master`) et de l'historique vit dans les fournisseurs (ADR 025).
- Les migrations sont générées par `dotnet-ef` 10.0.13, outil local du dépôt (`.config/dotnet-tools.json`),
  avec un projet de démarrage jetable hors du dépôt qui porte `Microsoft.EntityFrameworkCore.Design`
  (procédure dans `docs/modele-donnees.md` § 7.5) : ce paquet tire Roslyn, en conflit avec la version
  de `Microsoft.CodeAnalysis.CSharp` figée pour les tests d'architecture, et n'entre dans aucun projet
  du dépôt.

## Conséquences

- Une migration publiée qui change, une base migrée par une version plus récente, une base migrée sans
  l'application (`dotnet ef database update`) ou modifiée à la main sont refusées avec un code clair.
- Une mise à jour d'EF Core ne change pas l'empreinte d'une migration (source, pas SQL généré). Une mise
  à jour du moteur qui changerait sa façon de décrire le catalogue (normalisation d'une définition de
  CHECK par SQL Server, par exemple) serait vue comme `SCHEMA_MODIFIE` : la réponse est une migration
  vide qui réenregistre l'empreinte du schéma, après vérification.
- Le contrôle prend le verrou des migrations d'EF Core : il attend la fin d'une migration en cours.

## Vérification

`tests/Topogenia.Persistence.Tests/Schema/SchemaMigrationTests.cs` et `SchemaRefusalTests.cs`, sur
SQLite et SQL Server : création, historique et empreintes, migration idempotente, échec d'une
migration sans trace, deux migrations simultanées, et chaque refus.

## Révision (23/10/2026) : migration enregistrée hors transaction

Contexte : une migration qui contient une instruction hors transaction (option de base, ADR 031) est enregistrée
par EF Core 10 hors de toute transaction (`Migrator.GenerateUpSql` : la ligne de `__EFMigrationsHistory` est hors
transaction dès qu'une commande l'est). L'intercepteur ne voit donc aucune validation pour elle ; une panne entre
cet enregistrement et la transaction suivante laissait la base refusée (`EMPREINTE_ABSENTE`) jusqu'à une reprise
manuelle.

Décision :

- `MigrationTransactions.OutsideTransaction` désigne ces migrations par la règle même d'EF Core (commandes générées
  par le fournisseur, `TransactionSuppressed`), avec les services publics d'EF Core.
- `SchemaVerification` accepte une seule forme d'historique incomplet, « en attente d'inscription » : la dernière
  migration appliquée, sans ligne, est hors transaction, celle d'avant est inscrite (référence du schéma), et la
  migration connue suivante, en attente, est en transaction. La base est alors **en retard**, non refusée ; le schéma
  lu doit être celui inscrit par la migration d'avant (les effets de la migration hors transaction n'entrent dans la
  description qu'avec la suivante : `SnapshotIsolationCheck`, ADR 031).
- `SchemaMigrations.MigrateAsync` l'inscrit lui-même, sous le verrou des migrations d'EF Core : la première
  transaction de migration validée (création de l'historique d'EF Core, ou migration suivante) écrit sa ligne par
  `SchemaSealInterceptor`, avec l'empreinte de sa source et celle du schéma lu à ce moment, puis la migration
  suivante est inscrite avec la sienne. Une panne pendant cette reprise ne laisse rien de plus ; une nouvelle
  exécution reprend au même point. Aucune intervention manuelle n'est nécessaire, quelle que soit la panne.
- Une migration hors transaction est toujours suivie d'une migration en transaction (test
  `Every_migration_recorded_outside_a_transaction_is_followed_by_one_in_a_transaction`).

Ce qui reste refusé : toute migration en transaction sans ligne, une migration hors transaction sans ligne qui n'est
pas la dernière appliquée ou dont la suivante est déjà appliquée, et un schéma qui n'est plus celui de la référence
(`SCHEMA_MODIFIE`). Seul cas nouvellement accepté : la même forme laissée par `dotnet ef database update` arrêté juste
après la migration hors transaction, que rien ne distingue d'une panne ; la ligne inscrite porte alors l'empreinte de
la source de l'application et le schéma est contrôlé comme ci-dessus, aucune empreinte fausse n'est écrite.

Vérification : `tests/Topogenia.Persistence.Tests/Schema/SnapshotIsolationTests.cs` (reprise automatique après une
panne avant et après l'enregistrement par EF Core, refus maintenus). Mutation : sans l'acceptation « en attente
d'inscription », la reprise automatique échoue (`EMPREINTE_ABSENTE`) ; options décrites dès `SnapshotIsolation`, la
reprise échoue (`SCHEMA_MODIFIE`).

## Révision (23/10/2026) : description complète de la base SQL Server

La description du catalogue SQL Server couvre désormais tout objet, déclencheur de base, type, droit et membre de
rôle fixe de données de la base, et le catalogue SQLite les objets attachés aux tables d'EF Core et les noms qui
ressemblent aux noms internes (ADR 037). Règle pour les bases déjà migrées : aucune n'a été migrée par une version
publiée ; après publication, une évolution de la description garde celle d'un état existant ou vient avec une
migration qui réenregistre l'empreinte (Conséquences ci-dessus). Les échecs passagers du moteur pendant le contrôle
ou la migration sont rendus `BASE_OCCUPEE` (ADR 036, révision).

## Révision (23/10/2026) : délai des migrations

Le contrôle et la migration ouvrent leurs contextes par `DatabaseEngine.CreateMigrationContext` : leurs instructions,
le catalogue et l'attente du verrou des migrations ont le délai des migrations (`DatabaseEngine.MigrationTimeout`,
10 minutes par défaut, réglable par l'hôte), jamais le délai ordinaire des commandes ; l'attente du verrou dans le
contrôle est bornée par ce délai sur les deux moteurs, au-delà `BASE_OCCUPEE` (ADR 041). La conséquence « le contrôle
attend la fin d'une migration en cours » vaut donc jusqu'à ce délai, et non plus jusqu'au délai ordinaire (30 s en
SQL Server, 6,15 s en SQLite), qui faisait échouer un second démarrage pendant une migration longue.

## Révision (24/10/2026) : descente d'une migration et migrations SQLite atomiques (porte P3 round 3, Données B2, M2)

Contexte (M2) : EF Core retire sa ligne de `__EFMigrationsHistory` avec le `Down` d'une migration, jamais la ligne de
`schema_history`. Après la descente d'une seule migration par les outils d'EF Core (`dotnet ef database update
<migration>`), le contrôle lisait une ligne sans sa migration comme un historique changé à la main et refusait la base
pour toujours (`SCHEMA_MODIFIE`), sur les deux moteurs ; la migration elle-même la refusait aussi, avant de rien faire.

Options : (a) le `Down` de chaque migration retire sa ligne d'historique ; (b) la vérification ignore les lignes dont EF
Core n'a plus la migration ; (c) un dépôt d'historique d'EF Core remplacé, dont le script de suppression retire aussi la
ligne. Choix (a) :

- (b) affaiblit le contrôle : une ligne d'EF Core supprimée à la main pour une migration qui ne change pas le schéma (la
  vérification `TextGuardsCheck`) ne se distinguerait plus d'une descente, et le test
  `A_migration_removed_from_the_history_of_ef_core_is_refused_as_a_changed_schema` deviendrait faux ;
- (c) passe par une classe interne d'EF Core (`SqliteHistoryRepository`, avertissement EF1001 refusé par la règle des
  avertissements) ;
- (a) écrit la règle là où elle s'applique, dans la transaction même du `Down`, avec l'opération d'EF Core
  `DeleteData` (aucun SQL écrit à la main, ADR 025) : la migration, sa ligne d'EF Core et sa ligne d'historique sont
  retirées ensemble ou pas du tout, comme elles sont écrites (A-4-34).

Appliqué aux migrations jamais publiées que la descente d'une ou deux migrations traverse : SQLite
`20261023174605_TextGuardsCheck` et `20261023174259_TextGuardsAndIntegerBounds`, SQL Server
`20261023174530_TextGuardsAndIntegerBounds`. Leur source change, donc leur empreinte (`MIGRATION_MODIFIEE` pour un
fichier migré avec l'ancienne source ; aucun hors des tests avant la publication de la 2.0). La migration initiale
supprime la table `schema_history` avec elle. Les huit migrations SQL Server antérieures ne retirent pas leur ligne :
descendre en dessous de `ApplicationViewDefinition` laisse un refus `SCHEMA_MODIFIE` (le retour arrière du mode Serveur
est la restauration de la base par le plan de SQL Server, spec 6.2) ; écart nommé, à lever par une migration qui le
corrige si le porteur du projet le demande.

Sur SQLite, le `Down` de `TextGuardsAndIntegerBounds` généré par l'outil reconstruisait les tables d'après le modèle de
la migration initiale, colonnes dans un autre ordre (la clé, puis par nom) que la migration initiale ne les avait écrites
(l'ordre des propriétés) : un schéma équivalent, mais dont l'empreinte n'était pas celle inscrite par la migration
initiale, refusé `SCHEMA_MODIFIE`. Ce `Down` est écrit à la main (`SqliteTableRebuild.BackTo`) : chaque table est copiée
telle quelle, supprimée, recréée par les opérations de la migration initiale (mêmes instructions qu'à sa première
exécution), remplie de nouveau ; le schéma laissé est celui de la migration initiale, à l'octet près.

**Règle pour toute migration à venir**, sur les deux moteurs : son `Down` retire sa ligne de `schema_history`
(`DeleteData` sur `migration_id`) ; sur SQLite, un `Down` qui reconstruit des tables rend les instructions de la
migration à laquelle il revient (`SqliteTableRebuild.BackTo`) quand le moteur ne les rend pas de lui-même. Le test
`MigrationDescentTests` descend la dernière migration de chaque moteur et celle d'avant les gardes de texte, puis
remonte : une migration ajoutée sans cette règle le fait échouer.

Contexte (B2) : la migration SQLite `TextGuardsAndIntegerBounds` reconstruit 22 tables ; EF Core coupait les clés
étrangères hors de toute transaction (`PRAGMA foreign_keys`, sans effet dans une transaction) et découpait la migration
en trois transactions : un arrêt au milieu laissait `SCHEMA_MODIFIE` pour toujours et 22 tables `ef_temp_*`. La
couverture A-4-34 disait le contraire. Décision : le fournisseur génère toute reconstruction dans la transaction de sa
migration (`AtomicRebuildSqlGenerator`, enregistré par `SqliteEngine`, donc aussi pour les outils d'EF Core) : les clés
étrangères restent actives, leur contrôle est reporté à la validation (`PRAGMA defer_foreign_keys = ON`, permis dans une
transaction et remis à zéro par la validation, qui refuse alors toute la migration si une référence est cassée) ; au
lieu de renommer la copie (SQLite compte comme cassées les références d'une table supprimée, et un renommage ne les
décompte pas), chaque table est recréée sous son nom par l'instruction même de sa copie et remplie depuis elle, puis la
copie est supprimée. Le texte gardé dans `sqlite_master` est celui que le renommage donnait (description du schéma
identique, vérifiée). Les données sont copiées deux fois (fichiers du mode Poste petits). La reprise SQL Server de
`SnapshotIsolation` n'est pas reprise pour SQLite : il n'y a plus rien à reprendre. `TextGuardsAndIntegerBounds` s'inscrit
elle-même ; `TextGuardsCheck` reste une vérification à part des clés étrangères. **Règle** : aucune migration SQLite ne
s'exécute hors transaction (test `AtomicMigrationTests.Every_sqlite_migration_runs_in_a_transaction`, et
`SnapshotIsolationTests` attend une liste vide côté SQLite).

Vérification : `Sqlite/AtomicMigrationTests` (fichier à la migration initiale avec un graphe de projet, carte détaillée
comprise ; migration arrêtée avant chacune de ses instructions puis avant chacune de ses validations : aucune copie
`ef_temp_*` laissée, contrôle « en retard », migration suivante `UpToDate`, schéma identique à un fichier migré d'un
coup, chaque ligne présente, `integrity_check` et `foreign_key_check` vides) ; `Schema/MigrationDescentTests` (deux
moteurs) ; `Sqlite/MigrationRollbackTests` (descente complète refusée par la carte détaillée : fichier en retard aussitôt,
remigré, données intactes). Mutations : voir `docs/couverture-p3/r3-c.md`.
