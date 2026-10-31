<!-- r4-rejeu.md -->
<!-- Couverture de la porte 2, round 4 : annulation de la pile entière par le contenu, clés du modèle, Versioning.Update -->

# Couverture phase 2, porte round 4 : pile d'annulation, état attendu par le contenu

Code : `src/Topogenia.Domain/Workspace/` (`ChangeSet`, `ProjectWorkspace`, `ProjectWorkspace.Queries`,
`Versioning`), `src/Topogenia.Domain/Structure/UndoReplay.cs`, `src/Topogenia.Domain/Model/` (`KeyOf` de
chaque enregistrement, `ICreationAudited`). Tests : `tests/Topogenia.Domain.Tests/Structure/`
(`UndoStackTests`, `StepTouchTests`), `Workspace/ChangeSetTouchedTests`, et
`tests/Topogenia.Architecture.Tests/` (`DomainModelRulesTests`, `SemanticSamplesTests`).

Préfixe des tests : `Topogenia.Domain.Tests.` Chaque test ajouté ou transformé échoue sur le défaut du
constat : la mutation qui rétablit le défaut, passée sur une copie du projet dans le dossier de travail,
est citée en remarque.

| ID | Etat | Test 2.0 | Remarque |
|----|------|----------|----------|
| S-4.6-04 | part domaine couverte (AR-79, AR-81) ; écriture du journal en phase 4 | `Workspace.ChangeSetTouchedTests.A_removed_record_is_touched_as_absent_and_a_code_counter_is_not_touched` ; `Workspace.ChangeSetTouchedTests.A_record_without_version_is_touched_with_its_content_and_outdated_once_it_changed` ; `Workspace.ChangeSetTouchedTests.A_record_created_removed_and_created_again_in_one_operation_is_one_creation` ; `Workspace.ChangeSetTouchedTests.Only_the_closing_or_the_reopening_of_the_frame_created_by_the_operation_is_left_out_of_the_check` | AR-81 : l'état attendu (`ExpectedState`) se compare par le contenu hors version et audit ; `Absent` est lu par `FirstOutdated` ; `CreatedByOperation` restreint l'exemption du cadre à la remontée qui l'a créé (ARC4-mineur 3). `Version` reste donnée pour le journal (`touched`). Mutations tuées : toute `Dissolution` exemptée (1 test), aucune exemption (2 tests, dont `Dissolutions.DissolutionUndoTests.A_dissolution_whose_frame_was_closed_without_remainder_can_still_be_undone`) |
| S-4.6-05 | couvert (domaine) | `Workspace.ChangeSetTouchedTests.A_record_created_removed_and_created_again_in_one_operation_is_one_creation` | ARC4-M4 : l'ordre réel de `Commit` est écrit dans `ChangeSet.cs`, `architecture.md` 2.2 et ADR 015 : créations, modifications de contenu, suppressions physiques, puis enregistrements seulement touchés (`Touch`) |
| S-4.6-08 | part domaine couverte (AR-79, AR-81) ; pile de 50, transaction et AR-05 en phase 4 | `Structure.UndoStackTests.Two_renamings_of_a_step_are_undone_one_after_the_other_and_redone_in_turn` ; `Structure.UndoStackTests.A_creation_and_the_renaming_that_follows_are_undone_and_redone_in_turn` ; `Structure.UndoStackTests.A_creation_and_the_raci_role_set_on_the_new_step_are_undone_and_redone_in_turn` ; `Structure.UndoStackTests.A_change_of_content_by_another_user_between_two_undos_still_outdates_the_older_operation` ; `Structure.UndoStackTests.A_content_put_back_since_as_the_operation_left_it_does_not_outdate_its_undo` ; `Structure.UndoStackTests.A_blocking_record_created_since_names_its_creator_and_its_creation_time` ; `Structure.StepTouchTests.Setting_a_raci_role_moves_the_version_of_the_step_and_only_a_change_of_its_content_outdates_the_undo_of_its_renaming` ; `Structure.StepTouchTests.Detailing_a_step_moves_its_version_and_only_a_change_of_its_content_outdates_the_undo_of_its_renaming` ; et les tests de `Structure.UndoConcurrencyTests` cités au round 3, inchangés et verts | ARC4-B1, AR-81 : la pile entière s'annule puis se rétablit (trois scénarios du contrôleur), une écriture concurrente qui change le contenu reste refusée avec son auteur et son heure (de création pour un enregistrement créé depuis, ARC4-mineur 1). Les deux tests de `StepTouchTests` reposaient sur un refus par la seule version (le `Touch`) : renommés, leur attendu est remplacé (AR-81 cité) par l'annulation acceptée, la version qui avance toujours, et un refus après un changement de contenu. Mutations tuées : version comparée de nouveau (6 tests), contrôle retiré (4 tests), audit de mise à jour seul (1 test) |
| A-2.15-04 | couvert (domaine), inchangé | `Dissolutions.DissolutionUndoTests.Undo_is_refused_when_the_batch_of_the_dissolution_was_restored` ; `Dissolutions.DissolutionUndoTests.Undo_is_refused_when_a_flow_drawn_since_links_two_steps_of_the_frame` | Preuve inchangée et verte avec AR-81 ; l'exemption du cadre ne vaut plus que pour la remontée qui l'a créé (voir S-4.6-04) |

Lignes `A-2.15-09` à `A-2.15-11` : tests inchangés et verts avec le contrôle par le contenu ; leur preuve ne
change pas.

## Hors couverture (architecture)

| Constat | Test | Mutation tuée |
|---|---|---|
| ARC4-M3 | `Topogenia.Architecture.Tests.DomainModelRulesTests.Record_keys_are_built_only_by_the_records_of_the_model` ; `Topogenia.Architecture.Tests.SemanticSamplesTests.A_record_key_built_outside_the_model_in_any_way_is_detected` (6 contournements) ; `Topogenia.Architecture.Tests.SemanticSamplesTests.A_key_given_by_its_record_is_not_a_key_built_by_hand` | `RecordKey.Of("use", ...)` remis dans `DocumentUses` ; `new("step", ...)` dans `ProjectWorkspace.Queries` |
| ARC4-mineur 4 | `Topogenia.Architecture.Tests.DomainModelRulesTests.Only_the_rules_of_the_users_stamp_a_record_outside_a_workspace` ; `Topogenia.Architecture.Tests.SemanticSamplesTests.A_versioning_update_called_or_taken_as_a_method_group_is_detected` (3 formes) | `Versioning.Update` appelé depuis `RaciRules` ; `Versioning.Update` rendu public |
