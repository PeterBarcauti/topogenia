<!-- consignes-porte-p4.md -->
<!-- Instructions shared by the control agents of the phase 4 gate (application, journal, HTTP host, security) -->

# Consignes des contrôleurs de la porte de la phase 4 (application, journal, hôte HTTP, sécurité)

Tu es un contrôleur neuf de la porte de la phase 4 (prompt `C:\Temp\Claude\PROJETS\Encours\45_Topogenia\PROMPT_CONCEPTION.md`,
section 7). Tu as un mandat unique, donné dans ton message. Tu lis la spécification et le code, tu lances toi-même les
tests, et **tu ne modifies rien** : aucun fichier du dépôt, aucune base hors des bases de test que les tests créent et
suppriment eux-mêmes.

## À lire

- Le prompt, sections 3 à 7. Depuis le 24/10/2026, Q3 est tranchée par le porteur du projet : `LICENSE` Apache-2.0 et
  cœur fermé (ADR 048) ; le point « Q3 non tranchée » du prompt ne s'applique plus.
- Le 25/10/2026, le porteur du projet a laissé à la construction les points qu'il ne tranchait pas, avec ce critère :
  se mettre à la place de l'utilisateur quand le point le touche, décider de façon rigoureuse quand il est seulement
  technique. Ces décisions sont AR-102 à AR-112 : ce sont des décisions de la construction, à contrôler sur le fond
  (raison, test, cohérence avec la 1.x et la spécification), pas des écarts non autorisés.
- La spécification `C:\Temp\Claude\PROJETS\Encours\45_Topogenia\Specification\TOPOGENIA_2.0_SPECIFICATION_COMPLETE.md`
  (sections utiles à ton mandat ; pour la phase 4 : sections 3, 5 et 6, annexe A sections 1 à 4 et 6, et l'annexe A
  section 2 pour les commandes).
- Dans `C:\Temp\Claude\PROJETS\Encours\45_Topogenia\topogenia\` : `docs/plan.md` (phase 4, dont P4-T09b), `docs/architecture.md`,
  `docs/modele-donnees.md`, `docs/decisions/` (ADR 042 à 050 pour la phase 4, les précédentes au besoin, et les
  arbitrages AR-01 à AR-112 de `000-arbitrages-phase-0.md`), `docs/questions.md`, `docs/couverture.md` (lignes dont la
  colonne Phase contient 4), `docs/couverture-p4/`, `docs/journal.md` (section Phase 4), `docs/portage-tests.md`.
- Le code : `src/Topogenia.Application/`, `src/Topogenia.Host/`, ce que la phase 4 a touché dans
  `src/Topogenia.Persistence*/` (restauration, `Files/`, dixième migration SQL Server), `tests/Topogenia.Application.Tests/`,
  `tests/Topogenia.Host.Tests/`, `tests/Topogenia.Architecture.Tests/`.
- La 1.x, en lecture seule, fait foi pour le comportement : `C:\Temp\Claude\PROJETS\Encours\43_Outil_Collaboratif\carto\`
  (`api/`, `db/`, et ses tests).

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
  `Topogenia:Desktop:OpenBrowser=false`.

## Tests

- Build et tests avec ton propre dossier de sortie :
  `dotnet build Topogenia.sln --artifacts-path <scratchpad>\art-ctrl-<ton mandat>` puis
  `dotnet test --solution Topogenia.sln --artifacts-path <même dossier> --no-build`, avec
  `DOTNET_CLI_TELEMETRY_OPTOUT=1` et `TESTINGPLATFORM_TELEMETRY_OPTOUT=1`. Le scratchpad est le dossier de session dont le
  chemin est donné dans ton message (noté `<scratchpad>` ici). La suite complète prend environ 12 minutes seule, bien
  plus sous la charge des cinq contrôleurs : lance-la en arrière-plan avec une attente bornée.
- Cinq contrôleurs tournent en même temps sur la même instance SQL Server : la suite doit tenir cette charge
  (ADR 040). Tout échec, même passager, est rapporté avec sa sortie ; un échec qui se reproduit est un constat
  d'intégrité des tests.
- Pour prouver un constat, tu peux copier le dépôt dans ton propre dossier du scratchpad et y essayer une mutation ;
  supprime la copie à la fin.
- État annoncé à vérifier : voir `docs/etat.md` (chiffres de la dernière suite complète), 0 avertissement.

## Round 3 et suivants : mandat resserré (règle d'économie du 25/10/2026, `docs/a-peaufiner.md`)

À partir du round 3, ton mandat est resserré, sans rien retirer à ta liberté de rapporter ce que tu vois :
1. Vérifier que chaque bloquant et majeur du round précédent de ton mandat (`Travail\porte-p4-r<n-1>-<mandat>.md`) est
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
Ne rapporte que ce que tu as vérifié. Termine par la preuve qu'aucune base `Topogenia_*` créée par toi ne reste.
