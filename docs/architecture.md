<!-- architecture.md -->
<!-- Architecture of Topogenia 2.0: layers, project workspace, change sets, operation log, persistence, host modes -->

# Architecture de Topogenia 2.0

Ce document décrit comment le code est organisé et pourquoi. Les décisions sont tracées dans
`docs/decisions/` (ADR 001 et suivantes, index dans `docs/decisions/README.md` ; arbitrages AR-01 et
suivants dans `000-arbitrages-phase-0.md`). Glossaire français-anglais : spécification,
section 3.3, complété au § 2.5 pour les noms propres au code.

## 1. Couches et dépendances

```
Topogenia.Domain          entités, objets valeur, règles pures, espace de projet, ChangeSet
   ^
Topogenia.Application     un gestionnaire par cas d'usage, ports, autorisations, journal
   ^              ^
Persistence       Interchange          adaptateurs : EF Core, formats .topogenia / .pmap (migration 1.x retirée, AR-94)
   ^    ^
SqlServer Sqlite                       fournisseurs : migrations, réglages, SQL propre au moteur
   ^
Topogenia.Host                         HTTP, authentification par mode, OpenAPI, en-têtes, CLI
```

Règles vérifiées par `tests/Topogenia.Architecture.Tests` : voir la spec 3.2 et l'ADR 017.

## 2. Domaine

### 2.1 Enregistrements du modèle

Le modèle (`src/Topogenia.Domain/Model/`) est fait d'**enregistrements immuables publics**
(`sealed record`) : ils n'ont ni état privé ni méthode qui les modifie. Une opération ne
modifie jamais un enregistrement : elle en construit un nouveau (`record with { ... }`) et le
**remplace** dans l'espace de projet par `Put` ; `SoftDelete` et `Remove` suppriment
logiquement ou physiquement. Les types restent simples (texte, nombre, Guid, date UTC) pour
la persistance ; les règles d'une valeur saisie vivent dans les objets valeur de `Values/`
(ADR 026), qui produisent ces types simples.

- `IRecord` : la clé (`RecordKey`, type et valeurs de clé) de tout enregistrement. Chaque type
  d'enregistrement construit sa clé dans une seule méthode statique `KeyOf` (`Step.KeyOf(id)`,
  `DocumentUse.KeyOf(document, étape)`, `StepRaci.KeyOf(étape, couloir)`, `Translation.KeyOf(...)`...),
  que sa propriété `Key`, l'espace, les requêtes et les règles appellent : `RecordKey.Of`, `new RecordKey`
  ou un `with` sur une clé n'apparaissent que dans `Model/` (test d'architecture
  `Record_keys_are_built_only_by_the_records_of_the_model`, ARC4-M3).
- `IVersionedRecord` : un enregistrement modifiable par une demande d'utilisateur porte
  `Version` (entier, D06), `UpdatedAt/By` (UTC, D08) ; `Stamp` n'est appelé que dans
  `Workspace/` (`Versioning`, par `Commit` ou, pour un utilisateur qui n'appartient à aucun
  projet, par `Versioning.Update` : le profil est versionné, AR-07), jamais par une règle
  (vérifié par un test d'architecture) ; `Versioning.Update` est interne au domaine et seul
  `Projects/UserRules` l'appelle (test `Only_the_rules_of_the_users_stamp_a_record_outside_a_workspace`). Les usages, traductions, remontées, membres et rôles
  RACI en font partie : leurs propriétés `Version`, et pour `StepRaci` et
  `Dissolution` `UpdatedAt/By`, sont des propriétés `init`. Les usages, traductions, membres et
  rôles RACI, dont la clé est faite d'autres identifiants, portent le marqueur `IKeyReusedRecord` :
  à leur création, le commit leur donne la version de structure du projet (au moins 1) au lieu
  de 1, pour qu'un enregistrement recréé sous la même clé ne reprenne jamais une version déjà
  lue (AR-111) ; les autres enregistrements naissent à la version 1.
- Enregistrements **sans version**, liste exacte tenue par le test d'architecture
  `DomainModelRulesTests` : `ProjectCounter` (n'avance que sous le verrou de projet, jamais
  rejoué, D07), `DeletionBatch` (jamais modifié par une demande : créé par l'espace au premier
  objet supprimé d'une opération, marqué restauré par l'annulation, redaté par le
  rétablissement, retiré par la purge ; l'annulation contrôle ses objets et son contenu), `OperationLogEntry` (le journal lui-même),
  `SheetOperation` (ligne du déroulé, écrite seulement par l'enregistrement du brouillon, qui
  fait avancer la version de la fiche dès qu'une ligne change : `SheetWorkflow.Save` touche la
  fiche),
  `StepSheetVersion`, `PainPointStatusChange`, `AuditEvent` (ajout seul), `DissolutionStep`
  et `DissolutionObject` (créés et retirés avec leur remontée versionnée).
- `ISoftDeletable` : les enregistrements de structure (`ProcessMap`, `Lane`, `Step`,
  `Flow`) portent aussi `DeletedAt` et `DeletionBatchId` (suppression logique, spec 4.5).
- Les identifiants sont des Guid v7 (D07) donnés par `OperationContext.NewId()`, la date par
  `OperationContext.Now` : dans le domaine, seul `OperationContext` lit l'horloge ou crée un
  identifiant (vérifié par un test d'architecture).

| Groupe | Enregistrements |
|---|---|
| Projet | `Project` (code, nom, langue, description, dossier partagé, classification, libellé du groupe de partage), `ProjectMember`, `ProjectCounter`, `DeletionBatch` |
| Utilisateur | `AppUser` (login, `LoginNormalized`, nom affiché, e-mail, langue, administrateur, première visite, dernier projet ouvert) |
| Structure | `ProcessMap`, `Lane`, `Step`, `Flow`, `StepRaci`, `Dissolution` (+ `DissolutionStep`, `DissolutionObject`) |
| Fiche | `StepSheet`, `SheetOperation`, `StepSheetVersion` |
| Documents | `Document`, `DocumentUse`, `PainPoint`, `PainPointStatusChange` |
| Transverse | `Translation`, `OperationLogEntry` (sans logique, phase 4), `AuditEvent` |

Les réglages de l'application (profondeur conseillée, lettres RACI, rétention) sont un
`AppSettings` immuable passé par le contexte de l'opération ; les langues configurées sont un
`LanguageSettings` (AR-29). Ni l'un ni l'autre n'est un enregistrement.

Les **invariants** sont tenus à deux endroits : chaque opération vérifie ses préconditions et
refuse par une `DomainException` portant un code de `ErrorCodes` ; `StructureInvariants`
vérifie la structure entière (une étape vivante par case, couloir et carte vivants d'une
étape, RG-C05 (`FlowLinks`), pas de flèche en double, un seul A, une remontée ouverte par
carte, une sous-carte vivante par étape et une étape parente vivante, couloir vivant sur une
carte vivante) après une annulation ou un rétablissement. RG-C05 et la règle de doublon des
flèches sont écrites une seule fois, dans `Workspace/FlowLinks` : la création d'une flèche
(`StructureEdits.NewFlow`), la barre de commande, les invariants et l'annulation l'appellent
directement (aucun relais).

### 2.2 Espace de projet et ChangeSet (ADR 015)

Une opération travaille sur un **espace de projet** (`ProjectWorkspace`) : les
enregistrements du projet chargés en mémoire (cartes, couloirs, étapes, flèches, RACI,
remontées ; et, pour les opérations qui les touchent, documents, usages, irritants, fiches,
traductions, membres). Les volumes sont faibles (5 000 étapes par projet au plus).

L'espace garde la **référence** des enregistrements chargés (ou du dernier `Commit`). Une
règle lit (`Find`, `All`, `LiveStep`...) et remplace (`Put`, `SoftDelete`, `Remove`). À la fin
de l'opération, `Commit(context)` compare chaque enregistrement à sa référence et produit le
**ChangeSet** :

| Changement | Contenu |
|---|---|
| `Created` | l'enregistrement créé |
| `Modified` | l'enregistrement avant et l'enregistrement après |
| `Removed` | l'enregistrement supprimé physiquement |

La comparaison ignore la version et l'audit : remettre les mêmes valeurs ne change rien. Un
enregistrement versionné modifié reçoit au `Commit` la version suivante et `UpdatedAt/By`
de l'opération (AR-32) ; `Touch` force ce changement de version quand une opération touche un
objet sans modifier ses champs (l'étape dont le RACI ou la sous-carte change, la fiche dont une
ligne du déroulé change, comme la 1.x) : il sert au contrôle `If-Match` d'un client qui a lu
l'objet avant (spec 4.7) ; il ne sert plus au contrôle de l'annulation, qui compare les contenus (AR-81).
La suppression logique est une modification (`DeletedAt`, `DeletionBatchId`, lot créé au
premier objet supprimé). Les changements sont rangés dans l'ordre réel de `Commit` : les
créations dans l'ordre de création, puis les modifications de contenu, puis les suppressions
physiques, et en dernier les enregistrements seulement touchés par `Touch` (modification de
version sans changement de contenu) ; une clé créée, retirée puis recréée dans une même
opération est une seule création. `ChangeSet.Touched` donne chaque enregistrement créé,
modifié ou supprimé physiquement par l'opération avec l'**état attendu** (`ExpectedState`) pour
le contrôle de l'annulation : l'enregistrement tel que l'opération l'a laissé, comparé par son
contenu hors version et audit de mise à jour (AR-81), ou « absent » (`ExpectedState.Absent`)
pour un enregistrement supprimé physiquement. Les compteurs de codes n'y figurent pas (D07).

Une opération du domaine ne touche jamais la base : elle reçoit l'espace, applique la règle,
renvoie un résultat (identifiants, codes, avertissements). Le ChangeSet est calculé par
l'espace, pas par chaque règle : une règle ne peut pas oublier un changement. Une règle
n'appelle jamais `Commit` (l'Application le fait, vérifié par un test d'architecture) ; la
création d'un projet lit ses enregistrements créés par `CreatedSinceReference`, sans valider.

### 2.3 Annuler et rétablir (spec 4.6, ADR 005)

`OperationLogEntry` garde, par opération : le genre (`kind`), la carte, l'utilisateur, le
rang, l'aller (`forward`), l'inverse (`inverse`), les objets touchés et leur version après
l'opération.

- **Inverse d'un ChangeSet** : `Modified(avant, après)` devient `Modified(après, avant)` ;
  `Removed(e)` devient `Created(e)` ; `Created(e)` devient `Removed(e)` pour un objet sans
  suppression logique, et une **suppression logique dans un lot neuf** pour un objet de
  structure (le comportement de la 1.x : l'étape annulée reste restaurable, ses fiches et
  documents ne sont pas perdus). Les compteurs de codes (`ProjectCounter`) sont laissés tels
  quels par l'inverse comme par le rétablissement : un code n'est jamais redonné (D07).
- **Contrôle** (`ProjectWorkspace.FirstOutdated`, appelé par `UndoReplay` lui-même, AR-81) :
  chaque objet touché par l'opération (par son annulation, pour un rétablissement) doit avoir
  encore le contenu qu'elle lui a laissé, version et audit de mise à jour mis à part (la même
  égalité que `Commit`) ; une clé créée par l'opération doit exister avec ce contenu ; un
  enregistrement supprimé physiquement doit être toujours absent (une traduction réécrite
  depuis par un autre utilisateur bloque). La version n'est pas comparée : l'annulation de
  l'opération suivante remet le contenu avec une version neuve, et la pile entière (50
  opérations, spec 4.6) doit s'annuler puis se rétablir ; une écriture qui change le contenu
  bloque toujours, un contenu revenu à l'identique (ABA) ne bloque pas et ne perd rien. Sinon
  `OPERATION_DEPASSEE` (409, détails `object`, `updated_by`, `updated_at` de la modification
  qui bloque quand l'enregistrement est versionné ; pour un enregistrement créé depuis et
  jamais modifié, son auteur et son heure de création, `ICreationAudited`). Seul le cadre
  qu'une remontée a créé peut avoir été fermé ou rouvert depuis sans bloquer l'annulation de
  cette remontée (T-REMO-13) ; tout autre changement d'un cadre bloque. Le rétablissement
  reçoit l'aller et l'annulation de la même entrée du journal : c'est le journal (phase 4,
  `redone_from_id`) qui garantit leur correspondance, le domaine ne la vérifie pas (une
  annulation légitime peut ne rien changer, une flèche dont une extrémité est morte restant
  supprimée). Après
  application de l'inverse sur l'espace, les invariants de structure sont vérifiés (une
  étape vivante par case, flèches entre étapes vivantes d'une même carte, couloir vivant,
  un seul A, une seule remontée ouverte...) ; une violation donne `CARTE_MODIFIEE` (AR-04,
  AR-43). Un refus vide la pile de l'utilisateur sur la carte (AR-05).
- **Protocole entier dans `Structure/UndoReplay`** : `Undo(espace, contexte, aller)` et
  `Redo(espace, contexte, aller, annulation)` contrôlent les versions, appliquent l'inverse
  ou l'aller (`ApplyInverse` et `ApplyForward` sont internes au domaine), réparent, contrôlent
  les invariants et marquent les lots ; l'Application n'a plus qu'à valider l'espace. Seuls
  `OPERATION_DEPASSEE` et `CARTE_MODIFIEE` en sortent (AR-43) : tout autre refus rencontré en
  rejouant ou en réparant, par exemple un décalage au-delà de la colonne 10 000 (AR-73), devient
  `CARTE_MODIFIEE` (AR-79).
- **Réparation fidèle à la 1.x** (`Structure/UndoReplay`, annexe A 2.15, à l'annulation
  comme au rétablissement) : avant le contrôle des invariants, les couloirs des cartes
  touchées sont renumérotés de 0 à n-1 ; une étape revenue dans une case prise depuis par une
  étape hors de l'opération garde sa case et l'intruse est décalée (RG-C04) ; une flèche
  revenue qui doublerait une flèche vivante tracée depuis dans le même sens, ou dont une
  extrémité n'est plus une étape vivante de sa carte, reste supprimée. Après une annulation,
  chaque lot de l'opération qui ne contient plus rien est marqué restauré (heure et auteur de
  l'annulation, AR-68). Le rétablissement supprime de nouveau, comme la 1.x qui renvoyait la
  commande : chaque lot de l'opération revient non restauré, daté et signé par le
  rétablissement, et ses objets avec lui (la rétention de la corbeille part du
  rétablissement) ; chaque lot créé par l'annulation et vidé par le rétablissement est marqué
  restauré par l'auteur du rétablissement (AR-78). Un refus
  laisse l'espace modifié en mémoire : l'Application ne le valide pas et le jette avec sa
  transaction (AR-69).
- L'annulation est une nouvelle écriture dans une seule transaction, sous le verrou de projet.

### 2.4 Règles

Les services du domaine reprennent chacun une règle de l'annexe A (spec 3.3) : `GridRules`,
`FlowRules`, `Stitcher`, `SelectionBlocks`, `BlockEnds`, `GroupIntoSubMap`, `Dissolve`, `RaciRules`,
`SheetCompleteness`, `SheetVisibility`, `LayoutWarnings`, `CommandLineParser`, `TextRules`.
Les refus sont des `DomainException` portant un code stable de `ErrorCodes` et des détails
(valeurs seulement), jamais des messages : les messages traduits vivent dans l'hôte et dans
l'interface.

Chaque règle a une seule implémentation (spec 3.3). Le test d'architecture `SingleImplementationRules` en garde quinze sur le domaine compilé (symboles résolus par le modèle sémantique de Roslyn : un alias, un `using static`, un nom partiel ou un groupe de méthodes est vu comme le membre qu'il désigne). Douze sont reconnues à leur forme : chacune ne doit apparaître que dans son fichier et doit y apparaître. Trois sont reconnues à toute lecture d'un membre du modèle, quelle que soit la forme du code autour (tri, comparateur, regroupement, repli par `Aggregate`, case de tableau, deux méthodes qui s'appellent) : seuls les membres listés (fichier et méthode) le lisent, et chacun le lit encore ; une écriture (affectation, `with`, initialiseur) n'est pas une lecture. `SingleImplementationSamplesTests` prouve chaque détecteur sur ses contournements.

Lectures gardées :

- `Lane.SortOrder` : seulement dans `ProjectWorkspace.InLaneOrder` (le départage unique) et dans les renumérotations `LaneOperations.Add` (place du nouveau couloir), `LaneOperations.ApplyOrder` et `StructureEdits.NormaliseLaneOrder` ; en attente, signalé au domaine : `StepInsertion.Relay`, qui cherche le couloir voisin par `SortOrder` plus ou moins 1 au lieu de son voisin dans `LanesOf` (le test échoue dès qu'il ne le lit plus) ;
- `ProcessMap.ParentStepId` : seulement dans `ProjectWorkspace.ParentSteps` (la seule remontée) et `ProjectWorkspace.SubMapOf`, `ProcessMap.IsRoot`, les invariants (`StructureInvariants.AddMapAndLaneViolations`), la purge, qui suit aussi les cartes supprimées (`PurgeScope.Complete`, `PurgeScope.AddMapsBottomUp`), et deux lectures d'un seul niveau, jamais une remontée : `DissolutionFrequencies.Ensure` (fréquence de l'étape parente) et `TranslationCatalog.Texts` (étape dont la carte porte le libellé) ;
- toute suite de `StepSheet` (une expression dont le type est une suite de fiches : `All<StepSheet>()`, une liste, un tableau, d'où une fiche serait prise par `First`, `ElementAt`, une boucle ou un indice) : seulement dans `SheetQueries.SheetOf` et dans la purge des fiches des étapes purgées (`PurgeScope.AddSheetsAndDocuments`).

Formes gardées, chacune dans son fichier :

- la remontée vers les cartes parentes (`ProcessMap.ParentStepId` lu dans une boucle ou une récursion qui remonte d'une étape à sa carte) dans `ProjectWorkspace.ParentSteps` (profondeur, fil d'Ariane, chemin de lecture) ;
- la descente dans les sous-cartes (`SubMapOf` dans une boucle ou une récursion qui parcourt les étapes, ou rassemblé par `SelectMany`) dans `ProjectWorkspace.Subtree` et `SubMapSubtrees` ;
- le départage des couloirs (tout tri dont la clé lit `Lane.SortOrder` : `OrderBy`, `ThenBy`, `MinBy`, `Sort`, `Comparer.Create`, `CompareTo`) dans `ProjectWorkspace.InLaneOrder` ; ces trois dans `Workspace/ProjectWorkspace.Queries.cs` ;
- l'ordre d'entrée des documents (tri sur `Document.CreatedAt`) dans `DocumentRules.InEntryOrder` ;
- la clé d'un document (`RecordKey.Of` de la constante `"document"`, quelle que soit l'écriture de la constante) dans `Document.KeyOf` seul (`Model/SheetRecords.cs`), que `ProjectWorkspace.Document` appelle ; toute clé construite hors `Model/` (`RecordKey.Of`, `new RecordKey`, `with`) est en outre refusée par `DomainModelRulesTests` ;
- la fiche d'une étape (un élément unique pris par `First`, `Single` ou `Last` dans une suite de `StepSheet`) dans `SheetQueries.SheetOf` ;
- les entrées et sorties d'un bloc (un test qu'aucune flèche n'atteint, ou ne quitte, une étape, sur une seule extrémité : propriété de `Flow` ou de `FlowEnds`, premier ou second élément d'un tuple ; écrit par `Any` ou `Exists` nié, `All`, `Count` comparé à zéro, `Contains` nié sur les extrémités des flèches, rassemblées ou non dans une variable locale, ou `Except`) dans `Blocks/BlockEnds.Of` ;
- le refus RG-C09 qui demande un choix (`ErrorCodes.MacroChoiceRequired`) dans `MacroStepDeletion.SettleSubMaps` ;
- les codes d'erreur et d'avertissement, écrits seulement comme constantes de `ErrorCodes` et `WarningCodes` (aucun texte constant égal à un code ailleurs : littéral, concaténation ou interpolation de constantes, `string.Concat` ou `string.Join` de constantes) ;
- RG-C05 (son refus `LIEN_HORS_CARTE` ; la carte d'une étape comparée à celle d'une flèche ; la carte d'une étape, lue par un accès, un accès conditionnel ou un motif de propriété, dont l'étape vient dans le fichier d'une extrémité de flèche, `Flow.SourceStepId`, `TargetStepId`, `FlowEnds.Source` ou `Target`, à travers les variables locales ; une étape cherchée par `Step`, `LiveStep` ou `Find` depuis une extrémité de flèche, dans une fonction qui lit aussi `Step.MapId`) dans `Workspace/FlowLinks` ;
- la langue d'origine d'une traduction dans `LocalizedText.TakesTranslationIn`, appelée par la saisie manuelle, l'import et la portée de l'export (ADR 026) : (1) une comparaison de textes (`==`, `!=`, motif constant, `Equals`, comparateur comme `StringComparer.Ordinal`, `Compare`, `Contains` sur une suite de textes) dont un côté est une langue d'origine nommée par le modèle (`SourceLanguage` d'un enregistrement ou d'une requête du domaine, `Language` de `LocalizedText`, de `TranslatedText` et de `TranslatableText`), suivie à travers les variables locales du fichier quel que soit leur nom ; (2) une égalité de textes dont un côté lit un membre, un paramètre ou une variable locale dont le nom finit par « language » (une langue reçue en paramètre n'est connue que par son nom) ;
- la carte qui porte le texte de son étape ou de son projet (une comparaison, sous les mêmes formes, dont un côté vient de `ProcessMap.Name`, à travers les variables locales, `Contains` sur les noms de plusieurs cartes compris) dans `TranslationRules.BearsText`, appelée par le renommage d'une étape, l'export et l'import des traductions.

D'autres règles sont écrites une seule fois sans détecteur dédié : « étape vivante de cette carte » (`ProjectWorkspace.LiveStepOfMap`, et `StructureEdits.StepOfMap` qui refuse `ETAPE_HORS_CARTE`), « document tenu par l'étape » dans le reliquat (`Remainder.DocumentHeldBy`), l'écriture des traductions (`TranslationRules`), l'avertissement de profondeur (`LayoutWarnings.Depth`), la suppression d'un document (`DocumentEdits.Remove`), le doublon de flèche (`FlowLinks`).

Limites connues, qu'aucun détecteur ne voit : une valeur n'est suivie que dans son fichier et à travers ses variables locales, jamais à travers un paramètre, un champ, le retour ou le `out` d'une autre méthode ; ainsi une langue d'origine reçue en paramètre sous un nom qui ne finit pas par « language », ou une étape obtenue d'une extrémité de flèche par une autre méthode puis dont la carte est lue, ne sont pas vues. Une comparaison par `StartsWith`, `IndexOf`, `Except` ou un ensemble de langues construit par une autre méthode n'est pas vue pour la langue d'origine ni pour la carte qui porte le texte. Un test d'extrémité de bloc écrit par `Any()` sans condition après un `Where`, par `FirstOrDefault(...) is null` ou par un dictionnaire de degrés n'est pas vu. Un code d'erreur assemblé à l'exécution à partir d'une valeur non constante (une variable, `new string(...)`) n'est pas vu. Une remontée ou une descente écrite dans un membre autorisé à lire `ProcessMap.ParentStepId` n'est vue que si elle prend la forme que reconnaissent les détecteurs de remontée ou de descente (boucle ou récursion).

Le domaine ne fait aucune entrée ni sortie et ne lit aucun état du processus (spec 3.2, 3.3, D03). `DependencyRulesTests.The_domain_uses_only_the_framework_types_of_its_allow_list` le vérifie par une liste blanche (`DomainFrameworkDetectors`), sur chaque nom résolu (type, membre, attribut, méthode d'extension) et sur le type de chaque expression, de sorte qu'un `var` ne cache rien. Sont permis : une liste fermée de types de `System` (types de valeur et texte, `Math`, `Guid`, `DateTime`, `TimeSpan`, `Uri`, `Index`, `Range`, `Span`, délégués `Action`, `Func`, `Predicate`, `Comparison`, exceptions d'argument et d'opération, `Type` sans aucun de ses membres) et les espaces de noms `System.Collections`, `System.Collections.Generic`, `System.Collections.ObjectModel`, `System.Collections.Immutable`, `System.Collections.Frozen`, `System.Linq` (sans `ParallelEnumerable`), `System.Text`, `System.Text.RegularExpressions`, `System.Globalization`, `System.Diagnostics.CodeAnalysis`, `System.Runtime.CompilerServices` (sans `Unsafe`, `RuntimeHelpers`, `ModuleInitializerAttribute`, `UnsafeAccessorAttribute`) et `System.Numerics`. Tout le reste est refusé, notamment `System.IO`, `System.Net`, `System.Xml`, `System.Reflection`, `System.Runtime.InteropServices` (donc `DllImport` et `LibraryImport`), `System.Threading` (le domaine n'emploie pas `Interlocked`), `System.Text.Json` hors de l'exception ci-dessous, `Environment`, `AppContext`, `AppDomain`, `Console`, `GC`, `Activator`, `Random`, `DateTimeOffset`, `TimeProvider`, `TimeZoneInfo`, ainsi que `extern`, `unsafe`, les pointeurs et `dynamic` ; `HashCode` et `GetHashCode`, dont la valeur change d'un processus à l'autre, ne sont permis que dans une redéfinition de `GetHashCode`. Une exception documentée, limitée à son fichier et à ses types : `Sheets/SheetJson.cs` lit et écrit en mémoire le JSON d'une fiche avec `JsonDocument`, `JsonElement`, `JsonNode`, `JsonObject`, `JsonArray`, `JsonSerializerOptions` et `JavaScriptEncoder` (jamais `JsonSerializer`). Les listes des codes d'erreur que vérifie le test des messages (spec 3.6) sont lues par les tests eux-mêmes : le domaine ne lit aucun type. L'horloge et la fabrique d'identifiants (`DateTime.UtcNow`, `Guid.CreateVersion7`) restent réservées à `OperationContext` par `DecisionRulesTests`. Un second filet sur l'assemblage compilé, code généré compris (`The_compiled_domain_uses_no_process_reflection_or_interop_type`), refuse `System.Xml`, `System.Threading`, `System.Resources`, `System.Runtime.InteropServices.Marshalling` et les types `AppContext`, `Activator`, `GC`, `Random`, `TimeZoneInfo`, `TimeProvider`, `DateTimeOffset`, `DllImportAttribute`, `LibraryImportAttribute`, `Marshal`, `Assembly`, `MethodInfo` et `PropertyInfo`, en plus des types interdits au domaine et à l'application (`System.IO`, `System.Net`, `Console`, `Environment`...).

Les dossiers du domaine forment des couches ordonnées (test d'architecture `DomainModelRulesTests`, graphe lu sur les symboles résolus : directives `using`, `using static`, alias, noms partiels comme `Dissolutions.X` écrits dans l'espace de noms du domaine, membres et méthodes d'extension ; `SemanticSamplesTests` le prouve) : `Values` et `Errors` en bas, puis `Model`, `Workspace` (espace, ChangeSet, versions, invariants, `FlowLinks`), `Structure` (primitives, grille, annulation), puis `Blocks` et les dossiers de règles. Un dossier ne désigne qu'un dossier d'une couche inférieure ou de sa propre couche (`Values` et `Errors` entre eux, les dossiers de règles entre eux), jamais un dossier d'une couche supérieure : `The_folders_of_the_domain_follow_the_order_of_the_layers` vérifie cet ordre et donne chaque écart avec sa ligne, et `The_folders_of_the_domain_depend_on_each_other_without_cycle` garde le graphe sans cycle. `Blocks` (entrées et sorties d'un bloc) est une fonction pure sur des identifiants d'étapes et des couples d'extrémités : il ne peut désigner que `Errors`, `Values` et `Model` (aujourd'hui aucun dossier), jamais `Workspace` ni `Structure`. Parmi les dossiers de règles, `Dissolutions` ne désigne ni `Layout` ni `Selection`, qui lisent ses cadres ouverts et ses flux autonomes, et appelle `Blocks` pour les extrémités d'un détail.

### 2.5 Dossiers et glossaire du code

| Dossier | Contenu |
|---|---|
| `Values/`, `Errors/` | objets valeur (ADR 026), codes et refus |
| `Model/` | enregistrements immuables |
| `Workspace/` | espace de projet, `ChangeSet`, `Versioning`, `FlowLinks`, `StructureInvariants`, requêtes uniques |
| `Structure/` | primitives (`StructureEdits`, `GridRules`), annulation réparée (`UndoReplay`) |
| `Blocks/` | `BlockEnds` : entrées et sorties d'un bloc d'étapes (fonction pure sur des identifiants, qui ne peut désigner que `Errors`, `Values` et `Model` ; sous `Dissolutions` et `Selection`, ses seuls appelants) |
| `Flows/`, `Lanes/`, `Steps/`, `Layout/`, `Selection/`, `SubMaps/`, `Raci/`, `Dissolutions/` | règles de structure de l'annexe A |
| `Sheets/`, `Documents/`, `Translations/` | fiches, documents, irritants, traductions |
| `Projects/`, `Trash/` | projets, membres, utilisateurs, corbeille et purge |
| `Maps/` | `MapMarkers` (marqueurs des étapes et des flèches d'une carte, lus dans l'espace) et `AutomaticColumns` (fonction pure, sans espace, appelée par les formats d'échange : annexe A 2.17, placement automatique de l'import) |

| Terme | Dans le code |
|---|---|
| Cadre de remontée (`DissolutionFrame`, spec 3.3) | `Dissolution` (le cadre et son lot), `DissolutionStep` (case d'origine de chaque étape), `DissolutionObject` (reliquat) ; vu par `Frames.ViewOf` et `Frames.OpenFrameOf` (`FrameView`) ; un seul cadre ouvert par carte (A-2.9-02, invariant `open-dissolution`) ; fermé par `Remainder.Finish` |
| Placement automatique (1.x `ColonnesAutomatiques`) | `Maps/AutomaticColumns.Place` |
| Règles de lien (RG-C05, doublon) | `Workspace/FlowLinks` |
| Flèche (1.x « lien ») | `Flow` ; ses extrémités dans une sélection : `FlowEnds` (le mot « arrow » n'est plus employé) |

## 3. Application

Un gestionnaire par cas d'usage, regroupés par fonctionnalité (spec 3.4). Toute commande passe par le même gabarit,
`Commands/CommandRunner` (ADR 042) ; une commande est une classe `ProjectCommand<TResult>` qui déclare un seul droit et
n'appelle que le domaine :

1. autorisation déclarative (`Authorization/` : `RequiresRole(rôle minimal)`, `RequiresSheetFill`, `RequiresAdmin`,
   lus par `AccessPolicy`) : utilisateur authentifié, commande d'administrateur refusée à tout autre avant toute
   lecture, puis, dans l'unité, projet de la cible lu par `IAccessReader` : absent, dans le mauvais état (vivant ou
   corbeille) ou non-membre → `INTROUVABLE`, rôle insuffisant → `ACCES_REFUSE`, administrateur en animateur ;
2. unité de travail (`IUnitOfWork.RunAsync`, rejouée en entier sur interblocage avec un nouveau contexte d'opération)
   et verrou de projet (`structure_version + 1`, spec 4.7) ;
3. chargement de l'espace avec ce que la commande et la vue demandent ;
4. droit de remplir sur l'étape vivante (`SheetRights`) ; en mode Serveur, une requête sans la version que son mode
   exige (`If-Match` absent, signalé par le port `IVersionRequirement`) est refusée 428 `VERSION_REQUISE`
   (`VersionCheck.EnsureRead`) : après les droits, pour qu'un étranger reçoive toujours 404 et un rôle insuffisant 403,
   et avant le domaine (ADR 045) ; puis contrôle des versions lues (`VersionCheck`, `CONFLIT_VERSION` avec l'auteur et
   l'heure), appel du domaine, `Commit` ;
5. écriture du `ChangeSet`, puis, dans la même unité de travail, de l'événement d'audit de la commande quand elle en
   déclare un (`ProjectCommand.AuditKind`, construit par `Audit/AuditTrail` : pas d'événement sans changement ni de
   changement sans événement, spec 5.5, ADR 046) ; inscription au journal d'une commande annulable
   (`OperationLog/OperationRecorder`) ;
6. réponse `CommandOutcome` : le résultat du domaine (`id`, `code`, `ids`), `operation_id`, la version de structure,
   les avertissements de l'opération et la vue de la carte construite sur l'espace écrit (`Maps/MapViewBuilder`,
   AR-50).

Deux autres gabarits suivent le même ordre : `Queries/QueryRunner` pour les lectures d'un projet (droits, puis espace
lu dans une transaction cohérente, sans verrou ni écriture, ADR 031) et `Commands/UserRunner` pour les demandes qui ne
visent pas une carte (profil, liste des projets, création, membres, corbeille, audit) : droits lus par `AccessPolicy`,
projet de la demande dans l'état attendu (vivant ou corbeille), unité de travail pour une écriture, 428
`VERSION_REQUISE` après les droits ; `UserRunner.SignInAsync` connecte l'utilisateur de la requête (création à la
première requête avec l'événement d'audit `user.created`, drapeau administrateur relu sur `IAdministrators`). Les
événements qui ne suivent pas un `ChangeSet` (purge, refus répétés) sont écrits seuls par `IAuditStore.AppendAsync`.

Journal (`OperationLog/`, ADR 042) : une chaîne par opération logique, la racine écrite par la commande et ses
rétablissements qui pointent vers elle (`redone_from_id`) ; la dernière entrée d'une chaîne dit son état. Annuler
prend la chaîne vivante de plus haute place, rétablir la chaîne annulée de plus basse place, la place étant la
séquence de la racine (`OperationChain.Place`, ADR 042 révisée) ; une nouvelle opération
retire la pile de rétablissement et les chaînes au-delà de 50, chaînes entières (`OperationStack`). Le `ChangeSet`
est écrit par `ChangeSetJson` (enregistrements entiers, nommés par le type de leur clé). `UndoCommand` et
`RedoCommand` passent par le même gabarit : la pile est lue après le verrou (`PrepareAsync`), l'annulation marque son
entrée et y garde le `ChangeSet` appliqué, le rétablissement ajoute une entrée à la chaîne ; un refus
`OPERATION_DEPASSEE` ou `CARTE_MODIFIEE` retire, dans une seconde unité, toutes les entrées de l'utilisateur sur la carte
(AR-05). Chaque réponse sur une carte porte l'historique de l'utilisateur sur elle (`HistoryView`).

Ports (`src/Topogenia.Application/Ports/`, interfaces et types de données seulement) :

| Port | Rôle | Implémenté par |
|---|---|---|
| `IUnitOfWork` | Transaction d'écriture d'une requête : `BeginAsync`, verrou de projet `LockProjectAsync` (`structure_version + 1`, spec 4.7), `CommitAsync` ; `RunAsync` (unité entière, rejouée sur interblocage, base occupée ou création concurrente par clé naturelle, ADR 030 et 033) ; sans validation, tout est annulé | P3-T04 |
| `ITrashPurge` | Candidats de la purge et suppression physique d'un plan du domaine en transactions courtes, avec les entrées du journal liées (spec 4.1, 4.6) | P3-T07 (`Persistence/Purge/TrashPurge`) |
| `IWorkspaceStore` | `LoadAsync` (espace d'un projet, avec tous ses compteurs et les groupes demandés par `WorkspaceContent`) et `SaveAsync` (écriture d'un `ChangeSet`, versions contrôlées) | P3-T04 (écriture), P3-T05 (chargement) |
| `IOperationLog` | Entrées du journal (`operation_log`) : séquence suivante d'une carte, entrées d'un utilisateur sur une carte, ajout, mise à jour, retrait par chaînes entières ; la pile relève de `OperationLog/` | P4-T01 (`Persistence/Writing/OperationLogStore`) |
| `IAccessReader` | Projet de l'objet nommé par une requête (projet, carte, couloir, étape, flèche, remontée ; trouvé même supprimé logiquement), état corbeille, rôle de l'utilisateur | P4-T01 (`Persistence/Loading/AccessReader`) |
| `ICurrentUser` | Utilisateur authentifié de la requête (identifiant, login, nom, langue effective, administrateur relu à chaque requête) | hôte, `Users/RequestUser` (P4-T05, P4-T06) |
| `IVersionRequirement` | Dit si la requête manque de la version (`If-Match`) que son mode exige : le mode Serveur l'exige sur toute route qui la lit, le mode Poste jamais (AR-07) ; les gabarits refusent alors 428 `VERSION_REQUISE` après les droits et avant le domaine (spec 5.3, A-2.16-04, ADR 045) | hôte, `Api/IfMatchRequirement` (`RequestVersions`, posé par le filtre des routes) |
| `IUserReader` | Lectures des utilisateurs (`app_user`) : par identifiant, par liste d'identifiants (les auteurs d'un export, P5-T01b), par login normalisé, par liste de logins, et profil avec les rôles dans les projets vivants (`GET /api/me`) ; dans la transaction de l'unité quand elle est ouverte (ADR 033), sinon en lecture cohérente (ADR 031) | `Persistence/Reading/UserReader` (P4-T02) |
| `IProjectReader` | Lectures qui traversent les projets : liste des projets vivants visibles (membre, ou tous pour un administrateur), corbeille, projets vivants ou en corbeille d'un code (unicité du code jusqu'à la purge, A-2.1-03) | `Persistence/Reading/ProjectReader` (P4-T02) |
| `IUserDirectory` | Annuaire des comptes (1.x `IAnnuaire`) : nom affiché et e-mail d'un login, hors base ; une panne vaut login inconnu, jamais un refus (A-2.1-25) | hôte : `Server/WindowsDirectory` (domaine, sinon comptes locaux) en mode Serveur, `Users/EmptyDirectory` (ne connaît personne : le login court sert de nom) en mode Poste |
| `IAdministrators` | Logins des administrateurs tels que la configuration les donne maintenant, relus au démarrage et à chaque changement du fichier (spec 5.3, A-2.1-26) ; le drapeau d'un utilisateur est aligné sur cette liste à sa connexion | hôte : `Users/ConfiguredAdministrators` (Serveur), `Desktop/DesktopAdministrators` (Poste : le compte du processus plus ceux de la configuration) |
| `IAuditStore` | Journal d'audit hors `ChangeSet` (spec 5.5, ADR 046) : ajout d'un événement seul (purge, refus répétés), lecture par un administrateur (`GET /api/admin/audit`, `AuditQuery`), retrait des événements plus vieux que la conservation ; jamais de mise à jour (ADR 032) | `Persistence/Reading/AuditStore` (P4-T07) |
| `IBackupService` | Sauvegarde en ligne du fichier du mode Poste (`BackupAsync`, `Daily`, `BeforeMigration`, résultat `BackupResult`) et restauration d'une sauvegarde, application arrêtée (`RestoreAsync`, résultat `RestoreResult` : fait, ou échec avec un code stable et un détail sans chemin, le fichier laissé tel quel ; ADR 029) ; `Cause` des deux résultats pour le diagnostic local seulement | P3-T03 (`Persistence.Sqlite.Backup.SqliteBackupService`) |
| `ISchemaMigrations` | État du schéma (`CheckAsync`), migration avec historique et empreintes (`MigrateAsync`), refus `SchemaRefusedException` et codes `SchemaCodes` (ADR 027) ; la panne du moteur derrière un refus est gardée en exception interne réduite à ses numéros, `EngineFailureException.Numbers` (jamais le message du moteur, qui nomme le compte, la base ou le fichier ; ADR 036), ou une `TimeoutException` qui ne donne que le délai (ADR 041) | P3-T02 (`Persistence.Schema.SchemaMigrations`) |
| `IClock` | Heure UTC du serveur (D08) | hôte, `Composition/SystemClock` ; horloge fixe dans les tests |
| `IPackageExporter` | Paquet d'échange d'un projet (spec 7.2) : utilisateurs qu'il nomme, octets du paquet, nom du fichier (A-1.2-16) ; l'Application ne voit jamais le format (ADR 051) | P5-T01 (`Interchange/Packages/PackageExporter`) |
| `IPackageImporter` | Lecture d'un paquet reçu (`Read` : garde-fous de la spec 5.4, montée de version, reconstruction d'essai ; rend le code, les logins, les membres et les comptes de l'aperçu) et reconstruction de ses enregistrements sous un code, avec les utilisateurs de l'import (`Build`, `ImportPlan`) ; le format reste caché (ADR 052, 053) | P5-T02 (`Interchange/Packages/PackageImporter`) |
| `IPmapFormat` | Fichier `.pmap` 1.1 : lecture d'un texte (`Read`, fautes `FileError`), document d'un projet pour la copie (`Outline`, fiches selon ce que voit le demandeur) ou pour la duplication (`Model`, tous les niveaux, AR-118), écriture, construction d'un nouveau projet par les règles du domaine (`Build`), nom du fichier (ADR 054) | P5-T04 (`Interchange/Pmap/PmapFiles`) |

Les dépôts de lecture de la spec 3.4 sont `IUserReader`, `IProjectReader` et, pour l'audit, `IAuditStore` ; les
lectures d'un seul projet passent par `IWorkspaceStore` et `QueryRunner`. `IUserDirectory` est défini (ci-dessus).

Les codes lisibles sont alloués par le domaine (`ProjectWorkspace.NextCode`), sur les
`ProjectCounter` de l'espace, sous le verrou de projet. Exigence : l'espace chargé contient **tous** les
compteurs du projet (la création d'un projet les écrit tous, à 0) ; un compteur absent est
recréé à 1 par `NextCode`, et sa clé (projet, genre) le refuse à l'écriture : un oubli de
chargement échoue, il ne redonne jamais un code. C'est le chargement de l'espace lui-même
(`Persistence/Loading/WorkspaceLoader.LoadAsync`, par `IWorkspaceStore.LoadAsync`) qui lit tous les compteurs du
projet. Le port `ICounterService` que nomme la spec 3.4 est retiré avec son adaptateur et
`WorkspaceLoader.CountersAsync` (constat m2 de la porte P4, AR-148) : l'hôte ne l'enregistrait pas et l'application ne
l'appelait pas ; les deux tests des compteurs (`Loading/WorkspaceLoaderTests`) passent par le chargement de l'espace.

## 4. Persistance

- Un `DbContext` commun (`AppDbContext`, ADR 028), deux jeux de migrations (SQL Server, SQLite), noms
  en `snake_case`, schéma technique figé `topogenia` en SQL Server (`Schema/TechnicalNames`, comme les espaces de
  noms : il ne suit pas le nom affiché du produit, ADR 013), sans préfixe en SQLite.
  Le modèle physique, table par table, est dans `docs/modele-donnees.md`.
- Les enregistrements immuables du domaine sont projetés tels quels (constructeurs, clés Guid données
  par l'application, version entière jeton de concurrence, listes fermées par leur code, dates UTC à
  pleine précision). Les index uniques partiels et les `CHECK` sont décrits une fois sans SQL
  (`Model/ModelGuards`) ; le parcours commun `Model/GuardWriter` (colonnes, décimaux, ordre des conditions) n'écrit
  aucun SQL, chaque fournisseur écrit les opérateurs de son moteur (`SqlServerGuards`, `SqliteGuards`, ADR 025, 039).
- Un moteur (`Schema/DatabaseEngine` : `SqlServerEngine`, `SqliteEngine`) donne les options du contexte,
  son catalogue et ses migrations ; `Schema/SchemaMigrations` implémente `ISchemaMigrations` : historique
  `schema_history` avec empreinte de la source et du schéma, écrit dans la transaction de chaque
  migration, et refus d'une base plus récente, d'une migration modifiée, d'un historique incomplet ou d'un
  schéma changé hors migrations (ADR 027, 037). Les échecs du démarrage sont traduits par le catalogue de chaque
  moteur (`SchemaCatalog.RefusalOf` : droits insuffisants, base inaccessible ou occupée, ADR 036). Les migrations ont
  leur propre délai (`DatabaseEngine.MigrationTimeout`, 10 minutes par défaut) porté par un contexte dédié
  (`DatabaseEngine.CreateMigrationContext`), le contexte ordinaire gardant le délai de sa chaîne de connexion
  (ADR 041).
- `Persistence.SqlServer` : rôles et vues `bi` (`SqlServerAccess`, ADR 032), empreinte complète de la base
  (`SqlServerSchemaCatalog`, ADR 037) ; sondes de test, classes internes ouvertes au seul projet des tests de
  persistance : `SqlServerAccessProbe` (utilisateurs sans connexion, emprunt d'identité, droits effectifs, objets et
  droits changés à la main) et `SqlServerOptionsProbe` (options de la base retirées à la main, enregistrement d'une
  migration refusé), ADR 037.
- `Persistence.Sqlite` : `Connection/` (réglages de chaque connexion par un intercepteur : `foreign_keys`,
  WAL, `busy_timeout`, `synchronous`, délai de commande de 1 s, collation décimale invariante ; sonde de test
  `SqliteSettingsProbe`), `Files/` (fichier de données et règle des partages et dossiers synchronisés sur le
  chemin résolu : `..`, noms courts, liens), `Backup/` (sauvegarde en ligne contrôlée par `integrity_check` et
  `foreign_key_check`, dossier contrôlé, rotation des fichiers d'avant migration, codes d'échec sans chemin),
  `Startup/` (démarrage du mode Poste : sauvegarde avant migration, migration, sauvegarde du jour) ; ADR 029, 035.
  Depuis la porte P3 : le tampon qui lie le fichier à son journal et les points de contrôle du moteur
  (`Connection/SqliteDataFileGuard`, lecture du journal `SqliteWalFile`, refus d'un journal étranger
  `SqliteJournalCheck` ; la fermeture d'une connexion n'échoue jamais), l'effacement physique (`secure_delete`),
  l'arrêt propre `SqliteEngine.CloseAsync`, la restauration (`Backup/SqliteRestore`, identité du fichier
  `Files/FileIdentity`), les reconstructions atomiques de tables des migrations (`AtomicRebuildSqlGenerator`,
  `SqliteTableRebuild`), la recherche de texte (`SqliteTextSearch`, ADR 038) et le verrou du contrôle du schéma
  (`SqliteSchemaCatalog.CheckLockAsync`, ADR 041) ; ADR 029, 035, 038, 041.
  Depuis la phase 4 (P4-T09, P4-T09b), dans `Files/` : `DataFileUse` dit si le fichier de données, son journal ou sa
  mémoire partagée est tenu ouvert par une connexion, ici ou dans un autre processus (ouverture sans partage, fichier
  par fichier) : la restauration exige qu'aucune connexion ne reste, et `--check` du mode Poste n'ouvre pas un fichier
  tenu par l'application lancée (ADR 029, 050) ; `FolderExposure` lit, sans jamais les changer, les règles d'accès du
  dossier des données ou des sauvegardes et le dit exposé quand `Everyone`, `Users` ou `Authenticated Users` peut lire
  ses données sans refus (faux hors de Windows ou si les règles sont illisibles) : le démarrage n'en fait qu'un
  avertissement (AR-98). La restauration garde le fichier refusé et son journal à côté avant de le remplacer (AR-96).
- `Persistence/Purge/` : suppression d'un plan de purge par unités de travail de 500 enregistrements au plus, sous
  le verrou de projet, avec garde du plan et entrées du journal liées (ADR 034).
- L'unité de travail applique un ChangeSet : créations, modifications, suppressions, avec le
  **stationnement en deux temps** des étapes dont la case change (ADR 016).
- Erreurs du moteur traduites en codes (`CONFLIT_UNICITE`, `CONFLIT_INTEGRITE`) ; nouvel essai
  sur interblocage, base occupée ou création perdue sur une clé naturelle (3 essais, 50 puis 100 ms, ADR 030
  et 033).
- Lectures dans une transaction cohérente (instantané SQL Server, transaction différée SQLite, ADR 031).
- Interface commune `Errors/EngineDialect` (`SqlServerDialect`, `SqliteDialect`) ; adaptateurs
  `Writing/UnitOfWork`, `Writing/ChangeSetWriter`, `Writing/OperationLogStore`, `Loading/WorkspaceStore`,
  `Loading/AccessReader` sur une `StoreSession` par requête (ADR 030).
- `Reading/` (phase 4) : `UserReader`, `ProjectReader` (lectures qui traversent les projets, projetées directement vers
  les réponses, spec 3.4) et `AuditStore` (ajout, lecture filtrée et retrait des événements d'audit, ADR 046), chacun
  en lecture cohérente ou dans la transaction de l'unité ouverte (ADR 031, 033).

## 4 bis. Échange (phase 5)

`Topogenia.Interchange` écrit et lit les fichiers d'échange ; il ne dépend que de l'Application et du domaine, et
l'Application l'appelle par ses ports (ADR 003, 051).

| Dossier | Contenu |
|---|---|
| `Packages/` | Le paquet `.topogenia` (spec 7.2, ADR 051) : `PackageFormat` (noms techniques figés, dérivés de `nameof(Topogenia)`), `ExportScope` (ce qui sort d'un espace : structure vivante, remontées ouvertes avec leur étape dissoute, jamais la corbeille ni le journal), `StructureEntries`, `ContentEntries`, `TranslationEntries` (une entrée par fichier, objets désignés par leurs codes), `People` (logins, ou aucun sans les personnes), `PackageWriter` (entrées canoniques, manifeste et SHA-256, ZIP), `ZipEntryTime` (date fixe des entrées, seule exception à D08), `PackageExporter` (adaptateur de `IPackageExporter`) |
| `Json/` | `CanonicalJson` : l'écriture déterministe (clés triées, LF, dates à sept décimales, décimaux sans zéros de fin) ; `JsonText` : chaque nom et chaque texte d'un document reçu (paquet, `.pmap`) lu une fois juste après l'analyse, une moitié de paire de substitution refusée comme du JSON illisible |
| `Packages/` (import) | `PackageReader` (garde-fous dans l'ordre de l'ADR 052 : archive, entrées, manifeste, tailles lues en flux, SHA-256, JSON sans clé en double, schéma), `PackageDefinition` et `PackageChain` (une version du format, ses entrées et son schéma ; la chaîne des `IPackageConverter` qui monte un paquet ancien d'une version à la suivante, chaque sortie revalidée, ADR 053), `ImportJson` (valeurs d'une entrée déjà validée), `PackageBuild` (reconstruction sous de nouveaux identifiants : références par code résolues, clé portée deux fois refusée), `PackageImporter` (adaptateur de `IPackageImporter`) |
| `Pmap/` | Le format `.pmap` 1.1 (ADR 054) : `PmapFormat` (nom, version, limites ; longueurs des textes prises au domaine), `PmapModel` (document lu), `PmapReader` (nettoyage, règles du fichier, fautes à leur place, 30 au plus), `PmapWriter`, `PmapOutline` (document d'un projet : codes pour identifiants, ordre de lecture, RACI visible, coupe au niveau demandé), `PmapBuild` (nouveau projet par `ProjectCreation`, `StructureEdits` et les règles des fiches, en brouillons), `PmapFiles` (adaptateur de `IPmapFormat`) |
| `Schemas/` | `SchemaValidator` (sous-ensemble de JSON Schema 2020-12, mots-clés inconnus refusés au chargement ; un nombre hors de la plage décimale est au-delà de toute borne) et `PackageSchema` (le schéma publié `schemas/topogenia-package-2.0.schema.json`, embarqué) |
| `Files/` | `ExportFileName` : nom d'un fichier exporté, règle 1.x `CODE_Nom-court_aaaa-mm-jj` |

Côté Application, `Exchange/PackageExport` lit le projet par `ExportPackageQuery` (gabarit des requêtes, animateur seul,
AR-113), lit les logins des auteurs (`IUserReader.FindManyAsync`) quand le paquet porte les personnes, fait écrire le
paquet et inscrit l'événement d'audit `project.exported` avant de rendre les octets ; côté hôte, `Api/Exchange/ExchangeApi`
et la route `POST /api/projects/{id}/export` (fichier `application/zip`).

`Exchange/PackageImport` porte les quatre requêtes de l'import (aperçu et import d'un nouveau projet, administrateur ;
aperçu et remplacement d'un projet, son animateur) ; elles lisent le paquet une fois, après leur droit (`PackageSource`).
`Exchange/ImportPreparation` est leur part commune : utilisateurs des logins, enregistrements reconstruits, contrôle de
chaque valeur par la règle de sa saisie (`Domain/Projects/ImportedRecords`, raison `values`), classification et membres
du projet remplacé (AR-117, AR-115), importateur animateur et règles de la structure (`ImportedProjects`), tout avant
l'unité qui écrit. `Exchange/PmapExchange` porte la copie `.pmap` (lecteur), l'ouverture d'un fichier et la duplication
(administrateur). Côté hôte, `Api/Exchange/ExchangeApi` et `PmapApi` ; le contrat OpenAPI décrit le corps binaire des
imports (`application/octet-stream`) et les fichiers rendus (D09). La ligne de commande `--export` et `--import`
(`Startup/ExchangeCommands`, ADR 055) passe par les mêmes requêtes, sous le compte qui la lance.

## 5. Hôte

| Mode | Base | Authentification | Écoute |
|---|---|---|---|
| Poste (`Desktop`) | SQLite | jeton de lancement de 256 bits échangé contre un cookie de session ; identité du processus | `127.0.0.1` seulement |
| Serveur (`Server`) | SQL Server | Windows (Negotiate) | IIS, HTTPS |

En-têtes de sécurité, limites de taille avant lecture du corps, contrôle `Host`, `Origin`,
`Sec-Fetch-Site`, en-tête anti-CSRF (`X-Requested-With`, AR-13) : spec 5.

Dossiers de `src/Topogenia.Host` :

| Dossier | Contenu |
|---|---|
| `Endpoints/` | Les points d'entrée HTTP seulement (ADR 043, spec 3.2) : `ApiRoutes.MapApi` pose les routes une fois, lot par lot (`ProjectEndpoints`, `MapEndpoints`, `StepEndpoints`, `SelectionEndpoints`, `SheetEndpoints`, `AdministrationEndpoints`, `SystemEndpoints`), avec le filtre `If-Match`, OpenAPI, la route inconnue sous `/api` (`INTROUVABLE`) et le repli des autres adresses vers la page de l'application. Chaque délégué lit la route, la chaîne de requête, `If-Match` et le corps, appelle un gestionnaire et rend `TypedResults` ; il ne contient aucune règle. |
| `Api/` | Les gestionnaires, un par fonctionnalité (`Api/<Fonctionnalité>/<Fonctionnalité>Api.cs`, enregistrés par leur nom en portée de requête) : corps (`record <Action>Body`) traduit en commande ou requête, lancée par son gabarit, réponse (`StructureReply` pour une commande sur une carte, AR-50) ; lectures hors du domaine (`ApiInput` : version de `If-Match`, champ absent `CHAMP_OBLIGATOIRE`, codes des chaînes de requête) ; `IfMatchRequirement` (routes qui lisent `If-Match`, 428 en mode Serveur) ; `UnknownRoute`. |
| `Composition/` | Construction de l'application (`HostApplication`, la même pour l'exécutable et les tests), services (`HostServices` : adaptateurs des ports, gabarits, gestionnaires), ordre des intergiciels (`HostPipeline`), configuration (`ProductOptions`, section nommée d'après le produit), horloge (`SystemClock`). |
| `Desktop/`, `Server/` | Les deux modes (ADR 044, 045) : écoute, session et jeton de lancement, compte du processus, fichier de données, sauvegardes et restauration proposée (`DesktopRecovery`, AR-96), réglages utilisateur ; contrôle de la base SQL Server au démarrage (jamais migrée par l'hôte), Negotiate, noms autorisés, HTTPS et HSTS, annuaire Windows. |
| `Security/` | `RequestGuard` (`Host`, puis sur une écriture l'en-tête anti-CSRF, `Origin`, `Sec-Fetch-Site`), `BodySizeLimit` (1 Mo par défaut, avant toute lecture), `SecurityHeaders` (CSP et en-têtes de toute réponse), `HttpsOnly`. |
| `Errors/` | `ErrorMiddleware` (tout échec en `{code, message, details}` dans la langue effective), `ErrorResponses` (statut de chaque refus), `ErrorMessages` (messages par langue), `RefusalWatch` (refus d'accès d'un utilisateur comptés sur une fenêtre glissante jusqu'à l'événement d'audit, spec 5.5). |
| `Users/` | `RequestUser` (utilisateur de la requête, `ICurrentUser`, connecté par `UserRunner.SignInAsync`), `ConfiguredAdministrators`, `EmptyDirectory`. |
| `Json/`, `Logging/` | JSON de l'API (snake_case, codes des listes fermées, `Optional<T>` des PATCH, liste sans élément nul) ; journal par jour en fichier (30 jours) et une ligne par requête (`RequestLog`, utilisateur par identifiant). |
| `Startup/` | `StartupService` : ce que l'hôte fait avant la première requête et après la dernière (contrôles du mode, ouverture du fichier de données ou contrôle de la base, avertissement non bloquant des dossiers exposés, AR-98, arrêt propre `SqliteEngine.CloseAsync`) ; `HostCommands` : la ligne de commande `--migrate` (mode Serveur seulement) et `--check` (configuration puis base, les deux modes), l'application construite comme pour une exécution sans jamais démarrer le serveur, codes de sortie 0 réussite, 1 problème, 2 usage (ADR 050) ; `StartupConsole` et `messages.*.json` (phrases de la console dans la langue de l'utilisateur). |
| `Maintenance/` | `MaintenanceService` : service de l'hôte lancé au démarrage puis toutes les 24 h (A-2.15-19) : purge de la corbeille au-delà de sa conservation (`ExpiredPurge`), puis retrait des événements d'audit plus vieux que `AuditRetentionDays` (365 jours par défaut, spec 5.5) ; une panne est journalisée par son type et n'arrête ni l'hôte ni le tour suivant (ADR 046). |

## 6. Interface

React 19, TypeScript strict, `@xyflow/react`, zustand, i18next. Les commandes sont des
données (une définition par action pour le ruban, les menus et le clavier) ; un seul
gestionnaire de clavier à couches ; le magasin de la carte envoie `undo` / `redo` au serveur
et applique la vue renvoyée. Jetons de la charte dans `web/src/styles/tokens.css`, teintes des
couloirs générées depuis `web/src/shared/theme/laneTints.ts` (AR-36).

**Accès à l'API (`web/src/api/`, P6-T01, ADR 056).** Le contrat est le document OpenAPI que sert l'hôte, figé dans
`generated/openapi.json` et comparé au document servi par `ApiContractTests` sur les deux bases ; il porte le schéma
`ErrorBody` (`Json/ErrorSchemaTransformer`), dont le code est l'un de ceux du catalogue des messages. `npm run api`
en génère `generated/schema.ts` (openapi-typescript) et `generated/exchanges.ts` (opérations dont la réponse ou le corps
n'est pas du JSON) ; un test vitest vérifie qu'ils sont à jour et qu'aucun type d'API n'est écrit à la main.
`request.ts` est le seul `fetch` vers l'API (les polices de l'application, lues pour les exports, passent par un
`fetch` direct dans `features/map/export/capture.ts` et `pdfFonts.ts`) : `credentials: 'same-origin'`, `X-Requested-With` sur les écritures, `If-Match` depuis
une version, `Accept-Language`, erreurs normalisées en `ApiError` (`errors.ts` : codes de l'hôte typés par le contrat,
`RESEAU`, `REPONSE_INVALIDE`, `HTTP_<statut>`). `client.ts` est la façade typée (`api.patch('/api/steps/{id}', ...)`) :
route, valeurs, corps et réponse viennent du contrat, et `version` est exigée sur chaque route qui lit `If-Match`.

**Organisation (`web/src/`, P6-T02 à P6-T11, ADR 057 à 070).**

| Dossier | Rôle |
|---|---|
| `app/` | Enveloppe : routeur sans bibliothèque (`router.ts`, chemins et `navigate`), en-tête et barre de l'éditeur (`EditorBar.tsx` : fil d'Ariane, classification, Fichier, Membres), menu du compte et « Quitter » du mode Poste (`AccountMenu.tsx`, `quit.ts`), profil (`profile/`), page d'erreur |
| `shared/` | Ce que plusieurs fonctionnalités emploient : `i18n/` (un fichier par langue, `localizedName`, `formatDate`), `keyboard/` (le seul écouteur de touches, à couches champ > modal > panneau > canevas), `commands/` (commandes en données), `ui/` (bouton, menu, barre, dialogue, notifications, modalité de saisie), `state/openProject.ts` (le projet de la carte affichée, lu par l'en-tête), `theme/` (teintes des couloirs, AR-36) |
| `features/home/` | Accueil, corbeille, projet d'exemple du mode Poste (ADR 058, 069) |
| `features/project/` | Création, membres, propriétés du projet (classification, groupe de partage), fichiers `.pmap`, paquet d'échange (ADR 058, 069) |
| `features/map/` | La carte : `model/` (modules purs : mise en page, ordre de lecture, RACI, cadre, presse-papiers ; `geometry/` : grille, vue bornée, routage des flèches, ADR 059), `state/` (magasin de la carte par éditeur, commandes en données, opérations `actions/`), `canvas/`, `nodes/`, `edges/` (React Flow), `chrome/` (ruban, en-têtes des couloirs), `panels/` (menus, dialogues, RACI, reliquat), `export/` (PNG, PDF, classeur, ADR 066) |
| `features/sheet/` | La fiche d'étape, indépendante de la carte, branchée par `map/panels/SheetDock.tsx` (ADR 064) |
| `features/translations/` | Classeur des traductions, consigne et glossaire, traductions manuelles (ADR 065) |
| `styles/` | Feuilles de style aux jetons de `tokens.css` seulement |
| `test/` | Aides des tests (cartes, fiches, API simulée, PDF, cartes tirées au hasard, routage) et tests des règles de l'interface |

**Sens des dépendances.** `shared/` ne connaît ni `app/` ni aucune fonctionnalité. `app/` monte les écrans
(`App.tsx` : accueil, nouveau projet, membres, carte) et, dans l'en-tête, les propriétés du projet
(`features/project/properties/`) ; il ne connaît rien d'autre des fonctionnalités. Les fonctionnalités emploient
`shared/`, le routeur (`app/router.ts`), le profil (`app/profile/ProfileProvider.tsx`) et, pour la carte, la page
d'erreur (`app/ErrorPage.tsx`) ; `features/home` emploie `project` (dialogues d'ouverture d'un `.pmap` et d'import d'un
paquet) ; `features/map` emploie `project` (dialogues du menu Fichier, propriétés), `sheet`
(le panneau de la fiche) et `translations` ; aucune autre fonctionnalité n'emploie `map`. Les types de l'API viennent
du seul contrat généré.

**États.** zustand : un magasin de carte par éditeur (`map/state/mapStore.ts`, lecture annulée par la suivante),
l'éditeur (`map/state/editor.ts` : sélection, outil, fiche ouverte, file des écritures sans mise à jour optimiste, refus
affiché, 409 relu), le projet ouvert (`shared/state/openProject.ts`), les propriétés du projet
(`project/properties/projectProperties.ts`), les files d'écriture en attente qu'envoie « Quitter »
(`shared/state/pendingWrites.ts`, AR-143) ; préférences de l'utilisateur dans `localStorage` sous les clés `app.*`
(`map/state/preferences.ts`, et la largeur des volets de droite dans `map/state/dock.ts`, ADR 086). Annuler et rétablir passent toujours par l'hôte (ADR 062).

**Tests.** vitest sur jsdom : unitaires, composants et propriétés du routage sur 500 cartes d'une graine fixe ; tests
des règles de l'interface dans `test/` (textes en dur, caractères d'icône, langues, disposition, une définition par
règle, feuilles de style, code mort, `data-testid`, clés des fichiers de langue). Parcours Playwright contre le vrai hôte dans les deux modes
(`web/e2e/`, lanceur `tests/Topogenia.EndToEnd.Host`, ADR 067) ; mesures de durée à part (`web/e2e/measures/`,
`E2E_MEASURE=1`) ; démonstration rejouée par `npm run demonstration` (`web/scripts/demonstration/`).
