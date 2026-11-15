<!-- 008-dates-utc.md -->
<!-- D08 : dates en DateTime UTC partout, jamais DateTimeOffset -->

# 008 - Dates en UTC

Statut : acceptée (date 23/10/2026)

## Contexte

Le fournisseur EF Core SQLite traduit mal `DateTimeOffset` dans les comparaisons et les tris
(stockage en texte). Les dates doivent se comparer de la même façon sur les deux bases et
dans les paquets échangés.

## Décision

- Toutes les dates sont des `DateTime` en UTC (`DateTimeKind.Utc`), jamais `DateTimeOffset`.
- L'heure vient du port `IClock` (spec 3.4) ; le Domaine n'appelle pas `DateTime.Now`.
- La conversion en heure locale et le format « il y a… » sont faits à l'affichage (hôte et
  interface).

## Conséquences

- Tris et comparaisons identiques sur SQLite et SQL Server.
- Les tests fixent l'heure par une horloge factice.
- Les paquets `.topogenia` portent des dates UTC, sans ambiguïté de fuseau.

## Vérification

- Tests paramétrés sur les deux bases dans `tests/Topogenia.Persistence.Tests/` (tris et
  comparaisons de dates, P3-T07).
- Porte de contrôle Architecture : aucune occurrence de `DateTimeOffset` ni de `DateTime.Now`
  dans `src/`.

## Test

`SourceRulesTests.Production_code_uses_utc_dates_only` (aucun `DateTimeOffset`, `DateTime.Now`, `DateTime.Today`, `GetUtcNow`, `GetLocalNow` dans `src`), prouvé par `DetectorSamplesTests.Local_dates_are_detected`.
