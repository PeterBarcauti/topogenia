<!-- format-tableur.md -->
<!-- How a table (.xlsx, .csv) opens as a process in 3.1: the columns read and their names, the types, the flows, the RACI, the delays, what is said, the bounds -->

# Ouvrir un tableau (.xlsx, .csv)

Version 3.1, ADR 092. Code : `web/src/features/project/files/tableRead.ts`, `tableImport.ts`.

« Ouvrir un fichier… » lit un classeur `.xlsx` ou un tableau `.csv` dans le navigateur. Il en fait le texte d'un
`.pmap` 1.2, qui suit la route d'ouverture (`POST /api/projects/import-pmap`, administrateur) avec ses
`uncertainties`. L'hôte le lit avec ses propres règles, comme un `.pmap`.

## Lecture du fichier

- **CSV**, selon la RFC 4180 :
  - le séparateur est celui que la première ligne emploie le plus (`;`, `,` ou tabulation) ;
  - les champs entre guillemets peuvent contenir le séparateur et des fins de ligne ; un guillemet doublé y vaut un
    guillemet ;
  - le fichier est lu en UTF-8 (marque d'ordre ignorée), sinon en Windows-1252, comme Excel l'enregistre.
- **XLSX** : `exceljs` n'est chargé qu'à la lecture. Chaque feuille est lue. Les formules donnent leur résultat, le
  texte riche son texte.
- **Refusés clairement** : `.xlsm` (macros) et `.xls` (ancien format) ; « Enregistrer en .xlsx ou en .csv ».
- **Avant toute règle** : chaque cellule est rendue acceptable par l'hôte (`plainText`, comme `TextRules`). Les balises
  HTML sont ôtées, un `<` devant une lettre est suivi d'une espace, et les caractères de contrôle sont ôtés (sauf fin de
  ligne et tabulation).

## Le tableau des étapes

C'est la première feuille dont une ligne d'en-tête nomme au moins une colonne d'étape (Étape) avec une colonne ID ou
une colonne Couloir. Les noms sont comparés sans casse, sans accents, sans espaces ni signes.

| Rôle | Noms reconnus |
|---|---|
| ID | ID, Identifiant, Code, Numéro, Step ID, Process Step ID, ID de l'étape (du processus) |
| Étape | Étape, Libellé, Description, Nom, Activité, Tâche, Step, Label, Name, Activity, Task, Process Step Description, Description de l'étape du processus |
| Couloir | Couloir, Fonction, Acteur, Rôle, Responsable, R, Réalise, Function, Swimlane, Lane, Actor, Responsible |
| Suivant | Suivant(s), Suivante(s), Étape(s) suivante(s), Next, Next Step (ID), ID de l'étape suivante |
| Libellé du lien | Libellé (de la) flèche, Libellé du lien, Étiquette (du connecteur), Condition, Connector Label, Flow Label |
| Type | Type, Forme, Type de forme, Shape, Shape Type |
| Temps | Temps, Délai, Durée, Lead Time, Time, Duration |
| A, S, C, I | A, Approuve, Accountable ; S, Soutient, Supportive ; C, Consulté, Consulted ; I, Informé, Informed |
| RACI | RACI, RASCI (« A : Direction ; C : RH, Juridique ») |
| Phase | Phase, Jalon, Milestone : sans équivalent sur la carte, laissée de côté et dite |

Le classeur que Topogenia exporte se relit tel quel : ses en-têtes sont aussi reconnus dans la langue de l'interface,
quelle que soit son écriture (cyrillique, grec, japonais, chinois). Un en-tête vide n'est jamais une colonne.

## Règles

- **Lignes** : une ligne sans libellé ni identifiant est ignorée. Une ligne sans libellé porte son identifiant ; un
  identifiant déjà pris fait laisser la ligne de côté.
- **Couloirs** : ils sont créés dans l'ordre où ils viennent. Une ligne sans couloir va dans « Processus ». Les couloirs
  nommés seulement par le RACI sont créés aussi.
- **Types** :
  - Tâche : tâche, étape, processus, activité, document, sous-processus.
  - Décision.
  - Parallèle : parallèle, ET, AND. Inclusif : inclusif, OU, OR.
  - Attente : attente, delay.
  - Début : début, départ, start, begin. Fin : fin, end, stop.
  - « Début/Fin » ou « Terminateur » (Visio) donne un début s'il n'a pas de flèche entrante, une fin sinon.
  - Un type inconnu donne une tâche, et c'est dit.
- **Flèches** : la colonne Suivant liste des identifiants séparés par des virgules, des points-virgules ou des fins de
  ligne. La colonne Libellé du lien donne les libellés dans le même ordre (« Oui,Non »). Un suivant inconnu est dit et
  laissé de côté. La feuille Liens du classeur de Topogenia (De, Vers, Libellé, dans la langue de l'interface) donne les flèches à la place de la colonne Suivant.
- **RACI** : une colonne par lettre (noms de couloirs séparés par des virgules) ou une colonne RACI (`A : Direction ;
  C : RH`, sur une ou plusieurs lignes). Un début, une fin ou un losange ET/OU inclusif ne porte ni rôle ni délai : ils
  sont laissés de côté et c'est dit. Au plus un A par étape (le premier est gardé, les autres sont dits). Seules les
  lettres de l'installation sont gardées, les autres dites : celles de la carte quand le dialogue est ouvert depuis le
  menu Fichier, `ACI` depuis l'accueil (F-95). Une partie de la colonne RACI qui ne se lit pas est dite.
- **Délais** : un nombre seul vaut des heures ; sinon « 2,5 h », « 30 min », « 1 j » et leurs équivalents anglais ; une
  durée d'Excel (`[h]:mm`) se lit en heures. Un délai illisible ou au-delà de 100 000 est dit et laissé de côté.

## Bornes

5 000 étapes, 200 couloirs (au-delà, refus clair). Les textes sont coupés à leur borne, et c'est dit : 150 caractères
pour un libellé d'étape (un identifiant servant de libellé compris) ou un nom de couloir, 60 pour un libellé de flèche,
200 pour le nom du projet. Au plus 50 points à vérifier sont écrits ; ce qu'ils citent du fichier est coupé à 60
caractères, et chacun à 500, la borne de l'hôte. Une ligne citée est celle du fichier, lignes vides comprises.
