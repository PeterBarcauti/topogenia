<!-- selection-dissolutions.md -->
<!-- Couverture P4-T02 lot B : sélection, sous-cartes, remontée du détail et RACI, au niveau Application -->

# Couverture phase 4, lot B : sélection, sous-cartes, remontée, RACI

Commandes : `src/Topogenia.Application/Selection/SelectionCommands.cs`, `SubMaps/SubMapCommands.cs`,
`Dissolutions/DissolutionCommands.cs`, `Raci/RaciCommands.cs`. Tests : `tests/Topogenia.Application.Tests/Selection/`,
`SubMaps/`, `Dissolutions/`, `Raci/` ; droits dans `Commands/CommandCatalog.SelectionAndDissolutions.cs`. Préfixe des
tests : `Topogenia.Application.Tests.` Le niveau API (HTTP, `If-Match` lu dans l'en-tête, routes 2.0) relève de P4-T04
et P4-T08.

## Décisions du lot

- Genres du journal : ceux de `carto/web/src/carte/magasin.ts` (`supprimer`, `coller`, `deplacer_bloc`, `regrouper`,
  `detailler`, `supprimer_detail`, `remonter`, `affecter`, `terminer_remontee`, `raci`).
- Rattachement (`ReattachSubMapCommand`) : la 1.x ne l'inscrivait jamais (le client ne l'appelait que pour défaire la
  suppression d'une macro, `magasin.ts:738`). En 2.0 il est inscrit avec le genre le plus proche, `detailler` (l'étape
  cible reçoit un détail), sur la carte de l'étape cible.
- Carte d'inscription : « Détailler », « Remonter » et le RACI sur la carte de l'étape ; « Supprimer le détail » sur la
  carte de l'étape parente (là où l'utilisateur agit, comme `magasin.ts:756`) ; le transfert sur la carte d'arrivée
  (B-2.6-30) ; les commandes d'un cadre sur la carte du cadre.
- `If-Match` (A-2.16-05) : sur l'objet de la route quand il est modifié : l'étape pour le RACI, « Détailler » et
  « Remonter », la sous-carte pour « Supprimer le détail » et le rattachement. Les opérations sur une sélection (la carte
  n'est pas modifiée) et sur un cadre (la vue du cadre n'expose pas de version) n'en ont pas.
- « Terminer » est inscrit seulement quand il range quelque chose ; la fermeture d'un cadre vide (liste vide) ne l'est
  pas (B-2.6-40, comme `magasin.ts:684`).

## Lignes couvertes

| ID | État | Test 2.0 | Remarque |
|---|---|---|---|
| A-1.3-02 | couvert (application) | `SubMaps.SubMapCommandsTests.Deleting_the_detail_takes_the_descent_in_one_batch_is_logged_on_the_parent_map_and_undo_restores_it` ; `SubMaps.SubMapCommandsTests.The_root_map_is_not_a_detail_and_a_contributor_deletes_no_detail` ; `SubMaps.SubMapCommandsTests.The_undo_of_a_detail_completed_since_is_refused_and_clears_the_stack` | `DeleteSubMapCommand`, Animateur ; plus de `si_vide` : l'annulation d'un « Détailler » passe par `undo` |
| A-1.3-14 | couvert (application) | `Selection.SelectionCommandsTests.A_deleted_selection_is_stitched_logged_as_a_deletion_and_undo_brings_it_back_without_the_stitch` ; `Selection.SelectionCommandsTests.A_selection_with_a_filled_sub_map_asks_for_the_choice_and_its_deletion_takes_the_descent_back_on_undo` ; `Selection.SelectionCommandsTests.A_contributor_neither_deletes_nor_pastes_a_selection` | `DeleteSelectionCommand` : `Stitch`, `DeleteSubMaps` |
| A-1.3-16 | couvert (application) | `SubMaps.SubMapCommandsTests.A_reattached_sub_map_is_logged_as_a_detail_on_the_map_of_its_new_step_and_undo_gives_it_back` ; `SubMaps.SubMapCommandsTests.A_sub_map_is_not_reattached_to_an_unknown_step_a_start_or_its_own_descent` | `RATTACHEMENT_INVALIDE` ; `CARTE_RACINE` reste prouvé au domaine |
| A-1.3-17 | couvert (application) | `Selection.SelectionCommandsTests.A_pasted_copy_is_logged_as_a_paste_and_undo_and_redo_remove_and_bring_back_the_same_steps` ; `Selection.SelectionCommandsTests.A_paste_after_an_anchor_cuts_its_single_outgoing_flow_and_undo_puts_the_flow_back` | `PasteCommand`, avec et sans ancre |
| A-1.3-18 | couvert (application) | `Selection.SelectionCommandsTests.A_paste_with_an_unknown_anchor_position_is_refused_on_position_and_nothing_is_written` | `VALEUR_INVALIDE(position)`, rien d'écrit |
| A-1.3-19 | couvert (application) | `Selection.SelectionCommandsTests.A_pasted_step_on_an_unknown_lane_is_refused` | couloir inconnu → `VALEUR_INVALIDE` |
| A-1.3-20 | couvert (application) | `Selection.BlockCommandsTests.A_block_cut_to_a_sub_map_is_logged_on_the_target_map_and_undo_there_brings_it_back_stitches_removed` | `TransferBlockCommand` ; l'étape garde son code |
| A-1.3-21 | couvert (application) | `Selection.BlockCommandsTests.A_contributor_does_not_transfer_and_a_block_into_its_own_detail_or_an_unknown_step_is_refused` | carte d'origine et cible du même projet (l'espace d'un projet) : un seul rôle Animateur exigé ; niveau Sécurité à P4-T08 |
| A-1.3-22 | couvert (application) | `Selection.BlockCommandsTests.A_contributor_does_not_transfer_and_a_block_into_its_own_detail_or_an_unknown_step_is_refused` | étape absente → `INTROUVABLE` |
| A-1.3-23 | couvert (application) | `Selection.BlockCommandsTests.Detailing_a_selection_logs_a_grouping_with_the_cut_flows_and_the_new_step_remembers_the_operation` | `GroupSelectionCommand` |
| A-1.3-24 | couvert (application) | `Selection.BlockCommandsTests.A_grouping_without_label_with_a_step_of_another_map_or_of_an_open_frame_is_refused_and_a_deep_one_warns` | `CHAMP_OBLIGATOIRE(label)` |
| A-1.3-25 | couvert (application) | `Selection.BlockCommandsTests.A_grouping_without_label_with_a_step_of_another_map_or_of_an_open_frame_is_refused_and_a_deep_one_warns` | `ETAPE_HORS_CARTE` avec le code ; `VALEUR_INVALIDE(steps)` reste prouvé au domaine |
| A-1.3-26 | couvert (application) | `Selection.BlockCommandsTests.A_grouping_without_label_with_a_step_of_another_map_or_of_an_open_frame_is_refused_and_a_deep_one_warns` | `PROFONDEUR_DEPASSEE` dans `Warnings` de la réponse |
| A-1.4-19 | couvert (application) | `SubMaps.SubMapCommandsTests.Detailing_a_step_is_logged_on_its_map_and_undo_and_redo_remove_and_bring_back_the_sub_map` ; `SubMaps.SubMapCommandsTests.Reopening_an_empty_sub_map_warns_and_logs_nothing` ; `SubMaps.SubMapCommandsTests.A_detailed_step_is_not_detailed_again_a_start_is_never_detailed_and_a_deep_detail_warns` | `ETAPE_DEJA_DETAILLEE`, `ETAPE_NON_DETAILLABLE`, `SOUS_CARTE_EXISTANTE`, `PROFONDEUR_DEPASSEE` |
| A-1.4-20 | couvert (application) | `Raci.RaciCommandsTests.A_role_is_logged_as_raci_shown_in_the_view_and_undo_and_redo_remove_and_set_it_again` ; `Raci.RaciCommandsTests.A_role_on_a_step_without_content_on_the_lane_of_the_step_or_with_an_unknown_letter_is_refused` ; `Raci.RaciCommandsTests.A_contributor_sets_no_role` | `SetRaciCommand` |
| A-1.5-01 | couvert (application) | `Dissolutions.DissolutionCommandsTests.A_dissolution_is_logged_as_remonter_its_result_names_the_frame_and_undo_and_redo_replay_it` ; `Dissolutions.DissolutionCommandsTests.A_step_without_detail_is_not_dissolved_and_a_contributor_dissolves_nothing` | `DissolveCommand` ; `DETAIL_ABSENT` |
| A-1.5-02 | couvert (application) | `Dissolutions.DissolutionCommandsTests.A_dissolution_is_logged_as_remonter_its_result_names_the_frame_and_undo_and_redo_replay_it` | `DissolveResult.DissolutionId` = la remontée, aussi dans la vue |
| A-1.5-03 | couvert (application) | `Dissolutions.DissolutionCommandsTests.An_assignment_is_logged_as_affecter_and_undo_brings_the_document_back_to_the_remainder` ; `Dissolutions.DissolutionCommandsTests.An_assignment_names_a_known_frame_a_step_that_holds_content_and_a_facilitator` | `AssignRemainderCommand` ; remontée inconnue → `INTROUVABLE` |
| A-1.5-10 | couvert (application) | `Dissolutions.DissolutionCommandsTests.Removing_an_element_of_the_remainder_is_for_good_and_logs_nothing` | `RemoveRemainderCommand` |
| A-1.5-11 | couvert (application) | `Dissolutions.DissolutionCommandsTests.Nothing_is_removed_from_a_closed_frame` | `REMONTEE_TERMINEE` |
| A-1.5-12 | couvert (application) | `Dissolutions.DissolutionCommandsTests.Removing_an_element_of_the_remainder_is_for_good_and_logs_nothing` | élément déjà retiré → `INTROUVABLE` |
| A-1.5-13 | couvert (application) | `Dissolutions.DissolutionCommandsTests.Finishing_stores_the_remainder_is_logged_and_undo_reopens_the_frame_with_its_remainder` | `FinishDissolutionCommand` |
| A-1.5-14 | couvert (application) | `Dissolutions.DissolutionCommandsTests.A_dissolution_is_logged_as_remonter_its_result_names_the_frame_and_undo_and_redo_replay_it` ; `Dissolutions.DissolutionCommandsTests.The_undo_of_a_dissolution_whose_frame_step_was_deleted_by_another_user_is_refused_and_clears_the_stack` | par `UndoCommand` (AR-08) ; aucune route `/remontees/{id}/annuler` |
| A-2.2-03 | couvert (application) | `SubMaps.SubMapCommandsTests.The_root_map_is_not_a_detail_and_a_contributor_deletes_no_detail` | `CARTE_RACINE` |
| A-2.2-13 | couvert (application) | `SubMaps.SubMapCommandsTests.A_detailed_step_is_not_detailed_again_a_start_is_never_detailed_and_a_deep_detail_warns` | `ETAPE_DEJA_DETAILLEE` |
| A-2.2-18 | couvert (application) | `Selection.SelectionCommandsTests.A_selection_with_a_filled_sub_map_asks_for_the_choice_and_its_deletion_takes_the_descent_back_on_undo` | prouvé par la suppression d'une sélection (`sub_maps=delete`) : même lot ; la suppression d'une seule étape relève du lot A |
| A-2.2-20 | couvert (application) | `Selection.SelectionCommandsTests.A_selection_with_a_filled_sub_map_asks_for_the_choice_and_its_deletion_takes_the_descent_back_on_undo` | `MACRO_CHOIX_REQUIS`, rien d'inscrit (par la sélection) |
| A-2.2-26 | couvert (application) | `SubMaps.SubMapCommandsTests.The_undo_of_a_detail_completed_since_is_refused_and_clears_the_stack` | `CARTE_MODIFIEE`, pile vidée, sous-carte gardée |
| A-2.2-27 | couvert (application) | `SubMaps.SubMapCommandsTests.Deleting_the_detail_takes_the_descent_in_one_batch_is_logged_on_the_parent_map_and_undo_restores_it` | sous-carte, sous-sous-carte et étapes dans un même lot |
| A-2.8-10 | couvert (application) | `Selection.SelectionCommandsTests.A_selection_with_a_filled_sub_map_asks_for_the_choice_and_its_deletion_takes_the_descent_back_on_undo` | |
| A-2.8-34 | couvert (application) | `Selection.BlockCommandsTests.A_grouping_without_label_with_a_step_of_another_map_or_of_an_open_frame_is_refused_and_a_deep_one_warns` | `REMONTEE_EN_COURS` |
| A-2.8-35 | couvert (application) | `Selection.BlockCommandsTests.Detailing_a_selection_logs_a_grouping_with_the_cut_flows_and_the_new_step_remembers_the_operation` | les flèches coupées sont dans le `ForwardJson` de l'entrée `regrouper` |
| A-2.8-41 | couvert (application) | `Selection.BlockCommandsTests.Detailing_a_selection_logs_a_grouping_with_the_cut_flows_and_the_new_step_remembers_the_operation` | idem ; la réponse garde `CutFlows` dans le résultat |
| A-2.9-19 | couvert (application) | `Selection.BlockCommandsTests.Detailing_a_selection_logs_a_grouping_with_the_cut_flows_and_the_new_step_remembers_the_operation` | `step.origin_operation_id` de la nouvelle étape = identifiant de l'opération inscrite ; la recherche des flèches par la remontée reste prouvée au domaine |
| A-2.9-45 | couvert (application) | `Dissolutions.DissolutionCommandsTests.Finishing_stores_the_remainder_is_logged_and_undo_reopens_the_frame_with_its_remainder` | liste des rangements |
| A-2.9-46 | couvert (application) | `Dissolutions.DissolutionCommandsTests.Finishing_stores_the_remainder_is_logged_and_undo_reopens_the_frame_with_its_remainder` | second « Terminer » → [], rien d'inscrit |
| A-2.9-47 | couvert (application) | `Dissolutions.DissolutionCommandsTests.The_undo_of_a_dissolution_whose_frame_step_was_deleted_by_another_user_is_refused_and_clears_the_stack` | `OPERATION_DEPASSEE` (AR-66 : versions avant invariants), pile de l'utilisateur vidée |
| A-2.9-52 | couvert (application) | `Dissolutions.DissolutionCommandsTests.A_dissolution_is_logged_as_remonter_its_result_names_the_frame_and_undo_and_redo_replay_it` | sous-carte et étape détaillée vivantes après `undo` |
| A-2.9-53 | couvert (application) | `Dissolutions.DissolutionCommandsTests.A_dissolution_is_logged_as_remonter_its_result_names_the_frame_and_undo_and_redo_replay_it` | étapes du cadre revenues dans la sous-carte à leur colonne |
| A-2.9-54 | couvert (application) | `Dissolutions.DissolutionCommandsTests.A_dissolution_is_logged_as_remonter_its_result_names_the_frame_and_undo_and_redo_replay_it` | étape détaillée, flèches et sous-carte du lot de nouveau vivantes |
| A-2.9-57 | couvert (application) | `Dissolutions.DissolutionCommandsTests.A_dissolution_is_logged_as_remonter_its_result_names_the_frame_and_undo_and_redo_replay_it` | l'étape décalée (colonne 3) revient en colonne 2 |
| A-2.13-03 | couvert (application) | `Raci.RaciCommandsTests.A_role_on_a_step_without_content_on_the_lane_of_the_step_or_with_an_unknown_letter_is_refused` | `S` hors `ACI` → `VALEUR_INVALIDE(role)` |
| A-2.13-05 | couvert (application) | `Raci.RaciCommandsTests.A_role_on_a_step_without_content_on_the_lane_of_the_step_or_with_an_unknown_letter_is_refused` | `VALEUR_INVALIDE(lane_id)` |
| A-2.13-06 | couvert (application) | `Raci.RaciCommandsTests.Moving_the_approval_and_undoing_it_gives_the_A_back_to_the_former_approver` | |
| A-2.13-08 | couvert (application) | `Raci.RaciCommandsTests.A_role_removed_with_null_comes_back_on_undo` | |
| A-2.13-09 | couvert (application) | `Raci.RaciCommandsTests.A_role_on_a_step_without_content_on_the_lane_of_the_step_or_with_an_unknown_letter_is_refused` | `ETAPE_SANS_CONTENU` |
| A-2.16-05 | couvert (application) | `Raci.RaciCommandsTests.A_role_checks_the_version_read_of_the_step` ; `SubMaps.SubMapCommandsTests.Detailing_deleting_and_reattaching_check_the_version_read_of_the_object_of_their_route` ; `Dissolutions.DissolutionCommandsTests.A_dissolution_checks_the_version_read_of_the_dissolved_step` | pour les commandes de ce lot (voir « Décisions du lot ») |
| B-2.6-25 | couvert (application) | `Selection.SelectionCommandsTests.A_deleted_selection_is_stitched_logged_as_a_deletion_and_undo_brings_it_back_without_the_stitch` ; `Selection.SelectionCommandsTests.A_selection_with_a_filled_sub_map_asks_for_the_choice_and_its_deletion_takes_the_descent_back_on_undo` | flèche de raccord retirée, étape et flèches restaurées, colonnes revenues ; la macro et le couloir relèvent des lots A et des couloirs |
| B-2.6-27 | couvert (application) | `SubMaps.SubMapCommandsTests.Deleting_the_detail_takes_the_descent_in_one_batch_is_logged_on_the_parent_map_and_undo_restores_it` | et rétabli |
| B-2.6-28 | couvert (application) | `SubMaps.SubMapCommandsTests.Detailing_a_step_is_logged_on_its_map_and_undo_and_redo_remove_and_bring_back_the_sub_map` ; `SubMaps.SubMapCommandsTests.The_undo_of_a_detail_completed_since_is_refused_and_clears_the_stack` | |
| B-2.6-29 | couvert (application) | `Selection.SelectionCommandsTests.A_pasted_copy_is_logged_as_a_paste_and_undo_and_redo_remove_and_bring_back_the_same_steps` | |
| B-2.6-30 | couvert (application) | `Selection.BlockCommandsTests.A_block_cut_to_a_sub_map_is_logged_on_the_target_map_and_undo_there_brings_it_back_stitches_removed` ; `Selection.BlockCommandsTests.The_stack_of_the_map_of_origin_is_kept_and_its_operations_stay_protected_by_their_versions` | inscrit sur la carte d'arrivée ; la pile de la carte d'origine est gardée, son `raci` sur l'étape partie est refusé `OPERATION_DEPASSEE` |
| B-2.6-31 | couvert (application) | `Selection.BlockCommandsTests.Detailing_a_selection_logs_a_grouping_with_the_cut_flows_and_the_new_step_remembers_the_operation` ; `Selection.BlockCommandsTests.The_undo_of_a_grouping_is_refused_once_its_sub_map_was_completed_and_the_stack_is_cleared` | `OPERATION_DEPASSEE` ou `CARTE_MODIFIEE`, pile vidée |
| B-2.6-34 | couvert (application) | `Raci.RaciCommandsTests.Moving_the_approval_and_undoing_it_gives_the_A_back_to_the_former_approver` | |
| B-2.6-35 | couvert (application) | `Dissolutions.DissolutionCommandsTests.A_dissolution_is_logged_as_remonter_its_result_names_the_frame_and_undo_and_redo_replay_it` ; `Dissolutions.DissolutionCommandsTests.An_assignment_is_logged_as_affecter_and_undo_brings_the_document_back_to_the_remainder` ; `Dissolutions.DissolutionCommandsTests.Finishing_stores_the_remainder_is_logged_and_undo_reopens_the_frame_with_its_remainder` | |
| B-2.6-39 | couvert (application) | `Dissolutions.DissolutionCommandsTests.Removing_an_element_of_the_remainder_is_for_good_and_logs_nothing` | `OperationId` nul, aucune entrée |
| B-2.6-40 | couvert (application) | `Dissolutions.DissolutionCommandsTests.Closing_an_empty_frame_logs_nothing_and_the_next_undo_undoes_the_dissolution` | le `undo` suivant défait la remontée, comme la 1.x |
| B-2.6-41 | couvert (application) | `SubMaps.SubMapCommandsTests.Reopening_an_empty_sub_map_warns_and_logs_nothing` | |
| B-2.6-42 | couvert (application) | `Selection.BlockCommandsTests.Detailing_a_selection_logs_a_grouping_with_the_cut_flows_and_the_new_step_remembers_the_operation` | flèches coupées inscrites dans l'opération, regroupement toujours annulable |

## Lignes non couvertes par ce lot

| ID | Raison |
|---|---|
| A-2.8-26 | `TRANSFERT_HORS_PROJET` (AR-71) : le gabarit ne donne à une commande aucun moyen de chercher une étape absente de l'espace du projet (pas d'`IAccessReader` dans `PrepareAsync`) ; le transfert répond `INTROUVABLE`. À ajouter au gabarit (`Commands/`, hors lot). |
| B-2.6-26 | Suppression d'une macro avec « rattacher » : commande de suppression d'une étape (lot A). |
