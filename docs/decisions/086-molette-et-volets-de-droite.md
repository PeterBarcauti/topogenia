<!-- 086-molette-et-volets-de-droite.md -->
<!-- The wheel scrolls the canvas and Ctrl + wheel zooms (3.0, lot 7b); every right panel resizes and folds; the summary, main path and check of the value stream in their own panel, short texts, a page of the PDF -->

# 086 - La molette qui défile, les volets de droite, le volet du flux de valeur

Statut : acceptée (date 31/10/2026). Remplace la règle 2.0 B-2.2-02 (« la molette zoome ») et le point 5 de l'ADR 085
pour ce qui était dessiné sous les couloirs ; prolonge le point 7 de l'ADR 085 (exports). Décision du porteur du projet
du 31/10/2026, après l'essai du lot 7 : la molette qui ne fait que zoomer est « contre-intuitive » ; le bloc sous les
couloirs « n'est pas du tout pro : pour un écran collaboratif, le diagramme doit toujours rester visible » ; puis, sur
la conception présentée : « on les donne à tous les volets de droite. Le texte affiché est aussi trop bavard, résumer,
efficace ». Spécification : section 15 de la spécification 3.0 (révision 6).

## Contexte

La 1.x et la 2.0 zoomaient à la molette, comme les outils de tableau blanc. Les logiciels de schéma de processus que
connaissent les utilisateurs (Visio, draw.io, Lucidchart) et les pages web font défiler à la molette et zoomer avec
Ctrl. Le bloc du flux de valeur, dessiné sous le dernier couloir, sortait de l'écran sur une grande carte et obligeait
à quitter le diagramme pour le lire ; ses phrases étaient longues. Les volets de droite (fiche, assistant, reste d'un
cadre) avaient une largeur fixe et ne se repliaient pas.

## Décision

1. **Molette** : défilement vertical, Maj + molette horizontal, pavé tactile dans les deux sens ; Ctrl + molette et
   pincement zooment, centrés sur le pointeur ; un cran défile comme dans une page web ; la vue reste bornée ; les
   en-têtes fixes des couloirs suivent les mêmes gestes ; la mini-carte garde la molette qui zoome.
2. **Volets de droite**, tous : un à la fois (ouvrir l'un ferme les autres ; allumer le calque ferme la fiche,
   l'assistant et le reste du cadre ; le volet du flux de valeur revient quand le volet ouvert après lui se ferme) ;
   une poignée de largeur sur le bord gauche (souris et clavier, double clic pour la largeur d'origine, de 320 px à
   70 % de la fenêtre), une seule largeur retenue dans les préférences ; « Réduire » replie le volet en une bande sur
   le bord droit, sans perdre son état ; ouvrir un volet le montre déplié.
3. **Volet « Flux de valeur »** : le calque allumé l'ouvre ; bilan en tuiles, chemin principal ligne par ligne,
   contrôle groupé par code avec l'objet de chaque mention en pastille qui y mène ; libellés courts. Rien n'est plus
   dessiné sous les couloirs ; cartouches, parts et couloirs restent sur le diagramme.
4. **Exports** : l'image ne porte plus que le diagramme ; le PDF, calque allumé, ajoute une page « Flux de valeur »
   (bilan, base de calcul, chemin principal, contrôle) ; le classeur ne change pas.

## Conséquences

- Les habitués de la 2.0 zoomaient à la molette seule : ils zooment désormais avec Ctrl. Le défilement se fait sans
  outil Main ni touche.
- La géométrie de la carte ne dépend plus du bloc du bas : la hauteur bornée de la vue est celle des couloirs, calque
  allumé ou éteint.
- Aucun format ne change ; les tests de la vue, des couloirs et des exports sont adaptés aux nouveaux gestes.
