<!-- mesures-nf03-nf04.md -->
<!-- Ouverture d'une carte de 150 étapes et fluidité jusqu'à 300 étapes, mesurées dans le navigateur sur le vrai hôte (S-NF-04 à S-NF-06) -->

# Mesures de NF03 et NF04

Une durée n'est pas une assertion stable sous charge (ADR 040) : les deux mesures ne font pas partie de la suite des
parcours. Elles se jouent à part, depuis `topogenia/web`, sur l'interface construite et le lanceur des parcours :

- mode Poste : `E2E_MEASURE=1 E2E_NO_BUILD=1 E2E_PORT=<port> npx playwright test` ;
- mode Serveur : la même commande avec `E2E_MODE=server` (base `Topogenia_E2E`) ;
- le navigateur Edge de la machine : `E2E_BROWSER=msedge` en plus.

Sources : `web/e2e/measures/opening.measure.ts`, `web/e2e/measures/fluidity.measure.ts`, outils `web/e2e/measures/measure.ts` ;
le choix des fichiers par `E2E_MEASURE` dans `web/playwright.config.ts`. Chaque mesure échoue quand son critère n'est pas
tenu, et écrit ses valeurs dans le journal et dans les annotations du rapport de Playwright.

## Définition

**Carte mesurée.** Disposée par l'API avant la mesure : la forme de la mesure de NF05 (S-NF-07), des couloirs de 30 étapes
reliées en chaîne. Ouverture : 5 couloirs, 150 étapes, 145 flèches. Fluidité : 10 couloirs, 300 étapes, 290 flèches.

**Ouverture (NF03, S-NF-04 et S-NF-05).**

- Début : par son adresse, le début de la navigation vers `/maps/{id}` (origine de `performance.now()` : la page, le
  morceau principal de l'interface, le profil, la carte, la mise en page et le routage des flèches comptent) ; depuis
  l'accueil, le clic sur le lien du projet (`project-<code>-open`, navigation dans l'application).
- Fin : les 150 étapes (`.react-flow__node-step`) et les 145 flèches (`.react-flow__edge`) dans la page, puis deux images
  du navigateur (la carte peinte). Relevé dans la page par un `MutationObserver` posé avant le premier script.
- Répétitions : une ouverture de mise en train non comptée (première lecture de l'hôte et du morceau), puis 5 ouvertures
  par chemin.
- Critère : la médiane des 5 sous 1 000 ms en mode Poste, sous 2 000 ms en mode Serveur, par les deux chemins ; le
  maximum est écrit.

**Fluidité (NF04, S-NF-06).**

- Déplacement : l'outil Main (celui de l'ouverture) prend le fond dans la bande libre au-dessus des étapes du premier
  couloir et le tire de 600 px vers la gauche en 60 mouvements ; la vue doit avoir suivi (plus de 500 px).
- Zoom : quatre crans de molette vers l'avant puis quatre vers l'arrière au milieu du canevas, Ctrl tenu depuis la 3.0
  (spécification 3.0 section 15.1 : la molette seule fait défiler) (la vue doit dépasser 1,5),
  puis retour au début de la carte à 100 % par le bouton de la vue.
- Mesure : les intervalles entre deux images du navigateur (`requestAnimationFrame`) pendant le geste.
- Répétitions : un déplacement et un zoom de mise en train, écrits et non comptés (premier dessin de la carte à d'autres
  places et d'autres échelles), puis 3 déplacements et 3 zooms.
- Critère, pour chacun des 6 gestes comptés : la médiane des intervalles dans deux images de 60 Hz (33,8 ms : 30
  images par seconde et plus) et le 95e centile dans trois (50,5 ms : pas d'à-coup visible) ; le demi-millième vient de
  l'horloge des images, qui varie de quelques dixièmes autour de 16,7 ms (un 95e centile de trois images a été lu
  50,1 ms ; la première écriture du critère, « 50 ms », le refusait).

## Résultats du 29/10/2026

Poste de développement, Chromium de Playwright sans écran, pendant que d'autres agents compilaient et jouaient leurs
suites sur la même machine (les valeurs sont donc pessimistes).

| Mesure | Mode Poste | Mode Serveur |
|---|---|---|
| Ouverture par l'adresse (5 fois) | 483 / 466 / 465 / 407 / 397 ms, médiane 465, max 483 | 1 106 / 1 054 / 945 / 968 / 902 ms, médiane 968, max 1 106 |
| Ouverture depuis l'accueil (5 fois) | 283 / 275 / 346 / 425 / 375 ms, médiane 346, max 425 | 828 / 833 / 807 / 807 / 711 ms, médiane 807, max 833 |
| Critère d'ouverture | 1 000 ms : tenu | 2 000 ms : tenu |
| Déplacement, 300 étapes (3 fois) | 2e passage : médianes 16,7 / 16,7 / 16,7 ms, 95e centiles 33,3 / 16,8 / 16,7 ms, max 50,0 ms ; 1er passage (sans geste de mise en train) : 95e centiles 33,4 / 33,3 / 33,3 ms | 2e passage : médianes 16,7 / 33,3 / 16,7 ms, 95e centiles 50,0 / 50,0 / 50,0 ms ; 3e passage : médianes 16,7, 95e centiles 16,8 / 33,4 / 33,4 ms, max 66,7 ms |
| Zoom, 300 étapes (3 fois) | 2e passage : médianes 16,7, 95e centiles 16,7 / 16,8 / 16,8 ms, max 16,8 ms ; 1er passage : 95e centiles 33,3 / 33,4 / 33,4 ms | 2e passage : médianes 16,7, 95e centiles 66,6 / 66,6 / 16,7 ms, max 83,3 ms ; 3e passage : médianes 16,7, 95e centiles 50,1 / 50,0 / 49,9 ms, max 83,3 ms |
| Mise en train (non comptée) | déplacement : 95e centile 50,0 ms, max 100,0 ms ; zoom : 95e centile 66,7 ms, max 83,4 ms | 2e passage : 33,4 et 66,7 ms (max 83,4 et 83,3 ms) ; 3e passage : 16,8 et 16,8 ms |
| Critère de fluidité | tenu aux deux passages (médiane d’une image, 95e centile de deux au plus) | tenu au 3e passage (95e centile de trois images), pas au 2e (deux zooms à quatre images, 66,6 ms) |

Le déplacement et le zoom ne font aucun appel à l'hôte : les deux modes ne diffèrent que par la charge de la machine au
moment de la mesure. La fluidité reste donc à rejouer machine calme (à l'intégration, sans autre suite en cours) avant de
fermer S-NF-06 : tenue deux fois en mode Poste, une fois sur deux en mode Serveur. Un premier passage, sans geste de mise en train, avait donné en mode Serveur un 95e centile de
66,7 ms sur le premier zoom (premier dessin de la carte à d'autres échelles) : d'où le geste de mise en train.

## Fluidité rejouée machine calme (29/10/2026, intégration des corrections du round 1)

Aucune autre suite en cours ; interface construite des lots A et B ; deux passages par mode
(`Travail/mesure-fluidite-p6-r1-<mode>-<n>.log`), chacun avec son geste de mise en train, 3 déplacements et 3 zooms :

| Passage | Médianes | 95e centile le plus haut | Intervalle le plus long |
|---|---|---|---|
| Mode Poste, 1 | 16,7 ms partout | 16,8 ms | 16,8 ms |
| Mode Poste, 2 | 16,7 ms partout | 16,8 ms | 33,3 ms |
| Mode Serveur, 1 | 16,7 ms partout | 16,8 ms | 33,2 ms |
| Mode Serveur, 2 | 16,7 ms partout | 16,8 ms | 16,8 ms |

Critère de fluidité tenu aux quatre passages, avec de la marge (une image de 60 Hz en médiane et au 95e centile) : les
écarts du passage précédent venaient de la charge de la machine. S-NF-06 est fermée.
