<!-- projects-profile.md -->
<!-- Couverture du lot C de P4-T02 : projets, membres, corbeille, profil et utilisateurs, maintenance, gabarit UserRunner -->

# Couverture P4-T02, lot C : projets, membres, corbeille, profil, maintenance

Préfixes des tests : `App` = `Topogenia.Application.Tests`, `Per` = `Topogenia.Persistence.Tests` (matrice SQLite et
SQL Server). Le niveau API (HTTP, codes 4xx, `Location`, en-têtes) relève de P4-T04 et P4-T08 : une ligne qui le cite
est notée `couvert (application)`.

## Annexe A, section 1 : routes

| ID | État | Test 2.0 | Remarque |
|---|---|---|---|
| A-1.2-01 | couvert (application) | `App.Profile.ProfileCommandsTests.The_profile_holds_the_roles_of_the_user_in_the_live_projects_only` ; `App.Users.UserRunnerTests.An_anonymous_request_is_refused_as_not_authenticated` ; `Per.Reading.ReadingStoreTests.The_profile_holds_the_memberships_of_the_live_projects_only` | `GetProfileQuery`, droit `RequiresAuthenticated` |
| A-1.2-02 | couvert (application) | `App.Profile.ProfileCommandsTests.An_invalid_change_of_the_profile_is_refused_and_writes_nothing` ; `App.Profile.ProfileCommandsTests.A_change_of_the_profile_is_written_with_the_next_version_and_the_new_effective_language` | `LANGUE_INVALIDE`, `TEXTE_HTML_INTERDIT` |
| A-1.2-03 | couvert (application) | `App.Profile.ProfileCommandsTests.An_invalid_change_of_the_profile_is_refused_and_writes_nothing` | `CHAMP_OBLIGATOIRE` (nom blanc) |
| A-1.2-04 | couvert (application) | `App.Profile.ProfileCommandsTests.A_display_name_longer_than_128_characters_is_refused` | |
| A-1.2-06 | couvert (application) | `App.Profile.ProfileCommandsTests.A_profile_changed_since_the_version_read_is_refused_and_a_change_without_version_is_not_checked` ; `Per.Reading.UserTemplatesStoreTests.The_profile_is_changed_with_its_version_and_a_stale_version_is_refused` | Version lue contrôlée (`CONFLIT_VERSION`) ; sans version, aucun contrôle (mode Poste, comme la 1.x). Le 428 `VERSION_REQUISE` du mode Serveur reste à l'hôte |
| A-1.2-07 | couvert (application) | `App.Projects.ProjectCommandsTests.A_member_lists_his_live_projects_sorted_by_code_with_his_role_and_the_counts` ; `App.Projects.ProjectCommandsTests.An_administrator_lists_every_live_project_as_facilitator` ; `Per.Reading.ReadingStoreTests.A_member_sees_his_live_projects_with_his_role_the_counts_and_the_translations_and_an_administrator_every_one` | |
| A-1.2-08 | couvert (application) | `App.Projects.ProjectCommandsTests.An_administrator_creates_the_project_its_root_map_first_lane_counters_and_members_in_one_unit` ; `Per.Reading.UserTemplatesStoreTests.An_administrator_creates_a_project_with_an_invited_user_in_one_unit_and_its_code_stays_taken` | Réponse `CreatedProject` (id, code, carte racine) ; 201 et `Location` à l'hôte |
| A-1.2-12 | couvert (application) | `App.Projects.ProjectCommandsTests.A_reader_gets_the_detail_with_the_members_in_display_order_the_version_and_the_root_map` | |
| A-1.2-13 | couvert (application) | `App.Members.MemberCommandsTests.The_whole_list_is_replaced_in_its_order_with_the_invited_users_and_nothing_is_logged` ; `App.Members.MemberCommandsTests.Only_a_facilitator_replaces_the_members_and_a_non_member_does_not_see_the_project` ; `Per.Reading.UserTemplatesStoreTests.The_members_are_replaced_with_their_invited_users_and_a_refused_list_creates_nobody` | |
| A-1.2-14 | couvert (application) | `App.Members.MemberCommandsTests.The_participants_are_read_by_a_reader_sponsors_first` | |
| A-1.2-23 | couvert (application) | `App.Trash.TrashCommandsTests.The_exact_code_moves_the_project_to_the_trash_and_nothing_is_logged` ; `App.Trash.TrashCommandsTests.Another_confirmation_is_refused_with_the_code_and_the_project_stays_live` | |
| A-1.2-26 | couvert (application) | `App.Trash.TrashCommandsTests.The_trash_lists_the_projects_the_user_facilitated_with_their_purge_date_and_every_one_for_an_administrator` ; `Per.Reading.ReadingStoreTests.The_trash_shows_the_projects_the_user_facilitated_with_who_deleted_them_and_every_one_to_an_administrator` | |
| A-1.2-27 | couvert (application) | `App.Trash.TrashCommandsTests.A_facilitator_of_the_deleted_project_or_an_administrator_restores_it_and_nobody_else` | |
| A-1.2-29 | couvert (application) | `App.Trash.TrashCommandsTests.An_administrator_purges_a_project_of_the_trash_at_once_and_never_a_live_one` ; `App.Trash.TrashCommandsTests.A_facilitator_may_not_purge_his_project_of_the_trash` ; `Per.Reading.UserTemplatesStoreTests.A_project_goes_to_the_trash_comes_back_and_is_purged_at_once_by_an_administrator` | |

## Annexe A, section 2.1 : projets, membres, utilisateurs

| ID | État | Test 2.0 | Remarque |
|---|---|---|---|
| A-2.1-02 | couvert (application) | `App.Projects.ProjectCommandsTests.The_code_of_a_live_project_or_of_one_in_the_trash_is_taken_and_no_invited_user_is_created` ; `Per.Reading.UserTemplatesStoreTests.An_administrator_creates_a_project_with_an_invited_user_in_one_unit_and_its_code_stays_taken` | |
| A-2.1-03 | couvert (application) | mêmes tests ; `Per.Reading.ReadingStoreTests.The_codes_in_use_name_the_live_projects_and_those_of_the_trash` | |
| A-2.1-05 | couvert (application) | `App.Projects.ProjectCommandsTests.Only_an_administrator_creates_a_project_and_a_refusal_writes_nothing` ; `App.Users.UserRunnerTests.An_administrator_request_is_refused_to_anyone_else_before_anything_is_read` | Refus avant toute lecture |
| A-2.1-06 | couvert | `App.Projects.ProjectCommandsTests.Without_a_language_of_origin_the_effective_language_of_the_creator_applies` | |
| A-2.1-07 | couvert | `App.Projects.ProjectCommandsTests.An_administrator_creates_the_project_its_root_map_first_lane_counters_and_members_in_one_unit` | |
| A-2.1-14 | couvert | `App.Members.MemberCommandsTests.The_whole_list_is_replaced_in_its_order_with_the_invited_users_and_nothing_is_logged` | |
| A-2.1-19 | couvert | `App.Members.MemberCommandsTests.A_person_who_leaves_no_longer_holds_a_lane_nor_a_step` | |
| A-2.1-20 | couvert | `App.Members.MemberCommandsTests.A_refused_list_leaves_the_members_as_they_were_and_creates_no_invited_user` ; `Per.Reading.UserTemplatesStoreTests.The_members_are_replaced_with_their_invited_users_and_a_refused_list_creates_nobody` | |
| A-2.1-22 | couvert | `App.Members.MemberCommandsTests.A_refused_list_leaves_the_members_as_they_were_and_creates_no_invited_user` ; `App.Members.MemberCommandsTests.A_unit_that_lost_the_creation_of_an_invited_user_runs_again_and_takes_the_user_created_since` ; `App.Projects.ProjectCommandsTests.An_invalid_member_list_refuses_the_creation_before_anything_is_written` ; `Per.Reading.UserTemplatesStoreTests.The_members_are_replaced_with_their_invited_users_and_a_refused_list_creates_nobody` ; `Per.Reading.UserTemplatesStoreTests.Two_projects_that_invite_one_new_login_through_their_members_at_once_name_one_user` | Invités créés dans l'unité des membres ; course sur la clé naturelle rejouée (ADR 033) |
| A-2.1-23 | couvert | `App.Members.MemberCommandsTests.The_participants_are_read_by_a_reader_sponsors_first` ; `App.Projects.ProjectCommandsTests.A_reader_gets_the_detail_with_the_members_in_display_order_the_version_and_the_root_map` | |
| A-2.1-24 | couvert (application) | `App.Users.UserRunnerTests.The_first_request_of_a_login_creates_the_user_from_the_directory_and_the_next_ones_only_read_it` ; `Per.Reading.UserTemplatesStoreTests.The_first_sign_in_creates_the_user_the_next_ones_only_read_it_and_the_administrator_flag_follows_the_configuration` ; `Per.Reading.UserTemplatesStoreTests.Two_first_requests_of_one_login_at_the_same_time_both_sign_in_on_one_user` | `UserRunner.SignInAsync`. L'événement `audit_event` reste à P4-T07 (le domaine n'en construit aucun) |
| A-2.1-25 | couvert | `App.Users.UserRunnerTests.The_first_request_of_a_login_creates_the_user_from_the_directory_and_the_next_ones_only_read_it` ; `App.Users.UserRunnerTests.A_login_the_directory_does_not_know_takes_its_short_name_and_an_invalid_login_is_refused` ; `App.Members.MemberCommandsTests.The_whole_list_is_replaced_in_its_order_with_the_invited_users_and_nothing_is_logged` | Port `IUserDirectory` défini ; son implémentation (domaine, comptes locaux) revient à l'hôte |
| A-2.1-26 | couvert (application) | `App.Users.UserRunnerTests.The_administrator_flag_follows_the_configuration_both_ways_at_each_sign_in` ; `Per.Reading.UserTemplatesStoreTests.The_first_sign_in_creates_the_user_the_next_ones_only_read_it_and_the_administrator_flag_follows_the_configuration` | Port `IAdministrators` ; sa relecture au démarrage et à chaque changement du fichier revient à l'hôte |
| A-2.1-27 | couvert (application) | `App.Users.UserRunnerTests.The_language_of_the_profile_wins_over_the_browser_and_the_browser_over_the_default` | Langue effective rendue par `SignInAsync` |

## Annexe A, sections 2.15 et 2.18

| ID | État | Test 2.0 | Remarque |
|---|---|---|---|
| A-2.15-13 | couvert (application) | `App.Trash.TrashCommandsTests.The_exact_code_moves_the_project_to_the_trash_and_nothing_is_logged` ; `App.Trash.TrashCommandsTests.Another_confirmation_is_refused_with_the_code_and_the_project_stays_live` | |
| A-2.15-14 | couvert (application) | `App.Trash.TrashCommandsTests.A_project_in_the_trash_is_reached_by_nobody_but_the_trash` ; `App.Projects.ProjectCommandsTests.A_project_in_the_trash_is_out_of_reach_of_everyone_the_administrator_included` | Le niveau Sécurité (HTTP) reste à P4-T08 |
| A-2.15-15 | couvert | `App.Trash.TrashCommandsTests.The_trash_lists_the_projects_the_user_facilitated_with_their_purge_date_and_every_one_for_an_administrator` ; `Per.Reading.ReadingStoreTests.The_trash_shows_the_projects_the_user_facilitated_with_who_deleted_them_and_every_one_to_an_administrator` | |
| A-2.15-16 | couvert | `App.Trash.TrashCommandsTests.The_trash_lists_the_projects_the_user_facilitated_with_their_purge_date_and_every_one_for_an_administrator` ; `Per.Reading.UserTemplatesStoreTests.A_project_goes_to_the_trash_comes_back_and_is_purged_at_once_by_an_administrator` | |
| A-2.15-17 | couvert (application) | `App.Trash.TrashCommandsTests.A_facilitator_of_the_deleted_project_or_an_administrator_restores_it_and_nobody_else` ; `Per.Reading.UserTemplatesStoreTests.A_project_goes_to_the_trash_comes_back_and_is_purged_at_once_by_an_administrator` | |
| A-2.15-18 | couvert | `App.Trash.TrashCommandsTests.An_administrator_purges_a_project_of_the_trash_at_once_and_never_a_live_one` ; `Per.Reading.UserTemplatesStoreTests.A_project_goes_to_the_trash_comes_back_and_is_purged_at_once_by_an_administrator` | |
| A-2.15-20 | couvert | `App.Maintenance.ExpiredPurgeTests.A_project_in_the_trash_past_the_retention_goes_whole_and_an_expired_batch_goes_with_what_it_deleted` ; `App.Maintenance.ExpiredPurgeTests.Nothing_is_purged_before_the_retention` | |
| A-2.15-21 | couvert | `App.Maintenance.ExpiredPurgeTests.A_project_in_the_trash_past_the_retention_goes_whole_and_an_expired_batch_goes_with_what_it_deleted` ; `Per.Reading.UserTemplatesStoreTests.The_daily_purge_takes_a_project_of_the_trash_past_the_retention` | |
| A-2.15-22 | couvert | `App.Maintenance.ExpiredPurgeTests.A_project_in_the_trash_past_the_retention_goes_whole_and_an_expired_batch_goes_with_what_it_deleted` | Le cadre ouvert et les entrées du journal : domaine et persistance (déjà couverts) |
| A-2.15-24 | couvert (application) | `App.Maintenance.ExpiredPurgeTests.A_project_whose_purge_fails_is_reported_and_the_others_are_still_purged` | L'échec est rendu dans `PurgeReport` ; l'écrire au journal revient à l'hôte |
| A-2.18-01 | couvert | `App.Projects.ProjectCommandsTests.The_facilitator_changes_the_label_of_the_sharing_group_with_if_match_and_nothing_is_logged` ; `App.Profile.ProfileCommandsTests.A_change_of_the_profile_is_written_with_the_next_version_and_the_new_effective_language` | Trim du libellé du groupe et du nom affiché |
| A-2.18-02 | couvert | `App.Profile.ProfileCommandsTests.An_invalid_change_of_the_profile_is_refused_and_writes_nothing` | |

## Annexe B : écrans servis par ce lot, et opérations non annulables

| ID | État | Test 2.0 | Remarque |
|---|---|---|---|
| B-1.4-01 | couvert (application) | `App.Projects.ProjectCommandsTests.Only_an_administrator_creates_a_project_and_a_refusal_writes_nothing` | Partie API ; l'interface reste à la phase 6 |
| B-1.5-02 | couvert (application) | `App.Members.MemberCommandsTests.The_whole_list_is_replaced_in_its_order_with_the_invited_users_and_nothing_is_logged` | Partie API ; l'interface reste à la phase 6 |
| B-2.6-44 | couvert (application) | `App.Members.MemberCommandsTests.The_whole_list_is_replaced_in_its_order_with_the_invited_users_and_nothing_is_logged` ; `Per.Reading.UserTemplatesStoreTests.The_members_are_replaced_with_their_invited_users_and_a_refused_list_creates_nobody` | Aucune entrée au journal, aucune opération rendue |

Données servies aux écrans de l'annexe B (les lignes restent `à faire` : leur niveau est l'interface, phase 6) :
B-1.3-02 et B-1.3-24 (dernier projet ouvert : `RememberOpenedProjectCommand`, `ProfileView.LastProjectId`,
`App.Profile.ProfileCommandsTests.The_last_project_opened_is_remembered_for_a_reader_of_the_project_only`), B-1.3-12
(étapes, cartes, rôle : `ProjectListItem`), B-1.3-17 et B-1.3-18 (nom, code, étapes, supprimé le et par, purge :
`TrashItem`), B-1.3-23 (`TrashView.RetentionDays`), B-1.3-27 (`MemberView.IsSponsor`), B-1.5-01 (`MemberView.IsKeyContributor`),
B-1.8-01 et B-1.8-02 (`ProfileView`).

## Hors de ce lot

- A-1.2-05 (`VALEUR_INVALIDE` sur un type JSON), A-1.2-09 à 11 au niveau API, A-2.1-21 (réponse de succès), A-2.18-05
  à 07 : lecture du JSON par l'hôte (P4-T04).
- A-2.15-19 (purge au démarrage puis toutes les 24 h) : minuterie de l'hôte, qui appelle `Maintenance.ExpiredPurge.RunAsync`.
- Spec 5.5 : le domaine ne construit aucun `AuditEvent` ; connexion d'un nouvel utilisateur, changement de membres,
  création, suppression, restauration et purge d'un projet restent à tracer en P4-T07.
