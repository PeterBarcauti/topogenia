<!-- 078-volet-de-l-assistant.md -->
<!-- The panel of the assistant (3.0, lot 2): one exchange route per scope streamed as JSON lines, one structured reply of four forms, the context sent and cached, the proposal as a .pmap text corrected once, the copy and paste, the audit without text -->

# 078 - Volet de l'assistant

Statut : acceptée (date 30/10/2026). Prolonge l'ADR 077 (paramètres et connecteurs). Spécification :
`TOPOGENIA_3.0_SPECIFICATION_IA.md`, sections 3 à 9 (révision 4, quatre pratiques du volet, décisions du porteur du projet
du 29/10/2026 et du 30/10/2026).

## Contexte

Le lot 1 a posé les connecteurs et leur test. Le lot 2 ouvre le volet de la carte : quatre pratiques (créer depuis un
texte, modifier, analyser ce que l'on voit, poser une question), quatre formes de réponse, rien d'appliqué sans accord,
le contexte voulu par le porteur du projet (documentation des formats, schéma complet du projet, carte ouverte), rien de
gardé du fil. L'essai réel du lot 1 a montré qu'un modèle peut mettre près de vingt secondes pour un mot : le volet doit
montrer qu'il travaille, et un échange long ne doit pas être coupé en route.

## Décision

1. **Un échange par portée, en flux de lignes JSON** (`application/x-ndjson`) : `POST /api/maps/{id}/assistant/exchanges`
   (la carte, ou le projet entier depuis la carte racine) et `POST /api/assistant/exchanges` (un projet à créer, depuis
   l'accueil, administrateur). Tout refus est dit **avant** le flux, en erreur ordinaire : droits, connecteur qui
   n'appelle pas de modèle (`IA_INDISPONIBLE`), classification non autorisée (`IA_CLASSIFICATION_REFUSEE`), demande
   vide ou trop longue, invite au-delà de sa borne (`IA_CONTENU_TROP_GRAND`). Ensuite : une ligne `started` (où partent
   les données), des lignes `progress` (phase, dernière ligne de la réflexion résumée, caractères écrits, temps), **au
   moins une toutes les 2 s** (un proxy ou IIS ne coupent pas un échange long), puis une ligne `reply` ou `failure`.
   Fermer la requête (« Arrêter ») annule l'appel au modèle. L'état du volet se lit par `GET /api/maps/{id}/assistant`
   (mode, destination, classification permise, proposition permise, carte vide, coût montré).
2. **Une réponse unique et structurée**, contrainte par la sortie structurée des deux fournisseurs (API Anthropic :
   `output_config.format` ; Bedrock : `outputConfig.textFormat`) : `{ form, message, cited, questions, findings, pmap }`,
   `form` parmi `answer`, `questions`, `findings`, `proposal`. Les schémas récursifs étant refusés par la sortie
   structurée, **la proposition porte le texte d'un `.pmap` 1.1**, que le lecteur du format de l'application ouvre avec
   ses propres règles : une seule source de vérité du format. Un `.pmap` refusé est **renvoyé une fois** au modèle avec
   ses fautes (phase `correcting`) ; s'il reste refusé, ses fautes sont montrées, ligne par ligne, dans la langue de la
   personne. La réponse est une donnée : relue par `AssistantReplyReader` (textes bornés et coupés, listes raccourcies,
   codes inconnus écartés), jamais crue sur parole.
3. **Formes permises** : Réponse, Questions, Analyse pour tout membre ; la Proposition demande l'animateur (ou
   l'administrateur depuis l'accueil). La consigne le dit au modèle ; l'hôte refuse une forme non permise (échec
   `form_refused`). Créer ou modifier (tuiles) demande l'animateur avant tout envoi (`ACCES_REFUSE`).
4. **Contexte, dans l'ordre du cache** : (a) consigne commune et documentation des formats (`PMAP_GUIDE_IA.md`,
   `format-pmap.md`, schéma `.pmap` 1.1, `format-topogenia.md`, schéma du paquet 2.0), fichiers du dépôt **embarqués** dans
   `Topogenia.Assistant`, point de cache ; (b) schéma complet du projet, point de cache : les entrées du paquet sans les
   personnes ni les traductions pour l'animateur, qui peut exporter le paquet, **le `.pmap` du projet entier avec les
   fiches telles qu'il les voit pour un autre membre** (le paquet porte des brouillons qu'un lecteur ne voit pas,
   spec 5.6-1) ; écrits de la même façon pour le même état (horodatage de la création du projet) pour que le cache
   tienne ; (c) les échanges précédents (demande et JSON de la réponse, gardés par la page), point de cache sur le
   dernier ; (d) la carte ouverte au format `.pmap` (sans ses cartes détaillées, fiches telles que la personne les voit),
   les codes sélectionnés, la consigne de la tuile, la demande. Effort `medium` pour une question, `high` sinon ;
   réflexion adaptative résumée, écrite dans la langue de la demande.
5. **Connecteurs** : l'API Anthropic en flux (`CreateStreaming`, deux nouvelles tentatives, 15 min au plus) ; Bedrock par
   `Converse` sans flux au lot 2 (les battements de l'hôte disent qu'il travaille), points de cache du système, sortie
   structurée et effort. Un refus du modèle (`refusal`) et une réponse coupée par le plafond (`max_tokens`) sont des
   échecs dits en clair (`declined`, `truncated`).
6. **Copier-coller** (connecteur Copier-coller ou Aucun) : `POST …/assistant/prompt` rend l'invite complète en un texte
   (mêmes parties, mêmes règles de classification) ; `POST …/assistant/replies` lit la réponse collée comme celle d'un
   modèle (mots ou bloc de code autour admis, `.pmap` seul reconnu comme proposition). Rien ne sort de l'application.
7. **Audit** `assistant.exchanged` : connecteur, modèle, portée, pratique, forme, issue, correction, durée, jetons
   (entrée, cache lu, cache écrit, sortie) ; jamais un texte. Écrit aussi quand la personne arrête l'échange.
   L'administrateur voit sous chaque réponse ses jetons et son coût estimé aux prix publics de l'API Anthropic.
8. **Rien n'est gardé** : le fil vit dans la page, avec l'éditeur de la carte (et le fermer garde le fil tant que la
   carte reste ouverte) ; jamais en base, sur le poste ni au journal.
9. **Interface** : bouton IA en marge droite du ruban (aussi replié), **Ctrl+I** (libre sur la carte) ouvre et ferme ;
   volet ancré à droite, un volet à la fois avec la fiche et le reliquat ; en-tête portée, pastille de la sélection
   (retirable), ligne « où partent les données » ; volet vide : quatre tuiles et exemples adaptés (carte vide : créer
   d'abord) ; champ de quatre lignes qui grandit jusqu'à douze puis défile, Entrée envoie, Maj+Entrée va à la ligne,
   icônes Copier, Coller, Lire un fichier texte (`.txt`, `.md`, 100 000 caractères), Effacer ; cartes Réponse (codes
   cliquables : sélection et mise en vue), Questions (choix qui remplissent la réponse, « Proposer quand même »),
   Analyse (constats, « Montrer »), Proposition (chiffres, points à vérifier, fautes, « Créer le projet » ou « Ouvrir en
   nouveau projet », « Télécharger le .pmap ») ; carte de progression animée. Accueil : « Décrire un processus » ouvre le
   même volet pour l'administrateur ; la proposition passe par l'ouverture d'un `.pmap` (code et nom à confirmer).

## Conséquences

- L'aperçu et l'application d'une proposition sur une carte qui a du contenu viennent au lot 3 (calcul dans le cœur
  fermé) ; au lot 2, une proposition se crée en nouveau projet ou se télécharge.
- Bedrock sans flux, le repli côté serveur (`fallbacks`) non activé et le plafond du contenu envoyé fixé dans le code
  (2 400 000 caractères) sont inscrits dans `docs/a-peaufiner.md`.
- Un proxy d'entreprise qui met les réponses en tampon retarde l'affichage de la progression, sans couper l'échange
  (guides d'exploitation).
