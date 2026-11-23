<!-- 108-inserer-un-fichier-comme-etape-detaillee.md -->
<!-- A .pmap file inserted as a detailed step of the map open, or replacing the detail of a step (3.3, lot 4b, F-208): a copy, not a live link; built by the rules of the opening of a file; one operation, one Ctrl+Z -->

# 108 - Insérer un fichier comme étape détaillée

Statut : acceptée le 17/11/2026 (3.3, lot 4b). Prolonge les ADR 054 (`.pmap`) et 105 (`.pmap` 1.5). Demande du porteur
du projet du 15/11/2026 (F-208) : assembler une carte d'ensemble à partir de sous-processus décrits dans d'autres
projets, sur des postes locaux, sans serveur partagé.

## Contexte

BPMN distingue le sous-processus intégré (son contenu est copié dans le processus) de l'activité d'appel (un lien vers
un processus défini ailleurs, tenu à jour). Camunda, Bizagi et Signavio offrent les deux ; le lien suppose un dépôt
partagé. Visio copie la page d'un autre fichier, il ne la lie pas. Sans serveur, la pratique est la copie, avec un geste
pour la remplacer par une version plus récente.

## Décision

1. **Une copie, pas un lien vivant** (lecture acceptée par le porteur le 15/11/2026) : modifier le fichier ensuite ne
   change pas la carte ; « Remplacer le détail par un fichier » la met à jour.
2. **Trois entrées, pour l'animateur** : « Insérer un fichier comme étape détaillée… » (menu Fichier : couloir et
   place choisis, après une étape ou au début de la carte), « Insérer un fichier ici… » (menu du fond : la case visée),
   « Remplacer le détail par un fichier… » (menu d'une étape qui a un détail). Le fichier se choisit, se dépose ou se
   colle, comme à l'ouverture : `.pmap`, tableau, Mermaid ou SVG qui porte sa copie. Un fichier de plus de 10 Mo (ce
   que l'hôte lit) est refusé avant d'être lu dans la page, un paquet d'échange avant d'être envoyé : il s'importe comme
   un projet (audit de la copie 64).
3. **Un aperçu avant toute écriture** (`POST /api/maps/{id}/insert-pmap/preview`) : ce que le fichier tient, ses
   niveaux et la place que la carte laisse, les points à vérifier, les langues écartées, le libellé proposé (le nom du
   projet du fichier, dans la langue du projet si le fichier la donne).
4. **La construction de l'ouverture**, pas celle des propositions de l'assistant : le constructeur du `.pmap`
   (`PmapBuild`, hors du cœur fermé) passe par les règles du domaine et recopie tout, RACI, tracés des flèches,
   couleurs et effectifs des couloirs, résumés des cartes, traductions, commentaires de présentation, et les fiches en
   brouillon. Il numérote ses propres identifiants : un fichier copié du même projet s'insère à côté de ses étapes.
5. **L'étape, dans le flux** (retour d'essai du porteur du 17/11/2026 : la flèche passait au-dessus de l'étape
   insérée) : placée après une étape, ou au début de la carte (après son début quand elle s'ouvre sur un début, avant
   sa première étape sinon), elle l'est par la règle de « Insérer une étape après / avant » (`StepInsertion`) : la seule flèche de ce côté est coupée en deux, son libellé, sa part
   et ses côtés gardés du côté de son origine ; sans flèche unique de ce côté, seule la flèche vers la nouvelle étape
   est créée. C'est la pratique de Visio, Bizagi, Signavio et bpmn.io (une forme déposée sur un flux s'y insère). Un
   autre couloir choisi y déplace l'étape, ses flèches avec elle. **Sur une flèche** (second retour d'essai du même
   jour : une flèche sélectionnée était ignorée et l'étape partait au bout du processus, hors de la vue) : une flèche
   seule choisie sur la carte, ou le menu d'une flèche (« Insérer un fichier sur cette flèche… »), met l'étape sur
   cette flèche, comme une forme déposée sur un connecteur dans Visio, Bizagi ou draw.io ; c'est cette flèche qui est
   coupée, même une sortie d'une décision parmi d'autres, avec la même règle que « Insérer une étape après » son
   origine (colonne suivante, libellé, part, traductions et côtés gardés du côté de l'origine) ; dans un autre couloir
   choisi, l'étape y est créée d'emblée, à sa première rangée (audit de la copie 64 : créée dans le couloir de l'origine
   puis déplacée, elle y décalait les étapes pour rien, et pouvait être refusée à la dernière colonne). La règle est écrite
   hors du cœur fermé (`InsertPmapCommand.OnFlow`), par les primitives publiques du domaine (`StructureEdits`,
   `TranslationRules`) ; aucune n'est modifiée. Après l'insertion, la vue se centre sur la nouvelle étape. Posée sur une case vide par le menu du fond, elle
   n'est reliée à rien. **Jamais au sortir d'une fin ni à l'entrée d'un début** (relecture indépendante du 18/11/2026 :
   sans rien de choisi, l'étape partait après la dernière étape, souvent la fin, et une flèche sortait de la fin) : une
   fin n'est pas une étape après laquelle placer ; sans rien de choisi, l'étape va sur la seule flèche qui entre dans la
   dernière fin, sinon après la dernière étape qui n'est pas une fin. Type tâche, langue du projet, libellé saisi ; le libellé proposé garde les traductions du nom
   du fichier, et il est coupé à 150 caractères avant un caractère, jamais entre les deux moitiés d'un émoji. Sa carte détaillée prend son libellé et ses traductions, comme « Détailler ».
6. **Le remplacement** : l'ancien détail part à la corbeille avec toute sa descendance, dans le lot de l'opération
   (comme « Supprimer le détail »), puis le fichier est construit à sa place. Un début, une fin, un parallèle ou un
   inclusif refuse (`ETAPE_NON_DETAILLABLE`).
7. **Tout ou rien, un seul Ctrl+Z** : une opération du journal à elle, `inserer_fichier` ; un refus d'une règle en
   cours de construction refuse tout (`FICHIER_INVALIDE` avec sa place dans le fichier), l'étape comprise. La version
   de structure lue par l'aperçu est vérifiée (`CONFLIT_VERSION`).
8. **Les fiches sur option** (case cochée par défaut) : elles arrivent en brouillon, jamais validées, avec leurs
   documents et leurs irritants.
9. **Les langues** : une traduction ou une ligne de commentaire dans une langue non configurée est écartée et nommée
   dans l'aperçu ; un fichier dont la langue d'origine n'est pas configurée est refusé, comme à l'ouverture. Un
   fichier d'une autre langue configurée que celle du projet garde sa langue : ses libellés, son résumé, ses fiches,
   ses documents et ses irritants ont pour langue d'origine celle du fichier (les règles du cœur écrivent la langue du
   projet, `PmapBuild` remet celle du fichier, `InFileLanguage`), et la traduction que le fichier donne de son résumé
   dans la langue du projet reste une traduction (audit de la copie 64 : elle était perdue, et le changement ultérieur
   de la langue d'origine du projet refusé).
10. **La profondeur** : profondeur de la carte et niveaux du fichier tiennent dans les 12 niveaux d'un `.pmap`, pour
    que le projet assemblé se copie encore entier ; au-delà, l'aperçu le dit et l'insertion refuse
    (`PMAP_PROFONDEUR`, les niveaux restants).
11. **Aucun refus selon la classification** (décision du porteur du 16/11/2026).
12. **La trace** : l'audit garde chaque copie (`map.file_inserted` : mode, étape, carte, nombres), comme l'ouverture
    d'un fichier ; l'historique de la carte nomme l'opération.

## Écarté

- **Le lien vivant** : il suppose un dépôt partagé que les postes locaux n'ont pas.
- **Les propositions de l'assistant (`ProposalPlan`)** : leur état voulu ne porte ni RACI, ni tracés, ni couleurs, ni
  résumés, et leurs identifiants sont les codes du projet (un fichier copié du même projet aurait été lu « déplacé »).
- **La trace du fichier d'origine sur l'étape** : écartée par le porteur le 17/11/2026 (« ça surchargerait le
  graphique ») ; l'audit suffit.

## Conséquences

- Trois routes (`insert-pmap/preview`, `insert-pmap`, `steps/{id}/replace-pmap`), un type d'opération, un événement
  d'audit ; aucune migration.
- Le lecteur du `.pmap` gagne une lecture souple des langues, réservée à l'insertion.

## Vérification

- `PmapInsertionTests` (deux bases) : flèche coupée en deux après une étape (libellé du côté de l'origine), avant la
  première, autre couloir, un Ctrl+Z qui la rejoint, position refusée ; sur une sortie d'une décision, seule cette
  sortie coupée, l'autre gardée, Ctrl+Z, flèche inconnue ou d'une autre carte refusée ; aperçu sans écriture, copie fidèle (RACI, couleur, traductions, fiche en
  brouillon, flèche, deuxième niveau, commentaires dans les langues configurées), Ctrl+Z ; sans les fiches et sous un
  libellé saisi ; une copie du même projet insérée deux fois ; remplacement et son Ctrl+Z, audit ; étape sans contenu
  et fichier trop profond refusés ; refus d'une règle en cours de construction sans rien écrire ; aperçu périmé,
  lecteur, langue du fichier non configurée.
- `PmapInsertionReadTests` : langues écartées et nommées, refus de la langue du fichier, borne égale à celle du format.
- `insertPmap.test.tsx` : aperçu, place et libellé envoyés avec la version lue, fichier trop profond ou refusé, case
  du menu du fond, flèche choisie sur la carte (une sortie d'une décision, pas la dernière étape), menu d'une flèche puis
  autre place choisie, remplacement sans les fiches, lecteur.
- Témoins de mutation vus rouges : la borne de profondeur, l'écriture des commentaires, la suppression de l'ancien
  détail, la coupure de la flèche, le changement de couloir, la coupure de la flèche choisie, la flèche sélectionnée
  lue par le dialogue.
- Audit de la copie 64, chaque correction vue rouge sans elle : `PmapInsertionTests` (fichier anglais dans un projet
  français : résumé, documents, irritants en anglais, traduction française du résumé gardée ; libellé coupé avant un
  émoji ; flèche vers un autre couloir sans rien décaler) ; `insertPmap.test.tsx` (fichier trop gros, paquet).
