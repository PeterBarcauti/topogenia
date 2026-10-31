<!-- 042-gabarit-de-commande-et-pile-du-journal.md -->
<!-- Gabarit unique des commandes (droits déclaratifs, verrou, If-Match, journal, vue) et pile du journal par chaînes -->

# 042 - Gabarit de commande et pile du journal

Statut : acceptée (date 24/10/2026)

## Contexte

La spec 3.4 impose un même gabarit à toute commande : autorisation déclarative, unité de travail et verrou de projet,
chargement, domaine, contrôle `If-Match`, écriture, inscription au journal, réponse avec la vue de la carte. En 1.x,
les droits sont dispersés (`Droits.Exiger`, `EstAdmin`, `PeutRemplir`, annexe A 6-7) et l'annulation est pilotée par
le client. Trois points restaient ouverts :

1. où lire les droits et dans quel ordre refuser (404 du non-membre, 403, administrateur) ;
2. la forme du journal : la spec 4.6 donne `forward_json`, `inverse_json`, `touched`, `redone_from_id`, sans dire
   comment un rétablissement s'inscrit ni comment la pile se vide sans casser la clé étrangère `redone_from_id`
   (sans cascade, `docs/modele-donnees.md` 6.22) ;
3. la vue de la carte rendue par chaque commande (AR-50) : une seconde lecture, ou l'espace déjà chargé.

## Décision

### Gabarit (`Application/Commands/CommandRunner`)

- Une commande est une classe `ProjectCommand<TResult>` qui déclare **un seul** attribut de droit
  (`RequiresRole(rôle minimal)`, `RequiresSheetFill`, `RequiresAdmin`) ; sans attribut, le gabarit refuse de
  l'exécuter (erreur de programmation). Elle dit ce qu'elle vise (`AccessTarget`), ce qu'elle charge, son genre de
  journal (`LogKind`, nul pour une commande non annulable), les versions lues par le client (`VersionCheck`) et la
  carte de sa vue (`MapOf`) ; elle n'appelle que le domaine.
- Ordre fixe : utilisateur authentifié (`NON_AUTHENTIFIE`) ; commande d'administrateur refusée à tout autre avant
  toute lecture (`ACCES_REFUSE`) ; dans l'unité de travail, accès au projet de la cible (`IAccessReader`, objet
  trouvé même supprimé logiquement) : projet absent, dans le mauvais état (vivant ou corbeille) ou non-membre →
  `INTROUVABLE` (404, le projet n'est pas révélé) ; rôle insuffisant → `ACCES_REFUSE` ; un administrateur agit
  en animateur (`Droits.cs:30`) ; puis verrou (`structure_version + 1`), chargement (contenu de la commande et de la
  vue), droit de remplir sur l'étape vivante (`SheetRights`, AR-01), `If-Match` (`CONFLIT_VERSION`, auteur et heure du
  dernier changement, ou de la création), domaine, `Commit`, écriture, journal, vue.
- Le travail est rejoué en entier par l'unité après un interblocage (ADR 030) : un nouveau contexte d'opération, donc
  de nouveaux identifiants, à chaque essai.
- L'identifiant de l'entrée du journal est celui de l'opération (`OperationContext.OperationId`), que portent les
  lots qu'elle crée (`deletion_batch.operation_id`) : la purge d'un lot retrouve son opération (spec 4.6, ADR 034).

### Pile du journal (`Application/OperationLog`)

- Une **chaîne** est une opération logique : l'entrée écrite par la commande (sa racine) et les entrées de ses
  rétablissements, qui pointent toutes vers la racine (`redone_from_id`), jamais vers un rétablissement. Seule la
  dernière entrée d'une chaîne (plus haute séquence) dit son état : vivante, l'opération s'annule ; annulée, elle se
  rétablit. Les entrées plus anciennes restent tant que la chaîne reste dans la pile : la purge d'un lot atteint ainsi
  chaque rétablissement de son opération (`OperationLogLinks`), et aucune entrée ne pointe vers une entrée retirée.
- La **place** d'une chaîne est la séquence de sa racine, quel que soit le nombre de ses rétablissements. Annuler prend
  la chaîne vivante de plus haute place ; rétablir prend la chaîne annulée de plus basse place (la dernière annulée,
  puisque l'annulation descend la pile) ; un rétablissement annulé de nouveau garde sa place (révision du 25/10/2026).
- Toute nouvelle opération retire la pile de rétablissement de l'utilisateur sur la carte et les plus anciennes
  chaînes au-delà de 50, **chaînes entières** ; puis l'entrée est ajoutée avec la séquence suivante de la carte, prise
  après ces retraits.
- `forward_json` : le `ChangeSet` de l'opération, chaque enregistrement entier avant et après, nommé par le type de sa
  clé (`step`, `lane`... : noms techniques de `RecordKey`, insensibles au renommage d'une classe), listes fermées par
  leur nom, dates UTC à pleine précision (`ChangeSetJson`). `inverse_json` vaut `[]` tant que l'opération n'est pas
  annulée ; l'annulation y écrira le `ChangeSet` qu'elle a réellement appliqué, réparations comprises, dont le
  rétablissement a besoin (`UndoReplay.Redo`, P4-T03). `touched` : type, clé et version après l'opération.

### Annuler et rétablir (`Application/OperationLog/UndoRedoCommands`, P4-T03)

- `UndoCommand` et `RedoCommand` passent par le même gabarit (droit d'animateur sur la carte, verrou, chargement,
  vue) ; trois points d'extension de `ProjectCommand` leur suffisent : `PrepareAsync` lit la pile de l'utilisateur
  sur la carte après le verrou, `LogAsync` écrit leurs propres entrées, `LogsRefusal` / `LogRefusalAsync` gardent la
  trace d'un refus.
- Annuler : `UndoReplay.Undo` sur l'aller de la dernière entrée de la chaîne ; l'entrée est marquée annulée et reçoit
  dans `inverse_json` le `ChangeSet` de l'annulation. Rétablir : `UndoReplay.Redo` sur l'aller et l'annulation de la
  dernière entrée ; une nouvelle entrée de la chaîne (`redone_from_id` = racine) garde le `ChangeSet` du
  rétablissement ; la pile de rétablissement n'est pas vidée par lui. `operation_id` de la réponse : la racine de la
  chaîne, l'opération que l'utilisateur voit.
- Pile vide : `INTROUVABLE` (404, rien à annuler ou à rétablir ; `LOT_INTROUVABLE` n'est plus levé, AR-43).
- Refus `OPERATION_DEPASSEE` ou `CARTE_MODIFIEE` : l'unité refusée est annulée en entier (AR-69), puis une seconde
  unité, sous le verrou du projet, retire toutes les entrées de l'utilisateur sur la carte (AR-05), et le refus est
  rendu. Un refus de droit ou une pile vide ne touche à rien : il tombe avant le verrou, ou ne laisse rien à retirer.
- Chaque réponse d'une commande sur une carte porte l'historique de l'utilisateur sur cette carte (`HistoryView` :
  ce qu'il peut annuler et rétablir, le prochain d'abord, genre et heure), lu dans la même transaction : la pile
  survit au rechargement et au passage dans une sous-carte (annexe B 2.6).

### Vue de la carte (`Application/Maps/MapViewBuilder`)

La vue est construite sur l'espace que la commande vient d'écrire, dans la même transaction, par les règles du
domaine (`ReadingPaths`, `SheetVisibility`, `MapMarkers`, `LayoutWarnings`, `RaciRules`, `Frames`) ; la lecture d'une
carte (P4-T02) emploie le même constructeur. Le chargement ajoute les fiches, les documents et les traductions.

## Conséquences

- Plus aucun contrôle de droits dans un gestionnaire ; un nouveau droit est un nouvel attribut lu par `AccessPolicy`.
- Une seule aller-retour réseau par commande ; le chargement est plus large (volumes faibles, 5 000 étapes au plus).
- Les numéros de séquence retirés au sommet de la pile peuvent être repris : ils ne servent qu'à ordonner les
  entrées d'une carte, et l'index unique `(map_id, sequence)` reste tenu.
- Le port `IOperationLog` change avant toute implémentation : `NextSequenceAsync`, `EntriesAsync` (toutes les
  entrées d'un utilisateur sur une carte) remplacent `LatestAsync(limit)` ; `RemoveAsync` retire des chaînes entières.

## Vérification

- `tests/Topogenia.Application.Tests` : `CommandRightsTests`, `CommandTemplateTests`, `OperationStackTests`,
  `ChangeSetJsonTests`, `MapViewBuilderTests` ; mutations du gabarit (contrôle `If-Match`, droit de remplir, verrou,
  identifiant d'opération, administrateur, état corbeille, vidage de la pile, profondeur, propriétés calculées) : 9
  jouées, 9 tuées.
- `tests/Topogenia.Persistence.Tests/Commands/CommandTemplateStoreTests` sur SQLite et SQL Server : écriture,
  journal, 404 sans verrou, `If-Match` périmé, retrait de chaînes sous la clé étrangère, accès d'un objet supprimé et
  d'un projet à la corbeille.
- `UndoRedoTests` (application) et `UndoRedoStoreTests` (deux bases) : ordre de la pile, chaînes, pile vide,
  pile d'un autre utilisateur, pile vidée par une nouvelle opération, refus `OPERATION_DEPASSEE` et `CARTE_MODIFIEE`
  qui vident la pile de l'utilisateur seul, unité rejouée, historique dans la réponse ; mutations : 8 jouées, 7 tuées,
  1 équivalente (un refus de droit tombe avant le verrou).

## Révision (25/10/2026) : place d'une chaîne (porte P4, round 1, Données B1)

Le texte d'origine ordonnait les chaînes par la séquence de leur dernière entrée. Le round 1 a montré qu'un
rétablissement, qui écrit une entrée plus récente, déplaçait alors l'opération en haut de la pile : annulée de nouveau,
elle n'était plus la prochaine rétablie. La place d'une chaîne est donc la séquence de sa racine
(`OperationChain.Place`), et `Chains`, `Undoable` et `Redoable` ordonnent par cette place. Test :
`Topogenia.Application.Tests.OperationLog.UndoRedoTests.A_redo_undone_again_keeps_its_place_and_is_the_next_one_redone`.
