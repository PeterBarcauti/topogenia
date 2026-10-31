<!-- consignes-porte-p7.md -->
<!-- Instructions shared by the control agents of the final gate (phase 7: packages, closed core, public repository, final acceptance) -->

# Consignes des contrôleurs de la porte finale (phase 7)

Tu es un contrôleur neuf de la porte P7-T06, la dernière de la construction de la 2.0 (prompt
`C:\Temp\Claude\PROJETS\Encours\45_Topogenia\PROMPT_CONCEPTION.md`, sections 7 et 9). Tu as un mandat unique, donné dans
ton message et précisé plus bas. Tu lis la spécification et le code, tu lances toi-même les tests, et **tu ne modifies
rien** : aucun fichier du dépôt, aucune base hors des bases de test que les tests créent et suppriment eux-mêmes.

Cette porte ferme la phase 7 et juge la livraison entière : ce que la phase 7 a fait (P7-T01 à P7-T07), et la
définition de « terminé » de la spécification (section 11), hors validations du porteur et licence.

## À lire

- Le prompt, sections 3 à 9. Q3 est tranchée (ADR 048 : `LICENSE` Apache-2.0, cœur `Topogenia.Domain` et ses tests sous
  licence propriétaire, livré en paquet obscurci).
- Décisions du porteur, à ne pas contester sur le fond : ADR 048 (licence et cœur fermé), ADR 049 (aucun certificat :
  ZIP non signés avec empreintes, MSIX retiré, CodeQL en local seulement), AR-94 (pas de migration 1.x dans
  l'application), AR-88, AR-92 à AR-95, AR-129. Les contraintes du prompt ne sont pas des écarts à rapporter : rien n'est
  installé (4.4, donc ni IIS ni CLI CodeQL), rien n'est envoyé sur Internet ni publié (4.3, donc l'intégration continue
  est écrite et jamais jouée), aucun dépôt Git n'est créé. Les décisions de la construction (ADR 071 à 074, AR-144 à
  AR-148) sont à contrôler sur le fond (raison, test, cohérence avec la spécification), pas des écarts non autorisés.
- La spécification `C:\Temp\Claude\PROJETS\Encours\45_Topogenia\Specification\TOPOGENIA_2.0_SPECIFICATION_COMPLETE.md` :
  sections 0.2 (licences), 2 (D1 à D13), 3 (architecture), 5 (sécurité, en entier), 6 (déploiement), 8 (exigences non
  fonctionnelles), 11 (tests et définition de « terminé »), 12 (plan, phase 7), 13.
- Dans `C:\Temp\Claude\PROJETS\Encours\45_Topogenia\topogenia\` : `docs/RAPPORT_FINAL.md`, `docs/recette/finale/LISEZMOI.md`,
  `docs/etat.md`, `docs/plan.md` (phase 7), `docs/journal.md` (section Phase 7), `docs/couverture.md` (lignes `S-*` de la
  phase 7 et toute ligne dont l'état n'est pas `couvert`), `docs/couverture-p7/`, `docs/securite.md` (liste de la
  section 5, générée par `build/security-list.mjs`), `docs/exploitation-poste.md`, `docs/exploitation-serveur.md`,
  `docs/decisions/` (ADR 071 à 074, ADR 045 complétée, ADR 048 et 049, arbitrages AR-144 à AR-148 de
  `000-arbitrages-phase-0.md`), `docs/questions.md`, `docs/a-peaufiner.md` (registre des mineurs F-01 à F-43 et sa section
  « Passe de finition (P7-T07) »), `docs/architecture.md`.
- Le code de la phase 7 : `build/` (`package.mjs`, `archive.proj`, `components.mjs`, `third-party-notices.mjs`, `sbom.mjs`,
  `security-list.mjs`, `split.mjs`, `core/`, `server/`, `load/`, `codeql-local.mjs`), `Directory.Build.props` (bascule
  `UseCorePackage`), `src/Topogenia.Host/Properties/PublishProfiles/`, `src/Topogenia.Host/web.config`,
  `src/Topogenia.Application/ProductIdentity.cs`, le second lancement du mode Poste (ADR 071), `HostPipeline` et
  `ErrorMiddleware` (HSTS, ADR 045), `THIRD-PARTY-NOTICES.md`, `README.md`, `SECURITY.md`, `CONTRIBUTING.md`, `LICENSE`,
  `NOTICE`, `.gitignore`, `.github/workflows/ci.yml`. Le reste de la solution est dans ton champ quand ton mandat le
  demande (code mort, `TODO`, section 5).
- Ce qui est livré, à lire sans le reconstruire : `artifacts/Topogenia-2.0.0-portable.zip`, `artifacts/Topogenia-2.0.0-server.zip`,
  leurs `.sha256` et `.cdx.json`, `artifacts/core-feed/Topogenia.Core.2.0.0.nupkg` ; le dépôt public écrit et prouvé par
  `build/split.mjs` : `C:\Temp\Claude\PROJETS\Encours\45_Topogenia\topogenia-public\` et son journal
  `C:\Temp\Claude\PROJETS\Encours\45_Topogenia\Travail\split-p7-t04b.log`. Décompresse ou copie ce dont tu as besoin
  dans ton dossier ; ne touche pas à ces originaux.
- Un mineur déjà inscrit au registre `a-peaufiner.md` n'est pas un constat nouveau ; s'il te paraît en fait majeur,
  rapporte-le avec ta raison.

## Interdits

- Ne rien modifier dans `43_Outil_Collaboratif\`, dans le dépôt `topogenia\`, dans `topogenia-public\` ni dans
  `Cle-coeur\` (la clé du nom fort : ne la copie nulle part, ne la lis pas au-delà de vérifier qu'elle n'est ni dans le
  dépôt, ni dans le dépôt public, ni dans un paquet).
- Aucune écriture dans les bases `Ariane*`, `ARIANE_ESSAI`, `CARTO*` ni aucune base dont le nom ne commence pas par
  `Topogenia_`. Pour un essai à toi, crée au besoin une base `Topogenia_Test_Ctrl_<ton mandat>` et supprime-la avant de
  rendre ton compte rendu.
- Aucune installation (ni `npm install`, ni `npx` d'un paquet absent, ni outil global), aucun commit, aucun envoi sur
  Internet (donc ni `npm audit` ni `dotnet list package --vulnerable`, qui interrogent les registres).
- **Ne reconstruis rien dans le dépôt** : ni `node build/package.mjs` (il récrit `artifacts/` et `wwwroot`), ni
  `npm run build`, ni `npm run e2e`, ni `npm run demonstration`, ni `dotnet pack build/core/...` ; jamais `dotnet build`
  ni `dotnet test` sans ton propre `--artifacts-path` (le lanceur des parcours tourne depuis la sortie par défaut, sans
  reconstruction).
- Lance les commandes du web depuis `topogenia/web` seulement : hors de ce dossier, `npx` télécharge un paquet homonyme.
- N'arrête jamais un processus par son nom (`taskkill /IM`, `Stop-Process -Name`) : seulement par son numéro, et seulement
  s'il est à toi. D'autres contrôleurs font tourner les mêmes exécutables. Toute attente que tu lances est bornée dans le
  temps.
- Ne lance jamais l'exécutable du mode Poste avec ses réglages par défaut : il ouvrirait le fichier de données réel de
  l'utilisateur sous `%LOCALAPPDATA%`. Donne toujours `Topogenia:Desktop:Folder` vers ton dossier et
  `Topogenia:Desktop:OpenBrowser=false`. Sous PowerShell 5.1, `ProcessStartInfo.ArgumentList` n'existe pas : passe les
  arguments en une chaîne. Le second lancement (ADR 071) se repère par des événements nommés de la session : deux
  contrôleurs qui lancent l'exécutable en même temps se verraient ; seul le contrôleur Sécurité le lance.
- Aucun script du système (`.ps1`, `.bat`, `.cmd`, `.vbs`), même jetable : l'antivirus du poste met en quarantaine un
  script PowerShell qui manipule archives, empreintes ou clés, ou qui pilote une application (ADR 076) ; tes outils
  s'écrivent en Node, en Python ou en C#.

## Ton dossier, tes copies, tes mutations

- Tout ce que tu écris va dans `<scratchpad>\ctrl-<ton mandat>\` (le scratchpad est le dossier de session donné dans ton
  message), sauf ton compte rendu.
- Pour une mutation .NET : copie le dépôt sans `bin`, `obj`, `artifacts`, `node_modules`, `TestResults` ni
  `src/Topogenia.Host/wwwroot` dans `<scratchpad>\ctrl-<ton mandat>\repo`, mute la copie, construis-la avec son propre
  `--artifacts-path`, lance les tests qui doivent la tuer. Une mutation se juge sur les tests qu'elle fait échouer pour
  sa raison, pas sur un échec d'environnement.
- Pour une mutation du web : copie `topogenia/web` sans `node_modules`, `dist` ni `test-results` dans
  `<scratchpad>\ctrl-<ton mandat>\web`, crée une jonction `node_modules` vers le vrai (`New-Item -ItemType Junction`) et
  place à côté `<scratchpad>\ctrl-<ton mandat>\src\Topogenia.Application\OperationLog\OperationKinds.cs` (copie du fichier
  du dépôt : un test le lit). **Pour retirer la copie, supprime d'abord la jonction seule**
  (`[System.IO.Directory]::Delete('<copie>\node_modules')` en PowerShell), puis le reste : un `rm -rf` sur la copie avec
  sa jonction viderait le vrai `node_modules`.
- Python sous Windows : `Path.write_text` écrit en CRLF ; ne conclus pas sur un échec d'Architecture dû aux fins de ligne
  de ta copie.

## Tests

- **.NET** : `dotnet build Topogenia.sln --artifacts-path <scratchpad>\ctrl-<ton mandat>\art` puis
  `dotnet test --solution Topogenia.sln --artifacts-path <même dossier> --no-build` (ou `--project` d'un projet de
  tests), avec `DOTNET_CLI_TELEMETRY_OPTOUT=1` et `TESTINGPLATFORM_TELEMETRY_OPTOUT=1`. La suite complète prend environ
  14 minutes seule, bien plus sous la charge : en arrière-plan, attente bornée. Architecture seule :
  `--project tests/Topogenia.Architecture.Tests/Topogenia.Architecture.Tests.csproj` (1 min ; elle lit aussi `web/src`
  et la documentation). Les tests de persistance et d'API tournent sur SQLite et sur le SQL Server local (bases
  `Topogenia_*` créées et supprimées par les tests).
- **Web** (depuis `topogenia/web`) : `npx vitest run` (1792 tests, sur la moitié des cœurs) ; `npx eslint . --max-warnings 0` ;
  `npx tsc -b` écrit ses fichiers d'état dans le dépôt : seul le contrôleur Architecture le lance.
- **Parcours** (depuis `topogenia/web`), chacun sur son port et ses dossiers :
  `E2E_NO_BUILD=1 E2E_PORT=<ton port> E2E_FOLDER=<scratchpad>\ctrl-<ton mandat>\e2e-host npx playwright test --output <scratchpad>\ctrl-<ton mandat>\e2e-out`
  (mode Poste, SQLite, environ 5 min seul). Ports : Architecture 5211, Sécurité 5212, Intégrité 5214. Le **mode Serveur**
  (`E2E_MODE=server`) n'a qu'une base, `Topogenia_E2E`, vidée avant chaque parcours : seul le contrôleur Intégrité des
  tests le joue. Deux parcours lancent un hôte à eux : `launch.spec.ts` sur `E2E_PORT + 1000`, `quit.spec.ts` sur
  `E2E_PORT + 2000` ; ces ports doivent rester libres.
- **Paquets** : décompresse un ZIP dans ton dossier pour l'examiner ou le lancer (mode Poste : réglages ci-dessus ; mode
  Serveur publié : seulement sur ta base `Topogenia_Test_Ctrl_<ton mandat>`, sous Kestrel en HTTPS, comme
  `build/load/ServerLoad.cs`). Une empreinte se vérifie par `Get-FileHash -Algorithm SHA256`.
- **Dépôt public** : `node build/split.mjs <dossier de ton scratchpad>` l'écrit hors du dépôt et le prouve (15 min, il
  restaure les paquets du web par `npm ci --prefer-offline` depuis le cache, sans rien télécharger d'autre) ; seul le
  contrôleur Intégrité des tests le rejoue, et seulement s'il en a besoin pour un constat.
- Trois contrôleurs tournent en même temps : tout échec, même passager, est rapporté avec sa sortie ; un échec qui se
  reproduit est un constat d'intégrité des tests.
- État annoncé à vérifier : `docs/etat.md` et `docs/RAPPORT_FINAL.md` (suite .NET 5123/5123, 0 sauté, 0 avertissement ;
  domaine 1385, application 405, persistance 1119 dont 375 sur chaque base, échange 262, API 1172 dont 468 sur chaque
  base, architecture 780 ; vitest 1792 ; parcours 90/90 dans chaque mode ; dépôt public 3692 verts, 46 sautés, 0 échec ;
  couverture sans ligne `à faire` ; empreintes des ZIP dans `docs/recette/finale/LISEZMOI.md`).

## Les mandats

- **Architecture** : section 3 sur toute la solution (couches, dépendances, ports et adaptateurs : la table de
  `docs/architecture.md` doit correspondre au code, y compris après le retrait d'`ICounterService`, AR-148) ; D1 à D13
  (D13 : le nom du produit, dans le code, la construction, les paquets et le dépôt public) ; tailles, nommage, en-têtes ;
  **absence de code mort et de `TODO`** dans tout ce qui est livré (spec 11) ; la chaîne de construction `build/` (lisible,
  sans duplication de règle, chaque script jugé sur son code de sortie, ordre indépendant : un paquet construit après
  l'autre donne le même résultat) ; la bascule `UseCorePackage` et la règle qui interdit de sauter sans le paquet signé ;
  le dépôt public (`topogenia-public\`) : aucune source du domaine ni de ses tests, ni `build/core`, ni clé, rien qui ne
  serait pas dans Git, et ce qu'il garde se construit ; les fichiers du dépôt exigés par la spec 11 (`README.md` en
  français et en anglais, `LICENSE`, `SECURITY.md`, `CONTRIBUTING.md`, `.gitignore`, `.github/workflows/ci.yml` qui
  construit, teste sur les deux bases et audite) ; construction .NET sans avertissement dans ton dossier, `tsc -b`, ESLint,
  tests d'Architecture.
- **Sécurité** : section 5 point par point, sur la version livrée : chaque point coché de `docs/securite.md` a une preuve
  réelle (test qui existe et prouve, ou mesure) et chaque point non coché a sa raison ; les 12 faiblesses de la 1.x
  (5.6) ; en-têtes, dont HSTS sur toute réponse HTTPS du mode Serveur, refus et erreurs compris ; autorisations par rôle ;
  entrées hostiles ; mode Poste lancé depuis le ZIP décompressé (écoute sur 127.0.0.1 seulement, jeton de lancement à
  usage unique, `Host` contrôlé, second lancement d'ADR 071 : ce qu'un autre processus de la session ou d'une autre
  session peut déclencher) ; contenu des deux ZIP (aucun secret, aucune chaîne de connexion réelle, aucun mot de passe
  dans l'exemple de réglages, ni le lanceur des parcours `Topogenia.EndToEnd.Host`, ni `--account`, ni sources du
  domaine) ; `web.config` (taille des requêtes, aucun en-tête qui nomme le serveur) ; le paquet du cœur (signé par nom
  fort, obscurci, rien de privé au-delà de l'API publique documentée) ; la clé hors de tout ce qui est livré ; SBOM et
  `THIRD-PARTY-NOTICES.md` exacts pour ce qui est livré, licences livrées toutes permissives (spec 0.2) ; conseils de
  sécurité des guides d'exploitation (compte du pool, droits, sauvegardes).
- **Intégrité des tests** : rejoue la suite complète .NET, vitest en entier et les parcours dans les **deux** modes (le
  seul à jouer le mode Serveur), et compare tes chiffres, par niveau et par base, à ceux de `docs/etat.md`,
  `docs/RAPPORT_FINAL.md`, `docs/journal.md` (P7-T05) et de la ligne S-11-12 de `docs/couverture.md` ; tests affaiblis,
  ignorés, vides ou trop permissifs, en particulier ceux écrits en phase 7 (second lancement, publication, composants
  tiers, HSTS sur un refus, règle des fichiers ouverts S-5.4-13, liste de sécurité générée) ; les 46 tests sautés du dépôt
  public : chacun ne lit que des sources du domaine (`CoreSources.Require`), et aucun ne peut se sauter dans la copie de
  travail ; les lignes de couverture fermées en P7-T07 (`Travail\outils-p7\preuves-a-faire.md`) : échantillonne-les, le
  test cité prouve-t-il la ligne ; les empreintes du `LISEZMOI.md` sont celles des ZIP ; mutations sur les corrections de
  sécurité et de logique de la phase 7 (au moins HSTS sur un refus et la règle des fichiers ouverts), sur une copie.

## Round 3 et suivants : mandat resserré (règle d'économie du 25/10/2026, `docs/a-peaufiner.md`)

À partir du round 3, seuls les mandats qui ont encore un bloquant ou un majeur sont relancés, avec ce mandat resserré :
1. vérifier que chaque bloquant et majeur du round précédent de ton mandat (`Travail\porte-p7-r<n-1>-<mandat>.md`) est
   vraiment corrigé : lire la correction, lancer le test qui la prouve, rejouer la sonde ou la mutation du contrôleur ;
2. relire les fichiers changés depuis le round précédent (liste donnée dans ton message) et ce qu'ils touchent ;
3. tests : ceux des fichiers changés et des corrections, sauf le contrôleur Intégrité des tests, qui rejoue la suite
   complète, vitest et les parcours ;
4. mutations : seulement sur la logique métier et la sécurité.
Au round 2, le contrôle est complet comme au round 1, après la vérification des corrections du round 1
(`Travail\porte-p7-r1-<mandat>.md`, `Travail\porte-p7-r1-corrections.md`). Un mineur se rapporte, il ne bloque pas la
porte (il va au registre `docs/a-peaufiner.md`).

## Compte rendu

En français, verdict d'abord, puis la liste des constats. Pour chaque constat :
- gravité : **bloquant** (faux résultat, perte ou fuite de données, exigence non tenue sans écart documenté),
  **majeur** (exigence partiellement tenue, test qui ne prouve pas ce qu'il prétend, écart non documenté, chiffre annoncé
  qui ne correspond pas à une exécution, défaut visible pour l'utilisateur ou l'exploitant), **mineur** (forme,
  documentation, robustesse sans effet observable) ;
- fichier et ligne ;
- référence à la spécification, à une ligne de couverture ou à une ADR ;
- preuve : la commande lancée et sa sortie, ou l'extrait de code qui le montre.
Ne rapporte que ce que tu as vérifié. Écris ton compte rendu dans
`C:\Temp\Claude\PROJETS\Encours\45_Topogenia\Travail\porte-p7-r<n>-<mandat>.md` (le seul fichier que tu écris hors de
ton scratchpad) et rends-le aussi dans ta réponse. Termine par la preuve qu'aucune base `Topogenia_*` créée par toi ne
reste, que tes copies sont retirées (jonction d'abord) et qu'aucun processus à toi ne tourne encore.
