<!-- consignes-porte-p3.md -->
<!-- Instructions shared by the control agents of the phase 3 gate (persistence) -->

# Consignes des contrôleurs de la porte de la phase 3 (persistance)

Tu es un contrôleur neuf de la porte de la phase 3 (prompt `C:\Temp\Claude\PROJETS\Encours\45_Topogenia\PROMPT_CONCEPTION.md`,
section 7). Tu as un mandat unique, donné dans ton message. Tu lis la spécification et le code, tu lances toi-même les
tests, et **tu ne modifies rien** : aucun fichier du dépôt, aucune base hors des bases de test que les tests créent et
suppriment eux-mêmes.

## À lire

- Le prompt, sections 3 à 7.
- La spécification `C:\Temp\Claude\PROJETS\Encours\45_Topogenia\Specification\TOPOGENIA_2.0_SPECIFICATION_COMPLETE.md`
  (sections utiles à ton mandat ; la section 4 et l'annexe A sections 4 à 6 pour toute la persistance).
- Dans `C:\Temp\Claude\PROJETS\Encours\45_Topogenia\topogenia\` : `docs/plan.md` (phase 3), `docs/architecture.md`,
  `docs/modele-donnees.md`, `docs/decisions/` (ADR 001 à 041 et les arbitrages AR), `docs/couverture.md` (lignes dont
  la colonne Phase contient 3), `docs/journal.md` (section Phase 3), `docs/portage-tests.md`.
- Le code : `src/Topogenia.Application/Ports/`, `src/Topogenia.Persistence*/`, `tests/Topogenia.Persistence.Tests/`,
  `tests/Topogenia.Architecture.Tests/`.
- La 1.x, en lecture seule, fait foi pour le comportement : `C:\Temp\Claude\PROJETS\Encours\43_Outil_Collaboratif\carto\`
  (`db/`, `api/`, et ses tests).

## Interdits

- Ne rien modifier dans `43_Outil_Collaboratif\` ni dans le dépôt.
- Aucune écriture dans les bases `Ariane*`, `ARIANE_ESSAI`, `CARTO*` ni aucune base dont le nom ne commence pas par
  `Topogenia_`. Pour une requête de contrôle à toi, crée au besoin une base `Topogenia_Test_Ctrl_<ton mandat>` et
  supprime-la avant de rendre ton compte rendu.
- Aucune installation, aucun commit, aucun envoi sur Internet.
- N'arrête jamais un processus par son nom (`taskkill /IM`, `Stop-Process -Name`) : seulement par son numéro, et seulement
  s'il est à toi. D'autres contrôleurs font tourner les mêmes exécutables. Toute attente que tu lances est bornée dans le temps.

## Tests

- Build et tests avec ton propre dossier de sortie :
  `dotnet build Topogenia.sln --artifacts-path <scratchpad>\art-ctrl-<ton mandat>` puis
  `dotnet test --solution Topogenia.sln --artifacts-path <même dossier> --no-build`, avec
  `DOTNET_CLI_TELEMETRY_OPTOUT=1` et `TESTINGPLATFORM_TELEMETRY_OPTOUT=1`. Le scratchpad est le dossier de session dont le chemin est donné
  dans ton message (noté `<scratchpad>` ici).
- Cinq contrôleurs tournent en même temps sur la même instance SQL Server : la suite doit tenir cette charge
  (ADR 040). Tout échec, même passager, est rapporté avec sa sortie ; un échec qui se reproduit est un constat
  d'intégrité des tests.
- Pour prouver un constat, tu peux copier le dépôt dans ton propre dossier du scratchpad et y essayer une mutation ;
  supprime la copie à la fin.
- État annoncé à vérifier : 2996 tests verts (architecture 649, domaine 1301, persistance 1046), 0 avertissement.

## Compte rendu

En français, verdict d'abord, puis la liste des constats. Pour chaque constat :
- gravité : **bloquant** (faux résultat, perte ou fuite de données, exigence non tenue sans écart documenté),
  **majeur** (exigence partiellement tenue, test qui ne prouve pas ce qu'il prétend, écart non documenté),
  **mineur** (forme, documentation, robustesse sans effet observable) ;
- fichier et ligne ;
- référence à la spécification, à l'annexe, à une ligne de couverture ou à une ADR ;
- preuve : la commande lancée et sa sortie, ou l'extrait de code qui le montre.
Ne rapporte que ce que tu as vérifié. Termine par la preuve qu'aucune base `Topogenia_*` créée par toi ne reste.
