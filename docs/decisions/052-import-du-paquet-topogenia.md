<!-- 052-import-du-paquet-topogenia.md -->
<!-- Import du paquet d'échange : routes, garde-fous, nouveau projet, remplacement (code de l'ancien projet), personnes, remontée ouverte -->

# 052 - Import du paquet `.topogenia`

Statut : acceptée (date 25/10/2026) ; défauts revus le 25/10/2026 : AR-114 (code dérivé gardé), AR-115 (équipe gardée au
remplacement), AR-116 (droits de l'aperçu gardés)

## Contexte

La spec 7.3 fixe l'aperçu, les deux façons d'importer (nouveau projet, remplacement), le tout ou rien, l'importateur
animateur et la reprise des membres en mode Serveur ; la 5.4 fixe les garde-fous (100 Mo avant lecture, 500 Mo
décompressés, tailles par entrée, nombre d'entrées, chemins, empreintes, schéma, tout cela avant toute écriture). Elle
laisse ouverts :

1. **Le code de l'ancien projet lors d'un remplacement.** « Le projet actuel part à la corbeille, son code y est suffixé
   pour libérer l'original ». Or `project.code` tient en 3 à 6 lettres majuscules (`ProjectCode`, colonne de 6, index
   `UQ_project_code`), et les codes lisibles des étapes, couloirs et irritants (`VTE-E001`) sont uniques sur toute la
   base, supprimés compris (`UQ_step_code`, `UQ_lane_code`, `UQ_pain_point_code`) : le paquet, qui garde le code `VTE`,
   apporte les mêmes codes lisibles que l'ancien projet.
2. Le transport du paquet (fichier binaire, alors que toutes les autres routes lisent du JSON, A-4-54).
3. Le sort des personnes : auteurs, membres, acteur d'un couloir, assigné d'une étape, avec ou sans les personnes.
4. La remise en base d'une remontée ouverte, exportée sans son lot de suppression ni les cases d'origine (ADR 051).
5. Ce que veut dire « validation complète » d'un paquet reçu de l'extérieur.

## Décision

### 1. Code de l'ancien projet : un code libre dérivé, et ses codes lisibles réécrits

Au remplacement, l'ancien projet prend un **code libre dérivé du sien** (`ProjectCodeRules.TrashCode`) : ses cinq
premières lettres suivies d'une lettre de A à Z (`VTE` donne `VTEA`, `ACHATS` donne `ACHATA`), puis, si les 26 sont
pris, ses quatre premières lettres suivies de deux lettres de AA à ZZ ; le premier code qui n'est ni le code d'origine
ni celui d'un projet vivant ou dans la corbeille. Tous ses codes lisibles, supprimés compris, sont réécrits avec ce
préfixe (`VTE-E001` donne `VTEA-E001`) par la même règle que l'import en nouveau projet dont le code est pris
(`ReadableCode.WithProject`). Il part ensuite à la corbeille comme par `TrashRules.MoveToTrash`, dans la même unité
de travail que l'écriture du paquet.

- C'est le « suffixe » de la spec dans la seule forme que le code admet : un code reste de 3 à 6 lettres partout
  (saisie, préfixe des codes lisibles, saisie `e17` de la barre de commande, vues des rapports). Les codes lisibles d'un
  projet portent toujours le code de leur projet : un préfixe libre est donc libre aussi pour les codes lisibles.
- **Restauration** : le projet revient sous son code dérivé, sans conflit avec le projet importé ; la corbeille le
  montre sous ce code, avec son nom et sa date de suppression ; la réponse du remplacement donne ce code.
- Le JSON figé des versions validées des fiches et les textes libres ne sont pas réécrits : ce sont des photos, qui
  peuvent citer l'ancien code.
- Options écartées : (a) élargir `project.code` et le motif des codes lisibles pour un vrai suffixe (`VTE~1`) : une
  migration sur les deux moteurs, et un code de projet que plus aucune règle de saisie ne reconnaît, pour un projet
  qu'on ne verra que dans la corbeille ; (b) réécrire les codes du paquet plutôt que ceux de l'ancien projet : la spec
  veut que le paquet prenne la place « avec le même code ».

### 2. Routes et transport

- `POST /api/projects/import/preview` et `POST /api/projects/import` : aperçu et import en nouveau projet,
  administrateur (comme la création et l'import `.pmap`, annexe A 1).
- `POST /api/projects/{id}/import/preview` et `POST /api/projects/{id}/import` : aperçu et remplacement, animateur du
  projet visé (S-7.3-06 ; l'administrateur en animateur). Le remplacement lit `If-Match`, la version du projet remplacé,
  comme sa mise à la corbeille (spec 5.3 : exigé en mode Serveur), et verrouille ce projet avant de le lire
  (`UserRequest.LocksTarget`) : aucune commande du projet ne s'intercale, et aucune étape n'y naît sous l'ancien préfixe.
- L'aperçu a le droit de l'import qu'il prépare : un aperçu ouvert à tous dirait à un inconnu quels codes de projet
  existent, corbeille comprise.
- Le corps est le paquet lui-même, `application/octet-stream`, 100 Mo au plus refusés avant lecture (413
  `REQUETE_TROP_GROSSE`, `BodySizeLimit`) ; les options sont dans la chaîne de requête, toutes exigées (spec 5.4 : jamais
  un défaut silencieux) : `with_members` (`true` ou `false`) et, pour un nouveau projet, `code` facultatif (le code
  choisi quand celui du paquet est pris). Pas de formulaire multipart : rien à analyser d'autre que le ZIP.
- L'aperçu reste imposé par l'interface (AR-38) : chaque import refait toutes ses validations avant d'écrire.

### 3. Garde-fous, dans cet ordre, tous avant la première écriture

1. Taille du corps : 100 Mo (hôte, avant lecture).
2. Archive ZIP lisible (une archive tronquée ou abîmée est refusée) ; **8 entrées au plus**, chacune d'un nom connu (`PackageFormat.Entries` et le manifeste), sans
   doublon ; un chemin avec `..`, `/` ou `\` en tête, une lettre de lecteur ou un nom inconnu est refusé.
3. Manifeste lu en premier, **1 Mo au plus** ; `format` égal à `topogenia-package`, `formatVersion` égal à la version de
   l'application (un paquet plus récent est refusé `PAQUET_TROP_RECENT` avec les deux versions ; la chaîne de montée de
   version arrive en P5-T03), puis validé contre le schéma.
4. Chaque entrée annoncée par le manifeste existe, et chaque entrée de l'archive est annoncée ; `members.json` présent
   si et seulement si le paquet a les personnes.
5. Taille décompressée lue en flux et comptée : jamais plus que la taille annoncée par le manifeste, **200 Mo au plus
   par entrée**, **500 Mo au plus au total** ; la lecture s'arrête au premier octet de trop (bombe de décompression). Les
   limites sont un paramètre de l'importeur (`PackageLimits`, valeurs de la spec par défaut) : les tests atteignent une
   limite sans écrire des centaines de mégaoctets.
6. SHA-256 de chaque entrée égal à celui du manifeste, et taille lue égale à la taille annoncée.
7. Chaque entrée est du JSON (raison `json` ; une clé écrite deux fois dans un objet aussi, la seconde se perdrait) et suit
   sa définition du schéma (`PackageSchema` ; un nombre hors de la plage décimale est au-delà de toute borne, raison `schema`).
8. Cohérence du contenu (section 5 ci-dessous) : l'aperçu la vérifie aussi, par une reconstruction d'essai. Un login
   qu'aucun utilisateur ne peut avoir (espace autour, caractère de contrôle) est refusé dès la lecture, raison `values`,
   avant toute recherche dans la base.
9. **Valeurs** (porte P5, round 1) : chaque enregistrement reconstruit passe par la règle de sa saisie (`ImportedRecords`),
   comme le `.pmap` passe par elles (A-2.18-08) : un paquet ne peut rien écrire que l'interface n'aurait pu écrire. Textes
   rognés, sans balise ni caractère de contrôle, dans la longueur de leur champ comptée en unités UTF-16 comme les colonnes
   (le schéma compte en points de code : un nom de 100 émojis lui convient et dépasserait les 150 de la colonne, refusé
   par SQL Server et gardé par SQLite) ; couleur `#RRGGBB` en majuscules ; adresse de document qu'`DocumentLocation.Parse`
   rend telle quelle et de son type (`javascript:` refusé) ; nombres dans leurs bornes et déjà arrondis (durée, attente,
   %C&A, temps perdu) ; déroulé de 50 opérations au plus ; contenu d'une version validée égal à la forme figée
   (`SheetJson.Freeze`) d'un contenu dont chaque valeur passe sa règle, avec la fréquence et la durée de la version ;
   traduction du champ et de la longueur de son objet ; commentaire d'historique de 1 000 caractères au plus ; irritant
   cité par un usage au plus (index unique de la base). Une valeur en faute refuse le paquet, raison `values`, avec le nom
   des champs (`lane.name`, `sheet.versions.content`...). Une clé portée deux fois (fiche d'une étape, position du
   déroulé, numéro de version, usage, traduction, rôle RACI, étape, flèche ou couloir d'une remontée) est refusée raison
   `reference` : reconstruite, la seconde remplacerait la première sans rien dire.

Tout refus est `PAQUET_INVALIDE` (400) avec, dans le détail, la raison (`archive`, `entries`, `path`, `size`, `hash`,
`format`, `json`, `schema`, `reference`, `values`, `structure`), l'entrée en cause et, pour le schéma, les premiers chemins en
faute (pour une référence, le code qui ne désigne rien) ; jamais le contenu du paquet. Un paquet d'une version de format
plus récente est refusé `PAQUET_TROP_RECENT` (400) avec sa version et celle que lit l'application.

### 4. Personnes

- **Auteurs** (`createdBy`, `updatedBy`, `validatedBy`, `changedBy`) : retrouvés par login ; un login inconnu de la
  base crée l'utilisateur dans la même transaction, comme un membre invité (`UserRules.NewUser`, nom de l'annuaire, sinon
  le nom affiché du paquet pour un membre, sinon la partie compte du login). Seuls les logins que les enregistrements
  reconstruits nomment créent un utilisateur : sans les membres, un login cité par `members.json` seul n'en crée aucun.
  Un auteur de création absent (paquet sans les personnes) est l'importateur, les colonnes d'auteur de création n'étant
  jamais vides ; un auteur facultatif absent (dernier modificateur, traducteur) reste vide ; les dates restent celles du
  paquet.
- **Membres** : repris seulement si `with_members=true` et si le paquet les porte, dans les deux modes. Au remplacement,
  un import qui ne reprend pas les membres du paquet garde ceux du projet remplacé, avec leurs rôles, marques et ordre
  (AR-115, `ImportedProjects.KeepMembers`) : l'équipe garde son accès. Le projet qui prend la place garde aussi la
  classification du projet remplacé (AR-117, `ImportedProjects.KeepClassification`) : choisie à la création par un
  administrateur, aucune requête ne la change ensuite, et l'animateur qui remplace son projet ne la baisse pas avec le
  paquet qu'il apporte ; un import en nouveau projet, réservé à l'administrateur, prend celle du paquet. En mode
  Serveur, seulement les logins de la forme `DOMAINE\compte` (le schéma n'en admet pas d'autre, si bien que la condition
  de la spec est tenue deux fois). L'importateur devient animateur : ajouté en tête s'il n'est pas membre, son rôle porté
  à animateur s'il l'est.
- **Acteur d'un couloir, assigné d'une étape** : gardés seulement s'ils sont membres du projet importé (la règle de
  `MemberRules.Replace` : une personne qui n'est pas membre ne tient ni couloir ni étape) ; l'aperçu les compte.

### 5. Nouveau projet, remontée ouverte, cohérence

- **Nouveaux identifiants** pour tout ; codes et compteurs repris du paquet. Si le code du projet est pris (vivant ou
  dans la corbeille), l'import est refusé `PROJET_CODE_EXISTANT` ou `PROJET_CODE_CORBEILLE` tant que `code` ne donne pas
  un code libre ; le code choisi réécrit le préfixe de tous les codes lisibles.
- **Remontée ouverte** (ADR 051) : l'étape dissoute revient supprimée dans un lot de suppression neuf, daté de la
  création de la remontée et à son auteur ; elle est posée sur le premier couloir de sa carte, en première colonne et
  première rangée (une étape supprimée n'occupe aucune case : l'index des cases ne vaut que pour les vivantes, et rien ne
  relit sa case) ; la
  case d'origine de chaque étape du cadre est sa case actuelle ; les flèches et couloirs créés par la remontée sont
  reliés à elle.
- **Cohérence**, refusée `PAQUET_INVALIDE` avant écriture : codes du projet, des étapes, couloirs et irritants préfixés
  par le code du paquet et uniques ; numéros jamais au-delà des compteurs ; toute référence par code résolue (couloir
  d'une étape sur sa carte, étape parente d'une sous-carte, extrémités d'une flèche, couloir d'un RACI, irritant d'un
  usage, objet d'une traduction) ; une seule carte racine, toute sous-carte rattachée à la racine ; puis l'espace de
  travail construit passe `StructureInvariants` (une étape par case, une flèche par couple, un seul A, une seule remontée
  ouverte par carte), puis aucune sous-carte vivante sur une étape sans contenu. Un RACI, une fiche ou un document sur une
  étape sans contenu, un RACI sur le couloir de son étape ou sur un couloir d'une autre carte sont acceptés : ce sont les
  rôles et contenus masqués que la 1.x gardait aussi (un changement de type ou un déplacement les masque, annexe A 6), et
  le paquet les porte (ADR 051). Le refus `PAQUET_INVALIDE`, raison `structure`, nomme les règles en faute, sans
  identifiant. La langue d'origine du projet doit être configurée ici (`LANGUE_INVALIDE`), comme celle de chaque carte,
  couloir, étape, fiche, document et irritant (`values`, champ `<objet>.sourceLanguage`) ; une traduction dans la langue
  d'origine de son objet (A-2.14-05) ou dans une langue non configurée ici est ignorée, comme à l'import d'un classeur de
  traductions, et les autres sont gardées (AR-119). Un texte qui ne se lit pas en UTF-16 (moitié d'une paire de
  substitution échappée), dans une valeur comme dans le nom d'une propriété, est refusé avec la raison `json` dès la
  lecture de son entrée.
- **Réponse** de l'aperçu : nombres de cartes, couloirs, étapes vivantes, flèches, fiches, documents, irritants et
  traductions (celles que l'import écrit, les traductions ignorées par AR-119 en moins) ; avec ou sans personnes ; code
  du paquet, code que prendrait le projet et s'il est libre (`free`), pris
  (`taken`) ou dans la corbeille (`in_trash`) ; logins du paquet inconnus de la base ; nombre de membres du projet
  importé ; ce que l'import réglerait (importateur ajouté, acteurs et assignés retirés) ; au remplacement, le code que
  prendrait le projet remplacé. L'import rend l'identifiant du projet, son code,
  sa carte racine et, au remplacement, le code donné à l'ancien projet.
- **Audit** : `project.imported` (identifiant du projet, nouveau ou remplacement, avec ou sans membres) et, au
  remplacement, `project.trashed` de l'ancien projet, dans la même unité.

## Conséquences

- Aucune migration de schéma ; les projets remplacés se reconnaissent dans la corbeille à leur nom et à leur date.
- L'aller-retour exact (7.4) tient avec les personnes et avec l'importateur déjà animateur dans le paquet : sinon son
  ajout change `members.json`, ce qui est voulu par la spec.
- `IProjectReader` gagne la liste de tous les codes en usage (le code dérivé en a besoin).

## Vérification

- Domaine : `ProjectCodeRulesTests` (code dérivé : dérivation, codes pris, code de six lettres, 702 codes valides puis
  refus), `ReadableCodeTests` (`WithProject`), `ProjectReplacementTests` (réécriture de tous les codes lisibles,
  supprimés compris, puis corbeille), `ImportedProjectsTests` (importateur animateur, détenteurs libérés, règles).
- Échange : `PackageReaderTests` (chaque garde-fou de la section 3), `PackageImportTests` (aller-retour exact avec et
  sans les personnes, nouveaux identifiants, préfixe réécrit, personnes, remontée ouverte, références).
- Application : `Exchange.PackageImportTests` (droits, code pris, utilisateurs créés, aperçu sans écriture, tout ou rien,
  remplacement, `If-Match`, verrou, audit).
- API, sur les deux bases : `Exchange.ImportRoutesTests` (aller-retour par l'API, corbeille et restauration, nouveau
  projet, aperçu, paquet invalide, option exigée, 413 avant lecture) ; matrice des droits et liste des routes.
