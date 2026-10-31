<!-- 070-clavier-menus-et-finitions-de-l-interface.md -->
<!-- Fiche et panneau RACI au clavier, focus, menus contextuels de la maquette, contraste d'un irritant, « ! » d'un losange, notifications et panneau (porte P6 round 1, AR-136 à AR-142) -->

# 070 - Clavier, menus et finitions de l'interface

Statut : acceptée (date 29/10/2026)

## Contexte

Le contrôleur Interface et charte du round 1 de la porte de la phase 6 a relevé un bloquant et cinq majeurs dans la
carte : la fiche et le panneau RACI ne s'ouvraient qu'à la souris (B1, NF08, S-NF-12) ; les « + » d'ajout rapide
prenaient le focus sans se montrer (M1, S-NF-11) ; l'aide d'un irritant tombait à 4,35:1 sur son fond rose (M2,
S-NF-10) ; le « ! » d'un losange recouvrait le début de son code (M3) ; une étiquette de flèche flottait à 37 px de sa
flèche (M4, révision du point 5 de l'ADR 059, AR-139) ; les menus contextuels s'écartaient de la maquette `05-menus`
(groupes du couloir absents, pas de grille de pastilles, deux radios cochés ensemble, entrées de l'étape sans icône, M5).
La 1.x n'ouvrait la fiche qu'au clic et ne donnait le focus à aucun panneau.

## Décision

1. **Entrée sur une étape** (AR-136) : Entrée sur une étape qui a le focus fait exactement ce que fait le clic : sa
   fiche, son panneau RACI quand le calque est affiché et qu'on peut modifier la carte, la source puis la cible du mode
   liaison (`state/actions/stepActivation.ts`, appelé par le clic et par la touche). La touche est arrêtée là, sinon
   elle « appuierait » sur le contrôle qui vient de prendre le focus (vu dans Chromium : une lettre RACI donnée par la
   seule touche Entrée). Espace garde son rôle (choisir l'étape). Le menu de l'étape (clic droit, Maj+F10) gagne
   « Ouvrir la fiche » (étape à contenu) et « Rôles RACI… » (calque affiché, droit de modifier), en tête, après « Ouvrir
   sa carte détaillée » ; sa première entrée active, donc focalisée à l'ouverture, devient « Ouvrir la fiche ».
2. **Qui prend le focus** (AR-137) : un panneau ouvert depuis le clavier prend le focus (la fiche elle-même, la première
   lettre du panneau RACI) ; ouvert à la souris, il le laisse où il est, comme la 1.x. Refermé alors qu'il tient le
   focus (Échap, sa croix), il le rend à l'étape ; un menu le rend à ce qui l'avait avant lui. La modalité (clavier ou
   pointeur) est tenue par `shared/ui/inputModality.ts`, et non par `:focus-visible`, dont jsdom et Chromium n'ont pas
   la même heuristique ; le gestionnaire de clavier reste le seul à écouter les touches (B-8.1-08). Un panneau RACI sans
   lettre à donner (une étape du seul couloir : son R seul) prend lui-même le focus (précisé au round 2, trouvé par le
   parcours `e2e/keyboard.spec.ts`).
3. **Contraste de l'aide d'un irritant** (AR-138) : jeton dérivé `--text-muted-on-danger-soft` (#676A71, 4,62:1 sur
   `--danger-soft`), `--text-muted` assombri par pas de 2 % jusqu'à 4,5:1 (règle des encres de couloir, AR-36) ;
   ajustement d'accessibilité au même titre qu'AR-53 à AR-56.
4. **« ! » d'un losange** (AR-140) : au milieu du côté haut gauche du losange, attaché à la forme, loin des pointes où
   arrivent les flèches et sous le code écrit au-dessus.
5. **Menus de la maquette** (AR-141) : le menu reçoit des groupes de choix (`role="group"` nommé par son titre en petites
   capitales) : « Type » et « Couleur » pour un couloir (grille de 4 pastilles par ligne, coche sur la choisie, « Ciel ·
   teinte 3 sur 8 » dessous, flèches gauche et droite dans la grille), « Type de l'étape » pour une étape (radio, puis la
   forme à droite) ; **un seul choix coché par groupe**. Chaque action de tous les menus a son icône, au-delà de la
   maquette. Les raccourcis passent en `aria-keyshortcuts`, leur texte `aria-hidden`. Écart à la maquette gardé : les
   noms accessibles restent « Type : Service » et « Couleur : Rose » (la maquette nomme une pastille « Rose poudré ») ;
   ils contiennent le texte affiché (critère 2.5.3) et les parcours les emploient.
6. **Notifications et panneau latéral** (AR-142) : un panneau ouvert, les notifications passent dans le coin bas droit du
   canevas, à gauche du panneau, au-dessus de la mini-carte et du zoom, pour ne couvrir ni son pied (« Je valide ma
   fiche ») ni les commandes du canevas (précisé au round 2).
7. **`data-testid`** (S-3.5-04) : le lien d'un document (`document-link-<id>`) et les suggestions d'outil
   (`sheet-tool-suggestion-<outil>`) les reçoivent ; un test statique (`src/test/testIds.test.ts`) exige un
   `data-testid` sur chaque élément interactif natif des composants.

## Conséquences

- Les fonctions principales de la carte se font au clavier : choisir, ouvrir la fiche ou les rôles RACI, relier, menu
  d'une étape ou d'un couloir, ruban, ajout rapide ; relevé des tabulations dans Chromium sans arrêt caché.
- Hauteur du menu d'une étape à 1366 × 768, animateur, calque RACI affiché : 752 px, sans défilement ; une étape
  détaillée avec le calque peut faire défiler le menu.
- Les captures de la démonstration montrant les menus, les losanges et les étiquettes changent (rejouée à
  l'intégration).
- Reste au registre F-25 : les pastilles RACI écartées de l'axe passent à la ligne et peuvent toucher le couloir voisin ;
  sous le libellé d'un losange, la bande de 28 px n'a pas leur place (38 px).

## Révision (29/10/2026, essai du porteur, R1) : le clic choisit, Entrée ouvre la fiche (AR-151)

Le point 1 ne vaut plus pour le clic en mode normal : le clic sur une étape la choisit sans rien ouvrir. Entrée garde
ce qu'il faisait (la fiche, le panneau RACI sur son calque, la source puis l'arrivée en mode Relier) ; le clic ne le
fait plus que dans le mode Relier, sur le calque RACI pour qui peut changer la carte, et au doigt (un toucher ouvre la
fiche : une tablette n'a pas de touche Entrée, S-NF-03). Une fiche déjà ouverte suit
l'étape cliquée. Un nouveau clic sur l'étape choisie seule la renomme sur place, après le délai d'un double-clic et si
elle a encore le focus (un double-clic, Entrée ou un menu l'emportent) ; une lettre tapée sur elle commence son nom
(`canvas/useCanvasEvents.ts`, `canvas/useCanvasKeys.ts`, `nodes/LabelInput.tsx`, état `editingSeed` du magasin).
