<!-- api-selection.md -->
<!-- Couverture P4-T04 lot B : points d'entrée HTTP de la sélection et de la remontée du détail -->

# Couverture phase 4, P4-T04 lot B : sélection et remontée au niveau HTTP

Gestionnaires : `src/Topogenia.Host/Api/Selection/SelectionApi.cs`, `src/Topogenia.Host/Api/Dissolutions/DissolutionApi.cs`.
Routes : `src/Topogenia.Host/Endpoints/SelectionEndpoints.cs`. Tests : `tests/Topogenia.Host.Tests/Selection/`,
`tests/Topogenia.Host.Tests/Dissolutions/`, chaque test sur SQLite et SQL Server, par HTTP seulement, sauf l'écriture
des documents du reliquat (voir « Décisions du lot »).

## Décisions du lot

- **Corps** (AR-60, noms 1.x traduits) : suppression `{steps, flows, stitch, sub_maps}` ; collage `{steps: [{key, label,
  type, lane_id, column, row}], flows: [{source, target, label}], anchor: {step_id, position}}` ; transfert `{steps: [{id,
  lane_id, column, row}], cut, anchor}` ; « Détailler la sélection » `{steps, label}` ; affecter `{kind, object_id, step_id,
  from_step_id}` ; retirer `{kind, object_id}`. Les noms `steps`, `flows`, `label`, `position`, `kind`, `step_id`,
  `from_step_id` sont ceux que le domaine cite dans ses refus.
- **`sub_maps`** : un code de `SubMapChoice` lu par `ApiInput.Code` ; seul `delete` demande la suppression des
  sous-cartes remplies. Un code inconnu (dont le `supprimer` 1.x) est refusé `VALEUR_INVALIDE(sub_maps)` ; la 1.x
  l'ignorait (tout autre texte que `supprimer` valait « pas de choix »).
- **Réponses** : les quatre routes de la sélection et « Remonter » répondent un `StructureReply` : `ids` = étapes
  supprimées, collées ou déplacées ; regroupement : `id`, `code` de la nouvelle étape, `ids` = sa sous-carte ;
  remontée : `id` = la remontée (le cadre), `code` = l'étape remontée, `ids` = les étapes du cadre (1.x
  `ResultatStructure(remontee, macro.Code, ...)`). « Affecter » et « Retirer » répondent un `FrameReply`, « Terminer »
  un `FinishReply` : les champs du `StructureReply` (`id` = la remontée), plus ce que la 1.x renvoyait seul, le
  cadre (`frame`, ex-`RemonteeVue`, nul une fois fermé) ou les rangements (`arrangements`, ex-`Rangement`). Le client
  1.x lisait ces objets ; la carte à jour et l'historique s'y ajoutent comme pour toute opération de structure (AR-50).
- **Documents du reliquat** : les routes des documents relèvent du lot C, écrit en même temps. L'aide de test
  `Dissolutions/DetailedRow.cs` écrit les sorties de l'étape remontée par `AddDocumentCommand`, lancée dans une portée
  de l'hôte au nom de l'animateur. Tout le reste passe par HTTP.
- **Annulation d'une remontée** : aucune route `/api/dissolutions/{id}/cancel` (AR-08) ; `POST /api/maps/{id}/undo`
  la défait.
- `TRANSFERT_HORS_PROJET` (A-2.8-26) n'est pas traité ici : le chef de projet s'en charge.

## Lignes couvertes

| ID | État | Test 2.0 | Remarque |
|---|---|---|---|
| A-1.3-14 | couvert (API) | `Topogenia.Host.Tests.Selection.DeletionAndPasteRoutesTests.A_deleted_selection_is_stitched_answered_with_its_steps_and_undone_by_the_map` ; `Topogenia.Host.Tests.Selection.DeletionAndPasteRoutesTests.A_filled_sub_map_asks_for_the_delete_choice_whose_code_is_checked_and_which_takes_the_sub_map` ; `Topogenia.Host.Tests.Selection.SelectionRightsTests.The_selection_routes_hide_the_map_from_a_stranger_and_refuse_a_contributor_and_a_reader` | `stitch` (flèche de raccord), `sub_maps=delete`, `undo` `supprimer` ; 404 non-membre, 403 contributeur et lecteur |
| A-1.3-15 | couvert (API, en partie) | `Topogenia.Host.Tests.Selection.DeletionAndPasteRoutesTests.A_deleted_selection_is_stitched_answered_with_its_steps_and_undone_by_the_map` | identifiant d'étape en double dédoublonné sans refus (`ids` = une étape) ; la limite de 2 000 reste prouvée au domaine |
| A-1.3-17 | couvert (API) | `Topogenia.Host.Tests.Selection.DeletionAndPasteRoutesTests.A_pasted_copy_answers_its_new_steps_in_order_with_their_flow_and_is_undone_by_the_map` ; `Topogenia.Host.Tests.Selection.DeletionAndPasteRoutesTests.A_paste_after_an_anchor_takes_the_place_of_its_outgoing_flow` | `ids` dans l'ordre de la demande, `type`, `row`, flèche entre clés avec libellé ; ancre `after` ; `undo` `coller` |
| A-1.3-18 | couvert (API) | `Topogenia.Host.Tests.Selection.DeletionAndPasteRoutesTests.A_paste_refuses_a_missing_lane_or_column_an_unknown_lane_and_an_unknown_anchor_position_writing_nothing` | `VALEUR_INVALIDE(position)`, rien d'écrit ; ancre sans `step_id` → `CHAMP_OBLIGATOIRE(step_id)` |
| A-1.3-19 | couvert (API) | `Topogenia.Host.Tests.Selection.DeletionAndPasteRoutesTests.A_paste_refuses_a_missing_lane_or_column_an_unknown_lane_and_an_unknown_anchor_position_writing_nothing` | couloir inconnu → `VALEUR_INVALIDE(lane_id)` ; `lane_id` ou `column` absent → `CHAMP_OBLIGATOIRE` (A-2.18-07) |
| A-1.3-20 | couvert (API) | `Topogenia.Host.Tests.Selection.TransferAndGroupRoutesTests.A_block_cut_to_a_sub_map_keeps_its_code_leaves_its_flow_stitched_and_the_undo_of_the_target_map_brings_it_back` | `cut`, l'étape garde son code ; `undo` `deplacer_bloc` sur la carte d'arrivée |
| A-1.3-21 | couvert (API) | `Topogenia.Host.Tests.Selection.SelectionRightsTests.The_selection_routes_hide_the_map_from_a_stranger_and_refuse_a_contributor_and_a_reader` | contributeur et lecteur → 403 `ACCES_REFUSE` ; origine et cible du même projet ; niveau Sécurité à P4-T08 |
| A-1.3-22 | couvert (API) | `Topogenia.Host.Tests.Selection.TransferAndGroupRoutesTests.A_transfer_needs_the_identifier_of_each_step_and_refuses_an_unknown_step_or_its_own_detail` | étape inconnue → 404 `INTROUVABLE` ; `id` absent → `CHAMP_OBLIGATOIRE(id)` ; `TRANSFERT_CYCLE` |
| A-1.3-23 | couvert (API) | `Topogenia.Host.Tests.Selection.TransferAndGroupRoutesTests.Detailing_a_selection_answers_the_new_step_and_its_sub_map_and_is_undone_by_the_map` | `id`, `code` de la nouvelle étape, `ids` = sa sous-carte, flèches recréées ; `undo` `regrouper` |
| A-1.3-24 | couvert (API) | `Topogenia.Host.Tests.Selection.TransferAndGroupRoutesTests.A_grouping_needs_a_label_and_steps_of_its_map_and_warns_beyond_the_recommended_depth` | `CHAMP_OBLIGATOIRE(label)` |
| A-1.3-25 | couvert (API) | `Topogenia.Host.Tests.Selection.TransferAndGroupRoutesTests.A_grouping_needs_a_label_and_steps_of_its_map_and_warns_beyond_the_recommended_depth` | `ETAPE_HORS_CARTE` avec le `code` de l'étape |
| A-1.3-26 | couvert (API) | `Topogenia.Host.Tests.Selection.TransferAndGroupRoutesTests.A_grouping_needs_a_label_and_steps_of_its_map_and_warns_beyond_the_recommended_depth` | `PROFONDEUR_DEPASSEE` dans `warnings` à la 4e sous-carte (profondeur recommandée 4), aucun avant |
| A-1.5-01 | couvert (API) | `Topogenia.Host.Tests.Dissolutions.DissolveRoutesTests.A_dissolution_answers_its_frame_the_dissolved_code_and_the_frame_steps_and_the_undo_of_the_map_takes_it_back` ; `Topogenia.Host.Tests.Dissolutions.DissolveRoutesTests.A_dissolution_checks_the_version_read_of_the_step_and_needs_a_detail` ; `Topogenia.Host.Tests.Dissolutions.DissolveRoutesTests.A_dissolution_is_hidden_from_a_stranger_refused_to_a_contributor_and_an_open_frame_is_not_grouped` | `POST /api/steps/{id}/dissolve` ; `DETAIL_ABSENT` ; `If-Match` facultatif ; 404 non-membre, 403 contributeur |
| A-1.5-02 | couvert (API) | `Topogenia.Host.Tests.Dissolutions.DissolveRoutesTests.A_dissolution_answers_its_frame_the_dissolved_code_and_the_frame_steps_and_the_undo_of_the_map_takes_it_back` | `id` = la remontée, égal à `map.dissolution.id` ; `code` = l'étape remontée ; `ids` = étapes du cadre |
| A-1.5-03 | couvert (API) | `Topogenia.Host.Tests.Dissolutions.RemainderRoutesTests.An_assignment_answers_the_frame_left_with_the_map_brings_back_from_its_holder_and_is_undone_by_the_map` ; `Topogenia.Host.Tests.Dissolutions.RemainderRoutesTests.The_frame_routes_hide_the_frame_from_a_stranger_and_refuse_a_contributor` | `frame` restant et carte à jour ; `undo` `affecter` ; remontée inconnue → 404 |
| A-1.5-04 | couvert (API) | `Topogenia.Host.Tests.Dissolutions.RemainderRoutesTests.An_assignment_refuses_an_unknown_kind_a_missing_object_or_step_and_an_unknown_frame` | `VALEUR_INVALIDE(kind)` ; aussi pour « Retirer » |
| A-1.5-05 | couvert (API) | `Topogenia.Host.Tests.Dissolutions.RemainderRoutesTests.A_closed_frame_takes_no_assignment_and_no_removal` | 409 `REMONTEE_TERMINEE` |
| A-1.5-06 | couvert (API) | `Topogenia.Host.Tests.Dissolutions.RemainderRoutesTests.An_assignment_refuses_an_unknown_kind_a_missing_object_or_step_and_an_unknown_frame` | `CHAMP_OBLIGATOIRE(step_id)` ; `object_id` absent → `CHAMP_OBLIGATOIRE(object_id)` |
| A-1.5-10 | couvert (API) | `Topogenia.Host.Tests.Dissolutions.RemainderRoutesTests.Removing_from_the_remainder_is_for_good_logs_nothing_and_an_element_already_removed_is_not_found` | `operation_id` nul, `frame` restant |
| A-1.5-11 | couvert (API) | `Topogenia.Host.Tests.Dissolutions.RemainderRoutesTests.A_closed_frame_takes_no_assignment_and_no_removal` | 409 `REMONTEE_TERMINEE` |
| A-1.5-12 | couvert (API) | `Topogenia.Host.Tests.Dissolutions.RemainderRoutesTests.Removing_from_the_remainder_is_for_good_logs_nothing_and_an_element_already_removed_is_not_found` | élément déjà retiré → 404 `INTROUVABLE` |
| A-1.5-13 | couvert (API) | `Topogenia.Host.Tests.Dissolutions.RemainderRoutesTests.Finishing_answers_the_arrangements_closes_the_frame_and_a_second_finish_answers_none` | `arrangements`, `map.dissolution` nul ; `undo` `terminer_remontee` rouvre le cadre |
| A-1.5-14 | couvert (API) | `Topogenia.Host.Tests.Dissolutions.DissolveRoutesTests.A_dissolution_answers_its_frame_the_dissolved_code_and_the_frame_steps_and_the_undo_of_the_map_takes_it_back` | `POST /api/maps/{id}/undo` (`remonter`) défait la remontée : étape détaillée et sous-carte revenues, cadre nul ; aucune route d'annulation (AR-08) |
| A-2.8-10 | couvert (API) | `Topogenia.Host.Tests.Selection.DeletionAndPasteRoutesTests.A_filled_sub_map_asks_for_the_delete_choice_whose_code_is_checked_and_which_takes_the_sub_map` | 409 `MACRO_CHOIX_REQUIS` sans `sub_maps`, rien d'écrit |
| A-2.8-11 | couvert (API) | `Topogenia.Host.Tests.Selection.DeletionAndPasteRoutesTests.A_deletion_names_steps_and_flows_of_its_own_map_only` | flèche d'une autre carte → 404 `INTROUVABLE`, rien d'écrit |
| A-2.8-34 | couvert (API) | `Topogenia.Host.Tests.Dissolutions.DissolveRoutesTests.A_dissolution_is_hidden_from_a_stranger_refused_to_a_contributor_and_an_open_frame_is_not_grouped` | 409 `REMONTEE_EN_COURS` |
| A-2.9-45 | couvert (API) | `Topogenia.Host.Tests.Dissolutions.RemainderRoutesTests.Finishing_answers_the_arrangements_closes_the_frame_and_a_second_finish_answers_none` | `arrangements` : `kind`, `object_id`, `name` (et `step_id`, `step_code`) |
| A-2.9-46 | couvert (API) | `Topogenia.Host.Tests.Dissolutions.RemainderRoutesTests.Finishing_answers_the_arrangements_closes_the_frame_and_a_second_finish_answers_none` | second « Terminer » → `arrangements` vide, `operation_id` nul |
| A-2.16-02 | couvert (API, part de ce lot) | `Topogenia.Host.Tests.Dissolutions.DissolveRoutesTests.A_dissolution_checks_the_version_read_of_the_step_and_needs_a_detail` | `If-Match` entre guillemets lu sur « Remonter » |
| A-2.16-03 | couvert (API, part de ce lot) | `Topogenia.Host.Tests.Dissolutions.DissolveRoutesTests.A_dissolution_checks_the_version_read_of_the_step_and_needs_a_detail` | `"v1"` → `VERSION_INVALIDE` |
| A-2.16-05 | couvert (API, part de ce lot) | `Topogenia.Host.Tests.Dissolutions.DissolveRoutesTests.A_dissolution_checks_the_version_read_of_the_step_and_needs_a_detail` | version périmée de l'étape → 409 `CONFLIT_VERSION`, rien d'écrit ; sans `If-Match`, pas de contrôle |

## Lignes non couvertes au niveau HTTP

| ID | Raison |
|---|---|
| A-1.5-07, A-1.5-08, A-1.5-09 | Retour au reliquat (`from_step_id`) : règles prouvées au domaine ; la route transmet `from_step_id` sans le lire. |
| A-2.8-26 | `TRANSFERT_HORS_PROJET` : traité par le chef de projet après ce lot. |
| A-2.8-41 | Les flèches coupées par un regroupement ne sont plus dans la réponse (AR-50 : plus de `liens_coupes`) ; elles sont dans l'opération du journal (niveau application). |

## Routes du lot

| Méthode | Route 2.0 | Gestionnaire |
|---|---|---|
| POST | `/api/maps/{id}/deletion` | `SelectionApi.DeleteAsync` |
| POST | `/api/maps/{id}/paste` | `SelectionApi.PasteAsync` |
| POST | `/api/maps/{id}/transfer` | `SelectionApi.TransferAsync` |
| POST | `/api/maps/{id}/group` | `SelectionApi.GroupAsync` |
| POST | `/api/steps/{id}/dissolve` | `DissolutionApi.DissolveAsync` |
| POST | `/api/dissolutions/{id}/assign` | `DissolutionApi.AssignAsync` |
| POST | `/api/dissolutions/{id}/remove` | `DissolutionApi.RemoveAsync` |
| POST | `/api/dissolutions/{id}/finish` | `DissolutionApi.FinishAsync` |
