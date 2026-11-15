<!-- 062-annuler-commande-et-barre-de-l-editeur.md -->
<!-- Annuler et rétablir, barre de commande Ctrl+K, barre de l'éditeur dans l'en-tête, menu Fichier, dernier projet ouvert (P6-T04 lot 3) -->

# 062 - Annuler, barre de commande et barre de l'éditeur

Statut : acceptée (date 28/10/2026)

## Contexte

Le lot 3 de P6-T04 ferme l'éditeur de carte (`plan.md`, phase 6 ; ADR 060 § 9, ADR 061) : annuler et rétablir côté
interface (annexe B 2.6, AR-05, AR-108), bouton Commande et barre Ctrl+K (B 2.13, B-2.5-06, 33, 34), barre de l'éditeur
dans l'en-tête de l'application (fil d'Ariane, Fichier, Membres, B-2.1-01 à 06), menu Fichier (B-2.4-52 à 58) sur les
dialogues de P6-T03 (ADR 058 § 8), dernier projet ouvert mémorisé par le profil (AR-49). Référence 1.x : `historique.ts`,
`magasin.ts` (`annuler`, `retablir`), `EditeurCarte.tsx` (barre de titre, menu Fichier, clavier), `Fenetres.tsx`
(`BarreCommande`, `FilAriane`) ; rendu : la maquette validée (en-tête de l'éditeur, zones Liens et Édition).

## Décision

1. **Annuler et rétablir** (`state/actions/history.ts`) passent par la file des écritures (B-2.6-09). Les boutons de la
   zone Édition suivent l'historique de l'hôte et sont grisés pendant une annulation ou un rétablissement en cours
   (`replaying` au magasin, B-2.6-06, l'`enCours` de la 1.x). Le clavier (Ctrl+Z, Ctrl+Y, Ctrl+Maj+Z) met la demande dans la
   file même quand l'historique est vide à cet instant : à son tour, elle lit l'historique laissé par l'opération d'avant,
   si bien qu'un Ctrl+Z pressé pendant un déplacement annule ce déplacement ; rien n'est envoyé s'il est vide.
2. **Réponses** : « Annulé : {action} » et « Rétabli : {action} », l'action nommée par le `code` de la réponse (le `kind`
   du journal, codes de la 1.x, un test vérifie qu'ils ont tous un nom dans chaque langue). Un refus 409
   (`OPERATION_DEPASSEE`, `CARTE_MODIFIEE`) montre le message de l'hôte et relit la carte avec ses piles, que l'hôte a
   vidées (AR-05). `INTROUVABLE` est muet, la carte et l'historique sont relus (AR-108). Tout autre échec (réseau, erreur
   interne) dit « Impossible d'annuler » ou « de rétablir », avec la raison, et relit la carte ; **écart** à B-2.6-49 : la
   pile n'est pas vidée (AR-121).
3. **Barre de commande** (`panels/CommandBar.tsx`, `state/actions/commandBar.ts`) : élément flottant `command`, ouvert par
   le bouton Commande (zone Liens) et Ctrl+K pour qui modifie la carte ; sans voile, comme la 1.x, mais les raccourcis de
   la carte sont muets tant qu'elle est ouverte (couche `modal`). Fixe à 18 % de la hauteur, 560 px (jetons
   `--command-bar-top`, `--command-bar-width`), zone de saisie à chasse fixe de 1 à 6 lignes, aide et exemple dans les
   codes du projet. Entrée envoie, Maj+Entrée va à la ligne, un texte vide n'est pas envoyé. Un refus reste dans la barre
   (message, puis le message de chaque ligne de `details.errors`), sans notification ; un succès ferme la barre et dit
   « N flèches ajoutées ». Échap, Annuler et un clic au dehors la ferment.
4. **Barre de l'éditeur** : l'éditeur déclare le projet ouvert (`shared/state/openProject.ts` : code, fil d'Ariane, droit
   de modifier, ouverture du menu Fichier) et le retire en partant ; l'en-tête (`app/EditorBar.tsx`) le lit et montre, **à
   la place de la signature** comme la maquette validée, le fil d'Ariane (liens vers les cartes parentes, la carte montrée
   en `span` avec `aria-current="page"`, chevrons Lucide, pastille du code), le bouton Fichier (`aria-haspopup="menu"`)
   et le bouton Membres pour qui modifie la carte. La signature revient sur les autres écrans (B-1.2-10).
5. **Menu Fichier** (`state/commands/fileCommands.ts`, cible `file` des menus de l'éditeur, ouvert 4 px sous son bouton) :
   [admin] Ouvrir un fichier .pmap… ; Télécharger une copie (.pmap) ; [admin] Dupliquer le projet… ; puis, pour
   l'animateur, Exporter le projet et Remplacer par un paquet (ADR 058 § 8). Le remplacement lit d'abord la version du
   projet (If-Match) et la durée de la corbeille (30 jours si la corbeille ne se lit pas). Le nom du projet est celui de
   sa carte racine, premier du fil d'Ariane (1.x `nomProjet`). Les exports PDF, PNG et Excel viendront avec leur
   réalisation (P6-T09) : pas d'entrée sans action.
6. **Traductions** : le bouton de l'en-tête (P6-T02) suit le projet déclaré et n'apparaît que lorsque l'éditeur déclare
   aussi l'ouverture des traductions (`openTranslations`, facultatif) : leur écran vient avec P6-T08.
7. **Dernier projet ouvert** : l'enveloppe (`app/Shell.tsx`) fait mémoriser par le profil le projet déclaré
   (`PUT /api/me/last-project`, sans version ; la réponse est le profil, gardé), seulement s'il n'est pas déjà le dernier
   (identifiants comparés sans la casse) ; un échec est muet. L'administrateur est donné à l'éditeur par la route de la
   carte (`App.tsx`) : `features/map` ne lit pas le profil.
8. **Éditeur composé** (B-7-16) : magasin de la carte, préférences lues du stockage, élément flottant, notifications,
   navigation et administrateur, propres à chaque carte.

## Conséquences

- Le menu Fichier n'a pas encore ses trois exports (P6-T09) ; le bouton Traductions attend P6-T08.
- La copie téléchargée et le remplacement sont prouvés en unitaire ; leurs parcours viennent avec P6-T10.
- Le paquet JavaScript grossit encore (614 ko, registre F-26).

## Révision (29/10/2026, essai du porteur) : Fichier à droite de la marque (AR-155)

Le point 4 change d'ordre : le bouton Fichier vient juste après la marque, comme dans Office, puis le fil d'Ariane, puis
la classification et Membres (`app/EditorBar.tsx`). Le menu Fichier gagne « Importer un paquet… » (AR-154).
