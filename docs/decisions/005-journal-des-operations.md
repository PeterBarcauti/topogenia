<!-- 005-journal-des-operations.md -->
<!-- D05 : annuler et rétablir par un journal des opérations côté serveur -->

# 005 - Journal des opérations côté serveur

Statut : acceptée (date 23/10/2026)

## Contexte

En 1.x, l'annulation (Ctrl+Z) est orchestrée par le client en plusieurs appels : lots de
suppression, instantanés de positions, et une heuristique de ±60 secondes pour retrouver les
flèches coupées par « Détailler la sélection ». Elle n'est pas atomique, se perd au
rechargement et n'est pas sûre à plusieurs.

## Décision

- Table `operation_log` (spec 4.6) : chaque commande de structure y inscrit l'aller et
  l'inverse de son `ChangeSet`, les objets touchés et leurs versions après l'opération.
- `POST /api/maps/{id}/undo` et `/redo`, par carte et par utilisateur, appliquent l'inverse
  dans une seule transaction après contrôle des versions ; sinon `OPERATION_DEPASSEE` (409,
  auteur et heure) ou `CARTE_MODIFIEE` si l'inverse violerait un invariant (AR-04, AR-43).
- Profondeur 50 par carte et par utilisateur ; toute nouvelle opération vide la pile de
  rétablissement. Liste des opérations annulables : spec 4.6 et AR-21.
- `step.origin_operation_id` remplace l'heuristique des ±60 s.
- Les routes d'annulation pilotées par le client sont retirées (AR-08) ; le module
  d'instantanés côté client disparaît (AR-48).

## Conséquences

- L'annulation survit au rechargement et reste correcte à plusieurs utilisateurs.
- Le calcul des changements est générique (ADR 015) : aucun code d'annulation par opération.
- La purge des lots (30 jours) purge aussi les opérations qui y renvoient.

## Vérification

- Tests d'`undo` / `redo` dans `tests/Topogenia.Application.Tests/` et
  `tests/Topogenia.Host.Tests/` (phase 4, P4-T03), sur les deux bases.
- Parcours e2e « annulation après rechargement » (phase 6).
- Porte de contrôle Fidélité fonctionnelle (annexe B 2.6).
