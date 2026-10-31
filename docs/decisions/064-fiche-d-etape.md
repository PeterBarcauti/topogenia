<!-- 064-fiche-d-etape.md -->
<!-- La fiche d'étape côté interface : formulaire, texte libre, déroulé, sections pliables, enregistrement automatique, validation, précision, responsable, documents, usages, irritants, lecture (P6-T07) -->

# 064 - La fiche d'étape

Statut : acceptée (date 28/10/2026)

## Contexte

P6-T07 (`plan.md`, phase 6) porte l'interface de l'annexe B 4 : 81 lignes, dont 75 « à faire », avec AR-89 (poignée de
glissement du déroulé) et AR-91 (sections pliables), plus les lignes que les tâches précédentes lui ont laissées (B-2.1-39,
B-2.1-40, B-2.6-11, B-8.1-34, B-8.1-35). Les routes de l'hôte sont faites depuis la phase 4 (`GET/PUT
/api/steps/{id}/sheet`, `/validate`, `/clarification`, `/documents`, `/documents/available`, `/uses/{documentId}`,
`PATCH /api/steps/{id}`). Référence 1.x (lecture seule) : `fiche/PanneauFiche.tsx`, `Deroule.tsx`, `Documents.tsx` et leur
test ; rendu : la maquette validée (panneau Fiche, classes `.sheet-*`, `.seg`, `.entry`, `.micro`, `.usage`, `.irritant`,
`.output`, `.suggest`, `.resume`).

## Décision

1. **Module `web/src/features/sheet/`** (spec 3.5 : `SheetPanel`, file d'enregistrement, formulaire, lecture, déroulé,
   entrées, sorties, usage, dialogue « Autre… »), indépendant de l'éditeur de la carte : il reçoit l'étape (code, libellé
   et nom du couloir dans la langue affichée, responsable), le projet lu et des rappels (fermer, jauge, carte à relire,
   affecter, notifier). Seul `map/panels/SheetDock.tsx` connaît les deux ; aucun import de `sheet` vers `map`. Modules
   purs : `model/draft.ts` (brouillon, contenu envoyé, mode au chargement, résumé de la carte, outils proposés),
   `model/procedure.ts`, `model/documents.ts` ; état : `state/autoSave.ts` (file), `state/useSheetState.ts`,
   `state/sheetActions.ts`.
2. **Noms des personnes** : la vue de la fiche ne donne que des identifiants (responsable, validée par, document déclaré
   par), la 1.x donnait des noms. L'éditeur lit le projet une fois, à la première fiche ouverte (`GET /api/projects/{id}`,
   ouvert à tout membre : membres dans l'ordre d'affichage, libellé du groupe de partage) ; une personne qui n'est plus
   membre s'affiche « un ancien membre » ; « … » tant que le projet n'est pas lu.
3. **Versions** (`If-Match`) : la fiche, sa version ou `0` tant qu'aucun brouillon n'existe (ADR 045) ; un document, sa
   version ; un usage, la sienne ou `0` pour un premier usage ; la validation et la demande de précision, la dernière
   version connue de la file.
4. **Écritures** : le contenu passe par sa propre file (la fiche n'est pas au journal des opérations, sa réponse n'est pas
   une vue de structure) : un seul envoi à la fois, 1200 ms après la dernière frappe, réponse appliquée sans toucher à la
   saisie, envoi immédiat à la fermeture et au changement d'étape, confirmation du navigateur avant de quitter l'onglet
   tant qu'il reste une saisie non enregistrée ou un échec. Les documents et usages, un à la fois par panneau : la fiche
   rendue est appliquée, puis la carte est relue par la file de l'éditeur (`editor.reload`, après les écritures déjà
   envoyées) pour les flèches, les pictogrammes et les jauges voisines. Le responsable : `PATCH /api/steps/{id}` par
   `editor.write` (vue de structure appliquée), puis la fiche relue.
5. **Défaut de la 1.x corrigé** (B-4-43) : après un échec, la 1.x remettait en attente le contenu qui avait échoué même
   quand une saisie plus récente était déjà partie derrière lui ; il repartait ensuite et écrasait la plus récente. La
   file ne remet en attente que la dernière saisie donnée, et seulement si rien de plus récent n'attend ni n'est parti.
   L'état « Enregistrement… » reste affiché tant qu'un envoi demandé n'a pas répondu.
6. **Jauge** (B-2.6-11, B-4-50) : le résumé de chaque fiche reçue (état, complétude, droit de remplir, fréquence, durée)
   est écrit dans l'étape du magasin, sans relire la carte ; rien n'est écrit pour un résumé identique.
7. **Un seul panneau ancré** (AR-123) : la fiche et le reliquat partagent le côté droit du canevas ; ouvrir l'un ferme
   l'autre (`watchSidePanel`, posé par l'écran). Les deux panneaux font 440 px, 45 % de la fenêtre au plus (B-2.1-39), le
   canevas rétrécit (B-2.1-40). Une nouvelle `key` par étape remet le panneau à zéro (B-4-06).
8. **Échap** (B-4-04, B-8.1-34) : la couche `panel` du gestionnaire de clavier ferme le panneau quand le focus n'est pas
   dans un champ ; dans un champ du panneau, le `keydown` du panneau le ferme (le gestionnaire laisse les touches d'un
   champ au champ) ; le formulaire d'un document l'arrête d'abord (annuler), le dialogue « Autre… » est modal.
   **Défaut de P6-T06 trouvé et corrigé** : le gestionnaire transmet aussi le relâchement des touches, et les panneaux
   Reliquat et RACI fermaient sur le relâchement de l'Échap qui venait de fermer un dialogue au-dessus d'eux (confirmation
   d'une suppression). Les trois panneaux ne ferment plus que sur l'appui.
9. **Déroulé** (AR-89) : poignée de glissement à gauche du numéro (glisser-déposer HTML, cachée aux aides techniques),
   ligne visée marquée ; ↑ ↓ × montrés au survol et au focus de la ligne ; Alt+↑ et Alt+↓ dans le texte de la ligne ;
   Entrée et Retour arrière comme la 1.x ; le focus suit ; un déplacement est une modification ordinaire du brouillon.
10. **Sections pliables** (AR-91) : Entrées, Déroulé et Sorties, ouvertes par défaut, bouton d'en-tête `aria-expanded`,
    résumé gardé (« 3 documents », « 1 opération », « vide ») ; l'état est une préférence de l'éditeur
    (`app.sheetInputsFolded`, `app.sheetProcedureFolded`, `app.sheetOutputsFolded`) ; même chose en lecture.
11. **Disposition de la maquette** : indicateur d'enregistrement dans l'en-tête collant (la 1.x le mettait au pied),
    responsable en tête du corps, bascule segmentée, rubriques dans l'ordre testé, validation et son aide dans le pied
    collant. La phrase d'introduction de la 1.x n'est pas reprise (absente de la maquette).
12. **Lecture** (B-4-58 à B-4-61) : les rubriques dans l'ordre du formulaire, les champs renseignés seuls, le déroulé
    numéroté (outil et nature en discret, contrôle en vert), les entrées utilisées et les sorties dans leurs sections, le
    texte libre ; sans version validée, « Aucune fiche validée… » et les documents.
13. **Documents** : une adresse s'ouvre dans un nouvel onglet seulement si l'hôte l'a typée `url` et qu'elle commence par
    `http://` ou `https://` (aucun autre schéma en lien, `noopener noreferrer`) ; un chemin s'affiche en discret. La case
    du partage nomme le groupe du réglage du projet (B-4-69), « Partagé avec le groupe de partage du projet » sans libellé.
    Une entrée d'ailleurs garde la couleur des jetons d'AR-26 (`--other-input-bg`, `--other-input-border`). Un
    consommateur que la carte ne montre pas se dit « une étape d'une autre carte ».
14. **Classes d'état** (B-8.1-35) : `state--empty`, `state--draft`, `state--validated`, `state--needs_review`, aux codes
    du contrat, et la jauge du panneau aux couleurs de celle de la carte.

## Conséquences

- Les parcours de bout en bout de la fiche (B-8.3-14 à B-8.3-25) viennent avec P6-T10 ; les lignes à part bout en bout
  sont écrites « couvert (interface) » dans la couverture.
- Le défaut de P6-T06 (Échap relâché) est inscrit au journal ; ses deux panneaux ont chacun un test de non-régression.
