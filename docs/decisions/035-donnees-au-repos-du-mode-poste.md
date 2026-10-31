<!-- 035-donnees-au-repos-du-mode-poste.md -->
<!-- Fichier de données du mode Poste (emplacement, refus des partages et dossiers synchronisés), pas de chiffrement applicatif, données personnelles -->

# 035 - Données au repos : fichier du mode Poste, chiffrement, données personnelles

Statut : acceptée (date 23/10/2026), complétée le 23/10/2026 (porte de la phase 3, round 1 : chemin résolu,
données personnelles dans les sauvegardes ; round 2 : chemins non pris en charge, périphériques réseau, droits NTFS
hors du profil)

## Contexte

La spec 5.2 fixe le fichier de données du mode Poste : `%LOCALAPPDATA%\Topogenia\data\topogenia.db` par défaut,
sous les droits NTFS du profil, et refuse avec une explication un fichier situé sur un partage réseau ou dans un
dossier synchronisé (OneDrive, SharePoint), où SQLite risque la corruption (verrous et journal WAL exigent un disque
local) ; on échange par paquet `.topogenia` (S-5.2-08 à S-5.2-10). Elle écarte le chiffrement applicatif au repos
dans la 2.0, assuré par le chiffrement du disque du poste (BitLocker, exigé par la DSI), et demande une ADR
(S-5.2-11). La spec 5.4 limite les données personnelles au login, au nom affiché, à l'e-mail, à la langue, à
l'auteur et à la date des contributions, sans mesure d'activité individuelle (S-5.4-22).

## Décision

### Fichier de données

- `Persistence.Sqlite/Files/DesktopDataFile` : `DefaultPathIn(dossier du produit)` donne
  `<dossier du produit>\data\<produit en minuscules>.db` (nom du produit lu dans `ProductIdentity`, D13) ; l'hôte
  l'appelle depuis `DesktopPaths.Of` avec le dossier du produit, par défaut `%LOCALAPPDATA%\<produit>` (P4-T05, porte P7
  round 1 : une seule règle du fichier de données).
- `DesktopDataFile.Check(chemin, environnement)` rend un refus (code, chemin, lieu) ou rien ; elle ne lit du disque
  que les métadonnées des chemins locaux (développement des noms courts, liens), par l'environnement. Codes stables : `FICHIER_SUR_PARTAGE`, `FICHIER_DOSSIER_SYNCHRONISE`, `FICHIER_CHEMIN_NON_PRIS_EN_CHARGE`, `FICHIER_LIENS_PHYSIQUES` (round 3) ; l'explication traduite est celle
  de l'hôte (spec 3.6).
  - Partage : chemin UNC (`\\serveur\partage`, `//serveur/partage`, `\\?\UNC\...`, `\\.\UNC\...`, `\\localhost\c$`
    compris) ; lecteur que la machine associe au réseau (lettre testée par l'environnement, `DriveType.Network`) ;
    chemin par un périphérique réseau de Windows, `\\?\GLOBALROOT\Device\Mup\serveur\partage\...` ou
    `...\Device\LanmanRedirector\...` (préfixes `\\?\`, `\\.\`, `\??\`, casse ignorée), comme chemin donné ou comme cible
    d'un lien (porte P3 round 2, Sécurité m3).
  - Dossier synchronisé : chemin sous un dossier de l'environnement (variables `OneDrive`, `OneDriveCommercial`,
    `OneDriveConsumer` du client OneDrive, plus les dossiers que l'hôte ajoute) ; ou chemin qui traverse un dossier
    nommé comme OneDrive et SharePoint nomment les leurs (`OneDrive`, `OneDrive - <nom>`, `SharePoint`,
    `SharePoint - <nom>`, casse ignorée).
  - Un chemin relatif est une erreur de l'appelant : l'hôte résout le chemin avant le contrôle. Un chemin qui n'est
    pas un chemin absolu de lecteur (relatif, `C:fichier.db`, `\dossier\fichier.db`, volume ou périphérique par son nom
    `\\?\Volume{...}\`, `\\?\GLOBALROOT\Device\HarddiskVolume3\...`, `\\.\PhysicalDrive0`) est refusé par un code,
    `FICHIER_CHEMIN_NON_PRIS_EN_CHARGE`, jamais par une exception : le message d'une exception recopiait le chemin, donc le
    login Windows, dans les journaux (porte P3 round 2, Sécurité m3) ; le refus garde le chemin pour l'explication
    locale de l'hôte, comme les deux autres. Le dossier des sauvegardes reçoit le même refus
    (`SAUVEGARDE_DOSSIER_REFUSE: FICHIER_CHEMIN_NON_PRIS_EN_CHARGE`).
  - **Chemin résolu avant les règles** (porte P3, Sécurité m3, `Files/DataFilePath`) : les règles portent sur le
    chemin que Windows ouvrira, pas sur le texte donné. `Path.GetFullPath` retire `.` et `..`
    (`D:\Autre\..\Sync\data.db` est sous `D:\Sync`) et développe les noms courts 8.3 des dossiers qui existent
    (`ONEDRI~1` redevient `OneDrive - <nom>`) ; puis chaque lien (lien symbolique ou jonction) rencontré depuis la
    racine, le fichier compris, est remplacé par sa cible (absolue, relative au dossier du lien, `\??\...` d'une
    jonction, `\\?\UNC\...`) et les règles s'appliquent de nouveau, 31 liens au plus (une boucle s'arrête là, Windows
    refuse de l'ouvrir). Une cible UNC est un partage, refusé sans être touché ; une cible sur un lecteur réseau
    aussi. Les dossiers synchronisés de l'environnement sont comparés sous leur forme donnée et sous leur chemin
    complet. Le contrôle ne lit que les attributs et les données de lien (`DataFileEnvironment.LinkTargetOf`) de
    chemins de lecteurs locaux, jamais un chemin réseau (ADR 014).
- `DataFileEnvironment.Current()` lit les variables du processus, le type des lecteurs et les liens du disque ; les
  tests donnent des valeurs fictives (liens compris : un lien symbolique demande un privilège que les tests n'ont
  pas), et de vrais noms courts et une vraie jonction dans un dossier temporaire. L'appel par l'hôte, avant
  l'ouverture du fichier et avant le démarrage du mode Poste (`DesktopDatabaseStartup`), relève de P4-T05.
- Le dossier des sauvegardes suit la même règle (ADR 029) : `SqliteBackupService` le contrôle avant d'écrire.
- Limites connues (hors de portée de la 2.0, sans composant natif) : un lecteur créé par `subst` (type « fixe »,
  sa cible n'est lisible que par `QueryDosDevice`) ; un volume monté dans un dossier (cible `\\?\Volume{...}\`, non
  suivie, contrôlée telle quelle) ; un lien que le profil ne peut pas lire (droits) ; un dossier synchronisé par un
  autre outil ou une bibliothèque SharePoint montée sous un nom libre, que l'hôte doit ajouter à l'environnement
  (par exemple les points de montage que le client OneDrive inscrit dans le registre de l'utilisateur, P4-T05) ; un
  lien changé entre le contrôle et l'ouverture (fenêtre courte, poste d'un seul utilisateur).
- **Lien physique et caractère interdit** (porte P3 round 3, Sécurité m2 et m3) : un lien physique (`mklink /H`) donne
  au fichier un second nom, dans un dossier synchronisé par exemple, que le contrôle du chemin ne voit pas. Options :
  (a) lire tous les noms du fichier (`FindFirstFileNameW`) et appliquer les règles à chacun ; (b) refuser tout fichier
  qui a plus d'un nom ; (c) documenter la limite. Choix (b), le plus sûr et le plus simple : un fichier de données n'a
  aucune raison d'avoir un second nom. `DataFileEnvironment.LinkCountOf` (par défaut `FileSystemLinkCount`, qui lit
  `nNumberOfLinks` par `GetFileInformationByHandle` sur un descripteur ouvert en lecture avec tous les partages,
  `Files/FileIdentity`) ; plus d'un nom : refus `FICHIER_LIENS_PHYSIQUES`, appliqué au chemin complet et à chaque
  chemin derrière un lien. Un dossier n'a jamais de lien physique (un fichier absent compte un nom). Un chemin qui
  contient un caractère qu'aucun chemin ne porte (NUL) ou trop long, sur lequel `Path.GetFullPath` levait une
  exception, est refusé `FICHIER_CHEMIN_NON_PRIS_EN_CHARGE`, jamais levé. Limite : un lien physique créé entre le
  contrôle et l'ouverture (même fenêtre que les liens ci-dessus).
- **Droits NTFS d'un dossier choisi hors du profil** (porte P3 round 2, Sécurité m2) : la spec 5.2 fixe l'emplacement par
  défaut sous `%LOCALAPPDATA%`, protégé par les droits du profil, et ne demande pas de contrôler les droits d'un autre
  dossier ; la 1.x n'avait pas de fichier local. Défaut retenu (prompt 6.4) : non contrôlé, limite documentée. Un fichier
  de données ou un dossier de sauvegardes que l'utilisateur place ailleurs (`D:\Données`, dossier partagé du poste) garde
  les droits de ce dossier, que d'autres comptes du poste peuvent avoir en lecture : les données et les sauvegardes y
  sont lisibles par eux. Contrôler les ACL demanderait de lire le descripteur de sécurité de chaque dossier du chemin
  (accès des groupes `Users`, `Everyone`, `Authenticated Users`), propre à Windows, et de trancher ce qui est « trop
  ouvert » (un administrateur du poste lit tout) ; la documentation d'exploitation (P7-T04) recommande l'emplacement
  par défaut, l'hôte (P4-T05) le propose sans choix libre par défaut. Question pour le porteur du projet.

### Chiffrement au repos (S-5.2-11)

- **Pas de chiffrement applicatif dans la 2.0.** Le fichier SQLite, ses sauvegardes (`backups\`) et ses journaux ne
  sont pas chiffrés par l'application ; la confidentialité au repos repose sur le chiffrement du disque du poste
  (BitLocker, exigé par la DSI) et sur les droits NTFS du profil (`%LOCALAPPDATA%`), que l'emplacement par défaut et
  le refus des partages et dossiers synchronisés préservent.
- Raisons : un chiffrement applicatif demanderait une clé gardée sur le même poste (DPAPI du profil), donc la même
  frontière que BitLocker et le profil ; il imposerait un composant natif tiers (SQLCipher ou équivalent), contraire
  à la pile figée (ADR 001, 024) et aux antivirus de l'entreprise ; il compliquerait la sauvegarde en ligne
  (ADR 029) et l'échange, qui passe par le paquet `.topogenia`. Le mode Serveur relève du chiffrement de SQL Server
  géré par l'exploitation (TDE, sauvegardes chiffrées), hors de l'application.
- À revoir si l'entreprise demande un poste sans chiffrement de disque ou un fichier transportable : ce serait une
  nouvelle version du format du fichier et une nouvelle ADR.

### Données personnelles (S-5.4-22)

Revue du modèle (`docs/modele-donnees.md` § 6) : les seules données personnelles stockées sont celles de la
spec 5.4.

| Donnée | Où | Catégorie de la spec 5.4 |
|---|---|---|
| Login Windows, `login_normalized` | `app_user` | login |
| Nom affiché | `app_user.display_name` | nom affiché |
| E-mail (de l'annuaire, facultatif) | `app_user.email` | e-mail |
| Langue préférée | `app_user.language` | langue |
| `created_by`, `updated_by`, `deleted_by`, `restored_by`, `closed_by`, `validated_by`, `changed_by` et leurs dates ; `operation_log.user_id`, `created_at` | tables de données, journal des opérations | auteur et date des contributions |
| `project_member` (rôle, commanditaire, contributeur clé), `lane.actor_user_id`, `step.assignee_user_id` | membres, couloirs de type personne, étapes | rattachement de la personne (par son identifiant) à un rôle du projet ou du processus, saisi par l'animateur ; aucune donnée nouvelle sur la personne |
| `audit_event` (qui, quand, quoi, identifiants seulement) | journal de sécurité | exigé par la spec 5.5, conservation paramétrable |

- **Aucune mesure d'activité individuelle** : pas de date de dernière connexion ni de compteur par personne ;
  `app_user.first_visit_done` et `last_project_id` sont des préférences d'affichage (AR-49), `is_admin` vient de la
  configuration (A-4-08).
- Les textes libres (libellés, fiches, documents) sont du contenu saisi par les utilisateurs ; l'application n'y
  ajoute rien de personnel.
- Les journaux de l'hôte ne contiennent ni contenu métier ni liste de membres (spec 5.2, 5.4, phase 4) ; le paquet
  `.topogenia` peut être exporté sans membres ni auteurs (phase 5).
- Toute colonne nouvelle qui porte une donnée sur une personne demande la mise à jour de ce tableau.

### Données personnelles dans les sauvegardes (porte P3, Sécurité m4)

- Une sauvegarde est une copie complète du fichier : projets (corbeille comprise), membres, utilisateurs, journal
  des opérations et audit, tels qu'ils étaient à sa date. Elle reste dans `backups\` du profil, sous les mêmes droits
  NTFS et le même chiffrement du disque que le fichier ; son dossier est refusé sur un partage ou dans un dossier
  synchronisé (ADR 029).
- Rétention : les 7 fichiers quotidiens se remplacent en une semaine ; les fichiers d'avant migration sont deux au
  plus et 30 jours au plus (ADR 029). Une donnée effacée du fichier (purge de la corbeille après
  `trash_retention_days`, 30 jours) quitte donc toutes les sauvegardes au plus 30 jours après son effacement, à
  condition que l'application soit lancée (la rotation a lieu à chaque sauvegarde). Cette garantie suppose que
  l'effacement soit physique : chaque connexion règle `PRAGMA secure_delete = ON` (`SqlitePragmas.Settings`), le
  contenu supprimé est recouvert de zéros dans le fichier, donc absent des pages libres que la sauvegarde recopie page
  par page. Sans ce réglage (porte P3 round 4, Sécurité MAJ-1), le nom, la description et les libellés d'un projet
  purgé restaient dans `data.db` et dans chaque nouvelle sauvegarde, 40 jours après la purge comprise. Preuve :
  `Purge/PurgedTraceTests` (textes trouvés dans le fichier avant la purge, absents du fichier et de la sauvegarde
  d'après ; rouge sans le réglage). Coût : l'écriture des zéros à chaque suppression. Un fichier créé avant ce réglage
  garde ses pages libres anciennes jusqu'à leur réemploi (aucun fichier réel n'existe encore en 2.0).
- Effacement à la demande d'une personne : l'effacer du fichier, puis supprimer le dossier `backups\` (ou attendre
  la rotation) ; la documentation d'exploitation (P7-T04) le dit. Le mode Serveur relève de la politique de
  sauvegarde de SQL Server de l'exploitation.
- Les messages d'échec d'une sauvegarde ne contiennent que des codes (ni chemin, donc ni login Windows, ni contenu) ;
  l'exception d'origine reste dans le processus pour le diagnostic local.

## Conséquences

- Un utilisateur qui place son fichier sur un partage ou dans OneDrive est arrêté avant toute ouverture, avec un
  code que l'hôte explique ; il garde ses données en local et échange par paquet.
- La confidentialité au repos du mode Poste dépend de la politique de chiffrement du poste ; la documentation
  d'exploitation (P7-T04) le dit.

## Vérification

`tests/Topogenia.Persistence.Tests/Files/DesktopDataFileTests` (chemins fictifs, vérifié par mutation) ;
`Files/DataFileResolutionTests` (`..`, liens fictifs vers un partage, un lecteur réseau, un dossier synchronisé,
chaîne et boucle de liens, volume monté ; vrais noms courts et vraie jonction dans un dossier temporaire ; mutations :
sans `GetFullPath`, sans suivi des liens) ; porte P3 round 2 : `DesktopDataFileTests` (chemins par `GLOBALROOT\Device\Mup`
et `LanmanRedirector` refusés `FICHIER_SUR_PARTAGE` avec leur partage, chemins relatifs, volume, périphérique refusés
`FICHIER_CHEMIN_NON_PRIS_EN_CHARGE` sans exception), `BackupRetentionTests` (dossier des sauvegardes par ces formes refusé
par son code, jamais levé) ; porte P3 round 3 : `DesktopDataFileTests` (lien physique fictif et vrai lien physique
fait dans un dossier temporaire refusés `FICHIER_LIENS_PHYSIQUES`, accepté de nouveau sans son second nom ; chemins avec
un NUL refusés par leur code) ; `Sqlite/BackupRetentionTests` (rotation, dossier refusé) ; revues
S-5.2-11 et S-5.4-22 sur ce document (`docs/couverture-p3/portage-b.md`).

## Révision (24/10/2026) : avertissement des dossiers lisibles par d'autres comptes (AR-98)

Question des droits NTFS laissée au jugement de la construction : avertissement non bloquant. Après l'ouverture du
fichier de données, l'hôte lit la liste de contrôle d'accès du dossier du fichier et de celui des sauvegardes
(`FolderExposure.ReadableByOthers`, lecture seule, Windows) : un dossier dont `Tout le monde`, `Utilisateurs` ou
`Utilisateurs authentifiés` peut lire les données, sans refus explicite, est nommé à la console avec la recommandation
de l'emplacement par défaut, et journalisé par sa place (`data`, `backups`), jamais son chemin. Le démarrage continue :
les données sont des cartes de processus, et un poste à un seul utilisateur peut légitimement placer ses fichiers
ailleurs. Vérification : `Files/FolderExposureTests` (liste réelle d'un dossier de test), `Desktop/DesktopStartupTests`.
