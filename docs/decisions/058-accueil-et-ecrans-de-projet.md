<!-- 058-accueil-et-ecrans-de-projet.md -->
<!-- Accueil, corbeille, création et membres d'un projet, dialogues des fichiers .pmap et du paquet d'échange -->

# 058 - Accueil et écrans de projet

Statut : acceptée (date 28/10/2026)

## Contexte

P6-T03 porte l'annexe B 1.3 à 1.6 et 1.8 (accueil, corbeille, création d'un projet, membres, fichiers `.pmap`, pas
d'écran Profil), AR-88 (textes de l'accueil), AR-49 (dernier projet ouvert), AR-29 (un nom par langue de la
configuration), et la part interface de la spec 7 (paquet d'échange : export sans les personnes coché par défaut,
S-5.4-25 ; aperçu obligatoire, S-7.3-03). La 1.x n'avait aucun écran pour le paquet, qui n'existait pas.

## Décision

1. **Accueil** (`features/home/`) : bandeau aux textes d'AR-88 ; « Décrire un processus » et « Qui participe » portent
   sur le projet mis en avant, le dernier ouvert s'il est encore visible, sinon le premier par code (`focus.ts`) ; la
   mémorisation du dernier projet ouvert (`PUT /api/me/last-project`) est faite par l'éditeur à l'ouverture d'une carte
   (P6-T04). L'administrateur a trois boutons : ouvrir un `.pmap`, **importer un paquet** (nouveau projet), nouveau
   projet. La corbeille est lue à côté des projets, ses erreurs ignorées, y compris après une action.
2. **Suppression d'un projet** : la saisie du code (majuscules, 6 au plus) arme le bouton ; la route exige la version
   du projet (If-Match, mode Serveur) que la liste ne donne pas : le dialogue relit le projet au moment de supprimer et
   envoie sa version. Un refus reste dans le dialogue.
3. **Création d'un projet** : un champ « Nom » par langue de la configuration, nommée par sa clé `languages.<code>`
   (repli sur le code en capitales pour une langue sans nom) ; langue d'origine = celle de l'interface si son nom est
   rempli, sinon la première langue configurée dont le nom l'est ; les autres noms remplis sont ses traductions.
   Classification et libellé du groupe de partage envoyés vides (le serveur applique son défaut ; aucune ligne de
   l'annexe B ne les demande à la création). **Amendé le 29/10/2026 (ADR 069, AR-133)** : la classification se
   choisit à la création (« Interne » par défaut, envoyée explicitement) ; le libellé reste vide, réglé ensuite dans
   les propriétés du projet.
4. **Membres** : l'éditeur garde la marque « contribution clé » telle qu'elle a été lue ; l'enregistrement envoie la liste
   complète avec la version du projet, puis relit le projet pour la version de l'enregistrement suivant.
5. **Fichiers `.pmap`** : dialogues d'ouverture (accueil) et de duplication, téléchargement d'une copie (sans les fiches
   par défaut, ADR 054). Le nettoyage du contenu retire un bloc Markdown ; la marque d'ordre des octets (U+FEFF) est un
   blanc pour `trim`, une ligne dédiée était un mutant équivalent et a été retirée.
6. **Nom des fichiers téléchargés** : l'hôte n'envoie pas de `Content-Disposition` ; l'interface nomme le fichier par la
   règle de l'hôte (A-1.2-16 : `CODE_Nom-court_aaaa-mm-jj.ext`, jour UTC), reprise dans `shared/download.ts` et prouvée
   par les mêmes cas que `ExportFileNameTests`. Écarté : lire l'en-tête de la réponse, que le client ne rend pas
   (ADR 056) et que l'hôte n'écrit pas.
7. **Paquet d'échange** (`features/project/package/`) : l'export coche « Sans les membres ni les auteurs », décochable
   (S-5.4-25). L'import passe toujours par l'aperçu, et seulement pour les choix de cet aperçu (fichier, membres, code) :
   tout changement efface l'aperçu et demande de vérifier à nouveau (S-7.3-03, ADR 052 : l'aperçu est le compte rendu
   de l'import). « Reprendre les membres du paquet » est décoché par défaut (spec 7.3 : repris seulement si l'option est
   cochée). Un code pris (vivant ou dans la corbeille) fait demander un autre code, vérifié avant l'import. Le
   remplacement envoie la version du projet remplacé.
8. **Emplacements** : l'export du paquet, le remplacement par un paquet, la duplication et le téléchargement d'une copie
   s'ouvrent depuis le menu Fichier de la carte (P6-T04, comme la 1.x pour la duplication et la copie) ; les dialogues
   sont écrits et testés ici. L'extension du paquet (`PACKAGE_EXTENSION`) est le nom du produit en minuscules, lu dans la
   clé `app.name` (D13) et donné aux textes par la variable `{{extension}}`.
9. Le texte de Ctrl+S reprend celui de la 1.x (« Fichier > Télécharger une copie enregistre les cartes sur le disque (pas
   les fiches) »), le menu Fichier de P6-T04 portant cette entrée.

## Conséquences

- Lignes de bout en bout (B-1.4-15, B-1.6-17, B-1.6-22, S-7.3-03) : prouvées en unitaire, laissées « à faire » jusqu'aux
  parcours de P6-T10 ; les lignes qui citent aussi l'unitaire sont « couvert (interface) ».
