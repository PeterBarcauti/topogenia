<!-- 063-detail-remontee-et-raci.md -->
<!-- Détail et remontée du détail dans le flux (cadre, reliquat, rangement), calque, panneau et matrice RACI côté interface (P6-T06) -->

# 063 - Détail, remontée et RACI

Statut : acceptée (date 28/10/2026)

## Contexte

P6-T06 (`plan.md`, phase 6) porte l'interface de l'annexe B 2.9 et 2.11 : Détailler et les sous-cartes (Détailler, Ouvrir,
Supprimer le détail et le fil d'Ariane sont en place depuis P6-T04, ADR 061 et 062), la remontée du détail dans le flux
(cadre, reliquat, rangement) et le RACI (calque, panneau, matrice), avec les lignes que T04 lui a laissées (B-2.1-32,
B-2.4-16, B-3.2-17, B-6-26, B-6-30, B-7-21). Les routes de l'hôte sont faites depuis la phase 4 (`POST
/api/steps/{id}/dissolve`, `/api/dissolutions/{id}/assign`, `/remove`, `/finish`, `PUT /api/steps/{id}/raci`) ; la vue de
la carte porte `dissolution`, `dissolution_sheet`, `raci` et `raci_letters` (ADR 047). Référence 1.x (lecture seule) :
`Reliquat.tsx`, `raci.ts`, `Fenetres.tsx` (`PanneauRaci`, `DialogueMatrice`), `EditeurCarte.tsx`, `geometrie.ts`
(`cadreRemontee`), `Noeuds.tsx`, `magasin.ts` ; rendu : la maquette validée (cadre, étiquette, étape hors cadre, panneau
latéral de la fiche).

## Décision

1. **Modules purs** (`web/src/features/map/model/`) : `raci.ts` (étape concernée, rôles d'une étape avec le R implicite,
   approbateur manquant, lettres de la carte, pastilles A, S, C, I puis ordre des couloirs, matrice, texte tabulé, tableau
   HTML), `frame.ts` (boîte du cadre autour des étapes présentes, marges 14 et 26, étapes étrangères recouvertes),
   `remainder.ts` (taille du reliquat, candidats en deux groupes, rangement automatique, sens d'un rangement). Les étapes
   sont rangées par la seule fonction `readingOrder` (B-8.1-29, prouvé par `structure.test.ts`) ; `hasContent` passe dans
   `model/stepFacts.ts`, un module pur ne lisant pas l'état.
2. **Cadre** : nœud `frame` de React Flow, derrière les étapes et devant les couloirs, que le pointeur traverse sauf son
   étiquette (B-2.3-08) ; pointillés `--frame`, étiquette en haut à droite « Reliquat de CODE · n » ou « Remonté de CODE ·
   rien à ventiler » (maquette), qui ouvre le panneau Reliquat ; absent de la mini-carte. Une étape étrangère recouverte
   porte `is-outside`.
3. **AR-53 tenu pour l'étape coupée aussi** : la feuille du lot 2 de P6-T04 mettait toute l'étape coupée à 0,45, texte
   compris (1,8:1 à 2,8:1, sous NF08). L'étape coupée et l'étape hors cadre suivent la maquette : une couche sous le texte
   porte la carte à `--step-cut-opacity`, le texte reste à pleine opacité en `--text-muted` ; les pointillés marquent la
   coupe ; un losange atténue sa forme et son marqueur. Défaut du lot 2 trouvé ici et corrigé.
4. **Remontée** (`state/actions/dissolution.ts`) : « Remonter le détail dans le flux » au menu Étape, après « Détailler
   cette étape », pour une macro quand la carte n'a pas de cadre ouvert (B-2.4-16, B-2.9-15) ; remonter avec la version de
   l'étape, affecter, supprimer et terminer passent par la file des écritures et appliquent la carte rendue. Annuler et
   rétablir sont ceux de l'hôte (B-2.9-41, B-2.9-42) : plus d'état caché `remonteesActuelles` (B-8.1-15).
5. **Panneau Reliquat** (`panels/RemainderPanel.tsx`, `RemainderSheet.tsx`) : ancré à droite du canevas, qui rétrécit,
   comme le panneau de la fiche de la maquette (`map-workspace`, `.side-panel` de 440 px partagé avec P6-T07) ; non modal :
   Échap le ferme, les autres touches vont au canevas (B-2.5-04). Sections documents produits et reçus (« utilisé
   par… »), documents reçus d'autres étapes (« produit par CODE »), irritants, fiche en lecture seule (champs typés,
   déroulé, texte libre, B-8.1-18) ; « Affecter à… » en deux groupes et « La carte (sans étape) » pour un irritant ;
   Supprimer avec la confirmation définitive (usages retirés) ; Terminer avec son aide, « Fermer le cadre » quand le
   reliquat est vide. **Lecteur** (AR-122) : le cadre et le reliquat sans les choix ni les boutons, que l'hôte lui refuse.
6. **Rangement** : le dialogue des rangements après Terminer quand quelque chose a été rangé ; sinon rien, le panneau se
   ferme avec le cadre. **Rangement automatique** : la carte modifiable dont le cadre n'a plus qu'une étape ou aucune envoie
   Terminer une fois par remontée (identifiants gardés par l'éditeur, jamais en stockage), puis le dialogue s'affiche avec
   le texte « auto » (B-2.9-38, B-2.9-39). Une seule surveillance de la carte (`watchFrame`, posée par l'écran) : le
   cadre fermé ferme le panneau (B-2.9-40). Un refus de Terminer laisse le panneau ouvert sur le cadre relu (la 1.x le
   fermait).
7. **RACI** : calque par la préférence `showRaci` ; pastilles sous l'étape, sous le libellé d'un losange et sous la
   fréquence d'une macro (décalage `--raci-top` calculé par la mise en page, constantes `UNDER_STEP`) ; « A ? » aux
   couleurs d'avertissement. Clic, calque allumé, carte modifiable, étape concernée : panneau RACI à (+12, −20) du clic
   (B-2.3-12), non modal, gardé dans la fenêtre une fois dessiné, fermé par Échap ou une pression dehors ; le couloir R en
   tête sur `--raci-r-bg`, un bouton par lettre (`aria-pressed`), rien d'envoyé pour le même rôle ; `PUT
   /api/steps/{id}/raci` avec la version de l'étape, l'hôte déplace le A unique. Bouton **Matrice** de la zone Affichage
   seulement calque allumé (B-2.1-32 : `itemsOf` laisse désormais de côté une commande non visible) ; dialogue large,
   étapes concernées en lignes dans l'ordre de lecture, couloirs en colonnes, en-têtes sur `--table-header-bg` (B-6-30),
   légende ; Copier met `text/html` et `text/plain` ensemble, le texte seul en repli, « Matrice copiée… ».
8. **Couleurs du tableau copié** : le HTML collé dans Word ou PowerPoint ne lit pas les variables de la page ; la copie
   lit les jetons (`--border`, `--table-header-bg`, `--font-sans`) au moment de copier, aucune couleur n'est écrite dans
   le code ; une valeur absente laisse sa propriété de côté.
9. **Détailler la sélection** : la nouvelle étape « Sous-processus » est choisie et passe en renommage (B-2.9-10, comme
   la 1.x).
10. **Suite web** : vitest prend la moitié des cœurs (`maxWorkers: '50%'`) ; un travailleur par cœur logique (19 sur
    cette machine) affamait le premier rendu de React Flow de quatre fichiers au-delà des 5 s d'un test. Aucun délai de
    test n'est changé ; la suite est plus rapide (50 à 65 s).

## Conséquences

- Les exports viennent avec P6-T09 : cadre caché et étapes dégrisées pendant l'export (B-2.9-20, B-2.14-05, 10, 11), onglet
  et colonnes RACI de l'Excel (B-2.14-21, 24).
- Les parcours de bout en bout (B-8.3-41 à 46, 51 à 53, 65 à 67) viennent avec P6-T10.
- Le panneau Fiche (P6-T07) reprend l'espace de travail et le style `.side-panel`.

## Révision (29/10/2026, essai du porteur) : Grouper et Dissocier (AR-154)

Les points 4 et 9 changent de mots, pas de comportement. « Détailler la sélection » devient « Grouper en
sous-processus » (Ctrl+G), « Remonter le détail dans le flux » devient « Dissocier le sous-processus » (Ctrl+Maj+G),
les mots et les raccourcis d'Office et de Visio. Le ruban garde « Détailler » pour une étape seule et gagne « Grouper »
(plusieurs étapes choisies) et « Dissocier » (une étape détaillée, carte sans cadre ouvert) à côté de lui
(`state/commands/editCommands.ts`, `menuCommands.ts`, `mapCommands.ts`). La remontée, son cadre et son reliquat restent
ceux des points 4 à 6 : le panneau garde le nom de « Reliquat ».
