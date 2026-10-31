<!-- api-administration.md -->
<!-- Couverture du lot D de P4-T04 : points d'entrée HTTP des membres, de la corbeille, de la mise à la corbeille et des traductions d'un projet -->

# Couverture P4-T04, lot D : membres, corbeille, traductions d'un projet

Tests de `tests/Topogenia.Host.Tests` (hôte complet en mémoire, matrice SQLite et SQL Server, HTTP seulement). Préfixe
des noms : `Topogenia.Host.Tests`. Les règles métier sont prouvées par les couches domaine et application ; ces tests
prouvent la route, le corps, la réponse et le chemin des erreurs.

## Annexe A, section 1 : routes

| ID | État | Test 2.0 | Remarque |
|---|---|---|---|
| A-1.2-13 | couvert | `Topogenia.Host.Tests.Members.MemberRoutesTests.The_whole_list_replaces_the_members_with_an_invited_user_and_the_participants_read_it_back` ; `Topogenia.Host.Tests.Members.MemberRoutesTests.Only_a_facilitator_replaces_the_members_a_reader_reads_them_and_a_non_member_sees_nothing` ; `Topogenia.Host.Tests.Members.MemberRoutesTests.A_refused_list_changes_nothing_and_invites_nobody` | Corps : tableau JSON brut `[{login, role, is_sponsor, is_key_contributor}]` ; corps `null` : `CHAMP_OBLIGATOIRE(members)` ; `MEMBRE_ROLE_INVALIDE`, `MEMBRE_LOGIN_INVALIDE`, `PROJET_SANS_ANIMATEUR` (400) ; 403 contributeur, 404 non-membre. Réponse `MembersReply` (voir écarts) |
| A-1.2-14 | couvert | `Topogenia.Host.Tests.Members.MemberRoutesTests.Only_a_facilitator_replaces_the_members_a_reader_reads_them_and_a_non_member_sees_nothing` ; `Topogenia.Host.Tests.Members.MemberRoutesTests.The_whole_list_replaces_the_members_with_an_invited_user_and_the_participants_read_it_back` | Lecteur ; sponsors en tête ; 404 non-membre et membre retiré |
| A-1.2-23 | couvert | `Topogenia.Host.Tests.Trash.TrashRoutesTests.A_project_moved_to_the_trash_with_its_exact_code_is_listed_with_its_purge_date_then_restored` ; `Topogenia.Host.Tests.Trash.TrashRoutesTests.The_move_to_the_trash_needs_the_exact_code_and_a_facilitator` | 204 sans corps ; `CONFIRMATION_INVALIDE` (absente, casse différente) avec `code` ; 403, 404 |
| A-1.2-24 | couvert | `Topogenia.Host.Tests.Translations.TranslationRoutesTests.The_texts_to_translate_are_listed_in_reading_order_then_imported_in_one_pass_as_automatic` ; `Topogenia.Host.Tests.Translations.TranslationRoutesTests.The_translations_of_a_project_are_reserved_to_its_facilitators` ; `Topogenia.Host.Tests.Translations.TranslationRoutesTests.An_unknown_scope_or_a_missing_field_of_the_workbook_is_refused_by_its_name` | Tableau brut des textes ; `scope` absent = `missing` ; `VALEUR_INVALIDE(scope)` ; un non-membre reçoit 404 même avec une portée inconnue |
| A-1.2-25 | couvert | `Topogenia.Host.Tests.Translations.TranslationRoutesTests.The_texts_to_translate_are_listed_in_reading_order_then_imported_in_one_pass_as_automatic` ; `Topogenia.Host.Tests.Translations.TranslationRoutesTests.One_invalid_translation_refuses_the_whole_file_with_its_line_and_writes_nothing` ; `Topogenia.Host.Tests.Translations.TranslationRoutesTests.An_unknown_scope_or_a_missing_field_of_the_workbook_is_refused_by_its_name` | Corps `{scope, rows: [{line, type, id, original, translations}]}` ; réponse `{written, ignored: [{line, reason}]}` ; `CHAMP_OBLIGATOIRE` sur `rows`, `id`, `line` ; `TRADUCTIONS_INVALIDES` avec `errors[line, language, code]` |
| A-1.2-26 | couvert | `Topogenia.Host.Tests.Trash.TrashRoutesTests.A_project_moved_to_the_trash_with_its_exact_code_is_listed_with_its_purge_date_then_restored` | `{retention_days, projects: [{id, code, name, deleted_at, deleted_by, purge_at, step_count}]}` ; absent pour un lecteur, présent pour l'administrateur non membre |
| A-1.2-27 | couvert | `Topogenia.Host.Tests.Trash.TrashRoutesTests.A_project_moved_to_the_trash_with_its_exact_code_is_listed_with_its_purge_date_then_restored` ; `Topogenia.Host.Tests.Trash.TrashRoutesTests.Only_a_facilitator_of_the_deleted_project_or_an_administrator_restores_it_and_a_live_project_is_not_found` | 204 sans corps ; projet relu ensuite ; 403 contributeur, 404 non-membre |
| A-1.2-28 | couvert | `Topogenia.Host.Tests.Trash.TrashRoutesTests.Only_a_facilitator_of_the_deleted_project_or_an_administrator_restores_it_and_a_live_project_is_not_found` | Projet vivant ou déjà restauré : 404 `INTROUVABLE` |
| A-1.2-29 | couvert | `Topogenia.Host.Tests.Trash.TrashRoutesTests.Only_an_administrator_purges_a_project_of_the_trash_at_once_and_never_a_live_one` | 204 sans corps ; le projet quitte la corbeille, ni restauration ni seconde purge (404) |
| A-1.2-30 | couvert | `Topogenia.Host.Tests.Trash.TrashRoutesTests.Only_an_administrator_purges_a_project_of_the_trash_at_once_and_never_a_live_one` | Animateur du projet : 403 `ACCES_REFUSE`, le projet reste à la corbeille |
| A-1.2-31 | couvert | `Topogenia.Host.Tests.Trash.TrashRoutesTests.Only_an_administrator_purges_a_project_of_the_trash_at_once_and_never_a_live_one` | Projet vivant : 404 `INTROUVABLE`, le projet reste lisible |

## Annexe A, sections 2.1, 2.14, 2.15 : lignes au niveau API

| ID | État | Test 2.0 | Remarque |
|---|---|---|---|
| A-2.1-21 | couvert | `Topogenia.Host.Tests.Members.MemberRoutesTests.A_facilitator_who_removes_himself_still_gets_his_success_reply_and_then_loses_the_project` | 200 avec la liste écrite, puis 404 sur le projet |
| A-2.14-15 | couvert | `Topogenia.Host.Tests.Translations.TranslationRoutesTests.An_unknown_scope_or_a_missing_field_of_the_workbook_is_refused_by_its_name` | `VALEUR_INVALIDE` avec `field = scope`, en lecture et à l'import |
| A-2.15-13 | couvert | `Topogenia.Host.Tests.Trash.TrashRoutesTests.A_project_moved_to_the_trash_with_its_exact_code_is_listed_with_its_purge_date_then_restored` ; `Topogenia.Host.Tests.Trash.TrashRoutesTests.The_move_to_the_trash_needs_the_exact_code_and_a_facilitator` | Code entouré d'espaces accepté (trim) ; casse différente refusée |
| A-2.15-14 | couvert (API) | `Topogenia.Host.Tests.Trash.TrashRoutesTests.A_project_moved_to_the_trash_with_its_exact_code_is_listed_with_its_purge_date_then_restored` | Détail du projet à la corbeille : 404 pour l'animateur et pour l'administrateur ; la campagne Sécurité reste à P4-T08 |
| A-2.15-17 | couvert | `Topogenia.Host.Tests.Trash.TrashRoutesTests.Only_a_facilitator_of_the_deleted_project_or_an_administrator_restores_it_and_a_live_project_is_not_found` ; `Topogenia.Host.Tests.Trash.TrashRoutesTests.A_project_moved_to_the_trash_with_its_exact_code_is_listed_with_its_purge_date_then_restored` | Animateur, et administrateur retiré des membres au préalable |

Lignes sans niveau API, aussi prouvées par HTTP : A-2.1-22 (liste refusée : l'invité n'est pas créé, il l'est à la
liste acceptée suivante, `A_refused_list_changes_nothing_and_invites_nobody`), A-2.1-23 (ordre des participants),
A-2.14-18 (`OBJET_INTROUVABLE`), A-2.14-22 (fichier refusé en entier), A-2.14-23 (source `auto`), A-2.15-15 et 16
(liste selon le rôle, `purge_at = deleted_at + retention_days`).

## Routes du lot

| Méthode | Route 2.0 | Gestionnaire |
|---|---|---|
| PUT | `/api/projects/{id}/members` | `MemberApi.ReplaceAsync` (`ReplaceMembersCommand`) |
| GET | `/api/projects/{id}/participants` | `MemberApi.ParticipantsAsync` (`GetParticipantsQuery`) |
| DELETE | `/api/projects/{id}?confirmation=CODE` | `TrashApi.MoveToTrashAsync` (`MoveToTrashCommand`) |
| GET | `/api/projects/{id}/translations?scope=missing\|auto\|all` | `ProjectTranslationApi.ListAsync` (`GetTranslationsQuery`) |
| PUT | `/api/projects/{id}/translations` | `ProjectTranslationApi.ImportAsync` (`ImportTranslationsCommand`) |
| GET | `/api/trash` | `TrashApi.ListAsync` (`ListTrashQuery`) |
| POST | `/api/trash/{id}/restore` | `TrashApi.RestoreAsync` (`RestoreProjectCommand`) |
| DELETE | `/api/trash/{id}` | `TrashApi.PurgeAsync` (`PurgeProjectCommand`) |
