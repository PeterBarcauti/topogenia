<!-- 091-trace-impose-des-fleches.md -->
<!-- The route of a flow chosen by the user: the side of each end and one passage of the grid, kept in the core, the base and both formats; the flows without a choice drawn as in 3.0.2 -->

# 091 - Le tracé imposé des flèches

Statut : acceptée (date 03/11/2026). Prolonge l'annexe B 3.4 (tracé des flèches), la spécification 9.7, les ADR 027
(historique du schéma), 059 (tracé et libellés), 062 (annuler) et 085 (part de flux, précédent d'un champ facultatif de
la flèche). Demande du porteur du projet du 03/11/2026, après le bilan de maturité d'un auditeur externe : c'est le
principal inconvénient pour l'utilisateur. Une flèche accrochée sous une étape ne se raccroche pas au-dessus, même si
c'est plus lisible ; une flèche qui en coupe une autre ne peut pas être forcée par un autre passage libre.

## Contexte

Le tracé est calculé dans le navigateur, à partir de la seule place des deux étapes :

- les côtés : même colonne de bas en haut, cible à gauche de bas en bas, sinon de droite à gauche ; un point par sortie
  d'une décision (AR-24) ;
- les chemins : par les allées entre les colonnes et les bandes entre les rangées, puis des pistes de 6 px.

La flèche ne garde que ses deux étapes, son libellé et sa part. Les logiciels du marché (Visio, draw.io, Bizagi,
Lucidchart) offrent deux gestes : tirer le bout d'une flèche sur un autre point d'accroche de l'étape, et tirer un
tronçon pour la faire passer ailleurs. Une commande remet le tracé automatique.

## Décision

1. **Ce que garde une flèche**, sur `Flow`, en propriétés facultatives comme `SharePercent` :
   - `SourceSide` et `TargetSide` : `Top`, `Bottom`, `Left` ou `Right`, nul pour automatique ;
   - un passage au plus : une allée (`ViaAisle`, l'allée à droite de la colonne n, -1 avant la colonne 0), le milieu
     d'une colonne (`ViaColumn`), ou une rangée d'un couloir (`ViaLaneId`, `ViaRow`), le long de sa bande libre du haut
     ou du bas (`ViaEdge`) ou par son milieu (sans `ViaEdge`). Le milieu d'une colonne et celui d'une rangée viennent de
     l'essai du porteur du projet (04/11/2026) : une flèche ne pouvait pas se centrer dans une colonne vide entre deux
     autres, seulement se coller à l'une ou à l'autre ; la position libre au pixel près des logiciels du marché a été
     écartée pour garder la grille et l'assurance de ne jamais traverser une étape.
2. **Une flèche sans choix se dessine comme en 3.0.2.** Le calcul existant n'est pas modifié. Une copie figée en est
   gardée dans les tests et sert d'oracle sur 1 000 cartes au hasard. Les tracés réels des exemples, capturés avant le
   lot, sont comparés par un parcours.
3. **Une flèche avec un choix** :
   - Les bouts non imposés prennent les côtés du dessin automatique. Si rien ne diffère de ce dessin et qu'il n'y a pas
     de passage, il est gardé tel quel.
   - Le dessin se fait en deux passes. La première dessine toutes les flèches, dans l'ordre de lecture, comme si aucun
     choix n'existait : une flèche sans choix voit l'encombrement de ses voisines tel qu'en 3.0.2, et imposer un côté
     égal au dessin ne change donc rien à la carte. La seconde trace les flèches à choix, là où les autres laissent le
     plus de place.
   - Sinon, un routeur orthogonal cherche le chemin au moins de coudes :
     - il part du point imposé dans la direction de son côté et arrive par l'extérieur du côté de la cible ;
     - il passe par les amorces libres sur le milieu de la rangée ou l'axe de la colonne, puis par les allées et les
       bandes, et il passe par le passage imposé ;
     - à égalité, il prend le moins encombré, puis le plus court, puis le premier couloir. Le résultat est le même à
       chaque fois.
   - Un passage qui ne nomme rien de la carte (un couloir ou une rangée disparus, une allée au-delà de celle qui suit
     la dernière colonne, par où la flèche sortirait de la carte) laisse la flèche au dessin automatique.
   - Le milieu d'une colonne ou d'une rangée peut porter des étapes : chaque tronçon qui y passe est contrôlé, jamais
     à travers une étape ; la flèche y passe là où il est libre, entre les allées et les bandes que le routeur parcourt
     autour des deux étapes. S'il y est fermé, la flèche garde ses côtés choisis et le dessin choisit son passage.
   - Sur le milieu d'une ligne ou l'axe d'une colonne, les amorces des étapes prennent leur piste avant un passage
     choisi : une pointe reste au milieu de son côté.
   - Les pistes, la pointe unique par côté d'arrivée (AR-129), les libellés et ce qui pend sous une étape (AR-128)
     s'appliquent sans changement.
4. **Branchements** (décision, parallèle, inclusif : la règle vaut pour les trois, `StepTypes.IsGateway`) :
   - les sorties imposées prennent leur point d'abord, les autres se répartissent sur les points restants ;
   - le point de gauche est l'entrée du branchement (AR-24) : il n'est jamais un départ, ni offert, ni gardé ;
   - imposer à une sortie l'entrée, ou un point déjà imposé à une autre sortie du même branchement, est refusé
     (`POINT_PRIS`). Le contrôle ne porte que sur un départ qui change : une sortie garde le sien et change ses autres
     parties librement ;
   - un doublon venu d'un changement de type se résout au dessin : la seconde sortie, dans l'ordre de lecture, redevient
     automatique. Le paquet `.topogenia` le garde tel quel ; la copie `.pmap` (duplication du projet) laisse le second
     au dessin, sans refuser le fichier.
5. **Ce que devient un choix quand la carte change** :
   - Un côté suit son étape partout.
   - Un passage est libéré quand une des deux étapes de la flèche change de case ou de carte, ou quand son couloir est
     supprimé :
     - la règle est appliquée par le cœur au moment de valider l'opération (`ProjectWorkspace.Commit`), pour toute
       opération ;
     - un passage qui a changé dans l'opération elle-même n'est pas touché, si bien qu'annuler et rétablir le restaurent
       à l'identique ;
     - annuler ou rétablir applique la même règle avant de contrôler la structure : une flèche rendue par l'annulation
       de sa suppression, quand le couloir de sa bande a été supprimé depuis par une autre personne (mode Serveur),
       revient sans son passage au lieu d'être refusée.
   - Une flèche recréée garde son départ quand elle part de la même étape que celle qu'elle remplace, et son arrivée
     quand elle arrive sur la même étape. C'est la règle du report de la part (insertion, recousage, insertion en ancre,
     regroupement, remontée) ; le collage reçoit les côtés de la flèche copiée.
     - L'insertion en ancre lit les côtés sur la flèche coupée elle-même, même retirée par l'opération (coupe d'un
       bloc) : le départ va à la flèche vers l'entrée du bloc, l'arrivée à la flèche depuis sa sortie, quand le bloc
       n'en a qu'une.
     - La remontée d'un détail donne au bout resté à sa place le côté de la flèche telle qu'elle est, et au bout sur une
       étape du détail le côté de la flèche d'origine retrouvée.
   - Une flèche nouvelle est automatique.
   - Une proposition de l'assistant garde le tracé des flèches qu'elle garde et n'écrit jamais de tracé : la carte
     qu'elle réécrit lui est envoyée sans tracé, et un tracé qu'elle écrirait n'est ni lu ni contrôlé, y compris quand
     elle est ouverte en nouveau projet.
6. **L'interface** :
   - Une flèche seule sélectionnée, pour qui peut modifier la carte, montre une pastille à chaque bout et une barre sur
     chaque tronçon déplaçable.
   - Tirer une pastille l'accroche au plus proche des quatre points de son étape. Tirer une barre accroche le tronçon à
     la position la plus proche : pour un tronçon vertical, l'allée de gauche, le milieu de la colonne ou l'allée de
     droite (une position par demi-colonne) ; pour un tronçon horizontal, la bande du haut d'une rangée, son milieu ou
     sa bande du bas.
   - L'aperçu est en direct, recalculé seulement quand le côté ou le passage visé change ; Échap annule.
   - Un geste commence quand le pointeur s'éloigne de 4 px : un clic, un autre bouton que le principal, un geste
     annulé par le navigateur ou un autre choix pendant le geste n'écrivent rien.
   - Le changement est appliqué au tracé de la carte au moment où l'écriture part : ce que l'hôte a changé entre-temps
     (un passage libéré) n'est jamais réécrit. L'aperçu reste jusqu'à la réponse de sa propre écriture.
   - Chaque geste est une opération (`tracer_lien`), annulée par un Ctrl+Z.
   - Le menu de la flèche ajoute « Départ », « Arrivée » et « Tracé automatique ».
   - Rien de tout cela n'est visible à l'export ni pour un lecteur.
7. **Les données** :
   - sept colonnes facultatives sur `flows`, avec leurs contrôles, dans deux migrations par moteur (`FlowRoute`, puis
     `FlowRouteAxis` pour le milieu d'une colonne ou d'une rangée, ajoutée après l'essai sans toucher la première) ; la
     vue `bi.flow` est inchangée ;
   - `.topogenia` 2.2 et `.pmap` 1.3 : une flèche gagne `route` (`from`, `to`, `via` : `aisle`, `column`, ou `lane` et
     `row` avec un `edge` facultatif). Un passage n'est permis en `.pmap` que sur une carte qui donne ses colonnes ;
   - les deux formats sont écrits dans leur version précédente (2.1, 1.2) quand aucune flèche n'a de tracé imposé : un
     projet sans choix reste ouvrable par la 3.0.2 ;
   - API `PUT /api/flows/{id}/route` avec `If-Match`.

## Conséquences

- Une base passée en 3.1 n'est plus ouvrable par la 3.0.2 (`BASE_PLUS_RECENTE`, ADR 027) : les notes de version le
  disent.
- Un fichier qui porte un tracé imposé n'est pas lisible par la 3.0.2.
- Limites : un seul passage par flèche, pas de point libre au pixel près, pas de reconnexion d'un bout à une autre
  étape, passage libéré quand on déplace un bloc, une arrivée sur une décision libre de prendre un point de sortie.
  Les trois dernières sont inscrites au registre `a-peaufiner`.
- Une relecture indépendante du lot (code et interface, 04/11/2026) a trouvé les cas traités aux points 3, 4, 5 et 6 :
  le dessin en deux passes, l'entrée d'une décision, le départ contrôlé seulement quand il change, l'ancre, la remontée,
  l'annulation après la suppression d'un couloir, la proposition de l'assistant, le geste. Chacun a son test.
- La documentation du format `.pmap` disait lire une version plus récente qui n'ajoute que des propriétés facultatives.
  Le code la refuse, et c'est la règle gardée : la documentation est corrigée.
