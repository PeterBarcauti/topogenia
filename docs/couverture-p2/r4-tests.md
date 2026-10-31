<!-- r4-tests.md -->
<!-- Couverture du round 4 de la porte de la phase 2 : tests qui tuent les mutations survivantes de l'intégrité des tests (INT4) -->

# Couverture phase 2, round 4 : tests qui tuent les survivantes (INT4)

Chaque test ci-dessous a été vérifié contre sa mutation sur le code actuel (après AR-81 à AR-84) : copie du domaine et de
ses tests dans le bac à sable, mutation appliquée, construction, exécution de toute la suite, restauration ; la mutation
fait échouer le test. Préfixe des tests : `Topogenia.Domain.Tests.`. À reporter dans `docs/couverture.md` : la colonne
« Test 2.0 » reçoit les tests ci-dessous EN PLUS des tests déjà cités (sauf mention « remplace »), la colonne « État »
prend la valeur indiquée.

## Survivantes du constat INT4

| ID | État | Test 2.0 | Remarque |
|---|---|---|---|
| S-4.6-05 | couvert | `Workspace.CommitChangeSetTests.A_record_created_again_by_a_later_operation_is_reported_as_created` ; `Workspace.CommitChangeSetTests.A_record_changed_and_touched_by_one_operation_is_one_change` | tue X-W02 (créations oubliées au `Commit`) et X-W03 (objet modifié et touché compté deux fois) |
| A-2.2-28 | couvert (domaine) | `Workspace.ParentStepWalkTests.The_walk_up_follows_a_deleted_parent_step` | tue X-W29 ; 1.x `Profondeur` (`ServiceCartes.cs:236-246`) joint l'étape parente sans filtre de suppression |
| A-2.9-03 | couvert | `Dissolutions.DissolveClosedFrameBelowTests.A_closed_frame_below_the_detail_does_not_refuse_the_dissolution` ; `Dissolutions.DissolveClosedFrameBelowTests.An_open_frame_below_the_detail_refuses_the_dissolution` | tue X-D08 (cadre fermé compté) |
| A-2.9-16 | couvert | `Dissolutions.DissolveShiftAndEndsTests.A_step_in_the_column_of_the_detailed_step_is_not_shifted` | tue X-D19 (`>=` au lieu de `>`) |
| A-2.9-20 | couvert | `Dissolutions.DissolveShiftAndEndsTests.The_ends_of_the_detail_in_one_column_are_taken_by_row` | tue X-D23 (extrémités d'une colonne par rangée décroissante) |
| A-2.9-25 | couvert | `Dissolutions.RemainderViewTests.A_remainder_of_one_element_is_a_remainder` ; `Dissolutions.RemainderViewTests.A_frame_without_document_usage_or_pain_point_has_no_remainder` ; `Dissolutions.RemainderViewTests.A_usage_names_its_live_producer_and_not_a_deleted_one` | tue X-D46 (`> 1`) et X-D45 (producteur supprimé nommé), sur une vraie remontée |
| A-2.7-09 | couvert | `Selection.StitchLabelOrderTests.The_stitch_takes_the_label_of_the_older_entering_flow` | tue X-E33 ; la flèche récente a le plus petit identifiant : l'ordre de création décide, pas l'identifiant |
| A-2.3-15 | couvert (domaine) | `Lanes.LaneLanguageTests.A_new_lane_takes_the_language_of_the_user_not_the_one_of_its_map` ; `Lanes.LaneLanguageTests.The_translations_of_a_lane_are_checked_against_its_own_language_not_the_one_of_the_user` | tue X-L03 et X-L04 |
| A-2.6-01 | couvert | `Flows.FlowRefusalOrderTests.A_loop_on_a_deleted_or_unknown_step_is_refused_as_reflexive` | tue X-F15 ; ordre 1.x `CreerLien` (`ServiceStructure.cs:615-619`) : `LIEN_REFLEXIF` avant `INTROUVABLE` |
| A-2.11-10 | couvert (domaine) | `Documents.DocumentOrderTests.Documents_are_in_creation_order_before_identifier_order` ; `Documents.DocumentOrderTests.Offered_inputs_are_in_the_column_order_of_their_producers` ; `Documents.DocumentOrderTests.Offered_inputs_of_lanes_with_one_sort_order_follow_the_creation_of_the_lanes` | tue X-O13, X-O03 et X-O01 (rang du couloir dans l'ordre unique, AR-76) |
| A-2.11-11 | couvert (domaine) | `Documents.DocumentOrderTests.Users_of_an_output_in_one_column_are_in_code_order` | tue X-O06 (utilisateurs d'une sortie par colonne puis code) |
| A-2.12-03 | couvert (domaine) | `Documents.PainPointHistoryOrderTests.The_history_of_a_pain_point_is_oldest_first_whatever_the_identifiers` | tue X-O21 |
| A-2.14-19 | couvert (domaine) | `Translations.ImportChangedTextCaseTests.An_original_that_differs_by_case_only_is_a_changed_text` | tue X-N13 ; 1.x `ServiceTraductions.cs:106` compare en C# (ordinal) : casse comptée, comme `BearsText` (AR-83) |
| A-2.2-17 | couvert (AR-83) | (inchangé) `Steps.DetailedMapNameCaseTests.A_detailed_map_renamed_to_a_case_variant_of_the_label_keeps_its_name_when_the_step_is_renamed` | X-N08 déjà tuée par ce test du round 4 |
| A-2.4-13 | couvert | `Layout.EmptyLaneOrderTests.An_empty_lane_order_is_ignored_and_the_steps_are_still_placed` | tue X-Y01 ; 1.x `ServiceStructure.cs:446` (`OrdreCouloirs is { Count: > 0 }`) |
| A-2.13-08 | couvert (AR-86) | `Raci.RaciRecordAuditTests.A_role_that_changes_keeps_its_creation_audit` | tue X-R01 ; écart à la 1.x (suppression puis réinsertion) arbitré par AR-86 |
| A-2.8-32 | couvert (AR-86) | `Raci.RaciRecordAuditTests.A_role_that_follows_its_step_is_a_new_record_at_version_one` | tue X-R11 (D06 : la clé change, version 1) |
| A-2.15-10 | couvert | `Structure.UndoRestoreRulesTests.Redoing_the_creation_of_a_flow_purged_since_its_undo_is_refused_and_creates_nothing` (remplace `Structure.UndoRestoreRulesTests.A_replayed_flow_absent_before_the_replay_stays_absent_when_it_would_double_a_live_flow`) | mineur INT4 : le rétablissement reçoit la vraie annulation ; la flèche purgée depuis bloque le contrôle (`OPERATION_DEPASSEE`) avant tout rejeu |

Déjà tuées par des tests du round 4 : X-S05 (`Structure.UndoStackTests.A_blocking_record_created_since_names_its_creator_and_its_creation_time`),
X-E03 (`Selection.R4AnchorFidelityTests`, deux tests), X-N08 (ci-dessus). Équivalente : X-W19 (`Versioning.Update` est
interne au domaine et ses trois appels, dans `UserRules`, gardent la clé ; la garde n'est atteignable par aucune API
publique). INT4-M0 : déjà corrigé (l'aide `Undo` de `DissolutionUndoTests` passe par `UndoReplay.Undo` seul).

## Couloir voisin de l'insertion dessous / dessus (ARC4-M1, AR-85)

| ID | État | Test 2.0 | Remarque |
|---|---|---|---|
| A-2.3-16 | couvert (AR-85) | `Steps.RelayNeighbourLaneTests.After_a_lane_is_deleted_the_relay_below_goes_to_the_next_live_lane` ; `Steps.RelayNeighbourLaneTests.With_a_gap_in_the_sort_orders_the_neighbour_is_the_next_lane_of_the_order` ; `Steps.RelayNeighbourLaneTests.Lanes_with_the_same_sort_order_are_neighbours_in_creation_order` ; `Steps.RelayNeighbourLaneTests.The_last_lane_has_no_relay_below_and_the_first_none_above` | voisin d'indice ± 1 dans `LanesOf` au lieu de `sort_order ± 1` ; même couloir sur un ordre normal ; les deux tests sur trou et égalité tuent l'ancien code (N-I01) |
| A-2.4-21 | couvert | `Steps.RelayNeighbourLaneTests.After_a_lane_is_deleted_the_relay_below_goes_to_the_next_live_lane` ; `Steps.RelayNeighbourLaneTests.With_a_gap_in_the_sort_orders_the_neighbour_is_the_next_lane_of_the_order` | colonne de B + 1, rangée 0 |

## Campagne neuve sur le code du round 4 (40 mutations)

| ID | État | Test 2.0 | Remarque |
|---|---|---|---|
| A-6-09 | couvert (AR-12, AR-75) | `Raci.RaciTwoLinkedLanesTests.Two_linked_lanes_give_way_to_the_single_lane_of_the_same_name` | tue N-R06 (deux couloirs liés : le premier pris) |
| A-2.9-09 | couvert | `Dissolutions.DissolveLaneOrderTests.A_lane_of_the_detail_goes_to_the_first_lane_of_its_name_in_the_flow` | tue N-D03 ; 1.x `memeNom[0]` (`Remontee.cs:108-112`) |
| A-2.9-10 | couvert | `Dissolutions.DissolveLaneOrderTests.Lanes_copied_from_the_detail_keep_its_order_at_the_bottom_of_the_flow` | tue N-D04 (copies au même ordre, départagées par l'identifiant) |
