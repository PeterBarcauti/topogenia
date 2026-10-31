<!-- 083-les-langues.md -->
<!-- The languages (3.0, lot 6): eighteen languages, the language of the interface and the language of the process chosen apart, one language on the map, the version of origin locked, a translation by the assistant that takes only the translations and is previewed in place -->

# 083 - Les langues

Statut : acceptée (date 30/10/2026). Prolonge AR-29 (langues de la configuration), l'ADR 065 (traductions) et l'ADR 081
(traductions d'une proposition). Décisions du porteur du projet du 30/10/2026, après sa remarque « si le collègue est
portugais, il ne peut pas traduire le processus dans sa langue car on ne peut sélectionner que français et anglais dans la
barre de menu » : la langue de l'interface se choisit dans les paramètres ; le sélecteur du haut correspond à la
traduction actuelle du processus ; dix-huit langues ; « une seule langue sur le graphique, celle sélectionnée, un
verrou » ; traduire par l'IA passe par un bouton dédié, limité à la liste, qui affiche ensuite la langue traduite sans
écraser l'original.

## Contexte

Une seule liste (`Topogenia:Languages:Supported`, `fr` et `en` par défaut) servait aux langues de l'interface (les
boutons FR/EN de l'en-tête) et aux langues des traductions (classeur, champs par langue, consigne de l'IA). Un texte
nouveau prenait la langue de l'utilisateur : un processus se mélangeait. Renommer un libellé affiché dans une autre
langue que la sienne partait du texte d'origine et l'écrasait. Le marché (ARIS, Signavio) sépare la langue de
l'interface, réglage de la personne, et la langue du contenu, choisie à la lecture.

## Décision

1. **Dix-huit langues par défaut**, dans l'ordre de leur propre nom, l'écriture latine d'abord : cs, de, en, es, fr,
   it, hu, nl, pl, pt, ro, sv, tr, el, ru, uk, zh, ja ; le français reste la langue par défaut. Une langue est toujours
   un fichier de l'interface, un fichier de messages de l'API, un fichier de messages du démarrage et une ligne de la
   configuration (NF12) ; le nom d'une langue vient du navigateur (`Intl.DisplayNames`), aucune liste n'est écrite dans
   le code. Les seize fichiers nouveaux sont produits par l'IA, contrôlés (clés, formes du pluriel de chaque langue,
   variables, `$t()`) et **à relire par un natif**.
2. **Deux langues par personne** : la **langue de l'interface**, dans le menu de l'utilisateur, enregistrée dans son
   profil (en mode Poste comme en mode Serveur) ; la **langue du processus**, dans une liste de l'en-tête quand un
   projet est ouvert : la langue d'origine, les langues traduites avec leur avancement, les autres. Elle vaut pour tout
   le projet ouvert ; à l'ouverture, c'est la langue de l'interface si le projet y est écrit (sa langue d'origine, ou
   traduit dans cette langue pour la moitié de ses textes au moins : un nom traduit ne verrouille pas la carte de son
   auteur), sinon sa langue d'origine.
   Elle n'est pas enregistrée.
3. **La langue d'origine du processus** est celle du projet : tout texte créé dans le projet la prend, quelle que soit
   la langue de l'utilisateur (`ProjectWorkspace.TextLanguage`). La création d'un projet demande sa langue (celle de
   l'interface par défaut), son nom dans cette langue et, au choix, les traductions du nom.
4. **Une langue sur la carte** : chaque texte dans la langue choisie ; un texte qui n'y est pas traduit est montré dans
   sa langue d'origine, grisé, en italique, souligné en pointillé, « À traduire » ; le bandeau compte ceux de la carte.
5. **Le verrou** : dans une autre langue que l'origine, la structure est figée (le droit de structure est retiré de la
   carte montrée, quel que soit le rôle). Un animateur y garde la traduction : renommer une étape, un couloir ou une
   flèche écrit sa traduction manuelle dans la langue montrée, jamais l'original. Règle unique : un nom se tape dans la
   langue montrée ; si c'est sa langue d'origine, l'original change, sinon sa traduction. Ctrl+Z et Ctrl+Y lui restent :
   ils rejouent l'historique de la carte, et défont d'un coup une traduction par l'assistant.
6. **Traduire avec l'assistant** : le dialogue « Traductions » propose d'abord l'assistant (une langue de la liste, son
   avancement), puis le classeur. La pratique `translate` envoie au modèle la carte racine et tous ses niveaux, sans
   fiches, et la langue cible ; elle n'est permise qu'à un animateur, jamais dans la langue d'origine. De la réponse,
   l'application **ne prend que les traductions dans la langue demandée** (`ProposalPlan.TranslationOf`) : la carte est
   reconstruite telle qu'elle est, chaque texte recevant sa traduction lue par code ; un texte réécrit au lieu d'être
   traduit devient sa traduction ; tout autre changement est ignoré. Le nom du projet est traduit aussi, et la carte
   racine qui le porte (F-57). Aperçu, « Appliquer », un Ctrl+Z ; puis la langue du processus passe sur la langue
   traduite. Une demande libre de traduction reçoit une réponse qui renvoie au bouton.
7. **Les écrans de traduction ne montrent pas dix-huit langues** : le classeur porte la langue d'origine (sa première
   colonne de langue) et les langues cochées (celles déjà traduites et la langue montrée, `?languages=` côté API) ; la traduction manuelle d'un objet
   montre les langues du projet et la langue montrée, les autres par « Ajouter une langue ».
8. **Le PDF** : la carte y est une image rendue par le navigateur (toutes écritures) ; une ligne de l'en-tête dont Inter
   n'a pas les lettres (chinois, japonais) y est dessinée en image à haute résolution.
9. **Le serveur écrit le temps dans la langue de la personne** : « il y a 5 minutes » et « quelqu'un » viennent des
   fichiers de messages (`TEMPS_*`, `QUELQU_UN`), la forme du pluriel suivant les règles de chaque langue.
10. **Chargement à la demande** : seul le français vient avec la page ; une autre langue de l'interface est lue quand
    elle est montrée pour la première fois (dix-huit fichiers, 1,1 Mo de textes).
11. **Le plafond d'une réponse passe à 64 000 jetons par défaut** (16 000 avant) : à son essai, une traduction en chinois
    de son processus a dépassé 16 000 jetons. Le porteur du projet a préféré un plafond plus haut à une réponse allégée :
    la traduction reste rare et son coût (de l'ordre d'un euro) est raisonnable au regard du temps gagné ; l'effort du
    modèle reste élevé, le contexte entier lui est donné. Une installation déjà réglée garde son plafond enregistré (roue
    dentée, « Plafond d'une réponse »).
12. **L'aperçu d'une traduction montre la traduction, pas des changements** : à son essai, le porteur du projet ne
    comprenait pas l'aperçu d'une traduction (des étapes encadrées en orange, une liste de « modifications » de
    traductions). L'aperçu d'une traduction pose désormais sur la carte, en place, les textes que la traduction écrira,
    dans sa langue, la carte verrouillée et rien d'écrit (un bandeau le dit ; un texte qu'elle ne traduit pas reste
    marqué « à traduire »). Le volet en donne le résumé (le nombre de textes traduits, les traductions saisies à la main
    gardées) et « Relire les textes », chaque texte à côté de son original ; ces textes viennent du serveur, après le
    verrou (`ProposalPlan.TranslationTexts`, `translations` de l'aperçu). Seuls « Appliquer », « Écarter » et
    « Télécharger le .pmap » restent : une traduction ne se précise pas en mots, elle se redemande depuis
    « Traductions », et n'ouvre pas de nouveau projet. « Écarter », la fermeture du volet ou une conversation
    recommencée rendent la carte telle qu'elle était, dans la langue où elle était montrée ; « Appliquer » la laisse dans
    la langue traduite.

## Conséquences

- Les tests qui affirmaient l'ancienne règle (« un texte nouveau prend la langue de l'utilisateur ») sont réécrits pour
  la règle nouvelle ; le test des fichiers de langue contrôle les formes du pluriel de chaque langue et les variables.
- Une langue ajoutée plus tard avec des pluriels particuliers demande sa règle dans `RelativeTime.PluralForm`.
- Changer la langue d'origine d'un projet existant n'est pas offert (F-60).
- Les seize interfaces nouvelles sont à relire par des collègues natifs (F-59).
