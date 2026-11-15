<!-- 061-edition-de-la-carte.md -->
<!-- L'édition de la carte (P6-T04 lot 2) : sélection au magasin, écritures sans mise à jour optimiste, glisser, commandes en données, ruban, menus, presse-papiers, aligner, clavier -->

# 061 - L'édition de la carte

Statut : acceptée (date 28/10/2026)

## Contexte

Le lot 2 de P6-T04 rend la carte modifiable (`plan.md`, phase 6) : ruban et ses zones, outils Main et Sélection, menus
contextuels, commandes et raccourcis, sélection et rectangle, glisser-déposer, mode Relier, aligner, presse-papiers,
en-têtes de couloirs (renommer, réordonner, ajouter), états choisi, coupé et source (annexe B 2.1, 2.3 à 2.5, 2.7, 2.8,
2.12, 3.2, 7, 8.2). La référence de comportement est l'éditeur 1.x (`EditeurCarte.tsx`, `magasin.ts`, `pressePapiers.ts`,
`alignement.ts`, `EnTetesCouloirs.tsx`, `Noeuds.tsx`, `Fenetres.tsx`). L'annulation, la barre Ctrl+K, le menu Fichier et
la barre de l'éditeur dans l'en-tête vont au lot 3 (ADR 060 § 9) ; le panneau de fiche à P6-T07 ; RACI et remontée à P6-T06.

## Décision

1. **Un éditeur par carte** (`state/editor.ts`) : le magasin de la carte, celui des préférences, celui de l'élément
   flottant, les notifications et la navigation, donnés aux composants par `state/EditorContext.tsx`. L'écran accepte un
   éditeur fourni (tests qui lisent son état).
2. **Sélection au magasin** (`selection: { steps, flows }`), source unique des commandes, des menus et du canevas ; React
   Flow reçoit `selected` et renvoie ses changements `select` (clic, Maj+clic, rectangle). Elle survit aux relectures,
   sans les objets disparus (B-2.3-28) ; après un collage, une duplication ou un regroupement, les `ids` de la réponse
   sont choisis tout de suite (B-2.3-30). L'outil (Main à l'ouverture), Espace tenu et la fiche ouverte sont aussi au
   magasin, jamais gardés (B-7-07, B-7-10).
3. **Écritures** : aucune mise à jour optimiste (B-2.6-11). Chaque opération (`state/actions/`) passe par
   `editor.write`, file sérialisée (B-2.6-09) : l'appel est fait avec la carte du moment, la réponse `StructureReply`
   porte la carte à jour, appliquée si c'est celle de l'éditeur, sinon la carte est relue ; un refus affiche son message
   (B-2.6-12), un 409 relit la carte (B-2.6-13). Une écriture peut ne rien envoyer (rien n'a changé, collage refusé).
4. **Glisser** : React Flow déplace les nœuds par ses changements `position`, gardés par le canevas jusqu'à la réponse
   de l'hôte ; une relecture pendant le glisser ne replace rien (B-2.3-27). Au lâcher : case du centre de l'étape
   (`cellAt`, B-2.3-22, B-2.3-23), déplacements utiles triés par colonne (B-2.3-24) ; plusieurs étapes sur des cases libres
   et distinctes : `PUT layout` (`purpose: move`, B-2.3-25) ; sinon `POST move` (AR-44, B-2.3-26), une étape seule et les
   flèches du clavier comprises. Aligner : calcul de la 1.x porté (`model/alignment.ts`), `PUT layout` `purpose: align`.
5. **Commandes en données** (`state/commands/`) : une définition par action (identifiant, libellé, icône, raccourcis,
   condition, visibilité, choix coché, rouge, valeurs du libellé, pastille) lue par le ruban, les menus et le clavier
   (B-8.2-06, B-8.2-07) ; contexte de la commande : carte, droits, sélection, presse-papiers, préférences, objet du menu,
   point visé. Menus en lignes groupées (`MenuLine`, séparateur entre deux groupes) ; Ctrl+V a sa propre commande
   (activée sans rien à coller, pour le dire), le Coller du ruban est grisé sans rien à coller (B-2.1-26). Le partagé
   `shared/commands` gagne `visible`, `checked`, `danger`, `labelValues`, `swatch` et `menuItemsOf`.
6. **Ruban** (`chrome/Ribbon.tsx`) : la barre partagée gagne les choix radio (outil, forme, losange) et les séparateurs ;
   formes dessinées en icônes du jeu (`createLucideIcon`) d'après la maquette validée ; « Réduire le ruban » et Ctrl+F1
   (AR-90). Il tient sur une ligne en 1366 px (mesure dans Chromium sans tête : largeur utile 1366 px sur 1366).
7. **Menus** : le menu partagé gagne les entrées radio à puce, les entrées rouges, les groupes, la pastille de couleur et
   le placement de la 1.x (240 px, lignes de 34 px) ; **écart** : une fois dessiné, un menu plus haut que ce calcul (groupes,
   libellés sur deux lignes) remonte d'autant, et défile s'il dépasse la fenêtre (défaut trouvé à la vérification visuelle :
   le menu d'une étape, 19 entrées, sortait de l'écran de 768 px).
8. **Clavier** : la couche `canvas` du gestionnaire unique porte les commandes de la carte ; Espace tenu donne la Main,
   **sauf** sur un bouton, un lien, une étape ou une flèche qui a le focus (Espace les active : accessibilité ; la 1.x prenait
   toujours la touche), relâché à la perte du focus de la fenêtre (B-2.3-07). La touche d'activation de React Flow est
   coupée, il ne supprime jamais rien lui-même (`deleteKeyCode` nul, `onBeforeDelete` refuse, B-2.3-35).
9. **Fonds de couloir** traversés par le pointeur (`pointer-events: none` sur le nœud) : rectangle et double-clic partent
   de partout (B-2.3-08).
10. **Clic sur une étape** : ouvre sa fiche au magasin (le panneau vient avec P6-T07) ; calque RACI allumé pour qui modifie
    la carte : la fiche ne s'ouvre pas, le panneau RACI vient avec P6-T06 (B-2.3-12).

## Conséquences

- Remis à plus tard, écrits dans la couverture : bouton Commande et Ctrl+K, annuler et rétablir, menu Fichier (lot 3) ;
  Remonter le détail, panneau et matrice RACI (P6-T06) ; panneau de fiche et jauge mise à jour sans attendre (P6-T07) ;
  parcours de bout en bout (glisser d'une poignée, lâcher sur le corps d'une étape, défilement au bouton du milieu, P6-T10).
- Le paquet JavaScript grossit (registre F-26).

## Révision (29/10/2026, essai du porteur) : l'ordre des zones du ruban (AR-155)

Les zones vont dans l'ordre Niveau, Formes, Branchements, Liens, Édition, Affichage, Outils (`RIBBON` de
`state/commands/mapCommands.ts`) : la zone Niveau, neuve, porte « Remonter d'un niveau » (Alt+↑, grisé sur la carte
racine, vue aussi par un lecteur) ; Sélection et Main passent au bout, à côté de l'Affichage. Un lecteur voit Niveau,
Affichage et Outils. Le premier arrêt de tabulation du ruban devient le bouton de niveau.
