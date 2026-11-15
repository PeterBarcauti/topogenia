<!-- 079-propositions-appliquees-a-la-carte.md -->
<!-- A proposal of the assistant applied to the map open (3.0, lot 3): the state wanted compared with the map in the closed core, its preview marked on the map, its application by the operations of the domain in one operation undone by one Ctrl+Z, a stale preview refused -->

# 079 - Propositions appliquées à la carte

Statut : acceptée (date 30/10/2026). Prolonge l'ADR 078 (volet de l'assistant). Spécification :
`TOPOGENIA_3.0_SPECIFICATION_IA.md`, sections 3.1 (points 5 à 7), 3.4, 7 et 9 (lot 3).

## Contexte

Au lot 2, une proposition du modèle (le texte d'un `.pmap` 1.1) se créait en nouveau projet ou se téléchargeait. La
spécification veut plus : sur la carte ouverte, un aperçu des changements, « Appliquer » en une seule opération défaite
par un seul Ctrl+Z, « Préciser », et une carte changée entre l'aperçu et l'application qui refuse l'aperçu périmé.
Le calcul de l'aperçu et l'application restent dans le cœur fermé (ADR 048).

## Décision

1. **L'état voulu** : la carte racine du `.pmap` proposé est l'état voulu de la carte ouverte. Le format le lit avec ses
   règles (fautes ligne par ligne) et le rend au domaine sous sa forme (`DesiredMap` : couloirs, étapes avec leur case,
   flèches), les colonnes placées comme à l'ouverture d'un fichier quand le modèle n'en donne pas (`AutomaticColumns`).
   Un `id` qui est le code d'un couloir ou d'une étape de la carte désigne cet objet, gardé et changé ; tout autre `id`
   est un objet nouveau ; un objet de la carte que la proposition laisse de côté est supprimé.
2. **Le plan, dans le domaine** (`Topogenia.Domain.Proposals.ProposalPlan`) : couloirs ajoutés, renommés, retypés,
   supprimés ; étapes ajoutées, changées (libellé, type, automatisation, couloir, case), supprimées ; flèches ajoutées,
   supprimées, renommées, connues par leurs deux bouts. **Une étape détaillée laissée de côté est gardée avec sa carte**,
   et son couloir avec elle : supprimer une carte n'est jamais l'effet de bord d'une proposition (avertissements
   `step_kept`, `lane_kept`).
3. **L'application par les opérations du domaine**, chacune avec ses règles (textes, cases, flèches d'une même carte,
   traductions oubliées d'un libellé changé), dans un ordre qui ne heurte aucune case : flèches et étapes laissées de côté
   supprimées, couloirs ajoutés et changés, étapes gardées qui changent de case **mises de côté au-delà de toute colonne
   employée puis posées à leur case**, étapes changées, étapes nouvelles, flèches ajoutées et renommées, couloirs vidés
   supprimés, ordre des couloirs. L'espace de travail compare son état final à celui du chargement : les états
   intermédiaires ne comptent pas, et toute règle refusée refuse la proposition entière.
4. **Une commande, une opération** : `ApplyProposalCommand` (animateur), journalisée sous `proposition_ia` (« Annulé :
   proposition de l'IA ») : un seul Ctrl+Z la défait par le rejeu du journal (ADR 042). L'aperçu
   (`PreviewProposalQuery`, animateur) rend la version de structure du projet qu'il a lue ; l'application la reçoit en
   `If-Match` et la compare à la sienne, avancée d'un cran par son verrou (AR-111) : **tout changement du projet entre les
   deux refuse l'aperçu périmé** (`CONFLIT_VERSION`, spec 3.1-7).
5. **Routes** : `POST /api/maps/{id}/assistant/preview` (le `.pmap` proposé -> les changements, leurs avertissements, la
   version lue) et `POST /api/maps/{id}/assistant/apply` (le même `.pmap`, `If-Match`) -> la carte, comme toute écriture
   de la carte.
6. **Interface** : sur une carte, la carte Proposition offre d'abord « Aperçu sur la carte » (la liste des changements ;
   les étapes changées et supprimées marquées sur la carte tant que l'aperçu est ouvert), puis « Appliquer » (une écriture
   de l'éditeur ; « Ctrl+Z la défait d'un coup »), « Préciser » (le champ prêt pour la suite de l'échange), « Écarter » ;
   « Ouvrir en nouveau projet » et « Télécharger le .pmap » restent à portée.

## Conséquences

- Pas de migration : le journal des opérations ne contraint pas son type.
- Le paquet du cœur fermé est refait (le domaine a changé) avant le paquet de l'application.
- « Proposer la correction » d'un constat d'analyse (animateur) envoie au modèle une demande de modification qui nomme
  le constat et ses objets : la proposition qui revient suit le même aperçu et la même application.
- Restent pour plus tard (`docs/a-peaufiner.md`, F-53) : les fiches des étapes nouvelles en brouillon et les cartes
  détaillées proposées (le plan porte sur la carte ouverte).
