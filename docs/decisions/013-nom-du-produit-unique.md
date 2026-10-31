<!-- 013-nom-du-produit-unique.md -->
<!-- D13 : le nom du produit est écrit dans une constante C# et une clé i18n, nulle part ailleurs -->

# 013 - Nom du produit écrit une seule fois

Statut : acceptée (date 23/10/2026), révisée le 23/10/2026 (porte P3, round 2 : noms techniques figés)

## Contexte

Le produit change de nom entre la 1.x (Ariane) et la 2.0 (Topogenia, décision du
22/10/2026). En 1.x, le nom est répandu dans les en-têtes HTTP, les clés de stockage, les
codes d'erreur et les fichiers exportés. Un renommage doit tenir en une ligne.

## Décision

- Côté serveur, le nom vit dans la constante `ProductIdentity.Name` de
  `src/Topogenia.Application/ProductIdentity.cs` (dans le domaine jusqu'à P7-T04b, ADR 074). L'extension du paquet d'échange et le nom de son format ne le suivent
  pas : ce sont des noms techniques figés (section « Noms techniques figés », ADR 051, révision du 25/10/2026).
- `Directory.Build.props` lit cette constante au moment de la construction
  (`ProductIdentityFile`, `ProductName`) pour nommer le produit et l'exécutable.
- Côté interface, le nom vient d'une clé i18n ; le pied de page PDF, la feuille cachée du
  classeur et le `creator` des exports en sont tirés (AR-28).
- Identifiants techniques neutres : en-tête anti-CSRF `X-Requested-With` (AR-13), clés de
  stockage `app.*`, codes d'erreur sans nom de produit (AR-28).

## Conséquences

- Renommer = modifier la constante et la clé i18n. Le code compile et la suite reste verte : essai fait sur une
  copie du dépôt (`Name = "Topogenix"`), voir « Noms techniques figés ».
- Les noms de projets .NET et d'espaces de noms (`Topogenia.*`) sont des noms de code et
  relèvent de la structure de la solution (spec 3.1), pas du nom affiché.

## Vérification

- `tests/Topogenia.Architecture.Tests/SourceRulesTests.cs`
  (`SourceRulesTests.The_product_name_is_written_only_in_its_constant`,
  `SourceRulesTests.The_persistence_derives_no_technical_name_from_the_displayed_product_name`).
- `tests/Topogenia.Architecture.Tests/DecisionRulesTests.cs`
  (`DecisionRulesTests.The_technical_names_are_those_of_the_solution_and_of_the_migrations`).
- `tests/Topogenia.Persistence.Tests/Schema/SchemaMigrationTests.cs`
  (`SchemaMigrationTests.On_sql_server_the_tables_live_in_the_fixed_technical_schema`).
- Porte de contrôle Architecture.

## Noms techniques figés

Quelques identifiants portent le nom du produit parce que la spécification les fixe ainsi : le schéma SQL Server
`topogenia` et les rôles `topogenia_app` / `topogenia_bi` (spec 4.1, 4.4), la section de configuration `Topogenia:`
(spec 6.2), l'extension `.topogenia` (D11) et le format `topogenia-package` du manifeste, que `Interchange/Packages/PackageFormat`
dérive de l'identifiant racine `nameof(Topogenia)` (ADR 051). Le nom de l'assemblage des tests de persistance
(`Topogenia.Persistence.Tests`), auquel les fournisseurs ouvrent leurs sondes (ADR 037), est figé par son projet.

Ce sont des **noms techniques**, figés comme les espaces de noms `Topogenia.X`, et non le nom affiché :

- Ils vivent dans une constante technique unique, `TechnicalNames` (`src/Topogenia.Persistence/Schema/TechnicalNames.cs`) :
  `Root` est l'identifiant de l'espace de noms racine (`nameof(Topogenia)`), écrit comme un identifiant et jamais comme
  un texte ; `PersistenceTests` en dérive le nom de l'assemblage des tests, `Schema` le schéma (la racine en
  minuscules, `topogenia`). Les rôles dérivent du schéma (`SqlServerAccess`).
- Le nom affiché (`ProductIdentity.Name`) reste seul dans sa constante. Dans la persistance, seul le dossier et le fichier
  de données du mode Poste le suivent (`DesktopDataFile`, spec 5.2, ADR 035) ; aucune autre classe de la persistance ne
  le lit (règle `The_persistence_derives_no_technical_name_from_the_displayed_product_name`).
- Les migrations publiées écrivent le schéma et les rôles en clair (`"topogenia"`, `topogenia_app`) : elles figent l'état
  de la base et ne changent jamais (spec 4.4). La règle `The_technical_names_are_those_of_the_solution_and_of_the_migrations`
  vérifie que `TechnicalNames.Schema` est le schéma que les migrations créent et que les rôles créés sont ceux de
  `SqlServerAccess`.

Conséquence : renommer le produit ne change ni le schéma, ni les rôles, ni l'assemblage des tests. Une base existante
et une base neuve ont le même schéma ; renommer le schéma d'une base en service serait une migration de données, pas un
renommage, et aucune n'est prévue. La version précédente de cet ADR dérivait le schéma, les rôles et l'ami des
fournisseurs de `ProductIdentity.Name` : sur une copie renommée, la compilation échouait (`InternalsVisibleTo` vers un
assemblage qui n'existe pas, 32 erreurs CS0122) puis le modèle ne correspondait plus aux migrations
(`HasPendingModelChanges`). Essai refait après correction sur une copie du dépôt (`Name = "Topogenix"`) : la solution
compile sans avertissement et la suite a les mêmes résultats que la copie non renommée.

La section de configuration `Topogenia:` et l'extension `.topogenia` suivent, elles, le nom affiché (D11, spec 6.2) ;
elles seront dérivées de `ProductIdentity.Name` quand l'hôte et le format arriveront (phases 4 et 5).
