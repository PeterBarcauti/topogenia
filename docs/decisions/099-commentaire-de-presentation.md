<!-- 099-commentaire-de-presentation.md -->
<!-- The comment of presentation of a step (3.2): typed during the presentation without opening the sheet, said instead of the sentences of its step in the language shown, one line per language (its original and its translations), kept beside the steps outside the closed core, translated by the assistant and by the workbook, carried by the package 2.4, and by the .pmap 1.5 since ADR 105; and the same comment on a flow, said along the flow, by the same rules, translated by the same three ways (revision of 13/11/2026) -->

# 099 - Le commentaire de présentation d'une étape

Statut : acceptée le 11/11/2026 (3.2), sur demande du porteur du projet : « ajouter un champ commentaire pour la
présentation ; en présence d'un texte saisi, la présentation le lit et l'affiche », puis « je veux pouvoir saisir très
rapidement un texte de commentaire associé à une étape sans avoir à ouvrir la fiche », « sans toucher au cœur »,
porté par le `.topogenia`, pas par le `.pmap`. **Révisée le 11/11/2026**, avant toute livraison : « une traduction du
projet traduit les commentaires s'ils existent » ; le commentaire en une seule langue de la première rédaction devient
un commentaire traduit (points 1, 3, 4, 6, 8 à 10). **Révisée le 12/11/2026**, avant toute livraison, après deux
relectures critiques : un texte vide ne supprime rien quand l'étape n'a pas d'original (point 3) ; l'écriture dans
la scène et Entrée sans changement (point 2) ; la relecture à l'ouverture du champ et sa limite connue (point 5).
**Révisée le 12/11/2026 au soir**, à l'essai du porteur du projet (essai J6), avant toute livraison : quitter le champ
garde le texte, le champ suit l'étape montrée, et Échap ne quitte jamais la présentation champ ouvert (point 2).
**Révisée le 13/11/2026** (lot J10), sur demande du porteur du projet : une flèche a elle aussi son commentaire, dit
pendant le passage d'une étape à la suivante (section « Révision du 13/11/2026 », points 12 à 21) ; il se traduit comme
celui d'une étape, par l'assistant, par le classeur, et compte au changement de langue d'origine (points 19 à 21).

## Contexte

La présentation (ADR 097) dit des phrases écrites par l'application à partir des chiffres de la carte. Un animateur
veut parfois dire autre chose : le contexte d'une étape, ce qui compte pour la salle. La fiche d'une étape appartient
au cœur fermé (`Topogenia.Domain`) : un champ de plus l'aurait modifié, et l'ouvrir pendant une présentation est lent.
Le projet se présente dans plusieurs langues : un commentaire dit dans une seule langue se tairait dans les autres.

## Décision

1. **Une ligne par étape et par langue** : un texte de 500 caractères au plus (une à trois phrases), sans balise, dans
   une langue de la configuration, avec sa **source** (saisie à la main, assistant, classeur), sa version, son dernier
   auteur et sa date. La ligne de la **langue d'origine de l'étape** est l'**original** ; les autres sont ses
   **traductions**, comme pour les textes du cœur. Les lignes sont rangées **hors du cœur** : table
   `presentation_comment` (clé : l'étape et la langue), lue et écrite par un port de l'application
   (`IPresentationCommentStore`), comme les réglages de l'assistant. Aucune ligne de `Topogenia.Domain` ne change : sa
   table des traductions et sa liste fermée d'objets traduits restent telles quelles.
2. **Saisi dans la présentation** : le crayon de la barre noire, ou la touche T, ouvre un champ à la place des
   sous-titres, la présentation en pause sur l'étape montrée (sur un passage, l'étape qu'il quitte). Entrée enregistre,
   Maj+Entrée va à la ligne, Échap ferme le champ sans quitter la présentation. Le crayon et T ne sont offerts qu'à
   l'animateur ; un lecteur et un contributeur entendent le commentaire. Révision du 12/11/2026 : Entrée sur un texte
   laissé tel qu'il était n'écrit rien et ferme le champ (une traduction automatique relue sans changement reste
   automatique) ; après l'enregistrement, l'étape reprend à la première phrase du commentaire, en pause ; tant que le
   champ est ouvert, aucune touche ne pilote la présentation ; Échap ferme le champ où que soit le focus, qui revient
   au crayon ; un refus est dit dans la scène (ADR 097, point 14), le champ restant ouvert avec son texte.
   **Révision du 12/11/2026 au soir (essai J6 du porteur du projet)**, trois règles :
   - *Quitter le champ garde le texte.* Échap, le crayon et « Quitter la présentation » enregistrent d'abord le texte
     s'il a changé, comme Entrée : rien n'est perdu par réflexe (la pratique des notes de PowerPoint, de Visio et de
     Miro). Le texte tapé est gardé par l'état de la présentation, pas par le champ, pour que tout geste qui quitte
     le champ passe par la même porte (`afterWritten`).
   - *Le champ suit l'étape*, comme le volet des notes suit la diapositive : champ ouvert, un clic sur une étape de
     la carte, sur un repère du parcours, ou sur « Étape précédente », « Étape suivante » ou « Revenir au début »
     enregistre le texte, passe à l'étape et ouvre le champ sur elle, le curseur dedans ; là où la scène atteinte n'a
     pas d'étape (l'ouverture, la fin), le champ se ferme. « Lire », le choix d'une sortie de décision, la plongée
     dans un détail et la remontée restent sans effet champ ouvert. L'étape commentée repart de son début avant le
     déplacement, comme après un texte enregistré : « Étape précédente » la quitte donc aussitôt, quoi qu'elle ait
     joué (relecture du 13/11/2026).
   - *Un texte refusé ne retient personne.* Si l'hôte refuse le texte, rien ne bouge : le refus est dit, le texte
     reste dans le champ, le curseur y revient. Entrée renvoie le texte (après un conflit, par-dessus la ligne relue) ;
     un geste qui quitte le champ (Échap, le crayon, la sortie, un clic qui change d'étape), refait sur ce texte
     refusé, y renonce. Relecture du 13/11/2026 : cela ne vaut que tant que le refus est affiché (sept secondes) ; le
     message effacé, ce geste renvoie d'abord le texte, pour qu'un refus ancien ne fasse rien perdre sans le dire. Un
     hôte qui n'a pas répondu en dix secondes compte comme un refus, dit comme une absence de réponse : le champ
     reprend la main.
     Seconde relecture du 13/11/2026, quatre précisions. *Le conflit fait exception* (point 5) : la ligne relue est
     le texte d'un autre, que seul Entrée écrase, en connaissance de cause ; un geste qui quitte le champ n'envoie
     donc rien après un conflit : message affiché et texte tel quel, il y renonce ; message effacé ou texte retouché,
     il redit le conflit, et le même geste refait y renonce. *La sortie attend comme les autres gestes* : pendant
     qu'un texte est en route, « Quitter la présentation » ne quitte pas (quitter aussitôt, essayé le même jour,
     laissait sans message un refus arrivé après la sortie) ; l'attente est bornée par les dix secondes de l'envoi.
     *Un envoi abandonné ne prend rien d'une réponse tardive* : à sa limite de temps la requête est annulée, et une
     réponse qui arriverait quand même ne dit rien et ne touche pas au récit. *La réponse ne va qu'au champ qui a
     envoyé*, jamais à un autre ouvert depuis sur la même étape.
     Troisième relecture du 13/11/2026, trois précisions. *Renoncer demande d'avoir pu voir le refus* : un geste qui
     quitte le champ ne renonce au texte que si le refus est affiché depuis une seconde au moins ; plus tôt, c'est le
     second clic d'un double clic ou une touche tapée deux fois par réflexe, et rien ne bouge. *Le curseur revient
     dans le champ* quand un geste du pointeur n'a rien fait ou a redit un conflit : le bouton cliqué l'avait pris,
     et Entrée ne serait allée nulle part. Pas quand le clavier parcourt les commandes (Tab) : le bouton qu'une
     touche a pressé garde alors le focus, après un refus comme après un geste sans effet, pour que la même touche
     refaite soit le même geste et renonce ; ramenée dans le champ, elle aurait envoyé le texte, par-dessus celui
     d'un autre après un conflit (quatrième relecture). *Un refus est dit au moment où le champ reprend la main* : le message
     d'un conflit a pu s'effacer pendant la relecture des commentaires. Limite gardée (F-166) : un envoi abandonné à
     ses dix secondes que l'hôte avait quand même enregistré laisse la présentation en retard d'une ligne jusqu'à
     la relecture suivante, et l'envoi suivant reçoit un conflit contre le propre texte de la personne.
   Échap ne quitte jamais la présentation champ ouvert, quoi que le navigateur fasse du plein écran : ADR 097,
   point 11. Une touche d'une saisie en cours de composition (méthode de saisie) reste à la composition.
3. **La langue montrée décide de ce qu'on écrit** : dans la langue d'origine, T écrit l'original ; dans une autre
   langue, la traduction de cette langue, l'original intact et montré au-dessus du champ, qui dit la langue écrite. Un
   texte vide supprime ce qu'il remplace : l'original, avec toutes ses traductions ; une traduction, elle seule ; et
   rien quand l'étape n'a pas d'original (une traduction saisie dans une autre langue reste : révision du 12/11/2026). Un
   original réellement changé fait tomber ses traductions automatiques (assistant, classeur) et garde les saisies,
   comme un texte du cœur. Une traduction peut exister sans original : elle se dit dans sa langue.
4. **Dit à la place des phrases de l'étape** : la présentation dit la ligne de la langue montrée, chaque ligne écrite
   étant une phrase dite et sous-titrée, coupée comme toute phrase ; sans ligne dans cette langue, l'étape garde ses
   phrases automatiques.
5. **Deux routes** : `GET /api/maps/{id}/presentation-comments` (lecteur) rend toutes les lignes des étapes vivantes de
   la carte, lues au début de la présentation et à chaque détail ouvert ; `PUT /api/steps/{id}/presentation-comment`
   (animateur) écrit la ligne d'une langue, avec `If-Match` : la version de **cette ligne** lue, 0 sans ligne ; une
   ligne changée entre-temps est `CONFLIT_VERSION`. La réponse rend les lignes de l'étape après l'écriture. Le
   commentaire n'entre pas dans l'historique de la carte : Ctrl+Z ne le défait pas, on le récrit. Révision du
   12/11/2026 : le champ relit les commentaires de la carte à son ouverture ; tant que rien n'est tapé, il montre le
   texte à jour et enverra sa version ; un texte déjà tapé reste, avec la version de la ligne que le champ a montrée.
   Après un conflit, les commentaires sont relus, et un second Entrée enregistre par-dessus, en connaissance de
   cause. **Limite connue** (F-131 de `docs/a-peaufiner.md` (non publié)) : une ligne supprimée puis récrite repart de la
   version 1 ; si l'on tape avant que la relecture d'ouverture soit arrivée, et qu'un autre animateur a entre-temps
   supprimé puis récrit ce commentaire, la ligne a le même numéro de version et un autre texte : l'hôte ne voit pas
   de conflit, et le texte de l'autre est remplacé sans message.
6. **La vie de l'étape** : le commentaire suit son étape d'une carte à l'autre (sa clé commence par l'étape) ; il reste
   quand l'étape va à la corbeille, pour sa restauration ; la purge supprime toutes ses lignes avant l'étape.
7. **Pas dans le `.pmap`** (choix du porteur du projet) : une copie `.pmap`, et « Dupliquer » qui passe par elle, n'ont
   pas les commentaires. **Révisé le 16/11/2026 par l'ADR 105**, décision du porteur du projet : le `.pmap` 1.5 porte les
   commentaires dans une copie entière (copie d'un projet, copie d'une carte, duplication).
8. **Traduit par l'assistant** (« Traduire en … », ADR 083) : l'invite donne au modèle, à côté de la carte, les
   commentaires de l'arbre de la carte qui ont un original et aucune ligne dans la langue demandée (300 au plus par
   échange) ; la réponse les rend dans un champ à part, `comments` (`{ step, text }`), validé par le schéma de réponse
   et relu par l'application comme toute donnée. L'aperçu les montre avec les autres textes (genre
   `presentation_comment`) ; « Appliquer » les écrit **dans la même unité de travail** que les traductions de la carte,
   avec la source `assistant`, jamais sur une traduction saisie à la main ; « Écarter » n'écrit rien. Une traduction
   dont l'original a changé depuis la réponse est laissée de côté.
9. **Traduit par le classeur** : le classeur porte une ligne par commentaire qui a un original (type
   `presentation_comment`, identifiant : l'étape), après les textes de la carte, filtrée par les trois portées comme
   eux. À l'import, les mêmes raisons d'ignorer une ligne que pour les textes du cœur, la même borne de 20 000 lignes
   sur tout le fichier, et le même refus du fichier entier pour une traduction invalide, les erreurs des textes et des
   commentaires réunies. Les commentaires sont écrits **dans la même unité de travail** que les traductions du cœur,
   avec la source `workbook` ; seule la portée « Tout » remplace une traduction saisie.
10. **Le gabarit de commande écrit à côté du cœur** : pour que l'import du classeur et « Appliquer » n'écrivent jamais
    en deux unités, une commande peut lire les enregistrements rangés hors du cœur après le chargement de l'espace de
    projet (`ReadBesideAsync`), et les écrire après son jeu de changements, avant le journal (`WriteBesideAsync`), dans
    la même unité et sous le verrou du projet ; une requête de lecture a la même lecture. Aucune commande existante ne
    change : les deux étapes sont vides par défaut.
11. **Le paquet `.topogenia` 2.4** : une étape vivante porte l'original de son commentaire (`presentationComment` :
    `text`, `language`) et ses traductions (`presentationCommentTranslations` : `language`, `text`, `source`), une
    traduction sans original comprise. Le format 2.4 n'est écrit que si une étape en porte ; sinon le paquet reste en
    2.3, que lit la 3.1. Schéma `topogenia-package-2.4.schema.json` ; le maillon 2.3 vers 2.4 ne réécrit rien. À
    l'import, chaque ligne est écrite par l'importateur, en version 1 (le paquet ne porte pas son auteur) ; celle d'une
    langue que l'installation ne configure pas est laissée, comme les traductions (AR-119) ; deux lignes d'une étape
    dans une même langue refusent le paquet. Un état cible reprend les commentaires de sa source et leurs traductions.

## Révision du 13/11/2026 (lot J10) : le commentaire d'une flèche

Demande du porteur du projet : pendant une présentation, la lumière va d'une étape à la suivante le long d'une flèche ;
un texte saisi pour cette flèche est dit à la place de la phrase automatique du passage. Cette révision pose le côté
serveur (points 12 à 17), puis la traduction du commentaire d'une flèche par les trois chemins de celui d'une étape
(points 19 à 21) ; la saisie dans la présentation vient ensuite (point 18).

12. **Une ligne par flèche et par langue**, comme pour une étape (point 1) : le même texte (500 caractères au plus,
    sans balise, rogné, ses fins de ligne ramenées à `\n`), une langue de la configuration, la même source, sa version,
    son dernier auteur et sa date. Les lignes sont rangées hors du cœur, dans leur propre table,
    `flow_presentation_comment` (clé : la flèche et la langue), à côté de `presentation_comment` : type
    `FlowPresentationComment`, port `IFlowPresentationCommentStore`. La table des étapes et sa migration ne changent
    pas, des bases déjà migrées les ont : une migration nouvelle par fournisseur (`FlowPresentationComments`).
13. **L'original est la ligne de la langue d'origine de l'étape source.** Une flèche n'a pas de langue à elle : son
    libellé suit déjà la langue de son étape source (ADR 100, point 4), son commentaire aussi. Toute autre ligne est une
    traduction. Les règles du point 3 valent telles quelles : un texte vide supprime ce qu'il remplace (l'original,
    avec toutes ses traductions ; une traduction, elle seule ; rien quand la flèche n'a pas d'original) ; un original
    réellement changé fait tomber ses traductions automatiques et garde les saisies ; le même texte saisi n'écrit rien ;
    une traduction peut exister sans original.
14. **Une seule implémentation des règles.** Elles sont écrites une fois (`CommentLineRules`) sur un *sujet*
    (`CommentSubject`) : un identifiant et une langue d'origine. Une étape est son propre sujet ; une flèche prend la
    langue d'origine de son étape source. `PresentationCommentRules` (étape) et `FlowPresentationCommentRules` (flèche)
    n'en sont que les entrées ; le comportement du commentaire d'une étape et ses tests ne changent pas. La base suit
    le même principe : une seule écriture (`CommentLineStore`) pour les deux tables.
15. **Deux routes**, celles du point 5 pour une flèche : `GET /api/maps/{id}/flow-presentation-comments` (lecteur) rend
    toutes les lignes des flèches vivantes de la carte, par flèche puis par langue ;
    `PUT /api/flows/{id}/presentation-comment` (animateur) écrit la ligne d'une langue, corps `{ text, language }`, avec
    `If-Match` : la version de cette ligne lue, 0 sans ligne. Mêmes refus que pour une étape : `VERSION_REQUISE` sans
    `If-Match` quand le mode l'exige, `CONFLIT_VERSION` (objet `flow_presentation_comment`) sur une ligne changée
    entre-temps, `LANGUE_INVALIDE`, `TEXTE_TROP_LONG`, `TEXTE_HTML_INTERDIT`, `ACCES_REFUSE`, `INTROUVABLE`. La réponse
    rend les lignes de la flèche après l'écriture, chacune avec `flow_id`. Le projet est verrouillé pendant l'écriture.
    Le commentaire n'entre pas dans l'historique de la carte : Ctrl+Z ne le défait pas. Les deux listes restent
    séparées : la route des étapes et son contrat ne changent pas.
16. **La vie de la flèche** : le commentaire suit sa flèche (sa clé est la flèche) ; relibellée, inversée ou tracée
    autrement, elle le garde. Il reste quand la flèche va à la corbeille, seule ou avec une de ses étapes, pour sa
    restauration ; entre-temps il n'est ni lu ni écrit (`INTROUVABLE`, comme pour une étape supprimée). La purge
    supprime ses lignes avant la flèche, que celle-ci parte seule, avec son étape ou avec le projet. Une flèche
    inversée change d'étape source : son original devient la ligne de la langue d'origine de la nouvelle source, la
    même langue dans un projet écrit dans une seule. Une flèche créée par une autre opération (collage, recouture
    après la suppression d'une étape) est une autre flèche : elle n'a pas de commentaire. Il en va de même de trois
    opérations qui remplacent une flèche par d'autres : **l'insertion d'une étape sur une flèche** (la flèche part à
    la corbeille avec son commentaire, les deux flèches neuves n'en ont pas, et l'annulation rend la flèche avec son
    commentaire), **le regroupement en sous-carte** et **la remontée d'un détail**, dont les flèches recréées sont
    d'autres flèches. Le commentaire de la flèche d'origine n'est pas reporté sur celles qui la remplacent, alors que
    le cœur y reporte son libellé, sa part et les traductions du libellé : limite connue, tracée F-174 dans
    `docs/a-peaufiner.md` (non publié). La création d'une flèche annulée (Ctrl+Z) alors qu'elle porte un commentaire n'est pas
    refusée : le commentaire attend avec la flèche, et la rétablir le lui rend. Ces comportements sont fixés par
    `FlowCommentRoutesTests` (création annulée puis rétablie, inversion entre deux étapes de langues d'origine
    différentes, insertion d'une étape).
17. **Le paquet `.topogenia` 2.4** (pas encore publié) : une flèche vivante porte `presentationComment` et
    `presentationCommentTranslations`, les clés et les définitions du schéma d'une étape (point 11) ; le format 2.4
    est écrit dès qu'une étape ou une flèche en porte. À l'import, les mêmes règles : chaque ligne écrite par
    l'importateur, en version 1 ; celle d'une langue non configurée laissée ; deux lignes d'une flèche dans une même
    langue refusent le paquet ; la ligne de la langue d'origine de l'étape source est l'original, toujours une
    saisie. Elles sont écrites après les flèches, dans la même unité de travail. Un état cible reprend les commentaires
    des flèches de sa source ; un remplacement par un paquet les écrit sur le nouveau projet. Le `.pmap` 1.5 et
    « Dupliquer » les portent depuis la 3.3 (point 7 révisé, ADR 105).
18. **Fait dans le lot J10**, plus rien ne reste : la saisie et la lecture dans la présentation (ADR 097) et la
    traduction (points 19 à 21) ; la limite notée ici dans la première rédaction de cette révision est **levée**. Le changement de langue
    d'origine (ADR 100, point 6) compte désormais les commentaires des flèches parmi les textes manquants (point 21) :
    un commentaire de flèche ne reste plus sans original après un changement, et le refus vient avec les deux moyens de
    le traduire, l'assistant et le classeur.
19. **Traduit par l'assistant**, comme au point 8. « Traduire en … » demande aussi les commentaires des flèches
    vivantes de l'arbre de la carte qui ont un original et aucune ligne dans la langue demandée. Le plafond de 300 par
    échange vaut pour le **total** : les étapes d'abord, dans l'ordre de lecture, puis les flèches dans ce qu'elles
    laissent (par étape source, puis par étape cible, carte par carte) ; le reste part à l'échange suivant. Une flèche
    n'a pas de code : dans la liste `<comments>` de l'invite et dans le champ `comments` de la réponse, son entrée
    porte dans `step` les codes de ses deux étapes, `SOURCE > CIBLE`, la référence que le classeur et l'aperçu donnent
    déjà à une flèche. La réponse reste une donnée jamais crue : chaque flèche une fois, les espaces autour du signe
    indifférents, une flèche écrite de la source vers la cible reconnue quel que soit le signe (`->`, `=>`, `→`, le
    signe rendu en séquence d'échappement), une flèche inconnue, dans l'autre sens ou sans flèche laissée de côté
    (un texte n'est jamais écrit sur une flèche devinée), un texte trop long coupé à un mot comme pour une étape. Le mode sans connexion (consigne copiée, réponse collée) passe par le même lecteur. La vue rendue à
    l'interface garde les deux listes séparées (point 15) : `comments` (inchangée) et `flow_comments` (`flow_id`,
    `source_code`, `target_code`, `original`, `text`) ; l'aperçu et « Appliquer » les reçoivent de même (`comments`,
    `flow_comments` : `flow_id`, `original`, `text`), avec les mêmes refus (`VALEUR_INVALIDE` sur `flow_comments` sans
    langue de traduction, pour une flèche nommée deux fois ou au-delà de mille entrées ; texte invalide). L'aperçu les
    montre après ceux des étapes, genre `flow_presentation_comment`, identifiant : la flèche, code : `A > B`, celui du
    libellé d'une flèche ; « Appliquer » les écrit dans la même unité de travail, avec la source `assistant`, jamais
    sur une traduction saisie à la main ; une traduction dont l'original a changé depuis la réponse est laissée de
    côté. **Le schéma de la réponse envoyé aux fournisseurs ne change pas** : il est épinglé par son empreinte et ne
    change qu'après un essai réel contre le service ; c'est ce qui a fait écarter un champ à part ou une entrée
    `{ source, target, text }` (voir « Alternatives écartées »).
20. **Traduit par le classeur**, comme au point 9. Le classeur porte une ligne par commentaire de flèche qui a un
    original : type `flow_presentation_comment`, identifiant : la flèche, code et contexte : ceux de la ligne du
    libellé d'une flèche (`A > B` par les codes, puis par les libellés de ses deux étapes). Ces lignes viennent après
    celles des commentaires des étapes, dans l'ordre de lecture des flèches, filtrées par les trois portées. La clé
    cachée (`type:id`) distingue le commentaire d'une flèche de son libellé, qui ont le même identifiant. À l'import,
    les mêmes raisons d'ignorer une ligne (une flèche qui n'est plus vivante entre deux étapes vivantes, ou sans
    original, est `OBJET_INTROUVABLE`), la même borne de 20 000 lignes sur tout le fichier, le même refus du fichier
    entier, les erreurs des textes et des deux commentaires réunies ; l'écriture dans la même unité de travail, source
    `workbook`. La liste rendue par l'hôte garde les deux listes séparées : `comments` et `flow_comments`.
21. **Compté au changement de langue d'origine** (ADR 100, points 2 et 6) : un commentaire de flèche qui a un original
    et pas de ligne dans la langue demandée manque, et le changement est refusé avec lui, `TRADUCTION_INCOMPLETE` :
    type `flow_presentation_comment`, identifiant de la flèche, code `A > B`, après les textes du cœur et les
    commentaires des étapes. Après le changement, l'original du commentaire d'une flèche est sa ligne dans la nouvelle
    langue, celle de son étape source. `BesidePorts` porte les deux ports, `IPresentationCommentStore` et
    `IFlowPresentationCommentStore` : une commande lit et écrit les deux commentaires dans son unité de travail.

Comme les règles d'un commentaire (point 14), sa traduction est écrite une fois : `CommentLineRules` (ce que devient
une traduction proposée par l'assistant, ce qu'elle écrit) et `CommentRows` (les lignes du classeur : portée, plan de
l'import, écritures), sur un sujet ; `PresentationCommentRules` et `CommentWorkbook` pour l'étape,
`FlowPresentationCommentRules` et `FlowCommentWorkbook` pour la flèche n'en sont que les entrées.

Conséquences de cette révision : le contrat de l'API gagne deux routes et une vue (`flow_id` à la place de `step_id`),
puis, avec la traduction, trois champs `flow_comments` (la liste du classeur, la réponse de l'assistant, le corps de
l'aperçu et de l'application) ; `docs/format-topogenia.md` et le schéma 2.4 changent, et avec eux la consigne donnée
au modèle de l'assistant, qui les contient ; la consigne de la traduction dit comment nommer une flèche. Dans
l'interface, les commentaires des étapes et des flèches sont comptés ensemble (« Commentaires de présentation : N »,
textes manquants d'une langue) ; la relecture d'une traduction marque celui d'une flèche « commentaire de relais » à
côté des codes de ses deux étapes, et le classeur le nomme « Commentaire de relais ». Alternatives écartées : **une seule table pour les deux commentaires** (un type d'objet et un identifiant :
plus de clé étrangère vers `step` ni vers `flow`, donc plus de garde de la base sur la purge, et une table déjà
migrée à modifier) ; **copier les règles pour la flèche** (deux implémentations qui divergent à la première
retouche) ; **rendre les commentaires des flèches par la route des étapes** (un contrat changé sous l'interface déjà
à l'essai) ; **un original dans la langue du projet** (le commentaire suivrait une autre langue que le libellé de sa
flèche) ; **nommer une flèche dans la réponse du modèle par un champ à part (`flow_comments`) ou par une entrée
`{ source, target, text }`**, comme le `.pmap` écrit une flèche (un schéma de réponse nouveau envoyé aux fournisseurs,
alors qu'un schéma ne change qu'après un essai réel contre le service : la référence `SOURCE > CIBLE` dans le champ
`step` existant garde le schéma tel quel, à l'octet près) ; **copier pour la flèche le plan du classeur ou celui de
l'assistant** (deux implémentations qui divergent à la première retouche).

## Conséquences

- La présentation dit les mots de l'animateur, saisis en quelques secondes, sans sortir du plein écran, dans chaque
  langue où le projet se présente. Champ ouvert, l'animateur commente tout un processus d'un trait : il écrit, clique
  l'étape suivante, écrit (révision du 12/11/2026 au soir).
- Échap n'annule plus une saisie : pour revenir au texte d'avant, on le récrit ou on défait la frappe dans le champ
  (Ctrl+Z du navigateur) avant de le quitter.
- Le cœur fermé, son schéma de fiche et sa licence restent tels quels ; la table nouvelle a ses migrations (SQL Server,
  SQLite), ses gardes et sa purge.
- Le paquet 2.4 n'est lu que par la 3.2 et après ; un paquet sans commentaire reste lisible par la 3.1.
- Les textes des commentaires à traduire partent chez le fournisseur de l'IA avec le reste de la carte, aux mêmes
  conditions (assistant allumé, classification du projet autorisée, sur demande d'une personne) : `docs/securite.md`.
- **Ctrl+Z après « Appliquer »** défait les traductions de la carte, pas celles des commentaires, qui sont hors du
  journal des opérations comme le commentaire lui-même ; elles restent à jour (elles tombent si leur original change)
  et une traduction saisie à la main les remplace.
- Le pré-remplissage des commentaires par l'IA viendra plus tard.

## Alternatives écartées

- **Un champ de la fiche** : le cœur modifié, et la fiche à ouvrir pendant la présentation.
- **Un commentaire dans une seule langue** (première rédaction de cette décision) : il se taisait dans les autres
  langues, alors que le projet se présente d'abord à l'international.
- **Un type d'objet de plus dans les traductions du cœur** : le cœur modifié pour une donnée qu'il ne connaît pas.
- **Deux requêtes pour « Appliquer » ou pour l'import** (la carte, puis les commentaires) : un échec de la seconde
  laisserait une traduction à moitié écrite.
- **Le commentaire dans le `.pmap`** : écarté par le porteur du projet ; repris le 16/11/2026 (ADR 105).
