<!-- format-topogenia.md -->
<!-- Description du paquet d'échange .topogenia, format 2.1 : archive, manifeste, entrées, références, écriture, personnes -->

# Le paquet d'échange `.topogenia`, format 2.1

Un paquet `.topogenia` transporte **un projet complet** : d'un poste au serveur, du serveur à un poste, d'un poste à un
autre, ou pour sauvegarder un projet seul (spec 7.1). Le fichier `.pmap` reste le format léger, qui ne porte ni fiches,
ni documents, ni irritants (`format-pmap.md`).

- Schéma publié : [`schemas/topogenia-package-2.1.schema.json`](../schemas/topogenia-package-2.1.schema.json)
  (JSON Schema 2020-12) ; celui de la version 2.0 reste à côté, pour les paquets écrits en 2.0.
- La 2.1 ajoute les saisies du flux de valeur (ADR 085) : réglages du temps de travail du projet, volume entrant d'un
  Début, part des dossiers d'une sortie de branchement, effectif d'un couloir, délai et fréquence « à chaque dossier »
  des fiches. Les chiffres calculés du flux ne sont pas dans le paquet : ils se recalculent à l'ouverture.
- Exemple fictif : [`samples/conges.topogenia`](../samples/conges.topogenia), un projet de demande de congés exporté
  sans les personnes.
- Décisions : ADR 011 (paquet, intégrité sans signature), ADR 051 (ce format), ADR 053 (versions et conversion), ADR 085
  (flux de valeur, version 2.1), AR-09 (sous-cartes vides), AR-34 (export sans les personnes par défaut).

## 1. L'archive

Un fichier ZIP dont les entrées sont, dans cet ordre :

| Entrée | Contenu | Présence |
|---|---|---|
| `manifest.json` | Format, versions, export, projet, options, liste des entrées avec taille et empreinte | toujours |
| `project.json` | Le projet, ses réglages, sa classification et ses compteurs de codes | toujours |
| `members.json` | Les membres et leurs rôles | seulement avec les personnes |
| `maps.json` | Les cartes, leurs couloirs, étapes, flèches et rôles RACI, la remontée ouverte et son cadre | toujours |
| `sheets.json` | Les fiches, leur déroulé et leurs versions validées (JSON figé) | toujours |
| `documents.json` | Les documents, leurs usages et le %C&A | toujours |
| `painpoints.json` | Les irritants et l'historique de leurs statuts | toujours |
| `translations.json` | Toutes les traductions, manuelles et automatiques, avec leur source | toujours |

Chaque entrée de l'archive est datée du 01/01/1980 à 00:00 : l'archive ne révèle aucune heure de la machine qui l'a
écrite. L'heure de l'export est dans le manifeste.

**Intégrité, pas authenticité** : le manifeste donne le SHA-256 de chaque entrée, contrôlé avant toute écriture à
l'import ; le paquet n'est pas signé (ADR 011). Il se transmet par les canaux de confiance de l'entreprise.

## 2. Le manifeste

```json
{
  "appVersion": "3.0.0",
  "entries": [
    { "path": "project.json", "sha256": "63d2...be1d", "size": 507 }
  ],
  "exportedAt": "2026-09-01T12:30:00.0000000Z",
  "exportedBy": "DOMAINE\\compte",
  "format": "topogenia-package",
  "formatVersion": "2.1",
  "options": { "withoutPeople": false },
  "projectCode": "CONGES",
  "schemaVersion": "2.1"
}
```

| Clé | Sens |
|---|---|
| `format` | Toujours `topogenia-package` |
| `formatVersion` | Version du format ; un paquet d'une version antérieure est converti à l'import, un paquet plus récent que l'application est refusé (spec 7.3) |
| `appVersion` | Version de l'application qui a exporté |
| `schemaVersion` | Version du schéma publié que suivent les entrées (`schemas/topogenia-package-<version>.schema.json`) |
| `exportedAt` | Heure UTC de l'export |
| `exportedBy` | Login de l'auteur de l'export ; absent sans les personnes |
| `projectCode` | Code du projet |
| `options.withoutPeople` | Vrai quand le paquet ne porte ni membres ni logins (section 5) |
| `entries[]` | Pour chaque entrée hors manifeste, dans l'ordre de l'archive : `path`, `size` (octets) et `sha256` (hexadécimal minuscule) |

Ces métadonnées sont hors du contenu comparé par l'aller-retour (spec 7.4).

## 3. Comment les entrées se désignent

Le paquet ne contient **aucun identifiant de la base** : un import en nouveau projet en crée de nouveaux (spec 7.3), et
deux paquets du même état doivent rester identiques. Les objets sont désignés par leurs codes lisibles, uniques et jamais
redonnés :

| Objet | Désigné par |
|---|---|
| Étape | son code, `CONGES-E004` |
| Couloir | son code, `CONGES-C02` |
| Irritant | son code, `CONGES-I001` |
| Carte | `parentStep`, le code de l'étape qu'elle détaille ; la carte racine n'en a pas |
| Flèche | ses deux extrémités, `source` et `target` (une seule flèche vivante par couple) |
| Ligne du déroulé, version validée, rôle RACI, usage, historique d'un irritant | rangés sous leur objet |

Le code du projet est le préfixe des codes ; un import en nouveau projet sous un autre code réécrit ces préfixes.

## 4. Les entrées

Toutes les clés sont en camelCase ; les valeurs des listes fermées gardent les codes stables du modèle (`month_end_close`,
`needs_review`, `strictly_confidential`...). Une valeur absente est omise ; une liste est toujours écrite, même vide.
Chaque objet versionné porte `createdAt`, `createdBy`, `updatedAt`, `updatedBy` (auteurs par login, section 5).

### 4.1 `project.json`

`{ "project": { ... } }` : `code`, `name`, `sourceLanguage`, `description`, `sharedFolder`, `classification`
(`internal`, `confidential`, `strictly_confidential`), `sharingGroupLabel`, `translationGlossary` (glossaire des
traductions, texte sur plusieurs lignes de 4 000 caractères au plus, ADR 065), `workdayHours` et `fteHoursPerMonth`
(obligatoires en 2.1 : journée de travail de 1 à 24 heures, 7 par défaut, et heures d'un ETP par mois de 1 à 744,
151.67 par défaut, deux décimales au plus), et `counters`, le dernier numéro donné par genre de code (`step`, `lane`,
`pain_point`, `solution`) : un code n'est jamais redonné après un import.

### 4.2 `members.json`

`{ "members": [ ... ] }`, par ordre d'affichage puis login : `login`, `displayName`, `role` (`reader`, `contributor`,
`facilitator`), `isSponsor`, `isKeyContributor`, `displayOrder`. Pas d'e-mail.

### 4.3 `maps.json`

`{ "maps": [ ... ] }` : la carte racine, puis les sous-cartes vivantes par code de leur étape parente, **sous-cartes
vides comprises** (AR-09). Une carte porte `parentStep`, `name`, `sourceLanguage` et :

- `lanes` : couloirs vivants dans leur ordre : `code`, `name`, `sourceLanguage`, `laneType` (`person`, `department`,
  `system`), `actor` (login), `color` (`#RRGGBB`), `sortOrder`, `sourceLane` (le couloir copié par « Détailler »),
  `headcount` (effectif, entier de 0 à 100 000, jamais sur un couloir `system`) ;
- `steps` : étapes vivantes en ordre de lecture (colonne, couloir, rangée) : `code`, `lane`, `column`, `row`, `label`,
  `sourceLanguage`, `stepType` (`task`, `decision`, `parallel`, `inclusive`, `wait`, `start`, `end`), `automationStatus`
  (`manual`, `in_progress`, `automated`), `assignee` (login), `volume` (sur un `start` seulement : `{ "quantity",
  "period" }`, quantité de 0 à 1 000 000, période `day`, `week`, `month`, `year`), et `raci`, tous les rôles portés par
  un couloir vivant (`A`, `S`, `C`, `I`), **rôles masqués compris** ; le R reste implicite (le couloir de l'étape) ;
- `flows` : flèches vivantes par place de leur source puis de leur cible : `source`, `target`, `label`, `share` (part
  des dossiers en pour cent, de 0 à 100, sur une flèche qui sort d'une `decision` ou d'un `inclusive` seulement) ;
- `dissolution` : la remontée **ouverte** de la carte, s'il y en a une : `step`, l'étape dissoute (supprimée, donc sans
  case), `frameSteps`, les étapes du cadre encore sur la carte, `createdFlows` et `createdLanes`, les flèches et couloirs
  créés par la remontée encore vivants. Le reliquat de l'étape dissoute (fiche, documents, usages, irritants) est dans
  les autres entrées, sous son code.

### 4.4 `sheets.json`

`{ "sheets": [ ... ] }`, par code d'étape : `step`, `status` (`empty`, `draft`, `validated`, `needs_review`), `actor`,
`purpose`, `tools`, `frequency` (dont `per_case`, « à chaque dossier », en 2.1), `durationMinutes`, `leadTime` (délai :
`{ "value", "unit" }`, valeur de 0 à 100 000, unité `minute`, `hour`, `day`), `freeText`, `sourceLanguage`,
`clarificationRequest`, `procedure` (lignes `position`, `text`, `tool`, `nature`) et `versions` (`number`, `content`, le
JSON figé de la version tel qu'il a été validé, `frequency`, `durationMinutes`, `leadTime`, `validatedBy`,
`validatedAt`).

### 4.5 `documents.json`

`{ "documents": [ ... ] }`, par code d'étape puis ordre d'entrée : `step` (l'étape qui produit une sortie ou reçoit une
entrée), `direction` (`input`, `output`), `name`, `location`, `locationType` (`url`, `path`), `waitHours`,
`shareConfirmed`, `sourceLanguage`, et `uses`, par code d'étape : `step`, `waitHours`, `percentCompleteAccurate` (%C&A),
`painPoint`.

### 4.6 `painpoints.json`

`{ "painPoints": [ ... ] }`, par code : `code`, `step` (absent pour un irritant de la carte), `description`,
`sourceLanguage`, `lostHoursPerMonth`, `status` (`reported`, `scored`, `under_review`, `in_progress`, `implemented`,
`adopted`, `dropped`) et `history`, dans l'ordre du temps : `statusBefore`, `statusAfter`, `comment`, `changedBy`,
`changedAt`.

### 4.7 `translations.json`

`{ "translations": [ ... ] }` : `object` (`project`, `map`, `lane`, `step`, `flow`), l'objet désigné comme en section 3
(`parentStep` pour une sous-carte, `lane`, `step`, ou `flow` avec `source` et `target` ; rien pour le projet et la carte
racine), `field` (`name`, `label`), `language`, `text`, `source` (`auto`, `manual`), `updatedAt`, `updatedBy`.

## 5. Les personnes

L'option **« sans membres ni auteurs »** (`withoutPeople`) est cochée par défaut (AR-34, spec 5.4). Sans les personnes,
le paquet n'a ni `members.json` ni aucun login : pas d'auteur (`createdBy`, `updatedBy`, `validatedBy`, `changedBy`), pas
d'acteur de couloir, pas d'assigné d'étape, pas d'`exportedBy`. Les dates restent. Le champ « acteur » d'une fiche est un
texte saisi, pas un login : il reste.

Avec les personnes, chaque auteur est écrit par son login `DOMAINE\compte`, anciens membres compris.

## 6. Ce que le paquet ne porte pas

La corbeille (cartes, couloirs, étapes et flèches supprimés, et ce qui pend à leurs objets), les remontées fermées, les
lots de suppression, le journal des opérations (annuler, rétablir), les versions des enregistrements et la version de
structure du projet (jetons de concurrence de la base). Les cases d'origine des étapes d'une remontée ne sont pas non
plus exportées : elles désignent des couloirs de la sous-carte dissoute, dans la corbeille.

## 7. Écriture déterministe

Deux exports du même état donnent **les mêmes entrées, octet pour octet**, sur l'un ou l'autre moteur (spec 7.2) :

- clés de chaque objet dans l'ordre ordinal ; listes dans l'ordre décrit en section 4 ; une liste sans clé naturelle est
  départagée par son texte ;
- UTF-8 sans BOM, indentation de deux espaces, fins de ligne LF, une fin de ligne finale ;
- textes écrits tels quels, seuls le guillemet, la barre oblique inverse et les caractères de contrôle étant échappés ;
- dates UTC à sept décimales (`2026-09-01T07:30:00.0000000Z`) ; décimaux sans zéros de fin (`15.5`, jamais `15.50`).

Comparer deux paquets du même projet (par exemple avec `diff` sur les entrées décompressées) montre donc ce qui a changé
entre deux états.

## 8. Validation

Le schéma décrit le manifeste à sa racine et chaque entrée par la définition de `$defs` qui porte son nom (`project`,
`members`, `maps`, `sheets`, `documents`, `painpoints`, `translations`). Il reprend les bornes du domaine (longueurs des
textes, cases, durées, heures) et les règles des textes saisis : rognés, sans balise HTML, sans caractère de contrôle
autre que la tabulation et les fins de ligne. L'application l'embarque et le valide avec un validateur du sous-ensemble
de mots-clés qu'il emploie (ADR 051) ; l'import refait en plus toutes les règles du domaine.

Chaque version du format garde son schéma dans `schemas/` : une version suivante ajoute le sien à côté. Un paquet d'une
version antérieure est vérifié contre le schéma de sa version (entrées, tailles, empreintes des octets reçus), puis
converti de version en version jusqu'à la version courante, chaque étape vérifiée contre le schéma de la version
suivante (ADR 053). La 2.0 est la première version. La conversion 2.0 -> 2.1 ajoute au projet les réglages par défaut
du temps de travail (7 h, 151.67 h) et rien d'autre ; un paquet 2.1 est refusé par une application 2.0
(`PAQUET_TROP_RECENT`). L'import refait aussi les règles du flux de valeur : volume seulement sur un Début, part
seulement sur une sortie de décision ou de OU inclusif, effectif jamais sur un couloir Système, délai avec son unité.

## 9. Import

L'import (ADR 052) refait toutes ces vérifications avant d'écrire quoi que ce soit, dans cet ordre : archive ZIP lisible,
8 entrées au plus, chacune d'un nom connu et d'un chemin qui reste dans l'archive ; manifeste de 1 Mo au plus, de ce
format et de cette version (un paquet d'une version plus récente est refusé `PAQUET_TROP_RECENT`), conforme au schéma ;
entrées annoncées et présentes, `members.json` présent si et seulement si le paquet porte les personnes ; taille lue en
flux, jamais au-delà de la taille annoncée, de 200 Mo par entrée ni de 500 Mo en tout ; SHA-256 du manifeste ; schéma de
chaque entrée (une clé écrite deux fois dans un objet est refusée) ; puis toutes les références par code (section 3),
chaque valeur par la règle de sa saisie (textes en unités UTF-16, adresses, nombres, contenu des versions validées,
traductions, logins, langue d'origine configurée de chaque objet ; raison `values` avec le nom des champs) et les
règles de la structure. Un nom ou un texte qui ne se lit pas (moitié d'une paire de substitution échappée) est refusé
avec la raison `json`. Une traduction dans la langue d'origine de son objet ou dans une langue que l'installation ne
configure pas est ignorée, les autres gardées, et l'aperçu compte ce que l'import écrit (AR-119). Tout refus est
`PAQUET_INVALIDE`, avec sa raison et l'entrée en cause. Au remplacement d'un projet, le projet importé garde la
classification du projet remplacé (AR-117).

Le projet importé reçoit de nouveaux identifiants et garde les codes et les compteurs du paquet ; sous un autre code de
projet, chaque code lisible est réécrit avec le nouveau préfixe et garde son numéro. Les auteurs sont retrouvés par
login, et créés s'ils sont inconnus ; sans les personnes, l'importateur est l'auteur de chaque création. Un remplacement
qui ne reprend pas les membres du paquet garde ceux du projet remplacé (AR-115). Une remontée
ouverte revient avec son étape dissoute, supprimée dans un lot neuf, et la case d'origine de chaque étape du cadre est sa
case actuelle.
