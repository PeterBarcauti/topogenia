<!-- 046-communs-en-tetes-tailles-audit.md -->
<!-- Communs aux deux modes : en-tetes de securite, limite du corps, journal des requetes, cache court du profil, journal d'audit et taches quotidiennes -->

# 046 - Communs : en-têtes, limites de taille, journal des requêtes, cache du profil, audit

Statut : acceptée (date 24/10/2026)

## Contexte

P4-T07 couvre les communs des deux modes (spec 5.4, 5.5) : en-têtes de sécurité, limites de taille appliquées avant la
lecture du corps, validation stricte, cache court du profil (faiblesse 1.x 5.6-6), journal d'audit et sa lecture
`GET /api/admin/audit`. La validation stricte (A-2.18) était déjà faite par le domaine et l'hôte (P4-T04). Restaient à
trancher : la politique exacte, le code et le statut d'un corps trop gros, le traitement d'un corps sans longueur
annoncée, le niveau des lignes du journal des requêtes, la durée et l'invalidation du cache, la liste des événements
d'audit, le sens de « refus d'autorisation répétés », la conservation, et le moment où tournent les tâches
quotidiennes (la purge de la corbeille, A-2.15-19, n'était encore lancée par aucun hôte).

## Décision

- **En-têtes** (`Security/SecurityHeaders`), sur toute réponse des deux modes, erreurs et redirections comprises
  (ajoutés au départ de la réponse, après la réécriture par l'intergiciel d'erreurs) :
  `Content-Security-Policy: default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data: blob:;
  font-src 'self'; connect-src 'self'; object-src 'none'; base-uri 'none'; form-action 'self'; frame-ancestors 'none'`,
  `X-Frame-Options: DENY` (l'en-tête 1.x, pour les navigateurs qui ignorent `frame-ancestors`),
  `X-Content-Type-Options: nosniff`, `Referrer-Policy: no-referrer`, `Permissions-Policy` fermée (caméra, micro,
  position, paiement, périphériques...), `Cross-Origin-Opener-Policy: same-origin`,
  `Cross-Origin-Resource-Policy: same-origin`. Les styles sont limités à `'self'` comme les scripts : l'interface
  (phase 6) charge ses feuilles de style et pose ses styles par le DOM, ce que la politique permet. Les exports PDF, PNG
  et Excel sous cette politique sont prouvés par le test de bout en bout de la phase 6 (spec 5.4).
- **Limite du corps** (`Security/BodySizeLimit`), après la garde des requêtes et avant toute lecture : la limite de la
  route (`IRequestSizeLimitMetadata`, que les imports de la phase 5 poseront à 10 et 100 Mo), sinon 1 Mo. Un corps
  annoncé plus long est refusé tout de suite, sans lecture ; un corps sans longueur annoncée est lu à travers un flux qui
  refuse dès qu'il passe la limite ; la limite du serveur est posée 1 Mo au-dessus, en dernier recours. Refus : nouveau
  code `REQUETE_TROP_GROSSE`, statut 413, détail `max` en Mo (nouveau genre de refus `TooLarge`). `FICHIER_TROP_GROS`
  (1.x, 400) reste celui du contenu d'un fichier importé.
- **Journal des requêtes** (`Logging/RequestLog`, A-4-26, comme la 1.x) : Error à partir de 500, Information pour une
  écriture sous `/api`, Debug sinon ; la ligne donne la méthode, le patron de la route (`/api/lanes/{id:guid}`, jamais
  ses identifiants), le statut, la durée et l'identifiant de l'utilisateur ; jamais un login, un nom, un corps ni la
  chaîne de requête (le jeton de lancement du mode Poste y passe), A-4-28.
- **Cache du profil** (`SignInCache`, application) : l'utilisateur d'un login est gardé 30 s après sa connexion ; une
  requête de l'utilisateur qui écrit l'oublie aussitôt ; le drapeau d'administrateur est comparé à la configuration à
  chaque connexion, donc un changement du fichier est vu tout de suite ; une seconde instance devant la même base voit un
  changement de profil en 30 s au plus.
- **Audit** (spec 5.5) : événements `user.created` (première connexion), `project.created`, `project.members_changed`
  (identifiants ajoutés, retirés, changés ; leurs nombres si la liste dépasse 4 000 caractères), `project.trashed`,
  `project.restored`, `project.purged` (par l'administrateur, ou sans utilisateur par la purge quotidienne d'un projet
  entier), `access.repeated_refusals` ; l'import, l'export et la duplication viennent avec la phase 5. Un événement
  d'une modification est écrit dans la même unité de travail que son jeu de changements (`ProjectCommand.AuditKind`,
  création de projet, première connexion) : pas d'événement sans changement, ni de changement sans événement ; les
  autres (purge, refus) sont ajoutés seuls (`IAuditStore.AppendAsync`, refusé dans une unité). Détails : identifiants et
  nombres seulement.
- **Refus répétés** : 10 refus `ACCES_REFUSE` d'un même utilisateur en 10 minutes font un événement, puis le compte
  repart. L'`INTROUVABLE` d'un non-membre n'est pas compté : c'est la réponse même d'un objet absent, que la navigation
  ordinaire rencontre, et le compter rendrait l'événement bruyant. Les comptes vivent dans le processus.
- **Lecture** : `GET /api/admin/audit?from&to&project_id&user_id&kind&limit`, administrateur seulement, du plus récent
  au plus ancien, 200 événements par défaut, 1 000 au plus ; `limit` hors bornes ou période inversée : `VALEUR_INVALIDE`.
- **Conservation et tâches quotidiennes** (`Maintenance/MaintenanceService`) : au démarrage puis toutes les 24 h, la
  purge de la corbeille (`ExpiredPurge`, A-2.15-19, qui n'était branchée nulle part) puis le retrait des événements
  d'audit plus vieux que `AuditRetentionDays` (365 par défaut). Une panne est journalisée par son type et n'arrête ni
  l'hôte ni le tour suivant.

## Conséquences

- L'interface (phase 6) ne doit utiliser ni script en ligne, ni `eval`, ni balise `<style>` en ligne.
- Le compte de l'application garde `DELETE` sur `audit_event` (retrait de la conservation) et n'a pas `UPDATE` (ADR 032).
- L'arrêt du mode Poste ferme le moteur SQLite capturé au démarrage : les services peuvent être déjà libérés à l'arrêt
  (constaté par les tests, défaut corrigé dans `StartupService`).

## Vérification

`tests/Topogenia.Host.Tests/Api/CommonHeadersTests` (en-têtes sur toute réponse, politique, 413 avant lecture et rien
d'écrit), `Desktop/DesktopServerTests` (corps sans longueur coupé à la limite, ligne du journal avec route et
identifiant, sans login ni chaîne de requête), `Logging/RequestLogTests` (niveaux), `Api/AuditRoutesTests` (lecture
par l'administrateur seul, filtres, refus répétés, retrait passé la conservation), `Api/RouteTableTests` ;
`Topogenia.Application.Tests` : `Audit/AuditTrailTests`, `Maintenance/ExpiredPurgeTests`, `Users/UserRunnerTests`
(cache) ; `Topogenia.Persistence.Tests/Reading/AuditStoreTests` (deux moteurs).

## Révision (25/10/2026) : conservation de l'audit (porte P4, round 1)

`AuditRetentionDays` nul ou négatif prend la valeur par défaut, 365 jours (`AuditTrail.RetentionDays`), comme la
conservation de la corbeille : une valeur erronée n'efface jamais tout le journal d'audit.
