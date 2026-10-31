<!-- 044-hote-du-mode-poste.md -->
<!-- Hote du mode Poste : demarrage, jeton de lancement, cookie, controles des en-tetes, restauration proposee, journaux, arret -->

# 044 - Hôte du mode Poste

Statut : acceptée (date 24/10/2026)

## Contexte

L'ADR 010 fixe le principe du mode Poste (127.0.0.1, jeton de lancement échangé contre un cookie, contrôle de `Host`,
`Origin`, `Sec-Fetch-Site`) et la phase 3 a livré le démarrage du fichier SQLite (`DesktopDatabaseStartup`), la
sauvegarde en ligne, la restauration et la règle des partages et dossiers synchronisés. P4-T05 branche le tout dans
l'hôte. Plusieurs points restaient à trancher : le mode par défaut, la forme du jeton et du cookie, le sort d'une
écriture sans `Origin` ni `Sec-Fetch-Site`, la façon de « proposer » une restauration avant d'ouvrir le fichier (ADR 029),
la validation de `settings.json`, et la façon de tester l'API à plusieurs utilisateurs alors que le mode Poste n'en a
qu'un.

## Décision

- **Configuration** : `Mode` (`Desktop` par défaut : le mode Poste ne demande aucune configuration, spec 6.1 ;
  `Server` choisi explicitement), `Database:Provider` (`Sqlite`, `SqlServer`, spec 6.2 ; le mode Serveur avec SQLite est
  refusé au démarrage, `MODE_SERVEUR_SQLITE`), `Desktop:Folder`, `Desktop:Port` (0 : port libre), `Desktop:DataFile`,
  `Desktop:OpenBrowser`. Le fichier facultatif `settings.json` du dossier du produit donne `Port`, `DataFile`, `Language` ;
  une propriété inconnue ou une valeur fausse arrête le démarrage (`PARAMETRES_INVALIDES` et la propriété) au lieu d'être
  ignorée : une faute de frappe ne passe pas inaperçue.
- **Écoute** : Kestrel sur `127.0.0.1` seulement (`Listen(IPAddress.Loopback, port)`), jamais `0.0.0.0` ni `::`.
- **Jeton et cookie** : à chaque démarrage, un jeton de lancement (256 bits aléatoires) et un secret de session (256
  autres bits). Le navigateur est ouvert sur `/launch?token=...` ; l'échange pose le cookie `desktop_session` (`HttpOnly`,
  `SameSite=Strict`, `Path=/`, sans date d'expiration : cookie de session) et renvoie vers `/` sans le jeton dans
  l'adresse. Le jeton est dépensé par son premier échange : une adresse relue plus tard (historique, autre utilisateur du
  poste) n'ouvre rien. Comparaisons à temps constant. Le cookie est le seul moyen de s'authentifier (AR-17).
- **Identité** : le compte Windows du processus (`WindowsIdentity.GetCurrent()`), seul utilisateur ; administrateur de
  son fichier : les administrateurs du mode Poste sont ce compte plus ceux de la configuration.
- **Toute route exige l'authentification** (politique par défaut, A-1.1-02), santé comprise en mode Poste (AR-46) : un
  anonyme reçoit un 401 brut, sans JSON (A-1.1-12).
- **Contrôles avant toute lecture de la requête** : `Host` doit valoir exactement `127.0.0.1:port` (contre le rebinding
  DNS), sinon `REQUETE_INVALIDE` ; sur une écriture, un `Origin` autre que celui de l'application ou un `Sec-Fetch-Site`
  autre que `same-origin` ou `none` est refusé `ACCES_REFUSE`. Une écriture **sans** ces en-têtes est acceptée : un
  navigateur les envoie toujours, leur absence désigne un client qui n'est pas une page web, et il lui faut le cookie.
  L'en-tête anti-CSRF `X-Requested-With`, commun aux deux modes, vient avec P4-T07.
- **Démarrage** (service hébergé, avant que le serveur accepte une requête) : contrôle de l'emplacement du fichier
  (partage, dossier synchronisé, chemin non pris en charge, liens physiques : refus avec le lieu en cause), création des
  dossiers, `DesktopDatabaseStartup` (sauvegarde avant migration, migration, sauvegarde du jour). Un refus est écrit dans
  la console dans la langue de l'utilisateur (`Startup/messages.{fr,en}.json`) et le processus s'arrête avec un code
  non nul (AR-15). Échec de la sauvegarde d'avant migration : `MIGRATION_SANS_SAUVEGARDE` (AR-14). Échec de la sauvegarde
  du jour : journalisé, le démarrage continue (A-4-32).
- **Restauration proposée** (ADR 029) : après un refus qu'une sauvegarde peut réparer (`SCHEMA_MODIFIE`,
  `MIGRATION_MODIFIEE`, `EMPREINTE_ABSENTE`, `BASE_INACCESSIBLE` dont le journal refusé 26/538, `VERROU_MIGRATIONS`), la
  console liste les sauvegardes du fichier, de la plus récente à la plus ancienne, et la personne choisit un numéro ; la
  sauvegarde est restaurée, application arrêtée, et le démarrage repart une fois. Sans personne à la console (session
  non interactive, entrée redirigée), sans sauvegarde ou sans choix : rien n'est restauré, le refus tient, et la console
  donne le dossier des sauvegardes. `VERROU_MIGRATIONS` : fermer toute autre instance, sinon restaurer la sauvegarde
  d'avant la migration, ou retirer la ligne du verrou (utilisateur averti, ADR 041).
- **Journaux** : fichier quotidien dans `logs\` du dossier du produit, 30 jours (ADR 021) ; jamais un chemin, ni le
  message ou le `ToString()` d'une exception (type et numéros seulement, ADR 036).
- **Navigateur** : ouvert sur l'adresse d'échange une fois le serveur à l'écoute ; la console donne l'adresse sans le
  jeton et la façon de quitter. **Quitter** : `POST /api/desktop/quit` arrête l'application après la réponse (202) ; la
  route existe dans les deux modes, et répond `INTROUVABLE` en mode Serveur.
- **Arrêt** : une fois le serveur arrêté, `SqliteEngine.CloseAsync` reverse le journal dans le fichier et le ferme : aucun
  journal ne survit à un arrêt normal (ADR 029).
- **Tests** : l'API à plusieurs utilisateurs tourne sur SQLite **en mode Poste** (démarrage, sauvegardes et contrôles
  d'en-têtes compris, client à l'adresse `127.0.0.1:port`) et sur SQL Server en mode Serveur, avec l'authentification de
  test à la place de celle du mode (AR-17). Les tests propres au mode Poste démarrent l'exécutable pour de vrai (Kestrel
  sur un port libre, dossier temporaire) par la même fonction que lui (`Composition/HostApplication`).

## Conséquences

- Un second lancement qui rouvre le navigateur sur l'instance existante (verrou nommé, P7-T01) devra demander un nouveau
  jeton à l'instance lancée : le jeton actuel est à usage unique.
- Le nom de l'annuaire du mode Poste reste le login court (aucun annuaire lu) ; un nom complet viendrait d'une lecture
  du compte local, sans dépendance nouvelle.

## Vérification

`tests/Topogenia.Host.Tests/Desktop/` : `DesktopServerTests` (écoute, 401, jeton à usage unique, cookie, identité,
`Host`, `Origin`, `Sec-Fetch-Site`, fichiers créés, journal sans chemin, Quitter, fermeture du journal SQLite),
`DesktopStartupTests` (refus par emplacement, schéma, sauvegarde d'avant migration, sauvegarde du jour, mode Serveur avec
SQLite, chemins), `DesktopRecoveryTests` (restauration proposée, réglages, catalogue des messages) ;
`tests/Topogenia.Host.Tests/Logging/`.

## Révision (25/10/2026) : porte P4, round 1

- La section `Kestrel` de la configuration n'ajoute aucune adresse : seule l'écoute sur 127.0.0.1 existe.
- Les clés de protection des données d'ASP.NET Core sont gardées dans le dossier du produit (`keys`), protégées par DPAPI
  pour le compte de l'utilisateur (spec 6.1 : rien hors du dossier du produit).
- Limite connue, traitée par l'ADR 075 (porte P7 round 1 : toute connexion d'un processus d'un autre compte est
  fermée) : un cookie n'est pas lié au port (RFC 6265, 8.5). Un autre serveur local que le navigateur de
  l'utilisateur visite sur `127.0.0.1` pourrait recevoir le cookie `desktop_session` ; il ne vaut que pour l'exécution en
  cours (secret tiré à chaque démarrage) et le contrôle `Host` / `Origin` reste en place.
- Un second démarrage sur le même fichier est refusé (ADR 050, `DesktopInstance`).

## Révision (25/10/2026, round 2)

- Une panne du démarrage que le code ne prévoit pas (un port fixe déjà pris, par exemple) ne sort plus du processus :
  `Program.RunAsync` la rattrape, la console écrit `ERREUR_INTERNE` dans la langue de l'utilisateur (langues par défaut
  du code si les services sont eux-mêmes inutilisables), le journal du produit n'en reçoit que le type, et le code de
  sortie vaut 1. Le runtime n'écrit donc plus ni message, ni pile, ni chemin à la console, dans le journal Application
  de Windows ou dans un rapport d'erreur. Seule passe l'exception par laquelle les tests de l'API arrêtent l'hôte qu'ils
  ont construit (`HostAbortedException`).
- Un dossier du fichier de données ou des sauvegardes que le démarrage ne peut pas créer (accès refusé, fichier à sa
  place) refuse le démarrage avec `DROITS_INSUFFISANTS`, code existant réutilisé, qu'aucune sauvegarde ne répare (pas de
  restauration proposée) ; le journal donne le lieu (`data`, `backups`) et le type de la panne, jamais le chemin.
- Vérification : `DesktopStartupTests` (dossier impossible à créer, panne imprévue, avertissement AR-98 au vrai
  démarrage du mode Poste sur un dossier exposé par une ACL réelle).
