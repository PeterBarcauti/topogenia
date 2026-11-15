<!-- 034-requetes-parcours-et-purge.md -->
<!-- Nombre de requêtes indépendant de la taille, parcours récursifs sans SQL récursif, purge par lots en transactions courtes, erreurs du démarrage -->

# 034 - Nombre de requêtes, parcours récursifs et purge par lots

Statut : acceptée (date 23/10/2026)

## Contexte

L'annexe A 6-6 relève les requêtes N+1 de la 1.x : le fil d'Ariane lit une requête par niveau, `LibererCase` une ou
deux par étape posée, `CreerLien` trois par flèche, les membres une par membre, la restauration d'un lot et l'import
`.pmap` une par étape, et `MiseAJourBase.VersionAttendue` relit ses ressources à chaque appel (A-6-21 à A-6-26). La
1.x parcourait aussi les sous-cartes par une CTE récursive bornée à 50 niveaux (`ServiceCartes.cs:196`, A-5-21) et
renumérotait l'ordre des couloirs par une CTE modifiable propre à SQL Server (`StructureSql.cs`, A-5-20). La purge
était faite par deux procédures (`purge_project`, `purge_trash`, `db/006` et `db/007`) dans une seule longue
transaction ; la spec 4.1 la veut en C#, par lots, dans des transactions courtes, et la spec 4.6 veut que la purge des
lots purge aussi les opérations du journal qui y renvoient. Enfin A-5-10 demande que les erreurs de connexion et de
droits du démarrage (18456, 262, 1802 en 1.x) donnent un message clair.

## Décision

### Nombre de requêtes

- Une commande lit son espace de projet en un nombre **fixe** de requêtes (jointures sur le projet, `ProjectScope`,
  ADR 015) : une par groupe d'enregistrements, quelle que soit la taille du projet. La règle est appliquée en mémoire
  par le domaine ; aucune lecture n'est faite par élément.
- L'écriture d'un `ChangeSet` envoie une instruction par ligne changée (deux pour une étape stationnée, ADR 016),
  chacune avec le contrôle de sa version (D06) : c'est le minimum d'une écriture ligne par ligne versionnée.
  SQL Server les regroupe en lots (un aller-retour pour plusieurs instructions) ; SQLite est dans le processus.
  Les lectures de l'écriture (contrôle des enregistrements sans version) sont une par type et par paquet de 500.
- La preuve compte les commandes envoyées au moteur par un intercepteur d'EF Core (`CommandLog`, classées par la
  source de la commande : requête LINQ, enregistrement, instruction en masse, migration) sur une petite et une
  grande taille (1 contre 200 étapes ou flèches, 1 contre 50 membres, profondeur 1 contre 50, 2 contre 30
  couloirs) : mêmes lectures, écritures bornées par les lignes du plan d'écriture.
- La version attendue du schéma est la liste des migrations compilées avec l'application ; elle est contrôlée une
  fois, au démarrage (`ISchemaMigrations.CheckAsync`, appelé par l'hôte). Aucune commande d'une requête ne lit les
  migrations ni leur historique.

### Parcours récursifs et renumérotation

- **Aucune requête récursive dans le chemin de l'application.** La remontée des sous-cartes est celle, unique, du
  domaine (`ProjectWorkspace.ParentSteps`, bornée à 50), sur les enregistrements chargés : profondeur, fil d'Ariane
  et chemin de lecture sont les mêmes sur les deux moteurs par construction. Seule la vue de rapport
  `bi.process_map` (SQL Server, pour Power BI, ADR 032) garde une CTE récursive, comme la 1.x.
- **Renumérotation des couloirs** : par le domaine (`LaneOperations.ApplyOrder`, renumérotation après une
  suppression), écrite par le `ChangeSet` ; l'ordre n'a pas d'index unique, aucun stationnement n'est nécessaire.
- **Aucun dossier `Persistence/Reading/` n'est créé en phase 3.** Les lectures dédiées vers les DTO (vue de carte,
  fiche, corbeille, listes, spec 3.4) et leurs ports sont définis avec leurs gestionnaires (P4-T02), comme le prévoit
  `architecture.md` § 3 (« dépôts de lecture » à définir en phase 4). Aucune ne manque pour prouver les lignes de la
  phase 3 : le chargeur suffit. Règle pour la phase 4 : une lecture dédiée qui a besoin de la remontée ou de la
  descente des sous-cartes lit les cartes et les étapes parentes du projet en un nombre fixe de requêtes et appelle
  la règle du domaine ; elle n'écrit ni CTE récursive ni boucle de requêtes. Les tests de `Queries/` servent de
  modèle (`Counted`).

### Purge

- **La purge physique est de la persistance** : `Persistence/Purge/TrashPurge`, adaptateur du port
  `ITrashPurge` (nouveau, `Application/Ports/ITrashPurge.cs`). Restent à l'Application (P4-T02, P4-T03) : la limite
  de la rétention (`TrashRules.Limit`), le plan d'un projet (`PurgePlanner.Expired`, `PurgePlanner.WholeProject`,
  domaine), le droit de l'administrateur, le calendrier (au démarrage puis toutes les 24 h) et la journalisation
  d'une erreur sans blocage (annexe A 2.15).
- `ProjectsToPurgeAsync(limite)` donne les projets à la corbeille avant la limite et ceux qui ont un lot supprimé
  avant elle (candidats, planifiés un par un).
- `PurgeAsync(projet, plan)` efface exactement les enregistrements du plan, dans son ordre (dépendants avant ce
  qu'ils référencent) : une première unité de travail efface les entrées du journal qui renvoient à ce qui est
  purgé ; puis les enregistrements, 500 au plus par unité de travail (`RecordsPerTransaction`), chacune sous le
  verrou de projet (spec 4.7), écrite comme les suppressions d'un `ChangeSet` (version contrôlée, enregistrement
  sans version comparé, refus d'un garde-fou traduit, nouvel essai sur interblocage ou base occupée). Chaque préfixe
  validé laisse une base cohérente ; un arrêt au milieu est fini par un nouveau plan, relu.
- **Entrées du journal qui renvoient à un lot** (spec 4.6) : l'opération qui a rempli le lot
  (`deletion_batch.operation_id`) et ses rétablissements (`redone_from_id`) ; les entrées d'une carte purgée (clé
  étrangère) ; toutes celles d'un projet purgé entier. Toujours limitées au projet, effacées par une instruction par
  paquet de 500 identifiants.
- **Garde** : au début de chaque unité, le projet d'une purge entière doit avoir encore la version et la date de
  suppression du plan (toujours à la corbeille), et chaque lot qui reste à purger doit être encore la ligne du plan.
  Sinon `CONFLIT_VERSION` : un projet restauré ou un lot restauré par une annulation entre le plan et la purge
  n'est pas effacé. Un plan d'un autre projet, ou une purge entière d'un projet vivant, est refusé avant toute
  commande (`ArgumentException`, erreur de l'appelant).
- **Droits (ADR 032)** : la purge n'envoie que des `DELETE` (et l'`UPDATE` du verrou de projet) ; elle efface les
  historiques de données avec leur objet et ne les modifie jamais ; l'audit n'a pas de clé étrangère et reste
  (spec 5.5) ; les utilisateurs restent. Une purge entière réussit sous le seul rôle `topogenia_app`.

### Erreurs du démarrage (A-5-10)

- Le message clair et l'arrêt relèvent de l'hôte (P4-T06). Ce que l'instance locale donne, sans créer de login sur
  le serveur, est fixé par des tests : base absente, signalée « en retard » par le contrôle sans être créée, puis
  4060 et 18456 à la connexion ; utilisateur sans droit sur les données, 229 ; compte de l'application qui tente
  de changer le schéma, 262. 1802 (le serveur ne peut pas créer la base) demande des droits sur le serveur qu'un test
  ne touche pas.
- Constat : un compte qui n'a aucun droit sur le schéma `topogenia` ne voit pas les historiques des migrations ;
  EF Core et le catalogue lui donnent une liste vide, et `CheckAsync` conclut « en retard » au lieu de « droits
  insuffisants ». L'hôte du mode Serveur (P4-T06) doit donc, avant de conclure, vérifier que le compte peut lire les
  données (ou ajouter cette vérification à `CheckAsync`, code `DROITS_INSUFFISANTS`) et traduire 4060 et 18456
  (`BASE_INACCESSIBLE`), 229, 262 et 1802 (`DROITS_INSUFFISANTS`) ; la lecture des numéros a sa place dans le
  fournisseur SQL Server (comme `SqlServerDialect`).

## Conséquences

- Le nombre de requêtes d'une commande ne dépend que des groupes chargés ; une commande sur un très gros projet
  paie le chargement entier (5 000 étapes au plus, ADR 015).
- La purge d'un gros projet se fait en plusieurs transactions : un autre utilisateur n'est jamais bloqué
  longtemps ; une purge interrompue laisse un projet partiellement purgé mais cohérent, toujours à la corbeille ou
  toujours supprimé logiquement, que la purge suivante finit.
- Les lectures dédiées de la phase 4 suivent la règle ci-dessus ; un écart demande une nouvelle ADR.

## Vérification

`tests/Topogenia.Persistence.Tests/Queries/` (nombre de requêtes, profondeur et fil d'Ariane sur 60 niveaux,
couloirs, profil), `Purge/` (purge entière, lots et journal, cadre ouvert, arrêt au milieu, garde, droits du rôle),
`Startup/StartupErrorTests` ; chaque exigence vérifiée par mutation (`docs/couverture-p3/portage-b.md`).
