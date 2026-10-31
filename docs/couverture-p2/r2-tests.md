<!-- r2-tests.md -->
<!-- Couverture du round 2 de la porte de la phase 2 : tests qui prouvent les règles, bornes fixées, citations corrigées -->

# Couverture phase 2, round 2 : tests qui prouvent les règles

Chaque test ajouté ou renforcé a été vérifié contre la mutation du constat (copie du domaine dans le
bac à sable, mutation appliquée, construction, exécution, restauration) : la mutation fait échouer le
test. Préfixe des tests : `Topogenia.Domain.Tests.`

## Lignes dont la preuve ou l'état change

| ID | État | Test 2.0 |
|---|---|---|
| A-2.4-13 | couvert | `Layout.StepLayoutTests.A_layout_onto_the_cell_of_a_step_moved_elsewhere_does_not_shift_the_steps_that_follow` ; `Layout.StepLayoutTests.A_layout_places_steps_exactly_without_any_shift` |
| A-2.3-02 | couvert (AR-11 ; plus d'espaces rognés, comme `Couleurs.Valide` 1.x) | `Values.ValueObjectTests.A_colour_that_is_not_rrggbb_is_refused_on_its_field` ; `Values.ValueObjectTests.A_lane_colour_is_put_in_upper_case` ; `Lanes.AddLaneTests.A_colour_that_is_not_hex_rrggbb_is_refused` ; `Lanes.AddLaneTests.A_lowercase_colour_is_accepted_and_put_in_upper_case` |
| A-2.3-03 | couvert (AR-45, valeurs de la charte écrites en dur) | `Lanes.AddLaneTests.The_default_colour_follows_the_number_of_lanes_of_the_map_over_the_eight_tints` ; `Values.ValueObjectTests.The_eight_tints_are_those_of_the_charter_in_its_order` |
| A-2.8-37 | couvert | `Selection.GroupLanesAndLimitsTests.The_copied_lanes_follow_the_lane_order_of_the_map_and_not_the_reading_order_of_the_block` ; `Selection.GroupIntoSubMapTests.The_sub_map_receives_copies_of_the_occupied_lanes_in_order_and_the_block_at_relative_cells` |
| A-2.9-20 | couvert | `Dissolutions.DissolveEndsTests.The_step_designated_by_the_start_is_the_entry_even_when_another_step_has_no_predecessor` ; `Dissolutions.DissolveEndsTests.The_step_designated_by_the_end_is_the_exit_even_when_another_step_has_no_successor` ; `Dissolutions.DissolveOriginalFlowTests.Without_the_batch_of_the_origin_operation_the_flows_go_to_the_steps_without_predecessor_or_successor` ; `Dissolutions.DissolveOriginalFlowTests.When_every_step_of_the_detail_has_a_predecessor_the_first_and_last_steps_are_the_ends` |
| A-6-10 | couvert | mêmes tests que A-2.9-20 |
| A-2.9-19 | couvert | `Dissolutions.FlowScenarioTests.Flows_into_and_out_of_the_middle_of_the_block_pass_through_the_detailed_step_and_are_restored` (aller-retour réel `GroupIntoSubMap.Group` puis `Dissolve.Run`) |
| A-2.4-23 | couvert | `Steps.StepBoundsTests.A_parallel_step_takes_every_entering_and_every_leaving_flow_of_b` ; `Steps.InsertStepTests.Parallel_takes_the_same_lane_and_column_the_first_free_row_under_b_and_every_flow_of_b` |
| A-2.4-22 | couvert | `Steps.StepBoundsTests.A_parallel_step_takes_the_first_free_row_under_b_even_when_a_lower_row_is_taken` |
| A-2.4-03 | couvert | `Steps.StepBoundsTests.A_label_of_150_characters_is_accepted_everywhere_and_151_is_too_long_with_its_maximum` ; `Steps.CreateStepTests.A_label_is_required_and_bounded_to_150_characters` |
| A-2.4-04 | couvert (AR-73) | `Steps.StepBoundsTests.Inserting_after_a_step_of_the_last_column_is_refused_on_column` ; `Steps.CreateStepTests.The_last_column_and_the_last_row_are_accepted` |
| A-2.6-07 | couvert (AR-72) | `Coverage.FlowCheckOrderTests.A_loop_or_a_duplicate_with_a_too_long_label_gets_the_code_of_the_arrow` ; `Flows.FlowRulesTests.A_label_of_60_characters_is_accepted_and_61_is_too_long` |
| A-2.8-26 | couvert (domaine, AR-71) ; `TRANSFERT_HORS_PROJET` : Application, phase 4 | `Selection.BlockTransferTests.A_step_or_a_map_of_another_project_is_not_found_in_the_workspace_of_one_project` |
| A-3-38 | à faire (phase 4, AR-71) ; valeur du code : couvert | `Coverage.ErrorCodeValuesTests.Every_error_code_keeps_the_value_of_the_contract` (le refus lui-même est rendu par l'Application à partir d'une étape absente de l'espace) |
| A-2.2-21 | couvert (AR-71 : un seul projet dans l'espace, test déjà réaliste) | `SubMaps.ReattachTests.The_target_step_must_be_live_and_of_the_same_project` |
| A-2.6-22 | couvert (domaine, AR-71 : test déjà réaliste) | `Flows.CommandBarTests.A_code_of_another_project_is_unknown_and_never_outside_the_map` |
| A-6-20 | couvert (AR-74) | `Layout.StepLayoutTests.A_step_of_another_map_is_refused_as_outside_the_map_with_its_readable_code` ; `Layout.StepLayoutTests.An_unknown_step_is_refused_as_outside_the_map_without_any_code_detail` |
| A-2.8-01 | couvert | `Selection.GroupLanesAndLimitsTests.A_grouping_of_exactly_500_steps_is_accepted_and_501_steps_are_refused` ; `Selection.GroupLanesAndLimitsTests.A_transfer_of_exactly_500_steps_is_accepted_and_501_steps_are_refused` ; `Coverage.SelectionLimitsTests.A_paste_of_exactly_500_steps_is_accepted_and_501_steps_are_refused` |
| A-2.8-14 | couvert | `Selection.PasteOrderTests.An_unsorted_paste_is_created_column_by_column_with_the_shift` ; `Selection.PasteCopyTests.Pasted_steps_get_new_codes_no_sheet_and_the_operation_that_created_them` |
| A-2.9-14 | couvert | `Dissolutions.DissolveEndsTests.The_columns_are_offset_from_the_column_of_the_detailed_step_by_the_first_column_of_the_detail` ; `Dissolutions.DissolveTests.The_detail_steps_take_the_place_of_the_detailed_step_and_the_flow_is_shifted` |
| A-2.9-36 | couvert | `Dissolutions.DissolveEndsTests.Removing_a_document_of_the_remainder_takes_the_uses_of_every_step` ; `Dissolutions.RemainderRemoveTests.Removing_a_document_takes_all_its_uses_and_erases_their_reported_pain_points` |
| A-2.9-39 | couvert | `Dissolutions.DissolveEndsTests.Finish_follows_the_reading_order_of_the_map_when_it_finishes` ; `Dissolutions.RemainderFinishTests.Outputs_go_to_the_last_step_of_the_frame_inputs_to_the_first_and_pain_points_to_the_map` |
| A-2.10-33 | couvert | `Sheets.SheetBoundsTests.A_clarification_of_1000_characters_is_accepted_and_1001_is_too_long` |
| A-2.10-36 | couvert (domaine) | `Sheets.SheetBoundsTests.The_summary_of_the_others_reads_the_denormalised_columns_of_the_version` |
| A-2.10-34 | couvert (domaine) | `Sheets.SheetBoundsTests.The_summary_of_a_step_without_sheet_keeps_the_right_to_fill_of_the_viewer` (can_fill du résumé d'une étape sans fiche) |
| A-2.11-17 | couvert | `Documents.DocumentUsesTests.Changing_only_the_lost_time_of_a_pain_point_is_recorded` ; `Documents.DocumentUsesTests.A_pain_point_is_updated_without_duplicate_one_per_use` |
| A-2.11-18 | couvert | `Documents.DocumentUsesTests.Changing_only_the_lost_time_of_a_pain_point_is_recorded` ; `Documents.DocumentRulesTests.Bounds_of_waiting_time_percent_and_lost_time_with_their_rounding` |
| A-2.11-13 | couvert | `Documents.DocumentUsesTests.A_document_of_another_live_step_of_the_same_map_may_be_used_even_without_flow` (valeurs de l'usage vérifiées, plus seulement sa présence) |
| A-2.13-03 | couvert | `Raci.RaciRulesTests.The_letter_s_is_accepted_only_when_the_setting_offers_it` (refus `VALEUR_INVALIDE(role)` vérifié) |
| A-2.14-20 | couvert (domaine) | `Translations.ManualTranslationTests.Writing_the_text_of_an_automatic_translation_by_hand_makes_it_manual` ; `Translations.TranslationImportTests.A_manual_translation_is_overwritten_only_with_the_scope_all_and_then_becomes_automatic` |
| A-2.14-22 | couvert (domaine) | `Translations.TranslationImportTests.A_single_invalid_translation_refuses_the_whole_file` ; `Translations.TranslationImportTests.One_invalid_translation_refuses_the_whole_file_with_its_line` |
| A-2.14-23 | couvert (domaine) | `Translations.TranslationImportTests.An_identical_text_imported_with_the_scope_all_over_a_manual_translation_becomes_automatic` ; `Translations.TranslationImportTests.Untranslated_texts_are_listed_then_written_in_one_pass_as_automatic` |
| A-2.15-23 | couvert (domaine) | `Trash.PurgeCascadeTests.The_lanes_of_a_purged_map_go_with_their_translations_and_roles` ; `Trash.PurgeCascadeTests.A_use_that_carries_a_purged_pain_point_goes_with_it` ; `Trash.PurgeCascadeTests.The_frame_of_a_step_purged_through_its_lane_goes_with_it` ; `Trash.PurgePlannerTests.The_plan_deletes_dependents_before_what_they_reference` |
| T-SCFL-01 | couvert | `Dissolutions.FlowScenarioTests.A_block_over_three_lanes_keeps_its_lanes_and_reading_order_and_comes_back_exactly` (avec et sans opération d'origine) |
| T-SCFL-02 | couvert | `Dissolutions.FlowScenarioTests.Flows_into_and_out_of_the_middle_of_the_block_pass_through_the_detailed_step_and_are_restored` ; `Dissolutions.FlowScenarioTests.Without_the_origin_operation_the_jumps_into_the_block_go_to_its_entry_and_leave_from_its_exit` |
| T-SCFL-03 | couvert | `Dissolutions.FlowScenarioTests.A_loop_inside_the_block_survives_the_round_trip` |
| T-SCFL-04 | couvert | `Dissolutions.FlowScenarioTests.A_selection_skipping_a_lane_with_a_parallel_branch_copies_only_the_used_lanes_and_comes_back` |
| T-SCFL-05 | couvert | `Dissolutions.FlowScenarioTests.With_two_lanes_of_the_same_name_the_step_comes_back_to_its_own_lane` |
| T-SCFL-06 | couvert | `Dissolutions.FlowScenarioTests.Both_labelled_exits_of_a_decision_leaving_the_block_come_back_with_their_labels` |
| T-SCFL-07 | couvert | `Dissolutions.FlowScenarioTests.A_block_on_a_parallel_row_comes_back_exactly` |

Renforcement sans ligne propre de l'annexe : `Translations.ManualTranslationTests.A_copy_is_dated_by_the_operation_and_written_by_its_actor_with_the_source_kept`
(règle unique de copie des traductions, `TranslationRules.Copy`) part maintenant d'une source en
version 3 : la copie repart en version 1.

## Citations imprécises corrigées

| ID | État | Test 2.0 (citation juste) |
|---|---|---|
| A-2.2-01 | couvert | `Lanes.DeleteLaneTests.The_last_live_lane_of_a_map_cannot_be_deleted` ; `SubMaps.DetailTests.The_only_lane_of_a_new_sub_map_is_a_copy_of_the_lane_of_the_step` |
| A-2.8-14 | couvert | `Selection.PasteOrderTests.An_unsorted_paste_is_created_column_by_column_with_the_shift` ; `Selection.PasteCopyTests.Pasted_steps_get_new_codes_no_sheet_and_the_operation_that_created_them` (le test cité colle après une ancre, il ne prouve pas l'ordre par colonne) |
| A-2.8-32 | couvert | `Selection.BlockTransferTests.A_role_follows_the_only_lane_of_the_same_name_of_the_arrival_map` ; `Selection.BlockTransferTests.A_role_stays_hidden_on_its_old_lane_when_the_arrival_map_has_no_or_several_lanes_of_that_name` ; `Raci.RaciFollowTests.Two_lanes_of_the_same_name_on_the_old_map_keep_the_role_hidden_on_its_old_lane` (le « au plus un au départ ») |
| A-2.9-17 | couvert | `Dissolutions.DissolveLaneTests.Without_successor_the_flow_moves_by_the_smallest_step_that_frees_the_cells_of_the_detail` ; `Dissolutions.DissolveTests.A_step_in_the_column_of_the_detailed_step_is_pushed_only_where_the_detail_arrives` (noms complets) |
| A-2.11-19 | couvert | `Documents.DocumentUsesTests.An_empty_description_erases_a_reported_pain_point_with_its_history` ; `Documents.DocumentUsesTests.An_empty_description_attaches_a_pain_point_beyond_reported_to_the_map` (règle unique `Topogenia.Domain.Documents.PainPointRemoval`, et non `Dissolutions`, AR-02) |
| A-2.14-07 | couvert | `Steps.UpdateStepTests.A_new_label_text_forgets_the_automatic_translations_and_the_same_text_keeps_them` ; `Lanes.UpdateLaneTests.Renaming_a_lane_forgets_its_automatic_translations_and_keeps_its_code_and_manual_ones` ; `Flows.FlowRulesTests.A_changed_label_drops_its_automatic_translations_and_keeps_the_manual_ones` ; `Translations.ManualTranslationTests.A_real_text_change_drops_the_automatic_translations_and_keeps_the_manual_ones` (les tests cités appelaient la règle sans renommer d'objet) |
| A-3-31 | couvert | `Layout.StepLayoutTests.A_final_state_with_two_steps_in_one_cell_is_refused_without_any_write` ; `Selection.BlockTransferTests.Two_steps_on_the_same_arrival_cell_are_refused_and_nothing_is_written` ; `Selection.PasteCopyTests.Two_pasted_steps_on_the_same_cell_are_refused` ; `Coverage.ErrorCodeValuesTests.Every_error_code_keeps_the_value_of_the_contract` |
| A-3-62 | couvert (domaine) | `Structure.UndoRestoreRulesTests.Undoing_the_deletion_of_a_step_whose_lane_was_deleted_since_is_refused_with_map_modified` ; `Structure.UndoRestoreRulesTests.Undoing_the_deletion_of_a_step_of_a_detail_deleted_since_is_refused_with_map_modified` ; `Structure.UndoRestoreRulesTests.Undoing_the_deletion_of_a_lane_of_a_detail_deleted_since_is_refused_with_map_modified` (la condition 1.x de `LOT_NON_RESTAURABLE` : restauration impossible, donc `CARTE_MODIFIEE`) |

## Notes pour la phase 4

- A-2.9-47 : en 2.0, déplacer une étape du cadre DANS la carte suffit à rendre l'annulation de la
  remontée `OPERATION_DEPASSEE` (`FirstOutdated` voit la version changée) ; la 1.x n'annulait pas
  seulement si une étape du cadre avait été supprimée ou avait changé de carte
  (`ServiceStructure.Remontee.cs:609`). L'Application de la phase 4 doit trancher : reproduire la 1.x
  (contrôle propre à la remontée, qui ignore un simple déplacement) ou arbitrer l'écart plus strict.
- A-2.8-26 / A-3-38 : traduire en `TRANSFERT_HORS_PROJET` une étape demandée absente de l'espace et
  vivante dans un autre projet (AR-71), après les contrôles de nombre et de carte unique.
- A-6-20 : message de `ETAPE_HORS_CARTE` sans `{code}` quand le détail manque (AR-74).

## Mutation équivalente relevée

- `DissolveLayout.Positions` : remplacer la colonne minimale du détail par celle de la première étape
  lue survit, car l'ordre de lecture commence toujours par la plus petite colonne : mutation
  équivalente, pas de test possible.
