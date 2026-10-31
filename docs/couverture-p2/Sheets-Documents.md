<!-- Sheets-Documents.md -->
<!-- Couverture de la phase 2 pour les dossiers Sheets et Documents : fiches, documents, usages, irritants (annexe A 2.10 à 2.12) -->

# Couverture phase 2 : dossiers `Sheets` et `Documents`

Code : `src/Topogenia.Domain/Sheets/` (`SheetContentReader`, `SheetJson`, `SheetCompleteness`, `SheetWorkflow`,
`SheetVisibility`, `SheetRights`, `SheetSuggestions`, `SheetQueries`) et `src/Topogenia.Domain/Documents/`
(`DocumentRules`, `DocumentEdits`, `DocumentUses`, `DocumentFlows`, `PainPointRules`).
Tests : `tests/Topogenia.Domain.Tests/Sheets/` (aide de construction `SheetScenario`) et
`tests/Topogenia.Domain.Tests/Documents/`.

Le retrait d'un irritant (AR-02) passe par la règle unique `Dissolutions/PainPointRemoval.cs`, écrite par l'agent de
la remontée ; `PainPointRules.Withdraw` (interne) ne fait que l'appeler.

## Lignes couvertes

| ID | Test 2.0 |
|----|----------|
| A-2.10-01 | `Topogenia.Domain.Tests.Sheets.SheetContentReaderTests.Field_keys_states_frequencies_and_natures_are_the_stored_codes` |
| A-2.10-02 | `Topogenia.Domain.Tests.Sheets.SheetContentReaderTests.The_actor_is_read_but_does_not_count_in_the_filled_rubrics` ; `Topogenia.Domain.Tests.Sheets.SheetContentReaderTests.Texts_are_bounded_to_4000_and_the_free_text_to_20000_without_html` ; `Topogenia.Domain.Tests.Sheets.SheetCompletenessTests.Five_rubrics_count_and_the_actor_and_free_text_do_not` |
| A-2.10-03 | `Topogenia.Domain.Tests.Sheets.SheetContentReaderTests.Texts_are_bounded_to_4000_and_the_free_text_to_20000_without_html` |
| A-2.10-04 | `Topogenia.Domain.Tests.Sheets.SheetContentReaderTests.Unknown_keys_values_out_of_list_or_bounds_and_wrong_types_are_refused_with_the_field` ; `Topogenia.Domain.Tests.Sheets.SheetContentReaderTests.Field_keys_states_frequencies_and_natures_are_the_stored_codes` |
| A-2.10-05 | `Topogenia.Domain.Tests.Sheets.SheetContentReaderTests.Unknown_keys_values_out_of_list_or_bounds_and_wrong_types_are_refused_with_the_field` ; `Topogenia.Domain.Tests.Sheets.SheetContentReaderTests.Duration_bounds_zero_and_one_hundred_thousand_are_accepted` |
| A-2.10-06 | `Topogenia.Domain.Tests.Sheets.SheetContentReaderTests.Entries_are_trimmed_empty_fields_disappear_and_the_duration_is_rounded_to_two_decimals` |
| A-2.10-07 | `Topogenia.Domain.Tests.Sheets.SheetContentReaderTests.Texts_are_bounded_to_4000_and_the_free_text_to_20000_without_html` |
| A-2.10-08 | `Topogenia.Domain.Tests.Sheets.SheetContentReaderTests.Unknown_keys_values_out_of_list_or_bounds_and_wrong_types_are_refused_with_the_field` |
| A-2.10-09 | `Topogenia.Domain.Tests.Sheets.SheetContentReaderTests.Unknown_keys_values_out_of_list_or_bounds_and_wrong_types_are_refused_with_the_field` |
| A-2.10-10 | `Topogenia.Domain.Tests.Sheets.SheetContentReaderTests.Unknown_nature_tool_or_nature_without_text_and_more_than_50_lines_are_refused` ; `Topogenia.Domain.Tests.Sheets.SheetContentReaderTests.Fifty_lines_are_accepted_and_task_text_and_tool_are_bounded` |
| A-2.10-11 | `Topogenia.Domain.Tests.Sheets.SheetContentReaderTests.Fifty_lines_are_accepted_and_task_text_and_tool_are_bounded` |
| A-2.10-12 | `Topogenia.Domain.Tests.Sheets.SheetContentReaderTests.Unknown_nature_tool_or_nature_without_text_and_more_than_50_lines_are_refused` ; `Topogenia.Domain.Tests.Sheets.SheetContentReaderTests.Field_keys_states_frequencies_and_natures_are_the_stored_codes` |
| A-2.10-13 | `Topogenia.Domain.Tests.Sheets.SheetContentReaderTests.Procedure_lines_are_trimmed_and_empty_lines_removed_in_request_order` |
| A-2.10-14 | `Topogenia.Domain.Tests.Sheets.SheetContentReaderTests.Unknown_nature_tool_or_nature_without_text_and_more_than_50_lines_are_refused` |
| A-2.10-15 | `Topogenia.Domain.Tests.Sheets.SheetCompletenessTests.Completeness_is_rounded_over_seven_items` ; `Topogenia.Domain.Tests.Sheets.SheetCompletenessTests.Five_rubrics_count_and_the_actor_and_free_text_do_not` |
| A-2.10-16 | `Topogenia.Domain.Tests.Sheets.SheetCompletenessTests.An_external_input_or_a_used_output_counts_as_input_and_a_produced_document_as_output` ; `Topogenia.Domain.Tests.Sheets.SheetCompletenessTests.A_use_whose_producing_step_is_deleted_no_longer_counts_as_input` |
| A-2.10-17 | `Topogenia.Domain.Tests.Sheets.SheetCompletenessTests.Four_filled_rubrics_of_a_saved_draft_give_57_in_the_view_and_in_the_map_summary` (part domaine : un seul calcul `SheetCompleteness`, utilisé par la vue et le résumé ; la vue `bi.step_sheet` relève de la persistance, phase 3) |
| A-2.10-18 | `Topogenia.Domain.Tests.Sheets.SheetWorkflowTests.The_content_sent_replaces_the_previous_one_and_the_procedure_is_rewritten_entirely` ; `Topogenia.Domain.Tests.Sheets.SheetWorkflowTests.The_first_save_creates_a_draft_sheet_with_the_complete_content` (règle du domaine ; la route relève de la phase 4) |
| A-2.10-19 | `Topogenia.Domain.Tests.Sheets.SheetWorkflowTests.An_empty_draft_creates_no_sheet` (aucun enregistrement créé dans l'espace ; l'écriture relève de la persistance) |
| A-2.10-20 | `Topogenia.Domain.Tests.Sheets.SheetWorkflowTests.An_identical_content_leaves_the_state_and_the_records_unchanged` |
| A-2.10-21 | `Topogenia.Domain.Tests.Sheets.SheetWorkflowTests.A_validated_sheet_that_changes_goes_back_to_draft_and_keeps_its_version` ; `Topogenia.Domain.Tests.Sheets.SheetVisibilityTests.A_changed_validated_sheet_is_a_draft_for_the_filler_while_the_others_keep_the_validated_version` |
| A-2.10-22 | `Topogenia.Domain.Tests.Sheets.SheetWorkflowTests.A_cleared_sheet_never_validated_becomes_empty_and_cannot_be_validated` ; `Topogenia.Domain.Tests.Sheets.SheetWorkflowTests.A_cleared_sheet_once_validated_stays_a_draft` |
| A-2.10-23 | `Topogenia.Domain.Tests.Sheets.SheetWorkflowTests.The_content_sent_replaces_the_previous_one_and_the_procedure_is_rewritten_entirely` (part domaine : positions 1 à n dans l'ordre de la demande, anciennes lignes retirées ; l'écriture DELETE puis INSERT relève de la persistance, phase 3) |
| A-2.10-25 | `Topogenia.Domain.Tests.Sheets.SheetWorkflowTests.A_cleared_sheet_never_validated_becomes_empty_and_cannot_be_validated` ; `Topogenia.Domain.Tests.Sheets.SheetWorkflowTests.A_cleared_sheet_once_validated_stays_a_draft` |
| A-2.10-26 | `Topogenia.Domain.Tests.Sheets.SheetWorkflowTests.Validating_a_validated_sheet_has_no_effect` |
| A-2.10-27 | `Topogenia.Domain.Tests.Sheets.SheetWorkflowTests.A_validated_sheet_that_changes_goes_back_to_draft_and_keeps_its_version` ; `Topogenia.Domain.Tests.Sheets.SheetWorkflowTests.A_needs_review_sheet_validated_again_without_change_gets_a_new_version` (part domaine : numéro max + 1 ; l'absence de doublon sous concurrence relève de la persistance, phase 3) |
| A-2.10-28 | `Topogenia.Domain.Tests.Sheets.SheetWorkflowTests.Validating_freezes_the_content_with_the_denormalised_frequency_and_duration` ; `Topogenia.Domain.Tests.Sheets.SheetJsonTests.Frozen_content_is_readable_english_json_read_back_identically` ; `Topogenia.Domain.Tests.Sheets.SheetJsonTests.An_old_version_is_read_without_the_fields_and_natures_retired_since` ; `Topogenia.Domain.Tests.Sheets.SheetJsonTests.An_empty_content_freezes_without_null_values` (part domaine ; les colonnes en base relèvent de la phase 3) |
| A-2.10-29 | `Topogenia.Domain.Tests.Sheets.SheetWorkflowTests.Clarification_is_asked_only_on_a_validated_sheet_with_a_text_and_cleared_by_the_next_validation` |
| A-2.10-31 | `Topogenia.Domain.Tests.Sheets.SheetWorkflowTests.Clarification_is_asked_only_on_a_validated_sheet_with_a_text_and_cleared_by_the_next_validation` |
| A-2.10-32 | `Topogenia.Domain.Tests.Sheets.SheetWorkflowTests.Clarification_is_asked_only_on_a_validated_sheet_with_a_text_and_cleared_by_the_next_validation` ; `Topogenia.Domain.Tests.Sheets.SheetVisibilityTests.A_needs_review_sheet_shows_its_request_and_its_validated_version_to_everyone` |
| A-2.10-33 | `Topogenia.Domain.Tests.Sheets.SheetWorkflowTests.Clarification_is_asked_only_on_a_validated_sheet_with_a_text_and_cleared_by_the_next_validation` |
| A-2.10-34 | `Topogenia.Domain.Tests.Sheets.SheetVisibilityTests.The_draft_is_seen_by_the_filler_and_the_facilitator_only` ; `Topogenia.Domain.Tests.Sheets.SheetVisibilityTests.A_step_without_sheet_is_seen_empty_by_everyone` |
| A-2.10-35 | `Topogenia.Domain.Tests.Sheets.SheetVisibilityTests.After_validation_the_others_see_the_validated_version` ; `Topogenia.Domain.Tests.Sheets.SheetVisibilityTests.A_needs_review_sheet_shows_its_request_and_its_validated_version_to_everyone` ; `Topogenia.Domain.Tests.Sheets.SheetVisibilityTests.The_draft_is_seen_by_the_filler_and_the_facilitator_only` |
| A-2.10-36 | `Topogenia.Domain.Tests.Sheets.SheetVisibilityTests.The_map_summary_follows_the_same_rule_for_frequency_and_duration` ; `Topogenia.Domain.Tests.Sheets.SheetVisibilityTests.A_changed_validated_sheet_is_a_draft_for_the_filler_while_the_others_keep_the_validated_version` ; `Topogenia.Domain.Tests.Sheets.SheetVisibilityTests.The_documents_count_in_the_summary_of_every_viewer_even_without_a_validated_sheet` (part domaine : `SheetVisibility.SummaryFor` lit les colonnes dénormalisées de la version ; l'assemblage de la vue de carte relève de la phase 4) |
| A-2.10-37 | `Topogenia.Domain.Tests.Sheets.SheetRightsTests.Rights_returned_are_fill_validate_as_fill_clarification_and_assignment_for_the_facilitator` ; `Topogenia.Domain.Tests.Sheets.SheetRightsTests.The_facilitator_fills_every_sheet_a_contributor_only_assigned_steps_or_his_lane` ; `Topogenia.Domain.Tests.Sheets.SheetRightsTests.The_viewer_of_a_step_follows_its_assignment_and_its_lane` ; `Topogenia.Domain.Tests.Sheets.SheetRightsTests.A_person_outside_the_project_fills_nothing_even_when_assigned` (fonction pure ; leur renvoi par l'API relève de la phase 4) |
| A-2.10-38 | `Topogenia.Domain.Tests.Sheets.SheetRightsTests.The_responsible_is_the_assignee_of_the_step_otherwise_the_actor_of_the_lane` |
| A-2.10-39 | `Topogenia.Domain.Tests.Sheets.SheetSuggestionsTests.The_frequency_comes_from_the_nearest_previous_step_of_the_same_lane_only` ; `Topogenia.Domain.Tests.Sheets.SheetSuggestionsTests.The_facilitator_gets_the_frequency_of_the_previous_step_and_the_tools_of_the_map_from_the_drafts` |
| A-2.10-40 | `Topogenia.Domain.Tests.Sheets.SheetSuggestionsTests.Tools_are_split_trimmed_deduplicated_without_case_sorted_bounded_to_60_characters_and_40_entries` ; `Topogenia.Domain.Tests.Sheets.SheetSuggestionsTests.Splitting_tools_uses_comma_semicolon_and_line_breaks` ; `Topogenia.Domain.Tests.Sheets.SheetSuggestionsTests.The_facilitator_gets_the_frequency_of_the_previous_step_and_the_tools_of_the_map_from_the_drafts` |
| A-2.10-41 | `Topogenia.Domain.Tests.Sheets.SheetSuggestionsTests.Tools_are_split_trimmed_deduplicated_without_case_sorted_bounded_to_60_characters_and_40_entries` |
| A-2.10-43 | `Topogenia.Domain.Tests.Sheets.SheetSuggestionsTests.A_draft_the_user_may_not_see_does_not_leak_until_it_is_validated` |
| A-2.10-44 | `Topogenia.Domain.Tests.Sheets.SheetWorkflowTests.A_deleted_step_has_no_sheet_any_more` (part domaine : `INTROUVABLE` ; le 404 HTTP relève de la phase 4) |
| A-2.11-01 | `Topogenia.Domain.Tests.Documents.DocumentEditsTests.An_output_belongs_to_the_step_that_produces_it` ; `Topogenia.Domain.Tests.Documents.DocumentRulesTests.The_direction_is_output_or_input_otherwise_invalid` (le champ du détail est `direction`, nom anglais de l'API 2.0, au lieu de `sens`) |
| A-2.11-02 | `Topogenia.Domain.Tests.Documents.DocumentRulesTests.The_name_is_required_and_bounded_to_200` |
| A-2.11-03 | `Topogenia.Domain.Tests.Documents.DocumentRulesTests.An_empty_address_is_no_link_and_an_address_is_bounded_to_1000` |
| A-2.11-04 | `Topogenia.Domain.Tests.Documents.DocumentRulesTests.An_address_is_an_http_url_or_a_file_path` ; `Topogenia.Domain.Tests.Documents.DocumentRulesTests.Any_other_address_is_refused` |
| A-2.11-05 | `Topogenia.Domain.Tests.Documents.DocumentRulesTests.An_address_is_an_http_url_or_a_file_path` |
| A-2.11-06 | `Topogenia.Domain.Tests.Documents.DocumentRulesTests.Any_other_address_is_refused` |
| A-2.11-07 | `Topogenia.Domain.Tests.Documents.DocumentRulesTests.Bounds_of_waiting_time_percent_and_lost_time_with_their_rounding` ; `Topogenia.Domain.Tests.Documents.DocumentEditsTests.An_external_input_carries_its_waiting_time_and_an_output_refuses_one` ; `Topogenia.Domain.Tests.Documents.DocumentUsesTests.Ticking_creates_the_use_with_its_waiting_time_and_percent_then_updates_it` |
| A-2.11-08 | `Topogenia.Domain.Tests.Documents.DocumentEditsTests.An_external_input_carries_its_waiting_time_and_an_output_refuses_one` ; `Topogenia.Domain.Tests.Documents.DocumentEditsTests.Only_the_producing_step_changes_its_document_and_the_direction_stays` (part domaine ; la contrainte CHECK relève de la persistance) |
| A-2.11-09 | `Topogenia.Domain.Tests.Documents.DocumentEditsTests.An_output_belongs_to_the_step_that_produces_it` ; `Topogenia.Domain.Tests.Documents.DocumentEditsTests.An_external_input_carries_its_waiting_time_and_an_output_refuses_one` |
| A-2.11-10 | `Topogenia.Domain.Tests.Documents.DocumentFlowsTests.Outputs_of_the_steps_linked_by_an_incoming_flow_of_any_lane_are_offered_unticked` ; `Topogenia.Domain.Tests.Documents.DocumentFlowsTests.An_output_declared_before_the_flow_is_offered_as_soon_as_the_flow_exists` ; `Topogenia.Domain.Tests.Documents.DocumentFlowsTests.Outputs_of_a_deleted_producer_are_no_longer_offered` (règle pure `DocumentFlows.InputsOf` ; la vue de fiche relève de la phase 4) |
| A-2.11-11 | `Topogenia.Domain.Tests.Documents.DocumentFlowsTests.Ticked_outputs_from_elsewhere_stay_marked_remote_and_become_near_when_the_flow_comes_back` (règle pure ; vue en phase 4) |
| A-2.11-12 | `Topogenia.Domain.Tests.Documents.DocumentFlowsTests.External_inputs_come_last_ticked_and_are_not_offered_to_other_steps` (règle pure ; vue en phase 4) |
| A-2.11-13 | `Topogenia.Domain.Tests.Documents.DocumentUsesTests.A_document_of_another_live_step_of_the_same_map_may_be_used_even_without_flow` ; `Topogenia.Domain.Tests.Documents.DocumentUsesTests.Its_own_document_an_unknown_one_an_input_or_one_of_another_map_or_a_deleted_producer_is_not_found` |
| A-2.11-14 | `Topogenia.Domain.Tests.Documents.DocumentUsesTests.Ticking_creates_the_use_with_its_waiting_time_and_percent_then_updates_it` (upsert dans l'espace ; l'écriture relève de la persistance) |
| A-2.11-15 | `Topogenia.Domain.Tests.Documents.DocumentRulesTests.Bounds_of_waiting_time_percent_and_lost_time_with_their_rounding` ; `Topogenia.Domain.Tests.Documents.DocumentUsesTests.Invalid_values_of_a_use_are_refused_before_any_change` |
| A-2.11-16 | `Topogenia.Domain.Tests.Documents.DocumentUsesTests.A_pain_point_is_raised_reported_with_its_history_on_the_producing_step` |
| A-2.11-17 | `Topogenia.Domain.Tests.Documents.DocumentUsesTests.A_pain_point_is_updated_without_duplicate_one_per_use` ; `Topogenia.Domain.Tests.Documents.DocumentRulesTests.A_pain_point_description_is_optional_bounded_to_2000_without_html` |
| A-2.11-18 | `Topogenia.Domain.Tests.Documents.DocumentRulesTests.Bounds_of_waiting_time_percent_and_lost_time_with_their_rounding` ; `Topogenia.Domain.Tests.Documents.DocumentUsesTests.A_pain_point_is_updated_without_duplicate_one_per_use` |
| A-2.11-19 | `Topogenia.Domain.Tests.Documents.DocumentUsesTests.An_empty_description_erases_a_reported_pain_point_with_its_history` ; `Topogenia.Domain.Tests.Documents.DocumentUsesTests.An_empty_description_attaches_a_pain_point_beyond_reported_to_the_map` (règle unique `Dissolutions.PainPointRemoval`, AR-02) |
| A-2.11-20 | `Topogenia.Domain.Tests.Documents.DocumentUsesTests.A_pain_point_is_updated_without_duplicate_one_per_use` (part domaine ; l'index unique partiel relève de la persistance, phase 3) |
| A-2.11-21 | `Topogenia.Domain.Tests.Documents.DocumentUsesTests.Unticking_removes_the_use_and_its_pain_point_by_the_same_rule_and_is_idempotent` ; `Topogenia.Domain.Tests.Documents.DocumentFlowsTests.Outputs_of_the_steps_linked_by_an_incoming_flow_of_any_lane_are_offered_unticked` |
| A-2.11-22 | `Topogenia.Domain.Tests.Documents.DocumentEditsTests.Deleting_a_document_removes_its_uses_on_every_step_and_their_pain_points` |
| A-2.11-23 | `Topogenia.Domain.Tests.Documents.DocumentFlowsTests.Other_lists_the_outputs_of_the_map_except_the_step_and_its_predecessors_grouped_by_step` (règle pure `DocumentFlows.OtherOutputs` ; la route relève de la phase 4) |
| A-2.12-01 | `Topogenia.Domain.Tests.Documents.PainPointRulesTests.Statuses_are_the_stored_codes_and_only_reported_is_produced` |
| A-2.12-02 | `Topogenia.Domain.Tests.Documents.PainPointRulesTests.A_pain_point_beyond_reported_is_never_erased_only_detached_from_its_step` ; `Topogenia.Domain.Tests.Documents.DocumentUsesTests.An_empty_description_attaches_a_pain_point_beyond_reported_to_the_map` |
| A-2.12-03 | `Topogenia.Domain.Tests.Documents.PainPointRulesTests.The_history_is_append_only_no_operation_changes_a_history_record` (part domaine ; DENY UPDATE en SQL Server relève de la phase 3) |

## Lignes d'autres parts couvertes en plus ici

| ID | Test 2.0 |
|----|----------|
| A-2.7-02 | `Topogenia.Domain.Tests.Sheets.SheetWorkflowTests.A_step_without_content_has_no_sheet_to_write` ; `Topogenia.Domain.Tests.Documents.DocumentEditsTests.A_document_is_refused_on_a_step_without_content_or_a_deleted_step` ; `Topogenia.Domain.Tests.Documents.DocumentUsesTests.Invalid_values_of_a_use_are_refused_before_any_change` (écriture refusée en `ETAPE_SANS_CONTENU` ; la lecture ne contrôle pas le type) |
| A-6-06 | `Topogenia.Domain.Tests.Sheets.SheetCompletenessTests.Four_filled_rubrics_of_a_saved_draft_give_57_in_the_view_and_in_the_map_summary` (part domaine, comme A-2.10-17) |
| A-6-13 | `Topogenia.Domain.Tests.Documents.DocumentUsesTests.An_empty_description_attaches_a_pain_point_beyond_reported_to_the_map` ; `Topogenia.Domain.Tests.Documents.DocumentUsesTests.Unticking_removes_the_use_and_its_pain_point_by_the_same_rule_and_is_idempotent` ; `Topogenia.Domain.Tests.Documents.DocumentEditsTests.Deleting_a_document_removes_its_uses_on_every_step_and_their_pain_points` (les usages appellent `PainPointRemoval.Remove` du dossier Dissolutions) |

## Lignes non couvertes

| ID | Raison |
|----|--------|
| A-2.10-24 | Valider réservé à qui peut remplir (403) : autorisation, relève de l'Application (phase 4). Le domaine fournit `SheetRights.CanFill` et `SheetRights.PermissionsOf`. |
| A-2.10-30 | Demande de précision réservée à l'animateur (403) : autorisation, relève de l'Application (phase 4). |
| A-2.10-42 | Suggestions renvoyées seulement à qui peut remplir : choix de l'Application (phase 4) ; `SheetSuggestions.For` ne filtre que la visibilité. |
| A-2.11-24 | Documents visibles de tous les membres dès leur saisie : relève de l'Application (phase 4). Côté domaine, `DocumentFlows` ne prend aucun lecteur en paramètre et le résumé compte les documents pour tout lecteur (`Topogenia.Domain.Tests.Sheets.SheetVisibilityTests.The_documents_count_in_the_summary_of_every_viewer_even_without_a_validated_sheet`). |

Hors du domaine également : le contrôle `If-Match` et `CONFLIT_VERSION` sur la fiche, la validation et le document
(T-FICI-09, partie version de T-DOCF-04 : phase 4) ; un usage posé par l'étape voisine ne touche pas l'enregistrement
`StepSheet` du producteur (T-DOCF-08), à prouver avec la persistance.
