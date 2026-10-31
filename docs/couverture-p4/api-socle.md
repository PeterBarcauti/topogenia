<!-- api-socle.md -->
<!-- Coverage at the HTTP level of the common rules and of the routes written by the project lead (P4-T04) -->

# Couverture P4-T04 : socle de l'hôte

| ID | État | Test 2.0 | Remarque |
|---|---|---|---|
| A-1.1-01 | couvert | `Topogenia.Host.Tests.Api.CommonRulesTests.Bodies_and_replies_use_snake_case_names_and_the_codes_of_the_closed_lists` ; `Topogenia.Host.Tests.Api.RouteTableTests.The_openapi_document_describes_every_route_with_snake_case_bodies` | corps écrits en snake_case par le test, réponses relues ; codes des listes fermées |
| A-1.1-04 | couvert | `Topogenia.Host.Tests.Api.CommonRulesTests.A_non_member_is_told_not_found_and_a_weaker_role_is_refused` | `ApiResponse.Refused` exige exactement `code`, `message`, `details` sur chaque refus de la suite |
| A-1.1-05 | couvert | `Topogenia.Host.Tests.Api.CommonRulesTests.The_message_is_in_the_language_of_the_profile_else_of_the_browser_else_french` | profil, puis `Accept-Language`, puis le français (AR-51) |
| A-1.1-06 | couvert (API) | `Topogenia.Host.Tests.Api.CommonRulesTests.A_non_member_is_told_not_found_and_a_weaker_role_is_refused` ; `Topogenia.Host.Tests.Projects.ProjectRoutesTests.The_detail_of_a_project_gives_its_members_and_version_and_hides_the_project_from_a_stranger` | et un 404 par groupe de routes dans chaque lot |
| A-1.1-07 | couvert | `Topogenia.Host.Tests.Api.CommonRulesTests.A_non_member_is_told_not_found_and_a_weaker_role_is_refused` |  |
| A-1.1-08 | couvert | `Topogenia.Host.Tests.Api.CommonRulesTests.A_body_that_is_not_json_or_of_the_wrong_shape_is_an_invalid_request` | JSON cassé, nombre écrit en texte, objet au lieu d'un tableau, élément `null` d'une liste |
| A-1.1-11 | couvert | `Topogenia.Host.Tests.Api.CommonRulesTests.An_unexpected_failure_is_an_internal_error_in_the_json_format_that_says_nothing_of_its_cause` | panne d'un adaptateur : 500 `ERREUR_INTERNE`, détails vides, rien de la cause (ADR 036) |
| A-1.1-12 | couvert (API, en partie) | `Topogenia.Host.Tests.Api.CommonRulesTests.An_anonymous_request_gets_a_bare_401_from_the_fallback_policy_without_reaching_a_handler` | 401 sans utilisateur ; le challenge Negotiate et le cookie du mode Poste relèvent de P4-T05 et P4-T06 |
| A-1.1-16 | couvert (API, part du socle) | `Topogenia.Host.Tests.Maps.MapRoutesTests.A_lane_added_is_answered_with_its_code_the_operation_and_the_map_then_undone_and_redone` | `StructureReply` : `id`, `code`, `ids`, `operation_id`, `warnings`, `structure_version`, `map`, `history` |
| A-1.1-17 | couvert | `Topogenia.Host.Tests.Maps.MapRoutesTests.A_lane_added_is_answered_with_its_code_the_operation_and_the_map_then_undone_and_redone` | aucun `lot` dans la réponse : l'annulation passe par `undo` |
| A-1.2-01 | couvert | `Topogenia.Host.Tests.Projects.ProjectRoutesTests.The_profile_gives_the_roles_in_the_live_projects_the_effective_language_and_the_configured_languages` |  |
| A-1.2-02 | couvert | `Topogenia.Host.Tests.Projects.ProjectRoutesTests.The_profile_changes_only_the_fields_sent_and_refuses_a_null_name_an_unknown_language_and_a_stale_version` ; `Topogenia.Host.Tests.Api.CommonRulesTests.The_message_is_in_the_language_of_the_profile_else_of_the_browser_else_french` | `LANGUE_INVALIDE` ; seuls les champs envoyés changent |
| A-1.2-03 | couvert | `Topogenia.Host.Tests.Projects.ProjectRoutesTests.The_profile_changes_only_the_fields_sent_and_refuses_a_null_name_an_unknown_language_and_a_stale_version` | `display_name` nul : `CHAMP_OBLIGATOIRE(display_name)` |
| A-1.2-06 | couvert (API, en partie) | `Topogenia.Host.Tests.Projects.ProjectRoutesTests.The_profile_changes_only_the_fields_sent_and_refuses_a_null_name_an_unknown_language_and_a_stale_version` | version périmée : `CONFLIT_VERSION` ; le 428 du mode Serveur relève de P4-T06 |
| A-1.2-07 | couvert | `Topogenia.Host.Tests.Projects.ProjectRoutesTests.The_list_shows_the_projects_of_the_member_with_his_role_and_every_project_to_the_administrator` | rôle, `step_count`, `map_count`, tri par code, administrateur : tous |
| A-1.2-08 | couvert | `Topogenia.Host.Tests.Projects.ProjectRoutesTests.An_administrator_creates_a_project_answered_201_with_its_location_and_its_root_map` ; `Topogenia.Host.Tests.Projects.ProjectRoutesTests.Only_an_administrator_creates_a_project_and_its_code_is_checked` | 201 + `Location` ; `ACCES_REFUSE`, `PROJET_CODE_INVALIDE`, `PROJET_CODE_EXISTANT` |
| A-1.2-09 | couvert (API, en partie) | `Topogenia.Host.Tests.Projects.ProjectRoutesTests.Only_an_administrator_creates_a_project_and_its_code_is_checked` | nom absent : `CHAMP_OBLIGATOIRE(name)` ; la longueur est prouvée au domaine |
| A-1.2-12 | couvert | `Topogenia.Host.Tests.Projects.ProjectRoutesTests.The_detail_of_a_project_gives_its_members_and_version_and_hides_the_project_from_a_stranger` |  |
| A-1.3-01 | couvert | `Topogenia.Host.Tests.Maps.MapRoutesTests.The_read_of_a_map_gives_the_map_and_the_history_of_the_reader` | `{map, history}` |
| A-1.3-03 | couvert | `Topogenia.Host.Tests.Maps.MapRoutesTests.A_lane_added_is_answered_with_its_code_the_operation_and_the_map_then_undone_and_redone` ; `Topogenia.Host.Tests.Api.CommonRulesTests.Bodies_and_replies_use_snake_case_names_and_the_codes_of_the_closed_lists` |  |
| A-1.3-05 | couvert | `Topogenia.Host.Tests.Maps.MapRoutesTests.The_lanes_are_reordered_by_the_complete_list_of_their_identifiers` | tableau brut comme en 1.x ; `ORDRE_COULOIRS_INVALIDE` |
| A-1.4-01 | couvert | `Topogenia.Host.Tests.Maps.MapRoutesTests.A_lane_is_renamed_with_its_version_and_a_null_actor_removes_the_actor` ; `Topogenia.Host.Tests.Api.CommonRulesTests.A_stale_version_is_a_conflict_that_names_the_author_and_says_how_long_ago` | PATCH : seuls les champs présents ; `null` retire l'acteur ; `VALEUR_INVALIDE(color)` |
| A-1.4-02 | couvert | `Topogenia.Host.Tests.Maps.MapRoutesTests.A_lane_with_steps_needs_a_choice_and_is_deleted_with_them` | `COULOIR_NON_VIDE` avec `step_count` (AR-60), `steps` inconnu : `VALEUR_INVALIDE` |
| A-1.5-21 | couvert | `Topogenia.Host.Tests.Api.CommonRulesTests.An_unknown_route_of_the_api_is_not_found_in_the_format_of_the_errors` |  |
| A-2.8-26 | couvert | `Topogenia.Host.Tests.Selection.TransferOutsideProjectTests.A_step_cut_in_another_project_of_the_user_is_refused_as_outside_the_project_and_nothing_moves` ; `Topogenia.Host.Tests.Selection.TransferOutsideProjectTests.A_step_of_a_project_the_user_may_not_read_stays_not_found` | AR-71 : `TransferBlockCommand.TranslateRefusalAsync` ; code donné seulement à qui peut lire l'autre projet (5.6-2) |
| A-2.16-02 | couvert | `Topogenia.Host.Tests.Api.CommonRulesTests.If_match_accepts_quotes_and_refuses_an_unreadable_version` |  |
| A-2.16-03 | couvert | `Topogenia.Host.Tests.Api.CommonRulesTests.If_match_accepts_quotes_and_refuses_an_unreadable_version` |  |
| A-2.16-06 | couvert | `Topogenia.Host.Tests.Api.CommonRulesTests.A_stale_version_is_a_conflict_that_names_the_author_and_says_how_long_ago` | `by` = nom affiché |
| A-2.16-07 | couvert | `Topogenia.Host.Tests.Api.CommonRulesTests.A_stale_version_is_a_conflict_that_names_the_author_and_says_how_long_ago` | `when` = « à l'instant », `when_iso` |
| A-2.18-05 | couvert | `Topogenia.Host.Tests.Api.CommonRulesTests.A_body_that_is_not_json_or_of_the_wrong_shape_is_an_invalid_request` |  |
| A-2.18-06 | couvert | `Topogenia.Host.Tests.Api.CommonRulesTests.A_body_that_is_not_json_or_of_the_wrong_shape_is_an_invalid_request` |  |
| A-2.18-07 | couvert (API, part du socle) | `Topogenia.Host.Tests.Maps.MapRoutesTests.A_step_needs_its_lane_and_its_column_never_an_empty_identifier_nor_zero` ; `Topogenia.Host.Tests.Projects.ProjectRoutesTests.The_last_project_opened_is_remembered_for_a_member_only` | `ApiInput.Required` sur chaque identifiant ou nombre requis des corps |

| Méthode | Route 2.0 | Gestionnaire |
|---|---|---|
| GET | `/api/me` | `ProfileApi.GetAsync` |
| PATCH | `/api/me` | `ProfileApi.ChangeAsync` |
| PUT | `/api/me/last-project` (nommée par l'hôte, AR-49) | `ProfileApi.RememberProjectAsync` |
| GET | `/api/projects` | `ProjectApi.ListAsync` |
| POST | `/api/projects` | `ProjectApi.CreateAsync` |
| GET | `/api/projects/{id}` | `ProjectApi.GetAsync` |
| PUT | `/api/projects/{id}/sharing-group-label` (nommée par l'hôte, AR-64) | `ProjectApi.ChangeSharingGroupLabelAsync` |
| GET | `/api/maps/{id}` | `MapApi.GetAsync` |
| GET | `/api/maps/{id}/history` | `MapApi.HistoryAsync` |
| POST | `/api/maps/{id}/undo` | `MapApi.UndoAsync` |
| POST | `/api/maps/{id}/redo` | `MapApi.RedoAsync` |
| DELETE | `/api/maps/{id}` | `MapApi.DeleteDetailAsync` |
| POST | `/api/maps/{id}/reattach` | `MapApi.ReattachAsync` |
| POST | `/api/maps/{id}/lanes` | `LaneApi.AddAsync` |
| PUT | `/api/maps/{id}/lane-order` | `LaneApi.ReorderAsync` |
| PATCH | `/api/lanes/{id}` | `LaneApi.UpdateAsync` |
| DELETE | `/api/lanes/{id}` | `LaneApi.DeleteAsync` |
