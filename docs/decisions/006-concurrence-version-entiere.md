<!-- 006-concurrence-version-entiere.md -->
<!-- D06 : concurrence optimiste par version entière, If-Match obligatoire en mode Serveur -->

# 006 - Concurrence par version entière

Statut : acceptée (date 23/10/2026)

## Contexte

La 1.x s'appuie sur `rowversion`, qui n'existe pas en SQLite, et `If-Match` y est facultatif
(faiblesse 3 de la spec 5.6). Le profil utilisateur n'est pas versionné (faiblesse 3 de
l'annexe A).

## Décision

- Toute entité modifiable porte une colonne `version int`, incrémentée par l'application et
  déclarée jeton de concurrence EF Core. Le profil est versionné comme les autres (AR-07).
- `If-Match` est obligatoire en mode Serveur sur toute modification d'un objet versionné :
  absent, la requête reçoit 428 avec le code `VERSION_REQUISE` (messages FR et EN, AR-07).
  Il est facultatif en mode Poste.
- Toute modification bouge la version, assignation d'une étape comprise (AR-32).
- Les opérations de structure d'un même projet sont sérialisées par
  `UPDATE project SET structure_version = structure_version + 1` dans la transaction
  (spec 4.7), sans indication de verrou propre à un moteur.

## Conséquences

- Comportement identique sur les deux bases.
- Le client généré (ADR 009) envoie `If-Match` par un intercepteur ; le journal des
  opérations (ADR 005) s'appuie sur les mêmes versions.
- Un conflit renvoie un code stable traduit, jamais une erreur 500.

## Vérification

- Tests de concurrence paramétrés SQLite / SQL Server dans `tests/Topogenia.Persistence.Tests/`
  (P3-T07) et tests 428 `VERSION_REQUISE` dans `tests/Topogenia.Host.Tests/` (P4-T06).
- Portes de contrôle Données et portabilité et Sécurité.
