<!-- 017-tests-d-architecture.md -->
<!-- C4 : tests d'architecture par ArchUnitNET 0.13.4 et par analyse du code source -->

# 017 - Tests d'architecture

Statut : acceptée (date 23/10/2026)

## Contexte

La spec (D03) demande de vérifier les dépendances par des tests, avec NetArchTest ou
ArchUnitNET. La règle de maintenance de la spec 0.2 exige une publication dans les 12
derniers mois. NetArchTest.Rules (dernière version 1.3.2) n'a pas été publié depuis plus de
12 mois. ArchUnitNET (`TngTech.ArchUnitNET.xUnitV3`) 0.13.4 a été publié le 20/08/2026, sous
licence Apache-2.0.

## Décision

- `TngTech.ArchUnitNET.xUnitV3` 0.13.4 pour les dépendances entre couches et les types
  interdits dans Domain et Application, vérifiés sur les assemblages compilés.
- Analyse du code source (`Microsoft.CodeAnalysis.CSharp`, fichiers du dépôt) pour les règles
  que l'assemblage ne voit pas : SQL brut hors fournisseurs, tailles de fichiers (600) et de
  méthodes (80), absence de `TODO`, en-têtes et format des fichiers, nom du produit unique.
- NetArchTest.Rules est écarté.

## Conséquences

- Les deux paquets sont inscrits dans `Directory.Packages.props` (versions figées) et dans
  `THIRD-PARTY-NOTICES.md`.
- Les règles se lisent en anglais dans les noms de tests et échouent avec la liste des
  fichiers fautifs.

## Vérification

- `tests/Topogenia.Architecture.Tests/DependencyRulesTests.cs`,
  `tests/Topogenia.Architecture.Tests/SourceRulesTests.cs`,
  `tests/Topogenia.Architecture.Tests/FileFormatRulesTests.cs`.
- Porte de contrôle Architecture à chaque phase.

## Note sur les dépendances transitives

ArchUnitNET tire `Mono.Cecil` 0.11.6 (02/10/2024) et `CycleDetection` 2.0.0 (2019), publiés
il y a plus de 12 mois. Ils ne servent qu'aux tests et ne sont jamais livrés ; la règle de
maintenance de la spec 0.2 vise d'abord ce qui est livré. Le choix reste plus maintenu que
NetArchTest.Rules, dont le paquet principal lui-même n'est plus publié. Chaque détecteur
écrit à la main est prouvé sur des cas fautifs (`DetectorSamplesTests.cs`).


## Règles d'analyse désactivées

Seules deux règles d'analyse sont coupées dans `.editorconfig`, et un test le vérifie
(`QualityRulesTests.Editorconfig_turns_off_only_documented_rules`) :

- `CA2007` (`ConfigureAwait`) : sans objet dans ASP.NET Core et les tests, qui n'ont pas de
  contexte de synchronisation ;
- `CA1707` (soulignés dans les noms) : les noms de tests décrivent la règle en phrase avec
  des soulignés, comme ceux de la 1.x.

`CS1591` (commentaire XML manquant) est le seul avertissement retiré globalement
(`Directory.Build.props`) : la documentation XML est générée pour activer `IDE0005`, pas
pour imposer un commentaire sur chaque membre public.

## Fichiers de règles

`DependencyRulesTests` (dépendances, types interdits, liste blanche des types du framework permis au domaine sur les symboles résolus et second filet sur l'assemblage compilé du domaine, liste blanche des paquets de la persistance commune, points d'entrée), `SourceRulesTests` (SQL,
déclencheurs, procédures et fonctions, tailles, marqueurs, nom du produit, dates, adresses, dispense des fichiers écrits par un outil),
`ProbeRulesTests` (sondes de test des fournisseurs : un seul type par fichier `*Probe.cs`, nommé comme lui, classe
statique interne, ami unique, aucun usage hors des sondes ; ADR 037), `ExtraRulesTests` (scripts SQL, vues,
points d'entrée, cas de test TypeScript, configuration), `QualityRulesTests` (version unique,
réglages non redéfinis, avertissements, règles ESLint, en-têtes, identifiants anglais),
`ConfigurationRulesTests` (fichiers de la solution, versions figées, licence),
`DecisionRulesTests` (D07, D08, D12, D13, noms techniques figés), `GuardWritingRulesTests` (écriture des garde-fous par chaque fournisseur, accord des deux), `FileFormatRulesTests` (en-têtes, UTF-8, emoji,
CRLF), `DomainModelRulesTests` (enregistrements versionnés, `Stamp` et `Commit` dans
`Workspace/`, versions écrites à la main, clés construites seulement dans `Model/`,
`Versioning.Update` réservé aux règles des utilisateurs, compteurs jamais rejoués, dossiers du
domaine sans cycle et dans l'ordre des couches), `SingleImplementationRules` (règles à implémentation unique et membres du modèle
lus seulement par leur implémentation, ARC-M3 à ARC4-M3, ARC4-M1), et les preuves des détecteurs : `DetectorSamplesTests` avec la sonde
`Probes/UsesSystemData.cs`, `SemanticSamplesTests` (détecteurs sur symboles résolus :
horloge, identifiants, `Commit`, `Stamp`, versions, clés, `Versioning.Update`, graphe et ordre
des dossiers, liste blanche du domaine) et `SingleImplementationSamplesTests` (contournements de chaque règle unique). Les
détecteurs eux-mêmes sont dans `SourceDetectors`, `SemanticDetectors`,
`SingleImplementationDetectors`, `SingleReadDetectors`, `DomainFrameworkDetectors`, sur les compilations de `SemanticSources` et les fichiers de
`RepositoryFiles`.

## Mots clés coupés et paquets de la persistance (porte P3, round 3)

- La règle `No_trigger_procedure_or_function_anywhere_including_migrations` lit les littéraux C# de chaque instruction
  joints par une espace, puis bout à bout (`"CREATE TRIG" + "GER"`), un littéral placé dans un trou d'une chaîne
  interpolée compris ; dans le texte obtenu, comme dans un script `.sql`, deux littéraux SQL joints par `+` ou `||`
  sont lus comme un seul (`EXEC(N'CRE' + N'ATE FUNCTION ...')`, guillemets doublés d'une instruction dynamique dans une
  autre compris), après les commentaires SQL. Limite : ce qu'une variable porte d'une instruction à l'autre (C# ou
  `DECLARE @s` en SQL) échappe au texte ; le catalogue de la base le prouve à l'exécution (D04, `DatabaseShapeTests`).
- `Topogenia.Persistence` n'admet qu'une liste blanche de paquets, dans le projet, dans le fichier de verrouillage
  (paquets transitifs compris) et parmi les assemblages référencés : EF Core, sa couche relationnelle, les bibliothèques
  `Microsoft.Extensions.*` qu'elles tirent, les deux projets du dessous ; un assemblage référencé peut aussi être un
  assemblage du framework partagé de .NET (`System.Data.Common`, jamais un pilote). Tout fournisseur ou pilote, quel que
  soit son nom (`Npgsql`, `Pomelo`, `Oracle`, `MySql`, `System.Data.SQLite`...), est refusé ; un paquet nouveau d'EF Core
  s'ajoute à la liste à dessein (`DependencyRulesTests.IsAllowedPersistencePackage`).
