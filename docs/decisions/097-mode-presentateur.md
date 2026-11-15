<!-- 097-mode-presentateur.md -->
<!-- The presenter mode (3.2): a map played as a story in full screen, its tour read from the main path of the core, its subtitles sentence by sentence in the language shown, its camera, its light along the flows, its counters, its chapters, its keys of a presenter; interface only, nothing written -->

# 097 - Le mode présentateur

Statut : acceptée le 10/11/2026 (3.2, construite la nuit du 09 au 10/11 en autonomie, sur consigne du porteur du projet :
« tu es vraiment responsable du rendu final. Les habitués du PowerPoint n'ont qu'à bien se tenir »). Revue avec lui
prévue le 11/11/2026. **Révisée le 12/11/2026**, avant toute livraison, après deux relectures critiques : la scène
est un dialogue modal (point 2) ; une carte sans flèche (point 3) ; les sous-titres comptés en colonnes et annoncés
aux lecteurs d'écran (points 5 et 6) ; la caméra (point 7) ; les compteurs (point 9) ; les touches U et Tab, et F5
(point 11) ; la question de la voix et les sous-titres sans voix (point 12) ; la plongée abandonnée (point 13) ; les
refus dits dans la scène (point 14). **Révision du 12/11/2026 (audit 3.2, F-137, SEC-CRIT-02, UX-MOY-01)** : les voix
en ligne ne sont offertes que pour un projet dont la classification peut quitter le poste, la règle de l'assistant
(points 1 et 12), parce que Chrome n'a aucune stratégie qui les coupe ; la voix choisie est retenue par le navigateur
(point 12). **Révision du 12/11/2026 au soir (essai J6 du porteur du projet)** : Échap qui ferme ce qui est ouvert
dans la scène ne quitte jamais la présentation, quoi que le navigateur fasse du plein écran (point 11).

## Contexte

Une carte se lit mal en réunion : on la projette, et la salle cherche où regarder. Une étude du 09/11
(gardée hors du dépôt) propose de la jouer comme un récit. Vérifiée sur le
code et sur le marché (étude sourcée du 10/11) :

- les outils de processus animent un jeton sans caméra ni récit (simulation de jetons de bpmn-js : un jeton par branche
  après un parallèle, vitesses 0,5, 1 et 2 ; Disco et Celonis : rejeu d'un journal) ; IBM Blueworks Live « Playback »
  avance pas à pas avec des compteurs cumulés du temps de cycle ;
- les outils de présentation ont la caméra et les réflexes d'orateur sans rien savoir d'un processus (PowerPoint Morph et
  Zoom, Prezi, reveal.js) ; touches communes : flèches, Page suivante et précédente (télécommandes), B ou point pour
  l'écran noir, W ou virgule pour le blanc, Échap ;
- sous-titres : Netflix, 42 caractères par ligne, deux lignes, 17 caractères par seconde pour un adulte, 7 s au plus ;
- caméra : la trajectoire optimale de van Wijk et Nuij (2003), que `d3.interpolateZoom` et donc React Flow tracent déjà ;
  WCAG 2.3.3 (mouvement coupé sous `prefers-reduced-motion`) et 2.2.2 (une pause).

## Décision

1. **Interface seule, rien n'est écrit.** Tout vient de la `MapView` déjà lue : chemin principal, chiffres, parts,
   relais, ruptures de support, documents et irritants (marqueurs des étapes). Aucune route, aucun changement du cœur.
   Révision du 12/11/2026 (audit, F-137) : au départ, la présentation lit aussi l'état de l'assistant de la carte
   (`GET /api/maps/{id}/assistant`, route existante ouverte à tout membre) pour savoir si les voix en ligne peuvent
   être offertes (point 12) ; aucune route nouvelle.
2. **Une couche au-dessus de l'écran de la carte** (`features/map/presenter/`, dossier ajouté à la règle B-8.2-01),
   mise en plein écran par le geste qui l'ouvre ; refusé, elle couvre la fenêtre. Son propre React Flow en lecture
   seule ; l'éditeur reste dessous, sa vue gardée. Ses touches sur la couche `modal` : aucune commande d'édition ne passe.
   La scène est un **dialogue modal** (révision) : le focus y entre au départ et revient où il était à la sortie ; Tab
   et Maj+Tab parcourent ses commandes en boucle, sans sortir vers l'éditeur ; après Tab, et seulement après Tab,
   Entrée ou Espace activent le bouton focalisé. Après un clic sur un bouton, ou quand le focus est rendu au crayon
   d'un commentaire, ces deux touches gardent leur rôle de la présentation : sans ce choix, « T, texte, Entrée,
   Espace » rouvrirait le champ au lieu de relancer la lecture. Au repos, la barre se masque même après un clic sur
   un de ses boutons, et le pointeur avec elle, sur les étapes aussi.
3. **Le parcours est une règle de présentation, et il passe par toutes les étapes de la carte** (`model/tour.ts`,
   **récrit le 13/11/2026, lot J10**, à l'essai J9 du porteur du projet : « une étape qui a plusieurs sorties ne voit
   traiter qu'un parcours », « dans un calque, toutes les étapes devraient être parcourues avec une logique » ; la
   logique ci-dessous validée par lui le même jour). Le fil est le chemin principal du cœur (même contre une part plus
   forte), depuis son début ; hors de lui, une ligne avance par la sortie qui le rejoint, puis par la part effective
   du cœur, puis par l'ordre de lecture ; jamais un retour s'il y a une avance ; jamais vers une étape déjà prise.
   Chaque étape est racontée une fois, chaque flèche éclairée une fois :
   - *Là où la carte se divise, les autres branches d'abord, le fil en dernier* : une fois que le récit avance sur
     le fil, il ne revient plus en arrière. Une branche est racontée jusqu'à l'étape où elle se termine, ou jusqu'à
     la flèche par laquelle elle rejoint une étape déjà racontée ou gardée pour une ligne commencée ; cette flèche
     est éclairée au moment où le récit quitte la branche. La même règle vaut dans une branche qui se divise.
   - *Décision et inclusif* : l'arrêt est montré une fois par sortie (`Stop.again` pour les suivantes), chaque sortie
     prise depuis lui à son tour, la part la plus forte d'abord, celle du fil en dernier ; une sortie vers une étape
     déjà racontée (un retour) ou gardée (une rejointe directe) est seulement éclairée. L'arrêt revu dit la sortie
     qu'il prend, pas ses parts ni son commentaire une seconde fois.
   - *Toute autre étape à plusieurs sorties* (un parallèle, ou une étape sans losange, que le cœur compte en ET) :
     toutes ses sorties partent ensemble dans un seul passage (un point par flèche, celle qui mène à l'arrêt suivant
     en premier), puis chaque branche est racontée l'une après l'autre ; le récit passe de la fin d'une branche au
     début de la suivante par un saut, sans flèche parcourue. L'ancien « arrivent ensemble à la jonction » est
     abandonné : chaque branche est éclairée jusqu'à la jonction quand le récit la quitte.
   - *Plusieurs entrées* (`entriesOf` : les étapes qu'aucune flèche n'atteint et dont une flèche part) : les autres
     entrées sont racontées avant le fil, chacune jusqu'à l'étape où elle rejoint le reste (`Stop.aside`). Le reste
     est tout ce qu'un dossier atteint depuis le début du fil (`reachedFrom`), ses branches comprises : une branche
     du fil est racontée avec lui, à l'étape qu'elle quitte, jamais avant lui avec une entrée qui la rejoint
     (relecture indépendante du 13/11, S1).
   - *Ce qu'aucune flèche n'atteint depuis une entrée* (une étape seule, une boucle sans entrée) : à la fin, dans
     l'ordre de lecture, avant le bilan.
   - *Depuis une étape choisie* (Maj+F5) : cette étape et ce qui la suit, par les mêmes règles, et rien de ce qui la
     précède ; une étape d'avant cliquée devient alors le départ (BUG-04), en pause ou en lecture comme avant.
   - *Les pastilles d'une décision ne changent plus le parcours* : un clic ou un chiffre (1 à 9) va tout de suite au
     passage de l'arrêt de cette décision qui prend la sortie (`choose(stop, scenes)`), son vol joué aussitôt ; la
     pastille pleine est la sortie que prend l'arrêt montré. Le choix gardé par passage (BUG-05) et le retour rejoué
     une fois n'ont plus lieu d'être : chaque sortie a son arrêt.
   - La dernière étape garde un passage quand une de ses flèches reste à éclairer ; il finit où finit cette flèche.
   - *Précisions de la relecture indépendante du 13/11* : un chemin principal du cœur réduit à une étape reliée à
     rien, sur une carte qui a des flèches, n'est pas un fil (sans Début, le cœur compte comme entrée toute étape
     qu'aucune flèche n'atteint) : le récit part alors du premier Début, sinon de la première entrée (M9) ; une
     étape cliquée, ou dont on ouvre le détail, reste sur l'arrêt montré quand c'est un des siens (M1) ; un inclusif
     revu dit la sortie prise, comme une décision (M2) ; deux flèches éclairées ensemble entre les deux mêmes
     acteurs disent leur relais une fois (M3) ; le crayon et T, sur une décision revue, ramènent à son premier
     arrêt, celui qui dit son commentaire (M7).
   Une carte sans aucune flèche montre toutes ses étapes dans l'ordre de lecture, depuis la première ou depuis
   l'étape choisie (révision du 12/11 : le chemin principal du cœur n'y est qu'une étape, et le parcours partait
   d'elle). Refusé : un second mode « chemin principal seul » (question posée au porteur, restée sans réponse ;
   tranché : le parcours complet remplace l'ancien, un mode court pourra s'ajouter). Limite gardée (inscrite dans
   `a-peaufiner.md`, F-171) : le cadrage d'un passage dont les flèches partent ensemble vers des étapes éloignées, ou
   d'un retour au branchement, peut être large. La durée d'un passage à plusieurs flèches est celle de la plus longue,
   et chaque flèche emporte ses documents (point 7).
4. **Les scènes** : ouverture, **en pause** (lot J10, demande du porteur du projet : « il doit être par défaut sur stop
   et attendre la touche play » ; Lire ou Espace lance le récit, à tout départ : bouton, F5, Maj+F5, F5 dans la
   présentation), sur la carte entière (son nom, les étapes comptées une fois, les points de départ quand le récit en
   raconte plusieurs, les acteurs, le délai de bout en bout), puis chaque
   étape et le passage vers la suivante, puis le bilan (délai pour le travail, part du travail, relais, irritants, temps
   perdu), le chemin parcouru éclairé. Un parcours qui n'est pas le chemin principal (commencé à une étape, ou par une
   branche choisie) dit d'abord son délai pour son travail, ceux de ses compteurs, puis ceux du chemin principal.
5. **Des sous-titres, phrase par phrase, allumés par défaut** (`model/script.ts`), à la place du cartouche de l'étude
   (lot J10, demande du porteur du projet : « on peut désactiver indépendamment les messages associés aux étapes ou
   relais ou les deux » ; deux préférences du navigateur, `presenterStepMessages` et `presenterFlowMessages`, allumées
   par défaut, un bouton de la barre et une touche chacune, E et R ; coupée, une scène d'étape, ou une scène de
   passage, ne dit rien : ni sous-titre ni voix, phrases de l'application ou commentaire saisi ; l'ouverture et le
   bilan restent dits ; le réglage vaut à tout niveau et se change à tout moment : le présentateur peut conduire la
   carte à la main et parler à sa place, puis rendre la parole au récit) :
   une information par phrase ; chaque modèle de phrase traduit en entier ; durées en toutes lettres par
   `Intl.NumberFormat` (unité longue), listes par `Intl.ListFormat` (trois noms et le nombre des autres) ; une information
   absente fait sauter sa phrase ; une phrase de plus de 84 colonnes coupée après une ponctuation (révision : la
   découpe compte en colonnes, un caractère chinois, japonais ou coréen en prend deux ; elle coupe après une
   ponctuation pleine largeur, jamais dans un caractère ni après l'initiale d'un nom). Le texte est dans la
   langue montrée (ADR 083), la barre dans celle de l'interface. **Essai J2 du porteur (11/11)** : plus de bandeau (il
   répétait la case mise en avant et son couloir) ; une phrase ne dit que ce que la carte n'écrit pas : plus de « Le
   dossier entre ici », « sort ici », « Une décision se prend ici », « Les branches se rejoignent », « attend ici »,
   « Cette étape a sa propre carte détaillée », « Retour en arrière », « Transmis : » (les documents volent à côté de la
   lumière), ni la voie d'une sortie sans part (écrite sur sa flèche), ni le travail que la case montre (sauf une étape
   par séances, dont le compteur ajoute une autre part) ; restent le délai, la valeur, les noms des documents, les
   irritants, les parts d'une décision et la sortie suivie, la règle d'un parallèle ou d'une inclusive, un changement de
   support, un relais, l'ouverture et le bilan. Le sous-titre est posé juste au-dessus de la barre ; il est annoncé
   aux lecteurs d'écran (`aria-live`), sauf pendant que la voix le lit (révision).
6. **Le rythme** (`model/timing.ts`) : une phrase dure le temps de la lire à 15 colonnes par seconde (sous les 17 de
   Netflix : une salle lit de loin en écoutant), 1,6 s au moins, 7 s au plus ; un passage dure son vol (selon la longueur
   de la flèche) ou ses phrases ; vitesses 0,5 à 2. La présentation s'ouvre en pause (point 4) ; Lire ou Espace lance le récit.
7. **La caméra** (`model/camera.ts` ; lot J10, relecture indépendante, S2 : une étape est cadrée avec les flèches qui
   partent d'elle et les étapes où elles mènent, un passage avec ses flèches et leurs bouts, un saut sans flèche par
   son arrivée seule ; l'arrêt suivant n'est cadré que si une flèche éclairée y mène : le parcours complet saute de
   la fin d'une branche à la suivante et revient de loin à une décision, et cadrer les deux bouts sortait l'étape
   montrée de l'écran ; la durée d'un passage est celle de sa plus longue flèche, et chaque flèche éclairée emporte
   ses documents, M6) : une étape est cadrée avec la suivante et la flèche qui les relie, zoom borné à
   1,25 ; une vue qui montre déjà tout ne bouge pas ; le trajet est celui de React Flow (van Wijk et Nuij), de 0,6 à 1,8 s
   selon la longueur, divisé par la vitesse ; la carte ne laisse pas de vide à ses bords quand elle est plus grande que
   l'écran ; coupe nette sous `prefers-reduced-motion`. Un mouvement de la souris ou un changement de vitesse ne
   relance pas le trajet en cours, et la carte déplacée à la main en pause reste où on l'a mise jusqu'au changement
   de scène ou de taille d'écran (révision).
8. **La lumière d'un passage** : un point suit le tracé exact de la flèche (coins arrondis, courbe des retours), le trait
   s'allume derrière lui, les documents transmis voyagent à côté. Dessinée au-dessus des étapes, elle est masquée par
   leurs cases et leur anneau (`<mask>`, essai J2 : son trait et son halo empiétaient sur la case de départ ; essai J3 : il
   partait encore du cadre intérieur, la boîte, au lieu du cadre extérieur, l'anneau de 6 px d'une étape présentée,
   `PRESENTED_RING`, `lightHoles`) : elle naît au bord extérieur.
8 bis. **Les flèches toutes dessinées** (audit 3.2, BUG-01 et BUG-03) : la scène relie les poignées comme le canevas
   (`ConnectionMode.Loose`, toutes les poignées d'une étape sont des sources, B-3.3-01). Celles qui partent de l'étape
   présentée restent nettes (les pastilles d'une décision sont dessus) ; une flèche éclairée prend l'accent, son trait
   de 2,5 et une pointe de l'accent à la taille des autres ; les autres restent lisibles à 0,45 (le 0,22 du calque
   Analyse perd un trait fin sur un projecteur), leurs étiquettes avec elles. Une passerelle est éclairée par le contour
   de son losange, jamais par un cadre autour (BUG-02).
9. **Les repères** (lot J10 : les compteurs ne comptent que les étapes du fil, ni une étape racontée à part ni une
   décision revue ; « Étape i sur N » et les marques de la barre comptent chaque étape une fois) : couloirs nommés à
   gauche (celui de l'étape allumé), compteurs cumulés du travail par dossier et du
   délai en haut (comptés jusqu'à l'étape atteinte, depuis les segments du chemin principal du cœur, le travail par
   dossier du cœur hors de lui ; une étape faite par séances n'ajoute que sa part, que son sous-titre dit après le
   travail d'une séance ; écrits comme les phrases, sans décimale à partir de dix dans leur unité (révision) ; leur
   montée suit l'horloge de l'écran, pas celle du récit, pour
   qu'une étape atteinte en pause, à la télécommande, monte aussi), mini-carte « vous êtes ici »,
   chapitres d'une marque par étape dans l'encre de son couloir (cliquables jusqu'à 18, cible de 24 px, WCAG 2.5.8).
10. **Entrées** : bouton à icône « Présenter » à la marge droite du ruban (comme PowerPoint en haut à droite ; le ruban
    n'a que des icônes, B-2.1-12, et ses zones gardent leur ordre, AR-155), F5 depuis le début, Maj+F5 depuis l'étape
    choisie, un clic sur une étape pendant la présentation. Pas d'entrée au menu d'une étape : il a déjà 22 lignes et
    déborde un écran de 768 px.
11. **Touches** : Espace ou K lecture et pause ; les flèches du clavier : une étape entière en avant ou en arrière, le
    vol entre elles sauté, comme les boutons de la barre (demande du porteur, 10/11 ; au début de l'étape si elle a joué
    plus de 2 s) ; Page suivante, Entrée, N : la phrase suivante, puis la scène suivante (une télécommande révèle ainsi
    tout ce qui est dit, comme les animations de PowerPoint, audit 3.2, UX-03) ; Page précédente, Retour arrière, P : la
    phrase précédente, puis l'étape précédente ; Début et Fin ; B ou point,
    W ou virgule : écran noir ou blanc ; C ou J : sous-titres ; F : plein écran ; plus et moins : vitesse ; 1 à 9 : sortie
    d'une décision ; F5 : recommencer ; Échap : quitter. Quitter le plein écran par le navigateur quitte aussi la
    présentation, comme dans PowerPoint ; le quitter par F la garde ; V : la voix. Révision : U revient à la carte
    parente ; Tab et Maj+Tab parcourent les commandes de la scène (point 2) ; F5 ne recharge jamais la page pendant
    la présentation, même tapé dans un champ, Ctrl+R et Ctrl+F5 restant au navigateur. Tant qu'un commentaire
    s'écrit, ou que la question ou les réglages de la voix sont ouverts, aucune touche ne pilote le récit : Échap
    ferme ce qui est ouvert. **Révision du 12/11/2026 au soir (essai J6 du porteur du projet)** : cet Échap ne quitte
    jamais la présentation. Le défaut trouvé à l'essai : en plein écran, Échap fermait le champ d'un commentaire,
    puis le navigateur quittait le plein écran sur cette même touche (à l'appui, ou à son relâchement une fois le
    clavier rendu), et la sortie du plein écran, ne voyant plus rien d'ouvert, arrêtait la présentation. Trois
    parades (`escapeKey.ts`) : le verrou du clavier (`navigator.keyboard.lock`, Chromium) n'est rendu qu'une fois
    la touche relevée ; une sortie du plein écran qui suit de moins de deux secondes un Échap ayant fermé quelque
    chose garde la présentation, dans sa fenêtre, une seule fois (F rend le plein écran) ; Échap tenu enfoncé
    n'agit qu'une fois, ses répétitions ne font plus rien ; Entrée de même : ses répétitions, après le champ qu'elle
    a fermé, ne font plus avancer le récit, et dans le champ elles n'envoient rien (relectures du 13/11/2026). Quand rien n'est ouvert, quitter le plein écran par le
    navigateur quitte toujours la présentation. Ce qu'un navigateur fait d'Échap en plein écran ne se joue pas dans
    les tests (F-129 de `docs/a-peaufiner.md`) : la règle est prouvée par des tests qui envoient la touche puis
    l'événement du plein écran, dans l'ordre où un navigateur les donne.
12. **La voix**, éteinte par défaut (bouton « Voix », touche V) : elle lit la phrase affichée, et l'horloge attend
    qu'elle ait fini ; seules les voix du poste sont offertes, les voix en ligne après une case cochée qui dit que le
    texte part chez l'éditeur du navigateur (Microsoft pour Edge, Google pour Chrome) ; une voix qui ne répond plus
    libère l'horloge après 8 s au moins ; en pause, elle se tait. Son réglage se ferme par Échap (la présentation
    continue) ou un clic à côté (BUG-06). **Essai J2 du porteur (11/11)** : une phrase n'est dite qu'une fois (l'effet
    dépendait de l'objet de la présentation, refait à chaque rendu : un mouvement de la souris relançait la phrase ; il ne
    dépend plus que du texte, de la voix par son nom, de la vitesse ; `cancel()` seulement quand la voix parle). Au
    premier appui, une question unique quand le navigateur a des voix en ligne : la voix du poste (rien ne sort) ou une
    voix naturelle (le texte part chez Microsoft ou Google) ; la réponse est retenue (`presenterVoiceAsked`). La voix
    allumée cache les sous-titres (la salle les entend ; C les remontre), éteinte elle les rend. Révision : la
    question et les réglages s'ouvrent toujours à la vue (la barre gardée affichée, l'écran noir ou blanc levé), le
    focus sur le panneau et non sur une réponse ; tant qu'ils sont ouverts, seules Échap, qui ferme le panneau, et V
    agissent. Voix allumée, quand aucune voix ne sait dire la langue montrée, les sous-titres s'affichent quel que
    soit leur réglage. **Révision du 12/11/2026 (audit 3.2, F-137, SEC-CRIT-02)** : les voix en ligne ne sont
    offertes que pour un projet dont la classification est autorisée à quitter le poste, la liste des « Paramètres
    de l'IA » et la règle de l'assistant (`AssistantExchanges.Allowed`, côté serveur, qui ne dépend que de la
    classification et de cette liste, pas du connecteur) : la présentation lit au départ le champ `allowed` de l'état
    de l'assistant de la carte, une seule fois (une carte détaillée est du même projet). Lecture pas encore faite,
    impossible ou refusée : voix du poste seulement ; la question n'est pas posée, faute de voix naturelle à proposer
    (V allume la voix du poste), la case « Autoriser les voix en ligne » est éteinte et dit pourquoi, la voix
    lue n'est jamais une voix en ligne, même cochée et choisie avant. Aucune règle dans l'interface : elle applique le
    booléen de l'hôte. Raison : Edge a une stratégie qui coupe ses voix en ligne (`ConfigureOnlineTextToSpeech`),
    Chrome aucune ; la règle de Topogenia est la seule barrière sous Chrome. Elle reste dans le navigateur de la
    personne : l'hôte ne voit pas le texte partir, rien n'est audité. **La voix choisie** (UX-MOY-01) : son nom est
    retenu par le navigateur (`app.presenterVoiceName`, à côté des autres préférences) et repris après un
    rechargement s'il est encore offert, jamais une voix en ligne pour un projet qui ne les permet pas ; sinon la
    première voix de la langue. Répondre « Voix du poste » à la question l'oublie. **Révision du 13/11/2026 (lot
    J10, demande du porteur du projet) : la voix du projet.** Le projet retient une voix par langue (ADR 103 :
    table hors du cœur, routes, paquet 2.4). La présentation les lit à son départ
    (`GET /api/projects/{id}/presentation-voices`) ; illisibles, rien n'est dit et le navigateur décide comme avant.
    La voix qui lit est prise parmi les voix offertes (celles de la langue montrée, les voix en ligne seulement si
    la personne les a autorisées et si la classification du projet le permet), dans cet ordre (`readerOf`) : la
    voix choisie dans les réglages depuis l'ouverture de la carte ; sinon la voix du projet pour la langue montrée,
    retrouvée par son nom et par l'endroit où elle parle ; sinon le nom retenu par le navigateur ; sinon la
    première. Une voix du projet que ce poste n'offre pas à cette personne n'est donc jamais prise : la règle F-137
    tient par construction, sans règle de plus. Le choix dans la liste des réglages vaut pour la séance (gardé par
    l'éditeur de la carte, d'une présentation à la suivante), le navigateur retient son nom, et, pour un animateur,
    le projet la retient pour la langue montrée (`PUT …/presentation-voices/{language}`, version lue en
    If-Match). La ligne changée entre-temps par un autre animateur, les voix sont relues et l'écriture refaite
    par-dessus : le dernier qui choisit l'emporte, un choix de voix ne vaut pas un conflit montré. Les écritures
    d'un même éditeur se suivent une à une et écrivent le dernier choix. Un autre refus est dit dans la scène, et la
    voix choisie lit quand même. Un lecteur ou un contributeur choisit pour lui : rien n'est écrit. La réponse à
    la question du premier usage ne fixe pas la voix du projet : elle dit un genre de voix, pas une voix. Écarté :
    faire primer le nom retenu par le navigateur sur la voix du projet (la demande est que la voix du projet soit
    reprise par défaut à son lancement ; F-173).
13. **Entrer dans une étape détaillée** (F-120, spécification du porteur du 10/11) : un double-clic sur une étape
    détaillée, le bouton « Voir le détail » de la barre (sur une étape détaillée) ou D présente sa carte détaillée, lue
    sans quitter l'éditeur (`MapEditor.read`), depuis son ouverture, lecture ou pause comme avant, ses compteurs à elle ;
    à la fin de son récit, retour à l'étape de la carte au-dessus, à l'arrêt, son récit et ses compteurs retrouvés ;
    « Revenir à la carte parente », ou U, remonte d'un niveau ; de même à chaque niveau (`levels` du magasin) ; Échap
    quitte tout, l'éditeur n'a pas changé de carte. Un clic simple montre l'étape sans y entrer (BUG-04). Révision :
    une carte détaillée qui arrive après que le récit a quitté l'étape n'est pas présentée ; la plongée est
    abandonnée sans message et le geste se refait, si bien que la remontée revient toujours sur l'étape d'où l'on
    est parti.
14. **Les refus se disent dans la scène** (révision du 12/11/2026) : un commentaire refusé, des commentaires ou une
    carte détaillée illisibles s'affichent en haut au centre, sous les compteurs (`PresenterAlert`, `role="alert"`),
    et plus dans les notifications de l'application, que le plein écran cache ; le message s'efface au geste suivant
    ou après 7 secondes, le délai d'une erreur dans l'application.

## Écarts à l'étude du 09/11 (avis retenu par le porteur)

- Parcours lu dans le cœur au lieu d'un algorithme refait dans l'interface (une seule règle).
- Parallèle distinct d'une décision (toutes les branches ensemble).
- Voix : celles du poste par défaut ; les voix en ligne (Edge, Chrome) envoient le texte chez Microsoft ou Google, une
  entreprise peut les couper dans Edge (`ConfigureOnlineTextToSpeech`), pas dans Chrome : seulement sur un choix
  explicite, et seulement pour un projet dont la classification le permet (révision du 12/11/2026, point 12).
- Sous-titres au lieu d'un cartouche, phrases traduites en entier au lieu d'un assemblage.
- F5 recharge la page dans un navigateur : la touche est prise par l'application sur la carte (hors d'un champ), comme
  PowerPoint et Prezi ; Ctrl+F5 et Ctrl+R rechargent toujours.

## Conséquences

- Tests : modèle pur (parcours, script, rythme, lecteur, caméra, tracé, compteurs, lumière), état, scène ; parcours
  Playwright `e2e/presenter.spec.ts` (il compte les flèches dessinées) ; témoins 319 à 346, puis ceux de l'audit 3.2
  (gardés hors du dépôt).
- Audit du 10/11/2026 : six constats vérifiés dans le code ; réels : flèches absentes, cadre autour d'une passerelle,
  flèches trop estompées, clic hors du parcours, second choix après un retour, réglage de la voix ; faux : le sélecteur
  `.step-node` et les variables d'ombre non définies (le test des feuilles de style vérifie déjà chaque variable) ; pas
  des défauts : les pastilles d'une passerelle inclusive (le parcours suit la branche choisie), Échap en plein écran
  (le navigateur le prend ; Échap termine la présentation). Notés pour plus tard : les parts sur les flèches d'une
  décision, des chapitres cliquables au-delà de 18 étapes sans cibles de moins de 24 px.
- 87 textes (94 avec les formes du pluriel du français) dans les 18 langues ; les 16 autres que le français et
  l'anglais écrits contre le vocabulaire de chaque fichier, non relus par une personne de la langue.
- Essai J2 du porteur (11/11) : bandeau retiré, phrases utiles seules, lumière masquée par les cases, voix dite une fois,
  question de la voix, sous-titres cachés pendant la voix, plongée dans une étape détaillée (F-120) ; tests vus rouges
  d'abord, parcours Playwright de la plongée sur l'exemple des congés.
- Révision du 12/11/2026 (relectures 2 et 6) : tests vus rouges puis verts, aucun texte nouveau. Restent à voir à
  l'écran les règles de style (anneau de focus, pointeur, place du message), prouvées par la lecture de la feuille
  seulement, et l'annonce du sous-titre par un lecteur d'écran, qu'aucun test automatique n'entend.
- Révision du 12/11/2026 (audit 3.2) : voix en ligne et classification (F-137), voix choisie retenue (UX-MOY-01),
  compteur sous le commentaire (UX-MOY-04) ; tests vus rouges puis verts (`PresenterVoiceAllowed.test.tsx`,
  `PresenterComment.test.tsx`, `preferences.test.ts`), témoins gardés hors du dépôt ; deux textes
  nouveaux dans les 18 langues (`presenter.voice.online_refused`, `presenter.comment.count`).
- Révision du 13/11/2026 (lot J10, relecture indépendante de l'écran) : couper ou rendre les messages (E, R, leurs
  boutons) pendant une scène la fait repartir de son début quand c'est son texte qui change, et l'horloge ne reporte
  sur la scène suivante que le temps de l'image, jamais le temps déjà joué (`messages.ts`, `model/player.ts`) : le
  récit ne saute plus d'étapes ; une flèche hors du récit en cours (récit parti d'une étape) se montre et se commente
  par un récit repris de l'étape qu'elle quitte (`comments.ts`) ; répondre « Voix naturelle en ligne » à la question
  vaut choix de la séance et passe devant la voix du projet, sans rien écrire (`voiceSwitch.ts`) ; le champ du
  commentaire nomme par son code une étape sans libellé ; l'écriture de la voix du projet est abandonnée après 10 s
  sans réponse de l'hôte, et dite dans la scène ; une réponse de l'hôte plus vieille qu'un texte gardé entre-temps est
  laissée ou redemandée (voix du projet, commentaires). Tests vus rouges puis verts
  (`PresenterMessagesClock.test.tsx`, `PresenterCommentEdges.test.tsx`, `PresenterVoiceProject.test.tsx`,
  `player.test.ts`). Limite gardée : au clavier seul, une seule flèche se commente par passage (F-175). L'aide du
  champ dit « clic sur une étape ou une flèche » dans les 18 langues.
- Révision du 14/11/2026 (lot J10, seconde relecture indépendante, sur la traduction du commentaire d'une flèche et sur
  l'écran) : quand plusieurs flèches partent ensemble d'une étape, la flèche montrée par un clic est celle dont T, ou
  le crayon, ouvre le champ, et celle qu'il rouvre ensuite ; la présentation la retient (`pointed` du magasin) tant que
  la carte est présentée, et un passage qui ne l'éclaire pas prend sa première flèche (`comments.ts`,
  `commentableAt`). Avant, le champ s'ouvrait sur la première flèche du passage, et le texte partait sur elle. Une
  flèche de la scène et son étiquette montrent la main, comme une étape : elles prennent un clic
  (`presenter.css`). Tests vus rouges puis verts (`PresenterCommentEdges.test.tsx`, `presenterStyles.test.ts`).
  Limites gardées : F-180 à F-182.
- Révision du 14/11/2026 (lot J10, troisième relecture indépendante, sur la voix du projet) : le réglage de la voix porte,
  pour un animateur, la case « Voix du projet pour cette langue », cochée quand le projet retient la voix affichée.
  La cocher fait retenir la voix affichée sans en choisir une autre d'abord (une liste ne dit rien quand on rechoisit sa
  ligne : la voix que le navigateur gardait déjà ne pouvait pas être retenue par le projet) ; la décocher retire la
  voix du projet (un nom vide, ADR 103), et la voix affichée continue de lire pour la séance (`forgetVoice`,
  `projectVoices.ts`). Choisir dans la liste garde son effet : le projet retient aussitôt le choix d'un animateur.
  Répondre « Voix naturelle en ligne » à la question dit un genre de voix, pas une voix : quand le projet retient une
  voix en ligne que ce poste propose, c'est elle qui lit ; sinon la première voix naturelle lit pour la séance, comme
  avant (`voiceSwitch.ts`). Tests vus rouges puis verts (`PresenterVoiceProject.test.tsx`) ; un libellé de plus dans les
  18 langues (`presenter.voice.project`).
- Essais de l'interface pour le lot J10, par sujet : le parcours complet (`model/tour.test.ts`,
  `model/tourShapes.test.ts`) ; le commentaire d'une flèche (`PresenterCommentFlow.test.tsx`,
  `PresenterCommentEdges.test.tsx`) ; les messages coupés et l'horloge (`PresenterMessagesClock.test.tsx`,
  `model/player.test.ts`) ; la voix du projet (`projectVoices.test.ts`, `PresenterVoiceProject.test.tsx`, ADR 103) ;
  le libellé d'attente de l'assistant (`waitingLabel.test.tsx`, ADR 098). La ligne d'un document et ses deux boutons
  sont à l'ADR 104.
- Reste pour la suite : une voix libre de qualité livrée avec l'application (étude), l'essai des télécommandes.
