<!-- format-mermaid.md -->
<!-- Mermaid in 3.1: the diagrams the exports write (rules of the flowchart, of the report) and the subset a Mermaid flowchart is read in when it opens as a process -->

# Mermaid : ce que Topogenia écrit et ce qu'il lit

Version 3.1, ADR 092 et 093. Code : `web/src/features/map/export/mermaid.ts`, `markdown.ts`, `report.ts` ;
`web/src/features/project/files/mermaidImport.ts`. Le lecteur de référence est VS Code, avec Mermaid 11.4 ou plus récent :
aperçu intégré, extensions Mermaid Chart et Markdown Preview Enhanced (qui embarque Mermaid 11.4.0).

## Ce que les exports écrivent

La syntaxe est écrite par le code, jamais par un modèle. La même carte donne le même texte.

### En-tête

Chaque diagramme commence par un `%%{init: …}%%` qui fixe :
- le thème `neutral` ;
- une police présente partout (`Arial, Segoe UI, sans-serif`), la même pour la mesure et l'affichage ;
- les libellés HTML ;
- sous les titres de couloir, une marge pour autant de lignes que le plus long en compte. Mermaid ne la réserve pas
  lui-même, et sans elle une étape couvre un titre.

### Logigramme de la carte (`flowchart LR`)

- **Couloirs** : un sous-graphe par couloir montré, dans l'ordre des couloirs : `subgraph L1["Commercial"]` … `end`.
- **Étapes** : dans l'ordre de lecture, nommées `S1`, `S2`… Aucun identifiant n'est lu dans un texte.
- **Formes** :

  | Type | Forme | Écriture |
  |---|---|---|
  | Tâche | boîte | `S1["…"]` |
  | Tâche détaillée | boîte à double bord | `S1[["…"]]` |
  | Décision | losange | `S1{"…"}` |
  | Parallèle, inclusif | losange, `+` ou `O` en première ligne | `S1{"+<br/>…"}` |
  | Attente | drapeau | `S1>"…"]` |
  | Début | stade | `S1(["…"])` |
  | Fin | double cercle | `S1((("…")))` |

- **Texte d'une étape** : le code de l'étape (`VIS-E001`), puis le libellé en lignes de 24 caractères au plus, coupées
  aux espaces. Un mot plus long est coupé avec un trait d'union ; rien n'est retiré.
- **Signes** : `#`, `"`, `<`, `>` et l'accent grave sont écrits en codes d'entité (`#35;`, `#quot;`…).
- **Flèches** : `S1 --> S2`, ou `S1 -->|"oui"| S2` avec leur libellé en lignes. Elles sont triées par l'ordre de leur
  source, puis de leur cible.
- **Étapes à problème** (rapport) : `classDef issue` aux couleurs d'une faute de la charte, puis `class S2,S5 issue`.

### Diagrammes du rapport

Ce sont `journey`, `quadrantChart`, `timeline` et `pie` (six parts au plus). Leurs textes sont courts (25 caractères au
plus), sans `:` ni signe que ces diagrammes lisent. Les points du quadrant sont placés sur une grille fixe, et les
actions aux mêmes notes forment un seul point, nommé par toutes (`"A1 A3": [0.30, 0.72]`). Le parcours, la matrice et
le plan n'ont pas de `title` : l'intitulé de leur section les nomme déjà.

Le `journey` a des cases fixes de 150 × 50 px. Son en-tête lui est propre : police de 13 px, interligne de 1,25 et
marge intérieure de 6 px, fixés dans le diagramme (`themeCSS`), quelle que soit la page qui l'affiche. Sans cela,
l'aperçu de Markdown Preview Enhanced (16 px, interligne 1,6) coupait la seconde ligne d'un titre. Chaque titre est
mesuré avec les largeurs d'Arial (`arialWidth.ts`) pour tenir en deux lignes de 136 px ; un mot plus large est coupé
avec un trait d'union, un idéogramme peut aller seul à la ligne. Au-delà de deux lignes, la fin devient « … » : le
libellé complet est dans le tableau qui suit le diagramme. Le parcours n'a pas d'acteurs. Mermaid 11.4 dessine leur
légende dans une marge fixe de 150 px, sous la première section dès qu'un nom de couloir est un peu long ; le couloir
de chaque étape est donc une colonne du tableau.

Dans `quadrantChart`, les axes, les quadrants et les points sont écrits entre guillemets. Hors guillemets, un Mermaid
antérieur à 11.5 ne lit que les lettres ASCII et s'arrête sur « À », « É » ou toute lettre cyrillique, grecque ou
chinoise (« Lexical error … Unrecognized text »). Entre guillemets, toutes les versions les lisent.

### Vérification

Tous les diagrammes passent par `mermaid.parse`. Un test vérifie, dans les 18 langues, qu'aucune ligne du quadrant ne
porte de lettre non ASCII hors de ses guillemets. Ils sont aussi dessinés dans Chromium avec le Mermaid des tests, dans
une page au style de l'aperçu du lecteur de référence (16 px, interligne 1,6). On y mesure les textes coupés par leur
boîte, hauteur comprise pour les cases du parcours, et les éléments qui se recouvrent. Des témoins négatifs (titre
recouvert, police plus large qu'à la mesure, parcours à l'en-tête des autres diagrammes) prouvent que la mesure trouve
un défaut quand il existe. Mermaid 11.4.0, celle du lecteur de référence, n'est pas une dépendance du projet : son
DOMPurify est signalé comme vulnérable. Elle est contrôlée à part, à chaque changement des diagrammes du rapport.

## Ce que l'ouverture lit

« Ouvrir un fichier… » lit un `.mmd`, ou le premier bloc ` ```mermaid ` d'un `.md` dont le diagramme est un logigramme.
Il en fait un `.pmap` 1.2, comme pour un tableau (voir `format-tableur.md`). Un autre type de diagramme est refusé
clairement.

### Sous-ensemble lu

- **En-tête** :
  - `flowchart` ou `graph`, avec une direction ;
  - l'en-tête YAML (`---` … `---`) et les directives `%%{…}%%` sont ignorés ;
  - un commentaire `%%` est ôté, sauf entre guillemets.
- **Nœuds** :
  - clé seule, ou clé et forme : `[…]`, `(…)`, `([…])`, `[[…]]`, `[(…)]`, `((…))`, `(((…)))`, `{…}`, `{{…}}`,
    `>…]`, `[/…/]`, `[\…\]` ;
  - formes nommées de Mermaid 11 : `A@{ shape: diam, label: "…" }` ;
  - libellé entre guillemets, en texte Markdown entre accents graves, avec `<br/>` et codes d'entité (lus en une passe).
- **Liens** :
  - `-->`, `---`, `-.->`, `==>` et leurs longueurs, `--o`, `--x`, `<-->`, `~~~` ;
  - libellé `|…|` ou `-- … -->` ;
  - chaînes `A --> B --> C`, groupes `A & B --> C`.
- **Sous-graphes** : `subgraph id[Titre]`, `subgraph "Titre"`, `subgraph Titre`, imbriqués, avec `direction`.

### Correspondances

| Mermaid | Topogenia |
|---|---|
| sous-graphe | couloir ; le plus intérieur pour des sous-graphes imbriqués ; « Processus » pour les nœuds hors sous-graphe |
| `{…}` | décision ; `+` en première ligne : parallèle, `O` : inclusif |
| `((…))`, `([…])` | début sans flèche entrante, fin sans flèche sortante, tâche sinon |
| `(((…)))` | fin |
| `>…]` | attente |
| `[[…]]` | tâche (et c'est dit : le sous-processus n'a pas de carte détaillée) |
| autre forme | tâche |
| lien et son libellé | flèche et son libellé ; un second lien entre les mêmes nœuds et un lien d'un nœud vers lui-même sont ignorés |

Ce que l'export écrit se relit tel quel :
- la ligne du code d'étape (`VIS-E001`) est ôtée du libellé ;
- le signe d'un losange est lu ;
- un mot coupé en fin de ligne de 24 caractères est recollé.

### Mis de côté, et dit avec la ligne

Les lignes sont comptées depuis le haut du document, même pour un `.md`. Sont mis de côté et signalés :
- les styles : `classDef`, `class`, `style`, `linkStyle`, `click`, `accTitle`, `accDescr`, `:::classe` ;
- une instruction non comprise ;
- un lien caché (`~~~`) ;
- un lien à double sens (gardé dans un sens) ;
- un lien vers un sous-graphe entier.

### Bornes

- **Refus** : au-delà de 20 000 lignes, de 5 000 nœuds ou de 20 000 liens, le diagramme est refusé clairement.
- **Lignes trop longues** : une ligne de plus de 2 000 caractères est laissée de côté et dite.
- **Textes coupés à leur borne, et c'est dit** : 150 caractères pour un libellé d'étape ou un titre de couloir, 60 pour
  un libellé de flèche.
- **Temps de lecture** : l'analyseur ne revient jamais en arrière sur une recherche. Les textes piégés (espaces sans
  fin, crochets, guillemets non fermés) se lisent en temps borné.
