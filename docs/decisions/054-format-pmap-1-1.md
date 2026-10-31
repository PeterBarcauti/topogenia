<!-- 054-format-pmap-1-1.md -->
<!-- Format .pmap 1.1 : lecture, écriture, bloc de fiche, erreurs par code, routes d'ouverture, de copie et de duplication -->

# 054 - Format `.pmap` 1.1

Statut : acceptée (date 25/10/2026)

## Contexte

Le `.pmap` est le format léger, lisible et écrivable par une IA (D11, spec 7.5, annexe A 2.17). La 2.0 le reprend avec les
compléments demandés : traductions des étapes et des flèches (perdues en 1.0 à la copie et à la duplication), refus d'un
RACI sur un début, une fin ou un losange ET / OU inclusif, et, par décision du porteur du projet (AR-93), un bloc de fiche
facultatif par étape importé en brouillon. Seule la version 1.1 est lue : Ariane n'a jamais été publiée (AR-93, A-2.17-50).
Questions laissées ouvertes : la forme du bloc de fiche, la place des messages d'erreur (la 1.x les écrivait en dur dans
`FormatPmap.cs`, faiblesse A-3-72), la visibilité des fiches à l'export, les routes et leurs limites.

## Décision

### 1. Le format

- Racine `{format, version, generated_by, generated_at, $schema, project, map, uncertainties}` ; `format` vaut le nom du
  produit en minuscules, dérivé de l'identifiant racine de la solution comme les noms du paquet (ADR 013, 051 : renommer
  le produit affiché ne rend pas illisibles les fichiers écrits) ; `version` vaut `1.1`, toute autre version est refusée
  avec la version lue.
- Mêmes propriétés et mêmes règles que la 1.x (annexe A 2.17) ; s'y ajoutent, toutes facultatives :
  `steps[].translations` et `flows[].translations` (`{langue: texte}`, hors langue du projet), `steps[].sheet`.
- Bloc `sheet`, chaque champ facultatif, noms de l'API des fiches :
  `actor`, `purpose`, `tools` (4 000), `frequency` (liste fermée de l'annexe A 2.10), `duration_minutes`,
  `procedure` (50 lignes `{text, tool?, nature?}`), `free_text` (20 000), `outputs` (documents produits
  `{id?, name, location?, share_confirmed?}`), `inputs` (entrées extérieures `{id?, name, location?, share_confirmed?,
  wait_hours?}`), `uses` (sorties d'autres étapes de la même carte que l'étape utilise : `{document, wait_hours?,
  percent_complete_accurate?, pain_point?, lost_hours_per_month?}`, `document` l'identifiant d'une sortie dans le
  fichier). L'identifiant d'un document suit la règle des autres (1 à 64 caractères, unique dans tout le fichier) ; il
  n'est demandé qu'à une sortie utilisée : le nom ne suffirait pas, deux sorties d'une étape pouvant porter le même nom.
  L'export l'écrit `<code de l'étape>-S<n>` (`VTE-E003-S1`). Le bloc se contrôle par les règles mêmes de l'API (`SheetContentReader`, `DocumentRules`,
  `DocumentUses`) : un seul jeu de règles (A-2.18-08) ; une valeur refusée devient une erreur du fichier à son
  emplacement, avec le code de l'API (`VALEUR_INVALIDE`, `TEXTE_TROP_LONG`...).
- Un bloc vide (`{}`) ou dont rien ne reste après nettoyage ne crée rien.

### 2. L'import : en brouillon, jamais validé

- La fiche devient le brouillon de l'étape (état `draft`), sans version validée ; ses documents et usages sont créés
  comme par la fiche (visibles de tous les membres, comme dans l'application) ; l'irritant d'un usage naît sur l'étape
  productrice (A-2.11). Tout le reste suit la 1.x : `code` et `name` de la demande l'emportent, un nom changé perd les
  traductions du projet, codes lisibles attribués dans l'ordre de lecture (colonne, couloir, rangée), le créateur seul
  animateur, compteurs et incertitudes rendus, audit `project.imported` (`{"mode":"pmap"}`).
- Les règles de la structure passent par le domaine (`StructureEdits`, `RaciRules`, `TranslationRules`,
  `ProjectCreation`) et l'espace construit passe `ImportedProjects.Violations` avant d'être écrit : un fichier ne peut
  rien écrire que l'interface ne puisse écrire.

### 3. L'export (« Télécharger une copie ») et la duplication

- `GET /api/projects/{id}/pmap?with_sheets=` (lecteur) : `with_sheets` facultatif, faux par défaut (AR-93 : les fiches
  sur option, désactivée par défaut ; une copie sans fiches ne fait sortir aucune donnée de plus que la carte). Avec
  l'option, chaque fiche exportée est **celle que le demandeur voit** (`SheetVisibility.VisibleContent` : brouillon pour
  l'animateur et la personne qui remplit, sinon la dernière version validée) : un lecteur n'emporte jamais un brouillon
  (faiblesse 5.6-1). Documents et usages, visibles de tous les membres, suivent. Corps en texte, `text/plain` UTF-8 ;
  nom de fichier par la règle de la 1.x (A-1.2-16) dans `Content-Disposition`. Audit `project.exported`
  (`{"format":"pmap","with_sheets":...}`), comme le paquet : spec 5.5, « exports ».
- Export au-delà de 12 niveaux : les niveaux au-delà ne sont pas écrits et la réponse le signale (en-tête
  `X-Pmap-Warning: PMAP_PROFONDEUR_COUPEE`, A-2.17-47 : plus de troncature silencieuse).
- `POST /api/projects/{id}/duplicate` `{code?, name?}` (administrateur, la source lue comme par un lecteur ; un projet
  absent ou dans la corbeille est introuvable) : le modèle `.pmap` 1.1 construit en mémoire depuis la source, sans fiches
  (AR-10), puis le chemin de l'import ; traductions des étapes et des flèches gardées. Audit `project.duplicated`
  (identifiant de la source dans les détails, spec 5.5). La duplication n'écrit aucun fichier : elle garde **tous les
  niveaux** des sous-cartes, jusqu'à la borne des parcours du domaine (51 cartes, AR-67), et non les 12 du format
  (AR-118, porte P5 round 1 : la 1.x coupait en silence à 12, la copie d'un projet de 13 niveaux perdait le dernier) ;
  au-delà de cette borne, la réponse 201 porte le même en-tête `X-Pmap-Warning`. Le nom saisi est rogné avant d'être
  comparé à celui du fichier, dont les traductions restent quand ils sont égaux (1.x `ServiceFichiers.cs:172`).

### 4. L'ouverture d'un fichier

- `POST /api/projects/import-pmap` (administrateur) : corps JSON `{content, code?, name?}` (A-4-54 : pas de fichier
  binaire), 10 Mo au plus refusés avant lecture (A-4-22 ; 413 `REQUETE_TROP_GROSSE` du garde commun, la 1.x disait
  `FICHIER_TROP_GROS` après lecture) ; un `content` de plus de 8 M caractères, que seul un corps proche de la limite peut
  porter, est `FICHIER_TROP_GROS` comme en 1.x. Réponse 201 : projet, code, carte racine, nombres de cartes, couloirs,
  étapes, flèches et blocs de fiche écrits (un bloc qui ne porte que des documents ou des usages remplit la fiche de son
  étape sans formulaire : il compte aussi), incertitudes. Un code absent de la demande et du fichier, ou mal formé, est
  `PROJET_CODE_INVALIDE` ; un nom absent du fichier est une faute du fichier (`project.name`), un nom saisi vide prend
  celui du fichier.
- Nettoyage de la 1.x : BOM, bloc de code Markdown, virgules finales, commentaires ; profondeur JSON 256.

### 5. Les erreurs : des codes, des messages dans l'hôte

Le lecteur rend des erreurs `{path, code, values}` (`FileError`, domaine), 30 au plus ; `FICHIER_INVALIDE` porte `count`
et `errors` ; l'hôte rend chaque erreur avec son message français ou anglais tiré de ses fichiers de messages
(`PMAP_*`), comme les erreurs de la barre de commande et de l'import des traductions (A-3-72, A-6-19). Le chemin reste
celui de la 1.x (`map.steps[3].lane`).

## Options écartées

- Fiches dans un fichier séparé ou une version 2.0 du `.pmap` : le porteur veut qu'un récit donne tout ou partie des
  fiches dans le même fichier (AR-93).
- Fiche importée validée : une IA n'a pas l'autorité de valider une fiche (le circuit de validation, annexe A 2.10).
- Export des fiches sans tenir compte du demandeur : ferait sortir les brouillons par la copie d'un lecteur.

## Emplacements

`src/Topogenia.Interchange/Pmap/` (lecteur, écrivain, lecture d'un projet, construction, adaptateur `PmapFiles` du port
`IPmapFormat`) ; `src/Topogenia.Application/Exchange/PmapExchange.cs` ; `src/Topogenia.Host/Api/Exchange/PmapApi.cs` ;
codes `FileErrorCodes` (domaine) et messages `PMAP_*` des fichiers de l'hôte ; `docs/format-pmap.md` ;
`schemas/pmap-1.1.schema.json` ; consignes pour l'IA dans `docs/pmap/` (`PMAP_GUIDE_IA.md`, avec la méthode pour tirer les
fiches d'un récit et un récit converti, `PROMPT_croquis_vers_pmap.md`) ; exemples `samples/minimal.pmap` et
`samples/cloture-mensuelle.pmap`.

## Vérification

- Échange : `Topogenia.Interchange.Tests.Pmap.PmapReaderTests` (20 : chaque règle de l'annexe A 2.17, portage de
  `FormatPmapTests`), `PmapSheetReadingTests` (7 : bloc de fiche), `PmapBuildTests` (7 : construction par le domaine, codes
  dans l'ordre de lecture, brouillons, irritant sur l'étape productrice), `PmapCopyTests` (9 : copie de la copie
  identique, codes, visibilité des fiches, RACI visible, sous-cartes vides, coupe à 12 niveaux, écriture lisible),
  `PmapDocumentationTests` (4 : exemples des documents et schéma publié).
- Application : `Topogenia.Application.Tests.Exchange.PmapExchangeTests` (8 : code et nom, 8 M caractères, rien écrit sur
  une faute, duplication sans fiches et son audit, corbeille, copie selon le rôle et son audit).
- API, sur les deux bases : `Topogenia.Host.Tests.Exchange.PmapRoutesTests` (6 théories) et
  `Topogenia.Host.Tests.Ported.PmapFilesPortedTests` (9 théories : T-FICR-01 à 09, T-BRAN-05) ; tables des routes, des
  droits, des dispenses d'`If-Match` et catalogues des requêtes.
- Douze mutations tuées (RACI et fiche sur un type sans contenu, usage de sa propre sortie, lecteur traité en animateur,
  sous-carte vide gardée, traductions gardées après un renommage, codes dans l'ordre du fichier, limite de 8 M
  caractères, code saisi ignoré, coupe non signalée, traductions de la langue d'origine écrites, case en double admise).
