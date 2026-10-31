<!-- 085-le-flux-de-valeur.md -->
<!-- The value stream (3.0, lot 7): four optional entries, two project settings, a frequency per case; volumes, workload, lead time, main path, timeline and completeness check computed by the core at every reading, never stored -->

# 085 - Le flux de valeur

Statut : acceptée (date 31/10/2026). Prolonge l'ADR 084 (règles de modélisation, empruntées aux cartographies de flux de
valeur des services, VSDiA) et les ADR 051 à 054 (formats). Décision du porteur du projet du 31/10/2026 : « Le diagramme
doit être auditable par un organisme extérieur et donc être complet tout en restant simple pour l'utilisateur. C'est
l'ADN du logiciel. Pas de saisie inutile. » Conception acceptée sur maquette (« Ça me va »), avec trois exigences :
l'icône du calque aussi discrète que les autres, des spécifications et un code blindés, la non-régression testée après
chaque modification. Spécification : section 14 de la spécification 3.0 (révision 5).

## Contexte

La carte portait déjà la durée par exécution et la fréquence de chaque fiche, l'attente avant réception et le %C&A des
documents, les étapes d'attente et les heures perdues des irritants. Manquaient, par rapport à une cartographie VSDiA :
les volumes, les effectifs, le délai d'une étape distinct de son travail et la ligne de temps (délai total contre temps
de travail). Les outils du marché qui chiffrent un processus (simulation BPMN, BPSim) posent la demande à l'événement de
début et des probabilités sur les sorties des branchements : c'est la saisie la plus courte qui détermine tous les
volumes.

## Décision

1. **Saisies** : le volume entrant d'une étape Début (quantité et période) ; la part des dossiers d'une flèche qui sort
   d'une décision ou d'un OU inclusif ; le délai d'une fiche (valeur et unité, minute, heure ou jour ouvré) ; l'effectif
   d'un couloir Personne ou Service ; la fréquence « À chaque dossier » ; la journée de travail et les heures par ETP et
   par mois du projet (7 h et 151,67 h par défaut). Toutes facultatives sauf les deux réglages, toujours présents.
   Le délai ne compte pas dans la complétude des fiches, qui ne change pas.
2. **Invariants** du cœur, contrôlés à l'écriture, après une annulation ou un rétablissement et à l'import : un volume
   seulement sur un Début, une part seulement sur une sortie de décision ou de OU inclusif, un effectif jamais sur un
   couloir Système. Changer le type d'une étape ou d'un couloir, ou inverser une flèche, retire la valeur devenue sans
   objet dans la même opération (un Ctrl+Z la rend).
3. **Calcul** (`ValueStream`, cœur fermé), à chaque lecture, jamais stocké : débit de chaque étape par les équations de
   bilan (reprises comprises, résolues exactement), jonctions ET et OU inclusif par le branchement qui les domine,
   inconnus propagés sans estimation ; exécutions, travail, délai, charge et ETP ; étape détaillée chiffrée par sa
   carte détaillée ; chemin principal (le plus probable, puis le plus long) et ligne de temps ; délai moyen reprises
   comprises ; %C&A cumulé ; heures perdues des irritants ouverts. Règles exactes : spécification 14.3.
4. **Aucun total silencieux** : un contrôle de complétude liste, avec un code, une gravité et l'objet en cause, ce qui
   manque, ce qui est incohérent et chaque règle par défaut appliquée (spécification 14.4).
5. **Affichage** : un calque « Flux de valeur » du groupe Affichage (bouton du même dessin que ses voisins, un calque à
   la fois sous les étapes avec le RACI) ; cartouches, parts sur les flèches, effectif et charge des couloirs, ligne de
   temps, bilan et contrôle ; chaque chiffre donne sa formule et ses sources au survol. Calque allumé, chaque rangée
   gagne une bande de 36 px sous ses étapes (rangées de 156 px, celles de la maquette validée) : les cartouches y sont
   centrés sous leur étape, dessinés sous les flèches, et les flèches se routent sous eux ; calque éteint, la géométrie
   est celle d'avant, au pixel près.
6. **Formats** : paquet `.topogenia` 2.1 et `.pmap` 1.2, propriétés ajoutées ; un paquet 2.0 converti à l'import
   (réglages par défaut), un `.pmap` 1.1 toujours lu ; les chiffres calculés ne sont dans aucun format.
7. **Exports** : le calque dans le PDF et le PNG quand il est allumé ; dans le classeur, une feuille « Flux de valeur »
   en nombres à pleine précision, avec l'origine de chaque valeur, et une feuille « Bilan ».
8. **Assistant** : le guide et les consignes décrivent les propriétés et interdisent d'inventer un chiffre.

## Conséquences

- Les paquets écrits par la 3.0 ne s'ouvrent plus dans la 2.0 (`PAQUET_TROP_RECENT`), la règle du format.
- Les vues `bi` portent les saisies, pas les chiffres calculés : un rapport qui les veut les recalcule ou lit
  l'export.
- Les approximations sont écrites : un bloc parallèle compte pour sa branche la plus longue, un OU inclusif pour la plus
  longue de ses branches possibles ; une étape à plusieurs sorties sans branchement est un ET (BPMN).
- Allumer le calque change la hauteur des rangées : la carte se redessine plus haute, les positions des étapes dans la
  grille ne changent pas. Le cartouche d'un branchement pend sous son libellé, plus bas que les autres : une flèche qui
  passe dans la bande peut le traverser, dessinée par-dessus, donc toujours visible.
