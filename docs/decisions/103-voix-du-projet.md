<!-- 103-voix-du-projet.md -->
<!-- The voice of a project (3.2): the voice of the browser chosen for a language is kept by the project, one line per project and language beside the closed core, read by every reader of the project and written by a facilitator with If-Match, carried by the package 2.4 with or without the people, purged before its project, outside the history of the maps and the audit; this decision is the server, the screen is in ADR 097 -->

# 103 - La voix du projet

Statut : acceptée le 13/11/2026 (3.2, lot J10), sur demande du porteur du projet : « La voix sélectionnée dans une
langue est mémorisée dans le fichier .topogenia et reprise par défaut au lancement du projet. » Cette décision pose
le côté serveur ; ce que l'écran en fait (quelle voix lit, dans quel ordre, quand il écrit) est dans l'ADR 097,
point 12.

## Contexte

En mode présentation (ADR 097), une voix du navigateur lit les sous-titres. Le choix d'une voix n'était retenu que par
le navigateur de la personne (`app.presenterVoiceName`) : un autre poste, un autre navigateur ou une autre personne
repartait de la première voix de la langue, et un projet remis à quelqu'un par son paquet ne disait rien de la voix
avec laquelle il avait été préparé. Le projet appartient au cœur fermé (`Topogenia.Domain`) : une colonne de plus sur
`project` l'aurait modifié, comme le commentaire de présentation l'aurait fait de la fiche (ADR 099).

## Décision

1. **Une ligne par projet et par langue**, rangée **hors du cœur** comme le commentaire de présentation (ADR 099,
   point 1) : table `presentation_voice` (clé : le projet et la langue), type `PresentationVoice` de l'application,
   port `IPresentationVoiceStore`. Elle porte le **nom** de la voix tel que le navigateur le donne (un prénom, souvent
   suivi de la langue et du pays de la voix), **`local`** (vrai : une voix du poste ; faux : une voix lue en ligne),
   sa version, son dernier auteur et sa date. Une voix n'a ni original ni traduction : chaque langue a la sienne.
   Aucune ligne de `Topogenia.Domain` ne change ; aucune migration existante non plus : une migration nouvelle par
   fournisseur (`PresentationVoices`), qui ne crée que cette table.
2. **Le nom** est un texte saisi (rogné, sans balise ni caractère de contrôle) **d'une ligne** (une fin de ligne ou
   une tabulation est `VALEUR_INVALIDE`), de 200 caractères au plus (`TEXTE_TROP_LONG`), non vide. L'hôte ne le
   compare à aucune liste : les voix dépendent du navigateur et du système de chaque poste, qu'il ne connaît pas.
   **La langue** suit la règle de la langue d'un commentaire (`PresentationCommentRules.Language`) : une langue
   configurée de l'installation, sinon `LANGUE_INVALIDE`.
3. **Deux routes** :
   - `GET /api/projects/{id}/presentation-voices` (lecteur) : un tableau de `{ language, name, local, version }`,
     trié par langue, vide sans voix ; `INTROUVABLE` pour un projet qui n'est pas vivant ou dont la personne n'est pas
     membre, comme les autres lectures du projet.
   - `PUT /api/projects/{id}/presentation-voices/{language}` (animateur), corps `{ name, local }`, avec `If-Match` :
     la version lue de **cette ligne**, 0 quand la langue n'a pas de voix. Un nom vide ou absent **retire** la ligne
     (rien si elle n'existe pas ; `local` n'est alors pas lu). Avec un nom, `local` est obligatoire
     (`CHAMP_OBLIGATOIRE` : un champ absent n'est jamais lu comme « voix du poste » ni comme « voix en ligne »,
     spec 5.4). La même voix déjà gardée (même nom, même `local`) n'écrit rien et ne change pas sa version. Une ligne
     changée entre-temps est `CONFLIT_VERSION` (objet `presentation_voice`, avec l'auteur et la date de la ligne
     gardée) ; sans `If-Match` quand le mode l'exige, `VERSION_REQUISE`. La réponse, `{ voices }`, rend **toutes** les
     voix du projet après le changement.
4. **Dans une unité de travail, le projet verrouillé** : ni la purge du projet ni une autre écriture de ses voix ne
   s'intercale. La voix n'entre pas dans l'historique des cartes (Ctrl+Z ne la défait pas, on la choisit de nouveau)
   et n'écrit aucun événement d'audit, comme le commentaire de présentation ; la version du projet ne bouge pas.
5. **La vie du projet** : ses voix le suivent à la corbeille, pour sa restauration ; entre-temps elles ne sont ni lues
   ni écrites (`INTROUVABLE`). La purge du projet supprime ses voix **avant lui**, dans la transaction qui le
   supprime (`TrashPurge`) ; la purge d'un lot de la corbeille (une étape, une flèche) n'y touche pas.
6. **Le paquet `.topogenia` 2.4** : l'objet `project` de `project.json` porte `presentationVoices`, un tableau de
   `{ language, name, local }` trié par langue, écrit seulement s'il y en a au moins une. Un paquet qui porte une
   voix est écrit en 2.4, comme un paquet qui porte un commentaire ; rien ne change pour un paquet sans voix ni
   commentaire, qui garde le format de son contenu. Schéma `topogenia-package-2.4.schema.json` ; le maillon 2.3 vers
   2.4 ne réécrit rien. L'option d'export « sans les personnes » **garde** les voix : une voix n'est pas une
   personne, et le paquet ne porte ni son auteur ni sa date.
7. **À l'import**, les lignes sont écrites par l'importateur, en version 1, après le projet et dans la même unité de
   travail. Les voix d'une langue que l'installation ne configure pas sont laissées de côté, comme les commentaires
   et les traductions (AR-119). Un nom que la règle refuse est refusé d'abord par le schéma (`PAQUET_INVALIDE`,
   raison `schema`, entrée `project.json`) ; la règle de l'application est le dernier garde : deux voix pour une
   langue ou un nom hors règle, `PAQUET_INVALIDE`, raison `values`, champ `presentationVoices`. L'aperçu d'un import
   valide de même sans rien écrire.
8. **Elles suivent le projet où les commentaires le suivent** : export, import, import en remplacement (le projet
   remplacé part à la corbeille avec les siennes, le nouveau prend celles du paquet), état cible (celles de sa
   source). **Ni le `.pmap` ni « Dupliquer »**, qui passe par lui, ne les portent (ADR 099, point 7).
9. **Jamais envoyées à l'assistant** : la description du projet donnée au modèle est écrite sans ce qui est rangé à
   côté du cœur ; elle ne nomme aucune voix.
10. **Une seule implémentation de ce qui est commun avec le commentaire** : la lecture et l'écriture d'une table
    rangée par sujet et par langue, avec la version lue de chaque ligne (`CommentLineStore`, le projet étant le sujet
    d'une voix) ; le contrôle de la version lue (`RecordVersion.EnsureLine`). Les règles propres aux lignes d'un
    commentaire (original, traductions, source) ne s'appliquent pas à une voix et ne sont pas forcées sur elle.

## Conséquences

- Qui présente un projet après un autre, sur le même poste ou sur un autre qui offre cette voix, l'entend avec la
  voix choisie par son animateur ; l'écran décide (ADR 097, point 12) : une voix que le poste n'offre pas à la
  personne n'est jamais prise, et une voix en ligne gardée par le projet ne lit jamais sans l'accord de la personne
  ni contre la classification du projet.
- Le nom d'une voix dit la famille du navigateur et du système du poste où elle a été choisie, et voyage dans le
  paquet ; il ne dit rien d'une personne (`docs/securite.md`).
- Le contrat de l'API gagne deux routes et trois types (`PresentationVoiceView`, `PresentationVoiceBody`,
  `PresentationVoiceReply`) ; `docs/format-topogenia.md` et le schéma 2.4 changent, et avec eux la consigne donnée au
  modèle de l'assistant, qui les contient.
- Le cœur fermé et sa licence restent tels quels ; la table nouvelle a ses migrations (SQL Server, SQLite), ses
  gardes et sa purge. Le paquet 2.4 n'est lu que par la 3.2 et après ; un projet sans voix ni commentaire reste
  lisible par les versions d'avant.
- **Limites connues.** Une ligne retirée puis récrite repart de la version 1, comme une ligne de commentaire
  (F-131) : sans effet ici, l'écran relit et récrit, le dernier qui choisit l'emporte. Le nom est rogné : une voix
  dont le navigateur donnerait le nom avec une espace au bord ou une tabulation ne serait pas retrouvée ou serait
  refusée (aucune connue). La voix d'une langue retirée ensuite de la configuration reste dans la base et dans le
  paquet, sans route pour la retirer (`LANGUE_INVALIDE`) ; elle n'est plus proposée par l'écran et part avec le
  projet.

## Alternatives écartées

- **Une colonne ou un champ du projet** : le cœur modifié, et une seule voix pour toutes les langues ou un champ
  composite à relire.
- **Un réglage de l'installation ou du profil** : la demande est que la voix suive le fichier du projet, pas le
  poste ni la personne.
- **Une table commune avec les commentaires** (un type d'objet et un identifiant) : plus de clé étrangère vers
  `project`, donc plus de garde de la base sur la purge, et des colonnes (texte, source) sans objet pour une voix.
- **Copier l'écriture versionnée pour la voix** : deux implémentations qui divergent à la première retouche.
- **`local` facultatif** (faux ou vrai par défaut) : une voix en ligne prise pour une voix du poste ferait croire à
  l'écran qu'elle ne sort pas du poste.
- **Un événement d'audit par choix** : le commentaire de présentation n'en écrit pas ; le choix d'une voix n'est ni
  un accès ni un changement de droit.
- **La voix dans le `.pmap`** : comme le commentaire, écarté (ADR 099, point 7).

## Vérification

`PresentationVoicesTests` (règles et requêtes), `PackageImportTests` et `PackageExportTests` (import, aperçu,
remplacement, export), `PackageVoiceTests` (écriture, lecture et schéma du paquet), `PresentationVoiceStoreTests`,
`ForeignKeyTests`, `CheckConstraintTests`, `ColumnTests`, `RoleTests` et `SqliteRebuildTests` (table, gardes, purge,
sur les deux bases), `PresentationVoiceRoutesTests` et `VoiceRoundTripTests` (routes et allers-retours par l'hôte, sur
les deux bases), `RouteTableTests`, `RightsMatrixTests`, `IfMatchCoverageTests`, `ApiContractTests`.

Côté écran : `projectVoices.test.ts` (les voix du projet lues et écrites par l'hôte) et
`PresenterVoiceProject.test.tsx` (le réglage de la voix et sa case « Voix du projet pour cette langue », ADR 097).
