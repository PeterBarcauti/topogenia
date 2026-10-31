<!-- README.md -->
<!-- Build, publication, signature and SBOM scripts of the developer, never shipped in the packages -->

# Construction

Scripts du développeur (spec 3.1) : construction, publication des paquets Poste et Serveur,
signature quand un certificat est fourni, SBOM CycloneDX, mentions des composants tiers.
Écrits en phase 7 (P7-T01 à P7-T03), sauf le générateur des mentions tierces. Aucun de ces
scripts n'est livré ni exécuté par l'application (AR-37).

## Paquets (ADR 049, ADR 071 à 074, ADR 076)

- `node build/package.mjs Desktop|Server` (depuis `topogenia/`) : le ZIP d'un mode, dans `artifacts/`. C'est le seul
  point d'entrée ; le dépôt ne porte aucun script du système (`.ps1`, `.bat`, `.cmd`, `.vbs`) et la chaîne n'appelle
  aucun outil de Windows que des maliciels détournent (`tar.exe`, `certutil`...) : un antivirus ou un EDR réglé au plus
  prudent n'y voit rien à signaler (ADR 076). Chaque outil est lancé directement, sans interpréteur de commandes :
  l'interface construite par `node` (`tsc -b`, puis `vite build`, comme `npm run build`), la solution restaurée puis
  `THIRD-PARTY-NOTICES.md` vérifié, le paquet du cœur contrôlé, la publication du profil sur ce paquet
  (`-p:UseCorePackage=true`, fichiers de verrouillage écrits dans `obj`), la signature quand un certificat est donné
  (`--certificate <fichier.pfx>`, `--certificate-password`, `--timestamp-url` ; `signtool` du SDK Windows), puis
  l'assemblage. Prérequis : le paquet du cœur à jour (`core/`, ci-dessous) et les paquets de l'interface installés
  (`npm ci` dans `web`).
- Ce que `package.mjs` vérifie : un paquet du cœur absent ou plus ancien que les sources du domaine et de `core/` est
  refusé ; la publication porte le paquet du cœur et non le domaine construit depuis ses sources (sur le serveur,
  assemblage identique à celui du paquet) ; aucun fichier de verrouillage n'a changé ; aucun fichier publié ne nomme le
  dossier de construction. Il assemble ensuite le ZIP (publication, `README.txt`, licences, SBOM ; le nom du produit
  écrit `{{PRODUCT}}` ou `{{product}}` dans les fichiers de `build/` qui partent dans le ZIP), le fait écrire puis
  relire par le SDK, exige qu'il rende les fichiers de son dossier octet pour octet, vérifie ses entrées (présentes et
  interdites) et écrit, à côté de lui, son SHA-256 (`.zip.sha256`) et celui de chacun de ses fichiers
  (`.files.sha256`, pour la liste blanche d'un antivirus).
- `archive.proj` : les archives par les tâches `ZipDirectory` et `Unzip` de MSBuild, code signé de Microsoft :
  `dotnet msbuild build/archive.proj -t:Zip -p:Folder=<dossier> -p:Archive=<fichier.zip>`, ou `-t:Unzip`.
- `sbom.mjs` : la SBOM CycloneDX 1.5 d'une publication, depuis son `deps.json` et les composants de l'interface.
- `components.mjs`, `third-party-notices.mjs` (`--check`) : les composants tiers tels qu'ils sont livrés et
  `THIRD-PARTY-NOTICES.md` ; `security-list.mjs` (`--check`) : la liste de `docs/securite.md` depuis la couverture.
- `codeql-local.mjs` (`node build/codeql-local.mjs`) : CodeQL quand sa CLI est déjà installée, sinon il le dit sans
  rien installer.
- `load/ServerLoad.cs` : la charge de 30 clients sur un hôte publié du mode Serveur (ADR 072), jamais livrée.

## Cœur fermé (ADR 048, ADR 074, ADR 087)

- `core/licence.mjs` : le seul point d'entrée du cœur. `node build/core/licence.mjs secret --secret <fichier>` crée,
  une fois, le secret des empreintes ; `node build/core/licence.mjs pack --key <clé> --secret <fichier> --archive
  <dossier> [--licence <numéro> --licensee "<nom>"]` construit le cœur pour une licence (sans `--licence` : `HOLDER`, le
  paquet du titulaire, écrit dans `artifacts/core-feed` pour le ZIP et le dépôt public ; avec : le paquet du licencié,
  écrit dans son entrée d'archive), vérifie son empreinte dans l'assemblage obscurci et archive le paquet, les deux
  assemblages, la carte des noms, une copie des sources et un manifeste, plus le registre `licences.json` ;
  `node build/core/licence.mjs verify <.dll ou .nupkg> --secret <fichier> --archive <dossier>` lit et authentifie
  l'empreinte d'un cœur trouvé et nomme son licencié. La clé, le secret et l'archive sont refusés dans le dépôt.
- `core/Topogenia.Core.csproj` : la chaîne que `licence.mjs` lance (elle refuse un `dotnet pack` sans licence ni
  empreinte). La clé du nom fort est celle du titulaire, créée par `sn.exe -k 2048`. Le domaine construit en Release,
  signé et marqué (attributs `Licence` et `LicenceMark`), obscurci par Obfuscar (`core/obfuscar.xml`), la documentation
  réduite à son API publique (`core/public-docs.mjs`), le paquet `Topogenia.Core` et son empreinte SHA-256, la carte des
  noms dans `artifacts/core/out/Mapping.txt` (gardée dans l'archive).
- `split.mjs` : `node build/split.mjs <destination>` écrit le dépôt public hors de la copie de travail (sans le domaine,
  ses tests, `core/` ni lui-même ; le paquet dans son `artifacts/core-feed`) et prouve qu'il compile et passe ses tests
  avec le seul paquet (`--no-proof` : écrire sans prouver). Il refuse, sans rien effacer, une destination qui n'est ni
  vide ni une copie publique écrite par lui (même solution, bascule du cœur, aucune source du cœur, en haut seulement ce
  qu'il écrit ou ce que Git ignore).
