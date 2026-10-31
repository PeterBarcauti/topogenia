<!-- Dissolutions.md -->
<!-- Couverture de la phase 2 pour le dossier Dissolutions : remontée du détail, reliquat, cadre (annexe A 2.9) -->

# Couverture phase 2 : dossier `Dissolutions`

Code : `src/Topogenia.Domain/Dissolutions/` (`Dissolve`, `DissolveLayout`, `DissolveFlows`, `DissolutionFrequencies`,
`DissolutionGuards`, `Frames`, `Remainder`, `PainPointRemoval`).
Tests : `tests/Topogenia.Domain.Tests/Dissolutions/` (aide de construction `SalesFlow`, scénario des tests 1.x).

Préfixe des tests : `Topogenia.Domain.Tests.Dissolutions.`

## Lignes couvertes

| ID | Test 2.0 |
|----|----------|
| A-2.9-01 | `DissolvePreconditionTests.A_step_without_sub_map_has_no_detail_to_dissolve` ; `DissolvePreconditionTests.A_sub_map_with_only_its_start_and_end_has_no_detail_to_dissolve` |
| A-2.9-02 | `DissolvePreconditionTests.A_second_frame_on_the_same_map_is_refused_while_the_first_is_open` ; `DissolvePreconditionTests.A_frame_closed_on_the_map_does_not_prevent_a_new_dissolution` (l'index unique filtré relève de la persistance, phase 3 ; l'invariant `open-dissolution` du noyau le double en mémoire) |
| A-2.9-03 | `DissolvePreconditionTests.A_frame_open_in_the_detail_or_deeper_refuses_the_dissolution` |
| A-2.9-04 | `DissolvePreconditionTests.A_detail_of_another_frequency_than_the_parent_step_of_the_map_is_refused` |
| A-2.9-05 | `DissolvePreconditionTests.A_detail_of_another_frequency_than_an_autonomous_flow_of_the_map_is_refused_even_in_draft` (AR-03 : fiche en cours, brouillon compris) ; `DissolvePreconditionTests.The_autonomous_flows_of_a_map_are_its_detailed_steps_that_declare_a_frequency` ; `DissolvePreconditionTests.Same_frequency_no_declared_frequency_or_an_empty_detail_beside_is_accepted` |
| A-2.9-06 | `DissolvePreconditionTests.Same_frequency_no_declared_frequency_or_an_empty_detail_beside_is_accepted` |
| A-2.9-07 | `DissolveTests.Start_end_their_flows_the_detailed_step_and_the_sub_map_go_into_the_batch_of_the_operation` |
| A-2.9-08 | `DissolveLaneTests.A_lane_goes_to_the_lane_it_is_a_copy_of_while_alive_even_when_another_has_its_name` |
| A-2.9-09 | `DissolveLaneTests.Without_a_live_source_lane_a_lane_goes_to_the_lane_of_the_same_name_trimmed_and_case_ignored` ; `DissolveTests.A_step_in_the_column_of_the_detailed_step_is_pushed_only_where_the_detail_arrives` |
| A-2.9-10 | `DissolveLaneTests.A_lane_missing_from_the_flow_is_copied_at_the_bottom_with_its_translations_and_noted_in_the_frame` ; `DissolveTests.The_frame_records_its_steps_with_their_original_cell_and_the_objects_it_created` |
| A-2.9-11 | `DissolveLaneTests.A_second_lane_of_the_same_name_joins_the_first_copy_and_its_steps_are_stacked_below` |
| A-2.9-12 | `DissolveLaneTests.A_lane_that_holds_only_a_raci_role_follows_and_the_role_moves_with_it` |
| A-2.9-13 | `DissolveTests.Start_end_their_flows_the_detailed_step_and_the_sub_map_go_into_the_batch_of_the_operation` ; `DissolveTests.The_detail_steps_take_the_place_of_the_detailed_step_and_the_flow_is_shifted` |
| A-2.9-14 | `DissolveTests.The_detail_steps_take_the_place_of_the_detailed_step_and_the_flow_is_shifted` ; `DissolveLaneTests.A_second_lane_of_the_same_name_joins_the_first_copy_and_its_steps_are_stacked_below` |
| A-2.9-15 | `DissolveLaneTests.The_rows_start_at_the_row_of_the_detailed_step_in_its_lane_and_at_zero_elsewhere` ; `DissolveLaneTests.A_second_lane_of_the_same_name_joins_the_first_copy_and_its_steps_are_stacked_below` |
| A-2.9-16 | `DissolveTests.The_detail_steps_take_the_place_of_the_detailed_step_and_the_flow_is_shifted` ; `DissolveLaneTests.Successors_that_are_far_enough_do_not_move` |
| A-2.9-17 | `DissolveLaneTests.Without_successor_the_flow_moves_by_the_smallest_step_that_frees_the_cells_of_the_detail` ; `DissolveTests.A_step_in_the_column_of_the_detailed_step_is_pushed_only_where_the_detail_arrives` |
| A-2.9-18 | `DissolvePreconditionTests.A_detail_that_would_go_beyond_column_10000_is_refused` ; `DissolvePreconditionTests.A_shift_of_the_flow_beyond_column_10000_is_refused` |
| A-2.9-19 | `DissolveOriginalFlowTests.The_flows_cut_by_grouping_come_back_exactly_through_the_origin_operation_of_the_step` ; `DissolveOriginalFlowTests.Without_the_batch_of_the_origin_operation_the_flows_go_to_the_steps_without_predecessor_or_successor` ; `DissolveOriginalFlowTests.A_step_created_by_another_operation_uses_the_general_rule_even_if_flows_were_deleted_then` (part domaine ; l'inscription des flèches coupées dans l'opération relève de `GroupIntoSubMap` et de l'Application) |
| A-2.9-20 | `DissolveTests.The_flows_of_the_step_are_carried_to_the_entries_and_from_the_exits_of_the_detail` ; `DissolveOriginalFlowTests.Without_the_batch_of_the_origin_operation_the_flows_go_to_the_steps_without_predecessor_or_successor` ; `DissolveOriginalFlowTests.When_every_step_of_the_detail_has_a_predecessor_the_first_and_last_steps_are_the_ends` |
| A-2.9-21 | `DissolveTests.The_translations_of_the_label_of_a_carried_flow_are_copied` |
| A-2.9-22 | `DissolveTests.Start_end_their_flows_the_detailed_step_and_the_sub_map_go_into_the_batch_of_the_operation` |
| A-2.9-23 | `DissolveTests.The_frame_records_its_steps_with_their_original_cell_and_the_objects_it_created` (part domaine : les enregistrements sont créés ; leur écriture relève de la persistance, phase 3) |
| A-2.9-24 | `DissolveTests.The_frame_stays_open_without_remainder_until_finish` |
| A-2.9-25 | `RemainderAssignTests.The_remainder_holds_the_documents_usages_pain_points_and_the_sheet_of_the_dissolved_step` (la fiche est exposée par son identifiant seulement ; ce qu'un lecteur en voit relève de `SheetVisibility`) |
| A-2.9-26 | `RemainderAssignTests.A_target_outside_the_map_or_without_content_is_refused` |
| A-2.9-27 | `RemainderAssignTests.A_target_outside_the_map_or_without_content_is_refused` |
| A-2.9-28 | `RemainderAssignTests.A_pain_point_can_go_to_the_map_itself` |
| A-2.9-29 | `RemainderAssignTests.A_document_does_not_go_to_a_step_that_uses_it` |
| A-2.9-30 | `RemainderAssignTests.A_document_goes_to_its_new_producer_and_the_frame_closes_when_the_remainder_is_empty` ; `RemainderFinishTests.An_output_stored_on_a_step_that_used_it_loses_that_use` (avec contrôle, une cible qui utilise le document est refusée : l'usage de la cible n'est supprimé que dans les rangements sans contrôle, comme en 1.x) |
| A-2.9-31 | `RemainderAssignTests.A_usage_does_not_go_to_a_step_that_already_uses_or_produces_the_document` |
| A-2.9-32 | `RemainderAssignTests.Bringing_back_a_usage_returns_only_the_moved_one` ; `RemainderAssignTests.A_document_brought_back_to_the_remainder_leaves_its_producer` |
| A-2.9-33 | `RemainderAssignTests.Bringing_back_an_element_reopens_a_closed_frame_unless_another_frame_is_open` |
| A-2.9-34 | `RemainderAssignTests.An_element_is_brought_back_only_from_where_it_is` ; `RemainderAssignTests.A_pain_point_of_another_project_is_not_found` ; `RemainderAssignTests.An_unknown_kind_or_a_missing_target_is_refused` |
| A-2.9-35 | `RemainderAssignTests.A_document_goes_to_its_new_producer_and_the_frame_closes_when_the_remainder_is_empty` ; `RemainderRemoveTests.Removing_a_document_attaches_its_pain_points_beyond_reported_to_the_map_and_the_frame_closes` |
| A-2.9-36 | `RemainderRemoveTests.Removing_a_document_takes_all_its_uses_and_erases_their_reported_pain_points` ; `RemainderRemoveTests.Removing_a_usage_leaves_the_document_and_its_other_uses` |
| A-2.9-37 | `RemainderRemoveTests.Removing_a_document_takes_all_its_uses_and_erases_their_reported_pain_points` ; `RemainderRemoveTests.Removing_a_document_attaches_its_pain_points_beyond_reported_to_the_map_and_the_frame_closes` |
| A-2.9-38 | `RemainderRemoveTests.Removing_a_reported_pain_point_erases_it_with_its_history` ; `RemainderRemoveTests.Removing_a_pain_point_beyond_reported_attaches_it_to_the_map` ; `RemainderRemoveTests.The_pain_point_removal_rule_reports_whether_it_erased_or_kept` (AR-02 : règle unique `PainPointRemoval.Remove`) ; `RemainderRemoveTests.Only_an_element_of_the_remainder_of_an_open_frame_is_removed` |
| A-2.9-39 | `RemainderFinishTests.Outputs_go_to_the_last_step_of_the_frame_inputs_to_the_first_and_pain_points_to_the_map` ; `RemainderFinishTests.Finish_stores_everything_on_the_single_step_left_closes_the_frame_and_does_nothing_twice` |
| A-2.9-40 | `RemainderFinishTests.Outputs_go_to_the_last_step_of_the_frame_inputs_to_the_first_and_pain_points_to_the_map` |
| A-2.9-41 | `RemainderFinishTests.A_usage_goes_to_the_first_step_unless_it_would_be_a_duplicate` |
| A-2.9-42 | `RemainderFinishTests.Finish_stores_everything_on_the_single_step_left_closes_the_frame_and_does_nothing_twice` ; `RemainderFinishTests.Outputs_go_to_the_last_step_of_the_frame_inputs_to_the_first_and_pain_points_to_the_map` |
| A-2.9-43 | `RemainderFinishTests.A_frame_without_step_stores_on_the_first_step_of_the_map` |
| A-2.9-44 | `RemainderFinishTests.On_an_empty_map_the_documents_are_deleted_and_the_usages_dropped` |
| A-2.9-45 | `RemainderFinishTests.Finish_stores_everything_on_the_single_step_left_closes_the_frame_and_does_nothing_twice` (part domaine : la liste `Arrangement` est renvoyée ; la réponse HTTP relève de l'API, phase 4) |
| A-2.9-46 | `RemainderFinishTests.Finish_stores_everything_on_the_single_step_left_closes_the_frame_and_does_nothing_twice` |
| A-2.9-47 | `DissolutionUndoTests.Undo_is_refused_when_a_step_of_the_frame_was_deleted_or_moved_to_another_map` (détecté par `UndoReplay.Undo` : `OPERATION_DEPASSEE`, AR-04, AR-79) |
| A-2.9-48 | `DissolutionUndoTests.Undo_is_refused_when_a_created_lane_holds_another_step_but_not_for_a_step_put_there_then_deleted` (détecté par `StructureInvariants` : `CARTE_MODIFIEE`) |
| A-2.9-49 | `DissolutionUndoTests.Undo_is_refused_when_a_flow_drawn_since_links_the_frame_to_the_flow` (`StructureInvariants` : `CARTE_MODIFIEE`) ; `DissolutionUndoTests.Undo_is_refused_when_a_flow_drawn_since_links_two_steps_of_the_frame` (plus strict que la 1.x, voir plus bas) |
| A-2.9-50 | `DissolutionUndoTests.Undo_is_refused_when_the_batch_of_the_dissolution_was_restored` (`UndoReplay.Undo` : `OPERATION_DEPASSEE`, AR-04, AR-79) |
| A-2.9-51 | `DissolutionUndoTests.The_lanes_and_flows_created_by_the_dissolution_go_to_a_new_batch_on_undo` ; `DissolutionUndoTests.The_inverse_of_a_dissolution_puts_the_map_back_as_before_flow_positions_included` (traductions copiées effacées) |
| A-2.9-52 | `DissolutionUndoTests.The_inverse_of_a_dissolution_puts_the_map_back_as_before_flow_positions_included` |
| A-2.9-53 | `DissolutionUndoTests.The_inverse_of_a_dissolution_puts_the_map_back_as_before_flow_positions_included` |
| A-2.9-54 | `DissolutionUndoTests.The_inverse_of_a_dissolution_puts_the_map_back_as_before_flow_positions_included` |
| A-2.9-55 | `DissolutionUndoTests.The_lanes_and_flows_created_by_the_dissolution_go_to_a_new_batch_on_undo` |
| A-2.9-56 | `DissolutionUndoTests.The_lanes_and_flows_created_by_the_dissolution_go_to_a_new_batch_on_undo` ; `DissolutionUndoTests.The_inverse_of_a_dissolution_puts_the_map_back_as_before_flow_positions_included` |
| A-2.9-57 | `DissolutionUndoTests.The_inverse_of_a_dissolution_puts_the_map_back_as_before_flow_positions_included` ; `DissolutionUndoTests.A_dissolution_whose_frame_was_closed_without_remainder_can_still_be_undone` |
| A-2.9-58 | `DissolutionGuardTests.The_batch_of_a_dissolution_is_protected_from_the_purge_while_its_frame_is_open` (part domaine : `DissolutionGuards.IsBatchProtected` ; la purge elle-même relève de l'Application, phase 4) |

## Lignes non couvertes

Aucune ligne de la section A-2.9 n'est laissée sans test au niveau du domaine. Restent hors de la phase 2 :

- A-2.9-02 : l'index unique filtré (persistance, phase 3).
- A-2.9-23 : l'écriture des lignes `dissolution*` (persistance, phase 3).
- A-2.9-45 : la réponse HTTP de « Terminer » (API, phase 4).
- A-2.9-47 à A-2.9-57 : depuis AR-79, les règles de l'annulation sont exécutées par le domaine :
  `UndoReplay.Undo` contrôle lui-même les objets touchés (`OPERATION_DEPASSEE`), applique l'inverse (interne),
  répare et vérifie les invariants (`CARTE_MODIFIEE`) ; les tests de `DissolutionUndoTests` l'appellent seul
  (round 4 : l'aide ne fait plus de contrôle propre). Ces lignes relèvent donc de la phase 2 (niveau Domaine en
  premier, voir `r4-fidelite.md`) ; il ne reste à l'Application (phase 4) que le gestionnaire `undo` lui-même
  (journal, `If-Match`, transaction, réponse HTTP).
- A-2.9-58 : le travail de purge (Application, phase 4) ; il doit appeler `DissolutionGuards.IsBatchProtected`.

## Écarts et choix à connaître

- **Annulation, codes de refus** : les cas « étape du cadre supprimée ou changée de carte » et « lot déjà restauré »
  sont vus par le contrôle des objets touchés de `UndoReplay.Undo` (`OPERATION_DEPASSEE`, AR-04, AR-79) et non `CARTE_MODIFIEE` comme en 1.x ; les cas
  « couloir créé occupé » et « flèche qui relie le cadre au flux » sont vus par `StructureInvariants`
  (`CARTE_MODIFIEE`). Aucun cas ne passe à travers le noyau : pas de `DissolutionUndoGuard`.
- **Plus strict que la 1.x** : une flèche tracée après la remontée entre deux étapes du cadre bloque l'annulation
  (`CARTE_MODIFIEE`) ; la 1.x la ramenait dans le détail. L'inverse générique ne connaît pas cette flèche.
- **Annulation, flèches et lot** : les flèches créées partent dans un lot neuf (suppression logique) au lieu d'être
  effacées physiquement ; le lot de la remontée, dont tous les objets reviennent par l'inverse, est marqué restauré
  par l'auteur et à l'heure de l'annulation (AR-68). C'est le comportement de `UndoReplay.Undo`.
- **RACI** (AR-75) : un rôle suit le couloir source (`SourceLaneId`) même sous un autre nom ; par nom ou par copie, il ne
  suit que si le flux a un seul couloir de ce nom et le détail au plus un, comme `PoserBloc` 1.x ; il reste sur son
  couloir, masqué, sinon, ou si le couloir d'arrivée est celui de l'étape ou porte déjà un rôle de l'étape. Quand le
  couloir source est celui de l'étape, la règle du nom s'applique encore ; un rôle posé sur un couloir d'une
  troisième carte suit la règle générale de `PoserBloc` (round 4, AR-82, `DissolveThirdMapRaciTests`).
- **Ordre du repli « première ou dernière étape »** : ordre de lecture du détail (la requête 1.x n'avait pas d'ordre).
- **`ETAPE_HORS_CARTE`** : sans le détail `code = ""` de la 1.x.
