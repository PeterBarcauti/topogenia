<!-- 030-ecriture-du-changeset.md -->
<!-- Écriture d'un ChangeSet par l'unité de travail : ordre, stationnement, versions, traduction des erreurs, nouvel essai -->

# 030 - Écriture d'un `ChangeSet` et unité de travail

Statut : acceptée (date 23/10/2026)

## Contexte

P3-T04 écrit le `ChangeSet` du domaine (ADR 015) dans la transaction de l'unité de travail, avec le
stationnement en deux temps (ADR 016), le contrôle des versions (D06), la traduction des erreurs du moteur et le
nouvel essai sur interblocage et base occupée (spec 4.3-5). Plusieurs points ne sont tranchés ni par la spec ni
par les ADR précédentes :

1. l'ordre d'écriture : le `ChangeSet` range les créations, puis les modifications, puis les suppressions
   physiques, puis les objets seulement touchés ; écrire chaque changement dans cet ordre strict échoue sur des
   cas que le domaine produit (déplacer le A d'une étape : le domaine crée ou modifie le nouveau A avant de
   retirer l'ancien, et l'index `UX_step_raci_step_id_approver` refuse le second A ; un échange de cases n'a
   aucun ordre possible) ;
2. le stationnement : l'ADR 016 ne parle que des étapes dont la case change ; or une case, un couple de flèche,
   une sous-carte, un cadre ouvert ou un irritant libérés par une suppression logique (ou un changement de rôle,
   une fermeture, un retrait d'irritant) puis repris dans la même opération posent le même problème, car EF Core
   ne connaît pas les filtres des index partiels ;
3. les enregistrements sans version (`ProjectCounter`, `DeletionBatch`, `SheetOperation`, `DissolutionStep`...)
   n'ont aucune colonne à comparer dans l'instruction qui les écrit ;
4. un `CHECK` violé : la 1.x renvoyait 500 (`ErreurMetier.cs` : seule la clé étrangère `FK_` donnait
   `CONFLIT_INTEGRITE`), A-4-53 et AR-80 demandent qu'aucune violation de `CHECK` ne remonte en 500 ;
5. le nouvel essai « autour de toute l'unité » : le port `IUnitOfWork` (`BeginAsync`, `LockProjectAsync`,
   `CommitAsync`) ne permet pas de rejouer le travail du gestionnaire ;
6. ce qui sort après trois échecs.

## Décision

- **Ordre en deux enregistrements** (`Writing/WritePlan.cs`, `Writing/ChangeSetWriter.cs`) :
  1. d'abord les lots de suppression créés, puis tout ce qui libère une place d'un index unique partiel, écrit
     dans son état final : suppression logique (`deleted_at`, `deletion_batch_id`), rôle A qui devient un autre
     rôle, cadre qui se ferme, usage qui perd son irritant (un usage qui en change perd d'abord l'ancien), et
     suppression physique d'un rôle A ou d'un usage qui porte un irritant ; et chaque étape vivante dont la case
     (couloir, colonne, rangée, ou carte) change, stationnée en colonne 1 000 000 000 + i dans le couloir, la
     carte et la rangée qu'elle quitte (le couloir d'arrivée peut n'être créé qu'au second enregistrement) ;
  2. puis chaque changement du domaine à sa valeur finale, dans l'ordre du `ChangeSet` (créations,
     modifications, suppressions, touchés), EF Core ordonnant les instructions selon les clés étrangères.
  L'ordre du domaine est donc respecté par phases ; à l'intérieur de la seconde, l'ordre imposé par les
  références l'emporte. Les colonnes provisoires ne sortent jamais de la transaction (aucune étape relue au-delà
  de la colonne 10 000).
- **Versions** : une modification ou une suppression d'un enregistrement versionné porte la version lue dans sa
  condition (jeton de concurrence EF Core) ; aucune ligne touchée = `CONFLIT_VERSION`, avec `object` (type
  d'enregistrement), `updated_by`, `updated_at` de la modification qui bloque (sinon l'auteur et l'heure de
  création, comme `OPERATION_DEPASSEE`) ; l'hôte en fait `{par, le, le_iso}` (A-2.16-06, phase 4). Un
  enregistrement sans version modifié ou supprimé est relu par sa clé, par paquets de 500 identifiants
  (spec 4.3-2), et comparé à l'enregistrement de départ : différent ou absent = `CONFLIT_VERSION`. Une création
  dont la clé existe déjà est refusée par la clé (`CONFLIT_UNICITE`). Chaque instruction touche exactement sa
  ligne : après l'écriture, chaque objet touché est dans l'état attendu de `ChangeSet.Touched` (contenu, absent,
  créé), ce que les tests vérifient par `ProjectWorkspace.FirstOutdated` sur l'espace relu.
- **Contrat des enregistrements sans version** (révision du 23/10/2026, porte P3 round 1, Données m3) : la
  relecture d'un enregistrement sans version se fait, en SQL Server, sous versionnement des lignes (RCSI, ADR 031)
  et l'instruction qui l'écrit ensuite n'a aucune condition ; sans verrou, une unité concurrente peut le changer
  entre la relecture et l'écriture, et l'une des deux écritures est perdue (essai de la porte : un lot de
  suppression restauré par deux unités). **Toute modification ou suppression d'un enregistrement sans version se
  fait sous le verrou de son projet** (`LockProjectAsync`, spec 4.7), qui sérialise les unités qui écrivent le
  projet : la relecture voit alors le dernier état validé et rien ne peut le changer avant la validation. Le
  contrat est **vérifié à l'exécution** plutôt que par une condition ajoutée à l'`UPDATE` : l'unité retient les
  projets qu'elle a verrouillés (`StoreSession.LockedProjects`, vidé à chaque nouvel essai et à la validation) ;
  `UnversionedCheck` relit les lignes par leur clé **à travers les enregistrements des projets verrouillés**
  (`ProjectScope.Unversioned<T>`, jointures sur le projet, paquets de 500) ; une unité sans verrou, ou un
  enregistrement présent hors des projets verrouillés, est refusé par `Errors/UnlockedWriteException` (faute du
  gestionnaire, erreur interne, jamais un 409) avant toute écriture ; une ligne changée ou absente reste
  `CONFLIT_VERSION`. La condition sur l'`UPDATE` a été écartée : elle demandait des jetons de concurrence sur toutes
  les colonnes de ces tables (modèle et instantané des migrations changés), une comparaison de texte soumise à la
  collation de SQL Server, et ne protégeait pas les suppressions écrites hors du plan en deux enregistrements. Les
  créations ne sont pas concernées (refusées par leur clé). Les types couverts : `ProjectCounter`, `DeletionBatch`,
  `DissolutionStep`, `DissolutionObject`, `SheetOperation`, `StepSheetVersion`, `PainPointStatusChange`,
  `OperationLogEntry`, `AuditEvent` ; un type sans projet ne peut pas être modifié par un `ChangeSet`.
- **Traduction** : chaque fournisseur classe les erreurs de son moteur derrière `Errors/EngineDialect`
  (`SqlServerDialect` : 1205, 2601, 2627, 547 ; `SqliteDialect` : 5, 6, 19 et ses codes étendus 2067, 1555, 787,
  275) ; `Errors/StoreRefusals` donne `CONFLIT_UNICITE` (index ou clé unique) et `CONFLIT_INTEGRITE` (clé
  étrangère **et** `CHECK`), avec le nom de la contrainte de `docs/modele-donnees.md` dans `constraint` (lu dans
  le message quelle que soit la langue du serveur, ou retrouvé par le modèle depuis les colonnes que SQLite
  donne ; SQLite ne nomme aucune clé étrangère : `null`). Un `CHECK` violé signale une règle que le domaine n'a
  pas appliquée : c'est un conflit (409), pas une erreur interne (A-4-53, AR-80).
- **Nouvel essai** : le port `IUnitOfWork` gagne `RunAsync<T>(travail)` : ouverture, travail (verrou,
  chargement, domaine, écriture), validation ; sur interblocage ou base occupée, tout est annulé et l'unité
  entière repart d'un contexte neuf, 3 essais, 50 puis 100 ms (`Writing/RetryPolicy`). Le travail ne doit rien
  faire hors de la base avant de réussir (comme la 1.x `EnTransaction`). Un refus métier n'est jamais rejoué.
  Le chargement hors unité est rejoué de même.
- **Après trois échecs**, l'exception du moteur est levée telle quelle (l'hôte répond `ERREUR_INTERNE`, comme la
  1.x) : aucun code métier n'existe pour « base occupée », et la spec n'en ajoute pas.
- Les enregistrements du domaine ne sont jamais confiés à EF Core : il suit des copies, qu'il peut modifier.
- La persistance n'appelle jamais `Stamp` (règle `Only_the_workspace_stamps_a_record_or_commits_a_change_set`) :
  l'état intermédiaire part de l'état final, qui porte déjà la version et l'audit donnés par `Commit`.

## Conséquences

- Le domaine ignore le piège de l'unicité (ADR 016) pour tous les index partiels, pas seulement la case.
- Une écriture coûte deux enregistrements EF Core au plus, plus une lecture par type d'enregistrement sans
  version modifié (par 500).
- Un gestionnaire de la phase 4 passe par `IUnitOfWork.RunAsync` ; `BeginAsync`, `LockProjectAsync` et
  `CommitAsync` restent utilisables à l'intérieur.
- Tout gestionnaire qui modifie ou supprime un enregistrement sans version (création d'un code : compteur ;
  suppression et restauration : lot ; fiche : opérations ; dissolution : étapes du cadre) prend d'abord le verrou du
  projet ; un oubli échoue au premier test au lieu de perdre une écriture en production. Un `ChangeSet` calculé sur
  un état périmé et écrit sous le verrou est refusé en `CONFLIT_VERSION`.

## Vérification

`tests/Topogenia.Persistence.Tests/Writing/` sur SQLite et SQL Server : `OperationRoundTripTests` (création,
cycle de trois cases, échange de deux cases, suppression d'un groupe puis annulation, remontée du détail, fiche,
déplacement du A), `VersionConflictTests`, `ErrorTranslationTests`, `RetryTests` (simulés au moteur, plus un vrai
interblocage SQL Server ; la vraie base occupée SQLite est dans `Sqlite/RealBusyTests`, avec la configuration de
production), `ChunkTests`, `UnlockedWriteTests` (contrat des
enregistrements sans version, deux moteurs : sans verrou, sous le verrou d'un autre projet, sous le verrou du
projet) ; chaque exigence vérifiée par mutation
(`docs/couverture-p3/unite-de-travail-chargeur.md`).

## Révision du 24/10/2026, porte P3 round 4 (Sécurité m2)

Le contrôle des projets verrouillés ne porte que sur les enregistrements sans version (`UnversionedCheck`) : une
modification versionnée d'un enregistrement d'un autre projet, sous le verrou d'un seul projet, est écrite (sonde sur les
deux moteurs). C'est voulu par ce contrat : un enregistrement versionné est protégé par sa version (`CONFLIT_VERSION`),
et la sérialisation de la spec 4.7 est l'affaire des gestionnaires de la phase 4, qui verrouillent le projet qu'ils
modifient. Défense en profondeur possible (refuser toute écriture hors des projets verrouillés) : non retenue en phase 3,
à réexaminer en P4 quand les gestionnaires existeront ; noté dans `docs/questions.md`.
