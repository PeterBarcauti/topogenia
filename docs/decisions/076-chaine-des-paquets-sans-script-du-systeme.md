<!-- 076-chaine-des-paquets-sans-script-du-systeme.md -->
<!-- The chain of the packages written so that neither an antivirus nor an EDR set at its most careful reads it as malware: no system script, no borrowed tool of Windows, no packer mark in what ships (R4, AR-150) -->

# 076 - Chaîne des paquets sans script du système

Statut : acceptée (date 29/10/2026). Révise l'ADR 073 (§ 4 et § 5) et l'ADR 071 (§ 2 : exécutable compressé).

## Contexte

Le 29/10/2026, l'antivirus du poste de construction a mis en quarantaine `build/package.ps1` (heuristique
`Heur.BZC.PZQ.Boxter`), juste après son écriture et sans qu'il ait été lancé : le script écrivait les archives et
calculait les empreintes en mémoire. Un premier script PowerShell de la chaîne, qui créait la clé du cœur par la
CryptoAPI, avait connu le même sort (ADR 074). Le produit est destiné à une entreprise dont la DSI règle son antivirus
et son EDR au plus prudent : ce qui alerte un poste de développement alertera ses postes, et un faux positif sur un
paquet suffit à bloquer sa diffusion.

La recette transmise par le porteur du projet (29/10/2026) : n'appeler que des exécutables connus et signés ; aucun
travail de bas niveau dans un script (compression, flux en mémoire, cryptographie, `Add-Type`, Base64, `IEX`) ;
signer les scripts par un certificat de l'IT. Le but est de ne pas ressembler à un maliciel en restant entièrement
visible de la DSI, jamais de se cacher d'elle.

## Décision

1. **Un seul point d'entrée, sans PowerShell** (R4 (a), AR-150) : `node build/package.mjs <Desktop|Server>`, avec
   `--certificate <fichier.pfx>`, `--certificate-password` et `--timestamp-url` pour la signature (ADR 049).
   `build/package.ps1` est supprimé, `build/codeql-local.ps1` remplacé par `build/codeql-local.mjs`. Le dépôt ne porte
   plus aucun script du système (`.ps1`, `.psm1`, `.psd1`, `.bat`, `.cmd`, `.vbs`, `.wsf`) : la commande
   `powershell -ExecutionPolicy Bypass -File`, que les EDR signalent d'office, disparaît.
2. **Chaque outil lancé directement**, jamais par un interpréteur de commandes (`spawnSync`, `shell: false`) : `node`
   lui-même (TypeScript, Vite, les scripts de `build/`), `dotnet` (MSBuild, NuGet, les archives ci-dessous) et
   `signtool` du SDK Windows quand un certificat est donné. `npm` n'est plus appelé par la chaîne : `npm run build`
   lance `cmd.exe` pour exécuter son script ; la chaîne lance `tsc -b` puis `vite build` par `node`, et une règle garde
   le script `build` de `web/package.json` égal à ces deux commandes.
3. **Les archives écrites et relues par le SDK .NET** : `build/archive.proj`, cibles `Zip` (tâche `ZipDirectory`) et
   `Unzip` (tâche `Unzip`) de MSBuild, code signé de Microsoft, sans aucune tâche compilée à la volée (`UsingTask`).
   `tar.exe` de Windows n'est plus appelé (binaire du système que des maliciels emploient pour préparer une
   exfiltration, répertorié comme tel). Le ZIP est relu par le SDK dans un dossier vide et doit rendre les fichiers de
   son dossier, octet pour octet. Les empreintes sont calculées par `node:crypto`, jamais par `certutil`.
4. **Aucune marque de « packer » dans ce qui est livré** :
   - l'exécutable unique du mode Poste n'est plus compressé (`EnableCompressionInSingleFile=false`, `Desktop.pubxml`) :
     ses assemblages y sont rangés tels quels, rien n'est décompressé en mémoire au démarrage ; une forte entropie et une
     décompression en mémoire sont ce que les heuristiques lisent comme un exécutable empaqueté. Le ZIP, lui, reste
     compressé ;
   - l'obscurcissement du cœur renomme sans poser l'attribut contre le désassemblage (`SuppressIldasm=false`,
     `build/core/obfuscar.xml`) : cet attribut n'arrête que `ildasm` et compte comme un indice de maliciel ; les
     chaînes restent en clair (`HideStrings=false`, déjà). Le paquet du cœur est à refaire (ADR 074).
5. **Les empreintes de chaque fichier** : à côté de chaque ZIP, `<nom>.files.sha256` donne le SHA-256 de chaque
   fichier du ZIP (au format de `sha256sum`), pour la liste blanche d'un antivirus ou d'un EDR, l'exécutable d'abord.
6. **Pour la DSI** : § « Antivirus et EDR » de `docs/exploitation-poste.md` (1.4) et de `docs/exploitation-serveur.md`
   (2.1), qui disent ce que l'application fait sur la machine. Recette : le ZIP et l'exécutable lancé passent l'analyse
   de l'antivirus du poste avant la livraison, puis un poste pilote de l'entreprise. La signature, vrai remède de
   SmartScreen et des règles de réputation, reste la démarche du porteur auprès de la DSI (ADR 049).

## Options écartées

- Garder un `package.ps1` réduit aux appels d'outils (`Travail/package-ps1-reduit.txt`) : il garde le lancement de
  PowerShell avec `-ExecutionPolicy Bypass`, et l'heuristique a déjà réagi à ce nom de fichier.
- Écrire le ZIP en JavaScript (zlib) : de la compression dans un script du dépôt, ce que la recette demande d'éviter,
  alors que le SDK le fait avec du code signé.
- Demander l'exclusion du dossier `build` à l'antivirus : cela cache au lieu de montrer, et ne vaut pas pour les postes
  de la DSI.

## Conséquences

- `PublicationRulesTests` : deux règles neuves (aucun script du système, outils lancés directement et aucun outil
  détourné de Windows dans `build/*.mjs`, archives par le SDK ; exécutable non compressé, cœur sans `SuppressIldasm`,
  empreintes des fichiers écrites) ; les deux règles du script de paquet relisent `build/package.mjs`. L'extension
  `.proj` entre dans les règles des en-têtes, du format et du nom du produit.
- L'exécutable du mode Poste grossit (assemblages non compressés) ; le ZIP change peu. Taille et temps du premier
  lancement à mesurer au prochain paquet (`docs/couverture-p7/verification-poste.md`).
- Le paquet du cœur est à refaire avant le prochain ZIP (`build/package.mjs` refuse un paquet plus ancien que
  `build/core/`) ; les empreintes de la recette finale sont à réécrire.
- L'intégration continue garde des étapes `pwsh` : elles tournent sur les machines hébergées par GitHub, jamais sur un
  poste de l'entreprise.
