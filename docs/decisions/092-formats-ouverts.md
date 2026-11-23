<!-- 092-formats-ouverts.md -->
<!-- The open formats of 3.1: the map exported as Markdown with its Mermaid diagram and as an image SVG carrying its copy on option, a table (.xlsx, .csv) and a Mermaid flowchart (.mmd, .md) opened as a process -->

# 092 - Formats ouverts : exports Markdown Mermaid et SVG, imports Excel et Mermaid

Statut : acceptée (date 06/11/2026). Prolonge l'ADR 054 (fichiers `.pmap`), l'ADR 066 (exports de la carte) et
l'ADR 091 (tracé imposé, `.pmap` 1.3). Demande du porteur du projet du 05/11/2026 : reconnaître les formats ouverts et
exporter vers des formats communs. Les outils de référence sont Excel, Visio et Mermaid. Tout entre dans la 3.1, parce
qu'une mise à jour du Microsoft Store est lourde.

## Contexte

La 3.0 échange une carte par le `.pmap` (JSON propre à Topogenia) et le paquet `.topogenia`, et l'exporte en PDF, en
PNG et en classeur. Une carte ne se relit donc ni dans un document Markdown, ni dans une image modifiable. Un processus
écrit dans un tableur ou un diagramme Mermaid ne s'ouvre pas.

Le lecteur de référence du Markdown est VS Code, qui embarque un Mermaid récent. SharePoint n'est pas une cible : « si
VS Code lit correctement, on valide » (porteur du projet, 05/11/2026).

Retour d'expérience du porteur du projet sur les diagrammes : les défauts habituels sont les graphiques qui se recouvrent
(une légende sur son graphique) et les titres d'étapes coupés parce que trop longs pour leur cadre.

## Décision

1. **Export Markdown** (`web/src/features/map/export/markdown.ts`, `mermaid.ts`) : le titre, le chemin, la
   classification et la date, le diagramme dans un bloc `mermaid`, puis les tableaux du classeur.
   - **Syntaxe du diagramme** : elle est écrite par le code, jamais par un modèle.
     - Un `flowchart LR`, un sous-graphe par couloir.
     - Une forme par type : tâche en boîte, losange avec `+` ou `O` pour les branchements parallèle et inclusif, début en
       stade, fin en double cercle, attente en drapeau, étape détaillée en boîte à double bord.
   - **Textes** : le code est en première ligne, puis le libellé en lignes de 24 caractères au plus, jamais coupé (un mot
     trop long est coupé avec un trait d'union, rien n'est perdu). Les signes que Mermaid lit sont écrits en codes
     d'entité.
   - **En-tête** : une police fixe pour la mesure et l'affichage, et une marge sous les titres de couloir pour leurs lignes.
2. **Image SVG** (`export/svg.ts`, `svgSteps.ts`, `svgDraw.ts`, `svgIcons.ts`).
   - **Rendu** : la carte est redessinée à partir de `layoutMap`, comme le canevas la montre en état d'export, et non
     capturée.
     - Ce qui est dessiné : couloirs et leur marge, étapes par type avec la bande d'encre peinte comme une bordure CSS,
       codes, libellés coupés à trois lignes, pictogrammes, badges, RACI, flux de valeur, flèches et pointes en chemins,
       étiquettes, ligne de classification.
     - Ce qui est exclu : `foreignObject`, script, variable CSS, marqueur et référence.
     - Inter est intégrée en `@font-face`. Le cadre est celui du PNG.
   - **Case « Inclure une copie modifiable »** (pratique de draw.io) :
     - Elle est décochée par défaut. Elle est indisponible pour un projet « Strictement confidentiel », et l'export la
       refuse aussi dans ce cas.
     - La copie est le `.pmap` de la carte et de ses cartes détaillées, avec les tracés et les fiches telles que la
       personne les voit, en base64 d'UTF-8 dans `<metadata id="topogenia">`.
     - Elle est lue par la route nouvelle `GET /api/maps/{id}/pmap` (lecteur ; port `IPmapFormat.OutlineBranch`) et
       journalisée comme un export (`project.exported`, portée carte).
3. **Ouverture d'un tableau** `.xlsx` ou `.csv` (`project/files/tableRead.ts`, `tableImport.ts`).
   - **Lecture du fichier** : le CSV suit la RFC 4180, avec un séparateur deviné, en UTF-8 puis Windows-1252. `exceljs`
     n'est chargé que s'il faut lire un classeur.
   - **Colonnes reconnues** : en français, en anglais, celles de Visio Data Visualizer, et le classeur que Topogenia
     exporte, relu.
   - **Contenu repris** : les types, les flèches et leurs libellés, le RACI par lettre ou par colonne RACI, et les délais
     en h, min ou j.
   - Un classeur à macros ou au format Excel ancien est refusé clairement.
4. **Ouverture d'un diagramme Mermaid** `.mmd`, ou du premier bloc `mermaid` de logigramme d'un `.md`
   (`project/files/mermaidImport.ts`).
   - **Analyseur** : il est écrit dans le projet, borné à 20 000 lignes, 2 000 caractères par ligne, 5 000 étapes et
     20 000 liens. Il lit le sous-ensemble `flowchart` et `graph`.
   - **Formes** : les formes classiques et les formes nommées de Mermaid 11 (`A@{ shape: diam }`). `((…))` et `([…])`
     donnent un début sans flèche entrante, une fin sans flèche sortante, et sinon une tâche.
   - **Liens** : `-->`, `---`, `-.->`, `==>`, `<-->`, `~~~`, avec leurs libellés `|…|` ou `-- … -->`, les chaînes et
     `&`. Les sous-graphes deviennent des couloirs (le plus intérieur).
   - **Mis de côté, et dit avec la ligne** (comptée depuis le haut du `.md`) : les styles, `:::`, les liens cachés, à
     double sens ou vers un sous-graphe entier.
   - **Relecture de l'export** : ce que l'export écrit se relit tel quel (ligne du code ôtée, signe du losange lu, mot
     coupé en fin de ligne recollé).
5. **Un seul chemin d'ouverture.** Un tableau ou un diagramme est converti dans le navigateur en texte `.pmap` 1.2, avec
   ce qui a été deviné ou mis de côté dans ses `uncertainties`. Ce texte suit la route d'ouverture de l'ADR 054
   (administrateur), et l'hôte le lit avec ses propres règles. Les textes sont d'abord rendus acceptables par l'hôte
   (`plainText`, comme `TextRules` : sans balise HTML ni caractère de contrôle). L'entrée du menu devient « Ouvrir un
   fichier… ».

## Écarté

- **Visio `.vsdx`, BPMN et GraphML** : écartés par le porteur du projet le 05/11/2026 (« je ne sais même pas si je le
  traiterai »). L'export BPMN est repris en 3.3 à sa demande (ADR 106) ; Visio et GraphML restent écartés.
- **Le paquet `mermaid` dans le code livré** : il est lourd, et la syntaxe des exports est écrite par le code. Il reste
  une dépendance de test (point F-90).
- **Une capture `html-to-image` pour le SVG** : son `foreignObject` ne se lit ni dans Word ni dans PowerPoint.
- **Une conversion côté hôte** : elle aurait fait une seconde route d'ouverture, avec ses propres règles à tenir.
- **Un refus de la copie d'une carte par l'hôte pour un projet « Strictement confidentiel »** : la copie du projet
  entier reste permise à tout lecteur, ce refus n'ajouterait rien (point F-94).

## Conséquences

- Le dialogue d'ouverture accepte `.pmap`, `.xlsx`, `.csv`, `.mmd`, `.md` et `.svg`. Les points à vérifier s'affichent
  comme pour un `.pmap`.
- **Le SVG dans Word et PowerPoint** : ils remplacent Inter par Segoe UI ou Arial, si bien qu'une ligne mesurée au plus
  juste peut déborder d'un pixel (point F-92, à l'essai du porteur du projet).
- **Noms français de Visio** : les colonnes françaises de Visio Data Visualizer sont supposées et restent à confirmer sur
  un vrai modèle (point F-91).

## Vérification

- **Export Markdown et Mermaid** : `mermaid.test.ts`, `markdown.test.ts` et `mermaidRender.test.ts`. Les diagrammes y
  sont lus par `mermaid.parse` et dessinés dans Chromium. On y mesure les textes coupés et les recouvrements, avec des
  témoins négatifs.
- **Imports** :
  - `tableImport.test.ts` et `mermaidImport.test.ts` : chaque `.pmap` écrit est validé par le schéma publié.
  - `mermaidImport.test.ts` ajoute la relecture de l'export, 1 500 textes au hasard et des textes piégés lus en temps
    borné.
- **Image SVG** :
  - `svg.test.ts` contrôle la structure, des nombres finis et l'absence de `foreignObject` et de script ; un fichier de
    référence l'accompagne.
  - `svgCopy.test.ts` vérifie que la copie se relit entière et qu'un script reste sans effet.
- **Menu Fichier** : `fileMenu.test.tsx` couvre le dialogue, la case et le « Strictement confidentiel ».
- **Hôte** : `PmapExchangeTests` et `PmapRoutesTests` (deux bases) couvrent la copie d'une carte, les droits et le
  journal.
- **Parcours** `e2e/svg-export.spec.ts` sur l'hôte réel : même cadre que le PNG, huit codes dessinés, réouverture avec
  les fiches.
