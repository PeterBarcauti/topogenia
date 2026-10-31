<!-- api-sheets.md -->
<!-- Couverture P4-T04 lot C : points d'entrée HTTP des fiches d'étape, des documents et des usages -->

# Couverture phase 4, P4-T04 lot C : fiches, documents, usages (niveau API)

Gestionnaires : `src/Topogenia.Host/Api/Sheets/SheetApi.cs`, `src/Topogenia.Host/Api/Documents/DocumentApi.cs` ; routes :
`src/Topogenia.Host/Endpoints/SheetEndpoints.cs`. Tests : `tests/Topogenia.Host.Tests/Sheets/SheetRoutesTests.cs`,
`tests/Topogenia.Host.Tests/Documents/DocumentRoutesTests.cs` (aide : `Sheets/StepSheets.cs`), chacun sur SQLite et SQL
Server, par HTTP seulement.

## Décisions du lot

- **Réponse des écritures** : comme la 1.x (`FicheVue`), chaque écriture (fiche, validation, précision, documents,
  usages) rend la fiche de l'étape (`SheetView`), relue par `GetSheetQuery` après la commande : la commande ne connaît
  pas la visibilité selon le rôle, la lecture la connaît (version suivante à renvoyer dans `If-Match` comprise). La vue
  de carte portée par le résultat de la commande n'est pas renvoyée : la 1.x mettait la jauge à jour depuis la fiche et
  rechargeait la carte après un changement de documents (`PanneauFiche.tsx:250`).
- **Noms des champs (AR-60)** : fiche `{fields: {<clé>: {value, number}}, procedure: [{text, tool, nature}], free_text}`
  (1.x `champs` / `valeur`, `nombre` / `deroule` / `texte_libre`) ; clés des champs telles que le domaine les nomme
  (`actor`, `purpose`, `tools`, `frequency`, `duration_minutes`). Précision `{text}`. Document
  `{direction, name, location, wait_hours, share_confirmed}` (1.x `sens`, `libelle`, `adresse`, `attente_heures`,
  `partage_confirme`) et usage `{wait_hours, percent_complete_accurate, pain_point, lost_hours_per_month}` (1.x
  `attente_heures`, `pct_ca`, `irritant`, `temps_perdu_h_mois`) : les noms des champs que citent les refus du domaine
  (`DocumentRules`), pour qu'un `VALEUR_INVALIDE` nomme le champ du corps.
- **`If-Match`** : lu sur `PUT /sheet`, `POST /sheet/validate`, `POST /sheet/clarification` (étendu, A-2.16-05),
  `PUT /documents/{documentId}` et `PUT /uses/{documentId}` (usage existant, étendu). `DELETE` d'un document ou d'un
  usage : pas de version, comme la 1.x. `If-Match` absent : pas de contrôle (le 428 du mode Serveur relève de P4-T06).
- **Fenêtre « Autre… »** : la liste est rendue dans un type de l'hôte (`AvailableDocuments`, collection), pour que la
  route ne dépende d'aucun type du domaine (`DependencyRulesTests.Http_entry_points_depend_on_handlers_only`) ; le JSON
  reste un tableau, comme la 1.x.

## Lignes couvertes

| ID | État | Test 2.0 | Remarque |
|---|---|---|---|
| A-1.1-15 | couvert (API) | `Topogenia.Host.Tests.Sheets.SheetRoutesTests.A_contributor_fills_and_validates_only_the_sheets_of_the_lane_he_holds` ; `Topogenia.Host.Tests.Documents.DocumentRoutesTests.Documents_and_uses_are_written_by_the_one_who_may_fill_only` | Contributeur ni acteur ni assigné : 403 `ACCES_REFUSE` sur fiche, validation, documents, usages ; acteur du couloir (`PATCH /api/lanes/{id}` `actor_user_id`) : il remplit ; lecteur 403 ; non-membre 404 |
| A-1.4-03 | couvert (API) | `Topogenia.Host.Tests.Sheets.SheetRoutesTests.The_sheet_of_a_new_step_is_empty_with_the_rights_of_the_reader_and_a_stranger_learns_nothing` ; `Topogenia.Host.Tests.Sheets.SheetRoutesTests.A_saved_draft_is_answered_as_the_sheet_and_the_reader_sees_only_the_validated_version` | Fiche vide, droits selon le rôle ; le lecteur ne voit ni le brouillon ni sa version, puis la version validée seulement, même après un nouveau brouillon ; non-membre 404 |
| A-1.4-04 | couvert (API) | `Topogenia.Host.Tests.Sheets.SheetRoutesTests.A_saved_draft_is_answered_as_the_sheet_and_the_reader_sees_only_the_validated_version` ; `Topogenia.Host.Tests.Sheets.SheetRoutesTests.A_stale_if_match_on_the_sheet_is_a_conflict_and_the_draft_stays_as_it_was` ; `Topogenia.Host.Tests.Sheets.SheetRoutesTests.The_body_of_the_sheet_is_refused_on_the_key_of_its_wrong_field_and_an_empty_sheet_is_not_validated` | Brouillon complet (champs, déroulé, texte libre) relu dans la réponse ; `VALEUR_INVALIDE` sur la clé (inconnue, fréquence hors liste, texte sur la durée, nature du déroulé) ; `ETAPE_SANS_CONTENU` sur une étape `start` ; `CONFLIT_VERSION` |
| A-1.4-05 | couvert (API) | `Topogenia.Host.Tests.Sheets.SheetRoutesTests.A_contributor_fills_and_validates_only_the_sheets_of_the_lane_he_holds` | 403 `ACCES_REFUSE` pour le contributeur sans droit de remplir et le lecteur ; la fiche reste vide |
| A-1.4-06 | couvert (API) | `Topogenia.Host.Tests.Sheets.SheetRoutesTests.A_saved_draft_is_answered_as_the_sheet_and_the_reader_sees_only_the_validated_version` ; `Topogenia.Host.Tests.Sheets.SheetRoutesTests.A_contributor_fills_and_validates_only_the_sheets_of_the_lane_he_holds` ; `Topogenia.Host.Tests.Sheets.SheetRoutesTests.The_body_of_the_sheet_is_refused_on_the_key_of_its_wrong_field_and_an_empty_sheet_is_not_validated` ; `Topogenia.Host.Tests.Sheets.SheetRoutesTests.A_stale_if_match_on_the_sheet_is_a_conflict_and_the_draft_stays_as_it_was` | Version 1 figée, état validated ; `FICHE_VIDE` ; `CONFLIT_VERSION` ; 403 / 404 |
| A-1.4-07 | couvert (API) | `Topogenia.Host.Tests.Sheets.SheetRoutesTests.The_facilitator_alone_asks_for_a_clarification_on_a_validated_sheet_which_goes_to_needs_review` | `{text}` → needs_review, vue aussi du lecteur avec la demande ; `FICHE_ETAT_INVALIDE` sur un brouillon ; `CHAMP_OBLIGATOIRE(text)` ; 403 pour un contributeur qui peut remplir ; `CONFLIT_VERSION` |
| A-1.4-08 | couvert (API) | `Topogenia.Host.Tests.Documents.DocumentRoutesTests.An_output_and_an_external_input_added_are_answered_as_the_sheet_and_seen_by_every_member` ; `Topogenia.Host.Tests.Documents.DocumentRoutesTests.The_body_of_a_document_is_refused_on_the_name_of_its_wrong_field` | Sortie (URL, partage confirmé) et entrée extérieure (chemin, attente) dans la fiche rendue, vues du lecteur ; `VALEUR_INVALIDE(direction)`, `CHAMP_OBLIGATOIRE(name)`, `VALEUR_INVALIDE(wait_hours)` sur une sortie, `ADRESSE_INVALIDE` |
| A-1.4-09 | couvert (API) | `Topogenia.Host.Tests.Documents.DocumentRoutesTests.A_document_is_updated_with_its_version_belongs_to_its_step_and_a_stale_version_is_a_conflict` | Renommé avec sa version, sens inchangé ; `CONFLIT_VERSION` avec l'auteur |
| A-1.4-10 | couvert (API) | `Topogenia.Host.Tests.Documents.DocumentRoutesTests.A_document_is_updated_with_its_version_belongs_to_its_step_and_a_stale_version_is_a_conflict` | Document d'une autre étape → 404 `INTROUVABLE` |
| A-1.4-11 | couvert (API) | `Topogenia.Host.Tests.Documents.DocumentRoutesTests.A_document_is_deleted_with_its_uses_and_a_document_absent_from_the_step_is_not_found` | Le document part avec l'usage d'une autre étape (entrées de l'étape consommatrice vides) |
| A-1.4-12 | couvert (API) | `Topogenia.Host.Tests.Documents.DocumentRoutesTests.A_document_is_deleted_with_its_uses_and_a_document_absent_from_the_step_is_not_found` | Document d'une autre étape, puis déjà supprimé → 404 `INTROUVABLE` |
| A-1.4-13 | couvert (API) | `Topogenia.Host.Tests.Documents.DocumentRoutesTests.The_other_window_lists_the_outputs_of_the_other_steps_with_the_marker_of_a_used_one` | Lecteur ; sorties groupées par étape productrice, marqueur `used` après l'usage ; non-membre 404 |
| A-1.4-14 | couvert (API) | `Topogenia.Host.Tests.Documents.DocumentRoutesTests.A_use_ticks_the_output_of_another_step_with_its_pain_point_and_unticking_is_idempotent` ; `Topogenia.Host.Tests.Documents.DocumentRoutesTests.A_use_checks_the_version_read_its_values_and_a_producer_that_is_another_step` | Usage (attente, %C&A) et irritant sur l'étape productrice dans la fiche rendue ; `VALEUR_INVALIDE(percent_complete_accurate)`, `VALEUR_INVALIDE(lost_hours_per_month)` ; sortie de l'étape elle-même ou document inconnu → 404 ; `CONFLIT_VERSION` |
| A-1.4-15 | couvert (API) | `Topogenia.Host.Tests.Documents.DocumentRoutesTests.A_use_ticks_the_output_of_another_step_with_its_pain_point_and_unticking_is_idempotent` | Décoche : usage et irritant retirés ; second `DELETE` 200 ; plus d'usage côté producteur |
| A-2.10-24 | couvert (API) | `Topogenia.Host.Tests.Sheets.SheetRoutesTests.A_contributor_fills_and_validates_only_the_sheets_of_the_lane_he_holds` | Valider sans droit de remplir → 403, puis acteur du couloir : validée |
| A-2.10-30 | couvert (API) | `Topogenia.Host.Tests.Sheets.SheetRoutesTests.The_facilitator_alone_asks_for_a_clarification_on_a_validated_sheet_which_goes_to_needs_review` | Contributeur acteur du couloir → 403 |
| A-2.10-37 | couvert (API) | `Topogenia.Host.Tests.Sheets.SheetRoutesTests.The_sheet_of_a_new_step_is_empty_with_the_rights_of_the_reader_and_a_stranger_learns_nothing` ; `Topogenia.Host.Tests.Sheets.SheetRoutesTests.A_contributor_fills_and_validates_only_the_sheets_of_the_lane_he_holds` | `permissions` `{fill, validate, request_clarification, assign}` : animateur tout, lecteur rien, acteur du couloir remplir et valider |
| A-2.10-44 | couvert (API) | `Topogenia.Host.Tests.Sheets.SheetRoutesTests.The_sheet_of_a_deleted_step_is_not_found` | Étape supprimée avec son couloir : lecture et écriture 404 `INTROUVABLE` |
| A-2.11-21 | couvert (API) | `Topogenia.Host.Tests.Documents.DocumentRoutesTests.A_use_ticks_the_output_of_another_step_with_its_pain_point_and_unticking_is_idempotent` | Décocher retire l'irritant encore reported ; idempotent |
| A-2.16-02 | couvert (API) | `Topogenia.Host.Tests.Sheets.SheetRoutesTests.A_stale_if_match_on_the_sheet_is_a_conflict_and_the_draft_stays_as_it_was` | Version sans guillemets acceptée sur la fiche (les autres tests l'envoient entre guillemets) |
| A-2.16-03 | couvert (API) | `Topogenia.Host.Tests.Sheets.SheetRoutesTests.A_stale_if_match_on_the_sheet_is_a_conflict_and_the_draft_stays_as_it_was` | `"v2"` → 400 `VERSION_INVALIDE`, brouillon inchangé |
| A-2.16-05 | couvert (API) | `Topogenia.Host.Tests.Sheets.SheetRoutesTests.A_stale_if_match_on_the_sheet_is_a_conflict_and_the_draft_stays_as_it_was` ; `Topogenia.Host.Tests.Sheets.SheetRoutesTests.The_facilitator_alone_asks_for_a_clarification_on_a_validated_sheet_which_goes_to_needs_review` ; `Topogenia.Host.Tests.Documents.DocumentRoutesTests.A_document_is_updated_with_its_version_belongs_to_its_step_and_a_stale_version_is_a_conflict` ; `Topogenia.Host.Tests.Documents.DocumentRoutesTests.A_use_checks_the_version_read_its_values_and_a_producer_that_is_another_step` | Part du lot C : fiche (enregistrer, valider), précision, document, usage existant |
| A-2.16-06 | couvert (API) | `Topogenia.Host.Tests.Sheets.SheetRoutesTests.A_stale_if_match_on_the_sheet_is_a_conflict_and_the_draft_stays_as_it_was` ; `Topogenia.Host.Tests.Documents.DocumentRoutesTests.A_document_is_updated_with_its_version_belongs_to_its_step_and_a_stale_version_is_a_conflict` | 409 `CONFLIT_VERSION`, `by` = nom de l'auteur |
| A-2.16-07 | couvert (API) | `Topogenia.Host.Tests.Sheets.SheetRoutesTests.A_stale_if_match_on_the_sheet_is_a_conflict_and_the_draft_stays_as_it_was` | `when` = « à l'instant » |

## Routes du lot

| Méthode | Route 2.0 | Gestionnaire |
|---|---|---|
| GET | `/api/steps/{id}/sheet` | `SheetApi.GetAsync` (`GetSheetQuery`) |
| PUT | `/api/steps/{id}/sheet` | `SheetApi.SaveAsync` (`SaveSheetCommand.WithVersion`, puis `GetSheetQuery`) |
| POST | `/api/steps/{id}/sheet/validate` | `SheetApi.ValidateAsync` (`ValidateSheetCommand.WithVersion`, puis `GetSheetQuery`) |
| POST | `/api/steps/{id}/sheet/clarification` | `SheetApi.RequestClarificationAsync` (`RequestClarificationCommand.WithVersion`, puis `GetSheetQuery`) |
| POST | `/api/steps/{id}/documents` | `DocumentApi.AddAsync` (`AddDocumentCommand`, puis `GetSheetQuery`) |
| PUT | `/api/steps/{id}/documents/{documentId}` | `DocumentApi.UpdateAsync` (`UpdateDocumentCommand.WithVersion`, puis `GetSheetQuery`) |
| DELETE | `/api/steps/{id}/documents/{documentId}` | `DocumentApi.DeleteAsync` (`DeleteDocumentCommand`, puis `GetSheetQuery`) |
| GET | `/api/steps/{id}/documents/available` | `DocumentApi.AvailableAsync` (`GetAvailableDocumentsQuery`) |
| PUT | `/api/steps/{id}/uses/{documentId}` | `DocumentApi.UseAsync` (`UseDocumentCommand.WithVersion`, puis `GetSheetQuery`) |
| DELETE | `/api/steps/{id}/uses/{documentId}` | `DocumentApi.StopUsingAsync` (`StopUsingDocumentCommand`, puis `GetSheetQuery`) |
