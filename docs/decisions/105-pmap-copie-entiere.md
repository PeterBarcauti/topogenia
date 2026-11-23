<!-- 105-pmap-copie-entiere.md -->
<!-- The .pmap 1.5 (3.3): what a whole copy carries beside the structure, the comments of presentation of the steps and flows, the glossary of the project, the code and the status of a pain point; written by a copy and a duplication only, read and opened by the rules of each, left out of a proposal -->

# 105 - Le `.pmap` 1.5 : la copie entière

Statut : acceptée le 16/11/2026 (3.3, lot 1). Révise le point 7 de l'ADR 099 (« pas dans le `.pmap` ») et prolonge les
ADR 054 (format `.pmap`), 092 (copie d'une carte) et 094 (résumé). Décisions du porteur du projet du 16/11/2026 : les
commentaires de présentation, le glossaire, le code et l'état des irritants entrent dans le `.pmap` ; le `.pmap` devient
la seule description d'une carte hors de la base, que liront la vue et l'export BPMN 2.0, l'assistant qui écrit les
commentaires et l'insertion d'une carte comme étape détaillée.

## Contexte

- Le `.pmap` 1.4 décrit la structure, les fiches, le flux de valeur et le Makigami. Il ne porte ni les commentaires de
  présentation (rangés hors du cœur, ADR 099), ni le glossaire du projet, ni le code et l'état d'un irritant.
- Conséquences constatées : « Dupliquer » perd les commentaires (F-130) ; un irritant traité redevient ouvert après une
  copie rouverte ; un export qui partirait du `.pmap` (BPMN) n'aurait ni les commentaires ni le glossaire.
- Le cœur fermé ne produit qu'un irritant « signalé » : aucune ouverture ne peut écrire un autre état sans le modifier.

## Décision

1. **Trois ajouts, version 1.5** :
   - `project.glossary` : le glossaire du projet, texte de 4 000 caractères au plus, un terme par ligne ;
   - `comment` sur une étape et sur une flèche : `text`, la ligne dans la langue d'origine de l'étape (de l'étape de
     départ pour une flèche), absente quand il n'y en a pas, et `translations`, les autres lignes ; chaque ligne sous
     la règle de tout commentaire (500 caractères, sans balise, fins de ligne en `\n`) ;
   - `pain_point_code` et `pain_point_status` dans un usage, avec `pain_point` seulement.
2. **Écrits par une copie entière seulement** : copie d'un projet, copie d'une carte (celle que porte une image), et
   modèle d'une duplication. L'application lit les commentaires à côté de l'espace de travail, comme pour le paquet, et
   les donne au format (`PmapExtras`). Le contexte de l'assistant ne les reçoit pas : il les recevra avec le lot qui lui
   apprend à écrire les commentaires.
3. **Version écrite** : la plus ancienne qui porte le contenu, 1.5 seulement avec un de ces ajouts ; une 3.2 ouvre donc
   encore toute copie qui n'en a pas, et refuse un fichier 1.5 avec le message de version existant.
4. **À l'ouverture** :
   - le glossaire est posé par la règle du réglage (`ProjectSettings.ChangeTranslationGlossary`) ;
   - les commentaires sont écrits à côté du cœur, dans l'unité de l'ouverture, après le projet, par la règle de tout
     import (`CommentLineRules.Imported`) ; toutes leurs lignes sont des saisies manuelles, comme les traductions du cœur
     qu'apporte un fichier ; une traduction dans la langue du fichier cède la place à `text` ;
   - le code d'un irritant est informatif : l'application numérote les siens ;
   - un irritant traité (`implemented`, `adopted`, `dropped`) n'est pas rouvert : l'usage reste, sans irritant. Le projet
     ouvert montre les mêmes irritants ouverts et les mêmes heures perdues que sa source. Un état ouvert autre que
     « signalé » revient « signalé », le seul que le cœur produise.
5. **Une proposition de l'assistant** connaît les propriétés du 1.5 et n'en garde aucune valeur.
6. **Duplication** : elle garde désormais les commentaires et le glossaire, qui passent par le même modèle.

7. **Lettres du RACI** (ajout du 16/11/2026, après l'essai du porteur du projet) : la copie du projet d'exemple, qui
   donne un rôle S, ne se rouvrait pas dans une installation en RACI (A, C, I), défaut déjà présent en 3.2. Décision du
   porteur : une ouverture prend toute lettre du RACI (A, S, C, I) quel que soit le réglage, qui ne dit plus que ce
   qu'une personne saisit, comme le paquet. Une lettre offerte passe par la règle du domaine ; une autre est gardée telle
   quelle, le projet construit passant les règles de la structure. Révise T-FICR-09 (`docs/portage-tests.md` (non publié)).

## Conséquences

- Aucun fichier du cœur fermé, aucune migration ; le port `IPmapFormat` prend `PmapExtras` pour la copie, la copie d'une
  carte et le modèle d'une duplication, et rend les lignes de commentaires d'un projet construit.
- `schemas/pmap-1.5.schema.json` est publié à côté des précédents ; le guide et le prompt du croquis pour une IA passent
  en 1.5.
- Un irritant traité ne se retrouve plus rouvert par une copie rouverte, ce qui arrivait avant le 1.5. Ni par une
  proposition de l'assistant ouverte en nouveau projet (ADR 082) : les fiches qu'elle reprend de la carte d'origine sont
  lues avec l'état de leurs irritants, sans commentaire (audit de la copie 64, `PmapFiles.Carry`, essai
  `PmapCopyExtrasTests.A_proposal_opened_as_a_new_project_keeps_the_status_of_the_pain_points_its_map_of_origin_dealt_with`).

## Vérification

- `PmapCopyExtrasTests` (lecture, ouverture, copie entière et retour, refus des fautes, proposition, liste des états
  traités tenue égale à `MapMarkers.IsOpen`), `PmapSchemaTests`, `PmapDocumentationTests`.
- `PmapExchangeTests` : les copies et la duplication donnent les commentaires au format, l'ouverture les écrit.
- `PmapRoutesTests` sur SQLite et SQL Server : un fichier 1.5 ouvert, copié, rouvert et dupliqué garde ses commentaires,
  son glossaire et ses seuls irritants ouverts.
- Témoins de mutation vus rouges : commentaires des flèches retirés de la copie, écriture des commentaires des flèches
  à l'ouverture, règle de la proposition, règle des irritants traités.

## Écarté

- **Écrire les ajouts dans le contexte de l'assistant dès ce lot** : la proposition ne saurait pas encore les appliquer.
- **Rouvrir un irritant traité comme « signalé »** : le projet ouvert montrerait des heures perdues que sa source ne
  compte plus.
- **Un format de données à part pour le BPMN** : il dupliquerait la lecture des fiches et sa règle des brouillons.
