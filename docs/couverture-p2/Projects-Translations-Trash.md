<!-- Projects-Translations-Trash.md -->
<!-- Couverture phase 2 des dossiers Projects, Translations et Trash du domaine (annexe A 2.1, 2.14, 2.15, 2.16) -->

# Couverture phase 2 : Projects, Translations, Trash

Tests dans `tests/Topogenia.Domain.Tests/Projects/`, `Translations/` et `Trash/` (150 tests, verts).

| ID | Test 2.0 |
|---|---|
| A-2.1-01 | `Topogenia.Domain.Tests.Projects.ProjectCodeRulesTests.A_project_code_out_of_three_to_six_capital_letters_is_refused` ; `Topogenia.Domain.Tests.Projects.ProjectCodeRulesTests.A_valid_code_is_trimmed_and_accepted` |
| A-2.1-02 | `Topogenia.Domain.Tests.Projects.ProjectCodeRulesTests.The_code_of_a_live_project_is_taken` |
| A-2.1-03 | `Topogenia.Domain.Tests.Projects.ProjectCodeRulesTests.The_code_of_a_project_in_the_trash_stays_reserved_until_the_purge` ; `Topogenia.Domain.Tests.Projects.ProjectCreationTests.The_code_is_checked_against_the_codes_of_the_database` |
| A-2.1-06 | `Topogenia.Domain.Tests.Projects.ProjectCreationTests.Without_language_of_origin_the_effective_language_of_the_creator_applies` ; `Topogenia.Domain.Tests.Projects.ProjectCreationTests.A_language_of_origin_out_of_the_configuration_is_refused` |
| A-2.1-07 | `Topogenia.Domain.Tests.Projects.ProjectCreationTests.Creation_builds_the_root_map_the_first_lane_the_counters_and_the_members_with_the_creator_facilitator` ; `Topogenia.Domain.Tests.Projects.ProjectCreationTests.A_creator_already_in_the_list_keeps_his_role_and_is_not_added_twice` |
| A-2.1-08 | `Topogenia.Domain.Tests.Projects.ProjectCreationTests.The_name_translation_is_kept_outside_the_language_of_origin_and_copied_to_the_root_map` ; `Topogenia.Domain.Tests.Projects.ProjectCreationTests.Creation_builds_the_root_map_the_first_lane_the_counters_and_the_members_with_the_creator_facilitator` |
| A-2.1-09 | `Topogenia.Domain.Tests.Projects.ProjectCreationTests.Creation_builds_the_root_map_the_first_lane_the_counters_and_the_members_with_the_creator_facilitator` ; `Topogenia.Domain.Tests.Projects.ProjectCreationTests.The_first_lane_of_an_english_project_is_named_in_english` ; `Topogenia.Domain.Tests.Projects.LanguageSettingsTests.The_first_lane_name_comes_from_the_language_files_and_falls_back_to_the_default_language` |
| A-2.1-11 | `Topogenia.Domain.Tests.Projects.ReadableCodeTests.A_code_is_never_given_again_after_its_object_is_deleted` |
| A-2.1-12 | `Topogenia.Domain.Tests.Projects.ReadableCodeTests.Each_kind_of_object_has_its_readable_code_format` |
| A-2.1-13 | `Topogenia.Domain.Tests.Projects.ReadableCodeTests.The_number_is_not_capped_and_takes_four_digits_beyond_999` |
| A-2.1-14 | `Topogenia.Domain.Tests.Projects.MemberRulesTests.Replacing_the_members_replaces_the_whole_list_with_the_position_as_display_order` |
| A-2.1-15 | `Topogenia.Domain.Tests.Projects.UserRulesTests.A_windows_login_is_domain_backslash_account_only` ; `Topogenia.Domain.Tests.Projects.MemberRulesTests.Invalid_members_are_refused_without_changing_anything` |
| A-2.1-16 | `Topogenia.Domain.Tests.Projects.UserRulesTests.A_login_is_at_most_128_characters` ; `Topogenia.Domain.Tests.Projects.UserRulesTests.A_windows_login_is_domain_backslash_account_only` |
| A-2.1-17 | `Topogenia.Domain.Tests.Projects.MemberRulesTests.Invalid_members_are_refused_without_changing_anything` |
| A-2.1-18 | `Topogenia.Domain.Tests.Projects.MemberRulesTests.Invalid_members_are_refused_without_changing_anything` |
| A-2.1-19 | `Topogenia.Domain.Tests.Projects.MemberRulesTests.A_person_who_leaves_no_longer_holds_a_lane_nor_a_step_anywhere_in_the_project` ; `Topogenia.Domain.Tests.Projects.MemberRulesTests.A_member_who_stays_keeps_his_lane_and_his_step` |
| A-2.1-20 | `Topogenia.Domain.Tests.Projects.MemberRulesTests.A_list_without_facilitator_is_refused_and_the_project_keeps_its_members` ; `Topogenia.Domain.Tests.Projects.ProjectCreationTests.A_creator_who_leaves_the_project_without_facilitator_is_refused` |
| A-2.1-23 | `Topogenia.Domain.Tests.Projects.MemberRulesTests.Members_are_ordered_sponsors_first_then_display_order_then_name` |
| A-2.1-25 | `Topogenia.Domain.Tests.Projects.UserRulesTests.A_new_user_takes_the_directory_name_and_email_truncated_and_a_normalised_login` ; `Topogenia.Domain.Tests.Projects.UserRulesTests.Without_directory_entry_the_display_name_is_the_short_login` |
| A-2.1-27 | `Topogenia.Domain.Tests.Projects.LanguageSettingsTests.The_browser_language_is_the_first_configured_one_by_decreasing_weight` ; `Topogenia.Domain.Tests.Projects.LanguageSettingsTests.Without_a_configured_browser_language_the_default_language_of_the_configuration_applies` ; `Topogenia.Domain.Tests.Projects.LanguageSettingsTests.The_language_of_the_profile_wins_over_the_browser` ; `Topogenia.Domain.Tests.Projects.LanguageSettingsTests.A_profile_language_no_longer_configured_gives_way_to_the_browser` |
| A-2.1-28 | `Topogenia.Domain.Tests.Projects.LanguageSettingsTests.A_language_out_of_the_configured_list_is_refused` ; `Topogenia.Domain.Tests.Projects.LanguageSettingsTests.The_language_list_comes_from_the_configuration` ; `Topogenia.Domain.Tests.Projects.UserRulesTests.An_unsupported_profile_language_is_refused` |
| A-2.14-04 | `Topogenia.Domain.Tests.Translations.ManualTranslationTests.An_empty_value_deletes_the_translation_and_the_language_of_origin_is_ignored` |
| A-2.14-05 | `Topogenia.Domain.Tests.Translations.ManualTranslationTests.An_empty_value_deletes_the_translation_and_the_language_of_origin_is_ignored` ; `Topogenia.Domain.Tests.Projects.ProjectCreationTests.The_name_translation_is_kept_outside_the_language_of_origin_and_copied_to_the_root_map` |
| A-2.14-07 | `Topogenia.Domain.Tests.Translations.ManualTranslationTests.A_real_text_change_drops_the_automatic_translations_and_keeps_the_manual_ones` ; `Topogenia.Domain.Tests.Translations.ManualTranslationTests.An_unchanged_text_keeps_every_translation` |
| A-2.14-08 | `Topogenia.Domain.Tests.Translations.TranslationCatalogTests.The_live_texts_come_in_reading_order_project_renamed_maps_lanes_steps_then_labelled_arrows` ; `Topogenia.Domain.Tests.Translations.TranslationCatalogTests.Deleted_objects_and_the_content_of_deleted_maps_are_not_listed` |
| A-2.14-09 | `Topogenia.Domain.Tests.Translations.TranslationCatalogTests.A_detailed_map_that_still_bears_the_label_of_its_step_is_not_listed` |
| A-2.14-10 | `Topogenia.Domain.Tests.Translations.TranslationCatalogTests.Each_text_carries_its_code_its_context_its_language_and_its_original` |
| A-2.14-11 | `Topogenia.Domain.Tests.Translations.TranslationCatalogTests.Each_text_carries_its_code_its_context_its_language_and_its_original` ; `Topogenia.Domain.Tests.Translations.TranslationCatalogTests.The_live_texts_come_in_reading_order_project_renamed_maps_lanes_steps_then_labelled_arrows` |
| A-2.14-12 | `Topogenia.Domain.Tests.Translations.TranslationCatalogTests.Scope_missing_gives_the_texts_that_lack_a_target_language` ; `Topogenia.Domain.Tests.Translations.TranslationCatalogTests.A_configured_third_language_makes_a_text_missing_again` |
| A-2.14-13 | `Topogenia.Domain.Tests.Translations.TranslationCatalogTests.Scope_auto_gives_the_texts_missing_or_translated_automatically` |
| A-2.14-14 | `Topogenia.Domain.Tests.Translations.TranslationCatalogTests.Scope_all_gives_every_text_with_its_translations` |
| A-2.14-15 | `Topogenia.Domain.Tests.Translations.TranslationCatalogTests.An_unknown_scope_is_refused` ; `Topogenia.Domain.Tests.Translations.TranslationCatalogTests.The_scope_is_read_from_its_code_missing_by_default` |
| A-2.14-17 | `Topogenia.Domain.Tests.Translations.TranslationImportTests.More_than_twenty_thousand_rows_are_refused` |
| A-2.14-18 | `Topogenia.Domain.Tests.Translations.TranslationImportTests.Rows_whose_text_changed_whose_object_is_gone_or_whose_cell_is_empty_are_ignored_with_their_reason` |
| A-2.14-19 | `Topogenia.Domain.Tests.Translations.TranslationImportTests.Rows_whose_text_changed_whose_object_is_gone_or_whose_cell_is_empty_are_ignored_with_their_reason` ; `Topogenia.Domain.Tests.Translations.TranslationImportTests.Without_original_the_text_is_read_in_the_column_of_the_language_of_origin` |
| A-2.14-20 | `Topogenia.Domain.Tests.Translations.TranslationImportTests.A_manual_translation_is_overwritten_only_with_the_scope_all_and_then_becomes_automatic` |
| A-2.14-21 | `Topogenia.Domain.Tests.Translations.TranslationImportTests.Rows_whose_text_changed_whose_object_is_gone_or_whose_cell_is_empty_are_ignored_with_their_reason` ; `Topogenia.Domain.Tests.Translations.TranslationImportTests.Unconfigured_languages_are_skipped` |
| A-2.14-22 | `Topogenia.Domain.Tests.Translations.TranslationImportTests.One_invalid_translation_refuses_the_whole_file_with_its_line` ; `Topogenia.Domain.Tests.Translations.ManualTranslationTests.A_manual_translation_is_bounded_by_the_length_of_its_type` |
| A-2.14-23 | `Topogenia.Domain.Tests.Translations.TranslationImportTests.Untranslated_texts_are_listed_then_written_in_one_pass_as_automatic` ; `Topogenia.Domain.Tests.Translations.TranslationImportTests.A_manual_translation_is_overwritten_only_with_the_scope_all_and_then_becomes_automatic` |
| A-2.14-24 | `Topogenia.Domain.Tests.Translations.TranslationImportTests.The_root_map_follows_the_project_while_it_bears_the_same_name` |
| A-2.14-25 | `Topogenia.Domain.Tests.Translations.TranslationImportTests.A_detailed_map_follows_its_step_while_it_still_bears_its_label` |
| A-2.15-13 | `Topogenia.Domain.Tests.Trash.TrashRulesTests.The_exact_code_trimmed_moves_the_project_to_the_trash` ; `Topogenia.Domain.Tests.Trash.TrashRulesTests.Another_confirmation_is_refused_with_the_code_and_the_project_stays_live` ; `Topogenia.Domain.Tests.Trash.TrashRulesTests.A_project_already_in_the_trash_is_not_found_by_a_second_deletion` |
| A-2.15-16 | `Topogenia.Domain.Tests.Trash.TrashRulesTests.The_purge_date_is_the_deletion_date_plus_the_retention` |
| A-2.15-17 | `Topogenia.Domain.Tests.Trash.TrashRulesTests.A_project_in_the_trash_is_restored_and_a_live_project_cannot_be` |
| A-2.15-18 | `Topogenia.Domain.Tests.Trash.PurgePlannerTests.The_administrator_purges_a_trashed_project_at_once_but_never_a_live_one` |
| A-2.15-20 | `Topogenia.Domain.Tests.Trash.TrashRulesTests.The_retention_is_thirty_days_unless_a_positive_setting_says_otherwise` ; `Topogenia.Domain.Tests.Trash.TrashRulesTests.A_deletion_expires_strictly_after_the_retention` |
| A-2.15-21 | `Topogenia.Domain.Tests.Trash.PurgePlannerTests.A_trashed_project_past_the_retention_is_purged_whole_each_record_once` ; `Topogenia.Domain.Tests.Trash.PurgePlannerTests.Nothing_is_purged_before_the_retention` |
| A-2.15-22 | `Topogenia.Domain.Tests.Trash.PurgePlannerTests.An_expired_batch_of_a_live_project_is_purged_with_what_it_deleted_and_nothing_else` ; `Topogenia.Domain.Tests.Trash.PurgePlannerTests.The_batch_of_an_open_dissolution_frame_waits_for_the_end_of_the_frame` ; `Topogenia.Domain.Tests.Trash.PurgePlannerTests.A_restored_batch_past_the_retention_is_purged_alone` |
| A-2.15-23 | `Topogenia.Domain.Tests.Trash.PurgePlannerTests.The_plan_deletes_dependents_before_what_they_reference` ; `Topogenia.Domain.Tests.Trash.PurgePlannerTests.Maps_go_from_the_bottom_up_each_after_its_steps_and_lanes` ; `Topogenia.Domain.Tests.Trash.PurgePlannerTests.A_trashed_project_past_the_retention_is_purged_whole_each_record_once` |

Partie domaine seulement (la ligne reste à compléter par une autre phase) : A-2.1-03 et A-2.15-23
côté Persistance (index unique, effacement par lots courts) ; A-2.15-22 pour l'effacement des
opérations du journal qui renvoient aux lots purgés (`operation_log` n'est pas dans le modèle du
noyau).

## Lignes non couvertes

| ID | Raison |
|---|---|
| A-2.1-04 | Persistance (phase 3) : index unique et sérialisation des créations concurrentes. |
| A-2.1-05 | Application (phase 4) : droit administrateur ; le domaine ne vérifie pas les droits. |
| A-2.1-10 | Persistance (phase 3) : atomicité des compteurs. L'initialisation des compteurs `step`, `lane`, `pain_point` est prouvée par `ProjectCreationTests.Creation_builds_the_root_map_the_first_lane_the_counters_and_the_members_with_the_creator_facilitator`. |
| A-2.1-21 | API (phase 4) : réponse de succès à l'animateur qui se retire. Le domaine accepte le retrait (`MemberRulesTests.A_facilitator_may_leave_while_another_facilitator_remains`). |
| A-2.1-22 | Application et Persistance (phase 4) : la transaction. Le domaine renvoie les invités à créer et ne crée rien sur un refus (`MemberRulesTests.An_invited_login_unknown_to_the_database_is_returned_as_a_user_to_create_in_the_same_operation`, `ProjectCreationTests.An_invalid_member_refuses_the_creation_before_any_user_is_made`). |
| A-2.1-24 | Application et API (phase 4) : création à la première requête et événement d'audit. La construction de l'utilisateur est `UserRules.NewUser`. |
| A-2.1-26 | Application (phase 4) : relecture de la configuration au démarrage et à chaque changement. La règle du drapeau (casse ignorée, dans les deux sens) est prouvée par `UserRulesTests.The_administrator_flag_follows_the_configured_list_case_ignored_in_both_directions`. |
| A-2.14-01, A-2.14-02 | Persistance (phase 3) : table polymorphe sans clé étrangère. |
| A-2.14-03 | API (phase 4) : routes d'écriture. La règle d'écriture manuelle est `TranslationRules.WriteManual`. |
| A-2.14-06 | API (phase 4) : routes des traductions d'étape et de flèche. La règle du domaine est prouvée par `ManualTranslationTests.Steps_and_arrows_get_manual_translations_of_their_label`. |
| A-2.14-16 | Application et Persistance (phase 4) : transaction et verrou de projet. L'idempotence (5.6-11) est prouvée par `TranslationImportTests.Importing_the_same_file_again_changes_nothing`. |
| A-2.15-01 | Persistance (phase 3) : contrainte CHECK. |
| A-2.15-02 | Noyau (`ProjectWorkspace.EnsureBatch`), hors de cette part. |
| A-2.15-03 à A-2.15-12 | Restauration d'un lot : n'est plus une route (AR-08, AR-43) ; règle interne de l'annulation, hors de cette part, non écrite ici sur consigne. |
| A-2.15-14 | Application et Sécurité (phase 4) : projet à la corbeille inaccessible. |
| A-2.15-15 | Application (phase 4) : liste de la corbeille selon le rôle. |
| A-2.15-19 | Application (phase 4) : purge au démarrage puis toutes les 24 h. |
| A-2.15-24 | Application (phase 4) : journalisation d'une purge en erreur. |
| A-2.16-01 | Persistance (phase 3) ; la version entière est tenue par le noyau (`ProjectWorkspace.Commit`). |
| A-2.16-02 à A-2.16-07 | API (phase 4) : `If-Match`, 428, 409 `CONFLIT_VERSION`, durée relative formatée par l'hôte (non écrite dans le domaine, sur consigne). |
| A-2.16-08 à A-2.16-11 | Persistance (phase 3) : sérialisation, concurrence, nouvel essai, unicité. |
