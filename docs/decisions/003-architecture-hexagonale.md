<!-- 003-architecture-hexagonale.md -->
<!-- D03 : architecture hexagonale, dépendances vérifiées par ArchUnitNET et analyse Roslyn -->

# 003 - Architecture hexagonale

Statut : acceptée (date 23/10/2026)

## Contexte

En 1.x, les règles métier, l'accès aux données et le transport HTTP sont mêlés ; trois
fichiers concentrent l'essentiel (`ServiceStructure` 1 940 lignes, `magasin.ts`,
`EditeurCarte.tsx`). Une base interchangeable exige que le métier ne dépende d'aucune
technique.

## Décision

- Couches (spec 3.1, 3.2) : `Topogenia.Domain` (pur, aucune référence hors BCL) ←
  `Topogenia.Application` (cas d'usage, ports, ne référence que Domain) ← adaptateurs
  `Topogenia.Persistence*`, `Topogenia.Interchange`, `Topogenia.Host` (seul à connaître
  ASP.NET Core et à composer le tout).
- Aucun type d'EF Core, d'ASP.NET Core, de `System.Data` ni de `Microsoft.Data.*` dans Domain
  et Application. Persistence et Interchange ne référencent pas Host.
- Un point d'entrée HTTP lit la requête, appelle un gestionnaire, traduit le résultat ; il ne
  porte aucune règle.
- Outil : ArchUnitNET 0.13.4 pour les dépendances et les types interdits sur les assemblages
  compilés (dont les types de l'infrastructure, chargés par `Types(true)`), analyse du code
  source par Roslyn (`Microsoft.CodeAnalysis.CSharp`) pour le SQL brut, la taille des méthodes,
  le nom du produit et les dates, et par expressions régulières pour les marqueurs de travail
  et les en-têtes. Choix détaillé en ADR 017.

## Conséquences

- La base devient un détail d'adaptateur : changer ou ajouter un fournisseur ne touche ni le
  Domaine ni l'Application.
- Le Domaine et l'Application se testent sans base (dépôts en mémoire).
- Limites de taille (spec 3.2) : 600 lignes par fichier, 80 par méthode, hors code généré et
  migrations.

## Vérification

- `tests/Topogenia.Architecture.Tests/DependencyRulesTests.cs` (références entre projets,
  types interdits, règles rejouées sur les assemblages compilés).
- `tests/Topogenia.Architecture.Tests/SourceRulesTests.cs` (tailles de fichiers et de
  méthodes).
- Porte de contrôle Architecture à chaque phase.

## Entrées et sorties

Le Domaine et l'Application n'ont aucun accès direct au disque, au réseau, à la console, à
l'environnement ni aux processus (`DependencyRulesTests.Domain_and_application_use_no_infrastructure_nor_io_type`).
Les gestionnaires qui lisent ou écrivent un fichier (import, export, sauvegarde) passent par
un port (`IBackupService`, flux fournis par l'hôte), comme le prévoit la spec 3.4.
