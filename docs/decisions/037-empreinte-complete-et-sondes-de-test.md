<!-- 037-empreinte-complete-et-sondes-de-test.md -->
<!-- Ce que l'empreinte SQL Server décrit de la base entière, les sondes de test des fournisseurs, les migrations écrites à la main et les règles -->

# 037 - Empreinte complète de la base SQL Server, sondes de test et migrations écrites à la main

Statut : acceptée (date 23/10/2026), révisée (23/10/2026, porte P3 round 2 : points 1, 6 et Conséquences, voir la révision)

## Contexte

La porte de contrôle de la phase 3 (round 1) a montré que l'empreinte du schéma SQL Server (ADR 027, 032) ne
décrivait que les tables, vues, contraintes et droits des deux schémas et des deux rôles. Essais faits sur l'instance
locale, tous acceptés au démarrage (`UpToDate`) alors qu'un membre de `topogenia_bi` lisait `app_user` :
`CREATE FUNCTION bi.leak() RETURNS TABLE ... FROM topogenia.app_user`, `CREATE SYNONYM bi.users FOR
topogenia.app_user`, `GRANT SELECT TO public`, compte de rapport mis dans `db_datareader` (l'ADR 032 le disait
« refusé au démarrage »), déclencheur sur `app_user`, procédure `topogenia.purge_trash`. En SQLite, `name NOT LIKE
'sqlite_%'` laissait de côté tout nom `sqlite` suivi d'un caractère quelconque (`_` est un joker), et `tbl_name NOT IN
(historiques)` tout objet attaché aux tables d'EF Core.

Les sondes `SqlServerAccessProbe` et `SqlServerOptionsProbe` (code SQL des tests, gardé dans le fournisseur par la
spec 3.2) n'avaient aucune règle d'usage ; `InternalsVisibleTo` ouvrait tout l'assemblage. Les migrations écrites à la
main étaient exclues de toutes les règles de forme comme du code généré.

Contrainte de visibilité, mesurée sur l'instance : le contrôle du démarrage tourne sous le compte de l'application,
qui n'a `VIEW DEFINITION` que sur les schémas `topogenia`, `bi` et le rôle `topogenia_bi` (ADR 032, point 3). SQL
Server lui cache les utilisateurs (donc leurs noms dans `sys.database_role_members`, mais pas les lignes), les
déclencheurs de base, les objets hors des deux schémas sur lesquels il n'a aucun droit, et les droits donnés à un
utilisateur. Avec `VIEW DEFINITION` au niveau de la base, il voit tout (essai fait).

## Décisions

| Point | Options | Choix | Raison |
|---|---|---|---|
| 1. Objets décrits | (a) les objets de programmation des deux schémas ; (b) tout objet de `sys.objects` que Microsoft ne livre pas (`is_ms_shipped = 0`), quel que soit son schéma, hors ce qui est déjà décrit | (b) : ligne `X\|schéma.objet\|type` ; exclus : tables et vues des deux schémas (lignes `T`, `C`, `V`), contraintes d'une table (`PK`, `UQ`, `F`, `C`, `D`, déjà décrites ou propres à l'historique d'EF Core) | Une base des migrations n'en contient aucun (D04 : ni déclencheur, ni procédure, ni fonction) ; un synonyme ou une vue de `dbo` lit les tables par chaînage de propriété comme une vue `bi`. Les déclencheurs sur `__EFMigrationsHistory` sont vus aussi |
| 2. Déclencheurs de base | (a) ignorés ; (b) décrits | (b) : ligne `TR\|nom\|type` (`sys.triggers`, niveau base) | Un déclencheur DDL s'exécute pendant `--migrate` sous le compte `db_owner` |
| 3. Types | (a) ignorés ; (b) décrits | (b) : ligne `Y\|schéma.type` (`sys.types`, `is_user_defined = 1`) | Un type n'est pas un objet de `sys.objects` ; aucun n'est créé par les migrations |
| 4. Droits décrits | (a) ceux des deux rôles et des deux schémas ; (b) tout droit de la base | (b), sauf `CONNECT` d'un utilisateur (accordé par `CREATE USER`, propre à chaque installation ; celui de `guest` reste décrit) et les droits sur les objets système (`public` en tient plusieurs centaines) ; colonne de plus pour la cible d'une autre classe (clé, certificat, type...), placée avant le droit | `GRANT SELECT TO public` ouvre toutes les tables ; `IMPERSONATE` sur un utilisateur, un droit de base donné à un compte de rapport aussi. Les deux droits que `public` tient par défaut sur les clés de chiffrement des colonnes (`VIEW ANY COLUMN ... KEY DEFINITION`) entrent dans la description : ils viennent de `model`, identiques dans chaque base de l'instance |
| 5. Rôles fixes | (a) les membres de tous les rôles fixes ; (b) le nombre de membres des rôles qui ouvrent les données ; (c) aucun | (b) : ligne `MF\|rôle\|nombre` pour `db_datareader`, `db_datawriter`, `db_ddladmin` (crée des objets qui lisent les tables par chaînage), `db_securityadmin` (donne des droits, change les rôles), `db_backupoperator` (copie la base) | `db_owner` est le rôle des administrateurs, légitime (ADR 032) ; `db_accessadmin` ajoute des utilisateurs qui ne tiennent que ce que tient `public`, décrit ; les deux rôles `db_deny*` ne font que retirer des droits. Compter plutôt que nommer : le compte de l'application voit les lignes d'appartenance mais pas les noms des utilisateurs, et doit lire la même ligne qu'un administrateur |
| 6. Ce que voit le compte de l'application (périmé, voir la révision) | (a) lui donner `VIEW DEFINITION` sur la base ; (b) garder l'ADR 032 et documenter | (b) dans cette tâche, puis (a) par la migration `ApplicationViewDefinition` ; (a) proposé au chef de projet (migration du fournisseur SQL Server, hors de cette mission) | Sous le compte de l'application, sont refusés au démarrage : fonction, synonyme, déclencheur de table, procédure, séquence, type des deux schémas, droit donné à `public`, compte mis dans un rôle fixe de données. Ne sont vus qu'à `--migrate` (compte `db_owner`) : déclencheur de base, objet hors des deux schémas sans droit visible, droit donné à un utilisateur |
| 7. SQLite | - | `name NOT LIKE 'sqlite\_%' ESCAPE '\'` ; seules les deux tables d'EF Core sont exclues (`NOT (type = 'table' AND name IN (...))`), jamais ce qui s'y attache | Seuls les noms que SQLite se réserve sont internes ; un index ou un déclencheur sur une table d'EF Core est un changement |
| 8. Sondes | (a) SQL des tests dans les tests ; (b) classes internes des fournisseurs | (b), avec des règles : une sonde est une classe statique interne d'un fournisseur, dans un fichier `*Probe.cs` qui porte son nom et ne déclare aucun autre type à son niveau (ses types utiles y sont imbriqués : la dispense porte sur le fichier, la restriction d'usage sur le nom du type, et un second type du fichier aurait l'une sans l'autre) ; un fournisseur ouvre ses internes au seul projet de tests de persistance, par un `InternalsVisibleTo` au plus, écrit dans le fichier d'une de ses sondes ; aucun type de `src` hors des sondes ne dépend d'une sonde | Spec 3.2 (pas de SQL hors des fournisseurs, tests compris). `SqlServerAccessProbe` crée en base de test les objets que le démarrage doit refuser : c'est la seule exception à la règle D04 (`No_trigger_procedure_or_function_anywhere_including_migrations`), bornée aux fichiers des sondes |
| 9. Migrations et règles | (a) dossier `Migrations/` entièrement exclu ; (b) seuls les fichiers écrits en entier par un outil exclus | (b) : exclus, dans les migrations d'un fournisseur seulement, les `*.Designer.cs` et l'instantané du modèle qui commencent par l'en-tête de l'outil (`// <auto-generated`), et une migration sans SQL écrit à la main (`migrationBuilder.Sql`) ; aucun `*.g.cs` ni `*.Designer.cs` ailleurs n'est dispensé (porte P3, round 3) ; une migration avec du SQL écrit à la main suit les règles de forme (en-tête de deux lignes, marqueurs, commentaires en anglais) ; toute migration est dispensée des limites de taille (fichier, méthode) et de la règle du nom du produit | Une migration mêle ce que `dotnet ef` génère du modèle (une méthode par sens, aussi longue que le changement) et du SQL écrit à la main ; elle ne change jamais une fois publiée (spec 4.4) et fige les noms de la base (ADR 013, ADR 032 point 7) |

## Règle pour les bases déjà migrées

La description gagne des lignes que toute base porte déjà (les deux droits de `public`) et une colonne dans chaque
ligne `P`. Aucune base n'a été migrée par une version publiée : l'effet est nul, comme pour l'ADR 032. Après la
première publication, toute évolution de la description devra soit garder la description d'un état existant, soit
venir avec une migration qui réenregistre l'empreinte (ADR 027, Conséquences), soit n'entrer dans la description
qu'à partir d'une migration, comme la ligne des options (ADR 031). Une base de développement migrée avant ce
changement (`Topogenia_Dev`) est refusée `SCHEMA_MODIFIE` : on la recrée.

## Conséquences

- Une base où un administrateur a ajouté un objet, un droit ou un membre de rôle fixe de données est refusée au
  démarrage (`SCHEMA_MODIFIE`) tant que le changement n'est pas retiré. Les membres de `db_owner` et des deux rôles
  de l'application restent libres.
- Un outil de supervision qui a besoin d'un droit dans la base (`VIEW DATABASE STATE`) le reçoit au niveau du
  serveur (`VIEW SERVER STATE`), pas dans la base de l'application.
- Le schéma `dbo` ne contient rien de l'application ni d'autre : la base est dédiée.
- (Périmé, voir la révision.) Ce que le compte de l'application ne voit pas n'est refusé qu'à `--migrate` (point 6) ; la proposition (a) le
  ferait refuser à chaque démarrage, sans aucun accès aux données (`VIEW DEFINITION` ne lit aucune ligne).
- Les migrations écrites à la main reçoivent un en-tête de deux lignes ; la migration n'étant pas publiée, son
  empreinte de source change sans effet.

## Vérification

- `tests/Topogenia.Persistence.Tests/Schema/CatalogChangeTests.cs` (SQL Server) : chaque changement fait à la main
  (fonction et synonyme `bi`, déclencheur de table et de l'historique d'EF Core, procédure, séquence, type,
  déclencheur de base, `GRANT SELECT TO public`, synonyme de `dbo` donné aux rapports, compte de rapport dans chacun
  des cinq rôles fixes, droit de base donné à un compte) refusé `SCHEMA_MODIFIE`, sous le compte de l'application
  quand il le voit, puis retiré et la base de nouveau acceptée ; la fuite réelle (le rôle des rapports lit les
  utilisateurs par le synonyme ; un compte de `db_datareader` lit `app_user`) ; un administrateur dans `db_owner` et
  un membre de `topogenia_app` acceptés.
- `tests/Topogenia.Persistence.Tests/Schema/SqliteCatalogChangeTests.cs` : index nommés `sqlitex_probe`,
  `SQLITEXPROBE`, index sur `__EFMigrationsHistory` et `__EFMigrationsLock` refusés, puis retirés et acceptés.
- `tests/Topogenia.Architecture.Tests/ProbeRulesTests.cs` : sondes internes statiques des fournisseurs ; aucun
  type de `src` n'en dépend (échantillon fautif `Probes/SampleProbe.cs`) ; `InternalsVisibleTo` seulement dans une
  sonde, vers les tests de persistance, un par fournisseur au plus (échantillons) ; un fichier `*Probe.cs` ne déclare
  que sa sonde (`A_probe_file_holds_only_the_probe_named_after_it`, échantillons `A_probe_file_with_another_type_is_caught` :
  un `ReportSetup` à côté de la sonde est refusé).
- `tests/Topogenia.Architecture.Tests/SourceRulesTests.cs` : `Only_a_file_written_by_a_tool_where_a_tool_writes_is_generated`
  (dispense des fichiers générés, point 9).
- `SourceRulesTests.Only_the_files_written_whole_by_a_tool_are_spared_the_rules`,
  `A_migration_with_sql_written_by_hand_is_recognised`.
- Mutations : ligne `X` retirée (fonction, synonyme, déclencheur, procédure, séquence acceptés) ; ligne `MF` retirée
  (`db_datareader` accepté) ; filtre `CONNECT` retiré (un utilisateur ajouté change l'empreinte) ; `LIKE` sans
  échappement (`sqlitex_probe` accepté) ; un type de production qui lit une sonde (règle rouge).

## Révision (23/10/2026, porte P3 round 2) : nom de l'historique, propriétaires, schémas, options, objets marqués

Contexte :

- Le point 6 et la quatrième conséquence sont périmés : la migration `20261023124304_ApplicationViewDefinition` donne
  `VIEW DEFINITION` sur la base à `topogenia_app` (ADR 032) ; le contrôle du démarrage, sous ce compte, voit tout ce
  que voit un administrateur. Les cas de `CatalogChangeTests` dits « vus seulement à `--migrate` » (déclencheur de
  base, synonyme de `dbo`, droit de base donné à un compte) sont désormais refusés aussi sous le compte de
  l'application, et le test l'exige.
- La porte P3 (round 2) a trouvé quatre trous de l'empreinte, chacun accepté `UpToDate` : la table d'historique était
  écartée par `t.name <> '__EFMigrationsHistory'` dans les deux schémas, pour tout type et selon la collation de la base
  (insensible à la casse) : une vue `bi.__efmigrationshistory` qui lit `app_user` passait, lue par le rôle des
  rapports ; le propriétaire explicite d'un objet (`ALTER AUTHORIZATION ON OBJECT::topogenia.app_user TO topogenia_bi`
  donne au rôle des rapports toute la table) n'était pas décrit ; seuls les schémas `topogenia` et `bi` étaient décrits
  (un schéma nouveau possédé par le rôle des rapports passait) ; un objet marqué comme livré par Microsoft
  (`sp_MS_marksystemobject`, `is_ms_shipped = 1`) était écarté avec ses droits ; `TRUSTWORTHY` et `DB_CHAINING` n'étaient
  pas lus.

Décision (lignes ajoutées seulement quand le changement existe : une base des migrations garde sa description) :

- Seule la table (`U`) `topogenia.__EFMigrationsHistory` est écartée, nom et schéma comparés en
  `Latin1_General_BIN2` ; toute autre table ou vue de ce nom, quelle que soit sa casse, est décrite.
- Ligne `W|schéma.objet|propriétaire` pour tout objet de `sys.objects` dont `principal_id` est posé (tables, vues,
  autres objets) ; le numéro du principal, que le compte de l'application lit comme un administrateur.
- Ligne `S` pour tout schéma hors système (`schema_id` de 5 à 16383), avec son propriétaire (vide s'il n'est pas
  visible) : un schéma nouveau et le propriétaire d'un schéma changé sont vus.
- Ligne `X` : un objet marqué livré compte hors des schémas `sys` et `INFORMATION_SCHEMA`, sauf les files d'attente
  (`SQ`) ; une base neuve n'y tient que les trois files du service broker de `dbo` (constat sur l'instance locale,
  SQL Server 2022). Ligne `P` : seuls les droits sur les objets livrés de `sys` et `INFORMATION_SCHEMA` sont écartés.
- Ligne `B|TRUSTWORTHY`, `B|DB_CHAINING` quand l'option est posée (`sys.databases` de la base courante, lisible par
  tout utilisateur de la base).

Limites nommées :

- `TRUSTWORTHY` et `DB_CHAINING` ne sont éprouvés par aucun test automatique : la règle A-5-12
  (`SourceRulesTests.The_application_needs_neither_the_system_database_nor_the_server_procedures`) refuse
  `ALTER DATABASE ... SET TRUSTWORTHY` dans tout `src`, sondes comprises, et les tests n'écrivent pas de SQL (spec 3.2).
  Preuve faite à la main sur une base jetable `Topogenia_*` de l'instance locale (requête de la ligne `B` : aucune ligne
  option retirée, une ligne `B|TRUSTWORTHY` option posée), base supprimée ensuite.
- Une mise à jour de l'instance qui ajouterait un objet livré hors de `sys` dans les bases existantes ferait refuser
  ces bases (`SCHEMA_MODIFIE`) : à revoir si cela se produit ; aucune version connue ne le fait hors des files `SQ`.
- `sp_MS_marksystemobject` (procédure non documentée) demande `sysadmin` : le test correspondant tourne sur l'instance
  locale des tests, dont le compte l'est.

Vérification : `Schema.CatalogChangeTests` : vue `bi.__efmigrationshistory` lue par les rapports (fuite réelle) puis
refusée sous l'administrateur et sous le compte de l'application ; `app_user` donnée au rôle des rapports comme
propriétaire : SELECT tenu par un membre des rapports (fuite réelle), refusée, puis plus aucun droit une fois rendue ;
théorie étendue à la table `bi.__EFMigrationsHistory`, à la vue `bi.project` donnée au rôle des rapports, au schéma
possédé par ce rôle, à une fonction de `dbo` sur les utilisateurs marquée livrée (seule, et donnée aux rapports ; une
fonction et non une vue, aucune vue n'existant hors de `bi`, règle `ExtraRulesTests`), tous refusés sous le compte de
l'application. Mutations (chacune constatée rouge, code remis) ancienne exclusion `t.name <> @excluded` (3 rouges : fuite par la vue, vue et table nommées comme l'historique) ; ligne `W` retirée (3 rouges : fuite par la propriété, `app_user` et `bi.project` données aux rapports) ; ligne `S` limitée aux deux schémas (1 rouge : schéma possédé par les rapports) ; filtre `is_ms_shipped` d'origine dans `X` (1 rouge : fonction marquée seule ; la fonction donnée aux rapports reste refusée par sa ligne `P`) ; filtre d'origine dans `X` et `P` (2 rouges : les deux fonctions marquées).

## Révision (24/10/2026, porte P3 round 3) : rôles d'application et rôles imbriqués, D04 prouvé en SQL Server

- Les membres des deux rôles qui ne sont pas des utilisateurs (rôle de base `R`, rôle d'application `A`, ligne
  `MN|rôle|membre|type`) et tout rôle d'application de la base (ligne `AR|rôle`) entrent dans la description (ADR 032,
  révision du round 3) ; seuls les utilisateurs membres des deux rôles et les membres de `db_owner` restent libres. Le
  compte de l'application, qui tient `VIEW DEFINITION` sur la base, lit ces lignes comme un administrateur (vérifié par
  `CatalogChangeTests`).
- La sonde `SqlServerAccessProbe` crée le rôle d'application `probe_application` (mot de passe constant de la sonde,
  base de test seulement) et le rôle `probe_nested`, et prend ou rend le rôle d'application sur une session
  (`sp_setapprole` puis `sp_unsetapprole` avec son cookie, appelés comme procédures). `CatalogChange` et `ReportViewRows`
  sont imbriqués dans `SqlServerAccessProbe`, `DatabaseOptions` dans `SqlServerOptionsProbe`
  (`ProbeRulesTests.A_probe_file_holds_only_the_probe_named_after_it`).
- D04 en SQL Server (architecture MAJ-1) :
  `Guards.DatabaseShapeTests.The_database_keeps_structural_guards_only_without_trigger_default_value_nor_cascade` exige
  désormais, dans la base migrée, l'absence de toute ligne `X|` (déclencheur de table, procédure, fonction, synonyme,
  séquence...), `TR|` (déclencheur de base) et `Y|` (type). La règle des sources
  (`No_trigger_procedure_or_function_anywhere_including_migrations`) ne voit pas un mot coupé (`"CREATE TRIG" + "GER"`) :
  le test sur la base le voit. Preuve sur une copie du dépôt dans le scratchpad : une migration SQL Server ajoutée
  (un déclencheur sur `app_user`, une fonction, une procédure) rend le test rouge (`X|topogenia.mutation_trigger|TR`,
  `X|topogenia.mutation_function|FN`, `X|topogenia.mutation_procedure|P`) ; la copie a été supprimée.
