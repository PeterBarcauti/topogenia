<!-- 096-agrandir-un-couloir.md -->
<!-- A lane taller or shorter by a row (3.1, lot G): rows inserted or taken out by the core with what they hold, empty rows under what a lane holds kept by the editor only; its border pulled with what is under it going down live, double-clicked, its menu; the cell aimed shown while a step is dragged -->

# 096 - Agrandir ou réduire un couloir d'une rangée

Statut : acceptée le 07/11/2026 (lot G de la 3.1, essais J1c puis J1d), revue le même jour après l'essai J1c. Demande du
porteur du projet : poser une étape sous une autre du même couloir obligeait à la lâcher dans une bande d'environ 20 px au
bas de la dernière rangée (B-2.3-23, seuil 0,82), sans rien voir avant de lâcher. « On tire simplement au niveau de la
séparation des couloirs et le couloir s'agrandit automatiquement d'un pas. » Après J1c : « l'écran ne doit que
descendre » ; tirer vers le haut insère la ligne au-dessus de la première ligne du couloir inférieur, tout descendant ;
un double-clic sur la séparation retire la rangée vide de chaque côté.

## Contexte

Une rangée n'est pas enregistrée : un couloir a autant de rangées que ce qu'il tient, plus une. Le marché : Excel ajuste
une ligne par un double-clic sur sa bordure ; bpmn.io crée ou retire de l'espace en tirant (tout ce qui est dessous suit,
seul l'espace vide se retire, un couloir jamais sous son contenu) ; Visio fait suivre les formes groupées au couloir tiré ;
Lucidchart et draw.io redimensionnent le couloir sans déplacer les formes.

## Décision

1. **Ce qui tient une rangée** : une étape, et une flèche dont le tracé choisi passe par le milieu ou une bande de la
   rangée (ADR 091, `Flow.ViaLaneId`, `ViaRow`). Le cœur et l'interface comptent pareil (`LaneRows.IsEmpty`,
   `gridShape.holdingCells`).
2. **Le cœur insère ou retire des rangées** (`Domain/Lanes/LaneRows.cs`) : `POST /api/lanes/{id}/rows` (`row`, `count`)
   fait descendre les étapes du couloir à partir de la rangée et les tracés fixés le long de ces rangées ;
   `DELETE /api/lanes/{id}/rows/{row}` retire une rangée vide, ce qui est dessous remonte, `RANGEE_NON_VIDE` (409) si
   elle tient quelque chose. Une opération chacune (`inserer_rangees`, `retirer_rangee`), un Ctrl+Z ; rangées 0 à 50.
   Les autres tracés suivent la règle de tout déplacement (ADR 091 : libérés au commit quand une de leurs étapes change de
   case). Animateur, sans If-Match (plusieurs objets).
3. **Les rangées vides sous ce que tient un couloir sont tenues par l'éditeur seul** (`rowsAtLeast`), tant que la carte
   est ouverte, jamais envoyées ; une étape posée dedans les rend réelles. Quand l'hôte insère ou retire, leur nombre est
   gardé ; sans elles, le couloir n'en gagne pas, pour qu'une annulation le rende tel qu'il était.
4. **La grille affichée est un seul objet** (`GridShape` : bande du flux de valeur, rangées tenues par l'éditeur), lu par
   le dessin, le tracé des flèches, le dépôt, le double clic, le collage, le menu du fond et l'export SVG
   (`state/gridShape.ts`, `shapeOf`, `useGridShape`).
5. **La séparation de deux en-têtes**, un seul module (`chrome/laneBorder.ts`) :
   - tirée : la bordure suit la main (vers le bas, le couloir du dessus grandit en bas ; vers le haut, le couloir du
     dessous grandit en haut), une rangée dès 4 px, une de plus par hauteur de rangée ; **tout ce qui est sous la
     séparation descend en direct** (un brouillon `rowsDraft` dessine la carte comme le geste la laisserait), les rangées
     nouvelles dans la couleur du couloir ; relâchée, l'éditeur ou l'hôte les donne ; Échap abandonne ; la vue ne bouge
     jamais (rien au-dessus de la séparation) ;
   - double-cliquée : la dernière rangée du couloir du dessus si elle est vide, la première du couloir du dessous si elle
     est vide (ce qu'il tient remonte), l'une ou l'autre ou les deux ; un couloir garde une rangée ;
   - le « + » posé dessus se tire comme elle ; son clic attend le délai du double-clic (500 ms, AR-151) avant d'ajouter un
     couloir, pour qu'un double-clic sur lui n'en ajoute pas deux.
6. **Menu du couloir** : « Ajouter une rangée en haut / en bas », « Retirer la rangée vide du haut / du bas » (NF08,
   WCAG 2.5.7 : une voie sans glisser ni double-cliquer ; la séparation ne prend pas le focus).
7. **Aperçu pendant le glisser d'une étape** : la case où elle irait, en pointillés ; la rangée qu'un dépôt bas créerait
   (B-2.3-23), dans la couleur de son couloir, sous les étapes. La règle du dépôt ne change pas.

## Conséquences

- Écart à la première version (J1c), corrigé : l'interface faisait descendre les étapes par une disposition (`layout`) ;
  le cœur ignorait qu'il s'agissait d'une rangée et un tracé fixé dans une rangée du couloir ne suivait pas. La règle vit
  désormais dans le cœur.
- Une rangée vide laissée en bas disparaît à la réouverture de la carte ; c'est voulu.
- Tests : cœur `Lanes/LaneRowsTests`, application `Lanes/LaneRowCommandsTests`, hôte `Maps/LaneRowsRoutesTests` (deux
  bases), listes (routes, droits, If-Match, codes d'erreur, messages en 18 langues, genres d'opérations) ; interface
  `model/gridShape.test.ts`, `model/laneRows.test.ts`, `chrome/laneBorder.test.ts`, `chrome/borderGestures.test.tsx`,
  `moves.test.ts`, `mapLayout.test.ts`, `flowElements.test.ts`, `streamBand.test.ts`, `fileMenu.test.tsx`, `menus.test.ts`,
  `history.test.ts` ; parcours `e2e/map-gestures.spec.ts` ; témoins 71 à 86 (J1c), 87 et suivants (G2).
