<!-- consignes-agents-p4.md -->
<!-- Common instructions for the agents that write the handlers of phase 4 (P4-T02) -->

# Consignes communes aux agents des gestionnaires (phase 4, P4-T02)

## À lire d'abord

- Le prompt `C:\Temp\Claude\PROJETS\Encours\45_Topogenia\PROMPT_CONCEPTION.md` (sections 3 à 6 et 10 : décisions,
  interdits, environnement, méthode, forme).
- La spécification `C:\Temp\Claude\PROJETS\Encours\45_Topogenia\Specification\TOPOGENIA_2.0_SPECIFICATION_COMPLETE.md` :
  3.4 (Application), 4.6 et 4.7 (journal, verrou), 5.3 à 5.5 (droits, validation, audit), annexe A section 1 (table
  des routes : le rôle de chaque route) et la sous-section de l'annexe A 2 de ton lot, annexe B 2.6 (opérations
  annulables et non annulables).
- `docs/architecture.md` §3 (Application : gabarit, journal, ports), `docs/decisions/042-gabarit-de-commande-et-pile-du-journal.md`,
  `docs/decisions/000-arbitrages-phase-0.md` (AR-05, AR-08, AR-21, AR-43, AR-50), `docs/couverture.md` (lignes de ton
  lot dont la colonne Phase contient 4).
- **Le modèle à suivre**, déjà écrit et vert : `src/Topogenia.Application/Lanes/LaneCommands.cs` et ses tests
  `tests/Topogenia.Application.Tests/Lanes/LaneCommandsTests.cs` ; la lecture `src/Topogenia.Application/Maps/MapQueries.cs`
  et `tests/Topogenia.Application.Tests/Queries/QueryRunnerTests.cs` ; le gabarit `src/Topogenia.Application/Commands/`
  (`ProjectCommand`, `CommandRunner`, `VersionCheck`) et `src/Topogenia.Application/Queries/` (`ProjectQuery`,
  `QueryRunner`) ; les genres du journal `src/Topogenia.Application/OperationLog/OperationKinds.cs`.
- Le domaine de ton lot, en lecture : les opérations existent toutes (`src/Topogenia.Domain/<Dossier>/`), avec leurs
  requêtes et résultats. Le code 1.x, en LECTURE SEULE : `C:\Temp\Claude\PROJETS\Encours\43_Outil_Collaboratif\carto\api\`
  (`Services/*.cs`, `Controleurs*.cs`) et `carto\web\src\carte\magasin.ts` (ce que la 1.x inscrivait dans l'historique).

## Ce qu'est un gestionnaire

- Une commande est une classe `sealed` qui hérite de `ProjectCommand<TResult>`, porte **un seul** attribut de droit
  (`[RequiresRole(MemberRole.X)]`, `[RequiresSheetFill]`, `[RequiresAdmin]`) égal au rôle de sa route dans l'annexe A 1,
  et ne contient **aucune règle** : elle appelle une opération du domaine et rend son résultat. Une lecture hérite de
  `ProjectQuery<TResult>` de la même façon.
- Elle dit : sa cible (`AccessTarget`), le contenu à charger (`Content` : ce que l'opération du domaine lit, par exemple
  `Members` pour un acteur ou un assigné, `All` pour une suppression qui emporte fiches et documents), sa carte
  (`MapOf`, lue avant l'opération ; `null` si elle ne touche aucune carte), son genre (`LogKind`, une constante de
  `OperationKinds`, ou `null` pour une opération non annulable : fiche, documents et usages, suppression définitive
  d'un élément du reliquat, fermeture d'un cadre vide, traductions, membres ; annexe B 2.6, AR-21), le `If-Match`
  (une fabrique statique `WithVersion(request, int? version)` comme `UpdateLaneCommand` pour toute modification d'un
  objet versionné, A-2.16-05), les avertissements de l'opération (`WarningsOf`).
- Les requêtes du domaine (`AddLaneRequest`...) sont les entrées des commandes : l'hôte (P4-T04) les remplira depuis
  le JSON. N'ajoute pas de DTO d'entrée.
- Un comportement absent du domaine ou faux se signale (fichier, ligne d'annexe, besoin) dans ton compte rendu : tu ne
  modifies `src/Topogenia.Domain` que si ta mission le dit.

## Tests

- xUnit v3, dans `tests/Topogenia.Application.Tests/<Fonctionnalité>/`, avec les faux de
  `tests/Topogenia.Application.Tests/Support/` (`TestProject` : projet PRJ, carte racine, un couloir, animateur,
  contributeur, lecteur, administrateur non membre, inconnu ; `Runner`, `Queries`, `Database`). **Ne modifie pas**
  `Support/`, ni `Commands/`, `OperationLog/`, `Queries/`, `Maps/`, `Lanes/` des tests : s'il te manque une aide,
  écris-la dans ton dossier (`<Fonctionnalité>/<Fonctionnalité>Fixture.cs`).
- Pour chaque commande : le cas nominal (écrit, vue rendue, genre inscrit ou rien d'inscrit), les refus principaux de
  ses lignes d'annexe (code et détails), le `If-Match` périmé pour une modification ; pour chaque opération annulable,
  l'annulation **et** le rétablissement qui remettent l'état d'avant puis d'après (lignes B-2.6-16 à 35 de ton lot) ;
  pour une opération non annulable, aucune entrée au journal (B-2.6-37 à 44). Un test vérifie le comportement, pas
  l'absence d'erreur.
- Ajoute les droits de tes commandes et lectures dans **ton** fichier `tests/Topogenia.Application.Tests/Commands/CommandCatalog.<Lot>.cs`
  (sur le modèle de `CommandCatalog.Lanes.cs`) : le test `CommandCatalogTests` exige que toute commande y soit, avec
  le rôle de sa route.
- Lance toi-même `tests/Topogenia.Application.Tests` et `tests/Topogenia.Architecture.Tests` (et
  `tests/Topogenia.Persistence.Tests` si ton lot touche la persistance), avec ton propre `--artifacts-path` dans ton
  dossier du scratchpad et `DOTNET_CLI_TELEMETRY_OPTOUT=1 TESTINGPLATFORM_TELEMETRY_OPTOUT=1`. Tout doit être vert,
  0 avertissement. D'autres agents travaillent en même temps sur d'autres lots : un échec dans un fichier qui n'est
  pas à toi se signale, il ne se corrige pas.
- Éprouve tes tests par quelques mutations (retirer l'appel du domaine, changer le genre, retirer le `If-Match`) ;
  après chaque mutation, remets le fichier **et touche-le** (`touch`) pour forcer la recompilation.

## Règles

- Bases SQL Server : instance `.`, authentification Windows ; seulement des bases `Topogenia_*`. Fichiers SQLite :
  seulement dans le scratchpad ou un dossier temporaire supprimé par le test.
- Forme : code, identifiants et commentaires en anglais ; en-tête de deux lignes (nom du fichier, puis description) ;
  UTF-8 sans BOM, LF (avec python : `open(..., newline='\n')`) ; fichier de 600 lignes au plus (cible 400), méthode de
  80 au plus (cible 40) ; aucun `TODO`, aucun emoji, aucune suppression d'avertissement ; noms des tests en phrases
  anglaises qui décrivent la règle. Exemples fictifs seulement.
- Ne jamais affaiblir un test. Aucun paquet nouveau, aucune installation. Rien dans `43_Outil_Collaboratif` ; aucun
  commit, aucun envoi sur Internet.
- Couverture : ne modifie pas `docs/couverture.md` ; écris `docs/couverture-p4/<lot>.md`
  (`| ID | État | Test 2.0 | Remarque |`), une ligne par ligne d'annexe de ton lot que tes tests prouvent au niveau
  Application, avec le nom complet du test. Le niveau API (HTTP) relève de P4-T04 et P4-T08 : écris
  `couvert (application)` quand la ligne cite aussi le niveau API.
- Compte rendu final, court : fichiers créés, nombre de tests, mutations jouées et tuées, écarts et manques du domaine.
