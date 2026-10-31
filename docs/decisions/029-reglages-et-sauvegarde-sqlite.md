<!-- 029-reglages-et-sauvegarde-sqlite.md -->
<!-- Réglages de chaque connexion SQLite, sauvegarde en ligne du fichier du mode Poste et démarrage avec migration -->

# 029 - Réglages et sauvegarde SQLite

Statut : acceptée (date 23/10/2026), complétée le 23/10/2026 (porte de la phase 3, round 1 : délai de commande,
collation des décimaux, dossier et rotation des sauvegardes, détail des échecs ; round 2 : restauration et journal
étranger, emplacement de `DesktopDatabaseStartup` ; round 3 : tampon du fichier et de son journal, restauration par
l'API de sauvegarde)

## Contexte

La spec 4.3-6 impose à chaque connexion SQLite `foreign_keys = ON`, `journal_mode = WAL`,
`busy_timeout = 5000`, `synchronous = NORMAL`. La spec 5.2 impose une sauvegarde au premier lancement du
jour par l'API de sauvegarde en ligne de SQLite, 7 fichiers tournants, un fichier avant chaque migration
et un `PRAGMA integrity_check` de la copie ; la spec 4.4 et AR-14 imposent de ne pas migrer si cette
sauvegarde échoue. La 1.x (`Infrastructure/Sauvegarde.cs`, `Program.cs`) sauvegardait par
`BACKUP DATABASE` de SQL Server, un fichier par jour de la semaine (`Ariane_1_lundi.bak`), un fichier
avant mise à jour (`Ariane_avant_v5.bak`), et savait qu'elle avait déjà sauvegardé dans la journée par
`msdb`. Plusieurs points ne sont tranchés ni par la spec ni par la 1.x.

## Décision

| Point | Options | Choix | Raison |
|---|---|---|---|
| Où appliquer les réglages | (a) chaîne de connexion ; (b) intercepteur de connexion d'EF Core | (b) `SqliteSettingsInterceptor`, ajouté par `SqliteEngine` avant tout autre intercepteur, à chaque ouverture (`ConnectionOpened`) ; une connexion ouverte en lecture seule (`Mode=ReadOnly`, celle par laquelle EF Core regarde si le fichier existe) reçoit tous les réglages sauf le mode de journal, qu'elle ne peut pas changer, et la première connexion qui peut écrire passe le fichier en WAL (révision du 23/10/2026, ADR 036 révisée : une sauvegarde restaurée était refusée) | La chaîne ne connaît ni WAL ni `synchronous` ; une connexion rendue par le pool garde ce que son utilisateur précédent y a laissé |
| Ordre et contrôle | - | `busy_timeout` d'abord (les autres attendent un verrou), `journal_mode` vérifié : si SQLite garde un autre mode, exception | SQLite ne signale pas d'erreur quand il ne peut pas passer en WAL |
| SQL des réglages et contrôles | - | `Connection/SqlitePragmas.cs` du fournisseur ; les tests relisent les réglages par la sonde interne `Connection/SqliteSettingsProbe` (seul `InternalsVisibleTo` du fournisseur, vers les tests de persistance ; aucun type de `src` ne l'utilise, ADR 037) | ADR 025 : aucun SQL hors des fournisseurs ; la lecture des pragmas ne sert qu'aux tests (porte P3, Architecture m8) |
| Délai de commande du fournisseur | (a) défaut de Microsoft.Data.Sqlite (30 s) ; (b) 1 s, le plus petit accepté (0 = sans limite) ; (c) `busy_timeout` à 0 et attente laissée au fournisseur | (b) `SqliteConnectionSettings.CommandTimeoutSeconds = 1`, posé par `SqliteEngine` (option `CommandTimeout` d'EF Core) et par l'intercepteur (`DefaultTimeout` de la connexion, qui vaut aussi pour `BEGIN` et les pragmas), quelle que soit la chaîne de connexion | Microsoft.Data.Sqlite relance lui-même une instruction refusée `SQLITE_BUSY`/`SQLITE_LOCKED` toutes les 150 ms tant que la commande a moins que son délai : avec 30 s, une instruction attendait environ 34 s (5 s de SQLite, relancées jusqu'à 30 s) et les trois essais d'une unité environ 100 s (mesuré). (c) contredirait la spec 4.3-6 |
| Collation des décimaux | (a) celle d'EF Core ; (b) `EF_DECIMAL` invariante enregistrée à chaque ouverture | (b) `Connection/SqliteDecimalCollation`, enregistrée sous le même nom par l'intercepteur après celle d'EF Core ; un texte qui n'est pas un nombre passe après les nombres, ordre ordinal entre eux | EF Core 10.0.13 lit les deux textes par `decimal.Parse` dans la culture du fil : `FormatException` en français, ordre faux sans erreur en allemand (`12.5` lu 125). `Min`, `Max`, sommes et comparaisons passent par les fonctions d'EF Core, qui lisent en culture invariante : seule la collation est remplacée (porte P3, Données M3) |
| Dossier des sauvegardes | (a) non contrôlé ; (b) même règle que le fichier de données | (b) `DesktopDataFile.Check` sur le dossier au début de chaque sauvegarde ; refus = échec `SAUVEGARDE_DOSSIER_REFUSE: <code du refus>`, rien n'est écrit | Une sauvegarde sur un partage ou dans OneDrive sortirait du profil et de son chiffrement les données du fichier (porte P3, Sécurité m4) ; l'environnement (`DataFileEnvironment`) est donné au constructeur |
| Fichiers d'avant migration | (a) gardés sans limite (1.x) ; (b) les N derniers ; (c) les N derniers et pas plus d'une durée | (c) au plus `BeforeMigrationKept = 2` (la dernière migration et la précédente), aucun de plus de `BeforeMigrationRetention = 30 jours` (date d'écriture fixée par l'horloge) ; rotation après chaque sauvegarde réussie ou sautée, quotidienne comprise ; un fichier qui ne peut pas être supprimé l'est à une sauvegarde suivante | Ils gardent projets purgés, membres et audit ; 30 jours = la rétention de la corbeille (`trash_retention_days`) : ce qu'une purge efface quitte toutes les sauvegardes 30 jours après au plus (ADR 035) ; deux fichiers suffisent à revenir sur une migration récente |
| Détail d'un échec | (a) message de l'exception ; (b) codes seulement | (b) `SAUVEGARDE_IMPOSSIBLE: <type> 0x<HRESULT>` ou `SqliteException <code>/<code étendu>` ; `SAUVEGARDE_BASE_ABSENTE: no data file` ; jamais de chemin ; l'exception d'origine reste dans `BackupResult.Cause` et en exception interne de `MigrationBackupFailedException`, pour le diagnostic local | Le message d'une `IOException` contient le chemin complet, donc le login Windows ; spec 5.2 « identifiants et codes seulement » (porte P3, Sécurité m2) |
| Jour de la sauvegarde quotidienne | (a) jour local ; (b) jour UTC de l'horloge | (b) | D08 : aucune heure locale dans `src` (tests d'architecture) ; horloge injectée (`IClock`) |
| Noms des fichiers | (a) jours en français (1.x) ; (b) numéro ISO et jour en anglais | (b) `<fichier>_1_monday.db` à `<fichier>_7_sunday.db`, `<fichier>_before_<dernière migration>.db` | Forme 1.x (numéro pour l'ordre, jour pour la lecture) ; Q1 (anglais hors interface) ; nom du produit non écrit, dérivé du fichier de données |
| « Sauvegarde du jour déjà faite » | (a) table ou fichier témoin ; (b) date d'écriture du fichier du jour | (b), date fixée à l'heure de l'horloge à la mise en place | Rien d'autre à tenir ; le fichier de la semaine précédente (même nom, autre date) est remplacé |
| Écriture | (a) directement dans le fichier final ; (b) fichier `.partial`, contrôles, puis remplacement | (b) | Une copie ratée ou corrompue ne détruit jamais la sauvegarde précédente du même nom |
| Copie autonome | - | La copie passe en `journal_mode = DELETE` après contrôle | La copie se suffit à elle-même, sans `-wal` ; la restaurer n'est pas la copier à la main à la place du fichier de données (le journal de l'ancien fichier serait lu avec elle) mais `RestoreAsync` (révision du round 2 ci-dessous) |
| Contrôles de la copie | (a) `integrity_check` (spec) ; (b) plus `foreign_key_check` | (b) ; une erreur `SQLITE_CORRUPT` ou `SQLITE_NOTADB` levée par le contrôle compte comme copie corrompue | Une référence cassée rendrait la restauration inutilisable ; SQLite s'arrête sur une page illisible au lieu de la lister |
| Échec | (a) exception ; (b) résultat | (b) `BackupResult.Failure` = code stable (`SAUVEGARDE_BASE_ABSENTE`, `SAUVEGARDE_IMPOSSIBLE`, `SAUVEGARDE_CORROMPUE`) + `: ` + détail sans contenu métier ; seule l'annulation est levée | Le port le prévoit ; l'appelant décide (AR-14 avant migration, A-4-32 sinon) |
| Fichier de données absent | - | Échec `SAUVEGARDE_BASE_ABSENTE`, rien n'est créé | La sauvegarde n'ouvre jamais un fichier qui n'existe pas (l'ouverture le créerait) |
| Orchestration « en retard -> sauvegarde -> migration » | (a) hôte (phase 4) ; (b) fournisseur SQLite | (b) `Startup/DesktopDatabaseStartup`, qui ne dépend que des deux ports ; écart nommé ci-dessous (round 2) : un cas d'usage dans un adaptateur | Le mode Poste est toujours SQLite ; la règle AR-14 est testée sur de vrais fichiers ; l'hôte (P4-T05) n'a qu'un appel |
| Fichier neuf | - | Pas de sauvegarde avant migration quand aucune migration n'est appliquée | Rien à sauvegarder (1.x : `actuelle > 0`) |
| Ordre au démarrage | (a) quotidienne avant migration ; (b) après | (b) : contrôle, sauvegarde avant migration (si en retard), migration, quotidienne | La copie d'avant migration garde l'ancien schéma, la quotidienne le schéma courant |
| Port `IBackupService` | - | Inchangé ; `RestoreAsync` ajouté au round 2 (révision ci-dessous) | Le dossier et l'horloge sont donnés au constructeur de `SqliteBackupService` |

## Conséquences

- En France, le « jour » de la sauvegarde change à 1 h ou 2 h du matin (heure locale) : un lancement
  entre minuit et cette heure compte pour la veille. Sans effet pour un usage de jour.
- Les fichiers d'avant migration tournent (deux au plus, 30 jours au plus) : revenir sur une migration plus
  ancienne, ou plus de 30 jours après, n'est plus possible depuis ces fichiers ; les 7 quotidiens restent.
- **Attente maximale d'une requête avant `Busy`** (spec 4.3-5/6) : une instruction qui attend un verrou tenu
  ailleurs échoue après les 5 s de `busy_timeout` (le fournisseur abandonne aussitôt, son délai de 1 s étant passé) ;
  au pire, une réponse « occupée » immédiate de SQLite (instantané trop ancien du journal WAL) est relancée pendant
  1 s puis attend les 5 s : `SqliteConnectionSettings.MaximumBusyWait` = 1 s + 150 ms + 5 s = 6,15 s par instruction.
  Une unité de travail (verrou d'écriture pris par `BEGIN IMMEDIATE`) fait au plus trois essais séparés de 50 puis
  100 ms : environ 15,2 s (3 × 5 s + 0,15 s) quand le verrou reste tenu, 18,6 s au pire (3 × 6,15 s + 0,15 s), avant
  que l'exception `SQLITE_BUSY` remonte à l'appelant. Mesuré par `RealBusyTests` avec la configuration de production
  (5 s et environ 15 s ; 34 s et 103 s avant la correction). Ces durées sont celles d'une machine au repos : les 5 s de
  SQLite sont la somme des pauses que son gestionnaire d'occupation demande (59 pauses de 1 à 100 ms), pas un temps lu
  sur une horloge ; sur une machine chargée chaque pause finit au réveil de son thread et l'attente réelle s'allonge
  d'autant (environ 1,75 fois mesuré sous la charge de plusieurs suites, ADR 040 révisée). `RealBusyTests` fixe les
  valeurs de cette décision (5 s, 1 s, 150 ms) au lieu de les lire dans le produit, prouve le délai posé sur chaque
  connexion, mesure l'attente de chaque essai sur sa connexion et garde à l'horloge une borne sous les 30 s par
  instruction d'avant la correction (ADR 040, révision du 24/10/2026).
- Une instruction longue sans attente de verrou n'est pas coupée par ce délai : Microsoft.Data.Sqlite ne s'en sert que
  pour relancer une instruction refusée occupée.
- La sauvegarde, en ligne, ne bloque pas les autres connexions (WAL) et contient les lignes encore dans le
  journal `-wal`, qu'une copie de fichier perdrait.
- Le mode Serveur n'utilise ni ce service ni `DesktopDatabaseStartup` : sauvegardes par le plan de SQL
  Server (spec 6.2), base en retard refusée (`BASE_EN_RETARD`, phase 4).
- L'hôte du mode Poste (P4-T05) construit
  `SqliteBackupService(engine, "%LOCALAPPDATA%\Topogenia\backups", clock, DataFileEnvironment.Current(...))`,
  appelle `DesktopDatabaseStartup.RunAsync`, s'arrête avec un message clair sur
  `MigrationBackupFailedException` (AR-14) et journalise `Daily.Failure` sans s'arrêter. Une restauration passe par
  `RestoreAsync`, avant `RunAsync` (révision du round 2).

## Vérification

`tests/Topogenia.Persistence.Tests/Sqlite/` : `ConnectionSettingsTests` (réglages relus dans SQLite, connexion
neuve, rendue par le pool, ouverte par EF Core malgré une chaîne `Foreign Keys=False`),
`BackupServiceTests` (copie en ligne avec les lignes du `-wal`, journal de la copie, saut de la quotidienne,
rotation sur 8 jours, fichier avant migration, échecs : fichier absent, dossier impossible, copie corrompue,
référence cassée), `DesktopDatabaseStartupTests` (fichier neuf, base à jour, base en retard sauvegardée
avant migration, échec de sauvegarde qui arrête la migration, base refusée, échec de la quotidienne non
bloquant). SQLite seulement : il n'y a pas de côté SQL Server à ces comportements.

Porte P3 round 1 : `ConnectionSettingsTests.The_command_timeout_is_the_one_of_the_engine_whatever_the_connection_string_says`,
`RealBusyTests` (vraie base occupée, configuration de production, attentes mesurées : un essai, trois essais),
`DecimalCultureTests` (tri, `Min`, `Max`, comparaisons en fr-FR et de-DE), `BackupRetentionTests` (deux fichiers
d'avant migration au plus, 30 jours au plus, sauvegarde sautée comprise, dossier refusé dans un dossier nommé comme
OneDrive ou SharePoint, sous un dossier synchronisé de la machine, sur un lecteur réseau), `BackupServiceTests` et
`DesktopDatabaseStartupTests` (détail sans chemin, exception d'origine conservée). Chaque correction vérifiée par
mutation (délai de commande retiré : 34,5 s et 103 s ; collation retirée ; détail = message ; rotation, compte et
contrôle du dossier retirés).

## Révision (23/10/2026) : restauration et journal étranger (porte P3 round 2, Données B1)

Contexte : cette ADR disait « restaurer = copier un seul fichier ». SQLite lit le journal `-wal` (et `-shm`) trouvé à
côté d'un fichier, quel que soit l'en-tête du fichier. Une sauvegarde copiée à la main à la place de `data.db`, alors que
le journal de l'ancien fichier est resté (application arrêtée sans fermeture propre, ou encore ouverte), est lue avec ce
journal : elle montre le travail postérieur à la sauvegarde, ou des pages d'une autre disposition (fichier corrompu,
`integrity_check` en erreur `btreeInitPage() returns error code 11`), et le contrôle du démarrage l'acceptait
(`UpToDate`). La spec (5.2, 4.4) ne décrit pas la restauration ; la 1.x restaurait une base SQL Server par
l'administrateur (`RESTORE DATABASE`), qui remplace données et journal ensemble.

Options : (a) documenter « supprimer `-wal` et `-shm` avant de copier » ; (b) une restauration fournie par la
persistance ; (c) un contrôle à l'ouverture qui refuse un journal qui n'appartient pas au fichier ; (b) et (c).

Choix : (b) et (c), le plus proche de la 1.x (prompt 6.4 : une restauration remplace le fichier et son journal ensemble).

- `IBackupService.RestoreAsync(fichier de sauvegarde)`, implémentée par `SqliteBackupService` (`Backup/SqliteRestore`),
  application arrêtée : copie de la sauvegarde dans `<fichier>.restoring` à côté du fichier de données ; contrôles de
  cette copie (`integrity_check`, `foreign_key_check`, puis le contrôle du démarrage `SchemaMigrations.CheckAsync` : un
  fichier sans migration appliquée, plus récent ou modifié est refusé, un fichier en retard est accepté et sera migré
  par le démarrage) ; passage en journal `DELETE` ; fermeture des connexions inactives du moteur (`ClearPool`) ; le
  fichier de données, son `-wal`, son `-shm` et son `-journal` doivent s'ouvrir sans partage (aucune connexion ailleurs,
  sinon `RESTAURATION_FICHIER_OUVERT`) ; suppression du journal, remplacement du fichier, nouvel `integrity_check`.
  Codes : `RESTAURATION_SAUVEGARDE_ABSENTE`, `RESTAURATION_SAUVEGARDE_CORROMPUE`, `RESTAURATION_SAUVEGARDE_REFUSEE:
  <code du schéma>`, `RESTAURATION_FICHIER_OUVERT`, `RESTAURATION_IMPOSSIBLE` (`RestoreResult.Failure`, codes seulement,
  exception d'origine dans `Cause`) ; un refus laisse le fichier de données tel quel.
- `Connection/SqliteJournalCheck`, appelé par `SqliteSettingsInterceptor` avant chaque ouverture (`ConnectionOpening`) :
  un fichier dont l'en-tête dit le journal de retour arrière (octets 18 et 19 à 1, ce que laisse la sauvegarde) à côté
  d'un `-wal` non vide est refusé avant que SQLite le lise, par une `SqliteException` 26 (`SQLITE_NOTADB`) que le
  contrôle lit `BASE_INACCESSIBLE` (ADR 036) ; rien n'est lu ni écrit. SQLite écrit 2 dans ces octets, dans le fichier
  lui-même, avant d'ouvrir un journal : un tel couple n'est jamais l'état d'un fichier de l'application. Le code 14
  (`SQLITE_CANTOPEN`) est écarté : EF Core le lit comme un fichier absent et le contrôle conclurait « en retard ».

Conséquences :

- La copie à la main d'une sauvegarde à côté d'un journal périmé est refusée au démarrage (`BASE_INACCESSIBLE`) au lieu
  d'être lue ; l'hôte l'explique et propose la restauration.
- Limite (levée au round 3, révision ci-dessous) : un fichier en journal WAL (en-tête 2) copié à la main sur un autre, à côté du journal de ce dernier, ne se
  distingue pas d'un fichier et de son propre journal (SQLite ne lie le journal au fichier par rien) : seule
  `RestoreAsync` est sûre. Les sauvegardes de l'application sont toutes en journal `DELETE` et sont donc détectées.
- Une coupure du disque entre la suppression du journal et le remplacement du fichier laisse l'ancien fichier sans son
  journal : la restauration est à refaire depuis la même sauvegarde, qui n'est pas touchée.
- Le contrôle de l'ouverture lit 20 octets du fichier quand un `-wal` non vide est présent, à chaque ouverture (connexion
  du pool comprise) ; négligeable devant les quatre pragmas des réglages.
- Phase 4 (P4-T05) : l'hôte du mode Poste offre la restauration (choix d'une des sauvegardes de `backups\`, avec
  confirmation : le fichier de données est remplacé) avant d'ouvrir le fichier, appelle `RestoreAsync` application
  arrêtée (aucune autre instance, aucun contexte ouvert), puis `DesktopDatabaseStartup.RunAsync` ; il explique chaque
  code. La forme de l'appel (commande de l'interface au démarrage ou option de la ligne de commande, absente de la
  spec 6.3) est une question pour le porteur du projet.

Vérification : `tests/Topogenia.Persistence.Tests/Sqlite/RestoreTests.cs`, sur de vrais fichiers et les deux variantes
de la sonde de la porte (même disposition ; disposition changée par 20 projets écrits puis l'audit effacé et reporté
dans le fichier) : sauvegarde copiée à la main à côté du journal de l'ancien fichier refusée `BASE_INACCESSIBLE` (26),
fichier et journal intacts, puis `RestoreAsync` : premier projet seul, journal absent, `integrity_check` et
`foreign_key_check` vides, contrôle `UpToDate` ; fichier arrêté avec du travail dans son journal remplacé par la
sauvegarde, même résultat ; connexion ouverte ailleurs : `RESTAURATION_FICHIER_OUVERT`, données intactes, connexions
inactives du moteur fermées par la restauration ; sauvegarde absente, fichier texte, base vide, sauvegarde sans
historique (`EMPREINTE_ABSENTE`), sauvegarde = fichier de données : refus, fichier intact, aucun fichier partiel
laissé.

## Révision (23/10/2026) : `DesktopDatabaseStartup`, un cas d'usage dans un adaptateur (porte P3 round 2, Architecture m7)

Écart nommé : la spec 3.4 range la sauvegarde parmi les cas d'usage de `Application` (`Maintenance/`) et la spec 3.1
réserve à `Persistence.Sqlite` les migrations, les réglages et la sauvegarde. `DesktopDatabaseStartup` est un cas
d'usage (il enchaîne deux ports) placé dans l'adaptateur SQLite. Il n'est pas déplacé en phase 3 : `Application` n'a
encore que ses ports, le gabarit des gestionnaires (spec 3.4 : autorisation, unité de travail, journal) et leurs tests
de règles appartiennent à la phase 4, et le cas n'utilise rien du fournisseur hors des deux ports (il est testé sur
de vrais fichiers). Phase 4 (P4-T05) : le déplacer dans `Application/Maintenance/` avec `DesktopStartupReport` et
`MigrationBackupFailedException`, sans changer son comportement ni ses tests, quand le premier gestionnaire y est écrit.

## Révision (24/10/2026) : fichier et journal liés par un tampon (porte P3 round 3, Données B1, M3, m1, m2 ; Sécurité MAJ-2)

Contexte : le contrôle du round 2 (`SqliteJournalCheck`) ne regardait qu'à l'ouverture d'une connexion. Les sondes de la
porte ont montré quatre chemins vers une base fausse ou corrompue acceptée au démarrage :

- une sauvegarde copiée à la main sur `data.db` pendant que l'application tourne : la connexion déjà ouverte continue
  d'écrire dans le journal, et le point de contrôle de la fermeture de la dernière connexion réécrit ce journal dans la
  copie ; au redémarrage, en-tête 2/2 et `-wal` vide, `UpToDate` avec des données postérieures, ou un fichier corrompu
  (`integrity_check` en erreur 11) ;
- une copie personnelle d'un fichier en journal WAL (en-tête 2) posée à côté du journal d'un fichier arrêté sans
  fermeture : SQLite rejoue ce journal étranger, `UpToDate` avec des lignes en trop, ou corrompu (la limite nommée au
  round 2 était donc destructive) ;
- aucun contrôle d'intégrité au démarrage : un fichier corrompu dont le contrôle du schéma ne lit pas les pages abîmées
  est accepté ;
- la restauration comparait les chemins comme du texte (`\\?\` ou `\\.\` devant le fichier de données le restaurait sur
  lui-même, journal supprimé, travail perdu), copiait le seul fichier principal d'une copie en WAL (deux projets sur trois
  perdus) et laissait `.restoring` sur une sauvegarde en lecture seule.

SQLite ne lie un journal à son fichier par rien (le journal ne porte que ses propres sels et sommes de contrôle).

Options :

| Option | Effet | Retenue |
|---|---|---|
| (a) `quick_check` ou `integrity_check` au démarrage | Refuse un fichier corrompu ; ne voit pas une base cohérente mais fausse (pages d'une copie et d'un journal qui se recouvrent sans se contredire) | Oui, `integrity_check` et `foreign_key_check` (ceux des sauvegardes), à la première ouverture |
| (b) Verrou exclusif tenu sur `data.db` | Impossible : SQLite ouvre un descripteur par connexion, partagé en lecture et en écriture ; un descripteur sans partage d'écriture bloquerait les connexions suivantes de l'application elle-même, un verrou de plage bloquerait ses propres écritures | Non |
| (c) `wal_checkpoint(TRUNCATE)` à la fermeture propre | Plus aucun journal après un arrêt normal ; seul, il réécrit le journal dans la copie posée pendant que l'application tourne (c'est le défaut constaté) | Oui, mais précédé du contrôle (d) |
| (d) Repère d'appartenance entre le fichier et son journal | Distingue le journal du fichier de celui d'un autre fichier, au démarrage et avant chaque point de contrôle | Oui |

Décision : (a), (c) et (d), dans `Connection/SqliteDataFileGuard`, appelé par `SqliteSettingsInterceptor`.

- **Tampon** : un nombre aléatoire non nul écrit dans l'en-tête de la première page (`PRAGMA user_version`, que rien
  d'autre n'utilise ; les sauvegardes le portent comme le reste de la page). Le fichier n'est réécrit depuis son journal
  que par le moteur : `wal_autocheckpoint = 0` et `journal_size_limit = 0` sur chaque connexion
  (`SqlitePragmas.CheckpointSettings`), `SQLITE_DBCONFIG_NO_CKPT_ON_CLOSE` (la fermeture de la dernière connexion ne
  réécrit plus le journal). Chaque point de contrôle du moteur écrit d'abord un nouveau tampon, réécrit le journal dans
  le fichier, puis écrit le même tampon comme première trame du journal suivant : entre deux points de contrôle, le
  fichier n'est pas touché, et le fichier et chaque page 1 du journal portent le même tampon.
- **Première ouverture du fichier par le processus**, avant que SQLite le lise : le contrôle du round 2 (en-tête 1/1 et
  journal non vide : 26/26), puis un journal qui a des trames validées (sels et sommes de contrôle de SQLite, jusqu'à la
  dernière trame de validation, `Connection/SqliteWalFile`) doit contenir une page 1 dont le tampon est celui du fichier
  (sinon 26/282, journal d'un autre fichier ; 26/538 quand aucune page 1 ne permet de le dire). Puis, avant le mode de
  journal (qui peut écrire), `integrity_check` et `foreign_key_check` (26/794, fichier abîmé, aussi quand SQLite ne peut
  pas lire le fichier pendant l'ouverture). Puis la première connexion qui peut écrire fait un point de contrôle complet
  (`TRUNCATE`) : chaque exécution commence son propre journal.
- **En marche** : une connexion qui se ferme réécrit le journal quand il passe 4 Mio (`CheckpointLength`, environ les
  1 000 pages du point de contrôle automatique de SQLite), en mode `PASSIVE` (sans attendre les lecteurs), après le
  même test du tampon ; un fichier remplacé n'est jamais réécrit, le processus refuse ensuite toute ouverture (26/1050)
  et le démarrage suivant le refuse (26/282).
- **Arrêt propre** : `SqliteEngine.CloseAsync`, appelé par l'hôte quand plus aucun contexte n'est ouvert (P4-T05) :
  test du tampon, nouveau tampon, `wal_checkpoint(TRUNCATE)` ; aucun journal ne survit à un arrêt normal. Un fichier
  remplacé : `SqliteException` 26/1050, journal gardé. Sans cet appel, le journal reste et le démarrage suivant le relit
  (contrôle du tampon), sans perte.
- Tous les refus sont des `SqliteException` `SQLITE_NOTADB` (26), le cas dans le code étendu (26, puis 26 + 256 × n),
  que le contrôle du démarrage lit `BASE_INACCESSIBLE` (ADR 036) et garde en exception interne
  (`EngineFailureException.Numbers`). L'hôte explique et propose la restauration.
- **Restauration** (`Backup/SqliteRestore`) : chemin de la sauvegarde d'une forme que le contrôle du fichier ne lit pas
  (relatif, volume ou périphérique par son nom, NUL) : `RESTAURATION_CHEMIN_NON_PRIS_EN_CHARGE`, jamais ouvert ; même
  fichier que le fichier de données par son identité sur le disque (`GetFileInformationByHandle` : volume et index du
  fichier, `Files/FileIdentity`), quel que soit le texte (`\\?\`, `\\.\`, nom court, lien physique) :
  `RESTAURATION_IMPOSSIBLE: the backup is the data file`, rien de touché ; copie par l'API de sauvegarde en ligne
  (`BackupDatabase`) depuis une connexion en lecture seule, qui lit le journal de la sauvegarde s'il en a un et ne le
  réécrit jamais (une copie en WAL avec son `-wal` est restaurée entière) ; la copie n'hérite pas des attributs (une
  sauvegarde en lecture seule est restaurée, le fichier de données reste inscriptible) ; le fichier partiel est supprimé
  quoi qu'il arrive, sans que cette suppression masque le résultat ; le processus oublie ce qu'il savait du fichier
  remplacé (`SqliteDataFileGuard.Forget`), contrôlé de nouveau à l'ouverture suivante.

Scénarios de la sonde de la porte, après correction (`Sqlite/HandCopyTests`, `RestoreTests`, `RestoreSourceTests`) :

| Scénario | Résultat |
|---|---|
| Sauvegarde (en-tête 1) copiée sur le fichier ouvert, la connexion ouverte écrit encore, fermeture, redémarrage (deux dispositions) | Nouvelle connexion refusée 26/26 aussitôt ; la fermeture ne réécrit rien ; redémarrage refusé 26/26 ; `RestoreAsync` rend la sauvegarde |
| Copie personnelle d'une exécution précédente posée à côté du journal d'une exécution arrêtée (deux dispositions) | Refusée 26/282, fichier et journal intacts ; le vrai fichier remis avec son journal est lu entier |
| Copie du fichier prise pendant l'exécution, posée à côté du journal de cette exécution | Lue avec ce journal : même état que le fichier remplacé (le fichier n'a pas changé depuis le dernier point de contrôle), rien de perdu |
| Copie posée sur le fichier pendant que l'application tourne, puis arrêt propre | `CloseAsync` refuse 26/1050, ne réécrit rien, journal gardé ; le processus refuse ensuite toute ouverture ; redémarrage refusé 26/282 |
| Fichier abîmé sur une page de données que le contrôle du schéma ne lit pas | Refusé 26/794 à la première ouverture, rien d'écrit |
| Arrêt propre, puis sauvegarde remise à la main | Aucun journal ; la sauvegarde est le fichier de données, acceptée (restauration à la main d'un fichier fermé proprement) |
| Exécution réécrite plusieurs fois par le moteur, arrêtée sans fermeture | Relue entière |
| `RestoreAsync` sur `\\?\`, `\\.\` ou un lien physique du fichier de données | Refusée, journal gardé, rien de perdu |
| `RestoreAsync` d'une copie en WAL avec son journal ; d'une sauvegarde en lecture seule | Restaurée entière ; aucun fichier laissé |

Limites qui restent, aucune ne perd de donnée sans le signaler :

- Deux processus de l'application sur le même fichier (le mode Poste n'en suppose qu'un) : les tests du tampon portent
  sur les fichiers eux-mêmes, jamais sur ce qu'un processus garde en mémoire ; chacun reste sous la même règle.
- Une écriture d'une autre connexion du processus peut commencer le journal suivant entre le point de contrôle et le
  tampon qui le suit ; une panne dans ce même instant laisse un journal sans page 1 : le démarrage le refuse (26/538),
  le travail reste dans le journal. Refus signalé, aucune acceptation ; question pour le porteur du projet (le recours que
  l'hôte offre alors).
- Un outil tiers qui ouvre le fichier avec ses propres réglages (point de contrôle automatique, fermeture) peut le
  réécrire sans tampon : refus 26/538 ou 26/282 au démarrage suivant, jamais une acceptation d'un journal étranger.
- Pendant l'exécution, entre une copie à la main en WAL et le point de contrôle suivant (fermeture d'une connexion
  au-delà de 4 Mio, ou arrêt propre), les connexions déjà ouvertes lisent le fichier copié recouvert par le journal :
  des données fausses peuvent s'afficher ou une erreur `SQLITE_CORRUPT` survenir ; rien n'est réécrit dans la copie et
  le démarrage suivant la refuse. Une copie en journal de retour arrière (toute sauvegarde de l'application) est
  refusée dès l'ouverture suivante (26/26).
- Une collision de deux tampons aléatoires (une chance sur 2^32 par point de contrôle).

La limite nommée au round 2 (« un fichier en journal WAL copié à la main sur un autre, à côté du journal de ce dernier,
ne se distingue pas ») est levée. Coût : une écriture de la page 1 par point de contrôle, un `integrity_check` par
processus et par fichier, la lecture du journal à la première ouverture et à chaque point de contrôle (4 Mio au plus
tant qu'aucun lecteur long n'empêche le journal de repartir ; avec un lecteur long, le journal grandit et chaque
fermeture qui a écrit le relit en entier : 273 validations en 40 s au lieu de 7 496 sans lecteur, p50 289 ms, mesuré à
la porte P3 round 4, Données m1 ; l'effet cesse avec le lecteur. Le mode Poste n'a pas de lecteur long : les lectures
du chargeur sont courtes).

Vérification : `tests/Topogenia.Persistence.Tests/Sqlite/HandCopyTests.cs` (sept scénarios ci-dessus, rouges avant la
correction : fermeture qui réécrivait le journal, journal étranger rejoué, fichier abîmé accepté), `RestoreSourceTests`
(identité, formes de chemin, lecture seule, copie en WAL), `RestoreTests` (inchangés, la fermeture propre par
`CloseAsync`). Le redémarrage de l'application est un nouveau processus : les tests le figurent par
`SqliteDataFileGuard.Forget`. Mutations : voir `docs/couverture-p3/r3-c.md`.

## Révision du 24/10/2026, porte P3 round 4 (données B1) : la fermeture d'une connexion n'échoue jamais

Constat : au-delà de 4 Mio de journal, la fermeture de la connexion d'une unité, après son commit, écrit le tampon
(écriture qui attend le verrou d'écriture). Si une autre connexion tient ce verrou plus de 6,15 s, `SQLITE_BUSY` sortait
de la fermeture, donc de `CommitAsync`, et `RetryPolicy` rejouait l'unité entière : son travail écrit deux fois (sonde :
« runs=2, rows with marker = 2 »).

Décision, deux garde-fous indépendants :

- **Une unité validée n'est jamais rejouée** (`UnitOfWork`, `StoreSession`) : la session note le commit du moteur
  (événement `TransactionCommitted` d'EF Core, qui précède le nettoyage de la transaction) ; une défaillance rencontrée
  ensuite (nettoyage de la transaction, fermeture de la connexion) est levée telle quelle, jamais classée passagère. Vrai
  sur les deux moteurs : sur SQL Server, la connexion se ferme dans le nettoyage de la transaction, sur SQLite à la fin
  de l'unité.
- **La fermeture ne lève jamais une base occupée** (`SqliteDataFileGuard.Closing`) : un point de contrôle refusé par
  `SQLITE_BUSY` ou `SQLITE_LOCKED` est laissé à la connexion suivante qui se ferme (la longueur notée du journal ne
  change pas, la suivante réessaie). Le travail de la connexion est intact.

Limites :

- La fermeture qui rencontre le verrou attend encore jusqu'à 6,15 s (délai d'attente de SQLite et du fournisseur) avant
  d'y renoncer : l'unité est validée, l'appelant reçoit sa réponse après cette attente.
- Si le refus tombe sur le second tampon (après le point de contrôle), le journal n'a pas encore sa page 1 suivante :
  c'est l'instant déjà nommé plus haut (« une écriture d'une autre connexion peut commencer le journal suivant entre le
  point de contrôle et le tampon qui le suit »), même refus 26/538 au démarrage, aucune acceptation.
- Une défaillance après le commit est rendue à l'appelant alors que le travail est fait (aucun double travail, mais un
  échec annoncé à tort) ; elle ne peut plus venir du tampon, seulement d'une panne de la connexion elle-même.

Vérification : `Sqlite/BusyWriteBackTests` (la sonde du contrôleur : unité exécutée une fois, aucun nouvel essai,
journal réécrit à la fermeture suivante), `Writing/RetryTests.A_transient_failure_after_the_commit_is_thrown_and_the_committed_unit_never_runs_again`
(les deux moteurs). Mutations : garde sans le `catch` : `BusyWriteBackTests` rouge ; unité sans la note du commit : les
deux cas de `RetryTests` rouges ; chacune seule rougit son test.

Diagnostic local (porte P3 round 4, Sécurité m3) : `BackupResult.File` et `Cause`, et l'exception interne de
`MigrationBackupFailedException`, portent le chemin complet, donc le login Windows ; ils servent au diagnostic dans le
processus. Comme pour les exceptions du moteur (ADR 036), l'hôte ne les journalise jamais tels quels (ni `ToString()`,
ni `File`, ni `Cause`) : il journalise le code (`Failure`) seulement. Règle reportée dans P4-T05.

## Révision (24/10/2026) : fichier remplacé gardé par la restauration (AR-96)

Décision du porteur du projet sur le journal refusé au démarrage (26/538) : signaler le problème et proposer la
restauration d'une sauvegarde, ce que l'hôte faisait déjà (`BASE_INACCESSIBLE`, `DesktopRecovery`). La restauration ne
supprime plus le journal du fichier remplacé : le fichier et son journal sont renommés à côté
(`data.db.refused-<date UTC>`, puis `-2`... si le nom est pris ; `BackupFileNames.Refused`), la mémoire partagée seule
est supprimée, et ils sont remis en place si la copie restaurée ne peut pas prendre la place. Le travail resté dans un
journal refusé n'est donc jamais détruit ; le nom du fichier gardé est rendu par `RestoreResult.KeptFile` et dit à la
console, et la proposition prévient que les modifications postérieures à la sauvegarde n'y sont pas. Vérification :
`Sqlite/RestoreTests` (fichier gardé avec son journal, qui contient encore le travail ; seconde restauration dans la
même seconde), `Desktop/DesktopRecoveryTests`.
