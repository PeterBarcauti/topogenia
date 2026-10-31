<!-- api-steps.md -->
<!-- Couverture P4-T04 lot A : points d'entrée HTTP des étapes, flèches, disposition, barre de commande, RACI, Détailler, insertion, traductions -->

# Couverture P4-T04, lot A : étapes, flèches, disposition, RACI

Gestionnaires : `src/Topogenia.Host/Api/Steps/StepApi.cs`, `Api/Flows/FlowApi.cs`, `Api/Layout/LayoutApi.cs`,
`Api/Raci/RaciApi.cs` ; routes : `src/Topogenia.Host/Endpoints/StepEndpoints.cs`.
Tests : `tests/Topogenia.Host.Tests/Steps/` (hôte complet, HTTP seulement, SQLite et SQL Server ; 32 tests, 64 exécutions).
Les tests des flèches sont aussi dans `Steps/` : un espace de noms `Topogenia.Host.Tests.Flows` masquerait l'aide
`MapDrawing.Flows` (importée par `using static`) des tests du lot B (`Selection/`, `Dissolutions/`), qui ne compileraient plus.

Préfixe des tests : `Topogenia.Host.Tests.Steps.`

| ID | État | Test 2.0 | Remarque |
|---|---|---|---|
| A-1.1-16 | couvert (API, part du lot) | `StepRoutesTests.A_step_is_created_in_a_lane_of_its_map_with_a_label_and_answered_with_its_code` ; `StepActionRoutesTests.Detailing_a_step_answers_its_sub_map_and_detailing_it_again_while_empty_warns_and_logs_nothing` ; `LayoutRoutesTests.A_layout_puts_the_steps_in_their_exact_cells_orders_the_lanes_and_is_logged_as_a_move` ; `CommandBarRoutesTests.The_command_bar_draws_its_flows_in_one_operation_and_answers_them_in_the_order_of_the_lines` | `id`, `code`, `ids`, `operation_id`, `warnings`, `map`, `history` des routes du lot : étape créée ou insérée (`id`, `code`), sous-carte du « Détailler » (`id`, `ids`), étapes placées ou déplacées (`ids`), flèches de la barre de commande (`ids`, dans l'ordre des lignes) |
| A-1.3-06 | couvert (API) | `StepRoutesTests.A_step_is_created_in_a_lane_of_its_map_with_a_label_and_answered_with_its_code` | route écrite par le chef de projet ; code `XXXX-E002`, type et colonne relus, `creer_etape` dans l'historique ; contributeur 403, non-membre 404 |
| A-1.3-07 | couvert (API) | `StepRoutesTests.A_step_is_created_in_a_lane_of_its_map_with_a_label_and_answered_with_its_code` | couloir d'une sous-carte → `VALEUR_INVALIDE(lane_id)` |
| A-1.3-08 | couvert (API) | `StepRoutesTests.A_step_is_created_in_a_lane_of_its_map_with_a_label_and_answered_with_its_code` | libellé blanc → `CHAMP_OBLIGATOIRE(label)`, rien d'écrit |
| A-1.3-09 | couvert (API) | `LayoutRoutesTests.A_layout_puts_the_steps_in_their_exact_cells_orders_the_lanes_and_is_logged_as_a_move` ; `LayoutRoutesTests.A_layout_of_the_align_wand_is_logged_as_an_alignment_and_an_unknown_purpose_is_refused` ; `LayoutRoutesTests.A_contributor_neither_lays_out_nor_moves_and_a_stranger_is_told_not_found` | `{lane_order, steps[{id, lane_id, column, row}], purpose}` ; cases exactes et ordre des couloirs relus ; `purpose` `move` par défaut (`deplacer`), `align` (`aligner`), autre → `VALEUR_INVALIDE(purpose)` |
| A-1.3-10 | couvert (API) | `LayoutRoutesTests.A_layout_refuses_a_step_of_another_map_a_taken_cell_and_a_cell_without_column` | étape d'une sous-carte → `ETAPE_HORS_CARTE` |
| A-1.3-11 | couvert (API) | `LayoutRoutesTests.A_layout_refuses_a_step_of_another_map_a_taken_cell_and_a_cell_without_column` | couloir d'une autre carte → `VALEUR_INVALIDE(lane_id)` ; colonne absente → `CHAMP_OBLIGATOIRE(column)` (A-2.18-07) ; case prise → `DISPOSITION_CASE_OCCUPEE` ; aucune case changée |
| A-1.3-12 | couvert (API) | `FlowRoutesTests.A_flow_is_created_between_two_steps_of_the_map_with_its_label` ; `FlowRoutesTests.A_flow_to_itself_a_duplicate_flow_and_a_flow_to_another_map_are_refused` ; `FlowRoutesTests.A_contributor_neither_creates_relabels_deletes_nor_translates_a_flow_and_a_stranger_is_told_not_found` | route écrite par le chef de projet ; `LIEN_REFLEXIF` 400, `LIEN_HORS_CARTE` 400, `INTROUVABLE` 404, `LIEN_DOUBLON` 409, `TEXTE_TROP_LONG` (61), `CHAMP_OBLIGATOIRE(target_step_id)` |
| A-1.3-13 | couvert (API) | `CommandBarRoutesTests.The_command_bar_draws_its_flows_in_one_operation_and_answers_them_in_the_order_of_the_lines` ; `CommandBarRoutesTests.A_command_with_a_wrong_line_draws_nothing_and_details_the_errors_by_line` ; `CommandBarRoutesTests.A_contributor_runs_no_command_and_a_stranger_is_told_not_found` | `{text}` ; une opération `commande` ; `COMMANDE_ERREURS` avec `errors[{line, code, detail}]`, aucune flèche (voir A-2.6-28) |
| A-1.4-16 | couvert (API) | `StepRoutesTests.A_patch_changes_only_the_fields_sent_with_the_version_read_and_a_null_assignee_removes_him` ; `StepRoutesTests.A_patch_moves_a_step_to_its_cell_but_refuses_a_null_lane_or_column_and_an_unknown_type` ; `StepRoutesTests.A_patch_with_a_stale_version_is_a_conflict_that_names_the_author_and_writes_nothing` ; `StepRoutesTests.A_contributor_neither_patches_nor_deletes_a_step_and_a_stranger_is_told_not_found` | `Optional<T>` : seuls les champs présents changent ; `assignee_user_id: null` retire l'assigné (A-2.4-26) ; `lane_id`, `column`, `row` à null → `CHAMP_OBLIGATOIRE` ; type inconnu → `VALEUR_INVALIDE(type)` |
| A-1.4-17 | couvert (API) | `StepRoutesTests.A_macro_step_needs_a_choice_and_its_sub_map_is_reattached_to_the_step_named_by_to` | type `end` sur une étape dont la sous-carte est remplie → `ETAPE_NON_DETAILLABLE` |
| A-1.4-18 | couvert (API) | `StepRoutesTests.A_step_deleted_with_stitching_leaves_its_neighbours_linked_and_an_unknown_sub_map_choice_is_refused` ; `StepRoutesTests.A_step_deleted_without_stitching_takes_its_flows` ; `StepRoutesTests.A_macro_step_needs_a_choice_and_its_sub_map_is_reattached_to_the_step_named_by_to` ; `StepRoutesTests.A_macro_step_deleted_with_the_delete_choice_takes_its_sub_map_with_it` | `?sub_map=delete\|reattach&to=&stitch=` ; code inconnu → `VALEUR_INVALIDE(sub_map)` ; `MACRO_CHOIX_REQUIS` 409 ; `reattach` sans `to` → `RATTACHEMENT_INVALIDE` ; avec `to`, la sous-carte relue a l'héritier pour parent ; `delete` → sous-carte introuvable |
| A-1.4-19 | couvert (API) | `StepActionRoutesTests.Detailing_a_step_answers_its_sub_map_and_detailing_it_again_while_empty_warns_and_logs_nothing` ; `StepActionRoutesTests.Detailing_checks_the_version_of_the_step_and_refuses_a_start_a_filled_sub_map_and_a_contributor` ; `StepActionRoutesTests.A_detail_deeper_than_the_recommended_depth_is_answered_with_its_warning` | `SOUS_CARTE_EXISTANTE` (sans opération), `PROFONDEUR_DEPASSEE` dans `warnings` ; `ETAPE_NON_DETAILLABLE`, `ETAPE_DEJA_DETAILLEE` 409 ; If-Match facultatif |
| A-1.4-20 | couvert (API) | `RaciAndTranslationRoutesTests.A_role_is_set_with_the_version_of_the_step_and_a_null_role_removes_it` ; `RaciAndTranslationRoutesTests.A_role_needs_a_lane_other_than_the_lane_of_the_step_a_known_letter_and_a_step_with_content` ; `RaciAndTranslationRoutesTests.A_contributor_sets_no_role_and_writes_no_translation_and_a_stranger_is_told_not_found` | `{lane_id, role\|null}` ; `CHAMP_OBLIGATOIRE(lane_id)`, `VALEUR_INVALIDE(role)`, `VALEUR_INVALIDE(lane_id)` (couloir de l'étape), `ETAPE_SANS_CONTENU` |
| A-1.4-21 | couvert (API) | `StepActionRoutesTests.A_step_inserted_after_another_takes_its_outgoing_flow_and_is_answered_with_its_code` ; `StepActionRoutesTests.An_insertion_needs_a_known_position_and_a_facilitator` | `{position, label, type}` ; l'étape suivante décalée, la flèche sortante reprise ; libellé blanc → `CHAMP_OBLIGATOIRE` ; contributeur 403, non-membre 404 |
| A-1.4-22 | couvert (API) | `StepActionRoutesTests.An_insertion_needs_a_known_position_and_a_facilitator` | `position` inconnue ou absente → `VALEUR_INVALIDE(position)` (1.x `ParmiListe`) ; `below` sans couloir voisin → `VALEUR_INVALIDE(position)` |
| A-1.5-15 | couvert (API) | `FlowRoutesTests.A_flow_is_relabelled_with_its_version_and_a_stale_version_is_a_conflict` | `{label}` avec If-Match ; `modifier_lien` ; `CONFLIT_VERSION` ; `TEXTE_TROP_LONG` |
| A-1.5-16 | couvert (API) | `FlowRoutesTests.A_deleted_flow_leaves_the_map_and_comes_back_on_undo` | `id` = la flèche (AR-50 : plus de `lot`) ; `supprimer` ; rendue par `undo` ; flèche inconnue → `INTROUVABLE` |
| A-2.6-03 | couvert (API) | `FlowRoutesTests.A_flow_to_itself_a_duplicate_flow_and_a_flow_to_another_map_are_refused` | étape absente → 404 `INTROUVABLE` |
| A-2.6-04 | couvert (API) | `FlowRoutesTests.A_flow_to_itself_a_duplicate_flow_and_a_flow_to_another_map_are_refused` | doublon → 409 `LIEN_DOUBLON`, une seule flèche relue |
| A-2.6-28 | partiel (API) | `CommandBarRoutesTests.A_command_with_a_wrong_line_draws_nothing_and_details_the_errors_by_line` | tout ou rien et `errors[{line, code, detail}]` prouvés ; le `message` par ligne (`L{n} : ...`, 1.x) n'est pas produit par l'hôte (voir « Manques ») |
| A-2.14-06 | couvert (API) | `RaciAndTranslationRoutesTests.The_manual_translations_of_a_step_are_written_and_deleted_by_language_without_entering_the_history` ; `FlowRoutesTests.The_manual_translations_of_a_flow_are_written_and_deleted_by_language` ; `RaciAndTranslationRoutesTests.A_contributor_sets_no_role_and_writes_no_translation_and_a_stranger_is_told_not_found` ; `FlowRoutesTests.A_contributor_neither_creates_relabels_deletes_nor_translates_a_flow_and_a_stranger_is_told_not_found` | `PUT /api/steps/{id}/translations`, `PUT /api/flows/{id}/translations` `{translations: {langue: texte\|null}}` ; relu dans la carte ; null supprime ; `de` → `LANGUE_INVALIDE(translations)` ; `operation_id` null, rien dans l'historique ; Animateur |
| A-2.16-02 | couvert (API, part du lot) | toutes les modifications versionnées du lot (`ApiClient` envoie `If-Match: "n"`) : `StepRoutesTests.A_patch_changes_only_the_fields_sent_with_the_version_read_and_a_null_assignee_removes_him` ; `FlowRoutesTests.A_flow_is_relabelled_with_its_version_and_a_stale_version_is_a_conflict` ; `RaciAndTranslationRoutesTests.A_role_is_set_with_the_version_of_the_step_and_a_null_role_removes_it` | guillemets tolérés |
| A-2.16-03 | couvert (API, part du lot) | `StepRoutesTests.A_patch_with_a_stale_version_is_a_conflict_that_names_the_author_and_writes_nothing` | `If-Match: "v1"` → `VERSION_INVALIDE` |
| A-2.16-05 | couvert (API, part du lot) | `StepRoutesTests.A_patch_with_a_stale_version_is_a_conflict_that_names_the_author_and_writes_nothing` ; `FlowRoutesTests.A_flow_is_relabelled_with_its_version_and_a_stale_version_is_a_conflict` ; `RaciAndTranslationRoutesTests.A_role_is_set_with_the_version_of_the_step_and_a_null_role_removes_it` ; `StepActionRoutesTests.Detailing_checks_the_version_of_the_step_and_refuses_a_start_a_filled_sub_map_and_a_contributor` | contrôle de version transmis par `PATCH /api/steps/{id}`, `PATCH /api/flows/{id}`, `PUT /api/steps/{id}/raci`, `POST /api/steps/{id}/detail` (mutations « If-Match non transmis » tuées sur les quatre) |
| A-2.16-06 | couvert (API, part du lot) | `StepRoutesTests.A_patch_with_a_stale_version_is_a_conflict_that_names_the_author_and_writes_nothing` ; `FlowRoutesTests.A_flow_is_relabelled_with_its_version_and_a_stale_version_is_a_conflict` | 409 `CONFLIT_VERSION`, `by` = `anne`, rien d'écrit |
| A-2.16-07 | couvert (API, part du lot) | `StepRoutesTests.A_patch_with_a_stale_version_is_a_conflict_that_names_the_author_and_writes_nothing` | `when` = « à l'instant » |
| A-2.18-07 | couvert (API, part du lot) | `StepRoutesTests.A_patch_moves_a_step_to_its_cell_but_refuses_a_null_lane_or_column_and_an_unknown_type` ; `LayoutRoutesTests.A_layout_refuses_a_step_of_another_map_a_taken_cell_and_a_cell_without_column` ; `LayoutRoutesTests.Dragged_steps_are_moved_in_one_operation_taken_back_by_one_undo` ; `RaciAndTranslationRoutesTests.A_role_needs_a_lane_other_than_the_lane_of_the_step_a_known_letter_and_a_step_with_content` ; `FlowRoutesTests.A_flow_to_itself_a_duplicate_flow_and_a_flow_to_another_map_are_refused` | `CHAMP_OBLIGATOIRE` sur `lane_id`, `column`, `row` (PATCH à null), `column` (disposition), `steps` (déplacement), `lane_id` (RACI), `target_step_id` (flèche) ; jamais `Guid.Empty` ni 0 |

## Décisions de lecture prises par le lot

- `DELETE /api/steps/{id}` : `sub_map=reattach` (table des routes et mission) est lu vers `SubMapChoice.Attach`, dont le
  code est `attach` ; le commentaire de `DeleteStepCommand` écrit `delete|attach`. Seuls `delete` et `reattach` sont admis.
- `PUT /api/maps/{id}/layout` : `ApplyLayoutCommand` exige un `LayoutPurpose` que la 1.x ne transmettait pas (le client
  nommait l'entrée d'historique) ; le corps porte `purpose` (`move` par défaut, `align`).
- Cellule de disposition et de déplacement : `{id, lane_id, column, row}` (traduction de `PositionEtape`), `row` 0 par défaut
  comme en 1.x.
- `POST /api/steps/{id}/insert` : position absente → `VALEUR_INVALIDE(position)`, comme la 1.x (`ParmiListe`).
- Réponses : `id` = l'étape pour PATCH, DELETE, RACI et traductions d'étape ; la flèche pour PATCH, DELETE et traductions de
  flèche ; la sous-carte (`id` et `ids`) pour « Détailler » ; la carte pour disposition, déplacement et barre de commande
  (comme `PUT /api/maps/{id}/lane-order`), avec `ids` = étapes placées ou flèches tracées.

## Manques signalés (hors des fichiers du lot)

- `COMMANDE_ERREURS` : la spécification (A-1, A-2.6-28) et la 1.x donnent à chaque erreur un `message` (`L{n} : ` + message
  du code, dans la langue de l'utilisateur) ; `CommandBar` (domaine) laisse ce message à l'hôte, et ni
  `Errors/ErrorResponses.cs` ni `ErrorMessages.cs` ne le construisent. À traiter dans `src/Topogenia.Host/Errors/`.

## Routes du lot

| Méthode | Route 2.0 | Gestionnaire |
|---|---|---|
| POST | `/api/maps/{id}/steps` | `StepApi.CreateAsync` |
| POST | `/api/maps/{id}/flows` | `FlowApi.CreateAsync` |
| POST | `/api/maps/{id}/command` | `FlowApi.CommandAsync` |
| PUT | `/api/maps/{id}/layout` | `LayoutApi.LayOutAsync` |
| POST | `/api/maps/{id}/move` (nommée par le lot, AR-44, absente de la table) | `LayoutApi.MoveAsync` |
| PATCH | `/api/steps/{id}` | `StepApi.UpdateAsync` |
| DELETE | `/api/steps/{id}?sub_map=delete\|reattach&to=&stitch=` | `StepApi.DeleteAsync` |
| POST | `/api/steps/{id}/insert` | `StepApi.InsertAsync` |
| POST | `/api/steps/{id}/detail` | `StepApi.DetailAsync` |
| PUT | `/api/steps/{id}/raci` | `RaciApi.SetAsync` |
| PUT | `/api/steps/{id}/translations` | `StepApi.TranslateAsync` |
| PATCH | `/api/flows/{id}` | `FlowApi.RelabelAsync` |
| DELETE | `/api/flows/{id}` | `FlowApi.DeleteAsync` |
| PUT | `/api/flows/{id}/translations` | `FlowApi.TranslateAsync` |
