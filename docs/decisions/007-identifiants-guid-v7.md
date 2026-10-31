<!-- 007-identifiants-guid-v7.md -->
<!-- D07 : identifiants Guid v7 générés par l'application, codes lisibles immuables -->

# 007 - Identifiants Guid v7

Statut : acceptée (date 23/10/2026)

## Contexte

Les identifiants générés par la base (`NEWSEQUENTIALID()`, identité) diffèrent d'un moteur à
l'autre et obligent à relire la base après chaque insertion. Les paquets `.topogenia` et la
migration 1.x doivent pouvoir conserver ou réécrire des identifiants sans dépendre du moteur.

## Décision

- Clé primaire `Guid` générée par l'application avec `Guid.CreateVersion7()`.
- Les codes lisibles (`VTE-E001`) restent immuables et ne sont jamais réattribués, comme en
  1.x ; seuls les préfixes sont réécrits à l'import d'un paquet sous un autre code de projet
  (spec 7.3).

## Conséquences

- Le Domaine crée ses objets complets sans base ; le `ChangeSet` (ADR 015) connaît les
  identifiants avant l'écriture.
- L'ordre temporel des Guid v7 limite la fragmentation ; les volumes sont faibles (5 000
  étapes au plus par projet), la fragmentation résiduelle est négligeable.
- La migration 1.x conserve les identifiants existants (spec 7.6).

## Vérification

- Tests unitaires des entités dans `tests/Topogenia.Domain.Tests/` (P2-T04).
- Configurations EF relues par la porte de contrôle Données et portabilité : aucune
  génération de clé par la base (`ValueGeneratedNever`).
