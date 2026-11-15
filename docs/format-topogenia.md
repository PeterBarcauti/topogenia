<!-- format-topogenia.md -->
<!-- Description du paquet d'échange .topogenia, format 2.4 : archive, manifeste, entrées, références, écriture, personnes -->

# Le paquet d'échange `.topogenia`, format 2.4

Un paquet `.topogenia` transporte **un projet complet** : d'un poste au serveur, du serveur à un poste, d'un poste à un
autre, ou pour sauvegarder un projet seul (spec 7.1). Le fichier `.pmap` reste le format léger, qui ne porte ni fiches,
ni documents, ni irritants (`format-pmap.md`).

- Schéma publié : [`schemas/topogenia-package-2.4.schema.json`](../schemas/topogenia-package-2.4.schema.json)
  (JSON Schema 2020-12) ; ceux des versions 2.3, 2.2, 2.1 et 2.0 restent à côté, pour les paquets écrits dans ces versions.
- La 2.4 ajoute le commentaire de présentation d'une étape et ses traductions (ADR 099) : ce que la présentation dit
  de l'étape à la place de ses propres phrases, dans chaque langue où il est écrit ; et, de la même façon, le
  commentaire de présentation d'une flèche (révision du 13/11/2026), dit pendant le passage d'une étape à la suivante.
  Elle ajoute aussi les voix que le projet garde pour sa présentation (ADR 103) : la voix du navigateur choisie pour
  chaque langue, reprise par défaut par qui présente le projet ensuite.
  **Un projet sans commentaire ni voix est écrit en 2.3 ou avant**, selon son contenu, que la version 3.1 lit encore.
- La 2.3 ajoute le résumé d'une carte et ses traductions (ADR 094), puis, pour la démarche Makigami (ADR 095), la classe
  de valeur d'une étape, le support d'un document, les bornes de la durée d'une fiche, le plan d'actions d'un irritant,
  les photos des indicateurs du projet et la source d'un état cible.
  **Un projet qui n'a rien de tout cela est écrit en 2.2 ou en 2.1**, selon les tracés choisis, que les versions d'avant
  lisent encore.
- La 2.2 ajoute le tracé qu'un utilisateur choisit pour une flèche (ADR 091) : son côté de départ, son côté
  d'arrivée et un passage de la grille. **Un projet dont aucune flèche n'a de tracé choisi est écrit en 2.1** : son
  contenu est celui de la 2.1, qu'une version 3.0 de l'application lit encore.
- La 2.1 ajoute les saisies du flux de valeur (ADR 085) : réglages du temps de travail du projet, volume entrant d'un
  Début, part des dossiers d'une sortie de branchement, effectif d'un couloir, délai et fréquence « à chaque dossier »
  des fiches. Les chiffres calculés du flux ne sont pas dans le paquet : ils se recalculent à l'ouverture.
- Exemple fictif : [`samples/conges.topogenia`](../samples/conges.topogenia), un projet de demande de congés exporté
  sans les personnes, avec une photo de ses indicateurs (« Avant l'atelier ») calculée par l'application.
- Décisions : ADR 011 (paquet, intégrité sans signature), ADR 051 (ce format), ADR 053 (versions et conversion), ADR 085
  (flux de valeur, version 2.1), ADR 089 et 090 (contrôles de l'import ajoutés après l'audit externe, format inchangé),
  ADR 091 (tracé choisi des flèches, version 2.2), ADR 094 (résumé des cartes, version 2.3), ADR 095 (Makigami :
  classes de valeur, supports, photos des indicateurs et état cible, version 2.3), ADR 099 (commentaire de présentation,
  version 2.4), ADR 103 (voix du projet, version 2.4), AR-09 (sous-cartes vides), AR-34 (export sans les personnes par
  défaut).

## 1. L'archive

Un fichier ZIP dont les entrées sont, dans cet ordre :

| Entrée | Contenu | Présence |
|---|---|---|
| `manifest.json` | Format, versions, export, projet, options, liste des entrées avec taille et empreinte | toujours |
| `project.json` | Le projet, ses réglages, sa classification, ses compteurs de codes, ses photos des indicateurs et la source d'un état cible | toujours |
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
  "appVersion": "3.0.2",
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
| `formatVersion` | Version du format : 2.4 quand une étape ou une flèche porte un commentaire de présentation (ADR 099) ou quand le projet garde une voix (ADR 103), 2.3 quand le contenu porte une valeur de la 2.3 (résumé d'une carte, ADR 094 ; classe de valeur, support, photo des indicateurs ou source d'un état cible, ADR 095), 2.2 quand une flèche a un tracé choisi, 2.1 sinon (ADR 091) ; un paquet d'une version antérieure est converti à l'import, un paquet plus récent que l'application est refusé (spec 7.3) |
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

En 2.3 (Makigami M6, M8, ADR 095), deux clés absentes quand il n'y a rien à porter :

- `snapshots` : les photos des indicateurs de la carte racine et de ses détails, une liste d'au moins une photo, dans
  l'ordre où elles ont été prises. Chacune porte `label` (100 caractères au plus), `origin` (`photo`, prise par un
  animateur, ou `baseline`, la référence d'un état cible, une au plus), ses indicateurs (`leadHours`, délai en heures de
  travail, et deux de ses parties, `handoffWaitHours`, l'attente après un relais, et `otherWaitHours`, l'autre attente ;
  `workMinutes`, travail par dossier ; `valueAddedShare` et `coverage`, parts de 0 à 1 ; `handoffs` et
  `handoffsPerCase`, relais ; `mediaBreaks`, ruptures de support ; `rolledCompleteAccurate`, %C&A cumulé de 0 à 1 ;
  `lostHoursPerMonth` ; `openPainPoints`), un indicateur inconnu étant absent, et son audit. Une photo est figée : l'import
  la reprend telle quelle, sans la recalculer.
- `baselineProject` : le code du projet d'où vient un état cible, écrit seulement quand ce projet est vivant et que la
  personne qui exporte peut le voir. Un identifiant ne voudrait rien dire dans une autre installation : l'import relie
  l'état cible au projet vivant de ce code (section 9).

En 2.4 (ADR 103), une clé absente quand le projet ne garde aucune voix :

- `presentationVoices` : les voix que le projet garde pour sa présentation, une par langue au plus, rangées par langue :
  `[{ "language", "name", "local" }]`. `name` est le nom de la voix tel que le navigateur le donne, un texte saisi d'une
  ligne de 200 caractères au plus (ni tabulation ni fin de ligne) ; `local` dit où elle est lue : `true` pour une voix
  du poste, `false` pour une voix lue en ligne. Le paquet ne porte ni l'auteur ni la version d'une voix : à l'import,
  chacune est écrite par l'importateur, en version 1, et laissée si sa langue n'est pas configurée ; deux voix pour une
  même langue refusent le paquet (`values`, `presentationVoices`). Les voix sont écrites **avec ou sans les
  personnes** (section 5) : une voix n'est pas une personne.

La description du projet donnée au modèle de l'assistant laisse les photos et la source de côté : il lit la carte, pas
des indicateurs figés sans ceux d'aujourd'hui. Elle ne porte pas non plus les voix du projet.

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
  (`manual`, `in_progress`, `automated`), `valueClass` (2.3, Makigami : `value_added`, `necessary`, `waste`, sur une étape
  avec contenu seulement), `assignee` (login), `volume` (sur un `start` seulement : `{ "quantity",
  "period" }`, quantité de 0 à 1 000 000, période `day`, `week`, `month`, `year`), et `raci`, tous les rôles portés par
  un couloir vivant (`A`, `S`, `C`, `I`), **rôles masqués compris** ; le R reste implicite (le couloir de l'étape) ;
  `presentationComment` (2.4, absent sans original, ADR 099) : `{ "text", "language" }`, l'original du commentaire,
  le texte saisi de 500 caractères au plus (une ligne écrite est une phrase dite) dans la langue d'origine de
  l'étape ; `presentationCommentTranslations` (2.4, absent sans traduction) : ses traductions par langue,
  `[{ "language", "text", "source" }]`, la source étant `manual` (saisie à la main), `assistant` ou `workbook`
  (classeur) ; une traduction peut être portée sans original. Le paquet ne porte ni l'auteur ni la version d'une
  ligne : à l'import, chacune est écrite par l'importateur, en version 1, et laissée si sa langue n'est pas
  configurée ; la ligne de la langue d'origine de l'étape est l'original, toujours une saisie ; deux lignes d'une étape
  dans une même langue refusent le paquet (`values`, `presentationComment`) ;
- `flows` : flèches vivantes par place de leur source puis de leur cible : `source`, `target`, `label`, `share` (part
  des dossiers en pour cent, de 0 à 100, sur une flèche qui sort d'une `decision` ou d'un `inclusive` seulement),
  `route` (2.2, absent sans choix) : le tracé choisi par l'utilisateur, chaque partie facultative :
  - `from` et `to`, le côté de départ et le côté d'arrivée : `top`, `bottom`, `left` ou `right` ;
  - `via`, un passage, un seul parmi : une allée `{ "aisle": n }` (l'allée à droite de la colonne n, de -1, avant la
    colonne 0, à 10 000) ; le milieu d'une colonne `{ "column": n }` (de 0 à 10 000) ; une rangée d'un couloir
    `{ "lane", "row" }` (le code d'un couloir de la même carte, une rangée de 0 à 50), le long de sa bande du haut ou du
    bas avec `"edge": "top"` ou `"bottom"`, ou par son milieu sans `edge` ;

  et `presentationComment` et `presentationCommentTranslations` (2.4, ADR 099, révision du 13/11/2026), le commentaire
  de présentation de la flèche, écrits et lus comme ceux d'une étape : mêmes champs, mêmes bornes, mêmes refus. Une
  flèche n'a pas de langue à elle : son original est la ligne de la langue d'origine de son **étape source**
  (`sourceLanguage` de l'étape `source`) ; toute autre ligne est une traduction. Le commentaire d'une flèche à la
  corbeille n'est pas porté, comme la flèche. Les flèches que nomme une remontée (`createdFlows`) ne le répètent pas ;
- `dissolution` : la remontée **ouverte** de la carte, s'il y en a une : `step`, l'étape dissoute (supprimée, donc sans
  case), `frameSteps`, les étapes du cadre encore sur la carte, `createdFlows` et `createdLanes`, les flèches et couloirs
  créés par la remontée encore vivants. Le reliquat de l'étape dissoute (fiche, documents, usages, irritants) est dans
  les autres entrées, sous son code ;
- `summary` (2.3, absent sans résumé) : le résumé de la carte, une phrase de 200 caractères au plus sur une ligne,
  `text`, `sourceLanguage`, `origin` (`ai`, écrit par l'IA et relu, ou `manual`), `model` (le modèle de l'IA,
  facultatif), `current` (vrai quand il décrivait encore la carte à l'export : l'import reprend alors l'empreinte de la
  carte reconstruite, faute de quoi il reste périmé), `createdAt`, `createdBy`, `updatedAt`, `updatedBy`.

### 4.4 `sheets.json`

`{ "sheets": [ ... ] }`, par code d'étape : `step`, `status` (`empty`, `draft`, `validated`, `needs_review`), `actor`,
`purpose`, `tools`, `frequency` (dont `per_case`, « à chaque dossier », en 2.1), `durationMinutes`, `durationMinMinutes` et
`durationMaxMinutes` (2.3, Makigami M5 : durée la plus courte et la plus longue du brouillon, chacune facultative, dans
l'ordre autour de la durée ; le JSON figé d'une version les porte sous `duration_min_minutes` et `duration_max_minutes`),
`leadTime` (délai :
`{ "value", "unit" }`, valeur de 0 à 100 000, unité `minute`, `hour`, `day`), `freeText`, `sourceLanguage`,
`clarificationRequest`, `procedure` (lignes `position`, `text`, `tool`, `nature`) et `versions` (`number`, `content`, le
JSON figé de la version tel qu'il a été validé, `frequency`, `durationMinutes`, `leadTime`, `validatedBy`,
`validatedAt`).

### 4.5 `documents.json`

`{ "documents": [ ... ] }`, par code d'étape puis ordre d'entrée : `step` (l'étape qui produit une sortie ou reçoit une
entrée), `direction` (`input`, `output`), `name`, `location` et `locationType` (`url`, `path`), toujours ensemble,
`waitHours` (sur une entrée seulement : l'attente d'une sortie est portée par chacun de ses usages), `medium` (2.3, Makigami :
`paper`, `email`, `spreadsheet`, `file`, `software`, `other`), `shareConfirmed`,
`sourceLanguage`, et `uses`, par code d'étape : `step`, `waitHours`, `percentCompleteAccurate` (%C&A), `painPoint`. Un
usage porte sur une sortie et se fait dans une autre étape que celle qui la produit ; une entrée a `uses` vide.

### 4.6 `painpoints.json`

`{ "painPoints": [ ... ] }`, par code : `code`, `step` (absent pour un irritant de la carte), `description`,
`sourceLanguage`, `lostHoursPerMonth`, `action`, `owner` et `dueDate` (2.3, Makigami M7 : l'action décidée, 500
caractères, qui la porte, 150 caractères en texte libre, l'échéance, un jour `aaaa-mm-jj`), `status` (`reported`, `scored`, `under_review`, `in_progress`, `implemented`,
`adopted`, `dropped`) et `history`, dans l'ordre du temps : `statusBefore`, `statusAfter`, `comment`, `changedBy`,
`changedAt`.

### 4.7 `translations.json`

`{ "translations": [ ... ] }` : `object` (`project`, `map`, `lane`, `step`, `flow`, `summary` en 2.3), l'objet désigné
comme en section 3 (`parentStep` pour une sous-carte et son résumé, `lane`, `step`, ou `flow` avec `source` et `target` ;
rien pour le projet et la carte racine), `field` (`name`, `label` ; `label` pour un résumé), `language`, `text`, `source` (`auto`, `manual`), `updatedAt`, `updatedBy`.

## 5. Les personnes

L'option **« sans membres ni auteurs »** (`withoutPeople`) est cochée par défaut (AR-34, spec 5.4). Sans les personnes,
le paquet n'a ni `members.json` ni aucun login : pas d'auteur (`createdBy`, `updatedBy`, `validatedBy`, `changedBy`), pas
d'acteur de couloir, pas d'assigné d'étape, pas d'`exportedBy`. Les dates restent. Le champ « acteur » d'une fiche est un
texte saisi, pas un login : il reste. Les voix du projet (`presentationVoices`, section 4.1) restent aussi : le nom
d'une voix est celui d'une voix de synthèse du navigateur, pas celui d'une personne.

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
- dates UTC à sept décimales (`2026-09-01T07:30:00.0000000Z`) ; décimaux sans zéros de fin (`15.5`, jamais `15.50`) ;
  indicateurs des photos, calculés en virgule flottante binaire, dans leur forme la plus courte qui se relit à la même
  valeur (`0.30000000000000004`, `2`), jamais arrondis à un décimal ;
- photos prises au même instant départagées par leur texte, jamais par leur identifiant, que l'import renouvelle.

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
(`PAQUET_TROP_RECENT`). La conversion 2.1 -> 2.2 ne change rien : un paquet 2.1 n'a pas de tracé choisi. Un paquet 2.2
est refusé par une application 3.0 ; l'écriture en 2.1 d'un projet sans tracé choisi l'évite tant qu'aucune flèche
n'en a un. La conversion 2.2 -> 2.3 ne change rien non plus : un paquet 2.2 n'a aucune valeur de la 2.3 (son schéma
refuse un résumé, une classe, un support, une photo ou une source) ; l'écriture en 2.2 ou 2.1 d'un projet qui n'en a
aucune garde la lecture par les versions d'avant. La conversion 2.3 -> 2.4 ne change rien : un paquet 2.3 n'a pas de
commentaire de présentation ni de traduction d'un commentaire, sur une étape comme sur une flèche, ni de voix du projet
(son schéma les refuse) ; un paquet 2.4 est refusé par une
application 3.1, et l'écriture en 2.3 ou avant d'un projet sans commentaire ni voix l'évite. L'import refait les règles du tracé : un couloir de passage connu du paquet (sinon `reference`) et sur la
carte de la flèche (sinon `structure`, `route:`). L'import refait aussi les règles du flux de valeur : volume seulement sur un Début, part
seulement sur une sortie de décision ou de OU inclusif, effectif jamais sur un couloir Système, délai avec son unité.
Le volume et l'effectif sont contrôlés sur chaque enregistrement reconstruit, supprimés compris, comme la base les
contrôle sur toutes ses lignes (ADR 089).

## 9. Import

Le fichier reçu fait 100 Mo au plus : au-delà, l'hôte le refuse avant de le lire (route d'import comme `--import`),
si bien que les bornes de 200 Mo par entrée et de 500 Mo en tout, plus bas, ne valent que pour le contenu décompressé.

L'import (ADR 052) refait toutes ces vérifications avant d'écrire quoi que ce soit, dans cet ordre : archive ZIP lisible,
8 entrées au plus, chacune d'un nom connu et d'un chemin qui reste dans l'archive ; manifeste de 1 Mo au plus, de ce
format et de cette version (un paquet d'une version plus récente est refusé `PAQUET_TROP_RECENT`), conforme au schéma ;
entrées annoncées et présentes, `members.json` présent si et seulement si le paquet porte les personnes ; taille lue en
flux, jamais au-delà de la taille annoncée, de 200 Mo par entrée ni de 500 Mo en tout ; SHA-256 du manifeste ; schéma de
chaque entrée (une clé écrite deux fois dans un objet est refusée) ; puis toutes les références par code (section 3),
chaque valeur par la règle de sa saisie (textes en unités UTF-16, adresses, nombres, contenu des versions validées,
traductions, logins, langue d'origine configurée de chaque objet ; raison `values` avec le nom des champs) et les
règles de la structure (raison `structure`, avec le nom des règles en cause) : les invariants de la carte, une seule
carte racine d'où chaque carte vivante s'atteint, pas de sous-carte sous une étape sans contenu, un lien de document
avec son type, une attente de document sur une entrée seulement, un usage sur la sortie d'une autre étape que la sienne
(`document-use`, ADR 090). Un nom ou un texte qui ne se lit pas (moitié d'une paire de substitution échappée) est refusé
avec la raison `json`. Une traduction dans la langue d'origine de son objet ou dans une langue que l'installation ne
configure pas est ignorée, les autres gardées, et l'aperçu compte ce que l'import écrit (AR-119). Tout refus est
`PAQUET_INVALIDE`, avec sa raison et l'entrée en cause. Au remplacement d'un projet, le projet importé garde la
classification du projet remplacé (AR-117). Les photos sont contrôlées comme à leur prise (libellé saisi, parts de 0 à 1,
comptes et heures jamais négatifs, une référence au plus ; raison `values`). La source d'un état cible
(`baselineProject`) se relie au projet vivant de ce code que la personne qui importe peut voir ; sinon, et quand c'est
le projet que l'import remplace (il part à la corbeille), le lien reste vide et la photo de référence demeure. Les états
cibles qui nommaient le projet remplacé nomment le projet importé (F-108). Créer un état cible passe par le même paquet,
mais la cible ne garde aucune photo de sa source : seulement sa référence. Les voix du projet (`presentationVoices`,
ADR 103) sont contrôlées par la règle de leur nom et ne viennent qu'une fois par langue (raison `values`) ; celles d'une
langue que l'installation ne configure pas sont laissées de côté, comme les traductions (AR-119). Elles sont écrites
sur le projet importé, sur celui qui en remplace un autre (le projet remplacé part à la corbeille avec les siennes) et
sur un état cible, qui prend celles de sa source.

Le projet importé reçoit de nouveaux identifiants et garde les codes et les compteurs du paquet ; sous un autre code de
projet, chaque code lisible est réécrit avec le nouveau préfixe et garde son numéro. Les auteurs sont retrouvés par
login, et créés s'ils sont inconnus ; sans les personnes, l'importateur est l'auteur de chaque création. Un remplacement
qui ne reprend pas les membres du paquet garde ceux du projet remplacé (AR-115). Une remontée
ouverte revient avec son étape dissoute, supprimée dans un lot neuf, et la case d'origine de chaque étape du cadre est sa
case actuelle.
