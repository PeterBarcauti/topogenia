<!-- Selection.md -->
<!-- Coverage of annex A 2.8 (selection: group deletion, paste, anchor, transfer, grouping) by the domain tests of phase 2 -->

# Couverture phase 2 : Sélection (annexe A 2.8)

Code : `src/Topogenia.Domain/Selection/`. Tests : `tests/Topogenia.Domain.Tests/Selection/`.
Préfixe des noms de tests : `Topogenia.Domain.Tests.Selection.`

| ID | Test 2.0 |
|---|---|
| A-2.8-01 | `SelectionBlocksTests.The_limits_are_500_steps_per_operation_and_2000_items_per_group_deletion` ; `PasteCopyTests.Keys_must_be_present_and_unique_and_the_count_between_1_and_500` |
| A-2.8-02 | `GroupDeletionTests.More_than_2000_steps_and_flows_after_removing_duplicates_is_refused_on_steps` |
| A-2.8-03 | `SelectionBlocksTests.Blocks_are_the_selected_steps_connected_by_internal_flows` |
| A-2.8-04 | `SelectionBlocksTests.Stitching_links_the_predecessors_to_the_successors_of_each_removed_block_with_the_entering_label` ; `GroupDeletionTests.Deleting_with_stitch_links_the_predecessor_to_the_successor_and_keeps_the_entering_label` ; `GroupDeletionTests.Deleting_without_stitch_recreates_no_flow` |
| A-2.8-05 | `SelectionBlocksTests.Stitching_is_skipped_when_a_block_has_several_predecessors_and_several_successors` ; `SelectionBlocksTests.Stitching_a_switch_keeps_the_labels_of_its_leaving_flows` ; `GroupDeletionTests.Deleting_a_switch_keeps_its_labels_and_an_ambiguous_stitch_is_not_made` |
| A-2.8-06 | `SelectionBlocksTests.Stitching_never_creates_a_loop_on_a_step` |
| A-2.8-07 | `SelectionBlocksTests.Stitching_never_duplicates_a_flow_that_already_exists_between_kept_steps` ; `SelectionBlocksTests.Applying_stitches_creates_only_the_missing_flows_on_the_map` |
| A-2.8-08 | `GroupDeletionTests.Stitching_never_recreates_a_flow_that_the_selection_deletes` |
| A-2.8-09 | `GroupDeletionTests.A_step_that_is_not_a_live_step_of_the_map_is_refused_as_outside_the_map` |
| A-2.8-10 | `GroupDeletionTests.A_filled_sub_map_requires_the_delete_choice_which_deletes_its_whole_descent_in_the_batch` ; `GroupDeletionTests.An_empty_sub_map_is_deleted_without_asking_for_a_choice` |
| A-2.8-11 | `GroupDeletionTests.A_flow_of_another_map_or_deleted_by_an_earlier_operation_is_not_found` |
| A-2.8-43 | `GroupDeletionTests.A_flow_of_another_map_or_deleted_by_an_earlier_operation_is_not_found` ; `GroupDeletionTests.A_flow_already_deleted_in_this_batch_by_its_step_is_accepted` |
| A-2.8-12 | `GroupDeletionTests.Steps_and_flows_of_a_group_deletion_go_into_a_single_batch` |
| A-2.8-13 | `PasteCopyTests.Keys_must_be_present_and_unique_and_the_count_between_1_and_500` |
| A-2.8-14 | `PasteCopyTests.Pasting_after_a_step_shifts_the_following_steps_and_inserts_the_copy_in_the_flow` |
| A-2.8-15 | `PasteCopyTests.Pasted_steps_get_new_codes_no_sheet_and_the_operation_that_created_them` |
| A-2.8-16 | `PasteCopyTests.Pasted_steps_get_new_codes_no_sheet_and_the_operation_that_created_them` |
| A-2.8-17 | `PasteCopyTests.A_duplicate_flow_of_the_copy_is_ignored` ; `PasteCopyTests.Pasting_after_a_step_shifts_the_following_steps_and_inserts_the_copy_in_the_flow` |
| A-2.8-18 | `PasteCopyTests.A_flow_with_an_unknown_or_reflexive_key_is_refused_and_nothing_is_created` |
| A-2.8-19 | `PasteCopyTests.Two_pasted_steps_on_the_same_cell_are_refused` |
| A-2.8-20 | `PasteCopyTests.The_anchor_must_be_a_live_step_of_the_map_and_its_position_a_known_code` ; `BlockTransferTests.The_anchor_cannot_be_a_step_of_the_block` |
| A-2.8-21 | `PasteCopyTests.Pasting_after_a_step_shifts_the_following_steps_and_inserts_the_copy_in_the_flow` ; `BlockTransferTests.Cut_and_paste_within_the_map_stitches_the_origin_and_reinserts_the_block_with_its_identity` |
| A-2.8-22 | `PasteCopyTests.The_label_of_the_cut_flow_is_kept_only_when_the_block_has_one_entry` ; `PasteCopyTests.Pasting_after_a_step_shifts_the_following_steps_and_inserts_the_copy_in_the_flow` |
| A-2.8-23 | `PasteCopyTests.Pasting_before_a_step_with_several_incoming_flows_only_links_the_exits_to_it` |
| A-2.8-24 | `BlockTransferTests.A_stitch_of_the_same_operation_is_cut_first_and_removed_physically` |
| A-2.8-25 | `BlockTransferTests.The_steps_must_be_distinct_live_steps_of_a_single_map` |
| A-2.8-26 | `BlockTransferTests.A_step_or_a_map_of_another_project_is_not_found_in_the_workspace_of_one_project` |
| A-2.8-27 | `BlockTransferTests.Transferring_into_the_descent_of_its_own_sub_maps_is_refused_as_a_cycle` |
| A-2.8-28 | `BlockTransferTests.Cut_and_paste_within_the_map_stitches_the_origin_and_reinserts_the_block_with_its_identity` ; `BlockTransferTests.Cut_and_paste_to_another_map_keeps_sheet_documents_sub_map_and_internal_flows` |
| A-2.8-29 | `BlockTransferTests.Cut_and_paste_within_the_map_stitches_the_origin_and_reinserts_the_block_with_its_identity` ; `BlockTransferTests.Moving_to_another_map_without_cut_removes_the_crossing_flows_without_stitching` ; `BlockTransferTests.Moving_without_cut_within_the_map_keeps_the_crossing_flows` |
| A-2.8-30 | `BlockTransferTests.Moving_to_another_map_without_cut_removes_the_crossing_flows_without_stitching` ; `BlockTransferTests.Cut_and_paste_to_another_map_keeps_sheet_documents_sub_map_and_internal_flows` |
| A-2.8-31 | `BlockTransferTests.Cut_and_paste_to_another_map_keeps_sheet_documents_sub_map_and_internal_flows` |
| A-2.8-32 | `BlockTransferTests.A_role_follows_the_only_lane_of_the_same_name_of_the_arrival_map` |
| A-2.8-33 | `BlockTransferTests.A_role_stays_hidden_on_its_old_lane_when_the_arrival_map_has_no_or_several_lanes_of_that_name` |
| A-2.8-34 | `GroupIntoSubMapTests.A_step_in_an_open_dissolution_frame_cannot_be_grouped` ; `GroupIntoSubMapTests.A_closed_dissolution_frame_does_not_prevent_grouping` |
| A-2.8-35 | `GroupIntoSubMapTests.The_cut_flows_go_into_the_batch_of_the_operation_and_are_listed_in_the_result` (partie domaine : lot rattaché à l'opération, flèches coupées rendues dans `CutFlows` ; l'inscription au journal relève de l'Application) |
| A-2.8-36 | `GroupIntoSubMapTests.Grouping_replaces_the_block_by_a_task_whose_sub_map_holds_the_block` |
| A-2.8-37 | `GroupIntoSubMapTests.The_sub_map_receives_copies_of_the_occupied_lanes_in_order_and_the_block_at_relative_cells` |
| A-2.8-38 | `GroupIntoSubMapTests.The_sub_map_receives_copies_of_the_occupied_lanes_in_order_and_the_block_at_relative_cells` |
| A-2.8-39 | `GroupIntoSubMapTests.The_raci_roles_follow_the_copied_lanes_and_the_others_stay_hidden` |
| A-2.8-40 | `GroupIntoSubMapTests.Each_cut_flow_is_recreated_once_towards_or_from_the_new_step` |
| A-2.8-42 | `BlockTransferTests.Two_steps_on_the_same_arrival_cell_are_refused_and_nothing_is_written` |
| A-6-07 | `GroupIntoSubMapTests.Grouping_replaces_the_block_by_a_task_whose_sub_map_holds_the_block` (`OriginOperationId` de l'étape créée) |
| A-6-09 | `BlockTransferTests.A_role_follows_the_only_lane_of_the_same_name_of_the_arrival_map` ; `BlockTransferTests.A_role_stays_hidden_on_its_old_lane_when_the_arrival_map_has_no_or_several_lanes_of_that_name` (règle portée par `RaciRules.FollowToMap`, dossier Raci) |
| A-2.2-16 | `GroupIntoSubMapTests.Grouping_beyond_the_recommended_depth_warns_without_refusing` (cas du regroupement seulement) |
| A-1.3-15 | `GroupDeletionTests.Duplicate_identifiers_are_removed_and_an_empty_selection_is_accepted` ; `GroupDeletionTests.More_than_2000_steps_and_flows_after_removing_duplicates_is_refused_on_steps` (partie domaine ; la route relève de l'API) |

## Lignes de la part non couvertes

- **A-2.8-41** : l'inscription des flèches coupées dans l'opération du journal relève de
  l'Application et de l'API (phase 4). Le domaine fournit ce qu'il faut : `SelectionResult.CutFlows`
  et le lot de l'opération (`DeletionBatch.OperationId`), vérifiés par
  `GroupIntoSubMapTests.The_cut_flows_go_into_the_batch_of_the_operation_and_are_listed_in_the_result`.
- **A-2.8-35, partie journal** : même raison (Application, phase 4).

## Notes

- Noms de champs des refus en anglais (D12, comme le noyau : `column`, `row`, `label`) :
  `steps` (1.x `etapes`), `flows` (1.x `liens`), `anchor` (1.x `ancre`), `lane_id`
  (1.x `couloir_id`), `position` ; positions d'ancre `before` / `after` (1.x `avant` / `apres`).
- Couloir supprimé de la carte visée pendant un collage ou un transfert : `COULOIR_SUPPRIME`
  (409) ; couloir inconnu ou d'une autre carte : `VALEUR_INVALIDE(lane_id)`.
- Le suivi des rôles RACI (transfert et regroupement) appelle `RaciRules.FollowToMap` du
  dossier Raci, qui ajoute au 1.x le lien `SourceLaneId` (AR-12) avant l'homonyme.
