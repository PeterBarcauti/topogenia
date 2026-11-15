<!-- 071-publication-du-mode-poste-et-second-lancement.md -->
<!-- The portable ZIP of the desktop mode (one self-contained executable, native libraries beside it) and the second start that opens the browser on the running application (P7-T01, AR-144) -->

# 071 - Publication du mode Poste et second lancement

Statut : acceptée (date 29/10/2026)

## Contexte

La spec 6.1 veut un exécutable unique autonome (`PublishSingleFile`, runtime inclus, `win-x64`), aucune écriture hors de
`%LOCALAPPDATA%\Topogenia\`, un premier lancement en moins de 5 secondes, et qu'un second lancement ouvre simplement le
navigateur sur l'instance déjà lancée. L'ADR 049 a retiré le MSIX : le ZIP portable non signé est le seul livrable du
mode Poste. Jusqu'ici, un second lancement était refusé (`INSTANCE_DEJA_LANCEE`, ADR 029, ADR 050) ; le jeton de
lancement ne sert qu'une fois (spec 5.2) et n'est donné qu'au navigateur : un second processus ne peut pas réutiliser
l'adresse du premier.

## Décision

1. **Profil de publication** `src/Topogenia.Host/Properties/PublishProfiles/Desktop.pubxml` : `win-x64`, autonome, fichier
   unique (non compressé depuis l'ADR 076) ; les bibliothèques natives (SQLite, pilote SQL Server) **à côté** de l'exécutable
   (`IncludeNativeLibrariesForSelfExtract=false`) : extraites, elles s'écriraient sous `%TEMP%` à chaque démarrage ; ni
   module IIS, ni symboles, ni documentation XML, ni `web.config` ; les fichiers de messages, déjà ressources de
   l'assemblage, ne sont plus copiés. L'hôte prend le dossier de l'exécutable comme racine (`AppContext.BaseDirectory`) :
   l'interface est trouvée quel que soit le dossier courant.
2. **Paquet** `node build/package.mjs Desktop` (`build/package.ps1 -Mode Desktop` jusqu'à l'ADR 076) : la publication, `README.txt` (`build/portable-readme.txt`, français et
   anglais), `licenses/` (Apache-2.0, `NOTICE`, licence du cœur, `THIRD-PARTY-NOTICES.md`, Inter OFL-1.1, Lucide ISC : les
   morceaux de l'interface n'ont plus de bannière), puis `Topogenia-<version>-portable.zip` et son empreinte SHA-256 dans
   `artifacts/` (hors du dépôt suivi). Aucun raccourci du menu Démarrer : sans installeur (ADR 049), le README dit de garder
   les fichiers ensemble.
3. **Second lancement** (AR-144) : deux événements nommés de la session de l'utilisateur (`Local\`, noms dérivés de celui
   du verrou du fichier de données, sans chemin) : « open », signalé par le second lancement, et « opened », rendu par
   l'application en cours (`Desktop/DesktopReopen.cs`). L'application en cours tire un jeton de lancement neuf
   (`DesktopSession.Renew` : l'ancien n'ouvre plus rien, la session d'un onglet déjà ouvert reste), ouvre le navigateur
   dessus (sauf `OpenBrowser=false`), l'écrit à la console et répond ; le second processus dit « L'application est déjà
   lancée : elle s'ouvre dans le navigateur. » et finit avec le code 0. Sans réponse en 5 s, ou si le verrou est tenu par
   un processus qui n'écoute pas (`--check`), le refus `INSTANCE_DEJA_LANCEE` reste.
4. **Écarts à la spec 6.1** : MSIX signé, installation par utilisateur, désinstallation et raccourci du menu Démarrer
   (S-6.1-01 à 03, S-6.1-08) : sans objet depuis l'ADR 049 (pas de certificat, pas d'installeur).

## Options écartées pour le second lancement

- Un canal nommé (`NamedPipeClientStream`) : un client de canal nommé peut joindre une autre machine par son partage
  (SMB), ce que la règle D14 du code livré refuse (`ShippedCodeDetectors.OutgoingTypes`) ; deux événements suffisent, rien
  ne passe que deux signaux.
- Une requête HTTP sur le port de l'application : un client HTTP dans le code livré (D14) et un jeton à faire circuler.
- Écrire l'adresse de lancement dans un fichier : le jeton ne sert qu'une fois et ne doit être donné qu'au navigateur
  (spec 5.2).

## Conséquences

- Vérifié sur l'exécutable décompressé (`docs/couverture-p7/verification-poste.md`) : prêt en 4,2 s au premier lancement,
  2,2 s ensuite ; second lancement en 1,4 s, code 0 ; écritures dans le seul dossier du produit, rien dans `%TEMP%`.
- Tests : `Topogenia.Host.Tests.Desktop.DesktopReopenTests` (trois), deux tests de `HostCommandsTests` (le second
  lancement qui passe la main, le verrou tenu sans réponse refusé) ; `Topogenia.Architecture.Tests.PublicationRulesTests`
  (profil, licences du paquet). Le test qui attendait le refus du second lancement est remplacé par celui qui attend la
  main passée : c'est le comportement de la spec 6.1.
- Pour qu'un test donne son navigateur et sa console, `IBrowserLauncher` et `StartupConsole` sont enregistrés par
  `TryAdd`.

## Révision (29/10/2026, porte P7 round 1, R4) : exécutable non compressé, paquet par Node (AR-150)

L'exécutable unique n'est plus compressé (`EnableCompressionInSingleFile=false`) : rien n'est décompressé en mémoire au
démarrage, marque qu'un antivirus lit comme celle d'un exécutable empaqueté ; le ZIP reste compressé. Le paquet se
construit par `node build/package.mjs Desktop`, sans script PowerShell (ADR 076).
