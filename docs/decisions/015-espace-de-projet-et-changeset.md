<!-- 015-espace-de-projet-et-changeset.md -->
<!-- C1 et C2 : espace de projet en mémoire, ChangeSet calculé par traceur, annulation par l'inverse -->

# 015 - Espace de projet et `ChangeSet`

Statut : acceptée (date 23/10/2026)

## Contexte

Le journal des opérations (ADR 005) a besoin, pour chaque commande de structure, de l'aller
et de l'inverse des changements. Écrire ce calcul opération par opération reproduirait la
dispersion de la 1.x. Les volumes sont faibles (5 000 étapes au plus par projet), la cible
NF05 est de 300 ms en Poste pour une carte de 150 étapes.

## Décision

- C1 : les commandes de structure travaillent sur un espace de projet (`ProjectWorkspace`)
  chargé en mémoire : cartes, couloirs, étapes, flèches, RACI, remontées, et pour les
  opérations qui en ont besoin documents, usages, irritants, fiches. Le domaine applique la
  règle sur cet espace ; un traceur d'instantanés compare l'état avant et après et produit le
  `ChangeSet` : trois sortes de changements, créations, modifications (l'enregistrement
  entier avant et après ; une suppression logique est une modification de `DeletedAt` et
  `DeletionBatchId`) et suppressions physiques, rangées dans cet ordre, puis les
  enregistrements seulement touchés (`Touch` : version avancée sans changement de contenu,
  pour le contrôle `If-Match`). La spec 4.6 parle de modifications « champ
  par champ » : le `ChangeSet` garde les deux enregistrements immuables entiers, dont la
  différence champ par champ se déduit sans perte (la comparaison ignore la version et
  l'audit de mise à jour) ; l'inverse remet l'enregistrement d'avant entier, avec la version
  courante. Le journal (phase 4) peut sérialiser les seuls champs changés sans changer ce
  principe.
- Les compteurs de codes lisibles (`ProjectCounter`) figurent dans le `ChangeSet` mais ne
  sont jamais rejoués par l'inverse ni par le rétablissement (D07 : un code n'est jamais
  redonné).
- C2 : l'annulation applique l'inverse du `ChangeSet` sur l'espace rechargé, après contrôle
  des objets touchés (`ChangeSet.Touched` : chacun doit avoir encore le contenu que
  l'opération lui a laissé, version et audit de mise à jour mis à part, et un enregistrement
  supprimé physiquement doit être toujours absent ; sinon `OPERATION_DEPASSEE`). La version
  n'est pas comparée (AR-81) : l'annulation de l'opération suivante remet le contenu attendu
  avec une version neuve, et la pile entière doit s'annuler ; une écriture concurrente qui
  change le contenu est toujours refusée. Puis l'annulation répare comme la restauration 1.x d'un lot (annexe A 2.15 :
  couloirs renumérotés, intruse décalée, flèches qui ne peuvent pas revenir laissées
  supprimées, lot vidé marqué restauré), puis contrôle des invariants de la structure
  (AR-04, AR-68). Le rétablissement fait de même avec les objets touchés par l'annulation.
  Tout le protocole est dans `Structure/UndoReplay` (`Undo`, `Redo`) : l'Application ne peut
  pas rejouer sans le contrôle (AR-79). Un refus laisse l'espace modifié : l'Application le
  jette (AR-69).

## Conséquences

- Un seul calcul de changements pour toutes les opérations ; le journal et l'annulation en
  découlent sans code par opération.
- Le chargement de l'espace se fait dans une transaction cohérente, sous le verrou de projet
  (spec 4.7).
- Si un projet dépasse les volumes visés, le chargement pourra se restreindre à la carte
  concernée sans changer le principe.

## Vérification

- Tests du traceur et du `ChangeSet` (aller, inverse, objets touchés) dans
  `tests/Topogenia.Domain.Tests/` (P2-T05) ; pile annulée puis rétablie sur les mêmes objets
  et écriture concurrente refusée : `Structure/UndoStackTests` (AR-81).
- Tests d'`undo` après chaque type d'opération (P4-T03) ; mesure NF05 consignée dans
  `docs/journal.md`.
