<!-- r3-rejeu.md -->
<!-- Couverture de la porte 2, round 3 : annulation sans écrasement, protocole regroupé, version de la fiche, codes lisibles -->

# Couverture phase 2, porte round 3 : annulation, versions, codes lisibles

Code : `src/Topogenia.Domain/Workspace/` (`ChangeSet`, `ProjectWorkspace`, `ProjectWorkspace.Replay`,
`ProjectWorkspace.Counters`), `src/Topogenia.Domain/Structure/UndoReplay.cs`,
`src/Topogenia.Domain/Sheets/SheetWorkflow.cs`, `src/Topogenia.Domain/Values/ReadableCode.cs`.
Tests : `tests/Topogenia.Domain.Tests/Structure/` (`UndoConcurrencyTests`, `StepTouchTests`,
`BlockParkingTests`, aide `Replays`), `Workspace/` (`ChangeSetTouchedTests`, `StructureInvariantsTests`),
`Sheets/SheetVersionTests`, `Values/ReadableCodeDigitsTests`.

Préfixe des tests : `Topogenia.Domain.Tests.` Chaque test ajouté échoue sur le défaut du constat :
la mutation qui rétablit le défaut, passée sur une copie, est citée en remarque.

| ID | Etat | Test 2.0 | Remarque |
|----|------|----------|----------|
| S-4.6-04 | part domaine couverte (AR-79) ; écriture du journal en phase 4 | `Workspace.ChangeSetTouchedTests.A_removed_record_is_touched_as_absent_and_a_code_counter_is_not_touched` ; `Workspace.ChangeSetTouchedTests.A_record_without_version_is_touched_with_its_content_and_outdated_once_it_changed` ; `Workspace.ChangeSetTouchedTests.A_record_created_removed_and_created_again_in_one_operation_is_one_creation` | ARC3-B1, ARC3-mineur 1 : `ChangeSet.Touched` porte l'état attendu (`ExpectedState`) de chaque objet créé, modifié ou supprimé physiquement (absent), sans les compteurs (D07) ; une clé créée, retirée puis recréée est une seule création. Mutations tuées : ancien `Touched` (versionnés seuls, 7 tests), compteur inclus, création en double |
| S-4.6-05 | couvert (domaine) | `Workspace.ChangeSetTouchedTests.A_record_created_removed_and_created_again_in_one_operation_is_one_creation` | ARC3-mineur 6 : l'ordre réel (créations, modifications, suppressions physiques) est écrit dans `ChangeSet.cs` ; ADR 015 : trois sortes de changements, la suppression logique est une modification |
| S-4.6-08 | part domaine couverte (AR-79) ; pile, transaction et AR-05 en phase 4 | `Structure.UndoConcurrencyTests.Undoing_a_translation_removal_is_outdated_once_another_user_wrote_the_translation_again` ; `Structure.UndoConcurrencyTests.Redoing_a_translation_creation_is_outdated_once_another_user_wrote_the_translation_since_the_undo` ; `Structure.UndoConcurrencyTests.Undoing_a_renaming_is_outdated_once_an_import_translated_the_new_label` ; `Structure.UndoConcurrencyTests.Redoing_a_renaming_with_its_translation_is_outdated_once_an_import_translated_the_lane_since_the_undo` ; `Structure.UndoConcurrencyTests.Undoing_a_creation_is_outdated_when_its_key_was_created_again_since_even_at_the_same_version` ; `Structure.UndoConcurrencyTests.Undoing_is_outdated_when_a_removed_record_without_version_came_back_or_one_left_by_the_operation_changed` ; `Structure.UndoConcurrencyTests.The_undo_checks_the_versions_itself_and_names_the_author_and_the_time_of_the_blocking_change` ; `Structure.UndoConcurrencyTests.The_redo_checks_the_versions_left_by_the_undo_itself_and_names_the_blocking_change` | ARC3-B1 et ARC3-mineur 7 : sondes `ControlProbes` (traduction) et cas renommage + import, par undo et par redo ; `UndoReplay.Undo` et `Redo` contrôlent eux-mêmes, `ApplyInverse` et `ApplyForward` sont internes. Mutations tuées : clé supprimée non contrôlée (5 tests), contenu d'une clé recréée, enregistrement sans version, contrôle retiré de `Undo` (7 tests), contrôle retiré de `Redo` (3 tests) |
| S-4.6-08 (étape touchée) | couvert (domaine) | `Structure.StepTouchTests.Setting_a_raci_role_moves_the_version_of_the_step_and_outdates_the_undo_of_its_renaming` ; `Structure.StepTouchTests.Detailing_a_step_moves_its_version_and_outdates_the_undo_of_its_renaming` | INT3-M9 : mutants A08 (`Touch` retiré de `RaciRules.SetRole`) et B05 (`Touch` retiré de `SubMapRules.Detail`) tués |
| A-2.15-05 à A-2.15-08 | couvert (domaine), par le vrai protocole | `SubMaps.SubMapDeletionTests` (5 annulations, dont `Undoing_a_detail_is_refused_once_the_sub_map_holds_a_live_step`) ; `Raci.RaciRulesTests.Undoing_a_role_change_gives_back_the_former_role` ; `Raci.RaciVisibilityTests.A_role_on_a_deleted_lane_is_hidden_and_comes_back_when_the_lane_is_restored` | INT3 mineur : ces annulations passent par `Replays.Undo` ou `Replays.Operation` (contrôle dans `UndoReplay.Undo`, puis `Commit`), plus par un appel nu sans contrôle ni validation |
| A-2.15-09 | couvert (domaine, AR-79) | `Structure.UndoConcurrencyTests.An_undo_whose_repair_would_push_a_step_beyond_the_last_column_is_refused_with_map_modified` ; `Structure.UndoConcurrencyTests.A_redo_whose_repair_would_push_a_step_beyond_the_last_column_is_refused_with_map_modified` | ARC3-mineur 7 et FID3 mineur A-2.15-09 : dans une annulation AR-43 prime sur AR-73, une réparation au-delà de la colonne 10 000 donne `CARTE_MODIFIEE` (plus `VALEUR_INVALIDE(column)`). Mutation tuée : refus non traduit (2 tests) |
| A-2.15-11 | couvert (domaine, AR-68 et AR-78) | `Structure.UndoConcurrencyTests.Redoing_a_step_creation_marks_the_batch_of_its_undo_restored_by_the_author_of_the_redo` ; `Structure.UndoConcurrencyTests.Redoing_a_deletion_dates_its_batch_and_its_deleted_records_with_the_redo` ; `Structure.UndoRestoreRulesTests.Redoing_a_deletion_after_its_undo_deletes_again_in_its_batch_no_longer_restored` | ARC3-M6 : `Redo` reçoit un contexte et marque restauré le lot de l'annulation vidé par le rétablissement. ARC3-mineur 3, corrigé : le rétablissement date et signe de nouveau le lot de l'opération et ses objets, la rétention part du rétablissement (la 1.x renvoyait la commande). Mutations tuées : marquage retiré, lot non redaté, objets non redatés ; la justification de `DeletionBatch` est corrigée (`DomainModelRulesTests`, architecture.md 2.1) |
| A-1.4-04 (part domaine : version de la fiche) | part domaine couverte ; `If-Match` en phase 4 | `Sheets.SheetVersionTests.Changing_only_the_procedure_gives_the_sheet_its_next_version` ; `Sheets.SheetVersionTests.Saving_the_same_procedure_again_keeps_the_version_of_the_sheet` | ARC3-B2 : `SheetWorkflow.Save` touche la fiche ; la sonde `SheetVersionProbe` (version 1 puis 1) donne 1 puis 2. Mutation tuée : `Touch` retiré. Justification d'exemption de `SheetOperation` corrigée (`DomainModelRulesTests`, architecture.md 2.1) |
| A-4-53 | part domaine couverte (AR-80) ; `CHECK` en phase 3, aucune réponse 500 en phase 4 | `Values.ReadableCodeDigitsTests.A_step_code_typed_with_digits_other_than_ascii_is_not_a_step_code` ; `Values.ReadableCodeDigitsTests.A_readable_code_with_digits_other_than_ascii_is_refused_on_its_field` ; `Values.ReadableCodeDigitsTests.The_command_bar_reports_a_code_with_other_digits_as_an_unknown_step_of_its_line` | FID3-M4 : chiffres ASCII seulement (`[0-9]`, `int.TryParse`) ; un chiffre arabe-indien ou pleine largeur levait `FormatException` (500). Mutation tuée : `\d` et `int.Parse` rétablis (5 cas). Rattachement des phases : AR-80 |
| A-6-16 | couvert (domaine) ; part persistance en phase 3 (ADR 016) | `Structure.BlockParkingTests.A_block_cut_and_pasted_over_its_own_cells_never_pushes_a_step_outside_the_block` | FID3 mineur : le niveau « Domaine, Persistance » est exact, le domaine stationne aussi (ADR 016, Conséquences). Le seul stationnement en mémoire est `SelectionChecks.Park` (regroupement et transfert) ; celui de l'unité de travail traite l'unicité en base (phase 3). Corrige la ligne A-6-16 de `r2-rejeu.md` (« n'existe qu'en persistance »). Mutation tuée : `Park` sans effet |
| W22 (une étape par case) | couvert (domaine) | `Workspace.StructureInvariantsTests.Two_live_steps_on_one_cell_are_a_violation_but_a_deleted_step_on_it_is_not` | INT3-M9 : mutant W22 (`Count() > 2`) tué |

## Documentation corrigée (ARC3-mineur 6)

- `ChangeSet.cs` : ordre réel des changements ; ADR 015 : trois sortes de changements et
  protocole regroupé ; `OperationLogRecords.cs` : le contrôle et le rejeu sont dans le domaine
  (`UndoReplay`), la pile et l'écriture du journal dans l'Application.
- `architecture.md` : RG-C05 dans `FlowLinks` (2.1), justifications de `DeletionBatch` et
  `SheetOperation` (2.1), état attendu et ordre du `ChangeSet` (2.2), protocole regroupé,
  codes et rétablissement (2.3), `Maps/` (`MapMarkers` lit l'espace, 2.5), rôle
  d'`ICounterService` et exigence de chargement de tous les compteurs (3) ; `NextCode` documente
  la même exigence (un compteur oublié serait recréé à 1 et refusé par sa clé à l'écriture).
- `decisions/README.md` : « AR-01 et suivants ».

## Reste

- Phase 4 : gestionnaires `undo` et `redo` (pile, AR-05, transaction, verrou) et sérialisation
  de `Touched` (colonne `touched` du journal) avec l'état attendu.
- Phase 3 : clé (projet, genre) du compteur, qui rend l'exigence de chargement sûre.
