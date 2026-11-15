<!-- 100-langue-d-origine.md -->
<!-- The change of the language of origin of a project (3.2): a language into which every live text is translated becomes the version of origin, each text exchanged with its translation by an operation of the closed core; refused while a text lacks; reversible by the same change; outside the history of the maps -->

# 100 - Changer la langue d'origine d'un projet

Statut : acceptée le 11/11/2026 (3.2), sur demande du porteur du projet : « le projet et son usage est surtout
international, c'est le point fort de Topogenia » ; « échange de l'original et d'une traduction complète, animateur,
réversible ; le verrou de structure hors de la langue d'origine reste ». **Révisée le 12/11/2026**, avant toute
livraison, après une relecture critique : les commentaires de présentation comptent parmi les textes à traduire
(point 6), les historiques d'annulation du projet sont vidés par le changement (point 7 et « Conséquences »), un résumé
qui devient l'original prend la règle d'un original (point 5). **Révisée le 13/11/2026** (lot J10) : les commentaires
de présentation des flèches comptent de même parmi les textes à traduire (points 2 et 6 ; ADR 099, point 21).

## Contexte

Un projet est écrit dans une langue, sa langue d'origine, et traduit dans d'autres (ADR 081, ADR 083). La structure ne
se modifie que dans la langue d'origine ; dans une autre langue, on ne corrige que des traductions. Une équipe qui
reprend un projet écrit dans une autre langue que la sienne travaillait donc dans une traduction verrouillée, sans
pouvoir en faire sa version de travail. La langue d'origine ne changeait pas après la création.

## Décision

1. **Une opération du cœur** (`Topogenia.Domain/Projects/OriginLanguage.cs`) : une langue configurée, autre que la
   langue d'origine du projet, en devient la langue d'origine quand **chaque texte vivant** du projet y est traduit.
   Les textes sont ceux du classeur des traductions (`TranslationCatalog.Texts` : nom du projet, cartes renommées,
   couloirs, étapes, flèches étiquetées, résumés) : la couverture des langues que montre l'interface et le contrôle du
   cœur portent sur le même ensemble.
2. **Refus entier** : tant qu'un texte manque, rien n'est écrit ; `TRADUCTION_INCOMPLETE` (409), avec la langue, le
   nombre de textes manquants et les deux cents premiers (type, identifiant, code). Les commentaires de présentation
   qui manquent (point 6) sont comptés et nommés avec eux, après les textes du cœur : type `presentation_comment`,
   identifiant et code de leur étape ; puis ceux des flèches (ADR 099, point 21) : type `flow_presentation_comment`,
   identifiant de leur flèche, codes de ses deux étapes (`A > B`).
3. **Échange objet par objet**, calculé sur l'état d'avant puis appliqué : le texte de l'objet prend celui de sa
   traduction ; la traduction de la nouvelle langue est retirée (un original n'est jamais sa propre traduction) ;
   l'ancien texte devient la traduction de l'ancienne langue de l'objet, comme une saisie à la main ; la langue
   d'origine de l'objet, puis celle du projet, passe à la nouvelle langue. Les traductions des autres langues restent
   telles quelles. Un texte déjà écrit dans la nouvelle langue ne bouge pas.
4. **Ce qui suit un autre texte le suit encore** : une carte détaillée qui porte le libellé de son étape porte son
   nouveau libellé (sa propre traduction si elle en a une, sinon celle de son étape) ; la carte racine qui porte le nom
   du projet, son nouveau nom. Une carte racine au nom propre, que rien ne liste pour la traduction, garde son nom et
   sa langue. La langue d'une flèche reste celle de son étape source.
5. **Résumés** : le texte d'un résumé s'échange comme les autres ; un résumé à jour avant l'échange le reste (il
   reprend l'empreinte de sa carte, dont les textes ont changé) ; un résumé périmé reste périmé. Un résumé d'origine
   est une phrase sur une ligne, règle que sa traduction n'a pas (une cellule d'un classeur garde ses retours à la
   ligne) : la traduction qui devient l'original la prend (`MapSummaryRules.Sentence`), sinon un paquet exporté
   ensuite serait refusé à l'import.
   Révision du 12/11/2026 (audit) : un résumé dont le nouvel original est une traduction saisie à la main devient
   « écrit à la main », sans modèle, car ce texte n'est pas celui de l'IA ; une traduction automatique garde l'origine
   du résumé (IA et modèle, ou saisie). Le retour vers l'ancienne langue rend l'ancien texte, gardé comme une saisie
   (point 3) : un résumé de l'IA qui fait l'aller et retour revient « écrit à la main ».
6. **Ce qui ne bouge pas** : les textes qui ne se traduisent pas (fiches, documents, irritants, glossaire) et les
   objets à la corbeille gardent leur langue ; restauré, un objet s'affiche « À traduire ». Les **commentaires de
   présentation** n'ont rien à échanger, leur original étant la ligne de la langue d'origine de l'étape, ou de
   l'étape source pour le commentaire d'une flèche (ADR 099, points 13 et 21),
   mais ils sont des textes du projet comme les autres : un commentaire qui a un original et pas de ligne dans la
   langue demandée **manque**, et le changement est refusé avec lui. Sans cela, il resterait sans original : plus dit
   dans la langue d'origine, plus listé par le classeur ni par l'assistant. Les commentaires vivent à côté du cœur :
   c'est la commande de l'application qui les lit dans la même unité de travail et refuse, avant le cœur ; l'écran
   les demande à l'hôte comme le classeur les liste (portée `missing`, langue par langue) et les ajoute au compte de
   la couverture, pour offrir le bouton exactement quand le serveur accepte.
7. **Réversible** : le même changement vers l'ancienne langue rend les textes, texte pour texte (les traductions
   revenues sont des saisies). Ce n'est **pas une opération d'une carte** : hors du journal des opérations, Ctrl+Z ne
   le défait pas. Une fois écrit, il **vide les piles d'annulation et de rétablissement** de toutes les cartes du
   projet, pour tous : aucune opération d'avant n'est rejouée sur des textes qu'elle ne connaissait pas. Un changement
   refusé ne vide rien.
8. **Application et hôte** : commande de projet `ChangeOriginLanguageCommand`, réservée à l'animateur, `If-Match` sur
   la version du projet, événement d'audit `project.origin_language_changed` (ancienne et nouvelle langue) ; route
   `PUT /api/projects/{id}/origin-language` `{ language }`, qui rend le projet relu, comme les autres réglages du
   projet. Un projet à la corbeille est `INTROUVABLE`. Aucune migration.
9. **Interface** : le dialogue « Traductions » liste les langues où le projet est traduit ; une langue entièrement
   traduite propose « Faire de cette langue la langue d'origine », une langue incomplète dit « Textes manquants : N ».
   Une confirmation explique l'effet ; ensuite le projet est montré dans sa nouvelle langue d'origine.

## Conséquences

- Une équipe fait d'une traduction complète sa version de travail, et peut revenir en arrière.
- **Le cœur fermé change** : sa version passe à 3.2.0 ; le paquet du cœur du dépôt public est refait par la procédure
  de découpage (ADR 074) à la publication.
- **Les annulations d'avant le changement ne sont plus possibles** : les historiques du projet sont vidés par le
  changement (`IOperationLog.RemoveOfProjectAsync`, `ProjectCommand.ForgetsOperations`). Le contrôle existant
  (`OPERATION_DEPASSEE` quand le contenu d'un objet a changé) ne suffisait pas : une flèche étiquetée supprimée avant
  le changement n'est pas touchée par l'échange, l'annulation de sa suppression passait et la rendait avec son libellé
  de l'ancienne langue, tenu pour un original de la nouvelle (une flèche n'a pas de langue à elle).
- **Le verrou de la structure hors de la langue d'origine reste une règle de l'interface** : l'API n'a pas de « langue
  montrée » ; une écriture de structure écrit toujours l'original, dans la langue d'origine du projet. Après le
  changement, c'est la nouvelle langue.
- Un paquet exporté après le changement porte la nouvelle langue d'origine et se réimporte avec elle.

## Alternatives écartées

- **Changer seulement la langue déclarée du projet**, sans échanger les textes : les originaux seraient dits dans une
  langue qui n'est pas la leur.
- **Accepter un projet partiellement traduit** : des originaux dans deux langues, sans règle pour dire laquelle se
  modifie.
- **Une opération du journal, annulable par Ctrl+Z** : elle touche tous les objets de toutes les cartes, alors que le
  journal est tenu par carte et par personne.
- **Laisser les historiques d'annulation et compter sur le contrôle existant** (choix du 11/11, abandonné le 12/11) :
  il laissait revenir une flèche étiquetée de l'ancienne langue (voir « Conséquences »).
- **Compter les commentaires de présentation dans la couverture de la vue de carte** : chaque commande qui rend une
  carte devrait lire les commentaires ; le dialogue des traductions les demande seul, quand il s'ouvre.
