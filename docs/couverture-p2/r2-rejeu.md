<!-- r2-rejeu.md -->
<!-- Couverture de la porte 2, round 2 : annulation réparée, invariants de structure, versions et lignes reclassées (annexe A 2.15) -->

# Couverture phase 2, porte round 2 : annulation, invariants, versions

Code : `src/Topogenia.Domain/Structure/UndoReplay.cs`, `src/Topogenia.Domain/Workspace/`
(`FlowLinks`, `StructureInvariants`, `Versioning`, `ProjectWorkspace.Replay`),
`src/Topogenia.Domain/Projects/` (`UserRules`, `MemberRules`, `ProjectCreation`),
`src/Topogenia.Domain/SubMaps/SubMapRules.cs` (fil d'Ariane).
Tests : `tests/Topogenia.Domain.Tests/Structure/` (aide `Replays`), `Workspace/`, `Projects/`, `SubMaps/`,
`tests/Topogenia.Architecture.Tests/DomainModelRulesTests.cs`.

Préfixe des tests du domaine : `Topogenia.Domain.Tests.` ; des tests d'architecture : `Topogenia.Architecture.Tests.`

Les lignes A-2.15-05 à A-2.15-11 étaient classées en phase 4 dans `couverture.md` sans arbitrage
(constat FID-B2) : ce sont des règles du domaine, appliquées par `UndoReplay` sur l'espace de projet ;
elles sont ramenées en phase 2 et couvertes par des tests qui passent par `UndoReplay.Undo` ou
`UndoReplay.Redo` après le contrôle des versions, comme le fera l'Application. La part Application
(gestionnaire `undo` / `redo`, transaction, pile, AR-05) reste en phase 4.

| ID | État | Test 2.0 |
|----|------|----------|
| A-2.15-04 | couvert (domaine, AR-66) ; part Application en phase 4 | `Dissolutions.DissolutionUndoTests.Undo_is_refused_when_the_batch_of_the_dissolution_was_restored` (`OPERATION_DEPASSEE`, le contrôle des versions passe avant les invariants) ; `Dissolutions.DissolutionUndoTests.Undo_is_refused_when_a_flow_drawn_since_links_two_steps_of_the_frame` (`CARTE_MODIFIEE` quand les versions sont intactes) |
| A-2.15-05 | couvert (domaine), reclassé phase 2 | `Structure.UndoRestoreRulesTests.Undoing_the_deletion_of_a_detail_whose_parent_map_was_deleted_since_is_refused_with_map_modified` ; `Structure.UndoReplayTests.Undoing_the_deletion_of_a_detail_after_its_parent_step_was_deleted_is_refused_with_map_modified` |
| A-2.15-06 | couvert (domaine), reclassé phase 2 | `Structure.UndoRestoreRulesTests.Undoing_the_deletion_of_a_detail_after_its_parent_step_got_another_detail_is_refused_with_map_modified` |
| A-2.15-07 | couvert (domaine), reclassé phase 2 | `Structure.UndoRestoreRulesTests.Undoing_the_deletion_of_a_step_of_a_detail_deleted_since_is_refused_with_map_modified` ; `Structure.UndoRestoreRulesTests.Undoing_the_deletion_of_a_lane_of_a_detail_deleted_since_is_refused_with_map_modified` ; `Workspace.StructureInvariantsTests.A_live_step_on_a_deleted_map_is_a_violation_of_its_map` |
| A-2.15-08 | couvert (domaine), reclassé phase 2 | `Structure.UndoRestoreRulesTests.Undoing_the_deletion_of_a_step_whose_lane_was_deleted_since_is_refused_with_map_modified` ; `Workspace.StructureInvariantsTests.A_live_step_in_a_live_lane_of_another_map_is_a_violation` |
| A-2.15-09 | couvert (domaine), reclassé phase 2 | `Structure.UndoRestoreRulesTests.Undoing_a_lane_deletion_after_a_lane_was_added_numbers_the_lanes_again_from_zero` ; `Structure.UndoRestoreRulesTests.Undoing_a_step_deletion_shifts_the_intruder_and_the_block_that_follows_it` ; `Structure.UndoRestoreRulesTests.A_lane_deleted_with_its_steps_comes_back_whole_at_its_rank` ; `Structure.UndoRestoreRulesTests.Redoing_a_step_creation_when_its_cell_was_taken_since_shifts_the_intruder` ; `Structure.UndoReplayTests.A_step_put_meanwhile_on_the_original_cell_is_shifted_when_the_deletion_is_undone` |
| A-2.15-10 | couvert (domaine), reclassé phase 2 | `Structure.UndoRestoreRulesTests.A_flow_whose_end_moved_to_another_map_since_stays_deleted_by_the_undo` ; `Structure.UndoRestoreRulesTests.A_reverse_flow_drawn_since_does_not_keep_the_deleted_flow_from_coming_back` ; `Structure.UndoRestoreRulesTests.A_flow_drawn_again_since_with_another_label_keeps_its_label_and_is_not_doubled` ; `Structure.UndoRestoreRulesTests.Redoing_a_flow_creation_when_the_flow_was_drawn_again_since_does_not_double_it` ; `Structure.UndoRestoreRulesTests.A_replayed_flow_absent_before_the_replay_stays_absent_when_it_would_double_a_live_flow` ; `Structure.UndoReplayTests.A_flow_drawn_again_meanwhile_is_not_doubled_by_the_undo` ; `Structure.UndoReplayTests.A_flow_whose_end_was_deleted_meanwhile_stays_deleted_by_the_undo` |
| A-2.15-11 | couvert (domaine, AR-68), reclassé phase 2 | `Structure.UndoRestoreRulesTests.A_batch_given_back_whole_by_an_undo_is_marked_restored_by_the_author_of_the_undo` ; `Structure.UndoRestoreRulesTests.A_batch_given_back_in_part_stays_restorable` ; `Structure.UndoRestoreRulesTests.Redoing_a_deletion_after_its_undo_deletes_again_in_its_batch_no_longer_restored` |
| A-2.2-28 | couvert (domaine, AR-67 : 51 cartes au plus comme la 1.x) | `SubMaps.BreadcrumbTests.Depth_and_breadcrumb_stop_at_fifty_levels` ; `SubMaps.ParentWalkTests.Each_use_of_the_walk_up_keeps_its_own_bound` |
| A-2.9-02 | couvert (domaine, invariant) | `Workspace.StructureInvariantsTests.Two_open_frames_on_one_map_are_a_violation_but_an_open_and_a_closed_one_are_not` (en plus des tests de `Dissolutions.md`) |
| A-2.6-04 | couvert (domaine, invariant) | `Workspace.StructureInvariantsTests.Two_live_flows_in_the_same_direction_are_a_violation_but_not_the_reverse_one_nor_a_deleted_one` (en plus des tests de `Flows.md`) |
| A-2.6-08 | couvert (domaine, `FlowLinks`) | tests de `Flows.md` inchangés, la règle passe par `FlowLinks` ; `Architecture.Tests.DomainModelRulesTests.The_folders_of_the_domain_depend_on_each_other_without_cycle` |
| A-2.13-06 | couvert (domaine, invariant un seul A) | `Workspace.StructureInvariantsTests.Two_approvers_on_one_step_are_a_violation_but_one_approver_on_each_of_two_steps_is_not` |
| A-4-44 | part domaine couverte (AR-07 : le profil est versionné) ; `If-Match` en phase 4 | `Projects.UserRulesTests.Every_change_of_a_user_moves_its_version_on_with_the_audit_of_the_operation` ; `Architecture.Tests.DomainModelRulesTests.Every_record_of_the_model_is_versioned_unless_listed_with_its_reason` |
| A-2.1-14 | couvert (domaine, AR-70) | `Projects.MemberRulesTests.A_member_who_stays_keeps_when_and_by_whom_he_joined_and_a_newcomer_gets_the_replacement` |
| A-4-53 | phase 3 et 4, déplacement justifié | Les contraintes `CHECK` sont posées par les fournisseurs (ADR 025, phase 3) ; aucune violation ne doit remonter en 500 parce que le domaine contrôle avant : part domaine prouvée par `Workspace.StructureInvariantsTests` (chaque invariant isolément) et par les refus des opérations ; la traduction d'une erreur du moteur en code (`CONFLIT_INTEGRITE`) relève de la persistance (phase 3) et de l'hôte (phase 4) |
| A-6-16 | phase 3, déplacement justifié | Le stationnement en deux temps est une étape de l'unité de travail (ADR 016) : il n'existe qu'en persistance, phase 3. Côté domaine, le décalage RG-C04 a une seule implémentation (`GridRules.FreeCell`, utilisée aussi par la réparation de l'annulation) |
