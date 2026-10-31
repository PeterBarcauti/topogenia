<!-- 002-ef-core-deux-fournisseurs.md -->
<!-- D02 : accès aux données par EF Core 10 avec les fournisseurs SQL Server et SQLite -->

# 002 - EF Core 10 et deux fournisseurs

Statut : acceptée (date 23/10/2026)

## Contexte

En 1.x, le SQL propre à SQL Server est écrit dans chaque service et même dans un contrôleur
(environ 150 tournures dans 20 fichiers), avec Dapper, des procédures et des déclencheurs.
La 2.0 doit tourner sur un fichier SQLite (mode Poste) et sur SQL Server (mode Serveur) avec
le même code (objectif O1).

## Décision

- Accès aux données par Entity Framework Core 10 avec `Microsoft.EntityFrameworkCore.SqlServer`
  et `Microsoft.EntityFrameworkCore.Sqlite`. Dapper est retiré.
- Un seul modèle (`Topogenia.Persistence`), un jeu de migrations par fournisseur
  (`Topogenia.Persistence.SqlServer`, `Topogenia.Persistence.Sqlite`, spec 4.4).
- Le SQL brut (`FromSql`, `ExecuteSql`, `SqlQuery`, chaînes SQL) est interdit hors des deux
  projets de fournisseur.
- Les pièges de portabilité de la spec 4.3 sont traités explicitement (stationnement en deux
  temps, paquets de 500 identifiants, comparaisons de texte sur colonnes normalisées, dates UTC,
  traduction des erreurs par fournisseur, réglages SQLite à chaque connexion).

## Conséquences

- Les requêtes LINQ sont portables ; un troisième fournisseur (PostgreSQL, hors périmètre)
  s'ajoute sans toucher au métier.
- Ce qui ne s'exprime pas en LINQ portable reste dans le projet du fournisseur concerné
  (vues `bi` et rôles SQL Server, réglages et sauvegarde SQLite).
- Toute la suite de persistance et d'intégration tourne sur les deux bases (ADR 018).

## Vérification

- `tests/Topogenia.Architecture.Tests/SourceRulesTests.cs`
  (`No_raw_sql_outside_the_two_providers`).
- `tests/Topogenia.Persistence.Tests/` et `tests/Topogenia.Host.Tests/` en matrice SQLite /
  SQL Server (phases 3 et 4) ; porte de contrôle Données et portabilité.
