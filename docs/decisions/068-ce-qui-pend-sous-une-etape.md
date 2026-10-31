<!-- 068-ce-qui-pend-sous-une-etape.md -->
<!-- Le libellé d'un losange, la fréquence d'une étape détaillée et les pastilles RACI s'écartent de l'axe de la pointe basse quand une flèche y part ou y arrive (P6-T11b, AR-128) -->

# 068 - Ce qui pend sous une étape s'écarte des flèches du bas

Statut : acceptée (date 28/10/2026)

## Contexte

La démonstration (P6-T11) a montré la pointe d'une flèche cachée : le retour « non » d'une décision revient à l'étape
détaillée « Saisir la note de frais » par sa pointe basse (règle de la boucle, B-3.4-01 à B-3.4-04 : une cible à gauche
se rejoint par le bas), et le badge de sa fréquence, centré sous l'étape (spec 9.4, B-2.9-08), est dessiné par-dessus ;
les pastilles RACI (B-2.11-05), centrées au même endroit, cachaient le segment de cette flèche dans la bande libre. Le
registre le notait comme mineur (F-25) ; une pointe invisible fait croire qu'aucune flèche ne revient à l'étape.

Le routage (ADR 059) ne connaît pas ce qui pend sous une étape ; seule l'étiquette d'un losange s'écartait, et seulement
pour une sortie par la pointe basse (AR-58), toujours à droite. La bande libre sous une étape (28 px) est à la fois le
passage des boucles et des contours et la place de ces badges : agrandir les rangées changerait toute la géométrie de
la maquette validée.

## Options

1. Donner les badges au routage comme obstacles : les flèches les contournent. La bande de 28 px n'a pas la place d'un
   badge de 18 px et d'une voie à côté ; les pastilles RACI n'existent qu'avec le calque, les flèches bougeraient en
   l'allumant.
2. Dessiner les flèches par-dessus les badges : la pointe se voit, mais un trait barre le texte des badges.
3. Écarter ce qui pend sous l'étape de l'axe de la pointe basse dès qu'une flèche l'emprunte, du côté où ses flèches ne
   passent pas : la règle d'AR-58, étendue.

## Décision

Option 3 (AR-128).

1. **Quand** : dès qu'une flèche part ou arrive par la pointe basse d'une étape, quel que soit son type (une tâche, une
   attente ou un losange ; avant, seul un losange dont une sortie partait par le bas). Sinon, tout reste centré.
2. **De quel côté** : à droite de l'axe, aligné à gauche à 10 px de lui, comme AR-58 ; à gauche de l'axe, aligné à
   droite à 10 px de lui, quand les flèches de la pointe basse qui courent de côté dans la bande libre sous l'étape vont
   toutes vers la droite (le retour qui arrive de l'allée de droite, l'extrémité d'une boucle dont l'autre bout est à
   droite). Une flèche qui file tout droit vers le bas, ou ne tourne qu'une fois sortie de la bande, ne compte pas pour
   le côté ; un écart de moins de 10 px (passage sur une voie de l'axe) non plus. Des flèches des deux côtés : à droite.
3. **Quoi** : le libellé d'un losange (AR-57), le badge de fréquence d'une étape détaillée (spec 9.4) et les pastilles
   RACI (B-2.11-05), ensemble. Les pastilles gardent leur retour à la ligne, dans la moitié de l'étape et sa marge de
   14 px.
4. **Où** : module pur `model/geometry/routing/under.ts` (`underSides`), appelé par le routage après le placement sur
   les voies (`layoutFlows`, dont `routeMap` ne garde que les flèches) ; le côté sert à l'obstacle de l'étiquette d'un
   losange pour les étiquettes de flèches, et passe à l'affichage par `StepFigure.under` (classe `is-under-left` ou
   `is-under-right` de l'étape) ; la distance vient de `UNDER_STEP.shift` (variable `--under-shift`).

## Conséquences

- La pointe et l'arrivée d'une flèche du bas restent visibles : prouvé dans Chromium par le parcours
  `e2e/under-step.spec.ts` (points du tracé hors des boîtes du badge et des pastilles, pointe comprise ; il échoue sans
  les règles de style), et sur 500 cartes aléatoires par la propriété de `properties.test.ts` (aucune flèche de la
  pointe basse sous un badge de 60 × 18 placé du côté choisi, sauf si l'autre côté est pris aussi).
- Restes acceptés, inscrits à F-25 : une flèche d'une autre étape qui passe sous toute une ligne (un long contour) peut
  encore croiser des badges ; un badge de fréquence long (« Clôture mensuelle ») écarté de l'axe déborde dans l'allée ;
  une voie de l'axe décalée de 12 px ou plus (trois flèches sur un même axe) peut passer sous le côté droit.
- AR-58 est étendu, non remplacé : pour un losange dont une sortie part par le bas, le libellé reste à droite (sa
  sortie ne court pas dans la bande sous lui) ; une flèche qui arrive par la pointe basse d'un losange l'écarte aussi.
