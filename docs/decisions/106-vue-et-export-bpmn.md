<!-- 106-vue-et-export-bpmn.md -->
<!-- The view and the export in BPMN 2.0 (3.3, lot 2): one compiler in the interface from the .pmap 1.5 to the semantics and the drawing of the OMG, a view read only that replaces the canvas, a .bpmn valid against the official schemas -->

# 106 - Vue et export BPMN 2.0

Statut : acceptée le 17/11/2026 (3.3, lot 2). Revient sur le point « BPMN écarté » de l'ADR 092, prolonge les ADR 066
(exports de la carte), 091 (tracé) et 105 (`.pmap` 1.5). Demande du porteur du projet du 16/11/2026 : une bascule
visuelle de la carte en BPMN 2.0 et un export `.bpmn` lisible par les modeleurs du marché ; décisions du même jour :
le `.pmap` en est la seule source, la classification d'un projet ne refuse ni la vue ni l'export (« la confidentialité
limite l'export sur internet, elle ne doit pas interdire de travailler efficacement »).

## Contexte

- Les équipes qui reçoivent une carte travaillent souvent dans un modeleur BPMN (Camunda, Signavio, Bizagi, ARIS).
  Topogenia ne leur donnait qu'une image ou un classeur.
- Une carte et un diagramme BPMN se ressemblent : couloirs, étapes typées, branchements, attentes. Ils diffèrent sur
  ce que BPMN n'a pas (la fiche, le RACI complet, le flux de valeur, les irritants) et sur ce qu'il exige (une forme par
  type d'élément, un fichier XML validé par les schémas de l'OMG).
- Le `.pmap` 1.5 décrit une carte entière hors de la base, commentaires et glossaire compris (ADR 105).

## Décision

1. **Un compilateur, dans l'interface** (`web/src/features/map/bpmn/model/`), du `.pmap` vers la sémantique et le
   dessin BPMN 2.0.2 (`bpmnCompile.ts`, `bpmnMapping.ts`, `bpmnLayout.ts`, `bpmnXml.ts`). La vue et l'export lisent le
   même modèle en mémoire (`bpmnModel.ts`) ; rien n'est calculé deux fois.
2. **Une seule source : la copie de la carte**, lue par la route existante `GET /api/maps/{id}/pmap` avec son usage
   (`purpose`) : `view` pour la vue, sans événement du journal ; `bpmn` pour l'export, journalisé comme un export
   (`project.exported`, `format=bpmn`) ; `copy` (par défaut) pour la copie d'une image. Jamais la vue de la carte du
   canevas, jamais une route propre au BPMN.
3. **Correspondance des éléments** (spécification du lot, 4.1) :
   - début, fin : `startEvent`, `endEvent` ; attente : `intermediateCatchEvent` avec un minuteur (durée ISO 8601 quand
     la fiche la donne), ou `startEvent` minuté quand rien n'y entre ;
   - décision, branches parallèles, décision inclusive : passerelles exclusive, parallèle, inclusive, leur sens tiré de
     leurs flèches ; une flèche qui sort d'une décision porte sa condition (son libellé et sa part) ;
   - tâche : `serviceTask` si elle est automatisée ou dans un couloir de logiciel, `userTask` si sa fiche nomme un outil,
     `manualTask` sinon ; étape détaillée : `subProcess` replié sous son [+], avec son contenu sur option à l'export ;
   - couloir : `lane` d'un unique `participant`, et une ressource ; le couloir de l'étape en est l'exécutant (sauf un
     couloir de logiciel), les autres lettres du RACI des rôles nommés « Consulté (C) »… ;
   - documents : un objet de données (une réserve pour un logiciel) par sortie et par entrée externe, une référence près
     de l'activité qui le produit, le reçoit ou l'utilise ; une passerelle ou un événement ne lit aucune donnée en BPMN :
     son usage reste écrit dans sa documentation ;
   - irritants ouverts : une annotation par étape qui produit le document en cause, avec code, texte et heures perdues ;
   - tout le reste de la fiche, le commentaire de présentation, le flux de valeur de l'étape : sa `documentation`, en
     texte, dans la langue montrée. La documentation de la collaboration porte le projet, la carte, la classification,
     la langue, la journée de travail. L'emplacement des documents (chemins, liens) n'est jamais écrit.
4. **Dessin : la géométrie de la carte** (`gridOf`, `layoutFlows`) : mêmes colonnes, rangées et tracés, pour que la
   bascule ne déplace rien. Bandeau du `participant` et en-tête des couloirs à gauche (30 px chacun), comme les
   modeleurs ; formes de la taille BPMN centrées dans la case de l'étape ; une boucle courbe devient une ligne brisée
   (BPMNDI n'a pas de courbe). Les détails (documents, notes) se posent dans une bande sous chaque rangée (80 px),
   puis la bande est coupée dans chaque rangée là où rien n'est dessiné sur toute la largeur (`bpmnCompact.ts`, essai
   du 17/11) : une rangée sans document ni note reprend à peu près la hauteur de la carte, et ce qui est sous la coupe
   remonte. Seuls des segments verticaux raccourcissent, avec 16 px gardés entre deux choses dessinées.
5. **La vue** (`bpmn/view/`) remplace le canevas, lecture seule : Alt+B ou « BPMN 2.0 » de la zone Affichage, pour tout
   membre. Elle s'ouvre au zoom de la carte, l'étape la plus proche du milieu de l'écran au même point, et le retour
   fait de même (`state/viewHandover.ts`, essai du 17/11 : cadrer tout le schéma à l'ouverture le rendait minuscule sur
   une grande carte). À l'ouverture d'une carte, elle part de son début, au zoom 1. « Tout voir » cadre le schéma
   entier. On la déplace et on la zoome ; un clic ou Entrée sur une étape ouvre sa fiche en lecture seule ; un double
   clic sur une étape détaillée plonge dans son détail, la vue gardée. Sa barre dit « Schéma BPMN 2.0 · lecture seule »,
   porte l'interrupteur « Détails sur le schéma » (une préférence de la carte, `app.bpmnShowDetails`, oui par défaut),
   l'export et le retour à la carte. Échap n'en sort pas : il ferme d'abord la fiche ou la boîte ouverte, et une seule
   touche ne doit pas fermer deux choses.
6. **L'export** (menu Fichier, « Exporter en BPMN 2.0 (.bpmn)… », et bouton de la vue) : une boîte avec deux cases,
   « Inclure les sous-processus détaillés » (cochée si la carte en a, chacun sur son propre plan) et « Dessiner les
   documents et les irritants » (comme la vue). Le fichier est nommé par la règle des exports (`CODE_Nom_date.bpmn`),
   écrit dans la langue montrée.
7. **Aucun refus lié à la classification**, ni pour la vue ni pour l'export : la classification est écrite dans la
   documentation du fichier, comme sur les autres exports.

## Écarté

- **BPSim** (simulation) : les grandeurs du flux de valeur restent dans la documentation ; aucun modeleur visé ne les
  relit sans extension payante.
- **`bpmn-js` dans le code livré** : lourd, et la vue n'a besoin que de dessiner des formes connues. `bpmn-moddle` et
  `xmllint-wasm` restent des dépendances d'essai (MIT).
- **Un export côté hôte** : il aurait doublé la lecture des fiches, des traductions et de la mise en page.
- **Une vue modifiable** : modifier en BPMN obligerait à relire un BPMN, ce que la 3.3 ne fait pas (import reporté).
- **Une couleur par couloir lue du fichier** : BPMN 2.0.2 n'en a pas ; la vue teinte ses couloirs par leur rang.

## Conséquences

- Le `.bpmn` s'ouvre dans les modeleurs du marché ; ce qu'ils n'affichent pas reste lisible dans la documentation de
  chaque élément.
- Une vue de plus dans le ruban (zone Affichage) et une entrée de plus dans le menu Fichier, ouvertes à tout lecteur.
- Le `.pmap` devient l'entrée de deux lectures ; toute évolution du format (1.6…) se vérifie aussi par les essais BPMN.
- La règle « aucune adresse externe dans le code livré » admet les espaces de noms des spécifications de l'OMG
  (`www.omg.org/spec/…`), comme ceux du W3C : ce sont des noms qu'aucun lecteur ne va chercher ; une autre adresse de
  l'OMG reste refusée (`DetectorSamplesTests`). La valeur `format` du `.pmap` et le nom de l'exportateur viennent du nom
  du produit (`PMAP_FORMAT`, `PRODUCT_NAME`), jamais écrits en dur.

## Révision : audit externe de la copie 64 (18/11/2026)

1. **Lecture seule pour de bon.** La fiche de la vue reçoit une fiche sans aucun droit (`useSheetState`,
   `readOnlyOf`) : ni responsable, ni question de l'animateur, ni document, ni irritant à signaler, ni indicateur
   d'enregistrement ; une fiche en brouillon montre son brouillon, dit « Brouillon en cours, pas encore validé. », jamais
   « Aucune fiche validée » ni la date d'une version validée avant lui (`ReadView`). Le ruban ne garde que Niveau et
   Affichage (`ribbonGroups(context, bpmn)`) ; le clavier, ce qui ne change rien : Alt+Haut, Ctrl+F1, Ctrl+I, F5, Maj+F5
   (`bpmnKeyHandler`), le canevas de la carte et ses touches étant retirés.
2. **Au clavier.** Espace ouvre la fiche comme Entrée ; Alt+Bas plonge dans une étape détaillée comme le double clic
   (`aria-keyshortcuts`) ; la fiche fermée rend le focus à la forme de l'étape (`stepFocus.ts`). Un début, une fin, un
   parallèle ou un inclusif n'ouvre pas de fiche, comme sur la carte.
3. **Les libellés sous un rond ou un losange** sont centrés sous la forme (`Lines`, `anchor`) : écrits depuis son milieu,
   ils débordaient à droite.
4. **La langue.** Les mots du schéma et du `.bpmn` (rôles du RACI, intitulés de la documentation, classification) sont
   ceux de la langue montrée, lue avant l'export (`state/shownTexts.ts`), comme ses noms (point 3 ci-dessus).
5. **Ce que la vue lit.** Sans événement d'audit, la copie de la vue (`purpose=view`, `IPmapFormat.OutlineView`) ne porte
   que la carte elle-même : ni ses niveaux, ni l'emplacement des documents, ni commentaires ni glossaire, et l'hôte ne
   lui donne aucun nom de fichier ; la vue tient de la carte quelles étapes sont détaillées (`withDetailsOfMap`). Elle
   garde le code et l'état de chaque irritant, que la carte montre (`PmapOutlineOptions.WithPainPointStates`) : sans
   eux, un irritant traité était dessiné ouvert, sans son code (audit de la copie 65, sur la correction de la 64). La
   copie d'un `.bpmn` reste celle de la branche, avec son événement. Écarté : journaliser la vue (chaque bascule
   écrirait un export).
6. **Pour les modeleurs.** La documentation du projet est écrite aussi sur le `participant`, que sélectionne un clic sur
   le bassin, en texte simple ; le fichier ne porte que les documents et les notes dessinés, avec leurs associations
   (ce qui n'est pas dessiné reste écrit dans la documentation de l'étape) ; conditions et durées sont des
   `tFormalExpression`. Camunda et `bpmn-js` ne dessinent pas les couloirs à l'intérieur d'un sous-processus déplié :
   ses étapes y gardent leur acteur dans leur documentation (manuel).

## Vérification

- `bpmnCompile.test.ts` : le fichier, avec et sans détails ni sous-processus, validé par les **schémas officiels de
  l'OMG** (`web/src/test/oracle/bpmn-2.0.2/`, identiques à l'octet aux fichiers de l'OMG) et relu sans avertissement
  par `bpmn-moddle` ; types d'éléments, conditions, langue montrée, mise en page, même fichier pour la même carte, et
  la copie de l'exemple du dépôt.
- `bpmnProperties.test.ts` : 300 cartes au hasard, toutes valides, sans forme hors de son couloir ni chevauchement ;
  textes piégés échappés.
- `BpmnView.test.tsx` : copie lue en `view` une seule fois, formes par type, fiche ouverte, plongée, détails retirés et
  retenus, retour par Alt+B et par le bouton, export lu en `bpmn` et nommé par la règle.
- Hôte : `PmapRoutesTests` (deux bases) pour les trois usages et le journal ; `MapPmapExport` sans événement en `view`.
- Témoins de mutation vus rouges : ordre des balises, échappement, vue journalisée, double lecture de la copie.
- Révision de la copie 64 : `BpmnView.test.tsx` (libellés centrés, Espace, Alt+Bas, début et fin, ruban, touches, langue
  du `.bpmn`, étape détaillée tenue de la carte), `ReadView.test.tsx` (fiche sans droit, brouillon), `bpmnCompile.test.ts`
  (seuls les éléments dessinés, bassin documenté, expressions typées), `PmapCopyExtrasTests` et `PmapExchangeTests`
  (copie de la vue), `PmapRoutesTests` (sans nom de fichier) ; chaque correction vue rouge sans elle.
- Audit de la copie 65 : `PmapCopyExtrasTests.The_copy_the_view_in_bpmn_reads_keeps_the_code_and_the_status…` (un
  irritant abandonné garde son code et son état dans la vue, ni commentaire ni glossaire) ; vu rouge sans l'option, et
  rouge aussi avec la correction que proposait le rapport (des extras vides dans la vue), qui faisait sortir le
  glossaire.
