<!-- 109-gestes-de-la-carte.md -->
<!-- Gestures of the map (3.4, lot 1): Show all (Shift+1) on the map and the BPMN view, copy the map or a selection as an image, the keyboard shortcuts sheet and the Help menu, Present from this step in the step menu -->

# 109 - Gestes de la carte

Statut : acceptée le 21/11/2026 (3.4, lot 1). Prolonge les ADR 057 (socle de l'interface, commandes en données), 061
(édition de la carte), 066 (exports de la carte), 097 (mode présentateur) et 106 (vue BPMN). Arbitrages du chef
d'orchestre du 21/11/2026 (cadrage du lot 1, section 11).

## Contexte

Quatre gestes courants des outils de dessin manquent à la carte : voir tout le processus d'un coup, mettre la carte
dans un courriel ou une diapositive sans passer par un fichier, retrouver les touches sans ouvrir le manuel, lancer la
présentation depuis l'étape sous le doigt. Visio (« Ajuster à la fenêtre », Ctrl+Maj+W), draw.io (« Ajuster à la
fenêtre », « Copier comme image », menu Aide > Raccourcis clavier), Miro et Figma (Maj+1, « Copier en PNG »), Excel
(« Copier comme image »), Gmail, GitHub et Jira (la touche « ? ») les offrent. La commande `present.from` (Maj+F5)
existe déjà, il manque seulement sa ligne au menu d'une étape.

## Décision

1. **« Tout voir »** : une commande `fit` (`state/commands/viewCommands.ts`, libellé `map.fit`, icône `Maximize`),
   active pour tous les rôles, ajoutée à `MAP_COMMANDS`, `KEY_IDS` et `BPMN_KEY_COMMANDS` de `mapCommands.ts`. Elle
   appelle `editor.canvas.fit()` : `CanvasHandle` (`state/editor.ts`) gagne `fit` et `showFit`, sur le modèle de
   `reveal` et `showReveal` ; la toile montrée s'inscrit (la carte et la vue BPMN ne le sont jamais ensemble,
   `MapScreen.tsx`).
   - **Carte** : fonction pure `fitView(map, screen)` dans `model/geometry/viewport.ts` ; le plus grand zoom qui tient
     la carte entre la marge des en-têtes (`INITIAL_VIEW.x`), `RIGHT_MARGIN`, `TOP_MARGIN` et `BOTTOM_MARGIN`, borné
     entre `MIN_ZOOM` (0,2) et **1**, origine `{ x: INITIAL_VIEW.x, y: TOP_MARGIN }`, puis `boundView` comme toute vue ;
     `useBoundedViewport` expose `fit()` à côté de `backToStart()`. Le `fitView` de React Flow n'est pas repris : la vue
     de la carte est bornée. Une petite carte reste à 100 % (les textes des étapes sont faits pour 100 %) ; une carte
     trop grande même à 20 % s'affiche à 20 % depuis son début. La vue d'ouverture ne change pas (B-7-09).
   - **Vue BPMN** : `BpmnView.tsx` calcule la vue par `getViewportForBounds` (React Flow) sur la taille du dessin, zoom borné entre `MIN_ZOOM` et 1, marge `FIT_PADDING`, sans animation (`fitView` de React Flow demande des nœuds mesurés par le navigateur) ; le bouton de `BpmnToolbar.tsx`
     et la touche font le même geste (arbitrage du 21/11/2026 : zoom limité à 100 % dans les deux vues).
   - **Bouton** : un `ControlButton` de `Canvas.tsx`, entre le zoom et « Revenir au début », son titre et son
     `aria-label` « Tout voir (Maj+1) » écrits par `useShortcutLabel`.
   - **Touche** : Maj+1. Ctrl+Maj+W (Visio) ferme la fenêtre du navigateur : refusé. L'accord étant lu sur `event.key`
     (`shared/keyboard/keyboard.ts`), la commande déclare `shortcuts: ['Shift+1', 'Shift+!']` : `Shift+1` sur un clavier
     français, `Shift+!` sur un clavier anglais ou autre (même ruse que les chiffres de la présentation,
     `usePresenterStage.ts`) ; aucune collision, Maj+! donnant « § » sur AZERTY. Sans étape seule choisie : avec une étape
     seule choisie, un caractère tapé commence son nom (`useCanvasKeys.ts`), comme V et H (AR-151). Claviers non couverts
     (hongrois, suisse) : le bouton reste.

2. **« Copier en image »** : la capture de l'export PNG, sans format nouveau.
   - **Une seule capture** : `exports.ts` exporte `mapImage(editor, map, scope)`, qui rend la promesse du PNG ;
     `exportMap` la reprend. `state/actions/copyImage.ts` (nouveau) porte `copyMapImage(editor, scope)` : refuse pendant
     un autre export, lance la capture, appelle `copyImage(promesse)` de `shared/ui/clipboard.ts` (nouveau) dans le même
     tour que le clic, notifie « Copie de l'image… » puis « Image copiée… », ou l'erreur avec l'action « Exporter en
     PNG ». Même cadre, mêmes polices, même ligne de classification que l'export.
   - **Le geste du navigateur** : `navigator.clipboard.write` est appelé dans le geste, avec un `ClipboardItem` dont la
     valeur est la promesse de l'image (Firefox, depuis sa version 127, exige que l'écriture parte du geste, et la
     capture peut durer, `capture.ts` attendant les étapes jusqu'à 3 s) ; un repli écrit l'image toute faite quand le
     navigateur refuse une promesse ; `false` sans `ClipboardItem` ou si l'écriture est refusée. Le contexte sécurisé
     est déjà acquis (127.0.0.1 en mode Poste, HTTPS en mode Serveur, ADR 045).
   - **Taille** : `CaptureOptions` gagne `maximum` : 8 000 px de côté pour la copie (Chromium écrit un bitmap dans le
     presse-papiers de Windows : 12 000 x 12 000 pèse 576 Mo), 12 000 par défaut ; l'export PNG est inchangé.
   - **Une sélection** : `ExportRun` (`state/mapStore.ts`) gagne `steps`. Pendant la capture, les étapes non choisies,
     leurs flèches (gardée si ses deux bouts sont choisis), libellés et cartes du flux de valeur prennent la classe
     `is-left-out` ; le filtre de la capture (`keptInCapture`, `capture.ts`) ne les copie pas (retouche R8 : la règle
     `.map-canvas.is-exporting .is-left-out { visibility: hidden; }` ne suffit pas, React Flow écrivant
     `visibility: visible` dans le style de chaque nœud mesuré). Le cadre est la boîte des étapes choisies, de ce qui
     pend sous elles et des flèches entre elles (`selectionBounds`, `capture.ts`), au plus juste (R8 : il n'est plus
     étendu aux couloirs touchés, qui faisaient entrer des étapes non choisies) ; `ExportCaption.tsx` place la
     classification sous ce cadre, l'image s'élargissant à sa ligne si la sélection est plus étroite. **Sans les noms des couloirs** (arbitrage du
     21/11/2026) : ils laisseraient une bande vide dès que la sélection n'est pas au début de la carte.
   - **Entrées** : `file.copy-image` (menu Fichier, après `file.png`, `fileCommands.ts`), `pane.copy-image` (menu du
     fond) et `selection.copy-image` (menu d'une sélection, après `copy`), `menuCommands.ts` et `mapCommands.ts`. Aucun
     raccourci : Ctrl+Maj+C ouvre l'inspecteur dans Chrome et Edge. Grisées sans toile de carte montrée (vue BPMN).

3. **L'aide-mémoire des raccourcis** : tiré de la liste des commandes, jamais écrit à côté.
   - **Données** : `state/commands/shortcutSheet.ts` (nouveau) déclare des sections (Se déplacer, Choisir, Modifier la
     carte, Afficher, Présenter, Aide) de lignes `{ ids, label?, keys? }`, plus des lignes `other` pour les touches qui
     ne sont pas des commandes (Espace maintenu, Entrée sur une étape, un caractère tapé, Maj+F10, F1, Ctrl+S, molette).
   - **Aucun écart** : un essai vérifie que chaque commande de `KEY_COMMANDS` est sur une ligne et une seule, qu'aucune
     ligne ne nomme une commande que le clavier n'exécute pas, que chaque accord montré est un raccourci de sa
     commande ; chaque touche `other` a un essai qui la presse et voit son effet.
   - **Panneau** : `app/ShortcutsDialog.tsx` (nouveau), sur `shared/ui/Dialog.tsx` (Échap, focus gardé puis rendu),
     tableau par section, touches en `<kbd>`, noms des touches dans la langue (`useShortcutLabel`, qui gagne
     `ArrowLeft`, `ArrowRight`, `ArrowDown`, `Backspace`) ; la section « Modifier la carte » dit qu'elle est réservée à
     l'animateur ; fenêtre modale, comme Gmail, GitHub et Miro, rien n'est posé sur le schéma.
   - **Ouverture** : petit magasin `shared/state/shortcutsPanel.ts` (modèle `bpmnState.ts`) ; commande `help.keys`
     (`viewCommands.ts`), `shortcuts: ['Shift+?', '?']` (« ? » est Maj+, en AZERTY et Maj+/ en QWERTY), dans `KEY_IDS`
     et `BPMN_KEY_COMMANDS`, même règle que V et H : sans étape seule choisie.
   - **Menu Aide** : le lien « Aide » du bandeau (`app/Header.tsx`) devient un bouton qui ouvre un menu
     (`app/HelpMenu.tsx`, modèle `AccountMenu.tsx`) : « Aide en ligne » (F1, `openHelp`, nouvel onglet, la langue de
     l'aide comme aujourd'hui) et « Raccourcis clavier » (?). F1 ne change pas. Arbitré le 21/11/2026 (draw.io et Miro
     ont ce menu) ; le coût est un clic de plus pour l'aide en ligne.

4. **« Présenter depuis cette étape »** : `line('present.from', 1)` dans `STEP_MENU` (`mapCommands.ts`), groupe 1, juste
   après les lignes qui lisent (`step.open`, `step.sheet`, `step.raci`), avant `rename` : c'est le groupe que le lecteur
   voit en haut de son menu court. La commande, sa touche Maj+F5 et ses règles (ADR 097) existent ; rien d'autre.
   **F-28** (sous-menu « Changer le type ») reste dans `docs/a-peaufiner.md` (non publié) : `shared/ui/Menu.tsx` n'a pas de sous-menu
   (clavier, ARIA, placement). F-123 passe à « fait ».

5. **Mode Serveur** : aucun geste n'écrit sur l'hôte. Tout rôle (animateur, contributeur, lecteur) a les quatre : la
   copie lit le projet pour sa classification (`GET /api/projects/{id}`) comme l'export PNG que tout lecteur a ; la
   présentation garde ses règles (ADR 097, 099) ; l'aide-mémoire marque la section « Modifier la carte » réservée à
   l'animateur. Aucune API, aucune règle des droits nouvelle.

## Écarté

- **Ctrl+Maj+W** (Visio) pour « Tout voir » : ferme la fenêtre du navigateur.
- **Le `fitView` de React Flow sur la carte** : sa vue est bornée (premier couloir jamais sous la marge du haut, jamais à
  gauche de la colonne 0) ; il ignore ces bornes.
- **Un zoom jusqu'à 200 % pour une petite carte** : les textes sont faits pour 100 %, et trois étapes à 200 % ne montrent
  rien de plus.
- **Un raccourci pour « Copier en image »** : Ctrl+Maj+C (Figma) ouvre l'inspecteur dans Chrome et Edge.
- **L'image d'une sélection avec les noms des couloirs** : une bande vide dès que la sélection est loin du début.
- **Une bibliothèque d'images** pour réduire ou encoder : le canevas du navigateur suffit (`pixelRatio` borné).
- **Le sous-menu « Changer le type »** (F-28) : hors de ce lot, faute de sous-menu dans `Menu.tsx`.
- **Un changement de l'hôte** : `Permissions-Policy` ne coupe pas le presse-papiers et `img-src 'self' data: blob:`
  laisse passer la capture (`SecurityHeaders.cs`) ; la règle `connect-src 'self'` n'est pas touchée.

## Conséquences

- Aucune migration, aucune route, aucune dépendance, aucun appel sortant, aucun changement du cœur fermé.
- Le raccourci de « Tout voir » vaut pour la carte et la vue BPMN ; le zoom de la vue BPMN s'aligne sur la carte
  (100 % au plus, inchangé jusque-là à 200 %).
- Cinq commandes nouvelles (`fit`, `help.keys`, `file.copy-image`, `pane.copy-image`, `selection.copy-image`) et un
  fichier de sections, `shortcutSheet.ts`, que l'essai de non-écart garde juste.
- Firefox n'est pas dans les parcours (Chromium, ou Edge avec `E2E_BROWSER=msedge`) : la copie y reste à l'essai du porteur
  du projet. Claviers non couverts par Maj+1 : le bouton.
- Dix-huit langues pour les textes nouveaux ; manuels français et anglais.

## Vérification

À écrire avec le code (essais d'abord, vus rouges), le lot n'étant pas encore codé :

- `viewport.test.ts` (`fitView` : carte vide, petite carte à 1, large et haute, très grande à 0,2) ;
  `useBoundedViewport.test.tsx` ; `canvas/fit.test.tsx` (bouton, titre, ouverture inchangée) ; `keys.test.ts` (Maj+1 et
  Maj+! pour un lecteur aussi, étape seule choisie) ; `BpmnView.test.tsx` (zoom limité à 1).
- `clipboard.test.ts` (promesse écrite dans le tour du clic, repli, `false`) ; `copyImage.test.ts` (carte entière,
  8 000 px contre 12 000 de l'export, refus et « Exporter en PNG », rien pendant un autre export, cadre d'une
  sélection) ; `exportState.test.tsx` (`is-left-out`) ; `menus.test.ts` (entrées de chaque rôle, grisées en vue BPMN,
  « Présenter depuis cette étape » après les lignes de lecture).
- `shortcutSheet.test.ts` (aucun écart avec `KEY_COMMANDS`, chaque touche `other` entendue) ; `ShortcutsDialog.test.tsx` ;
  `help.test.tsx` (le menu Aide, F1 inchangé).
- Parcours `map-gestures-34.spec.ts` et `help.spec.ts`, en modes Poste et Serveur, dont un lecteur.
