<!-- 107-ia-ecrit-les-commentaires.md -->
<!-- The assistant writes the comments of presentation (3.3, lot 3, F-130): a practice and a form of their own, the steps and the flows of the map open and of its detailed maps listed with their comment of today, each comment reviewed before it is kept, one typed by hand replaced only when the person accepts it -->

# 107 - L'assistant écrit les commentaires de présentation

Statut : acceptée le 17/11/2026 (3.3, lot 3). Prolonge les ADR 099 (commentaire de présentation) et 105 (`.pmap` 1.5).
Demande du porteur du projet du 15/11/2026 (F-130) : sa première demande à l'assistant, sur un processus créé à la
main, a été d'écrire les commentaires des étapes et des relais pour la lecture à voix haute ; l'assistant ne savait
proposer qu'un `.pmap`, qui ne les portait pas. Ce qu'il en attend : « une proposition à part, acceptée ou non, comme
pour leur traduction ».

## Décision

1. **Une pratique et une forme à part, `comments`**, sur le modèle des résumés des cartes (plan 3.1, lot R) : la tuile
   « Commentaires de présentation » du panneau et l'entrée « Écrire les commentaires de présentation (IA)… » du menu
   Fichier, pour l'animateur, sur une carte qui a des étapes.
2. **Ce que l'assistant reçoit** : la carte ouverte comme pour toute pratique, et la liste `<comments_to_write>` de ses
   étapes puis de ses flèches, chacune avec sa référence (le code de l'étape, `A > B` pour une flèche), les mots qui la
   situent, la langue de son commentaire et son commentaire du jour avec qui l'a écrit. 300 au plus.
3. **Ce qu'il rend** : la forme `comments` seule, une liste `{ step, text }` ; chaque texte est rapproché d'une étape ou
   d'une flèche de la carte ouverte par la règle de la traduction des commentaires, borné à 500 caractères ; une
   réponse qui ne nomme rien de la carte est une réponse invalide.
4. **La relecture** : chaque commentaire se garde, se corrige ou s'écarte ; celui qui remplacerait un commentaire tapé
   à la main est décoché et le dit, il n'est envoyé qu'une fois coché par la personne.
5. **L'écriture** : `POST /api/maps/{id}/presentation-comments` (animateur, tout ou rien, projet verrouillé). Chaque
   commentaire gardé devient l'original de son étape ou de sa flèche, de source « assistant » ; si le texte change, les
   traductions automatiques de l'ancien original tombent, les traductions tapées restent, comme quand une personne
   récrit l'original. Un original tapé à la main ne change que si la requête le dit accepté (`replace`). La version lue
   est vérifiée comme pour un commentaire tapé.
6. **Jamais par le `.pmap` d'une proposition** : les consignes communes le disent à l'assistant, qui renvoie vers la
   tuile quand on lui demande des commentaires dans un autre échange.

## Écarté

- **Les commentaires dans le `.pmap` de la proposition** (lecture première du plan) : l'assistant devrait réécrire la
  carte entière pour dire trois phrases, la structure pourrait bouger par mégarde, et le cœur fermé (`DesiredStep`) ne
  porte pas de commentaire.
- ~~**Les commentaires des cartes détaillées dans le même échange**~~ : écarté le 17/11, repris le 18/11 (voir la
  révision ci-dessous).
- **Une écriture sans relecture** : un commentaire est dit à voix haute devant un public ; la personne le lit d'abord.

## Révision : essai du porteur du projet, 18/11/2026

Sa demande, depuis la tuile, des commentaires « de chaque étape et des sous-étapes détaillées » n'a été servie que pour
la carte ouverte : la pratique ne listait qu'elle. Pour une personne, « cette carte » est le processus avec ses détails ;
le choix de ne prendre que la carte ouverte est retiré.

7. **La liste couvre la carte ouverte et toutes ses cartes détaillées**, carte par carte (la carte ouverte d'abord,
   puis les détails niveau par niveau), chaque carte avec ses étapes dans l'ordre de lecture puis ses flèches ; chaque
   entrée porte le nom de sa carte (`map`). Toujours 300 au plus : au-delà, les dernières cartes sont coupées entières,
   jamais les flèches d'une carte déjà listée ; on les demande alors depuis la carte détaillée. Seule exception : une
   carte ouverte qui passe seule la borne donne ses 300 premières entrées, ses étapes avant ses flèches (audit de la
   copie 65 : le code coupait la carte qui franchissait la borne). Depuis une carte
   détaillée, seule elle et ce qui est sous elle sont listés.
8. **L'assistant lit les cartes détaillées dans le schéma du projet** (le paquet de l'animateur, où leurs étapes portent
   les mêmes codes) ; la carte ouverte reste donnée entière. La consigne dit que « cette carte » comprend ses cartes
   détaillées, sauf si la demande les écarte ou nomme une seule carte. La demande par défaut (menu Fichier) et
   l'exemple de la tuile le disent dans les 18 langues.
9. **La relecture suit l'ordre de la liste**, quel que soit l'ordre de la réponse, et nomme chaque carte avant son
   premier commentaire quand la réponse en couvre plusieurs.
10. **L'écriture accepte une étape ou une flèche vivante de toute carte vivante du projet** de la carte nommée par la
    route : la relecture peut être enregistrée depuis un autre niveau que celui de la demande (le volet de l'assistant
    suit la personne d'une carte à l'autre), et le rôle d'animateur est le même sur toutes les cartes du projet. Une
    étape d'un autre projet, ou supprimée, reste `INTROUVABLE`, et refuse toute la liste.

## Conséquences

- Une forme de réponse de plus pour tous les fournisseurs, par le même schéma ; une route de plus.
- Le contexte de l'assistant lit les commentaires de la carte ouverte pour cette seule pratique.

## Vérification

- `CommentDraftRulesTests` : original de source « assistant », traductions automatiques retirées et tapées gardées,
  même texte sans écriture, commentaire tapé gardé sauf accord.
- `AssistantCommentDraftsTests` (deux bases) : liste et schéma de l'invite, lecture de la réponse (flèche écrite
  `A -> B` reconnue, code inconnu écarté), réponse vide de la carte refusée, lecteur refusé ; écriture, commentaire tapé
  gardé puis remplacé une fois accepté, conflit de version, liste vide, doublée ou hors de la carte refusée en entier.
- `CommentsReply.test.tsx` : relecture, case décochée d'un commentaire tapé, envoi corrigé, carte réaffichée.
- Témoins de mutation vus rouges : la protection du commentaire tapé (règle, route, relecture).
- Révision du 18/11 : `AssistantCommentDraftsTests.The_practice_asked_on_the_main_map_writes_for_its_detailed_maps_too…`
  (deux bases : liste de la carte principale puis de son détail avec le nom de chaque carte, liste réduite depuis le
  détail, relecture dans l'ordre de la liste, écriture sur les deux niveaux depuis la carte détaillée) ; une étape d'un
  autre projet refusée ; `CommentsReply.test.tsx` (nom de chaque carte). Témoins vus rouges : liste réduite à la carte
  ouverte, écriture limitée à la carte de la route, ordre de la réponse gardé, nom de la carte absent, flèches regroupées
  sur la première carte, nom des cartes jamais affiché.
- Audit de la copie 65 : `AssistantCommentDraftTargetsTests` (une carte détaillée qui passerait la borne laissée
  entière, la carte ouverte gardant toutes ses flèches ; des cartes qui tiennent listées entières l'une après l'autre ;
  la carte ouverte seule au-delà de la borne donnant ses premières entrées, ses étapes avant ses flèches). Témoins vus
  rouges : coupe au milieu d'une carte, carte ouverte perdue.
