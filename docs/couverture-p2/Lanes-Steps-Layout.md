<!-- Lanes-Steps-Layout.md -->
<!-- Couverture de la phase 2 pour les dossiers Lanes, Steps et Layout : couloirs, étapes, disposition, avertissements (annexe A 2.3, 2.4, 2.5, 2.7) -->

# Couverture phase 2 : dossiers `Lanes`, `Steps` et `Layout`

Code : `src/Topogenia.Domain/Lanes/` (`LaneOperations`, `TranslationEdits`, `Membership`, `FieldChange`),
`src/Topogenia.Domain/Steps/` (`StepOperations`, `StepInsertion`),
`src/Topogenia.Domain/Layout/` (`StepLayout`, `ReadingPaths`, `LayoutWarnings`).
Tests : `tests/Topogenia.Domain.Tests/Lanes/`, `Steps/`, `Layout/` (aide de construction `Lanes/StructureTestData`).

## Lignes couvertes

| ID | Test 2.0 |
|----|----------|
| A-2.3-01 | `Topogenia.Domain.Tests.Lanes.AddLaneTests.A_lane_type_of_the_closed_list_is_accepted` ; `Topogenia.Domain.Tests.Lanes.AddLaneTests.A_lane_type_outside_the_closed_list_is_refused` ; `Topogenia.Domain.Tests.Lanes.AddLaneTests.A_lane_added_without_order_goes_last_with_the_next_code_and_the_tint_of_its_rank` |
| A-2.3-02 | `Topogenia.Domain.Tests.Lanes.AddLaneTests.A_lowercase_colour_is_accepted_and_put_in_upper_case` ; `Topogenia.Domain.Tests.Lanes.AddLaneTests.A_colour_that_is_not_hex_rrggbb_is_refused` ; `Topogenia.Domain.Tests.Lanes.UpdateLaneTests.Type_colour_and_actor_are_updated_and_the_actor_can_be_removed` |
| A-2.3-03 | `Topogenia.Domain.Tests.Lanes.AddLaneTests.The_default_colour_follows_the_number_of_lanes_of_the_map_over_the_eight_tints` ; `Topogenia.Domain.Tests.Lanes.AddLaneTests.A_lane_added_without_order_goes_last_with_the_next_code_and_the_tint_of_its_rank` (la conversion des couleurs 1.x à la migration relève de l'Interchange) |
| A-2.3-04 | `Topogenia.Domain.Tests.Lanes.AddLaneTests.A_lane_name_is_required_and_checked` ; `Topogenia.Domain.Tests.Lanes.UpdateLaneTests.Invalid_values_of_an_update_are_refused_without_any_write` |
| A-2.3-05 | `Topogenia.Domain.Tests.Lanes.AddLaneTests.A_lane_name_holds_150_characters_at_most` |
| A-2.3-06 | `Topogenia.Domain.Tests.Lanes.AddLaneTests.A_lane_added_at_the_head_shifts_the_others_and_the_order_is_renumbered` ; `Topogenia.Domain.Tests.Lanes.AddLaneTests.An_order_out_of_bounds_is_brought_back_into_zero_to_n` |
| A-2.3-07 | `Topogenia.Domain.Tests.Lanes.AddLaneTests.The_actor_of_a_lane_must_be_a_member_of_the_project` ; `Topogenia.Domain.Tests.Lanes.UpdateLaneTests.Invalid_values_of_an_update_are_refused_without_any_write` |
| A-2.3-08 | `Topogenia.Domain.Tests.Lanes.ReorderLanesTests.Reordering_with_every_live_lane_once_gives_each_lane_its_position` ; `Topogenia.Domain.Tests.Lanes.ReorderLanesTests.An_order_that_is_not_the_complete_list_of_live_lanes_is_refused` |
| A-2.3-09 | `Topogenia.Domain.Tests.Lanes.DeleteLaneTests.The_last_live_lane_of_a_map_cannot_be_deleted` (part domaine ; le statut 409 relève de l'API) |
| A-2.3-10 | `Topogenia.Domain.Tests.Lanes.DeleteLaneTests.Moving_the_steps_needs_another_live_lane_of_the_same_map` (part domaine) |
| A-2.3-11 | `Topogenia.Domain.Tests.Lanes.DeleteLaneTests.Moved_steps_keep_column_and_row_and_shift_only_the_contiguous_block_of_the_target` |
| A-2.3-12 | `Topogenia.Domain.Tests.Lanes.DeleteLaneTests.Deleting_the_steps_deletes_their_sub_maps_down_the_tree_and_their_flows_in_the_same_batch` |
| A-2.3-13 | `Topogenia.Domain.Tests.Lanes.DeleteLaneTests.A_lane_with_steps_deleted_without_a_choice_is_refused_with_its_number_of_steps` (part domaine) |
| A-2.3-14 | `Topogenia.Domain.Tests.Lanes.UpdateLaneTests.Renaming_a_lane_forgets_its_automatic_translations_and_keeps_its_code_and_manual_ones` ; `Topogenia.Domain.Tests.Lanes.UpdateLaneTests.Sending_the_same_name_keeps_the_automatic_translations` (spec 13) |
| A-2.3-15 | `Topogenia.Domain.Tests.Lanes.AddLaneTests.Manual_translations_of_the_name_are_written_at_creation_except_the_source_language` ; `Topogenia.Domain.Tests.Lanes.UpdateLaneTests.Manual_translations_are_written_by_the_translations_field_and_an_empty_value_deletes_one` ; `Topogenia.Domain.Tests.Lanes.AddLaneTests.An_unknown_translation_language_is_refused_before_anything_is_written` ; `Topogenia.Domain.Tests.Lanes.UpdateLaneTests.A_translation_too_long_is_refused` (part domaine ; la route relève de la phase 4) |
| A-2.3-16 | `Topogenia.Domain.Tests.Steps.InsertStepTests.Without_a_neighbour_lane_below_or_above_is_refused_without_creating_anything` ; `Topogenia.Domain.Tests.Steps.InsertStepTests.Below_or_above_puts_a_relay_in_the_neighbour_lane_and_cuts_nothing` |
| A-2.4-01 | `Topogenia.Domain.Tests.Steps.CreateStepTests.Every_step_type_of_the_closed_list_is_accepted_and_task_is_the_default` ; `Topogenia.Domain.Tests.Steps.CreateStepTests.An_unknown_step_type_is_refused` |
| A-2.4-02 | `Topogenia.Domain.Tests.Steps.UpdateStepTests.Automation_status_and_assignee_are_updated_and_the_assignee_can_be_removed` ; `Topogenia.Domain.Tests.Steps.UpdateStepTests.Unknown_values_and_an_assignee_outside_the_project_are_refused_without_any_write` |
| A-2.4-03 | `Topogenia.Domain.Tests.Steps.CreateStepTests.A_label_is_required_and_bounded_to_150_characters` |
| A-2.4-04 | `Topogenia.Domain.Tests.Steps.CreateStepTests.A_column_or_a_row_out_of_bounds_is_refused` ; `Topogenia.Domain.Tests.Steps.CreateStepTests.The_last_column_and_the_last_row_are_accepted` ; `Topogenia.Domain.Tests.Steps.UpdateStepTests.A_move_to_a_lane_of_another_map_or_out_of_bounds_is_refused` |
| A-2.4-05 | `Topogenia.Domain.Tests.Steps.CreateStepTests.A_column_or_a_row_out_of_bounds_is_refused` ; `Topogenia.Domain.Tests.Steps.CreateStepTests.The_last_column_and_the_last_row_are_accepted` ; `Topogenia.Domain.Tests.Layout.StepLayoutTests.A_lane_outside_the_map_or_a_cell_out_of_bounds_is_refused` |
| A-2.4-06 | `Topogenia.Domain.Tests.Layout.StepLayoutTests.A_final_state_with_two_steps_in_one_cell_is_refused_without_any_write` ; `Topogenia.Domain.Tests.Layout.StepLayoutTests.Two_steps_in_different_rows_of_a_column_are_not_a_duplicate_cell` ; `Topogenia.Domain.Tests.Steps.UpdateStepTests.Moving_within_a_lane_right_then_left_shifts_the_neighbours_without_blocking_the_step_itself` (part domaine ; l'index unique partiel relève de la Persistance) |
| A-2.4-08 | `Topogenia.Domain.Tests.Steps.CreateStepTests.Dropping_on_an_occupied_cell_shifts_only_the_contiguous_block_of_the_same_row` ; `Topogenia.Domain.Tests.Steps.UpdateStepTests.Moving_to_another_lane_shifts_the_occupant_of_the_cell` |
| A-2.4-09 | `Topogenia.Domain.Tests.Steps.CreateStepTests.Dropping_on_an_occupied_cell_shifts_only_the_contiguous_block_of_the_same_row` ; `Topogenia.Domain.Tests.Lanes.DeleteLaneTests.Moved_steps_keep_column_and_row_and_shift_only_the_contiguous_block_of_the_target` |
| A-2.4-11 | `Topogenia.Domain.Tests.Steps.UpdateStepTests.Changing_lane_without_a_row_brings_the_step_back_to_row_zero` |
| A-2.4-12 | `Topogenia.Domain.Tests.Steps.CreateStepTests.A_lane_of_another_map_or_an_unknown_lane_is_refused_rg_c02` ; `Topogenia.Domain.Tests.Steps.UpdateStepTests.A_move_to_a_lane_of_another_map_or_out_of_bounds_is_refused` (part domaine ; la clé étrangère composée relève de la Persistance) |
| A-2.4-13 | `Topogenia.Domain.Tests.Layout.StepLayoutTests.A_layout_places_steps_exactly_without_any_shift` ; `Topogenia.Domain.Tests.Layout.StepLayoutTests.A_layout_swaps_two_steps_and_reorders_the_lanes_in_one_call` |
| A-2.4-14 | `Topogenia.Domain.Tests.Layout.StepLayoutTests.A_final_state_with_two_steps_in_one_cell_is_refused_without_any_write` |
| A-2.4-15 | `Topogenia.Domain.Tests.Layout.StepLayoutTests.A_step_named_twice_in_a_layout_is_refused` ; `Topogenia.Domain.Tests.Layout.StepLayoutTests.A_step_of_another_map_is_refused_as_outside_the_map_with_its_readable_code` |
| A-2.4-16 | `Topogenia.Domain.Tests.Steps.InsertStepTests.Before_b_in_a_b_c_gives_a_x_b_c_and_cuts_the_single_incoming_flow_in_the_batch` |
| A-2.4-17 | `Topogenia.Domain.Tests.Steps.InsertStepTests.Before_b_in_a_b_c_gives_a_x_b_c_and_cuts_the_single_incoming_flow_in_the_batch` ; `Topogenia.Domain.Tests.Steps.InsertStepTests.The_label_of_a_cut_flow_stays_on_the_side_of_its_origin_before_and_after` |
| A-2.4-18 | `Topogenia.Domain.Tests.Steps.InsertStepTests.With_several_flows_on_the_side_none_is_cut_and_only_the_new_step_is_linked` ; `Topogenia.Domain.Tests.Steps.InsertStepTests.An_isolated_step_is_simply_linked_to_the_inserted_step` |
| A-2.4-19 | `Topogenia.Domain.Tests.Steps.InsertStepTests.After_b_in_a_b_c_gives_a_b_x_c_and_cuts_the_single_outgoing_flow` ; `Topogenia.Domain.Tests.Steps.InsertStepTests.The_label_of_the_single_outgoing_flow_stays_on_the_flow_that_leaves_b` |
| A-2.4-20 | `Topogenia.Domain.Tests.Steps.InsertStepTests.With_several_flows_on_the_side_none_is_cut_and_only_the_new_step_is_linked` ; `Topogenia.Domain.Tests.Steps.InsertStepTests.An_isolated_step_is_simply_linked_to_the_inserted_step` |
| A-2.4-21 | `Topogenia.Domain.Tests.Steps.InsertStepTests.Below_or_above_puts_a_relay_in_the_neighbour_lane_and_cuts_nothing` |
| A-2.4-22 | `Topogenia.Domain.Tests.Steps.InsertStepTests.Parallel_takes_the_same_lane_and_column_the_first_free_row_under_b_and_every_flow_of_b` |
| A-2.4-23 | `Topogenia.Domain.Tests.Steps.InsertStepTests.Parallel_takes_the_same_lane_and_column_the_first_free_row_under_b_and_every_flow_of_b` |
| A-2.4-24 | `Topogenia.Domain.Tests.Steps.InsertStepTests.Parallel_without_a_free_row_up_to_50_is_refused` |
| A-2.4-25 | `Topogenia.Domain.Tests.Steps.UpdateStepTests.Unknown_values_and_an_assignee_outside_the_project_are_refused_without_any_write` |
| A-2.4-26 | `Topogenia.Domain.Tests.Steps.UpdateStepTests.Automation_status_and_assignee_are_updated_and_the_assignee_can_be_removed` |
| A-2.4-27 | `Topogenia.Domain.Tests.Layout.ReadingPathsTests.Steps_are_read_left_to_right_by_column_then_top_to_bottom_by_lane_order` ; `Topogenia.Domain.Tests.Layout.ReadingPathsTests.Parallel_branches_of_a_column_are_read_top_to_bottom_before_the_next_column` (sur `ProjectWorkspace.ReadingOrder`, seule implémentation) |
| A-2.4-28 | `Topogenia.Domain.Tests.Layout.ReadingPathsTests.The_path_of_a_step_in_a_detail_prefixes_the_ranks_of_its_parent_steps` ; `Topogenia.Domain.Tests.Layout.ReadingPathsTests.A_hierarchical_path_is_written_with_dots` |
| A-2.5-01 | `Topogenia.Domain.Tests.Layout.LayoutWarningsTests.An_isolated_step_is_warned_only_from_two_steps_on_the_map` ; `Topogenia.Domain.Tests.Layout.LayoutWarningsTests.A_step_linked_by_any_flow_is_not_isolated` |
| A-2.5-02 | `Topogenia.Domain.Tests.Layout.LayoutWarningsTests.An_isolated_step_is_warned_only_from_two_steps_on_the_map` |
| A-2.5-03 | `Topogenia.Domain.Tests.Layout.LayoutWarningsTests.Autonomous_flows_side_by_side_are_not_warned_as_isolated` (fréquence de la fiche en cours, brouillon compris : AR-03) |
| A-2.5-04 | `Topogenia.Domain.Tests.Layout.LayoutWarningsTests.A_start_with_an_incoming_flow_and_an_end_with_an_outgoing_flow_are_warned` ; `Topogenia.Domain.Tests.Layout.LayoutWarningsTests.A_correct_flow_from_start_to_end_gives_no_warning` |
| A-2.5-05 | `Topogenia.Domain.Tests.Layout.LayoutWarningsTests.A_start_with_an_incoming_flow_and_an_end_with_an_outgoing_flow_are_warned` ; `Topogenia.Domain.Tests.Layout.LayoutWarningsTests.A_correct_flow_from_start_to_end_gives_no_warning` |
| A-2.5-06 | `Topogenia.Domain.Tests.Layout.LayoutWarningsTests.Warnings_never_block_an_operation` (part domaine) |
| A-2.7-01 | `Topogenia.Domain.Tests.Steps.CreateStepTests.Every_step_type_of_the_closed_list_is_accepted_and_task_is_the_default` (complète la ligne du dossier `Flows`) |
| A-2.7-07 | `Topogenia.Domain.Tests.Steps.InsertStepTests.Parallel_takes_the_same_lane_and_column_the_first_free_row_under_b_and_every_flow_of_b` ; `Topogenia.Domain.Tests.Steps.InsertStepTests.Parallel_without_a_free_row_up_to_50_is_refused` |
| A-2.7-08 | `Topogenia.Domain.Tests.Steps.InsertStepTests.The_label_of_a_cut_flow_stays_on_the_side_of_its_origin_before_and_after` ; `Topogenia.Domain.Tests.Steps.InsertStepTests.The_label_of_the_single_outgoing_flow_stays_on_the_flow_that_leaves_b` |

Règles annexes portées sans ligne `A-*` propre (annexe A 2.2) : le renommage d'une étape renomme sa carte détaillée
seulement si elle porte encore l'ancien nom (`Topogenia.Domain.Tests.Steps.UpdateStepTests.Renaming_a_step_renames_its_detailed_map_only_while_it_bears_the_old_label`) ;
le passage à un type sans contenu supprime d'office une sous-carte vide et refuse une sous-carte remplie avec
`ETAPE_NON_DETAILLABLE` (`Topogenia.Domain.Tests.Steps.UpdateStepTests.A_type_without_content_deletes_an_empty_sub_map`,
`Topogenia.Domain.Tests.Steps.UpdateStepTests.A_type_without_content_is_refused_for_a_step_whose_sub_map_is_filled`) ;
le déplacement groupé AR-44 (`Topogenia.Domain.Tests.Layout.StepLayoutTests.Dragging_several_steps_onto_free_cells_places_them_exactly`,
`Topogenia.Domain.Tests.Layout.StepLayoutTests.Dragging_onto_an_occupied_cell_moves_each_step_in_column_order_with_rg_c04`,
`Topogenia.Domain.Tests.Layout.StepLayoutTests.Dragged_steps_that_keep_their_cell_are_left_out_of_the_move`) ;
l'avertissement `PROFONDEUR_DEPASSEE` (`Topogenia.Domain.Tests.Layout.LayoutWarningsTests.Depth_beyond_the_recommended_depth_gives_the_depth_warning`).

## Lignes non couvertes

- **A-2.4-07** : stationnement en deux temps des cases (colonne 1 000 000 000 + i) : relève de la Persistance (ADR 016), phase 3. Le domaine pose les cases finales directement.
- **A-2.4-10** : couloir supprimé **entre-temps** pendant un placement : concurrence, relève de l'Application et de la Persistance (verrou de projet), phase 4. Le refus `COULOIR_SUPPRIME` du domaine sur un couloir supprimé de la carte est prouvé par `Topogenia.Domain.Tests.Steps.CreateStepTests.A_deleted_lane_of_the_map_is_refused_with_lane_deleted`.
- **A-2.4-29 à A-2.4-33, A-2.4-35** : contenu de la lecture de carte (GET) : relèvent de l'API, phase 4 (le domaine fournit `ReadingPaths.Paths` et `LayoutWarnings.Of`).
- **A-2.4-34** : version entière des objets : relève de la Persistance et de l'API, phase 3 (le noyau incrémente déjà `Version`).
- **A-2.4-36 à A-2.4-38** : repères documents et irritants, documents d'une flèche : hors de la part attribuée (lecture des documents et des irritants, dossiers des documents).
- **A-2.5-07** : renvoi de `PROFONDEUR_DEPASSEE` et `SOUS_CARTE_EXISTANTE` par l'opération : relève de l'API (phase 4) et de « Détailler » (autre dossier). Le calcul de `PROFONDEUR_DEPASSEE` est fourni par `LayoutWarnings.Depth` et testé (voir plus haut).
- **A-2.7-02 à A-2.7-06, A-2.7-09** : fiches, RACI, remontée, losanges et recousage : autres dossiers (`Flows`, fiches, RACI, `Selection`).

## Écarts et choix à signaler

- Libellé par défaut d'une nouvelle étape : la 1.x n'en a pas côté serveur (libellé obligatoire, `CHAMP_OBLIGATOIRE`) ; « Nouvelle étape » est posé par l'interface (`web/src/carte/magasin.ts`). Le domaine garde le libellé obligatoire.
- Création ou déplacement d'une étape vers un couloir **supprimé** de la carte : `COULOIR_SUPPRIME` (409), comme demandé ; la 1.x répondait `VALEUR_INVALIDE(couloir_id)` faute de verrou entre la lecture et l'écriture. Couloir inconnu ou d'une autre carte : `VALEUR_INVALIDE(lane_id)`. La disposition groupée garde `VALEUR_INVALIDE(lane_id)` pour tout couloir qui n'est pas un couloir vivant de la carte (A-1.3-11).
- Langues des traductions manuelles : `LANGUE_INVALIDE` hors de la liste reçue dans la demande (`Languages`), `fr` et `en` par défaut ; la liste configurée (AR-29) n'existe pas encore dans le noyau.
- Noms de champs des refus en anglais (`name`, `type`, `color`, `label`, `lane_id`, `actor_user_id`, `assignee_user_id`, `automation_status`, `position`, `steps`, `translations`) ; clé `nb_etapes` du refus `COULOIR_NON_VIDE` et clé `code` de `ETAPE_HORS_CARTE` reprises de la 1.x.
- Codes de position d'insertion en anglais (`before`, `after`, `below`, `above`, `parallel`) : la correspondance avec les codes 1.x (`avant`...) relève de l'API.
