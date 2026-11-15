<!-- 065-traductions.md -->
<!-- Les traductions côté interface : classeur construit dans le navigateur, consigne, glossaire en réglage du projet, retour du classeur, traductions manuelles des étapes, flèches et couloirs (P6-T08) -->

# 065 - Les traductions

Statut : acceptée (date 28/10/2026)

## Contexte

P6-T08 (`plan.md`, phase 6) porte l'interface de l'annexe B 5 : 28 lignes, dont 22 « à faire », et les six lignes
couvertes côté serveur (B-5-04, 05, 06, 22, 23, 28) à compléter par l'interface ; plus B-8.1-45 et B-8.2-12 (le glossaire
en données, réglage du projet, spec 10). Les routes de l'hôte sont faites depuis la phase 4 (`GET/PUT
/api/projects/{id}/translations`, `PUT /api/steps/{id}/translations`, `PUT /api/flows/{id}/translations`, `PATCH
/api/lanes/{id}` avec `translations`). Référence 1.x (lecture seule) : `traduction/DialogueTraductions.tsx`, `classeur.ts`,
`classeur.test.ts`, textes `traduction.*`. Le glossaire n'existait nulle part en 2.0 : la 1.x l'écrivait dans le code
(glossaire finance), la spécification (section 10) en fait un réglage du projet, comme le libellé du groupe de partage
(AR-64).

## Décision

1. **Glossaire en réglage du projet** (AR-124) : champ `translation_glossary` du projet, texte libre sur plusieurs lignes,
   un terme par ligne (« clôture mensuelle = monthly closing »), 4 000 caractères au plus, règles de texte communes (un
   texte vide l'efface). Même chemin que le libellé du groupe de partage : domaine `ProjectSettings.ChangeTranslationGlossary`,
   commande `ChangeTranslationGlossaryCommand` (Animateur, `If-Match` du projet, hors journal des opérations, aucune carte),
   route nommée par l'hôte `PUT /api/projects/{id}/translation-glossary` `{glossary}` qui rend le détail du projet ;
   `ProjectDetail.translation_glossary`, lisible par tout membre. Migration `TranslationGlossary` sur les deux moteurs
   (colonne et sa garde de texte, ADR 038 ; le `Down` retire sa ligne de `schema_history`, ADR 027). Le paquet
   `.topogenia` le transporte (`translationGlossary`, facultatif, revu par `ImportedRecords`, schéma 2.0 complété,
   `docs/format-topogenia.md`) ; le `.pmap` non, il ne porte aucun réglage du projet.
2. **Consigne** : texte de l'interface (fichiers de langue), sans contenu propre à un client ; elle nomme les colonnes des
   langues configurées (AR-29 ; la 1.x disait « FR ou EN ») et les longueurs (150, 200 pour le projet et les cartes, 60
   pour les flèches, A-2.14-22). Quand le projet a un glossaire, une phrase le cite et il suit la consigne, dans la
   feuille « Consigne » comme dans le texte copié (B-5-15) ; sans glossaire, ni la phrase ni le titre.
3. **Classeur** (`features/translations/model/workbook.ts`) : exceljs 4.4.0 (annexe B 0 ; version de la 1.x), chargé à la
   demande par `import()` dans son propre morceau (929 ko). `uuid` forcé en 11.1.1 sous exceljs par `overrides` : la
   version 8 qu'il demande a une alerte (GHSA-w5hq-g745-h8pq) ; la 11.1.1 la corrige et garde le `require` qu'exceljs
   utilise (`npm audit` : 0). **Précision (29/10/2026, porte P6 round 3)** : `overrides` ne vaut que pour l'arbre de
   `package-lock.json` (la partie Node d'exceljs, jamais servie) ; le paquet navigateur servi, `dist/exceljs.min.js`, est
   une construction browserify d'exceljs qui embarque sa propre série 8 d'`uuid` (et 67 autres paquets, listés dans
   `THIRD-PARTY-NOTICES.md` par sa carte des sources) ; exceljs n'y appelle que `v4`, que l'alerte ne touche pas. Feuille des textes : clé cachée `type:id`, Élément, Code, Contexte, une colonne par langue
   configurée, l'original dans la sienne, les autres vides ; volet figé, filtre de l'élément à la dernière langue, retour
   à la ligne (B-5-07 à B-5-09). Les noms des feuilles et les en-têtes suivent la langue de l'interface (la 1.x avait des
   noms français) ; la feuille de contrôle, `veryHidden`, porte le nom du produit (`PRODUCT_NAME`, le même dans toutes les
   langues, AR-28) et garde le projet, la portée, les langues et le nom de la feuille des textes. Nom du fichier par la
   règle de l'hôte : `CODE_traductions_aaaa-mm-jj.xlsx`.
4. **Relecture** (`model/workbookReader.ts`) : `FICHIER_ILLISIBLE` ; `FICHIER_SANS_FEUILLE_PROJET` sans feuille de contrôle
   complète (projet, langues, feuille des textes) ou sans la feuille des textes qu'elle nomme, ce qui refuse aussi un
   classeur de la 1.x (AR-28) ; `FICHIER_AUTRE_PROJET`. Les lignes sont lues par leur clé cachée (tri et filtre acceptés),
   texte riche, formule (son résultat), nombre et lien lus comme du texte, une date ou une erreur non ; une cellule vide
   n'est pas envoyée ; `original` vaut null, l'hôte lit la colonne de la langue d'origine (A-2.14-19). Une ligne sans clé
   est passée sous silence (1.x) ; une clé qui n'est pas celle d'un objet (type connu, identifiant du contrat) ne peut pas
   partir, le contrat typant l'identifiant : elle est comptée avec les lignes `OBJET_INTROUVABLE` du compte rendu, ce que
   le serveur 1.x aurait répondu.
5. **Dialogue** (`TranslationsDialog.tsx`, `DialogParts.tsx`, `useGlossary.ts`, `useWorkbook.ts`) : large, les trois
   étapes de la 1.x (B-5-02). Étape 1 : la portée (« Sans traduction » par défaut), le glossaire du projet et
   « Télécharger le classeur » ; un glossaire modifié est enregistré avant le téléchargement, et le bouton « Enregistrer
   le glossaire » le fait seul. Conflit de version : le projet est relu, le champ reprend le glossaire enregistré, le
   message le dit et le téléchargement s'arrête. Étape 2 : « Copier la consigne pour Copilot ». Étape 3 : le classeur
   rapporté, envoyé en une passe avec sa portée, puis le compte rendu (« N traductions enregistrées », lignes ignorées par
   raison dans un ordre fixe, numéros croissants) et la carte relue par la file de l'éditeur (`editor.reload`, B-5-24) ;
   `TRADUCTIONS_INVALIDES` : le message de l'hôte et une ligne par ligne refusée. Pendant un appel, la portée, le
   glossaire et les deux boutons qui appellent l'hôte attendent.
6. **Traductions manuelles** (AR-125, B-5-28, spec 10) : « Traduire… » aux menus d'une étape, d'une flèche libellée et d'un
   couloir (la route du couloir existait, A-2.14-03, sans écran en 1.x non plus). `ManualTranslationsDialog.tsx` : le texte
   saisi et sa langue, un champ par autre langue configurée, rempli de la traduction actuelle ; seuls les champs changés
   partent, rognés ; un champ vidé envoie un texte vide, qui supprime (A-2.14-04) ; longueurs 150 (étape, couloir) et 60
   (flèche) ; la langue d'origine d'une flèche est celle de son étape de départ. Écriture par `editor.write`
   (`map/state/actions/translations.ts`) : la vue de structure rendue est appliquée ; un refus laisse le dialogue ouvert,
   l'éditeur dit pourquoi. Une traduction saisie ici est manuelle : un classeur ne la remplace qu'avec « Tout » (aide du
   dialogue).
7. **Langues** : `App` donne à `MapScreen` les langues du profil (comme `admin`), l'éditeur les garde
   (`editor.languages`). Une seule langue configurée : ni bouton Traductions dans l'en-tête ni « Traduire… » (rien à
   traduire). Le bouton suit le droit de modifier la structure (B-5-01) : un lecteur ne le voit pas.
8. **Affichage traduit** (B-5-25 à B-5-27) : `localizedName` (P6-T02 à T04) rend l'original dans sa langue d'origine,
   même à côté d'une traduction écrite dans cette langue, sinon la traduction, sinon l'original ; il s'applique aux
   projets (accueil), aux cartes (fil d'Ariane, matrice RACI), aux couloirs, étapes et libellés de flèches (carte).

## Écart dans les tests de la persistance

Trois tests écrivaient un projet complet avec le modèle courant dans une base laissée à une migration ancienne
(`SqliteRebuildTests`, `AtomicMigrationTests`, `CollationMigrationTests`) : la première colonne ajoutée depuis la migration
initiale les fait échouer (`translation_glossary` inconnue du schéma ancien). Aucune assertion n'est retirée ni assouplie ;
la base ancienne est produite autrement : migrée jusqu'au bout, le projet écrit, puis redescendue à la migration voulue par
les `Down` des migrations, dont l'exactitude est prouvée par `MigrationDescentTests` (et, pour SQL Server, par
`CollationMigrationTests`, qui compare maintenant aussi le schéma après cette descente avec celui de la montée).
`AtomicMigrationTests` suivait déjà ce chemin, mais écrivait le projet après la descente. Le projet écrit est
`ProjectGraph.BeforeGlossary()` : tous les champs sauf le glossaire, ce qu'une version antérieure pouvait écrire ; le
graphe ordinaire porte un glossaire sur plusieurs lignes, relu à l'identique sur les deux moteurs. Toute colonne ajoutée
plus tard suit la même voie.

## Options écartées

- Glossaire structuré (un tableau d'une colonne par langue) dans une table nouvelle : plus de schéma, de purge, d'import
  et d'export pour un texte qu'une IA lit tel quel ; il serait aussi à refaire à chaque langue ajoutée.
- Glossaire seulement dans le classeur, à saisir à chaque téléchargement : la spécification en fait un réglage du projet.
- Noms de feuilles fixes en français (1.x) : un classeur anglais garderait des feuilles françaises ; la feuille de contrôle
  nomme la feuille des textes, la relecture ne dépend pas de la langue.
- Lire un classeur de la 1.x : écarté par AR-28.

## Vérification

- Serveur : `ProjectCreationTests.The_glossary_of_the_translations_is_a_project_setting_kept_on_several_lines_and_cleared_by_an_empty_text`,
  `ProjectDistinctionTests.The_glossary_of_the_translations_holds_four_thousand_characters_and_refuses_one_more_or_a_tag`,
  `ImportedRecordsTests` (`project-glossary-html`), `ProjectCommandsTests.The_facilitator_changes_the_glossary_of_the_translations_with_if_match_the_detail_gives_it_and_nothing_is_logged`,
  `ProjectRoutesTests.The_glossary_of_the_translations_is_changed_by_the_facilitator_on_several_lines_and_read_by_every_member`
  (deux moteurs), `RouteTableTests`, `RightsMatrixTests`, `IfMatchRequirementTests`, `StructureEntryTests`,
  `PackageSchemaTests`, `SamplePackageTests` (exemple réécrit), allers-retours de la persistance (graphe à glossaire),
  `MigrationDescentTests`.
- Interface : `features/translations/model/workbook.test.ts`, `workbookReader.test.ts`,
  `features/translations/translationsDialog.test.tsx`, `importWorkbook.test.tsx`, `features/map/panels/translations.test.tsx`,
  `app/translationsButton.test.tsx`, `shared/i18n/localizedName.test.ts`, `styles/translations.test.ts`.
- Mutations sur une copie du web (`Travail/outils-p6-t08/mutate-t08.py`, journal `Travail/mutations-p6-t08.log`) : 81
  mutations, 77 tuées au premier passage, 3 après renfort des tests (le titre du glossaire absent quand il n'y en a pas,
  le compte rendu d'avant effacé par un nouveau classeur, le champ du fichier vidé après chaque choix), une équivalente
  retirée du code avec sa garde (envoi sans changement, déjà empêché par le bouton désactivé) : 80 sur 80. Avant le lot,
  trois branches qui ne changeaient rien retirées du code (une date lue comme le reste, la position du séparateur d'une
  clé, l'envoi d'aucun changement), ainsi que le filtre sur le seul code `TRADUCTIONS_INVALIDES` des lignes refusées.
- Délais : exceljs lit et écrit lentement dans jsdom, davantage sous la suite entière (un test hors délai à 1 s sous la
  charge d'une compilation) ; les attentes de ses résultats passent à 5 s et les tests qui manipulent un classeur à 15 s,
  comme les tests lents de la carte (`MapScreen.test.tsx`) ; aucune assertion ne change.
- Vérification visuelle dans Chromium sans tête sous la CSP de l'hôte (`Travail/outils-p6-t08/capture-t08.mjs`) : classeur
  construit et téléchargé par le navigateur, relu (trois feuilles, contrôle `veryHidden`, glossaire en fin de consigne),
  rapporté, compte rendu, traduction manuelle ; aucune violation de la politique, aucune erreur ; une sonde finale prouve
  que la politique est en vigueur (`eval` refusé à un script de la page).
