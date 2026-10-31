<!-- SubMaps-Raci.md -->
<!-- Couverture de la phase 2 pour les dossiers SubMaps et Raci : cartes et sous-cartes, RACI (annexe A 2.2 et 2.13) -->

# Couverture phase 2 : dossiers `SubMaps` et `Raci`

Code : `src/Topogenia.Domain/SubMaps/` (`SubMapRules`, `MacroStepDeletion`) et `src/Topogenia.Domain/Raci/`
(`RaciRules`, `RaciRules.Follow`).
Tests : `tests/Topogenia.Domain.Tests/SubMaps/` et `tests/Topogenia.Domain.Tests/Raci/`.

## Lignes couvertes

| ID | Test 2.0 |
|----|----------|
| A-2.2-01 | `Topogenia.Domain.Tests.SubMaps.DetailTests.The_only_lane_of_a_new_sub_map_is_a_copy_of_the_lane_of_the_step` (une sous-carte naît avec un couloir ; le refus du dernier couloir relève de l'agent des couloirs) |
| A-2.2-02 | `Topogenia.Domain.Tests.SubMaps.ReattachTests.The_root_map_cannot_be_reattached_nor_moved_and_stays_unique` |
| A-2.2-03 | `Topogenia.Domain.Tests.SubMaps.SubMapDeletionTests.The_root_map_cannot_be_deleted` |
| A-2.2-04 | `Topogenia.Domain.Tests.SubMaps.DetailTests.Detailing_again_a_step_whose_sub_map_is_empty_reopens_the_same_map_without_any_change` ; `Topogenia.Domain.Tests.SubMaps.ReattachTests.The_empty_sub_map_of_the_target_is_replaced_and_goes_to_the_batch` |
| A-2.2-05 | `Topogenia.Domain.Tests.SubMaps.DetailTests.A_sub_map_stays_empty_until_it_holds_a_live_step` |
| A-2.2-06 | `Topogenia.Domain.Tests.SubMaps.DetailTests.Detailing_again_a_step_whose_sub_map_is_empty_reopens_the_same_map_without_any_change` |
| A-2.2-07 | `Topogenia.Domain.Tests.SubMaps.SubMapDeletionTests.A_step_whose_sub_map_is_empty_is_deleted_without_question_and_the_undo_gives_both_back` |
| A-2.2-09 | `Topogenia.Domain.Tests.SubMaps.ReattachTests.The_empty_sub_map_of_the_target_is_replaced_and_goes_to_the_batch` |
| A-2.2-12 | `Topogenia.Domain.Tests.SubMaps.DetailTests.Only_steps_with_content_can_be_detailed` |
| A-2.2-13 | `Topogenia.Domain.Tests.SubMaps.DetailTests.Detailing_a_step_whose_sub_map_holds_a_live_step_is_refused_as_already_detailed` |
| A-2.2-14 | `Topogenia.Domain.Tests.SubMaps.DetailTests.A_detailed_step_gets_a_sub_map_with_its_label_and_its_language` |
| A-2.2-15 | `Topogenia.Domain.Tests.SubMaps.DetailTests.The_only_lane_of_a_new_sub_map_is_a_copy_of_the_lane_of_the_step` |
| A-2.2-16 | `Topogenia.Domain.Tests.SubMaps.DetailTests.Detailing_beyond_four_levels_warns_without_refusing` ; `Topogenia.Domain.Tests.SubMaps.DetailTests.The_depth_warning_follows_the_recommended_depth_setting` |
| A-2.2-18 | `Topogenia.Domain.Tests.SubMaps.SubMapDeletionTests.Deleting_a_macro_step_with_delete_removes_every_level_below_in_the_same_batch` ; `Topogenia.Domain.Tests.SubMaps.SubMapDeletionTests.Undoing_the_deletion_of_a_macro_step_gives_back_its_descendants` |
| A-2.2-19 | `Topogenia.Domain.Tests.SubMaps.SubMapDeletionTests.Deleting_a_macro_step_with_attach_gives_its_sub_map_to_the_heir` ; `Topogenia.Domain.Tests.SubMaps.SubMapDeletionTests.Attaching_the_sub_map_of_a_deleted_macro_step_needs_an_heir` |
| A-2.2-20 | `Topogenia.Domain.Tests.SubMaps.SubMapDeletionTests.Deleting_a_macro_step_without_choice_is_refused_and_changes_nothing` |
| A-2.2-21 | `Topogenia.Domain.Tests.SubMaps.ReattachTests.The_target_step_must_be_live_and_of_the_same_project` ; `Topogenia.Domain.Tests.SubMaps.ReattachTests.A_sub_map_reattached_to_another_step_leaves_its_former_step` |
| A-2.2-22 | `Topogenia.Domain.Tests.SubMaps.ReattachTests.A_step_without_content_cannot_receive_a_sub_map` |
| A-2.2-23 | `Topogenia.Domain.Tests.SubMaps.ReattachTests.A_target_whose_sub_map_holds_a_live_step_or_that_already_holds_the_map_is_refused` |
| A-2.2-24 | `Topogenia.Domain.Tests.SubMaps.ReattachTests.A_sub_map_cannot_be_reattached_inside_its_own_subtree` |
| A-2.2-25 | `Topogenia.Domain.Tests.SubMaps.ReattachTests.The_root_map_cannot_be_reattached_nor_moved_and_stays_unique` |
| A-2.2-26 | `Topogenia.Domain.Tests.SubMaps.SubMapDeletionTests.Undoing_a_detail_is_refused_once_the_sub_map_holds_a_live_step` ; `Topogenia.Domain.Tests.SubMaps.SubMapDeletionTests.Undoing_a_detail_whose_sub_map_stayed_empty_is_accepted` (part domaine : inverse appliqué puis invariants, `CARTE_MODIFIEE` ; le gestionnaire `undo` reste en phase 4) |
| A-2.2-27 | `Topogenia.Domain.Tests.SubMaps.SubMapDeletionTests.Deleting_a_detail_deletes_the_sub_map_and_all_its_descendants_in_one_batch` ; `Topogenia.Domain.Tests.SubMaps.SubMapDeletionTests.Undoing_the_deletion_of_a_detail_gives_back_the_sub_map_with_its_content` |
| A-2.2-28 | `Topogenia.Domain.Tests.SubMaps.BreadcrumbTests.Depth_and_breadcrumb_stop_at_fifty_levels` ; `Topogenia.Domain.Tests.SubMaps.ParentWalkTests.Each_use_of_the_walk_up_keeps_its_own_bound` ; `Topogenia.Domain.Tests.SubMaps.ParentWalkTests.The_depth_the_breadcrumb_and_the_reading_path_are_read_from_the_same_parent_steps` (AR-67 ; part domaine ; la requête récursive de la persistance relève de la phase 3, A-5-21) |
| A-2.13-01 | `Topogenia.Domain.Tests.Raci.RaciVisibilityTests.The_r_is_the_lane_of_the_step_and_is_not_stored` |
| A-2.13-02 | `Topogenia.Domain.Tests.Raci.RaciRulesTests.Letters_come_from_the_setting_filtered_and_without_duplicates` ; `Topogenia.Domain.Tests.Raci.RaciRulesTests.Default_letters_are_aci_and_a_role_is_set_replaced_and_removed` |
| A-2.13-03 | `Topogenia.Domain.Tests.Raci.RaciRulesTests.A_letter_outside_the_setting_is_refused` ; `Topogenia.Domain.Tests.Raci.RaciRulesTests.The_letter_s_is_accepted_only_when_the_setting_offers_it` |
| A-2.13-04 | `Topogenia.Domain.Tests.Raci.RaciRulesTests.A_lane_of_another_map_a_deleted_lane_or_an_unknown_lane_is_refused` |
| A-2.13-05 | `Topogenia.Domain.Tests.Raci.RaciRulesTests.The_lane_that_performs_the_step_receives_no_other_role` |
| A-2.13-06 | `Topogenia.Domain.Tests.Raci.RaciRulesTests.Setting_a_on_another_lane_moves_the_single_approval` ; `Topogenia.Domain.Tests.Raci.RaciRulesTests.Undoing_a_role_change_gives_back_the_former_role` |
| A-2.13-07 | `Topogenia.Domain.Tests.Raci.RaciRulesTests.Setting_a_also_replaces_a_hidden_approval_left_on_a_lane_of_another_map` (part domaine ; l'index unique partiel relève de la phase 3) |
| A-2.13-08 | `Topogenia.Domain.Tests.Raci.RaciRulesTests.Default_letters_are_aci_and_a_role_is_set_replaced_and_removed` ; `Topogenia.Domain.Tests.Raci.RaciRulesTests.Setting_the_same_role_again_or_removing_a_missing_role_changes_nothing` |
| A-2.13-09 | `Topogenia.Domain.Tests.Raci.RaciRulesTests.Steps_without_content_take_no_raci` ; `Topogenia.Domain.Tests.Raci.RaciRulesTests.A_role_on_a_deleted_step_is_refused_as_not_found` |
| A-2.13-10 | `Topogenia.Domain.Tests.Raci.RaciVisibilityTests.A_role_on_the_lane_the_step_moved_into_is_hidden_by_the_r_and_comes_back_when_the_step_leaves` |
| A-2.13-11 | `Topogenia.Domain.Tests.Raci.RaciVisibilityTests.A_role_on_a_lane_of_another_map_is_hidden` ; `Topogenia.Domain.Tests.Raci.RaciFollowTests.Two_lanes_of_the_same_name_on_the_new_map_keep_the_role_hidden_on_its_old_lane` ; `Topogenia.Domain.Tests.Raci.RaciFollowTests.Two_lanes_of_the_same_name_on_the_old_map_keep_the_role_hidden_on_its_old_lane` ; `Topogenia.Domain.Tests.Raci.RaciFollowTests.A_role_follows_to_the_single_lane_of_the_same_name_on_the_new_map` ; `Topogenia.Domain.Tests.Raci.RaciFollowTests.A_lane_copied_from_the_old_lane_wins_over_a_lane_of_the_same_name` ; `Topogenia.Domain.Tests.Raci.RaciFollowTests.Steps_that_come_back_to_their_map_find_their_roles_again` ; `Topogenia.Domain.Tests.Raci.RaciFollowTests.A_role_does_not_follow_onto_the_lane_of_the_step_nor_onto_a_lane_that_already_holds_a_role` ; `Topogenia.Domain.Tests.Raci.RaciFollowTests.Roles_on_lanes_of_the_map_of_the_step_and_unknown_steps_are_left_alone` ; `Topogenia.Domain.Tests.Raci.RaciFollowTests.Lane_names_are_compared_trimmed_and_ignoring_case` |
| A-2.13-12 | `Topogenia.Domain.Tests.Raci.RaciVisibilityTests.A_role_on_a_deleted_lane_is_hidden_and_comes_back_when_the_lane_is_restored` |
| A-2.13-13 | `Topogenia.Domain.Tests.Raci.RaciVisibilityTests.The_map_view_gives_for_every_live_step_the_same_raci_as_the_step_computation` ; `Topogenia.Domain.Tests.Raci.RaciVisibilityTests.Roles_left_on_a_step_turned_into_a_step_without_content_are_hidden_and_come_back_with_the_type` ; `Topogenia.Domain.Tests.Raci.RaciVisibilityTests.A_deleted_step_shows_no_raci` ; `Topogenia.Domain.Tests.Raci.RaciVisibilityTests.Visible_roles_are_ordered_by_lane_order` (un seul calcul `RaciRules.IsVisible` / `Visible` / `VisibleOnMap` ; son usage par l'export et `bi.step_raci` relève des phases 3 et 5) |
| A-6-14 | mêmes tests que A-2.13-13 (part domaine) |

## Lignes de la part non couvertes ici

| ID | Raison |
|----|--------|
| A-2.2-08 | Changement de type d'une étape : écrit par l'agent des étapes (dossier `Steps`), qui appelle la suppression d'une sous-carte vide. |
| A-2.2-10 | Export `.pmap` sans sous-carte vide : relève de l'Échange, phase 5. |
| A-2.2-11 | Indicateur `sous_carte_vide=true` de la lecture de carte : relève de l'API, phase 4 (la donnée vient de `ProjectWorkspace.IsEmptyMap`). |
| A-2.2-17 | Renommer une étape renomme sa sous-carte si elle porte encore l'ancien nom : partie de la modification d'étape (agent des étapes). |
| A-1.3-02, A-1.3-16, A-1.4-18 à 20 | Routes HTTP : phase 4 (les règles qu'elles appellent sont couvertes ci-dessus). |
| A-5-21 | Requête récursive identique sur les deux bases : persistance, phase 3. |

## Tests 1.x portés (intention)

T-SCVI-01 à 04 (sauf la partie changement de type de T-SCVI-04, agent des étapes), T-SCAN-06 (copie du couloir, fil
d'Ariane ; les chemins 2.1, 2.2 relèvent de la vue de carte), T-SCAN-07 à 12, T-SCAN-14, T-SCAN-19, T-SCAN-20,
T-MODE-04 (partie RACI), T-MODE-06, T-MODE-09 (part RACI), T-MODE-10 (part RACI), T-MODE-11, T-RACI-01 à 07 (sauf les
droits de T-RACI-07, phase 4), T-WMAG-08 (réouverture sans changement).
