<!-- 018-tests-xunit-v3-et-matrice-de-bases.md -->
<!-- C5 : tests C# en xUnit v3 sur Microsoft.Testing.Platform, matrice SQLite / SQL Server -->

# 018 - xUnit v3 et matrice de bases

Statut : acceptée (date 23/10/2026)

## Contexte

La spec 11 exige que les tests de persistance, d'intégration HTTP et d'échange passent sur
SQLite **et** SQL Server, et le prompt 5 réserve à Topogenia les bases `Topogenia_Test_*`. Le
SDK .NET 10 propose Microsoft.Testing.Platform comme exécuteur de tests.

## Décision

- Tests C# en xUnit v3 (`xunit.v3` 4.0.2), exécutés par Microsoft.Testing.Platform, déclaré
  dans `global.json` (`"test": { "runner": "Microsoft.Testing.Platform" }`).
- Matrice de bases par théories paramétrées (`Sqlite`, `SqlServer`) : la même méthode de test
  tourne sur les deux fournisseurs.
- Une base SQL Server `Topogenia_Test_<n>` est recréée à chaque exécution ; SQLite utilise un
  fichier temporaire. Aucune autre base n'est touchée (prompt 4-2).

## Conséquences

- Un test ne peut pas passer sur une seule base sans que cela se voie.
- L'intégration continue a besoin d'un SQL Server (service de la CI, P1-T05).
- Les tests Domain et Application restent sans base.

## Vérification

- `global.json` et `Directory.Packages.props` relus par la porte de contrôle Architecture.
- Comptes de tests par fournisseur consignés dans `docs/journal.md` à chaque porte ; porte de
  contrôle Intégrité des tests (aucun cas ignoré sur une base).
