<!-- PMAP_GUIDE_IA.md -->
<!-- Guide à donner à une IA pour traduire une description de processus, un récit compris, en fichier .pmap 1.2 -->

# Guide .pmap pour une IA : traduire un processus en fichier (format 1.2)

Ce guide suffit pour produire un fichier `.pmap` que l'application ouvre du premier coup, à partir de n'importe quelle
description d'un processus : récit, compte rendu d'entretien, procédure, tableau, photo de croquis. Référence complète :
`docs/format-pmap.md` et `schemas/pmap-1.2.schema.json`.

**Sortie attendue : un seul objet JSON, rien avant, rien après.** Noms de propriétés et valeurs codées en anglais
minuscule, exactement comme ci-dessous ; textes (noms, libellés) dans la langue du processus. Toute propriété inconnue
fait refuser le fichier.

## 1. Squelette

```
{
  "format": "topogenia",                 obligatoire, toujours "topogenia"
  "version": "1.2",                      obligatoire, toujours "1.2"
  "generated_by": "...",                 facultatif, informatif
  "project": { "name": "...", ... },     obligatoire
  "map": { "lanes": [...], "steps": [...], "flows": [...] },   obligatoire, carte racine
  "uncertainties": ["..."]               facultatif, points à faire vérifier
}
```

## 2. Champs

**project**

| Champ | Oblig. | Valeur |
|---|---|---|
| `name` | oui | Nom du processus, 200 car. max |
| `code` | non | 3 à 6 lettres majuscules (`ACH`). En cas de doute, l'omettre : il est demandé à l'ouverture |
| `language` | non | `fr` ou `en` : langue des textes du fichier |
| `description` | non | 4 000 car. max : périmètre, déclencheur, résultat, volumes, contexte |
| `translations` | non | `{ "en": "Supplier invoice processing" }` |
| `workday_hours` | non | Journée de travail en heures, 1 à 24 (7 par défaut) : **seulement si la source la donne** (section 7 bis) |
| `fte_hours_per_month` | non | Heures d'un ETP par mois, 1 à 744 (151,67 par défaut) : **seulement si la source les donne** |

**map** (carte racine, et toute carte détaillée `sub_map`)

| Champ | Oblig. | Valeur |
|---|---|---|
| `lanes` | oui | 1 à 200 couloirs, **dans l'ordre de haut en bas** |
| `steps` | non | Étapes de cette carte |
| `flows` | non | Flèches entre étapes de cette carte |

**lane** (couloir = un acteur)

| Champ | Oblig. | Valeur |
|---|---|---|
| `id` | oui | Identifiant libre, unique dans tout le fichier |
| `name` | oui | 150 car. max : « Comptabilité fournisseurs », « ERP », « Acheteur » |
| `type` | non | `person` (défaut : une personne ou un rôle), `department` (un service), `system` (un logiciel qui agit seul, section 3) |
| `color` | non | `#RRGGBB` ; à omettre (alternance automatique) |
| `translations` | non | Nom dans l'autre langue |
| `headcount` | non | Effectif : entier, 0 à 100 000 personnes ; pas sur un couloir `system` ; **seulement s'il est donné** |

**step** (étape)

| Champ | Oblig. | Valeur |
|---|---|---|
| `id` | oui | Identifiant libre, unique dans tout le fichier |
| `lane` | oui | `id` d'un couloir **de la même carte** : l'acteur qui réalise (R) |
| `label` | oui | 150 car. max, verbe à l'infinitif + complément : « Saisir la facture » |
| `type` | non | Voir tableau ci-dessous ; défaut `task` |
| `column` | non | Entier 0 à 10 000, position de gauche à droite (section 4) |
| `row` | non | Entier 0 à 50, rangée dans le couloir ; défaut 0 |
| `automation` | non | `manual` (défaut), `in_progress` (en cours d'automatisation), `automated` |
| `raci` | non | Rôles des **autres** couloirs de la carte : `{ "dir": "A", "com": "I" }` (section 5) |
| `sub_map` | non | Carte détaillée de l'étape, même forme que `map` (section 6) |
| `translations` | non | Libellé dans l'autre langue |
| `sheet` | non | Fiche de l'étape (section 7) |
| `volume` | non | Sur un `start` seulement : `{ "quantity": 120, "period": "month" }`, quantité 0 à 1 000 000, période `day` (jour ouvré), `week`, `month`, `year` ; **seulement s'il est donné** |

| `type` | Sens | Quand l'utiliser |
|---|---|---|
| `task` | Tâche | Une action faite par l'acteur du couloir |
| `wait` | Attente | Un délai, une attente d'un tiers (« Attendre l'avoir ») |
| `start` | Début | Ce qui déclenche le processus (« Facture reçue », « Lundi matin », « Fin du mois ») |
| `end` | Fin | Le résultat obtenu (« Facture payée »), dans le couloir de celui qui l'obtient |
| `decision` | OU exclusif | Une question, une seule branche suivie ; libellé en question, flèches sortantes `oui` / `non` |
| `parallel` | ET | Ouvre des branches faites en même temps ; un second `parallel` les referme |
| `inclusive` | OU inclusif | Une ou plusieurs branches selon le cas ; un second `inclusive` les referme |

**flow** (flèche)

| Champ | Oblig. | Valeur |
|---|---|---|
| `source` | oui | `id` de l'étape de départ |
| `target` | oui | `id` de l'étape d'arrivée, **même carte** |
| `label` | non | 60 car. max : réponse d'une décision, condition (« > 10 k€ ») |
| `translations` | non | Libellé dans l'autre langue |
| `share` | non | Sur une flèche qui sort d'une `decision` ou d'un `inclusive` seulement : part des dossiers en %, 0 à 100 (2 décimales) ; **seulement si elle est donnée** |

## 3. Traduire un processus : méthode

La carte montre l'enchaînement du travail : qui fait quoi, dans quel ordre. Elle sert deux lectures à la fois : les
**gaspillages** (attentes, reprises, ressaisies, relances, passages d'un outil à l'autre) et l'**automatisation** (ce
que les logiciels font seuls, ce qui est en projet). Les règles ci-dessous servent ces deux lectures ; elles reprennent
celles des cartographies de flux de valeur et de BPMN.

1. **Acteurs → couloirs.** Un couloir répond à « qui fait le travail ? » : une personne ou un rôle (`person`), un
   service (`department`). Ordre de haut en bas : celui qui déclenche en haut, puis l'ordre d'intervention.
   - Un **participant extérieur** (client, fournisseur) qui déclenche le processus ou en reçoit le résultat a son
     couloir, en haut s'il déclenche.
   - Un **logiciel** n'a son couloir (`system`) que s'il agit **seul**, sans personne qui le lance : traitement
     planifié, calcul, envoi ou échange automatique entre deux logiciels ; ses étapes sont `automated`.
   - Quand une personne travaille **dans** un logiciel (« saisir dans l'ERP », « extraire de SAP », « reporter dans
     l'outil de reporting »), l'étape va dans le couloir de cette personne et le logiciel dans sa fiche (`tools`, et
     `tool` de chaque ligne du déroulé) : c'est là que se voient les ressaisies et les passages d'un outil à l'autre.
   - Pas de couloir pour un logiciel qui ne porterait qu'un stockage ou un état final (« ventes figées dans l'outil de
     reporting ») : ce résultat va chez celui qui l'obtient.
2. **Bornes.** Un `start` par déclencheur, nommé par l'événement (« Commande reçue », « Lundi matin », « Fin du
   mois ») ; un `end` par résultat distinct, dans le couloir de celui qui obtient ou porte ce résultat, pas dans celui
   du logiciel où il est enregistré. Plusieurs débuts et plusieurs fins sont admis.
3. **Actions → étapes `task`**, dans le couloir de celui qui agit. Une phrase contenant deux actions faites par deux
   acteurs = deux étapes. Garder le libellé court ; le détail va dans la fiche (section 7).
4. **Questions et contrôles qui orientent la suite → `decision`.** Chaque branche est une flèche étiquetée. **Une
   cadence n'est pas une décision** : pas de losange « Le mois est-il terminé ? » ni « Est-on lundi ? ». La fréquence de
   la fiche (`daily`, `weekly`, `month_end_close`...) dit quand une étape a lieu ; une partie du processus qui démarre
   sur le calendrier (la clôture du mois) commence par son propre `start` (« Fin du mois »).
5. **Tâches simultanées → `parallel`** (ouverture puis fermeture). Si elles sont faites par le même acteur, elles sont
   dans son couloir, même colonne, rangées 0, 1, 2...
6. **Délais, attentes, relances → `wait`.** Une boucle (retour en arrière) est une flèche vers une étape antérieure :
   elle est admise, et elle montre une reprise : la garder visible.
7. **Système et automatisation.** Une étape déjà faite par un logiciel seul : `automated`, dans son couloir `system` ;
   un projet d'automatisation en cours : `in_progress`, dans le couloir de la personne qui la fait encore.
8. **RACI** quand la source dit qui approuve, est consulté ou informé (section 5).
9. **Niveaux.** Les grandes étapes d'abord, le détail en dessous (section 6) : une étape décrite en plusieurs
   sous-actions faites par plusieurs acteurs → `sub_map` sur cette étape ; des sous-actions faites par le même acteur →
   le déroulé de sa fiche (`procedure`).
10. **Fiches.** Ce que la source dit d'une étape (but, outils, fréquence, durée, documents, attentes, irritants) va dans
    sa fiche (section 7).
11. **Doutes.** Tout ce qui est deviné, illisible ou contradictoire → une phrase dans `uncertainties`. Ne jamais
    inventer une étape, un acteur, une durée ou un document absents de la source.

## 4. Placement

- **Avec `column`** (recommandé dès que l'ordre est connu) : chaque étape a sa colonne. Les étapes qui se suivent ont
  des colonnes croissantes ; deux étapes simultanées ont la même colonne (dans deux couloirs, ou dans le même couloir
  sur deux `row`). Les trous sont admis.
- **Sans `column`** : l'application place chaque étape une colonne après la plus avancée de ses étapes précédentes, en
  suivant les flèches.
- Règle bloquante : dans une carte, `column` est donnée **pour toutes les étapes ou pour aucune**. Deux étapes ne
  partagent jamais la même case (couloir, colonne, rangée). Chaque carte détaillée choisit pour elle-même.

## 5. RACI

- R (réalise) = le couloir de l'étape (`lane`), jamais écrit ailleurs.
- `raci` ne cite que d'autres couloirs **de la même carte** : `A` approuve (un seul par étape), `C` est consulté, `I`
  est informé (`S` seulement si l'application est réglée en RASCI).
- Pas de `raci` sur `start`, `end`, `parallel`, `inclusive`. Facultatif partout : ne le remplir que si la source le dit.

## 6. Niveaux et cartes détaillées (`sub_map`)

Une carte se lit d'un coup d'œil : une dizaine d'étapes au plus. Un processus plus long se construit **par niveaux**,
comme des calques :

- La **carte racine** montre les **grandes étapes** du processus (5 à 10, une par phase, libellées par ce qu'elles
  font : « Recevoir et enregistrer la commande »), dans le couloir de l'acteur qui en répond, avec le début et les fins
  du processus.
- Chaque grande étape porte sa **carte détaillée** (`sub_map`) : les étapes qui la composent, dans les couloirs de ceux
  qui les font. Une carte détaillée encore longue se détaille à son tour (4 niveaux conseillés).
- Ce qu'un seul acteur fait en plusieurs sous-actions reste dans le déroulé de sa fiche (`procedure`), pas dans une
  carte détaillée.
- Un processus court (une dizaine d'étapes) reste sur une seule carte.

Règles de forme :

- Même forme que `map` : ses propres `lanes` (ids nouveaux, même si les noms reprennent ceux du parent), `steps`,
  `flows`.
- Pas de flèche entre une carte et sa carte détaillée : l'étape détaillée fait le lien.
- Interdite sur `start`, `end`, `parallel`, `inclusive`. 12 niveaux au plus (4 conseillés). Une carte détaillée sans
  étape est ignorée.

## 7. Fiches d'étape (`sheet`) : extraire d'un récit ce qui décrit une étape

Une fiche s'ouvre en **brouillon** : une personne la relira et la validera dans l'application. Chaque partie est
facultative : n'écrire que ce que la source dit. Pas de fiche sur `start`, `end`, `parallel`, `inclusive`.

| Champ | Ce qu'on y met | Indices dans un récit |
|---|---|---|
| `actor` | Qui fait l'étape, quand le couloir est un service (« l'assistante du service ») | « c'est l'assistante de la compta qui... » ; un rôle, jamais un nom de personne |
| `purpose` | À quoi sert l'étape | « pour que... », « afin de... », « sinon... » |
| `tools` | Outils employés | « dans l'ERP », « sur Excel », « par mail » |
| `frequency` | `daily`, `weekly`, `monthly`, `month_end_close` (à chaque clôture), `quarterly`, `yearly`, `occasional`, `per_case` (à chaque dossier) | « chaque matin », « tous les lundis », « à la clôture », « pour chaque demande » |
| `duration_minutes` | Durée d'une exécution, en minutes : le temps de travail | « ça prend un quart d'heure » → 15 ; « une demi-journée » → 240 |
| `lead_time` | Délai d'une exécution, attentes comprises : `{ "value": 2, "unit": "day" }`, unité `minute`, `hour` ou `day` (jour ouvré) | « il faut compter deux jours », « c'est traité dans l'heure » |
| `procedure` | Sous-actions de l'acteur, dans l'ordre : `{ "text", "tool", "nature" }` | « d'abord..., puis..., ensuite... » |
| `free_text` | Ce qui ne rentre pas ailleurs (exceptions, remarques) | « sauf en août », « ça dépend du client » |
| `outputs` | Documents que l'étape produit : `{ "id", "name", "location" }` | « il envoie le bon de commande » |
| `inputs` | Documents reçus de l'extérieur de la carte : `{ "name", "wait_hours" }` | « on attend le devis du fournisseur, deux jours en moyenne » → 48 |
| `uses` | Sorties d'**autres étapes de la même carte** que l'étape utilise : `{ "document", "wait_hours", "percent_complete_accurate", "pain_point", "lost_hours_per_month" }` | « avec le bon qu'a préparé l'acheteur ; il est faux une fois sur cinq » → 80 % |

Natures du déroulé : `extract` (extraire), `copy_paste` (copier-coller), `re_enter` (ressaisir), `format` (mettre en
forme), `calculate` (calculer), `check` (contrôler), `chase` (relancer), `file` (classer), `send` (envoyer), `wait`
(attendre).

- Une sortie qu'une autre étape utilise porte un `id` (unique dans tout le fichier) ; l'usage le cite dans `document`.
- `percent_complete_accurate` : part du document utilisable telle quelle (0 à 100, entier).
- `pain_point` : l'irritant que cause ce document (« arrive incomplet », « à ressaisir ») ; `lost_hours_per_month` :
  le temps perdu qu'il coûte, si la source le chiffre. L'irritant est rattaché à l'étape qui produit le document.
- Durées et volumes approximatifs (« environ », « à peu près ») : les écrire et le signaler dans `uncertainties`.
- Jamais de nom de personne, d'adresse électronique ni de donnée confidentielle dans une fiche : un rôle à la place.

## 7 bis. Flux de valeur : n'écrire un chiffre que s'il est donné

L'application calcule le flux de valeur (débits, charge, délais, chemin principal) à partir de quelques saisies : le
volume entrant d'un `start`, la part des dossiers des sorties d'une décision, l'effectif d'un couloir, le délai et la
fréquence des fiches, la journée de travail et les heures d'un ETP du projet. Le résultat doit pouvoir être audité :

- **N'écrire un volume, une part, un délai, un effectif ou un réglage que si le récit, un document ou la carte le
  donne.** Ne jamais en inventer, ne jamais « estimer » ce que la source ne dit pas : un chiffre absent est signalé par
  l'application comme à saisir, un chiffre inventé ne l'est pas.
- Un ordre de grandeur donné par la source (« une centaine par mois », « environ 15 % ») : l'écrire et le signaler
  dans `uncertainties`.
- Le volume va sur le `start` qui reçoit les dossiers ; « 6 par jour » → `{ "quantity": 6, "period": "day" }`.
- Les parts d'une décision : une sortie sans `share` prend le reste (100 moins les autres) ; ne pas l'écrire si la
  source ne donne que l'autre. Sur un `inclusive`, chaque sortie a sa part, indépendante des autres.
- Durée (`duration_minutes`) = temps de travail d'une exécution ; délai (`lead_time`) = temps écoulé, attentes
  comprises. « Ça prend 20 minutes mais il faut compter deux jours » → 20 et `{ "value": 2, "unit": "day" }`.
- Une étape faite pour chaque dossier : `frequency` = `per_case`.

## 8. Règles qui font refuser le fichier

Vérifier avant de rendre le fichier :

- [ ] `format` = `"topogenia"`, `version` = `"1.2"`, `project.name` et `map.lanes` présents.
- [ ] Aucune propriété hors de ce guide ; valeurs codées exactement comme écrites ici.
- [ ] Chaque `id` est unique dans tout le fichier (couloirs, étapes et documents, toutes cartes).
- [ ] Chaque `step.lane` et chaque clé de `raci` désigne un couloir de la **même** carte.
- [ ] Chaque `flow` relie deux étapes de la **même** carte ; pas de flèche d'une étape vers elle-même ; pas deux fois
      la même flèche dans le même sens.
- [ ] `column` partout ou nulle part dans une carte ; pas deux étapes dans la même case.
- [ ] Un seul `A` par étape ; pas de `R` ni d'autre rôle pour le couloir de l'étape.
- [ ] Pas de `sub_map`, de `raci` ni de `sheet` sur `start`, `end`, `parallel`, `inclusive`.
- [ ] Chaque `uses.document` est l'`id` d'une sortie d'une **autre** étape de la **même** carte, cité une fois par étape.
- [ ] `volume` seulement sur un `start`, avec ses deux membres ; `share` seulement sur une flèche qui sort d'une
      `decision` ou d'un `inclusive` ; `headcount` jamais sur un couloir `system` ; `lead_time` avec ses deux membres ;
      `workday_hours` de 1 à 24, `fte_hours_per_month` de 1 à 744.
- [ ] Longueurs : `name` 200 / 150, `label` 150, `flow.label` 60, `description` 4 000, textes de fiche 4 000, `free_text`
      20 000, 50 lignes de déroulé, `uncertainties` 500 car. ; pas de balise HTML dans les textes.
- [ ] 5 000 étapes, 200 couloirs par carte, 12 niveaux.

Ne bloquent pas (signalés à l'ouverture) : étape sans flèche, plusieurs débuts ou fins.

## 9. Exemple complet

Tous les éléments du format : décision, branches parallèles du même acteur (rangées), attente et boucle, système
automatisé, RACI, carte détaillée placée automatiquement, traductions, fiches tirées d'un récit, incertitudes.

```json
{
  "format": "topogenia",
  "version": "1.2",
  "generated_by": "Exemple du guide pour IA",
  "project": {
    "code": "FAC",
    "name": "Traitement d'une facture fournisseur",
    "language": "fr",
    "description": "De la réception de la facture à son paiement. Environ 400 factures par mois, saisies dans l'ERP.",
    "translations": { "en": "Supplier invoice processing" }
  },
  "map": {
    "lanes": [
      { "id": "com", "name": "Comptabilité fournisseurs", "type": "department", "translations": { "en": "Accounts payable" } },
      { "id": "erp", "name": "ERP", "type": "system" },
      { "id": "ach", "name": "Acheteur", "type": "person" },
      { "id": "dir", "name": "Direction financière", "type": "person" }
    ],
    "steps": [
      { "id": "debut", "lane": "com", "label": "Facture reçue", "type": "start", "column": 0 },
      {
        "id": "saisir", "lane": "com", "label": "Saisir la facture", "column": 1, "automation": "in_progress",
        "translations": { "en": "Enter the invoice" },
        "sheet": {
          "purpose": "Enregistrer la dette envers le fournisseur.",
          "tools": "ERP, messagerie",
          "frequency": "daily",
          "duration_minutes": 5,
          "procedure": [
            { "text": "Ouvrir la facture reçue par messagerie", "tool": "Messagerie" },
            { "text": "Ressaisir l'en-tête et les lignes", "tool": "ERP", "nature": "re_enter" }
          ],
          "inputs": [{ "name": "Facture du fournisseur" }],
          "outputs": [{ "id": "piece", "name": "Pièce comptable" }]
        }
      },
      { "id": "rapprocher", "lane": "erp", "label": "Rapprocher commande, réception et facture", "column": 2, "automation": "automated" },
      { "id": "conforme", "lane": "com", "label": "Facture conforme ?", "type": "decision", "column": 3 },
      { "id": "corriger", "lane": "ach", "label": "Faire corriger l'écart par le fournisseur", "column": 4, "raci": { "com": "C" } },
      { "id": "attendre", "lane": "ach", "label": "Attendre l'avoir du fournisseur", "type": "wait", "column": 5 },
      { "id": "et-debut", "lane": "com", "label": "En même temps", "type": "parallel", "column": 4 },
      {
        "id": "comptabiliser", "lane": "com", "label": "Comptabiliser la facture", "column": 5, "row": 0,
        "sub_map": {
          "lanes": [
            { "id": "d-com", "name": "Comptabilité fournisseurs", "type": "department" },
            { "id": "d-erp", "name": "ERP", "type": "system" }
          ],
          "steps": [
            { "id": "d-compte", "lane": "d-com", "label": "Choisir le compte de charge" },
            { "id": "d-centre", "lane": "d-com", "label": "Imputer le centre de coût" },
            { "id": "d-ecriture", "lane": "d-erp", "label": "Générer l'écriture comptable", "automation": "automated" }
          ],
          "flows": [
            { "source": "d-compte", "target": "d-centre" },
            { "source": "d-centre", "target": "d-ecriture" }
          ]
        }
      },
      {
        "id": "archiver", "lane": "com", "label": "Archiver la pièce", "column": 5, "row": 1,
        "sheet": {
          "uses": [{ "document": "piece", "wait_hours": 24, "pain_point": "La pièce arrive sans le bon de réception.", "lost_hours_per_month": 4 }]
        }
      },
      { "id": "et-fin", "lane": "com", "label": "Fin des tâches simultanées", "type": "parallel", "column": 6 },
      { "id": "montant", "lane": "com", "label": "Montant supérieur à 10 k€ ?", "type": "decision", "column": 7 },
      { "id": "approuver", "lane": "dir", "label": "Approuver le paiement", "column": 8, "raci": { "com": "I" } },
      { "id": "programmer", "lane": "erp", "label": "Programmer le paiement", "column": 9, "automation": "automated", "raci": { "dir": "A", "com": "I" } },
      { "id": "fin", "lane": "com", "label": "Facture payée", "type": "end", "column": 10 }
    ],
    "flows": [
      { "source": "debut", "target": "saisir" },
      { "source": "saisir", "target": "rapprocher" },
      { "source": "rapprocher", "target": "conforme" },
      { "source": "conforme", "target": "et-debut", "label": "oui", "translations": { "en": "yes" } },
      { "source": "conforme", "target": "corriger", "label": "non", "translations": { "en": "no" } },
      { "source": "corriger", "target": "attendre" },
      { "source": "attendre", "target": "rapprocher", "label": "avoir reçu" },
      { "source": "et-debut", "target": "comptabiliser" },
      { "source": "et-debut", "target": "archiver" },
      { "source": "comptabiliser", "target": "et-fin" },
      { "source": "archiver", "target": "et-fin" },
      { "source": "et-fin", "target": "montant" },
      { "source": "montant", "target": "approuver", "label": "oui" },
      { "source": "montant", "target": "programmer", "label": "non" },
      { "source": "approuver", "target": "programmer" },
      { "source": "programmer", "target": "fin" }
    ]
  },
  "uncertainties": [
    "Le seuil de 10 k€ pour l'approbation est à confirmer avec la direction financière.",
    "Durée de « Saisir la facture » : environ 5 minutes par facture, selon la source.",
    "L'outil d'archivage de « Archiver la pièce » n'est pas précisé dans la source."
  ]
}
```

## 10. D'un récit au fichier : exemple

Récit : « Chaque lundi, le magasinier compte le stock des consommables, ça lui prend une demi-heure avec la tablette.
Il envoie le relevé à l'acheteur, qui commande ce qui manque sur le portail du fournisseur. Le relevé arrive souvent
le mardi seulement, et il manque une ligne sur cinq, ce qui oblige l'acheteur à rappeler le magasin : il estime perdre
deux heures par mois. »

```json
{
  "format": "topogenia",
  "version": "1.2",
  "generated_by": "Exemple du guide pour IA, depuis un récit",
  "project": { "name": "Réapprovisionnement des consommables", "language": "fr" },
  "map": {
    "lanes": [
      { "id": "mag", "name": "Magasinier", "type": "person" },
      { "id": "ach", "name": "Acheteur", "type": "person" }
    ],
    "steps": [
      { "id": "debut", "lane": "mag", "label": "Lundi matin", "type": "start", "column": 0 },
      {
        "id": "compter", "lane": "mag", "label": "Compter le stock des consommables", "column": 1,
        "sheet": {
          "tools": "Tablette",
          "frequency": "weekly",
          "duration_minutes": 30,
          "outputs": [{ "id": "releve", "name": "Relevé du stock" }]
        }
      },
      {
        "id": "commander", "lane": "ach", "label": "Commander les consommables manquants", "column": 2,
        "sheet": {
          "tools": "Portail du fournisseur",
          "frequency": "weekly",
          "uses": [{ "document": "releve", "wait_hours": 24, "percent_complete_accurate": 80,
                     "pain_point": "Le relevé arrive le mardi et il manque une ligne sur cinq : l'acheteur rappelle le magasin.",
                     "lost_hours_per_month": 2 }]
        }
      },
      { "id": "fin", "lane": "ach", "label": "Commande passée", "type": "end", "column": 3 }
    ],
    "flows": [
      { "source": "debut", "target": "compter" },
      { "source": "compter", "target": "commander" },
      { "source": "commander", "target": "fin" }
    ]
  },
  "uncertainties": [
    "Le délai d'un jour du relevé est déduit de « le mardi seulement » : à confirmer."
  ]
}
```
