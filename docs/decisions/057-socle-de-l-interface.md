<!-- 057-socle-de-l-interface.md -->
<!-- Socle de l'interface : routeur, enveloppe, profil et langues, notifications, clavier à couches, commandes en données, préférences, shared/ui -->

# 057 - Socle de l'interface

Statut : acceptée (date 27/10/2026)

## Contexte

P6-T02 pose ce que tous les écrans de la phase 6 utilisent : la spécification 3.5 (commandes en données, un seul
gestionnaire de clavier à couches, organisation de `web/src`), l'annexe B 0, 1.1, 1.2, 2.5 et 7 (pile, routeur,
enveloppe, raccourcis, préférences), AR-29 (langues de la configuration), AR-30 (routes anglaises), AR-51 (repli sur
le français), AR-28 (clés `app.*`), AR-90 (ruban repliable mémorisé) et AR-106 (conflit de version du profil avec
soi-même). Deux manques sont apparus : l'interface n'avait aucun moyen de connaître les langues de la configuration,
et le code de la 1.x mêlait les raccourcis de la carte dans cinq écouteurs de la fenêtre.

## Décision

### 1. Les langues

1. **Le profil porte les langues de la configuration** : `GET /api/me` (et toute réponse qui rend le profil) ajoute
   `languages`, la liste `Topogenia:Languages:Supported` dans son ordre (repli `fr`, `en`). Le sélecteur de l'en-tête
   en est tiré (AR-29) : ajouter une langue reste « un fichier JSON et une ligne de configuration » (NF12). Écarté :
   une route dédiée (une entrée de plus dans la table des routes, la matrice des droits et le catalogue, pour une
   donnée que le profil, lu au démarrage, porte déjà avec la langue effective).
2. **Un fichier par langue** dans `web/src/shared/i18n/locales/`, chargé par `import.meta.glob` : la liste des
   langues connues de l'interface est celle des fichiers. Une langue configurée sans fichier affiche les textes du
   français (langue de repli d'i18next).
3. **Avant le profil**, la page est dans la première langue du navigateur qu'elle connaît, sinon en français (AR-51 ;
   la 1.x prenait l'anglais). La première lecture du profil part **sans `Accept-Language` imposé** : c'est la langue
   du navigateur qui part, et l'API rend la langue effective (profil, navigateur parmi les langues configurées,
   langue par défaut). Le client applique ensuite cette langue à la page (`lang`, titre tiré de `app.name`) et aux
   requêtes.
4. Le nom du produit n'est écrit qu'une fois par fichier de langue (`app.name`) ; les autres textes l'imbriquent
   (`$t(app.name)`), ce que vérifie la règle d'architecture existante.

### 2. Le profil et le conflit avec soi-même (AR-106)

Le profil est un magasin zustand (`app/profile/profileStore.ts`) fourni par `ProfileProvider`. Un changement envoie les
champs avec la version lue ; un `CONFLIT_VERSION` dont `details.updated_by` est l'utilisateur lui-même est réglé sans
lui : relecture du profil, puis même envoi avec la version relue, une fois. Un conflit d'un autre auteur ou un autre
refus remonte à l'appelant. Le changement de langue est appliqué avant l'envoi et reste appliqué si l'envoi échoue
(B-1.2-05, B-1.2-06).

### 3. Le clavier

1. **Un seul écouteur** sur la fenêtre, en phase de capture (avant React Flow, B-2.5-21), pour les touches pressées et
   relâchées (Espace maintenu, B-2.5-17). Les accords sont normalisés (`Ctrl+Shift+Z`, `F2`, `Space`), `metaKey`
   compte pour Ctrl (B-2.5-35).
2. **Les couches** (S-3.5-02) : un champ de saisie garde ses touches sans inscription ; un élément flottant modal
   (dialogue, menu) prend toutes les touches, seul le dernier ouvert est interrogé ; un panneau non modal est
   interrogé avant le canevas et lui laisse les touches qu'il n'utilise pas (B-2.5-04). Une touche utilisée est
   arrêtée là (pas d'action par défaut du navigateur), une touche inutilisée garde son action par défaut.
3. **Les accords globaux** passent avant les couches, même dans un champ ou sous un élément modal : Ctrl+S, posé par
   l'enveloppe, montre « Tout est enregistré automatiquement, à chaque modification. » sans la fenêtre du navigateur
   (B-2.5-05). La 1.x ajoutait l'emplacement du téléchargement d'une copie ; le menu Fichier de la 2.0 est construit
   en P6-T03, le texte sera complété alors s'il y a lieu.

### 4. Les commandes

Une commande est une donnée (`shared/commands/commands.ts`) : `id`, clé i18n du libellé, icône Lucide, accords (le
premier est montré), condition d'activation et exécution, lues dans le contexte de l'écran (S-3.5-01). Le même tableau
donne la couche du clavier (`keyHandlerOf`, sur la touche pressée seulement ; une commande inactive laisse sa touche au
navigateur, comme la 1.x), les éléments des menus et des barres d'outils (`itemsOf`). Les menus écrivent toujours
`Ctrl`, et le nom des touches qui changent selon la langue (`Maj`, `Suppr`, `Échap`, `Espace`) vient des fichiers de
langue (B-2.5-36).

### 5. Les magasins et les préférences

- zustand 5 en magasins « vanilla » (`createStore`), lus dans React par `useStore` (B-0-02) : notifications
  (`shared/ui/toasts.ts`, une pile pour toute l'application), projet ouvert (`shared/state/openProject.ts`, déclaré par
  l'éditeur, lu par l'en-tête, B-7-13), préférences, profil.
- Préférences d'affichage dans `features/map/state/preferences.ts` (la spec 3.5 les range dans l'état de la carte ;
  l'éditeur de P6-T04 les compose) : `app.showCodes` (`'1'`), `app.alwaysShowPictos` (`'1'`), `app.showRaci` (`'0'`),
  `app.ribbonCollapsed` (`'0'`, AR-90). Tout accès au stockage est protégé ; un stockage qui refuse laisse le
  changement pour la session (B-7-04). Un test garde que ce module est le seul de `web/src` à toucher au stockage du
  navigateur (B-7-05).

### 6. Routeur, enveloppe, page

- Routes anglaises d'AR-30, barre finale tolérée, identifiant en minuscules ; l'écran d'une route est remonté quand
  l'objet qu'elle nomme change (clé `map:<id>`, B-1.1-09). Les écrans de l'accueil, du projet et de la carte arrivent
  avec leurs tâches (P6-T03, P6-T04) ; d'ici là leur route montre un écran vide.
- `index.html` monte dans `#root` (Q1 : identifiants en anglais ; la 1.x montait dans `#racine`), sans titre écrit ;
  `lang` et titre suivent la langue appliquée (B-0-12, B-0-13). L'icône `/favicon.svg` est dans `web/public/` : un
  fichier servi seul ne peut pas lire les variables CSS, il porte donc en clair les valeurs de `--accent`,
  `--accent-strong` et `--surface` (même dessin que `shared/ui/Logo.tsx`, qui lit les jetons).
- Version injectée sous le nom neutre `__APP_VERSION__` (B-0-10 ; la 1.x l'appelait du nom du produit, D13).

### 7. `shared/ui`

`Button`, `Icon`, `Logo`, `Toaster`, `Dialog` (modal, focus donné puis rendu, Tab gardé dedans, Échap ferme même depuis
un champ, couche modale), `Confirm` (bouton de confirmation inactif tant que la condition ne tient pas, pour la
suppression par saisie du code de P6-T03), `Field` (libellé, aide et erreur liés au contrôle, que l'appelant dessine),
`Menu` (240 px, flèches, Entrée, Échap, clic extérieur, couche modale), `Toolbar` (groupes nommés, boutons à icône de
32 px nommés avec leur raccourci, un seul arrêt de tabulation, flèches, Début, Fin). Chaque élément interactif porte un
`data-testid` (S-3.5-04, vérifié par les parcours de P6-T10).

## Conséquences

- La réponse du profil change de forme (champ ajouté) : contrat régénéré (`CONTRACT_WRITE=1`, `npm run api`).
- Les lignes de B-2.5 propres à la carte (Ctrl+K à F2, outils, flèches, Suppr, Échap, saisie de libellé, barre de
  commande) et l'état de l'éditeur de B-7 (outil, type des prochaines étapes, vue, fiche, mode Relier, presse-papiers,
  magasin de la carte, élément flottant, état du canevas) sont rattachés à P6-T04, qui les écrit sur ce socle.
