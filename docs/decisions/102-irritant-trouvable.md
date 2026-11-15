<!-- 102-irritant-trouvable.md -->
<!-- The pain point made findable (3.2, F-118): its field always offered under a ticked input; in the outputs of the producer, a link by step that receives the document, and whose sheet the person may fill, opens that sheet on that field, its inputs unfolded for that opening; the helps say the notion first: a pain point is noted where it is suffered; interface only -->

# 102 - L'irritant trouvable

Statut : acceptée le 11/11/2026 (3.2), après l'essai du porteur du projet du 09/11/2026 (F-118) : il a cherché l'irritant
dans les sorties de l'étape qui produit le document, et ne l'a pas trouvé. **Révisée le 12/11/2026**, avant toute
livraison, après une relecture critique : la fiche d'arrivée s'ouvre sur son formulaire, aucun lien ne mène à une
étape qui n'a plus de fiche, chaque lien porte le nom de son document (point 2) ; la demande de curseur est oubliée
dès que la fiche est affichée (point 3). **Révisée le 12/11/2026 (audit)** : lien seulement vers une fiche que la
personne peut remplir, « Entrées » dépliée pour cette ouverture seulement (point 2, « Conséquences »).

## Contexte

Un irritant est ce qui empêche d'utiliser un document tel quel. Il se saisit dans la fiche de l'étape qui **reçoit** le
document (son usage, dans les entrées), et s'affiche sur l'étape qui le **produit**. Deux choses le cachaient :

- son champ ne paraissait que si la part « Utilisable tel quel » était sous 100 %, ou si un irritant existait déjà ;
- depuis l'étape qui produit, rien ne menait à l'endroit où l'écrire : on y voyait les irritants signalés, pas le moyen
  d'en signaler un.

## Décision

1. **Le champ « Irritant » est toujours là** sous chaque entrée cochée, quelle que soit la part utilisable. Le temps
   perdu et le plan d'actions ne paraissent qu'avec un texte d'irritant, comme avant.
2. **Dans les sorties**, sous chaque document utilisé, un lien par étape de la carte qui le reçoit : « Signaler un
   irritant dans CODE ». Il ouvre la fiche de cette étape sur son formulaire, même si elle ne contient que du texte
   libre (qui reste à un clic), section « Entrées » dépliée même si elle était repliée, le curseur dans le champ
   « Irritant » de ce document. Aucun lien pour une étape d'une autre carte (la carte ne peut pas l'ouvrir), ni pour
   une étape qui n'a plus de fiche (devenue un événement ou une passerelle qui joint : elle reste nommée dans
   « utilisé par », mais son usage ne peut plus s'écrire), ni pour une sortie que personne n'utilise (personne ne la
   subit). Le nom accessible de chaque lien finit par le nom de son document : deux sorties reçues par la même étape
   ont deux liens distincts pour un lecteur d'écran.
   Révision du 12/11/2026 (audit) : le lien n'est offert que vers une étape dont la personne peut remplir la fiche
   (`step.sheet.can_fill` de la vue de carte) ; vers une autre, il menait à une fiche en lecture où l'irritant ne peut
   pas s'écrire, sans rien dire. L'aide des sorties, qui dit où se note un irritant, reste. Et la section « Entrées »
   n'est dépliée que pour cette ouverture : le lien n'écrit plus la préférence gardée par le navigateur, qui changeait
   le choix de la personne pour toutes les fiches suivantes (AR-91) ; un repli à la main pendant cette ouverture replie.
3. **La demande de curseur ne vaut qu'une fois** : elle est gardée au-dessus de la fiche (le volet de la carte), donnée
   à la fiche qui s'ouvre, oubliée dès que la fiche est affichée, que le champ ait pris le curseur ou non (le document
   n'y est pas utilisé, la fiche est en lecture), et à la fermeture d'une fiche qui n'a pas pu être lue.
4. **Les aides posent la notion d'abord** : un irritant se note là où l'on subit le problème. L'aide des entrées, celle
   des sorties et celle du champ le disent, dans les 18 langues ; le manuel a une section « Où noter un irritant ».
5. **Interface seulement.** Le cœur n'attache pas l'irritant à la part utilisable : la condition « sous 100 % » était
   une règle d'affichage (`showsPainPoint`), retirée. Aucune route, aucune donnée, aucun droit ne change.

## Conséquences

- Qui cherche l'irritant du côté de celui qui produit trouve le chemin vers celui qui reçoit.
- Une personne qui ne peut pas remplir la fiche d'arrivée n'a pas de lien vers elle (révision du 12/11/2026, audit :
  avant, le lien l'y menait en lecture, sans champ ni message). Un lecteur n'a donc aucun lien ; un contributeur n'en a
  que vers les étapes qu'il remplit. Une vue de carte périmée (droit changé depuis sa lecture) peut montrer ou cacher un
  lien à tort jusqu'à la lecture suivante, sans effet sur les données : l'hôte refuse toujours l'écriture.
- Un document utilisable à 100 % peut porter un irritant : rien ne l'interdisait côté cœur, l'interface ne le cache plus.

## Alternatives écartées

- **Saisir l'irritant depuis les sorties de l'étape qui produit** : l'irritant appartient à un usage, donc à l'étape
  qui reçoit ; le saisir ailleurs demanderait de choisir l'étape et dédoublerait la saisie.
- **Garder la condition des 100 % et ajouter une aide** : l'essai a montré que le champ caché n'est pas cherché.
