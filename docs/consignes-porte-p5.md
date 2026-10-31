<!-- consignes-porte-p5.md -->
<!-- Instructions shared by the control agents of the phase 5 gate (exchange: package, .pmap 1.1, round trips, command line) -->

# Consignes des contrôleurs de la porte de la phase 5 (échange : paquet, `.pmap` 1.1, allers-retours, ligne de commande)

Tu es un contrôleur neuf de la porte de la phase 5 (prompt `C:\Temp\Claude\PROJETS\Encours\45_Topogenia\PROMPT_CONCEPTION.md`,
section 7). Tu as un mandat unique, donné dans ton message. Tu lis la spécification et le code, tu lances toi-même les
tests, et **tu ne modifies rien** : aucun fichier du dépôt, aucune base hors des bases de test que les tests créent et
suppriment eux-mêmes.

## À lire

- Le prompt, sections 3 à 7. Q3 est tranchée (ADR 048 : `LICENSE` Apache-2.0 et cœur fermé) ; le point « Q3 non
  tranchée » du prompt ne s'applique plus.
- Le porteur du projet a laissé à la construction les points qu'il ne tranchait pas, avec ce critère : se mettre à la
  place de l'utilisateur quand le point le touche, décider de façon rigoureuse quand il est seulement technique. Ces
  décisions (AR-102 à AR-119, ADR 051 à 055) sont des décisions de la construction, à contrôler sur le fond (raison,
  test, cohérence avec la 1.x et la spécification), pas des écarts non autorisés. AR-93, AR-94 et AR-95 sont des
  décisions du porteur : pas de migration 1.x dans l'application, `.pmap` 1.1 seul avec un bloc de fiche, IA intégrée en
  3.0 seulement.
- La spécification `C:\Temp\Claude\PROJETS\Encours\45_Topogenia\Specification\TOPOGENIA_2.0_SPECIFICATION_COMPLETE.md` :
  pour la phase 5, sections 5.4 (garde-fous des paquets), 5.5 (audit), 6.3 (ligne de commande), 7 (échange : 7.1 à
  7.5), 11 (critères) et l'annexe A sections 1.2 (routes des projets), 2.17 (format `.pmap`), 3 (codes), 4 et 6.
- Dans `C:\Temp\Claude\PROJETS\Encours\45_Topogenia\topogenia\` : `docs/plan.md` (phase 5 : P5-T01 à P5-T08, P5-T05 et
  P5-T06 retirées), `docs/format-topogenia.md`, `docs/format-pmap.md`, `docs/pmap/` (consignes pour l'IA),
  `schemas/`, `samples/`, `docs/decisions/` (ADR 051 à 055 et les arbitrages AR-01 à AR-116 de
  `000-arbitrages-phase-0.md`), `docs/questions.md`, `docs/couverture.md` (lignes dont la colonne Phase contient 5),
  `docs/journal.md` (section Phase 5), `docs/portage-tests.md` (section « Échange, `.pmap` 1.1 »), `docs/a-peaufiner.md`
  (registre des mineurs ; F-11 est à examiner par le mandat Sécurité).
- Le code : `src/Topogenia.Interchange/` (`Packages/`, `Pmap/`, `Schemas/`, `Files/`), `src/Topogenia.Application/Exchange/`,
  `src/Topogenia.Host/Api/Exchange/`, `src/Topogenia.Host/Startup/ExchangeCommands.cs`, ce que la phase 5 a touché dans le
  domaine (`Projects/ImportedProjects.cs`, `Projects/ImportedRecords.cs`, `ProjectReplacement.cs`, `ProjectCodeRules.cs`, `Errors/FileError.cs`) et dans
  la persistance (`Writing/WritePlan.cs`), et leurs tests : `tests/Topogenia.Interchange.Tests/`,
  `tests/Topogenia.Application.Tests/Exchange/`, `tests/Topogenia.Host.Tests/Exchange/`, `tests/Topogenia.Host.Tests/Ported/PmapFilesPortedTests.cs`,
  `tests/Topogenia.Host.Tests/Startup/ExchangeCommandsTests.cs`, `tests/Topogenia.Persistence.Tests/Writing/NewProjectWritingTests.cs`.
- La 1.x, en lecture seule, fait foi pour le comportement du `.pmap` : `C:\Temp\Claude\PROJETS\Encours\43_Outil_Collaboratif\carto\`
  (`api/Domaine/FormatPmap.cs`, `api/Services/ServiceFichiers.cs`, `format/`, et ses tests `FormatPmapTests`, `FichiersTests`).

## Interdits

- Ne rien modifier dans `43_Outil_Collaboratif\` ni dans le dépôt.
- Aucune écriture dans les bases `Ariane*`, `ARIANE_ESSAI`, `CARTO*` ni aucune base dont le nom ne commence pas par
  `Topogenia_`. Pour une requête de contrôle à toi, crée au besoin une base `Topogenia_Test_Ctrl_<ton mandat>` et
  supprime-la avant de rendre ton compte rendu.
- Aucune installation, aucun commit, aucun envoi sur Internet.
- N'arrête jamais un processus par son nom (`taskkill /IM`, `Stop-Process -Name`) : seulement par son numéro, et seulement
  s'il est à toi. D'autres contrôleurs font tourner les mêmes exécutables. Toute attente que tu lances est bornée dans le temps.
- Ne lance pas l'exécutable en mode Poste avec ses réglages par défaut : il ouvrirait le fichier de données réel de
  l'utilisateur sous `%LOCALAPPDATA%`. Pour un essai, donne toujours `Topogenia:Desktop:Folder` vers ton scratchpad et
  `Topogenia:Desktop:OpenBrowser=false`. Sous PowerShell 5.1, `ProcessStartInfo.ArgumentList` n'existe pas : passe les
  arguments en une chaîne.

## Tests

- Build et tests avec ton propre dossier de sortie :
  `dotnet build Topogenia.sln --artifacts-path <scratchpad>\art-ctrl-<ton mandat>` puis
  `dotnet test --solution Topogenia.sln --artifacts-path <même dossier> --no-build`, avec
  `DOTNET_CLI_TELEMETRY_OPTOUT=1` et `TESTINGPLATFORM_TELEMETRY_OPTOUT=1`. Le scratchpad est le dossier de session dont le
  chemin est donné dans ton message (noté `<scratchpad>` ici). La suite complète prend environ 15 minutes seule, bien
  plus sous la charge des cinq contrôleurs : lance-la en arrière-plan avec une attente bornée. Les tests de l'échange et
  de l'hôte lisent `samples/`, `schemas/` et `docs/` au chemin du dépôt enregistré à la construction : ton dossier de
  sortie peut être ailleurs.
- Cinq contrôleurs tournent en même temps sur la même instance SQL Server : la suite doit tenir cette charge
  (ADR 040). Tout échec, même passager, est rapporté avec sa sortie ; un échec qui se reproduit est un constat
  d'intégrité des tests.
- Pour prouver un constat, tu peux copier le dépôt dans ton propre dossier du scratchpad et y essayer une mutation ;
  supprime la copie à la fin. Une mutation se juge sur les tests qu'elle fait échouer pour sa raison, pas sur un échec
  d'environnement (un fichier introuvable, une base absente).
- État annoncé à vérifier : voir `docs/etat.md` (chiffres de la dernière suite complète), 0 avertissement.

## Round 3 et suivants : mandat resserré (règle d'économie du 25/10/2026, `docs/a-peaufiner.md`)

À partir du round 3, ton mandat est resserré, sans rien retirer à ta liberté de rapporter ce que tu vois :
1. Vérifier que chaque bloquant et majeur du round précédent de ton mandat (`Travail\porte-p5-r<n-1>-<mandat>.md`) est
   vraiment corrigé : lire la correction, lancer le test qui la prouve, rejouer la sonde ou la mutation du contrôleur
   quand elle est décrite.
2. Relire seulement les fichiers changés depuis le round précédent (liste donnée dans ton message) et ce qu'ils
   touchent, avec ton mandat.
3. Tests : ceux des fichiers changés et ceux des corrections, pas de suite complète, sauf le contrôleur Intégrité des
   tests, qui rejoue la suite complète et compare son chiffre à celui du chef de projet (`docs/etat.md`).
4. Mutations : seulement sur la logique métier et la sécurité.
Un mineur se rapporte, il ne bloque pas la porte (il va au registre `docs/a-peaufiner.md`).

## Compte rendu

En français, verdict d'abord, puis la liste des constats. Pour chaque constat :
- gravité : **bloquant** (faux résultat, perte ou fuite de données, exigence non tenue sans écart documenté),
  **majeur** (exigence partiellement tenue, test qui ne prouve pas ce qu'il prétend, écart non documenté),
  **mineur** (forme, documentation, robustesse sans effet observable) ;
- fichier et ligne ;
- référence à la spécification, à l'annexe, à une ligne de couverture ou à une ADR ;
- preuve : la commande lancée et sa sortie, ou l'extrait de code qui le montre.
Ne rapporte que ce que tu as vérifié. Écris ton compte rendu dans `C:\Temp\Claude\PROJETS\Encours\45_Topogenia\Travail\porte-p5-r<n>-<mandat>.md`
(le seul fichier que tu écris hors de ton scratchpad) et rends-le aussi dans ta réponse. Termine par la preuve qu'aucune
base `Topogenia_*` créée par toi ne reste.
