<!-- 066-exports-de-la-carte.md -->
<!-- Les exports de la carte : image PNG, PDF A3 et classeur Excel construits dans le navigateur sous la CSP de l'hôte, la classification imprimée, les entrées du menu Fichier (P6-T09) -->

# 066 - Les exports de la carte

Statut : acceptée (date 28/10/2026)

## Contexte

P6-T09 (`plan.md`, phase 6) porte l'annexe B 2.14 (31 lignes « à faire »), B-0-04 (bibliothèques chargées à la demande),
les entrées d'export du menu Fichier que l'ADR 062 avait laissées (B-2.4-56 à B-2.4-58), le cadre jamais exporté
(B-2.9-20, ADR 063), la jauge absente des exports (B-2.10-26), la marge des couloirs dessinée dans l'export (B-2.12-25),
les exports non annulables (B-2.6-45), l'état « export en cours » (B-7-20), le module dédié (B-8.1-06, B-8.1-22,
B-8.1-26), la classification imprimée sur les exports (S-4.1-08, aucune tâche ne la portait) et la preuve que les trois
exports fonctionnent sous la politique de l'hôte (S-5.4-04 ; `style-src 'self'`, `base-uri 'none'`, `img-src 'self' data:
blob:`). Référence 1.x (lecture seule) : `carte/export.ts`, `export.test.ts`, `EditeurCarte.tsx:455-547`,
`styles.css:204-246`, textes `export.*`.

## Décision

1. **Module dédié** (spec 3.5, B-8.1-06) : `features/map/export/` (`capture.ts` : bornes, cadrage, résolution, éléments
   retirés, attente des étapes, polices, capture ; `pdf.ts` ; `workbook.ts`), l'opération `state/actions/exports.ts`, le
   branchement du canevas `canvas/useExport.ts` et `canvas/ExportCaption.tsx`. html-to-image 1.11.11 et jsPDF 4.2.1
   (versions de la 1.x, annexe B 0, licences MIT, `npm audit` à 0) rejoignent exceljs 4.4.0 ; les trois sont chargés
   par `import()` au moment de l'export (B-0-04) et sortent en morceaux séparés à la construction. Le nom du fichier vient
   de `shared/download.ts` (B-8.1-22), qui garde aussi le type de média d'un classeur, partagé avec les traductions ;
   le type du traducteur passe dans `shared/i18n/i18n.ts`, défini une fois pour les deux classeurs (B-8.1-26).
2. **Rendu complet, état d'export** : le canevas 2.0 dessine toujours tous ses éléments (`onlyRenderVisibleElements`
   n'est jamais posé, ADR 060), l'export n'a rien à désactiver ; une étape hors de la vue est dans l'image. L'export en
   cours vit au magasin de l'éditeur (`exporting` : format et légende, B-7-20) ; pour une image il donne au canevas la
   classe `is-exporting`, et l'attente de la 1.x est gardée (3 s au plus que toutes les étapes soient dessinées, puis
   50 ms) pour que ce rendu ait eu lieu avant la capture. Le classeur ne demande aucune image : le canevas ne change pas.
3. **Le canevas donné aux exports** : le canevas inscrit son élément et les bornes de ses nœuds auprès de l'éditeur tant
   qu'il est affiché (`editor.canvas`), et les reprend en partant ; l'opération relit cette inscription après l'attente
   (le canevas a pu partir). Une image demandée sans canevas affiché échoue et le dit ; le classeur n'en a pas besoin.
4. **Pointe des flèches** : le marqueur est déplacé dans la vue de React Flow (`ViewportPortal`) ; hors de la vue, la
   capture de `.react-flow__viewport` perdait sa définition et les flèches sortaient sans pointe.
5. **Cadrage et résolution** (B-2.14-04 à B-2.14-06) : bornes des nœuds sauf le cadre (les couloirs partent de la marge
   de leurs en-têtes), marge 24, plus 20 px de hauteur ; `pixelRatio = clamp(0.5, min(2, 12000/l, 12000/h))` ; fond lu au
   jeton `--surface` au moment de l'export (rien d'écrit en dur, spec 9.2).
6. **Éléments retirés** (B-2.14-07) : boutons « + » et leur pont de survol, poignées, jauges, et le nœud du cadre (en plus
   de la règle CSS qui le cache).
7. **Pendant la capture** (B-2.14-08 à B-2.14-11, B-2.12-25) : `.is-exporting` coupe les animations (`animation-name`) et
   les transitions (`transition-property`), sans toucher aux durées des jetons (règle B-6-53) ; une étape, un losange et
   une flèche choisis s'y dessinent comme les autres, ni rectangle ni boîte de sélection ; le cadre est caché ; la marge
   des couloirs (nom et type sur le pastel, cachée à l'écran sous les en-têtes) est dessinée. Les étapes étrangères au
   cadre perdent `is-outside` par l'état des éléments du canevas (`ElementStates.exporting`) : une règle CSS aurait dû
   défaire les six règles de l'étape atténuée (AR-53).
8. **Polices sous la CSP** (S-5.4-04) : l'image est un SVG dessiné comme une image, qui ne charge aucun fichier ;
   html-to-image lirait les polices en posant une adresse de base dans un document à lui, ce que la politique refuse
   (`base-uri 'none'`, deux violations par fichier de police relevées au premier essai). `fontFaces` lit les règles
   `@font-face` des feuilles de la page, résout chaque adresse par `URL` contre sa feuille, lit chaque fichier une fois par
   `fetch` (`connect-src 'self'`) et le donne en base 64 ; html-to-image les reçoit par `fontEmbedCSS` et ne lit plus rien
   lui-même. Une face dont un fichier ne se lit pas est laissée (le texte prend la police de repli).
9. **PNG** (B-2.14-12) : la toile capturée donne le PNG, téléchargé tel quel, avec la ligne de la classification sous la
   carte.
10. **PDF** (B-2.14-13 à B-2.14-19, B-2.14-31) : A3 paysage, marge 12 mm ; titre Helvetica gras 16 « CODE - nom de la
    carte » ; sous-titre = fil d'Ariane joint par « > » (noms traduits) ; date longue alignée à droite, dans la langue de
    l'interface ; la classification en gras sous la date ; l'image (sans la ligne de classification) ajustée à la place
    laissée entre l'en-tête et le pied ; pied 8 pt « Généré par » + le nom du produit ; `creator` = le nom du produit ;
    textes passés en Latin-1 (guillemets, tirets, points de suspension, espaces insécables et chevron remplacés, tout
    autre caractère hors Latin-1 en « ? », accents français gardés).
11. **Excel** (B-2.14-20 à B-2.14-28, B-2.14-31) : « Étapes » (toutes, début et fin compris, dans l'ordre de lecture
    unique ; Code, Chemin, Étape, Type, Réalise, une colonne par lettre de la carte avec ses couloirs dans l'ordre des
    couloirs, Détaillée, Étapes suivantes dans l'ordre de lecture), « Liens » (dans l'ordre de lecture de leur source puis
    de leur cible ; libellé dans la langue affichée ; une flèche vers une étape absente est laissée), « Matrice RACI » (celle
    du dialogue Matrice, `raciMatrix`). Noms dans la langue de l'interface ; en-têtes figés, gras, sur `--table-header-bg`,
    filtre ; lettres centrées en gras sur `--raci-<lettre>-bg` (un couloir nommé comme une lettre garde l'en-tête) ; chaînes
    vides en cellules vides ; onglets nettoyés, 31 caractères ; `creator` = le nom du produit, titre = nom de la carte. Les
    couleurs sont lues aux jetons de la page au moment de l'export, comme la copie de la matrice (ADR 063) ; une valeur
    illisible laisse le remplissage de côté.
12. **Nom des fichiers** (AR-126, B-2.14-29) : règle de l'hôte (`exportFileName`, A-1.2-16 : code du projet, nom de la
    carte sans accents ni signes, 30 caractères, jour UTC), comme le classeur des traductions et la copie `.pmap`.
13. **Classification** (AR-127, S-4.1-08) : lue avec le projet au moment de l'export (`GET /api/projects/{id}`, tout
    membre) ; « Classification : Interne » imprimé sur le PDF (sous la date), dans l'en-tête d'impression de chaque onglet
    du classeur (`&R`, une esperluette doublée), et en ligne sous la carte dans le PNG. Un projet illisible fait échouer
    l'export.
14. **Notifications et historique** (B-2.14-30, B-2.6-45) : « Export en cours… », puis « Export terminé : le fichier est
    dans le dossier Téléchargements. » ou « L’export a échoué. Réessayer dans un instant. ». Un export ne passe pas par
    la file des écritures : rien n'est écrit, l'historique de la carte ne bouge pas. Un export demandé pendant un autre est
    laissé (la 1.x n'avait pas de garde : deux captures se seraient mêlées).
15. **Menu Fichier** (B-2.4-56 à B-2.4-58) : après un séparateur, « Exporter en PDF (A3 paysage) », « Exporter en image
    PNG », « Exporter en Excel », pour tout lecteur ; puis, après un autre séparateur, le paquet d'échange de l'animateur.
    Aucun export SVG (B-2.14-01).

## Options écartées

- Désactiver le rendu limité à l'écran pendant l'export comme la 1.x : la 2.0 ne le pose jamais (une carte de 300
  étapes se dessine entière) ; l'activer hors export aurait changé le canevas de P6-T04 et ses tests.
- `skipFonts` de html-to-image : sans police intégrée, l'image prend la police de repli et les libellés mesurés en Inter
  débordent ou se coupent autrement.
- Ajouter `'unsafe-inline'` ou ouvrir `base-uri` dans la politique : la spec 5.4 la veut stricte ; c'est l'export qui
  s'y plie.
- Dessiner la classification sur la toile après la capture (canevas 2D) : la ligne dans la vue est capturée avec la
  carte, testée sans canevas réel, et suit les jetons.
- Garder la règle de nom de la 1.x (40 caractères, jour local) : deux règles de nom pour les fichiers de la même
  application.

## Dépendances

jsPDF tire `fflate` et `fast-png` (MIT), et des dépendances facultatives que l'application n'appelle jamais (elles ne
servent qu'à `html()` et au SVG de jsPDF) : `html2canvas`, `canvg`, `core-js` (MIT), `dompurify` (MPL-2.0 OU Apache-2.0,
l'option Apache-2.0 est retenue), `rgbcolor` (MIT OU sa licence propre, MIT retenue). Vite les sort en morceaux à part,
servis mais jamais chargés ; leur retrait à la construction est inscrit au registre (F-31), avec la SBOM de P7-T03.
`THIRD-PARTY-NOTICES.md` régénéré.

## Vérification

- Interface : `features/map/export/capture.test.ts`, `pdf.test.ts` (PDF relu : page, polices, positions, gris, image,
  propriétés), `workbook.test.ts` (classeur relu par exceljs), `sources.test.ts` (aucun import statique des trois
  bibliothèques, `import()` aux seuls endroits voulus ; un seul type `Translate`), `features/map/state/actions/exports.test.tsx`
  (PNG, PDF, classeur, langue, échecs, historique intact, un export à la fois, couleurs des jetons, attente avant la
  capture), `features/map/canvas/exportState.test.tsx`, `features/map/panels/fileMenu.test.tsx`, `app/EditorBar.test.tsx`
  (entrées du menu par rôle), `styles/map.test.ts` (règles `.is-exporting`), `features/translations/model/workbook.test.ts`
  (créateur du classeur des traductions).
- Mutations sur une copie du web (`Travail/outils-p6-t09/mutate-t09.py`, journal `Travail/mutations-p6-t09.log`) : 132
  mutations, 128 tuées au premier passage, 4 après renfort des tests (la date et la classification finissent exactement
  sur la marge, largeur mesurée par jsPDF ; rôles d'une même lettre donnés hors de l'ordre des couloirs ; la capture
  attend au moins 50 ms après l'état d'export ; le classeur anglais nomme étapes et couloirs dans la langue affichée) :
  132 sur 132.
- Vérification visuelle dans Chromium sans tête **sous la CSP de l'hôte** (`Travail/outils-p6-t09/capture-t09.mjs`) : les
  trois exports lancés depuis le menu Fichier, fichiers enregistrés puis relus (PNG 6244 × 904, PDF A3 et son image
  6244 × 856, classeur à trois onglets) ; aucune violation de la politique ni erreur de console après la correction des
  polices (14 violations `base-uri` avant) ; sonde finale : `eval` refusé à un script de la page.

## Révision (28/10/2026, P6-T09b) : police Inter intégrée au PDF (AR-52)

Le point 10 écrivait les textes du PDF dans les polices standard de jsPDF, donc en Latin-1 : contraire à AR-52, qui
intègre Inter au PDF pour que les langues ajoutées par configuration s'impriment (AR-29). Relevé au portage des tests
(P6-T10, ADR 067 § 8).

- **Fontes** : jsPDF ne lit que des fontes TrueType ; celles de l'interface sont des woff2 variables
  (`@fontsource-variable/inter`). Les faces Regular et Bold viennent du paquet npm `inter-font` 3.19.0 (OFL-1.1, livré),
  republication des fichiers officiels d'Inter 3.019 (table `name` vérifiée : « Copyright 2020 The Inter Project
  Authors », « Version 3.019;git-0a5106e0b », contours `glyf`). Vite les sort en fichiers à part (310 et 316 ko),
  lus par l'application seulement au moment d'un PDF (`export/pdfFonts.ts`, `fetch` de la même origine, permis par
  `connect-src 'self'`), jamais au chargement (B-0-04).
- **Document** : `buildPdf(image, en-tête, fontes)` enregistre les deux faces comme deux familles, `Inter-Regular` et
  `Inter-Bold`, noms que montre un lecteur de PDF ; titre en Inter-Bold 16, sous-titre, date et pied en Inter-Regular,
  classification en Inter-Bold ; les textes sont écrits tels quels (accents de toutes les langues de la police,
  guillemets, tirets, espaces insécables), la fonction `latin1` est retirée. jsPDF n'écrit que les glyphes employés et
  leur table ToUnicode (recherche et copie du texte) ; un PDF d'une page avec son image pèse environ 220 ko. Le titre des
  informations du fichier s'écrit en UTF-16.
- **Vérification** : `export/pdf.test.ts` relit chaque texte par la table ToUnicode de sa fonte (`test/pdf.ts`) : faces,
  tailles, places, texte polonais et turc imprimé tel qu'écrit, deux fontes embarquées, fichier plus léger que la moitié
  des deux faces ; `export/pdfFonts.test.ts` (lecture des deux fichiers, chaîne binaire) ; le parcours
  `web/e2e/exports.spec.ts` « PDF A3 … in Inter, whatever its language » sur le vrai hôte, dans les deux modes, sous la
  CSP (projet « Księgowość – Şube »). Couverture : B-2.14-14 et B-2.14-19 `corrigé (AR-52)`.
