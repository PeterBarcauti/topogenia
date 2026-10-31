<!-- sheets-documents.md -->
<!-- Couverture P4-T02 lot D : fiches d'étape, documents et usages, irritants, traductions, au niveau Application -->

# Couverture phase 4, lot D : fiches, documents, usages, traductions

Gestionnaires : `src/Topogenia.Application/Sheets/`, `Documents/`, `Translations/`. Préfixe des tests :
`Topogenia.Application.Tests.` ; classes `Sheets.SheetCommandsTests`, `Sheets.SheetQueryTests`,
`Documents.DocumentCommandsTests`, `Translations.TranslationCommandsTests`. Le niveau API (HTTP, `If-Match` lu dans
l'en-tête, 428, routes 2.0) relève de P4-T04 et P4-T08.

## Décisions du lot

- **Réponse des écritures de fiche, de documents et d'usages** : `MapOf` rend la carte de l'étape. La réponse porte la
  vue de la carte avec le résumé de fiche à jour (état, jauge, fréquence) et les repères (documents des étapes et des
  flèches, irritants). En 1.x, l'enregistrement de la fiche mettait la jauge à jour depuis la réponse (`onResume`,
  B-2.6-11) et chaque changement de documents ou d'usages rechargeait la carte (`PanneauFiche.tsx:250`). La fiche
  elle-même (version suivante pour l'`If-Match`) se relit par `GetSheetQuery` : l'hôte peut l'ajouter à la réponse
  (en 1.x, chaque écriture renvoyait `FicheVue`). La commande ne connaît pas le rôle effectif du lecteur (administrateur
  en animateur) : elle ne peut pas projeter la fiche selon la visibilité.
- **`If-Match` de la fiche** : le client nomme l'étape, pas la fiche, et une étape n'a pas de fiche avant son premier
  enregistrement. `SheetIfMatch` contrôle la version sur la fiche de l'étape quand elle existe, par `VersionCheck`, avant
  l'appel du domaine ; sans fiche, rien à comparer (comme `ServiceFiches.cs:125`, `:159`). `If-Match` étendu (A-2.16-05)
  à la demande de précision (fiche) et à l'usage existant (`DocumentUse` est versionné).
- **Traductions manuelles des étapes et des flèches** : la spec 2.0 les ajoute (§3.6, §10, A-2.14-06, A-6-29), alors que
  l'annexe A 6-8 et A-2.14 constatent leur absence en 1.x. Deux commandes : `PUT /api/steps/{id}/translations` et
  `PUT /api/flows/{id}/translations`, Animateur (comme les traductions d'un couloir), non annulables comme toute
  traduction (B-2.6-43, AR-21), réponse avec la vue de la carte.
- **Import des traductions** : aucune carte nommée (`MapOf` nul), la réponse n'a pas de vue ; le client relit la carte
  (1.x `projet.recharger()`).
- **Responsable** : aucune route propre ; l'assignation passe par `PATCH /api/etapes/{id}` (lot des étapes, B-4-15).

## Lignes

| ID | État | Test 2.0 | Remarque |
|---|---|---|---|
| A-1.1-15 | couvert (application) | `Sheets.SheetCommandsTests.The_sheet_is_filled_by_the_facilitator_the_administrator_the_assigned_contributor_and_the_holder_of_the_lane_only` ; `Documents.DocumentCommandsTests.Documents_are_written_by_the_one_who_may_fill_only` | 403 du droit de remplir, administrateur en animateur, acteur du couloir contributeur (AR-01) |
| A-1.4-03 | couvert (application) | `Sheets.SheetQueryTests.A_reader_sees_the_latest_validated_version_without_lock_writing_nor_map_view` ; `Sheets.SheetQueryTests.The_one_who_may_fill_sees_the_draft_with_the_version_to_send_back_and_his_rights` ; `Sheets.SheetQueryTests.The_sheet_of_a_deleted_step_is_not_found_and_a_stranger_learns_nothing` | Lecteur ; ni verrou ni écriture ; charge fiches et documents, jamais la vue de carte |
| A-1.4-04 | couvert (application) | `Sheets.SheetCommandsTests.A_saved_draft_is_written_logs_nothing_and_the_reply_carries_the_gauge_of_its_step` ; `Sheets.SheetCommandsTests.A_stale_if_match_on_the_sheet_is_a_version_conflict_and_nothing_is_written` ; `Sheets.SheetCommandsTests.A_step_without_content_and_a_deleted_step_have_no_sheet` | |
| A-1.4-05 | couvert (application) | `Sheets.SheetCommandsTests.The_sheet_is_filled_by_the_facilitator_the_administrator_the_assigned_contributor_and_the_holder_of_the_lane_only` | |
| A-1.4-06 | couvert (application) | `Sheets.SheetCommandsTests.Validating_freezes_a_version_checks_the_version_read_and_logs_nothing` ; `Sheets.SheetCommandsTests.Only_the_one_who_may_fill_validates_and_an_empty_sheet_is_refused` | |
| A-1.4-07 | couvert (application) | `Sheets.SheetCommandsTests.The_facilitator_alone_asks_for_a_clarification_on_a_validated_sheet_which_goes_to_needs_review` | |
| A-1.4-08 | couvert (application) | `Documents.DocumentCommandsTests.An_added_output_is_written_logs_nothing_and_the_view_shows_its_marker_and_the_gauge` ; `Documents.DocumentCommandsTests.Documents_are_written_by_the_one_who_may_fill_only` ; `Documents.DocumentCommandsTests.The_entered_values_are_checked_by_the_domain` | |
| A-1.4-09 | couvert (application) | `Documents.DocumentCommandsTests.An_updated_document_checks_the_version_read_and_belongs_to_its_step` | |
| A-1.4-10 | couvert (application) | `Documents.DocumentCommandsTests.An_updated_document_checks_the_version_read_and_belongs_to_its_step` | |
| A-1.4-11 | couvert (application) | `Documents.DocumentCommandsTests.A_deleted_document_takes_its_uses_on_every_step_and_their_reported_pain_points` | |
| A-1.4-12 | couvert (application) | `Documents.DocumentCommandsTests.A_deleted_document_takes_its_uses_on_every_step_and_their_reported_pain_points` | |
| A-1.4-13 | couvert (application) | `Documents.DocumentCommandsTests.The_other_window_lists_the_outputs_of_the_map_except_the_step_and_its_predecessors_for_a_reader` | |
| A-1.4-14 | couvert (application) | `Documents.DocumentCommandsTests.A_used_document_raises_its_pain_point_on_the_producing_step_and_the_flow_carries_it` ; `Documents.DocumentCommandsTests.A_use_checks_the_version_read_and_the_producer_must_be_another_step_of_the_map` | |
| A-1.4-15 | couvert (application) | `Documents.DocumentCommandsTests.Unticking_is_idempotent_and_erases_a_reported_pain_point` | |
| A-1.2-24 | couvert (application) | `Translations.TranslationCommandsTests.The_facilitator_lists_the_texts_of_the_workbook_in_reading_order_without_lock_nor_map_view` ; `Translations.TranslationCommandsTests.The_list_is_for_the_facilitator_and_an_unknown_scope_is_refused_after_the_right` | |
| A-1.2-25 | couvert (application) | `Translations.TranslationCommandsTests.An_import_writes_automatic_translations_under_the_lock_reports_the_ignored_rows_and_logs_nothing` ; `Translations.TranslationCommandsTests.An_import_is_for_the_facilitator_and_bounded_to_twenty_thousand_rows` | |
| A-2.10-18 | couvert | `Sheets.SheetCommandsTests.A_saved_draft_is_written_logs_nothing_and_the_reply_carries_the_gauge_of_its_step` | |
| A-2.10-19 | couvert | `Sheets.SheetCommandsTests.An_empty_draft_creates_no_sheet` | |
| A-2.10-24 | couvert (application) | `Sheets.SheetCommandsTests.Only_the_one_who_may_fill_validates_and_an_empty_sheet_is_refused` | |
| A-2.10-30 | couvert (application) | `Sheets.SheetCommandsTests.The_facilitator_alone_asks_for_a_clarification_on_a_validated_sheet_which_goes_to_needs_review` | Un contributeur qui peut remplir est refusé |
| A-2.10-36 | couvert | `Sheets.SheetCommandsTests.Validating_freezes_a_version_checks_the_version_read_and_logs_nothing` ; `Sheets.SheetQueryTests.A_reader_sees_the_latest_validated_version_without_lock_writing_nor_map_view` | Résumé de la vue renvoyée et fiche lue selon la même visibilité |
| A-2.10-37 | couvert (application) | `Sheets.SheetQueryTests.A_reader_sees_the_latest_validated_version_without_lock_writing_nor_map_view` ; `Sheets.SheetQueryTests.The_one_who_may_fill_sees_the_draft_with_the_version_to_send_back_and_his_rights` | |
| A-2.10-42 | couvert | `Sheets.SheetQueryTests.The_suggestions_are_given_only_to_the_one_who_may_fill` | |
| A-2.10-44 | couvert (application) | `Sheets.SheetCommandsTests.A_step_without_content_and_a_deleted_step_have_no_sheet` ; `Sheets.SheetQueryTests.The_sheet_of_a_deleted_step_is_not_found_and_a_stranger_learns_nothing` | |
| A-2.11-10 | couvert | `Sheets.SheetQueryTests.The_sheet_shows_its_documents_and_their_pain_points_to_every_member_and_its_completeness` | |
| A-2.11-14 | couvert (application) | `Documents.DocumentCommandsTests.A_use_checks_the_version_read_and_the_producer_must_be_another_step_of_the_map` | Création puis mise à jour du même usage |
| A-2.11-21 | couvert (application) | `Documents.DocumentCommandsTests.Unticking_is_idempotent_and_erases_a_reported_pain_point` | |
| A-2.11-23 | couvert | `Documents.DocumentCommandsTests.The_other_window_lists_the_outputs_of_the_map_except_the_step_and_its_predecessors_for_a_reader` | |
| A-2.11-24 | couvert | `Sheets.SheetQueryTests.The_sheet_shows_its_documents_and_their_pain_points_to_every_member_and_its_completeness` | Un lecteur voit les entrées, usages et irritants |
| A-2.14-06 | couvert (application) | `Translations.TranslationCommandsTests.The_facilitator_writes_the_manual_translations_of_a_step_and_the_view_shows_them` ; `Translations.TranslationCommandsTests.The_facilitator_writes_the_manual_translations_of_a_flow_within_the_length_of_its_label` | Routes 2.0 nouvelles |
| A-6-29 | couvert (application) | idem A-2.14-06 | |
| A-2.14-08 | couvert | `Translations.TranslationCommandsTests.The_facilitator_lists_the_texts_of_the_workbook_in_reading_order_without_lock_nor_map_view` | |
| A-2.14-15 | couvert (application) | `Translations.TranslationCommandsTests.The_list_is_for_the_facilitator_and_an_unknown_scope_is_refused_after_the_right` ; `Translations.TranslationCommandsTests.An_import_is_for_the_facilitator_and_bounded_to_twenty_thousand_rows` | |
| A-2.14-16 | couvert (application) | `Translations.TranslationCommandsTests.An_import_writes_automatic_translations_under_the_lock_reports_the_ignored_rows_and_logs_nothing` ; `Translations.TranslationCommandsTests.One_invalid_translation_refuses_the_whole_file_and_writes_nothing` | Verrou (`structure_version + 1`) et une seule unité ; la persistance reste à P3 |
| A-2.14-17 | couvert | `Translations.TranslationCommandsTests.An_import_is_for_the_facilitator_and_bounded_to_twenty_thousand_rows` | |
| A-2.14-18 | couvert | `Translations.TranslationCommandsTests.An_import_writes_automatic_translations_under_the_lock_reports_the_ignored_rows_and_logs_nothing` | |
| A-2.14-19 | couvert | idem | |
| A-2.14-20 | couvert | `Translations.TranslationCommandsTests.A_manual_translation_is_kept_by_the_scopes_missing_and_auto_and_overwritten_by_the_scope_all` | |
| A-2.14-21 | couvert | `Translations.TranslationCommandsTests.An_import_writes_automatic_translations_under_the_lock_reports_the_ignored_rows_and_logs_nothing` | |
| A-2.14-22 | couvert | `Translations.TranslationCommandsTests.One_invalid_translation_refuses_the_whole_file_and_writes_nothing` | |
| A-2.14-23 | couvert | `Translations.TranslationCommandsTests.An_import_writes_automatic_translations_under_the_lock_reports_the_ignored_rows_and_logs_nothing` | |
| A-2.16-05 | couvert (application) | `Sheets.SheetCommandsTests.A_stale_if_match_on_the_sheet_is_a_version_conflict_and_nothing_is_written` ; `Sheets.SheetCommandsTests.Validating_freezes_a_version_checks_the_version_read_and_logs_nothing` ; `Sheets.SheetCommandsTests.The_facilitator_alone_asks_for_a_clarification_on_a_validated_sheet_which_goes_to_needs_review` ; `Documents.DocumentCommandsTests.An_updated_document_checks_the_version_read_and_belongs_to_its_step` ; `Documents.DocumentCommandsTests.A_use_checks_the_version_read_and_the_producer_must_be_another_step_of_the_map` | Part du lot D : fiche, validation, précision, document, usage |
| B-2.6-37 | couvert (application) | `Sheets.SheetCommandsTests.A_saved_draft_is_written_logs_nothing_and_the_reply_carries_the_gauge_of_its_step` ; `Sheets.SheetCommandsTests.Validating_freezes_a_version_checks_the_version_read_and_logs_nothing` ; `Sheets.SheetCommandsTests.The_facilitator_alone_asks_for_a_clarification_on_a_validated_sheet_which_goes_to_needs_review` | Aucune entrée, un `undo` ne trouve rien ; le responsable relève du lot des étapes |
| B-2.6-38 | couvert (application) | `Documents.DocumentCommandsTests.An_added_output_is_written_logs_nothing_and_the_view_shows_its_marker_and_the_gauge` ; `Documents.DocumentCommandsTests.An_updated_document_checks_the_version_read_and_belongs_to_its_step` ; `Documents.DocumentCommandsTests.A_used_document_raises_its_pain_point_on_the_producing_step_and_the_flow_carries_it` ; `Documents.DocumentCommandsTests.Unticking_is_idempotent_and_erases_a_reported_pain_point` ; `Documents.DocumentCommandsTests.A_deleted_document_takes_its_uses_on_every_step_and_their_reported_pain_points` | |
| B-2.6-43 | couvert (application) | `Translations.TranslationCommandsTests.An_import_writes_automatic_translations_under_the_lock_reports_the_ignored_rows_and_logs_nothing` ; `Translations.TranslationCommandsTests.The_facilitator_writes_the_manual_translations_of_a_step_and_the_view_shows_them` ; `Translations.TranslationCommandsTests.The_facilitator_writes_the_manual_translations_of_a_flow_within_the_length_of_its_label` | Import et traductions manuelles : non annulables |
| B-4-11 | couvert (application) | `Sheets.SheetQueryTests.The_sheet_shows_its_documents_and_their_pain_points_to_every_member_and_its_completeness` ; `Sheets.SheetCommandsTests.A_saved_draft_is_written_logs_nothing_and_the_reply_carries_the_gauge_of_its_step` | Complétude calculée par le serveur, dans la fiche et dans la vue |
| B-4-57 | couvert (application) | `Sheets.SheetCommandsTests.The_facilitator_alone_asks_for_a_clarification_on_a_validated_sheet_which_goes_to_needs_review` | |
| B-4-63 | couvert (application) | `Documents.DocumentCommandsTests.A_used_document_raises_its_pain_point_on_the_producing_step_and_the_flow_carries_it` | La flèche de la vue porte le document |
| B-4-76 | couvert (application) | idem | |
| B-5-04 | couvert (application) | `Translations.TranslationCommandsTests.A_manual_translation_is_kept_by_the_scopes_missing_and_auto_and_overwritten_by_the_scope_all` | |
| B-5-05 | couvert (application) | idem | |
| B-5-06 | couvert (application) | `Translations.TranslationCommandsTests.The_facilitator_lists_the_texts_of_the_workbook_in_reading_order_without_lock_nor_map_view` | Route 2.0 : `GET /api/projects/{id}/translations?scope=` |
| B-5-22 | couvert (application) | `Translations.TranslationCommandsTests.An_import_writes_automatic_translations_under_the_lock_reports_the_ignored_rows_and_logs_nothing` | Route 2.0 : `PUT /api/projects/{id}/translations` ; `written` = nombre enregistré |
| B-5-23 | couvert (application) | idem | Lignes ignorées par raison, avec leur numéro |
| B-5-28 | couvert (application) | `Translations.TranslationCommandsTests.The_facilitator_writes_the_manual_translations_of_a_step_and_the_view_shows_them` ; `Translations.TranslationCommandsTests.The_facilitator_writes_the_manual_translations_of_a_flow_within_the_length_of_its_label` | Part serveur |
