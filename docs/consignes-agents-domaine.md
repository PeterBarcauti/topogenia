<!-- consignes-agents-domaine.md -->
<!-- Common instructions for the agents that write the domain rules of phase 2 -->

# Consignes communes aux agents du domaine (phase 2)

## À lire d'abord

- `docs/architecture.md` (§2 : espace de projet, ChangeSet, annulation).
- Le noyau, à NE PAS modifier : `src/Topogenia.Domain/Errors/`, `Values/`, `Model/`,
  `Workspace/`, `Structure/GridRules.cs`, `Structure/StructureEdits.cs`, `ProductIdentity.cs` (dans l'application depuis P7-T04b),
  et `tests/Topogenia.Domain.Tests/Support/WorkspaceBuilder.cs`. S'il te manque quelque chose
  dans le noyau, écris-le dans TON dossier (classe `internal` à toi) et signale-le dans ta
  réponse ; ne modifie pas le noyau.
- Ta part de l'annexe A de la spécification
  (`C:\Temp\Claude\PROJETS\Encours\45_Topogenia\Specification\TOPOGENIA_2.0_SPECIFICATION_COMPLETE.md`,
  lignes 913 à 1590), les lignes correspondantes de `docs/couverture.md` (IDs `A-x.y-nn`), les
  arbitrages `docs/decisions/000-arbitrages-phase-0.md`, et les tests 1.x correspondants de
  `docs/portage-tests.md` (lignes `T-...`).
- Le code et les tests 1.x, en LECTURE SEULE :
  `C:\Temp\Claude\PROJETS\Encours\43_Outil_Collaboratif\carto\api\` et `api.Tests\`. En cas de
  doute sur une règle, le code 1.x fait foi (spec 0.1-3), sauf correction décidée par la spec
  (sections 4.6, 5.6, 13) ou par un arbitrage.

## Comment écrire une règle

- Une opération du domaine est une méthode statique qui reçoit l'espace
  (`ProjectWorkspace`), le contexte (`OperationContext`) et une demande, applique la règle en
  lisant et remplaçant des enregistrements (`workspace.Put(record with { ... })`,
  `workspace.SoftDelete(...)`, `workspace.Remove(...)`), et renvoie un résultat (identifiants,
  codes, avertissements). Elle ne fait aucune entrée/sortie, ne lit pas l'horloge
  (`context.Now`), ne crée d'identifiant que par `context.NewId()`.
- Un refus est une `DomainException` avec un code de `ErrorCodes` (jamais un message).
  Les avertissements sont des codes de `WarningCodes`, renvoyés comme données.
- Les textes saisis passent par `TextRules` ; les listes fermées par `Codes.Parse<T>` ; les
  valeurs saisies par leur objet valeur de `Values` (ADR 026 : `LaneColor.Parse`,
  `ProjectCode.Parse`, `Login.Parse`, `Duration.Parse`, `DocumentLocation.Parse`...) ; les
  cases par `GridRules` ; les traductions par `TranslationRules`.
- Ne pas gérer les versions ni l'audit de mise à jour : `workspace.Commit(context)` s'en
  charge. Les droits (rôle de l'utilisateur) relèvent de l'Application (phase 4), PAS du
  domaine ; le domaine vérifie la cohérence des données (par exemple : l'acteur d'un couloir
  doit être membre du projet, à partir des `ProjectMember` de l'espace).
- Pas de SQL, pas de `Guid.NewGuid`, pas de `DateTime.Now`, pas d'accès disque ni réseau.

## Où écrire

- Code : seulement dans le ou les dossiers de `src/Topogenia.Domain/` qui te sont attribués
  (l'espace de noms suit le dossier : `Topogenia.Domain.<Dossier>`).
- Tests : seulement dans `tests/Topogenia.Domain.Tests/<Dossier>/`. Tu peux ajouter des aides
  de construction dans `tests/Topogenia.Domain.Tests/<Dossier>/` (jamais dans `Support/`).
- Couverture : NE MODIFIE PAS `docs/couverture.md`. Écris
  `docs/couverture-p2/<Dossier>.md` : un tableau `| ID | Test 2.0 |` qui relie chaque ligne
  `A-*` de ta part que tu couvres au nom complet du test
  (`Topogenia.Domain.Tests.<Dossier>.<Classe>.<Méthode>`), plus une liste des lignes de ta part
  que tu ne couvres pas, avec la raison (par exemple « relève de l'Application, phase 4 »).

## Forme (vérifiée par `tests/Topogenia.Architecture.Tests`)

- Code, identifiants, commentaires et noms de tests en anglais (aucun mot français dans les
  identifiants : `Lane`, pas `Couloir`) ; commentaires sans accent ni phrase française.
- En-tête de chaque fichier : `// NomDuFichier.cs` puis `// description en anglais` (au moins
  trois mots). Pas d'auteur ni de date.
- Fichier de 600 lignes au plus (cible 400), méthode de 80 lignes au plus (cible 40).
- Aucun marqueur de travail (TODO...), aucun `NotImplementedException`, aucun emoji,
  aucune suppression d'avertissement. 0 avertissement de compilation (analyseurs en erreur).
- Noms de tests : une phrase en anglais avec des soulignés, qui dit la règle
  (`Dropping_on_an_occupied_cell_shifts_only_the_contiguous_block`).
- Un test vérifie le comportement (valeurs, codes, états), pas seulement l'absence d'erreur.
  Porte l'INTENTION des tests 1.x de ta part (voir `portage-tests.md`), en anglais.

## Construire et tester sans gêner les autres agents

Plusieurs agents travaillent en même temps : construis TOUJOURS dans ton propre dossier de
sortie, sinon les fichiers `obj/bin` se bloquent mutuellement :

```
set DOTNET_CLI_TELEMETRY_OPTOUT=1 & set TESTINGPLATFORM_TELEMETRY_OPTOUT=1
dotnet test --project tests/Topogenia.Domain.Tests --artifacts-path <scratchpad>\build-<ton-dossier>
dotnet test --project tests/Topogenia.Architecture.Tests --artifacts-path (même dossier)
```

(sous bash : `export DOTNET_CLI_TELEMETRY_OPTOUT=1 TESTINGPLATFORM_TELEMETRY_OPTOUT=1`). Tes
tests doivent être verts ; les tests d'architecture aussi. Si un test d'un AUTRE agent est
rouge, ne le corrige pas : signale-le.

## Interdits

Rien dans `43_Outil_Collaboratif` ; aucune base de données ; aucune installation ; aucun
affaiblissement de test.
