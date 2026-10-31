<!-- r4-fidelite.md -->
<!-- Couverture des constats de fidelite du round 4 de la porte de la phase 2 (FID4) -->

# Round 4 de la porte de la phase 2 : constats de fidélité (FID4)

Chaque test ajouté ou renforcé échoue sur la mutation du constat (mutation appliquée puis fichier restauré à
l'identique, ou copie dans le bloc-notes). `docs/couverture.md` n'est pas modifié : les corrections à y reporter
sont listées dans les deux dernières sections.

| ID | Etat | Test 2.0 | Remarque |
|---|---|---|---|
| A-2.9-12 | couvert (FID4-B1 corrigé) | `Topogenia.Domain.Tests.Dissolutions.DissolveThirdMapRaciTests.A_role_on_a_lane_of_a_third_map_follows_the_single_lane_of_its_name_in_the_flow` ; `Topogenia.Domain.Tests.Dissolutions.DissolveThirdMapRaciTests.A_role_on_a_lane_of_a_third_map_follows_the_lane_of_the_flow_it_was_copied_from` ; `Topogenia.Domain.Tests.Dissolutions.DissolveThirdMapRaciTests.A_role_on_a_lane_of_a_third_map_stays_hidden_when_the_flow_has_two_lanes_of_its_name` ; `Topogenia.Domain.Tests.Dissolutions.DissolveThirdMapRaciTests.A_role_on_a_lane_of_a_third_map_never_goes_to_the_lane_of_its_step` ; `Topogenia.Domain.Tests.Dissolutions.DissolveThirdMapRaciTests.A_role_on_a_lane_of_the_detail_copied_from_the_lane_of_its_step_follows_the_single_lane_of_its_name` | Rôle d'un couloir d'une troisième carte : règle générale de `RaciRules.FollowToMap` (`GivenLanes` ne vaut que pour les couloirs du détail), comme `PoserBloc` 1.x. Code du round 4 remis en mutant : 3 échecs. Statut : conservé (AR-12, AR-75, AR-82). Espace de tête de `SameLaneName` : AR-82 (3), fixé par `DissolveRaciFollowTests.A_role_follows_the_single_lane_of_its_name_in_the_flow_trimmed_and_case_ignored` (mutant `TrimEnd` tué) |
| A-2.3-06 | couvert (FID4-M1) | `Topogenia.Domain.Tests.Lanes.R4LaneOrderFidelityTests.A_lane_added_into_an_order_with_gaps_leaves_every_lane_renumbered_from_zero` ; `Topogenia.Domain.Tests.Lanes.R4LaneOrderFidelityTests.A_lane_added_at_the_end_of_an_order_with_gaps_lands_before_the_lanes_above_the_count_and_is_renumbered` ; `Topogenia.Domain.Tests.Lanes.R4LaneOrderFidelityTests.A_lane_added_into_an_order_with_duplicates_is_renumbered_by_sort_order_then_creation_then_identifier` ; `Topogenia.Domain.Tests.Lanes.R4LaneOrderFidelityTests.Deleting_a_lane_from_an_order_with_gaps_renumbers_the_remaining_lanes_from_zero` ; `Topogenia.Domain.Tests.Lanes.R4LaneOrderFidelityTests.Deleting_a_lane_from_an_order_with_duplicates_renumbers_the_ties_by_creation_then_identifier` ; `Topogenia.Domain.Tests.Lanes.R4LaneOrderFidelityTests.Reordering_lanes_whose_order_has_gaps_gives_each_lane_its_position_from_zero` | Ordres avec trous et doublons ; retrait de `NormaliseLaneOrder` dans `Add` (8 échecs) et dans `Delete` (2 échecs), `ApplyOrder` sans 0..n-1 : tués |
| A-2.4-25 | couvert (FID4-M2) | `Topogenia.Domain.Tests.Steps.UpdateStepTests.Unknown_values_and_an_assignee_outside_the_project_are_refused_without_any_write` ; `Topogenia.Domain.Tests.Steps.StepUpdateOrderTests.Without_a_filled_sub_map_the_automation_comes_before_the_assignee_and_the_assignee_before_the_cell` ; `Topogenia.Domain.Tests.Steps.StepClosedListTests.An_automation_status_in_another_case_or_with_spaces_is_refused_on_automation_status` | Les tests comparent `assignee_user_id` et `automation_status` écrits en dur (plus `StepOperations.AssigneeField` ni `AutomationField`) ; changer la valeur de l'une ou l'autre constante : tué (2 et 5 échecs) |
| A-2.8-21 | couvert (FID4-M3) | `Topogenia.Domain.Tests.Selection.R4AnchorFidelityTests.After_the_anchor_the_flow_from_the_anchor_to_the_block_is_ignored_and_the_only_flow_outside_the_block_is_cut` ; `Topogenia.Domain.Tests.Selection.R4AnchorFidelityTests.Before_the_anchor_the_flow_from_the_block_to_the_anchor_is_ignored_and_the_only_flow_outside_the_block_is_cut` | Clause « hors bloc » : filtre de `AnchorInsertion.cs:55` neutralisé : tué (2 échecs) ; le filtre ne joue que pour un déplacement sans couper dans la même carte |
| A-6-16 | couvert (domaine) (FID4-M4) | `Topogenia.Domain.Tests.Selection.R4ParkingFidelityTests.Grouping_a_block_does_not_shift_the_step_that_touches_it` ; `Topogenia.Domain.Tests.Selection.R4ParkingFidelityTests.Cells_are_freed_from_left_to_right_whatever_the_order_of_the_request` ; `Topogenia.Domain.Tests.Selection.R4ParkingFidelityTests.Moving_a_step_onto_the_cell_before_it_shifts_only_the_step_there_and_not_the_steps_beyond_its_old_cell` ; `Topogenia.Domain.Tests.Structure.BlockParkingTests.A_block_cut_and_pasted_over_its_own_cells_never_pushes_a_step_outside_the_block` | Retrait de `Park` dans `GroupIntoSubMap` (l.42), de `Park` dans `BlockTransfer.PlaceBlock` (l.86) et de l'`OrderBy(Column)` (l.87, 1.x `Selection.cs:247`) : tués. « Disposer » (`StepLayout.Apply`) n'emploie pas de stationnement en 2.0 : il pose sans décalage après contrôle de l'état final (le stationnement 1.x n'y évitait qu'un conflit d'unicité en base, phase 3) |
| A-2.1-09 | couvert (FID4-M5) | `Topogenia.Domain.Tests.Projects.R4FirstLaneNameFidelityTests.The_first_lane_name_of_any_configured_language_is_the_name_of_its_language_file` ; `Topogenia.Domain.Tests.Projects.R4FirstLaneNameFidelityTests.A_language_without_a_first_lane_name_falls_back_to_the_configured_default_language_and_not_to_french` ; `Topogenia.Domain.Tests.Projects.R4FirstLaneNameFidelityTests.A_project_created_in_a_third_language_gets_the_first_lane_name_of_that_language` | « Acteur 1 »/« Actor 1 » en dur (L1, 5 échecs) et repli sur « fr » en dur (L2, 1 échec) : tués |
| A-6-09 | couvert (FID4-M6, aligné sur la 1.x) | `Topogenia.Domain.Tests.Raci.RaciLinkedLaneOfStepTests.A_role_whose_linked_lane_is_the_lane_of_its_step_follows_the_single_lane_of_its_name` ; `Topogenia.Domain.Tests.Raci.RaciLinkedLaneOfStepTests.A_role_whose_linked_lane_is_the_lane_of_its_step_stays_hidden_without_a_single_homonym` | Couloir lié = couloir de l'étape : la règle du nom s'applique (AR-82) ; ancien code (`null` sans essayer l'homonyme) remis en mutant : tué. Statut : conservé (AR-12, AR-75, AR-82) |
| A-2.8-32 | couvert (FID4-M6) | `Topogenia.Domain.Tests.Raci.RaciLinkedLaneOfStepTests.A_role_whose_linked_lane_is_the_lane_of_its_step_follows_the_single_lane_of_its_name` ; `Topogenia.Domain.Tests.Selection.BlockTransferTests.A_role_follows_the_only_lane_of_the_same_name_of_the_arrival_map` | Statut à corriger (voir corrections de forme) |
| A-2.9-18 | couvert | `Topogenia.Domain.Tests.Dissolutions.DissolveMutantGuardTests.The_shift_of_the_flow_stops_exactly_at_column_10000` ; `Topogenia.Domain.Tests.Dissolutions.DissolvePreconditionTests.A_shift_of_the_flow_beyond_column_10000_is_refused` | Preuve de la borne exacte citée |
| A-2.4-04 | couvert | `Topogenia.Domain.Tests.Steps.R4StepProofTests.A_shift_that_brings_a_step_exactly_to_the_last_column_is_accepted` | Décalage RG-C04 qui amène exactement en colonne 10 000 ; `>` changé en `>=` (`GridRules.cs:58`, le constat citait la ligne 150) : tué |
| A-2.7-02 | couvert | `Topogenia.Domain.Tests.Sheets.R4SheetProofTests.Writing_the_sheet_of_an_event_or_a_parallel_or_inclusive_gateway_is_refused_as_step_without_content` ; `Topogenia.Domain.Tests.Sheets.R4SheetProofTests.A_task_a_decision_or_a_wait_may_hold_a_sheet` | start, end, parallel, inclusive ; contrôle restreint à start : tué |
| A-2.7-09 | couvert | `Topogenia.Domain.Tests.Selection.R4SelectionProofTests.With_two_entering_flows_and_one_leaving_flow_each_stitch_takes_the_label_of_its_own_entering_flow` ; `Topogenia.Domain.Tests.Selection.R4SelectionProofTests.With_three_entering_flows_one_without_label_and_one_leaving_flow_no_stitch_borrows_another_label` | Libellé de SA flèche entrante (`Stitcher.cs:38`) |
| A-2.11-05 | couvert | `Topogenia.Domain.Tests.Values.R4ValueProofTests.A_drive_letter_followed_by_a_colon_and_a_separator_is_a_path` ; `Topogenia.Domain.Tests.Values.R4ValueProofTests.A_drive_letter_and_a_colon_followed_by_anything_but_a_separator_is_not_an_address` | Séparateur après « X: » (`DocumentLocation.cs:38`) ; contrôle retiré : tué |
| A-2.10-15 | couvert | `Topogenia.Domain.Tests.Sheets.R4SheetProofTests.Completeness_follows_the_1x_table_for_every_count_out_of_seven` ; `Topogenia.Domain.Tests.Sheets.SheetCompletenessTests.Completeness_is_rounded_over_seven_items` | Table 1.x pour chaque compte sur 7 ; 100 × n / 7 n'a jamais de demi exact : le mode d'arrondi (écart de zéro ou pair) n'est pas distinguable sur 7 éléments, la troncature l'est |
| A-2.17-36 | couvert | `Topogenia.Domain.Tests.Maps.R4MapProofTests.At_equality_the_order_of_the_file_decides_even_when_the_flows_are_listed_the_other_way` ; `Topogenia.Domain.Tests.Maps.R4MapProofTests.At_equality_the_order_of_the_file_decides_between_steps_made_ready_by_different_predecessors` | `SortedSet` remplacé par une file (`AutomaticColumns.cs:54,60`) : tué |
| A-1.5-06 | couvert (domaine) | `Topogenia.Domain.Tests.Dissolutions.R4DissolutionProofTests.Assigning_a_usage_without_step_id_is_refused_as_a_required_field_and_nothing_moves` | Usage sans `step_id` : `CHAMP_OBLIGATOIRE(step_id)`, l'usage reste sur son étape et dans le reliquat, rien n'est écrit. Le mutant R1 survit sans trou de test : `Remainder.Move` (`Remainder.cs:111`, `:115`) refait le même contrôle avec le même code, le contrôle de `HolderToAssign` est redondant (sans effet observable) |
| A-2.3-03 | couvert | `Topogenia.Domain.Tests.Values.ValueObjectTests.Each_colour_of_the_1x_palette_becomes_the_nearest_tint` ; `Topogenia.Domain.Tests.Lanes.AddLaneTests.The_default_colour_follows_the_number_of_lanes_of_the_map_over_the_eight_tints` | Preuve de la conversion des couleurs 1.x citée |
| A-1.4-17 | couvert (domaine) | `Topogenia.Domain.Tests.Steps.R4StepProofTests.An_update_with_a_blank_label_and_an_unknown_type_is_refused_on_the_label_first` | Ordre 1.x : libellé avant type (`ServiceStructure.cs:389-390`) ; mutant S03 : tué |
| A-3-19 | couvert (domaine) | `Topogenia.Domain.Tests.Projects.R4ProjectProofTests.An_invalid_code_and_a_blank_name_give_the_refusal_of_the_code` ; `Topogenia.Domain.Tests.Projects.R4ProjectProofTests.An_unknown_language_and_a_too_long_description_give_the_refusal_of_the_language` ; `Topogenia.Domain.Tests.Projects.R4ProjectProofTests.A_known_language_and_a_too_long_description_give_the_refusal_of_the_description` | Ordre 1.x de `Creer` (`ServiceProjets.cs:98-101`) : code avant nom, langue avant description ; mutants P2 et P3 : tués |
| A-2.2-17 | écart (AR-83) | `Topogenia.Domain.Tests.Steps.DetailedMapNameCaseTests.A_detailed_map_renamed_to_a_case_variant_of_the_label_keeps_its_name_when_the_step_is_renamed` ; `Topogenia.Domain.Tests.Steps.DetailedMapNameCaseTests.A_detailed_map_bearing_the_exact_label_follows_a_change_of_case_of_the_label` | `BearsText` reste ordinal (la 1.x dépendait de la collation de l'instance, A-5-05) ; mutant `OrdinalIgnoreCase` : tué |
| A-2.10-09 | couvert (aligné sur la 1.x) | `Topogenia.Domain.Tests.Sheets.SheetFieldCheckOrderTests.A_text_field_with_html_and_a_number_is_refused_for_its_html` ; `Topogenia.Domain.Tests.Sheets.SheetFieldCheckOrderTests.A_text_field_too_long_with_a_number_is_refused_for_its_length` ; `Topogenia.Domain.Tests.Sheets.SheetFieldCheckOrderTests.A_valid_text_with_a_number_is_refused_on_the_field` | `SheetContentReader.Text` contrôle le texte avant le nombre, comme `Fiches.Lire` (`Fiches.cs:109`) ; ancien ordre remis en mutant : tué (2 échecs). Le champ liste rend `VALEUR_INVALIDE(champ)` dans les deux ordres |
| A-2.14-22 | couvert (domaine) (AR-84) | `Topogenia.Domain.Tests.Translations.TranslationImportTests.One_invalid_translation_refuses_the_whole_file_with_its_line` | Le domaine rend `TranslationRowError(Line, Language, Code, Max)` ; `erreurs[{ligne, message}]` 1.x construit par l'Application (phase 4) |
| A-2.6-19 | couvert | `Topogenia.Domain.Tests.Values.R4ValueProofTests.A_typed_step_code_is_normalised_as_in_the_1x` ; `Topogenia.Domain.Tests.Flows.R4FlowProofTests.A_code_of_one_to_six_digits_after_the_letter_e_is_read` | T-UDOM-03 : cas « 17 » sans E -> null porté ; mutant C02 (E facultatif) : tué |
| A-2.6-20 | couvert | `Topogenia.Domain.Tests.Values.R4ValueProofTests.A_typed_step_code_takes_one_to_six_digits_after_the_letter_e` ; `Topogenia.Domain.Tests.Flows.R4FlowProofTests.A_code_without_the_letter_e_or_without_one_to_six_digits_is_an_unknown_step_of_its_line` | 6 chiffres acceptés, 7 et 0 refusés |

## Corrections de forme

À reporter dans `docs/couverture.md` :

- A-2.8-32 et A-2.8-33 : statut « conservé (AR-12, AR-75, AR-82) » au lieu de « conservé » (AR-12 place le couloir lié
  avant l'homonyme ; AR-82 garde la règle du nom quand le couloir lié est celui de l'étape).
- A-2.8-26 : niveau « Domaine, Application », phase « 2, 4 » (`TRANSFERT_HORS_PROJET` relève de l'Application, AR-71).
- A-2.8-35 : phase « 2, 4 » (inscription au journal par l'Application).
- A-4-42 : phase « 2, 4 » ; A-4-53 : niveau « Domaine, Persistance, API », phases « 2, 3, 4 » (AR-80).
- Convention appliquée partout : « Phase = toutes les phases des niveaux cités, par ordre croissant » (Architecture 1,
  Domaine 2, Persistance 3, Application / API / Sécurité 4, Échange 5 ; Revue sans phase), celle que suivent déjà
  67 lignes « Domaine, API » en « 2, 4 ». La phrase de convention de l'annexe A (« celle du premier niveau de test
  cité ») est à remplacer par celle-ci.

Lignes dont la phase ne suit pas cette convention (101, hors lignes ramenées ci-dessous) :

| ID | Niveau | Phase actuelle | Phase selon la convention |
|---|---|---|---|
| A-1.1-13 | Domaine, Application | 2 | 2, 4 |
| A-1.1-15 | Domaine, Application | 2 | 2, 4 |
| A-1.2-15 | API, Échange | 4 | 4, 5 |
| A-1.2-16 | API, Échange | 4 | 4, 5 |
| A-1.2-17 | API, Échange | 4 | 4, 5 |
| A-1.2-19 | API, Échange | 4 | 4, 5 |
| A-1.2-20 | API, Échange | 4 | 4, 5 |
| A-1.2-21 | API, Échange | 4 | 4, 5 |
| A-0-01 | Domaine, Sécurité | 2 | 2, 4 |
| A-0-02 | Échange, API | 5 | 4, 5 |
| A-2.1-19 | Application, Persistance | 4 | 3, 4 |
| A-2.1-20 | Domaine, Application | 2 | 2, 4 |
| A-2.1-22 | Application, Persistance | 4 | 3, 4 |
| A-2.2-02 | Domaine, Persistance | 2 | 2, 3 |
| A-2.2-04 | Domaine, Persistance | 2 | 2, 3 |
| A-2.2-18 | Domaine, Application | 2 | 2, 4 |
| A-2.2-27 | Domaine, Application | 2 | 2, 4 |
| A-2.2-28 | Domaine, Persistance | 2 | 2, 3 |
| A-2.4-06 | Domaine, Persistance | 2 | 2, 3 |
| A-2.4-10 | Application, Persistance | 4 | 3, 4 |
| A-2.4-12 | Domaine, Persistance | 2 | 2, 3 |
| A-2.4-14 | Domaine, Application | 2 | 2, 4 |
| A-2.4-34 | Persistance, API | 3 | 3, 4 |
| A-2.6-11 | Domaine, Persistance | 2 | 2, 3 |
| A-2.6-22 | Domaine, Application, Sécurité | 2 | 2, 4 |
| A-2.8-35 | Domaine, Application | 2 | 2, 4 |
| A-2.9-02 | Domaine, Persistance | 2 | 2, 3 |
| A-2.9-19 | Domaine, Application | 2 | 2, 4 |
| A-2.10-17 | Domaine, Persistance | 2 | 2, 3 |
| A-2.10-19 | Application, Persistance | 4 | 3, 4 |
| A-2.10-36 | Domaine, Application | 2 | 2, 4 |
| A-2.10-43 | Domaine, Application | 2 | 2, 4 |
| A-2.11-08 | Domaine, Persistance | 2 | 2, 3 |
| A-2.11-14 | Application, Persistance | 4 | 3, 4 |
| A-2.11-20 | Domaine, Persistance | 2 | 2, 3 |
| A-2.12-03 | Domaine, Persistance | 2 | 2, 3 |
| A-2.13-07 | Domaine, Persistance | 2 | 2, 3 |
| A-2.13-13 | Domaine, Échange | 2 | 2, 5 |
| A-2.14-16 | Application, Persistance | 4 | 3, 4 |
| A-2.14-22 | Domaine, Application | 2 | 2, 4 |
| A-2.15-02 | Domaine, Application | 2 | 2, 4 |
| A-2.15-05 | Domaine, Application | 2 | 2, 4 |
| A-2.15-11 | Domaine, Application | 2 | 2, 4 |
| A-2.15-18 | Application, Persistance | 4 | 3, 4 |
| A-2.15-21 | Application, Persistance | 4 | 3, 4 |
| A-2.15-22 | Application, Persistance | 4 | 3, 4 |
| A-2.16-11 | Persistance, API | 3 | 3, 4 |
| A-2.17-38 | Échange, API | 5 | 4, 5 |
| A-2.17-42 | Échange, API | 5 | 4, 5 |
| A-3-03 | Persistance, API | 3 | 3, 4 |
| A-3-47 | Échange, API | 5 | 4, 5 |
| A-3-48 | Échange, API | 5 | 4, 5 |
| A-3-72 | Échange, API | 5 | 4, 5 |
| A-3-73 | API, Architecture | 4 | 1, 4 |
| A-4-06 | Application, Architecture | 4 | 1, 4 |
| A-4-20 | Domaine, Sécurité | 2 | 2, 4 |
| A-4-24 | Échange, Sécurité | 5 | 4, 5 |
| A-4-40 | Persistance, Sécurité | 3 | 3, 4 |
| A-4-42 | Domaine, API, Sécurité | 2 | 2, 4 |
| A-4-45 | Domaine, Application | 2 | 2, 4 |
| A-4-46 | Application, Persistance | 4 | 3, 4 |
| A-4-47 | Application, Persistance | 4 | 3, 4 |
| A-4-53 | Domaine, API | 2, 3, 4 | 2, 4 |
| A-5-02 | Domaine, Persistance | 2 | 2, 3 |
| A-5-12 | Persistance, Architecture | 3 | 1, 3 |
| A-5-31 | Persistance, API | 3 | 3, 4 |
| A-6-05 | Domaine, Persistance | 2 | 2, 3 |
| A-6-06 | Domaine, Persistance | 2 | 2, 3 |
| A-6-07 | Domaine, Persistance | 2 | 2, 3 |
| A-6-14 | Domaine, Persistance | 2 | 2, 3 |
| A-6-16 | Domaine, Persistance | 2 | 2, 3 |
| A-6-17 | Domaine | 2, 5 | 2 |
| A-6-30 | Domaine, Persistance | 2 | 2, 3 |
| A-6-36 | Architecture, API | 1 | 1, 4 |
| A-6-42 | Persistance, API | 3 | 3, 4 |
| A-6-44 | Domaine, Persistance | 2 | 2, 3 |
| B-0-17 | API | 6 | 4 |
| B-6-32 | API | 5 | 4 |
| S-3.1-05 | Architecture | 3 | 1 |
| S-3.1-06 | Architecture | 3 | 1 |
| S-3.1-07 | Architecture | 5 | 1 |
| S-3.1-08 | Architecture | 4 | 1 |
| S-3.1-09 | Architecture | 2 | 1 |
| S-3.4-01 | Architecture | 4 | 1 |
| S-3.4-04 | Architecture | 4 | 1 |
| S-3.6-06 | API | 5 | 4 |
| S-3.6-07 | API | 5 | 4 |
| S-3.6-08 | API | 5 | 4 |
| S-4.1-09 | Persistance, API | 4 | 3, 4 |
| S-4.3-01 | Architecture | 3 | 1 |
| S-5.4-05 | Sécurité | 5 | 4 |
| S-5.4-06 | Sécurité | 5 | 4 |
| S-5.4-07 | Sécurité | 5 | 4 |
| S-5.4-08 | Sécurité | 5 | 4 |
| S-5.4-09 | Sécurité | 5 | 4 |
| S-5.4-10 | Sécurité | 5 | 4 |
| S-5.4-12 | Sécurité | 5 | 4 |
| S-7.3-06 | Sécurité | 5 | 4 |
| S-7.4-04 | Sécurité | 5 | 4 |
| S-7.4-05 | Sécurité | 5 | 4 |
| S-NF-09 | API | 7 | 4 |

## Lignes ramenées en phase 2

Depuis AR-79, `UndoReplay.Undo` et `UndoReplay.Redo` exécutent dans le domaine tout le contrôle de l'annulation
(objets touchés, inverse, réparation, invariants, lots restaurés). Ces lignes passent au niveau Domaine en premier,
comme A-2.15-05 à A-2.15-11 ; il ne reste à l'Application que le gestionnaire `undo` (journal, `If-Match`,
transaction, réponse). La justification de `Dissolutions.md` (lignes non couvertes) est corrigée en conséquence.

| ID | Niveau | Phase | Statut 2.0 | Test 2.0 | Remarque |
|---|---|---|---|---|---|
| A-2.9-47 | Domaine, Application, Persistance | 2, 3, 4 | remplacé (§4.6, AR-79) | `Topogenia.Domain.Tests.Dissolutions.DissolutionUndoTests.Undo_is_refused_when_a_step_of_the_frame_was_deleted_or_moved_to_another_map` | Contrôle des objets touchés par `UndoReplay.Undo` (`OPERATION_DEPASSEE`) ; l'aide du test ne contrôle plus rien elle-même : `Undo` sans `EnsureNotOutdated` est tué (2 échecs) |
| A-2.9-48 | Domaine, Application | 2, 4 | remplacé (§4.6, AR-79) | `Topogenia.Domain.Tests.Dissolutions.DissolutionUndoTests.Undo_is_refused_when_a_created_lane_holds_another_step_but_not_for_a_step_put_there_then_deleted` | Invariants après l'inverse : `CARTE_MODIFIEE` |
| A-2.9-49 | Domaine, Application | 2, 4 | remplacé (§4.6, AR-62, AR-79) | `Topogenia.Domain.Tests.Dissolutions.DissolutionUndoTests.Undo_is_refused_when_a_flow_drawn_since_links_the_frame_to_the_flow` ; `Topogenia.Domain.Tests.Dissolutions.DissolutionUndoTests.Undo_is_refused_when_a_flow_drawn_since_links_two_steps_of_the_frame` |  |
| A-2.9-50 | Domaine, Application | 2, 4 | remplacé (§4.6, AR-66, AR-79) | `Topogenia.Domain.Tests.Dissolutions.DissolutionUndoTests.Undo_is_refused_when_the_batch_of_the_dissolution_was_restored` | `OPERATION_DEPASSEE` vu par `UndoReplay.Undo` |
| A-2.9-51 | Domaine, Application, Persistance | 2, 3, 4 | remplacé (§4.6) | `Topogenia.Domain.Tests.Dissolutions.DissolutionUndoTests.The_lanes_and_flows_created_by_the_dissolution_go_to_a_new_batch_on_undo` ; `Topogenia.Domain.Tests.Dissolutions.DissolutionUndoTests.The_inverse_of_a_dissolution_puts_the_map_back_as_before_flow_positions_included` |  |
| A-2.9-52 | Domaine, Application | 2, 4 | remplacé (§4.6) | `Topogenia.Domain.Tests.Dissolutions.DissolutionUndoTests.The_inverse_of_a_dissolution_puts_the_map_back_as_before_flow_positions_included` |  |
| A-2.9-53 | Domaine, Application | 2, 4 | remplacé (§4.6) | `Topogenia.Domain.Tests.Dissolutions.DissolutionUndoTests.The_inverse_of_a_dissolution_puts_the_map_back_as_before_flow_positions_included` |  |
| A-2.9-54 | Domaine, Application | 2, 4 | remplacé (§4.6, AR-68) | `Topogenia.Domain.Tests.Dissolutions.DissolutionUndoTests.The_inverse_of_a_dissolution_puts_the_map_back_as_before_flow_positions_included` ; `Topogenia.Domain.Tests.Structure.UndoRestoreRulesTests.A_batch_given_back_whole_by_an_undo_is_marked_restored_by_the_author_of_the_undo` | Lot rendu entier marqué restauré par `UndoReplay.Undo` (AR-68) |
| A-2.9-55 | Domaine, Application | 2, 4 | remplacé (§4.6) | `Topogenia.Domain.Tests.Dissolutions.DissolutionUndoTests.The_lanes_and_flows_created_by_the_dissolution_go_to_a_new_batch_on_undo` |  |
| A-2.9-56 | Domaine, Persistance | 2, 3 | remplacé (§4.6) | `Topogenia.Domain.Tests.Dissolutions.DissolutionUndoTests.The_lanes_and_flows_created_by_the_dissolution_go_to_a_new_batch_on_undo` ; `Topogenia.Domain.Tests.Dissolutions.DissolutionUndoTests.The_inverse_of_a_dissolution_puts_the_map_back_as_before_flow_positions_included` | Part domaine : l'inverse retire les enregistrements de la remontée ; l'écriture SQL en phase 3 |
| A-2.9-57 | Domaine, Application | 2, 4 | corrigé (§4.6) | `Topogenia.Domain.Tests.Dissolutions.DissolutionUndoTests.The_inverse_of_a_dissolution_puts_the_map_back_as_before_flow_positions_included` ; `Topogenia.Domain.Tests.Dissolutions.DissolutionUndoTests.A_dissolution_whose_frame_was_closed_without_remainder_can_still_be_undone` | La transaction unique reste à l'Application |
| A-2.2-26 | Domaine, Application, API | 2, 4 | remplacé (AR-08, AR-43, §4.6) | `Topogenia.Domain.Tests.SubMaps.SubMapDeletionTests.Undoing_a_detail_is_refused_once_the_sub_map_holds_a_live_step` ; `Topogenia.Domain.Tests.SubMaps.SubMapDeletionTests.Undoing_a_detail_whose_sub_map_stayed_empty_is_accepted` | Inverse puis invariants par `UndoReplay.Undo` : `CARTE_MODIFIEE` |
| A-2.15-04 | Domaine, Application, API (par `undo`) | 2, 4 | remplacé (§4.5, AR-08, AR-43, AR-66) | `Topogenia.Domain.Tests.Dissolutions.DissolutionUndoTests.Undo_is_refused_when_the_batch_of_the_dissolution_was_restored` ; `Topogenia.Domain.Tests.Dissolutions.DissolutionUndoTests.Undo_is_refused_when_a_flow_drawn_since_links_two_steps_of_the_frame` | Comme A-2.15-05 à 11 |
| A-3-61 | Domaine, Application, API | 2, 4 | conservé (AR-04, AR-43, AR-79, §4.6) | `Topogenia.Domain.Tests.Structure.UndoConcurrencyTests.An_undo_whose_repair_would_push_a_step_beyond_the_last_column_is_refused_with_map_modified` ; `Topogenia.Domain.Tests.Structure.UndoConcurrencyTests.A_redo_whose_repair_would_push_a_step_beyond_the_last_column_is_refused_with_map_modified` ; `Topogenia.Domain.Tests.Dissolutions.DissolutionUndoTests.Undo_is_refused_when_a_flow_drawn_since_links_the_frame_to_the_flow` ; `Topogenia.Domain.Tests.SubMaps.SubMapDeletionTests.Undoing_a_detail_is_refused_once_the_sub_map_holds_a_live_step` ; `Topogenia.Domain.Tests.Structure.UndoRestoreRulesTests.Undoing_the_deletion_of_a_detail_whose_parent_map_was_deleted_since_is_refused_with_map_modified` | État « couvert (domaine) » au lieu de « à faire » : `CARTE_MODIFIEE` est produit par le domaine (tout refus du rejeu ou de la réparation le devient, mutant U09 du contrôleur tué) ; le message FR/EN reste en phase 4 |
