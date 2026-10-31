<!-- format-pmap.md -->
<!-- Le format .pmap 1.2 : structure, règles de cohérence, fiches d'étape, flux de valeur, placement, ouverture, copie, duplication (ADR 054, ADR 085) -->

# Format de fichier `.pmap`, version 1.2

Un fichier `.pmap` décrit une carte de processus en couloirs : les acteurs (couloirs), les étapes, les flèches qui les
relient, les rôles RACI, le détail des étapes (sous-cartes), en version 1.1 les traductions des étapes et des flèches et
une fiche facultative par étape, et en version 1.2 les saisies du flux de valeur (ADR 085) : volume entrant d'un début,
part des sorties d'un branchement, délai d'une fiche, effectif d'un couloir, temps de travail du projet. C'est le format léger de l'application, lisible et écrivable par une
personne ou une IA (D11) ; le paquet `.topogenia` (`docs/format-topogenia.md`) porte, lui, tout le projet.

- **Fichier > Télécharger une copie** produit le `.pmap` d'un projet (tout membre) ;
- **Fichier > Ouvrir un fichier .pmap** crée un projet à partir d'un fichier (administrateur) ;
- **Dupliquer le projet** passe par le même modèle, en mémoire, sans les fiches (administrateur).

| Élément | Valeur |
|---|---|
| Extension | `.pmap` |
| Contenu | JSON (RFC 8259), UTF-8 |
| Schéma | `schemas/pmap-1.2.schema.json` (sous-ensemble de JSON Schema 2020-12, ADR 051) ; `schemas/pmap-1.1.schema.json` reste publié |
| Marqueur | `"format": "topogenia"` |
| Version | `"version": "1.2"`, écrite ; `"1.1"` toujours lue, sans les propriétés de la 1.2 (AR-93 : aucun fichier 1.0 n'a été publié) |

Le schéma décrit la forme ; ce document ajoute les règles qu'un schéma ne sait pas dire (section 4). Un exemple minimal
et un exemple complet sont dans `samples/minimal.pmap` et `samples/cloture-mensuelle.pmap` ; les consignes pour une IA
sont dans `docs/pmap/`.

## 1. Exemple minimal

```json
{
  "format": "topogenia",
  "version": "1.2",
  "project": { "name": "Demande d'achat" },
  "map": {
    "lanes": [{ "id": "c1", "name": "Demandeur" }, { "id": "c2", "name": "Acheteur", "type": "department" }],
    "steps": [
      { "id": "e1", "lane": "c1", "label": "Besoin identifié", "type": "start" },
      { "id": "e2", "lane": "c1", "label": "Saisir la demande" },
      { "id": "e3", "lane": "c2", "label": "Passer la commande" },
      { "id": "e4", "lane": "c2", "label": "Commande envoyée", "type": "end" }
    ],
    "flows": [{ "source": "e1", "target": "e2" }, { "source": "e2", "target": "e3" }, { "source": "e3", "target": "e4" }]
  }
}
```

Sans colonnes, l'application place les étapes elle-même en suivant les flèches (section 5).

## 2. Structure

Noms de propriétés et valeurs codées en anglais minuscule ; textes dans la langue du projet. Une propriété inconnue est
refusée avec la liste des propriétés possibles : une faute de frappe (`lable`) serait sinon perdue.

### 2.1 Document

| Propriété | Obligatoire | Règle |
|---|---|---|
| `format` | oui | `"topogenia"` |
| `version` | oui | `"1.2"`, ou `"1.1"` (une propriété de la 1.2 y est inconnue, la fréquence `per_case` refusée) |
| `generated_by`, `generated_at` | non | Informatifs ; la copie écrit le nom du produit et la date UTC |
| `project` | oui | Section 2.2 |
| `map` | oui | Carte racine, section 2.3 |
| `uncertainties` | non | Points à faire vérifier, 500 caractères chacun, les 50 premiers gardés et affichés à l'ouverture |
| `$schema` | non | Admis pour les éditeurs qui valident avec le schéma |

### 2.2 Projet

| Propriété | Obligatoire | Règle |
|---|---|---|
| `name` | oui | 200 caractères ; nom du projet et de la carte racine |
| `code` | non | 3 à 6 lettres majuscules ; absent ou mal formé, il est demandé à l'ouverture |
| `language` | non | Une langue configurée ; la langue par défaut de l'installation sinon |
| `description` | non | 4 000 caractères |
| `translations` | non | Nom du projet dans les autres langues configurées |
| `workday_hours` | non (1.2) | Journée de travail, de 1 à 24 heures ; 7 sans valeur |
| `fte_hours_per_month` | non (1.2) | Heures d'un équivalent temps plein par mois, de 1 à 744 ; 151,67 sans valeur |

### 2.3 Carte, couloir, étape, flèche

| Objet | Propriétés |
|---|---|
| Carte | `lanes` (1 à 200, de haut en bas, obligatoire), `steps`, `flows` |
| Couloir | `id`, `name` (150), `type` (`person` par défaut, `department`, `system`), `color` (`#RRGGBB`, minuscules acceptées et mises en majuscules ; absente : teinte du rang), `translations`, `headcount` (1.2 : effectif, entier de 0 à 100 000, jamais sur `system`) |
| Étape | `id`, `lane` (un couloir de sa carte), `label` (150), `type` (`task` par défaut, `decision`, `parallel`, `inclusive`, `wait`, `start`, `end`), `column` (0 à 10 000), `row` (0 à 50), `automation` (`manual` par défaut, `in_progress`, `automated`), `raci`, `sub_map`, `translations` (1.1), `sheet` (1.1), `volume` (1.2 : `{ "quantity", "period" }`, quantité de 0 à 1 000 000 par `day`, `week`, `month` ou `year`, seulement sur `start`) |
| Flèche | `source`, `target` (deux étapes de la même carte), `label` (60), `translations` (1.1), `share` (1.2 : part des dossiers en %, de 0 à 100, seulement sur une sortie de `decision` ou `inclusive`) |

## 3. Identifiants

- Un `id` est un texte de 1 à 64 caractères, sans espace au début ni à la fin, **unique dans tout le fichier** :
  couloirs, étapes et documents confondus, sous-cartes comprises.
- Il ne sert qu'à relier les éléments du fichier : à l'ouverture, l'application attribue ses codes (`VTE-C01`,
  `VTE-E001`), les étapes numérotées dans l'ordre de lecture (colonne, couloir, rangée).
- Une copie utilise les codes de l'application comme identifiants ; une sortie y est nommée par le code de son étape
  suivi de `-S` et de son rang (`VTE-E003-S1`).

## 4. Règles de cohérence

Le fichier est refusé, avec la liste des problèmes et leur emplacement (30 au plus), si l'une de ces règles n'est pas
tenue ; rien n'est créé tant que le fichier n'est pas entièrement correct.

1. Une étape appartient à un couloir de **sa** carte.
2. Une flèche relie deux étapes de la **même** carte (un message propre signale une étape d'une autre carte) ; pas de
   flèche d'une étape vers elle-même, pas deux fois la même flèche dans le même sens.
3. Une carte donne `column` **pour toutes ses étapes ou pour aucune** ; deux étapes ne partagent pas une case (couloir,
   colonne, rangée).
4. Un début, une fin, un losange ET ou OU inclusif n'a ni sous-carte, ni RACI, ni fiche (annexe A 2.7, spec 7.5).
5. RACI : section 6. Fiches : section 8.
6. Flux de valeur (1.2) : un `volume` seulement sur un début, une `share` seulement sur une flèche qui sort d'une décision
   ou d'un OU inclusif, un `headcount` jamais sur un couloir `system` ; les bornes sont celles de l'application.
7. Textes : les règles de toute saisie de l'application (un seul contrôle pour l'API et les formats, A-2.18-08) : espaces
   retirés, pas de balise HTML, pas de caractère de contrôle autre que la tabulation et les fins de ligne, longueur bornée.
8. Limites : 5 000 étapes par fichier, 200 couloirs par carte, 12 niveaux de sous-cartes, JSON de 256 niveaux au plus,
   8 millions de caractères ; le corps de la demande d'ouverture est limité à 10 Mo avant d'être lu.

Ne bloquent pas : une étape sans flèche, une flèche qui entre dans un début ou sort d'une fin, plusieurs débuts ou fins.
Un code de projet mal formé est ignoré (l'ouverture le demande) ; une sous-carte sans étape est ignorée.

Chaque problème est rendu par son chemin (`map.steps[3].lane`), son code (`PMAP_COULOIR_INCONNU`, ou le code de la règle
de saisie : `TEXTE_TROP_LONG`, `VALEUR_INVALIDE`...) et son message dans la langue de l'utilisateur, tiré des fichiers de
messages de l'application (A-3-72) ; la réponse `FICHIER_INVALIDE` porte `count` et `errors`.

## 5. Placement des étapes

- **Avec colonnes** : chaque étape est posée dans sa colonne ; c'est ce qu'écrit une copie.
- **Sans colonnes** : une étape se place une colonne après la plus avancée de ses étapes précédentes, la colonne 0 sans
  flèche entrante ; une case prise dans le couloir et la rangée la pousse d'une colonne ; à égalité, l'ordre du fichier
  décide ; une boucle est rompue à la première étape restante du fichier (`AutomaticColumns`).

## 6. RACI

- Le **R** est toujours le couloir de l'étape (`lane`). `raci` donne le rôle des **autres** couloirs de la même carte :
  `{ "dir": "A", "com": "C" }`.
- Les lettres sont celles du paramètre `raci_letters` (A, C, I par défaut ; S en RASCI) ; un seul A par étape.
- `"R"` pour le couloir de l'étape est admis et sans effet ; `"R"` pour un autre couloir est refusé ; une autre lettre
  sur le couloir de l'étape aussi.
- Une copie n'écrit que le RACI visible : un rôle masqué (couloir de l'étape après un déplacement, couloir d'une autre
  carte) n'y figure pas.

## 7. Sous-cartes

Une étape détaillée porte sa carte dans `sub_map`, avec ses propres couloirs, étapes et flèches ; la sous-carte prend le
libellé de l'étape pour nom. Pas de flèche entre une carte et sa sous-carte : l'étape détaillée fait le lien. Une copie
omet les sous-cartes vides et ne va pas au-delà de 12 niveaux ; elle le signale alors (en-tête `X-Pmap-Warning:
PMAP_PROFONDEUR_COUPEE`), sans troncature silencieuse.

## 8. Fiche d'une étape (`sheet`, 1.1)

Décision du porteur du projet (AR-93) : un récit, une procédure ou un entretien donne souvent tout ou partie des fiches ;
le fichier les porte. Chaque partie est facultative ; à l'ouverture, la fiche devient le **brouillon** de l'étape, jamais
une version validée (le circuit de validation reste celui de l'application).

| Propriété | Règle (celle de la fiche de l'application) |
|---|---|
| `actor`, `purpose`, `tools` | Textes de 4 000 caractères |
| `frequency` | `daily`, `weekly`, `monthly`, `month_end_close`, `quarterly`, `yearly`, `occasional`, et en 1.2 `per_case` (à chaque dossier) |
| `duration_minutes` | Durée par exécution, en minutes |
| `lead_time` | 1.2 : délai, de l'arrivée du dossier à son départ, attentes comprises : `{ "value", "unit" }`, valeur de 0 à 100 000 en `minute`, `hour` ou `day` (jour ouvré) |
| `procedure` | 50 lignes `{ "text", "tool", "nature" }` au plus : texte (1 000), outil (200), nature (`extract`, `copy_paste`, `re_enter`, `format`, `calculate`, `check`, `chase`, `file`, `send`, `wait`) ; une ligne sans texte est retirée, un outil ou une nature sans texte est refusé |
| `free_text` | 20 000 caractères |
| `outputs` | Documents que l'étape produit : `{ "id", "name", "location", "share_confirmed" }` ; `id` n'est demandé qu'à une sortie qu'une autre étape utilise |
| `inputs` | Documents reçus de l'extérieur de la carte : les mêmes propriétés, plus `wait_hours` (0 à 100 000) |
| `uses` | Sorties d'**autres** étapes de la **même** carte que l'étape utilise : `{ "document", "wait_hours", "percent_complete_accurate", "pain_point", "lost_hours_per_month" }` ; `document` est l'`id` de la sortie ; une sortie est utilisée une fois au plus par une étape ; l'irritant (`pain_point`, 2 000 caractères, temps perdu de 0 à 99 999,9 heures par mois) naît sur l'étape productrice |

Les liens (`location`) sont des adresses web `http(s)://` ou des chemins de fichier (`\\serveur\dossier`, `X:\dossier`).
Une copie n'écrit les fiches que sur option (`with_sheets=true`, désactivée par défaut) et chaque fiche telle que le
demandeur la voit : le brouillon pour l'animateur et la personne qui remplit, la dernière version validée pour les autres.
Documents et usages, visibles de tous les membres, suivent. La duplication ne copie jamais les fiches (AR-10).

## 9. Ouverture, copie, duplication

| Route | Droit | Effet |
|---|---|---|
| `GET /api/projects/{id}/pmap?with_sheets=` | Lecteur | La copie en texte, nommée `CODE_Nom-court_aaaa-mm-jj.pmap` (A-1.2-16) ; événement d'audit `project.exported` |
| `POST /api/projects/import-pmap` | Administrateur | Corps `{ "content", "code", "name" }` : le code et le nom saisis l'emportent sur ceux du fichier, un nom changé perd les traductions du nom ; l'ouvreur est le seul animateur ; réponse 201 avec les nombres de cartes, couloirs, étapes, flèches et fiches et les incertitudes ; événement `project.imported` |
| `POST /api/projects/{id}/duplicate` | Administrateur (la source lue) | Corps `{ "code", "name" }` : le modèle `.pmap` de la source, sans les fiches, ouvert comme un fichier ; traductions des étapes et des flèches gardées ; événement `project.duplicated` |

Pour faciliter le copier-coller depuis une conversation avec une IA, l'ouverture accepte aussi un contenu entouré d'un
bloc de code Markdown, la marque d'ordre des octets, les virgules en fin de liste et les commentaires `//`.

## 10. Versions

- `1.1` (25/10/2026) : traductions des étapes et des flèches, fiche d'étape, refus d'un RACI sur un début, une fin ou un
  losange ET ou OU inclusif, marqueur `topogenia`. Première version publiée.
- `1.2` (31/10/2026, ADR 085) : saisies du flux de valeur (`volume` d'un début, `share` d'une sortie de branchement,
  `headcount` d'un couloir, `lead_time` et fréquence `per_case` d'une fiche, `workday_hours` et `fte_hours_per_month` du
  projet). La copie écrit la 1.2 ; un fichier 1.1 reste lu tel quel.
- Une version suivante qui ajoute des propriétés facultatives sera lue avec celle-ci ; un changement incompatible
  donnera un message explicite.
