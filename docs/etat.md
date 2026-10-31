<!-- etat.md -->
<!-- Etat courant de la realisation : phase, tache, prochain pas, chiffres des tests, blocages -->

# État courant

- **Version 3.0 (IA intégrée)**, depuis le 29/10/2026, après la validation de la 2.0 par le porteur du projet :
  spécification `TOPOGENIA_3.0_SPECIFICATION_IA.md` (révision 4), quatre lots, feu vert du porteur pour les lots 2 à 4
  le 30/10/2026. **Lot 1 terminé** (paramètres et connecteurs, ADR 077), essai réel du porteur concluant le 30/10/2026.
  **Lot 2 construit la nuit du 30/10/2026** (volet de l'assistant, ADR 078) : bouton IA du ruban et Ctrl+I, volet ancré
  à droite, quatre tuiles, champ de quatre à douze lignes avec copier, coller, lire un fichier texte, effacer ; échange
  en flux de lignes JSON (refus avant le flux, battements toutes les 2 s, arrêt) ; réponse structurée de quatre formes
  (Réponse, Questions, Analyse, Proposition), la proposition en `.pmap` relu par le format et corrigé une fois ;
  contexte mis en cache (documentation des formats embarquée, schéma du projet), carte ouverte et sélection ;
  copier-coller ; accueil « Décrire un processus » (administrateur) ; audit `assistant.exchanged` sans texte. Essais
  réels du lot 2 avec le crédit d'essai : voir le journal. **Lot 3 construit la même nuit** (ADR 079) : aperçu d'une
  proposition sur la carte ouverte, étapes touchées marquées, « Appliquer » en une opération défaite par un Ctrl+Z,
  aperçu périmé refusé, « Proposer la correction » d'un constat. **Lot 4 construit au matin du 30/10/2026** (ADR 080) :
  fichiers joints (PDF et images tels quels, Word, Excel et Visio lus par l'hôte sans DTD), bornes de chaque fournisseur,
  fichiers gardés avec leur tour et relus depuis le cache, trombone, dépôt et collage, pastilles. Les quatre lots de la
  3.0 sont faits. **Essai du porteur le 30/10/2026 : concluant, sauf une demande de traduction**, corrigée le même jour
  (ADR 081 : traductions d'une proposition écrites comme automatiques, jamais sur une manuelle, langues dites au modèle).
  **Lot 5 construit le même jour** (ADR 082, feu vert du porteur) : répartir une carte existante en niveaux sur place
  (« Détailler la sélection » rejoué, fiches des étapes nouvelles en brouillon), et ramener un processus à un seul niveau,
  toujours en nouveau projet (fiches et traductions d'origine reportées) ; essai réel sur une vente fictive de 32 étapes
  concluant. **Lot 6 « les langues » construit le soir du 30/10/2026** (ADR 083, après une remarque du porteur sur un
  collègue portugais) : dix-huit langues (seize produites par l'IA, à relire, F-59), langue de l'interface dans le menu
  de l'utilisateur et langue du processus dans l'en-tête, une langue sur la carte, la version d'origine verrouillée dans
  une autre langue (un nom tapé devient sa traduction), « Traduire avec l'assistant » qui ne prend que les traductions de
  la langue demandée, nom du projet compris (F-57) ; essai réel concluant (portugais et japonais, 0,53 $). Après le
  premier essai du porteur : plafond d'une réponse à 64 000 jetons, aperçu d'une traduction montré en place sur la carte
  (ADR 083, points 11 et 12) ; règles de modélisation données à l'IA (ADR 084). **Lot 7 « le flux de valeur »
  construit le 31/10/2026** (ADR 085, spécification révision 5, section 14 ; feu vert du porteur : « le diagramme doit
  être auditable par un organisme extérieur et donc être complet tout en restant simple pour l'utilisateur ») : quatre
  saisies facultatives (volume entrant d'un Début, part des dossiers d'une sortie de branchement, délai d'une fiche,
  effectif d'un couloir), la fréquence « à chaque dossier », le temps de travail du projet ; débits, charge, ETP, chemin
  principal et délai moyen calculés par le cœur à chaque lecture, jamais stockés, vérifiés contre un oracle ; contrôle
  de complétude de seize codes ; calque « Flux de valeur » (cartouches, parts, couloirs, ligne de temps, bilan,
  contrôle), saisies par les menus, feuilles Excel ; paquet `.topogenia` 2.1 et `.pmap` 1.2 ; assistant qui porte ces
  valeurs sans en inventer (essai réel 0,31 $). **Lot 7b le même jour**, après l'essai du porteur (ADR 086,
  spécification révision 6, section 15) : la molette fait défiler et Ctrl + molette zoome ; tous les volets de droite se
  redimensionnent et se réduisent ; le bilan, le chemin principal et le contrôle dans un volet « Flux de valeur » en
  textes courts, plus rien sous les couloirs ; une page du flux de valeur dans le PDF. **Lot 7c** : l'animateur
  modifie ou retire la question posée à l'auteur d'une fiche (spécification révision 7, section 16) ; licence au nom
  de Peter Barcauti. **Lot 7d** : chaque paquet du cœur porte sa licence et une empreinte que seul le titulaire sait
  faire, et il est archivé avec ses assemblages, sa carte des noms et ses sources (ADR 087). Reste l'essai du porteur
  (le dossier d'essai du lot 7d, qui contient les lots précédents) et Bedrock.
- **Phase de la 2.0** : **7 (paquets et recette) en cours** depuis le 29/10/2026 ; phase 6 (interface) terminée le 29/10/2026
  (porte P6-T12 franchie au round 4).
- **Fait en phase 6** :
  - P6-T11c (AR-129, révision de l'ADR 059 ; relevé par le porteur du projet sur la capture 07) : les flèches qui arrivent sur un même
    côté d'une étape partagent leur dernier segment et finissent sur un seul point (une seule pointe), boucles par le bas
    comprises ; `routing/tracks.ts` ; propriétés des 500 cartes adaptées ; démonstration refaite.
  - P6-T11b (ADR 068, AR-128) : ce qui pend sous une étape (libellé d'un losange, fréquence, pastilles RACI) s'écarte de
    10 px de l'axe de la pointe basse dès qu'une flèche l'emprunte, à gauche quand ses flèches courent à droite dans la
    bande ; `routing/under.ts`, `layoutFlows`, `StepFigure.under`, classes `is-under-left` / `is-under-right` ; parcours
    `under-step.spec.ts` ; propriété sur 500 cartes ; démonstration refaite.
  - P6-T11 (S-P-18, S-P-19) : `docs/recette/demonstration/` : `scenario.md` (17 pas sur le projet fictif « Notes de frais »,
    comptes `DEMO\*`), `captures/` (30 captures à 1366 × 768 sur le vrai hôte en mode Poste), `exports/` (image, PDF,
    classeur, paquet, classeurs des traductions), `LISEZMOI.md`. Jeu : `npm run demonstration` (`web/scripts/demonstration/`,
    configuration Playwright à part, gestes de l'utilisateur, résultats vérifiés). Lanceur des parcours : option
    `--account` (mode Poste) par `FictitiousAccountFactory`, passée par `E2E_ACCOUNT`. Défauts relevés : F-25 aggravé
    (pointe d'une flèche cachée sous le badge de fréquence, segment sous les pastilles RACI) -> P6-T11b ; F-33 mineur.
  - P6-T09b (révision de l'ADR 066, AR-52) : police Inter intégrée au PDF au lieu du Latin-1 : faces Regular et Bold du
    paquet `inter-font` 3.19.0 (OFL-1.1, livré), lues à la demande (`export/pdfFonts.ts`), deux familles `Inter-Regular`
    et `Inter-Bold` ; textes écrits tels quels, glyphes employés seulement (PDF d'environ 220 ko) ; tests relus par la table
    ToUnicode (`web/src/test/pdf.ts`), parcours du PDF avec un titre polonais et turc dans les deux modes.
  - P6-T10 (ADR 067) : banc des parcours contre le vrai hôte, lanceur de test `tests/Topogenia.EndToEnd.Host` (jamais livré :
    `HostApplication.Build`, mode Poste sur SQLite avec l'adresse de lancement donnée au seul Chromium des parcours, mode
    Serveur sur `Topogenia_E2E` en HTTPS avec un certificat en mémoire et Negotiate) ; `web/playwright.config.ts`,
    `web/e2e/support/` (mise en place, API par la requête ou par une page selon le mode, base vidée et profil en français
    avant chaque parcours, axe-core) ; `npm run e2e` ; 70 parcours (49 portés de la 1.x, 21 nouveaux), verts dans les deux
    modes ; `@axe-core/playwright` 4.13.0 et `ajv` 8.20.0 en développement. Portage des 176 unitaires 1.x (30 lignes
    complétées) : `docs/couverture-p6/portage-interface.md` ; parcours : `docs/couverture-p6/portage-parcours.md`. Défauts
    corrigés : pont des « + » qui captait le pointeur (affiché au survol seulement), Échap avec la fiche ouverte qui
    n'abandonnait pas la coupe, cibles des radios du dialogue Traductions (WCAG 2.5.8), `data-testid` en double, consigne
    IA sans schéma. Écarts : lecture seule hors d'atteinte du banc (B-2.3-46, B-8.3-36) ; installation Poste à P7-T01.
    Écart relevé, à corriger : AR-52 (P6-T09b). 223 lignes de couverture écrites.
  - P6-T09 (ADR 066, AR-126, AR-127) : module `web/src/features/map/export/` : `capture.ts` (bornes des nœuds sans le cadre,
    marge 24 et 20 px de plus, `pixelRatio` borné, « + », pont, poignées, jauges et cadre retirés, attente de 3 s puis
    50 ms, polices lues par l'application et passées à html-to-image 1.11.11 par `fontEmbedCSS` : sa propre lecture posait
    une adresse de base refusée par la CSP), `pdf.ts` (jsPDF 4.2.1, A3 paysage, titre, fil d'Ariane, date longue,
    classification, image ajustée, pied au nom du produit, Latin-1), `workbook.ts` (Étapes, Liens, Matrice RACI, couleurs
    des jetons, en-tête d'impression à la classification) ; opération `state/actions/exports.ts` (projet lu, état
    `exporting` au magasin, notifications, un export à la fois, rien dans l'historique) ; canevas inscrit auprès de
    l'éditeur (`canvas/useExport.ts`, `editor.canvas`), classe `is-exporting`, étapes étrangères sans `is-outside`,
    marqueur des flèches dans la vue (`ViewportPortal`), ligne de classification sous la carte (`canvas/ExportCaption.tsx`) ;
    menu Fichier : PDF, PNG, Excel pour tout lecteur ; `Translate` et `WORKBOOK_TYPE` dans `shared/`. Vérifié sous la CSP
    de l'hôte dans Chromium (aucune violation). 47 lignes de couverture ; 132 mutations, toutes tuées (4 après renfort).
  - P6-T08 (ADR 065, AR-124, AR-125) : glossaire des traductions en réglage du projet (`translation_glossary`, texte libre,
    4 000 caractères ; `ProjectSettings.ChangeTranslationGlossary`, `ChangeTranslationGlossaryCommand`, route
    `PUT /api/projects/{id}/translation-glossary`, migration `TranslationGlossary` sur les deux moteurs, paquet
    `translationGlossary`, contrat régénéré) ; module `web/src/features/translations/` : `model/workbook.ts` (classeur exceljs
    4.4.0 construit dans le navigateur, une colonne par langue configurée, feuilles dans la langue de l'interface, contrôle
    `veryHidden` au nom du produit, consigne et glossaire), `model/workbookReader.ts`, `model/report.ts`, dialogue en trois
    étapes (`TranslationsDialog.tsx`, `DialogParts.tsx`, `useGlossary.ts`, `useWorkbook.ts`), `ManualTranslationsDialog.tsx` ;
    côté carte : bouton de l'en-tête déclaré quand une autre langue est configurée (langues du profil par `App`),
    « Traduire… » aux menus d'une étape, d'une flèche libellée et d'un couloir, carte relue après un import. `uuid` 11.1.1
    forcé sous exceljs (`npm audit` 0 ; vaut pour le verrouillage, le paquet navigateur servi embarque sa série 8, ADR 065). Trois tests de la persistance écrivent désormais la base ancienne avant de la
    redescendre (`ProjectGraph.BeforeGlossary()`). Vérifié sous la CSP de l'hôte dans Chromium. 30 lignes de couverture ;
    81 mutations, 80 sur 80 (une équivalente retirée).
  - P6-T07 (ADR 064, AR-123) : module `web/src/features/sheet/` indépendant de la carte, branché par
    `map/panels/SheetDock.tsx` ; modules purs `model/draft.ts`, `procedure.ts`, `documents.ts` ; file d'enregistrement
    `state/autoSave.ts` (1200 ms, un envoi à la fois, version `0`, conflit relu, envoi à la fermeture, confirmation avant de
    quitter, saisie ancienne jamais renvoyée : défaut 1.x évité) ; `state/useSheetState.ts`, `state/sheetActions.ts` ;
    panneau ancré (440 px, 45 % au plus), en-tête et pied collants, responsable, bascule, rubriques, sections pliables en
    préférences `app.sheet*Folded` (AR-91), déroulé à poignée et Alt+flèches (AR-89), entrées proches, d'ailleurs,
    extérieures, « Autre… », usage et irritant, sorties, lecture ; côté carte : jauge sans relire la carte, `editor.reload`
    après un document, responsable par `PATCH`, projet lu une fois (« un ancien membre »), un seul panneau ancré
    (`watchSidePanel`, AR-123) ; `styles/sheet.css`. Défaut de P6-T06 corrigé : Échap relâché fermait les panneaux Reliquat
    et RACI après un dialogue. 86 lignes de couverture ; 129 mutations, toutes tuées.
  - P6-T06 (ADR 063, AR-122) : modules purs `model/raci.ts`, `frame.ts`, `remainder.ts` (une seule fonction d'ordre de
    lecture, `hasContent` dans `model/stepFacts.ts`) ; cadre de remontée (`nodes/FrameNode.tsx`, nœud `frame` traversé par
    le pointeur sauf son étiquette), étape étrangère `is-outside` ; « Remonter le détail dans le flux » au menu Étape ;
    `state/actions/dissolution.ts` (remonter, affecter, supprimer, terminer, `watchFrame` : rangement automatique une fois
    par remontée, panneau fermé avec le cadre) ; panneau Reliquat ancré à droite (`panels/RemainderPanel.tsx`,
    `RemainderSheet.tsx`, `MapSidePanel.tsx`, `map-workspace`, `.side-panel` de 440 px pour la fiche aussi), lecteur sans
    actions (AR-122) ; `panels/ArrangementDialog.tsx` ; RACI : pastilles (`nodes/RaciBadges.tsx`, `UNDER_STEP`), panneau
    (`panels/RaciPanel.tsx`), bouton et dialogue Matrice (`panels/RaciMatrixDialog.tsx`), `state/actions/raci.ts` (pose,
    copie aux jetons) ; `itemsOf` lit `visible` ; groupe en renommage. Défauts corrigés : étape coupée selon AR-53 (le
    lot 2 atténuait son texte), refus de Terminer sans fermeture, vitest sur la moitié des cœurs (`maxWorkers: '50%'`).
    69 lignes de couverture ; 92 mutations, toutes tuées.
  - P6-T04 lot 3 (ADR 062, AR-121 ; **P6-T04 terminée**) : annuler et rétablir par la file des écritures
    (`state/actions/history.ts` : boutons selon l'historique, grisés pendant le rejeu, clavier mis en file, « Annulé :
    {action} », refus 409 relu, `INTROUVABLE` muet, autre échec dit sans vider la pile de l'hôte) ; barre de commande
    (`panels/CommandBar.tsx`, `state/actions/commandBar.ts` : Ctrl+K et bouton Commande, 18 %, 560 px, refus par ligne) ;
    barre de l'éditeur dans l'en-tête (`app/EditorBar.tsx`, `shared/state/openProject.ts` : fil d'Ariane, Fichier, Membres, à
    la place de la signature) ; menu Fichier (`state/commands/fileCommands.ts`, `state/actions/files.ts`) sur les dialogues
    de P6-T03 ; dernier projet ouvert mémorisé par l'enveloppe (`rememberProject`, `PUT /api/me/last-project`) ; charte des
    composants prouvée (`styles/charter.test.ts`), formulaire de fichier sur une colonne sous 600 px (B-6-55).
  - P6-T04 lot 2 (ADR 061 ; l'édition) : éditeur par carte (`state/editor.ts`, `state/EditorContext.tsx` : magasins, file
    des écritures sans mise à jour optimiste, refus affiché, 409 relu) ; sélection, outil, Espace, fiche ouverte au magasin ;
    modules purs `model/readingOrder.ts`, `clipboard.ts`, `alignment.ts`, `moves.ts` ; opérations `state/actions/` ;
    commandes en données `state/commands/` (ruban, menus en lignes groupées, clavier) ; `chrome/Ribbon.tsx` (radio,
    repliable AR-90), `chrome/LaneHeaders.tsx` (renommer, réordonner, « + », menu « ⋯ »), `canvas/useCanvasEvents.ts`,
    `canvas/useCanvasKeys.ts`, `nodes/LabelInput.tsx`, `nodes/QuickAdd.tsx`, `panels/MapFloating.tsx`,
    `panels/DeletionDialogs.tsx` ; menu et barre partagés étendus. 210 lignes de couverture (36 `partiel` renvoyées à
    P6-T06, T07, T10 et au lot 3). 61 mutations : 60 tuées, une équivalente retirée du code. Défaut trouvé à la
    vérification visuelle et corrigé : le menu d'une étape sortait de l'écran (remonté selon sa hauteur dessinée).
  - P6-T04 lot 1 (ADR 060 ; la carte affichée) : `@xyflow/react` 12.12.0 (version de la 1.x, `base.css` seule) ;
    `model/mapLayout.ts` (bandes des couloirs, boîtes et libellés des étapes, flèches routées avec leurs étiquettes mesurées
    par `model/textSize.ts`), `model/stepFacts.ts` (durée lisible, jauge, teinte, étape détaillée),
    `model/geometry/viewport.ts` (vue bornée) ; `state/mapStore.ts` (magasin par éditeur, lecture annulée par la suivante) ;
    `canvas/` (Canvas, nœuds et liens de React Flow à poignées déclarées, flèche, vue bornée à chaque mouvement, mesure
    après chargement des polices), `nodes/` (StepNode, Pictos, StepBadges, LaneBackground), `edges/` (RoutedEdge pour les
    trois types, EdgeLabel), `chrome/LaneHeaders.tsx`, `MapScreen.tsx` (chargement, erreur et « Réessayer », bandeaux),
    `styles/map.css` (maquette validée, jetons seulement). 100 lignes de couverture écrites (dont 9 `partiel` renvoyées à
    P6-T06, T09, T10 et aux lots 2 et 3). Défaut trouvé et corrigé : la vue restait au-delà de la borne quand la vue
    bornée égalait celle gardée. 42 mutations sur une copie : 41 tuées (34 au premier passage, les autres après renfort
    ou ajout de tests), une équivalente retirée du code. Vérification visuelle dans Chromium sans tête (API simulée) : conforme à la maquette, aucune erreur.
  - P6-T05 (ADR 059, AR-120 ; fait avant T04, ordre décidé le 28/10/2026) : module pur `web/src/features/map/model/geometry/`
    (`constants.ts`, `grid.ts`, `routing/` : côtés et pointes des losanges, tracés orthogonaux par les allées, axes, lignes
    et bandes libres, courbe des boucles d'une ligne, voies de 6 px, étiquettes, chemins SVG) ; `routeMap(map)` rend le
    tracé complet de chaque flèche. Propriétés de la spec 9.7 sur 500 cartes d'une graine fixe (`src/test/randomMaps.ts`) ;
    essai ponctuel à 5 000 cartes : 1 cas de capacité (registre F-24). 53 lignes de couverture écrites. 30 mutations sur une
    copie : 28 tuées (9 après renfort des tests), une ligne redondante retirée, un mutant équivalent (F-24).
  - P6-T03 (ADR 058, complément de l'ADR 056) : contrat corrigé (24 schémas vides : énumérations et listes des corps, deux
    transformateurs `Host/Json/CodeEnumSchemaTransformer`, `NonNullItemsSchemaTransformer`, test qui refuse tout schéma
    vide) ; `features/home/` (bandeau AR-88, projet mis en avant AR-49, projets, corbeille, suppression par le code avec la
    version relue, participants), `features/project/` (création, un nom par langue configurée ; membres ; dialogues `.pmap`
    ouvrir et dupliquer, copie ; paquet : export sans les personnes coché, import par l'aperçu des mêmes choix,
    remplacement), `shared/download.ts` (règle de nom de l'hôte), `styles/pages.css`. 75 lignes de couverture écrites.
    30 mutations sur une copie : 29 tuées (quatre après renfort des tests), une équivalente retirée du code.
  - P6-T02 (ADR 057) : le profil rend les langues de la configuration (`languages`) ; `web/src/app/` (routeur AR-30,
    enveloppe, en-tête, page d'erreur, `profile/` avec AR-106), `shared/i18n/` (un fichier par langue dans `locales/`,
    `applyLanguage`, `messageOf`), `shared/keyboard/` (un écouteur en capture, couches champ > modal > panneau > canevas,
    accords globaux, Ctrl+S), `shared/commands/` (commandes en données), `shared/ui/` (Button, Icon, Logo, Toaster,
    Dialog, Confirm, Field, Menu, Toolbar), `shared/state/openProject.ts`, `features/map/state/preferences.ts` (clés
    `app.*`, ruban replié AR-90), `styles/components.css`, `__APP_VERSION__`, `/favicon.svg`. zustand 5.0.15, i18next
    26.4.3, react-i18next 17.0.16 (MIT). 54 lignes de couverture écrites ; raccourcis de la carte et état de l'éditeur
    rattachés à T04. 29 mutations sur une copie, toutes tuées (une survivante au premier passage, test renforcé).
  - P6-T01 (ADR 056) : contrat OpenAPI servi figé dans `web/src/api/generated/openapi.json` (sans `servers` ni `info`,
    comparé sur les deux bases par `ApiContractTests`) ; schéma `ErrorBody` (`Host/Json/ErrorSchemaTransformer`) aux codes
    du catalogue des messages ; `npm run api` génère `schema.ts` (openapi-typescript 7.13.0, paire TypeScript fixée par
    `overrides`) et `exchanges.ts`, vérifiés par `scripts/api-generation.test.ts` ; client maison `web/src/api/`
    (`request.ts`, `errors.ts`, `client.ts`, `exchange.ts`) : en-têtes, erreurs normalisées, version exigée par le type
    sur chaque route qui lit `If-Match` ; proxy de développement à connexion unique (B-0-09). Lignes couvertes : S-D09-02,
    B-0-05, B-0-09, B-0-18 à B-0-21, part interface de B-0-14 à B-0-17 ; les autres lignes de B-0 sont rattachées à
    T02, T04, T09, T10 dans `plan.md`. Sept mutations tuées. `THIRD-PARTY-NOTICES.md` régénéré.
- **Fait en phase 5** :
  - P5-T01 (ADR 051) : format 2.0 du paquet (`src/Topogenia.Interchange`, `docs/format-topogenia.md`, schéma publié,
    `samples/conges.topogenia`).
  - P5-T01b : export `POST /api/projects/{id}/export` (animateur, AR-113) ; ses 4 tests rouges (catalogue des droits,
    dispense d'`If-Match`) corrigés à la reprise du 25/10.
  - P5-T02 (ADR 052) : import. `PackageReader` (garde-fous), `PackageBuild` (reconstruction), `PackageImporter` (port
    `IPackageImporter`) ; domaine `ProjectCodeRules.TrashCode`, `ProjectReplacement.Retire`, `ImportedProjects`,
    `ReadableCode.WithProject` ; application `Exchange/PackageImport` et `ImportPreparation`, `UserRequest.LocksTarget` ;
    routes `POST /api/projects/import/preview`, `/import`, `/{id}/import/preview`, `/{id}/import` (corps binaire,
    `with_members` exigé, `code` facultatif, `If-Match` au remplacement) ; codes `PAQUET_INVALIDE`, `PAQUET_TROP_RECENT`.
  - Défauts de P5-T02 revus (AR-114 à AR-116) : un remplacement qui n'apporte pas de membres garde l'équipe du projet
    remplacé (AR-115, `ImportedProjects.KeepMembers`) ; code dérivé et droits de l'aperçu gardés.
  - P5-T03 (ADR 053) : montée de version des paquets, `PackageDefinition`, `IPackageConverter`, `PackageChain`
    (vide en 2.0), preuve par des formats fictifs 1.8 et 1.9 dans `PackageConversionTests`.
  - P5-T04 (ADR 054) : `.pmap` 1.1, `src/Topogenia.Interchange/Pmap` (lecteur, écrivain, `PmapOutline`, `PmapBuild`,
    `PmapFiles`), bloc `sheet` en brouillon, traductions des étapes et des flèches ; application `PmapExchange` ; routes
    `GET /api/projects/{id}/pmap`, `POST /api/projects/import-pmap`, `POST /api/projects/{id}/duplicate` ; 48 codes
    `PMAP_*` et leurs messages ; `docs/format-pmap.md`, `schemas/pmap-1.1.schema.json`, `docs/pmap/` (guide IA, prompt),
    `samples/*.pmap`.
  - P5-T07 (ADR 055) : allers-retours exact et croisé par l'API sur l'exemple de toutes les fonctions (complété), paquets
    hostiles par l'API, ligne de commande `--export` / `--import` ; deux défauts corrigés (`WritePlan` : lot créé avec son
    projet ; `ImportedProjects.Violations` : `output-wait`, `document-link`).
- **Porte P5-T08, round 1** (25/10/2026) : 1 bloquant, 15 majeurs, 28 mineurs. Fond commun : l'import d'un paquet
  recopiait des valeurs qu'aucune règle de saisie ne revoyait. Corrigé par `Domain/Projects/ImportedRecords` (raison
  `values`), AR-117 (classification gardée au remplacement), AR-118 (duplication sans coupe à 12 niveaux), lecteur
  robuste (clé en double, nombre démesuré), contrat OpenAPI des fichiers, tests du `.pmap`, colonne Phase de 56 lignes
  de la couverture. Détail : `Travail/porte-p5-r1-corrections.md` ; mineurs au registre (F-12 à F-16).
- **Porte P5-T08, round 2** (25 et 27/10/2026) : 0 bloquant, 3 majeurs distincts corrigés (AR-119 langues d'un
  paquet, demi-paire de substitution refusée, preuve de la borne de la duplication), mineurs F-17 à F-21. Détail :
  `Travail/porte-p5-r2-corrections.md`.
- **Porte P5-T08, round 3** (27/10/2026) : Intégrité franchit ; 3 majeurs distincts corrigés (nom de propriété mal
  formé, tests d'AR-119 sur un objet d'une autre langue, compte de l'aperçu), F-22. Détail : `Travail/porte-p5-r3-corrections.md`.
- **Porte P5-T08 franchie au round 4** (27/10/2026) : 0 bloquant, 0 majeur ; **phase 5 terminée**. Mineurs F-23.
- **En cours** : rien ; P6-T11, P6-T11b et P6-T11c terminées.
- **Porte P6-T12, round 1** (28/10/2026) : 3 bloquants, 13 majeurs (11 distincts) ; Intégrité franchit. **Corrigés**
  (28 et 29/10, lots A, B, D, E et intégration, `Travail/porte-p6-r1-corrections.md`) : projet d'exemple du mode Poste,
  classification choisie, montrée et réglable, « Quitter », propriétés du projet (ADR 069, AR-131 à AR-135) ; fiche et
  RACI au clavier, focus, contraste, « ! » d'un losange, étiquettes près de leur flèche, menus de la maquette (ADR 070,
  AR-136 à AR-142, ADR 059 révisée) ; copie RACI sans formule (AR-130) ; couverture de la phase 6 close (règles de
  l'interface dans `web/src/test/`, 17 parcours nouveaux, mesures d'ouverture et de fluidité, Edge). Mineurs : F-34 à
  F-39.
- **Porte P6-T12, round 2** (29/10/2026) : Fidélité, Sécurité, Interface et charte, Intégrité franchissent ;
  Architecture : 1 majeur (licence de `buffers` dans `THIRD-PARTY-NOTICES.md`), corrigé avec les mineurs utiles des cinq
  mandats (`Travail/porte-p6-r2-corrections.md`, AR-143).
- **Porte P6-T12, round 3** (29/10/2026) : Architecture : 1 majeur (paquets embarqués dans `exceljs.min.js` absents de
  `THIRD-PARTY-NOTICES.md`), corrigé (`Travail/porte-p6-r3-corrections.md`) ; Architecture 758/758.
- **Porte P6-T12, round 4** (29/10/2026) : Architecture franchit (0 bloquant, 0 majeur ; mineurs F-41). **Porte
  franchie, phase 6 terminée.**
- **Fait en phase 7** : P7-T01 (ADR 071, AR-144) : second lancement par deux événements nommés de la session
  (jeton neuf, code 0) ; profil `Desktop.pubxml` et `build/package.ps1 -Mode Desktop` (ZIP portable, licences, SHA-256) ;
  exécutable décompressé vérifié (`docs/couverture-p7/verification-poste.md` : 4,2 s au premier lancement, rien hors du
  dossier du produit).
- **Fait en phase 7 (suite)** : P7-T02 (ADR 072, AR-145) : `Server.pubxml`, `web.config`, exemple de réglages,
  `build/package.ps1 -Mode Desktop|Server` ; hôte publié essayé en mode Serveur ; charge de 30 clients tenue
  (`docs/couverture-p7/verification-serveur.md`) ; IIS et le compte du seul rôle `topogenia_app` à la recette d'installation.
- **Fait en phase 7 (suite)** : P7-T03 (ADR 073, AR-146) : `web/served-packages.json`, `build/components.mjs`,
  `THIRD-PARTY-NOTICES.md` et `--check`, SBOM CycloneDX par ZIP, signature si un certificat est donné,
  `build/codeql-local.ps1`.
- **Fait en phase 7 (suite)** : P7-T04 : `docs/exploitation-poste.md`, `docs/exploitation-serveur.md`, `docs/securite.md`
  (liste générée depuis la couverture, revue ASVS et TISAX) ; `Cache-Control: no-store` sur l'API ; phrase de
  `PARAMETRES_INVALIDES`.
- **Fait en phase 7 (suite)** : P7-T04b (ADR 074, AR-147) : `ProductIdentity.cs` dans l'application ; bascule
  `UseCorePackage` ; chaîne `build/core/` (nom fort, Obfuscar, paquet `Topogenia.Core`) ; `build/split.mjs` écrit le dépôt
  public hors de la copie de travail et le prouve : essai 3 du 29/10/2026, 3 738 tests : 3 692 verts, 46 sautés (règles qui lisent les sources du cœur, `CoreSources.Require`), 0 échec, en 14 min (essai 1 : 2 échecs du contrat OpenAPI ; essai 2 : 1 échec, `ServerRunTests.A_refusal_over_https_carries_hsts_too` ; tous deux corrigés).
- **Fait en phase 7 (suite)** : P7-T07 (passe du registre : corrections, issue de chaque entrée de
  `docs/a-peaufiner.md`, port mort `ICounterService` retiré AR-148) ; P7-T05 (`docs/recette/finale/LISEZMOI.md` complet,
  ZIP refaits avec leurs empreintes, `build/package.ps1` restaure l'hôte avant la vérification des composants,
  `docs/RAPPORT_FINAL.md`) ; couverture sans ligne `à faire`.
- **En cours : P7-T06, porte finale, round 1** (29/10/2026) : contrôleurs Architecture, Sécurité, Intégrité des tests
  (`docs/consignes-porte-p7.md`), comptes rendus `Travail/porte-p7-r1-*.md` hors du dépôt.
- **Fait (29/10/2026, après le round 1)** : corrections du round 1 (lots 1 à 4, non vérifiées par une suite complète) ;
  R4 décidée par le porteur (AR-150, ADR 076) : chaîne des paquets sans script du système, `node build/package.mjs
  <Desktop|Server>` seul point d'entrée, ZIP écrit et relu par le SDK (`build/archive.proj`), `build/codeql-local.mjs`,
  exécutable unique non compressé, cœur sans `SuppressIldasm` (paquet du cœur à refaire), empreinte de chaque fichier
  à côté du ZIP, § « Antivirus et EDR » des deux guides ; Architecture 853/853.
- **Fait ensuite** : R1, R3, R2 (AR-151 à AR-153) ; vérification complète : suite .NET 5 188/5 188, vitest 1 805/1 805,
  parcours 91/91 dans les deux modes, démonstration rejouée.
- **Fait ensuite (AR-154)** : « Grouper » et « Dissocier » (Ctrl+G, Ctrl+Maj+G), import d'un paquet au menu Fichier,
  dialogue d'un .pmap qui reconnaît un paquet ; vitest 1 812/1 812, parcours 93/93 dans les deux modes, Architecture
  853/853, démonstration rejouée.
- **Fait ensuite (AR-155)** : Fichier après la marque, zone Niveau (« Remonter d’un niveau », Alt+↑) en tête du
  ruban et Outils en dernier, « Entrée » affiché pour « Ouvrir la fiche », « Inverser le sens » d’une flèche (cœur, route
  `POST /api/flows/{id}/reverse`) ; suite .NET 5 202/5 202, vitest 1 818/1 818, parcours 95/95 dans les deux modes,
  Architecture 853/853, démonstration rejouée.
- **Prochain pas exact** : le porteur essaie la nouvelle chaîne des paquets et l'interface (paquet du cœur refait, puis
  `node build/package.mjs Desktop` et `Server`) ; puis ZIP, empreintes, dépôt public, chiffres, round 2 de la porte. Détail : `REPRISE.md` à la
  racine du projet. Effort high.
- **Tests** : suite complète **5123/5123**, 0 sauté, 0 avertissement (`Travail/suite-p7-t05.log`) : domaine 1385,
  application 405, persistance 1119 (375 sur chaque base), échange 262, API 1172 (468 sur chaque base), architecture
  **780** ; web : `tsc -b`, ESLint, vitest **1792/1792** ; parcours **90/90** en mode Poste et en mode Serveur
  (`Travail/e2e-p7-t05-*.log`) ; dépôt public prouvé (3692 verts, 46 sautés, 0 échec).
- **Points bloqués** : aucun.
- **Rappel** : `dotnet` avec `DOTNET_CLI_TELEMETRY_OPTOUT=1` et `TESTINGPLATFORM_TELEMETRY_OPTOUT=1` ; solution
  `Topogenia.sln` ; agents ou constructions en parallèle : `--artifacts-path` propre à chacun ; mutations : remettre le
  fichier puis le toucher (`touch`) ; bases SQL Server : seulement `Topogenia_*` ; tests sans SQL écrit à la main ; les
  tests du vrai serveur (`Server/ServerRun`) s'authentifient en NTLM sous `localhost`. Mise à jour du tableau de
  couverture : script dans le scratchpad (cellules Test 2.0 et État d'une ligne par son identifiant, en coupant sur `|`) ;
  aucun texte « topogenia » dans le code de `src/` (règle D13 : littéraux et commentaires compris). Une nouvelle route
  entre dans `RouteTableTests`, la matrice des droits et, si elle n'a pas d'`If-Match`, ses dispenses ; une nouvelle
  requête dans le catalogue des droits (`CommandCatalog.*`).
