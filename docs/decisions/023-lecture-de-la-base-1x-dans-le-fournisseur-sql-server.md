<!-- 023-lecture-de-la-base-1x-dans-le-fournisseur-sql-server.md -->
<!-- La lecture de la base 1.x pour la migration vit dans le fournisseur SQL Server, derrière un port -->

# 023 - Lecture de la base 1.x dans le fournisseur SQL Server

Statut : retirée (24/10/2026, AR-94 : pas de migration 1.x dans l'application, le flux du porteur est migré une fois hors du dépôt) ; acceptée le 23/10/2026

## Contexte

La migration des données 1.x (spec 7.6, P5-T05) lit une base SQL Server de schéma `ariane`
(version 8), qui n'a pas de modèle EF Core 2.0. Le SQL brut est interdit hors des projets de
fournisseur (D02, spec 3.2), alors que la migration est rangée dans `Topogenia.Interchange`
(spec 3.1).

## Décision

`Topogenia.Application` définit le port `ILegacySource` (lecture projet par projet des
tables 1.x, en objets simples). `Topogenia.Interchange` s'en sert pour convertir vers le
modèle 2.0. L'adaptateur qui lit la base 1.x par requêtes SQL vit dans
`Topogenia.Persistence.SqlServer` (qui référence Application par la persistance commune),
seul endroit autorisé à écrire du SQL propre à SQL Server. L'hôte compose les deux pour
`--migrate-v1`. Aucune référence nouvelle entre projets n'est nécessaire (spec 3.1, test
`Project_references_are_exactly_the_allowed_ones`).

## Conséquences

- Interchange reste sans SQL et testable avec une source en mémoire.
- La lecture du schéma `ariane` est testée sur la copie restaurée `Topogenia_Migration_Source`.

## Vérification

`SourceRulesTests.No_raw_sql_outside_the_two_providers` ; tests de migration
(`tests/Topogenia.Interchange.Tests`, phase 5).
