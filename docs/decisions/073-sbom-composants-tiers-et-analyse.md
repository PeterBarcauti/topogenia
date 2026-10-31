<!-- 073-sbom-composants-tiers-et-analyse.md -->
<!-- The SBOM of each ZIP, the notices of the components computed from what the built interface serves, the hashes, the entry point of the signature and the local analysis by CodeQL (P7-T03, AR-146) -->

# 073 - SBOM, composants tiers et analyse

Statut : acceptée (date 29/10/2026)

## Contexte

La spec 5.4 veut une SBOM CycloneDX livrée avec chaque version et les empreintes SHA-256 des binaires ; l'ADR 049 garde
le point d'entrée de la signature et une analyse CodeQL locale (`build/codeql-local.mjs`, ADR 076), l'étape de l'intégration
continue étant désactivée. La porte de la phase 6 a montré que `THIRD-PARTY-NOTICES.md` devinait ce qui est servi depuis
le fichier de verrouillage (F-31, F-41) : ni la partie Node d'exceljs, jamais servie, ni les paquets embarqués dans son
paquet navigateur et dans le `jszip.min.js` qu'il contient n'y étaient justes. Aucun outil ne s'installe sur le poste
(prompt 4.4) et rien n'est lu sur Internet.

## Décision

1. **Ce qui est servi, lu dans la construction** (AR-146) : un greffon de `web/vite.config.ts` écrit, à chaque
   `npm run build`, `web/served-packages.json` : les paquets dont un module entre dans les morceaux servis, par leur chemin
   dans le verrouillage, et les fichiers déjà assemblés qu'ils servent tels quels (`*.min.js`). `build/components.mjs`
   en tire les composants : NuGet (graphes de restauration), npm (`livré` s'il est servi, `non servi` s'il est de
   production sans l'être, `développement`), et les paquets embarqués : ceux de la carte des sources d'un fichier assemblé
   (`exceljs.min.js`), et les dépendances du verrouillage d'un fichier assemblé sans carte qu'il contient (`jszip.min.js` :
   `pako`, `lie`, `immediate`, `setimmediate`) ; leur licence vient du verrouillage, du `package.json` embarqué, sinon
   d'une table relevée à la construction (à confirmer en ligne à la recette) ; une part sous une autre licence permissive
   est nommée (`hash.js` : Apache-2.0, `uuid` : BSD-3-Clause).
2. **`THIRD-PARTY-NOTICES.md`** écrit par `build/third-party-notices.mjs` ; `--check` refuse un fichier qui n'est pas celui
   des composants (un usage ou une licence changés à la main, une ligne restée) : `ThirdPartyNoticesTests` l'appelle, et le
   script de paquet ne livre rien sans lui.
3. **SBOM** CycloneDX 1.5 en JSON par `build/sbom.mjs`, sans outil installé : les paquets et les packs du runtime que la
   publication porte (son `deps.json`), les frameworks fournis par le Hosting Bundle pour le serveur (nommés, non portés),
   les paquets npm servis, et sous chacun les paquets qu'il embarque. Une SBOM par ZIP, à côté de lui et dedans
   (`sbom.cdx.json`).
4. **Paquet** (`build/package.mjs`, seul point d'entrée depuis l'ADR 076 ; `build/package.ps1` jusque-là) :
   construit l'interface, restaure la solution (le graphe de la construction : une publication laisse le sien, avec le
   paquet du cœur et, pour le Poste, un outil du SDK jamais livré), vérifie les composants tiers, publie sur le paquet
   du cœur (ADR 074 § 8), vérifie le contenu du ZIP qu'il écrit, signe l'exécutable
   quand un certificat est donné (`--certificate`, `signtool` du SDK Windows ; sans certificat, rien n'est signé, ADR 049),
   écrit la SBOM, le ZIP et son empreinte SHA-256. Les commandes externes sont jugées sur leur code de sortie seul (un
   avertissement de Vite sur sa sortie d'erreur ne l'arrête pas) ; elles sont lancées sans interpréteur de commandes et
   le ZIP est écrit par le SDK (ADR 076).
5. **CodeQL** : `build/codeql-local.mjs` analyse le C# et le TypeScript avec les suites « security-and-quality » du paquet
   de la CLI quand elle est déjà là (rapports SARIF dans `TestResults/codeql/`) ; sans elle, il le dit, dit comment
   l'obtenir et finit avec le code 3, sans rien installer. Elle n'est pas sur ce poste : l'analyse n'a pas tourné.

## Conséquences

- `THIRD-PARTY-NOTICES.md` : 147 composants livrés, dont 72 embarqués (68 de la carte d'exceljs, 4 par jszip) ; les
  copies du verrouillage de l'arbre d'exceljs sont « non servi » ; 38 licences relevées, toutes permissives, à confirmer en
  ligne (recette).
- Le paquet du mode Poste vérifié : SBOM de 35 composants .NET (dont les deux packs du runtime) et 40 paquets npm portant
  72 paquets embarqués ; ZIP et empreinte.
- Le point d'entrée de la signature n'est pas essayé (aucun certificat) ; l'analyse CodeQL non plus (CLI absente) :
  écarts dits au rapport final.

## Révision (29/10/2026, porte P7 round 1, R4) : chaîne sans script du système (AR-150)

`build/package.ps1` est supprimé : `build/package.mjs <Desktop|Server>` est le seul point d'entrée et lance chaque outil
sans interpréteur de commandes ; le ZIP est écrit et relu par les tâches du SDK (`build/archive.proj`), plus par
`tar.exe` ; l'empreinte de chaque fichier du ZIP est écrite à côté de lui. `build/codeql-local.ps1` devient
`build/codeql-local.mjs`, au même comportement (code 3 sans la CLI). Détail dans l'ADR 076.
