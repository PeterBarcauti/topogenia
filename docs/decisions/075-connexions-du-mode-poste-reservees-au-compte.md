<!-- 075-connexions-du-mode-poste-reservees-au-compte.md -->
<!-- The desktop mode keeps only the connections whose client process runs under its own Windows account, checked before a byte of HTTP is read (security M-1 and m-1 of the gate P7, spec 5.2 R449) -->

# 075 - Connexions du mode Poste réservées au compte

Statut : acceptée (date 29/10/2026)

## Contexte

La spec 5.2 (R449) veut qu'« un autre utilisateur du même poste (session à distance) ne [puisse] donc pas se
connecter » ; la ligne S-5.2-06 le reprend. Le mode Poste porte la session par le cookie `desktop_session` (`HttpOnly`,
`SameSite=Strict`, ADR 044). Or un cookie n'est pas lié au port (RFC 6265, § 8.5) : le navigateur l'envoie à tout
serveur de `127.0.0.1`. Le contrôle Sécurité de la porte P7 (constat M-1) l'a prouvé : un autre compte du poste écoute
sur un autre port de la boucle locale, attire l'utilisateur sur un lien, reçoit le cookie, puis le rejoue depuis son
propre client avec le bon `Host` et l'en-tête anti-CSRF, sans `Origin`. `RequestGuard` et l'authentification le
laissent passer, en lecture et en écriture, comme administrateur du fichier, jusqu'à l'arrêt de l'application. Sur un
serveur Citrix ou RDS partagé, c'est une fuite de données d'un utilisateur à l'autre. La révision du 25/10/2026 de
l'ADR 044 avait noté cette limite sans la traiter.

## Décision

1. **Le mode Poste ne garde que les connexions des processus de son propre compte Windows.** Sur une connexion de
   bouclage, Windows connaît le processus de chaque extrémité. À chaque connexion acceptée, avant que Kestrel en lise un
   octet, un intergiciel de connexion (`ListenOptions.Use`, `Desktop/DesktopConnectionGuard.cs`) cherche le processus
   client et son compte (`Desktop/WindowsConnectionOwners.cs`) :
   - `GetExtendedTcpTable` (iphlpapi, `AF_INET`, `TCP_TABLE_OWNER_PID_CONNECTIONS`) donne la ligne dont l'extrémité
     locale est l'extrémité distante de la connexion reçue (`127.0.0.1:<port éphémère>`) et dont l'extrémité distante
     est l'adresse d'écoute : son numéro de processus (une ligne en `TIME_WAIT`, sans processus, ne compte pas) ;
   - `OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION)`, `OpenProcessToken(TOKEN_QUERY)` et
     `GetTokenInformation(TokenUser)` donnent le SID du compte de ce processus ;
   - ce SID est comparé à celui du processus de l'application (`WindowsIdentity.GetCurrent().User`). Égal : la
     connexion continue. Différent, ou au moindre échec (ligne introuvable, accès refusé, processus disparu) : la
     connexion est fermée (`ConnectionContext.Abort`) sans aucune réponse HTTP.
2. **Journal** : un avertissement par connexion fermée, sans login, SID ni numéro de processus (ADR 036) : « A connection
   is closed unread: its client process runs under another account, or its account cannot be read. »
3. **Une fois par connexion**, pas à chaque requête : ce qu'une connexion transporte ensuite vient du processus qui l'a
   ouverte.
4. **Hors de Windows**, aucun propriétaire n'est jamais connu, donc toute connexion est refusée : le mode Poste est livré
   pour Windows seulement (spec 6.1, `win-x64`, ADR 071), et rien d'autre n'y dirait qui est à l'autre bout.
5. **Écoute** inchangée : IPv4 seulement, sur `127.0.0.1` (`kestrel.Listen(IPAddress.Loopback, port)`) ; la table lue est
   celle d'IPv4. Un client à socket double (socket IPv6 qui joint une adresse IPv4, cas de `HttpClient` et de
   `TcpClient()`) y figure aussi : les tests le vérifient.
6. **En-tête `Server`** : Kestrel ne l'écrit plus en mode Poste (`AddServerHeader = false`, constat m-1 de la même porte,
   ASVS V14.3.3), comme le `web.config` du mode Serveur sous IIS.
7. Le jeton de lancement, le cookie, les contrôles de `Host`, `Origin`, `Sec-Fetch-Site` et l'en-tête anti-CSRF
   restent : ils protègent toujours contre une page web ouverte dans le navigateur de l'utilisateur, que le contrôle du
   compte laisse passer puisqu'elle vient du même compte.

## Options écartées

- Un secret de session remis à la page par le fragment de l'adresse de redirection, puis exigé dans un en-tête de
  chaque appel de l'API, le cookie ne servant plus qu'aux fichiers de l'interface : change le contrat de l'API et
  l'interface, et la garantie tient encore au seul secret.
- Un nom d'hôte propre à chaque lancement (`<aléa>.localhost`) : dépend de la résolution de `*.localhost` par le
  navigateur et de son contournement par le serveur mandataire configuré ; la garantie tient encore au seul secret.
- Le contrôle du compte est la seule des trois à dire ce que dit la spec : un autre compte ne se connecte pas, quel que
  soit le secret qu'il a pu lire (historique, cookie reçu, copie de la mémoire du navigateur). Il ne change ni l'API, ni
  l'interface, ni l'adresse.

## Conséquences

- Un autre compte du poste, dans une autre session (bureau à distance, Citrix, RDS) ou par « Exécuter en tant
  qu'autre utilisateur », ne peut plus rien demander à l'application, même avec le cookie de la session ou un jeton de
  lancement : sa connexion est fermée avant la lecture de la requête.
- Le navigateur de l'utilisateur passe. Mesuré sur le poste de construction (session non élevée, sonde en lecture
  seule) : les 19 processus d'Edge de la session, ceux en bac à sable compris (rendu, stockage, audio), donnent leur
  compte par cette voie, le service réseau d'Edge compris. Les parcours de bout en bout (Chromium de Playwright, lancé
  par le même compte) passent par le même contrôle.
- Coût : une lecture de la table TCP IPv4 de la machine et trois appels système par connexion ; le navigateur garde ses
  connexions ouvertes, une page n'en ouvre que quelques-unes.
- Un compte qui ouvre des connexions en boucle fait écrire une ligne d'avertissement par connexion dans le journal de
  l'utilisateur, gardé 30 jours (ADR 021).
- Un navigateur lancé « en tant qu'administrateur » face à une application qui ne l'est pas peut être refusé si son
  jeton ne se laisse pas lire (non mesuré) : lancer les deux au même niveau.
- Pour qu'un test simule un propriétaire étranger, `IConnectionOwners` est enregistré par `TryAdd`, comme le navigateur
  et la console (ADR 071). Les autres tests qui démarrent le vrai serveur du mode Poste passent par la vraie résolution :
  le processus de test y est le client.
- L'intégration continue écrite (`.github/workflows/ci.yml`, `ubuntu-24.04`, jamais jouée) refuserait ces connexions :
  les tests qui démarrent le vrai serveur du mode Poste demandent un agent Windows.

## Ce qui reste hors d'atteinte

- Un processus du même compte : un autre programme de l'utilisateur, ou un logiciel malveillant qui tourne sous son
  compte. Il passe le contrôle ; il lit déjà le profil, les cookies du navigateur et le fichier de données.
- Le même compte ouvert dans deux sessions du même serveur : même SID, donc même utilisateur pour l'application.
- Un administrateur de la machine, ou un compte qui a le privilège de déboguer : il lit la mémoire du processus et
  ouvre tout jeton.
- Le cookie part toujours vers les autres serveurs de `127.0.0.1` que le navigateur visite : il n'y ouvre plus rien,
  mais le serveur qui le reçoit le voit.

## Vérification

`tests/Topogenia.Host.Tests/Desktop/DesktopConnectionTests.cs` : un cookie valide rejoué par un propriétaire étranger
simulé, ou inconnu, en lecture et en écriture, n'obtient aucun octet de réponse, et la même écriture venue du compte est
acceptée ; le journal n'a qu'une ligne fixe par refus, sans SID ; la décision du contrôle (compte seul, propriétaire
inconnu refusé) ; la vraie résolution Windows sur une vraie connexion de bouclage (client IPv4 et client à socket
double) rend le numéro du processus de test et le SID du compte courant, et aucun propriétaire pour une extrémité sans
connexion, pour la bonne extrémité vers un autre serveur, ou hors IPv4.
`DesktopServerTests.No_answer_of_the_desktop_mode_names_the_server` : 401, 302, 200 et 400 sans en-tête `Server`.
