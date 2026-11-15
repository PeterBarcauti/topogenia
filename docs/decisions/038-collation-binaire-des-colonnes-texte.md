<!-- 038-collation-binaire-des-colonnes-texte.md -->
<!-- Toute colonne texte de SQL Server compare en binaire, comme SQLite : unicité, listes fermées, filtres et recherches identiques -->

# 038 - Collation binaire des colonnes texte en SQL Server

Statut : acceptée (date 23/10/2026)

## Contexte

La spec 4.3-3 interdit toute comparaison de texte insensible à la casse dans la base : la casse est traitée par des
colonnes normalisées (`login_normalized`, `Login.Normalize`) ou en mémoire (A-5-05, A-5-32). Le modèle 2.0 ne donnait
aucune collation aux colonnes (test `ColumnTests.No_column_has_a_collation_of_its_own`). Or une colonne SQL Server sans
collation propre prend celle de la base, et celle-ci vient de l'instance : `French_CI_AS` sur l'instance locale,
insensible à la casse et à la largeur. Constat de la porte P3 (round 1, données M1) :

- `STRASSE` et `STRAßE`, `ABC` et `ＡＢＣ` (pleine chasse) : deux logins normalisés différents pour le domaine (majuscules
  invariantes), acceptés par SQLite, refusés par SQL Server sur `UQ_app_user_login_normalized` (2601), puis rejoués trois
  fois en vain comme une course perdue (ADR 033) jusqu'à `CONFLIT_UNICITE` ;
- un code de projet, de couloir ou d'étape dans une autre casse : un autre code pour SQLite, le même pour SQL Server ;
- `CK_step_raci_role` (`[role] IN ('A', 'S', 'C', 'I')`), les autres CHECK de listes fermées et le filtre de
  `UX_step_raci_step_id_approver` (`[role] = 'A'`) acceptaient `a` en SQL Server ;
- une recherche par la forme en minuscules trouvait la ligne en SQL Server, aucune en SQLite ;
- le tri des libellés suivait les règles du français en SQL Server, les points de code en SQLite.

SQLite compare le texte en binaire (`BINARY`, octets UTF-8, donc l'ordre des points de code) et EF Core traduit `==`
par `=` sur les deux moteurs.

## Décision

- Le personnalisateur de modèle SQL Server (`SqlServerModelCustomizer.BinaryText`) donne la collation
  `Latin1_General_100_BIN2` à **toute** colonne stockée en texte : chaînes (`nvarchar`, `varchar`, `char`) et codes des
  listes fermées (énumérations, `CodeConverter`). Le modèle commun reste sans collation (aucune collation de base, aucune
  en SQLite).
- `BIN2` plutôt que la variante UTF-8 (`Latin1_General_100_BIN2_UTF8`) : les colonnes Unicode (`nvarchar`) sont en
  UTF-16 quelle que soit la collation et comparent par point de code ; les colonnes ASCII (`varchar`, codes, langues,
  empreintes) gardent la page de code 1252 des instances usuelles (même page que `French_CI_AS`), donc ni conversion des
  données ni changement du sens de la longueur `varchar(n)` (octets en UTF-8). `BIN2` (et non `BIN`) compare tous les
  caractères par point de code, pas seulement le premier.
- Libellés et textes saisis compris (noms, libellés, descriptions, JSON des versions) : une seule règle, aucune
  comparaison de texte de la base ne dépend de la langue de l'instance, et les deux moteurs rendent le même ordre pour
  les caractères du plan de base (hors du plan de base, voir le complément du round 2). Un tri
  de libellés pour l'affichage (ordre alphabétique de l'utilisateur) reste à l'application, en mémoire, avec la culture
  de l'utilisateur (spec 4.3-3) ; aucune requête de la 2.0 ne trie aujourd'hui par libellé.
- Migration SQL Server `20261023113221_BinaryTextCollation` (sixième) : les 69 `ALTER COLUMN ... COLLATE` sont générés
  par `dotnet ef` ; SQL Server refuse de changer la collation d'une colonne dont dépend une clé primaire, un index (clé ou
  filtre), un CHECK ou des statistiques (erreur 5074) : la migration supprime d'abord, écrits à la main d'après le modèle,
  4 clés primaires (`translation`, `project_counter`, `dissolution_object`, `schema_history`), 7 index et 21 CHECK, et
  les statistiques des colonnes texte (celles d'un administrateur bloquent le changement, 5074 ; celles que le serveur a
  créées seules, qu'il supprimerait de lui-même, le sont aussi ; il les recrée au besoin), puis recrée clés, index et CHECK à l'identique et rafraîchit les 11 vues `bi` (`sp_refreshview`). `Down` fait
  l'inverse. Aucune migration SQLite : rien ne change pour SQLite.
- Le test `ColumnTests.No_column_has_a_collation_of_its_own` est remplacé par
  `ColumnTests.Every_text_column_compares_in_binary_on_both_engines` (modèle, catalogue de la base migrée et
  comparaison réelle de chaque colonne texte d'un utilisateur avec sa valeur dans une autre casse).

## Conséquences

- Pour la casse, les accents et la largeur, les deux bases acceptent et refusent les mêmes lignes, et une recherche
  rend les mêmes lignes, quelle que soit la collation de l'instance SQL Server (ADR 025). La collation seule ne suffit
  pas pour les espaces de fin, les longueurs et les caractères hors ASCII d'une colonne `varchar` : le complément du
  round 2 donne la garantie et ses limites exactes. Une base créée sur une instance en `Latin1_General_CI_AS` ou
  `French_CI_AS` se comporte de même.
- Une ligne écrite à la main qu'une comparaison sans casse acceptait et qu'une comparaison binaire refuse (code d'une
  liste fermée dans une autre casse) fait échouer la migration entière, qui ne valide rien : la donnée est à corriger
  avant de migrer. Les données écrites par l'application ne peuvent pas être dans ce cas (codes stables, domaine).
- Les vues `bi` exposent des colonnes en `Latin1_General_100_BIN2` : un outil de rapport qui compare ou trie dans la
  base compare en binaire ; Power BI compare et trie dans son propre moteur.
- Les statistiques d'un administrateur sur une colonne texte sont supprimées par la migration et sont à recréer par lui.

## Vérification

`tests/Topogenia.Persistence.Tests/Collation/TextComparisonTests.cs` (deux moteurs) : logins `STRASSE`/`STRAßE` et
`ABC`/`ＡＢＣ` acceptés et retrouvés chacun par sa forme exacte, code de projet en minuscules accepté, recherche par la
forme en minuscules d'un login, d'un code de couloir et d'étape vide, codes de listes fermées dans une autre casse
refusés par leur CHECK (`CK_step_raci_role`, `CK_project_member_role`, `CK_step_step_type`, `CK_project_classification`),
tri des libellés par point de code. `Collation/CollationMigrationTests.cs` (SQL Server) : une base de la version
précédente, avec lignes et statistiques créées par le serveur, migre, a le schéma d'une base neuve, garde ses lignes, et
revient au schéma précédent. `Model/ColumnTests.Every_text_column_compares_in_binary_on_both_engines`. Mutations : sans
`BinaryText` (18 échecs SQL Server : `TextComparisonTests`, `ColumnTests`, modèle en avance sur les migrations), migration
en `French_CI_AS` au lieu de `BIN2` (10 échecs SQL Server). Les statistiques d'un administrateur ne sont pas reproduites
par la suite (les tests n'écrivent aucun SQL, spec 3.2) : un essai manuel sur l'instance locale (base `Topogenia_Test_*`
supprimée ensuite) a montré qu'une statistique `CREATE STATISTICS` sur la colonne fait échouer `ALTER COLUMN ... COLLATE`
(5074, 4922) et qu'une statistique créée par le serveur ne le fait pas ; une sonde de test du fournisseur (ADR 037)
pourrait le couvrir.

## Complément (porte P3, round 2) : espaces de fin, longueurs, ASCII, entiers et ordres

### Contexte

Constats données M1, m1, m2 et m3 du round 2 (sondes sur les deux moteurs) : la collation binaire ne rendait pas les
deux bases identiques.

- Espaces de fin : SQL Server complète par des espaces toute comparaison de texte, quelle que soit la collation
  (`'X' = 'X '` est vrai). `X` puis `X ` : deux logins pour SQLite, 2601 sur `UQ_app_user_login_normalized` en SQL
  Server (de même `UQ_project_code`) ; une recherche `== 'X '` trouve `X` en SQL Server, rien en SQLite ; un code de
  liste fermée suivi d'une espace passe le `IN` de SQL Server, pas celui de SQLite.
- Longueur : SQL Server refuse un texte plus long que sa colonne (2628, que le dialecte ne classait pas : erreur
  interne) ; SQLite garde 201 caractères dans un `nvarchar(200)` et 7 dans le `varchar(6)` du code de projet.
- Hors ASCII dans un `varchar` : SQL Server convertit vers la page de code de la colonne avant tout contrôle
  (`pl-ł€` relu `pl-l€`), SQLite garde le texte.
- Entiers : `structure_version + 1` sur `int.MaxValue` : SQL Server refuse (8115, non classé), SQLite écrit
  2147483648 dans son entier de 64 bits, que la lecture d'un `int` ne relit plus (le projet devient illisible).
- Ordres : `BIN2` trie un `nvarchar` par unité UTF-16 (un caractère hors du plan de base, paire de substitution dès
  U+D800, passe avant U+E000), SQLite par point de code ; `OrderBy(Id)` trie un `uniqueidentifier` par ses six
  derniers octets d'abord en SQL Server, par le texte de l'identifiant (l'ordre de création d'un Guid v7) en SQLite.

### Décision

Dans l'esprit de l'ADR 039 (cas jumeau des décimaux) : la base de chaque moteur refuse ce que l'autre refuse, et ce
qu'aucun contrôle de la base ne peut aligner est refusé avant toute écriture par un convertisseur commun.

- Garde de texte de chaque colonne (`ModelGuards.TextChecks`, déduite du modèle, qui porte les largeurs) : un CHECK
  `CK_<table>_<colonne>_text` sur **toute** colonne stockée en texte, codes des listes fermées compris (69 colonnes).
  Pas d'espace de fin (`NoTrailingSpace`) ; au plus sa largeur, exactement pour un `char(n)` (`TextLength`) ; ASCII
  imprimable, de l'espace au tilde, pour une colonne technique `varchar` (`PrintableAscii`). Rendu par le
  personnalisateur de modèle de chaque fournisseur (SQL propre à chacun, ADR 025) :

  | Garde | SQL Server | SQLite |
  |---|---|---|
  | `NoTrailingSpace` | `DATALENGTH([c]) = DATALENGTH(RTRIM([c]))` (une égalité serait complétée par des espaces) | `length("c") = length(rtrim("c"))` |
  | `TextLength` | `LEN([c]) <= n` ; `LEN([c]) = n` pour un `char(n)` | `length("c") <= n` ; `length("c") = n` |
  | `PrintableAscii` | `[c] NOT LIKE '%[^ -~]%'` (intervalle comparé en binaire par la collation de la colonne) | `"c" NOT GLOB '*[^ -~]*'` |

  Plusieurs gardes d'une colonne sont jointes par `AND` entre parenthèses. En SQL Server, le type de la colonne refuse
  déjà un texte trop long (2628) avant le CHECK ; le CHECK le dit pour les deux moteurs.
- Convertisseur commun `StoredTextConverter` (`Model/Conversions.cs`), posé sur toute colonne `string` du modèle : il
  refuse avant toute écriture (`ArgumentException`, comme une date locale, D08) un texte qui finit par une espace et,
  pour une colonne non Unicode, un texte hors ASCII imprimable. Une valeur comparée dans une requête passe par le même
  convertisseur : une recherche par `X ` est refusée sur les deux moteurs, au lieu de trouver `X` en SQL Server seul. Le
  domaine ne doit jamais produire ces valeurs (textes saisis rognés par `TextRules`, `Login` sans espace autour, codes,
  langues et genres en ASCII) : un refus est un défaut de l'appelant, donc une erreur interne. Écrit ainsi au round 2,
  c'était faux pour le nom et l'e-mail de l'annuaire, coupés à 128 et 256 caractères après le rognage : la coupe pouvait
  finir sur une espace, et l'utilisateur ne pouvait jamais être créé (complément du round 3).
- Dialecte SQL Server : 2628 (texte trop long) est lu comme le CHECK de la garde de texte de la table et de la colonne
  que le message cite (`CK_<table>_<colonne>_text`, le nom que SQLite donne) ; 8152 (même erreur sans la colonne) et
  8115 (dépassement d'un entier) comme un CHECK sans nom. Tous donnent `CONFLIT_INTEGRITE` (409) sur les deux moteurs,
  comme tout CHECK de la 2.0 (ADR 030 ; la 1.x faisait de ces erreurs, comme d'un CHECK, une erreur interne).
- Entiers : les versions, `structure_version`, `last_value`, `column_index`, `version_number` et `sequence` sont bornés
  à `int.MaxValue` (`BETWEEN min AND 2147483647` au lieu de `>= min`) : le verrou d'un projet à `int.MaxValue` est
  refusé en `CONFLIT_INTEGRITE` sur les deux moteurs et le projet reste lisible.
- Migrations : SQL Server `20261023174530_TextGuardsAndIntegerBounds` (neuvième, générée par `dotnet ef` : 69 CHECK de
  texte ajoutés, 19 CHECK d'entier remplacés) ; SQLite `20261023174259_TextGuardsAndIntegerBounds` (deuxième, `Up`
  générée ; SQLite n'ajoute un CHECK qu'en reconstruisant la table), puis `20261023174605_TextGuardsCheck` (troisième,
  écrite à la main), qui vérifie dans une transaction qu'aucune ligne ne viole une clé étrangère. Au round 2, EF Core
  coupait les clés étrangères autour de la reconstruction, hors de toute transaction, et la troisième migration
  enregistrait les deux ; un arrêt au milieu laissait un fichier refusé pour toujours et 22 tables `ef_temp_*` (porte P3
  round 3, Données B2). Depuis le round 3, toute reconstruction SQLite s'exécute dans la transaction de sa migration
  (`AtomicRebuildSqlGenerator` : clés étrangères actives, contrôle reporté à la validation par
  `PRAGMA defer_foreign_keys`, chaque table recréée sous son nom et remplie depuis sa copie), chaque migration s'inscrit
  elle-même, et le `Down` de la deuxième rend les instructions de la migration initiale et retire sa ligne d'historique
  (ADR 027, révision du round 3). Règle pour toute migration SQLite à venir qui reconstruit une table : aucune instruction
  hors transaction (test `AtomicMigrationTests.Every_sqlite_migration_runs_in_a_transaction`) ; les sources des deux
  migrations SQLite et de la neuvième SQL Server ont changé au round 3 (empreintes nouvelles, aucune n'a été publiée).
- Ordres, non alignés : un alignement demanderait une collation SQLite déclarée sur chaque colonne et enregistrée sur
  chaque connexion, qu'aucun autre outil ne saurait lire, ou de repasser SQL Server en UTF-8 (refusé plus haut). Aucun
  tri de la base ne porte sur un libellé (spec 4.3-3 : tri d'affichage en mémoire, avec la culture de l'utilisateur) ni
  sur un identifiant ; un ordre stable se prend sur une colonne qui a un sens (`created_at`, `sequence`, `sort_order`),
  puis en mémoire. Piège nommé pour la phase 4 : **ne jamais trier par libellé ni par identifiant dans une requête** ;
  un ordre .NET en mémoire (`Order()` de textes ou d'identifiants, unités UTF-16) suit SQL Server, pas SQLite.

### Conséquences

- Les deux bases acceptent et refusent les mêmes lignes écrites par l'application, et une recherche rend les mêmes
  lignes (ou est refusée de même). Les lignes écrites à la main aussi, sauf deux cas qu'aucun CHECK ne peut aligner,
  car SQL Server change la valeur avant tout contrôle et garde alors une ligne valide : des espaces au-delà de la
  largeur d'une colonne, qu'il coupe sans erreur (`A ` dans le `char(1)` du rôle RACI devient `A`), et un caractère
  hors de sa page de code dans un `varchar` (`pl-ł` devient `pl-l`) ; SQLite refuse les deux par la garde de texte.
- Longueur hors du plan de base : SQLite compte les points de code, SQL Server les unités UTF-16. Un texte écrit à la
  main dont les caractères hors du plan de base font dépasser la largeur en UTF-16 est refusé par SQL Server seul ; le
  domaine compte en UTF-16 (`string.Length`, `TextRules`), comme SQL Server.
- Une ligne écrite à la main avant cette migration avec un espace de fin, un texte trop long ou hors ASCII, ou un
  entier hors bornes, fait échouer la migration entière, qui ne valide rien : la donnée est à corriger avant.
- Le modèle a 69 CHECK de plus par moteur. `ModelGuards.Checks` garde les gardes écrites à la main ; les gardes de
  texte viennent de `ModelGuards.TextChecks(model)`.

### Vérification

`tests/Topogenia.Persistence.Tests/Collation/TextParityTests.cs` (deux moteurs) : login et code à espace de fin, langue
et code hors ASCII refusés avant toute écriture ; recherche à espace de fin refusée ; login, code de projet et code de
liste fermée à espace de fin, langue `pl-é` et couleur trop courte écrits à la main refusés par leur garde ; nom de 201
caractères et code de 7 lus comme le CHECK de leur garde de texte, `CONFLIT_INTEGRITE` ; les deux cas non alignables
épinglés (`A ` et `pl-ł` gardés convertis par SQL Server, refusés par SQLite) ; chaque colonne texte a sa garde sur les
deux moteurs. `Writing/ErrorTranslationTests.cs` : libellé trop long refusé par l'unité de travail en
`CONFLIT_INTEGRITE` nommant `CK_step_label_text` ; verrou d'un projet à `int.MaxValue` refusé en `CONFLIT_INTEGRITE`,
projet relisible. `Model/CheckConstraintTests` : versions et compteurs à `int.MaxValue` acceptés.
`Collation/OrderParityTests.cs` : ordre hors du plan de base et ordre des identifiants épinglés par moteur ; aucun code
de la persistance ne trie par un identifiant.

Avant la correction (même suite sur le code et les migrations précédents) : 25 échecs, dont tous les cas d'espace de
fin, de longueur, d'ASCII et de verrou à `int.MaxValue` sur les deux moteurs. Mutations, toutes rouges : sans le
convertisseur des textes (6 échecs : écriture, recherche et langue sur les deux moteurs), 2628 lu sans le nom de la
garde (3 échecs SQL Server), 8115 non classé (1 échec SQL Server), garde du login SQLite sans le contrôle de l'espace de
fin (1 échec), garde du code de projet SQL Server sans ce contrôle (1 échec), `structure_version` SQLite non bornée
(1 échec). Une reconstruction de table SQLite crée la table d'après le modèle cible de la migration (`.Designer.cs`), pas
d'après le SQL des `AddCheckConstraint` : la mutation SQLite porte sur ce modèle cible.

## Complément (porte P3, round 3) : noms de l'annuaire, recherches par motif et caractère NUL

### Contexte

Constats fidélité MAJ-1 et m4, données M1 et m3 du round 3.

- Nom et e-mail de l'annuaire : `UserRules.NewUser` rognait le texte, puis le coupait à 128 et 256 caractères. La coupe
  pouvait finir sur une espace (127 caractères, une espace, la suite) : le convertisseur refusait l'utilisateur, jamais
  créé, et chaque requête de cette personne finissait en erreur interne. La 1.x enregistrait ce texte tel quel, sans
  rognage (`Utilisateurs.cs`, `Tronquer`). Le round 2 affirmait à tort que le domaine ne produit jamais ces valeurs.
- Migration 1.x (phase 5) : un texte 1.x terminé par une espace serait refusé de même par `--migrate-v1`.
- Recherches par motif (sondes du contrôleur, deux moteurs) : `StartsWith`, `EndsWith` et `EF.Functions.Like` sont
  traduits par `LIKE`, que SQLite compare sans la casse des lettres ASCII et SQL Server avec (`BIN2`) ;
  `Contains("Valider ")` était refusé en SQLite (`instr`, dont le texte cherché recevait le convertisseur de la
  colonne) et rendait la ligne en SQL Server (`LIKE '%Valider %'`, motif qui ne finit pas par une espace) ; `ToUpper`
  d'un texte non ASCII rendait 0 ligne en SQLite (`upper` ne change que l'ASCII), 1 en SQL Server.
- NUL : `length`, `rtrim` comparé par `length`, `replace` et `GLOB` de SQLite s'arrêtent au premier caractère NUL
  (vérifié : `length('ab' || char(0) || 'cd ')` vaut 2). Un texte de 252 caractères commençant par un NUL passait la
  garde de largeur d'un `nvarchar(200)` en SQLite ; SQL Server le refuse (2628).

### Décision

- Domaine : `UserRules.NewUser` coupe comme la 1.x, puis rogne à nouveau (`TrimEnd`), et ne coupe jamais entre les deux
  moitiés d'une paire de substitution (SQLite écrirait le caractère de remplacement, la relecture différerait) : le nom
  ou l'e-mail perd alors un caractère de plus. Le texte étant rogné avant la coupe, son premier caractère reste : il
  n'est jamais vide. Relecture du reste du domaine : tout texte saisi passe par `TextRules` (rogné, refusé s'il est
  trop long, jamais coupé) ; les codes lisibles (`ReadableCode`, 18 caractères au plus pour une largeur de 20), les noms
  de cartes repris d'un libellé (150 pour une largeur de 200) ou d'un nom de projet (200 pour 200), les couloirs et
  traductions copiés gardent un texte déjà contrôlé ; aucun autre endroit ne coupe ni ne compose un texte stocké.
- Phase 5 (P5-T05, `docs/plan.md`) : `app_user.display_name` et `app_user.email` de la 1.x, les seules colonnes texte
  de la 1.x qui peuvent finir par une espace (lecture du code 1.x : tout autre texte est rogné par `Textes.Obligatoire`
  ou `Facultatif`, par le lecteur `.pmap`, ou vient d'une liste fermée ou d'un code généré), sont rognés comme le fait
  `UserRules.NewUser` ; chaque ligne rognée est une anomalie du rapport de migration (S-7.6-11). Un NUL dans ces deux
  colonnes (l'annuaire n'en donne pas, mais la 1.x ne le refusait pas) est retiré et signalé de même.
- Recherches, dans l'esprit du complément du round 2 (les deux bases rendent les mêmes lignes, ou refusent de même) :
  - `PRAGMA case_sensitive_like = ON` sur chaque connexion SQLite (`SqlitePragmas.Settings`, connexion en lecture
    seule comprise). Le pragma est déprécié dans les versions récentes de SQLite mais reste compilé dans le SQLite embarqué
    (`e_sqlite3` de SQLitePCLRaw 2.1.13) : le test le prouve. Aucun CHECK ni index SQLite n'utilise `LIKE` (un CHECK
    qui en dépendrait changerait de sens selon la connexion) ; seul le catalogue du schéma l'utilise, sur les noms en
    minuscules réservés par SQLite.
  - `Contains` d'un texte stocké en SQLite : `SqliteTextSearch` (plugin de traduction) écrit `instr(colonne, texte) > 0`
    avec le convertisseur `SearchedTextConverter`, qui refuse le NUL et, pour une colonne `varchar`, un texte hors ASCII
    imprimable, mais garde les espaces de fin. Choix : **accepté pareil**. Une recherche n'est pas un texte stocké ; les
    deux moteurs comparent ses espaces de fin (`instr` octet par octet, `LIKE` sur un `nvarchar`, où les espaces de fin
    du motif comptent).
  - Bilan, identique sur les deux moteurs : `==` et `IN` refusent un texte à espace de fin (complément du round 2) ;
    `Contains` et `StartsWith` l'acceptent et trouvent les mêmes lignes ; `EndsWith` et `EF.Functions.Like` le
    refusent (le paramètre traduit finit par une espace et reçoit le convertisseur de la colonne, sur les deux
    moteurs) : aucun texte stocké ne finit par une espace, `EndsWith` n'en trouverait aucun. NUL et hors ASCII sur une
    colonne `varchar` refusés par toutes ces recherches.
  - `TextSearchParity` (`Model/TextSearchParity.cs`, enregistré par `UseTextSearchParity` dans les options des deux
    moteurs) refuse à la compilation de la requête, sur les deux moteurs, `ToUpper`, `ToLower`, `ToUpperInvariant`,
    `ToLowerInvariant`, `EF.Functions.Like` (classes `[a-z]` de SQL Server inconnues de SQLite, espaces de fin ignorées
    sur un `varchar` en SQL Server), `IndexOf`, `Substring` et `Length` (points de code et arrêt au NUL en SQLite, unités
    UTF-16 en SQL Server) : `InvalidOperationException` nommant la fonction et l'ADR, un défaut de l'appelant. Un
    plugin plutôt qu'une règle d'architecture : il voit la requête traduite, pas le texte des sources, et ne se trompe
    pas sur un `Contains` de liste ou un `Length` en mémoire.
- NUL : le convertisseur commun refuse un texte qui contient le caractère NUL, avant toute écriture et dans toute
  recherche, sur les deux moteurs (`ArgumentException`, comme une espace de fin). Aucune migration : le domaine refuse
  déjà ce caractère dans un texte saisi (`TextRules`), et aucune expression SQLite ne compte juste les caractères
  au-delà d'un NUL.

### Conséquences

- Un utilisateur dont l'annuaire donne un nom ou un e-mail plus long que sa colonne est toujours créé ; son nom stocké
  peut avoir un ou deux caractères de moins que la largeur (espace ou paire coupée).
- Limite nommée : une ligne écrite **à la main** en SQLite avec un NUL échappe aux gardes de texte au-delà du NUL
  (largeur, espace de fin, ASCII) ; SQL Server la contrôle en entier. Pour l'aligner, il faudrait refuser le NUL par un
  CHECK sur les deux moteurs, donc une nouvelle migration sur chacun et une reconstruction de toutes les tables SQLite
  pour un caractère que ni l'application ni la 1.x saisie ne produisent ; écart accepté (arbitrage du chef de projet).
- Pièges pour la phase 4 :
  - `StartsWith`, `EndsWith` (deux moteurs) et `Contains` (SQL Server) sont traduits par le visiteur interne du
    fournisseur, avant tout plugin de traduction (vérifié en EF Core 10.0.13) : leur parité repose sur le pragma, sur
    le convertisseur et sur les tests de `SearchParityTests`. Une montée de version d'EF Core se vérifie par ces tests.
  - Seuls `==`, `IN`, `StartsWith`, `EndsWith` et `Contains` sont prouvés alignés. Toute autre fonction de texte dans
    une requête (`Trim`, `Replace`, `string.Compare`, concaténation, `IsNullOrWhiteSpace`) n'est ni refusée ni prouvée :
    la requête qui en a besoin ajoute son test sur les deux moteurs, ou se fait en mémoire.
  - Une recherche sans la casse se fait sur une colonne normalisée (`login_normalized`) ou en mémoire (spec 4.3-3),
    jamais par `ToUpper` dans la requête (refusé).

### Vérification

`tests/Topogenia.Domain.Tests/Projects/UserRulesTests.cs` : nom de 127 caractères, une espace, puis la suite ; e-mail de
255 caractères, espaces, puis la suite ; coupe au milieu d'une paire de substitution.
`tests/Topogenia.Persistence.Tests/Collation/SearchParityTests.cs` (deux moteurs) : utilisateur créé par
`UserRules.NewUser` avec ce nom et cet e-mail, écrit et relu à l'identique ; `StartsWith` et `EndsWith` sensibles à la
casse, connexion SQLite en lecture seule comprise ; `Contains` et `StartsWith` à espace de fin acceptés et mêmes
lignes ; `EndsWith` à espace de fin, NUL, hors ASCII sur `varchar` refusés ; les sept fonctions refusées ; texte à NUL
refusé avant toute écriture.

Avant la correction (code et réglages précédents) : l'utilisateur coupé sur une espace refusé sur les deux moteurs,
toutes les recherches sensibles à la casse, le `Contains` à espace de fin, les refus et le NUL rouges en SQLite, les
refus et le NUL rouges en SQL Server. Mutations, toutes rouges : sans le pragma (2 échecs SQLite, dont la connexion en
lecture seule), sans `UseTextSearchParity` en SQLite (2 échecs), sans `SqliteTextSearch` (1 échec), sans
`UseTextSearchParity` en SQL Server (1 échec), sans le refus du NUL (4 échecs, deux moteurs), `Contains` SQLite avec le
convertisseur de la colonne (1 échec), sans le refus hors ASCII de la recherche (1 échec), `ToUpper`, `Like` ou
`Length` admis (2 échecs chacun, deux moteurs), coupe sans rognage (2 échecs persistance, 1 échec domaine), coupe au
milieu d'une paire (1 échec domaine).
