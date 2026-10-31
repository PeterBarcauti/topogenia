<!-- PROMPT_croquis_vers_pmap.md -->
<!-- Consigne à donner à une IA avec la photo d'un croquis de processus pour obtenir un fichier .pmap 1.2 -->

# Consigne : d'un croquis de processus à un fichier `.pmap`

Mode d'emploi : joins à une IA la photo du croquis, ce fichier et `PMAP_GUIDE_IA.md`, puis envoie « Applique la
consigne ». Copie la réponse, puis dans l'application : **Ouvrir un fichier .pmap**, colle le contenu (ou enregistre-le
dans un fichier `.pmap`).

---

## Ta mission

Tu reçois la photo d'un croquis de processus, souvent dessiné à la main : des bandes horizontales (les couloirs, un par
acteur), des boîtes (les étapes), des flèches et parfois des notes. Produis le fichier `.pmap` version 1.2 qui reproduit
ce croquis, conforme au guide `PMAP_GUIDE_IA.md`.

## Comment lire le croquis

1. **Couloirs** : chaque bande horizontale, de haut en bas, avec le nom écrit à gauche (ou en haut de la bande). Type
   `person` pour une personne ou une fonction, `department` pour une équipe ou un service, `system` pour un logiciel
   (ERP, tableur, messagerie...). Si le croquis n'a pas de bandes, crée un seul couloir nommé d'après l'acteur évident,
   ou « Acteur 1 », et signale-le dans `uncertainties`. Si le type n'est pas évident, choisis le plus probable sans le
   signaler : il se corrige en un clic.
2. **Étapes** : chaque boîte, dans le couloir où elle est dessinée. Le couloir d'une étape est l'acteur qui la réalise,
   y compris un système.
   - rectangle : `task` ;
   - losange, ou question avec « ? » : `decision` ; losange marqué « + » (tâches menées en même temps) : `parallel` ;
     losange marqué « o » (une ou plusieurs branches) : `inclusive` ;
   - boîte en pointillés, sablier, horloge, mot « attente » ou « délai » : `wait` ;
   - rond, ovale ou mot « début » au départ du flux : `start` ;
   - rond, ovale ou mot « fin » au bout du flux : `end`.
3. **Liens** : chaque flèche, dans le sens de la pointe. Recopie le texte écrit sur la flèche dans `label` (« oui »,
   « non », « si urgent »...). Un trait sans pointe entre deux étapes successives se lit de gauche à droite.
4. **Colonnes** : numérote les positions de gauche à droite à partir de 0. Deux boîtes l'une au-dessus de l'autre ont
   la même colonne. Si l'ordre horizontal n'est pas lisible, n'écris `column` pour aucune étape : l'application placera
   les étapes en suivant les flèches. Ne donne jamais de colonne à une partie des étapes seulement.
5. **Détail d'une étape** : si le croquis détaille une boîte dans un encadré à part (un zoom), mets ce détail dans
   `sub_map` de l'étape, avec ses propres couloirs, étapes et liens. N'y ajoute pas de début ni de fin s'ils ne sont pas
   dessinés.
6. **RACI** : seulement si le croquis l'indique (lettres A, C, I près d'une étape, ou tableau RACI). Le couloir de
   l'étape est le R ; `raci` donne le rôle des autres couloirs. Sinon, n'écris pas `raci`.
7. **Notes près d'une boîte** (« 15 min », « tous les lundis », « sur le tableur », « reçoit le devis ») : elles vont
   dans la fiche de l'étape (`sheet`, section 7 du guide) : `duration_minutes`, `frequency`, `tools`, `inputs`,
   `outputs`... Une note qui ne se range nulle part va dans `free_text`.

## Règles de rédaction

- Recopie les textes tels qu'ils sont écrits ; corrige seulement l'orthographe évidente. N'invente rien : pas d'étape,
  de flèche, d'acteur, de durée ni de document absents du croquis. Un mot illisible s'écrit au plus près, et tu le
  signales.
- `uncertainties` liste seulement les doutes qui peuvent changer le sens du processus, une phrase par point, en citant
  l'étape : « Étape e4 : mot illisible, lu "Contrôler" ». Pas de doute : pas de `uncertainties`.
- Identifiants courts, uniques dans tout le fichier : `c1`, `c2`... pour les couloirs, `e1`, `e2`... pour les étapes,
  `d1`, `d2`... pour les documents. Dans la sous-carte de l'étape `e3` : `e3-c1`... pour ses couloirs, `e3-1`... pour
  ses étapes.
- `project.name` : le titre du croquis, ou à défaut le nom du processus déduit des étapes. N'écris pas `project.code`
  sauf s'il est sur le croquis. `project.language` : la langue du croquis.
- `generated_by` : ton nom et « depuis un croquis ».
- Jamais de nom de personne dans le fichier : un rôle à la place.

## Contrôles avant de répondre

Les cases de la section 8 du guide, en particulier : `format` = `"topogenia"`, `version` = `"1.2"` ; chaque `lane`,
`source`, `target` et clé de `raci` dans la même carte ; colonnes pour toutes les étapes ou pour aucune ; pas de
`sub_map`, de `raci` ni de `sheet` sur `start`, `end`, `parallel`, `inclusive`.

## Réponse attendue

Réponds uniquement par le contenu du fichier, dans un seul bloc de code `json`, sans texte avant ni après.

## Exemple

Croquis : deux bandes « Demandeur » et « Acheteur ». Dans « Demandeur », un rond « Besoin identifié » puis une boîte
« Saisir la demande », annotée « 10 min » ; une flèche descend vers la boîte « Passer la commande » de « Acheteur »
(premier mot à moitié effacé), suivie d'un rond « Commande envoyée ».

```json
{
  "format": "topogenia",
  "version": "1.2",
  "generated_by": "Une IA, depuis un croquis",
  "project": { "name": "Demande d'achat", "language": "fr" },
  "map": {
    "lanes": [
      { "id": "c1", "name": "Demandeur", "type": "person" },
      { "id": "c2", "name": "Acheteur", "type": "department" }
    ],
    "steps": [
      { "id": "e1", "lane": "c1", "label": "Besoin identifié", "type": "start", "column": 0 },
      { "id": "e2", "lane": "c1", "label": "Saisir la demande", "type": "task", "column": 1, "sheet": { "duration_minutes": 10 } },
      { "id": "e3", "lane": "c2", "label": "Passer la commande", "type": "task", "column": 2 },
      { "id": "e4", "lane": "c2", "label": "Commande envoyée", "type": "end", "column": 3 }
    ],
    "flows": [
      { "source": "e1", "target": "e2" },
      { "source": "e2", "target": "e3" },
      { "source": "e3", "target": "e4" }
    ]
  },
  "uncertainties": [
    "Étape e3 : premier mot à moitié effacé, lu « Passer »."
  ]
}
```

## Schéma du fichier

Le fichier produit respecte ce schéma JSON, copie exacte de `schemas/pmap-1.2.schema.json`.

```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "$id": "pmap-1.2.schema.json",
  "title": "Fichier .pmap, version 1.2",
  "description": "Carte de processus en couloirs : couloirs, étapes, flèches, rôles RACI, cartes détaillées, traductions et fiches d'étape facultatives, et en 1.2 les saisies du flux de valeur (volume entrant, parts, délai, effectif, temps de travail). Le schéma décrit la forme ; docs/format-pmap.md ajoute les règles de cohérence qu'un schéma ne sait pas dire.",
  "type": "object",
  "required": ["format", "version", "project", "map"],
  "additionalProperties": false,
  "properties": {
    "$schema": { "type": "string" },
    "format": { "const": "topogenia", "description": "Toujours \"topogenia\"." },
    "version": { "const": "1.2", "description": "Version 1.2 ; l'application lit aussi un fichier 1.1, sans les propriétés du flux de valeur." },
    "generated_by": { "type": "string", "description": "Logiciel ou personne qui a produit le fichier (informatif)." },
    "generated_at": { "type": "string", "description": "Date de production, ISO 8601, par exemple 2026-10-25T15:30:00Z (informatif)." },
    "project": { "$ref": "#/$defs/project" },
    "map": { "$ref": "#/$defs/map" },
    "uncertainties": {
      "type": "array",
      "items": { "type": "string", "maxLength": 500 },
      "description": "Points à faire vérifier par une personne ; les 50 premiers sont gardés et affichés à l'ouverture."
    }
  },
  "$defs": {
    "id": {
      "type": "string",
      "minLength": 1,
      "maxLength": 64,
      "pattern": "^\\S(.*\\S)?$",
      "description": "Identifiant libre, unique dans tout le fichier (couloirs, étapes et documents confondus)."
    },
    "text": { "type": "string", "pattern": "\\S", "not": { "pattern": "<[a-zA-Z!/?]" } },
    "translations": {
      "type": "object",
      "additionalProperties": { "type": "string" },
      "description": "Le texte dans les autres langues configurées : { \"en\": \"...\" }."
    },
    "project": {
      "type": "object",
      "required": ["name"],
      "additionalProperties": false,
      "properties": {
        "code": { "type": "string", "description": "Code court du projet, 3 à 6 lettres majuscules ; absent ou mal formé, il est demandé à l'ouverture." },
        "name": { "allOf": [{ "$ref": "#/$defs/text" }, { "maxLength": 200 }] },
        "language": { "type": "string", "description": "Langue des textes du fichier, une langue configurée ; la langue par défaut de l'installation sinon." },
        "description": { "type": "string", "maxLength": 4000 },
        "translations": { "$ref": "#/$defs/translations" },
        "workday_hours": { "type": "number", "minimum": 1, "maximum": 24, "description": "Journée de travail, en heures (7 par défaut)." },
        "fte_hours_per_month": { "type": "number", "minimum": 1, "maximum": 744, "description": "Heures d'un équivalent temps plein par mois (151,67 par défaut)." }
      }
    },
    "map": {
      "type": "object",
      "required": ["lanes"],
      "additionalProperties": false,
      "properties": {
        "lanes": { "type": "array", "minItems": 1, "maxItems": 200, "items": { "$ref": "#/$defs/lane" }, "description": "Couloirs de haut en bas." },
        "steps": { "type": "array", "items": { "$ref": "#/$defs/step" } },
        "flows": { "type": "array", "items": { "$ref": "#/$defs/flow" } }
      }
    },
    "lane": {
      "type": "object",
      "required": ["id", "name"],
      "additionalProperties": false,
      "properties": {
        "id": { "$ref": "#/$defs/id" },
        "name": { "allOf": [{ "$ref": "#/$defs/text" }, { "maxLength": 150 }] },
        "type": { "enum": ["person", "department", "system"] },
        "color": { "type": "string", "pattern": "^#[0-9A-Fa-f]{6}$" },
        "headcount": { "type": "integer", "minimum": 0, "maximum": 100000, "description": "Effectif d'un couloir de personnes ; interdit sur un couloir system." },
        "translations": { "$ref": "#/$defs/translations" }
      }
    },
    "step": {
      "type": "object",
      "required": ["id", "lane", "label"],
      "additionalProperties": false,
      "properties": {
        "id": { "$ref": "#/$defs/id" },
        "lane": { "type": "string", "description": "id d'un couloir de la même carte : l'acteur qui réalise l'étape (R du RACI)." },
        "label": { "allOf": [{ "$ref": "#/$defs/text" }, { "maxLength": 150 }] },
        "type": { "enum": ["task", "decision", "parallel", "inclusive", "wait", "start", "end"] },
        "column": { "type": "integer", "minimum": 0, "maximum": 10000, "description": "Donnée pour toutes les étapes d'une carte ou pour aucune." },
        "row": { "type": "integer", "minimum": 0, "maximum": 50 },
        "automation": { "enum": ["manual", "in_progress", "automated"] },
        "raci": {
          "type": "object",
          "additionalProperties": { "enum": ["A", "S", "C", "I", "R"] },
          "description": "Rôles des autres couloirs de la carte : { \"id du couloir\": \"A\" }. Interdit sur start, end, parallel et inclusive."
        },
        "sub_map": { "$ref": "#/$defs/map", "description": "Carte détaillée de l'étape. Interdite sur start, end, parallel et inclusive." },
        "translations": { "$ref": "#/$defs/translations" },
        "sheet": { "$ref": "#/$defs/sheet" },
        "volume": {
          "type": "object",
          "required": ["quantity", "period"],
          "additionalProperties": false,
          "description": "Volume entrant d'une étape start : { \"quantity\": 120, \"period\": \"month\" }. Interdit sur les autres types.",
          "properties": {
            "quantity": { "type": "number", "minimum": 0, "maximum": 1000000 },
            "period": { "enum": ["day", "week", "month", "year"] }
          }
        }
      }
    },
    "flow": {
      "type": "object",
      "required": ["source", "target"],
      "additionalProperties": false,
      "properties": {
        "source": { "type": "string" },
        "target": { "type": "string" },
        "label": { "type": "string", "maxLength": 60 },
        "share": { "type": "number", "minimum": 0, "maximum": 100, "description": "Part des dossiers, en %, d'une sortie de decision ou de inclusive ; interdite ailleurs." },
        "translations": { "$ref": "#/$defs/translations" }
      }
    },
    "sheet": {
      "type": "object",
      "additionalProperties": false,
      "description": "Fiche de l'étape, importée en brouillon ; chaque partie est facultative. Interdite sur start, end, parallel et inclusive.",
      "properties": {
        "actor": { "type": "string", "maxLength": 4000 },
        "purpose": { "type": "string", "maxLength": 4000 },
        "tools": { "type": "string", "maxLength": 4000 },
        "frequency": { "enum": ["daily", "weekly", "monthly", "month_end_close", "quarterly", "yearly", "occasional", "per_case"] },
        "duration_minutes": { "type": "number", "minimum": 0 },
        "lead_time": {
          "type": "object",
          "required": ["value", "unit"],
          "additionalProperties": false,
          "description": "Délai de l'étape, de l'arrivée du dossier à son départ, attentes comprises ; le jour est un jour ouvré.",
          "properties": {
            "value": { "type": "number", "minimum": 0, "maximum": 100000 },
            "unit": { "enum": ["minute", "hour", "day"] }
          }
        },
        "procedure": {
          "type": "array",
          "maxItems": 50,
          "items": {
            "type": "object",
            "additionalProperties": false,
            "properties": {
              "text": { "type": "string", "maxLength": 1000 },
              "tool": { "type": "string", "maxLength": 200 },
              "nature": { "enum": ["extract", "copy_paste", "re_enter", "format", "calculate", "check", "chase", "file", "send", "wait"] }
            }
          }
        },
        "free_text": { "type": "string", "maxLength": 20000 },
        "outputs": { "type": "array", "items": { "$ref": "#/$defs/output" } },
        "inputs": { "type": "array", "items": { "$ref": "#/$defs/input" } },
        "uses": { "type": "array", "items": { "$ref": "#/$defs/use" } }
      }
    },
    "output": {
      "type": "object",
      "required": ["name"],
      "additionalProperties": false,
      "properties": {
        "id": { "$ref": "#/$defs/id" },
        "name": { "allOf": [{ "$ref": "#/$defs/text" }, { "maxLength": 200 }] },
        "location": { "type": "string", "description": "Adresse web http(s):// ou chemin de fichier." },
        "share_confirmed": { "type": "boolean" }
      }
    },
    "input": {
      "type": "object",
      "required": ["name"],
      "additionalProperties": false,
      "properties": {
        "id": { "$ref": "#/$defs/id" },
        "name": { "allOf": [{ "$ref": "#/$defs/text" }, { "maxLength": 200 }] },
        "location": { "type": "string" },
        "share_confirmed": { "type": "boolean" },
        "wait_hours": { "type": "number", "minimum": 0, "maximum": 100000 }
      }
    },
    "use": {
      "type": "object",
      "required": ["document"],
      "additionalProperties": false,
      "properties": {
        "document": { "type": "string", "description": "id d'une sortie d'une autre étape de la même carte." },
        "wait_hours": { "type": "number", "minimum": 0, "maximum": 100000 },
        "percent_complete_accurate": { "type": "integer", "minimum": 0, "maximum": 100 },
        "pain_point": { "type": "string", "maxLength": 2000 },
        "lost_hours_per_month": { "type": "number", "minimum": 0, "maximum": 99999.9 }
      }
    }
  }
}
```
