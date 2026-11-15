<!-- 095-makigami-utile.md -->
<!-- The useful part of the Makigami method (3.1): the handoffs computed by the core across the levels, the layer « Analysis » and its panel; completed phase by phase (value, media, target state, ranges, actions, wall print) -->

# 095 - La partie utile de Makigami

Statut : acceptée pour les passages de main (07/11/2026, phase P1), le classement selon la valeur et les ruptures de support (phase P2) ; complétée à chaque phase du plan
du lot (gardé hors du dépôt). Prolonge les ADR 085 (flux de valeur calculé par le cœur) et 086 (volets à droite).
Demande du porteur du projet du 06/11/2026 : garder de la démarche Makigami ce qui sert un atelier, sans saisie de plus
quand c'est possible, sur une carte aussi sobre qu'en 3.0 hors inspection.

## Contexte

Un Makigami déroule un processus administratif sur un rouleau et y relève les passages de main (le dossier change de
responsable), les ruptures de support, la valeur des étapes, puis compare l'état actuel à un état cible. Topogenia a déjà
les couloirs, les flèches, les niveaux de détail et un flux de valeur calculé à chaque lecture par le cœur (ADR 085).

## Décision : les passages de main (M2)

1. **Calculés par le cœur, jamais stockés ni saisis**, dans le flux de valeur (`Domain/ValueStreams/Handoffs.cs`) :
   une seule implémentation pour l'écran, les exports et l'IA. Le résultat `MapHandoffs` (flèches marquées dans l'ordre
   de lecture, nombre porté par chaque macro-étape, total, nombre par dossier) est dans `value_stream.handoffs`.
2. **Règles.** Un couloir de personnes ou de service tient le dossier à ses étapes ; un système ne tient personne et
   transmet qui le tenait avant lui. Une flèche est un passage de main quand l'un de ceux qui tiennent le dossier à sa
   source diffère de l'un de ceux qui le reçoivent à sa cible. D'où : direct entre deux couloirs humains (règle A),
   aller-retour par un système sans passage (règle B), transfert par un système compté sur la flèche qui entre dans le
   nouveau couloir (règle C). Toute étape garde son couloir, début, fin et losanges compris.
3. **Niveaux, revus avant le code (P0).** Le plan comparait un détail au couloir de sa macro-étape ; les cas de test ont
   montré qu'il comptait deux fois (cas N1 des cas de test du lot, gardés hors du dépôt). Une macro-étape n'a donc pas de
   couloir propre ici : une flèche qui y entre se compare aux couloirs où son détail commence, une flèche qui en sort à
   ceux qui tiennent le dossier où il finit ; un détail qui commence dans un système reçoit qui le lui envoie, depuis la
   carte du dessus. Un couloir d'un détail est celui qu'il copie (`SourceLaneId`), sinon le premier de même nom au-dessus,
   et ainsi jusqu'à la racine, comme « Remonter le détail » les apparie (AR-12) ; sinon un autre responsable. Le calcul
   part de la racine du projet (point fixe des origines, boucles comprises). Propriété tenue par un test oracle : le
   nombre d'une carte et de ses détails est celui de la carte redessinée à plat.
4. **Par dossier** : chaque flèche marquée pondérée par son volume (débit de la source × part) sur le volume entrant de
   la carte, une macro-étape apportant son débit × le nombre par dossier de son détail ; inconnu dès qu'un volume manque.
5. **Écran** : un troisième calque « Analyse » au ruban (Affichage), exclusif avec RACI et Flux de valeur, éteint par
   défaut ; les flèches de passage plus épaisses dans la couleur de l'analyse, un badge sur une macro-étape ; son volet
   à droite avec une tuile par indicateur. Le survol d'une tuile met ses objets en avant (le reste estompé, les flèches
   numérotées dans l'ordre), un clic la garde. Écart au plan : la tuile est dans le volet de l'analyse, pas dans celui du
   flux de valeur, les deux calques s'excluant.
6. **Exports** : le total et le nombre par dossier dans la feuille « Bilan » du classeur, donc dans le chapitre des temps
   du rapport.

## Décision : la valeur des étapes (M1) et les ruptures de support (M3)

1. **Deux données facultatives, sans défaut** : `Step.ValueClass` (`value_added`, `necessary`, `waste`, `null` non classée)
   et `Document.Medium` (`paper`, `email`, `spreadsheet`, `file`, `software`, `other`, `null`). Une étape non classée n'est
   comptée ni nécessaire ni gaspillage : un défaut ferait d'une carte jamais classée une carte sans valeur ajoutée.
2. **Classe** : sur une étape avec contenu, le losange de décision compris (écart au plan, qui l'excluait : il porte du
   travail, sans lui la couverture n'atteindrait jamais 100 %) ; refusée sur un début, une fin, une porte parallèle ou
   inclusive et sur une macro-étape, dont la valeur vient de son détail ; perdue quand l'étape devient sans contenu.
   Écrite par la mise à jour de l'étape (une opération annulable), au menu de l'étape sous le calque « Analyse ».
3. **Calcul du cœur** (`ValueStreams/StepValues.cs`) : par classe, les étapes et leur travail par dossier pondéré comme le
   flux de valeur ; la part de valeur ajoutée sur le travail classé et la couverture (part du travail classé) ; sans
   volume ni travail connus, les étapes seules, jamais une part de zéro. Rupture : une étape qui reçoit un document (entrée
   de l'extérieur ou sortie utilisée d'une autre étape) sur un support et en produit un sur un autre, les deux renseignés,
   `other` jamais comparé ; chaque paire dite une fois ; une macro-étape porte celles de son détail ; la couverture compte
   les documents dont le support est renseigné. L'assistant ne devine aucun support par mots.
4. **Formats** : `valueClass` et `medium` au paquet 2.3, `value_class` et `medium` au `.pmap` 1.4, versions non publiées
   étendues ; la version écrite reste la plus ancienne qui porte le contenu. Base : colonnes et contrôles
   (`CK_step_value_class`, `CK_step_value_class_content`, `CK_document_medium`), migration `ValueClassAndMedium`.
5. **Écran** : une bande de couleur de la classe en haut de l'étape, un pointillé autour d'une étape à rupture ; tuiles
   « Valeur ajoutée » et « Ruptures de support » du volet, avec leur focus ; le support choisi dans la fiche du document.

## Décision : le volet « Analyse » guidé (essai J1, 07/11/2026)

Demande du porteur à l'essai J1 : compter les passages de main ne suffit pas, il faut chiffrer le temps qu'ils font perdre
et guider à tout moment l'équipe qui anime l'atelier. Maquette validée en trois versions
(gardée hors du dépôt), avec la relecture qu'il a transmise : pas de liste de saisie, pas
de sigle, un seul mot par notion, les bases des pourcentages dites.

1. **Décomposition du délai moyen** (`ValueStreams/StreamAnalysis.cs`, `StreamPaths.Part`) : le délai moyen, reprises
   comprises (spec 14.3, 13), se découpe exactement en travail, travail des étapes classées gaspillage, attente après un
   relais et autre attente. Une étape attend son délai moins sa durée ; une macro-étape apporte les parts de son détail.
   Chaque part suit les branches du délai (la plus longue d'une porte parallèle est celle du délai, pas la sienne) :
   leur somme vaut le délai. Inconnue sans la durée d'une étape (le délai reste affiché).
2. **Part des passages venus d'autres mains** (`ValueStreams/RelayShares.cs`, une seule implémentation) : une flèche
   apporte le dossier d'autres mains quand elle est marquée, ou quand elle sort d'un événement ou d'une porte sans
   contenu qui l'a reçu ainsi ; l'entrée d'un détail le reçoit comme sa macro-étape. Une étape dont les flèches
   l'apportent toutes de la même façon n'a pas besoin des volumes ; sinon chaque flèche pèse ses dossiers (débit de sa
   source fois sa part), inconnue si l'un manque. Ainsi une étape reprise depuis un autre service n'attend « après un
   relais » que pour la part de ses passages qui reviennent (la première règle, par oui ou non, comptait tout). Une étape
   qui n'attend pas (une fin, une porte) ne partage rien : sa part n'est pas demandée, inconnue elle ne rend pas la
   décomposition inconnue (audit 3.1 V2, 4.1, 08/11/2026).
3. **Où agir** : les étapes de la carte où l'équipe peut agir, rangées par temps perdu par dossier (son attente, plus son
   travail s'il est gaspillage, fois ses passages par dossier ; par passage sans volume), les inconnues ensuite, puis par
   nombre de causes, puis l'ordre de lecture. Causes : reçoit d'autres mains, relais et ruptures de son détail, rupture,
   gaspillage, part utilisable telle quelle de ses réceptions (%C&A), irritants ouverts. Une étape sans cause ni temps
   perdu n'y est pas.
4. **Écran** : sous les tuiles, « Le dossier » (délai, travail, barre en trois parts dont les pourcentages entiers font
   100, une phrase en jours, la part après un relais en gras ; la question à poser à l'équipe tant que le délai est
   inconnu ; les délais saisis quand certains suivent la règle par défaut) et « Où agir » (trois lignes, puis toutes, un
   clic mène à l'étape). En français, « Relais » remplace « Passages de main » partout (tuile, Excel, aides) ; le volet
   écrit « Utilisable tel quel », le libellé de la fiche, et garde %C&A au calque Flux de valeur.
5. **Exports** : trois lignes de plus dans « Bilan » (les parts) et les délais saisis ; une feuille « Où agir », donc
   aussi un tableau du chapitre des temps du rapport.

## Décision : les photos des indicateurs et l'état cible (M6, M8)

1. **Une photo est figée** (`Model/SnapshotRecords.cs`, `StreamSnapshot`) : les indicateurs de la carte racine et de ses
   détails tels que les voit qui la prend (`ValueStreams/StreamIndicators.cs` : délai et deux de ses parties, l'attente
   après un relais et l'autre attente, travail par dossier, part de valeur
   ajoutée et couverture, relais et relais par dossier, ruptures, %C&A cumulé, heures perdues et irritants ouverts), sous
   un libellé saisi de 100 caractères au plus. Ce sont les seuls indicateurs enregistrés ; hors du journal des opérations
   (Ctrl+Z ne l'annule pas), comme le résumé d'une carte. Écart au plan : des colonnes typées de `stream_snapshot`, pas un
   JSON (le cœur n'a droit au JSON que dans `Sheets/SheetJson.cs`) ; plus strict, chaque indicateur nouveau demande une
   migration. Les deux attentes (F-105, choix du client du 07/11) sont venues ainsi, par la migration `SnapshotWaits` : la
   comparaison d'un état cible dit le temps que les relais font perdre, le cœur de Makigami ; une photo prise avant elles
   les a inconnues. Ce sont des parties du délai (`LeadParts` : le reste est le temps où le dossier est traité) ; le
   travail par dossier est une autre mesure, le travail réparti sur les dossiers : une étape faite à cadence n'en compte
   qu'une part par dossier. Le délai moins les deux attentes n'est donc pas le travail par dossier, et l'écran les montre
   comme « dont » sous le délai, sans en déduire le travail (exemple : 31,96 h, dont 12,06 h après un relais et 19,17 h
   d'autre attente, pour 32 min de travail par dossier).
2. **Origine** : `photo`, prise par un animateur ; `baseline`, la référence d'un état cible, une au plus, qui ne se
   supprime pas et reste lisible quand la source a disparu (`Snapshots/SnapshotRules.cs`).
3. **L'état cible** est un projet neuf fait par l'export puis l'import, en mémoire, du paquet complet de sa source (fiches,
   documents, irritants, résumés, traductions, personnes), sous un code libre et son nom ; il nomme sa source
   (`project.baseline_project_id`, sans clé étrangère : la source peut partir à la corbeille puis être purgée) et garde les
   indicateurs de la source à cet instant comme référence. Administrateur, comme « Dupliquer ». **Relire la référence**
   (`RefreshBaselineCommand`) revient à l'**animateur de l'état cible** (choix du client du 07/11, P3e : c'est lui qui
   conduit l'atelier), l'administrateur gardant le droit comme partout ; il faut voir la source (en être membre, ou
   administrateur), sinon ACCES_REFUSE, et ses indicateurs sont lus tels que la personne les voit sur la source, selon son
   rôle là (un brouillon compte pour un animateur de la source, pas pour un de ses lecteurs). La lecture des photos dit à
   l'écran où en est la source (`BaselineSourceQuery` : `visible` avec son code, `hidden` pour qui n'en est pas membre,
   `missing` quand elle n'est plus vivante), pour que le bouton grisé dise pourquoi.
4. **Le paquet 2.3 porte les photos et la source** (P3d, `Packages/SnapshotEntries.cs`) : dans l'objet `project` de
   `project.json`, la liste `snapshots` (absente sans photo) et `baselineProject`, le **code** de la source, écrit
   seulement quand elle est vivante et que la personne qui exporte peut la voir (`IProjectReader.LiveCodeAsync`). Un
   identifiant ne veut rien dire dans une autre installation : l'import relie l'état cible au projet vivant de ce code que
   la personne qui importe peut voir (`IProjectReader.LiveIdAsync`), jamais au projet qu'il remplace ; sinon le lien reste
   vide et la photo de référence demeure. Les indicateurs s'écrivent dans la forme la plus courte qui se relit à la même
   valeur, jamais par un décimal (`CanonicalJson.Real`) ; deux photos du même instant se rangent par leur texte. Le
   `.pmap` ne porte ni l'un ni l'autre.
5. **Créer un état cible ne reprend aucune photo de la source** : son paquet les porte désormais, la commande les écarte ;
   la cible ne garde que sa référence et son lien, imposés par la commande.
6. **L'assistant reçoit les indicateurs et les photos** (F-109, décision du porteur du 07/11, fait le 08/11) : la
   description du projet donnée au modèle laisse toujours de côté les photos et la source (le paquet) ; à la place, un
   bloc `<indicators>` les lui donne déjà calculés par le cœur, comme l'écran : les indicateurs d'aujourd'hui tels que la
   personne les voit, puis chaque photo, la référence d'un état cible d'abord, chaque indicateur `avant -> maintenant`,
   avec `better` ou `worse` selon le sens que dit le cœur, et la journée de travail
   (`Assistant/AssistantContext.cs` : `AssistantIndicators` ; `AssistantInstructions.IndicatorsOf`). Il va aux pratiques
   qui lisent et analysent (question libre, question, analyse, rapport), pas aux autres. La consigne commune lui dit de
   les citer tels quels, sans les recalculer, sous les noms du volet « Analyse » ; celle du rapport, de dire le gain
   contre la référence ou une photo dans le résumé et la conclusion.
7. **La source remplacée est suivie** (F-108, décision du porteur du 07/11) : remplacer la source par un import lui donne
   un identifiant neuf ; dans la même unité de travail, chaque état cible qui la nommait, vivant ou à la corbeille, nomme
   le projet qui prend sa place (`SnapshotRules.FollowReplacement`, `IProjectReader.TargetsOfAsync`), qu'il soit ou non
   visible de qui remplace : le lien est gardé entier, jamais montré. L'événement de l'import le compte
   (`targets_relinked`, absent sans état cible). Suivre la trace à la lecture a été écarté : une colonne de plus, et la
   trace se perdrait à la purge de l'ancien projet.
8. **Une seule lecture des fiches par le flux de valeur** (`ValueStreams/StreamFacts.cs`) : ce que la carte montre, ce
   que garde une photo (`StreamFacts.IndicatorsOf`) et la photo du projet d'exemple (F-110 : « Avant l'atelier »,
   calculée par le cœur sur l'exemple tel qu'il est, jamais écrite à la main) passent par elle ; un changement du calcul
   fait tomber le test des exemples livrés jusqu'à leur réécriture. L'exemple a reçu de quoi tout chiffrer : une fiche
   validée avec sa durée sur chaque tâche et la décision, un délai sur les étapes qui reçoivent un relais et sur l'attente,
   les parts du OU inclusif, l'effectif des couloirs de personnes.
9. **Une étape au détail vide garde sa fiche** (F-102, décision du porteur du 07/11) : tant que « Détailler » n'a donné
   aucune étape au détail, le flux de valeur lit la fiche de l'étape, comme il lui garde déjà ses volumes, ses relais et sa
   classe ; dès que le détail a une étape, c'est lui qui donne les chiffres (`ValueStream.Assemble`).
10. **L'écran (P3e)** : dans le volet « Analyse », sur la carte principale seulement (les photos portent sur tout le
    projet), une section **Photos** (prendre une photo sous son libellé, la supprimer : animateur ; chacune se compare à
    maintenant, la comparaison étant faite par le cœur pour chaque photo, `SnapshotView.Changes`) et, pour un état cible,
    une section **État cible** (sa référence, son projet source ou la raison pour laquelle il ne peut plus être relu, la
    comparaison, « Actualiser la référence »). Une comparaison est un tableau : les deux attentes sous le délai, en retrait,
    jamais additionnées au travail ; l'écart en vert ou en rouge selon le sens que dit le cœur. Les noms des indicateurs
    sont ceux du Bilan des exports, déjà dans les 18 langues. « Créer l'état cible… » est au menu Fichier, à côté de
    « Dupliquer le projet… » (administrateur), son dialogue propose « {nom} (état cible) » et « État actuel ».
11. **Une proposition de l'assistant porte la classe de valeur** (F-103, choix du client du 07/11) : la classe donnée par
    le `.pmap` 1.4 de la proposition entre dans l'état voulu (`DesiredStep.ValueClass`), s'affiche dans l'aperçu comme un
    champ changé et s'applique avec le reste ; une étape sans classe dans la proposition garde la sienne (une proposition
    n'enlève jamais une classe choisie par l'équipe) ; une classe sur une étape dont le détail a des étapes est refusée dès
    l'aperçu (VALEUR_INVALIDE, avec le code de l'étape), comme le type.

## Décision : la durée la plus courte et la plus longue, le plan d'actions (M5, M7)

1. **Les bornes de la durée** (`Values/DurationBounds.cs`, `Sheets/SheetContentReader.cs`) : deux champs de la fiche,
   `duration_min_minutes` et `duration_max_minutes`, chacun facultatif (un seul suffit : « au moins 10 minutes », « au
   plus 25 minutes »), avec les bornes d'une durée ; dans l'ordre autour de la durée (la plus courte au plus la durée, la
   plus longue au moins la durée, la plus courte au plus la plus longue), sinon `VALEUR_INVALIDE` sur la borne hors
   d'ordre. Ils ne comptent pas dans la complétude et le flux de valeur garde la durée. Le brouillon les porte en colonnes
   de `step_sheet`, la version validée dans son JSON figé seulement : rien ne les lit hors de la fiche. Écran : deux
   champs sous la durée, la borne hors d'ordre dite avant l'envoi ; lecture « 15 minutes (10 à 25 minutes) ».
2. **Le plan d'actions d'un irritant** (`Documents/DocumentRules.cs`, `PainPointPlan`) : l'action décidée (500), qui la
   porte (150, texte libre : la personne peut ne pas être membre), l'échéance (un jour sans heure), chacune facultative,
   saisies avec l'irritant sur l'usage du document ; refusées sans description d'irritant, sur la première valeur donnée ;
   elles changent l'irritant en place, partent avec lui, et l'historique de ses états ne bouge pas.
3. **Le plan d'actions d'une carte** (`Documents/ActionPlan.cs`, `GET /api/maps/{id}/action-plan`, Lecteur) : les irritants
   des étapes vivantes de la carte et des cartes en dessous, quel que soit leur état (le plan montre aussi ce qui est fait),
   dans l'ordre du sous-arbre, de la lecture et des codes ; un irritant détaché de son étape n'est dans aucun plan. Il donne
   la feuille « Plan d'actions » du classeur Excel, après les autres, quand il tient un irritant.
4. **Formats** : le paquet 2.3 (`durationMinMinutes`, `durationMaxMinutes` d'une fiche ; `action`, `owner`, `dueDate` d'un
   irritant) et le `.pmap` 1.4 (`duration_min_minutes`, `duration_max_minutes` ; `action`, `owner`, `due_date` d'un usage),
   non publiés, s'étendent sans version neuve ; l'un ou l'autre en impose la version, une borne dans le JSON figé d'une
   version comprise (la 3.0 figerait le contenu sans elle et le refuserait). Le projet d'exemple montre une borne et un plan.
5. **Écart au plan** (F-111) : les bornes ne passent ni par l'import ni par l'export des tableurs ; l'import lit sa colonne
   « Durée » comme le délai (lot L5) et n'a pas de durée de travail à borner, l'export ne lit pas les versions.

## Décision : l'impression murale (M9)

Un atelier Makigami se tient devant la carte au mur : l'équipe la lit de loin, y pointe un relais, y colle un post-it.
Le PDF A3 met toute la carte sur une page, illisible dès qu'elle passe une dizaine d'étapes. Les logiciels du marché
impriment une affiche en mosaïque : Acrobat Reader (« Affiche » : échelle de la mosaïque, chevauchement, repères de coupe,
étiquettes), Visio (« ajuster à N feuilles en largeur »), draw.io (« affiche », pages en largeur et en hauteur).

1. **Fichier > Imprimer en grand…**, pour tout lecteur comme les autres exports. Le dialogue demande le papier (A4 ou A3
   paysage, A4 par défaut : toute imprimante de bureau), le nombre de pages en largeur (1 à 10 ; la hauteur en découle) et
   les marques du calque Analyse (cochées quand le calque est allumé). Il dit avant tout calcul le nombre de pages, la
   taille de l'affiche en centimètres et son échelle, en pour cent de la carte à 100 % à l'écran ; il propose d'abord le
   moins de pages qui l'imprime au moins à cette taille. Plus de 100 pages sont refusées par le dialogue.
2. **La mosaïque** (`web/src/features/map/export/wallPrint.ts`, fonctions pures) : une marge de 10 mm et une bande de pied
   de 6 mm sur chaque feuille, 10 mm partagés par deux pages voisines, chaque page nommée par sa rangée et sa colonne (A1,
   A2…, B1… ; après Z, AA comme les colonnes d'un tableur) ; la dernière rangée n'imprime que ce qui reste de la carte.
   Aux coins de la zone imprimée, des repères de coupe dans la marge ; un trait dans la marge dit où commencent la voisine
   de droite et celle du dessous : on coupe la marge gauche et la marge haute d'une page, on la pose sur ses voisines.
3. **Le dessin** (`wallPdf.ts`, `exports.ts`) : l'image SVG de la carte (L7) vue par la fenêtre de chaque page, dessinée par
   le navigateur à 150 points par pouce, une page à la fois (aucune image ne dépasse ce qu'un navigateur dessine, quelle
   que soit l'affiche), gardée en JPEG ; sous chaque page le titre de la carte avec sa classification (S-4.1-08) et le nom
   de la page avec son rang. Jamais la copie modifiable de la carte.
4. **Les marques du calque Analyse dans l'image SVG** : les relais dans leur couleur et leur épaisseur, la bande de la
   classe d'une étape, les pointillés d'une rupture de support, les relais d'un détail à la place de « Détaillée », comme
   le canevas les dessine sans tuile en avant ; l'export en image SVG les porte aussi, calque allumé, comme le PNG et le PDF
   qui capturent le canevas.

## Conséquences

- Aucune donnée ni migration pour M2 ; le contrat de l'API gagne `MapHandoffs`, puis `StreamAnalysis` (volet guidé).
- Un couloir sans lien ni homonyme est compté comme un autre responsable : renommer un couloir comme celui du dessus le
  fait reconnaître, comme pour la remontée d'un détail.
- Par dossier, une flèche marquée compte tout son volume, même quand une partie seulement de ses dossiers change de
  responsable (système alimenté par deux couloirs, détail à deux débuts) : la définition reste « nombre moyen de flèches
  de passage parcourues par un dossier ».
- Bornes : 2 000 étapes sur 5 niveaux calculées sous 150 ms (test du cœur).
- Photos et état cible : table `stream_snapshot` et colonne `project.baseline_project_id` (migration `StreamSnapshots`) ;
  le port `IProjectReader` gagne deux lectures (code d'un projet vivant, projet vivant d'un code) ; `PackageRequest` et
  `IReadPackage` portent le code de la source ; `IProjectReader` lit aussi les états cibles d'une source, qui la suivent
  quand un import la remplace (F-108).
- Durées et plan d'actions : colonnes `step_sheet.duration_min_minutes` et `duration_max_minutes`, `pain_point.action`,
  `owner` et `due_date` (migration `SheetRangesAndActions`), la condition d'ordre `NotAbove` des garde-fous (deux colonnes
  décimales, une valeur absente ne compare rien), une route de lecture et une feuille du classeur.
- Impression murale : rien côté serveur ; l'image SVG gagne les marques du calque Analyse, `pdf.ts` partage ses polices,
  son écriture et sa coupe des lignes avec l'affiche.
