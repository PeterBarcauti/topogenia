<!-- journal.md -->
<!-- Compte rendu de chaque phase : fait, écarts, décisions, chiffres des tests, rounds de contrôle -->

# Journal de réalisation de Topogenia 2.0

## Phase 0 - Lecture et cadrage

Démarrée le 23/10/2026.

### Prérequis vérifiés (P0-T04)

| Prérequis | Constat |
|---|---|
| .NET SDK | 10.0.402 (et 8.0.403, 9.0.304) ; runtimes ASP.NET Core et .NET 10.0.13 |
| Node / npm | 24.5.0 / 11.5.1 |
| `sqlcmd` | ODBC 17 (`Client SDK\ODBC\170\Tools\Binn`) |
| SQL Server local | Instance `.`, authentification Windows, compte du poste membre de `sysadmin` et `dbcreator` ; aucune base `Topogenia_*` existante |
| Bases interdites présentes | `Ariane`, `Ariane_Test`, `Ariane_E2E`, `ARIANE_ESSAI`, `CARTO`, `CARTO_TEST`, `CARTO_E2E` (jamais ouvertes en écriture) |
| Sauvegarde pour la migration | `Ariane_avant_v7.bak` (21/10/2026, la plus récente) dans `MSSQL16.MSSQLSERVER\MSSQL\Backup`. Son en-tête indique la base d'origine `Ariane` : voir `questions.md` Q-03 |
| Navigateurs Playwright | `chromium-1248`, `chromium_headless_shell-1248`, `ffmpeg-1013` déjà installés (Playwright 1.64.1 de la 1.x) |
| Dépôts de paquets | nuget.org et registry.npmjs.org joignables (téléchargement de paquets du projet seulement) |
| Outils Windows SDK | `makeappx.exe` (10.0.19041.0) et `signtool.exe` présents : MSIX possible, signature sans certificat impossible |

Note de suivi (non une question, prompt 4-4) : Bitdefender peut mettre en quarantaine ce qui
pilote un navigateur ou fabrique des paquets ; tout blocage constaté sera noté ici avec le
fichier concerné.

### Porte de la phase 0

| Round | Contrôleur | Bloquants | Majeurs | Mineurs | Suite |
|---|---|---|---|---|---|
| 1 | Phase 0 (agent neuf, Opus, effort élevé) | 1 | 17 | 19 | Plan : tâches P6 rattachées à B-7 et B-2.13, colonne « Lignes visées » en phases 3 à 7, A-2.7 dans P2-T07, C4 passé à ArchUnitNET (D03). Couverture : 23 corrections de statut alignées sur les arbitrages. Arbitrages : AR-24 corrigé (3 pointes), AR-21 et AR-10 précisés, références au code 1.x ajoutées. Questions : Q-08 retirée (note de suivi), AR-02 et AR-29 retirés (tranchés par la spec). |
| 2 | Phase 0 (agent neuf) | 2 | 21 | 31 | Plan : rattachement généré de chaque sous-section aux phases, « Lignes visées » en phase 1, portes 6 et 7 limitées aux contrôleurs du prompt 7.1. Arbitrages : colonne « Options » (prompt 6.4), AR-41 inversé (grille de points conservée), AR-03, 05, 12, 24, 28, 32, 33, 36, 38, 39 précisés, AR-43 à AR-49 ajoutés. Couverture et inventaire des tests alignés (codes de refus d'undo, glisser groupé, teintes, santé par mode, contraste du code de couloir). Questions renumérotées Q-01 à Q-06, défauts du prompt retirés. |
| 3 | Phase 0 (agent neuf) | 1 | 3 | 18 | Profil versionné (D06, faiblesse 3) : AR-07, A-1.2-06, S-3.6-09 corrigés. AR-11 (point ambigu A-9), B-6-38 (30 px conservés), T-WHIS-06 (pile entièrement vidée comme en 1.x) alignés. Mineurs : rattachement regénéré avec les lignes à `\|`, AR-10, 12, 24, 25, 32, 44, 45, 49 précisés, S-4.1-09 (`last_project_id`), totaux S, renvois, questions Q-02 et Q-06 reformulées. |
| 4 | Phase 0 (agent neuf) | 0 | 7 | 62 | Majeurs corrigés : réponse des opérations de structure (`id`, `code`, `ids` gardés, AR-50), conditions « modifiable » du menu Étape et du menu Sélection, jetons distincts pour les pastilles d'initiales et les en-têtes de tableau, statuts d'annulation des couloirs et de la commande alignés sur AR-21, langue de repli française (AR-51), limite de 4 000 caractères de `actor`, Q-06 retirée (lecture de la spec). Ajout AR-52 (police embarquée dans le PDF, signalé hors grille). |

**Porte de la phase 0 : franchie au 4e round** (prompt 7.2-5) : aucun bloquant ; les 7 majeurs
du dernier round sont corrigés sans nouveau round, faute de round autorisé. Les 62 mineurs
du round 4 restent à reprendre au fil des phases, quand la ligne de couverture concernée
reçoit son test (prompt 7.2-6) : renvois d'arbitrage non cités dans une trentaine de lignes,
une dizaine de numéros de ligne 1.x approximatifs, quelques codes ou valeurs à préciser
(`OPERATION_DEPASSEE` et `PROJET_SANS_ANIMATEUR` sans ligne de message propre, versions de
paquets de B-0), formulations imprécises (A-2.7, A-2.9-15, B-2.5-04, B-2.9-24), et des
rattachements au plan qui passent par la règle par défaut.

Accord du porteur du projet consigné : le 23/10/2026, après la vérification des prérequis, le porteur du projet a
répondu « go et ne t'arrête pas » à la proposition de migrer une copie restaurée de la base
`Ariane` avec un rapport anonymisé (questions.md Q-03).

### Bilan de la phase 0

- Livrables : `plan.md` (8 phases, 60 tâches, rattachement de chaque sous-section),
  `couverture.md` (2 073 lignes : A 787, B 964, S 322), `portage-tests.md` (484 tests 1.x :
  334 cas C#, 176 vitest, 49 parcours e2e), `questions.md` (5 questions), 52 arbitrages.
- Aucun code. Aucun test.

## Phase 1 - Squelette, règles, charte, maquette

Fait : solution (7 projets, 6 projets de tests, xUnit v3 sur Microsoft.Testing.Platform),
`Directory.Build.props` (avertissements en erreurs, analyseurs, nom du produit lu dans la
constante D13), `Directory.Packages.props` (versions figées, fichiers de verrouillage), tests
d'architecture (ArchUnitNET et détecteurs Roslyn éprouvés sur des cas fautifs), ADR 001 à 024,
CI écrite (jamais poussée), web (Vite 8, TypeScript 6.0.3 strict, ESLint, vitest), jetons de
la charte avec teintes générées, maquette statique autonome et captures, README, SECURITY,
CONTRIBUTING, mentions tierces générées.

| Round | Contrôleurs | Bloquants | Majeurs | Mineurs | Suite |
|---|---|---|---|---|---|
| 1 | Architecture ; Interface et charte (agents neufs) | 2 + 5 | 12 + 12 | 15 + 12 | Architecture : `dotnet test` en code 8 (projets vides : `--ignore-exit-code 8` posé jusqu'à leurs premiers tests), règle « point d'entrée sans règle » ajoutée, détecteurs réécrits (SQL brut, déclencheurs, marqueurs, nom du produit, emoji, dates locales, adresses externes, lambdas et instructions de niveau supérieur) et prouvés sur des cas fautifs et des cas corrects, exclusions resserrées, `.gitattributes`, SqlClient 7.1.1 (fin de la chaîne Azure), ADR 001, 004, 017, 020 corrigées, ADR 023 et 024, CI (CodeQL v4, attente SQL Server, audit JSON), `samples/`, `build/`, générateur des mentions tierces. Interface : AR-53 à AR-57 (contrastes, focus, flèches, texte vert/orange, losanges), menu des types direct, fil d'Ariane, boucle et entrée de losange, notifications, hors cadre, aucune valeur en dur, vérification automatique des contrastes de la maquette (1000 éléments, minimum 4,60:1), test des jetons renforcé. |
| 2 | Architecture ; Interface et charte (agents neufs) | 1 + 0 | 7 + 7 | 13 + 7 | Architecture : règle des types interdits rendue effective (`Types(true)`) et prouvée par une sonde `System.Data`, détecteurs étendus (SQL en corps d'expression, paramètres, minuscules qualifiées ; déclencheurs temporaires ; vues hors `bi` ; `GetUtcNow`, `GetLocalNow` ; adresses `//`, `wss://` ; propriétés et initialiseurs longs ; marqueurs supplémentaires ; emoji supplémentaires), scripts `.sql` limités aux fournisseurs, points d'entrée limités au dossier `Endpoints`, longueur des cas de test TypeScript, interdiction de `--ignore-exit-code 8` dès qu'un projet a des tests, ADR 003, 019, 022, 023 (port dans Application) corrigées, CI (attente SQL réelle, contrôle de dérive des mentions tierces, secret documenté). Interface : AR-58 (libellé de losange décalé), AR-59 (jetons graphiques à 3:1), document porté par la boucle, ARIA des menus, tabindex itinérant, pilules et attente, contrôle automatique des pseudo-éléments, du texte SVG et de 176 objets graphiques, test des jetons étendu (117 cas après retrait du cas conditionnel). |
| 3 | Architecture ; Interface et charte (agents neufs) | 1 + 0 | 11 + 7 | 12 + 13 | Architecture : version unique (props et `package.json`), aucun projet ne redéfinit les réglages communs, aucune suppression d'avertissement, seules `CA1707` et `CA2007` coupées (documentées), règle des types interdits réutilisée telle quelle par la sonde et étendue aux entrées/sorties (`System.IO`, `System.Net`, console, environnement, processus), SQL du modèle EF interdit hors fournisseurs (ADR 025), SQL en minuscules avec fonctions et alias, `NotImplementedException` et « à faire » accentué, identifiants français (glossaire) refusés en C# et TypeScript, points d'entrée de tout style (contrôleurs, `Map`) dans `Endpoints`, espaces de noms alignés sur les dossiers (IDE0130), règles de code mort (IDE0051, IDE0052, IDE0060, CA1823), aucun `eslint-disable` ni `@ts-ignore`, limites ESLint vérifiées et appliquées aux tests, en-têtes sans auteur ni date sur tous les types de fichiers, ADR 008, 013, 014, 017, 024 complétées. Interface : bordure de la fin et bascules à 3:1, libellé sur 3 lignes avec pictogrammes, raccourcis en `--text-muted`, focus des menus, flèches 1,6 px, zoom et mini-carte avec le panneau, test des jetons découpé (153 cas, 148 couleurs nommées, SVG et TSX), contrôle de la maquette sur le fond réellement peint, page autonome vérifiée, jetons dérivés écrits en alias. |
| 4 | Architecture ; Interface et charte (agents neufs) | 1 + 0 | 8 + 2 | 12 + 7 | Bloquant corrigé : 13 lignes de couverture citaient des tests sous un ancien nom (noms alignés sur les listes réelles des tests). Majeurs corrigés : D07 (`Guid.NewGuid` refusé), D08 (dates construites sans genre UTC, `ToLocalTime`, `DateTimeKind.Local`), D12 (commentaires en français refusés), D13 dans les fichiers de construction et la CI, ADR 013 corrigée, code mort (`CA1812`), règles d'analyse verrouillées, limites ESLint non surchargeables, « + » dessus/dessous selon les couloirs voisins, preuve réelle du chargement de la police. Restent, notés pour les phases suivantes : une règle métier écrite directement dans un point d'entrée n'est pas détectable automatiquement (revue de la porte 4) ; les méthodes publiques et exports TypeScript inutilisés ne sont pas détectés (revue des portes) ; relecture de dates par EF en `Unspecified` à traiter en phase 3 (convertisseur UTC) ; règle de remontée du libellé au-dessus de la jauge posée en style en ligne dans `map.mjs` (à reporter dans le CSS de l'interface réelle en phase 6). |

**Porte de la phase 1 : franchie au 4e round** (prompt 7.2-5) : aucun bloquant restant ; les
majeurs du round 4 sont corrigés, sauf les trois limites de détection automatique citées
ci-dessus, consignées pour la revue des portes suivantes. Les mineurs restants (ARIA des
champs de la fiche maquette, tailles de police en dur dans la maquette, radiogroupes du ruban)
sont à reprendre dans l'interface réelle (phase 6).

### Bilan de la phase 1

- Tests : architecture 218 (0 échec) ; web 153 (0 échec) ; contrôle automatique de la maquette
  (951 textes au-dessus de 4,5:1, 192 objets graphiques au-dessus de 3:1, police embarquée
  prouvée, page autonome, mise en page sans défaut) ; 0 avertissement (dotnet, tsc, eslint).
- ADR 001 à 025 ; arbitrages AR-01 à AR-59.
- Maquette à valider par le porteur du projet : `docs/recette/maquette/maquette.html` et `LISEZMOI.md`.

## Phase 2 - Domaine

Fait : noyau (codes d'erreur, listes fermées, enregistrements immuables, espace de projet,
ChangeSet, rejeu pour annuler et rétablir, invariants, `Touch`, compteurs, règles de texte,
teintes, primitives de grille et de flèches) ; règles de l'annexe A écrites par sept agents en
parallèle, dossiers séparés : `Lanes`, `Steps`, `Layout`, `Flows`, `SubMaps`, `Raci`,
`Selection`, `Dissolutions`, `Sheets`, `Documents`, `Projects`, `Translations`, `Trash`, plus
`Maps` (repères, placement automatique). Intégration : règles « flux autonome » et « cadre
ouvert » ramenées à une seule implémentation, détail `code` d'`ETAPE_HORS_CARTE` toujours
lisible (spec 13 ; un test 1.x qui attendait l'identifiant a été aligné sur cette correction),
fiche du reliquat selon `SheetVisibility` (faiblesse 5.6-1), version de l'étape avancée par le
RACI et le détail. Arbitrages AR-60 à AR-65.

Couverture des lignes de phase 2 de l'annexe A : 259 couvertes, 109 couvertes pour leur part
domaine (le reste en phase 3, 4 ou 5), 0 restante. Correction après le round 1 de la porte : 16 lignes de
phase 3, 4 ou 5 marquées `couvert (domaine)` sans test (une raison avait été fusionnée dans « Test 2.0 ») sont
repassées `à faire`, la raison gardée en remarque ; 3 lignes de phase 2 citent désormais leur test réel, et un
contrôle par script vérifie que chaque test C# cité par une ligne `couvert` existe (864 citations, 0 manquante).
Tests ajoutés dans `Coverage/` pour les mutations survivantes du contrôle (23 mutations, toutes tuées).

Tests : domaine 888, architecture 235, web 153 ; 0 avertissement.

### Porte de la phase 2

| Round | Contrôleurs | Bloquants | Majeurs | Mineurs | Suite |
|---|---|---|---|---|---|
| 1 | Fidélité ; Architecture ; Intégrité des tests (agents neufs, 91 mutations : 80 tuées) | 2 + 5 + 3 | 3 + 11 + 6 | nombreux | Consolidation confiée à deux agents : une seule implémentation par règle (profondeur, traductions, RG-C05, report RACI, cadre ouvert, recousage, suppression de document), objets valeur (ADR 026), versions sur les enregistrements modifiables (D06), `OperationLogEntry`, invariants complétés et rejeu fidèle à la 1.x (intruse décalée, flèche recréée non doublée), noms de champs anglais du reliquat ; couverture corrigée (19 lignes de fusion fausses) et tests des mutations survivantes. |
| 2 | Fidélité (368 lignes : 346 prouvées, 21 partielles, 1 non prouvée, 0 fausse ; 83 essais, 16 mutations) ; Architecture ; Intégrité des tests (107 mutations : 76 tuées, 19 survivantes réelles) | 2 + 1 + 1 | 8 + 7 + 8 | 38 + 9 + 5 | Bloquant D07 corrigé par l'orchestrateur (compteurs de codes exclus du rejeu, test prouvé par mutation). Constats : test de disposition exacte non discriminant, 7 lignes A-2.15 sorties de la phase 2 sans arbitrage, règles écrites plusieurs fois (RG-C05, RG-C09, sous-arbre, extrémités, remontée), invariants et rejeu sous-testés, chiffres d'`etat.md` périmés. Corrections confiées à trois agents sur des fichiers disjoints (rejeu et noyau ; doublons ; tests des règles). |
| 3 | Fidélité (375 lignes : 328 prouvées, 45 partielles, 2 fausses sur un cas limite ; 280 mutations, 61 essais) ; Architecture (15 violations injectées : 4 détectées) ; Intégrité des tests (423 mutations : 375 tuées, 23 survivantes prouvées ; chiffres déclarés exacts, aucun test affaibli) | 0 + 2 + 0 | 23 + 6 + 9 | nombreux | Bloquants : l'annulation et le rétablissement d'une suppression écrasent sans refus une écriture concurrente (les suppressions n'entrent pas dans le contrôle des versions) ; la version d'une fiche n'avance pas quand seul le déroulé change. Majeurs : cycles de dossiers restants que la règle ne voyait pas (« plus de cycle » du round 2 était faux), ordre des couloirs encore écrit trois fois, règles d'architecture et détecteurs textuels contournables, écarts d'ordre des refus avec la 1.x, preuves non discriminantes. Corrections confiées à trois agents (rejeu et versions ; règles d'architecture sémantiques et doublons ; écarts et tests). |
| 4 | Fidélité (377 lignes : 361 prouvées, 15 partielles, 1 fausse sur un sous-cas ; 174 mutations) ; Architecture (68 violations injectées : 43 détectées) ; Intégrité des tests (171 mutations neuves : 130 tuées, 25 survivantes prouvées ; chiffres exacts, aucun test affaibli) | 1 + 1 + 0 | 7 + 4 + 22 | nombreux | Bloquants : la pile d'annulation ne servait qu'une fois par objet (contrôle par la version, introduit au round 3) ; un rôle RACI d'un couloir d'une troisième carte ne suivait plus à la remontée. Corrections : AR-81 (contrôle par le contenu), AR-82 (RACI comme la 1.x), clés d'enregistrement seulement dans `Model`, liste blanche des types du domaine, ordre des couches, détecteurs d'implémentation unique renforcés (68 injections sur 68 détectées au rejeu), un test pour chaque mutation survivante (plus une campagne neuve de 40 : 38 tuées, 2 équivalentes), ordre des refus et petits écarts alignés ou arbitrés (AR-83 à AR-86). |

Après les corrections du round 1 : domaine 954 tests, architecture 235, 0 avertissement. Le
code `CARTE_NON_VIDE` est retiré (AR-43) et le test qui figeait les valeurs des codes est aligné.

Corrections du round 2 (trois agents sur des fichiers disjoints, plus l'orchestrateur) : compteurs exclus du
rejeu (D07) ; A-2.15-05 à 11 ramenées en phase 2 et prouvées par 17 tests qui passent par `UndoReplay` ;
couloirs renumérotés après annulation ; lot marqué restauré quand une annulation l'a vidé (AR-68) ; un test
par invariant de structure ; `AppUser` versionné par l'opération (`Versioning`) ; une seule implémentation de
RG-C05 (`FlowLinks`), de RG-C09 (`SettleSubMaps`), du sous-arbre, de la remontée (`ParentSteps`), des
extrémités de bloc et de l'ordre des couloirs (`InLaneOrder`), gardée par `SingleImplementationRules` ; plus
de cycle entre dossiers du domaine (règle d'architecture) ; T-SCFL-01 à 07 portés ; couleur de couloir sans
espaces (1.x) ; teintes confrontées à la charte ; bornes fixées par des tests ; arbitrages AR-66 à AR-74 ;
ADR 015 et 016 précisées ; `architecture.md` corrigé (enregistrements sans version, glossaire, dossiers) ;
64 fichiers repassés en LF et règle d'architecture ajoutée. Couverture fusionnée : phase 2 de l'annexe A
375 lignes (257 couvertes, 118 pour leur part domaine, 0 à faire), 973 citations de tests (936 C#, 37 web), 0 manquante.

Après les corrections du round 2 : domaine 1035 tests, architecture 249, web 153, 0 avertissement.

Corrections du round 3 (trois agents, plus l'orchestrateur) : le contrôle des versions de l'annulation porte aussi
les clés supprimées et créées et les enregistrements sans version (refus `OPERATION_DEPASSEE`), il est fait par
`UndoReplay.Undo` et `Redo` eux-mêmes, et tout autre refus d'un rejeu devient `CARTE_MODIFIEE` (AR-79) ; la fiche
change de version quand son déroulé change ; le rétablissement marque restauré le lot vidé (AR-78) ; codes
lisibles en chiffres ASCII (AR-80). Règles d'architecture réécrites sur le modèle sémantique de Roslyn (cycles de
dossiers, douze implémentations uniques, horloge et identifiants, `Commit`, écriture des versions), prouvées sur
leurs contournements ; cycles réels supprimés (dossier `Blocks`) ; ordre des couloirs et langue d'origine en une
seule implémentation ; API inutile retirée. Ordre des refus aligné sur la 1.x (changement de type, création de
projet) ; suivi des rôles à la remontée arbitré (AR-75) ; AR-76 et AR-77 ; un test pour chaque mutation survivante
des contrôleurs. Limite connue : l'usage déplacé sur une autre étape (`Remainder`) repart en version 1 mais garde
l'audit de mise à jour de l'ancien usage.

Après les corrections du round 3 : domaine 1165 tests, architecture 357, web 153, 0 avertissement. Couverture
fusionnée : phase 2 de l'annexe A 377 lignes (265 couvertes, 112 pour leur part domaine, 0 à faire), 1078
citations C# distinctes et 37 web, 0 manquante.

Corrections du round 4 (dernier) : voir la ligne 4 du tableau. Couverture fusionnée : phase 2 de l'annexe A 391 lignes
(264 couvertes, 127 pour leur part domaine, 0 à faire ; les 14 lignes de l'annulation exécutée par le domaine sont
ramenées en phase 2) ; convention de phase écrite dans l'en-tête et appliquée à toutes les lignes ; 1173 citations C#
par ligne (802 tests distincts) et 37 web (16 tests distincts), 0 manquante.

**Porte de la phase 2 fermée après le round 4** (le dernier permis par le prompt 7) : les bloquants du round 4 sont
corrigés, toutes les suites sont vertes et sans avertissement. Restes connus, sans effet observable : une branche
défensive de `UndoReplay.KeepFlowsThatCannotComeBack` (flèche absente avant le rejeu) n'est atteignable que hors du
protocole réel ; le contrôle `step_id` de `Remainder.HolderToAssign` est redondant avec `Remainder.Move` ; les
exceptions documentées de la liste blanche (`ErrorCodes.All` par réflexion, `SheetJson` en `JsonDocument`) ; les
limites écrites des détecteurs (`architecture.md` §2.4). Arbitrages à valider par le porteur du projet : `questions.md`.

Tests à la fin de la phase 2 : domaine 1299, architecture 477, web 153 ; 0 avertissement (dotnet, tsc, eslint).

## Phase 3 - Persistance

(en cours)

### P3-T01 et P3-T02 - Modèle, garde-fous, migrations, historique du schéma (23/10/2026)

- Ports de l'Application (`src/Topogenia.Application/Ports/`) : `IUnitOfWork`, `IWorkspaceStore`, `ICounterService`,
  `IOperationLog`, `IBackupService`, `IClock`, `ISchemaMigrations` (état du schéma, migration, refus par codes).
- `docs/modele-donnees.md` : 24 tables, colonnes et types par fournisseur extraits du modèle compilé, clés, index,
  CHECK, correspondance avec le domaine et la 1.x, écarts ; procédure de génération d'une migration.
- `AppDbContext` (et non `TopogeniaDbContext` : D13, ADR 028) ; snake_case et conventions de noms 1.x ; schéma
  `topogenia` en SQL Server dérivé de `ProductIdentity.Name`, aucun préfixe en SQLite ; 7 index uniques partiels et
  56 CHECK décrits une fois (`ModelGuards`) et écrits par chaque fournisseur (ADR 025).
- Migrations initiales générées par `dotnet-ef` 10.0.13 en outil local du dépôt, puis relues ; `schema_history`
  avec empreinte de la source de chaque migration et du schéma lu dans le catalogue, écrite dans la transaction de
  la migration ; refus `BASE_PLUS_RECENTE`, `MIGRATION_MODIFIEE`, `EMPREINTE_ABSENTE`, `SCHEMA_MODIFIE` (ADR 027).
- Décisions (ADR 028) : langues et réglages en configuration (tables 1.x `language`, `app_setting` retirées), dates
  `datetime2(7)` (une troncature ferait refuser une annulation légitime, AR-81), entiers `int`, quelques identifiants
  sans clé étrangère, décimaux comparés comme nombres dans les CHECK SQLite.
- Vérifié par une exécution complète : 2072 tests verts (architecture 477, domaine 1299, persistance 296, chaque
  théorie sur SQLite et SQL Server), 0 avertissement ; aucune base `Topogenia_*` ni fichier SQLite restant.
- Ajout : règle d'architecture A-5-12 (ni `master`, ni `sp_getapplock`, ni `CREATE DATABASE` dans `src`), 13 tests,
  vérifiée par mutation ; architecture 490.
- Couverture : 29 lignes reportées depuis `couverture-p3/modele-migrations.md` ; nouvel état `couvert (<niveaux>)`
  défini dans l'en-tête de `couverture.md`.

### P3-T03 - Réglages SQLite, sauvegarde en ligne, démarrage du mode Poste (23/10/2026)

- `Persistence.Sqlite/Connection/` : intercepteur qui impose à chaque connexion `busy_timeout = 5000`,
  `foreign_keys = ON`, `synchronous = NORMAL`, `journal_mode = WAL` (vérifié), même contre une chaîne de connexion
  contraire ; le SQL `PRAGMA` est dans `SqlitePragmas`.
- `Backup/` : sauvegarde par l'API en ligne vers un fichier `.partial`, contrôlé (`integrity_check`,
  `foreign_key_check`), passé en journal `DELETE`, puis substitué à l'ancien ; 7 fichiers quotidiens (jour UTC),
  un fichier avant migration ; échecs rendus par codes (`SAUVEGARDE_BASE_ABSENTE`, `SAUVEGARDE_IMPOSSIBLE`,
  `SAUVEGARDE_CORROMPUE`).
- `Startup/DesktopDatabaseStartup` : contrôle, sauvegarde avant migration d'une base en retard non vide (échec :
  aucune migration, AR-14), migration, quotidienne non bloquante ; l'appel par l'hôte relève de P4-T05.
- 19 tests SQLite, 18 mutations toutes rouges ; ADR 029 ; 9 lignes de couverture reportées (A-4-38 et S-5.2-12
  reclassées « Persistance, Application », la part de l'hôte restant en phase 4).

### P3-T06 - Rôles de base et vues `bi` du mode Serveur (23/10/2026)

- Seconde migration SQL Server `RolesAndReportViews` (aucun changement du modèle, `Up` et `Down` écrits à la main) :
  schémas propriété de `dbo` ; `topogenia_app` (données du schéma de l'application ; `DENY` d'écriture sur
  `schema_history` et `__EFMigrationsHistory` ; `DENY UPDATE` sur `step_sheet_version`,
  `pain_point_status_history` et `audit_event` ; `VIEW DEFINITION` pour le contrôle du démarrage) ; `topogenia_bi`
  (lecture des seules vues `bi`) ; 11 vues au contenu de la 1.x après `db/006`.
- Les droits des rôles et leurs appartenances entrent dans l'empreinte du schéma (pas leurs membres) : un droit
  donné ou retiré à la main est refusé au démarrage par `SCHEMA_MODIFIE`. Aucune vue en SQLite (D04).
- 40 tests SQL Server (`Bi/`, utilisateurs `WITHOUT LOGIN` et `EXECUTE AS USER`, oracle des vues calculé par le
  domaine), 11 mutations toutes rouges ; ADR 032 ; question sur la classification dans `bi.project` ajoutée à
  `questions.md` ; 14 lignes de couverture reportées.
- Texte de P3-T03 intégré dans `modele-donnees.md` (§ 7.4, § 7.6, § 9).

### P3-T04 et P3-T05 - Unité de travail, écriture du ChangeSet, erreurs, chargeur (23/10/2026)

- `Writing/` : unité de travail (READ COMMITTED en SQL Server, IMMEDIATE en SQLite), verrou de projet
  `structure_version + 1` en LINQ, `RunAsync` qui rejoue toute l'unité sur interblocage ou base occupée (3 essais,
  50 puis 100 ms, contexte neuf à chaque essai, refus métier jamais rejoué) ; écriture du `ChangeSet` en deux
  enregistrements (places d'index partiel libérées et étapes stationnées d'abord), version contrôlée sur chaque
  modification (`CONFLIT_VERSION`), enregistrements sans version relus par paquets de 500.
- `Errors/` : dialectes des deux moteurs ; unicité en `CONFLIT_UNICITE`, clé étrangère et CHECK en
  `CONFLIT_INTEGRITE`, avec le nom de la contrainte.
- `Loading/` : chargeur de l'espace (projet d'abord, structure, compteurs, groupes demandés), par jointures sur
  le projet ; lecture cohérente.
- 92 cas de test sur les deux moteurs (70 `Writing/`, 22 `Loading/`), dont un vrai interblocage SQL Server et une
  vraie base occupée SQLite ; mutations vérifiées (une survivante documentée : retrait physique écrit d'abord,
  qu'EF Core ordonne déjà). Port `IUnitOfWork.RunAsync` ajouté ; ADR 030 et 031.
- Vérification complète : 2236 tests verts (architecture 490, domaine 1299, persistance 447), 0 avertissement,
  aucune base ni fichier restant.
- Écart relevé : l'agent avait choisi la lecture sérialisable en SQL Server, parce que la règle d'architecture
  A-5-12 refusait tout `ALTER DATABASE`. La règle était plus large que la ligne A-5-12 (`master`, procédures
  système, `CREATE DATABASE`) ; la spec 3.4 demande l'instantané et la 1.x posait l'option par
  `ALTER DATABASE CURRENT` (`db/003`). Décision (prompt 6.4, défaut de la spec) : règle resserrée, option posée par
  une migration, chargement en instantané ; confié à un agent.
- Couverture : 20 lignes reportées ; 69 lignes de phase 3 restent ouvertes, confiées à P3-T07.

### Lecture cohérente en instantané SQL Server (ADR 031 révisée, 23/10/2026)

- Règle A-5-12 resserrée : `ALTER DATABASE CURRENT` permis ; `ALTER DATABASE <nom>`, `CREATE`/`DROP DATABASE`,
  `sp_getapplock`, `master` restent refusés (9 échantillons de plus).
- Migrations `SnapshotIsolation` (`ALLOW_SNAPSHOT_ISOLATION` et `READ_COMMITTED_SNAPSHOT`, comme `db/003` de la
  1.x, hors transaction, rejouables) et `SnapshotIsolationCheck` (en transaction : EF Core enregistre hors
  transaction une migration qui contient une instruction hors transaction, l'historique n'y était donc pas écrit).
  RCSI mesuré avant de décider : aucun effet sur le verrou de projet, les versions ni l'interblocage réel.
- L'état des deux options entre dans l'empreinte du schéma (ligne `O`) : une option retirée à la main fait refuser
  la base au démarrage (`SCHEMA_MODIFIE`) au lieu de l'erreur 3952 à chaque lecture. `BeginReadAsync` en
  `Snapshot` ; nouveau test : une écriture concurrente n'attend pas la lecture et n'y est pas vue ; lecture sous
  `topogenia_app`.
- Écart A-4-34 : `SnapshotIsolation` n'est pas atomique (comme `db/003`). Reste signalé : après une panne dans la
  migration de contrôle, la reprise était manuelle (`EMPREINTE_ABSENTE`) ; confié au même agent pour une reprise
  automatique.
- `--migrate` doit tourner application arrêtée (`ROLLBACK IMMEDIATE`) : à reporter dans `docs/exploitation-serveur.md`
  (phase 7).

### P3-T07 partie A - Concurrence, garde-fous, aller-retour, portage des tests 1.x (23/10/2026)

- 160 cas nouveaux sur SQLite et SQL Server : `Concurrency/` (opérations réellement simultanées, point de
  rencontre), `Guards/`, `RoundTrips/` (projet, structure, fiches), `Porting/` (13 tests 1.x portés,
  `docs/portage-tests.md`).
- Défaut trouvé et corrigé (ADR 033) : deux unités qui créaient en même temps un utilisateur (même login), une
  traduction ou un usage se gênaient, la seconde recevait `CONFLIT_UNICITE`. Une création perdue sur ces clés
  naturelles est désormais rejouée comme un interblocage (toute l'unité, 3 essais) ; les autres clés gardent leur
  refus. Constat consigné : le verrou de projet est obligatoire pour toute opération de structure (aucun
  déclencheur ne protège un couloir ou une étape supprimés entre-temps).
- 10 mutations toutes rouges ; tests de concurrence stables sur quatre exécutions. 45 lignes reportées (26
  `couvert`, 19 dont la part phase 4 est rattachée à sa tâche).
- 35 bases `Topogenia_Test_*` laissées par des processus de test arrêtés ont été supprimées (préfixe autorisé).

### P3-T07 partie B - Nombre de requêtes, parcours, purge, fichier du mode Poste (23/10/2026)

- Nombre de requêtes indépendant de la taille prouvé par comptage des commandes (`CommandLog`) sur les deux
  moteurs (1 contre 200 étapes ou flèches, 1 contre 50 membres, profondeur 1 contre 50) ; parcours de 60 niveaux
  relu, borné à 50 ; renumérotation des couloirs.
- Purge physique en C# (`Persistence/Purge`, port `ITrashPurge`) : journal lié d'abord, puis le plan du domaine
  par transactions de 500 au plus sous le verrou de projet, garde du plan ; réussie sous `topogenia_app`.
- `Persistence.Sqlite/Files` : chemin par défaut, refus d'un partage réseau ou d'un dossier synchronisé (chemins
  fictifs) ; erreurs de démarrage 4060/18456/229/262 relevées sur l'instance locale ; reconstruction de table
  SQLite conservant lignes, index, CHECK et clés.
- 67 cas, 13 mutations rouges ; ADR 034 et 035 ; 24 lignes reportées (six reclassées avec leur part de phase 4
  ou 5).
- Constats renvoyés à l'agent propriétaire de `SqlServer/` et `Schema/` : vue `bi.process_map` à récursion non
  bornée ; un compte sans droits voit la base « en retard » au lieu de « droits insuffisants » (A-5-10).
- Une exécution complète a connu des échecs d'ouverture de connexion SQL Server pendant que trois agents
  chargeaient l'instance ; relancée sans changement, elle est passée. À surveiller par la porte (stabilité).

### Reprise automatique des migrations, borne des rapports, droits au démarrage (23/10/2026)

- Reprise automatique (ADR 027 révisée) : une migration enregistrée hors transaction par EF Core
  (`SnapshotIsolation`) est reconnue (`MigrationTransactions`) ; la seule forme incomplète acceptée est « en
  attente d'inscription » (dernière appliquée, hors transaction, suivie d'une migration en transaction en
  attente), inscrite par l'exécution suivante sous le verrou des migrations ; tous les refus antérieurs gardés.
  Règle de structure testée : toute migration hors transaction est suivie d'une migration en transaction.
- `bi.process_map` bornée à 50 niveaux (migration `ReportMapDepthBound`) : la vue de la 1.x échouait au-delà de
  101 niveaux ; les cartes plus profondes n'apparaissent plus dans les rapports (écart, `modele-donnees.md` § 10.2).
- Démarrage (ADR 036) : lecture d'essai à l'ouverture ; `DROITS_INSUFFISANTS` et `BASE_INACCESSIBLE` ; tests sous
  `EXECUTE AS USER` (membre de `topogenia_bi`, utilisateur sans rôle refusés ; `topogenia_app` accepté).
- Mutations toutes rouges ; couverture `couverture-p3/instantane.md` reportée (A-4-51, A-5-11 : l'écart
  « sérialisable » est levé).

### Porte de la phase 3

| Round | Contrôleurs | Bloquants | Majeurs | Mineurs | Suite |
|---|---|---|---|---|---|
| 1 | Architecture ; Sécurité ; Données et portabilité ; Fidélité ; Intégrité des tests (agents neufs) | 0 | 3 + 3 + 5 + 1 + 2 | 8 + 7 + 4 + 4 + 3 | Communs : suite de persistance instable sous la charge de cinq exécutions simultanées (verrou exclusif de `model` à la création des bases, `ALTER DATABASE` de la migration, cascade sur une classe quand sa base échoue) ; 1802 accompagnant 1807 lu comme `DROITS_INSUFFISANTS`. Architecture : règle A-5-12 trop large (`ALTER DATABASE CURRENT` sous toute forme), sonde d'options non documentée ni gardée par une règle. Sécurité : empreinte SQL Server aveugle aux fonctions, synonymes, déclencheurs, procédures, droits donnés à `public` et membres de `db_datareader` (lecture de `app_user` par le rôle des rapports prouvée). Données : collation de la base en SQL Server (casse, ß, pleine chasse) ; tri d'un décimal SQLite en culture française ; délai réel de « base occupée » SQLite de 30 s ; CHECK et relecture des décimaux différents selon le moteur. Fidélité : cartes des niveaux 51 à 101 retirées des rapports sans arbitrage. Corrections (quatre agents, fichiers disjoints) : C1 empreinte SQL Server complète (tout objet, déclencheur de base, type, droit de la base, membres des rôles fixes de données ; 31 fuites reproduites rouges puis refusées), empreinte SQLite (joker `_`, historiques), `BASE_OCCUPEE` passager et 1802 seul seulement en `DROITS_INSUFFISANTS`, A-5-12 limitée aux deux options de l'instantané, règles des sondes et d'`InternalsVisibleTo`, migrations écrites à la main soumises aux règles de forme, aucun paquet de fournisseur dans la persistance commune (ADR 037) ; C2 collation `Latin1_General_100_BIN2` sur les 69 colonnes texte (sixième migration, ADR 038), décimaux à l'échelle de leur colonne et rendu commun des garde-fous (ADR 039), `bi.process_map` à 101 niveaux (septième migration, AR-87), `VIEW DEFINITION` de la base pour le compte de l'application (huitième migration, ADR 032 révisée) ; C3 bases de test sous charge (base rangée une fois migrée, nouvel essai borné des pannes passagères, deux créations simultanées par processus, nettoyage des bases et dossiers d'un processus mort, ADR 040), tests de course prouvants, écritures sans version sous le verrou de leur projet (`UnlockedWriteException`, ADR 030) ; C4 collation décimale invariante en SQLite, délai de commande de 1 s (attente réelle d'une base occupée : environ 15 s par unité au lieu de 100 s), messages de sauvegarde sans chemin, chemin du fichier résolu (`..`, noms courts, liens), rotation des sauvegardes d'avant migration et contrôle de leur dossier (ADR 029, 035). Par le chef de projet : anonymisation des documents, AR-87, tâches nommées dans la couverture, `portage-tests.md`, `architecture.md`. Mutations : 55 jouées, 54 rouges (la suppression des statistiques de la migration de collation n'est pas exercée : une statistique d'administrateur ne peut pas être créée sans SQL dans les tests, ADR 038). |


Vérification après le round 1 (chef de projet) : suite complète trois fois de suite verte (2693, 0 avertissement), puis
trois exécutions simultanées de la solution : deux vertes, une avec un échec,
`SchemaMigrationTests.Two_processes_that_migrate_at_once_apply_each_migration_once(kind: SqlServer)` en `BASE_OCCUPEE`
(délai d'exécution dépassé dans l'attente du verrou des migrations). Défaut de l'application, pas du test : les
migrations et l'attente de leur verrou suivaient le délai ordinaire des commandes (30 s en SQL Server, 1 s en
SQLite), et en SQLite une ligne de verrou laissée par une migration arrêtée bloquait sans fin. Correction par un
agent : ADR 041, délai propre aux migrations (10 min par défaut, réglable par l'hôte), attente du verrou bornée sur
les deux moteurs, délai ordinaire inchangé ailleurs ; six tests nouveaux rouges avant la correction, trois mutations
rouges ; suite complète verte (2699 : architecture 532, domaine 1299, persistance 868). Restent deux défauts du même
genre, confiés à un second agent : `BASE_OCCUPEE` non traduit en SQLite (`SqliteSchemaCatalog.RefusalOf` absent) et
`CollationMigrationTests` qui crée ses bases hors du chemin de l'ADR 040.

Suite (second agent) : SQLite traduit ses échecs au démarrage (`SqliteSchemaCatalog.RefusalOf` : `BASE_OCCUPEE`,
`BASE_INACCESSIBLE`, `DROITS_INSUFFISANTS`, ADR 036 révisée, défaut de mode Poste dans `questions.md`). Défaut trouvé en
écrivant le test réel : une sauvegarde restaurée (journal `DELETE`) était refusée au contrôle (`SQLITE_READONLY` sur la
connexion en lecture seule d'EF Core), corrigé dans `SqliteConnectionSettings` (ADR 029 annotée). Les migrations des
tests passent par le contexte des migrations et `PreparedAsync` (Collation, AccessMigration, SnapshotIsolation ; ADR 040
révisée). `StartupErrorTests` rouvre seulement sur un échec sans réponse du serveur (233 seul sous charge), écart nommé
à l'ADR 040, assertion inchangée. Tests : 25 + 5 nouveaux, 19 rouges avant correction ; 5 mutations rouges. Solution
2729/2729 ; trois suites de persistance simultanées 898 × 3 vertes au troisième essai (les deux premiers : 1 échec,
`StartupErrorTests`, 233, avant sa correction).

Vérification avant le round 2 (chef de projet) : trois exécutions simultanées de la solution entière, chacune avec son
dossier d'artefacts : 2729/2729 vertes chacune, 0 avertissement, aucune base `Topogenia_*` ni dossier `topogenia-test-*` restant.

| Round | Contrôleurs | Bloquants | Majeurs | Mineurs | Suite |
|---|---|---|---|---|---|
| 2 | Architecture ; Sécurité ; Données et portabilité ; Fidélité ; Intégrité des tests (agents neufs) | 0 + 0 + 1 + 0 + 0 | 2 + 2 + 2 + 1 + 3 | 7 + 5 + 6 + 5 + 5 | Corrections du round 1 confirmées. Bloquant : une sauvegarde SQLite restaurée par copie à côté d'un `-wal` périmé rend des données postérieures ou une base corrompue, acceptée au démarrage. Architecture : renommage en une ligne (D13) cassé (schéma et nom des tests dérivés du nom du produit, migrations figées) ; SQL composé dans la persistance commune (`GuardRenderer`). Sécurité : vue `bi.[__EFMigrationsHistory]` hors de l'empreinte et propriétaire d'objet non décrit (lecture de `app_user` par le rôle des rapports prouvée). Données : parité du texte (espaces de fin, longueur, non-ASCII en `varchar`) ; retour en arrière SQLite impossible et verrou laissé. Fidélité : base existante non ouvrable (4060) dite en retard. Intégrité : nettoyage du démarrage en échec gardé pour tout le processus (400 échecs), `SnapshotIsolationTests` sous charge, 12408 à la suppression. Suites : 2729 avec 0 à 3 échecs par contrôleur sous charge ; suites supplémentaires jusqu'à 400 échecs. |

Corrections du round 2 (cinq agents, fichiers disjoints) : A empreinte SQL Server (historique d'EF Core écarté à la
casse exacte dans le seul schéma du produit, propriétaires des objets et des schémas, options `TRUSTWORTHY` et
`DB_CHAINING`, objets marqués livrés hors de `sys`), refus sans le message du moteur (`EngineFailureException`, numéros
seuls), base absente en SQL Server refusée `BASE_INACCESSIBLE` (4060 indiscernable), verrou SQLite laissé
`VERROU_MIGRATIONS` non passager (ADR 036, 037, 041) ; B nettoyage abandonné plutôt que gardé en échec, liste des bases
lue sans attendre, 12408 passager à la suppression, création seule rejouée sous un budget commun de 10 min, retrait de
l'instantané au délai des migrations (il attend les écritures de toute l'instance), bornes de `RealBusyTests` sur les
pauses dormies (ADR 040, 029) ; C restauration par `IBackupService.RestoreAsync` (copie contrôlée, journal retiré,
fichier ouvert refusé) et contrôle à l'ouverture d'un journal étranger (`SqliteJournalCheck`), `Down` SQLite sans
`DropForeignKey`, chemins non pris en charge refusés par code (ADR 029, 035, 036) ; D1 gardes de texte sur les 69
colonnes (espace de fin, longueur, ASCII des `varchar`), convertisseur `StoredTextConverter`, 2628 / 8152 / 8115 en
`CONFLIT_INTEGRITE`, entiers bornés à `int.MaxValue`, neuvième migration SQL Server, deuxième et troisième SQLite
(ADR 038, 039) ; D2 noms techniques figés (`TechnicalNames`, renommage du produit essayé : compile, suite inchangée),
SQL des garde-fous écrit par chaque fournisseur (`GuardWriter` commun sans SQL, `GuardWritingRulesTests`), règles
d'architecture renforcées (verrou des paquets, chaînes regroupées, commentaires SQL, A-5-12 resserrée) (ADR 013, 025,
039). Par le chef de projet : test de la base absente aligné sur l'ADR 036, documentation du port `ISchemaMigrations`,
couvertures fusionnées, `portage-tests.md` (T-PSEC-09, T-RACI-02, T-STRU-16, T-WHIS-03, T-WHIS-07), lignes A-4-33,
A-5-21, A-5-30 corrigées, `questions.md` (six entrées), dernier prénom réel retiré. Mutations : plus de 50 jouées par
les agents, toutes rouges.

Vérification avant le round 3 (chef de projet) : trois exécutions simultanées de la solution entière : 2860/2860 vertes chacune
(architecture 585, domaine 1299, persistance 976), 0 avertissement, aucune base ni dossier de test restant.

| Round | Contrôleurs | Bloquants | Majeurs | Mineurs | Suite |
|---|---|---|---|---|---|
| 3 | Architecture ; Sécurité ; Données et portabilité ; Fidélité ; Intégrité des tests (agents neufs) | 0 + 0 + 2 + 0 + 0 | 1 + 2 + 3 + 1 + 1 | 6 + 4 + 6 + 5 + 3 | Corrections du round 2 confirmées (renommage du produit essayé : compile, suite verte ; empreinte : une vingtaine de fuites refusées). Bloquants : sauvegarde copiée à la main application ouverte (le point de contrôle de fermeture réécrit l'ancien journal dans la copie, base fausse ou corrompue acceptée) ; migration SQLite de reconstruction non atomique (arrêt en cours : `SCHEMA_MODIFIE` définitif, tables temporaires laissées). Majeurs : rôle d'application ou rôle de base membre de `topogenia_app` hors de l'empreinte (lecture de `app_user` prouvée) ; restauration du fichier sur lui-même par un chemin `\\?\` (perte du journal) ; recherches par motif différentes selon le moteur ; remontée refusée après la descente d'une migration ; copie d'un fichier en WAL à côté d'un journal étranger acceptée ; nom d'annuaire coupé sur une espace (utilisateur jamais créé) ; D04 non vérifié à l'exécution en SQL Server ; reprise des préparations de test inopérante depuis `EngineFailureException`. Incident : un contrôleur a arrêté par son nom tous les processus de tests de persistance vers 23 h ; les exécutions coupées ont été refaites. Suites : 2860 verts deux fois (Intégrité), persistance 976 avec 0 à 2 échecs passagers sous charge. |

Corrections du round 3 (cinq agents) : A empreinte des membres non utilisateurs des deux rôles et des rôles
d'application (fuite par `sp_setapprole` prouvée puis refusée), `TimeoutException` du verrou sans exception du moteur,
18456 seul en `DROITS_INSUFFISANTS`, D04 prouvé à l'exécution en SQL Server (aucun objet de programmation dans la base
migrée), `VERROU_MIGRATIONS` seulement pour un verrou resté tout le délai sans écriture (ADR 032, 036, 037, 041) ; B
reprise des préparations par le vrai chemin (`EngineFailureException` sous `BASE_OCCUPEE`), bornes de `RealBusyTests`
fixées dans le test et mesurées sur le fil de SQLite, bases de test suffixées nettoyées (les deux bases `_kept`
orphelines ont disparu au lancement suivant), existence d'une base lue par une ouverture (22 ms au lieu de 11 s sous
charge), collision de logins de test (ADR 040, 029) ; C les deux bloquants : fichier lié à son journal par un tampon
(`SqliteDataFileGuard`, `SqliteWalFile` : journal étranger, journal sans page 1, fichier abîmé, fichier remplacé en
marche refusés `BASE_INACCESSIBLE`, plus aucun point de contrôle hors du moteur, arrêt propre `CloseAsync`) et
reconstruction SQLite dans la transaction de sa migration (`AtomicRebuildSqlGenerator`, `defer_foreign_keys`), descente
puis remontée d'une migration sur les deux moteurs, restauration par identité de fichier et API de sauvegarde, liens
physiques et NUL refusés par code (ADR 027, 029, 035, 038) ; D1 noms d'annuaire rognés après la coupe, recherches par
motif alignées (`case_sensitive_like`, `SqliteTextSearch`, `TextSearchParity` qui refuse `ToUpper`, `Like`,
`Substring`...), NUL refusé, consigne pour la migration 1.x (P5-T05) (ADR 038) ; D2 règles d'architecture : mots clés
coupés entre littéraux, un seul type par sonde, dispense des seuls fichiers générés par un outil, liste blanche des
paquets de la persistance commune (ADR 017, 022, 037). Par le chef de projet : arrêt de quatre shells morts de travaux
terminés (par numéro), consignes de fusion restées dans sept lignes de la couverture et fragments non-tests retirés,
couvertures r3 fusionnées (noms de tests seulement), `questions.md` (dix entrées), nettoyage des dossiers de test en
lecture seule, textes périmés de `modele-donnees.md`. Incident : un contrôleur du round 3 a arrêté par son nom tous les
processus de tests de persistance ; règle donnée à tous les agents : arrêt par numéro seulement. Mutations : plus de 60,
toutes rouges.

Vérification avant le round 4 (chef de projet) : trois exécutions simultanées de la solution entière : 2996/2996 vertes chacune
(architecture 649, domaine 1301, persistance 1046), 0 avertissement, aucune base ni dossier de test restant.

Round 4 lancé (cinq contrôleurs neufs), puis coupé par un redémarrage imprévu du PC le 24/10/2026, avant tout compte
rendu. Après le redémarrage : deux bases de contrôle laissées (`Topogenia_Test_Ctrl_Securite4`, `...4b`) supprimées, aucun
autre reste ; le round 4 est à relancer en entier (`REPRISE.md` § 0).

Round 4 relancé le 24/10/2026 après vérification (build 0 avertissement, 2996/2996) : cinq contrôleurs neufs, mêmes
mandats ; constats détaillés dans `Travail/porte-p3-r4-constats.md` (hors dépôt).

| Round | Contrôleurs | Bloquants | Majeurs | Mineurs | Suite |
|---|---|---|---|---|---|
| 4 | Architecture ; Sécurité ; Données et portabilité ; Fidélité ; Intégrité des tests (agents neufs) | 0 + 0 + 1 + 0 + 0 | 0 + 1 + 1 + 1 + 2 | 5 + 2 + 1 + 5 + 3 | Corrections du round 3 confirmées par les cinq. Bloquant : au-delà de 4 Mio de journal, la fermeture de la connexion d'une unité après son commit écrivait le tampon ; une autre connexion qui tenait le verrou plus de 6,15 s faisait sortir `SQLITE_BUSY` de `CommitAsync`, et l'unité validée était rejouée (travail écrit deux fois, sonde). Majeurs : données purgées lisibles dans les pages libres du fichier et dans chaque sauvegarde, 40 jours après comprise (Sécurité) ; contrôle du démarrage tué pendant qu'il tenait la ligne du verrou d'EF Core, base saine refusée `VERROU_MIGRATIONS` au bout de 10 min (Données) ; parts hôte de A-4-29, 32, 36, 37 suivies par aucune tâche de phase 4 (Fidélité) ; refus 26/538 et `foreign_key_check` de la garde et de la restauration éprouvés par aucun test (Intégrité, mutations survivantes). Vigilance : 30 arrêts brutaux d'écrivains, aucun faux refus du tampon ni validation perdue. Mutations des contrôleurs : 17 jouées, 12 tuées. Suites sous charge : 2996 vertes pour quatre contrôleurs ; un échec passager chez Données (`TestInfrastructureTests`, 4060 sur une base juste créée, comme au round 3, non reproduit par Intégrité sur deux exécutions). |

Corrections du round 4 (dernier round, prompt 7.2 point 5) :
- **Bloquant** (chef de projet) : une unité validée n'est jamais rejouée (`StoreSession` note le commit du moteur par
  `TransactionCommitted`, `UnitOfWork.Retried` ne classe plus passagère une défaillance d'après) et la fermeture d'une
  connexion ne lève jamais une base occupée (`SqliteDataFileGuard.Closing` laisse le point de contrôle à la suivante)
  (ADR 029 révisée). Le test des deux moteurs a révélé que sur SQL Server une défaillance dans le nettoyage de la
  transaction, après le commit, faisait aussi rejouer l'unité : couvert par la même note du commit.
  `Sqlite/BusyWriteBackTests` (sonde du contrôleur), `RetryTests` (deux moteurs) ; chaque garde-fou retiré seul rougit
  son test.
- **Majeurs** : `PRAGMA secure_delete = ON` (ADR 035, `Purge/PurgedTraceTests`, rouge sans le réglage) ; le contrôle du
  démarrage SQLite tient le verrou d'écriture de SQLite et ne prend la ligne d'EF Core que si une migration est là ou si
  le fichier n'est pas inscriptible (ADR 041, `Schema/CheckLockTests`, rouge sans la redéfinition ; la régression trouvée
  en route, fichier en lecture seule plus refusé pour ses droits, est corrigée et prouvée par `SqliteStartupErrorTests`) ;
  trois tests nouveaux de la garde et de la restauration (agent, `HandCopyTests` ×2, `RestoreTests`, mutations M02,
  `EnsureSound`, `DamageAsync` rouges) ; couverture et plan corrigés (agent : parts hôte en P4-T05 / P4-T06, consignes
  d'exploitation en P7-T04, états, énoncés périmés, tableau de rattachement regénéré : 107 lignes A en phase 3).
- **Mineurs corrigés** : borne stricte de la purge prouvée (`TrashPurgeTests`, deux mutations `<=` rouges) ;
  `architecture.md` § 4 ; comptage de `couverture-p3/r3-b.md` ; ADR 029 (coût de la relecture du journal avec un
  lecteur long, règle de journalisation de `BackupResult` pour l'hôte), ADR 030 (écriture versionnée hors des projets
  verrouillés : question pour la phase 4).
- **Mineurs listés, non corrigés** (forme, sans effet observable) : Architecture m1 (`IsGenerated` dispense une migration
  au `Down` écrit à la main), m2 (duplications : `NoCheckpointOnClose`, test de lecture seule, contrôle d'un fichier
  trois fois, code 26 trois fois, cycle `Connection/` → `Backup/`), m3 (retour de `SqlitePragmas.Checkpoint` jamais lu,
  surcharge `ApplyAsync` servie aux seuls tests, `HasForeignJournal` public), m4 (contrôle d'intégrité synchrone dans
  l'ouverture asynchrone) ; Intégrité m1 (somme de contrôle des trames du journal non éprouvée) ; Fidélité m4 (type des
  paramètres de la recherche indexée fixé par aucun test, juste aujourd'hui) ; échec passager 4060 de
  `TestInfrastructureTests` sous charge (non reproduit).
- **Incident** : le contrôleur Intégrité a relié `web/node_modules` par une jonction ; vitest a écrit trois fichiers de
  cache dans le dépôt, supprimés aussitôt par lui, jonction retirée.

Vérification après les corrections du round 4 (chef de projet) : suite complète 3006/3006 (architecture 649, domaine
1301, persistance 1056), puis trois exécutions simultanées de la solution entière, chacune avec son dossier d'artefacts :
3006/3006 vertes chacune (17 à 18 min), 0 avertissement, aucune base `Topogenia_*` ni dossier `topogenia-*` restant.

**Porte de la phase 3 fermée après le round 4** (prompt 7.2 point 5) : le bloquant et les cinq majeurs du dernier round
sont corrigés et prouvés par des tests qui rougissent sans la correction ; aucun bloquant ne reste. Ces corrections n'ont
pas été relues par un cinquième round de contrôleurs (quatre rounds au plus) : la porte de la phase 4 les relira avec
le reste. Mineurs restants listés ci-dessus ; décisions en attente dans `docs/questions.md` (entrées « porte P3 r4 »).

## Phase 4 - Application, journal, hôte HTTP, sécurité

### P4-T01 - Gabarit de commande (24/10/2026)

- Fait : `Commands/` (`ProjectCommand<TResult>`, `CommandRunner`, `VersionCheck`, `CommandOutcome`, `IMapViewBuilder`),
  `Authorization/` (`RequiresRole`, `RequiresSheetFill`, `RequiresAdmin`, `AccessPolicy`), ports `ICurrentUser`,
  `IAccessReader`, `IOperationLog` révisé (séquence suivante, entrées d'un utilisateur, retrait par chaînes),
  `OperationLog/` (`ChangeSetJson`, `OperationStack`, `OperationRecorder`), `Maps/` (`MapView`, `MapViewBuilder`, sur
  le modèle de la `CarteVue` 1.x) ; adaptateurs `Persistence/Loading/AccessReader`, `Persistence/Writing/OperationLogStore`.
- Décisions : ADR 042 (ordre du gabarit : utilisateur, administrateur avant toute lecture, accès dans l'unité,
  404 non-membre, 403, administrateur en animateur, verrou, chargement, droit de remplir, `If-Match`, domaine,
  écriture, journal, vue ; l'identifiant de l'entrée est celui de l'opération ; journal par chaînes) ; question
  ADR 030 de la porte P3 tranchée (le gabarit ne charge qu'un projet, la garde supplémentaire n'est pas ajoutée).
- Écart corrigé en cours de route : la commande d'administrateur lisait l'accès avant de refuser un
  non-administrateur (test rouge, refus avancé avant toute lecture) ; `ChangeSetJson` écrivait encore la propriété
  calculée `Key` (test rouge, propriétés sans accesseur d'écriture retirées par leur `PropertyInfo`).
- Mutations : 9 jouées sur le gabarit, 9 tuées (contrôle `If-Match`, droit de remplir, verrou, identifiant d'opération,
  administrateur, état corbeille, vidage de la pile, profondeur, propriétés calculées).
- Tests : suite complète 3119/3119 (architecture 649, domaine 1301, persistance 1066 dont 10 du gabarit sur deux
  bases, application 103), 0 avertissement.

### P4-T03 - Annuler, rétablir, historique (24/10/2026, partie mécanique)

- Fait : `UndoCommand`, `RedoCommand` par le même gabarit (points d'extension `PrepareAsync`, `LogAsync`,
  `LogsRefusal` / `LogRefusalAsync`) ; refus `OPERATION_DEPASSEE` ou `CARTE_MODIFIEE` : unité annulée puis seconde
  unité qui retire les entrées de l'utilisateur sur la carte (AR-05) ; pile vide `INTROUVABLE` ; historique
  (`HistoryView`) dans chaque réponse et dans la lecture d'une carte ; genres `OperationKinds` (les 21 codes
  `historique.action.*` de la 1.x, S-4.6-02).
- Mutations : 8 jouées, 7 tuées, 1 équivalente (un refus de droit tombe avant le verrou) ; elle a fait ajouter le test
  du refus `CARTE_MODIFIEE`.
- Incident de méthode : une mutation restaurée par `mv` garde une date ancienne et la compilation suivante conserve
  l'assemblage muté (relevé sur un dossier d'artefacts séparé, 8 faux échecs) ; la procédure touche désormais le fichier
  restauré, et l'état restauré a été recompilé et revérifié.
- Reste : les lignes B-2.6-16 à 35 par opération (portées par les lots de P4-T02), le niveau API (P4-T04, P4-T08).

### P4-T02 - Gestionnaires (24/10/2026, en cours)

- Fait par le chef de projet : couloirs (`Lanes/LaneCommands`, annulables, B-2.6-33), lecture d'une carte
  (`Maps/MapQueries`, gabarit de lecture `Queries/ProjectQuery` et `QueryRunner`, sans verrou), catalogue des droits de
  toutes les commandes et lectures (`CommandCatalogTests`, table partielle par lot).
- Lancé : quatre agents d'exécution (lots A à D, `docs/consignes-agents-p4.md`).
- Retour des quatre lots (24/10/2026) : lot A 61 tests, 13 mutations tuées ; lot B 40 tests, 22 mutations tuées ; lot C
  49 tests d'application et 26 de persistance (deux bases), 18 mutations tuées, gabarit `UserRunner` pour les requêtes
  sans projet, ports `IUserDirectory`, `IAdministrators`, `IUserReader`, `IProjectReader` et adaptateurs
  `Persistence/Reading/` ; lot D 31 tests, 26 mutations tuées. Couverture des lots fusionnée dans `couverture.md`
  (110 lignes, niveaux déjà prouvés gardés).
- Points remontés par les lots, à traiter :
  - `TRANSFERT_HORS_PROJET` (A-2.8-26, AR-71) : le gabarit ne permet pas à une commande de savoir qu'une étape coupée
    appartient à un autre projet ; le transfert répond `INTROUVABLE` (P4-T04 : accès aux étapes sources) ;
  - rôle effectif inconnu d'une commande (une fiche ne peut pas être rendue selon sa visibilité après écriture) :
    l'hôte enchaîne la lecture, ou le gabarit transmet le rôle (P4-T04) ;
  - `If-Match` d'une fiche contrôlé dans `Execute` (la fiche est nommée par son étape) ; ordre version avant
    appartenance d'un document à son étape (`CONFLIT_VERSION` au lieu de `INTROUVABLE`) ;
  - suppression d'une étape avec recousage composée de deux appels du domaine (`SettleSubMap` puis `GroupDeletion`) ;
  - routes à nommer par l'hôte : glisser groupé, libellé du groupe de partage, dernier projet ouvert (AR-49) ;
  - audit (spec 5.5) : aucun événement construit, P4-T07 ; 428 `VERSION_REQUISE` du profil en mode Serveur : P4-T06.
- Vérification par le chef de projet après intégration : suite complète 3386/3386 (architecture 649, domaine 1301,
  persistance 1096, application 315, hôte 25), 0 avertissement, aucune base `Topogenia_Test_*` restante. P4-T02 fait.

### Retour du porteur du projet sur la maquette (24/10/2026)

- Textes de l'accueil remplacés à sa demande (AR-88) ; IA : pas d'IA intégrée en 2.0, suggestions par la structure du
  `.pmap` et des consignes documentées, extension `.pmap` gardée (AR-92).
- Suggestions d'ergonomie pesées et retenues pour la phase 6 : poignée de glissement du déroulé avec boutons gardés
  (AR-89), ruban repliable (AR-90), sections pliables de la fiche (AR-91) ; barre flottante verticale et accordéon
  exclusif écartés.
- Maquette reconstruite (`node scripts/build-mockup.mjs`, puis `capture-mockup.mjs`) : 10 captures, contrastes NF08 et
  WCAG 1.4.11 sans défaut, aucune ressource extérieure.

### P4-T04 - Hôte HTTP : routes, JSON, erreurs, OpenAPI (24/10/2026)

- Fait par le chef de projet (ADR 043) : composition (`Composition/` : options de la section du produit, adaptateurs de
  la persistance par requête, gestionnaires `*Api` trouvés par leur nom), JSON (`Json/` : snake_case, codes des listes
  fermées, `Optional<T>` des PATCH, refus d'un élément `null` dans une liste d'un corps, schéma OpenAPI d'un champ
  facultatif), erreurs (`Errors/ErrorMiddleware`, premier du pipeline ; message par ligne de `COMMANDE_ERREURS`),
  utilisateur de la requête (`Users/` : `SignInMiddleware`, `RequestUser`, administrateurs relus à chaque changement
  du fichier, annuaire vide en attendant les modes), lectures hors du domaine (`Api/ApiInput` : `If-Match`, champs
  requis, codes), réponse `StructureReply` ; routes du profil, des projets, des cartes, des couloirs, de l'annulation,
  de la création d'étape et de flèche ; tests de bout en bout (`Topogenia.Host.Tests` : hôte entier en mémoire sur une
  base migrée, SQLite et SQL Server, authentification de test par en-tête ; aides des bases de test de la persistance
  liées comme sources, `TestDatabase.ConnectionString` exposée pour cela).
- Quatre agents (`docs/consignes-agents-p4-api.md`) : lot A étapes, flèches, disposition, barre de commande, RACI,
  détailler, insertion, traductions (14 routes, 32 tests, 13 mutations tuées) ; lot B sélection et remontées (8 routes,
  20 tests, 14 mutations tuées) ; lot C fiches et documents (10 routes, 15 tests, 10 mutations tuées) ; lot D membres,
  corbeille, mise à la corbeille, traductions du projet (8 routes, 12 tests, 9 mutations tuées). Chaque test tourne
  sur les deux bases.
- Intégration : `TRANSFERT_HORS_PROJET` (AR-71) par un crochet du gabarit, `ProjectCommand.TranslateRefusalAsync`,
  appelé dans l'unité quand le domaine refuse ; `TransferBlockCommand` rend le code si une étape absente de l'espace
  appartient à un autre projet vivant que l'utilisateur peut lire, sinon `INTROUVABLE` (5.6-2) ; enveloppe 1.x de la
  liste des traductions rétablie (`project_id`, `project_code`, `scope`, `languages`, `texts`), qui donne aux colonnes du
  classeur les langues de la configuration ; tests de la table des routes et du document OpenAPI ; erreur interne
  prouvée au niveau HTTP. Mutations du chef de projet : 3 jouées, 2 tuées par les tests, 1 refusée à la compilation.
- Écarts à la 1.x, choisis et testés : `PUT /members` rend `MembersReply` (un animateur qui se retire reçoit son succès,
  la relecture du projet lui répondrait 404, A-2.1-21) ; import des traductions plus strict (`rows` absent, ligne sans
  `id` ou `line` : `CHAMP_OBLIGATOIRE`, A-2.18-07) ; `sub_maps` et `sub_map` lus comme des codes (`supprimer` 1.x refusé) ;
  ligne nulle d'un déroulé refusée `REQUETE_INVALIDE` (le client 1.x n'en envoyait pas) ; disposition avec `purpose`
  (`move` par défaut, `align`) ; réponses d'« affecter », « retirer » et « terminer » : `StructureReply` plus le cadre ou
  les rangements de la 1.x.
- Routes nommées par l'hôte : `PUT /api/me/last-project` (AR-49), `PUT /api/projects/{id}/sharing-group-label`
  (AR-64), `POST /api/maps/{id}/move` (AR-44). Reportées avec leur phase : `.pmap` et `.topogenia` (phase 5), santé
  (P4-T05, P4-T06), audit (P4-T07).
- Incident de méthode : deux agents partageaient un dossier de scripts ; le lot C a joué une fois les mutations du lot B
  pendant la série de celui-ci. Textes d'origine revérifiés, série du lot B rejouée par lui, suite complète verte.
  Les agents ont désormais chacun leur dossier d'outils.
- Reste mineur : l'aide `Dissolutions/DetailedRow.cs` écrit les documents du reliquat par la commande, faute de route au
  moment de son écriture (la route existe maintenant).
- Tests : suite complète 3596/3596 (architecture 649, domaine 1301, persistance 1096, application 315, hôte 235), puis
  le test de l'erreur interne (hôte 237), 0 avertissement. Couverture : 117 lignes fusionnées depuis
  `docs/couverture-p4/api-*.md`. P4-T04 fait.

### Décisions du porteur du projet sur les formats (24/10/2026)

- AR-93 : le `.pmap` 1.1 porte un bloc de fiche facultatif par étape (rubriques, déroulé, texte libre, documents,
  usages), importé en brouillon ; seule la 1.1 est lue (Ariane n'a jamais été publiée) ; identifiant `format` = nom du
  produit ; guide et prompt IA étendus à l'extraction des fiches d'un récit (P5-T04, S-7.5-02).
- AR-94 : pas de migration 1.x dans l'application (P5-T05 et P5-T06 retirées, ADR 023 retirée, lignes S-7.6 sans objet) ;
  le seul flux du porteur sera migré une fois, à sa demande, hors du dépôt.
- AR-95 : le panneau « récit -> schéma » avec IA intégrée (API Anthropic, AWS Bedrock) va dans la version 3.0,
  spécifiée dans `Specification/TOPOGENIA_3.0_SPECIFICATION_IA.md` ; la version en cours est terminée et validée d'abord.

### P4-T05 - Mode Poste (24/10/2026)

- Fait par le chef de projet (ADR 044) : configuration (`Mode`, Poste par défaut ; `Database:Provider`, renommé depuis
  `Engine` comme la spec 6.2 ; `Desktop:Folder`, `Port`, `DataFile`, `OpenBrowser` ; `settings.json` strict,
  `PARAMETRES_INVALIDES`), écoute Kestrel sur 127.0.0.1, jeton de lancement de 256 bits à usage unique échangé contre le
  cookie `desktop_session` (`HttpOnly`, `SameSite=Strict`), schéma d'authentification du compte du processus,
  administrateurs du mode Poste (le compte plus la configuration), politique par défaut « authentifié » pour toute route
  (A-1.1-02 : un anonyme reçoit un 401 brut, le test des règles communes est passé du JSON au 401 brut comme l'exige
  A-1.1-12), contrôles `Host`, `Origin`, `Sec-Fetch-Site`, démarrage par service hébergé (emplacement du fichier,
  `DesktopDatabaseStartup`, refus expliqués dans la console FR/EN, `Startup/messages.*.json`, code de sortie non nul),
  restauration proposée à la console (ADR 029), refus du mode Serveur avec SQLite, santé (`GET /api/health`), Quitter
  (`POST /api/desktop/quit`), fermeture du journal SQLite à l'arrêt, construction commune de l'application
  (`Composition/HostApplication`) pour l'exécutable et les tests.
- Un agent : journal en fichier (ADR 021, `Logging/`), 14 tests, 4 mutations tuées ; branché par le chef de projet dans
  le mode Poste (`logs\` du dossier du produit).
- Tests : les tests de l'API tournent désormais sur SQLite en mode Poste (démarrage, sauvegardes, contrôles d'en-têtes
  compris) et sur SQL Server en mode Serveur ; tests propres au mode Poste sur un vrai Kestrel (`Desktop/`) : 11 tests du
  serveur, 7 du démarrage, 35 cas de la restauration, des réglages et du catalogue des messages. Mutations du chef de
  projet : 6 jouées, 6 tuées (contrôle de `Host`, jeton réutilisable, cookie non strict, `Origin` non contrôlé, fermeture
  sautée, restauration offerte pour tout code).
- Incident de test corrigé : sous la charge de toute la suite, un hôte arrêté rendait encore sa dernière connexion au
  pool pendant que la base de test supprimait son dossier (`data.db` occupé) ; `TestDatabase.DisposeAsync` réessaie la
  suppression pendant 5 s au plus. La panne simulée de l'erreur interne passe de l'horloge (utilisée par le démarrage du
  mode Poste) au constructeur de vues (utilisé par les seules commandes).
- Reste pour d'autres tâches : `X-Requested-With` (P4-T07), mode Serveur et santé anonyme (P4-T06), nouveau jeton pour
  un second lancement (P7-T01), ouverture du navigateur et délai de démarrage (phase 7).
- Tests : suite complète 3665/3665 (architecture 649, domaine 1301, persistance 1096, application 315, hôte 304),
  0 avertissement, aucune base `Topogenia_Test_*` restante. Couverture : 26 lignes fusionnées depuis
  `docs/couverture-p4/poste.md`. P4-T05 fait.

### P4-T06 - Mode Serveur (24/10/2026)

- Fait par le chef de projet (ADR 045) : réglages `Server:AllowedHosts` et `Server:LogsFolder`, refus de démarrer sans
  SQL Server, sans chaîne de connexion ou sans liste de noms explicite (`HOTES_AUTORISES_INVALIDES`,
  `CHAINE_CONNEXION_ABSENTE`) ; contrôle de la base au démarrage sans migration (`Server/ServerDatabase` : `BASE_EN_RETARD`,
  codes du schéma avec messages propres au serveur `SERVEUR_*`, 18456 seul en connexion refusée, toute autre panne
  `ERREUR_INTERNE`, journal sans message du moteur) ; Negotiate ; santé anonyme `{status}` en mode Serveur seulement ;
  HSTS d'un an et redirection 308 ; garde commune aux deux modes (`Security/RequestGuard` : `Host`, `X-Requested-With`
  avancé de P4-T07, `Origin`, `Sec-Fetch-Site`) ; `If-Match` exigé en mode Serveur sur les 15 routes qui le lisent,
  refusé 428 par le gabarit après les droits (port `IVersionRequirement`), version 0 pour un enregistrement non lu ;
  statiques après l'autorisation (deux modes) ; annuaire Windows (`System.DirectoryServices.AccountManagement`, MIT,
  avis tiers à jour) ; journal du mode Serveur dans un dossier configurable ; `appsettings.json` sans `AllowedHosts: "*"`
  ni clé morte `Database:Engine`.
- Défauts trouvés en route : la santé détaillée sérialisait une propriété calculée `healthy` hors du contrat 1.x
  (`[JsonIgnore]`) ; l'en-tête de `docs/couverture-p4/poste.md` portait un ancien nom de fichier (test de format rouge).
- Trois agents, fichiers disjoints : les 35 tests du mode Serveur qui écrivaient sans `If-Match` envoient la version
  lue (dans `Steps/`, `Dissolutions/`, `Maps/`, `Sheets/`, `Documents/`) ; ceux qui prouvaient l'absence de contrôle la
  prouvent en mode Poste et prouvent le 428 en mode Serveur ; deux théories nouvelles sur la version 0 (fiche, usage).
  Aucun test affaibli ; résultats vérifiés par la suite complète. Point remonté par un agent et tranché : une seconde
  remontée d'une étape déjà remontée répond 409 en mode Serveur (version lue dépassée par la suppression), 404 en mode
  Poste sans `If-Match` (ADR 045).
- Tests nouveaux : `Server/ServerRunTests` (Kestrel réel, HTTPS par certificat fait pour l'exécution, Negotiate en NTLM
  sous le compte des tests : 11), `Server/ServerStartupTests` (18 cas), `Api/IfMatchRequirementTests` (6 cas),
  santé selon le mode, en-tête anti-CSRF du mode Poste, 3 tests de l'application (428 après les droits, version 0, profil).
  L'hôte en mémoire du mode Serveur passe en HTTPS sur `topogenia.test` ; son schéma Negotiate est servi par
  l'authentification des tests (le serveur en mémoire n'a pas la connexion que Negotiate lit).
- Mutations du chef de projet : 4 jouées, 4 tuées (428 du gabarit neutralisé, `*` accepté dans les noms, en-tête
  anti-CSRF non exigé, santé anonyme en mode Poste).
- Tests : suite complète 3720/3720 (architecture 649, domaine 1301, persistance 1096, application 318, hôte 356),
  0 avertissement, aucune base `Topogenia_Test_*` restante. Couverture : 24 lignes mises à jour dans `docs/couverture.md`
  (A-4-01, 03, 04, 05, 08, 10 à 13, 17, 19, 25, A-2.16-04, A-1.5-20 couvertes ; A-4-07 partielle, le test par point
  d'entrée et par rôle restant à P4-T08). P4-T06 fait.

### P4-T07 - Communs (24/10/2026)

- Fait par le chef de projet (ADR 046) : en-têtes de sécurité sur toute réponse des deux modes (CSP stricte,
  `X-Frame-Options`, `nosniff`, `no-referrer`, `Permissions-Policy`, COOP, CORP) ; limite du corps avant lecture
  (1 Mo par défaut, limite propre à une route par métadonnée, 413 `REQUETE_TROP_GROSSE` nouveau, flux borné pour un corps
  sans longueur annoncée) ; journal des requêtes par niveau (Error, Information pour les écritures de l'API, Debug), avec
  le patron de la route et l'identifiant de l'utilisateur, sans login ni chaîne de requête ; cache de connexion de 30 s
  oublié à l'écriture de l'utilisateur ; journal d'audit (`user.created`, `project.created`, `project.members_changed`,
  `project.trashed`, `project.restored`, `project.purged`, `access.repeated_refusals`) écrit dans l'unité du changement,
  port `IAuditStore` et son adaptateur, `GET /api/admin/audit` ; service quotidien de l'hôte (purge de la corbeille,
  A-2.15-19, branchée pour la première fois, et conservation de l'audit, 365 jours par défaut). La validation stricte
  (A-2.18) était déjà en place.
- Défauts trouvés en route : un corps sans longueur au-delà de la limite recevait un 413 vide (la lecture JSON des
  routes avale l'erreur du serveur) : flux borné qui lève le refus de l'API ; l'arrêt du mode Poste allait chercher le
  moteur SQLite dans des services déjà libérés (211 échecs de nettoyage des tests) : moteur capturé au démarrage.
- Garde-fous qui ont mordu : tri de l'audit par identifiant (ordre propre au moteur, règle `OrderParityTests`) remplacé
  par l'heure puis le genre ; nouveau code ajouté à la liste du contrat ; une valeur d'en-tête prise pour du SQL dans un
  test, remplacée par la constante.
- Tests nouveaux : 14 de l'application (audit, purge auditée, cache), 8 de la persistance (deux moteurs), 21 de l'hôte
  (en-têtes, tailles, journal des requêtes, audit, refus répétés, conservation).
- Mutations du chef de projet : 4 jouées, 4 tuées (audit du gabarit sauté, contrôle de la longueur annoncée retiré, CSP
  retirée, oubli du cache sauté).
- Tests : suite complète 3764/3764 (architecture 649, domaine 1302, persistance 1104, application 332, hôte 377),
  0 avertissement, aucune base `Topogenia_Test_*` restante. Couverture : A-4-14, 15, 16, 18, 23, 26, 28, 47, 49, 50,
  A-2.1-24, A-2.15-19 couvertes ; A-4-27 partielle (import, export, duplication : phase 5). P4-T07 fait.

### P4-T08 - Portage des tests d'intégration API 1.x et matrice des droits (24/10/2026)

- Fait par le chef de projet (ADR 047) : matrice des droits `Api/RightsMatrixTests` (58 routes x 6 rôles x 2 bases : anonyme
  401, non-membre 404, rôle insuffisant 403, droit accordé au rôle déclaré et à l'administrateur ; table comparée à celle
  des routes de l'hôte) ; `Api/ContractCodesTests` (codes du contrat sans test HTTP : `PROJET_CODE_CORBEILLE`,
  `COULOIR_SUPPRIME`, avertissements de disposition, raisons des lignes ignorées d'un classeur, barre de commande et code
  d'un autre projet) ; une trentaine de lignes A-1, A-3, A-4 de la couverture rattachées à leurs tests.
- Quatre agents, fichiers disjoints sous `tests/Topogenia.Host.Tests/Ported/` : chaque test 1.x de cible `Host.Tests`
  (195 lignes de `portage-tests.md`) renvoie à un test 2.0 existant (25), à un test nouveau (160 lignes, 165 théories, 330
  cas), ou à la phase 5 (10, `.pmap` et duplication) ; les renvois sont dans `docs/couverture-p4/portage-*.md`. Résultats
  vérifiés par la suite complète et une revue (aucun `Skip`, aucune assertion vide).
- Défauts du produit trouvés par le portage et corrigés : la fiche du reliquat d'une remontée n'était lisible par aucune
  route (la règle de visibilité du reliquat n'était appelée par personne) : `dissolution_sheet` dans la vue de la carte ;
  le refus `OPERATION_DEPASSEE` sans auteur donnait une phrase sans sujet : « quelqu'un » / « someone ».
- Garde-fous qui ont mordu : commentaires avec des libellés accentués (règle de l'anglais) dans quatre fichiers des
  agents ; table de la matrice de plus de 80 lignes, découpée par famille de routes.
- Mutation du chef de projet : droit de la suppression d'une flèche abaissé au lecteur : la matrice échoue sur les deux
  bases ; code remis.
- Tests : suite complète 4213/4213 (architecture 649, domaine 1302, persistance 1104, application 332, hôte 826),
  0 avertissement, aucune base `Topogenia_Test_*` restante. P4-T08 fait.

### P4-T09b - Décisions du porteur du projet (24/10/2026, soir)

- Le porteur a répondu sur la page des points en attente (`45_Topogenia/Travail/points_en_attente.html`) :
  - Q3 : Apache-2.0, avec un cœur fermé dont il garde la main ; Q-02 : aucun certificat, pas de signature payante ;
    Q-07 : CodeQL en local ;
  - E1, E6, E8 décidés par lui ; E2, E4, E5, E7 laissés à la construction ; E3 et les défauts B, C, D, F sans réponse.
- Écrit : ADR 048 (cœur fermé = `Topogenia.Domain` et ses tests, licence propriétaire, paquet obscurci depuis un dépôt
  privé, tâche P7-T04b), ADR 049 (ZIP non signé, MSIX retiré, CodeQL local), AR-96 à AR-101, `LICENSE` (texte Apache-2.0
  repris de `typescript/LICENSE.txt`), `NOTICE`, `LICENSE.md` du domaine et de ses tests ; la règle d'architecture des
  licences vérifie désormais leur présence. README, CONTRIBUTING, couverture S-P-02 et S-P-03, plan, questions.
- Code :
  - AR-96 : la restauration renomme le fichier remplacé et son journal à côté (`BackupFileNames.Refused`,
    `RestoreResult.KeptFile`) et les remet en place si la copie ne peut pas prendre la place ; la console prévient de la
    perte des modifications postérieures à la sauvegarde et nomme le fichier gardé ;
  - AR-98 : `FolderExposure` lit la liste de contrôle d'accès des dossiers des données et des sauvegardes ; avertissement
    à la console et au journal (par la place, jamais le chemin), démarrage continu ;
  - AR-100, AR-101 : dixième migration SQL Server `ReportViewsClassificationAndValidatedVersion` (`bi.project.classification`,
    `bi.step_sheet.validated_version`, colonnes à la fin, descente qui retire sa ligne d'historique).
- Premier passage : 3 échecs, tous dans les tests nouveaux (le fichier gardé contient aussi le travail des 20 projets
  du scénario `layoutChanged` ; le contrôle « aucun fichier partiel » voyait le fichier gardé et son journal ; le dossier
  de test refusé aux utilisateurs ne se supprimait plus) ; corrigés sans affaiblir la règle (le contrôle n'admet que le
  fichier gardé et son journal).
- Mutations du chef de projet : restauration qui supprime de nouveau les journaux, lecture des droits qui ignore les
  refus : `RestoreTests` et `FolderExposureTests` échouent ; code remis.
- Tests : suite complète 4222/4222 (architecture 649, domaine 1302, persistance 1108, application 332, hôte 831),
  0 avertissement. P4-T09b fait.

### P4-T09 - Ligne de commande `--migrate` et `--check` (24/10/2026)

- `Startup/HostCommands` (ADR 050) : l'application est construite sans démarrer le serveur ; codes 0, 1, 2.
  - `--migrate` : mode Serveur seul ; crée la base absente ; n'exige pas la liste des noms.
  - `--check` : vérifie les réglages et le contrôle du démarrage des deux modes, sans rien créer ni migrer ; refuse un
    fichier Poste tenu ouvert par l'application lancée (`DataFileUse.HeldOpen`, extrait de la restauration).
  - Réglage `Database:MigrationTimeoutMinutes` (1 à 1440, 10 par défaut), passé au moteur : le délai « réglable par
    l'hôte » de l'ADR 041 n'était branché nulle part.
- Mutations : garde du fichier ouvert retirée, liste des noms exigée par `--migrate` : `HostCommandsTests` échoue ;
  code remis.
- Tests : suite complète 4245/4245 (architecture 649, domaine 1302, persistance 1108, application 332, hôte 854),
  0 avertissement. P4-T09 fait ; porte P4-T10 lancée.

### Porte P4-T10 - round 1 (24-25/10/2026)

Cinq contrôleurs neufs (consignes `docs/consignes-porte-p4.md`), constats détaillés dans `Travail/porte-p4-r1-*.md`
(hors dépôt). Suites sous la charge des cinq : 15 à 34 échecs chacun, tous de création de base (1807 sur `model`) ou de
délai, tous verts au rejeu.

| Round | Contrôleurs | Bloquants | Majeurs | Mineurs | Suite |
|---|---|---|---|---|---|
| 1 | Fidélité ; Architecture ; Sécurité ; Données et portabilité ; Intégrité des tests (agents neufs) | 1 + 0 + 0 + 1 + 0 | 7 + 4 + 4 + 5 + 6 | 5 + 10 + 5 + 6 + 5 | Bloquants : le repli vers `index.html` (A-1.5-22) n'existait pas ; un rétablissement annulé de nouveau perdait sa place dans la pile (l'opération rejouée était la mauvaise, puis le refus vidait la pile). Majeurs : couverture de la phase 4 non tenue (356 lignes) ; If-Match non exigé sur 9 routes ; HTTP clair servi sans adresse HTTPS ; chemins et chaîne de requête dans les journaux (et le journal de Windows) ; `--check` qui ouvrait le fichier d'une application inactive et plantait sur un fichier endommagé ou en lecture seule ; lot d'une annulation purgé sans son opération ; refus de restauration affiché comme panne interne ; audit effacé par une conservation nulle ; section du produit de `appsettings.json` hors de D13 ; deux règles d'architecture contournables (routes, en-têtes de licence) ; matrice des droits sans vrai administrateur non membre et acceptant un 500 ; mutations survivantes (AR-96, AR-98, service quotidien, limite de 1 Mo, `--check` d'un fichier en retard) ; tests HTTP et tests portés manquants ; écarts au contrat 1.x (`VALEUR_INVALIDE` du champ, `sub_map_empty`) ; suite qui ne tient pas la charge. |

Corrections du round 1 :
- **Bloquants** (chef de projet) : place d'une opération dans la pile = séquence de sa racine (`OperationChain.Place`) ;
  repli `MapFallbackToFile("index.html")` après les routes de l'API. Tests : `UndoRedoTests` (rétablissement annulé de
  nouveau), `ServerRunTests` (adresse profonde, route d'API inconnue, fichier absent).
- **Majeurs, chef de projet** : lot d'une annulation ou d'un rétablissement rattaché à l'opération rejouée
  (`OperationContext.Replaying`) ; `HttpsOnly` (403 `HTTPS_EXIGE`) ; racine du contenu fixée au dossier de
  l'exécutable, fichier seul journal, catégories du cadre à partir des erreurs ; verrou d'instance du mode Poste
  (`DesktopInstance`) pris par le démarrage et par `--check`, `INSTANCE_DEJA_LANCEE` ; `--check` sans trace ni plantage ;
  refus de restauration lu par son code ; conservation de l'audit protégée ; langues par défaut dans le code, section du
  produit retirée de `appsettings.json` et règle D13 ; détecteurs de routes indépendants du récepteur ; règle des
  en-têtes de licence élargie ; `VALEUR_INVALIDE` avec le champ d'un JSON de mauvais type ; `sub_map_empty` faux sans
  sous-carte ; remise en place après une restauration ratée éprouvée (`SqliteRestore.MoveFile`) ; AR-98 éprouvé sur les
  trois groupes, le dossier des sauvegardes et la console ; créations de bases de test sérialisées entre processus
  (`ModelLock`) ; `--migrate` rejoué sur un refus passager ; délai des migrations réglable (P4-T09).
- **Majeurs, agents** : If-Match exigé sur 9 routes de plus et classement de toutes les routes d'écriture (agent
  If-Match, ADR 045 révisée) ; couverture de la phase 4 et `architecture.md` remises à jour (agent documentation : 394
  lignes couvertes, 43 partielles, 21 à faire, 29 lignes renvoyées en phase 5) ; tests de l'hôte (agent des tests :
  matrice, service quotidien, limite de 1 Mo, fenêtre des refus, conflits d'intégrité et d'unicité, santé 503, tests
  portés manquants).
- **Mineurs corrigés** : section `Kestrel` ignorée et clés de protection des données dans le dossier du produit (mode
  Poste) ; `--check` journalise les dossiers exposés ; job CodeQL désactivé (ADR 049) ; clé de détail `step_count` au lieu
  de `nb_etapes` ; message de `RESTAURATION_SAUVEGARDE_REFUSEE` juste ; ADR 044, 045, 046, 048, 050 révisées.
- **Mineurs listés, non corrigés** : ordre de lecture de l'audit à heure et genre égaux différent entre moteurs (Données
  m1) ; fichier restauré laissé en place si un dommage est vu après le déplacement, cas presque inaccessible (Données
  m3) ; ligne « Daily job audit failed » à l'arrêt d'un hôte de test (Données m5) ; code mort ou servi aux seuls tests
  (`DesktopPaths.SettingsFile`, `DesktopDataFile.DefaultPath`, `StepInsertion.ParsePosition`, `CounterService`...,
  Architecture m2) ; droit recopié dans `SelectionCommands` (m3) ; hôte qui ouvre un contexte pour la santé et
  `DesktopPaths` qui emploie `Microsoft.Data.Sqlite` (m4) ; règle de santé dans le délégué de la route (m5) ; types du
  mode Poste rangés dans `Startup/StartupService.cs`, clé `MIGRATE_MODE_POSTE` mélangée (m9).

Fin des corrections du round 1 (25/10/2026, après la reprise ; le porteur du projet a laissé ses points ouverts au
jugement de la construction : AR-102 à AR-109) :
- `VALEUR_INVALIDE` avec le champ restreint aux corps de `PATCH`, comme la 1.x (`CorpsPatch.Lire`) ; un POST ou un PUT
  de mauvaise forme reste `REQUETE_INVALIDE` (ADR 043 révisée) : les 4 échecs laissés par la correction Fidélité M3
  (`Api.CommonRulesTests`, `Ported.SheetsAndDocumentsPortedTests`, deux moteurs) ont disparu.
- `Touch` du projet par un changement de la liste des membres déplacé dans `MemberRules.Replace` (AR-104) ; décoche
  d'un usage déjà décoché réussie quelle que soit la version (`VersionCheck.OfErasure`, AR-107) ; ADR 045 révisée.
- Test de bout en bout de la purge à J+31 du lot d'une annulation, deux moteurs :
  `Maintenance.UndoBatchPurgeTests.The_batch_of_an_undo_is_purged_with_the_operation_undone_and_the_rest_of_the_stack_stays`.
- Mutations du chef de projet (`Travail/mutations-p4-r1.sh`, `mutations-p4-r1b.sh`) : 15 jouées, 15 tuées (dont M7 en
  deux formes, M10 sous une forme qui compile ; le premier script ne voyait pas un échec de compilation, le second le
  signale).
- Suite complète : 4347/4347, 0 avertissement (avant le test de purge, +2 depuis).
- Couverture (25/10/2026) : un agent documentaire a passé 12 lignes en `couvert` et noté le test à écrire pour 27
  lignes partielles ; quatre agents d'exécution (routes ; traductions ; hôte ; application et architecture) les ont
  écrits sur des fichiers disjoints, chacun éprouvé par mutation (toutes tuées), aucun défaut du produit trouvé hors
  A-6-18 : le message par ligne d'un classeur de traductions refusé (« L{n} : ... ») est construit par l'hôte comme en
  1.x (`ErrorResponses`, `ServiceTraductions.cs:124-128`). Deux règles d'architecture nouvelles : aucun code livré
  n'ouvre de connexion sortante (S-D14-01), aucun secret dans le code, la configuration publiée et le navigateur
  (S-5.1-04) ; le mot de passe écrit en clair du rôle d'application de `SqlServerAccessProbe` est désormais tiré au
  hasard à chaque exécution et la sonde n'est plus exemptée. A-4-40 : la part de l'hôte réel sous un compte membre de
  `topogenia_app` seul passe à la recette de P7-T02 (AR-110 ; ouvrir le fournisseur aux tests de l'hôte contredirait
  l'ADR 037). Restent partielles : 8 lignes de la phase 5.

### Porte P4-T10 - round 2 (25/10/2026)

Cinq contrôleurs neufs, comptes rendus dans `Travail/porte-p4-r2-<mandat>.md`. Corrections du round 1 confirmées : le
bloquant et la plupart des majeurs ; restes partiels sur la règle des routes (A1), la règle des licences (A4), la
couverture (Fidélité M1) et AR-98. Suites des contrôleurs : 4449 tests partout, 0 avertissement ; sous la charge des
cinq, `RealBusyTests` échoue sur sa borne de durée (deux contrôleurs), vert seul.

| Round | Contrôleurs | Bloquants | Majeurs | Mineurs | Suite |
|---|---|---|---|---|---|
| 2 | Fidélité ; Architecture ; Sécurité ; Données et portabilité ; Intégrité des tests (agents neufs) | 0 | 5 + 2 + 1 + 1 + 4 = 13 (11 distincts) | 6 + 6 + 3 + 3 + 2 = 20 | 4449 tests, 0 avertissement ; 4447/4449 sous charge (`RealBusyTests`, délai), 4449/4449 relancés. Majeurs : version d'un usage réutilisée après effacement (AR-107 trompé) ; panne de démarrage imprévue qui écrit chemins et pile à la console et au journal Windows ; règle des routes contournable ; règle des licences qui ne lit pas la racine ; couverture de phase 4 non close (11 lignes à faire, 24 lignes B prouvées par l'application seule) ; `redo` refusé non prouvé ; mesure NF05 absente ; avertissement AR-98 au démarrage non éprouvé ; 404 hors du compte des refus non éprouvé ; refus de `--migrate`/`--check` sans chaîne de connexion prouvé par le seul code de sortie ; `RealBusyTests` instable sous charge. |

Plan de correction : `../REPRISE_PORTE_P4.md`, section « Round 2 ». Mineurs : `docs/a-peaufiner.md` (F-05 et suivants).

#### Corrections du round 2 (25/10/2026)

Chef de projet (lots A et E) et quatre agents sur des fichiers disjoints (B démarrage, C règles d'architecture, D tests,
E-tests couverture), consignes `Travail/consignes-correctifs-p4-r2.md`.

- **R2-1** (version d'un usage recréé, AR-111) : au commit, un enregistrement dont la clé est faite d'autres
  identifiants (usage, membre, RACI, traduction : `IKeyReusedRecord`) prend à sa création la version de structure du
  projet, avancée par le verrou de chaque commande ; il ne reprend jamais une version de l'effacé.
  `ProjectWorkspace.StampCreated` ; tests `Domain.Tests.Workspace.KeyReusedVersionTests` (dont le relevé des types
  versionnés à clé composée), sonde du contrôleur en application (`IfMatchCommandsTests`) et en HTTP sur les deux moteurs
  (`IfMatchCoverageTests`) ; mutation tuée aux trois niveaux ; ADR 045 révisée. Quatre tests qui codaient la version 2
  d'un usage lisent maintenant la version de création (application, persistance).
- **R2-2, R2-8, R2-10** (lot B) : `catch` final de `Program.RunAsync` (`ERREUR_INTERNE` dans la langue de l'utilisateur,
  code 1, journal par type) ; dossiers du mode Poste créés dans le `try` (`DROITS_INSUFFISANTS`, sans chemin) ;
  avertissement AR-98 éprouvé au vrai démarrage ; sortie exacte de `--check` et `--migrate` sans chaîne de connexion ;
  4 mutations tuées ; ADR 044 révisée. Libellé du refus d'un dossier : registre F-07.
- **R2-3, R2-4** (lot C) : détecteur des routes sur symboles résolus (`RouteDetectors.cs`, un seul motif pour les deux
  règles, 21 échantillons positifs et 7 négatifs, mutation tuée) ; règle des en-têtes de licence étendue aux racines du
  dépôt et de `web/` ; formes de secrets et tube nommé ajoutés aux détecteurs (Architecture m1, m2) ; ADR 048 révisée.
- **R2-6, R2-9, R2-11** (lot D) : `redo` refusé qui vide les piles, application et HTTP deux moteurs (mutation
  `LogsRefusal => false` tuée) ; 404 d'un non-membre jamais comptés dans les refus répétés (mutation N5 tuée) ;
  `RealBusyTests` en collection non parallélisée, borne haute 4 x n x 6,15 s justifiée dans l'ADR 040 révisée, bornes
  basses inchangées.
- **R2-5** (lot E) : AR-112 (ligne B de niveau API dont la route ne fait que transmettre : couverte par le test
  d'application, câblage prouvé par la matrice des droits, la table des routes et la règle des dépendances) appliquée à
  25 lignes ; 10 lignes « à faire » prouvées par 18 tests (`Application.Tests.Coverage.PhaseFourCoverageTests`,
  `Host.Tests.Coverage.PhaseFourCoverageRoutesTests`) ; B-2.6-48 par le lot D ; 4 lignes à part de phase 4 prouvée
  passées en `partiel` (Fidélité m4) ; S-NF-09 renvoyée à P7-T02 (charge sur l'hôte publié).
- **R2-7, mesure NF05** : script `Travail/mesure-nf05/Nf05Measure.cs`, copié pour un passage dans `Host.Tests` puis
  retiré (pas de durée dans la suite, ADR 040). Hôte entier du mode Poste sur son fichier SQLite, en mémoire, carte de
  150 étapes (5 couloirs de 30), 20 commandes de structure, machine sans autre charge. Trois passages : médiane
  24,4 / 19,3 / 21,6 ms ; 95e centile 28,0 / 20,3 / 23,8 ms ; maximum 28,5 ms. Cible 300 ms tenue avec une marge de 10.
  S-NF-07 `couvert (mesure)`.
- **Mineurs de documentation** (§ 0.5) : ADR 042 révisée (place d'une chaîne) et `architecture.md` ; AR-108 dans
  A-3-61, lignes A-1.5-18, A-2.15-04, A-3-52, A-3-59 en `écart (AR-108)` ; parts client d'AR-106 et AR-108 dans P6-T02
  et P6-T04 ; S-5.4-13 (règle des connexions sortantes citée, reste au registre F-06) ; S-5.1-04, S-D13-01, A-6-03.

Couverture de la phase 4 après corrections (528 lignes) : 437 `couvert`, 45 `couvert (API)`, 26 `couvert (API, AR-112)`,
1 `couvert (application)` (A-4-06), 1 `couvert (mesure)`, 4 `écart`, 1 `sans objet`, 12 `partiel` renvoyées à la phase 5
(registre F-02) et S-5.4-13 `partiel` (F-06), 0 `à faire`.

Suite complète seule (25/10/2026, après corrections) : **4512/4512**, 0 avertissement, 11 min 15 s, aucune base de
test restante (`Travail/suite-p4-r2.log`).

### Porte P4-T10 - round 3 (25/10/2026, mandat resserré)

Cinq contrôleurs neufs, mandat resserré (`consignes-porte-p4.md`, « Round 3 et suivants »), comptes rendus
`Travail/porte-p4-r3-<mandat>.md`. Corrections du round 2 confirmées par chacun, mutations du round 2 rejouées et tuées ;
AR-111 éprouvé dans tous les chemins (annulation, rétablissement, reliquat, création de projet, import) ; AR-112 jugé
tenable (lecture des classes appelées par les 26 routes) ; mesure NF05 rejouée sous charge (médianes 68 à 182 ms).

| Round | Contrôleurs | Bloquants | Majeurs | Mineurs | Suite |
|---|---|---|---|---|---|
| 3 | Fidélité ; Architecture ; Sécurité ; Données et portabilité ; Intégrité des tests (agents neufs) | 0 | 0 + 1 + 0 + 0 + 0 = 1 | 6 + 2 + 0 + 1 + 2 = 11 | Intégrité : suite complète rejouée sous la charge des tests ciblés, 4512/4512, 0 avertissement, aucune base restante. Majeur : la règle des routes exemptait tout fichier `Composition/HostPipeline*` (une route posée là passait). |

Correction du majeur (chef de projet) : `RouteDetectors.AllowedPlaces` réduit à `Host/Endpoints/` ; un appel à une méthode
de l'espace de noms `Topogenia.Host.Endpoints` (`MapApi`) n'est pas une route ; échantillons posés à `HostPipeline.cs`,
`HostPipelineLeak.cs` et `HostPipeline/Leak.cs`, et à une méthode de passage hors de cet espace de noms ; témoin
`QualityRulesTests` : `HostPipeline.cs` n'est plus vu ; deux mutations (exemption remise, exception de l'espace de noms
neutralisée) tuées ; ADR 043 révisée (le cas des intergiciels écrit, Architecture m1). Architecture.Tests 754/754.
Mineurs de documentation faits : `architecture.md` (AR-111, Architecture m2), limite d'AR-112 écrite (Fidélité m2),
B-2.9-34 (m3), B-2.9-41 en `couvert (API)` (m5) ; les autres au registre (F-08, F-09).

Incident du contrôleur Sécurité : une sonde lancée sans arguments a démarré l'exécutable en mode Poste par défaut et créé
`%LOCALAPPDATA%\Topogenia\` (base vide, sauvegarde, clé, tous datés du lancement, rien d'antérieur) ; processus arrêté
par son numéro, dossier supprimé par le chef de projet après vérification.

Round 4 (le dernier, prompt 7.2) : mandat Architecture seul (règle 1 de `a-peaufiner.md`).

### Porte P4-T10 - round 4 (25/10/2026) : porte franchie

Mandat Architecture seul (seul mandat qui gardait un majeur au round 3), contrôleur neuf, compte rendu
`Travail/porte-p4-r4-architecture.md`. A1 corrigé : mutations N1 et N2 du round 3 prises ; les deux mutations du
détecteur tuées ; une méthode de l'espace de noms `Endpoints` déclarée hors du dossier est arrêtée par la construction
(IDE0130 en erreur) et par la règle. Architecture.Tests 754/754, 0 avertissement.

| Round | Contrôleurs | Bloquants | Majeurs | Mineurs | Suite |
|---|---|---|---|---|---|
| 4 | Architecture (agent neuf) | 0 | 0 | 1 | Architecture 754/754 ; dernière suite complète 4512/4512 (chef de projet seul, rejouée par Intégrité au round 3), plus les 4 tests d'architecture du round 3, soit 4516, 0 avertissement |

Mineur : un relais générique déclaré dans `Endpoints/` pourrait poser une route depuis un autre fichier ; aucun n'existe ;
phrase de l'ADR 043 nuancée, règle au registre (F-10).

**Porte P4-T10 franchie le 25/10/2026** : le round 3 n'a relevé de majeur que pour l'Architecture, et le round 4 n'en
relève aucun ; tout est vert sur les deux bases avec 0 avertissement. Phase 4 terminée ; phase 5 à suivre.

## Phase 5 - Échange

### P5-T01 - Format du paquet `.topogenia` (25/10/2026)

Fait : le format 2.0 du paquet dans `src/Topogenia.Interchange` (ADR 051) : `Packages/` (portée de l'export, une entrée
par fichier, personnes, manifeste et SHA-256, ZIP), `Json/CanonicalJson` (écriture déterministe), `Schemas/` (schéma
publié `schemas/topogenia-package-2.0.schema.json`, embarqué, et son validateur) ; `docs/format-topogenia.md` ; paquet
fictif `samples/conges.topogenia`, export du projet de démonstration des tests (`SampleProject`, projet « Demande de
congés » qui emploie chaque enregistrement exporté, et la corbeille, une remontée fermée et le journal qui ne le sont pas).

Décisions (ADR 051) : aucun identifiant de la base dans le paquet, les objets se désignent par leurs codes (condition de
l'aller-retour exact de la 7.4, l'import donnant de nouveaux identifiants) ; clés en camelCase comme le manifeste de la
spec ; `schemaVersion` = version du schéma publié ; remontée ouverte exportée avec son étape dissoute, sans les cases
d'origine du détail ; export sans les personnes = aucun login nulle part ; entrées du ZIP datées du 01/01/1980, seule
exception à D08 (`ZipEntryTime`, règle `Production_code_uses_utc_dates_only` révisée) ; noms techniques du format figés
(`nameof(Topogenia)`, ADR 013 révisée). Le validateur de schéma est écrit dans la solution pour le sous-ensemble employé :
JsonSchema.Net est sous MIT jusqu'à la 8.0.5 (28/12/2025) puis sous une licence à accepter à partir de la 9.0.

Écarts à la spec : aucun. Lignes : S-7.1-02, S-7.2-01 à 14 couvertes (S-7.2-11 par revue et tests), S-5.4-11 `partiel`
(le contrôle à l'import vient avec P5-T02).

Tests : Interchange.Tests 121 (archive et manifeste, chaque entrée, portée et personnes, déterminisme, schéma et
validateur, paquet d'exemple) ; trois mutations tuées (tri des clés retiré : 11 rouges ; remontée fermée exportée : 3 ;
option des personnes ignorée : 3). Suite complète seule `Travail/suite-p5-t01.log` : **4637/4637**, 0 avertissement.

### P5-T01b - Export par l'API (25/10/2026)

Fait : `POST /api/projects/{id}/export` (corps `{without_people}`, exigé), réponse `application/zip` nommée
`CODE_Nom-court_aaaa-mm-jj.topogenia` (règle 1.x `Codes.NomDossier`, T-UDOM-04 porté sur des libellés fictifs,
`Interchange/Files/ExportFileName`, jour UTC) ; port `IPackageExporter` (adaptateur `Interchange/Packages/PackageExporter`) ;
`Application/Exchange/PackageExport` : lecture du projet par le gabarit des requêtes (`ExportPackageQuery`, animateur
seul, AR-113), logins des auteurs lus seulement avec les personnes (`IUserReader.FindManyAsync`, nouveau, par paquets de
500), événement d'audit `project.exported` (détails `without_people`) écrit avant de rendre le paquet (spec 5.5).

Tests : Application 7 (droits, logins, audit, corbeille), Persistence 2 (lecture par identifiants au-delà de la limite des
paramètres, deux moteurs), Host 8 (route sur les deux moteurs ; matrice des droits et table des routes complétées),
Interchange 8 (nom du fichier). Lignes : S-3.6-06, S-5.5-02 couvertes ; A-1.2-16 `partiel` (la route `.pmap`, P5-T04).


Correction de P5-T01b (reprise du 25/10/2026) : la suite complète de fin de P5-T01b (`Travail/suite-p5-t01b.log`, relancée
à la reprise, le journal laissé avant le clear n'ayant que la compilation) comptait **4 rouges sur 4664** : la requête
`ExportPackageQuery` absente du catalogue des droits (`CommandCatalogTests`, 2) et la route d'export absente des dispenses
d'`If-Match` (`IfMatchRequirementTests`, deux bases). Corrigés : `ExportPackageQuery` = animateur au catalogue ; dispense
« lecture du projet entier, n'écrit que son événement d'audit ».

### P5-T02 - Import du paquet (25/10/2026)

Fait (ADR 052) : `Interchange/Packages/PackageReader` (garde-fous dans l'ordre de l'ADR : archive, 8 entrées de noms connus,
chemins, manifeste de 1 Mo, format et version, schéma du manifeste, entrées annoncées, tailles lues en flux contre la
taille annoncée, 200 Mo par entrée et 500 Mo en tout, SHA-256, JSON, schéma de chaque entrée ; limites en paramètre,
`PackageLimits`), `PackageBuild` (reconstruction sous de nouveaux identifiants, références par code résolues, préfixe
réécrit, auteurs par login, remontée ouverte remise dans un lot neuf), `PackageImporter` (adaptateur du port
`IPackageImporter`, reconstruction d'essai à la lecture pour les comptes et les références) ; domaine :
`ReadableCode.WithProject`, `ProjectCodeRules.TrashCode`, `ProjectReplacement.Retire`, `ImportedProjects` (importateur
animateur, détenteurs non membres libérés, règles de la structure) ; application : `Exchange/PackageImport` (aperçu et
import en nouveau projet, administrateur ; aperçu et remplacement, animateur du projet visé, `If-Match` et verrou du
projet par `UserRequest.LocksTarget`), `ImportPreparation` (utilisateurs des logins, créés seulement s'ils sont nommés),
audit `project.imported` ; `IProjectReader.CodesStartingWithAsync` ; hôte : quatre routes, corps binaire de 100 Mo au plus,
options `with_members` et `code` dans la chaîne de requête ; codes `PAQUET_INVALIDE` et `PAQUET_TROP_RECENT` (messages
français et anglais) ; `docs/format-topogenia.md` section 9.

Décisions (ADR 052, inscrites dans `questions.md`) : le projet remplacé prend un code libre dérivé du sien (`VTE` devient
`VTEA`) et tous ses codes lisibles sont réécrits, sans migration ; l'aperçu a le droit de l'import qu'il prépare ; les
auteurs inconnus sont créés comme des membres invités ; RACI, fiches et documents masqués acceptés (ils existent en 1.x).

Écarts à la spec : le « suffixe » du code devient un code dérivé de 3 à 6 lettres (ADR 052, section 1). Lignes : S-5.4-05 à
12, S-7.3-01, 02, 04 à 11, 13, S-7.4-04, 05 couvertes ; S-7.4-01 `partiel` (le projet de toutes les fonctions par l'API sur
les deux bases et le croisé restent à P5-T07) ; S-7.3-03 (interface, phase 6) et S-7.3-12 (P5-T03) à faire.

Tests : Domain 25 (code dérivé 7, `WithProject` 6, réécriture 4, règles du projet importé 8) ; Interchange 55 (garde-fous,
aller-retour exact avec et sans les personnes sur le projet de toutes les fonctions, références) ; Application 18 ; Host
16 sur les deux bases (aller-retour par l'API, corbeille et restauration, nouveau projet, aperçu, refus, 413) et la
matrice des droits pour les quatre routes. Douze mutations, toutes tuées (empreinte, chemin dangereux, limite totale,
entrée courte, drapeau des personnes, borne des compteurs, préfixe, cases d'origine, auteur de création, irritants non
réécrits, assignés non membres, code propre non sauté) ; le mutant du préfixe survivait d'abord (le test refusait pour une
autre référence) : test refait sur un irritant que rien ne référence. Suite complète seule `Travail/suite-p5-t02.log` :
**4786/4786**, 0 avertissement ; puis un test ajouté (membre cité deux fois) et un refait : Interchange 184/184,
Architecture 754/754.

### Défauts de P5-T02 revus (25/10/2026, à la demande du porteur : « corrige les défauts »)

Les trois défauts de l'ADR 052 en attente sont revus du point de vue de l'utilisateur (AR-114 à AR-116). Un vrai défaut
trouvé à la revue et corrigé (AR-115) : un remplacement par un paquet qui n'apporte pas de membres (export sans les
personnes, le choix par défaut, ou option `with_members` décochée) laissait l'importateur seul membre, l'équipe perdait
l'accès au projet. Le projet importé garde désormais les membres du projet remplacé (personnes, rôles, marques, ordre ;
`ImportedProjects.KeepMembers`, appelé avant `Settle`), l'aperçu les compte ; un paquet dont les membres sont repris les
remplace, comme le dit la spec 7.3. Gardés après examen : le code dérivé du projet remplacé (AR-114 ; un échange des
codes à la restauration réattribuerait les codes lisibles, D07) et les droits de l'aperçu (AR-116). Tests : Domain 2,
Application 2, Host 1 sur les deux moteurs ; deux mutations tuées (appel retiré, condition inversée). Couverture : S-7.3-08.

### P5-T03 - Montée de version des paquets (25/10/2026)

Fait (ADR 053) : `PackageDefinition` (une version du format : entrées et schéma, la courante tirée de `PackageFormat` et
du schéma 2.0), `IPackageConverter` et `PackageChain` (contrôlée à la construction ; `PackageChain.Application` vide, la
2.0 étant le premier format) ; `PackageReader` lit un paquet ancien contre le schéma de son format (noms d'entrées admis :
union des formats de la chaîne, puis vérification exacte contre le format du paquet), vérifie tailles et empreintes des
octets reçus, puis applique chaque maillon : version du manifeste avancée, entrées annoncées à nouveau, tout vérifié contre
le schéma du format suivant. Une entrée connue d'un autre format seulement est maintenant refusée dans un paquet 2.0
(`entries`). `PackageImporter(limits, chain)`, le constructeur par défaut inchangé pour l'application.

Preuve par deux formats fictifs, jamais livrés (1.9 : projet nommé `title`, pas de traductions ; 1.8 : 1.9 avec
`irritants.json`) : `PackageConversionTests`, 9 tests (reconstruction identique à celle du paquet 2.0, deux maillons,
schéma de l'ancien format, sortie fautive refusée, empreintes et entrées de l'ancien format, entrée d'un autre format,
version non convertie, chaînes refusées, chaîne vide de l'application). Cinq mutations tuées ; le premier test du
convertisseur fautif laissait survivre le retrait du contrôle de sortie (la reconstruction refusait pour une autre
raison) : refait avec une propriété que seul le schéma refuse. Interchange 193/193. Couverture : S-7.3-12 couverte.
Suites complètes seules : état AR-115 **4792/4792** (`Travail/suite-ar115.log`), état P5-T03 **4801/4801**
(`Travail/suite-p5-t03.log`), 0 avertissement.

### P5-T04 - Format `.pmap` 1.1 (25/10/2026)

Fait (ADR 054) : `src/Topogenia.Interchange/Pmap/` : modèle, lecteur (`PmapReader`, portage de `FormatPmap.Lire` : nettoyage
du copier-coller, chaque règle de l'annexe A 2.17, fautes `{path, code, values}` au lieu des textes en dur), écrivain
(`PmapWriter`, ordre du format, valeurs par défaut omises), lecture d'un projet (`PmapOutline` : codes comme identifiants,
ordre de lecture, RACI visible, sous-cartes vides omises, coupe à 12 niveaux signalée, traductions hors langue d'origine,
fiches sur option telles que le demandeur les voit), construction (`PmapBuild` : `ProjectCreation`, `StructureEdits`,
`RaciRules`, `TranslationRules`, `SheetWorkflow`, `DocumentEdits`, `DocumentUses`, puis `ImportedProjects.Violations`) et
adaptateur `PmapFiles` du port `IPmapFormat`. Nouveautés 1.1 : traductions des étapes et des flèches, bloc `sheet` par
étape (AR-93 : rubriques, déroulé, texte libre, sorties, entrées, usages des sorties d'autres étapes de la carte par leur
identifiant, irritant sur l'étape productrice), importé en brouillon, contrôlé par les règles de l'API ; refus d'un RACI,
d'une sous-carte ou d'une fiche sur un début, une fin, un ET ou un OU inclusif. Domaine : `FileError`, `FileErrorCodes`
(48 codes `PMAP_*`, liste `ErrorCodes.FileFaults`, à part des codes de refus du contrat). Application : `PmapCopyQuery` et `PmapCopyExport` (lecteur ; audit
`project.exported` au format `pmap`), `OpenPmapCommand` (administrateur ; 8 M caractères ; audit `project.imported`, mode
`pmap`), `DuplicateProjectCommand` (administrateur, source lue ; audit `project.duplicated`). Hôte : `PmapApi`, routes
`GET /api/projects/{id}/pmap?with_sheets=`, `POST /api/projects/import-pmap` (10 Mo avant lecture),
`POST /api/projects/{id}/duplicate` ; rendu des fautes de fichier dans `ErrorResponses` ; 48 messages français et anglais.
Documents : `docs/format-pmap.md`, `schemas/pmap-1.1.schema.json`, `docs/pmap/PMAP_GUIDE_IA.md` (fiches tirées d'un récit,
récit converti), `docs/pmap/PROMPT_croquis_vers_pmap.md` ; exemples `samples/minimal.pmap`, `samples/cloture-mensuelle.pmap`
(fictifs : les sites et logiciels réels des exemples 1.x sont remplacés).

Constats en cours de route : une fiche qui ne porte que des usages ne crée pas d'enregistrement de fiche (règle du
domaine), le compte de la réponse compte donc les blocs écrits ; le corps de la suppression d'une sélection est
`{steps, stitch}` (une propriété inconnue du corps est ignorée par l'hôte : relevé pour la porte P5-T08, registre
`a-peaufiner.md`).

Tests : Interchange 47 nouveaux (lecteur 20, fiche 7, construction 7, copie 9, documents 4), Application 8, Host 15
théories sur les deux bases (`PmapRoutesTests` 6, `PmapFilesPortedTests` 9 : T-FICR-01 à 09, T-BRAN-05, 1.x
`FormatPmapTests` portés dans les classes de l'échange, registre `portage-tests.md`) ; tables des routes, des droits, des
dispenses d'`If-Match` et catalogues complétés. Douze mutations tuées (trois réécrites : la première forme ne compilait
pas, code mort refusé par l'analyseur). Couverture : A-2.17-01 à 33, 38 à 49, S-7.5-01, 02, A-1.2-15 à 22, A-0-02,
A-2.2-10, A-3-47, 48, 72, A-4-09, 21, 22, 24, 27, 48, 54, A-6-19, 32, 45, S-3.1-17, S-5.5-03 couvertes ; B-1.6-23,
B-2.15-01, 02 couvertes au niveau API ; S-3.6-07, 08 et S-5.4-24 (faites en P5-T01b et P5-T02, non reportées) inscrites ;
S-P-17 sans objet (AR-94) ; S-3.1-13 partiel (allers-retours de P5-T07).

Suite complète seule : **4890/4892** (`Travail/suite-p5-t04.log`). Deux rouges, corrigés : `ErrorCodeValuesTests` (la liste
des codes du contrat recevait les 48 codes de fautes de fichier : ils ont maintenant leur liste, `ErrorCodes.FileFaults`,
que le test des messages de l'hôte vérifie avec celle des refus) et `Csharp_identifiers_are_english` (une variable
`valider` dans `PmapCopyTests`). Après correction : Domain 1337/1337, Architecture 754/754, Interchange 240/240, hôte
(messages, routes et tests portés du `.pmap`) 40/40.

### P5-T07 - Allers-retours, paquets hostiles, ligne de commande (25/10/2026)

Fait (ADR 055) : l'exemple livré `samples/conges.topogenia` sert de projet de toutes les fonctions ; il est complété des
losanges ET et OU inclusif, d'une attente et d'une automatisation en cours (carte détaillée), et vérifié par
`SampleCoverageTests`. Allers-retours par l'API (`RoundTripTests`) : exact sur chaque base, sans puis avec les personnes,
par remplacement (le code reste celui du projet) ; croisé SQLite -> SQL Server -> SQLite. Paquets hostiles par l'API
(`HostilePackageTests`, douze paquets sur les deux bases, aperçu et import refusés, rien d'écrit). Ligne de commande
`--export <code> <fichier> --with-people|--without-people` et `--import <fichier> --with-members|--without-members
[--code CODE]` (`Startup/ExchangeCommands.cs`) : sous les droits du compte qui la lance, connecté comme à une première
requête ; mode Poste : fichier présent, non tenu, à jour ; mode Serveur : contrôle du démarrage ; écriture du fichier
entière ou nulle, jamais d'écrasement ; dix messages français et anglais de la console.

Deux défauts trouvés par les allers-retours à travers les vraies bases (l'aller-retour en mémoire de P5-T02 passait),
corrigés : (1) `WritePlan` écrivait un lot de suppression créé avant son projet et les utilisateurs qu'il nomme quand ils
sont créés dans le même jeu de changements (import d'une remontée ouverte : `FK_deletion_batch_project`) ; (2) un document
de sortie avec un temps d'attente (l'exemple en portait un) passait l'import et la base le refusait en `CONFLIT_INTEGRITE`
au lieu de `PAQUET_INVALIDE` : `ImportedProjects.Violations` refuse `output-wait` et `document-link`, l'exemple est
corrigé. Les tests de l'hôte lisent le dépôt au chemin enregistré à la construction (comme ceux de l'échange) : construits
dans un autre dossier, ils ne le trouvaient pas, ce qui faussait une première série de mutations (relancée).

Tests : Host 3 (allers-retours) + 24 (paquets hostiles) + 12 (commandes, dont 9 formes fautives), Persistence 2 (ordre
d'écriture), Domain 1 (cohérence des documents), Interchange 2 (couverture de l'exemple) ; tests de l'échange recalés sur
l'exemple complété (compteur 23, 19 étapes vivantes et 20 flèches, attente sur l'entrée). Trois mutations tuées.
Couverture : S-6.3-01, 02, S-7.4-01 à 03, S-11-01, S-11-08, S-3.1-13 couvertes ; plus aucune ligne de phase 5 à faire.
Suite complète seule : **4946/4946**, 0 avertissement (`Travail/suite-p5-t07.log`).

### Porte P5-T08 - round 1 (25/10/2026)

Cinq contrôleurs neufs (`Travail/porte-p5-r1-<mandat>.md`), chacun avec la suite complète rejouée sous la charge des
autres : **4946/4946** partout, 0 avertissement. Constats : Fidélité 0 bloquant, 5 majeurs, 7 mineurs ; Architecture 0, 2,
6 ; Sécurité 1, 2, 5 ; Données 0, 3, 5 ; Intégrité 0, 3, 5. Fond commun : l'import du paquet recopie des valeurs que les
règles du domaine ne revoient jamais (textes en unités UTF-16, adresses des documents, contenu figé des versions, logins,
doublons de clés, champ et longueur des traductions, irritant d'un seul usage, commentaire d'historique), d'où des refus
409 ou 500 après un aperçu à 200, un écart SQLite / SQL Server sur les textes hors du plan de base, et un contenu faux
servi aux lecteurs (bloquant Sécurité B-1). Autres majeurs : classification changée par un remplacement, paquets
malformés en 500 (clé en double, nombre démesuré), contrat OpenAPI des routes d'échange, coupe à 12 niveaux muette à la
duplication et non testée à la copie, couleurs des couloirs, ordre de lecture et identifiant entouré d'espaces non
prouvés, garde-fous de doublons non testés, lecture arrêtée à la taille annoncée non prouvée, part `.pmap` d'A-6-25.

Plan des corrections : lot A, contrôle par le domaine de chaque enregistrement reconstruit avant écriture
(`ImportedRecords`) et classification gardée au remplacement ; lot B, robustesse du lecteur (clé en double, nombres) et
détail de la limite atteinte ; lot C, contrat OpenAPI ; lot D, `.pmap` (coupe signalée à la duplication, tests de
l'en-tête, des couleurs, de l'ordre de lecture, des identifiants, nom rogné) ; lot E, documents (couverture : colonnes
Phase perdues par la mise à jour de P5-T04, à rétablir ; ADR 055 ; `architecture.md` ; comptes du journal) ; mineurs au
registre `a-peaufiner.md`.

### Porte P5-T08 - corrections du round 1 (25/10/2026)

Détail constat par constat : `Travail/porte-p5-r1-corrections.md`.

- Lot A : `ImportedRecords` revoit chaque valeur reconstruite par la règle de sa saisie avant toute écriture (textes en
  unités UTF-16, couleurs, adresses, nombres arrondis et bornés, déroulé, contenu figé des versions, traductions,
  commentaire d'historique, irritant d'un seul usage) ; logins contrôlés dès la lecture ; raison `values`. AR-117 :
  classification du projet remplacé gardée. Doublons de RACI et d'objets d'une remontée refusés. `SheetJson.TryThaw`
  (le JSON du domaine reste dans `SheetJson`, liste d'exceptions du domaine étendue à `JsonException` pour ce fichier ;
  la règle des lectures des fiches nomme `ImportedRecords.Sheets`, qui lit les valeurs de toutes les fiches).
- Lot B : clé en double refusée (`json`), nombre hors de la plage décimale au-delà de toute borne (`schema`) ; preuve de
  l'arrêt de la lecture à la taille annoncée (données abîmées au-delà : `size`, témoin `archive`).
- Lot C : `Accepts`/`Produces` des routes d'échange, test du document OpenAPI.
- Lot D : AR-118 (`IPmapFormat.Model`, duplication de tous les niveaux) ; en-tête `X-Pmap-Warning` testé ; couleurs,
  ordre de lecture et identifiants prouvés ; nom saisi rogné ; A-6-25 (nombre de requêtes de l'ouverture).
- Lot E : colonne Phase de 56 lignes de la couverture, états et textes de cinq lignes, ADR 052, 054, 055, format du
  paquet, `architecture.md`, portage de T-FICR-02, 04, 06 ; mineurs Architecture m3, m4 faits ; registre F-12 à F-16.
- L'exemple `conges.topogenia` est régénéré : sa version validée n° 2, écrite à la main, n'était pas une forme figée.
- Comptes (Intégrité m3) : P5-T01 avait trois mutations, absentes de `etat.md` ; `ExchangeCommandsTests` comptait 13
  tests, non 12 ; la suite était passée de 4892 à 4946 (+54) et non +44. Après les corrections : 5020 (+74).
- Première suite après corrections : 5016/5020, quatre règles d'architecture rouges (lecture des fiches hors de leur
  point unique, `JsonException` dans le domaine, méthode de test de plus de 80 lignes, `Guid` aléatoire) ; corrigées,
  seconde suite seule **5020/5020**, 0 avertissement. 17 mutations des corrections, toutes tuées.


### Porte P5-T08 - round 2 et ses corrections (25 et 27/10/2026)

Cinq contrôleurs neufs, mandat complet. Architecture et Sécurité ont rendu le 25/10 ; Fidélité, Données et Intégrité,
interrompus par l'arrêt de la session, ont été relancés neufs le 27/10 avec le même brief (aucune base ni trace laissée,
dépôt inchangé).

| Round | Contrôleurs | Bloquants | Majeurs | Mineurs | Tests |
|---|---|---|---|---|---|
| 2 | Fidélité ; Architecture ; Sécurité ; Données ; Intégrité (agents neufs) | 0 | 1 + 1 + 1 + 2 + 1 = 6 (3 distincts) | 4 + 5 + 4 + 2 + 3 = 18 | 5020/5020 sous charge (Fidélité, Intégrité), 5019/5020 (Données : interblocage 1205 passager, vert relancé). Majeurs : langues d'un paquet hors des règles de saisie (Architecture M3 = Sécurité M-A) ; demi-paire de substitution en 500 (Données M1) ; AR-118 sans preuve de la borne de 51 cartes ni de l'en-tête de la duplication (Données M2 = Intégrité = Fidélité MAJ-1). Corrections du round 1 toutes confirmées, 17 mutations rejouées tuées, régénération de `conges.topogenia` jugée légitime. |

Corrections (détail : `Travail/porte-p5-r2-corrections.md`) :
- AR-119 : une traduction dans la langue d'origine de son objet ou dans une langue non configurée est ignorée à l'import,
  les autres gardées ; la langue d'origine de chaque objet doit être configurée (`values`, `<objet>.sourceLanguage`)
  (`ImportedRecords.IgnoreTranslations`, `Violations(workspace, languages)`).
- `Interchange/Json/JsonText` : chaque texte et chaque nom d'un document reçu lu juste après l'analyse ; paquet refusé
  `json`, `.pmap` `PMAP_JSON`.
- Test de la borne de la duplication (51 niveaux entiers, 52e coupé avec `PMAP_PROFONDEUR_COUPEE`).
- Mineurs au registre F-17 à F-21. Huit mutations sur une copie du dépôt, toutes tuées
  (`Travail/mutations-p5-r2-corrections.log`). Suite complète seule **5038/5038**, 0 avertissement.

### Porte P5-T08 - round 3 et ses corrections (27/10/2026)

| Round | Contrôleurs | Bloquants | Majeurs | Mineurs | Tests |
|---|---|---|---|---|---|
| 3 | Fidélité ; Architecture ; Sécurité ; Données ; Intégrité (agents neufs, mandat resserré) | 0 | 1 + 1 + 2 + 2 + 0 = 6 (3 distincts) | 4 + 3 + 1 + 2 + 0 | Intégrité franchit : suite complète sous charge 5038/5038, mutations du round 2 rejouées et onze de plus, toutes tuées. Majeurs : moitié de paire de substitution dans un nom de propriété d'un paquet (500) ; tests d'AR-119 tous en français (langue de l'objet non prouvée) ; aperçu qui compte les traductions écartées. |

Corrections (détail : `Travail/porte-p5-r3-corrections.md`) : `PackageReader.Parse` attrape l'erreur de lecture d'un nom
(`json`) ; étape anglaise d'un projet français dans les tests du domaine et de l'hôte ; `PreparedImport.IgnoredTranslations`
et aperçu juste ; AR-119 complétée, `format-topogenia.md` § 9, `architecture.md`, F-22. Trois mutations sur une copie,
toutes tuées. Suite complète seule **5041/5041**, 0 avertissement. Round 4 (le dernier) : Fidélité, Architecture,
Sécurité, Données.

### Porte P5-T08 - round 4 : porte franchie (27/10/2026)

| Round | Contrôleurs | Bloquants | Majeurs | Mineurs | Tests |
|---|---|---|---|---|---|
| 4 | Fidélité ; Architecture ; Sécurité ; Données (agents neufs, mandat resserré ; Intégrité franchi au round 3) | 0 | 0 | 1 + 0 + 1 + 0 | Tests des fichiers changés verts sous charge, sans échec passager ; mutations des corrections du round 3 et sept de plus, toutes tuées ; `--import` éprouvé par l'exécutable réel (dossier de données dans le scratchpad) ; aperçu = lignes écrites = export sur les deux moteurs. |

**Porte P5-T08 franchie au round 4 : phase 5 terminée.** Dernière suite complète seule : **5041/5041**, 0 avertissement
(`Travail/suite-p5-r3-corrections.log`). Mineurs du round 4 : Fidélité (traçabilité du compte de l'aperçu : S-7.3-02,
ADR 052 § 5) fait à la fermeture ; Sécurité au registre (F-23). Registre de la phase 5 : F-11 à F-23, repris par P7-T07.

## Phase 6 - Interface

### P6-T01 - Client d'API généré (27/10/2026)

Fait (ADR 056) : le contrat est le document OpenAPI que sert l'hôte, figé dans `web/src/api/generated/openapi.json`
(sans `servers` ni `info`) ; `ApiContractTests` le compare au document servi sur les deux bases : il est le même dans les
deux modes. L'hôte décrit le corps des refus (`ErrorBody`, `Json/ErrorSchemaTransformer`), son code parmi ceux du
catalogue des messages. `npm run api` génère `schema.ts` (openapi-typescript 7.13.0) et `exchanges.ts` (six opérations
de fichiers) ; un test vitest vérifie qu'ils sont à jour et qu'aucun type d'API n'est écrit à la main. Client maison :
`request.ts` (seul `fetch` : `same-origin`, `X-Requested-With` sur les écritures, `If-Match` d'une version,
`Accept-Language`, `RESEAU`, `REPONSE_INVALIDE`, `HTTP_<statut>`, annulation rendue telle quelle), `errors.ts`
(`ApiError`, codes typés), `client.ts` (façade typée par le contrat, `version` exigée sur chaque route qui lit
`If-Match`). Proxy de développement à une connexion (B-0-09).

Écarts et décisions : `openapi-typescript` annonce TypeScript 5 en paire, le projet est en 6.0.3 : paire fixée par
`overrides` (ADR 056 § 4). Les lignes de B-0 hors client sont rattachées aux tâches de leur thème (`plan.md`).
`THIRD-PARTY-NOTICES.md` régénéré : les dépendances de développement du générateur, et deux composants NuGet en 9.0.18
reclassés `tests` (le livré est en 10.0.13, déjà listé).

Tests : une première suite complète donne 5044/5045, le rouge est la règle D13 (nom du produit dans un commentaire de
`client.ts` et dans le titre du contrat) : commentaire reformulé, `info` retiré du contrat. Suite complète seule
**5045/5045**, 0 avertissement (`Travail/suite-p6-t01.log`) ; web vitest **204/204**, `tsc -b`, ESLint, build verts.
Sept mutations du client (en-tête anti-CSRF, guillemets d'`If-Match`, code vide, refus comptés dans les échanges,
version et corps rendus facultatifs dans les types, `credentials`), toutes tuées.

### P6-T02 - Socle de l'interface (27 et 28/10/2026)

Fait (ADR 057) : l'interface n'avait aucun moyen de connaître les langues de la configuration ; le profil les rend
(`ProfileView.Languages`, `GET /api/me` et toute réponse du profil), contrat régénéré. Côté web : routeur (routes
anglaises, barre finale, identifiant en minuscules, écran remonté pour chaque carte), enveloppe (« Chargement… », page
d'erreur et « Réessayer », page introuvable), en-tête (marque, version en infobulle, signature, nom et initiales,
Traductions selon le projet ouvert, sélecteur des langues configurées), profil en magasin zustand avec AR-106 (conflit
avec soi-même : relecture et même envoi), i18n (un fichier par langue, français avant le profil et en repli, `lang` et
titre de la page), notifications (coin bas droit, pile, 3,5 s et 7 s, rôles), clavier (un écouteur en capture, couches,
Ctrl+S global), commandes en données lues par le clavier, `Menu` et `Toolbar`, préférences `app.*` (ruban replié, AR-90)
avec un test qui garde que seul ce module touche au stockage du navigateur, composants `shared/ui`.

Écarts et décisions : `#root` au lieu de `#racine` (Q1) ; `__APP_VERSION__` (D13) ; `/favicon.svg` porte en clair les
valeurs des jetons (fichier servi seul) ; texte de Ctrl+S sans l'emplacement du téléchargement d'une copie, à reprendre
en P6-T03 (deux défauts appliqués dans `questions.md`). Raccourcis de la carte (B-2.5-06 à 34) et état de l'éditeur
(B-7-07 à 12, 16 à 21) rattachés à P6-T04. Dépendances : zustand 5.0.15, i18next 26.4.3, react-i18next 17.0.16 (MIT,
versions d'au moins deux semaines), `THIRD-PARTY-NOTICES.md` régénéré.

Tests : web vitest **391/391** (204 avant), `tsc -b`, ESLint, build verts ; un test d'architecture rouge au premier
passage (mots français hors chaînes dans un test web), corrigé. Suite complète seule **5046/5046**, 0 avertissement
(`Travail/suite-p6-t02.log`). 29 mutations sur une copie du web (clavier, commandes, profil et AR-106, notifications,
routeur, préférences, langues, enveloppe, couches des dialogues et menus) : 28 tuées au premier passage, la survivante
(clé de remontage de l'écran de carte) tuée après avoir renforcé le test.

### P6-T03 - Accueil et écrans de projet (28/10/2026)

Fait (ADR 058) : accueil (bandeau d'AR-88, « Décrire un processus » et « Qui participe » sur le dernier projet ouvert,
sinon le premier par code, AR-49 ; projets avec nom traduit, code, description, compteurs, rôle ; Membres et Supprimer
pour l'animateur ; corbeille facultative, Restaurer, Supprimer définitivement pour l'administrateur ; suppression par
saisie du code avec la version relue ; mise en page 2/3 - 1/3, une colonne sous 900 px), création d'un projet (un nom par
langue de la configuration, langue d'origine), membres (liste complète avec la version, relue après chaque
enregistrement), dialogues `.pmap` (ouvrir : fichier choisi, déposé ou collé, code et nom proposés, fautes avec leur
chemin, points à vérifier ; dupliquer ; copie), paquet `.topogenia` (export sans les personnes coché, import toujours par
l'aperçu des mêmes choix, code pris, remplacement avec la version), B-1.8 (pas d'écran Profil). Texte de Ctrl+S complété.

Écart trouvé et corrigé : 24 schémas du contrat de P6-T01 étaient vides (énumérations écrites en codes, listes des
corps), typés `unknown` par le générateur ; deux transformateurs de schéma de l'hôte, un test du contrat qui refuse tout
schéma vide sur les deux bases, contrat et types régénérés (complément de l'ADR 056). Le nom des fichiers téléchargés
reprend côté client la règle de l'hôte (A-1.2-16), avec les mêmes cas de test. Lignes de bout en bout (B-1.4-15,
B-1.6-17, B-1.6-22, S-7.3-03) prouvées en unitaire et laissées à P6-T10. Entrées du menu Fichier de la carte à brancher
en P6-T04.

Tests : web vitest **536/536** (391 avant), `tsc -b`, ESLint, build verts ; Architecture 754/754 ; suite complète seule
**5048/5048**, 0 avertissement (`Travail/suite-p6-t03.log`). 30 mutations sur une copie du web : 25 tuées au premier
passage ; quatre survivantes (bouton d'effacement de la corbeille, erreur de la corbeille relue, priorité de la langue de
l'interface, seuil des points à vérifier) tuées après renfort des tests ; une équivalente (retrait du BOM, déjà fait par
`trim`) retirée du code.

### P6-T05 - Routage des flèches (28/10/2026)

Ordre : fait avant P6-T04, sur décision du porteur (l'éditeur dessine les flèches avec ce module ; le plan de la phase 0
suivait l'ordre de l'inventaire sans ranger par dépendance, note dans `plan.md`).

Fait (ADR 059) : module pur `web/src/features/map/model/geometry/` : constantes (une seule fois, variables CSS dérivées),
grille (rangées par couloir, positions, case sous un point, largeur), routage. Le module calcule le tracé complet, là où
la 1.x ne rendait que poignées et coude : côtés (B-3.4-01 à 04), pointes des losanges (une par sortie, sorties imposées
d'abord, surnombre depuis la pointe droite sur sa voie, AR-24), verticale par l'allée de droite, contournement par la bande
au-dessus de la ligne de la cible, coude avant l'arrivée ou après le départ, boucle en courbe sous une ligne, et, vers une
autre ligne, tracé orthogonal par les bandes libres (AR-120 : la courbe 1.x traversait les étapes). Voies de 6 px par
passage (allées, axes, lignes, bandes), horizontaux d'abord sur des étendues élargies de 24 px, puis verticaux ; boucles
vers une autre ligne sur l'allée la moins chargée. Étiquettes hors des étapes, de l'étiquette des losanges (AR-57, AR-58)
et des autres étiquettes. Ordre de lecture interne : tracé indépendant de l'ordre des listes.

Mise au point : premier passage sur 500 cartes, seules les étiquettes échouaient (une étiquette de 110 px entre deux
étapes voisines ne pouvait pas quitter la hauteur des étapes) : places dans la bande libre et à côté des verticaux
ajoutées. Essai à 5 000 cartes : débordements de capacité (toutes les boucles vers une colonne passaient par la même
allée) : allée choisie par charge, générateur aux sorties de losange surtout vers l'avant ; reste 1 cas sur 5 000 (plus de
5 contournements au-dessus de la première ligne, bande de 5 voies), au registre F-24.

Tests : web vitest **613/613** (536 avant), `tsc -b`, ESLint, build verts ; Architecture 754/754 ; suite complète seule
**5048/5048**, 0 avertissement (`Travail/suite-p6-t05.log`). 30 mutations sur une copie du web : 20 tuées au premier
passage ; 10 survivantes : 8 tuées après renfort des tests (losange à sortie verticale haute et boucle adjacente, losange
d'une sortie, allée de boucle exacte, ordre des voies, flèches dans la carte, étiquette d'un losange, égalité d'ordre des
couloirs), une ligne redondante retirée (hauteur initiale d'un contournement, écrasée par les voies), une équivalente
(F-24).

### P6-T04 lot 1 - La carte affichée (28/10/2026)

Fait (ADR 060) : `@xyflow/react` 12.12.0, la version de la 1.x (B-0-01), sa seule feuille `base.css`, les couleurs écrites
sur ses éléments avec les jetons. Mise en page pure (`model/mapLayout.ts`) : bandes des couloirs dans leur ordre, impaires
puis paires, teinte par le pastel ou la plus proche ; étapes dans leur boîte, libellé dans la langue affichée ; flèches
routées par le module de P6-T05, leurs étiquettes et celles des losanges mesurées (`model/textSize.ts` : canevas 2D dans la
police de la page, remesuré après le chargement des polices, estimation sans canevas), libellé d'une flèche dans la langue
de son étape source. Nœuds de React Flow à taille et poignées déclarées (quatre `source`, au milieu des côtés), un seul
composant pour les trois types de flèches, pointe `ArrowClosed` 18 × 18 en `--edge`, zone de clic de 16 px. Étapes : sept
types, liseré et contour dans l'encre du couloir, code (préférence), infobulle « code · chemin », pictogrammes (documents
produits et reçus, irritants ouverts, durée lisible ; toujours ou au survol), jauge par état et complétude, pastille « ! »
des avertissements, étape détaillée (pile, « détaillée », fréquence). En-têtes de couloirs fixes de 184 px qui suivent la
vue verticale et le zoom ; fonds de couloir et leur marge d'export cachée. Vue contrôlée, bornée à chaque mouvement et
quand la carte ou l'écran change, ouverte au début de la carte ; zoom de 0,2 à 2, double clic sans zoom, contrôles + −
et « Revenir au début de la carte », mini-carte, points de 24 px. Magasin par éditeur (lecture annulée par la suivante),
écran de la carte (chargement, erreur et « Réessayer », bandeaux « Consultation » et profondeur, carte vide).

Écarts et décisions : la barre de l'éditeur dans l'en-tête (fil d'Ariane, Fichier, Membres, Traductions) passe au lot 3 ;
éléments sélectionnables sans changement appliqué jusqu'au lot 2, sans quoi React Flow coupe le survol des étapes ;
étiquette des flèches en 11 px comme la maquette ; mention de React Flow masquée (D14) ; pictogrammes d'un losange sur fond
blanc (contraste) ; défaut appliqué ajouté à `questions.md`. Registre F-25 (badge de fréquence non vu du routage), F-26
(paquet JavaScript de plus de 500 ko).

Défaut trouvé par les tests et corrigé : quand React Flow passait au-delà de la borne et que la vue bornée égalait celle
gardée, l'état ne changeait pas et la vue restait hors des bornes ; l'éditeur rend une vue neuve à chaque mouvement. Le
glisser du fond et le zoom par les contrôles sont éprouvés sous jsdom (`src/test/flowDom.ts` fournit à React Flow ce que
jsdom n'a pas). Vérification visuelle dans Chromium sans tête sur l'interface construite, l'API simulée par
interception : rendu conforme à la maquette, survol, zoom, retour au début et bornes du glisser, aucune erreur de console.

Tests : web vitest **802/802** (613 avant), `tsc -b`, ESLint, build verts ; suite complète **5048/5048**, 0 avertissement
(`Travail/suite-p6-t04-l1.log`), Architecture rejouée après les dernières retouches 754/754. 100 lignes de couverture
écrites (9 `partiel`, renvoyées à P6-T06, T09, T10 et aux lots 2 et 3). 42 mutations sur une copie du web : 34 tuées au
premier passage ; survivantes tuées après renfort des tests (l'ancienne vue égale non rendue, prouvée par le glisser du
fond ; un losange seul marqué « sortie basse » ; le libellé d'un losange donné au routage ; l'alerte de profondeur au
seuil ; le seuil exact du retour à la ligne) ; une équivalente (garde redondante du retour à la ligne) retirée du code.

### P6-T04 lot 2 - L'édition de la carte (28/10/2026)

Fait (ADR 061) : un éditeur par carte (`state/editor.ts`, `state/EditorContext.tsx`) avec ses magasins (carte, préférences,
élément flottant) et la file des écritures : aucune mise à jour optimiste, la carte de la réponse appliquée ou relue, refus
affiché, 409 relu. Sélection, outil (Main à l'ouverture), Espace tenu et fiche ouverte au magasin, jamais gardés ; la
sélection survit aux relectures. Modules purs : ordre de lecture unique (`model/readingOrder.ts`), presse-papiers
(`model/clipboard.ts` : bloc relatif à sa première étape, flèches internes, placement, insertion, duplication, fin de
carte), alignement porté de la 1.x (`model/alignment.ts`), déplacements (`model/moves.ts` : flèches du clavier, case du
lâcher, déplacements utiles, bloc sur cases libres). Opérations (`state/actions/` : étapes, disposition, sélection,
flèches, couloirs, presse-papiers) : un bloc sur des cases libres part en `layout`, sinon en `move` (AR-44). Commandes en
données (`state/commands/` : contexte, outils, édition, menus, registre) lues par le ruban, les menus et le clavier ; menus
en lignes groupées. Ruban (`chrome/Ribbon.tsx`) : zones nommées, choix radio, icônes des formes d'après la maquette,
« Réduire le ruban » et Ctrl+F1 (AR-90). Canevas : outils Main et Sélection, Espace, rectangle, Maj+clic, glisser-déposer
gardé jusqu'à la réponse, clics et double-clics (fiche, mode Relier, création, renommage, sous-carte), poignées en
connexion souple, menus contextuels de l'étape, de la sélection, du fond et de la flèche, champ flottant du libellé d'une
flèche, saisie du nom en place, ajout rapide « + », états choisi, coupé et source. En-têtes de couloirs : renommer,
réordonner en glissant, « + » entre les couloirs, menu « ⋯ » (type, huit teintes en pastilles, supprimer). Dialogues
avant suppression (couloir, étape détaillée, sélection avec détail, détail). Le menu et la barre partagés gagnent les
choix radio, les entrées rouges, les groupes, la pastille et le placement.

Écarts et décisions : un menu dessiné plus haut que le calcul de la 1.x remonte d'autant et défile au-delà de la fenêtre
(défaut vu à la vérification visuelle : le menu d'une étape sortait de l'écran de 768 px) ; Espace laissé aux boutons,
étapes et flèches qui ont le focus (accessibilité) ; un clic avec le calque RACI allumé n'ouvre pas la fiche (panneau RACI
en P6-T06) ; la fiche ouverte est gardée au magasin, son panneau vient en P6-T07. Remis au lot 3 : bouton Commande et
Ctrl+K, annuler et rétablir, menu Fichier, barre de l'éditeur dans l'en-tête ; à P6-T06 : Remonter le détail, panneau et
matrice RACI. Registre F-27 (Espace sur un nœud focalisé), F-28 (menu d'une étape de 19 entrées), F-29 (« + » de droite
contre la poignée).

Vérification visuelle dans Chromium sans tête sur l'interface construite, l'API simulée par interception
(`Travail/outils-p6-t04/capture-edit.mjs`) : ruban sur une ligne en 1366 px (largeur utile 1366 sur 1366), « + » au
survol, menu d'une étape, étape coupée grisée, source du mode Relier et bandeau, menu d'un couloir et ses pastilles, saisie
du nom, rectangle de sélection, ruban replié ; aucune erreur de console.

Tests : web vitest **1046/1046** (802 avant), `tsc -b`, ESLint, build verts ; suite complète **5048/5048**, 0 avertissement
(`Travail/suite-p6-t04-l2.log`, sous la charge des mutations : 14 min 16) ; Architecture rejouée sur le code final
754/754. 210 lignes de couverture écrites (`Travail/outils-p6-t04/coverage_lot2.py`) : 154 `couvert`, 20 `couvert
(interface)` (bout en bout ou axe-core en P6-T10), 36 `partiel` (lignes de bout en bout seul prouvées à l'interface, et
celles qui attendent P6-T06, T07 ou le lot 3). 61 mutations sur une copie du web (`Travail/outils-p6-t04/mutate-lot2.py`) :
57 tuées au premier passage ; survivantes tuées après renfort des tests (une étape détaillée choisie avec une flèche passe
par la confirmation de la sélection ; Ctrl+V quand l'unique étape choisie est celle coupée ; le menu de l'étape sur
l'unique étape choisie) ; une équivalente (seuil de 4 px du glisser d'un en-tête : le centre doit passer la moitié d'une
bande pour changer de couloir) retirée du code. Le test du lot 1 qui fait 32 clics de zoom (2 s seul) dépassait 5 s sous
la suite entière : délai porté à 15 s, contrôles inchangés ; les gestionnaires du canevas sont gardés tant que la carte
ne change pas (React Flow n'en reçoit plus de nouveaux à chaque mouvement de la vue).

### P6-T04 lot 3 - Annuler, barre de commande, barre de l'éditeur (28/10/2026)

Fait (ADR 062, AR-121 ; **P6-T04 terminée**) : annuler et rétablir par la file des écritures (`state/actions/history.ts`) :
boutons de la zone Édition selon l'historique de l'hôte, grisés pendant un rejeu (`replaying`, B-2.6-06) ; Ctrl+Z, Ctrl+Y et
Ctrl+Maj+Z mis en file derrière les écritures en cours, l'historique lu à leur tour (un Ctrl+Z pressé pendant un
déplacement l'annule) ; « Annulé : {action} » et « Rétabli : {action} » nommés par le `kind` de la réponse (les 21 codes
du journal ont un nom dans chaque langue, test qui lit `OperationKinds.cs`) ; refus 409 relu avec ses piles vidées
(AR-05), `INTROUVABLE` muet (AR-108), autre échec dit avec sa raison, la pile de l'hôte gardée (AR-121, écart à B-2.6-49).
Barre de commande (`panels/CommandBar.tsx`, `state/actions/commandBar.ts`) : Ctrl+K et bouton Commande de la zone Liens,
fixe à 18 % et 560 px (jetons), zone de saisie à chasse fixe de 1 à 6 lignes, Entrée envoie et Maj+Entrée va à la ligne,
refus et erreurs par ligne dans la barre, « N flèches ajoutées » ; fermée pendant l'envoi, elle ne touche plus à ce qui
est ouvert. Barre de l'éditeur dans l'en-tête (`app/EditorBar.tsx`) : l'écran de la carte déclare le projet ouvert
(`shared/state/openProject.ts`), l'en-tête montre à la place de la signature le fil d'Ariane (liens, `aria-current`,
chevrons, code du projet), Fichier et Membres. Menu Fichier (`state/commands/fileCommands.ts`, `state/actions/files.ts`)
sur les dialogues de P6-T03 : `.pmap` et duplication pour l'administrateur, copie pour tous, export et remplacement du
paquet pour l'animateur (version du projet et durée de la corbeille lues avant le remplacement). Dernier projet ouvert
mémorisé par l'enveloppe (`rememberProject`, `PUT /api/me/last-project`, échec muet) : l'accueil le met en avant
(B-8.1-42, B-8.1-43). L'administrateur est donné à l'éditeur par la route de la carte. Charte des composants prouvée par
les feuilles de style (`styles/charter.test.ts` : 40 lignes de B-6) ; règle ajoutée : le formulaire de fichier sur une
colonne sous 600 px (B-6-55).

Écarts et décisions : la barre de l'éditeur prend la place de la signature (maquette validée) ; le bouton Traductions
attend son écran (P6-T08) ; les entrées PDF, PNG et Excel du menu Fichier viennent avec les exports (P6-T09). Lignes de T04
laissées à d'autres tâches, écrites dans la couverture : P6-T06 (bouton Matrice, Remonter le détail, cadre, en-têtes de
tableau, rangement automatique), P6-T07 (panneau Fiche), P6-T09 (exports), P6-T10 (parcours de bout en bout). Registre :
F-26 mis à jour (614 ko), F-30 (libellés du menu Fichier sur deux lignes).

Vérification visuelle dans Chromium sans tête sur l'interface construite, l'API simulée par interception
(`Travail/outils-p6-t04/capture-lot3.mjs`) : en-tête de 48 px sans débordement, ruban toujours sur une ligne en 1366 px
(largeur utile 1366 sur 1366), menu Fichier 4 px sous son bouton, barre de commande à 18 % (138 px sur 768), 560 px et
centrée, refus par ligne, « Annulé : déplacement » puis Rétablir activé ; seule erreur de console : le 400 voulu de la
commande refusée.

Tests : web vitest **1128/1128** (1046 avant), `tsc -b`, ESLint, build verts ; suite complète **5048/5048**, 0 avertissement
(`Travail/suite-p6-t04-l3.log`, 13 min 04) ; Architecture rejouée sur le code final 754/754 (un premier passage avait
trouvé le nom du produit écrit dans un test, remplacé par `PACKAGE_EXTENSION`). 99 lignes de couverture écrites
(`Travail/outils-p6-t04/coverage_lot3.py`) : 72 `couvert`, 2 `couvert (interface)`, 7 `partiel`, 18 `à faire` routées vers
leur tâche ; deux titres de tests renommés mis à jour partout. 58 mutations sur une copie du web
(`Travail/outils-p6-t04/mutate-lot3.py`) : 53 tuées au premier passage, 3 après renfort des tests (touche de la carte muette
sous la barre, ligne d'erreur sans message, bouton Membres d'un lecteur), une équivalente retirée du code (droit de
modifier revérifié par les boutons Annuler et Rétablir, dans une zone que le lecteur ne voit pas) ; défaut trouvé à la
relecture et corrigé : une réponse de la barre arrivée après sa fermeture fermait l'élément ouvert depuis (mutation
ajoutée, tuée).

### P6-T06 - Détail, remontée et RACI (28/10/2026)

Fait (ADR 063, AR-122) : modules purs `model/raci.ts` (étape concernée, R implicite, approbateur manquant, lettres de la
carte, pastilles A, S, C, I puis ordre des couloirs, matrice en ordre de lecture, texte tabulé, tableau HTML), `frame.ts`
(boîte du cadre, marges 14 et 26, étapes étrangères recouvertes), `remainder.ts` (taille, candidats en deux groupes,
rangement automatique, sens d'un rangement) ; `hasContent` passé dans `model/stepFacts.ts`. Cadre de remontée : nœud
`frame` derrière les étapes, que le pointeur traverse sauf son étiquette « Reliquat de CODE · n » / « Remonté de CODE ·
rien à ventiler », qui ouvre le panneau ; étape étrangère `is-outside`. « Remonter le détail dans le flux » au menu Étape
(macro, pas de cadre ouvert). Panneau Reliquat ancré à droite du canevas (`map-workspace`, `.side-panel` de 440 px que
reprendra la fiche), non modal (Échap, couche `panel`) : sections, fiche en lecture seule, « Affecter à… » en deux groupes
et « La carte (sans étape) », Supprimer confirmé, Terminer ou « Fermer le cadre » ; un lecteur voit le reliquat sans les
actions (AR-122). Dialogue des rangements ; rangement automatique une fois par remontée et cadre fermé qui ferme le
panneau (`watchFrame`, posé par l'écran). RACI : pastilles sous l'étape (sous le libellé d'un losange et la fréquence
d'une macro, `UNDER_STEP`), « A ? » ; panneau à (+12, −20) du clic, R en tête, une lettre par bouton ; bouton Matrice
avec le calque (`itemsOf` lit désormais `visible`), dialogue large, légende, copie `text/html` et `text/plain` aux jetons
de la page, texte seul en repli. « Détailler la sélection » : la nouvelle étape passe en renommage (B-2.9-10).

Défauts trouvés et corrigés : l'étape coupée du lot 2 de P6-T04 atténuait son texte (opacité 0,45 sur toute l'étape,
contraire à AR-53 et à la maquette) : la carte seule est atténuée, pour l'étape coupée comme pour l'étape hors cadre ; un
refus de Terminer fermait le panneau (comme la 1.x), il le laisse ouvert ; la clé de texte `raci.roles` répondait au test
qui interdit de lire `.roles` du profil, renommée `raci.names` ; la suite web, sur 19 travailleurs, dépassait les 5 s du
premier test de quatre fichiers : `maxWorkers: '50%'`, sans toucher aux délais (et plus rapide) ; les fichiers réécrits par
mes scripts étaient en CRLF (Architecture), remis en LF ; une expression régulière d'un test lue comme identifiant
français, remplacée par le nom exact. Deux branches redondantes retirées (fermeture du panneau après Terminer, garde
`typeof ClipboardItem`).

Écarts et décisions : panneau ancré plutôt que posé sur la carte (maquette de la fiche) ; lecteur sans actions (AR-122) ;
refus de Terminer sans fermeture. Restent : cadre caché à l'export (B-2.9-20, P6-T09), parcours de bout en bout
(B-2.9-12, 13, B-2.11-08, 09 `partiel`, P6-T10). Registre : F-26 mis à jour (638 ko).

Vérification visuelle dans Chromium sans tête (`Travail/outils-p6-t06/capture-t06.mjs`, API simulée) : cadre de 418 × 224
autour de ses deux étapes, étiquette en haut à droite, losange étranger atténué, texte gris ; panneau de 440 px, canevas
ramené à 926 px ; pastilles sous la fréquence d'une macro et sous le libellé d'un losange ; panneau RACI à (+12, −20) ;
matrice aux en-têtes `--table-header-bg` ; dialogue des rangements ; étape coupée selon AR-53. Aucune erreur de console.

Tests : web vitest **1243/1243** (1128 avant), `tsc -b`, ESLint, build verts ; Architecture **754/754** sur le code final
(un premier passage de la suite complète, `Travail/suite-p6-t06.log`, avait 5046/5048 : les deux échecs d'Architecture
ci-dessus) ; suite complète finale **5048/5048**, 0 avertissement (`Travail/suite-p6-t06-final.log`, 11 min 45). 69 lignes de couverture écrites (`Travail/outils-p6-t06/coverage_t06.py`) :
B-2.9 et B-2.11 n'ont plus qu'une ligne `à faire` (P6-T09) et quatre `partiel` (P6-T10), plus aucune ligne renvoyée à
P6-T06 ; trois titres de tests renommés mis à jour partout. 92 mutations sur une copie du web
(`Travail/outils-p6-t06/mutate-t06.py`) : 92 tuées, après renfort de trois tests avant le passage (type du blob HTML
copié, sections vides absentes, notification de la copie) et retrait des deux branches redondantes.

### P6-T07 - La fiche d'étape (28/10/2026)

Fait (ADR 064, AR-123) : module `web/src/features/sheet/` indépendant de l'éditeur de la carte (spec 3.5), branché par
`map/panels/SheetDock.tsx`. Modules purs `model/draft.ts` (brouillon, contenu envoyé nettoyé, mode au chargement, résumé
de la carte, outils proposés), `model/procedure.ts`, `model/documents.ts` (formulaire d'un document, entrées en trois
groupes, usage, consommateurs, lien http(s) seulement) ; file d'enregistrement `state/autoSave.ts` (1200 ms, un envoi à la
fois, version `0` d'une fiche jamais enregistrée, conflit relu sans toucher à la saisie, envoi à la fermeture,
confirmation avant de quitter l'onglet) ; `state/useSheetState.ts`, `state/sheetActions.ts` (validation après le dernier
enregistrement, précision, responsable). Panneau ancré à droite (440 px, 45 % au plus, canevas rétréci), une clé par
étape ; en-tête collant (titre, couloir, état coloré par son code, enregistrement, complétude, précision demandée) ;
responsable choisi parmi les membres non lecteurs ; bascule Formulaire / Écrire librement ; rubriques dans l'ordre testé ;
Entrées, Déroulé, Sorties pliables en préférences de l'éditeur (AR-91) ; déroulé à poignée de glissement, ↑ ↓ × au survol
et au focus, Alt+↑ / Alt+↓ (AR-89) ; entrées proches, d'ailleurs (jetons d'AR-26), extérieures, dialogue « Autre… » ;
usage (attente, %C&A, irritant et temps perdu, envoyé à la sortie du champ s'il a changé) ; sorties et irritants de leurs
consommateurs ; lecture de la version validée ; pied collant et « Je valide ma fiche ». Côté carte : jauge prise de la
fiche reçue sans relire la carte (B-2.6-11), carte relue par la file de l'éditeur après un document (`editor.reload`),
responsable par `PATCH` dans la file, projet lu une fois pour nommer les personnes (« un ancien membre »), un seul panneau
ancré (`watchSidePanel`, AR-123). Feuille `styles/sheet.css` (maquette, jetons seulement).

Défauts trouvés et corrigés : **défaut de la 1.x** évité en écrivant la file : après un échec, la saisie qui avait échoué
était remise en attente même quand une plus récente était déjà partie, et repartait après elle en l'écrasant ; la file ne
remet en attente que la dernière saisie. **Défaut de P6-T06** trouvé à la vérification visuelle : le gestionnaire de clavier
transmet aussi le relâchement des touches, et les panneaux Reliquat et RACI fermaient sur le relâchement de l'Échap qui
venait de fermer un dialogue au-dessus d'eux ; les trois panneaux ne ferment plus que sur l'appui (un test par panneau,
rouge sans la correction). Par les mutations : une confirmation de départ manquait pendant un envoi pas encore répondu
(B-4-46), corrigée ; deux gardes redondantes avec l'hôte retirées (entrée extérieure toujours utilisée, version validée
absente d'une fiche vide). Règles du dépôt rencontrées : un type nommé comme un schéma du contrat (`Frequency`, renommé), des
adresses d'exemple hors `localhost` dans les tests, un caractère d’icône de la 1.x dans un commentaire, des fonctions de test nommées comme
des hooks.

Écarts et décisions (questions.md, « Défaut appliqué ») : un seul panneau ancré (AR-123) ; « un ancien membre » ; groupe de
partage sans libellé ; saisie ancienne jamais renvoyée. Restent : les parcours de bout en bout de la fiche (B-8.3-14 à 25,
P6-T10). Registre : F-26 mis à jour (692 ko, 212 ko compressé).

Vérification visuelle dans Chromium sans tête (`Travail/outils-p6-t07/capture-t07.mjs`, API simulée) : panneau de 440 px
à droite, canevas ramené à 926 px puis rendu à 1366 px à la fermeture, en-tête et pied collants ; enregistrement envoyé avec
`If-Match "2"`, jauge de l'étape passée à 75 % ; entrée d'ailleurs en lilas, irritant sur `--danger-soft`, ligne de contrôle
verte, boutons d'une ligne à 0 puis 1 au survol ; sections pliées, « Autre… », formulaire d'une entrée extérieure, texte
libre, lecture. Aucune erreur de console. Retouches : libellé court de l'attente d'un usage, ligne d'irritant d'un seul
tenant, aide des outils avant leurs pastilles.

Tests : web vitest **1452/1452** (1243 avant), `tsc -b`, ESLint, build verts ; suite complète **5048/5048**, 0 avertissement
(`Travail/suite-p6-t07.log`, 12 min 39, avant les renforts des mutations, qui ne touchent que le web) ; Architecture
**754/754** sur le code final. 86 lignes de couverture écrites (`Travail/outils-p6-t07/coverage_t07.py`, plus B-2.3-13 et
B-2.3-18) : B-4 n'a plus de ligne `à faire` (62 `couvert`, 15 `couvert (interface)` dont le parcours reste à P6-T10, 3
`couvert (API)` complétées côté interface, un renvoi). 131 mutations sur une copie du web
(`Travail/outils-p6-t07/mutate-t07.py`) : 117 tuées au premier passage ; 12 tuées après renfort des tests (dont le vrai
défaut B-4-46 ci-dessus) ; 2 équivalentes retirées avec leur garde redondante ; 129 sur 129 au final.

### P6-T08 - Les traductions (28/10/2026)

Fait (ADR 065, AR-124, AR-125) : **glossaire des traductions en réglage du projet** (spec 10, B-8.1-45 ; la 1.x avait un
glossaire finance dans le code) : champ `translation_glossary` (texte libre, un terme par ligne, 4 000 caractères), règle
`ProjectSettings.ChangeTranslationGlossary`, commande `ChangeTranslationGlossaryCommand` (Animateur, `If-Match` du projet),
route nommée par l'hôte `PUT /api/projects/{id}/translation-glossary`, `ProjectDetail.translation_glossary`, migration
`TranslationGlossary` sur les deux moteurs (générée par l'outil local, `Down` qui retire sa ligne de `schema_history`),
paquet `.topogenia` (`translationGlossary`, schéma, `format-topogenia.md`, exemple `samples/conges.topogenia` réécrit),
contrat OpenAPI et client régénérés. Interface : module `web/src/features/translations/` : `model/workbook.ts` (classeur
exceljs 4.4.0 construit dans le navigateur, chargé à la demande ; une colonne par langue configurée ; feuilles dans la
langue de l'interface ; contrôle `veryHidden` au nom du produit ; consigne aux langues configurées, glossaire à sa suite),
`model/workbookReader.ts` (refus `FICHIER_ILLISIBLE`, `FICHIER_SANS_FEUILLE_PROJET`, `FICHIER_AUTRE_PROJET` ; lignes par la
clé cachée ; texte riche, formule, nombre, lien), `model/report.ts` (lignes ignorées par raison), dialogue en trois étapes
(`TranslationsDialog.tsx`, `DialogParts.tsx`, `useGlossary.ts`, `useWorkbook.ts`), traductions manuelles
(`ManualTranslationsDialog.tsx`) ; côté carte : `openTranslations` déclaré avec l'écran quand une autre langue est
configurée, langues du profil données par `App` à l'éditeur, « Traduire… » aux menus d'une étape, d'une flèche libellée et
d'un couloir (`state/commands/translateCommands.ts`, `state/actions/translations.ts`), carte relue après un import
(`editor.reload`). `uuid` forcé en 11.1.1 sous exceljs (`overrides`) : l'alerte de la version 8 levée, `npm audit` à 0 (précisé au round 3 de
la porte P6 : pour l'arbre du verrouillage seulement ; le paquet navigateur servi embarque sa série 8, dont seul `v4`
est appelé, ADR 065).
Styles dans `styles/pages.css`.

Défauts trouvés et corrigés : le nom accessible des choix de portée collait le libellé et son aide (« Sans traduction·
seulement… ») ; l'espace est sorti de l'élément de l'aide. Par les mutations : trois tests renforcés (le titre du
glossaire cherché sans tenir compte de la casse, le compte rendu d'un classeur effacé par le suivant même refusé, le
champ du fichier vidé après chaque choix) ; une garde équivalente retirée (envoi sans changement, déjà empêché par le
bouton désactivé). Par la suite complète : Architecture refusait des expressions régulières françaises dans un test
(noms des choix de portée), remplacées par les noms complets ; un test de la carte hors délai sous la charge d'une
compilation (exceljs est lent dans jsdom) : attentes de 5 s et tests de classeur à 15 s, aucune assertion changée.

Écart dans les tests de la persistance (ADR 065) : trois tests écrivaient un projet avec le modèle courant dans une base
laissée à une migration ancienne (`SqliteRebuildTests`, `AtomicMigrationTests`, `CollationMigrationTests`) et échouaient
dès la première colonne ajoutée ; la base ancienne est maintenant migrée jusqu'au bout, le projet écrit
(`ProjectGraph.BeforeGlossary()`, ce qu'une version antérieure pouvait écrire), puis redescendue par les `Down` des
migrations ; aucune assertion retirée, une ajoutée (le schéma après la descente égale celui de la montée).

Écarts et décisions (questions.md, « Défaut appliqué ») : glossaire en texte libre du projet (AR-124) ; « Traduire… » aussi
pour les couloirs, ni bouton ni entrée avec une seule langue (AR-125) ; classeur dans la langue de l'interface, clé changée
à la main comptée avec les éléments introuvables. Reste : le parcours de bout en bout de B-5-24 (P6-T10). Registre : F-26
mis à jour (718 ko, 223 ko compressé ; exceljs à part, 929 ko à la demande).

Vérification visuelle dans Chromium sans tête **sous la CSP de l'hôte** (`Travail/outils-p6-t08/capture-t08.mjs`, API
simulée, en-tête `Content-Security-Policy` de `SecurityHeaders.Policy` sur chaque fichier) : dialogue de 1100 px, portée en
colonne, glossaire enregistré avec `If-Match "4"` avant le téléchargement ; classeur `VTE_traductions_2026-10-28.xlsx`
construit par le navigateur, relu (feuilles Traductions, Consigne et contrôle `veryHidden`, glossaire en fin de consigne),
complété puis rapporté (« 3 traductions enregistrées », une ligne au texte modifié) ; traduction manuelle d'une étape
envoyée avec `If-Match "2"` ; à 820 px, le dialogue tient sur la largeur. Aucune violation de la politique ni erreur de
console sur tout le parcours ; une sonde finale prouve la politique en vigueur (`eval` refusé à un script de la page ;
`page.evaluate` passe par DevTools et ne la subit pas).

Tests : suite complète **5057/5057** (5048 avant), 0 avertissement (`Travail/suite-p6-t08.log`, 13 min 58 ; le seul
rouge, Architecture sur les expressions régulières du test, corrigé puis Architecture **754/754** sur le code final) ;
web vitest **1527/1527** (1452 avant), `tsc -b`, ESLint et construction verts ; persistance seule 1119/1119 après
l'écart des bases anciennes. 30 lignes de couverture écrites (`Travail/outils-p6-t08/coverage_t08.py`) : B-5 n'a plus de
ligne `à faire` (27 `couvert`, B-5-24 `partiel` jusqu'au parcours de P6-T10), B-8.1-45 et B-8.2-12 `couvert`. 81
mutations sur une copie du web (`Travail/outils-p6-t08/mutate-t08.py`) : 77 tuées au premier passage, 3 après renfort,
une équivalente retirée du code : 80 sur 80. `THIRD-PARTY-NOTICES.md` régénéré (exceljs, uuid 11.1.1 ; F-31 au
registre).

### P6-T09 - Les exports de la carte (28/10/2026)

Fait (ADR 066, AR-126, AR-127) : **les trois exports de la carte, construits dans le navigateur sous la CSP de l'hôte**.
Module `web/src/features/map/export/` : `capture.ts` (bornes des nœuds sans le cadre, marge 24 et 20 px de plus en hauteur,
`pixelRatio` borné, éléments retirés : « + » et leur pont, poignées, jauges, cadre ; attente de 3 s des étapes dessinées puis
50 ms ; polices de la page lues par l'application ; capture par html-to-image 1.11.11), `pdf.ts` (jsPDF 4.2.1, A3 paysage,
titre, fil d'Ariane, date longue, classification, image ajustée, pied « Généré par » + le nom du produit, Latin-1),
`workbook.ts` (classeur exceljs à trois onglets : Étapes, Liens, Matrice RACI ; couleurs lues aux jetons). Opération
`state/actions/exports.ts` (projet lu pour sa classification, état d'export au magasin, notifications, un export à la fois,
rien dans l'historique) ; canevas : état `exporting` et classe `is-exporting`, canevas inscrit auprès de l'éditeur
(`canvas/useExport.ts`), étapes étrangères au cadre dessinées comme les autres, marqueur des flèches dans la vue
(`ViewportPortal`), ligne de la classification sous la carte (`canvas/ExportCaption.tsx`) ; règles `.is-exporting` de
`styles/map.css` ; entrées « Exporter en PDF (A3 paysage) », « Exporter en image PNG », « Exporter en Excel » au menu
Fichier, pour tout lecteur. Type `Translate` et type de média du classeur déplacés dans `shared/` (définis une fois).

Défauts trouvés et corrigés : **sous la CSP, html-to-image posait une adresse de base pour lire les polices** (deux
violations `base-uri 'none'` par fichier de police, vues à la première vérification dans Chromium) : les polices sont lues
par l'application et passées par `fontEmbedCSS` ; **hors de la vue, le marqueur des flèches n'entrait pas dans l'image**
(déplacé dans la vue de React Flow). Par les mutations : 132 mutations sur une copie du web (`Travail/outils-p6-t09/mutate-t09.py`) : 128 tuées au premier passage, 4 après renfort des tests (la date et la classification finissent exactement sur la marge, largeur mesurée par jsPDF ; rôles d'une même lettre donnés hors de l'ordre des couloirs ; la capture attend au moins 50 ms après l'état d'export ; le classeur anglais nomme étapes et couloirs dans la langue affichée) : 132 sur 132.

Écarts et décisions (questions.md, « Défaut appliqué ») : nom des fichiers par la règle de l'hôte (AR-126) ; classification
imprimée sur le PDF, le classeur et le PNG, projet lu à chaque export (AR-127, S-4.1-08, ligne sans tâche jusqu'ici) ; un
export à la fois ; en-têtes du classeur aux couleurs de la matrice de l'écran. Reste : les parcours de bout en bout (B-8.3-11
à B-8.3-13, S-5.4-04) à P6-T10. Registre : F-26 (729 ko, 227 ko compressé ; jsPDF et html-to-image à part), F-31 (dépendances
facultatives de jsPDF servies, jamais chargées).

Vérification visuelle dans Chromium sans tête **sous la CSP de l'hôte** (`Travail/outils-p6-t09/capture-t09.mjs`, API
simulée, en-tête `Content-Security-Policy` sur chaque fichier) : les trois exports lancés depuis le menu Fichier sur une carte
de trois couloirs et sept étapes dont deux hors de l'écran, avec un cadre de remontée, deux jauges et un « + » survolé ; PNG
de 6244 × 904 (ratio 2), carte entière, marges des couloirs dessinées, cadre, jauges et « + » absents, pointes des flèches,
police Inter, « Classification : Confidentiel » sous la carte ; PDF A3 (`/MediaBox` 1190,55 × 841,89), textes « VTE -
Ventes », « Ventes », date, « Classification : Confidentiel », « Généré par Topogenia », image 6244 × 856 ; classeur relu
(trois onglets figés, filtrés, en-tête d'impression à la classification, couleurs des jetons R, A, C, I et en-têtes) ;
canevas rendu à son état après chaque export. Aucune violation de la politique ni erreur de console ; sonde finale : `eval`
refusé à un script de la page.

Tests : suite complète **5057/5057** (inchangée : T09 ne touche que le web), 0 avertissement
(`Travail/suite-p6-t09.log`, 12 min 55) ; Architecture **754/754** sur le code final (deux refus en chemin : un émoji et
une adresse hors `localhost` dans les tests, remplacés) ; web vitest **1598/1598** (1527 avant), `tsc -b`, ESLint et
construction verts ; règle B-6-53 de la charte respectée (mouvement coupé par `animation-name` et
`transition-property`). 47 lignes de couverture écrites (`Travail/outils-p6-t09/coverage_t09.py`) : B-2.14 n'a plus de
ligne `à faire` (27 `couvert`, 4 `couvert (interface)` jusqu'aux parcours de P6-T10), B-0-04, B-2.4-56 à 58, B-2.6-45,
B-7-20, B-8.1-06, B-8.1-22, B-8.1-26 `couvert`, S-4.1-08 `couvert (interface)`, S-5.4-04 `partiel`.
`THIRD-PARTY-NOTICES.md` régénéré (jsPDF, html-to-image et leurs dépendances).

### P6-T10 - Portage des tests de l'interface et parcours de bout en bout (28/10/2026)

Fait (ADR 067) : banc des parcours contre le vrai hôte. Le lanceur de test `tests/Topogenia.EndToEnd.Host` (jamais livré,
dans la solution) construit l'application par `HostApplication.Build` : en mode Poste sur SQLite il lit le jeton de
lancement comme `DesktopRun` et l'écrit dans le dossier du passage, que la mise en place de Playwright ouvre dans
Chromium (vrai échange, cookie `desktop_session`) ; en mode Serveur (`E2E_MODE=server`) il migre `Topogenia_E2E` par
`--migrate` puis écoute en HTTPS sur `localhost` (certificat en mémoire, Negotiate, Chromium avec
`--auth-server-allowlist=localhost`). `web/playwright.config.ts` (1.64.1, Chromium installé, 1366x768, `fr-FR`),
`web/e2e/support/` (lanceur, mise en place, API par la requête de Playwright ou depuis une page selon le mode, base vidée
et profil en français avant chaque parcours, axe-core après les fondus), `npm run e2e`. `@axe-core/playwright` 4.13.0 et
`ajv` 8.20.0 en développement (`npm audit` 0, `THIRD-PARTY-NOTICES.md` régénéré).

Méthode : un relevé des 176 tests unitaires 1.x par un agent (143 portés, 28 partiels, 2 manquants, 3 sans objet), puis
les 30 lignes écrites par un agent (vitest et xUnit, aucune ne révèle de défaut) ; les 15 fichiers de parcours 1.x hors
exports portés par trois agents en parallèle (chacun son port et son dossier, `E2E_NO_BUILD=1`, consignes
`Travail/p6-t10-consignes-portage.md`), chaque parcours vert trois fois de suite ; les exports, les parcours nouveaux,
axe-core et les lignes restantes par moi. Tableaux : `docs/couverture-p6/portage-interface.md` (173 portés dont 11 côté
hôte, 3 sans objet), `docs/couverture-p6/portage-parcours.md` (49 parcours 1.x, 21 nouveaux).

Défauts trouvés et corrigés : **le pont invisible des « + »** (`.quick-add-bridge`, 30 px autour de chaque étape) captait
le pointeur en permanence, la 1.x ne l'affichant qu'au survol : une flèche entre deux étapes voisines ne se cliquait plus
(B-8.3-49), un rectangle partant près d'une étape la glissait (B-8.3-07, B-8.3-58), l'étiquette du cadre de remontée était
couverte aux deux tiers (B-8.3-51 à 53) ; affiché au survol seulement, test `styles/map.test.ts`. **Échap avec la fiche
ouverte** ne fermait que la fiche, une coupe restait (B-8.3-55, 56) : le panneau laisse la touche à la carte, test
`sheetDock.test.tsx` qui tombe sans le correctif. **Cibles des radios du dialogue Traductions** trop proches (axe-core,
WCAG 2.5.8) : 12 px entre les options. `data-testid="translations-close"` en double (bouton du pied :
`translations-done`). **La consigne pour l'IA n'embarquait plus le schéma** du `.pmap` : copie exacte rétablie et gardée
par un test. axe-core lisait le contraste pendant le fondu des dialogues : l'analyse attend la fin des animations finies.
En mode Serveur, deux préparations de parcours écrivaient sans `If-Match` (428) : version 0 pour un élément jamais
enregistré. `dotnet sln add` avait réécrit la solution avec un BOM et des CRLF (remise en LF sans BOM).

Écarts : la lecture seule n'est pas atteignable de bout en bout (un seul compte, administrateur) : B-2.3-46 et la part
lecture seule de B-8.3-36 en `écart`, prouvées au niveau de l'interface ; l'installation Poste (S-11-04) va à P7-T01.
**Écart relevé, non corrigé ici** : AR-52 (police Inter intégrée au PDF) n'est pas appliqué, le PDF reste en Latin-1 :
tâche P6-T09b ajoutée au plan. Registre : F-32 (certificat en mémoire recopié de `ServerRun`).

Tests : suite complète **5062/5062** (5057 avant ; Application et Interchange : N° 80, 160, 161, 163, 164 du portage),
0 avertissement (`Travail/suite-p6-t10.log`, 13 min 51), Architecture verte dans la suite ; web vitest **1627/1627**
(1598 avant), `tsc -b`, ESLint et construction verts ; parcours **70/70** en mode Poste et **70/70** en mode Serveur.
Couverture : 223 lignes écrites (`Travail/outils-p6-t10/coverage_t10.py`) : B-8.3 entière, B-0-06 à 08, S-11-02, 03, 05,
06, 07, 09 et les 145 lignes renvoyées à P6-T10 `couvert`, sauf deux `écart`.

### P6-T09b - La police Inter intégrée au PDF (28/10/2026)

Correctif d'AR-52, relevé au portage des tests de P6-T10 : le PDF écrivait ses textes dans les polices standard de jsPDF,
en Latin-1, si bien qu'un titre en polonais ou en turc sortait avec des « ? ». Fait (révision de l'ADR 066) : les faces
Regular et Bold d'Inter en TrueType viennent du paquet npm `inter-font` 3.19.0 (OFL-1.1, livré ; fichiers officiels
d'Inter 3.019 vérifiés par leur table `name`), sorties par Vite en fichiers à part et lues seulement au moment d'un PDF
(`web/src/features/map/export/pdfFonts.ts`) ; `buildPdf` les reçoit et les enregistre comme deux familles
(`Inter-Regular`, `Inter-Bold`) ; `latin1` retirée, les textes s'écrivent tels quels. jsPDF n'écrit que les glyphes
employés et leur table ToUnicode : un PDF d'une page pèse environ 220 ko.

Tests : `export/pdf.test.ts` réécrit sur un décodeur commun (`web/src/test/pdf.ts` : fontes lues sur le disque, chaque
texte relu par la table ToUnicode de sa fonte, informations du fichier en UTF-16) : faces, tailles, places, texte
polonais et turc tel qu'écrit, deux fontes embarquées, fichier plus léger que la moitié des deux faces ;
`export/pdfFonts.test.ts` (3 tests) ; `exports.test.tsx` et `fileMenu.test.tsx` lisent les fontes du disque (jsdom ne
sert aucun fichier de l'application) ; le parcours « PDF A3 … in Inter, whatever its language » (projet « Księgowość –
Şube ») vert dans les deux modes sous la CSP de l'hôte, PDF regardé : titre, fil d'Ariane, date, classification et pied
justes. Web vitest **1636/1636** (1627 + 3 tests de `pdfFonts`, + 6 contrôles de `tokens.test.ts` sur les deux nouveaux
fichiers sources), `tsc -b`, ESLint et construction verts ; Architecture **754/754** ; aucun code C# touché (suite
complète inchangée : 5062/5062 après P6-T10). Couverture : B-2.14-14 et B-2.14-19 `corrigé (AR-52)`, citations des tests
renommés reprises (`Travail/outils-p6-t10/coverage_t09b.py`). `THIRD-PARTY-NOTICES.md` régénéré (`inter-font` livré).
Mutations : non lancées, selon la règle 2 de `a-peaufiner.md` (correctif d'affichage, ni métier ni sécurité) ; la
preuve par le texte décodé tombe si les polices standard ou le Latin-1 reviennent.

### P6-T11 - Le dossier de démonstration (28/10/2026)

Fait : `docs/recette/demonstration/` (S-P-18, S-P-19). `scenario.md` décrit 17 pas sur un projet fictif, « Notes de
frais » (code NDF, comptes `DEMO\camille`, `DEMO\karim`, `DEMO\ines`) : premier lancement, création du projet avec ses
membres, couloirs typés, dix étapes de six formes posées par double-clic, onze flèches par la barre Ctrl+K, deux fiches
(l'une validée, avec une entrée utilisée et son irritant), RACI et matrice, une étape détaillée, un détail remonté dans
le flux avec son reliquat affecté, traductions par le classeur (glossaire, 22 textes) et à la main, interface en
anglais, exports PNG, PDF et Excel, paquet exporté puis importé sous le code NDFB, corbeille, membres. `captures/` :
30 captures à 1366 × 768 ; `exports/` : les six fichiers produits (image, PDF, classeur, paquet, classeur des
traductions et le même complété). `LISEZMOI.md` : tableau des écrans principaux, mode d'emploi, limites.

Le jeu : `npm run demonstration` (`web/scripts/demonstration/`, configuration Playwright à part, hors de `web/e2e/` : ce
n'est pas un parcours) démarre le vrai hôte par le lanceur de l'ADR 067 en mode Poste et fait les gestes de
l'utilisateur, vérifie chaque résultat attendu, capture et garde les fichiers. Pour qu'aucune capture ne montre le
compte Windows ni le nom de la machine (prompt 4-5), le lanceur accepte `--account DOMAINE\compte` (mode Poste
seulement) : une fabrique de services (`FictitiousAccountFactory`) remplace le compte du processus une fois tous les
services déclarés ; `start-host.mjs` le passe depuis `E2E_ACCOUNT`, que seule la configuration de la démonstration pose.
Le produit n'a pas cette option.

Défauts relevés en jouant : F-25 (badge de fréquence sous une étape détaillée, non obstacle du routage) est pire que
noté : la pointe de la flèche « non » qui revient à NDF-E002 par le bas est cachée sous le badge « Mensuelle », et les
pastilles RACI cachent le segment horizontal de la même flèche ; tâche P6-T11b ajoutée avant la porte. F-33 (mineur) :
« Export en cours… » reste affichée après « Export terminé ». Pièges du jeu, sans défaut du produit : un champ d'usage
tapé pendant que l'hôte répond au champ précédent est remis à la valeur de l'hôte (le champ est désactivé pendant
l'envoi, une main ne peut pas y taper ; le jeu attend la réponse) ; une animation annulée rejette `finished`.

Vérifications : jeu vert en 52 s (`Travail/demonstration-p6-t11.log` ; sans trace Playwright, dont l'arrêt prenait
4 min sur un jeu aussi long) ; `tsc -b` et ESLint verts ; solution construite sans avertissement ; Architecture
**754/754** ; parcours **70/70** en mode Poste et **70/70** en mode Serveur après le changement de `start-host.mjs`
(`Travail/e2e-p6-t11-poste.log`, `Travail/e2e-p6-t11-serveur.log`) ; web vitest inchangé (aucun fichier de `web/src`
touché). Couverture : S-P-18 et S-P-19 `couvert (revue)`. Aucun code du produit touché.

### P6-T11b - Ce qui pend sous une étape s'écarte des flèches du bas (28/10/2026)

Correctif de F-25, aggravé par la démonstration : la pointe du retour « non » qui rejoint NDF-E002 par le bas était
cachée sous le badge « Mensuelle », et les pastilles RACI, centrées au même endroit, cachaient son segment. Fait (ADR 068,
AR-128, AR-58 étendu) : dès qu'une flèche part ou arrive par la pointe basse d'une étape, quel que soit son type, le
libellé d'un losange, le badge de fréquence et les pastilles RACI s'écartent de 10 px de l'axe : à droite comme AR-58,
ou à gauche quand les flèches du bas qui courent dans la bande libre sous l'étape vont toutes vers la droite.
Module pur `model/geometry/routing/under.ts` (`underSides`, après le placement sur les voies) ; `layoutFlows` rend les
flèches et ces côtés (`routeMap` garde sa forme) ; l'obstacle de l'étiquette d'un losange suit le côté ;
`StepFigure.under` remplace `bottomExit` ; classes `is-under-left` et `is-under-right` (`styles/map.css`) ; distance
`UNDER_STEP.shift` et variable `--under-shift`.

Tests : `under.test.ts` (9 : cas de la démonstration, boucle d'une ligne, flèche droite, deux sens, écart sur une voie,
virage hors de la bande, courbes) ; propriété sur les 500 cartes aléatoires (`properties.test.ts` : aucune flèche de la
pointe basse sous un badge de 60 × 18 du côté choisi tant que l'autre côté est libre ; elle échoue sur la règle
« toujours à droite », vérifié) ; `canvas/underStep.test.tsx` (2) ; `mapLayout.test.ts` adapté (une tâche qui part par le
bas s'écarte aussi) ; parcours `e2e/under-step.spec.ts` dans Chromium (points du tracé, pointe comprise, hors des boîtes
du badge et des pastilles ; il échoue sans les règles de style, vérifié). Web vitest **1651/1651**, `tsc -b`, ESLint et
construction verts ; Architecture **754/754** ; parcours **71/71** en mode Poste et **71/71** en mode Serveur
(`Travail/e2e-p6-t11b-poste.log`, `Travail/e2e-p6-t11b-serveur.log`) ; démonstration rejouée (52 s), captures et
exports refaits, regardés (13, 17, 24, image exportée) ; suite complète **5062/5062**, 0 avertissement (aucun code C#
touché, `Travail/suite-p6-t11b.log`). Couverture : B-2.9-08 et B-2.11-05 complétées (AR-128) ; parcours
ajouté à `couverture-p6/portage-parcours.md` (71). Mutations : non lancées (règle 2, correctif d'affichage), en dehors
des deux sondes ci-dessus. Restes inscrits à F-25 (long contour sous une ligne, badge long, voie de l'axe à 12 px).

### P6-T11c - Un seul point d'arrivée par côté d'une étape (28/10/2026)

Relevé par le porteur du projet sur la capture 07 de la démonstration : les deux flèches qui arrivent sur la pointe gauche du losange
NDF-E009 (celle de la ligne et celle qui monte de NDF-E008) montraient deux pointes à 6 px l'une de l'autre, la seconde à
côté de la pointe. Cause : l'ADR 059 point 4, qui lisait la propriété de la spec 9.7 « aucun chevauchement colinéaire de
plus de 8 px entre deux flèches de sources différentes » jusque sur l'arrivée et posait les arrivées d'un même côté sur
des points distincts ; le dernier segment changé de voie emportait le point d'arrivée. Décidé par le porteur du projet (AR-129,
révision de l'ADR 059) : comme en 1.x, les flèches qui arrivent sur un même côté d'une étape partagent leur dernier
segment et finissent sur un seul point, sur les quatre côtés et pour les boucles par le bas ; deux sorties d'un losange
ne partagent toujours jamais un point (AR-24).

Fait : `routing/tracks.ts` (clé d'arrivée du dernier segment, partage de voie par groupe ou par arrivée, derniers segments
d'une même arrivée placés ensemble) ; commentaire du `Draft` (`routes.ts`). Tests : `routing.test.ts`, le cas « points
distincts » inversé et deux cas ajoutés (trois boucles vers le bas d'une étape ; deux sorties d'un losange vers un même
côté) ; `properties.test.ts` sur les 500 cartes : « les flèches d'un même côté finissent sur un seul point », et le
chevauchement admis sur le seul dernier segment commun. Sondes sur une copie de `web` : sans la clé d'arrivée, les trois
cas et la propriété échouent ; sans le placement d'ensemble, la propriété échoue encore. Web vitest **1653/1653**,
`tsc -b` et ESLint verts ; Architecture **754/754** ; parcours **71/71** en mode Poste et **71/71** en mode Serveur
(`Travail/e2e-p6-t11c-poste.log`, `Travail/e2e-p6-t11c-serveur.log`) ; démonstration rejouée (57 s,
`Travail/demonstration-p6-t11c.log`), captures 07, 08 et 13 regardées : une seule pointe par arrivée. Suite complète non
relancée : aucun code C# touché. Couverture : S-9.7-02 et B-3.4-06, B-3.4-13, B-3.4-34 (noms des tests de propriétés),
ligne 62 de `couverture-p6/portage-interface.md`.

### Porte P6-T12 - round 1 (28/10/2026)

Cinq contrôleurs neufs (consignes `docs/consignes-porte-p6.md`, déclinées de celles de la phase 5 avec le contrôleur
Interface et charte ; comptes rendus `Travail/porte-p6-r1-<mandat>.md`). Chacun sur son port et ses dossiers de sortie ;
le mode Serveur, à une seule base, joué par le seul contrôleur Intégrité, qui rejoue aussi la suite complète.

| Round | Contrôleurs | Bloquants | Majeurs | Mineurs | Notes |
|---|---|---|---|---|---|
| 1 | Fidélité ; Architecture ; Sécurité ; Interface et charte ; Intégrité (agents neufs) | 2 + 0 + 0 + 1 + 0 = 3 | 3 + 2 + 2 + 6 + 0 = 13 (11 distincts) | 4 + 9 + 4 + 8 + 5 | Intégrité franchit : vitest 1653/1653, suite complète 5062, 0 avertissement, parcours 71/71 dans les deux modes, 33 mutations rejouées tuées, tables de portage justes. Bloquants : aucun projet d'exemple au premier lancement (S-6.1-10) ; classification ni affichée ni réglable (S-4.1-07) ; fiche et panneau RACI inaccessibles au clavier (NF08). Majeurs : « Quitter » absent de l'interface du mode Poste ; libellé du groupe de partage non réglable ; copie de la matrice RACI qui colle des formules dans Excel ; focus invisible des « + » ; contraste d'une aide de la fiche ; « ! » d'un losange sur son code ; étiquette de flèche loin de sa flèche ; menus contextuels hors de la maquette (deux radios cochés) ; `data-testid` manquants ; couverture de la phase non close (67 lignes `à faire` ou `couvert (API)`). |

Plan des corrections : `Travail/porte-p6-r1-corrections.md` ; lot A (interface de la carte et de la fiche), lot B
(projet et serveur : exemple, classification, propriétés du projet, Quitter), lot D (couverture, parcours de mesure),
confiés à trois agents d'exécution aux fichiers séparés ; AR-130 (copie de la matrice) fait directement. Mineurs au
registre `a-peaufiner.md` (F-34 à F-38).

### Porte P6-T12 - corrections du round 1 (28 et 29/10/2026)

Quatre agents d'exécution aux fichiers séparés, puis l'intégration (comptes rendus `Travail/p6-r1-lot-a.md` à
`p6-r1-lot-e.md`, plan et décisions `Travail/porte-p6-r1-corrections.md`) :

| Lot | Constats | Fait | Preuves |
|---|---|---|---|
| A, interface | C3, C7 à C12, m6, A4 | Entrée sur une étape = clic, « Ouvrir la fiche » et « Rôles RACI… » au menu, focus pris au clavier et rendu à l'étape (AR-136, AR-137) ; focus visible des « + » ; jeton `--text-muted-on-danger-soft` (AR-138) ; « ! » sur le côté haut gauche du losange (AR-140) ; étiquettes sur ou près de leur flèche avant la bande (AR-139, ADR 059 révisée) ; menus de la maquette, un choix coché par groupe (AR-141) ; notifications à gauche d'un panneau (AR-142) ; `data-testid` et test statique de S-3.5-04 ; entrée « Propriétés du projet… » du menu Fichier | 14 sondes, chacune tue ses tests ; propriété des étiquettes sur 500 cartes (418 étiquettes à plus de 24 px avant, 125 après, toutes expliquées) ; défaut vu dans Chromium et corrigé : une seule Entrée ouvrait le panneau RACI et y posait une lettre |
| B, projet et serveur | C1, C2, C4, C5, C12 (fil d'Ariane) | `ProfileView.desktop` (AR-131) ; projet d'exemple par `POST /api/desktop/sample-project`, paquet `samples/conges.topogenia` embarqué (AR-132) ; classification choisie, montrée, changée par l'animateur par `PUT /api/projects/{id}/classification`, tracée (AR-133) ; dialogue « Propriétés du projet » (AR-134) ; « Quitter » au menu du compte du mode Poste (AR-135) ; ADR 069, ADR 058 point 3 amendé | 14 mutations web et 11 C# tuées ; les deux routes dans la table, la matrice des droits et la couverture `If-Match` |
| D, couverture | C13, Fidélité m1 à m3, Intégrité 2 et 3 | 59 lignes fermées sur 63 ; 7 fichiers de règles dans `web/src/test/` (42 tests) ; 11 parcours nouveaux ; mesures d'ouverture et de fluidité (`web/e2e/measures/`, `E2E_MEASURE=1`, `docs/couverture-p6/mesures-nf03-nf04.md`) ; passage complet dans Edge 156 (`E2E_BROWSER=msedge`) ; citations périmées remplacées ; port du second hôte de `launch.spec.ts` dérivé de `E2E_PORT` | 24 sondes vitest et 8 copies fautives sur le vrai hôte, toutes tuées ; défauts relevés dans les lots A et B, corrigés par eux : textes en dur, un écouteur de touches à côté du gestionnaire unique, signature de `notify` écrite trois fois, variable `--swatch` perdue |
| E, parcours | S-6.1-10, S-4.1-07, B-4-69, B-8.1-44, S-5.2-16, S-3.5-04 | 6 parcours (`sample-project`, `project-properties`, `quit` sur un hôte à lui) ; fil d'Ariane du parcours de l'animateur par `breadcrumb-level-<n>` | 13 sondes sur 14 en mode Poste (la survivante, une classification sans `If-Match`, est permise en mode Poste) et 10 sur 10 en mode Serveur ; le lecteur reste simulé (rôle donné à la page, ADR 067 § 6) |

Intégration (moi) : AR-130 (copie de la matrice RACI sans formule exécutable) ; langue d'origine dans `BreadcrumbView`,
fil d'Ariane et sous-titre du PDF par `localizedName` (B-8.1-23, contrat régénéré, un test de l'application) ; aides des
seuls tests déplacées dans `web/src/test/routedMaps.ts`, `sameView` mort retiré avec son test (B-8.1-41) ; chiffres
tabulaires des durées saisies et lues (S-9.5-01) ; `e2e/files.spec.ts` (troisième entrée après les exports) ; attente des
animations d'axe-core en boucle, une animation annulée ne fait plus échouer l'analyse (la transition du bouton qui
ouvre un dialogue, coupée par lui : `accessibility.spec.ts` tombait au premier parcours de chaque hôte neuf) ; parcours
de l'animateur sur les constantes de la géométrie ; `architecture.md` section 6 et `architecture-detaillee.md` (A9) ;
couverture : 22 lignes des lots A et B, 6 du lot E, S-NF-06 fermée, ligne S-5.5-08 (audit de la classification), deux
routes à la table ; F-39, m1 versé à F-25.

Résultats : construction à 0 avertissement ; suite complète **5089/5089** (11 min 55,
`Travail/suite-p6-r1-integration.log`) ; Architecture **754/754** ; vitest **1779/1779**, `tsc -b` et ESLint verts ;
parcours **88/88** en mode Poste (4,8 min) et **88/88** en mode Serveur (7,3 min) (`Travail/e2e-p6-r1-integration-*.log`) ;
démonstration verte en 1 min 12 (`Travail/demonstration-p6-r1-integration.log`), captures relues (une pointe par
arrivée, menu du couloir de la maquette, « ! » hors du code, étiquette « 1 » sur sa flèche) ; fluidité sur 300 étapes
tenue aux quatre passages machine calme (médianes 16,7 ms, 95e centiles 16,8 ms au plus) ; relevé des citations de la
couverture : aucune périmée. Couverture : plus aucune ligne de la phase 6 ouverte ; restent 92 lignes `à faire`
(phase 7 et lignes transverses vérifiées à la recette finale) et une ligne `partiel` de la phase 4 (S-5.4-13).

### Porte P6-T12 - round 2 et ses corrections (29/10/2026)

| Round | Contrôleurs | Bloquants | Majeurs | Mineurs | Notes |
|---|---|---|---|---|---|
| 2 | Fidélité ; Architecture ; Sécurité ; Interface et charte ; Intégrité (agents neufs, mandat complet) | 0 | 0 + 1 + 0 + 0 + 0 = 1 | 4 + 6 + 1 + 5 + 3 | Fidélité, Sécurité, Interface et charte, Intégrité **franchissent** : tous les bloquants et majeurs du round 1 de leur mandat corrigés et prouvés (mutations, sondes, captures, parcours dans Chromium et Edge, démonstration rejouée sans violation axe-core ; Intégrité retrouve tous les chiffres d'`etat.md`). Majeur Architecture : `THIRD-PARTY-NOTICES.md` déclarait `buffers` 0.1.1 « livré » sans licence, alors qu'il n'est pas dans l'interface construite. |

Corrections (moi, sans agent ; `Travail/porte-p6-r2-corrections.md`) :
- **Majeur** : `build/third-party-notices.mjs` calcule ce qui est servi au navigateur (graphe de `package-lock.json` depuis
  les dépendances de l'interface, sans la partie Node d'exceljs, servi par son paquet navigateur) ; usage `non servi`
  pour 161 paquets, dont `buffers` ; test `ThirdPartyNoticesTests` (licence permissive de tout composant livré, ou
  exception écrite en ADR) ; sonde : sur l'ancien fichier, il nomme `buffers` et `chainsaw`.
- **Mineurs corrigés** : « Quitter » envoie d'abord les saisies en attente de la fiche et reste ouvert si l'une ne part
  pas (AR-143, `shared/state/pendingWrites.ts`) ; focus du dialogue des propriétés sur son premier champ une fois le
  projet lu ; menu du compte sous son bouton ; notifications au-dessus de la mini-carte et du zoom quand un panneau est
  ouvert (AR-142 précisé) ; type de la classification hors du composant ; test des clés des fichiers de langue
  (`translationKeys.test.ts`) ; horloge fixée dans le test du PDF ; borne de 60 px de la propriété des étiquettes ;
  clic du calque RACI prouvé sur une fin et un losange de jonction ; parcours `e2e/keyboard.spec.ts` (fiche et RACI au
  clavier sur le vrai hôte, axe-core sur un irritant, les propriétés et le menu du compte), qui a trouvé un défaut : un
  panneau RACI sans lettre à donner (étape du seul couloir) ne prenait pas le focus (corrigé, AR-137 précisé) ;
  documents d'architecture ; couverture (B-2.4-30 `étendu`, S-D04-01 et S-3.1-07 à 09 fermées, un test nouveau pour
  S-3.1-09, passages complets à jour). Mineurs gardés : F-40, F-34 complété. Chaque correction de comportement mise en
  échec par une sonde.

Résultats : construction à 0 avertissement ; Architecture **757/757** ; vitest **1792/1792** ; parcours **90/90** en mode
Poste, en mode Serveur et dans Edge ; démonstration verte en 55 s ; relevé des citations sans périmée. Aucun code C# de
`src/` changé depuis la suite complète 5089/5089. Round 3 : Architecture seule, mandat resserré.

### Porte P6-T12 - round 3 et ses corrections (29/10/2026)

| Round | Contrôleurs | Bloquants | Majeurs | Mineurs | Notes |
|---|---|---|---|---|---|
| 3 | Architecture (agent neuf, mandat resserré) | 0 | 1 | 3 | R2-M1 corrigé pour ce qu'il visait (`buffers` hors de l'interface construite, fichier régénéré identique, sondes tuées). Majeur nouveau : le paquet navigateur d'exceljs servi est une construction browserify qui embarque 68 paquets, dont 39 absents de `THIRD-PARTY-NOTICES.md`, et `uuid` en série 8. |

Corrections (`Travail/porte-p6-r3-corrections.md`) : le générateur lit la carte des sources d'`exceljs.min.js` et écrit
une ligne `livré` par paquet embarqué (licence du verrouillage, du `package.json` embarqué, sinon relevée à la
construction et à confirmer avec la SBOM de P7-T03) ; `ThirdPartyNoticesTests` vérifie que chaque paquet du verrouillage
et de la carte a sa ligne ; ADR 065 précisée sur `uuid` ; documents d'architecture ; mineurs au registre (F-35, F-40).
Architecture 758/758. Round 4 : Architecture seule, mandat resserré (dernier round permis par le prompt 7).

### Porte P6-T12 - round 4 : porte franchie (29/10/2026)

| Round | Contrôleurs | Bloquants | Majeurs | Mineurs | Notes |
|---|---|---|---|---|---|
| 4 | Architecture (agent neuf, mandat resserré) | 0 | 0 | 4 | R3-M1 corrigé et prouvé : les 68 paquets de la carte d'`exceljs.min.js` ont leur ligne « livré », licences du verrouillage et du `package.json` embarqué justes, 38 « relevées » dites comme telles (3 confirmées sur des copies locales), fichier régénéré identique, quatre sondes tuées. Mineurs au registre (F-41, repris à P7-T03) ; le compte de la suite complète rejoué. |

**Porte P6-T12 franchie au round 4 : phase 6 terminée.** Rounds 1 et 2 à cinq contrôleurs neufs, 3 et 4 Architecture
seule (les quatre autres mandats franchis au round 2). Corrections : AR-130 à AR-143, ADR 069 et 070, révisions des
ADR 058, 059 et 065. Dernière suite complète seule : **5093/5093**, 0 avertissement (`Travail/suite-p6-fin.log`,
12 min 20) ; Architecture 758/758 ; vitest 1792/1792 ; parcours 90/90 en mode Poste, en mode Serveur et dans Edge ;
démonstration verte ; couverture : aucune ligne de la phase 6 ouverte. Mineurs de la phase au registre (F-24 à F-41).
Reste au porteur du projet : la validation de la démonstration (`docs/recette/demonstration/`). Prochain pas : phase 7 (P7-T01).

## Phase 7 - Paquets et recette

### P7-T01 - Publication du mode Poste (29/10/2026, ADR 071, AR-144)

- **Second lancement** (spec 6.1, S-6.1-09 ; jusqu'ici `INSTANCE_DEJA_LANCEE`) : l'application en cours écoute deux
  événements nommés de la session de l'utilisateur (`Desktop/DesktopReopen.cs`) ; le second lancement signale « open »,
  l'application tire un jeton de lancement neuf (`DesktopSession.Renew`) et ouvre le navigateur dessus, puis répond ;
  le second processus finit avec le code 0 et le dit (`StartupHandedOverException`, message `DEJA_LANCEE_OUVERTE`).
  Un premier essai par un canal nommé a été refusé par la règle D14 du code livré (un client de canal nommé peut joindre
  une autre machine) : deux événements suffisent et ne quittent pas la machine. Le test qui attendait le refus du
  second lancement est remplacé par celui de la main passée ; le refus reste pour un verrou tenu sans réponse.
  `IBrowserLauncher` et `StartupConsole` enregistrés par `TryAdd` (un test donne les siens).
- **Publication** : profil `src/Topogenia.Host/Properties/PublishProfiles/Desktop.pubxml` (`win-x64`, autonome, fichier
  unique compressé, bibliothèques natives à côté de l'exécutable : extraites, elles s'écriraient sous `%TEMP%` ; ni IIS,
  ni symboles, ni documentation, ni `web.config` ; messages non recopiés) ; `build/package.ps1 -Mode Desktop` : ZIP portable
  (exécutable, natifs, `wwwroot`, `README.txt` en français et en anglais, `licenses/` : Apache-2.0, `NOTICE`, licence du
  cœur, composants tiers, Inter, Lucide) et son empreinte SHA-256 dans `artifacts/` (ignoré par `.gitignore`).
- **Vérification de l'exécutable décompressé** (`docs/couverture-p7/verification-poste.md`, toujours avec ses réglages :
  dossier de travail, `OpenBrowser=false`) : prêt en 4,2 s au premier lancement (cible 5 s ; 6,5 s une fois sur une copie
  toute neuve que l'antivirus examinait), 2,2 s ensuite ; second lancement en 1,4 s, code 0 ; écritures dans le seul
  dossier du produit, rien sous `%TEMP%` ; 401 sans session. ZIP de 51 Mo (exécutable de 55 Mo).
- **Tests** : `DesktopReopenTests` (3), `HostCommandsTests` (2), `PublicationRulesTests` (6) ; sonde (jeton non
  renouvelé) tuée par trois tests. Architecture 764/764.
- **Couverture** : S-6.1-04 à 07, 09, 12, 13 couvertes ; S-6.1-01 à 03 et 08 (MSIX, installation, raccourci) sans objet
  depuis l'ADR 049.
- **Résultats** : construction à 0 avertissement ; suite complète **5103/5103** (`Travail/suite-p7-t01.log`, 11 min 42) ;
  parcours 90/90 en mode Poste et 90/90 en mode Serveur (`Travail/e2e-p7-t01-*.log`).

### P7-T02 - Publication du mode Serveur et charge de 30 clients (29/10/2026, ADR 072, AR-145)

- **Publication** : profil `Server.pubxml` (`win-x64`, dépendant du Hosting Bundle .NET 10) : 84 fichiers, 22 Mo ;
  `src/Topogenia.Host/web.config` complété du module ASP.NET Core (requête de 101 Mio pour un paquet de 100 Mio, la limite
  d'IIS par défaut étant de 30 Mo ; `removeServerHeader` ; `X-Powered-By` retiré ; écarté de la publication du mode Poste) ;
  `build/server/appsettings.Production.example.json` (fictif, sans mot de passe) et `server-readme.txt` ; un seul script de
  paquet pour les deux modes, `build/package.ps1 -Mode Desktop|Server` (le ZIP du serveur attend le guide de P7-T04).
- **Hôte publié essayé en mode Serveur** (`docs/couverture-p7/verification-serveur.md`) : Kestrel en HTTPS avec un
  certificat de test en mémoire (aucun magasin touché), `--migrate` sur une base absente (11 mises à jour), santé anonyme
  minimale, profil en Negotiate, interface refusée sans authentification ; base `Topogenia_Charge` supprimée à la fin.
- **Charge de 30 clients** (`build/load/ServerLoad.cs`, programme C# d'un fichier, jamais livré) : avec une seconde entre
  deux gestes, 95e centile de 48 à 101 ms pour la carte et la fiche, 542 ms pour la liste des projets de l'accueil ; en
  rafale, 1,4 s pour l'accueil, moins de 0,7 s pour le reste ; 2 334 requêtes par passage, aucun échec. S-NF-09 couverte
  (mesure) ; F-42 pour la liste des projets.
- **Renvoyé à la recette d'installation** : IIS (module, `web.config`, authentification Windows) et le démarrage sous un
  compte membre du seul rôle `topogenia_app` (AR-110 ajusté : ce compte de connexion sortirait des bases `Topogenia_*`).
- **Tests** : `PublicationRulesTests` (profil du serveur, `web.config`, script commun) ; Architecture 766/766.

### P7-T03 - SBOM, composants tiers, empreintes et analyse (29/10/2026, ADR 073, AR-146)

- **Le servi lu dans la construction** : greffon de `web/vite.config.ts` qui écrit `web/served-packages.json` (40 paquets,
  par leur chemin dans le verrouillage, et les fichiers déjà assemblés servis : `exceljs.min.js`, `jspdf.es.min.js`) ;
  `build/components.mjs` en tire les composants, dont les paquets embarqués (carte d'`exceljs.min.js`, et les dépendances
  de `jszip.min.js`, sans carte) ; `THIRD-PARTY-NOTICES.md` : 147 livrés, dont 72 embarqués, 209 non servis.
- **`--check`** du générateur : `ThirdPartyNoticesTests` l'appelle (sonde : `react` passé à la main à « non servi », le test
  nomme la ligne) ; le script de paquet refuse de livrer sans lui.
- **SBOM** CycloneDX 1.5 (`build/sbom.mjs`) par ZIP, dans le ZIP et à côté : paquet du mode Poste refait avec la sienne
  (35 composants .NET dont les deux packs du runtime, 40 npm portant 72 embarqués).
- **Paquet** : `build/package.ps1` construit l'interface, vérifie les tiers, publie, signe si un certificat est donné
  (`signtool`, non essayé : aucun certificat), écrit la SBOM, le ZIP et l'empreinte ; commandes externes jugées sur leur
  code de sortie (sous PowerShell 5.1, l'avertissement de Vite sur la sortie d'erreur arrêtait le script ; `npm.cmd`, car
  l'enveloppe `npm.ps1` comprenait mal la commande).
- **CodeQL** : `build/codeql-local.ps1` (CLI absente du poste : il le dit et finit avec le code 3, rien d'installé).
- **Couverture** : S-3.1-16, S-5.4-18, S-5.4-20 couvertes ; S-11-24 en écart (ADR 049). F-31 et F-41 traités. Tests :
  `ThirdPartyNoticesTests` (5), `PublicationRulesTests` (9) ; Architecture 767/767.

### P7-T04 - Exploitation et sécurité (29/10/2026)

- **Guides** (rédigés par un agent sur les ADR, la spécification et le code, chaque clé, commande et code vérifiés dans le
  code ; relus) : `docs/exploitation-poste.md` (8 sections : copie et empreinte, lancement, données et `settings.json`,
  sauvegardes et restauration, mise à jour, refus du démarrage dont `VERROU_MIGRATIONS`, ligne de commande, écriture hors
  application) et `docs/exploitation-serveur.md` (15 sections, dont IIS, `--migrate` application arrêtée sous `db_owner`,
  lecture des 18456 et 4060, supervision, et une recette d'installation en 15 contrôles pour ce que le poste de
  construction ne peut pas faire). Ce que le code ne tranche pas y est écrit comme une étape d'exploitation.
- **`docs/securite.md`** : la liste des 57 exigences de la section 5, écrite par `build/security-list.mjs` depuis la
  couverture (`--check`, appelé par `ThirdPartyNoticesTests`) : 51 cochées ; revue OWASP ASVS 4.0 niveau 2 par chapitre
  et TISAX / VDA ISA ; restes (signature, audits de l'intégration continue jamais joués, IIS et compte du seul rôle à
  l'installation, S-5.4-13).
- **Corrigé au passage** : `Cache-Control: no-store` sur toute réponse de l'API (ASVS V8.2.1, F-36 m-3,
  `CommonHeadersTests`) ; un `settings.json` fautif fait dire sa phrase et la propriété à la console au lieu du code brut
  sur la sortie d'erreur (relevé par la rédaction, `HostCommandsTests`) ; aucun processus lancé par le code livré que le
  navigateur (`PublicationRulesTests`, S-5.4-21). Relevés mineurs : F-43.
- **Couverture** : S-5.1-01, S-5.1-02, S-5.2-03, S-5.3-04, S-5.4-21, S-5.4-23, S-6.2-04 couvertes ; S-5.2-15 et S-5.3-01
  renvoyées à la recette ; S-5.4-16, S-5.4-17 (prompt 4.3) et S-5.4-19 (ADR 049) en écart.
- **ZIP du serveur** construit (`Topogenia-2.0.0-server.zip`, 9 Mo : application, `web.config`, exemple de réglages,
  guide, README, SBOM, licences) ; sa SBOM lit le `deps.json` de la publication (33 composants .NET et les deux frameworks
  du Hosting Bundle), celle du Poste celui de la construction du fichier unique (35, packs du runtime compris).
- **Résultats** : construction à 0 avertissement ; suite complète **5111/5111** (`Travail/suite-p7-t04.log`, 12 min 17) ;
  parcours 90/90 en mode Poste et 90/90 en mode Serveur (`Travail/e2e-p7-t04-*.log`) ; Architecture 769/769.

### P7-T04b - Cœur fermé (29/10/2026)

- **Nom du produit** : `ProductIdentity.cs` passe du domaine à l'application (`Topogenia.Application`) ; lu là par
  `Directory.Build.props`, `build/sbom.mjs` et `web/scripts/build-mockup.mjs` ; les règles qui nommaient l'assemblage du
  domaine par ce type le nomment par `DomainException`.
- **Bascule** `UseCorePackage` (`Directory.Build.props`) : le projet source du domaine là où ses sources sont, le paquet
  `Topogenia.Core` (2.0.0, `CoreFeed`) ailleurs, dans l'application, l'échange et la persistance.
- **Chaîne** `build/core/Topogenia.Core.csproj` : une commande `dotnet pack ... -p:CoreKeyFile=<clé>` construit le domaine
  signé (clé créée par `sn.exe -k 2048` hors du dépôt, `45_Topogenia\Cle-coeur\core.snk`, jeton `0124debc601359fe`),
  l'obscurcit par Obfuscar 2.2.50 (`build/core/obfuscar.xml` : API publique gardée, 2 883 noms privés et internes
  renommés, chaînes intactes, nom fort redonné), emballe l'assemblage, la documentation de sa seule API publique
  (`build/core/public-docs.mjs`, 597 membres sur 750) et `LICENSE.md`, écrit l'empreinte et vide la copie du cache NuGet.
  Un premier script PowerShell qui créait la clé par la CryptoAPI a été verrouillé puis supprimé par l'antivirus dès son écriture, sans avoir tourné : la chaîne est passée en MSBuild, la clé par `sn.exe`.
- **Dépôt public** : `build/split.mjs` l'écrit hors de la copie de travail (ce que Git garderait, sans le domaine, ses
  tests, `build/core` ni lui-même ; solution sans les deux projets ; paquet dans `artifacts/core-feed`) et le prouve :
  essai 3 du 29/10/2026, 3 738 tests : 3 692 verts, 46 sautés (règles qui lisent les sources du cœur, `CoreSources.Require`), 0 échec, en 14 min (essai 1 : 2 échecs du contrat OpenAPI ; essai 2 : 1 échec, `ServerRunTests.A_refusal_over_https_carries_hsts_too` ; tous deux corrigés) (`Travail/split-p7-t04b.log`). La copie de travail tient lieu de dépôt privé (AR-147, écart à l'ADR 048).
- **Règles d'architecture** : 35 échouaient dans la copie publique (sources du domaine absentes, et code généré de
  l'assemblage obscurci que ArchUnitNET ne reconnaît plus à son nom) : sautées là avec leur raison (`CoreSources.Require`),
  la règle domaine et application y garde l'application, les règles des projets admettent le paquet du cœur sous la
  condition de la bascule ; `The_domain_is_built_from_its_sources_or_comes_signed_from_its_package` interdit de sauter
  sans le paquet signé. Copie de travail : 780/780, rien de sauté (29/10, après P7-T07).
- **Contrat OpenAPI** : la première preuve a échoué sur `ApiContractTests` (descriptions des types du domaine absentes : le
  générateur ne lit que la documentation des références de projet) ; le paquet porte la documentation de l'API publique
  et l'hôte la donne au générateur dans le dépôt public (`CoreDocumentationForOpenApi`).
- **Composants** : le paquet du cœur écarté de `THIRD-PARTY-NOTICES.md` (composant du produit), porté par la SBOM du
  dépôt public ; `build/package.ps1` prend la licence du cœur dans son paquet quand ses sources manquent.
- **Docs** : ADR 074, AR-147, révisions des ADR 048 et 013, `CONTRIBUTING.md`, `build/README.md`, S-P-02.

### P7-T07 - Passe de finition du registre (29/10/2026)

- **Corrections** : HSTS sur toute réponse HTTPS du mode Serveur, refus compris (`HostPipeline`, `ErrorMiddleware`, ADR 045
  complétée, F-05) ; phrases de `DROITS_INSUFFISANTS` et `SERVEUR_DROITS_INSUFFISANTS` (F-07, F-43) ; règle
  `SourceRulesTests.Shipped_code_opens_files_only_where_listed_and_never_reads_a_document_there` (S-5.4-13, F-06,
  `docs/securite.md` régénéré : 52 sur 57) ; prénom du porteur retiré des documents et de `walk.ts` (il reste dans les
  seules mentions de droit d'auteur) ; table des routes de `docs/couverture.md` complétée ; port mort `ICounterService`
  retiré avec `CounterService` et `WorkspaceLoader.CountersAsync` (constat m2 de la porte P4, AR-148, F-01) : les deux
  tests des compteurs passent par le chargement de l'espace.
- **Registre** : chaque entrée de `docs/a-peaufiner.md` a son issue (section « Passe de finition ») : fait, gardé (écart
  sans effet sur les données ni la sécurité, dit au rapport final) ou recette (repris dans les contrôles de
  `docs/recette/finale/LISEZMOI.md`).
- **Couverture** : les 57 lignes `à faire` traitées sur les propositions d'un agent, vérifiées une à une
  (`Travail/outils-p7/preuves-a-faire.md`) ; les 1 994 tests cités existent (`Travail/outils-p7/check_citations.py`).
- **Vérifications** : suite complète 5 123/5 123 (`Travail/suite-p7-t07.log`), vitest 1 792/1 792, parcours 90/90 dans les
  deux modes ; après AR-148 : `WorkspaceLoaderTests` 20/20, Architecture 780/780.

### P7-T05 - Recette finale et rapport (29/10/2026)

- **Dépôt public** prouvé une troisième fois (voir P7-T04b) : 3 692 verts, 46 sautés, 0 échec.
- **Paquets** : les deux ZIP refaits, empreintes dans `docs/recette/finale/LISEZMOI.md`. Défaut trouvé : le ZIP du Serveur
  construit juste après celui du Poste échouait à la vérification de `THIRD-PARTY-NOTICES.md`. La publication autonome
  du Poste restaure l'hôte pour `win-x64` avec le paquet `Microsoft.NET.ILLink.Tasks`, outil du SDK jamais livré (la SBOM,
  lue dans le `deps.json`, ne le porte pas), et laisse ce graphe dans `obj/project.assets.json` ; la vérification le lisait
  comme un composant livré. `build/package.ps1` restaure l'hôte avant la vérification (ADR 073 § 4) ; prouvé en rejouant
  Poste puis Serveur (`Travail/package-p7-t05-*.log`).
- **Recette** : `docs/recette/finale/LISEZMOI.md` complet (empreintes, état de la couverture) ; S-11-12 et S-P-20
  `couvert (revue)` : plus aucune ligne `à faire` dans `docs/couverture.md`.
- **Dernier passage** : construction 0 avertissement ; suite complète 5 123/5 123, 0 sauté (`Travail/suite-p7-t05.log`,
  13 min 58) ; parcours 90/90 en mode Poste (4,7 min) et en mode Serveur (7,0 min) (`Travail/e2e-p7-t05-*.log`) ; le web
  n'a pas changé depuis son passage vert de P7-T07. Comptes par niveau à l'exécution : domaine 1 385 (1 382 à la
  découverte : trois théories se déroulent à l'exécution), application 405, persistance 1 119, échange 262, API 1 172,
  architecture 780.
- **Rapport** : `docs/RAPPORT_FINAL.md` (prompt 9).

### Porte P7-T06 - R4 : chaîne des paquets sans script du système (29/10/2026)

- **Origine** : l'antivirus du poste de construction a mis en quarantaine `build/package.ps1` avant tout lancement ;
  remarque R4 du porteur, qui a choisi la chaîne 100 % Node (AR-150, ADR 076).
- **Fait** : `build/package.ps1` supprimé par le porteur, `build/package.mjs <Desktop|Server>` seul point d'entrée (outils
  lancés sans interpréteur de commandes : `tsc` et Vite par `node`, `dotnet`, `signtool`) ; `build/archive.proj` (tâches
  `ZipDirectory` et `Unzip` du SDK) au lieu de `tar.exe`, ZIP relu et comparé octet pour octet au dossier ; empreinte de
  chaque fichier du ZIP (`.files.sha256`) ; `build/codeql-local.ps1` réécrit en `build/codeql-local.mjs` ; exécutable
  unique non compressé (`Desktop.pubxml`) ; `SuppressIldasm=false` (`build/core/obfuscar.xml`) ; § « Antivirus et EDR »
  des deux guides ; contrôle P-07 de la recette ; ADR 049, 071 à 074 révisés.
- **Règles** : `PublicationRulesTests` relit `package.mjs` ; deux règles neuves (aucun script du système, outils lancés
  directement, archives par le SDK ; exécutable non compressé, cœur sans `SuppressIldasm`, empreintes des fichiers) ;
  `.proj` dans les règles d'en-tête, de format et du nom du produit. Le ZIP et l'archive ont été essayés sur un dossier
  factice (barres obliques dans les entrées, relecture identique) ; aucun ZIP du produit construit, à la demande du
  porteur, qui essaie la chaîne d'abord.
- **Vérifications** : Architecture 853/853 ; `node --check` des deux scripts ; `security-list.mjs --check`.

### Porte P7-T06 - R1, R3, R2 : remarques de l'essai du porteur (29/10/2026)

- **R1, AR-151** (révision de l'ADR 070) : le clic sur une étape la choisit sans rien ouvrir ; Entrée ou « Ouvrir la
  fiche » ouvre la fiche ; une fiche déjà ouverte suit l'étape cliquée ; un nouveau clic sur l'étape choisie seule la
  renomme sur place après le délai d'un double-clic (500 ms) et si elle a encore le focus ; F2 inchangé ; une lettre
  tapée sur l'étape choisie seule commence son nom (état `editingSeed`, curseur après la lettre ; V et H choisissent un
  outil seulement sans étape choisie seule ; AltGr écrit) ; au doigt, un toucher ouvre toujours la fiche (la tablette en
  lecture n'a pas de touche Entrée, S-NF-03 ; trouvé par la vérification des titres cités). Défaut trouvé en écrivant les parcours et corrigé : un clic
  sur l'étape choisie suivi d'Entrée ouvrait la fiche puis lançait le renommage, qui lui prenait le focus ; le renommage
  attend que l'étape ait encore le focus.
- **R3, AR-152** : le type choisi dans la palette ne vaut que pour la création suivante (double-clic sur le fond,
  insertions), remis à « Étape » quand elle réussit, gardé quand elle est refusée ; libellés « Type de la prochaine
  étape », « Losange de la prochaine étape ».
- **R2, AR-153** : curseurs `web/src/styles/cursors/link-spool.svg` (bobine) et `link-thread.svg` (fil déroulé, classe
  `has-link-source`), à l'encre et au blanc de la charte, point actif au bout du fil, sur le fond, les étapes, les
  poignées et les flèches ; rendus dans Chromium pour les regarder (`Travail/outils-r2/render-cursors.mjs`) ; servis
  par l'hôte et lus sous sa CSP (parcours).
- **Tests** : `canvas/naming.test.tsx` (7), `styles/cursors.test.ts` (5), tests de la fiche et du clic repris (aide
  `openSheetOf` dans `src/test/editor.tsx` et `e2e/support/journey.ts` : 24 ouvertures de fiche des parcours et 3 de
  la démonstration passées par Entrée), parcours du curseur ; 13 mutants tués (délai, toucher, déplacement, Maj, étape choisie au moment de l'appui, droits,
  double-clic, fiche qui suit, focus, lettre et sélection, ordre des couches du clavier, curseur de saisie). Deux règles
  du dépôt ont repris le code : un `useRef` qui ne tient ni élément ni booléen (`interfaceLayout.test.ts`), et
  `getModifierState` lu comme le mot français « Modifier » (`Typescript_identifiers_are_english`).
- **Couverture** : B-2.3-13 remplacé (AR-151), B-2.1-15 et B-2.1-17 remplacés (AR-152), B-2.1-22 étendu (AR-153) ;
  scénario de la démonstration (pas 6 et 7).
- **Vérification complète** (après R1 à R4 et les corrections du round 1) : construction 0 avertissement ; suite .NET
  **5 188/5 188**, 0 sauté (`Travail/suite-r1r3.log`, 15 min ; à la découverte : domaine 1 366, application 405,
  persistance 1 119, échange 261, hôte 1 181, architecture 853) ; web : `tsc -b`, ESLint, vitest **1 805/1 805** ;
  parcours **91/91** en mode Poste (4,5 min) et en mode Serveur (5,7 min) ; démonstration rejouée (30 captures) ;
  `third-party-notices.mjs --check` et `security-list.mjs --check` à jour. Aucun ZIP construit : le porteur essaie
  d'abord la chaîne des paquets et l'interface.

### Porte P7-T06 - AR-154 : Grouper, Dissocier, import du paquet depuis la carte (29/10/2026)

- **Origine** : essai du porteur sur un paquet `.topogenia` écrit par claude.ai (processus de vente, 15 cartes sur
  4 niveaux, importé du premier coup et réexporté au même contenu). Il cherchait « grouper » (nommé « Détailler »), ne
  trouvait pas son inverse (« Remonter le détail dans le flux »), ni d'import de paquet au menu Fichier de la carte ; un
  paquet choisi dans le dialogue d'un .pmap s'y affichait en texte illisible.
- **AR-154** (révision de l'ADR 063), mots et raccourcis d'Office et de Visio : « Grouper » et « Dissocier » au ruban à
  côté de « Détailler » (une étape seule), Ctrl+G et Ctrl+Maj+G pris au navigateur seulement quand ils s'appliquent ;
  « Grouper en sous-processus » au menu de la sélection, « Dissocier le sous-processus » au menu d'une étape détaillée ;
  historique « groupement en sous-processus », « dissociation du sous-processus ». Menu Fichier : [admin] « Importer
  un paquet… » après « Ouvrir un fichier .pmap… ». Dialogue d'un .pmap : un fichier qui commence par la signature d'une
  archive ZIP ouvre l'import du paquet avec lui, en le disant. La barre Ctrl+K relie par les codes et ne cherche pas de
  commande : aucun synonyme ajouté (la proposition faite au porteur le supposait à tort, il en est averti).
- **Tests** : ruban, menus et clavier (`menus.test.ts`, `keys.test.ts`), menu Fichier (`fileMenu.test.tsx`,
  `EditorBar.test.tsx`), dialogue d'un .pmap (`files.test.tsx`) ; parcours `dissolution.spec.ts` (ruban, Ctrl+G et
  Ctrl+Maj+G dans Chromium), `package.spec.ts` (menu Fichier, paquet donné au dialogue d'un .pmap), `selection.spec.ts`,
  `regressions.spec.ts`, `files.spec.ts` ; démonstration (`work.ts`, pas 10 du scénario) ; 15 mutants tués sur 15.
  Deux défauts de mes tests trouvés par les parcours (liste du menu Fichier d'un parcours plus ancien ; nom d'un
  téléchargement de Playwright) et un par la règle du nom du produit (« .topogenia » écrit en dur dans des tests :
  l'extension vient de sa constante) ; deux octets de contrôle bruts écrits par un heredoc, remplacés par leurs
  échappements.
- **Couverture** : B-2.1-29, B-2.4-16, B-2.4-35, B-8.3-58 remplacés (AR-154) ; B-1.6-03, B-2.4-53, B-8.3-52 étendus ;
  B-2.1-28 complété ; titres des tests renommés repris dans `couverture.md` et `couverture-p6/portage-parcours.md`.
- **Vérification** : web : `tsc -b`, ESLint, vitest **1 812/1 812** ; parcours **93/93** en mode Poste et en mode Serveur, puis
  `files.spec.ts` et `package.spec.ts` rejoués dans les deux modes après la correction du nom du produit
  (`Travail/e2e-ar154-*.log`) ; démonstration rejouée (30 captures) ; Architecture **853/853**. Aucune source .NET
  changée : la suite .NET n'est pas rejouée (5 188/5 188 au passage précédent). Exécutable d'essai construit pour le
  porteur (`node build/package.mjs Desktop`, ZIP SHA-256 `a8edd5a9...`), aucun ZIP livré.

### Porte P7-T06 - AR-155 : retouches d'ergonomie avant la 3.0 (29/10/2026)

- **Origine** : balayage de l'ergonomie par le porteur après l'essai d'AR-154, juste avant de passer à la 3.0.
- **AR-155** (révisions des ADR 061 et 062) : en-tête dans l'ordre marque, Fichier, fil d'Ariane, classification,
  Membres ; ruban dans l'ordre Niveau, Formes, Branchements, Liens, Édition, Affichage, Outils ; zone Niveau neuve,
  « Remonter d'un niveau » (Alt+↑ comme l'Explorateur Windows, grisé sur la carte racine, vu aussi par un lecteur ;
  « Niveau » parce que « calque » nomme le calque RACI) ; « Entrée » écrit en face de « Ouvrir la fiche » ; « Inverser
  le sens » au menu d'une flèche.
- **Inversion d'une flèche** : opération neuve du cœur (`FlowRules.Reverse` : source et cible échangées, identifiant,
  libellé et traductions gardés, `LIEN_DOUBLON` quand la flèche inverse existe), `ReverseFlowCommand` (sorte
  `inverser_lien`, la seule sans code 1.x), route `POST /api/flows/{id}/reverse` avec If-Match (vingt-cinquième route
  de l'ADR 045) ; contrat OpenAPI et client régénérés ; paquet du cœur refait.
- **Tests** : `FlowReversalTests` (4), `FlowCommandsTests`, droits et catalogue des commandes, sortes du journal ;
  `FlowRoutesTests`, `IfMatchCoverageTests`, table des routes et matrice des droits ; ruban, menus, clavier, en-tête,
  menu rendu (« Entrée ») ; parcours de l'inversion (`flows-and-insertion.spec.ts`) et du niveau
  (`types-and-detail.spec.ts`) ; 13 mutants tués sur 13 (12 dans l’interface, un dans le domaine ; le seul survivant du premier passage a demandé le test de la commande du menu d’une flèche). Défauts de mes tests trouvés en route : un trait de flèche horizontal n'a
  pas de hauteur et Playwright le dit caché (clic droit par coordonnées) ; « Traduire… » absent sans langue de
  traduction ; un signe « ↑ » refusé dans un fichier de langue (noms « Haut », « Up »).
- **Couverture** : B-2.1-01, B-2.1-08, B-2.4-44 étendus (AR-155), B-2.1-05 complété, B-2.3-13 cite le menu rendu ;
  route neuve dans la table des routes.
- **Vérification** : paquet du cœur refait (SHA-256 `e9907c22...`) ; construction 0 avertissement ; suite .NET
  **5 202/5 202**, 0 sauté (`Travail/suite-ar155.log`) ; web : `tsc -b`, ESLint, vitest **1 818/1 818** ; parcours
  **95/95** en mode Poste et en mode Serveur (`branches.spec.ts`, qui listait les zones dans l’ancien ordre, corrigé
  puis rejoué dans les deux modes) ; démonstration rejouée ; Architecture **853/853** ; citations de la couverture
  contrôlées. Exécutable d’essai construit pour le porteur, aucun ZIP livré.
- **Défaut de la chaîne des paquets corrigé** (ADR 076) : un paquet du cœur refait a une autre empreinte de contenu,
  que les fichiers de verrouillage de la publication précédente (`obj/packages.publish.lock.json`, nés avec R4)
  refusaient (NU1403) ; `build/package.mjs` les efface avant de publier (`forgetPublicationLocks`), ceux du dépôt
  restent intacts. Première fois qu’un cœur était refait entre deux publications.

### 3.0 - Lot 1 : paramètres et connecteurs de l'IA (29-30/10/2026)

- **Origine** : la 2.0 validée par le porteur, la 3.0 (IA intégrée) ouverte dans une copie propre ; spécification
  `TOPOGENIA_3.0_SPECIFICATION_IA.md` révisée (révisions 2 à 4 : liste de connecteurs, contexte et documentation des
  formats envoyés, quatre pratiques du volet) ; feu vert du porteur pour le lot 1, puis pour les lots 2 à 4.
- **ADR 077** : liste fermée de connecteurs (aucun par défaut, copier-coller, Claude par l'API Anthropic, Claude par AWS
  Bedrock) ; table `assistant_settings` (une ligne, versionnée) et ses migrations, dont le `Down` retire sa ligne
  d'historique ; clé Anthropic saisie dans le dialogue, chiffrée par la protection des données (nom du produit dans les
  deux modes), jamais rendue, ASCII imprimable ; Bedrock sans clé (profil SSO nommé ou identité de l'hôte,
  `AWSSDK.BedrockRuntime`, `Converse`) ; routes `GET`/`PUT /api/admin/assistant`, `POST /api/admin/assistant/test` ;
  audit sans la clé ; exception à D14 bornée à `src/Topogenia.Assistant/` par une règle d'architecture.
- **Interface** : roue dentée de l'en-tête pour l'administrateur, dialogue « Paramètres de l'IA » (connecteurs, clé en
  écriture seule, région et profil AWS, modèle et plafond, classifications avec leurs avertissements, test de la
  connexion), textes français et anglais.
- **Constats de la construction** : le SDK Anthropic 13.3.0 envoie la clé en `Authorization: Bearer` et porte sa propre
  télémétrie OpenTelemetry (coupée) ; le test de connexion se fait sans nouvelle tentative, en 90 s au plus, sur les
  deux connecteurs ; le SDK AWS ne connaît pas `ServiceQuotaExceededException` pour `Converse` (lu dans `ErrorCode`) ;
  une écriture devancée par un autre administrateur rend `CONFLIT_VERSION` ; des valeurs propres à l'entreprise du
  porteur relevées dans des tests et remplacées par des valeurs fictives.
- **Tests** : `AssistantRulesTests`, `AssistantRequestsTests`, `AssistantRoutesTests`, `AnthropicConnectorTests`,
  `BedrockConnectorTests` (faux client HTTP et identifiants des tests), `DataProtectionSecretsTests`, catalogues
  (requêtes, routes, droits, If-Match, tables, CHECK, rôle, colonnes, descentes) ; `AssistantSettings.test.tsx` ;
  parcours `assistant-settings.spec.ts` ; faux connecteurs dans les hôtes des tests et le lanceur des parcours
  (`JourneyServicesFactory`) : aucun test n'appelle un vrai service. Mutations ciblées : 12/12 dans l'interface, 15/15
  dans le .NET (un survivant au premier passage, la suppression de la clé absente de l'audit, a demandé un test).
- **Vérification** : paquet du cœur 3.0.0 (SHA-256 `607b9673...`) ; construction 0 avertissement ; suite .NET
  **5 267/5 267**, 0 sauté (`Travail/suite-lot1-final.log`) ; web : `tsc -b`, ESLint, vitest **1 838/1 838** ;
  parcours **97/97** en mode Poste et en mode Serveur ; Architecture **854/854** ; ZIP du mode Poste
  `Topogenia-3.0.0-portable.zip` (SHA-256 `7b3b3e14...`), décompressé pour l'essai du porteur, aucun ZIP livré.
- **Premier appel réel** (30/10/2026, 0 h 56, par le porteur, sa clé saisie dans le dialogue de l'exécutable d'essai) :
  « Tester la connexion » concluant, `claude-opus-5-5` a répondu en 18,5 s, 22 jetons envoyés, 4 reçus, soit environ
  0,0002 $ (4 $ et 20 $ le million) ; mesure lue dans l'audit du poste (`assistant.connection_tested`), sans la clé.
  La durée, pour une réponse d'un mot, dit que le volet doit montrer que le modèle travaille (flux, état de l'appel).


### 3.0 - Lot 2 : le volet de l'assistant (30/10/2026, nuit)

- **Origine** : feu vert du porteur pour les lots 2 à 4 (spec révision 4, section 3.4), puis autonomie complète pour la
  nuit, avec des appels réels permis pour les essais sur son crédit d'essai (plafond fixé : 20 $ pour la nuit).
- **ADR 078** : un échange par portée en flux de lignes JSON (`started`, `progress` au moins toutes les 2 s, `reply` ou
  `failure`), refus dits avant le flux (`IA_INDISPONIBLE`, `IA_CLASSIFICATION_REFUSEE`, `IA_CONTENU_TROP_GRAND`) ;
  réponse unique structurée de quatre formes, la proposition en texte `.pmap` relu par le format et renvoyé une fois au
  modèle avec ses fautes ; contexte dans l'ordre du cache (consigne et documentation des formats embarquées, schéma du
  projet en paquet pour l'animateur, en `.pmap` vu par le membre sinon, échanges précédents, carte ouverte, sélection) ;
  copier-coller ; audit `assistant.exchanged` sans texte ; coût estimé pour l'administrateur ; fil jamais gardé.
- **Interface** : bouton IA en marge droite du ruban et Ctrl+I ; volet ancré à droite (un volet à la fois avec la fiche
  et le reliquat) ; portée, pastille de la sélection, ligne des données ; quatre tuiles ; champ de quatre à douze lignes,
  Entrée envoie, icônes Copier, Coller, Lire un fichier texte, Effacer ; cartes Réponse, Questions, Analyse,
  Proposition, progression animée avec la réflexion résumée, échec avec « Réessayer » ; « Décrire avec l'IA » sur
  l'accueil (administrateur), la proposition ouverte en nouveau projet après confirmation du code et du nom. Le bouton
  principal du bandeau garde son rôle de la 2.0 (B-1.3-02 : la carte du dernier projet ouvert).
- **Constats** : la raison d'arrêt du SDK Anthropic se lit par `Raw()` (par `ToString()`, un refus ou une réponse coupée
  passaient pour des succès : trouvé par le test du flux simulé) ; la réflexion résumée suit la langue de la consigne,
  qui demande désormais d'écrire dans la langue de la demande ; la marque d'ordre des octets d'un fichier texte est déjà
  retirée par le navigateur ; le jeton de lancement du mode Poste ne sert qu'une fois (les captures gardent la session).
- **Essais réels** (`claude-opus-5-5`, hôte des parcours avec `--assistant real` sur une copie des données de l'essai
  du porteur, projet SALES de 15 cartes et 93 étapes) : question sur le projet entier, 17,4 s, 0,47 $ (86 079 jetons
  écrits en cache) ; analyse d'une sous-carte, 30,7 s, 0,50 $ ; question suivante, **cache lu** (86 917 jetons), 4,3 s,
  0,04 $ ; question sur une sous-carte depuis le volet, 10,5 s, 0,46 $ ; **récit de l'écran de référence** (spec
  section 8) depuis l'accueil, 28,1 s, 0,23 $ : projet créé, six tâches enchaînées et leurs durées, fiches, prénom
  remplacé par un rôle, points à vérifier ; deux couloirs au lieu des trois de l'écran (le tableur tenu pour un outil,
  comme le guide du format le dit d'un logiciel qui n'agit pas seul). **Total : 1,70 $.**
- **Tests** : `AssistantPanelTests` (16, deux bases), `AssistantExchangeConnectorTests` (8 : flux SSE simulé, Converse,
  consignes et documentation embarquées, schéma de la réponse), `AssistantRepliesTests` (12) ; catalogues (routes,
  If-Match, requêtes, messages d'erreur) ; vitest `AssistantPanel.test.tsx` et `Composer.test.tsx` ; parcours
  `assistant-panel.spec.ts` (4) sur le faux connecteur scripté du lanceur. Mutations ciblées de l'interface : 12/12.
- **Vérification** : web `tsc -b`, ESLint, vitest **1 886/1 886** ; parcours **101/101 en mode Poste et 101/101 en mode
  Serveur** ; Architecture 854/854 ; mutations ciblées **12/12 dans l'interface, 17/17 dans le .NET** ; ZIP du mode
  Poste `Topogenia-3.0.0-portable.zip` (SHA-256 `1ca2255c...`), décompressé pour l'essai du porteur, aucun ZIP livré.

### 3.0 - Lot 3 : les propositions appliquées à la carte (30/10/2026, nuit)

- **ADR 079** : la carte racine du `.pmap` proposé est l'état voulu de la carte ouverte (un `id` qui est un code désigne
  l'objet gardé) ; `ProposalPlan` (cœur fermé) compare et applique par les opérations du domaine : suppressions, couloirs,
  étapes gardées qui changent de case mises de côté au-delà de toute colonne puis posées, étapes changées et nouvelles,
  flèches, couloirs vidés, ordre des couloirs ; une étape détaillée laissée de côté est gardée avec sa carte. Une commande
  journalisée `proposition_ia` (un Ctrl+Z) ; l'aperçu rend la version de structure lue, l'application la reçoit en
  If-Match et refuse un aperçu périmé (`CONFLIT_VERSION`). Routes `POST /api/maps/{id}/assistant/preview` et `/apply`.
- **Interface** : sur une carte, la Proposition offre d'abord « Aperçu sur la carte » (changements listés, étapes changées
  et supprimées marquées), puis « Appliquer », « Préciser », « Écarter » ; « Proposer la correction » sur un constat.
- **Constat** : sans la mise de côté, l'échange de deux cases passait par le décalage automatique des étapes, qui poussait
  aussi une troisième étape restée en place ; le test qui le prouve a tué le mutant qui survivait.
- **Essai réel** (copie des données, sous-carte « Extraire de SAP les factures du mois », `claude-opus-5-5`) : « ajouter un
  contrôle des montants avant l'export et automatiser la requête » -> proposition (une étape ajoutée, flèches refaites,
  requête passée en automatisée, points à vérifier), aperçu marqué sur la carte, appliquée, puis défaite d'un seul
  Ctrl+Z (« Annulé : proposition de l'IA ») ; 88 878 jetons envoyés, 2 863 reçus, **0,50 $**. Total de la nuit : 2,20 $.
- **Tests** : domaine `ProposalPlanTests` (6), hôte `AssistantProposalTests` (6, deux bases), matrice des droits avec les
  routes de l'assistant (164), vitest `ProposalPreview.test.tsx`, parcours « appliquer puis Ctrl+Z ». Mutations ciblées :
  11/11 (10 dans le plan du domaine, l'aperçu périmé dans l'application).
- **Vérification** : parcours **102/102 en mode Poste et 102/102 en mode Serveur** ; suite .NET 5 329/5 332, les trois
  écarts (catalogue des types d'opération, méthode de plus de 80 lignes découpée) corrigés et rejoués (domaine 1 378,
  application 441, Architecture 854) ; paquet du cœur refait (`804bf56c...`) ; ZIP du mode Poste (`7cbd90ec...`),
  contrôlé (l'exécutable porte le nouveau domaine), décompressé pour l'essai du porteur, aucun ZIP livré.

### 3.0 - Lot 4 : les fichiers joints (30/10/2026, matin)

- **ADR 080** : PDF et images (PNG, JPEG, GIF, WebP) envoyés tels quels (API Anthropic : blocs `document` et `image` ;
  Bedrock : `DocumentBlock` et `ImageBlock`) ; Word, Excel et Visio lus par l'hôte (`Topogenia.Interchange.Documents`,
  port `IDocumentTextReader`) : archive aux entrées comptées et lues dans un flux borné (64 Mo par entrée, 256 Mo en
  tout), XML sans DTD ni résolveur, relation qui sort de l'archive ignorée, texte borné à 400 000 caractères et coupé
  en le disant. Word : titres, listes, tableaux, texte supprimé laissé de côté ; Excel : feuilles, lignes numérotées,
  dates, heures et durées reconnues à leur format ; Visio : formes, modèles, places et tailles, liaisons des connecteurs.
  La sorte d'un fichier vient de son extension, confirmée par sa signature ; anciens formats et PDF protégés refusés.
- **Bornes de chaque fournisseur** (relevées le 30/10/2026 dans la documentation d'Anthropic) : API, 10 Mo par fichier,
  7 Mo par image, 20 Mo en tout ; Bedrock, 4 Mo, 3,5 Mo, 10 Mo ; 5 fichiers par conversation ; données au volet par
  l'état du volet et les paramètres ; corps d'un échange porté à 64 Mo.
- **Les fichiers restent avec leur demande** : la page les renvoie avec le tour qui les portait ; un point de cache suit
  ceux de la demande, le tour suivant les relit depuis le cache.
- **Interface** : trombone, dépôt sur le champ (un `.txt` ou `.md` déposé est lu dans le champ), collage d'une capture,
  pastilles (sorte, nom, taille, retrait) au-dessus du champ et sous la demande envoyée, chaque refus dit en clair ;
  au copier-coller, le volet dit de joindre soi-même les PDF et les images. Tuile « Créer depuis un texte ou un fichier ».
- **Constats** : le formateur des messages d'erreur ôte l'espace avant un point (« au format .docx » devenait
  « au format.docx ») : le format conseillé s'écrit sans son point. La règle D14 refuse toute URL dans le code livré :
  les vocabulaires d'Office se reconnaissent à la fin de leur espace de noms, ce qui lit aussi la forme « Strict ».
  Les dates d'Excel se calculent depuis leur premier jour, en UTC (règle D08). Un tuple par défaut pris pour une relation
  trouvée faisait lire une partie nulle : trouvé par les premiers tests des lecteurs.
- **Essais réels** (copie des données, `claude-opus-5-5`) : sur l'accueil, « Décrire avec l'IA » avec une procédure Word
  fictive (titres, liste, tableau des durées) et un PDF fictif (circuit des avoirs) -> un projet de 2 cartes, 7 couloirs,
  17 étapes, 9 fiches, le circuit du PDF en sous-carte, les durées du tableau dans les fiches, la ressaisie en irritant,
  4 points à vérifier ; 73 s, 36 062 jetons écrits en cache, 8 643 reçus, **0,35 $**. Puis, sur la carte créée, une
  question avec la procédure jointe (**0,24 $**) et une relance sans fichier, qui a relu la procédure depuis le cache
  (41 135 jetons lus, **0,04 $**). Total de la nuit : **2,84 $**.
- **Tests** : lecteurs `DocumentTextReaderTests` (19, fichiers bâtis en mémoire, forme « Strict » comprise), règles
  `AssistantAttachmentsTests` (14), connecteurs (3 de plus), hôte `AssistantFilesTests` (5, deux bases), vitest
  `Attachments.test.tsx` (8), parcours « fichiers joints » (un `.docx` bâti par le parcours, un PDF, un `.doc` refusé,
  la relance qui renvoie les fichiers). Mutations ciblées : **.NET 32/32, web 18/18** (deux tests renforcés).
- **Vérification** : suite .NET **5 373/5 373**, 0 sauté ; vitest 1 907/1 907 ; **parcours 103/103 en mode Poste et
  103/103 en mode Serveur** ; Architecture 854/854. Le cœur n'a pas changé (paquet gardé, `804bf56c...`). ZIP du mode
  Poste (`883877a2...`), vérifié (l'exécutable porte les lecteurs du lot 4), décompressé pour l'essai du porteur, aucun
  ZIP livré. La copie des données des essais réels est effacée.


### 3.0 - Correctif des traductions (30/10/2026, fin de matinée)

- **Le défaut** (essai de l'exécutable du lot 4 par le porteur du projet) : interface en anglais, « traduis tout le
  diagramme en anglais » : le modèle a rendu une proposition avec `translations.en`, rien n'a été traduit.
- **ADR 081** : la carte voulue du cœur porte les traductions (couloirs, étapes, flèches) ; le format les recopie ;
  une langue non configurée est refusée par le lecteur (`FICHIER_INVALIDE`, chemin de la traduction) puis par le cœur
  (`LANGUE_INVALIDE`) ; écrites **en dernier** comme **automatiques** (une langue absente gardée, la langue d'origine
  ignorée, la carte détaillée qui porte le libellé suit son étape), **jamais sur une traduction manuelle**
  (avertissement `translation_kept`) ; champ `translations` dans l'aperçu ; le message de chaque échange dit la langue
  d'origine et les langues configurées (`<languages origin="fr" configured="fr, en" />`), la consigne (« Translating »)
  fait garder chaque libellé et remplir `translations`.
- **Constats** : l'aperçu ne chargeait que la structure (`WorkspaceContent.None`) et ne voyait pas les traductions
  existantes : il charge les traductions. « Appliquer » les avait déjà (une commande d'une carte charge fiches, documents
  et traductions pour la carte qu'elle rend) : je l'avais d'abord cru privé et lui avais fait charger tout le projet ;
  la mutation qui le retirait a survécu, le lanceur relu a montré l'erreur, changement retiré. Par le shell, un
  `\\n` d'un script d'édition est devenu un vrai retour à la ligne : scripts écrits par l'outil d'écriture.
- **Tests** : domaine `ProposalTranslationTests` (7), hôte `AssistantProposalTests` (+3, deux bases : traduction
  appliquée et défaite, manuelle gardée, langue non configurée, carte rendue identique à la carte relue), langues du
  message (`AssistantPanelTests`, `AssistantExchangeConnectorTests`), vitest (aperçu en français et en anglais),
  `JourneyConnector` (« tradu ») et parcours « traduire, EN, Ctrl+Z ». Mutations ciblées **.NET 14/14**.
- **Essai réel** (copie des données, `claude-opus-5-5`) : l'exemple de clôture ouvert en projet, interface en anglais,
  « Translate the whole diagram into English. » -> proposition en 30 s, aperçu (3 couloirs et 7 étapes « traductions »,
  les traductions déjà présentes gardées), appliquée : couloirs, étapes et libellés « oui »/« non » en anglais, libellés
  français intacts ; 40 166 jetons envoyés, 3 658 reçus, **0,27 $**. Le modèle a aussi traduit la sous-carte et le nom du
  projet : le cœur ne les applique pas encore (une carte à la fois) ; repris au lot 5 (portée du projet). Copie effacée.
- **Vérification** : suite .NET **5 387/5 387**, 0 sauté ; vitest 1 908/1 908 ; **parcours 104/104 en mode Poste et
  104/104 en mode Serveur** ; Architecture 854/854 (rejouée après le paquet). Paquet du cœur refait (`96c67574...`) ;
  ZIP du mode Poste (`bc81e048...`), vérifié (l'exécutable porte le nouveau domaine), décompressé dans
  le dossier d'essai du lot 4b pour l'essai du porteur, aucun ZIP livré.

### 3.0 - Lot 5 : les niveaux (30/10/2026, après-midi)

- **Feu vert du porteur du projet** (30/10/2026) : répartir une carte existante en niveaux, et l'inverse, ramener à un
  niveau un processus sur plusieurs cartes, toujours en proposant de l'ouvrir dans un nouveau projet.
- **ADR 082** : l'IA propose la forme, l'application déplace. La carte voulue a des niveaux (`SubMap`) et des fiches
  (`Sheet`) ; chaque étape existante nommée est gardée, descendue (sous une étape nouvelle de sa carte), remontée ou
  déplacée ; une étape remontée ou déplacée fait une proposition `new_project`, jamais appliquée
  (`PROPOSITION_NOUVEAU_PROJET`, 409), dont l'aperçu dit pourquoi et ce qu'un nouveau projet ne reprendrait pas. Sur place,
  carte par carte depuis le haut : « Détailler la sélection » (`GroupIntoSubMap`) pour les étapes descendues, puis la
  correspondance (ADR 079, 081), puis chaque sous-carte (regroupée, détail gardé, détail nouveau) ; couloirs copiés repris
  par leur nom ; fiches voulues en brouillon sur les étapes sans fiche (F-53 fait). « Ouvrir en nouveau projet » envoie la
  carte d'origine (`origin_map_id`) : l'hôte reporte fiches et traductions par code (`PmapCarry`), la carte d'origine reste.
- **Interface** : carte « nouveau projet » (pourquoi, étapes remontées ou déplacées, étapes non reprises dont les étapes
  détaillées et leur fiche, « Ouvrir en nouveau projet », ou le téléchargement pour un animateur) ; tuile « Modifier la
  carte » : « Répartis cette carte en niveaux de dix étapes au plus. » ; consigne « Splitting into levels » et « Bringing
  back to one level ».
- **Constats** : le `.pmap` veut des identifiants uniques dans tout le fichier, couloirs compris (d'où les couloirs copiés
  repris par leur nom) ; une étape placée sous une étape existante sans détail n'a pas d'opération sur place (F-58) ; la
  traduction du nom du projet par une proposition n'est pas appliquée (F-57) ; F-52 et F-55 closes par la décision du
  porteur. Au premier parcours, l'hôte des parcours n'était pas reconstruit après une correction du faux connecteur
  (`parentStep` absent de la carte racine du paquet) : relancé après construction, vert.
- **Tests** : domaine `ProposalLevelsTests` (7), format `PmapProposalTests` (3), hôte `AssistantLevelsTests` (2, deux
  bases), vitest (+2), parcours « répartir, Ctrl+Z, Ctrl+Y, ramener, ouvrir en nouveau projet ». Mutations ciblées
  **.NET 21/21, web 3/3** (un mutant réécrit pour compiler).
- **Essai réel** (copie des données, `claude-opus-5-5`) : une carte de vente fictive à plat de 32 étapes, 6 couloirs,
  3 fiches (`Travail\fichiers-essai-lot5\vente-a-plat.pmap`), ouverte en projet. « Split this map into levels of ten
  steps at most. » -> en 52 s, 5 phases détaillées (offre, commande, préparation et expédition, réception, facturation),
  7 étapes sur la racine, les 32 étapes d'origine gardées avec leurs codes et leurs 3 fiches ; appliquée, défaite par
  Ctrl+Z, refaite par Ctrl+Y ; **0,36 $**. Puis « Ramène le processus sur un seul niveau. » -> en 37 s, la proposition à
  plat : 30 étapes remontées, 5 étapes détaillées non reprises, pas d'« Appliquer » ; ouverte en nouveau projet : 32
  étapes sur un niveau, les 3 fiches reportées, l'original garde ses niveaux ; environ **0,20 $** (calculé sur les
  jetons). Critère 2 de la spécification tenu. Copie des données effacée ; captures dans `Travail\essai-reel-lot5\`.
- **Architecture** : la première suite complète a eu 5 échecs, tous des règles d'Architecture, à raison (parcours des
  sous-cartes écrit une seconde fois, lecture de `ParentStepId` hors de ses propriétaires, `MemoryExtensions` dans le
  domaine, commentaires accentués) : remplacés par `Subtree` et `ParentSteps`, commentaires en anglais ; mutants rejoués.
- **Vérification** : suite .NET **5 402/5 402**, 0 sauté ; vitest 1 910/1 910 ; **parcours 105/105 en mode Poste et
  105/105 en mode Serveur** ; Architecture 854/854 (rejouée après le paquet). Paquet du cœur refait (`c59ed981...`) ;
  ZIP du mode Poste (`69d25d08...`), vérifié (l'exécutable porte le nouveau domaine), décompressé dans
  le dossier d'essai du lot 5 pour l'essai du porteur, aucun ZIP livré.

### 3.0 - Lot 6 : les langues (30/10/2026, soir)

- **Déclencheur** : remarque du porteur, « si le collègue est portugais, il ne peut pas traduire le processus dans sa
  langue car on ne peut sélectionner que français et anglais dans la barre de menu ». Sa proposition, précisée ensemble
  puis acceptée (« c'est parfait, si tu peux rendre cela joli c'est mieux ») : langue de l'interface dans les paramètres,
  langue du processus dans le sélecteur du haut, dix-huit langues, une seule langue sur la carte, un verrou, un bouton
  dédié pour traduire par l'IA (ADR 083).
- **Langues** : cs, de, en, es, fr, it, hu, nl, pl, pt, ro, sv, tr, el, ru, uk, zh, ja par défaut (le français reste la
  langue par défaut). Seize langues nouvelles produites par seize agents en parallèle (interface, messages de l'API,
  messages du démarrage, glossaire de chaque langue dans `Travail\lot6-glossaires\`), contrôlées par
  `Travail\lot6-outils\check-locale.mjs` (clés, formes du pluriel de chaque langue, variables, `$t()`, nom du produit),
  puis 32 textes nouveaux ou changés traduits par quatre agents (`new-keys\`, fusion par `merge-new-keys.py`). À relire
  par des natifs (F-59). Les agents ont tous relevé que le temps relatif du serveur (« il y a 5 minutes ») et
  « quelqu'un » n'existaient qu'en français et en anglais : clés `TEMPS_*` et `QUELQU_UN` dans les dix-huit fichiers de
  messages, règle de pluriel par langue (`RelativeTime.PluralForm`). Les dix-huit fichiers d'interface (1,1 Mo) sont
  chargés à la demande, le français avec la page.
- **Cœur** : un texte créé prend la langue du projet (`ProjectWorkspace.TextLanguage`), plus celle de l'utilisateur
  (deux tests réécrits pour la règle nouvelle, un test couvre l'insertion, le collage, le regroupement, le document,
  l'irritant et la fiche) ; avancement de chaque langue (`TranslationCatalog.Coverage`) ; classeur sur les langues
  choisies ; verrou d'une traduction par l'assistant (`ProposalPlan.TranslationOf` : la carte telle qu'elle est, chaque
  texte recevant la traduction lue par code dans la réponse, un texte réécrit pris pour sa traduction, rien d'autre) ;
  nom du projet et carte racine traduits par la proposition (F-57 fait).
- **Hôte** : vue de la carte avec `languages` (origine, total, écrits) ; pratique `translate` (langue cible, carte racine
  et tous ses niveaux sans fiches, formes proposition et réponse, animateur, jamais la langue d'origine) ; aperçu et
  application avec `language` ; `GET /api/projects/{id}/translations?languages=` (colonnes : langue d'origine puis
  langues cochées).
- **Interface** : « Langue de l'interface » dans le menu du compte (Poste et Serveur), cartes de langue chacune dans sa
  langue ; liste de la langue du processus dans l'en-tête (origine, traduites avec leur avancement, pas encore
  traduites, « Traduire avec l'assistant… ») ; carte dans une seule langue, textes non traduits grisés, italiques,
  soulignés en pointillé « À traduire » ; verrou dans une autre langue (structure retirée, bandeau, « Afficher en … »,
  un nom tapé devient sa traduction, Ctrl+Z et Ctrl+Y gardés) ; dialogue Traductions (l'assistant d'abord, langues du
  classeur cochées) ; traduction manuelle (langues du projet, « Ajouter une langue ») ; nouveau projet (langue du
  processus, un nom, « Traduire le nom en… ») ; PDF (une ligne d'en-tête hors Inter dessinée en image).
- **Constats** : voir `Travail\lot6-plan.md` (règle d'ouverture corrigée à la moitié des textes, Ctrl+Z gardé, contraste,
  colonnes du classeur, verrou réécrit sur `Subtree` et `ParentSteps` avant la règle d'Architecture).
- **Tests** : domaine `TextLanguageTests` (5), `ProposalTranslationOnlyTests` (4), catalogue (+2) ; hôte
  `AssistantTranslationTests` (3, deux bases), `LanguagesTests` (2), messages et temps des dix-huit langues ; vitest
  (vue traduite 7, traduction par l'assistant 1, dialogue 3, en-tête 3, PDF 2, nouveau projet réécrit) ; parcours
  « traduire en portugais par l'assistant, verrouillé, un Ctrl+Z » et « dans une autre langue, un nom tapé est sa
  traduction ». La première suite complète a eu 77 échecs : des tests qui supposaient deux langues (l'allemand ou
  l'espagnol pris pour une langue non configurée, la liste fr/en affirmée, le classeur sur toutes les langues), le
  contrat de l'API à régénérer après le champ `language` de la demande, et un fichier en CRLF (`messages.zh.json` du
  démarrage) ; corrigés sans rien affaiblir (l'espéranto pour une langue non configurée, les dix-huit langues
  affirmées, le classeur sur ses langues choisies).
- **Essai réel** (copie des données, `claude-opus-5-5`) : la vente fictive à plat (32 étapes, 6 couloirs) traduite en
  portugais par « Traduire avec l'assistant » : 52 s, 39 textes sur 39 (vocabulaire du Portugal : « Registar a
  encomenda no ERP »), nom du projet compris (« Teste de idiomas ») ; une étape renommée en vue portugaise : sa traduction
  écrite, le libellé français intact ; **0,36 $**. Puis en japonais : 48 s, 39 sur 39 (« 見積書を作成する »), **0,17 $**
  (cache lu). PDF en japonais : titre et fil en image, le reste en Inter. Interface passée en portugais. Total
  **environ 0,53 $**. Copie des données effacée ; rapport et captures dans `Travail\essai-reel-lot6\`.
- **Mutations** ciblées : **.NET 30/30, web 13/13** (`Travail\mutations-lot6.log`, `mutations-web-lot6.log` ; scripts dans
  `Travail\lot6-outils\`).
- **Vérification** : suite .NET **5 494/5 494**, 0 sauté ; vitest 1 944/1 944 ; **parcours 106/106 en mode Poste et en
  mode Serveur** (le seul échec du mode Serveur, le compte « un nom seul », réécrit et rejoué dans les deux modes) ;
  Architecture 854/854 (rejouée après le paquet). Paquet du cœur refait (`362c5677...`) ; ZIP du mode Poste
  (`104decb1...`), vérifié (l'exécutable porte les messages des dix-huit langues et la pratique de traduction),
  décompressé dans le dossier d'essai du lot 6 pour l'essai du porteur, aucun ZIP livré.
- **Après l'essai du porteur** : une traduction en chinois de son processus a dépassé le plafond d'une réponse (16 000
  jetons). Proposé : une réponse allégée ; le porteur a préféré un plafond plus haut (« pour une traduction qui reste
  très occasionnelle même 1 € est très raisonnable au regard du temps gagné »). Plafond par défaut porté à **64 000**
  (`AssistantSettings.DefaultMaxOutputTokens`, ADR 077 et 083) ; une installation déjà réglée garde le sien (roue dentée).
  Tests liés, Architecture 854/854 et parcours de l'assistant 11/11 verts ; l'exécutable du dossier d'essai du lot 6, en
  essai, n'est pas refait : le prochain portera ce défaut.
- **Aperçu d'une traduction, après l'essai du porteur** (feu vert « Oui ok ») : l'aperçu d'une traduction encadrait en
  orange les étapes et listait des « modifications … traductions » sans les textes ; le porteur ne comprenait pas ce
  qui était montré. Désormais (ADR 083, point 12) la carte montre en place les textes que la traduction écrira, dans sa
  langue, verrouillée, rien d'écrit (bandeau) ; le volet donne le résumé, les traductions saisies à la main gardées et
  « Relire les textes » (original | traduction) ; seuls « Appliquer », « Écarter » et « Télécharger le .pmap » restent.
  Les textes viennent du serveur après le verrou (`ProposalPlan.TranslationTexts`, `translations` de l'aperçu, contrat
  régénéré). Au passage : le type « projet » d'une modification n'avait pas de libellé (clé manquante) ; un espace
  manquait entre le nom d'un connecteur et sa description. Textes nouveaux dans les dix-huit langues
  (`Travail\lot6-outils\apercu-traduction-keys.py`). La règle d'Architecture « langue d'origine d'une traduction écrite
  une fois » a refusé une comparaison écrite à la main : `LocalizedText.TakesTranslationIn` la remplace.
  **Vérification** : suite .NET 5 496/5 496 ; vitest 1 948/1 948 ; parcours 106/106 en mode Poste et en mode Serveur ;
  Architecture 854/854 ; mutations ciblées .NET 8/8, web 13/13 (un survivant d'abord, le retour fugitif à l'ancienne
  langue après « Appliquer » : le test suit maintenant chaque langue montrée). Cœur `a86d28b3...` ; ZIP du mode Poste
  `ed82d5aa...` décompressé dans le dossier d'essai du lot 6b (porte aussi le plafond de 64 000), aucun ZIP livré.

### 3.0 - Règles de modélisation (30/10/2026, nuit)

- **Demande du porteur** : sur la carte de ventes produite par l'assistant, SAP et l'outil de reporting avaient chacun un
  couloir. Après la comparaison VSDiA / BPMN : « l'esprit de l'outil est de montrer visuellement l'enchaînement du
  travail et chercher à la fois les gaspillages et l'automatisation » ; ajouter ces règles, et la structuration en
  niveaux, au document donné à son IA, et améliorer la consigne de création de l'application.
- **Fait** (ADR 084) : guide `docs/pmap/PMAP_GUIDE_IA.md` (section 3 : couloir = qui fait le travail, participant
  extérieur, logiciel en couloir seulement s'il agit seul, sinon outil de la fiche ; début par déclencheur, fin chez qui
  obtient le résultat ; une cadence n'est pas une décision ; section 6 « Niveaux et cartes détaillées ») ; consignes
  `common.md` et `practice-create.md` ; consigne claude.ai du paquet `.topogenia` refaite hors dépôt dans
  `46_Topogenia\Test-format-topogenia\` (celle de 45 intacte), son paquet d'exemple importé dans la 3.0.
- **Essai réel** (récit fictif d'une usine de pièces, `Travail\essai-reel-creation\`, copie des données effacée) : SAP en
  couloir seulement dans la carte détaillée de la facturation automatique de nuit, CPM outil des fiches, trois débuts de
  calendrier au lieu d'un losange, cinq grandes étapes à la racine dont quatre détaillées, ressaisies et copier-coller
  nommés dans les déroulés ; **environ 0,56 $**.
- **Vérification** : suite .NET 5 496/5 496, Architecture 854/854 ; ZIP du mode Poste `210eaabc...` décompressé dans
  le dossier d'essai du lot 6c (contient l'aperçu d'une traduction et le plafond de 64 000), aucun ZIP livré.

### 3.0 - Lot 7 : le flux de valeur (31/10/2026)

- **Demande du porteur**, après une comparaison avec les cartographies de flux de valeur des services (VSDiA) : « Le
  diagramme doit être auditable par un organisme extérieur et donc être complet tout en restant simple pour
  l'utilisateur. C'est l'ADN du logiciel. Pas de saisie inutile. » Maquette acceptée (« Ça me va »), avec trois
  exigences : l'icône du calque aussi discrète que les autres, des spécifications et un code blindés, la non-régression
  testée après chaque modification. Spécification révision 5, section 14 ; ADR 085.
- **Cœur** : quatre saisies facultatives (volume entrant d'un Début, part des dossiers d'une sortie de décision ou de OU
  inclusif, délai d'une fiche, effectif d'un couloir de personnes), la fréquence « à chaque dossier », le temps de
  travail du projet (7 h et 151,67 h par défaut) ; invariants contrôlés à l'écriture, après annulation et à l'import ;
  calcul `ValueStream` à chaque lecture, jamais stocké : débits par les équations de bilan (reprises exactes,
  composantes fortement connexes), jonctions par le branchement qui les domine, inconnus propagés sans estimation,
  exécutions, travail, délai, charge, ETP, étape détaillée chiffrée par sa carte, chemin principal, délai moyen reprises
  comprises, %C&A cumulé, heures perdues ; contrôle de complétude de seize codes. Oracle écrit à part (élimination de
  Gauss sur 200 cartes au hasard, simulation de 40 000 dossiers) ; les chiffres exacts de la maquette sur les deux bases.
- **Base et formats** : trois migrations SQL Server et deux SQLite (pas de valeur par défaut en base : colonnes
  facultatives, mise à jour, puis obligatoires), vues `bi` complétées en fin de vue ; paquet `.topogenia` 2.1 (conversion
  2.0 -> 2.1), `.pmap` 1.2 qui lit encore 1.1 ; exemples régénérés.
- **Hôte** : bloc `value_stream` et `stream_subjects` (l'objet de chaque mention, même dans une carte détaillée) dans la
  vue de carte ; `PUT /api/flows/{id}/share`, `PUT /api/projects/{id}/work-time`, `volume` et `headcount` dans les
  PATCH. Contrat : deux défauts du générateur OpenAPI corrigés de façon générale (membres d'une structure nullable non
  visités, membres d'un champ facultatif objet perdus) ; les membres d'une structure y sont requis.
- **Interface** : bouton « Flux de valeur » du groupe Affichage (même dessin que ses voisins, exclusif avec le RACI) ;
  calque allumé, chaque rangée gagne une bande de 36 px sous ses étapes (rangées de 156 px de la maquette) ; calque
  éteint, la géométrie d'avant au pixel près (propriétés du routage sur 500 cartes dans les deux cas). Une première
  version des cartouches, décalés comme les badges RACI, croisait les allées sur plus de 600 cas : centrés, dessinés
  sous les flèches, boucles plus profondes de deux bandes. Cartouches de quatre cases avec leur formule et leurs
  sources, parts sur les étiquettes (reste en italique), effectif et charge des couloirs, ligne de temps du chemin
  principal, bilan, contrôle dont chaque mention mène à son objet ; saisies au point du menu (volume avec sa période,
  part avec le reste de l'autre sortie, effectif), délai dans la fiche, temps de travail dans les propriétés du projet ;
  feuilles Excel « Flux de valeur » et « Bilan » en nombres. Seize langues traduites par quatre agents (186 clés),
  fusionnées et contrôlées par machine, à relire par des natifs (F-61).
- **Assistant** : une proposition porte volume, part et effectif, appliqués par les règles du domaine ; une valeur
  omise garde la valeur saisie (F-65) ; guide `.pmap` (section 7 bis « n'écrire un chiffre que s'il est donné »),
  consignes ; consigne claude.ai du paquet en 2.1, son exemple réimporté et réexporté à l'identique. **Essai réel**
  (`Travail\essai-reel-lot7\`, récit fictif de demandes d'achat, copie des données effacée) : volume, parts, effectifs
  donnés, durées, délais et « à chaque dossier » portés ; ni journée de travail ni effectif du responsable inventés,
  écrits en incertitudes ; **0,31 $**. Il a montré un défaut de lecture (« Effectif à saisir » sur un couloir sans
  charge), aligné sur la règle du contrôle.
- **Vérification** : suite .NET 5 644/5 644 (5 496 avant le lot), Architecture 854/854, vitest 2 015/2 015, parcours
  110/110 en mode Poste et en mode Serveur (`e2e/value-stream.spec.ts` nouveau, 4 parcours), mutations .NET 13/13 et web
  15/15 ; cœur `6088af48...` ; ZIP du mode Poste `d1418134...` décompressé dans le dossier d'essai du lot 7 (contient les
  lots précédents), aucun ZIP livré, aucun commit.

### 3.0 - Lot 7b : la molette, les volets de droite, le volet du flux de valeur (31/10/2026)

- **Retours du porteur** après l'essai du lot 7 : la molette qui ne fait que zoomer est « contre-intuitive » ; le bloc
  sous les couloirs « n'est pas du tout pro : pour un écran collaboratif, le diagramme doit toujours rester visible » ;
  un volet à droite, réductible et redimensionnable ; puis « on les donne à tous les volets de droite. Le texte affiché
  est aussi trop bavard, résumer, efficace ». Spécification révision 6, section 15 ; ADR 086 (remplace B-2.2-02).
- **Molette** : `panOnScroll` de React Flow, un cran pour autant de pixels d'écran ; Maj + molette à l'horizontale,
  Ctrl + molette et pincement zooment sur le pointeur ; les en-têtes fixes des couloirs, hors de React Flow, prennent
  les mêmes gestes (`useHeaderWheel`, écouteur non passif) ; la mini-carte garde la molette qui zoome ; vue toujours
  bornée.
- **Volets de droite** : un cadre commun (`SideDock`) autour de la fiche, de l'assistant, du reste d'un cadre et du flux
  de valeur : poignée du bord gauche (souris, flèches, Début et Fin, double clic), une largeur retenue
  (`app.sidePanelWidth`, de 320 px à 70 % de la fenêtre), « Réduire » en bande de 40 px (titre vertical, nombre
  d'avertissements) sans démonter le volet ; Échap laisse une fiche réduite ; les notifications suivent la largeur
  (`--dock-room`). La règle « un volet à la fois » existante (`watchSidePanel`) prend le calque : il ferme fiche,
  assistant et reste du cadre ; son volet revient quand le volet ouvert après lui se ferme.
- **Volet « Flux de valeur »** à la place du bloc sous les couloirs : bilan en six tuiles (définition au survol, « ≥ »
  pour un minimum, tiret pour un inconnu) et base de calcul, chemin principal ligne par ligne, contrôle groupé par code
  avec l'objet de chaque mention en pastille ; chaque ligne et chaque pastille mènent à leur objet. Textes raccourcis
  (75 clés : volet, mentions, info-bulles des cartouches et des parts), seize langues par quatre agents, fusionnées et
  contrôlées (F-66). F-63 fait : « – ETP » pour un couloir dont aucune charge n'est connue.
- **Exports** : l'image ne porte plus que le diagramme ; le PDF, calque allumé, ajoute ses pages « Flux de valeur »
  (trois colonnes, texte trop long coupé, lettres hors d'Inter dessinées) ; le classeur garde ses feuilles, mentions
  « objet : libellé court ».
- **Vérification** : suite .NET 5 644/5 644, Architecture 854/854, vitest 2 061/2 061, parcours 112/112 en mode Poste
  et en mode Serveur (deux parcours de plus : la molette, le volet réduit et redimensionné), mutations web 19/19 ; cœur
  `f09b8ed6...` ; ZIP du mode Poste `9fac705a...` décompressé dans le dossier d'essai du lot 7b, aucun ZIP livré, aucun
  commit.

### 3.0 - Lot 7c : la question de l'animateur modifiée ou retirée ; la licence au nom de Peter Barcauti (31/10/2026)

- **Licence** : à la demande du porteur, la licence est désormais au nom de son pseudonyme, Peter Barcauti
  (`NOTICE`, les deux `LICENSE.md` du cœur, avec l'adresse pour obtenir la licence écrite ; `ConfigurationRulesTests`).
  Les documents ne citent plus le nom des dossiers d'essai ; un contrôle hors du dépôt fouille le dépôt et ce qui est
  livré (texte, octets des binaires, intérieur des archives) avant chaque livraison.
- **Question de l'animateur** (spécification révision 7, section 16), sur une question du porteur : « où et comment
  modifie-t-on ce texte ? » (une question venue d'un paquet préparé par une IA). L'animateur la modifie en place ou la
  retire : `SheetWorkflow.ChangeClarification` et `WithdrawClarification` (une fiche « à revoir » repasse « validée »,
  un brouillon le reste ; sans question, `FICHE_ETAT_INVALIDE`), deux commandes réservées à l'animateur,
  `PUT` et `DELETE /api/steps/{id}/sheet/clarification` avec le contrôle de version ; dans l'encart, « Modifier » et
  « Retirer », Échap qui abandonne sans fermer la fiche ; cinq textes en dix-huit langues.
- **Vérification** : suite .NET 5 653/5 653, Architecture 854/854, vitest 2 068/2 068, parcours 113/113 en mode Poste
  et en mode Serveur, mutations 12/12 ; le dépôt et la livraison passés au contrôle d'identité ; cœur `b5cc86b4...` ;
  ZIP du mode Poste `a151aa19...` décompressé dans le dossier d'essai du lot 7c, aucun ZIP livré, aucun commit.

### 3.0 - Lot 7d : empreinte par licencié et archive du cœur (31/10/2026)

- **Demande du porteur**, après une question sur le « tatouage » du code : la recommandation (archiver la clé et les
  cartes des noms de chaque version, marquer chaque paquet du cœur pour son licencié, aucun tatouage caché dans le code
  du domaine), puis « oui fais l'archivage et l'empreinte par licencié ». ADR 087.
- **Empreinte** : l'assemblage du domaine construit par la chaîne porte deux attributs `AssemblyMetadata`, `Licence` et
  `LicenceMark` (HMAC-SHA256 d'un secret du titulaire sur le paquet, la licence, le licencié et la version, 128 bits) ;
  la chaîne refuse un `dotnet pack` sans eux ; le paquet du titulaire porte la licence `HOLDER`.
- **Outil** `build/core/licence.mjs` (chez le titulaire, hors du dépôt public) : `secret`, `pack` (construit, vérifie
  l'empreinte dans l'assemblage obscurci, archive le paquet, les deux assemblages, la carte des noms, les sources, un
  manifeste et le registre `licences.json`), `verify` (authentifie l'empreinte d'un assemblage, d'un paquet ou d'un
  exécutable et nomme le licencié). Outils lancés sans interpréteur de commandes, paquets dépaquetés par `archive.proj`
  (ADR 076) ; clé, secret et archive refusés dans le dépôt. Règle d'architecture nouvelle.
- **Vérification** : Architecture 855/855, suite .NET 5 654/5 654 ; essai réel sur un licencié fictif (authentique,
  falsifié, autre numéro, autre secret, sans empreinte) puis paquet du titulaire archivé ; empreinte authentifiée
  jusque dans l'exécutable du mode Poste ; ZIP `dd1b21a7...` décompressé dans le dossier d'essai du lot 7d, aucun ZIP
  livré, aucun commit.
