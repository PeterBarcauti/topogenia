<!-- LISEZMOI.md -->
<!-- Mode d'emploi de la maquette statique de la charte (P1-T07) et de ses captures (P1-T08), avec la correspondance charte et arbitrages. -->

# Maquette statique de la charte graphique (P1-T07, P1-T08)

## Ouvrir la maquette

- Double-cliquer sur `maquette.html` : un seul fichier, autonome, sans aucun appel réseau (D14).
  La police Inter (woff2 latin et latin-ext, en base64), les icônes Lucide (SVG en ligne) et le
  contenu de `web/src/styles/tokens.css` (inséré tel quel) y sont embarqués.
- Le fichier est **généré** : ne pas le modifier à la main. Pour le reconstruire, puis refaire les
  captures, depuis `web` :

```
node scripts/build-mockup.mjs
node scripts/capture-mockup.mjs
```

- Sources : `web/scripts/build-mockup.mjs` (assemblage et garde-fous : aucune ressource hors du
  fichier, ni adresse réseau ni chemin relatif ou absolu, aucun caractère pictographique, aucun
  gabarit non résolu) et `web/scripts/mockup/` (gabarit `template.html`, fragments HTML, trois
  feuilles CSS, `map-data.mjs` pour les données fictives de la carte, `map.mjs` pour son rendu,
  `palette.mjs` pour la section des jetons, `icons.mjs` pour les icônes, `self-contained.mjs`
  pour la recherche des ressources extérieures).
- Les contrastes affichés sont calculés à la construction avec `contrastRatio` de
  `web/src/shared/theme/laneTints.ts`, sur les valeurs lues dans `tokens.css`.
- `capture-mockup.mjs` refuse toute ressource extérieure écrite dans `maquette.html` (adresse
  réseau, chemin relatif ou absolu, `@import`), bloque et signale toute requête qui ne vise pas
  le fichier lui-même, vérifie que la police Inter est **réellement** chargée et utilisée
  (`document.fonts.check` ne prouve rien : il répond vrai même sans `@font-face`) : la famille
  déclarée en tête de `--font-sans` a au moins une `FontFace` à l'état `loaded` après
  `document.fonts.ready`, et les polices dont Chromium se sert pour un libellé d'étape (CDP
  `CSS.getPlatformFontsForNode`) sont toutes des polices embarquées (`isCustomFont`), jamais une
  police système de même nom ; puis vérifie :
  - les **contrastes de texte** (NF08, 4,5:1) : texte propre de chaque élément visible, texte des
    pseudo-éléments `::before` / `::after` dont le `content` est un texte, texte SVG (couleur
    `fill`). Le fond est **ce qui est peint sous le texte** : la page est capturée une seconde
    fois avec tous les textes transparents, et chaque pixel du rectangle de chaque ligne de texte
    est lu (fonds des ancêtres et des éléments voisins placés dessous, pseudo-éléments, dégradés,
    ombres) ; le pixel le plus défavorable est retenu, la couleur du texte étant composée avec
    les opacités des ancêtres. Les parties d'un texte coupées par un ancêtre (`overflow`) ou
    recouvertes par un autre élément sont exclues ; une couche passagère (menu, notification,
    infobulle) masque aussi la bande de 16 px de son ombre ;
  - les **objets graphiques utiles** (WCAG 1.4.11, 3:1) : jauges de fiche et de complétude,
    pastilles « ! », pointillés de l'attente, bordure double de la fin, bordures des champs de
    saisie, contour de l'élément choisi du ruban, de « Formulaire / Écrire librement » et de
    « FR / EN », contour de focus des menus, cases à cocher, puces radio, flèches et pointes sur
    les deux fonds de couloir, contour des losanges ;
  - la **mise en page des libellés** : libellé d'étape de 3 lignes au plus, à 1 px au moins de la
    bordure, sans passer sous les pictogrammes, à 2 px au moins au-dessus de la jauge de fiche ;
    étiquette de flèche sans recouvrir une étape, une séparation de couloirs, une autre étiquette
    de flèche ni le libellé d'un losange ;
  - les **écrans 1366 × 768** : l'écran mesure 1366 × 768 dans la page, et son image aussi ;
  - un **autocontrôle** passe d'abord les mêmes vérifications sur une page témoin fautive
    (pseudo-éléments, texte SVG, texte ordinaire trop pâle, texte sombre sur un élément voisin
    sombre peint dessous alors que ses ancêtres sont blancs, champ et flèche trop pâles,
    étiquette de flèche sur une séparation de couloirs, sur une autre étiquette et sur le libellé
    d'un losange, libellé d'étape qui déborde, libellé d'étape collé à sa jauge), sur un extrait
    de page à ressources extérieures et sur une page témoin qui utilise la famille de
    `--font-sans` sans aucun `@font-face` : si l'un de ces défauts n'est pas signalé, le script
    échoue. Un sélecteur graphique qui ne trouve rien fait aussi échouer.

  Les éléments désactivés sont exclus. En cas de défaut, le script liste les éléments et sort avec
  un code non nul.
- `web/src/shared/theme/tokens.test.ts` vérifie aussi : aucun jeton déclaré deux fois dans
  `:root` ; les 8 teintes de `laneTints.ts`, les neutres, `--shadow-step`, `--shadow-raised`,
  `--focus-ring`, les rayons et les dimensions de la charte (en-tête, ruban, panneau, menu,
  tailles de police, durées) égaux aux valeurs recopiées de la spec ; les couples texte et fond
  (4,5:1) et objet graphique et fond (3:1) ; le bloc `prefers-reduced-motion` qui met chaque
  durée à 0 ; aucun littéral de couleur hors de `tokens.css` ; aucune des 148 couleurs nommées
  de CSS (`transparent` et `currentColor` restent permis) dans les feuilles CSS, les attributs
  `style`, les attributs SVG `fill`, `stroke`, `stop-color` et `color`, et les objets de style
  TS / TSX (`color: 'red'`, `markerEnd: { color: 'black' }`), sources et `maquette.html` ;
  aucun `--x: var(--y)` hors de `tokens.css` ; aucun jeton redéfini ailleurs. Chaque contrôle a
  un cas fautif qui prouve qu'il échoue.
- `--danger-muted` tient 4,83:1 sur blanc mais 4,43:1 sur `--lane-even` : il ne colore un texte
  que sur une carte blanche (pictogramme d'irritant d'une étape), ce que le test vérifie tant que
  ce ratio reste sous 4,5:1.
- Aucune valeur en dur dans `web/scripts/mockup` : couleurs, ombres et rayons passent tous par les
  jetons de `tokens.css` (aucun `color-mix()`, aucun repli `var(--x, valeur)`). `palette.mjs` lit
  toutes les valeurs affichées dans `tokens.css`.
- D13 : le nom du produit est lu par `build-mockup.mjs` dans `src/*.Application/ProductIdentity.cs`
  (`public const string Name`) et injecté par le marqueur `{{productName}}` ; la construction
  échoue si le nom apparaît en clair dans `web/scripts/mockup`.

Données entièrement fictives : projet « Achats fournisseurs » (code PRJ), prénoms génériques.

## Captures (`captures/`, Chromium, largeur 1366 px, échelle 1)

Les captures « 1366 × 768 » sont l'écran seul : une image de 1366 × 768 pixels de contenu (sans
le filet du cadre de la page), et non une fenêtre de navigateur de cette taille. Les captures de
section font 1366 px de large et la hauteur de leur section.

| Fichier | Contenu |
|---|---|
| `00-page-entiere.png` | Toute la page |
| `01-palette.png` | Section 1 : neutres, contrastes texte, jetons d'accessibilité AR-53 à AR-59 avec ratios, 8 couples pastel et encre avec ratios, jetons dérivés, rayons et ombres, typographie, boutons et focus |
| `02-accueil.png` | Section 2 : accueil complet |
| `03-editeur.png` | Section 3 avec son titre |
| `03b-editeur-1366x768.png` | L'éditeur seul, 1366 × 768 pixels de contenu |
| `04-panneau-fiche.png` | Section 4 : éditeur avec panneau ouvert, puis panneau complet déroulé et notes de lecture |
| `04b-panneau-fiche-1366x768.png` | L'éditeur avec le panneau fiche, 1366 × 768 pixels de contenu |
| `05-menus.png` | Section 5 : menu d'en-tête de couloir et menu d'étape avec les 7 types, focus sur la première entrée (contour et halo) |
| `06-notifications.png` | Section 6 : notifications avec son titre |
| `06b-notifications-1366x768.png` | L'éditeur avec trois notifications empilées, 1366 × 768 pixels de contenu |

## Correspondance avec la charte et les arbitrages

| Élément montré | Référence |
|---|---|
| Neutres, états, ombres, rayons | §9.2, NF08 |
| Focus clavier : halo `--focus-ring` plus contour `--focus-outline` sur `:focus-visible` | AR-54 |
| Flèches `--edge` #80858E (3:1 sur couloir pair) | AR-55 |
| Texte vert et orange en `--success-text` / `--warning-text`, survol `--hover` | AR-56 |
| 8 teintes pastel et encre, ratios ≥ 4,5:1 affichés | §9.2, AR-45 |
| Code du couloir en encre à 100 % | AR-47 |
| Jetons dérivés (`--frame`, `--canvas-dot`, `--other-input-*`, `--initials-*`...) | AR-26, AR-41 |
| Inter embarquée, base 14 px, h1 20 px, h2 15 px, chiffres tabulaires | §9.5, AR-40 |
| Accueil : bandeau, « Décrire un processus » sur le dernier projet ouvert, Projets 2/3, corbeille, « Qui participe » 1/3 | Annexe B §1.3, §10, AR-49 |
| En-tête de 48 px, fil d'Ariane des seules cartes (la carte racine porte le nom du projet), Fichier et Membres à droite du fil, Traductions, FR/EN (langue choisie : fond et contour 1 px `--accent`) | §9.5, AR-31, AR-59, annexe B §1.2, §2.1 (B-2.1-01) |
| Ruban : 6 groupes nommés (`role="group"` et `aria-label`), boutons icône de 32 px, séparateurs fins, une ligne, tabindex itinérant (un seul bouton à `tabindex=0`) ; bouton choisi : fond `--accent-soft` et contour 1 px `--accent` ; « Coller » actif (PRJ-E009 coupée) ; infobulle « Couper · Ctrl+X » | §9.5, annexe B §2.1, AR-59 |
| Marge fixe de 184 px, colonne 0 à 30 px après la vue de départ x = 192 ; colonne 220, rangée 120, étape 170 × 64 | AR-19, annexe B §3.1 |
| En-têtes de couloirs : pastel, liseré de 4 px, icône personne / service / système, nom 13 px demi-gras sur 2 lignes, code 11 px chasse fixe ; survol avec « ⋯ » et « + » d'ajout ; chaque en-tête est un groupe nommé (nom, code, type) qui tient lieu d'en-tête de ligne | §9.3, AR-27 |
| Corps blanc / `--lane-even` alternés, séparés par `--lane-separator` ; grille de points `--canvas-dot` | §9.3, AR-41 |
| Étapes : carte blanche, rayon 10, liseré 3 px dans l'encre, code 10,5 px, libellé 13 px sur 3 lignes au plus, à 2 px au moins de la bordure | §9.4 |
| Losanges : marqueur centré dans la forme, libellé 13 px sous la forme, code au-dessus à gauche ; libellé centré, ou aligné à gauche à 10 px à droite de l'axe quand la pointe basse porte une sortie (PRJ-E003, PRJ-E013) : aucune flèche ne passe sous un libellé | §9.4, AR-57, AR-58 |
| Attente : pointillés en `--edge` (PRJ-E011) ; début en pilule à bordure 1 px `--border` comme les autres étapes ; fin en pilule à bordure double `--edge` (PRJ-E005) ; liseré gauche dans l'encre | §9.4, AR-59 |
| Losanges décision ×, parallèle +, inclusif ○, contour 1,5 px dans l'encre | §9.4, annexe B §3.2 |
| Flèches de 1,6 px en `--edge`, coins arrondis de 10 | Annexe B §3.4 règles 7 et 8, AR-55 |
| Inclusif PRJ-E013 à deux sorties : droite vers PRJ-E021, pointe basse vers PRJ-E018 (étiquette « écart » sur le segment horizontal) | Annexe B §3.4 règle 2, §9.7 |
| Décision PRJ-E003 à trois sorties, une pointe par sortie : « oui » à droite, « non » en haut, « à revoir » en bas | §9.7, AR-24 |
| Étiquette « oui » sur un segment horizontal hors des étapes ; « non » et « à revoir » sont sur des flèches purement verticales, hors du libellé du losange ; « non » est dans la bande libre au-dessus du losange, pas sur la séparation de couloirs qui coupe sa flèche en son milieu | §9.7, AR-58 |
| Branche parallèle sur deux rangées du couloir Service Achats (PRJ-E007 vers E008 et E009) | Annexe B §2.1, §3.4 |
| Retour PRJ-E006 vers E002 (cible à gauche) : boucle `b`→`b`, courbe de Bézier à 32 px sous l'extrémité la plus basse, dans la bande libre ; elle porte le document coché dans la fiche de PRJ-E002 (« Précisions sur la demande ») : étiquette avec l'icône document et le nombre, nom au survol ; l'étiquette est placée à 12 px du bas de la bande libre (et non à bas − 8) pour rester dans la bande, sans toucher la séparation de couloirs | Annexe B §3.4 règles 1, 6 et 9 |
| Jonction PRJ-E009 vers le parallèle E016 : entrée par la pointe gauche `g`, jamais par une pointe de sortie | Annexe B §3.4, §9.7 |
| Macro-étape PRJ-E012 : pile décalée de 3 px, pastille « détaillée », badge vanille « Hebdomadaire » avec horloge | §9.4, annexe B §2.9 |
| Cadre de remontée en pointillés `--frame` (classes `dissolution-frame`, `dissolution-label`), étiquette « Reliquat de PRJ-E004 · 3 » ; étape étrangère PRJ-E021 recouverte, hors cadre | Annexe B §2.9, AR-26, AR-53 |
| Pictogrammes en bas à droite (documents, irritant en `--danger-muted` sur la carte blanche, durée) ; quand ils sont affichés, le texte remonte de 12 px (marge basse, comme la 1.x) ; un libellé de 3 lignes les garde (PRJ-E006) | §9.4, annexe B §2.10 |
| Jauge de fiche de 3 px : brouillon en `--warning-graphic` (E002, E017), validée (E008, E012), à revoir (E006) ; un libellé de 3 lignes finit à 2 px au-dessus de la jauge (carte à jauge sans marge haute, PRJ-E006) | §9.4, AR-59 |
| Avertissement « ! » en `--warning-graphic` sur les étapes isolées PRJ-E020 et PRJ-E014 | Annexe B §2.10, AR-59 |
| Sélection : anneau accent 2 px (PRJ-E002) ; coupée (PRJ-E009) et hors cadre (PRJ-E021) : carte atténuée à `--step-cut-opacity`, pointillés pour la coupe, texte en `--text-muted` à pleine opacité | §9.4, AR-53 |
| « + » d'ajout rapide sur l'étape survolée (PRJ-E014) : avant, après, et en dessous ; pas de « + » au-dessus, l'étape étant dans le premier couloir (« dessus » / « dessous » seulement s'il existe un couloir au-dessus / en dessous) | AR-27, annexe B §2.3 |
| Zoom et mini-carte en bas à droite, décalés avec le bord du canevas quand le panneau fiche est ouvert (le panneau réduit la largeur du canevas) | Annexe B §2.1 |
| Panneau fiche 440 px, en-tête collant, complétude, Formulaire / Écrire librement (mode choisi : fond et contour 1 px `--accent`), champs dans l'ordre testé, entrée proche cochée avec %C&A et irritant, micro-tâche « Contrôler » en vert (numéro blanc sur `--success-text`), irritant en `--text` sur `--danger-soft`, suggestions, « Reprendre », « Je valide ma fiche » ; champs de saisie bordés en `--field-border`, jauge de complétude « brouillon » en `--warning-graphic` | §9.5, annexe B §4, AR-59 |
| Menus de 240 px, lignes de 34 px, rayon 8, ombre `--shadow-menu`, raccourcis à droite en `--text-muted`, y compris « Suppr » sur l'entrée rouge « Supprimer » | §9.5, annexe B §2.4, AR-39 |
| Menu d'étape : entrées et séparateurs dans l'ordre de l'annexe (séparateur avant les groupes 9, 10 et 11, aucun avant « Supprimer »), « Ajouter une branche en parallèle (même acteur) », les 7 « Changer le type : X » en `menuitemradio` dans un `role="group"` (type courant marqué), « Coller avant / après » actifs | Annexe B §2.4 points 1 à 13 |
| Menus : la première entrée active reçoit le focus à l'ouverture (« Renommer », contour `--focus-outline` et halo `--focus-ring`) ; raccourcis en `aria-keyshortcuts`, leur texte affiché en `aria-hidden` (hors du nom accessible) | Annexe B §2.4, NF08, AR-54 |
| Menu de couloir : « Type » et « Couleur » en `menuitemradio` dans un `role="group"` nommé par son titre (`aria-labelledby`), conformément à ARIA 1.2 ; « Couleur » à vraies pastilles des 8 teintes | §10, annexe B §2.4 |
| Notifications : coin bas droit, empilées, icône d'état (information, succès, erreur) | §9.5 |

## Écarts restants

Seuls subsistent les écarts couverts par un arbitrage :

1. **Étape coupée et hors cadre** (AR-53) : la charte demande « opacité 0,45 » ; la maquette
   atténue la carte seule (fond, bordure, ombre à `--step-cut-opacity`) et garde le texte à pleine
   opacité en `--text-muted`, pour tenir 4,5:1.
2. **Focus clavier** (AR-54) : contour `--focus-outline` ajouté au halo `--focus-ring`.
3. **Flèches** (AR-55) : `--edge` #80858E au lieu de #8A8F98.
4. **Texte vert et orange, survol** (AR-56) : `--success-text`, `--warning-text`, `--hover`.
5. **Libellé des losanges** (AR-57) : sous la forme, et non dedans.
6. **Libellé d'un losange dont la pointe basse porte une sortie** (AR-58) : aligné à gauche, à
   10 px à droite de l'axe vertical, au lieu d'être centré, pour qu'aucune flèche ne passe dessous.
7. **Objets graphiques utiles à 3:1** (AR-59) : jauge « brouillon » et pastille « ! » en
   `--warning-graphic` #C27400 au lieu de `--warning` ; champs de saisie bordés en
   `--field-border` au lieu de `--border` ; élément choisi du ruban, de « Formulaire / Écrire
   librement » et de « FR / EN » marqué d'un contour de 1 px `--accent` en plus de son fond ;
   pointillés de l'attente et bordure double de la fin, qui portent le type de l'étape, en
   `--edge` au lieu de `--border`.

Conventions de revue (pas des écarts) : les deux menus de la section 5 sont montrés ensemble,
alors qu'à l'écran un seul est ouvert à la fois ; dans les sections 4 et 6, l'infobulle et les
« + » de survol sont masqués ; dans l'écran avec le panneau fiche, la vue est décalée de 152 px
vers la gauche, pour que le bord du panneau et son ombre tombent dans l'allée après la colonne 3
et non sur le texte d'une étape ou d'une étiquette. Le logo est une marque provisoire dessinée
avec les jetons.
