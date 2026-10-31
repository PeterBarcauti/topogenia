<!-- r3-tests.md -->
<!-- Couverture du round 3 de la porte de la phase 2 : tests qui distinguent les règles, ordres alignés sur la 1.x, citations et forme -->

# Couverture phase 2, round 3 : tests qui distinguent les règles

Chaque test ajouté ou renforcé a été vérifié contre la mutation du constat (copie du dépôt dans le bac à sable,
mutation appliquée, construction, exécution, restauration) : la mutation fait échouer le test. Préfixe des tests :
`Topogenia.Domain.Tests.`. À reporter dans `docs/couverture.md` : la colonne « Test 2.0 » reçoit les tests ci-dessous
EN PLUS des tests déjà cités (sauf mention « retirer »), la colonne « État » prend la valeur indiquée.

## Écarts à la 1.x : alignés ou arbitrés

| ID | État | Test 2.0 | Remarque |
|---|---|---|---|
| A-1.4-17 | couvert (domaine) | `Steps.StepUpdateOrderTests.A_filled_sub_map_is_refused_before_an_unknown_automation_status` ; `Steps.StepUpdateOrderTests.A_filled_sub_map_is_refused_before_an_assignee_outside_the_project` ; `Steps.StepUpdateOrderTests.A_filled_sub_map_is_refused_before_a_lane_of_another_map_and_a_column_out_of_bounds` ; `Steps.StepUpdateOrderTests.The_label_and_the_type_are_refused_before_a_filled_sub_map` ; `Steps.StepUpdateOrderTests.Without_a_filled_sub_map_the_automation_comes_before_the_assignee_and_the_assignee_before_the_cell` | aligné sur la 1.x (`ServiceStructure.cs:391-395` avant `:398`, `:417`, `:424`) : `StepOperations.Update` contrôle libellé, type, sous-carte remplie, automatisation, assigné, case |
| A-3-19 | couvert (domaine) | `Projects.ProjectCreationOrderTests.A_taken_code_with_an_invalid_member_login_gives_the_refusal_of_the_member` ; `Projects.ProjectCreationOrderTests.A_code_in_the_trash_with_an_invalid_member_role_gives_the_refusal_of_the_member` ; `Projects.ProjectCreationOrderTests.A_taken_code_with_a_member_given_twice_gives_the_refusal_of_the_member` ; `Projects.ProjectCreationOrderTests.A_taken_code_with_valid_members_is_refused_on_its_uniqueness` ; `Projects.ProjectCreationOrderTests.A_taken_code_is_refused_before_an_invalid_translation_of_the_name` ; `Projects.ProjectCreationOrderTests.The_format_of_the_code_and_the_name_are_refused_before_an_invalid_member` | aligné sur la 1.x (`ServiceProjets.cs:104` membres avant `:136` unicité, puis traductions) : `ProjectCreation.Create` appelle `EnsureFree` après `MemberRules.Resolve` |
| A-2.9-12 | couvert (AR-75) | `Dissolutions.DissolveRaciFollowTests.A_role_on_a_lane_copied_from_a_lane_of_the_flow_follows_that_lane_even_under_another_name` ; `Dissolutions.DissolveRaciFollowTests.A_role_stays_hidden_on_its_lane_when_the_flow_has_two_lanes_of_its_name` ; `Dissolutions.DissolveRaciFollowTests.A_role_stays_hidden_on_its_lane_when_the_detail_has_two_lanes_of_its_name` ; `Dissolutions.DissolveRaciFollowTests.A_role_follows_the_single_lane_of_its_name_in_the_flow_trimmed_and_case_ignored` ; `Dissolutions.DissolveRaciFollowTests.A_role_whose_lane_goes_to_the_lane_of_its_step_stays_hidden_on_its_old_lane` | couloir rattaché par `source_lane_id` sous un autre nom : 2.0 gardée (AR-12) ; deux homonymes (flux ou détail) : aligné sur la 1.x (`PoserBloc`, `ServiceStructure.Selection.cs:264-280`), rôle masqué ; tue aussi A19 |
| A-2.4-25 | couvert (AR-77) | (inchangé) | champ `assignee_user_id` au lieu de `acteur_utilisateur_id` 1.x |
| A-2.4-27 | couvert (AR-76) | `Layout.ReadingOrderTieTests.Lanes_with_the_same_sort_order_are_read_in_creation_order_before_the_rows` | départage des couloirs de même `sort_order` par l'ordre unique des couloirs |
| A-2.9-18 | couvert (AR-73) | (inchangé) ; `Dissolutions.DissolveMutantGuardTests.A_detail_may_end_exactly_at_column_10000` | la 1.x laissait une étape du détail dépasser 10 000 (contrôle du `MAX` avant `PoserBloc`, `Remontee.cs:169-174`) ; AR-73 (b) cite déjà la remontée |

## Preuves qui distinguent la règle (tests nouveaux, mutation tuée)

| ID | État | Test 2.0 | Remarque |
|---|---|---|---|
| A-2.8-38 | couvert | `Selection.SelectionRuleGuardTests.A_grouped_block_is_placed_at_row_minus_its_minimal_row_in_every_copied_lane` | tue X30 (INT3-M1) |
| A-2.3-11 | couvert | `Lanes.LaneRuleGuardTests.Moved_steps_of_one_row_are_moved_by_increasing_column_so_that_the_target_step_ends_after_them` ; `Lanes.LaneRuleGuardTests.Moved_steps_are_moved_by_row_then_column_and_each_row_of_the_target_shifts_on_its_own` | tue L16 (INT3-M2) ; l'ordre entre rangées est équivalent (`FreeCell` ne décale que la rangée de l'étape) |
| A-2.8-03 | couvert | `Selection.SelectionRuleGuardTests.A_block_is_connected_whatever_the_direction_of_its_flows` ; `Selection.SelectionRuleGuardTests.Steps_linked_through_a_chain_form_one_block_that_is_stitched_from_its_predecessor_to_its_successor` | tue X12 (INT3-M3) et la connexité non transitive |
| A-2.8-08 | couvert | `Selection.SelectionRuleGuardTests.A_stitch_is_not_made_through_an_entering_flow_that_the_selection_deletes` ; `Selection.SelectionRuleGuardTests.A_stitch_is_not_made_through_a_leaving_flow_that_the_selection_deletes` | tue X04 (INT3-M4) ; raccords sans les flèches supprimées |
| A-2.2-07 | couvert | `SubMaps.SubMapRuleGuardTests.With_the_attach_choice_an_empty_sub_map_is_deleted_and_not_given_to_the_heir` ; `SubMaps.SubMapRuleGuardTests.With_the_attach_choice_an_empty_sub_map_needs_no_heir` | tue B02 (INT3-M5) |
| A-2.2-01 | couvert | `Lanes.LaneRuleGuardTests.The_last_lane_of_a_map_holding_steps_stays_whatever_the_choice` | COULOIR_DERNIER aussi pour un couloir non vide |
| A-2.3-09 | couvert (domaine) | `Lanes.LaneRuleGuardTests.The_last_lane_of_a_map_is_refused_even_when_it_holds_steps_and_no_choice_is_given` ; `Lanes.LaneRuleGuardTests.The_last_lane_of_a_map_holding_steps_stays_whatever_the_choice` | idem |
| A-2.4-06 | couvert | `Steps.StepRuleGuardTests.Moving_a_step_inside_its_own_block_shifts_only_the_steps_before_it` | exclusion de l'étape déplacée (`GridRules.cs:67`) |
| A-2.4-08 | couvert | `Steps.StepRuleGuardTests.Moving_a_step_inside_its_own_block_shifts_only_the_steps_before_it` | idem |
| A-2.8-36 | couvert | `Selection.SelectionRuleGuardTests.At_equal_column_the_new_step_of_a_group_takes_the_cell_of_the_first_lane_before_the_lower_row` | couloir avant rangée |
| A-2.8-43 | couvert (domaine) | `Selection.SelectionRuleGuardTests.A_flow_deleted_by_an_earlier_operation_is_not_found_even_when_the_batch_of_the_operation_exists` ; `Selection.SelectionRuleGuardTests.A_flow_already_deleted_in_the_batch_of_the_operation_is_accepted` | flèche d'un autre lot |
| A-2.3-03 | couvert | `Lanes.LaneRuleGuardTests.The_default_tint_of_a_new_lane_counts_only_the_live_lanes_of_the_map` | couloirs vivants |
| A-2.3-16 | couvert | `Steps.StepRuleGuardTests.A_step_inserted_below_goes_to_the_next_lane_and_not_to_the_last_one` ; `Steps.StepRuleGuardTests.A_step_inserted_above_goes_to_the_previous_lane_and_not_to_the_first_one` | voisin ±1 |
| A-3-37 | couvert | `SubMaps.SubMapRuleGuardTests.Reattaching_an_empty_sub_map_to_the_step_that_holds_it_is_refused_without_any_write` ; `SubMaps.SubMapRuleGuardTests.Reattaching_a_filled_sub_map_to_the_step_that_holds_it_is_refused_without_any_write` | la cible tient déjà la carte |
| A-2.2-23 | couvert | `SubMaps.SubMapRuleGuardTests.Reattaching_an_empty_sub_map_to_the_step_that_holds_it_is_refused_without_any_write` | idem |
| A-2.8-32 | couvert | `Raci.RaciFollowGuardTests.A_role_does_not_follow_a_linked_lane_that_is_the_lane_of_its_step` ; `Raci.RaciFollowGuardTests.A_step_cut_into_the_copy_of_the_lane_of_its_role_keeps_the_role_hidden_on_the_old_lane` | tue A15 |
| A-2.8-33 | couvert | `Raci.RaciFollowGuardTests.A_step_cut_into_the_copy_of_the_lane_of_its_role_keeps_the_role_hidden_on_the_old_lane` | tue A15 |
| A-2.15-23 | couvert (domaine) | `Trash.PurgeMutantGuardTests.The_frame_step_of_a_purged_member_goes_before_it_while_the_open_frame_stays` ; `Trash.PurgeMutantGuardTests.A_role_held_by_a_purged_lane_goes_before_the_lane_while_its_step_stays` ; `Trash.PurgeMutantGuardTests.A_whole_purge_takes_the_pain_points_of_the_map_without_step_but_not_those_of_another_project` ; `Trash.PurgePlannerTests.The_plan_deletes_dependents_before_what_they_reference` (renforcé : chaque type présent) | tue U13, U16, U18 (INT3-M6) ; `IndexOf` -1 fermé |
| A-2.9-41 | couvert | `Dissolutions.FinishMutantGuardTests.Finish_drops_the_usage_of_a_document_that_the_first_step_produces` | tue D46 (INT3-M8) |
| A-2.9-40 | couvert | `Dissolutions.FinishMutantGuardTests.Finish_skips_a_parallel_gateway_of_the_frame_for_inputs_and_pain_points` | tue D49 |
| A-2.9-42 | couvert | `Dissolutions.FinishMutantGuardTests.Finish_skips_a_parallel_gateway_of_the_frame_for_inputs_and_pain_points` | tue D49 |
| A-2.9-43 | couvert | `Dissolutions.FinishMutantGuardTests.A_frame_without_step_stores_on_the_first_step_of_the_map_that_holds_content` | filtre « sans contenu » |
| A-2.9-46 | couvert (domaine) | `Dissolutions.FinishMutantGuardTests.A_second_finish_changes_nothing_not_even_the_closing_moment` | tue D50 (Commit vide, `ClosedAt` inchangé) |
| A-2.9-25 | couvert | `Dissolutions.RemainderMutantGuardTests.The_remainder_lists_the_outputs_by_name_then_the_inputs` ; `Dissolutions.RemainderMutantGuardTests.The_users_of_a_remainder_document_are_its_live_users_only` | tue D51, D52 |
| A-2.9-26 | couvert | `Dissolutions.RemainderMutantGuardTests.A_live_step_of_another_map_is_not_a_target_and_the_document_stays` | `Remainder.cs:100` |
| A-2.9-34 | couvert (domaine) | `Dissolutions.RemainderMutantGuardTests.A_deleted_step_of_the_map_is_not_a_target` ; `Dissolutions.RemainderMutantGuardTests.Bringing_back_from_a_deleted_step_of_the_map_is_refused` | ETAPE_HORS_CARTE comme la 1.x (`Remontee.cs:392-393`, `:413-414`) |
| A-1.5-05 | couvert (domaine) | `Dissolutions.RemainderMutantGuardTests.Assigning_on_a_closed_frame_is_a_conflict` | genre Conflit vérifié |
| A-1.5-07 | couvert (domaine) | `Dissolutions.RemainderMutantGuardTests.A_usage_brought_back_without_its_holder_is_refused_on_from_step_id` | détail `field = from_step_id` |
| A-1.5-08 | couvert (domaine) | `Dissolutions.RemainderMutantGuardTests.Bringing_back_from_a_step_of_another_map_is_refused_and_its_pain_point_stays` | l'irritant ne bouge pas |
| A-2.9-02 | couvert (domaine) | `Dissolutions.DissolveMutantGuardTests.An_open_frame_is_reported_before_mixed_frequencies` | ordre 1.x (`Remontee.cs:55-57`) |
| A-2.9-05 | couvert | `Dissolutions.DissolveMutantGuardTests.An_open_frame_is_reported_before_mixed_frequencies` | idem |
| A-2.9-19 | couvert | `Dissolutions.DissolveMutantGuardTests.Only_the_original_flows_of_the_map_come_back_a_flow_of_another_map_in_the_batch_is_ignored` | tue D11, D14 ; D12 équivalent |
| A-2.9-20 | couvert | `Dissolutions.DissolveMutantGuardTests.The_ends_of_the_detail_are_taken_by_column_before_row` | tue D10 |
| A-2.9-21 | couvert | `Dissolutions.DissolveMutantGuardTests.A_carried_flow_takes_the_translations_of_the_flow_it_copies` | `DissolveFlows.cs:80` |
| A-2.9-23 | couvert (domaine) | `Dissolutions.DissolveMutantGuardTests.The_frame_records_the_original_lane_column_and_row_of_each_step` | tue D05 |
| A-2.9-14 | couvert | `Dissolutions.DissolveMutantGuardTests.The_detail_is_put_left_to_right_and_pushes_a_step_in_its_way_past_the_whole_block` | tue D27 (ordre de `PoserBloc`, `Selection.cs:247-248`) |
| A-2.9-17 | couvert | (inchangé) | D23 équivalent : au pas `largeur - 1` aucune étape du flux ne peut tomber sur une case du détail |
| A-6-11 | couvert | `Dissolutions.DissolveMutantGuardTests.The_shift_of_the_flow_stops_exactly_at_column_10000` ; `Dissolutions.DissolveMutantGuardTests.A_detail_may_end_exactly_at_column_10000` | bornes exactes 10 000 / 10 001 |
| A-2.11-23 | couvert (domaine) | `Documents.DocumentDistinctionTests.The_other_window_lists_only_the_outputs_of_the_steps_of_the_same_map` ; `Documents.DocumentDistinctionTests.The_outputs_of_a_step_list_only_the_uses_of_live_steps` ; `Documents.DocumentDistinctionTests.Outputs_of_one_column_come_in_lane_order_before_the_entry_order` | tue O11, O09 (INT3-M7), O13 |
| A-2.11-09 | couvert | `Documents.DocumentDistinctionTests.A_change_without_the_share_flag_sets_it_back_to_false` | tue O05 |
| A-2.14-22 | couvert | `Translations.TranslationDistinctionTests.An_invalid_text_refuses_the_file_even_on_a_protected_manual_translation` | validation avant protection manuelle |
| A-2.14-24 | couvert | `Translations.TranslationDistinctionTests.Only_the_root_map_follows_the_project_even_when_a_detailed_map_bears_the_project_name` | carte racine seule |
| A-2.14-25 | couvert | `Translations.TranslationDistinctionTests.A_deleted_detailed_map_does_not_follow_its_step` | carte supprimée |
| A-2.14-05 | couvert | `Translations.TranslationDistinctionTests.An_arrow_takes_the_language_of_its_source_step_not_the_one_of_the_project` | tue T08 : la langue d'origine d'une flèche est celle de son étape source |
| A-2.14-08 | couvert (domaine) | `Translations.TranslationDistinctionTests.Labelled_arrows_come_in_the_reading_order_of_their_source_before_their_target` | tue T21 : flèches dans l'ordre de lecture de la source (1.x `ServiceTraductions.cs:205`) |
| A-2.13-13 | couvert | `Raci.RaciDistinctionTests.Visible_roles_follow_the_lane_sort_order_not_the_identifiers_nor_the_creation_order` | tue A12 |
| A-2.10-36 | couvert | `Sheets.SheetDistinctionTests.The_gauge_of_the_others_counts_the_rubrics_of_the_validated_version_not_of_the_draft` | jauge de la version |
| A-2.7-02 | couvert | `Sheets.SheetDistinctionTests.The_sheet_of_a_step_without_content_is_read_as_an_empty_sheet` | pas de contrôle du type à la lecture |
| A-2.10-10 | couvert | `Sheets.SheetDistinctionTests.Fifty_operations_and_blank_lines_are_accepted_because_the_limit_counts_the_lines_with_a_text` | comptage après retrait des lignes vides |
| A-2.10-12 | couvert | `Sheets.SheetDistinctionTests.The_nature_of_an_operation_is_trimmed_before_the_closed_list` | Trim de la nature |
| A-2.10-31 | à reporter | `Sheets.SheetWorkflowDistinctionTests.A_second_clarification_on_a_sheet_to_review_is_refused_and_keeps_the_first_request` | test de `SheetWorkflow` hors dépôt (scratchpad `r4\sub3\pending`), vert, mutation tuée |
| A-3-50 | à reporter | `Sheets.SheetWorkflowDistinctionTests.An_empty_clarification_is_refused_on_the_text_before_the_state_of_the_sheet` | idem |
| A-3-20 | couvert (domaine) | `Projects.ProjectDistinctionTests.An_unknown_member_role_is_reported_as_entered` | détail `role` tel que saisi |
| A-3-21 | couvert (domaine) | `Projects.ProjectDistinctionTests.A_login_entered_twice_is_reported_trimmed_as_entered_the_second_time` | détail `login` nettoyé |
| A-3-15 | couvert (domaine) | `Projects.ProjectDistinctionTests.A_project_code_of_two_or_seven_letters_is_refused` ; `Projects.ProjectDistinctionTests.A_project_code_of_three_or_six_letters_is_accepted` | bornes 2 / 3 / 6 / 7 |
| A-5-25 | couvert (domaine) | mêmes tests que A-3-15 | |
| A-2.18-02 | couvert | `Values.TextDistinctionTests.A_text_with_a_tag_and_a_control_character_is_refused_as_html` ; `Values.TextDistinctionTests.A_too_long_text_with_a_tag_is_refused_for_its_length_with_the_field_and_the_limit` | ordre 1.x (`Validation.cs:34-39`) |
| A-2.18-04 | couvert | `Values.TextDistinctionTests.A_too_long_text_with_a_tag_is_refused_for_its_length_with_the_field_and_the_limit` ; `Values.TextDistinctionTests.A_too_long_text_with_a_control_character_is_refused_for_its_length` | longueur d'abord |
| A-2.8-09 | couvert | `Selection.SelectionRuleGuardTests.A_deleted_step_of_the_map_is_refused_as_outside_the_map` | étape supprimée |
| A-2.8-37 | couvert | `Selection.SelectionRuleGuardTests.The_copied_lanes_of_a_group_are_numbered_from_zero_without_gap_when_an_unused_lane_separates_them` | numérotation sans trou |
| A-2.3-04 | couvert | `Lanes.LaneRuleGuardTests.A_blank_lane_name_is_refused_as_required_on_the_name_field` | champ `name` |
| (aucune ligne ; AR-64) | - | `Projects.ProjectDistinctionTests.The_sharing_group_label_holds_one_hundred_characters_and_refuses_one_more` | tue Q11 : libellé du groupe de partage de 100 caractères au plus |
| (aucune ligne ; architecture.md §2) | - | `Dissolutions.RemainderUsageVersionTests.A_usage_assigned_to_a_step_of_the_flow_starts_at_version_1` ; `Dissolutions.RemainderUsageVersionTests.A_usage_stored_by_finish_starts_at_version_1` | correction `Remainder.cs:149` : l'usage recréé sur la cible (clé changée) est à la version 1 |

## Citations à compléter

| ID | État | Test 2.0 à ajouter | Remarque |
|---|---|---|---|
| A-2.4-04 | couvert (AR-73) | `Structure.StructurePrimitivesTests.A_shift_beyond_the_last_column_is_refused` | décalage RG-C04 au-delà de 10 000 |
| A-2.4-01 | couvert | `Steps.StepClosedListTests.A_step_type_in_another_case_or_with_spaces_is_refused_on_type` | codes exacts, comme `ParmiListe` 1.x (`Validation.cs:45`) ; mutation casse ignorée tuée |
| A-2.4-02 | couvert | `Steps.StepClosedListTests.An_automation_status_in_another_case_or_with_spaces_is_refused_on_automation_status` | idem |
| A-2.11-01 | couvert | `Documents.DocumentEditsTests.An_external_input_carries_its_waiting_time_and_an_output_refuses_one` | une entrée appartient à l'étape qui la reçoit ; autre sens refusé |
| A-2.8-10 | couvert (domaine) | `SubMaps.ParentWalkTests.Without_choice_one_filled_sub_map_among_several_steps_refuses_before_any_deletion` ; `SubMaps.SubMapDeletionTests.Deleting_a_macro_step_without_choice_is_refused_and_changes_nothing` | refus 409 avant toute suppression |
| A-2.8-22 | couvert | `Coverage.AnchorFlowsTests.Pasting_before_a_step_with_one_incoming_flow_cuts_it_and_links_the_predecessor_to_the_entry_and_the_exit_to_the_step` ; `Coverage.AnchorFlowsTests.Pasting_after_a_step_with_one_outgoing_flow_cuts_it_and_links_the_step_to_the_entry_and_the_exit_to_the_successor` | libellé « suite » gardé |
| A-3-23 | couvert (domaine) | `Translations.TranslationImportTests.A_single_invalid_translation_refuses_the_whole_file` ; `Translations.TranslationDistinctionTests.An_invalid_text_refuses_the_file_even_on_a_protected_manual_translation` | |
| A-6-06 | couvert (domaine) | `Sheets.SheetCompletenessTests.Completeness_is_rounded_over_seven_items` ; `Sheets.SheetCompletenessTests.An_external_input_or_a_used_output_counts_as_input_and_a_produced_document_as_output` ; `Sheets.SheetDistinctionTests.The_gauge_of_the_others_counts_the_rubrics_of_the_validated_version_not_of_the_draft` | un seul calcul |
| A-6-09 | couvert (AR-12, AR-75) | `Raci.RaciFollowTests.A_role_follows_to_the_single_lane_of_the_same_name_on_the_new_map` ; `Raci.RaciFollowTests.Two_lanes_of_the_same_name_on_the_new_map_keep_the_role_hidden_on_its_old_lane` ; `Raci.RaciFollowTests.Two_lanes_of_the_same_name_on_the_old_map_keep_the_role_hidden_on_its_old_lane` ; `Dissolutions.DissolveRaciFollowTests.A_role_stays_hidden_on_its_lane_when_the_flow_has_two_lanes_of_its_name` | référence 1.x à corriger : `Services/ServiceStructure.Selection.cs:264-280` (`PoserBloc`) au lieu de `Selection.cs:264-280` |
| A-6-14 | couvert (domaine) | `Raci.RaciVisibilityTests.A_role_on_a_lane_of_another_map_is_hidden` ; `Raci.RaciVisibilityTests.A_role_on_a_deleted_lane_is_hidden_and_comes_back_when_the_lane_is_restored` ; `Raci.RaciVisibilityTests.A_role_on_the_lane_the_step_moved_into_is_hidden_by_the_r_and_comes_back_when_the_step_leaves` | un seul masquage (`RaciRules.Visible`) |
| A-2.6-08 | couvert | retirer `Topogenia.Architecture.Tests.DomainModelRulesTests.The_folders_of_the_domain_depend_on_each_other_without_cycle` | test d'absence de cycle entre dossiers : ne prouve rien sur RG-C05 |

## Corrections de forme

État « couvert (domaine) » à passer à « couvert » (niveau « Domaine » seul, vérifié ligne par ligne dans
`couverture.md`) : A-2.15-06, A-2.15-07, A-2.15-08, A-2.15-09, A-2.15-10, A-2.8-26, A-2.10-34, A-6-17.

Lignes de niveau « Domaine, API » dont la phase « 2 » doit devenir « 2, 4 » (26 lignes) : A-1.4-17, A-1.5-05,
A-1.5-06, A-1.5-07, A-1.5-08, A-1.5-09, A-2.2-03, A-2.2-13, A-2.2-20, A-2.3-09, A-2.3-10, A-2.3-13, A-2.5-06,
A-2.6-04, A-2.6-10, A-2.6-28, A-2.8-10, A-2.8-11, A-2.8-43, A-2.8-34, A-2.9-29, A-2.9-31, A-2.9-34, A-2.9-46,
A-2.11-21, A-6-20.

`portage-tests.md` : T-UDOM-04 → `Interchange.Tests` (nom du fichier d'export, phase 4) ; T-UINF-17 → `Host.Tests`
(durée relative formatée par l'hôte, A-2.16-07, phase 4). Fait.
