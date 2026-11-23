<!-- format-collection.md -->
<!-- The template collection .pmaps 1.0: what it is, its structure, its bounds, what makes it refused, how it opens, which version of the application it needs (ADR 114) -->

# Format de fichier `.pmaps` (collection de modèles), version 1.0

Une **collection de modèles** réunit plusieurs processus dans un seul fichier. Chacun de ses éléments est un document
`.pmap` complet (`docs/format-pmap.md`), recopié tel quel : la collection n'est qu'une enveloppe, elle n'ajoute aucune
règle à la description d'un processus. Elle sert à la galerie du site, qui offre par domaine et par langue tous ses
modèles en un seul téléchargement ; elle s'ouvre par **Fichier > Ouvrir un fichier…**, qui crée un projet par processus
coché. Elle se lit au Bloc-notes comme un `.pmap` : du texte JSON, ni archive ni script (ADR 114).

| Élément | Valeur |
|---|---|
| Extension | `.pmaps` |
| Contenu | JSON (RFC 8259), UTF-8 |
| Schéma | `schemas/topogenia-collection-1.0.schema.json` (l'enveloppe seule ; chaque élément suit `schemas/pmap-<version>.schema.json`) |
| Marqueur | `"format": "topogenia-collection"` |
| Version | `"version": "1.0"`, indépendante de la version de chaque `.pmap` (1.1 à 1.5) |
| Application | 3.4 et suivantes pour la collection ; un élément copié seul s'ouvre dans toute version qui lit sa version de `.pmap` (`format-pmap.md` § 10) |

Un exemple est dans `samples/exemple.pmaps` : les deux `.pmap` d'exemple, `samples/minimal.pmap` et
`samples/cloture-mensuelle.pmap`, réunis.

## 1. Exemple

```json
{
  "format": "topogenia-collection",
  "version": "1.0",
  "name": "Achats",
  "description": "Les processus d'achat les plus courants, à adapter.",
  "processes": [
    { "format": "topogenia", "version": "1.5", "project": { "code": "ACHAT", "name": "Demande d'achat", "language": "fr" }, "map": { … } },
    { "format": "topogenia", "version": "1.5", "project": { "code": "RECEP", "name": "Réception", "language": "fr" }, "map": { … } }
  ]
}
```

## 2. Structure

| Propriété | Obligatoire | Règle |
|---|---|---|
| `$schema` | non | Texte, ignoré |
| `format` | oui | `topogenia-collection` |
| `version` | oui | `1.0` |
| `name` | oui | Nom de la collection, montré en tête de la liste à l'ouverture ; texte de 200 caractères, règles de toute saisie (`format-pmap.md` § 4, règle 7) ; il ne nomme aucun projet |
| `description` | non | Texte de 4 000 caractères, informatif |
| `processes` | oui | Liste de 1 à 50 objets, chacun un document `.pmap` complet, dans l'ordre de la liste montrée |

Aucune autre propriété n'est admise. L'enveloppe ne porte **ni langue ni code** : chaque `.pmap` porte son code, son
nom, sa langue, ses traductions et ses points à vérifier, et le projet créé les prend de lui. Un élément est une valeur
JSON, jamais un texte qui contiendrait un `.pmap` échappé.

Une collection est écrite dans une seule langue (les modèles de la galerie existent en un fichier par langue, une
collection par langue) ; rien ne l'impose, mais un élément dont la langue n'est pas configurée est refusé seul (section 4).

## 3. Bornes

| Borne | Valeur | Porte sur |
|---|---|---|
| Corps de la demande | 10 Mo, refusé avant d'être lu (413) ; l'interface refuse un fichier plus gros avant de le lire | la collection entière |
| Texte lu | 8 millions de caractères | la collection entière |
| Profondeur JSON | 256 niveaux (l'enveloppe en ajoute deux à chaque élément) | la collection entière |
| Processus | 50 au plus | la liste `processes` |
| Étapes, couloirs, niveaux | 5 000 étapes, 200 couloirs par carte, 12 niveaux de cartes détaillées | chaque élément, par les règles du `.pmap` |
| Fautes rendues | 30 | l'enveloppe, puis chaque élément |

## 4. Règles de refus

La lecture se fait en deux temps. La frontière entre les deux est celle des schémas : ce que décrit le schéma de la
collection est l'enveloppe, ce que décrit le schéma du `.pmap` est l'élément.

**L'enveloppe.** Le texte est nettoyé comme un `.pmap` (marque d'ordre des octets, bloc de code Markdown autour,
virgules en fin de liste et commentaires `//` admis). Une faute de l'enveloppe refuse **le fichier entier**
(`FICHIER_INVALIDE`, avec `count` et `errors`, chaque faute par son chemin, son code et son message), avant qu'aucun
élément soit lu :

| Cas | Code | Chemin |
|---|---|---|
| Fichier vide | `PMAP_VIDE` | (racine) |
| JSON illisible, de plus de 256 niveaux, ou une clé écrite deux fois dans un même objet (enveloppe ou processus) | `PMAP_JSON` | (racine) |
| Racine qui n'est pas un objet | `PMAP_PAS_OBJET` | (racine) |
| `format` autre que `topogenia-collection` (un `.pmap` en est un) | `PMAP_FORMAT` | `format` |
| `version` autre que `1.0` | `PMAP_VERSION` | `version` |
| Propriété inconnue | `PMAP_PROPRIETE_INCONNUE` | son nom |
| `format`, `version`, `name` ou `processes` absent ; `processes` vide | `PMAP_OBLIGATOIRE` | son nom |
| `processes` qui n'est pas une liste | `PMAP_TYPE_LISTE` | `processes` |
| Élément qui n'est pas un objet | `PMAP_TYPE_OBJET` | `processes[2]` |
| `name` ou `description` hors des règles des textes | code de la règle de saisie (`TEXTE_TROP_LONG`…) | son nom |
| Plus de 50 processus | `COLLECTION_TROP_DE_PROCESSUS` (`count`, `max`) | `processes` |

**Chaque élément.** Son texte est passé tel quel au lecteur du `.pmap`, avec les réglages de l'ouverture d'un fichier
(langues configurées, lettres du RACI) : toutes les règles de `format-pmap.md` s'appliquent, aucune n'est réécrite. Une
faute d'un élément ne refuse **que lui** : il reste dans la liste, décoché, avec ses fautes (chemin dans l'élément, code,
message), et les autres s'ouvrent. Un élément d'une langue que l'installation ne configure pas est refusé ainsi
(`PMAP_LANGUE`) ; la galerie offre alors la collection de l'autre langue.

Une collection n'est pas un `.pmap` : la 3.3 et les versions d'avant la refusent (`PMAP_FORMAT`,
`PMAP_PROPRIETE_INCONNUE`) ; il faut la 3.4 pour l'ouvrir. L'insertion d'un fichier comme étape détaillée (ADR 108)
refuse une collection avant de l'envoyer et renvoie à « Ouvrir un fichier… ».

## 5. Ouverture

| Route | Droit | Effet |
|---|---|---|
| `POST /api/projects/import-collection/preview` | Administrateur | Corps `{ "content" }`, 10 Mo, lu après le droit : la liste des processus, rien d'écrit (ni projet, ni journal des opérations, ni audit) |
| `POST /api/projects/import-pmap` | Administrateur | La route d'ouverture d'un `.pmap` (`format-pmap.md` § 9), appelée une fois par ligne cochée avec le texte de l'élément |

1. **L'aperçu.** Le dialogue d'ouverture reconnaît une collection, choisie, déposée ou collée, à son `format`. L'aperçu
   rend le nom et la description de la collection, puis par élément : son rang, son nom, sa langue, le code du fichier,
   le code proposé, sa description, ses nombres (cartes, couloirs, étapes, flèches, fiches), ses points à vérifier, ses
   fautes, et `content`, le texte de l'élément tel que l'hôte l'a lu. Un élément en faute n'a ni code proposé ni
   nombres. L'interface ne relit jamais l'enveloppe : elle renvoie `content` tel quel.
2. **Le code proposé.** Le code du fichier, sinon celui que propose le nom du processus (ADR 111) ; puis le premier code
   libre selon la règle de la corbeille, en comptant aussi les codes proposés plus haut dans la liste. Le code ACHAT pris
   donne ACHATA ; deux éléments ACHAT donnent ACHAT puis ACHATA. Chaque code reste modifiable.
3. **La liste.** Tous les processus valides sont cochés ; une case d'en-tête coche ou décoche tout. Rien n'est envoyé
   avant « Créer les projets », jamais une ligne décochée.
4. **La création.** Un projet par ligne cochée, dans l'ordre, par la route d'ouverture : chaque projet est créé entier ou
   pas du tout, avec son événement `project.imported`. Une ligne refusée (code pris ou mis à la corbeille entre-temps,
   `PROJET_CODE_EXISTANT`, `PROJET_CODE_CORBEILLE`) est marquée, les suivantes continuent ; elle garde son code
   modifiable et peut être réessayée. La liste finale dit ce qui est créé et ce qui ne l'est pas.
5. **Mode Serveur.** Seul l'administrateur ouvre une collection ; il devient le seul animateur de chaque projet créé,
   comme à l'ouverture d'un fichier. Les codes sont ceux de tout le serveur.

L'application ne va chercher aucune collection : la personne la télécharge depuis la galerie du site, par son
navigateur, et peut vérifier son empreinte SHA-256 publiée à côté du fichier.

## 6. Écrire une collection

- Recopier chaque `.pmap` entier, sans le changer, comme élément de `processes` ; un élément s'ouvre ensuite seul s'il
  est recopié hors de la collection.
- Donner à chaque processus son code et sa langue dans son `.pmap`, jamais dans l'enveloppe.
- Une IA écrit un processus à la fois, en `.pmap` ; la collection se fait ensuite, en réunissant les fichiers.

## 7. Versions

- `1.0` (3.4, ADR 114) : première version. Une version plus récente est refusée (`PMAP_VERSION`, avec les versions lues) :
  l'application doit être mise à jour pour l'ouvrir.
