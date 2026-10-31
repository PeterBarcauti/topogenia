<!-- plan.md -->
<!-- Decoupage de la realisation de Topogenia 2.0 en phases et en taches -->

# Plan de réalisation de Topogenia 2.0

Source : spécification `Specification\TOPOGENIA_2.0_SPECIFICATION_COMPLETE.md` (notée « spec »),
prompt `PROMPT_CONCEPTION.md` (noté « prompt »). Les identifiants de comportement (`A-x.y-nn`,
`B-x.y-nn`) renvoient à `docs/couverture.md` ; les identifiants de tests 1.x (`T-...`) à
`docs/portage-tests.md`.

Chaque tâche suit le même cycle (prompt 6.1) : tests d'abord (traduction des lignes d'annexe
visées), code ensuite, toute la suite verte, mise à jour de `couverture.md` et `etat.md`.
Chaque phase se termine par une porte de contrôle par agents neufs (prompt 7).

## Choix de conception structurants (détaillés en ADR en phase 1)

| N° | Choix | Raison |
|---|---|---|
| C1 | Les commandes de structure travaillent sur un **espace de projet** chargé en mémoire (cartes, couloirs, étapes, flèches, RACI, remontées, et pour les opérations qui en ont besoin documents, usages, irritants, fiches). Le domaine applique la règle sur cet espace ; un **traceur d'instantanés** du domaine compare l'état avant et après et produit le `ChangeSet` (créations, modifications champ par champ avant/après, suppressions logiques, suppressions physiques). | Un seul calcul de changements pour toutes les opérations ; le journal (4.6) et l'annulation en découlent sans code par opération ; volumes faibles (5 000 étapes au plus par projet, cible NF05 tenable). |
| C2 | L'annulation applique l'inverse du `ChangeSet` sur l'espace rechargé, après contrôle des versions des objets touchés. | Annulation atomique et générique (D05). |
| C3 | Le stationnement en deux temps (4.3-1) est fait par l'unité de travail : toute étape dont la case change est d'abord posée hors bornes (colonne 1 000 000 000 + i), enregistrée, puis posée à sa case finale, dans la même transaction. | Le domaine ignore le piège ; un seul endroit le traite (fin de la quadruple duplication 1.x, annexe A 6-4). |
| C4 | Tests d'architecture avec **ArchUnitNET** (`TngTech.ArchUnitNET.xUnitV3`, Apache-2.0, version 0.13.4 publiée le 20/08/2026) pour les dépendances et les types interdits (D03) ; règles de taille, de SQL brut, de `TODO` et d'en-têtes par analyse du code source. | D03 cite NetArchTest ou ArchUnitNET ; NetArchTest.Rules (1.3.2) n'a pas été publié depuis plus de 12 mois (spec 0.2), ArchUnitNET oui. |
| C5 | Tests C# : xUnit v3 ; matrice de bases par théories paramétrées (`Sqlite`, `SqlServer`) avec une base `Topogenia_Test_<n>` recréée par exécution. | Spec 11, prompt 5. |
| C6 | Interface : Vite + React 19 + TypeScript strict, Playwright 1.64.1 (même version que la 1.x, pour réutiliser les navigateurs déjà installés, prompt 4-4). | Pas d'installation de navigateur. |

## Phase 0 - Lecture et cadrage (aucun code)

| Tâche | Contenu | Sortie |
|---|---|---|
| P0-T01 | Lecture complète de la spec, des annexes, du prompt | - |
| P0-T02 | Squelette de couverture : une ligne par comportement des annexes A et B | `docs/couverture.md` |
| P0-T03 | Inventaire des 334 + 176 + 49 tests 1.x et de leur cible | `docs/portage-tests.md` |
| P0-T04 | Vérification des prérequis (SDK, Node, SQL Server, navigateurs, sauvegarde) | `docs/journal.md` |
| P0-T05 | Questions réelles et décisions par défaut | `docs/questions.md` |
| P0-T06 | Porte : contrôleur « phase 0 » | journal |

## Phase 1 - Squelette, règles, charte

| Tâche | Contenu | Réf. spec | Lignes visées |
|---|---|---|---|
| P1-T01 | `Topogenia.sln`, `global.json`, `Directory.Build.props` (Nullable, TreatWarningsAsErrors, analyseurs, version unique), `Directory.Packages.props` (versions figées) | 3.1 | S-3.1 |
| P1-T02 | Projets vides `src/*` et `tests/*` avec leurs références (et seulement elles) | 3.1, 3.2 | S-3.1, S-3.2 |
| P1-T03 | `Topogenia.Architecture.Tests` : dépendances entre projets, types interdits dans Domain/Application, SQL brut hors fournisseurs, tailles de fichiers (600) et de méthodes (80), absence de `TODO`, en-têtes de fichiers, constante du nom de produit unique (D13) | 3.2, D13, NF09 | S-3.2, S-D13, A-6 (tailles, duplications, nommage), A-5 (SQL brut) |
| P1-T04 | ADR 001 à 014 (D01-D14) et ADR des choix C1-C6 | 2 | S-D01 à S-D14 |
| P1-T05 | Intégration continue `.github/workflows/ci.yml` (construction, tests SQLite et SQL Server, `dotnet list package --vulnerable`, `npm audit`, CodeQL) - écrite, jamais poussée | 5.4, 11 | S-5.4 (audit), S-11 |
| P1-T06 | `web/` : Vite, TS strict, ESLint, vitest ; `styles/tokens.css` (9.2) ; test de contraste pastel/encre >= 4,5:1 | 9.2 | S-9.2, B-0, B-6, B-3.2 (rendu des types), B-8.1 (CSS mort), B-8.2 |
| P1-T07 | Maquette statique `docs/recette/maquette/maquette.html` (autonome, polices et icônes embarquées) : palette et jetons, accueil, carte de 3 couloirs avec les 7 types, décision à 3 sorties, branche parallèle, macro-étape, cadre de remontée, pictogrammes, panneau fiche ouvert, menu contextuel | 9.6, prompt 8 | S-9.6, B-6, B-2.1, B-2.9 (cadre), B-2.10, B-2.12, B-3.1 à B-3.4, B-4 (panneau), B-2.4 (menu), A-2.6 (rendu des flèches) |
| P1-T08 | Captures de la maquette (Playwright, navigateur déjà installé) | prompt 8 | S-P (recette) |
| P1-T09 | `README.md` (FR, EN), `SECURITY.md`, `CONTRIBUTING.md`, `.gitignore`, `THIRD-PARTY-NOTICES.md` (pas de `LICENSE`, Q3) | 11, prompt 3 | S-11, S-P |
| P1-T10 | Porte : contrôleurs Architecture, Interface et charte | prompt 7 | - |

## Phase 2 - Domaine

Ordre : objets valeur, puis entités, puis services, chacun avec ses tests unitaires.

| Tâche | Contenu | Lignes visées |
|---|---|---|
| P2-T01 | Codes stables d'erreur (`DomainError`, catalogue des codes de l'annexe A 3) | A-3 |
| P2-T02 | Objets valeur : `ProjectCode`, `ReadableCode` (E/C/I/S, 3 puis 4 chiffres), `Cell`, `Color` (#RRGGBB majuscules, palette 2.0 et conversion 1.x), `LocalizedText`, `Frequency`, `Duration`, `DocumentLocation` (url/path), `Login` (DOMAINE\compte) | A-2.1, A-2.3, A-2.10, A-2.11 |
| P2-T03 | `TextRules` (trim, refus HTML `<[a-zA-Z!/?]`, caractères de contrôle, longueurs par champ) | A-2.18 |
| P2-T04 | Entités : Project, ProjectMember, ProcessMap, Lane, Step, Flow, StepRaci, StepSheet, Operation, StepSheetVersion, Document, DocumentUse, PainPoint (+ historique), DeletionBatch, Dissolution (+ étapes, objets), Translation, OperationLog, AuditEvent ; versions entières (D06), Guid v7 (D07), UTC (D08) | 4.1 |
| P2-T05 | Espace de projet en mémoire (`ProjectWorkspace`), traceur d'instantanés et `ChangeSet` (aller, inverse, objets touchés) | C1, C2, 4.6 |
| P2-T06 | `GridRules` : une étape par case, RG-C04 (bloc contigu), bornes, placement automatique (tri topologique) | A-2.4, A-2.17 |
| P2-T07 | `FlowRules` (réflexive, doublon, même carte), `LayoutWarnings` (isolée sauf flux autonome, début entrant, fin sortant), losanges et libellés de sortie (« oui », « non ») conservés à l'insertion et au recousage | A-2.5, A-2.6, A-2.7 |
| P2-T08 | Couloirs : ajout, ordre ramené dans [0,n], réordonner, renommer (efface les auto), suppression (dernier, déplacer, supprimer) | A-2.3 |
| P2-T09 | Étapes : création, PATCH (libellé, type, automatisation, assigné, couloir, colonne, rangée), renommage de la sous-carte homonyme, insertion avant/après/dessous/dessus/parallèle | A-2.4 |
| P2-T10 | Sous-cartes : Détailler, sous-carte vide, supprimer une macro, rattacher, supprimer une sous-carte, profondeur | A-2.2 |
| P2-T11 | `SelectionBlocks` et `Stitcher` : composantes connexes, recousage, suppression groupée, coller, ancre, transférer, RACI vers une autre carte | A-2.8 |
| P2-T12 | `GroupIntoSubMap` (Détailler la sélection, flèches coupées inscrites dans l'opération) | A-2.8, 4.6 |
| P2-T13 | `Dissolve` : préconditions, plan complet (couloirs, positions, décalage, flèches par `origin_operation_id`), reliquat, affecter, supprimer du reliquat, Terminer, annuler | A-2.9, 4.6 |
| P2-T14 | `RaciRules` : R implicite, lettres du réglage, un seul A, rôles masqués, report vers couloir homonyme | A-2.13 |
| P2-T15 | Fiches : champs, déroulé, `SheetCompleteness` (unique), `SheetVisibility`, brouillon, validation, précision, responsable, suggestions | A-2.10 |
| P2-T16 | Documents et usages, irritants (règle unique de retrait : 13), entrées proposées, « Autre… » | A-2.11, A-2.12 |
| P2-T17 | Traductions : manuelles (projet, carte, couloir, étape, flèche), effacement des auto au renommage seulement si le texte change, liste et import Excel (miroirs) | A-2.14, 10 |
| P2-T18 | Corbeille et lots : suppression logique, lot paresseux, règles de restauration (servent l'annulation), purge (plan d'effacement) | A-2.15 |
| P2-T19 | `CommandLineParser` (barre de commande) et membres (remplacement, `PROJET_SANS_ANIMATEUR`) | A-2.6, A-2.1, 5.6-4 |
| P2-T20 | Concurrence : contrôle des versions, `OPERATION_DEPASSEE`, format « il y a… » côté hôte | A-2.16, 4.6 |
| P2-T21 | Porte : Fidélité, Architecture, Intégrité des tests | prompt 7 |

## Phase 3 - Persistance

| Tâche | Contenu | Lignes visées |
|---|---|---|
| P3-T01 | `docs/modele-donnees.md` ; `AppDbContext` (ADR 028), configurations (snake_case, schéma technique figé `topogenia` en SQL Server, `TechnicalNames.Schema`, indépendant du nom affiché (ADR 013 révisée), sans préfixe en SQLite), clé composée `step(lane_id, map_id)` ; index uniques partiels et CHECK simples ajoutés par chaque fournisseur (ADR 025) | A-5, S-4.1, S-4.2 |
| P3-T02 | Migrations initiales par fournisseur ; historique avec empreinte ; refus d'une base plus récente ou modifiée | S-4.4, A-4 (mise à jour du schéma) |
| P3-T03 | Réglages SQLite à chaque connexion (foreign_keys, WAL, busy_timeout, synchronous) ; sauvegarde en ligne SQLite et contrôle d'intégrité | S-4.3, S-5.2 (sauvegarde) |
| P3-T04 | Unité de travail : verrou de projet (4.7), stationnement en deux temps (C3), découpage des listes par 500, traduction des erreurs (unicité, intégrité, interblocage, occupé) et nouvel essai 3 fois (50/100 ms) | A-2.16, S-4.3, S-4.7 |
| P3-T05 | Dépôts / chargeur de l'espace de projet ; lectures en transaction cohérente (instantané SQL Server, transaction de lecture SQLite) | A-4 (faiblesse 10), S-3.4 |
| P3-T06 | Migration SQL Server propre : rôles `topogenia_app`, `topogenia_bi`, vues `bi` (même contenu fonctionnel que la 1.x) | A-4 (rôles), A-5 (db/004) |
| P3-T07 | Tests paramétrés sur les deux bases : correspondances, index partiels, stationnement, concurrence, pièges 4.3 | A-5, S-4.2, S-4.3 |
| P3-T08 | Porte : Architecture, Sécurité, Données et portabilité, Fidélité, Intégrité des tests | - |

## Phase 4 - Application, journal, hôte HTTP, sécurité

| Tâche | Contenu | Lignes visées |
|---|---|---|
| P4-T01 | Gabarit de commande (autorisation déclarative, unité de travail, verrou, domaine, versions, journal, vue en réponse) | S-3.4, A-6 (droits dispersés) |
| P4-T02 | Gestionnaires par fonctionnalité (Projects, Members, Trash, Maps, Lanes, Steps, Flows, Selection, Dissolutions, Sheets, Documents, Raci, Translations, OperationLog, Profile, Maintenance) avec faux dépôts | A-1, A-2 (toutes sous-sections, partie autorisations et gestionnaires) |
| P4-T03 | Journal des opérations : undo/redo/history, profondeur 50, pile de rétablissement, `OPERATION_DEPASSEE`, purge liée aux lots | S-4.6, B-2.6 (part serveur), A-2.15 |
| P4-T04 | Hôte : routes 2.0 (table 1.x -> 2.0 dans couverture), JSON snake_case, erreurs `{code,message,details}`, messages FR/EN, OpenAPI | A-1, A-3, S-3.6 |
| P4-T05 | Mode Poste : écoute 127.0.0.1, jeton de lancement 256 bits -> cookie, contrôle `Host`, `Origin`, `Sec-Fetch-Site`, identité du processus, fichier refusé sur partage ou dossier synchronisé, sauvegardes, journaux, quitter ; part hôte des lignes de la phase 3 : refus de démarrer avec message clair selon les codes du schéma (`SCHEMA_MODIFIE`, `MIGRATION_MODIFIEE`, `BASE_PLUS_RECENTE`, `VERROU_MIGRATIONS`), arrêt avec message clair après l'échec de la sauvegarde d'avant migration (`SAUVEGARDE_IMPOSSIBLE`), restauration proposée avant l'ouverture du fichier (ADR 029), conduite sur `VERROU_MIGRATIONS` (fermer toute autre instance, sinon retirer la ligne de `__EFMigrationsLock` ou restaurer la sauvegarde d'avant la migration ; code non rejoué, ADR 041), appel de `SqliteEngine.CloseAsync` à l'arrêt ; l'hôte ne journalise jamais le chemin complet ni la cause d'un `BackupResult` (`File`, `Cause`) ni le `ToString()` d'une exception du moteur (ADR 029, 036) | S-5.2, S-D10, A-4-29, A-4-32, A-4-35, A-4-36, A-4-37 |
| P4-T06 | Mode Serveur : Negotiate, santé minimale, HTTPS/HSTS, 404 non-membre / 403, administrateurs relus à chaque changement de configuration, If-Match obligatoire (428), en-tête anti-CSRF, statiques après authentification, refus SQLite en mode Serveur ; part hôte des lignes de la phase 3 : refus de démarrer avec message clair selon les codes du schéma (`SCHEMA_MODIFIE`, `MIGRATION_MODIFIEE`, `BASE_PLUS_RECENTE`) ; l'hôte ne journalise jamais le `ToString()` d'une exception du moteur (ADR 036) | S-5.3, A-4, A-4-36, A-4-37 |
| P4-T07 | Communs : CSP, en-têtes, limites de taille avant lecture, validation stricte (champ absent = erreur), cache court du profil, audit `audit_event` + `GET /api/admin/audit` | S-5.4, S-5.5, A-2.18, A-4 |
| P4-T08 | Portage des tests d'intégration API 1.x sur la matrice SQLite x SQL Server ; tests de sécurité (un par point d'entrée et par rôle) | A-1 à A-4 |
| P4-T09 | Ligne de commande `--migrate`, `--check` (fait, ADR 050) | S-6.3 |
| P4-T09b | Décisions du porteur du 24/10/2026 : fichier refusé et son journal gardés avant une restauration, avertissement de la proposition (AR-96) ; avertissement non bloquant des droits trop larges sur le dossier des données ou des sauvegardes (AR-98) ; migration SQL Server des vues : `classification` dans `bi.project` (AR-100), `validated_version` dans `bi.step_sheet` (AR-101) | AR-96, AR-98, AR-100, AR-101 |
| P4-T10 | Porte : Fidélité, Architecture, Sécurité, Données, Intégrité des tests | - |

## Phase 5 - Échange et migration

| Tâche | Contenu | Lignes visées |
|---|---|---|
| P5-T01 | `docs/format-topogenia.md`, `schemas/` et un paquet fictif dans `samples/` ; paquet `.topogenia` : manifeste, entrées, empreintes, schéma JSON publié, sérialisation déterministe, option sans membres ni auteurs | S-7.2, S-5.4 (paquets) |
| P5-T01b | Export par l'API : `POST /api/projects/{id}/export` (animateur, AR-113), option `without_people` exigée, nom du fichier par la règle 1.x (A-1.2-16), événement d'audit `project.exported` ; port `IPackageExporter`, lecture des utilisateurs par identifiants | S-3.6-06, S-5.5-02, A-1.2-16 |
| P5-T02 | Import : aperçu, nouveau projet (réécriture des préfixes), remplacement (corbeille suffixée), tout ou rien, garde-fous (tailles, chemins, empreintes, schéma) | S-7.3 |
| P5-T03 | Montée de version des paquets (chaîne de convertisseurs) ; refus d'un paquet plus récent | S-7.3 |
| P5-T04 | `docs/format-pmap.md` et des `.pmap` fictifs dans `samples/` ; `.pmap` 1.1 seul (traductions des étapes et flèches, bloc de fiche facultatif par étape importé en brouillon, AR-93), placement automatique, avertissement au-delà de 12 niveaux, guides IA mis à jour (extraction des fiches d'un récit, exemple de récit converti) | A-2.17, S-7.5, AR-93 |
| P5-T05 | Retirée (AR-94) : pas de migration 1.x dans l'application ; le flux du porteur sera migré une fois, à sa demande, hors du dépôt | AR-94 |
| P5-T06 | Retirée (AR-94) | AR-94 |
| P5-T07 | Allers-retours 7.4 (exact, croisé SQLite -> SQL Server -> SQLite), paquets hostiles ; CLI `--export`, `--import` | S-7.4, S-6.3 |
| P5-T08 | Porte : Fidélité, Architecture, Sécurité, Données, Intégrité des tests | - |

## Phase 6 - Interface

Ordre d'exécution (décidé le 28/10/2026, avec le porteur) : **T05 avant T04**, puis T04 en trois lots (1 : la carte affichée,
magasin, géométrie, vue, canevas, nœuds, en-têtes de couloirs, flèches routées, fait le 28/10/2026, ADR 060 ; 2 : l'édition,
ruban, menus, commandes et raccourcis, sélection, glisser-déposer, aligner, presse-papiers, fait le 28/10/2026, ADR 061 ; 3 : annuler / rétablir, barre
Ctrl+K, menu Fichier, dernier projet ouvert, et la barre de l'éditeur dans l'en-tête : fil d'Ariane, boutons Fichier,
Membres et Traductions, fait le 28/10/2026, ADR 062 ; P6-T04 terminée). Lignes de T04 laissées à d'autres tâches, écrites
dans la couverture : entrées d'export du menu Fichier (P6-T09), bouton Matrice et remontée (P6-T06), panneau Fiche
(P6-T07), parcours de bout en bout (P6-T10) ; le bouton Traductions de l'en-tête apparaît avec son écran (P6-T08). Raison : l'éditeur dessine les flèches avec le module de routage ; T04 d'abord
l'aurait fait poser des flèches provisoires, refaites en T05. Les numéros, cités dans les ADR et la couverture, sont
gardés. Le plan de la phase 0 suivait l'ordre de l'inventaire (annexe B 3.1 à 3.3, puis 3.4) sans ranger par dépendance.

| Tâche | Contenu | Lignes visées |
|---|---|---|
| P6-T01 | Client généré depuis OpenAPI, erreurs typées, intercepteur If-Match et en-tête anti-CSRF (fait, ADR 056) ; proxy de développement à connexion unique | B-0-05, B-0-09, B-0-14 à B-0-21 (part interface), S-D09 ; les autres lignes de B-0 vont aux tâches qui en portent le thème (T02, T04, T09, T10) |
| P6-T02 | Socle (fait, ADR 057) : routeur, enveloppe, profil (un `CONFLIT_VERSION` du profil dont l'auteur est l'utilisateur lui-même est réglé sans lui : relire le profil et renvoyer les mêmes champs, AR-106), notifications, i18n (liste de langues en configuration), `shared/ui`, gestionnaire de clavier unique à couches, commandes en données, préférences d'affichage en stockage local et état non persisté (annexe B 7) | B-0-02, B-0-03, B-0-10 à B-0-13, B-1.1, B-1.2, B-2.5-01 à 05, 35, 36 (gestionnaire et accords globaux), B-7-01 à 06, 13, 15, S-3.5-01, S-3.5-02 ; les raccourcis de la carte (B-2.5-06 à 34) et l'état de l'éditeur (B-7-07 à 12, 16 à 21) vont à T04, S-3.5-03 à T05, S-3.5-04 à T10 |
| P6-T03 | Accueil (textes AR-88), corbeille, projet (nouveau, membres, .pmap, .topogenia) (fait, ADR 058) ; les entrées du menu Fichier de la carte (copie, duplication, export, remplacement) sont branchées en T04 | B-1.3 à B-1.6, B-1.8, AR-88, S-5.4-25, S-7.3-03 (part interface) |
| P6-T04 | Carte : géométrie, vue accrochée, nœuds, codes, pictogrammes, avertissements et jauge, en-têtes de couloirs, ruban, menus, raccourcis, barre de commande Ctrl+K, sélection, glisser-déposer, presse-papiers, aligner, annuler/rétablir côté interface (envoi au serveur, messages, état des boutons ; un `undo` ou un `redo` refusé `INTROUVABLE` est muet, l'historique est relu, AR-108), ruban repliable (AR-90) | B-0-01, B-2.1, B-2.2, B-2.3, B-2.4, B-2.5, B-2.6 (part interface : file sérialisée, messages, boutons), B-2.7, B-2.8, B-2.10 (codes, pictogrammes, avertissements, jauge), B-2.12, B-2.13 (barre de commande Ctrl+K), B-3.1 à B-3.3, B-6, B-7 |
| P6-T05 | Routage des flèches (module pur) et attribution des voies, tests par propriétés (500 cartes) (fait, ADR 059) ; le rendu des flèches (trait, pointe, zone de clic, étiquette, documents) va à T04 | B-3.4 (tracé), B-3.1 (constantes et grille), S-9.7, S-3.5-03 |
| P6-T06 | Détailler, sous-cartes, fil d'Ariane, remontée (cadre, reliquat, rangement), RACI (calque, panneau, matrice) (fait, ADR 063, AR-122 ; le cadre caché à l'export va à P6-T09, les parcours à P6-T10) | B-2.9, B-2.11 |
| P6-T07 | Fiche (formulaire, texte libre, déroulé avec poignée de glissement AR-89, sections pliables AR-91, enregistrement automatique, validation, précision, responsable, documents, usages, irritants) (fait, ADR 064, AR-123 ; les parcours vont à P6-T10) | B-4, AR-89, AR-91 |
| P6-T08 | Traductions (classeur, consigne, glossaire en réglage du projet) et traductions manuelles des étapes et flèches (fait, ADR 065, AR-124, AR-125 : glossaire en texte libre du projet, route, migration et paquet ; « Traduire… » aussi pour les couloirs ; le parcours de bout en bout B-5-24 va à P6-T10) | B-5, A-2.14 (traductions manuelles des étapes et flèches, §10), B-8.1-45, B-8.2-12 |
| P6-T09 | Exports PNG, PDF A3, Excel sous CSP (fait, ADR 066, AR-126, AR-127 : nom par la règle de l'hôte ; classification imprimée sur les trois exports, S-4.1-08 ; entrées du menu Fichier ; vérifiés sous la CSP de l'hôte dans Chromium, le test de bout en bout va à P6-T10) | B-0-04, B-2.14, B-2.15, S-5.4 (CSP), S-4.1-08 |
| P6-T10 | Portage des 176 tests unitaires et des 49 parcours e2e, plus les nouveaux (jeton de lancement, .topogenia, annulation après rechargement, exports sous CSP), axe-core (fait, ADR 067 : lanceur de test contre le vrai hôte, modes Poste et Serveur ; 70 parcours ; tableaux `docs/couverture-p6/` ; défauts corrigés : pont des « + », Échap avec la fiche, cibles des radios, consigne IA ; l'installation Poste S-11-04 va à P7-T01) | B-0-06, B-0-07, B-0-08, B-8.3, S-11 |
| P6-T09b | Correctif d'AR-52 relevé en P6-T10 : la police Inter intégrée au PDF (fonte TrueType d'un paquet npm sous OFL, chargée à la demande) au lieu du passage en Latin-1, pour que les langues ajoutées par configuration s'impriment ; tests du PDF et parcours B-8.3-12 adaptés (fait, révision de l'ADR 066 : `inter-font` 3.19.0, faces `Inter-Regular` et `Inter-Bold`, `export/pdfFonts.ts`, texte relu par la table ToUnicode) | AR-52, B-2.14-14, B-2.14-19, NF12 |
| P6-T11 | Dossier de démonstration `docs/recette/demonstration/` (captures, scénario pas à pas sur un projet fictif) (fait : 30 captures et 17 pas sur le projet fictif « Notes de frais », joués sur le vrai hôte en mode Poste par `npm run demonstration` (`web/scripts/demonstration/`), sous un compte fictif donné au lanceur des parcours par `--account` ; exports gardés ; défaut relevé : F-25 aggravé, d'où P6-T11b) | S-P-18, S-P-19 |
| P6-T11b | Correctif de F-25 relevé en P6-T11 : le badge de fréquence et les pastilles RACI sous une étape ne cachent plus une flèche de la pointe basse ni sa pointe ; captures de la démonstration refaites (fait, ADR 068, AR-128 : ce qui pend sous une étape s'écarte de 10 px de l'axe dès qu'une flèche part ou arrive par la pointe basse, du côté où ses flèches ne courent pas ; AR-58 étendu ; module `routing/under.ts`, parcours `under-step.spec.ts`) | spec 9.4, 9.7, B-2.9-08, B-2.11-05, AR-58 |
| P6-T11c | Correctif relevé par le porteur du projet sur la démonstration (capture 07) : deux flèches vers la pointe gauche du losange NDF-E009 montraient deux pointes décalées de 6 px ; démonstration refaite (fait, AR-129, révision de l'ADR 059 : les flèches qui arrivent sur un même côté d'une étape partagent leur dernier segment et finissent sur un seul point ; `routing/tracks.ts`) | spec 9.7, S-9.7-02, AR-24 |
| P6-T12 | Porte : Fidélité, Architecture, Sécurité, Interface et charte, Intégrité des tests (prompt 7.1) (franchie au round 4 le 29/10/2026 : rounds 1 et 2 à cinq contrôleurs, 3 et 4 Architecture seule ; corrections AR-130 à AR-143, ADR 069 et 070) | - |

## Phase 7 - Paquets et recette

| Tâche | Contenu | Lignes visées |
|---|---|---|
| P7-T01 | Publication Poste : exécutable autonome `win-x64`, `PublishSingleFile`, ZIP portable seul, non signé, MSIX retiré (ADR 049) ; verrou nommé, second lancement (fait, ADR 071, AR-144 : profil `Desktop.pubxml`, `build/package.ps1 -Mode Desktop`, second lancement par deux événements nommés, exécutable vérifié) | S-6.1 |
| P7-T02 | Publication Serveur : ZIP, `web.config`, `appsettings.Production.example.json` (fait, ADR 072, AR-145 : `Server.pubxml`, `web.config` du dépôt, `build/package.ps1 -Mode Server`, charge de 30 clients sur l'hôte publié ; IIS et le compte du seul rôle `topogenia_app` à la recette d'installation) ; charge de 30 utilisateurs simultanés sur l'hôte publié (NF07, S-NF-09) ; recette de l'hôte publié sous un compte membre de `topogenia_app` seul : démarrage, santé, connexion, création d'un projet et d'un couloir (A-4-40, AR-110), résultat dans `docs/recette/finale/` | S-6.2 |
| P7-T03 | SBOM CycloneDX, empreintes SHA-256, `THIRD-PARTY-NOTICES.md` regénéré (`build/third-party-notices.mjs`, F-31 et F-41 : le servi lu dans la construction, mode de contrôle, licences relevées confirmées) (fait, ADR 073, AR-146 ; CLI CodeQL absente : analyse non jouée) ; `build/codeql-local.ps1` (CLI CodeQL si installée, rien d'installé ; étape CodeQL de la CI désactivée, ADR 049) | S-5.4 (SBOM) |
| P7-T04 | `docs/exploitation-poste.md`, `docs/exploitation-serveur.md`, `docs/securite.md` (liste cochée avec preuves) (fait : guides, liste générée par `build/security-list.mjs` et sa revue ASVS et TISAX, `Cache-Control: no-store`, phrase de `PARAMETRES_INVALIDES`) ; consignes nées en phase 3 à y reporter : `--migrate` lancé application arrêtée (il pose RCSI par `ROLLBACK IMMEDIATE`, qui annule les transactions ouvertes des autres sessions, ADR 031) ; `--migrate` sous un compte membre de `db_owner` (spec 5.3) ; `VIEW DEFINITION` sur la base donné à `topogenia_app` par la migration, nécessaire au contrôle du démarrage (ADR 032, 037) ; lecture des refus de connexion : 18456 seul, connexion refusée par l'instance (`DROITS_INSUFFISANTS`, compte à autoriser sur l'instance), 4060 avec ou sans 18456, base absente ou compte sans utilisateur dans la base (`BASE_INACCESSIBLE`) (ADR 036) ; mode Poste : conduite sur `VERROU_MIGRATIONS` (ADR 041) ; diffusion par le dossier interne de téléchargement et vérification des empreintes (ADR 049) ; retour arrière du mode Serveur par la restauration seule (AR-97) ; écriture SQL hors application non prise en charge (AR-99) | S-5, S-6, S-6.2-04 |
| P7-T04b | Cœur fermé (ADR 048) (fait, ADR 074 : chaîne `build/core/`, bascule `UseCorePackage`, dépôt public écrit et prouvé par `build/split.mjs` ; la copie de travail tient lieu de dépôt privé, AR-147) : dépôt privé `topogenia-core` (domaine et ses tests), paquet NuGet `Topogenia.Core` signé par nom fort et obscurci (Obfuscar), référence conditionnelle au paquet dans le dépôt public, `ProductIdentity.cs` sorti du domaine ; preuve : le dépôt public compile et passe ses tests avec le seul paquet | ADR 048 |
| P7-T05 | Dossier `docs/recette/finale/` ; `docs/RAPPORT_FINAL.md` (fait : `LISEZMOI.md` complet, ZIP refaits, rapport écrit ; `build/package.ps1` restaure l'hôte avant la vérification des composants) | S-11, S-P |
| P7-T06 | Porte : Architecture, Sécurité, Intégrité des tests (prompt 7.1) | - |
| P7-T07 | Passe de finition : reprendre le registre `docs/a-peaufiner.md` entrée par entrée (mineurs, lignes partielles, mutations non jouées, doublons de tests), avant le rapport final (fait : issue de chaque entrée, corrections, AR-148) | - |

## Rattachement des lignes de couverture aux phases

Généré depuis la colonne « Phase » de `couverture.md` : chaque sous-section des annexes dont au moins une ligne est affectée à la phase y est traitée, par la tâche qui la cite dans « Lignes visées » ou, à défaut, par la tâche de la phase qui porte le thème. Les lignes de la phase sont toutes cochées à la porte de la phase. Une ligne qui cite plusieurs phases compte dans chacune. Aucune ligne des annexes n'est affectée à la phase 7 ; sans phase (« — ») : B-4-09, B-6-09, B-6-15.

| Phase | Lignes A / B | Sous-sections (nombre de lignes) |
|---|---|---|
| 1 | 13 / 111 | A-2.6 (1), A-3 (1), A-4 (1), A-5 (3), A-6 (7), B-1.2 (2), B-2.1 (8), B-2.9 (4), B-2.10 (9), B-2.12 (6), B-3.1 (2), B-3.2 (16), B-3.3 (1), B-3.4 (3), B-4 (1), B-6 (50), B-8.1 (6), B-8.2 (3) |
| 2 | 392 / 0 | A-0 (1), A-1.1 (2), A-1.4 (1), A-1.5 (5), A-2.1 (13), A-2.2 (26), A-2.3 (15), A-2.4 (29), A-2.5 (6), A-2.6 (25), A-2.7 (9), A-2.8 (42), A-2.9 (55), A-2.10 (34), A-2.11 (18), A-2.12 (3), A-2.13 (13), A-2.14 (9), A-2.15 (9), A-2.17 (4), A-2.18 (5), A-3 (44), A-4 (4), A-5 (2), A-6 (18) |
| 3 | 107 / 0 | A-2.1 (5), A-2.2 (3), A-2.4 (5), A-2.6 (2), A-2.9 (5), A-2.10 (5), A-2.11 (3), A-2.12 (1), A-2.13 (1), A-2.14 (3), A-2.15 (5), A-2.16 (5), A-3 (2), A-4 (18), A-5 (29), A-6 (15) |
| 4 | 375 / 101 | A-0 (2), A-1.1 (18), A-1.2 (25), A-1.3 (26), A-1.4 (22), A-1.5 (22), A-2.1 (15), A-2.2 (7), A-2.3 (4), A-2.4 (9), A-2.5 (2), A-2.6 (6), A-2.8 (7), A-2.9 (17), A-2.10 (9), A-2.11 (7), A-2.14 (15), A-2.15 (17), A-2.16 (7), A-2.18 (3), A-3 (73), A-4 (43), A-5 (2), A-6 (17), B-0 (4), B-1.2 (2), B-1.4 (1), B-1.5 (1), B-2.1 (1), B-2.3 (4), B-2.5 (2), B-2.6 (40), B-2.7 (4), B-2.8 (1), B-2.9 (11), B-2.10 (2), B-2.11 (2), B-2.12 (4), B-2.13 (3), B-4 (5), B-5 (6), B-6 (2), B-7 (3), B-8.1 (3) |
| 5 | 73 / 3 | A-0 (1), A-1.2 (8), A-2.2 (1), A-2.13 (1), A-2.17 (46), A-2.18 (1), A-3 (3), A-4 (7), A-6 (5), B-1.6 (1), B-2.15 (2) |
| 6 | 0 / 908 | B-0 (17), B-1.1 (11), B-1.2 (20), B-1.3 (28), B-1.4 (15), B-1.5 (4), B-1.6 (24), B-1.8 (2), B-2.1 (48), B-2.2 (12), B-2.3 (47), B-2.4 (58), B-2.5 (36), B-2.6 (18), B-2.7 (23), B-2.8 (17), B-2.9 (37), B-2.10 (26), B-2.11 (18), B-2.12 (23), B-2.13 (6), B-2.14 (31), B-2.15 (2), B-3.1 (20), B-3.2 (20), B-3.3 (4), B-3.4 (39), B-4 (79), B-5 (28), B-6 (52), B-7 (20), B-8.1 (43), B-8.2 (11), B-8.3 (69) |
