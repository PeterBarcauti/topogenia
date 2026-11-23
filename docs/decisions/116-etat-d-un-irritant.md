<!-- 116-etat-d-un-irritant.md -->
<!-- The status of a pain point (3.4, lot 4b): one rule of the closed core, ChangeStatus, a status button that opens a list on the sheet of the receiving step and on the lines of the action plan, one route under the right to fill the sheet, a history that only grows, a removal that keeps every pain point whose status changed, the only opening of the closed core granted for the 3.4 -->

# 116 - L'état d'un irritant

Statut : acceptée le 21/11/2026 (3.4, lot 4b). Prolonge les ADR 112 (le plan d'actions à l'écran), 105 (le `.pmap` 1.5),
064 (la fiche d'étape) et 106 (la vue BPMN). Révise la règle A-0-03 et son voisinage (A-6-34, A-2.12-01, A-2.12-02).
Demande du porteur du projet : lever un irritant (étape automatisée, par exemple) sans perdre sa trace ni fausser le plan.

## Contexte

- L'application ne produit que « signalé » (`PainPointRules.Raise`) ; aucune route ne fait évoluer le statut (A-0-03,
  A-6-34). Un irritant levé ne peut donc qu'être effacé (la trace est perdue) ou rester ouvert (le plan et les heures
  perdues sont faussés). L'ADR 112 l'a laissé de côté (F-221) : il demandait le cœur fermé.
- Les sept états et leur historique existent déjà : `pain_point.status` (CHECK sur les sept codes),
  `pain_point_status_history` (ajout seul, CHECK sur les deux colonnes d'état), et le paquet `.topogenia` les porte
  (`painpoints.json`). Un paquet importé et le projet d'exemple en contiennent.
- « Ouvert » est `MapMarkers.IsOpen` : signalé, pris en compte, à l'étude, en cours. Réalisé, adopté et abandonné sont
  « traités » : l'irritant quitte la carte et ses heures ne comptent plus.
- Le retrait (`PainPointRemoval.Remove`, règle unique AR-02) efface l'irritant tant qu'il est « signalé ». Un irritant
  réalisé puis rouvert serait donc effacé, avec son historique, au premier retrait.

## Décision

1. **Accord du porteur.** Le cœur fermé (`src\Topogenia.Domain` (non publié)) est normalement interdit aux agents. La modification
   du cœur fermé a été **accordée par le porteur du projet le 21/11/2026 pour cette seule règle** : « ok pour le geste »
   (la liste d'état), puis « oui » pour la seconde fonction touchée, `PainPointRemoval.Remove`. Les fichiers du cœur
   touchés sont `Documents\PainPointRules.cs` et `Documents\PainPointRemoval.cs`, avec leurs essais de domaine
   (`Documents\` seulement). Aucun autre fichier du cœur ne change ; l'accord ne couvre aucune autre règle.
2. **La règle `PainPointRules.ChangeStatus(workspace, context, stepId, painPointId, status)`.** L'étape est celle de la
   fiche (`SheetQueries.WritableStep` : vivante, sur une carte vivante, avec contenu, sinon `INTROUVABLE` ou
   `ETAPE_SANS_CONTENU`) ; un usage de cette étape porte l'irritant (`DocumentUse.PainPointId`), sinon `INTROUVABLE` (un
   irritant d'une autre étape, d'un autre projet, détaché ou effacé n'est pas distingué d'un absent) ; l'état est l'un
   des sept (`Enum.IsDefined`, sinon `VALEUR_INVALIDE`). **Toutes les vérifications précèdent toute écriture.** Les
   sept états acceptés, **toutes les transitions permises** (de chacun vers chacun des six autres : rouvrir, c'est
   choisir « signalé » ou « en cours »). Choisir l'état qu'il a déjà n'écrit rien (ni version, ni historique). Sinon :
   `painPoint with { Status }` et une ligne `PainPointStatusChange(avant, après, commentaire nul, ActorId, Now)`.
   `PainPointRules.LatestChange` donne la dernière ligne de l'historique (l'ordre de l'historique est un fait du
   domaine) ; elle sert à la date montrée sur la ligne du plan.
3. **Le retrait garde tout irritant dont l'état a changé.** `PainPointRemoval.Remove` efface un irritant seulement s'il
   est « signalé » **et** que son historique n'a que sa ligne de création ; sinon il est détaché de son étape
   (`StepId = null`) et gardé avec son historique. Un irritant réalisé puis rouvert n'est jamais effacé : la trace que le
   lot doit garder ne se perd pas par un retrait.
4. **La route.** `PUT /api/steps/{id}/pain-points/{painPointId}/status`, corps `{ "status": "implemented" }`, `{id}` est
   l'étape qui reçoit (celle dont la fiche se remplit, comme `/uses`). Commande `ChangePainPointStatusCommand` par le
   gabarit (`ProjectCommand`) : `[RequiresSheetFill]` (le droit de `UseDocumentCommand`, aucune autre règle des droits),
   `If-Match` sur la version de l'irritant, exigé en mode Serveur (428 `VERSION_REQUISE`), verrou du projet, tout ou
   rien (l'état et sa ligne d'historique dans le même ensemble de changements). Deux personnes parties de la même
   version : l'une écrit, l'autre reçoit 409 et l'interface relit. `LogKind` nul (rien dans le journal des opérations),
   `AuditKind` nul (l'historique de l'irritant est la trace métier). Réponse : la fiche de l'étape relue, comme toute
   écriture de document. `ActionPlanLine` gagne `Version` (de l'irritant) et `StatusChangedAt`.
5. **Cinq états montrés, sept gardés.** La liste offre cinq états, dans l'ordre de la roue PDCA : signalé (Plan), en
   cours (Do), réalisé (Check), adopté (Act), puis abandonné, à part après un séparateur ; elle montre en plus l'état que
   l'irritant a déjà quand c'est l'un des deux autres (pris en compte, à l'étude : le projet d'exemple en porte).
   Réalisé, adopté et abandonné sortent de la carte et du total des heures ouvertes. Chaque choix porte sa ligne d'aide
   dans la liste même (`sheet.pain_point.status_helps.*`), pas seulement une info-bulle que le clavier et le tactile ne
   voient pas. La liste des cinq est une constante de l'interface, `OFFERED_STATUSES`, écrite une fois, lue par la fiche
   et le volet, et un essai la tient incluse dans l'énumération du contrat. Les libellés sont ceux du format
   (`sheet.pain_point.statuses.*`). Réponse du porteur du 21/11/2026 : cinq états, « ok mais il ne faut pas que ça
   complique, il faut que chaque choix soit effectivement documenté ».
6. **Le geste** (révisé par la retouche R4c du 21/11/2026, relecture M1). Un **bouton d'état qui ouvre une liste**, le
   geste des tableaux de tâches (Planner, Jira) : le bouton montre l'état seul ; un clic, Entrée, Espace ou Alt+Bas
   ouvre la liste sur l'état du moment, marqué ; Haut et Bas s'y déplacent **sans rien écrire** ; un clic ou Entrée sur
   un choix l'écrit une seule fois, sans bouton « Enregistrer » (rien quand c'est l'état du moment) ; Échap ferme sans
   écrire et rend le focus au bouton. Un seul composant, `PainPointStatusOptions`, posé sur le menu partagé
   (`shared/ui/Menu.tsx`, rôles `menu` et `menuitemradio`, `aria-haspopup` et `aria-expanded` sur le bouton), sert dans
   le cadre de l'irritant de la fiche de l'étape qui reçoit (par la file d'écriture des documents de la fiche, après les
   écritures de l'usage en cours) et sur chaque ligne du volet « Plan d'actions » dont `can_fill` est vrai. Le nom lu du
   bouton contient l'état qu'il montre. Après l'écriture le plan est relu ; sous « Ouverts », une ligne réalisée, adoptée
   ou abandonnée sort de la liste ; un message le dit, dans une région `aria-live="polite"` présente avant lui (il est donc
   annoncé), avec un bouton « Annuler » dont le nom lu commence par ce mot, qui prend le focus et écrit l'état d'avant
   comme une ligne de plus. Pas de Ctrl+Z : comme toute écriture de fiche (B-2.6-38), le changement n'entre pas dans le
   journal des opérations, et défaire effacerait une ligne d'un historique en ajout seul. La vue BPMN lit l'état et
   n'offre pas le bouton (la fiche y est en lecture seule, ADR 106). Le lecteur voit l'état sans le changer.
7. **La date.** `ChangedAt` est l'instant de l'opération en heure universelle, comme chaque enregistrement ; l'interface
   ne montre que le jour, dans le fuseau du navigateur (`formatDate`), jamais l'heure ni une coupe de la chaîne
   universelle. Une ligne traitée dit « réalisé depuis le … ». Aucune heure de construction n'est écrite.
8. **A-0-03 révisée** : « Les `PainPoint` n'ont pas de route propre : ils sont créés, modifiés et retirés par les
   `DocumentUse` et le `Remainder` ; leur statut n'évolue que par une route, `PUT /api/steps/{id}/pain-points/{painPointId}/status`,
   sous le droit de remplir la fiche de l'étape dont un usage porte l'irritant, une ligne ajoutée à l'historique, jamais
   une ligne changée. » A-6-34 (le statut d'irritant évolue par la seule route de A-0-03), A-2.12-01 (reported est
   l'état de création ; la route accepte les sept, la liste de la fiche et du volet en offre cinq) et A-2.12-02 (jamais effacé au-delà de
   reported ni après un changement d'état) sont révisées dans `docs\couverture.md` (non publié) et `docs\couverture-p2\Sheets-Documents.md` (non publié).
9. **Le paquet du cœur 3.4.0.** Dès qu'un fichier du cœur change, tout ZIP livré demande un paquet du cœur neuf
   (`build\package.mjs` refuse un paquet plus ancien que les sources). Le chef d'orchestre fabrique `Topogenia.Core.3.4.0`
   une fois, après la relecture, par `build\core\licence.mjs pack` (non publié) (clés passées par leur chemin, comme le 3.2.0), après
   avoir posé 3.4.0 dans `Directory.Packages.props` ; il vérifie marque, empreinte, brouillage et documents publics, et
   le porteur sauvegarde ensuite son dossier de clés. Aucun agent de code ne l'exécute.
10. **Une copie `.pmap` entière garde l'état** (F-210, tâche T3, hors du cœur). À l'ouverture d'une copie entière (et à
    l'insertion d'un fichier, qui partage le même chemin), l'irritant naît par `DocumentUses.Use`, puis
    `PainPointRules.ChangeStatus` lui donne l'état du fichier s'il n'est pas « signalé », traité compris ; l'historique
    de la copie commence à la copie. Une proposition de l'assistant rouverte depuis sa carte d'origine (`PmapFiles.Carry`
    relit les fiches de cette carte avec leur état) garde l'état de leurs irritants ; elle n'en ajoute aucun d'elle-même.
    **La duplication d'un
    projet ne copie aucune fiche, donc aucun irritant** (AR-10, A-1.2-21) : elle n'a rien à garder. Le format ne
    change pas.

## Écarté

- **L'état écrit par l'application hors du cœur** (`ProjectWorkspace.Put`, ADR 112) : une règle réécrite hors du cœur.
- **Un état dans le corps de `PUT /uses`** : une écriture de l'usage changerait l'état.
- **Un flux de transitions imposé** : la robustesse vient de l'historique, pas d'un parcours obligé.
- **Les sept états à l'écran** : deux d'entre eux (pris en compte, à l'étude) restent dans le format, le paquet et le
  cœur, montrés seulement quand l'irritant les a. Cinq états suffisent, dans l'ordre de la roue PDCA.
- **Une liste déroulante native** (`select`) : sous Windows, chaque flèche d'une liste fermée change sa valeur et
  l'écrirait ; l'historique garderait des états jamais choisis (relecture M1).
- **Ctrl+Z** : l'historique est en ajout seul et la fiche n'est pas annulable ; « Annuler » du volet et le nouveau choix
  suffisent.
- **Un commentaire obligatoire** : la colonne existe, l'interface ne l'offre pas.
- **Un événement d'audit** : l'historique de l'irritant est la trace.
- **Une colonne de date dans l'export** : un fichier relu plus tard mentirait ; l'état est déjà dans la colonne « État ».
- **Une migration** : aucun huitième état, aucune colonne, aucune table.

## Conséquences

- Deux fichiers de logique du cœur changent (`PainPointRules.cs`, `PainPointRemoval.cs`), avec leurs essais de
  domaine, une commande, une route (la seule à porter `/status` sous `pain-points`), deux champs sur la ligne du plan ;
  le contrat OpenAPI est réécrit. Aucune migration, aucune dépendance, aucun appel sortant (`connect-src 'self'`
  inchangé).
- Un irritant réalisé, adopté ou abandonné quitte la carte, le filtre « Ouverts » et le total des heures perdues par le
  `IsOpen` existant ; les photos des indicateurs du flux de valeur gardent les chiffres d'avant. Aucune seconde copie de
  la liste des états traités n'est écrite.
- Le paquet `.topogenia` et l'export portent l'état et son historique sans changement de format.
- F-221 est traité ; F-210 ne garde que ce que le lot ne couvre pas (le code de l'irritant, les commentaires de
  présentation de l'assistant).

## Vérification

Essais écrits avec le code de chaque partie :

- Domaine : `PainPointStatusTests` (un changement ajoute une ligne ; les 42 paires de transitions ; même état sans
  écriture ; retour à l'état d'avant ; irritant non porté par un usage de l'étape introuvable et rien ne change ;
  étape sans contenu ou supprimée ; état indéfini refusé ; irritant traité fermé pour les marqueurs et le plan ;
  dernier changement ; irritant dont l'état a changé jamais effacé), `PainPointRulesTests`
  (`Statuses_are_the_stored_codes_and_a_raised_pain_point_is_reported`), `DocumentUsesTests`
  (`An_empty_description_keeps_a_reopened_pain_point_attached_to_the_map`).
- Application et hôte, deux bases : droit de remplir la fiche de l'étape qui reçoit pour chaque rôle, version de
  l'irritant, une unité sans entrée du journal ni de l'audit, 428 sans `If-Match`, 409 à la seconde écriture et une seule
  ligne d'historique, 400 pour un état inconnu, 404 pour un étranger et un irritant d'une autre étape, réponse = la fiche ;
  `RouteTableTests` (une route de plus), `RightsMatrixTests` (une ligne), `IfMatchCoverageTests` sans exemption.
- Format : l'état changé et son historique font l'aller-retour du paquet sans changement.
- Interface et parcours : essais de la fiche et du volet, `web\e2e\pain-point-status-34.spec.ts` en Poste et en Serveur.
- Témoins de mutation : la garde « même état », la ligne d'historique, `before` pris après le changement, l'écriture
  avant la recherche de l'usage, le retrait qui ne lit que `Status`, `[RequiresSheetFill]`, `SheetStepId`, `Versions`,
  `LogKind`, la liste montrée sans le droit de remplir.

## Révision (21/11/2026) : l'irritant sans document, l'état sous les Sorties, le paquet du cœur 3.4.1 (retouche R10)

Constat : un irritant rattaché à une étape sans usage qui le porte (un paquet écrit à la main en apporte ; une étape qui
recevait le document a pu être supprimée) ne se lisait dans aucune fiche, et aucune étape ne pouvait changer son état :
la garde de `ChangeStatus` exigeait un usage de l'étape. **Accord du porteur du 21/11/2026 (voie A) : le cœur fermé est
modifié pour cette seule garde.**

1. **Le cœur.** `PainPointRules.ChangeStatus` accepte aussi, sous la route d'une étape, un irritant **qu'aucun usage
   vivant ne porte** (un usage d'une étape vivante sur une carte vivante) quand il est rattaché à cette étape ; un
   irritant qu'un usage vivant porte garde sa seule place, l'étape qui reçoit. Rien d'autre du cœur ne change ; les
   vérifications précèdent toujours l'écriture. Le droit est celui de remplir la fiche de cette étape
   (`[RequiresSheetFill]`, inchangé).
2. **La fiche.** `SheetView` gagne `StepPainPoints` (`step_pain_points`) : les irritants rattachés à l'étape qu'aucun
   usage vivant ne porte, dans l'ordre des codes, lus par la même définition de l'usage vivant que le plan
   (`ActionPlanLines.LiveStep`). L'interface les montre dans une partie **« Irritants de l'étape »**, après Sorties,
   repliable avec son compte, dessinée seulement quand l'étape en porte : code, état, description, temps perdu, plan, en
   lecture (la description et le plan ne s'écrivent que par un usage), et le bouton **État** pour qui peut remplir la
   fiche.
3. **Le plan.** `ActionPlanLine` gagne `StatusStepId` (`status_step_id`) : l'étape sous laquelle l'état se choisit,
   celle qui reçoit, ou celle de l'irritant quand aucun usage vivant ne le porte. `CanFill` est le droit de remplir sa
   fiche. Les champs de l'étape qui reçoit (`ReceivingStepId`, `ReceivingMapId`, `DocumentId`) ne changent pas de sens :
   « Modifier le plan » reste offert sur les seules lignes qui ont un document.
4. **Les Sorties.** Sous l'irritant noté par l'étape qui reçoit, pour qui peut remplir sa fiche (le droit que la carte
   donne avec chaque étape, celui du lien « Signaler un irritant dans CODE »), le bouton **État** (route existante sous
   l'étape qui reçoit ; la fiche de l'étape qui produit est relue ensuite) et le lien **« Modifier dans CODE »**, qui
   prend le chemin de « Signaler un irritant dans CODE » et le remplace pour ce consommateur. Sans ce droit, et dans la
   vue BPMN 2.0, l'état se lit.
5. **Le paquet du cœur 3.4.1.** L'archive du cœur (ADR 087) a déjà une entrée 3.4.0, et ce paquet est dans des
   dossiers d'essai déjà remis : une entrée d'archive ne se récrit jamais. Le paquet refait prend donc la
   version 3.4.1, le produit restant en 3.4.0 : `Directory.Packages.props` dit 3.4.1, et `build\core\licence.mjs` (non publié) donne
   cette version au `dotnet pack` (`-p:PackageVersion`), jusque-là égale à celle du produit. Fabrication et
   vérifications : au journal de la construction.
6. **Le paquet 3.4.1 refait (23/11/2026).** Le pré-audit de la publication a demandé deux retouches de la chaîne du
   cœur, sans changement de code : les entrées du paquet datées du jour de la version à minuit UTC (aucune heure de la
   fabrication), et la bibliothèque déclarant la version du paquet (3.4.1.0 en version de fichier, 3.4.1 en version du
   produit). Le paquet est refait sous le même numéro : son code est celui du premier, qui n'a servi qu'à des copies
   d'essai et d'audit et n'est publié nulle part. Le paquet 3.4.1 qui fait foi a pour SHA-256
   `9e83db5c6e2f70a95d746002005ba3cdd25cc6b48373569207d9aea07ad9cb04` ; le premier,
   `ec32fbe50dd65be363909218a6fd5521697a16fe3e99ecf7567fa97505ea38fc`, ne doit plus servir (un poste qui l'aurait gardé
   dans son cache NuGet est arrêté par le `contentHash` des fichiers de verrouillage, NU1403). L'archive du cœur garde
   les deux entrées, distinguées par leur date de fabrication et leur empreinte (`licence.mjs verify`).

Écarté : l'état écrit hors du cœur pour ces irritants (deux sources d'une règle) ; un nouveau champ « étape qui reçoit »
qui ne recevrait rien ; la description et le plan d'un irritant sans document modifiables (ils appartiennent à l'usage).

Vérification : `PainPointStatusTests.A_pain_point_no_live_use_carries_changes_its_status_on_the_sheet_of_its_own_step_only`
(trois témoins de mutation), `ActionPlanQueryTests` (ligne sans usage, droit par rôle), `SheetStepPainPointsTests` (deux
bases : la fiche, les droits par rôle, la route), `PublicationRulesTests` (la version donnée au `pack`), essais de
l'interface (`stepPainPoints.test.tsx`, `status.test.tsx` du plan), parcours `pain-point-status-34.spec.ts` (irritant
sans document posé par l'API) et `branches.spec.ts` (« Modifier dans CODE »).
