<!-- 059-routage-des-fleches.md -->
<!-- Routage des flèches en module pur : côtés, pointes des losanges, tracés, voies de 6 px, étiquettes, propriétés sur 500 cartes -->

# 059 - Routage des flèches

Statut : acceptée (date 28/10/2026)

## Contexte

La spec 3.5 veut le routage en module pur, sans React ni DOM ; la spec 9.7 reprend les règles de la 1.x (annexe B 3.4) et
y ajoute l'attribution des voies (segments verticaux d'une même allée sur des voies distinctes de 6 px, deux sorties d'un
losange jamais sur une même pointe, étiquettes hors des étapes et des autres étiquettes), prouvée par des tests de
propriétés sur au moins 500 cartes : aucun segment à travers une étape, aucun chevauchement colinéaire de plus de 8 px
entre deux flèches de sources différentes, aucune étiquette sur une étape ou une autre étiquette, tracé déterministe.
En 1.x, le routage ne rendait que des poignées et deux abscisses ou ordonnées (`coudeX`, `contourY`) ; React Flow
dessinait. Ces propriétés portent sur le tracé : le module doit le calculer en entier. AR-24 et AR-25 ont déjà fixé les
voies de 6 px et les sorties en surnombre ; AR-19 la marge de 184 px.

## Décision

1. **Module** : `web/src/features/map/model/geometry/` : `constants.ts` (toute la géométrie, une seule fois, et les
   variables CSS qui en dérivent), `grid.ts` (grille, cases, positions), `routing/` (`model.ts`, `sides.ts`, `routes.ts`,
   `tracks.ts`, `labels.ts`, `paths.ts`, `routing.ts`). Entrée : couloirs, étapes (case, type, taille de l'étiquette d'un
   losange écrite sous sa forme), flèches (taille de leur étiquette, mesurée par l'appelant). Sortie par flèche : type
   (`flow`, `loop`, `detour`), côtés, points, courbe éventuelle, chemin SVG, boîte de l'étiquette. L'éditeur (P6-T04)
   dessine ces chemins tels quels.
2. **Côtés et pointes** : règles de la 1.x (B-3.4-01 à 12), avec AR-24 : une sortie imposée sur une pointe déjà prise,
   ou au-delà des trois pointes, part de la pointe droite sur sa propre voie ; deux sorties d'un losange ne partagent
   jamais un point.
3. **Tracés** : polylignes orthogonales par les passages qui ne croisent jamais une étape : la ligne des étapes (entre
   deux étapes, la ligne est libre par la règle), les allées entre colonnes, l'axe d'une colonne (vertical libre), les
   bandes libres entre deux rangées (contournements au-dessus d'une ligne, boucles en dessous). Coude et contournement
   suivent la 1.x (B-3.4-14 à 23). Coins arrondis de 10, au plus la moitié du plus court côté. Boucle sur une même ligne :
   la courbe de la 1.x ; vers une autre ligne : tracé orthogonal (AR-120).
4. **Voies** : chaque segment appartient à un passage ; deux segments de groupes différents dont les étendues se
   recouvrent y prennent des voies distinctes, de 6 px en 6 px autour du passage (0, −6, 6, −12, 12... ; dans une bande,
   haut de la rangée + 14 d'abord pour un contournement, B-3.4-17). Le groupe est l'étape source (ses flèches peuvent
   partir ensemble), et chaque sortie d'un losange à elle seule. Les horizontaux sont placés d'abord, leurs étendues
   élargies du plus grand déplacement d'un vertical (24 px), puis les verticaux sur les étendues obtenues. Les arrivées
   sur un même côté d'une étape partagent leur dernier segment et finissent sur un seul point (révision ci-dessous,
   AR-129 ; la première rédaction les posait sur des points distincts). Une boucle vers une autre ligne prend l'allée où
   les flèches déjà tracées laissent le plus de place, la plus proche de la cible d'abord.
5. **Étiquettes** : dans l'ordre de lecture des flèches, à la première place qui ne touche ni une étape, ni l'étiquette
   d'un losange (AR-57, écartée de l'axe dès qu'une flèche part ou arrive par la pointe basse, AR-58 étendu par AR-128,
   ADR 068), ni une étiquette déjà placée. Les places sont essayées dans cet ordre : celle de la boucle (bas − 8,
   B-3.4-26) ; sinon, **sur sa flèche ou près d'elle** : les segments horizontaux du plus long au plus court, sur le
   segment puis à 12 et à 24 px de lui, puis les segments verticaux, sur le segment puis juste à côté (6 px du trait) ;
   **seulement ensuite, loin de la flèche** : la bande libre au-dessus ou au-dessous des segments horizontaux (46 et
   60 px), puis plus loin le long des segments verticaux. Quand aucune place n'est libre, la première essayée
   (révision du 29/10/2026 ci-dessous).
6. **Déterminisme** : les flèches sont traitées dans l'ordre de lecture de leur source puis de leur cible ; le tracé ne
   dépend pas de l'ordre des listes reçues (prouvé en les mélangeant).
7. **Tests** : règle par règle sur des cartes faites à la main (`routing.test.ts`, `grid.test.ts`) ; propriétés sur
   500 cartes tirées d'une graine fixe (`properties.test.ts`, générateur `src/test/randomMaps.ts` : 1 à 4 couloirs, 1 ou
   2 rangées, 4 à 10 colonnes, tous les types d'étape, flux de lecture, sorties de losange surtout vers l'avant, retours),
   chaque échec nommé par sa graine pour être rejoué.

## Conséquences

- Capacité : 9 voies par allée et par bande entre deux rangées, 5 dans la bande au-dessus de la première ligne et sous
  la dernière (la grille est fixe). Au-delà, deux segments peuvent partager une voie : 1 carte sur 5 000 cartes très
  denses en essai, aucune sur les 500 du test (registre F-24).
- Les lignes de rendu des flèches (trait, pointe, zone de clic, boîte de l'étiquette, documents portés, B-3.4-29 à 32,
  35, 37 à 39) et l'emploi des variables CSS de la géométrie (B-3.1-04, 05, 20) vont à P6-T04.

## Révision (28/10/2026, P6-T11c) : un seul point d'arrivée par côté d'une étape (AR-129)

Le point 4 laissait chaque arrivée sur sa voie : deux flèches de sources différentes vers un même côté d'une étape
finissaient sur deux points à 6 px l'un de l'autre, d'où deux pointes de flèche. Sur un losange, dont le côté est une
pointe, la seconde finissait même à côté de la forme (capture 07 de la démonstration, NDF-E009). Le porteur du projet a tranché : une
seule arrivée par côté, comme la 1.x, où toutes les flèches finissaient sur la poignée du côté.

- **Voies** (`tracks.ts`) : le dernier segment d'une flèche porte son arrivée (étape cible et côté). Deux segments
  peuvent partager une voie s'ils sont du même groupe (point 4) ou s'ils arrivent sur le même côté de la même étape ; les
  derniers segments d'une même arrivée sont placés ensemble, sur la voie qui leur convient à tous, et finissent donc sur
  un seul point. Les autres segments gardent leurs voies distinctes : deux flèches qui montent une même allée vers une
  même arrivée courent côte à côte puis se rejoignent sur la ligne de la cible. Vaut pour les quatre côtés et les
  boucles par le bas, courbe d'une ligne et tracé orthogonal d'une autre ligne ensemble.
- **Sorties d'un losange** : inchangées, deux sorties ne partagent jamais un point (AR-24), même quand elles vont au
  même côté d'une même étape (elles s'y rejoignent alors).
- **Écart avec la lettre de la spec 9.7** : la propriété « aucun chevauchement colinéaire de plus de 8 px entre deux
  flèches de sources différentes » admet désormais ce dernier segment commun ; la propriété « aucune fin commune » devient
  « les flèches d'un même côté finissent sur un seul point ».
- **Vérification** : `routing.test.ts` (trois cas : une flèche de la ligne et une flèche à coude vers une tâche et vers
  un losange ; trois boucles vers le bas d'une étape ; deux sorties d'un losange vers un même côté) ; `properties.test.ts`
  sur les 500 cartes (fin commune par côté, chevauchement admis sur le seul dernier segment commun). Sondes sur une
  copie : sans la clé d'arrivée, les trois cas et la propriété de fin commune échouent ; sans le placement d'ensemble,
  la propriété échoue encore.

## Révision (29/10/2026, porte P6 round 1, C10) : une étiquette reste près de sa flèche (AR-139)

La première rédaction essayait la bande libre des segments horizontaux (jusqu'à 46 et 60 px) avant les places voisines
des segments verticaux. Une étiquette qui ne tenait pas sur un segment horizontal court partait dans la bande, loin de
sa flèche : « 1 document » de NDF-E002 vers NDF-E003 flottait à 37 px, au-dessus de NDF-E002, qui porte lui-même un
pictogramme de document (contrôleur Interface, M4). La spec 9.7 veut l'étiquette « de préférence sur un segment
horizontal » ; elle n'admet pas qu'elle quitte sa flèche.

- **Ordre** (`labels.ts`) : sur ou près des horizontaux (0, ±12, ±24), puis sur ou à côté des verticaux, puis la bande
  (±46, ±60) et plus loin le long des verticaux. La préférence horizontale tient : un segment horizontal qui a la place
  reste le premier choix.
- **Mesure sur les 500 cartes** : avant, 418 étiquettes sur 1 825 à plus de 24 px de leur flèche ; après, 125, toutes sur
  une flèche droite d'un seul segment horizontal plus court qu'elle (seule la bande au-dessus est libre) ou sur une
  flèche qu'une autre étiquette occupe.
- **Nouvelle propriété** (`properties.test.ts`) : une étiquette est à 16 px au plus de sa flèche (24 px moins la
  demi-hauteur d'une étiquette), sauf sur une flèche droite trop courte pour elle ou quand une autre étiquette est sur sa
  flèche ; elle échoue sur 229 étiquettes avec l'ordre d'avant. Cas de la démonstration rejoué à la main
  (`routing.test.ts`) : l'étiquette est centrée sur le segment vertical de l'allée.
- **Conséquence** : posée au milieu d'un segment vertical, une étiquette peut chevaucher la séparation de deux couloirs
  (comme le milieu du tracé de la 1.x) et couvrir la voie voisine d'une autre flèche qui emprunte la même allée.
