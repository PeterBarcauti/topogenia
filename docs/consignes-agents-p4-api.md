<!-- consignes-agents-p4-api.md -->
<!-- Common instructions for the agents that write the HTTP entry points of phase 4 (P4-T04) -->

# Consignes communes aux agents des points d'entrée HTTP (phase 4, P4-T04)

## À lire d'abord

- Le prompt `C:\Temp\Claude\PROJETS\Encours\45_Topogenia\PROMPT_CONCEPTION.md` (sections 3 à 6 et 10 : décisions,
  interdits, environnement, méthode, forme).
- La spécification `C:\Temp\Claude\PROJETS\Encours\45_Topogenia\Specification\TOPOGENIA_2.0_SPECIFICATION_COMPLETE.md` :
  3.2, 3.6, annexe A section 1 (table des routes : rôle, corps, codes de chaque route), les sous-sections de l'annexe A 2
  de ton lot (champs et codes), 2.16 (concurrence), 2.18 (textes saisis).
- `docs/couverture.md` : la table « Correspondance des routes 1.x -> 2.0 » (route 2.0 de chaque route 1.x) et les lignes
  A-1.x de ton lot ; `docs/decisions/000-arbitrages-phase-0.md` : AR-50 (réponse d'une opération de structure), AR-60
  (noms des champs en anglais snake_case ; clés de détail `nb_etapes` et `code` gardées), AR-08, AR-49, AR-64.
- Le code 1.x, en LECTURE SEULE, pour la forme exacte des corps et des réponses :
  `C:\Temp\Claude\PROJETS\Encours\43_Outil_Collaboratif\carto\api\Controllers\Controleurs.cs` et `Services/*.cs`
  (records `*Demande`, `*Vue`).
- **Le modèle à suivre**, déjà écrit et vert (53 tests sur les deux bases) :
  - gestionnaires d'API : `src/Topogenia.Host/Api/Lanes/LaneApi.cs`, `Api/Maps/MapApi.cs`, `Api/Projects/ProjectApi.cs`,
    `Api/Profile/ProfileApi.cs`, `Api/Steps/StepApi.cs`, `Api/Flows/FlowApi.cs` ;
  - aides : `Api/ApiInput.cs` (`Version` pour `If-Match`, `Required`, `Code`, `Change`), `Api/StructureReply.cs`,
    `Json/Optional.cs` (champ d'un PATCH : absent, ou présent avec une valeur éventuellement nulle) ;
  - routes : `src/Topogenia.Host/Endpoints/MapEndpoints.cs`, `ProjectEndpoints.cs`, `StepEndpoints.cs` ;
  - tests : `tests/Topogenia.Host.Tests/Maps/MapRoutesTests.cs`, `Api/CommonRulesTests.cs`, et l'infrastructure
    `tests/Topogenia.Host.Tests/Infrastructure/` (`ApiHosts`, `ApiHost`, `ApiProject`, `ApiClient`, `ApiResponse`, `Logins`).
- Les commandes et lectures de ton lot, déjà écrites et vertes : `src/Topogenia.Application/<Fonctionnalité>/` (une
  commande par cas, son droit, son `If-Match` par `WithVersion`), et les requêtes du domaine qu'elles prennent
  (`src/Topogenia.Domain/<Dossier>/*Requests.cs` ou `*Rules.cs`).

## Ce qu'est un point d'entrée

Deux parties, imposées par les tests d'architecture :

1. **La route** (`src/Topogenia.Host/Endpoints/<Lot>Endpoints.cs`, le fichier de ton lot, déjà créé et vide) : un
   `MapGet` / `MapPost`... par route, dont le délégué ne fait que lire la requête (route `{id:guid}`, chaîne de requête,
   `[FromHeader(Name = "If-Match")] string? ifMatch`, corps) et appeler le gestionnaire, puis rendre `TypedResults.Ok(...)`,
   `TypedResults.Created(...)` ou `TypedResults.NoContent()`. **Le dossier `Endpoints/` ne doit dépendre d'aucun type de
   `Topogenia.Domain`, `Topogenia.Persistence` ni `Topogenia.Interchange`** (`DependencyRulesTests.Http_entry_points_depend_on_handlers_only`) :
   il ne voit que les types de l'hôte (`Api/`) et de l'application. Toute déclaration de route (`Map*`) hors de
   `Endpoints/` est refusée.
2. **Le gestionnaire d'API** (`src/Topogenia.Host/Api/<Fonctionnalité>/<Fonctionnalité>Api.cs`) : une classe `sealed` dont
   le nom finit par `Api` (elle est enregistrée automatiquement, par son nom, dans l'injection), qui reçoit les runners
   par son constructeur (`CommandRunner`, `QueryRunner`, `UserRunner`, `LanguageSettings`...), lit le corps dans la
   requête du domaine, lance la commande et rend la réponse. Les corps sont des `record` du même fichier, nommés
   `<Action>Body`, aux propriétés anglaises (le JSON est en snake_case : `LaneId` s'écrit `lane_id`) : les noms suivent
   la 1.x traduite selon AR-60. Un gestionnaire ne contient **aucune règle** : il ne fait que lire et transmettre.
   - un champ dont le domaine a besoin comme identifiant ou nombre non nullable : `ApiInput.Required(body.X, "x")`
     (CHAMP_OBLIGATOIRE, jamais `Guid.Empty` ni 0, A-2.18-07) ; un texte ou un code reste une chaîne transmise au
     domaine, qui refuse lui-même (CHAMP_OBLIGATOIRE, VALEUR_INVALIDE...) ;
   - un code de liste fermée en chaîne de requête ou dans le corps quand la requête du domaine prend une énumération :
     `ApiInput.Code<T>(texte, "nom")` (VALEUR_INVALIDE sur ce nom) ;
   - un PATCH : `Optional<T>` par champ et `ApiInput.Change(...)` vers le `FieldChange` du domaine ;
   - `If-Match` : `ApiInput.Version(ifMatch)` passé à la fabrique `WithVersion` de la commande.

## Réponses

- Toute commande qui rend une vue de carte (`CommandOutcome.View` non nul) répond un `StructureReply`
  (`StructureReply.Of(outcome, id, code, ids)`) : `id`, `code`, `ids` de l'objet créé ou touché (AR-50 : ce que la 1.x
  mettait dans `ResultatStructure`), plus l'opération, les avertissements, la carte et l'historique.
- Une lecture répond son objet (vue de l'application) tel quel. Une écriture dont la 1.x renvoyait une vue (fiche
  `FicheVue`, projet `ProjetDetail`) enchaîne la lecture correspondante après la commande et la renvoie (la commande ne
  connaît pas la visibilité selon le rôle : la lecture la connaît).
- Créations de projet : 201 + `Location` ; restauration, mise à la corbeille, purge : 204, comme la 1.x.
- Les erreurs ne se traitent pas dans le gestionnaire : toute `DomainException` remonte, `Errors/ErrorMiddleware` en fait
  `{code, message, details}` dans la langue de l'utilisateur.

## Tests

- xUnit v3, dans `tests/Topogenia.Host.Tests/<Fonctionnalité>/`, classes `IClassFixture<ApiHosts>`, chaque test en
  `[Theory]` sur `[MemberData(nameof(Engines.All), MemberType = typeof(Engines))]` (SQLite et SQL Server) ; chaque test
  crée son projet (`ApiProject.CreateAsync(await hosts.OfAsync(kind))`) et passe par HTTP seulement. Les corps des
  tests nomment les champs en snake_case (`new { lane_id = ..., column = 0 }`) : c'est le contrat qui est prouvé.
- **Ne modifie pas** `tests/Topogenia.Host.Tests/Infrastructure/`, `Api/`, `Maps/`, `Errors/`. S'il te manque une aide,
  écris-la dans ton dossier de tests.
- Pour chaque route de ton lot : le cas nominal (statut, forme de la réponse, effet relu par une lecture), le refus du
  rôle (404 pour un non-membre au moins une fois par groupe de routes, 403 pour un rôle trop faible), les refus propres
  à la route qui viennent du corps ou de la chaîne de requête (champ absent, code inconnu, `If-Match` périmé pour une
  modification), et un ou deux refus métier de sa ligne de l'annexe A 1. Un test vérifie le comportement (valeur relue,
  code et détails exacts), jamais seulement l'absence d'erreur. Les règles métier sont déjà prouvées par les couches
  domaine et application : ici on prouve la route, le corps, la réponse et le chemin des erreurs.
- Lance toi-même `tests/Topogenia.Host.Tests` et `tests/Topogenia.Architecture.Tests` avec ton propre
  `--artifacts-path` (un dossier de ton scratchpad) et `DOTNET_CLI_TELEMETRY_OPTOUT=1 TESTINGPLATFORM_TELEMETRY_OPTOUT=1` :
  tout doit être vert, 0 avertissement (les avertissements sont des erreurs). D'autres agents travaillent en même temps
  sur d'autres lots : un échec dans un fichier qui n'est pas à toi se signale, il ne se corrige pas.
- Éprouve tes tests par quelques mutations (route qui n'appelle pas la bonne commande, `If-Match` non transmis, champ
  mal lu) ; après chaque mutation, remets le fichier **et touche-le** (`touch`) pour forcer la recompilation.

## Règles

- Bases SQL Server : instance `.`, authentification Windows ; seulement des bases `Topogenia_*` (l'infrastructure des
  tests les crée et les supprime). Fichiers SQLite : seulement dans un dossier temporaire supprimé par le test.
- Forme : code, identifiants et commentaires en anglais ; en-tête de deux lignes (nom du fichier, puis description) ;
  UTF-8 sans BOM, LF (avec python : `open(..., newline='\n')`) ; fichier de 600 lignes au plus (cible 400), méthode de
  80 au plus (cible 40) ; aucun `TODO`, aucun emoji, aucune suppression d'avertissement ; noms des tests en phrases
  anglaises qui décrivent la règle. Exemples fictifs seulement.
- Ne jamais affaiblir un test. Aucun paquet nouveau, aucune installation. Rien dans `43_Outil_Collaboratif` ; aucun
  commit, aucun envoi sur Internet.
- Ne modifie que les fichiers de ton lot (listés dans ta mission). `src/Topogenia.Application` et `src/Topogenia.Domain`
  ne se modifient pas : un manque se signale (fichier, ligne d'annexe, besoin) dans ton compte rendu.
- Couverture : ne modifie pas `docs/couverture.md` ; écris `docs/couverture-p4/api-<lot>.md`
  (`| ID | État | Test 2.0 | Remarque |`), une ligne par ligne d'annexe (A-1.x surtout, et les lignes A-2.x qui citent le
  niveau API) que tes tests prouvent au niveau HTTP, avec le nom complet du test (`Topogenia.Host.Tests.<Dossier>.<Classe>.<Méthode>`).
  Ajoute en fin de fichier la liste des routes de ton lot sous la forme `| Méthode | Route 2.0 | Gestionnaire |`.
- Compte rendu final, court : fichiers créés, nombre de tests, mutations jouées et tuées, routes nommées par toi
  (absentes de la table), écarts et manques.
