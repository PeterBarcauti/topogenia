<!-- 060-carte-affichee.md -->
<!-- La carte affichée (P6-T04 lot 1) : React Flow, nœuds et poignées déclarés, flèches du routage, mesure des étiquettes, vue bornée, styles -->

# 060 - La carte affichée

Statut : acceptée (date 28/10/2026)

## Contexte

P6-T04 est découpée en trois lots (`plan.md`, phase 6). Le lot 1 affiche la carte : magasin et chargement, canevas,
vue accrochée, étapes (codes, pictogrammes, jauge, avertissements), en-têtes de couloirs, flèches tracées par le module
de routage de P6-T05 (ADR 059). La spec 3.5 donne l'organisation (`model/`, `state/`, `canvas/`, `nodes/`, `edges/`,
`chrome/`), la spec 9.3 à 9.5 et la maquette validée donnent le rendu, l'annexe B 2.1, 2.2, 2.10, 2.12 et 3 les
comportements. L'annexe B 0 garde React Flow 12 (B-0-01).

## Décision

1. **React Flow** : `@xyflow/react` 12.12.0, la version de la 1.x, publiée le 24/09/2026 (licence MIT ; dépendances MIT,
   ISC, BSD-3-Clause, `THIRD-PARTY-NOTICES.md` régénéré). Seule sa feuille `base.css` (structure) est chargée ; les
   couleurs viennent de `styles/map.css`, écrites sur les éléments de React Flow avec les jetons : la règle de la charte
   interdit d'affecter une variable à une autre hors de `tokens.css` (`--xy-*: var(--...)`). La mention de la
   bibliothèque, un lien vers son site, est masquée (`proOptions.hideAttribution`, licence MIT) : l'application ne montre
   aucune adresse extérieure (D14).
2. **Données en deux temps** : `model/mapLayout.ts` (pur) met la carte en page (bandes des couloirs et leur teinte,
   boîtes des étapes, libellés dans la langue affichée, flèches routées avec leurs étiquettes), `canvas/flowElements.ts`
   en fait les nœuds et les liens de React Flow. Une étape déclare sa taille (170 × 64) et ses quatre poignées
   (`handles`, toutes `source`, au milieu de chaque côté) : React Flow place les liens sans avoir mesuré les étapes sur
   la page, et les tests les voient sous jsdom.
3. **Flèches** : le chemin SVG vient entier du routage ; un seul composant (`edges/RoutedEdge.tsx`) dessine les trois
   types (`flow`, `loop`, `detour`, B-3.4-33), écart assumé à la liste de fichiers de la spec 3.5 (`Flow`, `Loop`,
   `Detour`) qui supposait trois tracés. Pointe : le `ArrowClosed` de React Flow (18 × 18, à l'échelle du trait), dessiné
   une fois par le canevas (`canvas/ArrowMarker.tsx`) pour prendre `--edge` par la feuille de style. Zone de clic de
   16 px, étiquette dans la boîte placée par le routage.
4. **Mesure des étiquettes** : le routage reçoit la taille de l'étiquette de chaque flèche et du libellé écrit sous un
   losange. `model/textSize.ts` les calcule : texte coupé mot à mot (un mot trop long coupé comme la feuille de style le
   coupe), 110 px au plus pour une flèche, deux lignes de 200 px au plus sous un losange. La largeur est mesurée par un
   canevas 2D dans la police de la page, mesurée de nouveau quand les polices sont chargées ; sans canevas (jsdom), une
   estimation. Les dimensions viennent de `geometry/constants.ts` (`EDGE_LABEL`, `GATEWAY_LABEL`), exposées en variables
   CSS (spec 3.5). L'étiquette d'une flèche est en 11 px, comme la maquette validée (la 1.x écrivait 0,75 rem).
5. **Vue** : contrôlée par l'éditeur (`canvas/useBoundedViewport.ts`), jamais gardée dans le navigateur (B-7-09),
   ouverte au début de la carte. Chaque mouvement de React Flow est borné (`model/geometry/viewport.ts`, B-2.2-04 à 08) ;
   la vue montrée est bornée de nouveau quand la carte ou l'écran change (B-2.2-09). Défaut trouvé par le test du
   glisser du fond et corrigé : quand la vue bornée était égale à celle gardée, l'état ne changeait pas et React Flow
   restait au-delà de la borne ; l'éditeur rend une vue neuve à chaque mouvement, comme la 1.x. Zoom de 0,2 à 2, double
   clic sans zoom, contrôles + − et « Revenir au début de la carte », sans « ajuster » ni verrou ; mini-carte et points
   de 24 px aux couleurs des jetons.
6. **Étapes et couloirs** : rendu de la maquette validée (spec 9.4) ; teinte du couloir par son numéro (pastel exact,
   sinon la teinte la plus proche, en distance RGB) ; liseré et contour du losange dans l'encre du
   couloir. Pictogrammes d'un losange sur un fond blanc : le bas étroit du losange laisserait le rouge sourd d'un
   irritant sur le gris d'un couloir pair (4,43:1, la charte demande 4,5:1). Étape détaillée : pile, pastille
   « détaillée » et fréquence (spec 9.4). En-têtes fixes : 184 px, haut et hauteur suivant la vue et le zoom. La colonne
   du fond de couloir qui reprend nom et type (B-2.12-25) est dessinée mais cachée à l'écran : la vue part 8 px à droite
   de la marge et la laissait dépasser des en-têtes ; l'export (P6-T09) la montrera.
7. **Sélection au lot 2** : les éléments sont sélectionnables (comme la 1.x) mais aucun changement n'est encore appliqué
   au magasin : sans cela, React Flow coupe les événements de pointeur des étapes et le survol (pictogrammes « au
   survol », poignées) ne répondrait pas. Ni glisser ni relier au lot 1.
8. **Magasin** : `state/mapStore.ts`, un par éditeur : carte, historique, chargement, erreur, mode, source de liaison,
   élément en édition, type des prochaines étapes (`task`) ; une lecture annule celle qui court (B-7-18). Les préférences
   restent dans leur magasin (P6-T02), composées au magasin de l'éditeur au lot 3.
9. **Répartition des lots** : la barre de l'éditeur dans l'en-tête de l'application (fil d'Ariane B-2.1-01 à 04, bouton
   Fichier, bouton Membres, bouton Traductions) va au lot 3, avec le menu Fichier et le dernier projet ouvert.
10. **Tests sous jsdom** (`src/test/flowDom.ts`) : `ResizeObserver`, `DOMMatrixReadOnly`, `checkVisibility`, la taille
    du panneau de React Flow (1000 × 600) et l'absence de canevas sont fournis ; React Flow dessine alors nœuds, liens,
    contrôles et mini-carte, et le zoom et le glisser de sa bibliothèque d3 répondent à de vrais événements.

## Conséquences

- Le badge de fréquence d'une étape détaillée, sous l'étape, n'est pas un obstacle pour le routage : une boucle peut le
  croiser (registre F-25).
- Le paquet JavaScript dépasse 500 ko (540 ko, 173 ko compressé) ; Vite l'annonce. Découpage à étudier en fin de projet
  (registre F-26).
- Vérification visuelle faite dans Chromium sans tête sur l'interface construite, l'API simulée par interception
  (captures dans le scratchpad de la session, non versionnées) : rendu conforme à la maquette, survol, zoom et bornes de
  la vue, aucune erreur de console.
