<!-- portage-sous-cartes-remontee.md -->
<!-- Portage P4-T08 des tests d'intégration 1.x SousCartesEtAnnulationTests, SousCarteVideTests et RemonteeTests vers Host.Tests -->

# Portage P4-T08 : sous-cartes, annulation, remontée (niveau Host.Tests)

Portée : les lignes de `portage-tests.md`, partie 1, sections `SousCartesEtAnnulationTests.cs` (T-SCAN),
`SousCarteVideTests.cs` (T-SCVI) et `RemonteeTests.cs` (T-REMO) dont la cible 2.0 nomme `Host.Tests` : 40 lignes
(T-REMO-06 n'a pas de cible `Host.Tests`). Chaque test cité est une théorie jouée sur les deux moteurs (SQLite en mode
Poste, SQL Server en mode Serveur avec `If-Match` envoyé quand la route le lit).

Préfixe des classes : `Topogenia.Host.Tests.`. Nouveaux fichiers : `tests/Topogenia.Host.Tests/Ported/`
(`SubMapsAndUndoCommandPortedTests.cs`, `SubMapsAndUndoPortedTests.cs`, `SubMapsAndUndoRestorePortedTests.cs`,
`DissolutionPortedTests.cs`, `DissolutionUndoPortedTests.cs`, aides `SubMapsAndUndoPortedCalls.cs` et
`DissolutionPortedPlan.cs`, ce dernier reprenant le plan `Preparer` du 1.x par les routes).

## Règles du portage

- La restauration d'un lot (`POST /api/suppressions/{lot}/restaurer`) et l'annulation d'une remontée
  (`/api/remontees/{id}/annuler`) sont portées vers `POST /api/maps/{id}/undo` (AR-43). L'undo étant une pile par
  carte et par utilisateur, quand le scénario 1.x intercale une modification plus récente entre la suppression et sa
  restauration, cette modification est faite par un second animateur (l'administrateur, membre de chaque projet de
  test) : T-SCAN-16, 17, 18, 22, T-REMO-10.
- Le « retour au reliquat » (`from_step_id`, ex-`depuis`) existe toujours en 2.0 ; il est gardé et complété par l'undo
  serveur de l'affectation (T-REMO-02, T-REMO-08).
- Un animateur qui n'est pas membre du projet (T-SCAN-13, T-SCAN-23) est l'utilisateur « étranger » des tests, rendu
  animateur d'un autre projet créé pour l'occasion.

## Correspondance

| N° | Test 1.x | Test 2.0 qui le prouve (classe.méthode, niveau) | Remarque |
|---|---|---|---|
| T-SCAN-01 | `CommandeApiTests.Codes_complets_courts_et_libelle` | `Ported.SubMapsAndUndoCommandPortedTests.The_command_takes_full_and_short_codes_and_a_label_after_the_colon_and_is_undone_at_once` (Host) | Nouveau : codes courts `e2`, `E3` et libellé ; la commande est annulée d'un coup (4.6). `Steps.CommandBarRoutesTests` ne jouait que des codes complets |
| T-SCAN-02 | `CommandeApiTests.Tout_ou_rien_avec_numeros_de_ligne` | `Ported.SubMapsAndUndoCommandPortedTests.A_command_with_errors_creates_nothing_and_answers_every_error_by_line_number` (Host) | Nouveau : deux erreurs (ETAPE_INCONNUE ligne 2, COMMANDE_SYNTAXE ligne 3) ; le test existant n'en avait qu'une |
| T-SCAN-03 | `CommandeApiTests.Doublons_existants_et_internes_signales` | `Ported.SubMapsAndUndoCommandPortedTests.Duplicates_of_an_existing_flow_and_inside_the_command_are_reported_on_their_lines` (Host) | Nouveau |
| T-SCAN-04 | `CommandeApiTests.Etape_d_une_autre_carte_refusee` | `Ported.SubMapsAndUndoCommandPortedTests.A_step_of_another_map_is_outside_the_map_and_a_step_of_another_project_is_unknown` (Host) | Nouveau, avec le point 5.6-2 : le code d'un autre projet donne ETAPE_INCONNUE |
| T-SCAN-05 | `CommandeApiTests.Commande_vide_et_lecteur_refuses` | `Ported.SubMapsAndUndoCommandPortedTests.An_empty_command_and_a_command_of_a_reader_are_refused` (Host) | Nouveau : COMMANDE_VIDE et lecteur 403 ; contributeur et étranger restent dans `Steps.CommandBarRoutesTests.A_contributor_runs_no_command_and_a_stranger_is_told_not_found` |
| T-SCAN-06 | `SousCartesTests.Detailler_reprend_le_couloir_parent_et_construit_l_ariane` | `Ported.SubMapsAndUndoPortedTests.Detailing_copies_the_lane_of_the_step_and_builds_the_breadcrumb_and_the_paths` (Host) | Nouveau : nom, couleur, traduction et code neuf du couloir copié, profondeur, fil d'Ariane, chemins 2.1 et 2.2 |
| T-SCAN-07 | `SousCartesTests.Une_seule_sous_carte_et_pas_de_debut_ni_fin` | `Steps.StepActionRoutesTests.Detailing_checks_the_version_of_the_step_and_refuses_a_start_a_filled_sub_map_and_a_contributor` ; `Steps.StepRoutesTests.A_macro_step_needs_a_choice_and_its_sub_map_is_reattached_to_the_step_named_by_to` (Host) | Déjà couvert : ETAPE_DEJA_DETAILLEE, début ETAPE_NON_DETAILLABLE, contributeur 403 ; passage en fin d'une étape détaillée ETAPE_NON_DETAILLABLE |
| T-SCAN-08 | `SousCartesTests.Avertissement_au_dela_de_quatre_niveaux` | `Ported.SubMapsAndUndoPortedTests.Detailing_beyond_four_levels_warns_without_refusing` (Host) | Nouveau : l'avertissement arrive exactement au niveau 5, profondeur 5, conseillée 4, fil d'Ariane de 5 ; `Steps.StepActionRoutesTests.A_detail_deeper_than_the_recommended_depth_is_answered_with_its_warning` ne vérifiait ni le niveau 5 seul ni la profondeur |
| T-SCAN-09 | `SousCartesTests.Supprimer_une_macro_etape_exige_un_choix` | `Steps.StepRoutesTests.A_macro_step_needs_a_choice_and_its_sub_map_is_reattached_to_the_step_named_by_to` ; `Selection.DeletionAndPasteRoutesTests.A_filled_sub_map_asks_for_the_delete_choice_whose_code_is_checked_and_which_takes_the_sub_map` (Host) | Déjà couvert : MACRO_CHOIX_REQUIS (suppression simple et groupée), RATTACHEMENT_INVALIDE sans cible, rien n'est supprimé |
| T-SCAN-10 | `SousCartesTests.Supprimer_une_macro_etape_avec_toute_sa_descendance` | `Ported.SubMapsAndUndoPortedTests.Deleting_a_macro_step_with_the_delete_choice_takes_its_whole_descent` (Host) | Nouveau : trois niveaux ; le test existant `Steps.StepRoutesTests.A_macro_step_deleted_with_the_delete_choice_takes_its_sub_map_with_it` n'en avait que deux |
| T-SCAN-11 | `SousCartesTests.Supprimer_une_macro_etape_en_rattachant_sa_carte` | `Steps.StepRoutesTests.A_macro_step_needs_a_choice_and_its_sub_map_is_reattached_to_the_step_named_by_to` (Host) | Déjà couvert : la sous-carte passe à l'héritière, `parent_step_id` suit |
| T-SCAN-12 | `SousCartesTests.Rattachement_interdit_dans_sa_propre_descendance` | `Ported.SubMapsAndUndoPortedTests.A_sub_map_is_not_reattached_inside_its_own_descent_the_root_map_never_and_never_to_another_project` (Host) | Nouveau : RATTACHEMENT_INVALIDE (descendance, autre projet), CARTE_RACINE |
| T-SCAN-13 | `SousCartesTests.Carte_d_un_autre_projet_introuvable` | `Ported.SubMapsAndUndoPortedTests.A_map_of_another_project_is_not_found_for_a_facilitator_who_is_no_member_of_it` (Host) | Nouveau : 404 avant 428 en mode Serveur, sous-carte et racine |
| T-SCAN-14 | `SousCartesTests.La_carte_racine_de_son_projet_ne_se_supprime_pas` | `Ported.SubMapsAndUndoPortedTests.The_root_map_of_its_project_is_not_deleted` (Host) | Nouveau |
| T-SCAN-15 | `RestaurationTests.Etape_et_liens_reviennent_avec_le_meme_code` | `Ported.SubMapsAndUndoRestorePortedTests.A_deleted_step_comes_back_with_its_identifier_its_code_its_column_and_its_labelled_flow_once` (Host) | Remplacé par undo serveur ; « une seule fois » : l'opération quitte la pile d'annulation pour la pile de rétablissement (plus de LOT_INTROUVABLE, AR-43) |
| T-SCAN-16 | `RestaurationTests.Case_reprise_entre_temps_l_intruse_est_decalee` | `Ported.SubMapsAndUndoRestorePortedTests.A_step_put_meanwhile_on_the_original_cell_is_shifted_by_the_undo` (Host) | Remplacé par undo serveur ; l'intruse est posée par un second animateur |
| T-SCAN-17 | `RestaurationTests.Un_lien_dont_l_extremite_a_disparu_reste_supprime` | `Ported.SubMapsAndUndoRestorePortedTests.A_flow_whose_end_disappeared_meanwhile_stays_deleted` (Host) | Remplacé par undo serveur ; l'extrémité est supprimée par un second animateur, l'undo réussit sans rendre la flèche |
| T-SCAN-18 | `RestaurationTests.Un_lien_recree_entre_temps_n_est_pas_double` | `Ported.SubMapsAndUndoRestorePortedTests.A_flow_drawn_again_meanwhile_is_not_doubled_by_the_undo` (Host) | Remplacé par undo serveur ; flèche redessinée par un second animateur ; partie persistance : `Porting.ModelPortedTests.Undoing_a_flow_deletion_after_the_flow_was_drawn_again_never_doubles_it` |
| T-SCAN-19 | `RestaurationTests.Annuler_un_detailler_puis_le_retablir` | `Ported.SubMapsAndUndoRestorePortedTests.A_deleted_filled_sub_map_comes_back_with_its_content` (Host) | Remplacé par undo serveur : « Supprimer le détail » (`supprimer_detail`) s'annule sur la carte de l'étape parente |
| T-SCAN-20 | `RestaurationTests.Restaurer_une_macro_etape_et_sa_descendance` | `Ported.SubMapsAndUndoRestorePortedTests.A_macro_step_deleted_with_its_descent_comes_back_with_its_sub_map_and_its_steps` (Host) | Remplacé par undo serveur |
| T-SCAN-21 | `RestaurationTests.Couloir_supprime_avec_ses_etapes_revient_entier` | `Ported.SubMapsAndUndoRestorePortedTests.A_lane_deleted_with_its_steps_comes_back_whole_at_its_rank_and_in_its_columns` (Host) | Remplacé par undo serveur : même identifiant, même rang, colonnes 0 et 3 |
| T-SCAN-22 | `RestaurationTests.Etape_d_une_carte_supprimee_depuis_n_est_pas_restaurable` | `Ported.SubMapsAndUndoRestorePortedTests.A_step_whose_map_was_deleted_since_is_not_brought_back` (Host) | Remplacé par undo serveur ; la sous-carte est supprimée par un second animateur ; refus 409 CARTE_MODIFIEE (LOT_NON_RESTAURABLE n'est plus levé, AR-43), rien ne revient |
| T-SCAN-23 | `RestaurationTests.Restauration_reservee_a_l_animateur_du_projet` | `Ported.SubMapsAndUndoRestorePortedTests.The_undo_is_for_a_facilitator_of_the_project_and_takes_back_only_his_own_operations` (Host) | Remplacé par undo serveur : contributeur 403, animateur d'un autre projet 404, autre animateur du projet 404 (pile vide : l'undo est réservé à l'auteur) ; plus de LOT_INTROUVABLE (AR-43) |
| T-SCAN-24 | `RestaurationTests.Suppression_groupee_etapes_et_liens_en_un_lot` | `Ported.SubMapsAndUndoRestorePortedTests.A_group_deletion_of_steps_and_flows_is_one_operation_undone_at_once` (Host) | Remplacé par undo serveur : étapes et flèches en une opération, défaite d'un seul undo ; `Selection.DeletionAndPasteRoutesTests.A_deleted_selection_is_stitched_answered_with_its_steps_and_undone_by_the_map` ne supprimait pas de flèche |
| T-SCVI-01 | `Vide_tant_qu_aucune_etape_n_y_est_ajoutee` | `Ported.SubMapsAndUndoPortedTests.A_sub_map_stays_empty_until_a_step_is_added_a_lane_is_not_enough` (Host) | Nouveau : `sub_map_empty` suit couloir ajouté, étape ajoutée puis supprimée |
| T-SCVI-02 | `Detailler_de_nouveau_rouvre_la_meme_carte_vide` | `Steps.StepActionRoutesTests.Detailing_a_step_answers_its_sub_map_and_detailing_it_again_while_empty_warns_and_logs_nothing` ; `Steps.StepActionRoutesTests.Detailing_checks_the_version_of_the_step_and_refuses_a_start_a_filled_sub_map_and_a_contributor` (Host) | Déjà couvert : même sous-carte, SOUS_CARTE_EXISTANTE au second appel seulement, ETAPE_DEJA_DETAILLEE une fois remplie |
| T-SCVI-03 | `Suppression_sans_question_et_annulable` | `Ported.SubMapsAndUndoPortedTests.A_step_with_an_empty_sub_map_is_deleted_without_question_and_the_undo_gives_it_back_with_its_sub_map` (Host) | Remplacé par undo serveur : deux undo, la dernière suppression d'abord, chaque étape revient avec sa sous-carte vide |
| T-SCVI-04 | `Changer_en_debut_ou_fin_retire_une_carte_vide_mais_pas_une_remplie` | `Ported.SubMapsAndUndoPortedTests.Becoming_a_start_or_an_end_removes_an_empty_sub_map_but_is_refused_with_a_filled_one` (Host) | Nouveau : la sous-carte vide disparaît, le début est refusé sur une remplie, la décision reste permise |
| T-REMO-01 | `Les_etapes_du_detail_prennent_la_place_de_l_etape_detaillee_avec_ses_fleches` | `Ported.DissolutionPortedTests.The_detail_steps_take_the_place_of_the_detailed_step_with_its_flows_and_a_frame_opens_with_the_remainder` ; `Ported.DissolutionPortedTests.The_sheet_of_the_remainder_is_seen_as_a_draft_by_the_facilitator_and_as_validated_only_by_a_reader` (Host) | Nouveau : colonnes, couloir créé, flèches et libellé reportés, codes gardés, détail parti, cadre (étapes, document et ses utilisateurs, irritant, `sheet_id`). Fiche du reliquat et point 5.6-1 : lue avec la carte (`dissolution_sheet`), brouillon pour l'animateur, dernière version validée seule pour le lecteur, qui ne reçoit jamais le texte du brouillon. Défaut relevé au portage (fiche du reliquat illisible) corrigé dans le produit par le pilote |
| T-REMO-02 | `Le_cadre_disparait_quand_le_reliquat_est_ventile_et_les_usages_suivent` | `Ported.DissolutionPortedTests.The_frame_closes_when_the_remainder_is_assigned_the_uses_follow_and_bringing_back_reopens_it` (Host) | Nouveau : cible d'une autre carte ETAPE_HORS_CARTE, fermeture du cadre, la flèche porte le document, retour au reliquat qui rouvre le cadre, undo serveur de ce retour |
| T-REMO-03 | `Terminer_range_le_reste_et_la_derniere_etape_recoit_tout` | `Ported.DissolutionPortedTests.Finish_stores_the_rest_on_the_last_step_of_the_frame_closes_it_does_nothing_twice_and_is_undone` (Host) | Nouveau : tout va à la seule étape restante ; « Terminer » annulable (4.6) ; complète `Dissolutions.RemainderRoutesTests.Finishing_answers_the_arrangements_closes_the_frame_and_a_second_finish_answers_none` qui ne vérifiait pas l'étape d'arrivée |
| T-REMO-04 | `Annuler_la_remontee_remet_le_detail_et_l_etape_detaillee` | `Ported.DissolutionUndoPortedTests.Undoing_the_dissolution_gives_back_the_detailed_step_its_flows_its_detail_and_the_lanes_and_it_can_be_dissolved_again` (Host) | Remplacé par undo serveur : colonnes du flux remises (dette 6.3), couloir créé retiré, libellé de flèche, détail intact, nouvelle remontée possible |
| T-REMO-05 | `Refus_un_seul_cadre_par_carte_pas_de_detail_dans_le_cadre_frequences_et_droits` | `Dissolutions.DissolveRoutesTests.A_dissolution_is_hidden_from_a_stranger_refused_to_a_contributor_and_an_open_frame_is_not_grouped` ; `Dissolutions.DissolveRoutesTests.A_dissolution_checks_the_version_read_of_the_step_and_needs_a_detail` ; `Ported.DissolutionPortedTests.A_detail_is_not_dissolved_next_to_a_flow_of_another_frequency` (Host) | Droits, DETAIL_ABSENT et REMONTEE_EN_COURS déjà couverts ; FREQUENCES_MELANGEES ajouté |
| T-REMO-07 | `Securite_on_ne_ramene_dans_le_reliquat_que_ce_qui_est_sur_la_carte` | `Ported.DissolutionPortedTests.Only_what_is_on_the_map_is_brought_back_to_the_remainder` (Host) | Nouveau : ETAPE_HORS_CARTE, INTROUVABLE, rien ne bouge dans l'autre projet |
| T-REMO-08 | `Un_usage_ne_se_pose_pas_sur_une_etape_qui_le_recoit_deja_et_l_annulation_le_rend` | `Ported.DissolutionPortedTests.A_usage_does_not_go_to_a_step_that_already_receives_it_and_its_return_gives_back_only_the_usage_moved` (Host) | Nouveau : USAGE_EXISTANT, DOCUMENT_UTILISE_PAR_CIBLE, retour de l'usage déplacé seul, puis undo serveur de ce retour |
| T-REMO-09 | `On_ne_remonte_pas_par_dessus_un_cadre_ouvert_plus_bas` | `Ported.DissolutionPortedTests.A_detail_is_not_dissolved_over_a_frame_open_below` (Host) | Nouveau : REMONTEE_EN_COURS_DESSOUS |
| T-REMO-10 | `Annulation_refusee_proprement_si_une_fleche_relie_le_cadre_au_flux_et_couloirs_crees_a_la_corbeille` | `Ported.DissolutionUndoPortedTests.The_undo_of_a_dissolution_is_refused_as_map_modified_when_a_flow_links_the_frame_to_the_flow` ; `Ported.DissolutionUndoPortedTests.A_step_put_then_deleted_in_the_lane_created_by_the_dissolution_does_not_block_its_undo` ; `Ported.DissolutionUndoPortedTests.The_undo_of_a_dissolution_is_outdated_when_a_step_it_touched_changed_since_and_names_the_author` (Host) | Remplacé par undo serveur : CARTE_MODIFIEE et piles vidées (AR-05) ; étape passagère dans le couloir créé sans effet ; OPERATION_DEPASSEE avec auteur et heure (4.6). Le refus vidant les piles, les deux volets du 1.x sont joués sur deux plans distincts |
| T-REMO-11 | `Les_roles_RACI_suivent_meme_sur_un_couloir_sans_etape` | `Ported.DissolutionPortedTests.The_raci_roles_follow_even_on_a_lane_without_step` (Host) | Nouveau |
| T-REMO-12 | `Le_lot_d_une_remontee_ne_se_restaure_pas_seul_et_pas_d_affectation_a_une_fin` | `Ported.DissolutionPortedTests.The_batch_of_a_dissolution_is_not_restored_alone_and_nothing_is_assigned_to_an_end` (Host) ; `Topogenia.Domain.Tests.Dissolutions.DissolutionUndoTests.Undo_is_refused_when_the_batch_of_the_dissolution_was_restored` (Domain) | HTTP : la réponse n'expose plus de lot, la route 1.x de restauration n'existe plus (404), ETAPE_SANS_CONTENU vers une fin. La condition interne LOT_REMONTEE devenue CARTE_MODIFIEE (AR-43) n'est atteignable par aucune route (rien ne restaure un lot seul) : prouvée au niveau domaine |
| T-REMO-13 | `Sans_reliquat_le_cadre_reste_jusqu_a_ce_qu_on_le_ferme` | `Ported.DissolutionUndoPortedTests.Without_remainder_the_frame_stays_until_it_is_closed_and_the_dissolution_is_still_undone` (Host) | Remplacé par undo serveur ; plan construit sans document ni irritant (plus de SQL direct) ; la fermeture du cadre vide n'est pas inscrite (`operation_id` nul) |

## Bilan

- Lignes dans la portée : 40 (T-SCAN 24, T-SCVI 4, T-REMO 12).
- Déjà prouvées par un test Host existant : 4 (T-SCAN-07, T-SCAN-09, T-SCAN-11, T-SCVI-02).
- Prouvées par les nouveaux tests : 36 lignes, 39 méthodes (78 cas sur les deux moteurs).
- Renvoyées à un autre niveau : une part de T-REMO-12 (condition interne du lot, domaine). Aucune ligne ne dépend du
  `.pmap` (phase 5).

## Défaut produit relevé, corrigé

Au portage, la fiche de l'étape remontée (reliquat d'un cadre ouvert) n'était lisible par aucune route : `GET
/api/steps/{étape remontée}/sheet` répond 404 INTROUVABLE (A-2.10-44, inchangé) et le cadre n'exposait que `sheet_id`.
Le pilote a ajouté à la vue de carte le champ `dissolution_sheet` (`SheetVisibility.ForRemainder`), nul sans cadre
ouvert. Le test de la partie fiche de T-REMO-01 a été rétabli et passe sur les deux moteurs.

## Complément (porte P4, round 1) : tests web 1.x de cible Host.Tests portés en undo serveur

Cinq lignes de `portage-tests.md` (`carte/geometrie.test.ts`, instantanés de positions, et `carte/historique.test.ts`,
pile d'annulation) ont pour cible « Application.Tests, Host.Tests » et n'avaient pas de test Host nommé. Les instantanés
du client et sa pile sont remplacés par le journal des opérations du serveur (AR-48, 4.6) : chaque scénario 1.x est joué
par `PUT /api/maps/{id}/layout`, `undo` et `redo`, sur les deux moteurs. Classe : `Ported.UndoHistoryPortedTests`
(`tests/Topogenia.Host.Tests/Ported/UndoHistoryPortedTests.cs`).

| N° | Test 1.x | Test 2.0 qui le prouve (classe.méthode, niveau) | Remarque |
|---|---|---|---|
| T-WGEO-27 | `rien a refaire quand rien n a bouge` | `Ported.UndoHistoryPortedTests.A_layout_to_the_positions_already_held_writes_no_operation_and_leaves_nothing_new_to_undo` (Host) | Remplacé par undo serveur : une disposition aux positions déjà tenues n'écrit rien (carte et historique identiques) ; l'undo suivant reprend l'opération d'avant |
| T-WGEO-28 | `retrouve les colonnes et l ordre des couloirs` | `Ported.UndoHistoryPortedTests.The_undo_of_a_layout_brings_back_the_columns_and_the_order_of_the_lanes_and_the_redo_takes_them_again` (Host) | Remplacé par undo serveur : ordre des couloirs et cases rendus par l'undo, repris par le redo |
| T-WGEO-29 | `ignore les etapes disparues et les couloirs disparus` | `Ported.UndoHistoryPortedTests.The_undo_of_a_layout_leaves_removed_the_steps_and_lanes_removed_since_and_puts_back_the_others` (Host) | Remplacé par undo serveur : une étape et un couloir que la disposition n'a pas déplacés, supprimés depuis par un autre animateur, restent supprimés ; le reste revient |
| T-WHIS-01 | `annule dans l ordre inverse et retablit` | `Ported.UndoHistoryPortedTests.The_undo_takes_back_the_operations_in_reverse_order_and_the_redo_replays_the_last_undone` (Host) | Remplacé par undo serveur : journal `-b`, `-a`, `+a`, une opération à annuler et une à rétablir |
| T-WHIS-04 | `pile vide : rien ne se passe` | `Ported.UndoHistoryPortedTests.An_undo_or_a_redo_on_an_empty_stack_is_not_found_and_changes_nothing` (Host) | Remplacé par undo serveur : pile vide = `INTROUVABLE` 404 (ADR 042), carte et historique inchangés |

Bilan du complément : 5 lignes, 5 méthodes (10 cas sur les deux moteurs). Avec ce complément, les 195 lignes de cible
`Host.Tests` de `portage-tests.md` recensées par la porte P4 sont toutes reprises dans les documents de portage.
