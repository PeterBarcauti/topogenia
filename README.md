<!-- README.md -->
<!-- Presentation of the product, how to build and run it, in French then in English -->

# Topogenia

## Français

Topogenia décrit un processus avec ceux qui le font : carte en couloirs (un couloir par
acteur), étapes, décisions, branches parallèles, cartes détaillées imbriquées, fiche par étape
jusqu'aux micro-tâches, documents échangés et leur qualité (%C&A), irritants, RACI, contenus
bilingues. Ces informations servent ensuite de contexte à l'IA pour automatiser le travail
décrit.

Deux modes, un seul code :

- **Poste** : un exécutable autonome, un fichier de données SQLite, un seul utilisateur, aucune
  installation de base de données, aucune écoute réseau hors de `127.0.0.1`.
- **Serveur** : IIS, SQL Server, authentification Windows, plusieurs utilisateurs.

Un projet passe d'un mode à l'autre par un paquet d'échange `.topogenia` ; le format `.pmap`
reste le format léger, lisible et écrivable par une IA.

### Construire et tester

Le cœur du domaine (`Topogenia.Domain`) est fermé : le dépôt public ne contient pas ses
sources. Il se construit avec le paquet `Topogenia.Core`, remis avec une licence écrite
(`Peter.Barcauti@topogenia.com`) et posé dans `artifacts/core-feed` (ou le dossier donné par
la propriété `CoreFeed`). Sans ce paquet, la restauration échoue.

Prérequis : SDK .NET 10, Node 24, le paquet du cœur. SQL Server local (authentification
Windows) pour la moitié SQL Server de la matrice de tests.

```
dotnet build Topogenia.sln
dotnet test --solution Topogenia.sln
cd web && npm ci && npm run typecheck && npm run lint && npm test
```

Documentation : `docs/` (architecture, sécurité, modèle de données, formats, exploitation,
décisions d'architecture dans `docs/decisions/`).

### Licence

Apache-2.0 (`LICENSE`, `NOTICE`), sauf le cœur du domaine (`Topogenia.Domain`), sous licence
propriétaire : ses sources ne sont pas publiées et son paquet ne se réutilise qu'avec une
licence écrite. Les versions publiées de l'exécutable, qui contiennent le cœur, sont d'usage
libre et gratuit ; la licence du cœur est livrée avec elles (`licenses/CORE-LICENSE.md`).

## English

Topogenia maps a process together with the people who run it: swimlane map (one lane per
actor), steps, decisions, parallel branches, nested detailed maps, a sheet per step down to
micro-tasks, exchanged documents and their quality (%C&A), pain points, RACI, bilingual
content. This information then serves as context for AI to automate the described work.

Two modes, one code base:

- **Desktop**: a self-contained executable, a SQLite data file, a single user, no database
  installation, no network listening outside `127.0.0.1`.
- **Server**: IIS, SQL Server, Windows authentication, many users.

A project moves between modes through a `.topogenia` exchange package; the `.pmap` format
remains the light format, readable and writable by an AI.

### Build and test

The core of the domain (`Topogenia.Domain`) is closed: the public repository does not hold its
sources. It builds with the `Topogenia.Core` package, delivered with a written licence
(`Peter.Barcauti@topogenia.com`) and placed in `artifacts/core-feed` (or the folder given by
the `CoreFeed` property). Without this package, the restore fails.

Requirements: .NET 10 SDK, Node 24, the core package. A local SQL Server (Windows
authentication) for the SQL Server half of the test matrix.

```
dotnet build Topogenia.sln
dotnet test --solution Topogenia.sln
cd web && npm ci && npm run typecheck && npm run lint && npm test
```

### Licence

Apache-2.0 (`LICENSE`, `NOTICE`), except the core of the domain (`Topogenia.Domain`), under a
proprietary licence: its sources are not published and its package may be reused only under a
written licence. The published releases of the executable, which contain the core, are free to
use; the core licence ships with them (`licenses/CORE-LICENSE.md`).
