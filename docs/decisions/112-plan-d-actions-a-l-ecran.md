<!-- 112-plan-d-actions-a-l-ecran.md -->
<!-- Action plan on screen (3.4, lot 4): the existing action plan query enriched with three facts, one list built once and read by a side panel and by the sheet of the Excel export, a late mark judged on the local day of the browser, a way to edit the plan through the sheet; closing a pain point left aside -->

# 112 - Plan d'actions à l'écran

Statut : acceptée le 21/11/2026 (3.4, lot 4). S'appuie sur les ADR 102 (l'irritant trouvable : le lien vers la fiche
qui reçoit, F-118), 106 (lecture sans événement), 110 (le volet « Rechercher » et le magasin de la page) et 066 (les
exports de la carte). Demande du porteur du projet : suivre les actions et les échéances des irritants sans exporter le
projet en Excel.

## Contexte

- L'hôte donne déjà le plan : `ActionPlan.Of` (cœur) liste, pour une carte et toutes celles en dessous, les irritants
  des étapes vivantes, tous états, dans l'ordre du sous-arbre puis de lecture ; `GetActionPlanQuery` (lecteur) en fait
  des `ActionPlanLine`, route `GET /api/maps/{id}/action-plan`. Seule la feuille « Plan d'actions » de l'export Excel
  (`actionPlanSheet`) l'affiche aujourd'hui.
- Les irritants ne dépendent pas de celui qui regarde : tout lecteur les voit sur la fiche. Ce qui dépend du rôle, c'est
  les écrire : remplir la fiche de l'étape qui reçoit le document (`SheetRights.CanFill`).
- **Aucune commande ne change l'état d'un irritant.** Le cœur fermé dit que l'application ne produit que « signalé »
  (`PainPointRules.cs:10-17`) ; la règle A-0-03 (`docs\couverture.md` (non publié)) tient qu'aucune route ne le fait évoluer. Vider
  la description efface l'irritant et son historique, ce n'est pas le clore. Clore ou rouvrir demande une règle dans le
  cœur (interdit 1 de REGLES § 3) : ce lot ne le fait pas.
- La règle « ouvert » existe (`MapMarkers.IsOpen` : ni réalisé, ni adopté, ni abandonné) ; l'interface en porte déjà une
  copie (`bpmn\model\bpmnMapping.ts`, F-215) : le lot n'en ajoute pas une seconde.
- `GetActionPlanQuery` appelle `TranslationRules.Of` trois fois par ligne, et cette règle parcourt toutes les
  traductions : lue pour tout le projet à chaque affichage, la requête doit indexer les traductions une fois.

## Décision

1. **Une liste, construite une fois, lue deux fois.** Côté hôte, `ActionPlan.Of` et `GetActionPlanQuery` restent la
   seule construction ; le volet lit `GET /api/maps/{root}/action-plan`, la carte principale du projet, la feuille de
   l'export garde la carte exportée et ses niveaux. Aucune route nouvelle (`RouteTableTests` inchangé), pas de route
   `projects/{id}/action-plan`. Côté interface, une seule fonction fait les valeurs, `planEntries`
   (`web\src\features\map\plan\actionPlan.ts`) : une entrée `{ line, cells, open, noAction }` par ligne, dans l'ordre de
   l'hôte ; `actionPlanSheet` prend ses rangées de `planEntries`, le volet affiche les mêmes entrées. Un essai compare
   cellule par cellule. Tris, filtres et signal de retard sont de l'affichage seul : la feuille garde toutes les lignes
   dans l'ordre du projet.

2. **La requête gagne quatre faits, sans fichier du cœur.** `ActionPlanLine` : `Open` (`MapMarkers.IsOpen`, appelée et
   non recopiée), `ReceivingStepId`, `ReceivingMapId` (la carte de l'étape qui reçoit, qui peut différer de celle de
   l'étape qui produit après un déplacement) et `DocumentId` (l'usage dont `PainPointId` est l'irritant, étape vivante, par un
   dictionnaire construit une fois ; nul si aucun), `CanFill` (étape qui reçoit connue, avec contenu, et
   `SheetRights.ViewerOf(workspace, receiving, role, userId).CanFill` : la règle de la fiche, écrite une fois, celle que
   vérifie `UseDocumentCommand`). `ActionPlanView` gagne `OpenLostHoursPerMonth`, somme exacte en `decimal` des lignes
   ouvertes, pour qu'aucun arrondi de nombres flottants ne se fasse dans la page. Les traductions de la carte, de
   l'étape et du couloir sont lues dans un index construit une fois par requête ; un essai d'équivalence tient les noms
   rendus égaux à ceux d'avant. Le reste ne change pas : droit lecteur, chargement sans verrou ni écriture, aucun
   journal, aucun audit. Contrat OpenAPI réécrit.

3. **Visibilité : toutes les lignes pour tout membre, comme les fiches.** Animateur, contributeur et lecteur voient les
   mêmes lignes, le même total et les mêmes retards. Seul le bouton « Modifier le plan » dépend du rôle (`can_fill`) :
   l'animateur et l'administrateur toutes les lignes (sauf étape qui reçoit sans contenu), le contributeur les étapes
   qu'il remplit (assignée, couloir tenu), le lecteur aucune. Non-membre et projet à la corbeille : 404.

4. **Le retard se juge sur le jour local du navigateur.** En retard = irritant ouvert, échéance donnée, échéance
   strictement avant aujourd'hui (celle du jour n'est pas en retard ; un irritant traité ne l'est jamais). Aujourd'hui =
   `localDay()` de `shared\i18n\formatDate.ts`, écrit par `getFullYear`, `getMonth` et `getDate`, jamais par
   `toISOString` (jour en temps universel : la veille à 0 h 30 à Paris) ; comparaison de deux chaînes `yyyy-mm-dd`,
   l'échéance n'étant jamais lue par `new Date('2026-12-15')`. Pas par l'hôte : l'échéance est un jour sans fuseau, et en
   mode Serveur l'IIS peut être dans un autre fuseau que la personne ; deux personnes de deux fuseaux peuvent donc voir un
   retard différent le même soir, chacune sur son calendrier. Le jour est relu à chaque rendu, sans minuterie. **Aucune
   colonne « En retard » dans l'export** : un fichier se relit des semaines plus tard, un retard figé au jour de l'export
   y deviendrait faux ; le format de la feuille ne change pas.

5. **Le volet « Plan d'actions ».** Un volet latéral réductible à droite (`DockPanel` `'plan'`, après `'search'`), pas
   une fenêtre. Son état est un magasin de la page (`features\map\plan\planStore.ts`), hors de l'éditeur, sur le modèle
   de `searchStore.ts` : projet, ouvert, tri, filtres, réponse, demande de modification en attente ; jamais gardé dans le
   navigateur, remis à zéro quand un autre projet s'ouvre. Lu à chaque fois qu'il devient visible (ouvert, ou découvert à
   la fermeture de la fiche qui le couvrait, sur cette carte ou sur la carte montrée ensuite : le magasin retient qu'une
   fiche l'a couvert, R4b), une fois enregistré ce que la fiche avait encore à envoyer (les files d'écriture suivies par
   `pendingWrites`, celles de « Quitter »), une lecture nouvelle annulant la précédente (`AbortController`) ; pas de
   lecture continue (F-151 : l'hôte n'a pas de limiteur de débit). En tête, le total des heures perdues des irritants
   ouverts, le nombre en retard, « Irritants montrés : 2 sur 3 » ; trois tris stables (ordre du projet, heures perdues, échéance, les vides en
   dernier) ; trois filtres cumulables (« Ouverts », « En retard », « Sans action »), « Ouverts » enfoncé au départ. Une
   ligne est un bouton qui mène à l'étape où l'irritant est signalé par `useLeadTo` et l'arrivée de F-64, sur une autre
   carte comme sur celle-ci ; dans la vue BPMN, la vue se ferme d'abord. Règle de place reprise de la recherche : une
   fiche couvre le volet (il revient, relu, à sa fermeture) ; l'assistant ou un calque allumé le ferme ; la recherche et le
   plan s'excluent. Commande `plan`, icône `ClipboardList`, dans la zone Affichage après « Analyse », pour tous les rôles
   et dans la vue BPMN, sans raccourci. Rien n'est ajouté sur le schéma.

6. **« Modifier le plan » par le chemin F-118.** Le bouton, sur la ligne dont `can_fill` est vrai, ouvre la fiche de
   l'étape qui reçoit, Entrées dépliées, le curseur dans le champ du document (le geste de l'ADR 102 révisée), sur la
   carte de cette étape (`receiving_map_id`), qui n'est pas toujours celle de l'irritant (une étape déplacée garde ses
   documents) ; sur une autre carte, la demande est gardée dans `planStore`, la carte s'ouvre, son `MapSidePanel` la
   prend une fois, et elle est oubliée si une autre carte s'ouvre. Pas de bouton quand `can_fill` est faux : jamais de lien vers une fiche où
   l'irritant ne peut pas s'écrire. C'est ce que le lot offre à la place de « Clore » : compléter l'action, le
   responsable et l'échéance depuis la liste.

## Écarté

- **Clore ou rouvrir par une commande de l'application qui écrirait l'état** (`ProjectWorkspace.Put` est public) : ce
  serait réécrire hors du cœur une règle qu'il interdit, choisir seul les passages permis entre sept états et casser
  A-0-03. Laissé : demande le cœur fermé (voir F-221).
  *Révisée par 116* : l'ADR 116 ouvre cette règle du cœur, avec l'accord du porteur du 21/11/2026.
- **Une route `projects/{id}/action-plan`** : la carte principale suffit.
- **Le retard calculé par l'hôte** : le fuseau du serveur n'est pas celui de la personne.
- **Une colonne « En retard » dans l'export** : un fichier relu plus tard mentirait.
- **Une seconde copie des états traités dans l'interface** : l'hôte dit `open` sur chaque ligne.
- **Une lecture continue du plan** : une lecture périodique chargerait l'hôte sans limiteur de débit (F-151).
- **Une case « terminé » à la manière des outils de suivi de tâches** : elle demande le cœur, comme clore.

## Conséquences

- Aucun fichier du cœur fermé ni migration ; ni dépendance (`lucide-react` est déjà livrée), ni appel sortant
  (`connect-src 'self'` inchangé). Le changement de l'application est borné à `ActionPlanQuery.cs` (et
  `ActionPlanLines.cs` pour tenir 80 lignes par fonction) ; le contrat OpenAPI est réécrit.
- Un irritant noté dans l'application reste « signalé », donc ouvert, tant que le cœur ne change pas ; le manuel le dit,
  pas l'écran. Les autres états viennent d'un paquet (le projet d'exemple en porte). `open` et `can_fill` sont sur
  chaque ligne : un bouton d'état se posera plus tard sans rien refaire (F-221).
- Le projet en PDF du lot 5 lira `planEntries` et la même route, sans troisième construction.
- Coût : un chargement du projet et une passe en mémoire à chaque affichage du volet, traductions indexées une fois.

## Vérification

- Hôte : `ActionPlanQueryTests` (application) : `open` comme la carte, total exact (0,1 + 0,2), étape et document qui
  reçoivent, droit de remplir de chaque rôle (animateur et administrateur, contributeur par étape et couloir de
  l'étape qui reçoit, lecteur aucun, étape qui reçoit sans contenu), lignes et ordre ceux du cœur, noms par l'index
  égaux aux noms lus un à un, un chargement sans écriture ni journal. `ActionPlanRightsTests` (deux bases) : droits par
  rôle, total des niveaux, aucun événement d'audit, 404. `RouteTableTests` inchangé, `ApiContractTests` à jour.
- Interface : `actionPlan.test.ts` (volet et feuille cellule par cellule, retard, tris, filtres), `formatDate.test.ts`
  (jour local à `Pacific/Kiritimati` et `Pacific/Pago_Pago`), `ActionPlanPanel.test.tsx`, essai du ruban (zone Affichage,
  tous rôles, vue BPMN), `exports.test.tsx` et `actionPlanSheet.test.ts` verts.
- Parcours : `web\e2e\action-plan-34.spec.ts`, en mode Poste et en mode Serveur.
- Témoins de mutation vus rouges puis remis : `ViewerOf` remplacé par `new SheetViewer(true, true)` ; rôle forcé à
  animateur ; droit calculé sur l'étape qui produit ; `IsOpen` remplacé par `Status == Reported` ; total qui compte les
  lignes traitées ; `[RequiresRole(MemberRole.Contributor)]` sur la requête ; feuille qui refait une colonne seule ; `<=`
  au lieu de `<` dans `isLate` ; `localDay` par `toISOString` ; bouton montré quel que soit `can_fill`.
