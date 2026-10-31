<!-- consignes-agents-persistance.md -->
<!-- Common instructions for the agents that write the persistence of phase 3 -->

# Consignes communes aux agents de la persistance (phase 3)

## À lire d'abord

- Le prompt `C:\Temp\Claude\PROJETS\Encours\45_Topogenia\PROMPT_CONCEPTION.md` (sections 3 à 6 : forme,
  interdits, environnement, méthode).
- La spécification `C:\Temp\Claude\PROJETS\Encours\45_Topogenia\Specification\TOPOGENIA_2.0_SPECIFICATION_COMPLETE.md` :
  3.1 et 3.2 (organisation et dépendances), 3.4 (Application et ports), 4 en entier (données, garde-fous,
  pièges de portabilité, migrations, lots, journal, sérialisation), 5.2 (sauvegardes du mode Poste),
  5.5 (audit), 6.3 (ligne de commande), 8 (exigences non fonctionnelles), annexe A sections 4, 5 et 6.
- `docs/plan.md` (phase 3), `docs/architecture.md` (§2 domaine, §3 Application, §4 Persistance),
  `docs/decisions/` (en particulier ADR 002, 006, 007, 008, 015, 016, 018, 023, 024, 025 et les AR),
  `docs/couverture.md` (lignes de phase 3 : colonne Phase contenant 3).
- Le modèle du domaine, en lecture : `src/Topogenia.Domain/Model/*.cs` (enregistrements immuables, `KeyOf`),
  `src/Topogenia.Domain/Workspace/*` (espace de projet, `ChangeSet`, `ExpectedState`).
- Le schéma et le code 1.x, en LECTURE SEULE : `C:\Temp\Claude\PROJETS\Encours\43_Outil_Collaboratif\carto\db\`
  et `carto\api\` (le code 1.x fait foi sauf correction de la spec ou arbitrage).

## Règles

- Le domaine ne change pas pour la persistance : s'il manque quelque chose, signale-le (fichier, besoin)
  au lieu de modifier `src/Topogenia.Domain`, sauf mention contraire dans ta mission.
- Bases SQL Server : instance `.`, authentification Windows. Tu ne crées, n'écris et ne supprimes que des
  bases dont le nom commence par `Topogenia_` (`Topogenia_Dev`, `Topogenia_Test_*`, `Topogenia_E2E`). Jamais
  `Ariane`, `Ariane_*`, `ARIANE_ESSAI`, `CARTO*` ni aucune autre base.
- Fichiers SQLite : seulement dans le scratchpad ou dans un dossier temporaire créé par le test, supprimé par
  le test lui-même.
- Tests : xUnit v3 ; matrice par théories paramétrées (`Sqlite`, `SqlServer`) ; une base
  `Topogenia_Test_<n>` recréée par exécution et supprimée à la fin (ADR 018). Retire
  `--ignore-exit-code 8` du projet de tests dès qu'il a des tests (un test d'architecture l'exige).
- Forme : code, identifiants et commentaires en anglais ; en-tête de deux lignes (nom du fichier, puis
  description) ; UTF-8 sans BOM, LF (avec python : `open(..., newline='\n')`, pas d'antislash dans les
  heredocs) ; fichier de 600 lignes au plus, méthode de 80 au plus ; aucun `TODO`, aucun emoji, aucune
  suppression d'avertissement ; 0 avertissement. Les règles d'architecture (`tests/Topogenia.Architecture.Tests`)
  doivent rester vertes : le SQL brut n'est permis que dans les fournisseurs (ADR 025).
- Paquets : seulement ceux déjà centralisés dans `Directory.Packages.props` ou leurs compagnons officiels
  EF Core 10.0.13 (`Microsoft.EntityFrameworkCore.Design` en outil de projet si nécessaire, jamais un outil
  global) ; version exacte, fichiers de verrouillage à jour. Aucune installation sur le poste.
- Ne jamais affaiblir un test. Un test rouge se corrige dans le code, ou l'écart est documenté dans
  `docs/decisions/`.
- Build et tests avec ton propre `--artifacts-path` dans le scratchpad, et
  `DOTNET_CLI_TELEMETRY_OPTOUT=1 TESTINGPLATFORM_TELEMETRY_OPTOUT=1`.
- Couverture : ne modifie pas `docs/couverture.md` ; écris `docs/couverture-p3/<ta-tâche>.md`
  (`| ID | État | Test 2.0 | Remarque |`).
- Rien dans `43_Outil_Collaboratif` ; aucun commit, aucun envoi sur Internet.
