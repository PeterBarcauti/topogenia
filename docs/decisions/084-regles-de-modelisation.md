<!-- 084-regles-de-modelisation.md -->
<!-- The rules of modelling given to the AI (3.0): a lane is who does the work, a software lane only when it acts alone, a cadence is no decision, the main steps first and their detail below -->

# 084 - Règles de modélisation

Statut : acceptée (date 30/10/2026). Prolonge les ADR 078 (consignes de l'assistant) et 082 (niveaux). Décision du
porteur du projet du 30/10/2026, après une carte de ventes produite par l'assistant où SAP et l'outil de reporting
avaient chacun un couloir : « l'esprit de l'outil est de montrer visuellement l'enchaînement du travail et chercher à la
fois les gaspillages et l'automatisation » ; il emprunte aux cartographies de flux de valeur des services (VSDiA) et à
BPMN, dont les règles sont proches ; il manquait ces règles dans le guide donné aux IA, avec la structuration en niveaux.

## Décision

Le guide `docs/pmap/PMAP_GUIDE_IA.md` (donné tel quel à une IA extérieure, et embarqué dans les consignes de
l'assistant) et les consignes de création (`common.md`, `practice-create.md`) portent les mêmes règles :

1. **Un couloir répond à « qui fait le travail ? »** : une personne, un rôle, un service ; un participant extérieur
   (client, fournisseur) qui déclenche ou reçoit le résultat a son couloir.
2. **Un logiciel n'a son couloir que s'il agit seul** (traitement planifié, calcul, envoi ou échange automatique), ses
   étapes `automated`. Quand une personne travaille dans un logiciel, l'étape est dans le couloir de cette personne et le
   logiciel dans la fiche (`tools`, `tool` de chaque ligne du déroulé, avec sa nature : ressaisie, copier-coller...) :
   c'est là que se voient les gaspillages. Pas de couloir pour un logiciel qui ne porterait qu'un stockage ou un état
   final.
3. **Une cadence n'est pas une décision** : pas de losange « Le mois est-il terminé ? » ; la fréquence de la fiche dit
   quand une étape a lieu ; une partie qui démarre sur le calendrier a son propre début (« Fin du mois »). Une fin est
   dans le couloir de celui qui obtient le résultat.
4. **Les grandes étapes d'abord, le détail en dessous** : au-delà d'une dizaine d'étapes, la carte racine montre les
   grandes étapes (5 à 10, une par phase), chacune détaillée par sa carte ; ce qu'un seul acteur fait en plusieurs
   sous-actions reste dans le déroulé de sa fiche.

La consigne donnée à claude.ai pour écrire un processus en paquet `.topogenia` (hors dépôt,
`46_Topogenia\Test-format-topogenia\`, assemblée par `assembler.py`) porte les mêmes règles en section 2 ; son paquet
d'exemple s'importe dans la 3.0 (format du paquet inchangé). Les numéros de sections du guide ne changent pas (la consigne du croquis les cite) ; sa section 6 devient « Niveaux et
cartes détaillées ». La consigne du croquis n'est pas touchée : elle reproduit ce qui est dessiné.

## Conséquences

- Essai réel (récit fictif d'une usine de pièces, `Travail\essai-reel-creation\`) : SAP n'a de couloir que dans la carte
  détaillée de la facturation automatique de nuit ; l'outil de reporting n'est qu'un outil des fiches ; trois débuts de
  calendrier (« Chaque nuit », « Chaque jour », « Fin du mois ») au lieu d'un losange ; cinq grandes étapes sur la carte
  racine, quatre détaillées ; les ressaisies et copier-coller nommés dans les déroulés. Environ 0,56 $.
- Une carte déjà faite autrement n'est pas modifiée ; « Modifier la carte » peut la reprendre sur demande.
