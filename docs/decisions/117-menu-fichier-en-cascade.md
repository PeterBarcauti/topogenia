<!-- 117-menu-fichier-en-cascade.md -->
<!-- The File menu in cascade (3.4, retouche R15): the shared menu learns the submenu, the 22 flat lines become 9 (three clicks, four groups, the properties), each role keeps the commands it sees, the short labels of a group are a key of the menu, not a second label of the command -->

# 117 - Le menu Fichier en cascade

Statut : acceptée le 22/11/2026 (3.4, retouche R15). Prolonge les ADR 058 (le menu Fichier), 109 (la carte copiée en
image) et 116 (le bouton d'état, qui a posé le menu partagé `shared/ui/Menu.tsx`). Demande du porteur du projet, sur la
proposition d'une maquette de travail (non publiée) : le menu Fichier compte 22 lignes à plat pour un
animateur administrateur ; les formats ne sont jamais à plat au premier niveau dans les outils connus (draw.io,
Google Docs).

## Décision

1. **Le premier niveau tient en neuf lignes.** Trois commandes à un clic (Ouvrir un fichier…, Enregistrer la carte
   ouverte (.pmap), Copier la carte en image), quatre groupes après un séparateur (Exporter la carte, Projet, Paquet
   d'échange, Assistant IA), un séparateur, Propriétés du projet…. Un groupe s'ouvre en sous-menu.
2. **Les droits ne changent pas.** Chaque rôle voit exactement les commandes d'aujourd'hui (`visible` des commandes,
   inchangé) ; un groupe dont aucune commande n'est visible pour ce rôle ne s'affiche pas (un lecteur n'a ni « Paquet
   d'échange » ni « Assistant IA »). Aucune règle de droits n'est écrite deux fois : les groupes ne portent que des
   identifiants de commandes.
3. **Le composant sait faire un sous-menu, sans connaître le menu Fichier.** `Menu` reçoit `submenus` (comme il reçoit
   `groups`) : un nom, les identifiants des commandes qu'il range, éventuellement des libellés courts. Le même composant
   servira aux menus clic droit ; aucun autre menu ne change.
4. **Le geste de draw.io et de Windows.**
   - Souris : le survol d'une ligne « › » ouvre son sous-menu après un court délai (150 ms) ; le survol d'une autre
     ligne le ferme ; un clic l'ouvre et donne le focus à sa première entrée ; un clic sur une entrée l'exécute et ferme
     tout ; un clic dehors ferme tout.
   - Clavier : Entrée, Espace ou flèche droite ouvre le sous-menu et donne le focus à sa première entrée ; flèche
     gauche ou Échap le ferme et rend le focus à sa ligne ; Échap au premier niveau ferme tout, comme avant ; haut et
     bas se déplacent dans le niveau où se trouve le focus seulement.
   - Place : à droite de son menu, à gauche s'il sortirait de la fenêtre, jamais plus haut que la fenêtre (il défile
     alors) ; il s'aligne sur sa ligne. Fonction pure `submenuPlacement` (`menuPlacement.ts`).
   - ARIA 1.2 : la ligne est un `menuitem` avec `aria-haspopup="menu"` et `aria-expanded` ; le sous-menu est un
     `role="menu"` nommé par le nom du groupe. Les groupes de choix, le nuancier et le focus rendu à l'ouvreur ne
     changent pas.
5. **Libellés courts, une seule source.** Dans un sous-menu, le libellé est court (« PDF (A3 paysage) », « Image
   PNG »…) par une clé du menu (`menu.file_short.*`) ; le nom lu de l'entrée reste le libellé complet de la commande
   (`aria-label`, comme les textes courts des groupes de choix). La barre de commande (Ctrl+K) et les autres lieux
   gardent le libellé complet. Quand le libellé court est le libellé complet (« Imprimer en grand… »), aucune clé n'est
   écrite. Le libellé de « Exporter le projet (.topogenia)… » garde l'extension du paquet (la commande la fournit) : il
   n'a pas de clé courte.
6. **Une seule liste de groupes.** `FILE_SUBMENUS` (`state/commands/fileMenu.ts`, sans dépendance de l'interface) est lue
   par le menu et par les aides des essais et des parcours (« ouvrir le groupe de cette entrée »). `FILE_MENU` garde
   les lignes à plat et leurs numéros de groupe (les séparateurs) ; il reste la seule liste des lignes du menu.

## Écarté

- **Une page entière** (Backstage d'Office) : trop lourde pour une trentaine de commandes.
- **Une fenêtre d'export** (Lucidchart) : un clic et une fenêtre de plus à chaque export.
- **Un second libellé dans la commande** : la commande garde un seul libellé ; le libellé court est celui du menu.
- **Un sous-menu propre au menu Fichier** : le composant reste générique.

## Conséquences

- Aucun changement du cœur, du format, des droits ni de l'API. Aucune dépendance, aucun appel sortant.
- Les essais et les parcours qui cliquaient une entrée du menu Fichier directement ouvrent d'abord son groupe : un clic
  de plus, rien de relâché.
- Le manuel (français et anglais) écrit « Fichier > Exporter la carte > PDF (A3 paysage) », etc. ; l'aide en ligne se
  régénère à part.

## Vérification

Essais de l'interface : le composant (`menuCascade.test.tsx` : survol, clic, clavier, fermeture, placement à gauche au
bord, ARIA, groupes de choix intacts), le placement (`menuPlacement.test.ts`), le menu Fichier par rôle (`menus.test.ts`,
`EditorBar.test.tsx`), la barre de commande inchangée. Parcours : les 13 fichiers qui passent par `header-file` ouvrent
le groupe avant l'entrée (aide `e2e/support/file-menu.ts`).
