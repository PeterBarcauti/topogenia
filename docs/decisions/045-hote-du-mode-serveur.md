<!-- 045-hote-du-mode-serveur.md -->
<!-- Hote du mode Serveur : reglages exiges, base controlee, Negotiate, sante anonyme, HTTPS, HSTS, noms autorises, anti-CSRF, If-Match exige, statiques, annuaire, journal -->

# 045 - Hôte du mode Serveur

Statut : acceptée (date 24/10/2026)

## Contexte

La spec 5.3 fixe le mode Serveur : authentification Windows, seule la santé anonyme (`{status}`), HTTPS et HSTS,
autorisation par projet, administrateurs relus à chaque changement du fichier, `If-Match` obligatoire (428), en-tête
anti-CSRF, fichiers statiques après l'authentification. L'annexe A ajoute la liste explicite des noms (A-4-19), le
journal dans un dossier configurable (A-4-25) et l'annuaire (A-2.1-25). Plusieurs points restaient à trancher : la
forme des réglages, l'ordre du 428 par rapport aux droits, le cas d'un enregistrement qui n'existe pas encore (fiche
jamais enregistrée, usage de document pas encore coché), la durée de HSTS, le code de la redirection, la place de
l'en-tête anti-CSRF (l'ADR 044 l'avait renvoyé à P4-T07), et la façon de tester Negotiate.

## Décision

- **Réglages** : une sous-section `Server` comme `Desktop` : `Server:AllowedHosts` (liste de noms) et
  `Server:LogsFolder`. Le démarrage est refusé sans SQL Server (`MODE_SERVEUR_SQLITE`), sans chaîne de connexion
  (`CHAINE_CONNEXION_ABSENTE`), ou sans liste de noms explicite : liste vide, nom vide ou nom avec `*`
  (`HOTES_AUTORISES_INVALIDES`). La clé `AllowedHosts: "*"` et la clé morte `Database:Engine` sont retirées de
  `appsettings.json`.
- **Base** : contrôlée au démarrage, jamais migrée par l'hôte (A-4-38 : `--migrate`, P4-T09). En retard :
  `BASE_EN_RETARD`. Refus du schéma : le code de `SchemaCodes`, avec une phrase propre au serveur (une base et son
  compte, pas un fichier) : clé `SERVEUR_<code>` ; `DROITS_INSUFFISANTS` portant 18456 : `SERVEUR_CONNEXION_REFUSEE`
  (ADR 036). Toute autre panne du contrôle : `ERREUR_INTERNE`. Le journal reçoit le code, la migration, le caractère
  passager et les numéros du moteur, ou le type de la panne : jamais un message ni un `ToString()` (ADR 036).
  `BASE_OCCUPEE` n'est pas rejoué : la console dit de réessayer, IIS relance le processus à la requête suivante.
- **Authentification** : Negotiate (Kerberos, sinon NTLM ; derrière IIS, l'authentification Windows d'IIS). La
  politique par défaut exige un utilisateur ; un anonyme reçoit le défi `WWW-Authenticate: Negotiate`, un 401 sans
  JSON (A-1.1-12).
- **Santé** : seule route anonyme, en mode Serveur seulement. Un anonyme reçoit `{status}` ; un utilisateur
  authentifié reçoit aussi l'état de la base, la version et l'heure. La santé ne demande pas d'authentification, donc
  le détail n'est servi qu'à un client déjà authentifié sur sa connexion (navigateur de l'application) ; une sonde de
  supervision reçoit `{status}`.
- **Transport** : HSTS d'un an (`max-age=31536000`, sans `includeSubDomains` ni `preload` : les autres noms de
  l'intranet ne sont pas tous en HTTPS) ; redirection de HTTP vers HTTPS en 308, qui garde la méthode d'une écriture.
  HSTS n'est pas envoyé à `localhost` (valeur par défaut d'ASP.NET Core, pour ne pas lier le navigateur d'un
  administrateur qui essaie sur le serveur). Depuis P7-T07 (F-05), HSTS est posé avant le middleware des erreurs et la
  garde des requêtes, et le middleware des erreurs le garde quand il récrit la réponse : un refus en HTTPS le porte
  comme un succès (`ServerRunTests.A_refusal_over_https_carries_hsts_too`).
- **Garde des requêtes, commune aux deux modes** (`Security/RequestGuard`) : `Host` doit nommer l'application (mode
  Poste : `127.0.0.1:port` ; mode Serveur : un nom de la liste, casse ignorée, port libre), sinon `REQUETE_INVALIDE`,
  avant l'authentification et avant la redirection. Sur une écriture : l'en-tête `X-Requested-With` (toute valeur non
  vide, comme `X-Ariane` en 1.x) est exigé, sinon 400 `EN_TETE_MANQUANT` ; puis `Origin` (mode Serveur : le schéma et
  l'autorité de la requête) et `Sec-Fetch-Site` comme dans l'ADR 044. L'en-tête anti-CSRF, prévu en P4-T07 par l'ADR
  044, arrive ici parce que le plan de P4-T06 le demande et que la garde est la même dans les deux modes.
- **If-Match exigé** : une route dont le gestionnaire lit `If-Match` change un enregistrement versionné ; la liste est
  trouvée par le paramètre de la route (`Api/IfMatchRequirement`) : une route du groupe de l'API qui lit l'en-tête y est
  soumise sans autre déclaration. Qu'une route d'écriture lise l'en-tête n'est pas déduit : un test exige que toute route
  d'écriture le lise ou figure, avec sa raison, dans la liste des exemptions (révision du 25/10/2026 ci-dessous).
  En mode Serveur, une requête sans l'en-tête est marquée (`IVersionRequirement`, port de l'application) et le gabarit
  la refuse 428 `VERSION_REQUISE` **après** les droits (un étranger reçoit toujours 404, un rôle insuffisant 403) et
  **avant** le domaine : le 428 ne révèle rien d'un projet, et l'ordre est celui du gabarit (droits, verrou, espace,
  versions, domaine). Les contrôles du corps faits par l'hôte (`CHAMP_OBLIGATOIRE`...) passent avant. En mode Poste,
  l'en-tête reste facultatif (AR-07).
- **Version 0** : un client qui n'a lu aucun enregistrement (fiche jamais enregistrée, usage pas encore coché) envoie
  `If-Match: 0` (`VersionCheck.NoRecord`) : accepté tant que l'enregistrement n'existe pas, `CONFLIT_VERSION` dès que
  quelqu'un l'a créé. Les enregistrements commencent à 1. La fiche renvoie `version: null` sans brouillon : le client
  envoie alors 0.
- **Objet supprimé depuis la lecture** : la version est contrôlée avant le domaine ; une écriture qui porte la version
  lue d'un objet supprimé depuis (sa suppression a avancé sa version) reçoit `CONFLIT_VERSION`, avec l'auteur et l'heure
  de la suppression, et non `INTROUVABLE`. Exemple : une seconde « remontée » de la même étape. En mode Poste, sans
  `If-Match`, la même requête reste `INTROUVABLE`.
- **Statiques** : servis après l'autorisation, dont la politique par défaut s'applique à une requête qu'aucune route
  ne prend : un anonyme reçoit 401 (A-4-04, faiblesse 1.x 5.6-9), dans les deux modes.
- **Administrateurs** : `ConfiguredAdministrators` lit la configuration par `IOptionsMonitor` : relue à chaque
  changement du fichier, jamais à chaque requête (A-2.1-26).
- **Annuaire** : `WindowsDirectory`, la reprise de l'`AnnuaireWindows` 1.x (domaine, sinon comptes locaux, sinon
  rien : le login court), par `System.DirectoryServices.AccountManagement` (MIT, Microsoft). Une panne est journalisée
  par son type, sans le login. Le mode Poste garde l'annuaire vide (ADR 044).
- **Journal** : fichier quotidien, 30 jours (ADR 021), dans `Server:LogsFolder`, relatif au dossier de l'application,
  par défaut `logs`.
- **Tests** : l'API en mode Serveur tourne en mémoire en HTTPS sur le nom `topogenia.test` ; le schéma Negotiate y est
  servi par l'authentification des tests (AR-17), le serveur en mémoire n'ayant pas la connexion que Negotiate lit. Le
  vrai Negotiate, HTTPS, HSTS, la redirection, les noms, les statiques, le fichier des administrateurs et le journal
  sont éprouvés sur Kestrel (`Server/ServerRunTests`), avec un certificat fait pour l'exécution (jamais installé) et le
  compte Windows des tests en NTLM sur la boucle locale. Le nom `localhost` sert aux requêtes authentifiées : Windows
  refuse NTLM en boucle locale sous un autre nom (contrôle de bouclage).

## Conséquences

- Le client web (phase 6) envoie `X-Requested-With` sur toute écriture et `If-Match` sur toute route qui le lit, 0
  quand il n'a lu aucun enregistrement.
- Le guide d'exploitation (P7) dira : pool sous un compte de service, authentification Windows d'IIS, liste des noms,
  dossier des journaux accessible en écriture au compte, certificat interne, `--migrate` sous un compte distinct.
- Les tests du mode Serveur qui écrivent sans `If-Match` ont été corrigés pour envoyer la version lue ; ceux qui
  prouvaient l'absence de contrôle la prouvent en mode Poste et prouvent le 428 en mode Serveur.

## Vérification

`tests/Topogenia.Host.Tests/Server/` : `ServerRunTests` (Negotiate, santé, HSTS, redirection, noms, anti-CSRF,
statiques, administrateurs, Quitter absent, journal, annuaire), `ServerStartupTests` (réglages, base en retard, base
absente, refus du schéma et leurs messages, panne du contrôle) ; `Api/IfMatchRequirementTests` (liste des routes, 428
après les droits, en-tête vide) ; `Api/CommonRulesTests` (santé selon le mode, statiques anonymes) ;
`Desktop/DesktopServerTests` (en-tête anti-CSRF) ; `Topogenia.Application.Tests` : `CommandTemplateTests`
(428 après les droits et avant le domaine, version 0 sur un enregistrement existant), `ProfileCommandsTests` (428 du
profil).

## Révision du 25/10/2026, porte P4 (Sécurité M2)

Constat du contrôleur : en mode Serveur, des modifications d'objets versionnés passaient sans `If-Match` (suppression
d'une étape, d'un couloir, d'une flèche, d'un document, décoche d'un usage, traductions d'une étape et d'une flèche,
remplacement des membres), et le test des routes comparait les routes qui lisent l'en-tête à une liste écrite à la main.

**Périmètre exigé** (spec 5.3 et D06 : « toute modification d'un objet versionné ») : toute route d'écriture qui modifie
ou supprime l'objet versionné nommé par sa route lit `If-Match` et sa commande contrôle la version de cet objet (même
forme qu'avant : 428 `VERSION_REQUISE` en mode Serveur après les droits, `CONFLIT_VERSION` sur une version périmée,
version 0 acceptée tant que l'objet n'existe pas ; facultatif en mode Poste, AR-07). 24 routes :

- déjà soumises (15) : `PATCH /api/me` (profil), `PUT /api/projects/{id}/sharing-group-label` (projet),
  `DELETE /api/maps/{id}` et `POST /api/maps/{id}/reattach` (sous-carte), `PATCH /api/lanes/{id}` (couloir),
  `PATCH /api/steps/{id}`, `POST /api/steps/{id}/detail`, `PUT /api/steps/{id}/raci`, `POST /api/steps/{id}/dissolve`
  (étape), `PUT /api/steps/{id}/sheet`, `POST .../sheet/validate`, `POST .../sheet/clarification` (fiche),
  `PUT /api/steps/{id}/documents/{documentId}` (document), `PUT /api/steps/{id}/uses/{documentId}` (usage),
  `PATCH /api/flows/{id}` (flèche) ;
- ajoutées par cette révision (9) : `DELETE /api/lanes/{id}` (couloir), `DELETE /api/steps/{id}` (étape),
  `PUT /api/steps/{id}/translations` (étape), `DELETE /api/flows/{id}` et `PUT /api/flows/{id}/translations` (flèche),
  `DELETE /api/steps/{id}/documents/{documentId}` (document), `DELETE /api/steps/{id}/uses/{documentId}` (usage),
  `PUT /api/projects/{id}/members` (projet), `DELETE /api/projects/{id}?confirmation=` (projet).
- ajoutée depuis (AR-155, 29/10/2026) : `POST /api/flows/{id}/reverse` (flèche), la vingt-cinquième.

Précisions sur les ajouts :

- **Traductions d'une étape ou d'une flèche** : la version contrôlée est celle de l'objet dont le libellé est traduit,
  comme les traductions d'un couloir passent par le `PATCH` du couloir. Une traduction ne fait pas avancer la version
  de l'objet : la version protège d'une traduction écrite sur un libellé changé depuis ; deux traductions concurrentes
  d'une même langue restent « dernier écrit gagne » (une traduction est un enregistrement par langue, sans route ni
  version lue par le client).
- **Membres** : la liste n'a pas de version propre ; la version contrôlée est celle du projet (lue dans
  `GET /api/projects/{id}`). Un remplacement qui change la liste fait avancer la version du projet (`Touch` du projet
  dans `MemberRules.Replace`, l'équivalent du `Touch` de l'étape par le RACI, AR-104), un remplacement identique ne
  l'avance pas : un animateur qui a lu les membres avant un remplacement concurrent reçoit `CONFLIT_VERSION` avec son
  auteur au lieu d'effacer ce remplacement.
- **Mise à la corbeille** : version du projet, lue dans le détail ; la liste de l'accueil ne la porte pas, le client
  lit le détail avant de demander la confirmation du code.
- **Décoche d'un usage** : version de l'usage, 0 quand le client n'en a lu aucun. L'usage est effacé (pas supprimé
  logiquement) ; une décoche dont l'usage n'existe plus réussit sans rien écrire, quelle que soit la version envoyée
  (`VersionCheck.OfErasure`, AR-107 : l'état demandé est atteint) ; un usage recoché depuis reste `CONFLIT_VERSION`, car
  il ne reprend jamais une version déjà lue (AR-111, révision du round 2 ci-dessous).

**Exemptions** (routes d'écriture qui ne lisent pas `If-Match`), chacune avec sa raison, écrites dans le test :

| Route | Raison |
|---|---|
| `POST /api/projects`, `POST /api/maps/{id}/lanes`, `.../steps`, `.../flows`, `POST /api/steps/{id}/documents` | Création : aucune version à avoir lue |
| `POST /api/maps/{id}/command`, `POST /api/maps/{id}/paste` | Créent des étapes et des flèches (tout ou rien) |
| `POST /api/steps/{id}/insert` | Crée une étape à côté de l'étape de la route, qui est un repère et non l'objet modifié |
| `PUT /api/maps/{id}/layout`, `POST .../move`, `POST .../deletion`, `POST .../transfer`, `POST .../group` | Plusieurs objets nommés par le corps, chacun contrôlé vivant par le domaine, en une opération de structure sérialisée par le verrou du projet et annulée d'un bloc |
| `PUT /api/maps/{id}/lane-order` | Liste complète des couloirs, refusée `ORDRE_COULOIRS_INVALIDE` si un couloir a été ajouté ou supprimé depuis |
| `PUT /api/projects/{id}/translations` | Import d'un classeur en une passe (A-2.14-16) : une ligne par texte de n'importe quel objet, sous le verrou du projet |
| `POST /api/maps/{id}/undo`, `POST /api/maps/{id}/redo` | Le journal contrôle que chaque objet touché a encore le contenu laissé par l'opération (AR-81, `OPERATION_DEPASSEE`) |
| `POST /api/trash/{id}/restore`, `DELETE /api/trash/{id}` | Un projet à la corbeille est figé, aucune route ne le modifie (A-2.15-14) ; une seconde restauration ou une purge le trouve parti (`INTROUVABLE`), une restauration entre le plan et la purge arrête la purge (`CONFLIT_VERSION`) |
| `POST /api/dissolutions/{id}/assign`, `.../remove`, `.../finish` | Un élément du reliquat, trouvé dans le cadre ouvert ou refusé (`INTROUVABLE`, `REMONTEE_TERMINEE`) ; la fermeture est idempotente |
| `PUT /api/me/last-project` | Trace de navigation écrite par le client à l'ouverture d'un projet (AR-49), pas une saisie : aucun contenu lu n'est remplacé |
| `POST /api/desktop/quit`, `* /api/{**rest}` | Aucun enregistrement (arrêt du mode Poste, route inconnue de l'API ; la page de l'application, `MapFallbackToFile`, ne répond qu'à `GET` et `HEAD`) |

**Ce que le test garantit** : `IfMatchRequirementTests.Every_write_route_reads_if_match_unless_it_is_exempted_with_its_reason`
énumère toutes les routes de l'hôte, une par méthode (une route sans méthode déclarée compte comme une écriture) ; toute
route qui n'est ni `GET` ni `HEAD` lit `If-Match` ou figure dans les exemptions avec une raison non vide ; une exemption
ne lit pas l'en-tête et nomme une route qui existe ; aucune lecture ne lit l'en-tête. Une route d'écriture nouvelle fait
donc échouer le test tant qu'elle n'est pas classée. Il ne garantit pas que le classement soit juste (c'est la revue de
la liste, raison par raison), ni que la commande contrôle la version du bon objet (tests HTTP `Api/IfMatchCoverageTests`
et d'application `Versions/IfMatchCommandsTests`, route par route), ni que la route soit déclarée dans le groupe de l'API
où le filtre s'applique (toutes les routes le sont, dans `Endpoints/ApiRoutes`).

**Conséquences** : le client web envoie `If-Match` sur les 24 routes. `PUT /api/me/last-project` change le profil, qui
est versionné : le client reprend la version du profil rendu par la route ; un `CONFLIT_VERSION` sur le profil dont
l'auteur est l'utilisateur lui-même (deux onglets) est réglé par le client sans lui : il relit le profil et renvoie les
mêmes champs (AR-106, phase 6). Les traductions concurrentes d'une même langue restent « dernier écrit gagne » (AR-105).

**Vérification** : `Topogenia.Host.Tests` : `Api/IfMatchRequirementTests` (classement de toutes les routes d'écriture),
`Api/IfMatchCoverageTests` (les 9 routes ajoutées, deux moteurs : 428 sans l'en-tête en mode Serveur et rien d'écrit,
`CONFLIT_VERSION` et rien d'écrit, succès avec la version lue, facultatif en mode Poste) ; `Topogenia.Application.Tests` :
`Versions/IfMatchCommandsTests` (les 9 commandes : sans version rien n'est contrôlé, version périmée `CONFLIT_VERSION` sur
l'objet de la route et rien d'écrit, version lue acceptée ; version du projet avancée par un remplacement des membres qui
change la liste, pas par un remplacement identique).

## Révision (25/10/2026) : HTTPS exigé, journaux, repli de l'interface (porte P4, round 1)

- **HTTPS exigé** (`Security/HttpsOnly`) : sans adresse HTTPS connue, la redirection d'ASP.NET Core laisse passer la
  requête en clair. Le mode Serveur la refuse alors 403 `HTTPS_EXIGE`, santé comprise, avant l'authentification : ni
  identifiants ni données ne circulent en clair (spec 5.1, 5.3). Derrière IIS, la liaison HTTPS du site fait arriver la
  requête en HTTPS.
- **Journaux** (deux modes) : la racine du contenu est le dossier de l'exécutable, quel que soit le dossier courant ; le
  fichier quotidien est le seul journal (les fournisseurs par défaut, dont le journal Application de Windows, sont
  retirés) ; les catégories du cadre (`Microsoft.`, `System.`) n'y sont écrites qu'à partir des erreurs, quoi que dise la
  configuration : elles nomment la racine du contenu, la racine web, les clés et la chaîne de requête.
- **Repli de l'interface** (A-1.5-22, comme la 1.x) : toute autre adresse rend `index.html` à un utilisateur authentifié ;
  une route inconnue sous `/api` reste `INTROUVABLE`, un fichier absent 404.

## Révision (25/10/2026, round 2) : version d'un usage recoché (porte P4, Données M1)

Le round 2 a montré qu'un usage décoché puis recoché repartait à la version 1 : la décoche d'un client qui avait lu la
version 1 de l'usage effacé passait et effaçait la saisie d'un autre (et son irritant, par la règle du retrait unique).
La phrase « un usage recoché depuis reste `CONFLIT_VERSION` » n'était vraie que pour une version que personne n'avait
lue. Correction (AR-111) : au commit, un enregistrement dont la clé est faite d'autres identifiants (usage, membre,
RACI, traduction) prend à sa création la version de structure du projet, que le verrou de chaque commande avance
avant le chargement ; il ne reprend donc jamais une version de l'enregistrement effacé. La modification d'un usage
(`PUT .../uses/{docId}`) avec la version de l'usage effacé est refusée de la même façon.
