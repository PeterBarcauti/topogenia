<!-- 082-niveaux-d-une-proposition.md -->
<!-- The levels of a proposal of the assistant (3.0, lot 5): a map split into levels in place by the operations of the map, a process brought back to one level only as a new project, the sheets of the new steps as drafts -->

# 082 - Niveaux d'une proposition

Statut : acceptée (date 30/10/2026). Prolonge les ADR 079 (propositions appliquées) et 081 (traductions). Spécification :
`TOPOGENIA_3.0_SPECIFICATION_IA.md`, sections 3.1 (points 2 et 6), 7 et 8 (critère 2). Décisions du porteur du projet
du 30/10/2026 : répartir une carte existante en niveaux, et l'inverse, ramener à un niveau un processus sur plusieurs
cartes, **toujours en proposant de l'ouvrir dans un nouveau projet**.

## Contexte

Au lot 3, « Appliquer » ne portait que sur la carte ouverte : les sous-cartes d'une proposition et les fiches des étapes
nouvelles n'étaient pas créées (F-53), et rien ne ramenait un processus à plat. Répartir en niveaux est le geste
« Détailler la sélection » de l'application (le « Créer un sous-processus » de Visio) ; ramener à plat, sur place,
passerait par la « Remontée » carte par carte, dont le reliquat (documents, usages, irritants de l'étape détaillée) ne
se range qu'à la main.

## Décision

1. **L'IA propose la forme, l'application fait les déplacements** avec ses propres opérations : aucune étape existante
   n'est réécrite par le modèle ; elle garde son identité (code, fiche, documents, irritants, RACI, traductions).
2. **La carte voulue a des niveaux** : une étape voulue porte la carte voulue de son détail (`SubMap`) et sa fiche
   voulue (`Sheet`) ; le format les lit de tout l'arbre du `.pmap`.
3. **Chaque étape existante nommée est placée** : gardée sur sa carte ; **descendue** sous des étapes nouvelles de sa
   propre carte ; **remontée** sur une carte au-dessus ; **déplacée** vers une autre carte (ou sous une étape existante,
   qu'aucune opération ne remplit sur place). Une carte voulue tient lieu de la carte ouverte, du détail d'une étape
   gardée, ou d'un détail nouveau.
4. **Mode de la proposition** : `in_place`, ou `new_project` dès qu'une étape est remontée ou déplacée (raisons
   `raised:CODE`, `moved:CODE`). Une proposition `new_project` n'est jamais appliquée (`PROPOSITION_NOUVEAU_PROJET`,
   409) ; son aperçu dit pourquoi et liste les étapes de la carte et de ses niveaux qu'elle ne reprend pas (les étapes
   détaillées qui disparaissent, et si leur fiche est perdue).
5. **Sur place, carte par carte depuis le haut** : les étapes descendues sont regroupées sous chaque étape nouvelle qui
   les contient (`GroupIntoSubMap`, qui copie leurs couloirs et refait les flèches coupées) ; puis la carte suit la
   correspondance des ADR 079 et 081, les étapes regroupées étant les étapes nouvelles ; puis chaque sous-carte : celle
   du regroupement, le détail gardé d'une étape, ou un détail nouveau (`SubMapRules.Detail`). Dans une carte faite par
   l'application, un couloir de la proposition prend la copie **de son nom** (le `.pmap` veut des identifiants uniques
   dans tout le fichier). Une opération, un Ctrl+Z.
6. **Fiches (F-53)** : une étape sans fiche reçoit la fiche voulue en brouillon (`SheetWorkflow.Save`), ses sorties et
   entrées, puis ses usages de sorties d'autres étapes de sa carte (une sortie de la proposition, ou une sortie existante
   nommée comme la copie du projet la nomme, `CODE-S1`) ; une étape qui a une fiche la garde ; l'aperçu nomme le champ
   `sheet`.
7. **Ramener à un niveau** : « Ouvrir en nouveau projet » envoie la carte d'origine (`origin_map_id`) ; l'hôte relit
   cette carte et ses niveaux, fiches comprises (telles que l'animateur les voit), et **reporte** dans la proposition,
   pour chaque étape nommée par un code d'origine, sa fiche (si la proposition n'en donne pas) et ses traductions (si son
   libellé est celui d'origine) ; un usage dont la sortie n'est plus sur sa carte est laissé de côté. La carte d'origine
   n'est pas touchée. Un animateur qui n'est pas administrateur télécharge le `.pmap`.
8. **Consigne** : répartir = garder chaque étape avec son code et placer celles d'une partie dans la `sub_map` d'une
   étape nouvelle de la même carte, couloirs nommés comme ceux d'origine ; ramener = une carte à plat avec les codes
   d'origine, sans réécrire les fiches ; le message dit qu'une telle proposition s'ouvre en nouveau projet.

## Conséquences

- Pas de migration. Le paquet du cœur fermé est refait (le domaine a changé) ; `ProposalPlan` tient en quatre fichiers
  (`.cs`, `.Matching.cs`, `.Levels.cs`, `.Sheets.cs`).
- Depuis la carte racine, la portée est le projet : les sous-cartes données d'étapes gardées suivent la correspondance,
  traductions comprises (la limite relevée à l'essai du correctif des traductions est levée pour les sous-cartes).
- Restent : la traduction du nom du projet par une proposition ; une étape existante placée sous une autre étape
  existante (nouveau projet, faute d'opération qui le fasse sur place).
- Tests : domaine (`ProposalLevelsTests`), format (`PmapProposalTests`), hôte sur les deux bases
  (`AssistantLevelsTests`), interface (`ProposalPreview.test.tsx`), parcours « répartir, Ctrl+Z, Ctrl+Y, ramener, ouvrir en
  nouveau projet » sur le faux connecteur.
