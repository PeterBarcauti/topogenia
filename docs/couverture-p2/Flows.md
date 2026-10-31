<!-- Flows.md -->
<!-- Couverture de la phase 2 pour le dossier Flows : flèches, barre de commande, losanges (annexe A 2.6 et 2.7) -->

# Couverture phase 2 : dossier `Flows`

Code : `src/Topogenia.Domain/Flows/` (`FlowRules`, `CommandLineParser`, `CommandBar`).
Tests : `tests/Topogenia.Domain.Tests/Flows/` (aide de construction `FlowMap`).

## Lignes couvertes

| ID | Test 2.0 |
|----|----------|
| A-2.6-01 | `Topogenia.Domain.Tests.Flows.FlowRulesTests.A_reflexive_flow_is_refused_as_reflexive` |
| A-2.6-02 | `Topogenia.Domain.Tests.Flows.FlowRulesTests.A_live_step_of_another_map_is_refused_as_outside_the_map` ; `Topogenia.Domain.Tests.Flows.FlowRulesTests.Two_steps_of_another_map_than_the_requested_one_are_outside_the_map` ; `Topogenia.Domain.Tests.Flows.FlowRulesTests.A_deleted_step_of_another_map_is_not_found_and_not_outside_the_map` ; `Topogenia.Domain.Tests.Flows.FlowRulesTests.An_absent_step_is_not_found_and_not_outside_the_map` |
| A-2.6-03 | `Topogenia.Domain.Tests.Flows.FlowRulesTests.An_absent_step_is_not_found_and_not_outside_the_map` ; `Topogenia.Domain.Tests.Flows.FlowRulesTests.A_deleted_map_is_not_found` (part domaine seulement, voir plus bas) |
| A-2.6-04 | `Topogenia.Domain.Tests.Flows.FlowRulesTests.A_live_duplicate_in_the_same_direction_is_a_conflict` ; `Topogenia.Domain.Tests.Flows.FlowRulesTests.A_deleted_flow_does_not_prevent_drawing_it_again` |
| A-2.6-06 | `Topogenia.Domain.Tests.Flows.FlowRulesTests.The_reverse_direction_and_a_loop_a_b_a_are_accepted` ; `Topogenia.Domain.Tests.Flows.CommandBarTests.A_chain_back_to_its_start_draws_a_loop` |
| A-2.6-07 | `Topogenia.Domain.Tests.Flows.FlowRulesTests.A_label_of_60_characters_is_accepted_and_61_is_too_long` ; `Topogenia.Domain.Tests.Flows.FlowRulesTests.A_blank_label_gives_a_flow_without_label` ; `Topogenia.Domain.Tests.Flows.FlowRulesTests.Relabelling_a_deleted_flow_is_not_found_and_a_long_label_is_refused` |
| A-2.6-08 | `Topogenia.Domain.Tests.Flows.FlowConsistencyTests.Flows_drawn_by_the_rules_link_live_steps_of_their_map` ; `Topogenia.Domain.Tests.Flows.FlowConsistencyTests.A_live_flow_towards_a_deleted_step_breaks_the_rule` ; `Topogenia.Domain.Tests.Flows.FlowConsistencyTests.A_live_flow_whose_step_moved_to_another_map_breaks_the_rule` ; `Topogenia.Domain.Tests.Flows.FlowConsistencyTests.A_flow_recorded_on_another_map_than_its_steps_breaks_the_rule` ; `Topogenia.Domain.Tests.Flows.FlowConsistencyTests.A_reflexive_record_breaks_the_rule_and_a_deleted_flow_never_does` ; `Topogenia.Domain.Tests.Flows.FlowConsistencyTests.The_rule_agrees_with_the_structure_invariants` |
| A-2.6-11 | `Topogenia.Domain.Tests.Flows.FlowConsistencyTests.Deleting_steps_with_their_flows_leaves_no_live_flow_on_a_deleted_step` ; `Topogenia.Domain.Tests.Flows.FlowConsistencyTests.A_live_flow_towards_a_deleted_step_breaks_the_rule` (part domaine) |
| A-2.6-13 | `Topogenia.Domain.Tests.Flows.FlowRulesTests.A_changed_label_drops_its_automatic_translations_and_keeps_the_manual_ones` ; `Topogenia.Domain.Tests.Flows.FlowRulesTests.An_unchanged_label_keeps_the_automatic_translations_and_changes_nothing` ; `Topogenia.Domain.Tests.Flows.FlowRulesTests.Relabelling_changes_the_label_and_the_commit_numbers_the_next_version` ; `Topogenia.Domain.Tests.Flows.FlowRulesTests.An_empty_label_clears_the_label` |
| A-2.6-14 | `Topogenia.Domain.Tests.Flows.CommandLineParserTests.A_simple_command_gives_one_flow_on_line_1` ; `Topogenia.Domain.Tests.Flows.CommandLineParserTests.Lines_are_numbered_counting_blank_lines_and_crlf_endings` |
| A-2.6-15 | `Topogenia.Domain.Tests.Flows.CommandLineParserTests.A_chain_gives_one_flow_per_pair_and_the_label_goes_to_the_last_one` ; `Topogenia.Domain.Tests.Flows.CommandBarTests.Full_and_short_codes_with_a_label_draw_the_flows_in_line_order` |
| A-2.6-16 | `Topogenia.Domain.Tests.Flows.CommandLineParserTests.A_chain_gives_one_flow_per_pair_and_the_label_goes_to_the_last_one` ; `Topogenia.Domain.Tests.Flows.CommandLineParserTests.A_label_of_60_characters_is_accepted_and_61_is_too_long` |
| A-2.6-17 | `Topogenia.Domain.Tests.Flows.CommandLineParserTests.The_first_colon_separates_the_label_which_may_contain_other_colons` ; `Topogenia.Domain.Tests.Flows.CommandLineParserTests.An_empty_label_after_the_colon_gives_a_flow_without_label` |
| A-2.6-18 | `Topogenia.Domain.Tests.Flows.CommandLineParserTests.Codes_ignore_case_and_make_the_project_prefix_and_leading_zeros_optional` |
| A-2.6-19 | `Topogenia.Domain.Tests.Flows.CommandLineParserTests.Codes_ignore_case_and_make_the_project_prefix_and_leading_zeros_optional` ; `Topogenia.Domain.Tests.Flows.CommandBarTests.Full_and_short_codes_with_a_label_draw_the_flows_in_line_order` |
| A-2.6-20 | `Topogenia.Domain.Tests.Flows.CommandLineParserTests.Codes_ignore_case_and_make_the_project_prefix_and_leading_zeros_optional` ; `Topogenia.Domain.Tests.Flows.CommandLineParserTests.A_wrong_line_gives_its_error_code_and_no_flow` |
| A-2.6-21 | `Topogenia.Domain.Tests.Flows.CommandLineParserTests.A_wrong_line_gives_its_error_code_and_no_flow` ; `Topogenia.Domain.Tests.Flows.CommandLineParserTests.A_wrong_line_does_not_prevent_parsing_the_others` ; `Topogenia.Domain.Tests.Flows.CommandLineParserTests.A_text_with_only_wrong_lines_is_not_an_empty_command` |
| A-2.6-22 | `Topogenia.Domain.Tests.Flows.CommandBarTests.A_code_of_another_project_is_unknown_and_never_outside_the_map` ; `Topogenia.Domain.Tests.Flows.CommandBarTests.A_deleted_step_is_unknown` ; `Topogenia.Domain.Tests.Flows.CommandBarTests.One_wrong_line_draws_nothing_and_reports_every_error_by_line` |
| A-2.6-23 | `Topogenia.Domain.Tests.Flows.CommandBarTests.A_step_of_the_project_on_another_map_is_outside_the_map` ; `Topogenia.Domain.Tests.Flows.CommandBarTests.The_command_on_a_sub_map_finds_the_steps_of_that_map` |
| A-2.6-24 | `Topogenia.Domain.Tests.Flows.CommandLineParserTests.A_wrong_line_gives_its_error_code_and_no_flow` ; `Topogenia.Domain.Tests.Flows.CommandBarTests.Syntax_and_project_errors_of_different_lines_are_ordered_by_line` |
| A-2.6-25 | `Topogenia.Domain.Tests.Flows.CommandBarTests.Existing_and_internal_duplicates_are_reported_on_their_lines` ; `Topogenia.Domain.Tests.Flows.CommandBarTests.A_duplicate_inside_one_chain_is_reported` |
| A-2.6-26 | `Topogenia.Domain.Tests.Flows.CommandLineParserTests.A_label_of_60_characters_is_accepted_and_61_is_too_long` ; `Topogenia.Domain.Tests.Flows.CommandBarTests.Syntax_and_project_errors_of_different_lines_are_ordered_by_line` |
| A-2.6-27 | `Topogenia.Domain.Tests.Flows.CommandLineParserTests.A_wrong_line_gives_its_error_code_and_no_flow` ; `Topogenia.Domain.Tests.Flows.CommandLineParserTests.A_null_text_is_an_empty_command` ; `Topogenia.Domain.Tests.Flows.CommandBarTests.An_empty_command_is_refused_with_commande_vide_in_the_errors` |
| A-2.6-28 | `Topogenia.Domain.Tests.Flows.CommandBarTests.One_wrong_line_draws_nothing_and_reports_every_error_by_line` ; `Topogenia.Domain.Tests.Flows.CommandBarTests.Existing_and_internal_duplicates_are_reported_on_their_lines` ; `Topogenia.Domain.Tests.Flows.CommandBarTests.Syntax_and_project_errors_of_different_lines_are_ordered_by_line` |
| A-2.7-01 | `Topogenia.Domain.Tests.Flows.GatewayFlowTests.Decision_parallel_and_inclusive_are_step_types` |
| A-2.7-05 | `Topogenia.Domain.Tests.Flows.GatewayFlowTests.Parallel_inclusive_start_and_end_have_no_content_while_decision_and_wait_have_a_sheet` (classement ; l'écriture effective de la fiche relève du dossier des fiches) |
| A-2.7-06 | `Topogenia.Domain.Tests.Flows.GatewayFlowTests.One_diamond_opens_and_joins_branches_without_limit_of_flows` ; `Topogenia.Domain.Tests.Flows.GatewayFlowTests.Exit_labels_yes_and_no_of_a_decision_are_kept_per_flow` |

## Lignes de ma part non couvertes

- **A-2.6-03** (partie HTTP) : le domaine lève `INTROUVABLE` avec `RefusalKind.NotFound` (testé ci-dessus) ; la traduction en 404 relève de l'hôte, phase 4.
- **A-2.6-05** : deux créations concurrentes de la même flèche, une seule réussit : index unique partiel, relève de la Persistance, phase 3.
- **A-2.6-09** : les flèches internes au bloc suivent les étapes qui changent de carte : relève des opérations qui déplacent des étapes entre cartes (dossiers `Selection`, `SubMaps`). La règle `FlowRules.LinksLiveStepsOfItsMap` / `FlowRules.BrokenFlows` est disponible pour leur contrôle.
- **A-2.6-10** : flèche restante vers l'autre carte → `CONFLIT_INTEGRITE` : même raison que A-2.6-09 (dossiers `Selection`, `SubMaps`) ; partie HTTP en phase 4.
- **A-2.6-11** (partie Persistance) : relève de la phase 3 ; la partie domaine est couverte.
- **A-2.6-12** : déjà couverte par les tests d'architecture (aucun déclencheur), hors de ma part de code.
- **A-2.6-29** : message `L{n} : …` construit par l'hôte, phase 4. Le domaine rend seulement `ligne`, `code`, `detail` (`CommandLineError`).
- **A-2.7-02, A-2.7-03, A-2.7-04** : les refus `ETAPE_SANS_CONTENU` sur la fiche, le RACI, les documents et les irritants en remontée relèvent des dossiers des fiches, du RACI et des remontées. Le classement « sans contenu » (`StepTypes.HasNoContent`, noyau) est vérifié par `GatewayFlowTests.Parallel_inclusive_start_and_end_have_no_content_while_decision_and_wait_have_a_sheet`.
- **A-2.7-07** : rangées 0 à 50 des branches parallèles : relève de `GridRules` (noyau) et du dossier des étapes.
- **A-2.7-08** : à l'insertion, le libellé de sortie reste côté origine : relève de l'insertion d'étape (dossiers `Steps` / `Selection`).
- **A-2.7-09** : recousage (libellé de la flèche entrante ou des sortantes d'un aiguillage) : relève du dossier `Selection` (`Stitcher`).

## Choix faits dans ce dossier

- Commande vide : comme en 1.x, le refus est `COMMANDE_ERREURS` avec une seule erreur `COMMANDE_VIDE` en ligne 1 (le test d'intégration 1.x `Commande_vide_et_lecteur_refuses` attend ce format).
- Le libellé d'une ligne de commande passe par `TextRules` dans l'analyse : un libellé avec balise HTML donne une erreur de ligne `TEXTE_HTML_INTERDIT` (en 1.x, ce refus sortait hors du tout-ou-rien, sans numéro de ligne).
- Les erreurs du refus sont rangées sous la clé de détail `errors` (`CommandBar.ErrorsDetail`), triées par ligne (tri stable).
