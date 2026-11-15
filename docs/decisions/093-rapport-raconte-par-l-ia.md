<!-- 093-rapport-raconte-par-l-ia.md -->
<!-- The report told by the AI (3.1): a fifth form of reply of the assistant, its texts from the model, its diagrams and figures from the map, saved as a Markdown file and kept nowhere in Topogenia -->

# 093 - Le rapport raconté par l'IA

Statut : acceptée (date 06/11/2026). Prolonge les ADR 077 (connecteurs de l'IA), 078 (volet de l'assistant), 083 (langues) et 092 (export Markdown Mermaid).
Demande du porteur du projet du 05/11/2026. Il s'agit d'un rapport Mermaid complet qui raconte l'histoire du diagramme,
avec ses problèmes et les actions associées. Il est rédigé par l'IA à partir d'une consigne préparée, pour résumer et
dégrossir un processus. C'est un export : rien n'est enregistré dans Topogenia.

## Contexte

L'export Markdown (ADR 092) donne le diagramme et les tableaux, mais pas de récit. Un récit demande un modèle de langue.
Or le volet de l'assistant a déjà tout ce qu'un appel exige :
- la destination des données et leur coût ;
- l'avancement et l'arrêt ;
- le mode copier-coller sans connecteur ;
- les refus par classification et par droits.

## Décision

1. **Une cinquième forme de réponse, `report`, sur la route d'échange du volet**, avec la pratique `report`. Il n'y a
   pas de route propre, contrairement à ce que le plan prévoyait. Le volet reste le seul chemin vers un modèle, avec ses
   refus et son journal sans texte.
2. **Ce que le modèle reçoit** :
   - La consigne fixe embarquée `Instructions/practice-report.md`, pour un public non spécialiste : phrases courtes,
     codes cités, aucun chiffre inventé, le relevé distinct du repéré, des actions concrètes portées par un couloir, un
     effort et un impact de 1 à 5, une vague (tout de suite, dans le mois, plus tard), la langue de la carte.
   - Le contexte du volet, plus un bloc `<pain_points>` : les points de douleur des fiches des étapes de la carte
     (`AssistantPainPoints.Of`), selon les droits. Ils arrivent avec l'échange, sans route de lecture nouvelle.
3. **Une réponse structurée et bornée** (`ReplySchema`, objet `report` strict). Le lecteur `AssistantReportReader` borne
   les textes et raccourcit les listes. `AssistantReportCodes` ne garde que les codes d'étape de la carte, et ne marque
   un problème « relevé » que s'il porte un point de douleur de ses fiches.
4. **L'assemblage est fait par le code** (`web/src/features/map/export/report.ts`). Les textes viennent du modèle ; les
   diagrammes, les chiffres et les tableaux viennent de la carte. Le rapport compte 11 sections, dont l'en-tête
   « Rédigé avec l'IA » (modèle, date, classification). Les diagrammes sont écrits par le code (ADR 092) :
   - la vue d'ensemble, avec les étapes à problème colorées ;
   - un extrait par chapitre ;
   - un `journey` ;
   - un `quadrantChart` des actions par effort et impact ;
   - une `timeline` des vagues ;
   - un `pie` du temps par nature.
5. **Le rapport s'affiche dans le volet et « Enregistrer » le télécharge en `.md`.** Il n'est écrit ni dans la carte, ni
   dans l'historique, ni dans la base.

## Écarté

- **Une route `POST /api/maps/{id}/assistant/report` et un schéma passé en paramètre aux connecteurs** : cela doublait le
  volet, et donc ses refus, son flux et son mode copier-coller.
- **Une route `GET /api/maps/{id}/pain-points`** : le rapport est le seul à lire les points de douleur, et l'échange les
  porte déjà.
- **Un diagramme écrit par le modèle** : une erreur de syntaxe casserait le rendu, et les recouvrements ne seraient pas
  maîtrisés.

## Conséquences

- Le rapport suit les mêmes règles que le volet : connecteur configuré ou copier-coller, refus pour un projet dont la
  classification interdit l'assistant, coût affiché.
- L'essai réel demande un connecteur qui marche sur le poste (clé de l'API Anthropic ou Bedrock), à voir à l'essai du
  porteur du projet.

## Vérification

- **Application** : `AssistantReportTests` et `AssistantPainPointsTests` (bornes, codes inconnus, « relevé »).
- **Hôte** :
  - `AssistantPanelTests` : rapport demandé par un lecteur.
  - `AssistantExchangeConnectorTests` : rapport par le connecteur.
- **Interface** :
  - `report.test.ts` : assemblage, `mermaid.parse` de chaque diagramme, cas difficiles.
  - `actions/report.test.tsx` et `assistant/panel/Report.test.tsx`.
