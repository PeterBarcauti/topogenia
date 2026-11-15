<!-- 032-roles-et-vues-bi.md -->
<!-- Rôles topogenia_app et topogenia_bi, schéma bi et vues de rapport du mode Serveur, et leur place dans l'empreinte du schéma -->

# 032 - Rôles de base et vues `bi` du mode Serveur

Statut : acceptée (date 23/10/2026)

## Contexte

La spec 4.4 demande une migration propre à SQL Server qui crée les rôles `topogenia_app` et `topogenia_bi`
et les vues `bi`, « avec le même contenu fonctionnel » que le script 1.x `db/004_acces.sql` (vues
modifiées par `db/006_corbeille.sql`). La spec 5.3 fixe les droits : le compte de l'application lit et
écrit les données, sans droit sur le schéma ni sur les historiques ; les rapports Power BI ne voient
que les vues `bi`. D04 réserve ces vues au mode Serveur. Plusieurs points ne sont pas tranchés par la
spec :

1. les vues existent-elles aussi en SQLite (mode Poste) ;
2. les rôles, droits et vues entrent-ils dans l'empreinte du schéma (ADR 027) ;
3. le contrôle du démarrage tourne sous le compte de l'application : que doit-il voir du catalogue ;
4. quels historiques de données gardent le `DENY` de la 1.x, alors que la purge passe en C# ;
5. où vit le SQL dont les tests ont besoin pour agir avec les droits d'un rôle (spec 3.2).

## Décisions

| Point | Options | Choix | Raison |
|---|---|---|---|
| 1. Vues en SQLite | (a) mêmes vues sans schéma ; (b) aucune vue | (b) : aucune migration SQLite, le jeu SQLite reste à sa migration initiale | La 1.x n'avait aucun mode fichier (base SQL Server seulement, création en `Local` par `MiseAJourBase`) ; D04 et l'annexe A-5 (`db/004`, « sans objet en SQLite ») ; en mode Poste, il n'y a ni compte de rapport ni séparation des droits |
| 2. Empreinte | (a) tables seules ; (b) tables, vues, propriétaires des schémas, rôles, droits des rôles, appartenance des rôles à d'autres rôles | (b) | Un `GRANT` ajouté à la main (un rapport qui lit une table, un rôle de rapport mis dans `db_datareader`) est une fuite de données : il doit être refusé comme une colonne ajoutée (`SCHEMA_MODIFIE`). Les **membres** des rôles (comptes de l'application, des rapports) ne sont pas décrits : ils appartiennent à chaque installation, l'administrateur les ajoute sans changer l'empreinte |
| 3. Catalogue vu par l'application | (a) contrôle du démarrage sous un compte d'administration ; (b) `VIEW DEFINITION` sur les schémas `topogenia`, `bi` et sur le rôle `topogenia_bi`, donné à `topogenia_app` ; (c) `VIEW DEFINITION` sur toute la base | (b) | Sans ce droit, SQL Server cache au compte de l'application la définition des CHECK et des vues et les droits de l'autre rôle (essai sur l'instance locale) : l'empreinte lue différerait de celle enregistrée et une base saine serait refusée. `VIEW DEFINITION` ne donne accès à aucune donnée ; (b) est le plus étroit qui suffise |
| 4. Historiques de données | (a) `DENY UPDATE, DELETE` sur `step_sheet_version` et `DENY UPDATE` sur `pain_point_status_history` comme la 1.x ; (b) `DENY UPDATE` seulement, sur ces deux tables et `audit_event` | (b) | La purge de la corbeille est écrite en C# sous le compte de l'application (spec 4.1) : elle efface des versions de fiche, que la procédure 1.x effaçait par chaînage de propriété. Aucun historique n'est jamais réécrit (ajout seul, A-2.12-03) ; le journal d'audit, nouveau en 2.0, suit la même règle (spec 5.1, 5.5 : il ne s'efface que par la conservation paramétrable) |
| 5. SQL des tests | (a) chaînes SQL dans les tests ; (b) classe interne du fournisseur SQL Server, visible du seul projet de tests | (b) : `SqlServerAccessProbe` (utilisateur sans connexion, `EXECUTE AS USER`, droits effectifs par `HAS_PERMS_BY_NAME`, lecture d'une vue, `GRANT`/`REVOKE` d'un administrateur) | La spec 3.2 interdit le SQL hors des fournisseurs, tests compris (`SourceRulesTests.No_raw_sql_outside_the_two_providers`) ; `InternalsVisibleTo` vise `TechnicalNames.PersistenceTests`, nom technique figé (ADR 013 révisée) ; aucun login de serveur n'est créé |
| 6. Propriétaire des schémas | (a) laisser celui qui a lancé la migration ; (b) `dbo` pour `topogenia` et `bi` | (b) : `ALTER AUTHORIZATION ON SCHEMA::topogenia TO dbo` puis `CREATE SCHEMA bi AUTHORIZATION dbo` | Les vues lisent les tables par chaînage de propriété : les deux schémas doivent avoir le même propriétaire, quel que soit le compte des migrations (la 1.x écrivait `AUTHORIZATION dbo`) |
| 7. Noms | (a) écrits dans la migration ; (b) calculés à l'exécution | (a) dans la migration, (b) dans le code (`SqlServerAccess.ApplicationRole`, `ReportRole`, dérivés de `SqlServerEngine.Schema`) | ADR 013 : une migration fige un état de la base ; l'empreinte de sa source ne verrait pas un SQL construit ailleurs |
| 8. Contenu des vues | (a) recopier la 1.x ; (b) l'enrichir (classification, traductions) | (a) : mêmes vues, mêmes colonnes, mêmes filtres, schéma `topogenia` au lieu de `ariane` | Spec 4.4 ; ne rien exposer que la 1.x n'exposait pas. Écarts de type seulement (§ 10 de `docs/modele-donnees.md`). Une seule colonne des vues porte une donnée personnelle au sens de l'ADR 035 : `bi.step.assignee_user_id`, le rattachement d'une personne à une étape. Gardée, parce que la 1.x l'exposait dans la même vue (`db/004_acces.sql`, `bi.step`) et que la spec 4.4 demande le même contenu fonctionnel ; c'est un identifiant opaque (GUID) : le rôle des rapports ne lit ni `app_user` ni aucune table (`RoleTests`), aucune vue ne donne le nom affiché, le login ni l'e-mail, et la colonne a le même contenu qu'en 1.x. Un rapport ne peut donc que compter ou regrouper les étapes par personne, sans la nommer ; la retirer serait un écart à la 1.x que la spec ne demande pas (question au porteur du projet, `docs/questions.md`) |

## Conséquences

- La description du catalogue d'une base à la migration initiale gagne la ligne du propriétaire du
  schéma `topogenia` : aucune base n'a été migrée par une version publiée, l'effet est nul. Après la
  première publication, toute évolution du catalogue devra garder la description d'un état existant ou
  venir avec une migration qui réenregistre l'empreinte (ADR 027).
- Le retour arrière de la migration (`Down`) exige que les rôles n'aient plus de membre ; le
  propriétaire du schéma `topogenia` reste `dbo`.
- L'administrateur crée l'utilisateur du compte du pool IIS, l'ajoute à `topogenia_app`, et les comptes
  des rapports à `topogenia_bi` (jamais à `db_datareader`, refusé au démarrage) ; le compte des
  migrations est distinct (spec 5.3, S-5.3-04).
- `SqlServerAccessProbe` est livré dans l'assemblage du fournisseur mais interne : aucun code de
  production ne l'appelle.

## Vérification

`tests/Topogenia.Persistence.Tests/Bi/` : `RoleTests` (droits effectifs exacts des deux rôles, écritures
et lectures réelles, historiques, contrôle du démarrage sous le rôle de l'application),
`ReportViewTests` (colonnes, contenu de chaque vue sur un projet fictif, corbeille, complétude et RACI
du domaine), `AccessMigrationTests` (rôles et vues décrits, migration rejouée sans effet, base initiale
migrée, retour arrière, droits changés à la main refusés, membres ajoutés acceptés).
`Topogenia.Architecture.Tests.ExtraRulesTests.Views_exist_only_in_the_bi_schema_of_the_sql_server_provider`
pour l'absence de vue en SQLite.

## Révision (23/10/2026) : toute la base dans l'empreinte (ADR 037)

Le point 2 ne décrivait que les objets et droits des deux schémas et des deux rôles : une fonction ou un synonyme de
`bi` sur `app_user`, un `GRANT SELECT TO public`, un compte de rapport mis dans `db_datareader` étaient acceptés au
démarrage (porte P3, round 1), contrairement à ce que disaient les Conséquences. L'empreinte décrit désormais tout
objet que Microsoft ne livre pas (ligne `X`), les déclencheurs de base (`TR`), les types (`Y`), tout droit de la
base hors `CONNECT` d'un utilisateur et droits sur les objets système (`P`), et le nombre de membres de
`db_datareader`, `db_datawriter`, `db_ddladmin`, `db_securityadmin` et `db_backupoperator` (`MF`) ; les membres de
`db_owner` et des deux rôles restent hors de l'empreinte. Le compte de l'application ne voit pas tout : ce qu'il ne
voit pas n'est refusé qu'à `--migrate` (ADR 037, point 6, avec la proposition d'un `VIEW DEFINITION` sur la base, retenue
par la révision suivante).
Les sondes (point 5) suivent les règles de l'ADR 037 (`ProbeRulesTests`).

## Révision (23/10/2026) : `VIEW DEFINITION` sur toute la base (porte P3, round 1, sécurité M1)

Décision du chef de projet : le point 3 passe de (b) à (c). Avec `VIEW DEFINITION` sur les deux schémas et sur le rôle
des rapports seulement, le compte de l'application ne voyait ni les déclencheurs de base, ni les objets hors des deux
schémas sur lesquels il n'a aucun droit, ni les droits donnés à un autre utilisateur : le contrôle du démarrage du mode
Serveur, qui tourne sous ce compte, acceptait une base ainsi changée, que seul `--migrate` refusait. La migration SQL
Server `20261023124304_ApplicationViewDefinition` (huitième) donne `GRANT VIEW DEFINITION TO topogenia_app` au niveau de
la base (`Down` : `REVOKE`) ; les droits du point 3 (b) restent, couverts par celui-ci. `VIEW DEFINITION` montre des
définitions, jamais des lignes : il n'ouvre aucune donnée, et la base ne contient que l'application. Le droit est décrit
par l'empreinte (ligne `P` de classe `DATABASE`) : retiré à la main, il fait refuser la base au démarrage. Le catalogue
SQL Server ne change pas : sous ce droit, il lit sous le compte de l'application la même description que sous un
administrateur.

Vérification : `tests/Topogenia.Persistence.Tests/Schema/AppAccountCatalogTests.cs`, sous `EXECUTE AS USER` d'un membre
de `topogenia_app` : un déclencheur de base, un synonyme hors des deux schémas et un droit sur la base donné à un
utilisateur sont refusés en `SCHEMA_MODIFIE` au contrôle du démarrage, puis la base est de nouveau acceptée une fois le
changement défait. Mutation : sans le `GRANT` de la migration, les trois tests sont rouges.

## Révision (24/10/2026, porte P3 round 3) : membres des deux rôles qui ne sont pas des utilisateurs

Contexte (sécurité MAJ-1) : la ligne `M` ne décrivait que les rôles dont les deux rôles sont membres, et le point 2
laissait hors de l'empreinte tous les membres des deux rôles. Un rôle d'application (`CREATE APPLICATION ROLE`) ou un
rôle de base ajouté comme membre de `topogenia_app` (ou de `topogenia_bi`) était donc accepté au démarrage
(`UpToDate`) ; par `sp_setapprole`, un compte de rapport lisait alors `app_user`.

Décision : le point 2 vise les **comptes** d'une installation, c'est-à-dire les utilisateurs (connexion Windows ou SQL,
groupe Windows, utilisateur sans connexion, utilisateur d'un certificat...). Un rôle de base ou un rôle d'application
n'est pas un compte : c'est une structure de droits, comme un `GRANT`. L'empreinte décrit désormais :

- ligne `MN|rôle|membre|type` : tout membre de `topogenia_app` ou de `topogenia_bi` de type `R` (rôle de base) ou `A`
  (rôle d'application) ;
- ligne `AR|rôle` : tout rôle d'application de la base, membre de quoi que ce soit ou de rien (les migrations n'en
  créent aucun ; ses droits directs sont déjà dans les lignes `P`, son appartenance à un rôle fixe dans `MF`).

Les utilisateurs ajoutés aux deux rôles restent libres. Une base des migrations n'a aucune de ces lignes : sa
description ne change pas (ADR 037, règle pour les bases déjà migrées). Conséquence pour l'installation : regrouper les
comptes du pool dans un rôle de base intermédiaire membre de `topogenia_app` n'est plus accepté, il faut ajouter les
utilisateurs (ou un groupe Windows) directement au rôle ; la 1.x ne prévoyait pas ce regroupement.

Vérification : `Schema.CatalogChangeTests.An_application_role_in_the_role_of_the_application_opens_the_users_to_the_reports_and_is_refused`
(fuite réelle : un membre de `topogenia_bi`, refusé sur `app_user`, le lit après `sp_setapprole` du rôle d'application
membre de `topogenia_app` ; puis refus `SCHEMA_MODIFIE` sous l'administrateur et sous le compte de l'application, base
de nouveau acceptée une fois le rôle retiré) ; théorie `An_object_or_a_right_added_by_hand_is_refused_as_a_changed_schema`
étendue au rôle d'application seul, membre de chacun des deux rôles, et au rôle de base membre de chacun des deux rôles,
tous refusés sous le compte de l'application. Mutations (constatées rouges, code remis) : lignes `MN` et `AR`
neutralisées (6 rouges : la fuite et les 5 cas) ; `AR` seule (1 rouge : rôle d'application seul ; les autres restent
refusés par `MN`) ; `MN` seule (2 rouges : les deux rôles de base ; les rôles d'application restent refusés par `AR`).

## Révision (24/10/2026) : classification et dernière version validée (AR-100, AR-101)

Décisions du 24/10/2026 : la classification du projet est exposée (`bi.project.classification`, AR-100 : les exports
l'impriment déjà, un rapport doit pouvoir la marquer ou la filtrer) ; le brouillon des fiches reste montré et signalé
(AR-101 : `status` = `draft`, plus `bi.step_sheet.validated_version`, numéro de la dernière version validée ou nul).
Dixième migration SQL Server `ReportViewsClassificationAndValidatedVersion`, colonnes ajoutées à la fin pour ne pas
déplacer celles d'un rapport existant ; le point 8 (« recopier la 1.x ») devient « la 1.x plus ces deux colonnes ».
Vérification : `Bi/ReportViewTests` (colonnes, contenu calculé par l'oracle, brouillon sur une fiche validée).
