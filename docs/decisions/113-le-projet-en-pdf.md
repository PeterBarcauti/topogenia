<!-- 113-le-projet-en-pdf.md -->
<!-- The project as one PDF (3.4, lot 5): built in the browser by jsPDF, every map redrawn from its view without an editor, the sheets read once from one copy of the branch with purpose pdf, two page formats, one final pass for the classification and the page number, bounds of 100 maps, 12 levels and 1000 pages -->

# 113 - Le projet en PDF

Statut : acceptée le 21/11/2026 (3.4, lot 5). S'appuie sur les ADR 066 (les exports de la carte), 083 (le texte que la
police ne sait pas écrire, dessiné en image), 106 (la copie lue par l'interface pour écrire un fichier, avec son
événement), 109 (« Copier en image »), 110 (la règle des fiches appelée, jamais réécrite) et 112 (`planEntries` et la
route du plan d'actions). Demande du porteur du projet : remettre le projet entier, cartes et fiches, à qui n'a pas
l'application, dans un seul fichier.

## Contexte

- L'export « Exporter en PDF (A3 paysage) » écrit une carte : l'image est une capture de la toile montrée
  (`export\capture.ts`), qui demande un éditeur monté par carte. Pour cent cartes, il faudrait ouvrir chaque carte dans
  l'éditeur, attendre son dessin et changer ce que la personne voit.
- Une seconde voie existe, sans toile : l'image SVG redessinée depuis la vue de la carte (`svgText`, `exports.ts:93-108`,
  puis `layoutMap` et `mapSvg`, `export\svg.ts`). L'affiche du mur la dessine déjà en JPEG, page à page
  (`windowPainter`, `exports.ts:133-153`).
- La règle des fiches est dans le cœur : `SheetRights.ViewerOf` et `SheetVisibility` (celui qui peut remplir et
  l'animateur voient le brouillon, les autres la dernière version validée ou rien). La copie d'une carte et de ses niveaux
  l'applique à chaque étape (`MapPmapExport`, `PmapExchange.cs:89-108`, `viewerOf` ligne 95). Lire une fiche par étape
  (`GET /api/steps/{id}/sheet`) charge tout le projet à chaque appel : cinq cents fiches, cinq cents chargements.
- Le cœur ne borne pas le nombre de cartes ; la profondeur va jusqu'à 50 (`ProjectWorkspace.MaxDepth`), la copie d'une
  branche s'arrête à 12 niveaux (`PmapFormat.DepthMax`), l'affiche à 100 pages (`WALL_PAGES_MAX`, 150 dpi).
- jsPDF 4.2.1, déjà livré et chargé à la demande, suffit : `addPage(format, orientation)` par page, polices Inter
  embarquées, mesure du texte, images JPEG, liens internes (`link`) et signets (`outline.add`).

## Décision

1. **Construit dans le navigateur, par jsPDF, sans dépendance.** Le dessin d'une carte n'existe que dans l'interface ;
   l'hôte ne fait que servir des lectures existantes et une copie dont l'événement dit « pdf ». Fichiers nouveaux, sous
   `web\src\features\map\export\project\` : `projectWalk.ts` (la marche), `mapRaster.ts` (une carte en image),
   `sheetBlocks.ts` (une fiche en blocs de texte), `pageFlow.ts` (les blocs coulés sur les pages), `tablePages.ts` (RACI et
   plan d'actions en tableau), `projectPdf.ts` (le document) ; `state\actions\projectPdf.ts` (la tâche : lectures,
   avancement, abandon, téléchargement) ; `panels\ProjectPdfDialog.tsx` (le dialogue). Commande `file.project-pdf`, ligne du
   menu Fichier après « Exporter en PDF (A3 paysage) », pour tout membre et dans la vue BPMN. Chaque fichier sous 600
   lignes. La tâche ne passe pas par `exporting` de `mapStore.ts` : la toile n'entre pas en état d'export.

2. **Chaque carte est redessinée depuis sa vue, une à la fois, sans éditeur ni toile.** Le SVG de l'export « Image SVG »
   (`svgText`, rendu indépendant de l'éditeur : préférences et rangées passées en paramètres, rangées vides pour une
   carte non montrée), polices Inter embarquées, dessiné par le navigateur en JPEG à 150 dpi (qualité 0,92) sur la surface
   de l'image de la page A3 (`imagePlacement`), par le peintre de l'affiche (`windowPainter`, la fenêtre étant toute la
   `viewBox`). Vue, SVG, canevas, image ajoutée, puis le canevas est rendu : la mémoire est libérée carte après carte.
   Les aides de `exports.ts` et de `pdf.ts` sont rendues indépendantes de l'éditeur sans changer un export existant
   (`writeMapPage` tiré de `buildPdf`, `writeStreamPages` exporté). Écart connu : la page d'une carte est le dessin SVG,
   non la capture de la toile ; une différence de détail se signale (ligne à peaufiner), elle ne justifie pas un éditeur
   par carte.

3. **La marche suit `sub_map_id`, niveau par niveau, dans l'ordre de lecture ; bornes, avancement et abandon.** Départ :
   la carte principale, ou la carte montrée pour « Cette carte et ses niveaux ». `GET /api/maps/{id}` carte par carte ;
   dans un niveau, les cartes dans l'ordre de lecture de leurs étapes parentes (celui de la vue, non l'ordre
   d'enregistrement du cœur). Une carte vide a sa page, « Carte sans étape. ». Bornes : **100 cartes** (à la 101e la marche
   s'arrête, rien n'est lu de plus, aucune fiche n'est demandée, donc aucun événement), **12 niveaux** sous le départ (ceux
   de la copie ; au-delà, les cartes ne sont pas dans le document et le sommaire le dit, sauf pour une carte détaillée vide,
   qui ne manque pas ; au-dessus, une carte détaillée vide a sa page et compte parmi les 100), **1 000 pages** (compté pendant
   l'écriture ; à la 1 001e, rien n'est téléchargé). Le dialogue reste ouvert et dit l'étape avec une barre `<progress>`.
   « Annuler », Échap ou la fermeture arrêtent la tâche par un `AbortController` passé aux lectures et vérifié entre deux
   cartes et entre deux pages ; rien n'est téléchargé, « Export annulé. ».

4. **Les fiches viennent d'une seule copie de la branche, `purpose=pdf`, selon la règle du cœur.** Si « Avec les fiches »
   est cochée, une seule lecture `GET /api/maps/{départ}/pmap?purpose=pdf`, une fois la marche finie et la borne tenue.
   L'hôte (`MapCopyPurpose.Pdf`, `PmapApi.MapCopyAsync`) applique `viewerOf` (appelé, non recopié) et décrit la branche par
   `IPmapFormat.OutlinePrint`, **sans emplacement des documents et sans commentaires de présentation**
   (`WithLocations = false`, aucun extra, donc aucun glossaire ; l'état de chaque irritant servi comme pour la vue BPMN ; pas de nom de fichier,
   la réponse servant à faire le PDF et non à être gardée) : l'hôte ne sert pas ce que le PDF n'imprime pas. Au-delà de
   12 niveaux, l'en-tête `X-Pmap-Warning` signale la coupure. Événement `project.exported`,
   format `pdf`, écrit avant que le texte parte, comme pour le .bpmn ; gardé si l'on annule après la lecture ; aucun
   événement sans fiches. Dans l'interface, une étape a sa fiche si la copie lui en donne une non vide (jamais « à
   remplir », jamais une fiche vide) ; l'état écrit dessous est celui que la carte montre à la personne. Les textes
   viennent des fonctions pures livrées (`durationText`, `factsText`, `citedTools`), qui ne sont pas récrites. Aucune route
   nouvelle (`RouteTableTests` inchangé), même droit lecteur, aucune migration, aucun fichier du cœur.

5. **Le plan d'actions vient de `planEntries` et de sa route.** `GET /api/maps/{départ}/action-plan` et
   `planEntries(...).cells` (ADR 112), les onze valeurs de l'onglet Excel, dans l'ordre de l'hôte, en-tête répété,
   à la fin du document, s'il a une ligne parmi les cartes du document (la route rend tous les niveaux sous le départ ;
   les lignes des cartes au-delà de 12 niveaux sont laissées). Aucune troisième construction. Le plan garde l'ordre de l'hôte (ordre
   d'enregistrement, celui de l'export Excel) alors que les cartes suivent l'ordre de lecture : l'écart se dit au manuel
   (F-223).

6. **Deux formats de page.** A3 paysage pour ce qui se dessine par carte (la carte, le flux de valeur par
   `writeStreamPages`, la matrice RACI, le plan d'actions) ; A4 portrait pour le texte (titre, sommaire, fiches). Toute
   mise en page lit la taille de **sa** page, jamais une constante ; les essais relisent la `MediaBox` de chaque page
   (`pdfPages` dans `test\pdf.ts`). À l'impression sur A4, la case « Ajuster » réduit les pages A3 ; le manuel le dit.

7. **La classification et le numéro de page sont posés par une seule passe sur toutes les pages.** En haut à droite, en
   gras et gris, au même endroit que le PDF d'une carte ; en pied, « Généré par Topogenia · CODE » à gauche et « Page n sur
   N » à droite. C'est le seul endroit qui les écrit : une page ajoutée par n'importe quelle partie les reçoit, de
   l'une ou l'autre taille. Le sommaire (une ligne par carte, liens internes, nombre de pages connu avant l'écriture,
   réservées puis remplies) et les signets imbriqués par niveau se numérotent après coup. Propriétés du fichier : titre
   « CODE - projet », créateur le produit. Nom du fichier : `exportFileName(code, "<nom du projet> - projet", ".pdf")`.

8. **Les noms sont imprimés tels que l'équipe les a écrits ; un emplacement de document, jamais.** L'acteur de la fiche
   et le « responsable » d'une action du plan (comme dans l'export Excel) sont le contenu du processus, imprimés tels
   qu'écrits (arbitrage du 21/11/2026). L'emplacement des documents n'est jamais imprimé ; l'irritant d'une entrée est
   cité par son code, qui renvoie au plan d'actions, et le plan ne porte que les irritants des cartes du document.
   Chaque personne obtient le document de son rôle : l'animateur le brouillon de chaque fiche, le contributeur celui des
   fiches qu'il peut remplir et la dernière version validée des autres, le lecteur la dernière version validée seulement ;
   cartes, matrice RACI et plan sont les mêmes pour tous. Le projet « Strictement confidentiel » s'exporte, sa
   classification imprimée sur chaque page.

## Écarté

- **La capture de la toile carte par carte** : un éditeur monté par carte, et ce que la personne voit changerait.
- **Le PDF construit par l'hôte** : une bibliothèque PDF .NET (dépendance nouvelle, interdit 2 de REGLES § 3) et un second
  dessin des cartes (deux sources d'un même fait).
- **Une route par fiche** : un chargement du projet par fiche.
- **Une route nouvelle des fiches** : une troisième lecture des fiches à côté de la fiche et de la copie, une ligne de plus
  aux droits.
- **`addSvgAsImage` de jsPDF** : il demande `canvg`, dépendance facultative non livrée.
- **Des cartes en vecteurs (`svg2pdf.js`)** : dépendance nouvelle. Les cartes sont des images (texte non cherchable sur la
  carte) ; le titre, le chemin, les fiches, le sommaire, la matrice et le plan restent du texte cherchable.
- **Un seul format de page** : tout en A3 paysage rendrait les fiches illisibles en largeur et lourdes à imprimer.
- **Le choix « Pages de … à … »** : le sommaire et l'étendue suffisent.
- **L'ordre du cœur pour les cartes** : l'ordre d'enregistrement n'est pas un ordre de lecture.

## Conséquences

- Ni fichier du cœur fermé, ni migration, ni dépendance, ni appel sortant (`connect-src 'self'` inchangé ; aucun `fetch`
  nouveau, `outgoingCalls.test.ts` inchangé). L'application change à trois endroits : `PmapExchange.cs`
  (`MapCopyPurpose.Pdf`, l'événement), `IPmapFormat.cs` et `PmapFiles.cs` (`OutlinePrint`), `PmapApi.cs` (`"pdf"`).
- Deux personnes de rôles différents qui exportent le même projet n'ont pas le même document ; le manuel le dit.
- Un fichier de quelques dizaines de Mo à cent cartes ; le pic de mémoire du navigateur est deux à trois fois plus à
  l'écriture. L'essai « très grand » mesure le temps et la taille au bord de la borne.
- `exports.ts`, retouché par le lot 1, est touché par la tâche d'aides sans changer de comportement.
- Le sommaire et l'étendue sont l'issue honnête des bornes : une carte détaillée et ses niveaux, ou décocher les fiches.

## Vérification

- Application : `PmapExchangeTests` : la copie « pdf » est la branche avec les fiches telles que la personne les voit et
  son événement dit « pdf » ; sans emplacement ni commentaire de présentation ; lecteur (dernière version validée, jamais
  le brouillon) ; contributeur (brouillon de ce qu'il remplit) ; arrêt à 12 niveaux. Échange : `OutlinePrint` égale
  `OutlineBranch` sans emplacements ni extras (aucun glossaire). Hôte (deux bases) : `ProjectPdfCopyRoutesTests` (chaque rôle, un événement
  `project.exported` au format `pdf` lu par `GET /api/admin/audit`, 404 pour un étranger et un projet à la corbeille,
  autre but toujours refusé). `RouteTableTests` et `RightsMatrixTests` inchangés, `Topogenia.Architecture.Tests` verts.
- Interface : `mapRaster.test.ts`, `sheetBlocks.test.ts`, `pageFlow.test.ts`, `tablePages.test.ts`,
  `projectPdf.test.ts` (le document, deux tailles de page, la passe finale, au-delà de 1 000 pages), `projectWalk.test.ts`
  (ordre, départ, 101e carte, 12 niveaux, abandon), `state\actions\projectPdf.test.ts` (la copie lue une fois, avec
  `pdf`, après la marche ; abandon ; refus de l'hôte), `projectPdfDialog.test.tsx` ; `pdf.test.ts`, `wallPdf.test.ts`,
  `exports.test.tsx` verts sans changer leurs attentes.
- Parcours : `web\e2e\project-pdf-34.spec.ts`, en mode Poste et en mode Serveur, dont l'essai « très grand » (100 cartes
  de 20 étapes, Poste seulement) et le refus de la 101e.
- Témoins de mutation vus rouges puis remis : `viewerOf` remplacé par `new SheetViewer(true, true)` ; rôle forcé à
  animateur ; `WithLocations = true` ; événement écrit au format `pmap` ou non écrit ; `pdf` lu comme `view` ; classification
  d'une passe qui commence à la page 2, ajoutée avant les pages de fiches, ou écrite par l'en-tête des cartes seulement ;
  copie lue avec `purpose: 'view'` ; fiche prise dans la vue quand la copie n'en donne pas.
