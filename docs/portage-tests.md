<!-- portage-tests.md -->
<!-- Inventaire des tests 1.x et de leur cible de portage 2.0 -->

# Portage des tests 1.x vers Topogenia 2.0

Inventaire exhaustif des tests d'Ariane 1.x (`43_Outil_Collaboratif\carto`), produit en phase 0.
Chaque ligne correspond à une méthode xUnit (`[Fact]` ou `[Theory]`), à un `it(...)` vitest ou à un
`test(...)` Playwright. La colonne « Cas » compte les jeux `InlineData` d'une `[Theory]`.

## Conventions

- **Test 2.0 qui porte le cas** : pour les lignes de cible `Domain.Tests`, le ou les tests 2.0 du domaine qui
  portent l'intention du test 1.x sont donnés par `portage-domaine.md` (table `T-*` -> test 2.0, phase 2) ; les
  autres niveaux recevront leur renvoi dans leur phase.
- **Niveaux 2.0** : `Domain.Tests` (règle pure, sans base), `Application.Tests` (gestionnaires et
  autorisations, dépôts en mémoire), `Persistence.Tests` (index, stationnement, concurrence, sur
  SQLite et SQL Server), `Host.Tests` (HTTP de bout en bout, matrice SQLite x SQL Server),
  `Interchange.Tests` (.pmap, paquet `.topogenia`, migration 1.x), `web unit` (vitest), `e2e`
  (Playwright).
- **Annulation** : en 2.0 l'annulation est côté serveur (journal `operation_log`, spécification
  4.6). Tout test 1.x qui appelle `POST /api/suppressions/{lot}/restaurer` ou qui orchestre une
  annulation côté client est porté vers `undo` / `redo` serveur (remarque `remplacé par undo serveur`).
  La règle de restauration d'un lot (annexe A, 2.15) reste testée, mais comme mécanisme interne
  du journal : `CARTE_NON_VIDE`, `LOT_INTROUVABLE`, `LOT_REMONTEE` et `LOT_NON_RESTAURABLE` ne
  sont plus levés par l'API ; un undo ou un redo refusé ne renvoie que `OPERATION_DEPASSEE` ou
  `CARTE_MODIFIEE` (AR-43).
- **Déclencheurs, procédures, vues `bi`** : la règle est portée en `Domain.Tests` /
  `Application.Tests` ; la vue `bi` éventuelle en `Persistence.Tests`, SQL Server seulement
  (remarque `dépend de SQL Server`).
- **Concurrence optimiste** : en mode Serveur, `If-Match` devient obligatoire (5.6, point 3). Les
  tests 1.x qui écrivent sans version restent valides en mode Poste ; en mode Serveur, ils doivent
  fournir la version (remarque `If-Match obligatoire en mode Serveur`).
- **Remarque `défaut 1.x corrigé, inverser l'assertion`** : le test 1.x prouve un comportement que
  la spécification 2.0 corrige (sections 5.6 et 13) ; le test 2.0 affirme le contraire.
- Les noms 2.0 sont proposés en anglais (décision Q1), forme `Subject_Behaviour_Condition`.

## Partie 1 - Tests C# d'intégration (`api.Tests\Integration`)

Les fichiers `FabriqueApi.cs` (hôte de test, base `Ariane_Test` recréée via `master`,
authentification `X-Test-User`) et `Scenario.cs` (projet neuf avec animateur, contributeur,
lecteur, étranger) ne contiennent aucun test. Leur équivalent 2.0 est l'hôte de test paramétré
par fournisseur (SQLite, SQL Server) de `Host.Tests` ; la recréation de base passe par les
migrations EF Core et non plus par `master`.

### BranchesTests.cs

| N° | Test 1.x | Cas | Intention (ce que le test prouve, une phrase) | Cible 2.0 | Nom 2.0 proposé (anglais, PascalCase avec _ ) | Remarque |
|---|---|---|---|---|---|---|
| T-BRAN-01 | `En_parallele_meme_couloir_meme_colonne_rangee_suivante_et_memes_fleches` | 1 | L'insertion « en parallèle » pose l'étape dans le même couloir et la même colonne, à la rangée suivante, avec une copie des flèches entrantes et sortantes, et l'ordre de lecture descend la colonne. | Domain.Tests, Host.Tests | `InsertParallel_SameLaneSameColumnNextRow_CopiesFlows` | |
| T-BRAN-02 | `Le_decalage_RG_C04_ne_touche_que_la_rangee_et_les_cases_sont_distinctes` | 1 | Le décalage RG-C04 ne pousse que les étapes de la même rangée, la disposition groupée refuse deux étapes dans une case et la rangée est bornée à 50. | Domain.Tests, Application.Tests, Host.Tests | `ShiftRgC04_OnlyAffectsSameRow_AndLayoutRejectsSharedCell` | |
| T-BRAN-03 | `Changer_de_couloir_sans_preciser_la_rangee_ramene_a_la_premiere` | 1 | Un changement de couloir sans rangée explicite ramène l'étape à la rangée 0. | Application.Tests, Host.Tests | `MoveStepToOtherLane_WithoutRow_ResetsToFirstRow` | |
| T-BRAN-04 | `Losanges_parallele_et_inclusif_sans_fiche_detaillee_ni_carte_fille` | 1 | Les losanges parallèle et inclusif existent comme types d'étape, ne se détaillent pas et un type inconnu est refusé. | Domain.Tests, Host.Tests | `ParallelAndInclusiveGateways_CannotBeDetailed_UnknownTypeRejected` | |
| T-BRAN-05 | `Fichier_pmap_garde_les_rangees_et_les_losanges` | 1 | L'export puis l'import `.pmap` conservent les rangées et les types parallèle. | Interchange.Tests, Host.Tests | `PmapRoundTrip_KeepsRowsAndGateways` | |

### CorbeilleTests.cs

| N° | Test 1.x | Cas | Intention (ce que le test prouve, une phrase) | Cible 2.0 | Nom 2.0 proposé (anglais, PascalCase avec _ ) | Remarque |
|---|---|---|---|---|---|---|
| T-CORB-01 | `Suppression_confirmee_par_le_code_par_l_animateur_seulement` | 1 | Seul l'animateur met un projet à la corbeille, avec le code exact (sensible à la casse), et le projet devient invisible et inaccessible à tous, administrateur compris. | Application.Tests, Host.Tests | `TrashProject_RequiresExactCode_FacilitatorOnly_ThenHiddenFromEveryone` | |
| T-CORB-02 | `Corbeille_de_l_animateur_et_de_l_administrateur_restauration_et_code_reserve` | 1 | La corbeille est visible des animateurs et de l'administrateur avec la date de purge à 30 jours, le code reste réservé, la restauration est réservée à l'animateur et un projet vivant ne se restaure ni ne se purge. | Application.Tests, Host.Tests | `Trash_ListedForFacilitatorAndAdmin_CodeReserved_RestoreByFacilitator` | |
| T-CORB-03 | `Suppression_definitive_efface_tout_le_projet_et_seulement_lui` | 1 | La suppression définitive par l'administrateur efface tout le projet (traductions comprises) sans toucher un projet témoin, et libère le code. | Application.Tests, Persistence.Tests, Host.Tests | `PurgeProject_DeletesWholeProjectOnly_AndFreesCode` | `purge_project` est une procédure SQL Server en 1.x : réécrite en C#, testée sur les deux bases |
| T-CORB-04 | `Purge_au_dela_de_la_duree_de_conservation_projets_et_objets_supprimes` | 1 | La purge efface après 30 jours (et pas avant) les projets à la corbeille et les lots des projets vivants, sans toucher au reste ; le lot purgé n'est plus restaurable. | Application.Tests, Persistence.Tests, Host.Tests | `Purge_AfterRetention_RemovesTrashedProjectsAndBatches_ThenUndoRefused` | `purge_trash` réécrite en C# ; dernière assertion (`/api/suppressions/{lot}/restaurer` → `LOT_INTROUVABLE`) remplacée par undo serveur : la purge efface aussi les opérations du journal (4.6) ; `LOT_INTROUVABLE` n'est plus levé par l'API 2.0, un undo refusé ne renvoie que `OPERATION_DEPASSEE` ou `CARTE_MODIFIEE` (AR-43) |

### DocumentsFluxTests.cs

| N° | Test 1.x | Cas | Intention (ce que le test prouve, une phrase) | Cible 2.0 | Nom 2.0 proposé (anglais, PascalCase avec _ ) | Remarque |
|---|---|---|---|---|---|---|
| T-DOCF-01 | `Les_sorties_des_etapes_precedentes_tous_couloirs_sont_proposees_a_cocher_non_cochees` | 1 | Les sorties des prédécesseurs directs, tous couloirs confondus, sont proposées non cochées ; cocher crée l'usage avec son attente, compte dans la complétude, se voit chez le producteur et sur la flèche ; décocher l'annule. | Application.Tests, Host.Tests | `PredecessorOutputs_OfferedUnchecked_CheckCreatesUsageOnFlow` | |
| T-DOCF-02 | `Une_sortie_declaree_avant_la_fleche_est_proposee_des_que_la_fleche_existe` | 1 | Une sortie déclarée avant la création de la flèche est proposée dès que la flèche existe. | Application.Tests, Host.Tests | `OutputDeclaredBeforeFlow_OfferedOnceFlowExists` | |
| T-DOCF-03 | `Autre_les_sorties_des_etapes_lointaines_coches_elles_restent_marquees_comme_venant_d_ailleurs` | 1 | « Autre » liste les sorties des étapes non voisines ; cochées, elles restent marquées « d'ailleurs », y compris après suppression de la flèche, et redeviennent proches quand la flèche revient. | Application.Tests, Host.Tests | `OtherOutputs_CheckedRemainMarkedRemote_AndBecomeNearAfterFlowRestored` | la restauration de la flèche par `/api/suppressions/{lot}/restaurer` est remplacée par undo serveur |
| T-DOCF-04 | `Entree_exterieure_et_sortie_renommee_par_son_etape_seulement` | 1 | Une entrée extérieure porte son attente et n'est pas proposée ailleurs ; une sortie n'a pas d'attente ; seule l'étape productrice renomme le document, avec contrôle de version. | Domain.Tests, Application.Tests, Host.Tests | `ExternalInput_HasWait_OutputRenamedOnlyByProducer_WithVersionCheck` | If-Match obligatoire en mode Serveur |
| T-DOCF-05 | `Droits_cocher_dans_sa_fiche_produire_dans_la_sienne_lecture_pour_tous` | 1 | On coche dans une fiche qu'on peut remplir, on produit dans la sienne, tout membre lit ; cocher son propre document ou un document inconnu est introuvable, une attente négative refusée. | Application.Tests, Host.Tests | `DocumentUsage_Permissions_FillOwnSheet_ReadForAll` | |
| T-DOCF-06 | `Qualite_pct_ca_et_irritant_rattache_a_l_etape_qui_produit_le_document` | 1 | Le %C&A et l'irritant d'un usage sont rattachés à l'étape productrice, historisés, mis à jour sans doublon, effacés s'ils sont encore « reported » quand la description est vidée, conservés sinon au décochage ; bornes et HTML contrôlés. | Domain.Tests, Application.Tests, Host.Tests | `UsageQuality_PainPointOnProducer_ReportedDeletedOtherwiseDetached` | règle unique `RetirerIrritant`/`OterIrritant` (section 13) : vérifier en plus qu'un irritant déjà traité est détaché (`step_id` NULL, rattaché à la carte) ; la mise à jour du statut se fait aujourd'hui en SQL direct |
| T-DOCF-07 | `Document_supprime_par_son_etape_il_disparait_des_etapes_qui_l_utilisaient` | 1 | Supprimer un document supprime ses usages sur toutes les étapes et le retire de la flèche. | Application.Tests, Host.Tests | `DeleteDocument_RemovesAllUsagesAndFlowLabels` | |
| T-DOCF-08 | `Un_document_coche_par_l_etape_voisine_ne_provoque_pas_de_conflit_sur_la_fiche_en_cours` | 1 | Un usage posé par l'étape voisine ne change pas la version de la fiche du producteur (pas de conflit). | Application.Tests, Persistence.Tests, Host.Tests | `NeighbourUsage_DoesNotBumpProducerSheetVersion` | |
| T-DOCF-09 | `Lien_vers_le_document_url_ou_chemin` | 6 | L'adresse d'un document est classée URL http(s) ou chemin de fichier, et les adresses dangereuses ou mal formées sont refusées. | Domain.Tests, Host.Tests | `DocumentAddress_IsUrlOrPath_InvalidRejected` | |

### FichesTests.cs (intégration)

| N° | Test 1.x | Cas | Intention (ce que le test prouve, une phrase) | Cible 2.0 | Nom 2.0 proposé (anglais, PascalCase avec _ ) | Remarque |
|---|---|---|---|---|---|---|
| T-FICI-01 | `Fiche_vide_au_depart_et_droits_selon_le_role` | 1 | Une fiche neuve est vide, l'animateur a tous les droits, contributeur non assigné et lecteur aucun, l'étranger reçoit 404. | Application.Tests, Host.Tests | `NewSheet_IsEmpty_RightsDependOnRole` | |
| T-FICI-02 | `Le_contributeur_remplit_les_etapes_qui_lui_sont_assignees_ou_celles_de_son_couloir` | 1 | Un contributeur remplit seulement les étapes qui lui sont assignées ou celles du couloir dont il est l'acteur, et le responsable indique son origine. | Application.Tests, Host.Tests | `Contributor_FillsAssignedStepsOrOwnLane` | |
| T-FICI-03 | `Le_brouillon_reste_prive_jusqu_a_la_validation` | 1 | Le brouillon est visible de l'animateur mais pas du lecteur (fiche et résumé sur la carte) jusqu'à la validation, qui crée la version 1. | Application.Tests, Host.Tests | `Draft_HiddenFromReaders_UntilValidated` | |
| T-FICI-04 | `Une_fiche_validee_modifiee_repasse_en_brouillon_et_les_autres_gardent_la_version_validee` | 1 | Réenregistrer sans changement garde l'état validé ; une modification repasse en brouillon pendant que les lecteurs gardent la version validée ; une nouvelle validation crée la version 2. | Domain.Tests, Application.Tests, Host.Tests | `ValidatedSheet_Modified_BecomesDraft_ReadersKeepValidatedVersion` | |
| T-FICI-05 | `Completude_sur_sept_champs_l_acteur_prerempli_ne_compte_pas` | 1 | La complétude se calcule sur sept champs sans l'acteur, identique dans la vue de la carte et dans la vue `bi.step_sheet`. | Domain.Tests, Host.Tests, Persistence.Tests | `Completeness_SevenFields_ActorExcluded_SameInBiView` | la partie `bi.step_sheet` dépend de SQL Server (Persistence.Tests, SQL Server seulement) ; une seule implémentation de la complétude (dette 6.2) |
| T-FICI-06 | `Deroule_lignes_vides_retirees_et_saisies_refusees` | 1 | Le déroulé est nettoyé (trim, lignes vides retirées) et refuse nature inconnue, outil sans texte, plus de 50 lignes et HTML. | Domain.Tests, Host.Tests | `Procedure_TrimsEmptyLines_RejectsInvalidEntries` | |
| T-FICI-07 | `Vider_une_fiche_jamais_validee_la_remet_a_vide` | 1 | Une fiche vide n'est pas créée en base, une fiche jamais validée vidée redevient vide et une fiche vide ne se valide pas. | Domain.Tests, Application.Tests, Host.Tests | `ClearingNeverValidatedSheet_ResetsToEmpty_EmptyCannotBeValidated` | |
| T-FICI-08 | `Demande_de_precision_par_l_animateur_puis_reprise_par_l_auteur` | 1 | Seul l'animateur demande une précision, sur une fiche validée et avec un texte ; tous la voient ; la reprise par l'auteur repasse en brouillon et la validation l'efface. | Domain.Tests, Application.Tests, Host.Tests | `ReviewRequest_ByFacilitatorOnValidated_ClearedOnRevalidation` | |
| T-FICI-09 | `Modification_concurrente_signalee_avec_son_auteur` | 1 | Une version périmée sur la fiche ou sur la validation renvoie 409 `CONFLIT_VERSION` avec l'auteur de la modification. | Application.Tests, Persistence.Tests, Host.Tests | `SheetConcurrentEdit_Returns409WithAuthor` | jeton de concurrence portable (plus de `rowversion`) ; If-Match obligatoire en mode Serveur |
| T-FICI-10 | `Valeurs_refusees` | 7 | Clé inconnue ou obsolète, fréquence hors liste, durée négative, textuelle ou trop grande, nombre sur un champ texte sont refusés avec le nom du champ. | Domain.Tests, Host.Tests | `SheetField_InvalidValue_RejectedWithFieldName` | |
| T-FICI-11 | `Textes_controles_html_et_longueur` | 1 | HTML et longueurs (4 000 par champ, 20 000 pour le texte libre) sont refusés ; le texte libre seul est enregistré sans compter dans la complétude. | Domain.Tests, Host.Tests | `SheetTexts_HtmlAndLengthChecked_FreeTextNotCounted` | |
| T-FICI-12 | `Suggestions_frequence_de_l_etape_precedente_et_outils_de_la_carte_selon_ce_qui_est_visible` | 1 | Les suggestions (fréquence du prédécesseur, outils de la carte) ne montrent que ce que l'utilisateur a le droit de voir, et rien à qui ne remplit pas. | Application.Tests, Host.Tests | `Suggestions_RespectSheetVisibility` | |
| T-FICI-13 | `Une_etape_supprimee_n_a_plus_de_fiche_accessible` | 1 | La fiche d'une étape supprimée renvoie 404. | Application.Tests, Host.Tests | `DeletedStep_SheetNotFound` | |

### FichiersTests.cs

| N° | Test 1.x | Cas | Intention (ce que le test prouve, une phrase) | Cible 2.0 | Nom 2.0 proposé (anglais, PascalCase avec _ ) | Remarque |
|---|---|---|---|---|---|---|
| T-FICR-01 | `Ouvrir_l_exemple_du_guide_pour_IA` | 1 | L'exemple JSON embarqué dans `PMAP_GUIDE_IA.md` s'ouvre comme projet avec les compteurs attendus et son RACI. | Interchange.Tests, Host.Tests | `OpenPmap_AiGuideExample_CreatesProject` | guide mis à jour pour la version 1.1 (7.5) : compteurs à recaler |
| T-FICR-02 | `Ouvrir_l_exemple_complet_de_la_documentation` | 1 | L'exemple complet crée le projet avec nom, traduction, description, incertitudes, couloirs, types, couleurs, codes réattribués dans l'ordre de lecture, RACI et sous-carte ; l'importateur est seul animateur. | Interchange.Tests, Host.Tests | `OpenPmap_FullExample_AllContentImported_ImporterIsSoleFacilitator` | |
| T-FICR-03 | `Ouvrir_un_fichier_sans_colonnes_place_les_etapes_selon_les_fleches` | 1 | Un fichier sans colonnes est placé automatiquement selon les flèches, sans avertissement. | Interchange.Tests, Host.Tests | `OpenPmap_WithoutColumns_AutoPlacedFromFlows` | |
| T-FICR-04 | `Copie_telechargee_puis_rouverte_a_l_identique` | 1 | La copie téléchargée (tout membre, nom `CODE_nom_date.pmap`) omet les objets supprimés et sous-cartes vides, et un aller-retour export-import-export redonne le même fichier. | Interchange.Tests, Host.Tests | `PmapExport_ReopenedAndReexported_IsIdentical` | en 1.1, vérifier aussi les traductions d'étapes et de flèches (7.5) |
| T-FICR-05 | `Fichier_invalide_liste_des_problemes_et_rien_n_est_cree` | 1 | Un fichier invalide renvoie la liste localisée des problèmes (FR/EN, JSON illisible compris) sans rien créer, et un contenu trop gros est refusé. | Interchange.Tests, Host.Tests | `OpenPmap_Invalid_ListsProblems_CreatesNothing` | défaut 1.x corrigé, inverser l'assertion pour la taille : limite 10 Mo appliquée avant lecture du corps (5.4, 5.6 point 7) au lieu de 8 M caractères après lecture ; message et borne à changer |
| T-FICR-06 | `Code_et_nom_du_projet_a_l_ouverture` | 1 | Le code est exigé, celui du fichier est repris s'il est libre, un code pris est refusé sans rien créer, et un nom saisi l'emporte en faisant perdre la traduction du nom. | Application.Tests, Host.Tests | `OpenPmap_CodeAndName_EnteredWinsOverFile` | |
| T-FICR-07 | `Dupliquer_un_projet` | 1 | La duplication crée un projet distinct de même contenu, sans modifier la source. | Application.Tests, Host.Tests | `DuplicateProject_SameContent_SourceUnchanged` | |
| T-FICR-08 | `Droits_ouvrir_et_dupliquer_reserves_aux_administrateurs` | 1 | Ouvrir et dupliquer sont réservés à l'administrateur, la copie est permise à tout membre, un non-membre reçoit 404 et l'en-tête anti-CSRF est exigé. | Application.Tests, Host.Tests | `ImportAndDuplicate_AdminOnly_ExportForMembers` | contrôle anti-CSRF 2.0 : en-tête plus `Origin` / `Sec-Fetch-Site` (5.2, 5.3) |
| T-FICR-09 | `Lettres_RACI_du_parametre_appliquees_a_l_ouverture` | 1 | Les lettres RACI acceptées à l'ouverture et à l'export suivent le paramètre `raci_letters` (S refusé en ACI, accepté en ASCI). | Interchange.Tests, Host.Tests | `OpenPmap_RaciLetters_FollowSetting` | le paramètre est modifié en SQL direct : passer par la configuration 2.0 |

### FrequenceTests.cs

| N° | Test 1.x | Cas | Intention (ce que le test prouve, une phrase) | Cible 2.0 | Nom 2.0 proposé (anglais, PascalCase avec _ ) | Remarque |
|---|---|---|---|---|---|---|
| T-FREQ-01 | `Frequence_de_la_fiche_dans_la_carte_validee_pour_le_lecteur` | 1 | La fréquence de la fiche est renvoyée avec la carte : brouillon pour l'animateur, version validée seulement pour le lecteur. | Application.Tests, Host.Tests | `MapView_SheetFrequency_ReaderSeesValidatedOnly` | |

### InsertionTests.cs

| N° | Test 1.x | Cas | Intention (ce que le test prouve, une phrase) | Cible 2.0 | Nom 2.0 proposé (anglais, PascalCase avec _ ) | Remarque |
|---|---|---|---|---|---|---|
| T-INSE-01 | `Avant_B_dans_A_B_C_donne_A_X_B_C` | 1 | Insérer avant B coupe la flèche unique entrante (A>X>B), décale le flux et rend le lot et la flèche coupée. | Domain.Tests, Application.Tests, Host.Tests | `InsertBefore_SplitsSingleIncomingFlow` | le lot rendu sert en 1.x à l'annulation client ; en 2.0, l'opération est inscrite au journal |
| T-INSE-02 | `Apres_B_dans_A_B_C_donne_A_B_X_C` | 1 | Insérer après B coupe la flèche unique sortante (B>X>C). | Domain.Tests, Application.Tests, Host.Tests | `InsertAfter_SplitsSingleOutgoingFlow` | |
| T-INSE-03 | `Apres_une_decision_le_libelle_reste_sur_la_branche_de_la_decision` | 1 | Le libellé d'une branche de décision reste sur la flèche qui part de la décision, avant comme après. | Domain.Tests, Host.Tests | `InsertAroundDecision_LabelStaysOnDecisionBranch` | |
| T-INSE-04 | `Plusieurs_fleches_entrantes_aucune_n_est_coupee` | 1 | Avec plusieurs flèches entrantes, aucune n'est coupée et la nouvelle étape est seulement reliée. | Domain.Tests, Host.Tests | `InsertBefore_MultipleIncoming_NoFlowCut` | |
| T-INSE-05 | `Etape_isolee_reliee_a_la_nouvelle` | 1 | Une étape isolée est simplement reliée à l'étape insérée avant ou après. | Domain.Tests, Host.Tests | `InsertAroundIsolatedStep_LinksNewStep` | |
| T-INSE-06 | `Annulation_par_suppression_de_la_nouvelle_et_restauration_de_la_fleche` | 1 | L'insertion s'annule (nouvelle étape supprimée, flèche d'origine restaurée) puis se rétablit à l'identique. | Application.Tests, Host.Tests | `InsertStep_Undo_RestoresOriginalFlow_RedoReappliesInsertion` | remplacé par undo serveur |
| T-INSE-07 | `Relais_vers_le_couloir_du_dessous_et_du_dessus` | 1 | L'insertion « dessous » ou « dessus » pose un relais dans le couloir voisin, décale l'occupante et ajoute une flèche sans couper l'existante. | Domain.Tests, Application.Tests, Host.Tests | `InsertBelowOrAbove_AddsRelayInNeighbourLane` | |
| T-INSE-08 | `Pas_de_couloir_voisin_refuse` | 1 | Sans couloir voisin, l'insertion dessous ou dessus est refusée sans rien créer. | Domain.Tests, Host.Tests | `InsertBelowOrAbove_NoNeighbourLane_Rejected` | |
| T-INSE-09 | `Saisies_et_droits_controles` | 1 | Position invalide, libellé vide, contributeur et étape inconnue sont refusés sans modifier la carte. | Application.Tests, Host.Tests | `InsertStep_InvalidInputAndRights_Rejected` | |

### ModeleTests.cs

| N° | Test 1.x | Cas | Intention (ce que le test prouve, une phrase) | Cible 2.0 | Nom 2.0 proposé (anglais, PascalCase avec _ ) | Remarque |
|---|---|---|---|---|---|---|
| T-MODE-01 | `Un_lot_garde_qui_supprime_et_qui_restaure_et_ne_se_restaure_qu_une_fois` | 1 | Un lot de suppression garde qui supprime et qui restaure, et ne se restaure qu'une fois. | Application.Tests, Persistence.Tests, Host.Tests | `DeletionBatch_RecordsWhoDeletedAndRestored_RestoredOnlyOnce` | remplacé par undo serveur : undo restaure le lot, un second undo de la même opération est impossible ; l'audit qui/quand reste testé en Persistence |
| T-MODE-02 | `Une_operation_qui_ne_supprime_rien_ne_laisse_pas_de_lot` | 1 | Une opération de structure qui ne supprime rien ne crée pas de lot de suppression. | Application.Tests, Persistence.Tests | `OperationWithoutDeletion_CreatesNoBatch` | |
| T-MODE-03 | `Detailler_la_selection_garde_les_roles_raci_sur_les_couloirs_copies` | 1 | « Détailler la sélection » reporte les rôles RACI sur les couloirs copiés dans la carte détaillée, mais pas ceux des couloirs non occupés par le bloc. | Domain.Tests, Application.Tests, Host.Tests | `DetailSelection_KeepsRaciOnCopiedLanes_DropsUnoccupiedLanes` | |
| T-MODE-04 | `Debut_fin_et_portes_n_ont_ni_fiche_ni_raci` | 1 | Début, fin et losanges parallèles refusent RACI et fiche (`ETAPE_SANS_CONTENU`). | Domain.Tests, Host.Tests | `ContentlessStepTypes_RejectRaciAndSheet` | |
| T-MODE-05 | `Un_login_au_format_upn_est_refuse` | 1 | Un login au format UPN est refusé et aucun utilisateur n'est créé. | Domain.Tests, Host.Tests | `UpnLogin_Rejected_NoUserCreated` | mode Serveur seulement (en mode Poste, l'identité est celle du processus) |
| T-MODE-06 | `Annuler_detailler_est_refuse_quand_la_carte_detaillee_a_ete_completee` | 1 | La suppression conditionnelle (`si_vide`) d'une carte détaillée complétée est refusée (`CARTE_NON_VIDE`) et rien n'est perdu. | Application.Tests, Host.Tests | `UndoDetail_RefusedWhenSubMapFilled` | remplacé par undo serveur : l'undo de « Détailler » est refusé si la sous-carte a reçu des étapes : `CARTE_MODIFIEE` (condition interne `si_vide`, `CARTE_NON_VIDE` n'est plus levé, AR-43), ou `OPERATION_DEPASSEE` si un objet touché a changé de version |
| T-MODE-07 | `Couper_coller_juste_avant_le_successeur_s_annule_sans_fleche_en_trop` | 1 | Un couper-coller juste avant le successeur garde A>X>B, et son annulation (telle que la fait l'interface) ne laisse aucune flèche en trop. | Domain.Tests, Application.Tests, Host.Tests | `CutPasteBeforeSuccessor_Undo_LeavesNoExtraFlow` | remplacé par undo serveur (l'annulation n'est plus une suite d'appels du client) |
| T-MODE-08 | `Couper_coller_apres_une_decision_remplace_le_raccord` | 1 | Coller après une décision reprend le libellé de branche et remplace le raccord. | Domain.Tests, Host.Tests | `CutPasteAfterDecision_ReplacesStitchFlow` | |
| T-MODE-09 | `Deux_couloirs_de_meme_nom_ne_bloquent_pas_un_deplacement_vers_une_autre_carte` | 1 | Avec deux couloirs homonymes, un déplacement vers une autre carte réussit, les rôles restent masqués et aucune donnée n'est perdue. | Domain.Tests, Application.Tests, Host.Tests | `TransferToOtherMap_AmbiguousLaneNames_RolesKeptHidden` | heuristique par nom conservée, `source_lane_id` prioritaire (AR-12) |
| T-MODE-10 | `Les_roles_raci_reviennent_quand_le_bloc_revient_dans_sa_carte` | 1 | Quand un bloc regroupé revient dans sa carte d'origine, ses rôles RACI retrouvent leur couloir. | Domain.Tests, Application.Tests, Host.Tests | `BlockReturnsToOriginMap_RaciRolesRestored` | en 1.x le retour est l'annulation client ; en 2.0, à tester aussi via undo serveur |
| T-MODE-11 | `Rattacher_a_une_etape_dont_la_carte_detaillee_est_vide` | 1 | Rattacher une carte à une étape dont la sous-carte est vide supprime la sous-carte vide. | Application.Tests, Host.Tests | `AttachMapToStepWithEmptySubMap_ReplacesEmptySubMap` | |
| T-MODE-12 | `Un_couloir_ne_revient_pas_sur_une_carte_supprimee_depuis` | 1 | Un couloir supprimé ne se restaure pas si sa carte a été supprimée depuis (`LOT_NON_RESTAURABLE`). | Application.Tests, Host.Tests | `UndoLaneDeletion_RefusedWhenMapDeletedSince` | remplacé par undo serveur : refus `OPERATION_DEPASSEE`, ou `CARTE_MODIFIEE` quand la règle interne de restauration du lot échoue (`LOT_NON_RESTAURABLE` n'est plus levé, AR-43) |
| T-MODE-13 | `La_base_refuse_une_etape_supprimee_qui_garde_des_fleches_vivantes` | 1 | Le déclencheur SQL Server refuse (50006) une étape supprimée qui garde des flèches vivantes. | Domain.Tests, Application.Tests, Persistence.Tests | `DeletedStep_WithLiveFlows_Rejected` | dépend de SQL Server en 1.x (déclencheur) : règle portée dans le domaine et l'application ; garde-fou portable en base si retenu (4.2) |
| T-MODE-14 | `Les_vues_des_rapports_se_lisent` | 1 | Les vues `bi` se lisent, avec la profondeur des cartes et les étapes vivantes seulement. | Persistence.Tests | `BiViews_AreReadable_DepthAndLiveStepsOnly` | dépend de SQL Server (schéma `bi`, rôle `topogenia_bi`) |

### ProjetsEtSecuriteTests.cs

Le fichier contient trois classes : `ProfilTests`, `ProjetsTests`, `SecuriteTests`.

| N° | Test 1.x | Cas | Intention (ce que le test prouve, une phrase) | Cible 2.0 | Nom 2.0 proposé (anglais, PascalCase avec _ ) | Remarque |
|---|---|---|---|---|---|---|
| T-PSEC-01 | `ProfilTests.Premiere_requete_cree_le_profil_depuis_l_annuaire` | 1 | La première requête crée le profil depuis l'annuaire, sans doublon au second appel. | Application.Tests, Host.Tests | `FirstRequest_CreatesProfileFromDirectory_NoDuplicate` | 5.6 point 6 : cache court par utilisateur ; ajouter qu'un second appel n'écrit pas ; événement d'audit « nouvel utilisateur » (5.5) |
| T-PSEC-02 | `ProfilTests.Langue_et_premiere_visite_memorisees` | 1 | Langue et première visite sont mémorisées et un PATCH partiel ne touche pas les autres champs. | Application.Tests, Host.Tests | `PatchProfile_PartialUpdate_KeepsOtherFields` | |
| T-PSEC-03 | `ProfilTests.Langue_non_prise_en_charge_refusee` | 1 | Une langue non prise en charge est refusée. | Domain.Tests, Host.Tests | `PatchProfile_UnsupportedLanguage_Rejected` | liste des langues en configuration (section 13) |
| T-PSEC-04 | `ProfilTests.Messages_d_erreur_dans_la_langue_du_profil_sinon_du_navigateur` | 1 | Les messages d'erreur suivent la langue du profil, sinon celle du navigateur. | Host.Tests | `ErrorMessages_UseProfileLanguage_ElseBrowser` | |
| T-PSEC-05 | `ProjetsTests.Creation_cree_carte_racine_premier_couloir_et_membres` | 1 | La création d'un projet crée la carte racine, le premier couloir codé et coloré, et les membres avec le créateur animateur. | Application.Tests, Host.Tests | `CreateProject_CreatesRootMapFirstLaneAndMembers` | |
| T-PSEC-06 | `ProjetsTests.Premier_couloir_en_anglais_si_projet_anglais` | 1 | Le premier couloir d'un projet anglais s'appelle « Actor 1 ». | Application.Tests, Host.Tests | `CreateProject_English_FirstLaneNamedInEnglish` | |
| T-PSEC-07 | `ProjetsTests.Traduction_du_nom_conservee_et_reportee_sur_la_carte_racine` | 1 | La traduction du nom est conservée hors langue d'origine et reportée sur la carte racine et le fil d'Ariane. | Application.Tests, Host.Tests | `CreateProject_NameTranslation_CopiedToRootMap` | |
| T-PSEC-08 | `ProjetsTests.Code_projet_invalide` | 5 | Un code projet hors format (3 à 6 majuscules) est refusé. | Domain.Tests, Host.Tests | `CreateProject_InvalidCode_Rejected` | |
| T-PSEC-09 | `ProjetsTests.Code_projet_unique` | 1 | Un code projet déjà pris est refusé en 409. | Application.Tests, Persistence.Tests, Host.Tests | `CreateProject_DuplicateCode_Conflict` | index unique sur les deux bases |
| T-PSEC-10 | `ProjetsTests.Seul_l_administrateur_cree_un_projet` | 1 | Seul l'administrateur crée un projet. | Application.Tests, Host.Tests | `CreateProject_NonAdmin_Forbidden` | |
| T-PSEC-11 | `ProjetsTests.Nom_obligatoire_et_sans_html` | 1 | Le nom du projet est obligatoire et sans HTML. | Domain.Tests, Host.Tests | `CreateProject_NameRequiredWithoutHtml` | |
| T-PSEC-12 | `ProjetsTests.Liste_limitee_aux_projets_dont_on_est_membre` | 1 | La liste des projets se limite à ceux dont on est membre, sauf pour l'administrateur. | Application.Tests, Host.Tests | `ListProjects_MembersOnly_AdminSeesAll` | |
| T-PSEC-13 | `ProjetsTests.Non_membre_ne_voit_ni_projet_ni_carte` | 1 | Un non-membre reçoit 404 sur le projet et sur la carte. | Application.Tests, Host.Tests | `NonMember_ProjectAndMap_NotFound` | |
| T-PSEC-14 | `ProjetsTests.Remplacement_des_membres_par_l_animateur` | 1 | L'animateur remplace la liste des membres, l'ancien membre perd l'accès et un invité jamais connecté accède dès sa première visite. | Application.Tests, Host.Tests | `ReplaceMembers_ByFacilitator_RemovedLoseAccess_InviteeGetsAccess` | ajouter le refus `PROJET_SANS_ANIMATEUR` (5.6 point 4) |
| T-PSEC-15 | `ProjetsTests.Membres_invalides_refuses_sans_rien_modifier` | 1 | Rôle inconnu, login sans domaine et doublon (casse ignorée) sont refusés sans rien modifier. | Domain.Tests, Application.Tests, Host.Tests | `ReplaceMembers_Invalid_RejectedWithoutChange` | 5.6 point 5 : vérifier en plus qu'aucun utilisateur invité n'est créé quand la requête échoue (création dans la transaction) |
| T-PSEC-16 | `ProjetsTests.Contributeur_et_lecteur_ne_gerent_pas_les_membres` | 1 | Contributeur et lecteur ne modifient pas les membres. | Application.Tests, Host.Tests | `ReplaceMembers_ContributorOrReader_Forbidden` | |
| T-PSEC-17 | `ProjetsTests.Participants_sponsors_en_tete` | 1 | La liste des participants place les sponsors en tête. | Application.Tests, Host.Tests | `Participants_SponsorsFirst` | |
| T-PSEC-18 | `SecuriteTests.Anonyme_refuse` | 1 | Une requête anonyme est refusée en 401. | Host.Tests | `AnonymousRequest_Unauthorized` | en mode Poste : refus sans cookie de session (jeton de lancement, 5.2) |
| T-PSEC-19 | `SecuriteTests.Ecriture_sans_en_tete_anti_falsification_refusee` | 1 | Une écriture sans l'en-tête anti-CSRF est refusée sans rien écrire, la lecture reste permise. | Host.Tests | `WriteWithoutAntiCsrfHeader_Rejected_ReadAllowed` | compléter par le contrôle `Origin` / `Sec-Fetch-Site` (5.2, 5.3) |
| T-PSEC-20 | `SecuriteTests.Roles_sur_la_structure_selon_la_matrice` | 1 | Contributeur et lecteur ne modifient pas la structure, l'étranger reçoit 404 et l'administrateur a tous les droits. | Application.Tests, Host.Tests | `StructureRights_FollowRoleMatrix` | à étendre : un test par point d'entrée et par rôle (section 11, Sécurité) |
| T-PSEC-21 | `SecuriteTests.Route_api_inconnue_au_format_erreur` | 1 | Une route API inconnue renvoie 404 au format d'erreur normalisé. | Host.Tests | `UnknownApiRoute_NotFoundInErrorFormat` | |
| T-PSEC-22 | `SecuriteTests.Corps_json_illisible` | 1 | Un corps JSON illisible renvoie `REQUETE_INVALIDE`. | Host.Tests | `MalformedJsonBody_BadRequest` | ajouter : champ obligatoire absent = erreur, jamais valeur par défaut (5.4) |
| T-PSEC-23 | `SecuriteTests.Identifiant_inexistant` | 1 | Un identifiant inexistant renvoie 404 en lecture et en écriture. | Application.Tests, Host.Tests | `UnknownId_NotFound` | |
| T-PSEC-24 | `SecuriteTests.Sante_accessible_sans_authentification` | 1 | `/api/sante` répond sans authentification, avec l'état de la base. | Host.Tests | `Health_Anonymous_ReturnsStatusOnly` | défaut 1.x corrigé, inverser l'assertion : en mode Serveur, `/api/health` anonyme ne renvoie que `{status}`, le détail (version, base) exige l'authentification (5.3, 5.6 point 8) ; en mode Poste, la santé n'est pas anonyme, le cookie de session est exigé (5.2, AR-46) : un cas par mode |

### RaciTests.cs

| N° | Test 1.x | Cas | Intention (ce que le test prouve, une phrase) | Cible 2.0 | Nom 2.0 proposé (anglais, PascalCase avec _ ) | Remarque |
|---|---|---|---|---|---|---|
| T-RACI-01 | `Attribuer_retirer_et_lettres_par_defaut` | 1 | Les lettres par défaut sont ACI, un rôle se pose, se remplace et se retire (null). | Domain.Tests, Application.Tests, Host.Tests | `SetRaci_AssignReplaceRemove_DefaultLettersAci` | |
| T-RACI-02 | `Un_seul_approbateur_poser_A_ailleurs_le_deplace` | 1 | Poser A sur un autre couloir déplace l'approbateur unique. | Domain.Tests, Persistence.Tests, Host.Tests | `SetRaci_SecondApprover_MovesA` | un seul A garanti par le domaine et par un index unique partiel (deux bases) |
| T-RACI-03 | `Le_couloir_qui_realise_ne_recoit_pas_d_autre_role` | 1 | Le couloir de l'étape (R) ne reçoit pas d'autre rôle. | Domain.Tests, Host.Tests | `SetRaci_OnPerformingLane_Rejected` | |
| T-RACI-04 | `Le_R_l_emporte_apres_deplacement_et_le_role_revient_au_retour` | 1 | Quand l'étape passe dans un couloir porteur d'un rôle, le R l'emporte (rôle masqué), et le rôle revient au retour. | Domain.Tests, Application.Tests, Host.Tests | `MoveStepToRaciLane_RHidesRole_RoleBackOnReturn` | |
| T-RACI-05 | `Lettre_S_seulement_si_le_parametre_le_prevoit` | 1 | S n'est accepté que si le paramètre `raci_letters` le prévoit ; R et lettres multiples sont refusés. | Domain.Tests, Host.Tests | `SetRaci_LetterS_OnlyWhenConfigured` | paramètre modifié en SQL direct : passer par la configuration 2.0 |
| T-RACI-06 | `Couloir_supprime_ses_roles_disparaissent_puis_reviennent_a_la_restauration` | 1 | Les rôles d'un couloir supprimé disparaissent et reviennent avec sa restauration. | Application.Tests, Host.Tests | `DeleteLane_RaciHidden_UndoRestoresRoles` | remplacé par undo serveur |
| T-RACI-07 | `Couloir_d_une_autre_carte_et_droits` | 1 | Un couloir d'une autre carte est refusé, contributeur et lecteur ne posent pas de rôle, le lecteur les voit. | Application.Tests, Host.Tests | `SetRaci_OtherMapLaneOrInsufficientRole_Rejected_ReaderSees` | |

### RemonteeTests.cs

| N° | Test 1.x | Cas | Intention (ce que le test prouve, une phrase) | Cible 2.0 | Nom 2.0 proposé (anglais, PascalCase avec _ ) | Remarque |
|---|---|---|---|---|---|---|
| T-REMO-01 | `Les_etapes_du_detail_prennent_la_place_de_l_etape_detaillee_avec_ses_fleches` | 1 | Remonter un détail place ses étapes à la place de l'étape détaillée (flux décalé, couloir manquant créé, flèches reportées sur les extrémités, codes et fiches gardés, début et fin retirés) et ouvre un cadre avec le reliquat (documents, usages, irritant, fiche). | Domain.Tests, Application.Tests, Host.Tests | `PromoteDetail_StepsReplaceParent_FlowsRewired_LeftoverFrameOpened` | 5.6 point 1 : ajouter qu'un lecteur ne voit dans le reliquat que la fiche validée (`SheetVisibility`) ; requêtes `OPENJSON` 1.x réécrites portables |
| T-REMO-02 | `Le_cadre_disparait_quand_le_reliquat_est_ventile_et_les_usages_suivent` | 1 | Une affectation hors carte est refusée, le cadre se ferme quand le reliquat est ventilé, les usages suivent le nouveau producteur, et le retour d'une affectation rouvre le cadre. | Domain.Tests, Application.Tests, Host.Tests | `AssignLeftover_FrameClosesWhenEmpty_UsagesFollowProducer` | le retour d'affectation (paramètre `depuis`, Ctrl+Z client) est remplacé par undo serveur (l'affectation devient annulable, 4.6) |
| T-REMO-03 | `Terminer_range_le_reste_et_la_derniere_etape_recoit_tout` | 1 | « Terminer » range tout le reste sur la dernière étape du cadre, ferme le cadre et ne fait rien une seconde fois. | Domain.Tests, Application.Tests, Host.Tests | `FinishFrame_AssignsRemainderToLastStep_Idempotent` | « Terminer » devient annulable par undo serveur (4.6) : ajouter le cas |
| T-REMO-04 | `Annuler_la_remontee_remet_le_detail_et_l_etape_detaillee` | 1 | Annuler la remontée remet l'étape détaillée à sa place avec ses flèches, la carte détaillée intacte, supprime le couloir créé, et permet de remonter de nouveau. | Application.Tests, Host.Tests | `UndoPromoteDetail_RestoresParentStepSubMapAndFlowPositions` | remplacé par undo serveur ; l'undo remet aussi les positions du flux, que le client remettait en 1.x (4.6, dette 6.3) |
| T-REMO-05 | `Refus_un_seul_cadre_par_carte_pas_de_detail_dans_le_cadre_frequences_et_droits` | 1 | Remonter est réservé à l'animateur, exige un détail, un seul cadre par carte (pas de regroupement pendant le cadre) et refuse de mélanger deux flux de fréquences différentes. | Domain.Tests, Application.Tests, Host.Tests | `PromoteDetail_Refusals_RightsNoDetailOpenFrameMixedFrequencies` | |
| T-REMO-06 | `La_purge_attend_la_fin_du_cadre` | 1 | La purge ne touche pas le lot d'une remontée tant que son cadre est ouvert, puis l'efface sans perdre ce qui a été rangé. | Application.Tests, Persistence.Tests | `Purge_KeepsOpenFrameBatch_ThenDeletesAfterFinish` | procédure de purge réécrite en C# |
| T-REMO-07 | `Securite_on_ne_ramene_dans_le_reliquat_que_ce_qui_est_sur_la_carte` | 1 | On ne ramène dans le reliquat ni un document ou un irritant d'un autre projet, ni un objet depuis une étape qui ne le porte pas. | Application.Tests, Host.Tests | `AssignLeftover_ObjectsFromOtherMapOrProject_Rejected` | le « ramener » (`depuis`) est remplacé par undo serveur ; garder le contrôle de périmètre sur l'affectation |
| T-REMO-08 | `Un_usage_ne_se_pose_pas_sur_une_etape_qui_le_recoit_deja_et_l_annulation_le_rend` | 1 | Un usage du reliquat ne va pas sur une étape qui le reçoit déjà ou qui produit le document, un document ne va pas à une étape qui l'utilise, et le retour ne rend que l'usage déplacé. | Domain.Tests, Application.Tests, Host.Tests | `AssignLeftoverUsage_DuplicateOrProducerTarget_Conflict_UndoReturnsOnlyMoved` | partie annulation remplacée par undo serveur |
| T-REMO-09 | `On_ne_remonte_pas_par_dessus_un_cadre_ouvert_plus_bas` | 1 | On ne remonte pas un détail tant qu'un cadre est ouvert dans sa descendance. | Domain.Tests, Application.Tests, Host.Tests | `PromoteDetail_OpenFrameBelow_Conflict` | |
| T-REMO-10 | `Annulation_refusee_proprement_si_une_fleche_relie_le_cadre_au_flux_et_couloirs_crees_a_la_corbeille` | 1 | L'annulation de la remontée est refusée (`CARTE_MODIFIEE`) si une flèche relie le cadre au flux, et une étape posée puis supprimée dans le couloir créé ne la bloque pas. | Application.Tests, Host.Tests | `UndoPromoteDetail_RefusedWhenFrameLinkedToFlow_AllowedAfterTransientStep` | remplacé par undo serveur : refus `CARTE_MODIFIEE` (l'inverse violerait un invariant, AR-04, AR-43) ; `OPERATION_DEPASSEE` avec auteur et heure si un objet touché a changé de version (4.6) |
| T-REMO-11 | `Les_roles_RACI_suivent_meme_sur_un_couloir_sans_etape` | 1 | Les rôles RACI posés sur un couloir du détail sans étape suivent dans le flux après remontée. | Domain.Tests, Application.Tests, Host.Tests | `PromoteDetail_RaciOnEmptyLane_Follows` | correspondance des couloirs par nom (dette 6.3) |
| T-REMO-12 | `Le_lot_d_une_remontee_ne_se_restaure_pas_seul_et_pas_d_affectation_a_une_fin` | 1 | Le lot d'une remontée ne se restaure pas seul (`LOT_REMONTEE`) et rien ne s'affecte à une étape de fin. | Domain.Tests, Application.Tests, Host.Tests | `PromoteDetailBatch_NotRestorableAlone_NoAssignToEndStep` | partie lot remplacée par undo serveur (le lot n'est plus exposé au client ; `LOT_REMONTEE` devient une condition interne qui produit `CARTE_MODIFIEE`, AR-43) |
| T-REMO-13 | `Sans_reliquat_le_cadre_reste_jusqu_a_ce_qu_on_le_ferme` | 1 | Sans reliquat, le cadre reste ouvert jusqu'à « Terminer » et la remontée s'annule encore cadre fermé. | Application.Tests, Host.Tests | `PromoteDetail_NoLeftover_FrameStaysUntilClosed_UndoStillPossible` | annulation remplacée par undo serveur ; la fermeture d'un cadre vide reste non annulable (4.6) ; préparation en SQL direct à remplacer |

### ReperesTests.cs

| N° | Test 1.x | Cas | Intention (ce que le test prouve, une phrase) | Cible 2.0 | Nom 2.0 proposé (anglais, PascalCase avec _ ) | Remarque |
|---|---|---|---|---|---|---|
| T-REPE-01 | `Documents_irritants_et_duree_de_chaque_etape` | 1 | La carte renvoie pour chaque étape ses documents produits et reçus, ses irritants ouverts (rattachés au producteur, hors abandonnés) et la durée de la fiche, brouillon pour l'animateur et validée pour le lecteur. | Application.Tests, Host.Tests | `MapView_StepMarkers_DocumentsOpenPainPointsDuration` | statut d'irritant modifié en SQL direct (pas de route en 1.x, dette 6.9) |

### ScenariosFluxTests.cs

Tous ces scénarios enchaînent « Détailler la sélection » puis « Remonter le détail ». En 2.0, la
remontée retrouve les flèches coupées par `step.origin_operation_id` (4.6) au lieu de
l'heuristique des ±60 s : chaque scénario est à jouer deux fois, avec l'opération d'origine au
journal et sans elle (règle générale de l'annexe A, 2.9).

| N° | Test 1.x | Cas | Intention (ce que le test prouve, une phrase) | Cible 2.0 | Nom 2.0 proposé (anglais, PascalCase avec _ ) | Remarque |
|---|---|---|---|---|---|---|
| T-SCFL-01 | `Trois_etapes_sur_trois_lignes_aller_retour` | 1 | Un bloc réparti sur trois couloirs garde ses couloirs, son ordre et l'ordre de lecture dans le détail, et l'aller-retour remet chaque étape dans sa case avec les mêmes flèches. | Domain.Tests, Application.Tests, Host.Tests | `DetailThenPromote_ThreeLanes_ExactRoundTrip` | |
| T-SCFL-02 | `Fleches_qui_sautent_des_lignes_vers_une_etape_du_milieu` | 1 | Les flèches qui entrent ou sortent au milieu du bloc passent par l'étape détaillée et sont rendues exactement après remontée. | Domain.Tests, Application.Tests, Host.Tests | `DetailThenPromote_FlowsIntoMiddleOfBlock_Restored` | |
| T-SCFL-03 | `Boucle_de_retour_dans_la_selection` | 1 | Une boucle interne au bloc survit à l'aller-retour. | Domain.Tests, Application.Tests, Host.Tests | `DetailThenPromote_InternalLoop_Preserved` | |
| T-SCFL-04 | `Selection_qui_saute_une_ligne_et_branches_paralleles` | 1 | Une sélection sur deux couloirs non contigus avec une branche parallèle ne copie que les couloirs utilisés et rend rangées, couloirs et flèches. | Domain.Tests, Application.Tests, Host.Tests | `DetailThenPromote_SkippedLaneAndParallelBranch_Restored` | |
| T-SCFL-05 | `Deux_couloirs_de_meme_nom` | 1 | Avec deux couloirs homonymes, l'étape revient dans son couloir d'origine et non dans l'homonyme. | Domain.Tests, Application.Tests, Host.Tests | `DetailThenPromote_HomonymousLanes_ReturnsToOriginalLane` | repli sur `source_lane_id` (dette 6.3) |
| T-SCFL-06 | `Decision_dans_la_selection_dont_les_deux_sorties_quittent_le_bloc` | 1 | Les deux sorties libellées d'une décision qui quittent le bloc partent de l'étape détaillée puis reviennent avec leurs libellés et positions. | Domain.Tests, Application.Tests, Host.Tests | `DetailThenPromote_DecisionExitsLeavingBlock_LabelsRestored` | |
| T-SCFL-07 | `Detail_pose_sur_une_branche_parallele` | 1 | Un bloc posé sur une rangée parallèle revient à l'identique. | Domain.Tests, Application.Tests, Host.Tests | `DetailThenPromote_BlockOnParallelRow_ExactRoundTrip` | |

### SelectionTests.cs

| N° | Test 1.x | Cas | Intention (ce que le test prouve, une phrase) | Cible 2.0 | Nom 2.0 proposé (anglais, PascalCase avec _ ) | Remarque |
|---|---|---|---|---|---|---|
| T-SELE-01 | `Supprimer_en_recousant_relie_les_predecesseurs_aux_successeurs` | 1 | Supprimer en recousant relie prédécesseur et successeur en gardant le libellé entrant, s'annule, et sans « recoudre » rien n'est recréé. | Domain.Tests, Application.Tests, Host.Tests | `DeleteWithStitch_LinksPredecessorsToSuccessors_KeepsLabel` | partie annulation remplacée par undo serveur |
| T-SELE-02 | `Supprimer_un_aiguillage_garde_ses_libelles_et_un_raccord_ambigu_n_est_pas_fait` | 1 | Recoudre un aiguillage reporte ses libellés, et un raccord ambigu (plusieurs prédécesseurs et successeurs) n'est pas fait. | Domain.Tests, Host.Tests | `DeleteWithStitch_SwitchKeepsLabels_AmbiguousNotStitched` | |
| T-SELE-03 | `Coller_une_copie_cree_de_nouvelles_etapes_et_l_insere_dans_le_flux` | 1 | Coller une copie crée de nouvelles étapes codées, les insère dans le flux (RG-C04, libellés) et refuse cases en double, clés inconnues et rôle insuffisant. | Domain.Tests, Application.Tests, Host.Tests | `PasteCopy_CreatesNewStepsInsertedInFlow_InvalidRejected` | coller devient annulable (4.6) : ajouter undo et redo |
| T-SELE-04 | `Couper_coller_dans_la_carte_sort_le_bloc_du_flux_et_le_reinsere_en_gardant_son_identite` | 1 | Couper-coller dans la carte recoud l'origine, réinsère le bloc avec son code et sa fiche, et l'annulation rend les flèches d'avant. | Domain.Tests, Application.Tests, Host.Tests | `CutPasteWithinMap_KeepsIdentity_UndoRestoresFlows` | partie annulation remplacée par undo serveur |
| T-SELE-05 | `Couper_coller_vers_une_autre_carte_du_projet_garde_fiche_documents_et_fleches_internes` | 1 | Couper-coller vers une autre carte du projet garde fiche, documents et flèches internes, refuse un lien entre cartes, et l'annulation remet tout en place. | Domain.Tests, Application.Tests, Host.Tests | `CutPasteToOtherMap_KeepsSheetDocumentsInternalFlows` | partie annulation remplacée par undo serveur ; la règle « pas de flèche entre cartes » (RG-C05, déclencheur en 1.x) passe au domaine |
| T-SELE-06 | `Couper_coller_refuse_la_propre_carte_detaillee_et_un_autre_projet` | 1 | Couper-coller est refusé vers sa propre carte détaillée, vers un autre projet et sur une case en double. | Domain.Tests, Application.Tests, Host.Tests | `CutPaste_IntoOwnSubMapOrOtherProject_Rejected` | |
| T-SELE-07 | `Detailler_la_selection_cree_une_etape_dont_la_carte_detaillee_recoit_le_bloc` | 1 | « Détailler la sélection » remplace le bloc par une étape dont la carte détaillée reçoit les étapes, couloirs et positions relatives ; renommer l'étape renomme la carte ; l'annulation rend l'état initial. | Domain.Tests, Application.Tests, Host.Tests | `DetailSelection_CreatesStepWhoseSubMapHoldsBlock_UndoRestores` | partie annulation remplacée par undo serveur ; l'opération inscrit les flèches coupées (4.6) |

### SousCarteVideTests.cs

| N° | Test 1.x | Cas | Intention (ce que le test prouve, une phrase) | Cible 2.0 | Nom 2.0 proposé (anglais, PascalCase avec _ ) | Remarque |
|---|---|---|---|---|---|---|
| T-SCVI-01 | `Vide_tant_qu_aucune_etape_n_y_est_ajoutee` | 1 | Une sous-carte reste « vide » tant qu'elle n'a pas d'étape vivante, un couloir ne suffisant pas. | Domain.Tests, Application.Tests, Host.Tests | `SubMap_IsEmptyUntilItHasLiveStep` | |
| T-SCVI-02 | `Detailler_de_nouveau_rouvre_la_meme_carte_vide` | 1 | Détailler de nouveau rouvre la même sous-carte vide avec un avertissement, et refuse une sous-carte remplie. | Application.Tests, Host.Tests | `DetailAgain_ReopensSameEmptySubMap_RefusedWhenFilled` | |
| T-SCVI-03 | `Suppression_sans_question_et_annulable` | 1 | Une étape à sous-carte vide se supprime sans question et l'annulation rend l'étape et sa sous-carte vide. | Application.Tests, Host.Tests | `DeleteStepWithEmptySubMap_NoChoiceRequired_UndoRestores` | remplacé par undo serveur |
| T-SCVI-04 | `Changer_en_debut_ou_fin_retire_une_carte_vide_mais_pas_une_remplie` | 1 | Passer en début ou fin retire une sous-carte vide mais est refusé pour une remplie, les autres types restant permis. | Domain.Tests, Application.Tests, Host.Tests | `ChangeTypeToStartOrEnd_RemovesEmptySubMap_RefusedWhenFilled` | |

### SousCartesEtAnnulationTests.cs

Le fichier contient trois classes : `CommandeApiTests`, `SousCartesTests`, `RestaurationTests`.
Les tests de `RestaurationTests` appellent tous `/api/suppressions/{lot}/restaurer` : ils sont
portés vers undo serveur. Comme l'undo 2.0 est une pile par carte et par utilisateur (dernière
opération d'abord), les scénarios où une suppression plus récente précède la restauration
(T-SCAN-17, T-SCAN-22) se jouent avec un second utilisateur qui fait la suppression intermédiaire.

| N° | Test 1.x | Cas | Intention (ce que le test prouve, une phrase) | Cible 2.0 | Nom 2.0 proposé (anglais, PascalCase avec _ ) | Remarque |
|---|---|---|---|---|---|---|
| T-SCAN-01 | `CommandeApiTests.Codes_complets_courts_et_libelle` | 1 | La commande texte accepte codes complets et courts et un libellé après les deux-points. | Domain.Tests, Host.Tests | `Command_FullAndShortCodesWithLabel_CreatesFlows` | la commande devient annulable (4.6) |
| T-SCAN-02 | `CommandeApiTests.Tout_ou_rien_avec_numeros_de_ligne` | 1 | Une commande avec erreurs ne crée rien et rend les erreurs par numéro de ligne (étape inconnue, syntaxe). | Domain.Tests, Application.Tests, Host.Tests | `Command_AllOrNothing_ErrorsByLine` | messages « L{n} » construits hors du service (dette 6.5) |
| T-SCAN-03 | `CommandeApiTests.Doublons_existants_et_internes_signales` | 1 | Les doublons, avec une flèche existante ou internes à la commande, sont signalés par ligne. | Domain.Tests, Host.Tests | `Command_ExistingAndInternalDuplicates_Reported` | |
| T-SCAN-04 | `CommandeApiTests.Etape_d_une_autre_carte_refusee` | 1 | Une étape d'une autre carte du projet est refusée (`ETAPE_HORS_CARTE`). | Domain.Tests, Application.Tests, Host.Tests | `Command_StepFromOtherMap_Rejected` | 5.6 point 2 : ajouter qu'un code d'un autre projet donne `ETAPE_INCONNUE` (recherche limitée au projet) |
| T-SCAN-05 | `CommandeApiTests.Commande_vide_et_lecteur_refuses` | 1 | Une commande vide est refusée et le lecteur n'a pas le droit. | Domain.Tests, Application.Tests, Host.Tests | `Command_EmptyOrReader_Rejected` | |
| T-SCAN-06 | `SousCartesTests.Detailler_reprend_le_couloir_parent_et_construit_l_ariane` | 1 | Détailler crée une sous-carte qui reprend le couloir parent (nom, couleur, traduction, nouveau code), le fil d'Ariane et les chemins hiérarchiques 2.1, 2.2. | Domain.Tests, Application.Tests, Host.Tests | `DetailStep_CopiesParentLane_BuildsBreadcrumbAndPaths` | fil d'Ariane sans N+1 (dette 6.6) |
| T-SCAN-07 | `SousCartesTests.Une_seule_sous_carte_et_pas_de_debut_ni_fin` | 1 | Une étape n'a qu'une sous-carte, début et fin ne se détaillent pas, une étape détaillée ne devient pas fin, et le contributeur ne détaille pas. | Domain.Tests, Application.Tests, Host.Tests | `DetailStep_OneSubMapOnly_NotForStartEnd` | |
| T-SCAN-08 | `SousCartesTests.Avertissement_au_dela_de_quatre_niveaux` | 1 | Au-delà de quatre niveaux, détailler avertit (`PROFONDEUR_DEPASSEE`) sans refuser. | Domain.Tests, Host.Tests | `DetailStep_BeyondFourLevels_Warns` | |
| T-SCAN-09 | `SousCartesTests.Supprimer_une_macro_etape_exige_un_choix` | 1 | Supprimer une étape à sous-carte remplie exige un choix (supprimer ou rattacher avec une cible). | Application.Tests, Host.Tests | `DeleteMacroStep_RequiresChoice` | |
| T-SCAN-10 | `SousCartesTests.Supprimer_une_macro_etape_avec_toute_sa_descendance` | 1 | Supprimer une macro-étape avec « supprimer » efface toute sa descendance. | Application.Tests, Persistence.Tests, Host.Tests | `DeleteMacroStep_WithDescendants_DeletesAllLevels` | CTE récursive portable |
| T-SCAN-11 | `SousCartesTests.Supprimer_une_macro_etape_en_rattachant_sa_carte` | 1 | Supprimer une macro-étape en rattachant sa carte la donne à l'étape héritière. | Application.Tests, Host.Tests | `DeleteMacroStep_AttachSubMapToHeir` | |
| T-SCAN-12 | `SousCartesTests.Rattachement_interdit_dans_sa_propre_descendance` | 1 | Le rattachement est refusé dans sa propre descendance, pour la carte racine et vers un autre projet. | Domain.Tests, Application.Tests, Host.Tests | `AttachMap_IntoOwnDescendantsRootOrOtherProject_Rejected` | |
| T-SCAN-13 | `SousCartesTests.Carte_d_un_autre_projet_introuvable` | 1 | La carte d'un autre projet est introuvable pour un animateur qui n'en est pas membre. | Application.Tests, Host.Tests | `DeleteMap_OtherProject_NotFound` | |
| T-SCAN-14 | `SousCartesTests.La_carte_racine_de_son_projet_ne_se_supprime_pas` | 1 | La carte racine ne se supprime pas. | Domain.Tests, Host.Tests | `DeleteRootMap_Rejected` | |
| T-SCAN-15 | `RestaurationTests.Etape_et_liens_reviennent_avec_le_meme_code` | 1 | Une étape supprimée revient avec son identifiant, son code, sa colonne et ses flèches libellées, une seule fois. | Application.Tests, Host.Tests | `UndoDeleteStep_RestoresSameIdCodeAndFlows_Once` | remplacé par undo serveur |
| T-SCAN-16 | `RestaurationTests.Case_reprise_entre_temps_l_intruse_est_decalee` | 1 | À la restauration, une étape posée entre-temps sur la case d'origine est décalée (RG-C04). | Domain.Tests, Application.Tests, Host.Tests | `UndoDelete_CellTakenMeanwhile_IntruderShifted` | remplacé par undo serveur |
| T-SCAN-17 | `RestaurationTests.Un_lien_dont_l_extremite_a_disparu_reste_supprime` | 1 | Une flèche dont une extrémité a disparu entre-temps n'est pas restaurée. | Application.Tests, Host.Tests | `UndoDeleteFlow_EndpointGone_FlowStaysDeleted` | remplacé par undo serveur ; suppression intermédiaire par un second utilisateur |
| T-SCAN-18 | `RestaurationTests.Un_lien_recree_entre_temps_n_est_pas_double` | 1 | Une flèche recréée entre-temps n'est pas doublée par la restauration. | Application.Tests, Persistence.Tests, Host.Tests | `UndoDeleteFlow_RecreatedMeanwhile_NotDuplicated` | remplacé par undo serveur ; unicité de flèche par index partiel (deux bases) |
| T-SCAN-19 | `RestaurationTests.Annuler_un_detailler_puis_le_retablir` | 1 | Supprimer une sous-carte remplie puis la restaurer la rend avec son contenu. | Application.Tests, Host.Tests | `UndoDeleteSubMap_RestoresContent` | remplacé par undo serveur |
| T-SCAN-20 | `RestaurationTests.Restaurer_une_macro_etape_et_sa_descendance` | 1 | Restaurer une macro-étape supprimée avec sa descendance rend la sous-carte et ses étapes. | Application.Tests, Host.Tests | `UndoDeleteMacroStep_RestoresDescendants` | remplacé par undo serveur |
| T-SCAN-21 | `RestaurationTests.Couloir_supprime_avec_ses_etapes_revient_entier` | 1 | Un couloir supprimé avec ses étapes revient entier, à son rang et aux colonnes d'origine. | Application.Tests, Host.Tests | `UndoDeleteLaneWithSteps_RestoresWholeLane` | remplacé par undo serveur |
| T-SCAN-22 | `RestaurationTests.Etape_d_une_carte_supprimee_depuis_n_est_pas_restaurable` | 1 | Une étape dont la carte a été supprimée depuis n'est pas restaurable (`LOT_NON_RESTAURABLE`). | Application.Tests, Host.Tests | `UndoDeleteStep_MapDeletedSince_Refused` | remplacé par undo serveur ; suppression de la carte par un second utilisateur ; refus `CARTE_MODIFIEE` (condition interne, `LOT_NON_RESTAURABLE` n'est plus levé, AR-43) ou `OPERATION_DEPASSEE` |
| T-SCAN-23 | `RestaurationTests.Restauration_reservee_a_l_animateur_du_projet` | 1 | La restauration est réservée à l'animateur du projet, un autre projet reçoit 404 et un lot inconnu `LOT_INTROUVABLE`. | Application.Tests, Host.Tests | `Undo_RequiresStructureRightOnProject` | remplacé par undo serveur (undo réservé à l'auteur de l'opération, sur sa carte) ; plus d'assertion `LOT_INTROUVABLE`, code qui n'est plus levé par l'API 2.0 (AR-43) |
| T-SCAN-24 | `RestaurationTests.Suppression_groupee_etapes_et_liens_en_un_lot` | 1 | Une suppression groupée d'étapes et de flèches forme un seul lot, restauré d'un coup. | Application.Tests, Host.Tests | `GroupDelete_SingleOperation_UndoneAtOnce` | remplacé par undo serveur |

### StructureTests.cs

Le fichier contient quatre classes : `CouloirsTests`, `EtapesTests`, `LiensTests`, `DispositionTests`.

| N° | Test 1.x | Cas | Intention (ce que le test prouve, une phrase) | Cible 2.0 | Nom 2.0 proposé (anglais, PascalCase avec _ ) | Remarque |
|---|---|---|---|---|---|---|
| T-STRU-01 | `CouloirsTests.Ajout_en_fin_puis_en_tete_avec_couleurs_alternees` | 1 | Un couloir s'ajoute en fin ou en tête avec un code séquentiel, un ordre renuméroté et une couleur alternée. | Domain.Tests, Application.Tests, Host.Tests | `AddLane_AtEndOrStart_SequentialCodeAlternatingColour` | l'ajout de couloir devient annulable (4.6) |
| T-STRU-02 | `CouloirsTests.Ajout_au_milieu_et_ordre_hors_bornes_ramene_en_fin` | 1 | Un couloir s'insère au milieu et un ordre hors bornes le place en fin. | Domain.Tests, Host.Tests | `AddLane_InMiddle_OutOfRangeOrderGoesLast` | |
| T-STRU-03 | `CouloirsTests.Type_et_couleur_controles` | 1 | Type et couleur du couloir sont contrôlés (liste fermée, `#RRGGBB`). | Domain.Tests, Host.Tests | `AddLane_TypeAndColourValidated` | minuscules acceptées et mises en majuscules (AR-11) |
| T-STRU-04 | `CouloirsTests.Renommage_avec_version_et_traduction` | 1 | Renommer un couloir avec sa version garde le code, enregistre la traduction, change la version, et une traduction vide l'efface. | Application.Tests, Host.Tests | `RenameLane_WithVersionAndTranslation_KeepsCode` | |
| T-STRU-05 | `CouloirsTests.Acteur_du_couloir_doit_etre_membre` | 1 | L'acteur d'un couloir doit être membre du projet et peut être retiré. | Application.Tests, Host.Tests | `LaneActor_MustBeProjectMember` | |
| T-STRU-06 | `CouloirsTests.Version_perimee_renvoie_409_avec_auteur` | 1 | Une version périmée sur un couloir renvoie 409 avec l'auteur et la durée relative, sans rien écrire. | Application.Tests, Persistence.Tests, Host.Tests | `PatchLane_StaleVersion_Conflict409WithAuthor` | jeton de concurrence portable sur les deux bases |
| T-STRU-07 | `CouloirsTests.Le_dernier_couloir_ne_se_supprime_pas` | 1 | Le dernier couloir d'une carte ne se supprime pas. | Domain.Tests, Host.Tests | `DeleteLane_LastLane_Rejected` | |
| T-STRU-08 | `CouloirsTests.Couloir_vide_supprime_et_ordre_renumerote` | 1 | Un couloir vide se supprime dans un lot et l'ordre est renuméroté. | Domain.Tests, Application.Tests, Host.Tests | `DeleteEmptyLane_RenumbersOrder` | |
| T-STRU-09 | `CouloirsTests.Couloir_non_vide_exige_un_choix` | 1 | Un couloir non vide exige un choix (supprimer ou déplacer vers un autre couloir valide). | Application.Tests, Host.Tests | `DeleteNonEmptyLane_RequiresChoiceAndValidTarget` | |
| T-STRU-10 | `CouloirsTests.Supprimer_un_couloir_en_deplacant_ses_etapes` | 1 | Supprimer un couloir en déplaçant ses étapes les pose dans la cible avec le décalage RG-C04. | Domain.Tests, Application.Tests, Host.Tests | `DeleteLane_MoveSteps_ShiftsWithRgC04` | |
| T-STRU-11 | `CouloirsTests.Supprimer_un_couloir_avec_ses_etapes_et_leurs_liens` | 1 | Supprimer un couloir avec ses étapes supprime aussi leurs flèches. | Application.Tests, Host.Tests | `DeleteLane_WithSteps_DeletesTheirFlows` | |
| T-STRU-12 | `CouloirsTests.Reordonnancement_complet_exige` | 1 | Le réordonnancement des couloirs exige la liste complète, sans doublon ni inconnu. | Domain.Tests, Host.Tests | `ReorderLanes_RequiresCompleteList` | l'ordre des couloirs devient annulable (4.6) |
| T-STRU-13 | `EtapesTests.Codes_sequentiels_et_immuables` | 1 | Les codes d'étape sont séquentiels, immuables au renommage et jamais réutilisés après suppression. | Domain.Tests, Persistence.Tests, Host.Tests | `StepCodes_SequentialImmutableNeverReused` | compteur atomique portable (`UPDATE … OUTPUT` en 1.x) |
| T-STRU-14 | `EtapesTests.Etape_creee_avec_son_type_et_sa_langue` | 1 | Une étape est créée avec son type, la langue du projet et l'état manuel ; un type inconnu est refusé. | Domain.Tests, Host.Tests | `CreateStep_WithTypeLanguageAndManualState` | |
| T-STRU-15 | `EtapesTests.Saisies_invalides` | 1 | Colonne hors bornes, libellé vide, trop long ou HTML, et couloir inconnu sont refusés sans rien créer. | Domain.Tests, Host.Tests | `CreateStep_InvalidInput_Rejected` | |
| T-STRU-16 | `EtapesTests.Couloir_d_une_autre_carte_refuse_RG_C02` | 1 | Une étape ne se crée ni ne se déplace dans le couloir d'une autre carte (RG-C02). | Domain.Tests, Application.Tests, Host.Tests | `StepLane_FromOtherMap_RejectedRgC02` | clé étrangère composée `(lane_id, map_id)` à vérifier en Persistence |
| T-STRU-17 | `EtapesTests.Depot_sur_case_occupee_decale_le_bloc_contigu_RG_C04` | 1 | Un dépôt sur une case occupée décale le seul bloc contigu (RG-C04). | Domain.Tests, Host.Tests | `DropOnOccupiedCell_ShiftsContiguousBlock` | |
| T-STRU-18 | `EtapesTests.Les_autres_couloirs_ne_bougent_pas` | 1 | Le décalage ne touche pas les autres couloirs. | Domain.Tests, Host.Tests | `Shift_DoesNotAffectOtherLanes` | |
| T-STRU-19 | `EtapesTests.Deplacement_dans_le_couloir_vers_la_gauche_et_vers_la_droite` | 1 | Un déplacement dans le couloir, vers la gauche ou la droite, décale les voisines sans que l'étape se bloque elle-même. | Domain.Tests, Persistence.Tests, Host.Tests | `MoveStepWithinLane_LeftAndRight_ShiftsNeighbours` | stationnement puis libération de case (Persistence, index unique) |
| T-STRU-20 | `EtapesTests.Deplacement_vers_un_autre_couloir` | 1 | Un déplacement vers un autre couloir décale l'occupante. | Domain.Tests, Host.Tests | `MoveStepToOtherLane_ShiftsOccupant` | |
| T-STRU-21 | `EtapesTests.Modification_concurrente_detectee` | 1 | Une version périmée sur une étape renvoie 409 et une version illisible `VERSION_INVALIDE`. | Application.Tests, Persistence.Tests, Host.Tests | `PatchStep_StaleVersionConflict_UnreadableVersionRejected` | le format de version change (plus de `rowversion` base64) : adapter le cas « illisible » ; If-Match obligatoire en mode Serveur (428 si absent) |
| T-STRU-22 | `EtapesTests.Etat_d_automatisation_et_assignation` | 1 | État d'automatisation et assignation se modifient, valeurs inconnues et types faux refusés. | Domain.Tests, Application.Tests, Host.Tests | `PatchStep_AutomationStateAndAssignee` | toute modification d'une étape, assignation comprise, met à jour `updated_at`, `updated_by` et la version (AR-32 ; en 1.x seule l'assignation séparée ne le faisait pas) |
| T-STRU-23 | `EtapesTests.Chemins_de_lecture_gauche_droite_haut_bas` | 1 | Les chemins de lecture suivent colonnes puis couloirs (gauche-droite, haut-bas). | Domain.Tests, Host.Tests | `ReadingPaths_LeftToRightThenTopToBottom` | une seule implémentation de l'ordre de lecture (section 13) |
| T-STRU-24 | `EtapesTests.Creations_concurrentes_dans_la_meme_case` | 1 | Dix créations concurrentes dans la même case réussissent toutes, avec des colonnes et des codes distincts. | Persistence.Tests, Host.Tests | `ConcurrentCreatesSameCell_AllSucceedDistinctColumnsAndCodes` | sérialisation par `structure_version` (4.7) au lieu d'UPDLOCK ; à jouer sur les deux bases |
| T-STRU-25 | `LiensTests.Creation_libelle_et_suppression` | 1 | Une flèche se crée avec libellé, se renomme avec version, un libellé vide devient null, une version périmée est refusée et une flèche supprimée est introuvable. | Application.Tests, Host.Tests | `Flow_CreateRenameDelete_WithVersionCheck` | section 13 : ajouter qu'un libellé inchangé n'efface pas les traductions |
| T-STRU-26 | `LiensTests.Doublon_refuse_mais_sens_inverse_et_boucle_acceptes` | 1 | Un doublon de flèche est refusé, le sens inverse accepté et une flèche réflexive refusée. | Domain.Tests, Persistence.Tests, Host.Tests | `Flow_DuplicateRejected_ReverseAllowed_SelfLoopRejected` | |
| T-STRU-27 | `LiensTests.Lien_entre_cartes_refuse_RG_C05` | 1 | Une flèche entre deux cartes est refusée (RG-C05) et une cible inconnue introuvable. | Domain.Tests, Application.Tests, Host.Tests | `Flow_AcrossMaps_RejectedRgC05` | RG-C05 n'est qu'un déclencheur SQL Server en 1.x : règle portée dans le domaine |
| T-STRU-28 | `LiensTests.Avertissements_debut_et_fin_RG_C08` | 1 | Les avertissements RG-C08 signalent une fin isolée, un début avec flèche entrante et une fin avec flèche sortante. | Domain.Tests, Host.Tests | `LayoutWarnings_StartAndEnd_RgC08` | |
| T-STRU-29 | `LiensTests.Supprimer_une_etape_supprime_ses_liens` | 1 | Supprimer une étape supprime ses flèches et garde les autres. | Application.Tests, Host.Tests | `DeleteStep_DeletesItsFlowsOnly` | |
| T-STRU-30 | `DispositionTests.Echange_de_deux_etapes_et_ordre_des_couloirs_en_un_appel` | 1 | La disposition groupée échange deux étapes et réordonne les couloirs en un appel. | Domain.Tests, Persistence.Tests, Host.Tests | `Layout_SwapStepsAndReorderLanes_SingleCall` | stationnement des cases (Persistence) |
| T-STRU-31 | `DispositionTests.Etat_final_en_double_refuse_sans_rien_ecrire` | 1 | Une disposition dont l'état final a une case en double, ou qui cite deux fois une étape, est refusée sans rien écrire. | Domain.Tests, Host.Tests | `Layout_DuplicateFinalCellOrStep_RejectedWithoutWrite` | |
| T-STRU-32 | `DispositionTests.Etape_d_une_autre_carte_refusee` | 1 | Une disposition citant une étape d'une autre carte est refusée. | Application.Tests, Host.Tests | `Layout_StepFromOtherMap_Rejected` | |

### TraductionsTests.cs

| N° | Test 1.x | Cas | Intention (ce que le test prouve, une phrase) | Cible 2.0 | Nom 2.0 proposé (anglais, PascalCase avec _ ) | Remarque |
|---|---|---|---|---|---|---|
| T-TRAD-01 | `Les_textes_sans_traduction_sont_donnes_et_les_traductions_enregistrees_en_une_passe` | 1 | La liste « manquants » donne les textes non traduits dans leur langue d'origine, l'import les enregistre en une passe et la portée « auto » les redonne. | Application.Tests, Host.Tests | `Translations_MissingListed_ImportedInOnePass` | écriture idempotente sous le verrou de projet (5.6 point 11) ; `MERGE` réécrit en upsert portable |
| T-TRAD-02 | `Une_traduction_manuelle_n_est_ecrasee_qu_avec_la_portee_tout` | 1 | Une traduction manuelle n'est écrasée qu'avec la portée « tout », et devient alors automatique. | Domain.Tests, Application.Tests, Host.Tests | `Translations_ManualOverwrittenOnlyWithScopeAll` | |
| T-TRAD-03 | `Lignes_ignorees_texte_modifie_depuis_l_export_objet_supprime_cellule_vide` | 1 | Les lignes dont le texte a changé depuis l'export, dont l'objet est supprimé ou dont la cellule est vide sont ignorées avec leur raison. | Domain.Tests, Application.Tests, Host.Tests | `Translations_StaleDeletedOrEmptyRows_IgnoredWithReason` | |
| T-TRAD-04 | `Une_traduction_invalide_refuse_tout_le_fichier` | 1 | Une seule traduction invalide refuse tout le fichier, avec le numéro de ligne. | Domain.Tests, Application.Tests, Host.Tests | `Translations_OneInvalidRow_RejectsWholeFile` | message « L{n} » construit hors du service (dette 6.5) |
| T-TRAD-05 | `Renommer_oublie_la_traduction_automatique_et_la_carte_detaillee_suit_son_etape` | 1 | La sous-carte suit la traduction de son étape et un renommage oublie la traduction automatique. | Application.Tests, Host.Tests | `RenameStep_DropsAutoTranslation_SubMapFollowsStep` | section 13 : ajouter qu'un PATCH sans changement de texte garde la traduction (étape et flèche) |
| T-TRAD-06 | `Reserve_a_l_animateur` | 1 | Lister et importer les traductions est réservé à l'animateur. | Application.Tests, Host.Tests | `Translations_FacilitatorOnly` | |

## Partie 2 - Tests C# unitaires (`api.Tests\Unitaires`)

### DomaineTests.cs

Le fichier contient quatre classes : `CodesTests`, `CommandeTests`, `DispositionTests`, `SelectionTests`.

| N° | Test 1.x | Cas | Intention (ce que le test prouve, une phrase) | Cible 2.0 | Nom 2.0 proposé (anglais, PascalCase avec _ ) | Remarque |
|---|---|---|---|---|---|---|
| T-UDOM-01 | `CodesTests.Code_projet_3_a_6_majuscules` | 9 | Un code projet valide fait 3 à 6 majuscules ASCII, sans chiffre, accent, vide ni null. | Domain.Tests | `ProjectCode_ThreeToSixUppercaseAsciiLetters` | |
| T-UDOM-02 | `CodesTests.Formats_des_codes_conformes_au_tableau_7_1` | 1 | Les codes d'étape, de couloir, d'irritant et de solution suivent leur format (padding, au-delà de 999). | Domain.Tests | `ReadableCodes_FollowFormatTable` | |
| T-UDOM-03 | `CodesTests.Normalisation_des_codes_d_etape_saisis` | 9 | Un code d'étape saisi (complet, court, minuscules, espaces) est normalisé, les saisies non conformes donnent null. | Domain.Tests | `NormaliseStepCode_FromUserInput` | |
| T-UDOM-04 | `CodesTests.Nom_de_dossier_suggere` | 5 | Le nom de dossier suggéré combine code et libellé sans accent ni caractère spécial, borné à 30 caractères. | Interchange.Tests | `SuggestedFolderName_CodeAndSanitisedLabel` | phase 4 : nom du fichier d'export `.pmap` (1.x `Codes.NomDossier`, appelé par `ServiceFichiers.cs:45`), pas une règle du domaine |
| T-UDOM-05 | `CommandeTests.Lien_simple` | 1 | La commande « A > B » donne une flèche ligne 1. | Domain.Tests | `ParseCommand_SimpleFlow` | |
| T-UDOM-06 | `CommandeTests.Lien_avec_libelle_et_deux_points_dans_le_libelle` | 1 | Le libellé suit le premier deux-points et peut en contenir d'autres. | Domain.Tests | `ParseCommand_LabelMayContainColons` | |
| T-UDOM-07 | `CommandeTests.Chaine_le_libelle_va_au_dernier_lien` | 1 | Une chaîne A > B > C crée deux flèches et le libellé va à la dernière. | Domain.Tests | `ParseCommand_Chain_LabelOnLastFlow` | |
| T-UDOM-08 | `CommandeTests.Plusieurs_lignes_numerotees` | 1 | Les lignes sont numérotées en comptant les lignes vides et les fins de ligne CRLF. | Domain.Tests | `ParseCommand_MultipleLines_NumberedIncludingBlanks` | |
| T-UDOM-09 | `CommandeTests.Erreurs_de_syntaxe` | 5 | Syntaxe incomplète, étape inconnue, flèche réflexive et commande vide donnent chacune leur code d'erreur. | Domain.Tests | `ParseCommand_SyntaxErrors_ReturnCode` | |
| T-UDOM-10 | `CommandeTests.Libelle_trop_long_refuse` | 1 | Un libellé de plus de 60 caractères est refusé. | Domain.Tests | `ParseCommand_LabelTooLong_Rejected` | |
| T-UDOM-11 | `CommandeTests.Une_ligne_fausse_n_empeche_pas_l_analyse_des_autres` | 1 | Une ligne fausse est signalée sans empêcher l'analyse des autres. | Domain.Tests | `ParseCommand_BadLine_OthersStillParsed` | |
| T-UDOM-12 | `DispositionTests.Case_libre_rien_a_decaler` | 1 | Une case libre ne demande aucun décalage. | Domain.Tests | `ShiftRange_FreeCell_None` | |
| T-UDOM-13 | `DispositionTests.Seul_le_bloc_contigu_est_decale` | 1 | Seul le bloc contigu à partir de la case visée est décalé. | Domain.Tests | `ShiftRange_OnlyContiguousBlock` | |
| T-UDOM-14 | `DispositionTests.Bloc_d_une_seule_case` | 1 | Un bloc d'une seule case se décale seul. | Domain.Tests | `ShiftRange_SingleCellBlock` | |
| T-UDOM-15 | `DispositionTests.Case_en_double_detectee` | 1 | Une case en double est détectée, des rangées différentes ne le sont pas. | Domain.Tests | `DuplicateCell_Detected_DifferentRowsAllowed` | |
| T-UDOM-16 | `DispositionTests.Rangs_branches_paralleles_lues_de_haut_en_bas_dans_le_couloir` | 1 | Dans une colonne, les branches parallèles se lisent de haut en bas avant la colonne suivante. | Domain.Tests | `ReadingRank_ParallelBranchesTopToBottom` | ordre de lecture unique (section 13) |
| T-UDOM-17 | `DispositionTests.Rangs_de_gauche_a_droite_puis_de_haut_en_bas` | 1 | Les rangs se lisent par colonne puis par couloir. | Domain.Tests | `ReadingRank_LeftToRightThenTopToBottom` | ordre de lecture unique (section 13) |
| T-UDOM-18 | `DispositionTests.Chemins_hierarchiques` | 1 | Le chemin hiérarchique s'écrit « 3 » ou « 1.3.2 ». | Domain.Tests | `HierarchicalPath_Format` | |
| T-UDOM-19 | `DispositionTests.Avertissements_debut_et_fin` | 1 | Un début avec flèche entrante et une fin avec flèche sortante sont signalés, un flux correct non. | Domain.Tests | `LayoutWarnings_StartIncomingEndOutgoing` | |
| T-UDOM-20 | `DispositionTests.Etape_isolee_signalee_des_que_la_carte_compte_deux_etapes` | 1 | Une étape isolée n'est signalée qu'à partir de deux étapes sur la carte. | Domain.Tests | `IsolatedStep_WarnedFromTwoSteps` | |
| T-UDOM-21 | `DispositionTests.Flux_autonomes_cote_a_cote_non_signales` | 1 | Des flux autonomes déclarés côte à côte ne sont pas signalés comme isolés. | Domain.Tests | `AutonomousFlows_NotWarnedAsIsolated` | |
| T-UDOM-22 | `DispositionTests.Couleurs_des_couloirs_en_alternance` | 1 | Les couleurs de couloir alternent sur un cycle de six et la validation exige `#RRGGBB` en majuscules. | Domain.Tests | `LaneColours_CycleOfEightHues_LowercaseNormalised` | cycle de 8 teintes de la charte, premier couloir = teinte 1 (AR-45, 9.2) au lieu de 6 ; minuscules acceptées et mises en majuscules (AR-11) : l'assertion de refus des minuscules devient une assertion de normalisation |
| T-UDOM-23 | `SelectionTests.Blocs_regroupe_les_etapes_reliees_entre_elles` | 1 | La sélection se découpe en blocs d'étapes reliées entre elles. | Domain.Tests | `Selection_GroupsConnectedStepsIntoBlocks` | |
| T-UDOM-24 | `SelectionTests.Raccords_relie_avant_et_apres_chaque_bloc_retire` | 1 | Les raccords relient l'avant et l'après de chaque bloc retiré en gardant le libellé entrant. | Domain.Tests | `Stitch_LinksBeforeAndAfterEachRemovedBlock` | |
| T-UDOM-25 | `SelectionTests.Raccords_garde_les_libelles_d_un_aiguillage_et_ne_double_pas_une_fleche` | 1 | Les raccords gardent les libellés d'un aiguillage, ne doublent pas une flèche existante, s'abstiennent si ambigus et ne créent pas de boucle. | Domain.Tests | `Stitch_KeepsSwitchLabels_NoDuplicateNoAmbiguousNoSelfLoop` | |
| T-UDOM-26 | `SelectionTests.Extremites_entrees_et_sorties_du_bloc` | 1 | Les extrémités d'un bloc sont ses entrées et ses sorties. | Domain.Tests | `BlockEnds_EntriesAndExits` | |

### FichesTests.cs (unitaires)

Classe unique : `FichesDomaineTests`.

| N° | Test 1.x | Cas | Intention (ce que le test prouve, une phrase) | Cible 2.0 | Nom 2.0 proposé (anglais, PascalCase avec _ ) | Remarque |
|---|---|---|---|---|---|---|
| T-UFIC-01 | `Les_cles_sont_celles_de_la_base` | 1 | Les clés de champs et les natures d'opération sont celles stockées en base. | Domain.Tests | `SheetFieldKeysAndNatures_MatchStoredValues` | |
| T-UFIC-02 | `Saisies_nettoyees_les_champs_vides_disparaissent` | 1 | Les saisies sont nettoyées, les champs vides disparaissent, la durée est arrondie au centième et une fiche sans contenu est vide. | Domain.Tests | `SheetInput_Trimmed_EmptyFieldsDropped_DurationRounded` | |
| T-UFIC-03 | `Completude_arrondie_sur_sept_champs` | 1 | La complétude est arrondie sur sept éléments (cinq champs hors acteur, entrées, sorties). | Domain.Tests | `Completeness_RoundedOverSevenItems` | une seule implémentation (dette 6.2) |
| T-UFIC-04 | `Contenu_fige_puis_relu_a_l_identique` | 1 | Le contenu figé à la validation est un JSON anglais lisible (accents, sauts de ligne) relu à l'identique. | Domain.Tests | `FrozenSheetContent_RoundTripsExactly` | |
| T-UFIC-05 | `Version_validee_relue_sans_les_champs_retires_depuis` | 1 | Une version validée ancienne se relit sans les champs retirés depuis et sans les natures absentes. | Domain.Tests, Interchange.Tests | `ReadOldValidatedVersion_IgnoresRetiredFields` | aussi utile à la migration 1.x (7.6) |
| T-UFIC-06 | `Meme_contenu_compare_valeurs_deroule_et_texte_libre` | 1 | La comparaison de contenu tient compte des valeurs, des champs, du déroulé et du texte libre. | Domain.Tests | `SameContent_ComparesValuesProcedureAndFreeText` | |
| T-UFIC-07 | `Saisies_refusees` | 7 | Fréquence hors liste ou avec nombre, durée négative, textuelle ou trop grande, clé inconnue et clé réservée sont refusées avec le champ. | Domain.Tests | `SheetInput_InvalidValues_RejectedWithField` | |
| T-UFIC-08 | `Adresse_url_http_ou_chemin_de_fichier` | 8 | Une adresse de document est une URL http(s) ou un chemin de fichier ; ftp, file, chemin incomplet et nom seul sont refusés, une adresse vide vaut null. | Domain.Tests | `DocumentAddress_HttpUrlOrFilePath_OthersRejected` | lien jamais suivi par le serveur (5.4) |

### FormatPmapTests.cs

Classe unique : `FormatPmapTests`.

| N° | Test 1.x | Cas | Intention (ce que le test prouve, une phrase) | Cible 2.0 | Nom 2.0 proposé (anglais, PascalCase avec _ ) | Remarque |
|---|---|---|---|---|---|---|
| T-UPMA-01 | `Exemple_minimal_de_la_documentation` | 1 | L'exemple minimal se lit avec ses valeurs par défaut (langue fr, colonnes absentes). | Interchange.Tests | `ReadPmap_MinimalExample` | |
| T-UPMA-02 | `Exemple_complet_sous_carte_raci_traductions_incertitudes` | 1 | L'exemple complet se lit avec code, traductions, RACI, sous-carte, automatisation, libellés et incertitudes. | Interchange.Tests | `ReadPmap_FullExample_AllSections` | |
| T-UPMA-03 | `Exemple_du_guide_pour_IA_accepte_tel_quel` | 1 | L'exemple du guide pour IA s'ouvre tel quel, avec ses rangées et ses incertitudes. | Interchange.Tests | `ReadPmap_AiGuideExample_AcceptedAsIs` | guide mis à jour en 1.1 (7.5) |
| T-UPMA-04 | `Copier_coller_depuis_une_IA_bloc_de_code_BOM_virgules_finales_commentaires` | 1 | La lecture tolère bloc de code Markdown, BOM, virgules finales et commentaires. | Interchange.Tests | `ReadPmap_ToleratesCodeFenceBomTrailingCommasComments` | |
| T-UPMA-05 | `Ni_JSON_ni_Process_map_ni_version_lisible` | 1 | Fichier vide, JSON invalide localisé, tableau, format étranger et version majeure non prise en charge sont refusés ; une version 1.x mineure est lue. | Interchange.Tests | `ReadPmap_EmptyInvalidForeignOrUnsupportedVersion_Rejected` | la 2.0 lit 1.0 et écrit 1.1 (7.5) ; valeur `format` (`ariane`) à garder en lecture |
| T-UPMA-06 | `Propriete_inconnue_signalee_avec_les_proprietes_possibles` | 1 | Une propriété inconnue est signalée avec son chemin et la liste des propriétés possibles. | Interchange.Tests | `ReadPmap_UnknownProperty_ListsAllowedProperties` | |
| T-UPMA-07 | `Champs_obligatoires_types_et_valeurs` | 1 | Champs obligatoires, types et listes fermées sont contrôlés avec un message par emplacement. | Interchange.Tests | `ReadPmap_RequiredFieldsTypesAndValues_Located` | |
| T-UPMA-08 | `Textes_bornes_sans_HTML_ni_caracteres_de_controle` | 1 | Les textes sont bornés, sans HTML ni caractère de contrôle, et trimés. | Interchange.Tests, Domain.Tests | `ReadPmap_TextsBoundedNoHtmlNoControlChars` | contrôle de texte unique avec l'API (dette 6.4) |
| T-UPMA-09 | `Identifiants_uniques_dans_tout_le_fichier` | 1 | Les identifiants sont uniques dans tout le fichier, couloirs et étapes confondus, et non vides. | Interchange.Tests | `ReadPmap_IdsUniqueAcrossFile` | |
| T-UPMA-10 | `Regle_1_une_etape_dans_un_couloir_de_sa_carte` | 1 | Une étape doit être dans un couloir de sa propre carte. | Interchange.Tests | `ReadPmap_StepLaneMustBelongToItsMap` | |
| T-UPMA-11 | `Regles_2_et_3_liens_dans_la_meme_carte_sans_boucle_ni_doublon` | 1 | Les flèches relient deux étapes de la même carte, sans réflexive, doublon, cible inconnue ni libellé trop long. | Interchange.Tests | `ReadPmap_FlowsSameMapNoSelfLoopNoDuplicate` | |
| T-UPMA-12 | `Regle_4_colonnes_pour_toutes_ou_pour_aucune_et_sans_case_en_double` | 1 | Les colonnes sont données pour toutes les étapes ou aucune, sans case en double. | Interchange.Tests | `ReadPmap_ColumnsAllOrNone_NoDuplicateCell` | |
| T-UPMA-13 | `Regle_5_ni_debut_ni_fin_detailles_et_sous_carte_vide_ignoree` | 1 | Un début ou une fin ne porte pas de sous-carte et une sous-carte sans étape est ignorée. | Interchange.Tests | `ReadPmap_NoSubMapOnStartEnd_EmptySubMapIgnored` | |
| T-UPMA-14 | `Regle_6_raci` | 1 | Le RACI du fichier tolère R sur son propre couloir, refuse un autre rôle sur ce couloir, les lettres hors paramètre, les couloirs inconnus et un second A. | Interchange.Tests | `ReadPmap_RaciRules` | 7.5 : ajouter le refus d'un RACI sur début, fin, losange ET et OU inclusif |
| T-UPMA-15 | `Messages_en_anglais` | 1 | Les messages d'erreur du fichier existent en anglais. | Interchange.Tests | `ReadPmap_ErrorMessagesInEnglish` | messages .pmap à sortir des chaînes en dur (dette 6.5) |
| T-UPMA-16 | `Nombre_d_erreurs_borne` | 1 | Le nombre d'erreurs rapportées est borné. | Interchange.Tests | `ReadPmap_ErrorCountCapped` | |
| T-UPMA-17 | `Placement_suit_les_fleches_d_un_couloir_a_l_autre` | 1 | Le placement automatique suit les flèches d'un couloir à l'autre, indépendamment de l'ordre du fichier. | Domain.Tests, Interchange.Tests | `AutoPlacement_FollowsFlowsAcrossLanes` | |
| T-UPMA-18 | `Placement_d_une_decision_les_branches_d_un_meme_couloir_se_decalent` | 1 | Les branches d'une décision dans un même couloir se décalent pour ne pas partager une case. | Domain.Tests, Interchange.Tests | `AutoPlacement_DecisionBranchesSameLane_Shifted` | |
| T-UPMA-19 | `Placement_branches_paralleles_dans_deux_couloirs_meme_colonne` | 1 | Des branches parallèles dans deux couloirs prennent la même colonne. | Domain.Tests, Interchange.Tests | `AutoPlacement_ParallelBranchesTwoLanes_SameColumn` | |
| T-UPMA-20 | `Placement_etapes_isolees_a_la_suite_et_boucle_rompue` | 1 | Les étapes isolées se placent à la suite par couloir et une boucle est rompue à la première étape du fichier. | Domain.Tests, Interchange.Tests | `AutoPlacement_IsolatedStepsAndBrokenLoop` | |
| T-UPMA-21 | `Ecriture_relue_a_l_identique_et_lisible` | 1 | L'écriture est stable par relecture et lisible (accents en clair, LF, indentation de 2, valeurs par défaut omises). | Interchange.Tests | `WritePmap_StableOnReread_HumanReadable` | `generated_by` passe à Topogenia ; écriture en 1.1 |
| T-UPMA-22 | `Ecriture_echappe_ce_qui_doit_l_etre` | 1 | L'écriture échappe guillemets et barres obliques inverses et se relit. | Interchange.Tests | `WritePmap_EscapesQuotesAndBackslashes` | |

### InfrastructureTests.cs

Le fichier contient quatre classes : `MessagesTests`, `LanguesTests`, `TextesTests`, `VersionsTests`.

| N° | Test 1.x | Cas | Intention (ce que le test prouve, une phrase) | Cible 2.0 | Nom 2.0 proposé (anglais, PascalCase avec _ ) | Remarque |
|---|---|---|---|---|---|---|
| T-UINF-01 | `MessagesTests.Chaque_message_existe_dans_les_deux_langues_et_differe` | 1 | Chaque code d'erreur a un message français et un message anglais, non vides et différents. | Application.Tests | `ErrorMessages_ExistInBothLanguagesAndDiffer` | catalogue des messages hors des services (dette 6.5) ; langues issues de la configuration |
| T-UINF-02 | `MessagesTests.Variables_remplacees` | 1 | Les variables des messages sont remplacées dans les deux langues. | Application.Tests | `ErrorMessages_PlaceholdersReplaced` | |
| T-UINF-03 | `MessagesTests.Code_inconnu_donne_le_message_generique` | 1 | Un code inconnu donne le message générique. | Application.Tests | `ErrorMessages_UnknownCode_GenericMessage` | |
| T-UINF-04 | `MessagesTests.Codes_d_erreur_utilises_par_les_services_ont_un_message` | 1 | Tout code d'erreur utilisé dans le code source a un message traduit. | Application.Tests | `AllErrorCodesUsedInCode_HaveMessage` | en 2.0, contrôle par réflexion sur un catalogue typé plutôt que par expression régulière sur les sources |
| T-UINF-05 | `LanguesTests.Langue_du_navigateur_sinon_anglais` | 8 | La langue est déduite de `Accept-Language` avec poids, sinon anglais. | Host.Tests | `BrowserLanguage_FromAcceptLanguage_DefaultEnglish` | liste des langues en configuration (section 13) |
| T-UINF-06 | `TextesTests.Texte_obligatoire_nettoye` | 1 | Un texte obligatoire est trimé. | Domain.Tests | `RequiredText_Trimmed` | |
| T-UINF-07 | `TextesTests.Texte_obligatoire_vide_refuse` | 3 | Un texte obligatoire vide, blanc ou null est refusé. | Domain.Tests | `RequiredText_EmptyOrNull_Rejected` | |
| T-UINF-08 | `TextesTests.Balises_html_refusees` | 3 | Balises HTML et commentaires sont refusés. | Domain.Tests | `Text_HtmlTags_Rejected` | |
| T-UINF-09 | `TextesTests.Comparaisons_acceptees` | 3 | Les comparaisons avec `<` ne sont pas prises pour des balises. | Domain.Tests | `Text_ComparisonSigns_Accepted` | |
| T-UINF-10 | `TextesTests.Longueur_maximale` | 1 | Un texte trop long est refusé. | Domain.Tests | `Text_TooLong_Rejected` | |
| T-UINF-11 | `TextesTests.Caracteres_de_controle_refuses` | 1 | Un caractère de contrôle est refusé. | Domain.Tests | `Text_ControlCharacters_Rejected` | |
| T-UINF-12 | `TextesTests.Facultatif_vide_donne_null` | 1 | Un texte facultatif vide vaut null. | Domain.Tests | `OptionalText_Blank_IsNull` | |
| T-UINF-13 | `TextesTests.Login_windows` | 7 | Un login Windows valide a la forme `DOMAINE\compte`, sans UPN, espace ni niveau supplémentaire. | Domain.Tests | `WindowsLogin_DomainBackslashAccountOnly` | |
| T-UINF-14 | `VersionsTests.If_match_lu_avec_ou_sans_guillemets` | 1 | L'en-tête `If-Match` se lit avec ou sans guillemets. | Host.Tests | `IfMatch_ParsedWithOrWithoutQuotes` | format du jeton de version 2.0 (plus de `rowversion` 8 octets) |
| T-UINF-15 | `VersionsTests.If_match_absent_donne_null` | 1 | Un `If-Match` absent donne « pas de contrôle ». | Host.Tests | `IfMatch_Absent_ServerModeRequires428` | défaut 1.x corrigé, inverser l'assertion en mode Serveur : `If-Match` absent sur un objet versionné renvoie 428 (5.3, 5.6 point 3) ; inchangé en mode Poste |
| T-UINF-16 | `VersionsTests.If_match_illisible_refuse` | 2 | Un `If-Match` illisible ou de mauvaise longueur est refusé (`VERSION_INVALIDE`). | Host.Tests | `IfMatch_Unreadable_Rejected` | jeux de données à adapter au nouveau format de version |
| T-UINF-17 | `VersionsTests.Durees_relatives` | 1 | Les durées relatives s'écrivent en français et en anglais (instant, minutes, heures, jours). | Host.Tests | `RelativeDurations_FrenchAndEnglish` | `Durees` sorti de `ServiceStructure` (dette 6.1) ; durée relative formatée par l'hôte (A-2.16-07, §3.3), phase 4 |

## Partie 3 - Tests vitest (`web\src\**\*.test.ts(x)`)

Les fonctions pures du client (alignement, géométrie, export, presse-papiers, RACI, classeur)
restent côté client en 2.0 (`model/`, `features/map/…`) ; leurs tests se portent en `web unit`.
Les règles qui deviennent serveur en 2.0 (historique, ordre des opérations d'annulation) changent
de niveau.

### carte/alignement.test.ts

| N° | Test 1.x | Cas | Intention (ce que le test prouve, une phrase) | Cible 2.0 | Nom 2.0 proposé (anglais, PascalCase avec _ ) | Remarque |
|---|---|---|---|---|---|---|
| T-WALI-01 | `carte deja alignee : rien a deplacer` | 1 | Une carte déjà alignée ne produit aucun déplacement. | web unit | `Align_AlreadyAligned_NoMoves` | |
| T-WALI-02 | `resserre les colonnes vides` | 1 | La baguette resserre les colonnes vides du flux. | web unit | `Align_CompactsEmptyColumns` | |
| T-WALI-03 | `ramene sur sa rangee une etape descendue a la main` | 1 | Une étape descendue à la main revient sur sa rangée. | web unit | `Align_ReturnsManuallyLoweredStepToItsRow` | |
| T-WALI-04 | `pousse une etape placee avant son predecesseur, sans toucher au couloir` | 1 | Une étape placée avant son prédécesseur est poussée à droite sans changer de couloir. | web unit | `Align_PushesStepBeforePredecessor_KeepsLane` | |
| T-WALI-05 | `garde une fleche verticale voulue et ignore les boucles de retour` | 1 | Une flèche verticale voulue est gardée et les boucles de retour sont ignorées. | web unit | `Align_KeepsIntendedVerticalFlow_IgnoresBackLoops` | |
| T-WALI-06 | `decision : la branche courte et la branche longue restent lisibles` | 1 | Après une décision, branche courte et branche longue restent lisibles. | web unit | `Align_DecisionShortAndLongBranchesReadable` | |
| T-WALI-07 | `branches paralleles d'un meme couloir : une rangee par branche, chacune suivie` | 1 | Les branches parallèles d'un même couloir gardent chacune leur rangée et sont alignées. | web unit | `Align_ParallelBranchesSameLane_OneRowEach` | |
| T-WALI-08 | `retire les rangees vides` | 1 | Les rangées vides sont retirées. | web unit | `Align_RemovesEmptyRows` | |
| T-WALI-09 | `un morceau non relie garde sa premiere colonne` | 1 | Un morceau non relié garde sa première colonne. | web unit | `Align_DisconnectedPart_KeepsFirstColumn` | |
| T-WALI-10 | `jamais deux etapes dans la meme case` | 1 | L'alignement ne met jamais deux étapes dans la même case. | web unit | `Align_NeverTwoStepsInSameCell` | |
| T-WALI-11 | `decision : oui a droite, non (contournement) par la pointe haute` | 1 | La sortie « oui » d'une décision part à droite et le contournement par la pointe haute. | web unit | `GatewayExit_YesRight_BypassTop` | routage des flèches revu (9.7) : vérifier que la règle est reprise |
| T-WALI-12 | `sortie vers un couloir du dessous : par la pointe basse si la droite est prise` | 1 | Une sortie vers le couloir du dessous part par la pointe basse si la droite est prise. | web unit | `GatewayExit_LowerLane_BottomWhenRightTaken` | idem 9.7 |
| T-WALI-13 | `pointe basse seulement si la ligne d'arrivee est libre jusqu'a la cible` | 1 | La pointe basse n'est utilisée que si la ligne d'arrivée est libre jusqu'à la cible. | web unit | `GatewayExit_BottomOnlyIfTargetRowFree` | idem 9.7 |
| T-WALI-14 | `une etape ordinaire n'est pas concernee` | 1 | Les pointes de départ ne concernent que les losanges. | web unit | `GatewayExit_OrdinaryStepNotConcerned` | |
| T-WALI-15 | `un second debut se place juste avant l etape qu il alimente` | 1 | Un second début reste juste avant l'étape qu'il alimente. | web unit | `Align_SecondaryStart_StaysJustBeforeTarget` | |
| T-WALI-16 | `une etape isolee ne prend pas la case du flux` | 1 | Une étape isolée ne prend pas la case du flux et descend d'une rangée. | web unit | `Align_IsolatedStep_DoesNotTakeFlowCell` | |

### carte/export.test.ts

| N° | Test 1.x | Cas | Intention (ce que le test prouve, une phrase) | Cible 2.0 | Nom 2.0 proposé (anglais, PascalCase avec _ ) | Remarque |
|---|---|---|---|---|---|---|
| T-WEXP-01 | `etapes : acteur R, roles RACI, detaillee, etapes suivantes` | 1 | L'onglet Étapes donne code, chemin, type, acteur R, rôles RACI, détaillée et étapes suivantes. | web unit | `ExportSteps_ActorRaciDetailedNextSteps` | |
| T-WEXP-02 | `liens avec codes et libelles` | 1 | L'onglet Liens donne codes, libellés d'étape et libellé de flèche. | web unit | `ExportFlows_CodesAndLabels` | |
| T-WEXP-03 | `matrice RACI sans debut ni fin, lettres centrees` | 1 | La matrice RACI exclut début et fin et centre les lettres. | web unit | `ExportRaciMatrix_NoStartEnd_LettersCentred` | |
| T-WEXP-04 | `avec la variante RASCI, la colonne Soutient apparait` | 1 | Avec la variante RASCI, la colonne « Soutient » apparaît. | web unit | `ExportSteps_Rasci_AddsSupportColumn` | |
| T-WEXP-05 | `se relit : trois onglets, en-tetes figes, contenu et couleurs RACI` | 1 | Le classeur généré se relit avec trois onglets, en-têtes figés, contenu et couleurs RACI. | web unit | `ExcelWorkbook_RereadThreeSheetsFrozenHeadersColours` | couleurs issues des jetons de la charte (9.2) |
| T-WEXP-06 | `noms de fichiers sans accents ni caracteres speciaux` | 1 | Les noms de fichiers d'export sont sans accent ni caractère spécial, avec un repli. | web unit | `ExportFileName_NoAccentsOrSpecialChars` | |
| T-WEXP-07 | `noms d onglets acceptes par Excel` | 1 | Les noms d'onglets sont nettoyés et bornés à 31 caractères. | web unit | `SheetName_AcceptedByExcel` | |
| T-WEXP-08 | `texte du PDF en Latin-1 (accents francais conserves)` | 1 | Le texte du PDF est converti en Latin-1 en gardant les accents français. | web unit | `PdfText_Latin1_KeepsFrenchAccents` | |
| T-WEXP-09 | `resolution doublee, bornee pour les tres grandes cartes` | 1 | La résolution de l'export est doublée puis bornée pour les très grandes cartes. | web unit | `ExportPixelRatio_DoubledThenCapped` | |

### carte/geometrie.test.ts

| N° | Test 1.x | Cas | Intention (ce que le test prouve, une phrase) | Cible 2.0 | Nom 2.0 proposé (anglais, PascalCase avec _ ) | Remarque |
|---|---|---|---|---|---|---|
| T-WGEO-01 | `position et case sont reciproques` | 1 | La position d'une case et la case d'une position sont réciproques. | web unit | `Grid_PositionAndCell_AreInverse` | |
| T-WGEO-02 | `un depot proche est aimante a la case la plus proche` | 1 | Un dépôt proche est aimanté à la case la plus proche. | web unit | `Grid_DropSnapsToNearestCell` | |
| T-WGEO-03 | `hors des couloirs : ramene au premier ou au dernier, jamais de colonne negative` | 1 | Un point hors des couloirs est ramené au premier ou au dernier couloir, sans colonne négative. | web unit | `Grid_PointOutsideLanes_ClampedNoNegativeColumn` | |
| T-WGEO-04 | `aucun couloir : aucune case` | 1 | Sans couloir, aucune case n'est trouvée. | web unit | `Grid_NoLanes_NoCell` | |
| T-WGEO-05 | `vers la droite : de la droite vers la gauche` | 1 | Une flèche vers la droite part de la poignée droite vers la poignée gauche. | web unit | `FlowHandles_Rightwards_RightToLeft` | |
| T-WGEO-06 | `meme colonne : verticale dans le bon sens` | 1 | Dans une même colonne, la flèche est verticale dans le bon sens. | web unit | `FlowHandles_SameColumn_VerticalCorrectDirection` | |
| T-WGEO-07 | `retour en arriere : boucle par le bas (RG-C07)` | 1 | Un retour en arrière est une boucle par le bas (RG-C07). | web unit | `FlowHandles_Backwards_LoopUnderneathRgC07` | |
| T-WGEO-08 | `la boucle passe sous les deux etapes` | 1 | Le tracé de boucle passe sous les deux étapes. | web unit | `LoopPath_PassesUnderBothSteps` | |
| T-WGEO-09 | `couloir de depart libre : coude juste avant l etape d arrivee` | 1 | Couloir de départ libre : le coude se place juste avant l'étape d'arrivée, jamais derrière une étape. | web unit | `ElbowX_FreeSourceLane_JustBeforeTarget` | voies distinctes (9.7) à ajouter |
| T-WGEO-10 | `couloir de depart encombre, arrivee libre : coude juste apres le depart` | 1 | Couloir de départ encombré : le coude se place juste après le départ. | web unit | `ElbowX_BusySourceLane_JustAfterSource` | |
| T-WGEO-11 | `seuls les liens entre couloirs vers la droite recoivent un coude` | 1 | Seules les flèches entre couloirs vers la droite reçoivent un coude. | web unit | `Elbow_OnlyCrossLaneRightwardFlows` | |
| T-WGEO-12 | `meme couloir avec des etapes entre les deux : contournement dans la bande au-dessus des etapes` | 1 | Une flèche qui saute des étapes du même couloir contourne par la bande au-dessus. | web unit | `Bypass_SameLaneWithStepsBetween_AboveBand` | |
| T-WGEO-13 | `trajet libre : fleche directe ; voisines, boucles et couloir libre inchanges` | 1 | Un trajet libre reste une flèche directe ; voisines, boucles et couloir libre sont inchangés. | web unit | `Routing_FreePath_DirectFlowOthersUnchanged` | |
| T-WGEO-14 | `entre couloirs, les deux encombres : contournement dans le couloir d arrivee` | 1 | Entre deux couloirs encombrés, le contournement passe dans le couloir d'arrivée. | web unit | `Bypass_BothLanesBusy_InTargetLane` | |
| T-WGEO-15 | `le trace ne touche aucune etape intermediaire : segments verticaux dans les allees` | 1 | Le tracé de contournement ne touche aucune étape intermédiaire et ses verticales sont dans les allées. | web unit | `BypassPath_TouchesNoIntermediateStep` | généralisé par les tests par propriétés de 9.7 (500 cartes) |
| T-WGEO-16 | `le couloir grandit d une bande par rangee ; les suivants descendent d autant` | 1 | Un couloir grandit d'une bande par rangée et les suivants descendent d'autant. | web unit | `Grid_LaneGrowsPerRow_NextLanesShift` | |
| T-WGEO-17 | `deposer vers le bas de la derniere rangee cree une rangee de plus` | 1 | Déposer sous la dernière rangée crée une rangée de plus. | web unit | `Grid_DropBelowLastRow_CreatesRow` | |
| T-WGEO-18 | `deux branches de la meme colonne : fleche verticale entre rangees, couloir et noeuds a la bonne hauteur` | 1 | Deux branches de la même colonne ont une flèche verticale, et couloir et nœuds sont à la bonne hauteur. | web unit | `ParallelRows_VerticalFlowAndHeights` | |
| T-WGEO-19 | `carte plus petite que l ecran : ni plus bas, ni plus haut, ni a gauche, ni a droite` | 1 | Une carte plus petite que l'écran reste accrochée en haut à gauche. | web unit | `ClampView_SmallMap_PinnedTopLeft` | |
| T-WGEO-20 | `carte plus haute que l ecran : on descend jusqu au dernier couloir, pas au-dela` | 1 | Une carte plus haute que l'écran défile jusqu'au dernier couloir, pas au-delà. | web unit | `ClampView_TallMap_ScrollsToLastLaneOnly` | |
| T-WGEO-21 | `carte plus large : a droite jusqu au bout, jamais a gauche de la premiere colonne` | 1 | Une carte plus large défile à droite jusqu'au bout, jamais à gauche de la première colonne. | web unit | `ClampView_WideMap_ScrollsRightToEndOnly` | |
| T-WGEO-22 | `le zoom est conserve et pris en compte : dezoomee, la grande carte tient et reste en haut` | 1 | Le zoom est conservé et, dézoomée, une grande carte tient et reste en haut. | web unit | `ClampView_ZoomKept_ZoomedOutMapPinnedTop` | |
| T-WGEO-23 | `detaillee seulement si sa carte detaillee contient une etape` | 1 | Une étape n'est affichée détaillée que si sa sous-carte contient une étape. | web unit | `MacroStep_OnlyWhenSubMapHasSteps` | |
| T-WGEO-24 | `un fond par couloir derriere les etapes, puis les etapes` | 1 | La conversion produit un fond par couloir derrière les étapes, puis les étapes avec leurs données. | web unit | `ToCanvas_LaneBackgroundsThenSteps` | |
| T-WGEO-25 | `fleches typees, libelle repris, lien orphelin ignore` | 1 | Les flèches sont typées, gardent leur libellé, et un lien orphelin est ignoré. | web unit | `ToCanvas_TypedFlows_OrphanIgnored` | |
| T-WGEO-26 | `lecture seule : rien de deplacable ni de reliable` | 1 | En lecture seule, aucune étape n'est déplaçable ni reliable. | web unit | `ToCanvas_ReadOnly_NothingDraggableOrConnectable` | |
| T-WGEO-27 | `rien a refaire quand rien n a bouge` | 1 | Un instantané identique ne produit aucune disposition à refaire. | Application.Tests, Host.Tests | `Undo_NoPositionChange_NothingToRelayout` | remplacé par undo serveur (AR-48) : les instantanés de positions côté client disparaissent, le journal les remplace ; plus de test web unit d'instantané, la preuve passe par un test d'undo serveur |
| T-WGEO-28 | `retrouve les colonnes et l ordre des couloirs` | 1 | Un instantané rend les colonnes et l'ordre des couloirs d'avant. | Application.Tests, Host.Tests | `Undo_RestoresColumnsAndLaneOrder` | remplacé par undo serveur (AR-48) : l'inverse est dans `inverse_json` ; plus de test web unit d'instantané |
| T-WGEO-29 | `ignore les etapes disparues et les couloirs disparus` | 1 | Un instantané ignore les étapes et couloirs disparus. | Application.Tests, Host.Tests | `Undo_IgnoresVanishedStepsAndLanes` | remplacé par undo serveur (AR-48) ; plus de test web unit d'instantané |
| T-WGEO-30 | `bordure foncee de 30 %` | 1 | La bordure est la couleur foncée de 30 %, une valeur invalide est rendue telle quelle. | web unit | `DarkenColour_ThirtyPercent` | remplacé par les jetons d'encre des couloirs (9.2, 9.3) : la fonction d'assombrissement disparaît ; le test devient « l'encre d'un couloir est celle de sa teinte »|
| T-WGEO-31 | `reordonnancement des couloirs par glissement` | 1 | Le glissement d'un en-tête calcule le rang cible et réordonne la liste. | web unit | `LaneHeaderDrag_TargetRankAndReorder` | |
| T-WGEO-32 | `duree lisible` | 1 | Une durée en minutes s'affiche lisiblement (min, h, h mm). | web unit | `ReadableDuration_MinutesAndHours` | |
| T-WGEO-33 | `englobe les etapes remontees, grise les etrangeres recouvertes, absent sans remontee` | 1 | Le cadre de remontée englobe ses étapes, grise les étrangères recouvertes et n'existe pas sans remontée. | web unit | `PromotionFrame_BoundsMembers_GreysForeignSteps` | |
| T-WGEO-34 | `propose d abord les etapes du cadre, en ordre de lecture, sans debut ni fin` | 1 | L'affectation du reliquat propose d'abord les étapes du cadre, en ordre de lecture, sans début ni fin. | web unit | `LeftoverTargets_FrameStepsFirst_ReadingOrder` | ordre de lecture unique `model/readingOrder` (section 13) |
| T-WGEO-35 | `une verticale qui traverserait une etape passe par l allee de droite` | 1 | Une verticale qui traverserait une étape passe par l'allée de droite. | web unit | `Routing_VerticalCrossingStep_UsesRightAisle` | |
| T-WGEO-36 | `un losange ne sort pas par le haut si sa colonne est occupee au-dessus` | 1 | Un losange ne sort pas par le haut si sa colonne est occupée au-dessus. | web unit | `GatewayExit_NotTopWhenColumnAboveOccupied` | |
| T-WGEO-37 | `deux contournements vers la meme ligne ne se superposent pas` | 1 | Deux contournements vers la même ligne ne se superposent pas. | web unit | `Bypass_TwoToSameRow_DoNotOverlap` | |
| T-WGEO-38 | `la boucle reste dans la bande libre sous les etapes` | 1 | La boucle reste dans la bande libre sous les étapes. | web unit | `LoopPath_StaysInFreeBandBelowSteps` | |

### carte/historique.test.ts

La pile d'annulation 1.x vit dans le navigateur. En 2.0, elle est remplacée par le journal des
opérations côté serveur (4.6) : les règles de pile se testent sur le gestionnaire `undo` / `redo`
(Application.Tests, Persistence.Tests pour la profondeur et la purge), et le client ne garde
qu'une file d'envoi des commandes.

| N° | Test 1.x | Cas | Intention (ce que le test prouve, une phrase) | Cible 2.0 | Nom 2.0 proposé (anglais, PascalCase avec _ ) | Remarque |
|---|---|---|---|---|---|---|
| T-WHIS-01 | `annule dans l ordre inverse et retablit` | 1 | Les annulations se font dans l'ordre inverse et le rétablissement rejoue la dernière annulée. | Application.Tests, Host.Tests | `OperationLog_UndoReverseOrder_RedoLastUndone` | remplacé par undo serveur ; pile par carte et par utilisateur |
| T-WHIS-02 | `une nouvelle operation vide le retablissement` | 1 | Une nouvelle opération vide la pile de rétablissement. | Application.Tests | `OperationLog_NewOperation_ClearsRedoStack` | remplacé par undo serveur |
| T-WHIS-03 | `garde au moins ${NIVEAUX_HISTORIQUE} niveaux et oublie les plus anciens` | 1 | La pile garde 50 niveaux et oublie les plus anciens. | Application.Tests, Persistence.Tests | `OperationLog_Keeps50PerMapAndUser_DropsOldest` | remplacé par undo serveur ; profondeur 50 par carte et par utilisateur (4.6) |
| T-WHIS-04 | `pile vide : rien ne se passe` | 1 | Annuler ou rétablir une pile vide ne fait rien. | Application.Tests, Host.Tests | `OperationLog_EmptyStack_NothingToUndo` | remplacé par undo serveur ; réponse « rien à annuler » à définir |
| T-WHIS-05 | `un echec d annulation abandonne tout l historique et remonte l erreur` | 1 | Un échec d'annulation vide tout l'historique de la carte et remonte l'erreur. | Application.Tests, web unit | `Undo_Blocked_ReturnsOperationOutdatedWithAuthor` | remplacé par undo serveur : refus `OPERATION_DEPASSEE` (409) avec auteur et heure (4.6), ou `CARTE_MODIFIEE`, seuls codes de refus (AR-43) ; un refus vide la pile de l'utilisateur sur la carte, comme en 1.x, avec un message selon le code et « impossible » pour tout autre échec (AR-05) |
| T-WHIS-06 | `un echec de retablissement vide le futur` | 1 | Un échec de rétablissement vide tout l'historique de la carte. | Application.Tests | `Redo_Blocked_ReturnsOperationOutdated` | remplacé par undo serveur : un refus de rétablissement vide toute la pile de l'utilisateur sur la carte, comme en 1.x (`historique.ts:69-70` appelle `vider()`, test `historique.test.ts:79`) (AR-05) |
| T-WHIS-07 | `pas de double annulation pendant qu une operation est en cours` | 1 | Une seconde annulation est ignorée tant que la première est en cours. | web unit, Persistence.Tests | `Undo_NoConcurrentUndoWhileInFlight` | côté client : file d'envoi ; côté serveur : sérialisation par `structure_version` (4.7) |

### carte/magasin.test.ts

Ces tests simulent l'API et vérifient la suite exacte d'appels qu'envoie le magasin pour faire
et défaire chaque opération. En 2.0, « défaire » est un seul appel `undo` (ou `redo`) : la suite
d'appels inverses disparaît du client. Chaque test se scinde donc en deux : côté client
(`web unit`), le magasin envoie la commande puis `undo` / `redo` et applique la vue renvoyée
(section 13 : plus de rechargement complet après chaque micro-opération) ; côté serveur
(`Application.Tests`), l'inverse rejoué rend l'état exact.

| N° | Test 1.x | Cas | Intention (ce que le test prouve, une phrase) | Cible 2.0 | Nom 2.0 proposé (anglais, PascalCase avec _ ) | Remarque |
|---|---|---|---|---|---|---|
| T-WMAG-01 | `charge la carte demandee` | 1 | Le magasin charge la carte demandée. | web unit | `MapStore_LoadsRequestedMap` | |
| T-WMAG-02 | `jauge de fiche mise a jour sans recharger la carte ni toucher a l historique` | 1 | La jauge de fiche se met à jour localement sans recharger la carte, sans recréer l'objet si rien ne change, et sans entrée d'historique. | web unit | `MapStore_SheetGaugeUpdatedLocally_NoReloadNoHistory` | |
| T-WMAG-03 | `erreur de chargement conservee pour affichage` | 1 | Une erreur de chargement est conservée pour affichage. | web unit | `MapStore_LoadError_Kept` | |
| T-WMAG-04 | `ajout d etape : libelle par defaut, edition immediate, annulation qui retablit les positions` | 1 | L'ajout d'étape envoie le libellé par défaut, passe en édition, et l'annulation supprime l'étape et remet les positions, le rétablissement restaure le lot. | web unit, Application.Tests | `AddStep_DefaultLabelInlineEdit_UndoRestoresPositions` | remplacé par undo serveur (l'inverse remet aussi les positions décalées par RG-C04) |
| T-WMAG-05 | `insertion : une seule entree d historique, annulation complete puis retablissement` | 1 | Une insertion est une seule entrée d'historique, annulée et rétablie complètement, la flèche recoupée passant dans un nouveau lot. | web unit, Application.Tests | `InsertStep_SingleHistoryEntry_UndoRedoComplete` | remplacé par undo serveur |
| T-WMAG-06 | `couper dans une carte et coller dans une autre : l historique de la carte d origine est vide` | 1 | Un couper-coller vers une autre carte vide l'historique de la carte d'origine et s'inscrit dans celui de la carte cible. | Application.Tests, web unit | `CutPasteToOtherMap_OperationLoggedOnTargetMap` | remplacé par undo serveur ; opération inscrite sur la carte d'arrivée, pile d'origine non vidée (AR-06) |
| T-WMAG-07 | `l historique d une carte survit a l editeur : on le retrouve en revenant sur la carte` | 1 | L'historique d'une carte survit à la sortie de l'éditeur. | Application.Tests, e2e | `OperationLog_SurvivesNavigationAndReload` | remplacé par undo serveur ; en 2.0 il survit aussi au rechargement (section 10, e2e « annulation après rechargement ») |
| T-WMAG-08 | `detailler une etape dont la carte detaillee est vide : reouverture, rien dans l historique` | 1 | Rouvrir une sous-carte vide existante n'entre pas dans l'historique. | Application.Tests | `DetailExistingEmptySubMap_NotLogged` | |
| T-WMAG-09 | `supprimer le detail : sous-carte supprimee, annulation par restauration du lot` | 1 | Supprimer le détail supprime la sous-carte et s'annule par restauration du lot ; une étape sans détail est refusée. | web unit, Application.Tests | `DeleteDetail_UndoRestoresSubMap` | remplacé par undo serveur |
| T-WMAG-10 | `RACI : deplacer le A puis annuler rend le A a l ancien approbateur et l ancien role au couloir` | 1 | Annuler le déplacement d'un A rend le A à l'ancien approbateur et l'ancien rôle au couloir ; un rôle identique n'envoie rien. | web unit, Application.Tests | `MoveApprover_UndoRestoresPreviousApproverAndRole` | remplacé par undo serveur |
| T-WMAG-11 | `le type debut donne son libelle par defaut` | 1 | Le type « début » choisi dans la palette donne son libellé par défaut. | web unit | `AddStep_StartType_DefaultLabel` | |
| T-WMAG-12 | `renommage : version envoyee, annulation avec l ancien libelle` | 1 | Le renommage envoie la version connue et l'annulation renvoie l'ancien libellé. | web unit, Application.Tests | `RenameStep_SendsVersion_UndoRestoresLabel` | remplacé par undo serveur |
| T-WMAG-13 | `renommage identique : aucun appel, rien dans l historique` | 1 | Un renommage identique n'envoie rien et n'entre pas dans l'historique. | web unit | `RenameStep_Unchanged_NoCall` | |
| T-WMAG-14 | `deplacement d un bloc sur des cases libres : un seul appel, a l identique, annulable` | 1 | Un bloc déplacé sur des cases libres part en un seul appel trié, les étapes inconnues ignorées, et l'annulation remet les positions. | web unit, Application.Tests | `MoveBlockToFreeCells_SingleLayoutCall_Undoable` | remplacé par undo serveur |
| T-WMAG-15 | `deplacement vers une case prise : etape par etape, de gauche a droite, avec le decalage RG-C04` | 1 | Un déplacement vers une case prise passe étape par étape pour que le serveur applique RG-C04. | web unit, Application.Tests | `MoveToOccupiedCell_SingleGroupedMove_OneOperation_RgC04` | intention changée (AR-44) : le glisser de plusieurs étapes vers une case prise part en une seule commande serveur de déplacement groupé, inscrite comme une seule opération du journal ; le serveur applique RG-C04 à chaque étape dont la case est occupée |
| T-WMAG-16 | `suppression puis annulation par restauration du lot` | 1 | Une suppression recousue s'annule par restauration du lot et se rétablit. | web unit, Application.Tests | `DeleteSelection_UndoRedo` | remplacé par undo serveur |
| T-WMAG-17 | `erreur serveur : message affiche, rien dans l historique, rechargement sur conflit` | 1 | Une erreur serveur affiche le message, n'entre pas dans l'historique, et un 409 recharge la carte. | web unit | `ServerError_MessageShown_ReloadOnConflict` | |
| T-WMAG-18 | `erreur reseau : message traduit` | 1 | Une erreur réseau affiche un message traduit. | web unit | `NetworkError_TranslatedMessage` | le message cite le nom du produit : Topogenia |
| T-WMAG-19 | `commande : erreurs par ligne rendues, succes annulable` | 1 | Une commande en échec rend ses erreurs par ligne sans historique, une commande réussie s'annule. | web unit, Application.Tests | `Command_LineErrorsReturned_SuccessUndoable` | remplacé par undo serveur |
| T-WMAG-20 | `detailler : annulation par suppression de la sous-carte, retablissement par restauration` | 1 | « Détailler » s'annule par suppression conditionnelle de la sous-carte et se rétablit par restauration. | web unit, Application.Tests | `DetailStep_UndoDeletesEmptySubMap_RedoRestores` | remplacé par undo serveur |
| T-WMAG-21 | `supprimer une macro en rattachant : l annulation la rattache de nouveau` | 1 | Annuler la suppression d'une macro rattachée restaure le lot puis rattache de nouveau la sous-carte. | web unit, Application.Tests | `DeleteMacroWithAttach_UndoReattachesSubMap` | remplacé par undo serveur |
| T-WMAG-22 | `suppression recousue : l annulation retire d abord la fleche de raccord` | 1 | L'annulation d'une suppression recousue retire d'abord la flèche de raccord. | Application.Tests | `DeleteWithStitch_UndoRemovesStitchFirst` | remplacé par undo serveur |
| T-WMAG-23 | `copier puis coller apres une etape : copie inseree dans le flux, annulable et retablie a l identique` | 1 | Coller une copie après une étape l'insère dans le flux, garde le presse-papiers, s'annule et se rétablit à l'identique. | web unit, Application.Tests | `CopyPasteAfterStep_InsertedInFlow_UndoRedoExact` | remplacé par undo serveur |
| T-WMAG-24 | `couper puis coller : deplacement du bloc, presse-papiers vide ; l annulation ramene le bloc` | 1 | Couper-coller déplace le bloc, vide le presse-papiers, et l'annulation ramène le bloc. | web unit, Application.Tests | `CutPaste_MovesBlockClearsClipboard_UndoReturnsBlock` | remplacé par undo serveur |
| T-WMAG-25 | `un bloc qui deborde des couloirs n est pas colle` | 1 | Un bloc qui déborde des couloirs n'est pas collé et un message d'erreur s'affiche. | web unit | `Paste_BlockOverflowingLanes_Refused` | |
| T-WMAG-26 | `dupliquer colle la copie juste a droite du bloc, sans toucher au presse-papiers` | 1 | Dupliquer colle la copie juste à droite du bloc sans toucher au presse-papiers. | web unit | `Duplicate_PastesRightOfBlock_ClipboardUntouched` | |
| T-WMAG-27 | `detailler la selection : renommage propose ; le retablissement garde la meme etape` | 1 | « Détailler la sélection » propose le renommage, s'annule, et le rétablissement garde la même étape au lieu d'en recréer une. | web unit, Application.Tests | `DetailSelection_RenamePrompted_RedoKeepsSameStep` | remplacé par undo serveur |
| T-WMAG-28 | `detailler la selection : l annulation est refusee si la carte detaillee a ete completee` | 1 | L'annulation de « Détailler la sélection » est refusée si la carte détaillée a été complétée. | Application.Tests, web unit | `UndoDetailSelection_RefusedWhenSubMapCompleted` | remplacé par undo serveur (refus `OPERATION_DEPASSEE`, ou `CARTE_MODIFIEE` si l'inverse violerait un invariant, AR-43 ; message côté client selon le code, pile vidée, AR-05) |
| T-WMAG-29 | `couloirs : ajout en edition immediate, ordre annulable, suppression restauree` | 1 | Ajouter un couloir passe en édition, l'ordre des couloirs s'annule, un ordre identique n'envoie rien et la suppression se restaure. | web unit, Application.Tests | `Lanes_AddInlineEdit_ReorderAndDeleteUndoable` | remplacé par undo serveur ; ajout et ordre des couloirs annulables (4.6) |
| T-WMAG-30 | `historique propre a chaque carte` | 1 | L'historique est propre à chaque carte. | Application.Tests | `OperationLog_PerMap` | remplacé par undo serveur |
| T-WMAG-31 | `afficher les codes : choix memorise` | 1 | Le choix d'afficher les codes est mémorisé dans le navigateur. | web unit | `ShowCodes_PreferencePersisted` | |
| T-WMAG-32 | `Ctrl+Z presse pendant un deplacement annule bien le deplacement (file des operations)` | 1 | Un Ctrl+Z pressé pendant un déplacement en cours annule bien ce déplacement grâce à la file des opérations. | web unit | `UndoDuringPendingMove_UndoesThatMove` | la file d'envoi reste côté client ; l'undo serveur porte sur la dernière opération enregistrée |
| T-WMAG-33 | `rien a annuler : message` | 1 | Annuler sans historique affiche « Rien à annuler ». | web unit | `Undo_EmptyStack_ShowsMessage` | |

### carte/pressePapiers.test.ts

| N° | Test 1.x | Cas | Intention (ce que le test prouve, une phrase) | Cible 2.0 | Nom 2.0 proposé (anglais, PascalCase avec _ ) | Remarque |
|---|---|---|---|---|---|---|
| T-WPRE-01 | `extraire : positions relatives a la premiere etape, fleches internes seulement` | 1 | Un bloc extrait garde des positions relatives à sa première étape et ses seules flèches internes ; une coupe garde les identifiants. | web unit | `ExtractBlock_RelativePositions_InternalFlowsOnly` | |
| T-WPRE-02 | `placer : la premiere etape arrive dans la case, le reste suit ; couloirs manquants refuses` | 1 | Placer un bloc met sa première étape dans la case visée, le reste suit, et des couloirs manquants le refusent. | web unit | `PlaceBlock_FirstStepInTargetCell_MissingLanesRefused` | |
| T-WPRE-03 | `placer : un bloc qui remonte de couloir, colle dans le couloir du haut, descend au lieu d etre refuse` | 1 | Un bloc qui remonte de couloir, collé en haut, descend au lieu d'être refusé. | web unit | `PlaceBlock_RisingBlockAtTop_ShiftedDown` | |
| T-WPRE-04 | `placer : les rangees remontent pour rester positives` | 1 | Les rangées d'un bloc placé remontent pour rester positives. | web unit | `PlaceBlock_RowsKeptNonNegative` | |
| T-WPRE-05 | `cases d insertion et a droite du bloc` | 1 | Les cases d'insertion avant, après et à droite d'un bloc sont calculées. | web unit | `InsertionAndRightOfBlockCells` | |

### carte/raci.test.ts

| N° | Test 1.x | Cas | Intention (ce que le test prouve, une phrase) | Cible 2.0 | Nom 2.0 proposé (anglais, PascalCase avec _ ) | Remarque |
|---|---|---|---|---|---|---|
| T-WRAC-01 | `le R est le couloir de l etape et l emporte sur tout autre role` | 1 | Le R est le couloir de l'étape et masque tout autre rôle sur ce couloir. | web unit | `StepRoles_RIsOwnLane_OverridesOtherRole` | |
| T-WRAC-02 | `approbateur manquant, sauf pour les debuts et fins` | 1 | Une étape sans A est signalée, sauf début et fin. | web unit | `MissingApprover_FlaggedExceptStartEnd` | étendre aux losanges ET et OU inclusif (types sans contenu) |
| T-WRAC-03 | `pastilles dans l ordre A, S, C, I, sans le R` | 1 | Les pastilles RACI suivent l'ordre A, S, C, I sans le R. | web unit | `RaciBadges_OrderedASCI_WithoutR` | |
| T-WRAC-04 | `matrice : etapes en lignes (sans debut ni fin), couloirs en colonnes` | 1 | La matrice RACI a les étapes en lignes (sans début ni fin) et les couloirs en colonnes. | web unit | `RaciMatrix_StepsRowsLanesColumns` | |
| T-WRAC-05 | `copie : texte tabule propre pour Excel, HTML echappe pour Word et PowerPoint` | 1 | La copie de la matrice produit un texte tabulé propre et un HTML échappé. | web unit | `RaciMatrixCopy_CleanTsvAndEscapedHtml` | |

### commun/commun.test.tsx

| N° | Test 1.x | Cas | Intention (ce que le test prouve, une phrase) | Cible 2.0 | Nom 2.0 proposé (anglais, PascalCase avec _ ) | Remarque |
|---|---|---|---|---|---|---|
| T-WCOM-01 | `lecture : sans en-tete X-Ariane, avec la langue` | 1 | Une lecture part sans en-tête anti-CSRF, avec la langue et les cookies de même origine. | web unit | `ApiClient_Read_NoAntiCsrfHeader_SendsLanguage` | client généré depuis OpenAPI (phase 6) ; en-tête `X-Requested-With: XMLHttpRequest` (AR-13) |
| T-WCOM-02 | `ecriture : X-Ariane, JSON et version If-Match` | 1 | Une écriture envoie l'en-tête anti-CSRF, le JSON et la version en `If-Match`. | web unit | `ApiClient_Write_AntiCsrfJsonAndIfMatch` | format du jeton de version 2.0 |
| T-WCOM-03 | `erreur normalisee de l API` | 1 | Une erreur de l'API est normalisée (statut, code, message, détails). | web unit | `ApiClient_NormalisedError` | |
| T-WCOM-04 | `erreur sans corps et reseau coupe` | 1 | Une erreur sans corps donne `HTTP_<statut>` et une coupure réseau `RESEAU`. | web unit | `ApiClient_EmptyErrorBodyAndNetworkFailure` | |
| T-WCOM-05 | `identifiants encodes dans les adresses et options de suppression` | 1 | Les identifiants sont encodés dans l'URL et les options de suppression passées en paramètres. | web unit | `ApiClient_EncodesIdsAndDeleteOptions` | routes 2.0 en anglais (3.6) : adresses à mettre à jour |
| T-WCOM-06 | `memes cles en francais et en anglais, aucune vide` | 1 | Les dictionnaires français et anglais ont les mêmes clés, aucune vide. | web unit | `I18n_SameKeysInAllLanguages_NoneEmpty` | à étendre à chaque langue configurée |
| T-WCOM-07 | `vocabulaire a eviter absent (annexe C.5)` | 1 | Le vocabulaire à éviter est absent des textes français. | web unit | `I18n_ForbiddenVocabularyAbsent` | |
| T-WCOM-08 | `style impersonnel en francais (annexe C, 17/10/2026) : jamais de tutoiement` | 1 | Aucun texte français ne tutoie. | web unit | `I18n_French_NeverInformalYou` | |
| T-WCOM-09 | `textes de l annexe C repris a l identique` | 1 | Les textes de l'annexe C sont repris à l'identique. | web unit | `I18n_ReferenceTextsVerbatim` | |
| T-WCOM-10 | `langue du navigateur sinon anglais` | 1 | La langue du navigateur est retenue si elle est prise en charge, sinon l'anglais. | web unit | `BrowserLanguage_SupportedElseEnglish` | liste des langues en configuration, plus de cast de langue en dur (section 13) |
| T-WCOM-11 | `nom traduit si disponible, sinon original` | 1 | Un nom est affiché traduit si la traduction existe, sinon dans l'original. | web unit | `TranslatedName_FallbackToOriginal` | |
| T-WCOM-12 | `pluriels` | 1 | Les pluriels sont gérés. | web unit | `I18n_Plurals` | |
| T-WCOM-13 | `reconnait les ecrans` | 1 | Le routeur reconnaît les écrans et rejette un identifiant invalide. | web unit | `Router_RecognisesScreens` | |
| T-WCOM-14 | `naviguer change l adresse` | 1 | Naviguer change l'adresse du navigateur. | web unit | `Router_NavigateChangesLocation` | |
| T-WCOM-15 | `profil sans langue : langue du navigateur (anglais sous jsdom)` | 1 | Sans langue au profil, l'en-tête suit le navigateur, et le bouton « Pour moi » n'apparaît pas. | web unit | `Header_NoProfileLanguage_UsesBrowser_NoForMeButton` | bouton « Pour moi » mort retiré (section 13) : l'assertion d'absence reste |
| T-WCOM-16 | `affiche le nom, bascule la langue et la memorise dans le profil` | 1 | L'en-tête affiche le nom et le changement de langue est mémorisé dans le profil. | web unit | `Header_ShowsName_LanguageSwitchPersisted` | |
| T-WCOM-17 | `projets, participants avec sponsors, bouton principal vers la carte` | 1 | L'accueil liste les projets et les participants avec sponsors, et le bouton principal ouvre la carte du projet. | web unit | `Home_ProjectsParticipants_MainButtonOpensLastOpenedProject` | défaut 1.x corrigé : bouton principal et « Qui participe » portent sur le dernier projet ouvert, mémorisé dans le profil serveur ; avant toute ouverture, sur le premier projet par code (sections 10 et 13, AR-49) ; ajouter un cas à plusieurs projets, avant et après une ouverture |
| T-WCOM-18 | `aucun projet : message explicatif, bouton principal inactif` | 1 | Sans projet, un message explicatif s'affiche et le bouton principal est inactif. | web unit | `Home_NoProjects_MessageAndDisabledButton` | |
| T-WCOM-19 | `initiales` | 1 | Les initiales d'un nom sont calculées, avec repli « ? ». | web unit | `Initials_FromDisplayName` | |
| T-WCOM-20 | `Entree execute ; les erreurs par ligne restent affichees` | 1 | Dans la barre de commande, Entrée exécute, Maj+Entrée ajoute une ligne et les erreurs par ligne restent affichées. | web unit | `CommandBar_EnterExecutes_LineErrorsShown` | |
| T-WCOM-21 | `succes : la barre se ferme ; Echap ferme aussi` | 1 | La barre de commande se ferme au succès et sur Échap. | web unit | `CommandBar_ClosesOnSuccessAndEscape` | |

### fiche/PanneauFiche.test.tsx

| N° | Test 1.x | Cas | Intention (ce que le test prouve, une phrase) | Cible 2.0 | Nom 2.0 proposé (anglais, PascalCase avec _ ) | Remarque |
|---|---|---|---|---|---|---|
| T-WPFI-01 | `textes nettoyes, champs vides et operations sans texte retires, texte libre vide = null` | 1 | Le contenu envoyé est nettoyé : champs vides et opérations sans texte retirés, texte libre vide à null. | web unit | `SheetPayload_TrimmedEmptyRemoved_FreeTextNull` | |
| T-WPFI-02 | `document : attente seulement pour une entree exterieure` | 1 | L'attente n'est envoyée que pour une entrée extérieure, et la saisie d'outils se découpe sur virgules, points-virgules et lignes. | web unit | `DocumentRequest_WaitOnlyForExternalInput_ToolsSplit` | |
| T-WPFI-03 | `un seul enregistrement apres plusieurs frappes, avec la version connue, et la jauge suit` | 1 | Plusieurs frappes donnent un seul enregistrement différé, avec la version connue, et la jauge suit. | web unit | `AutoSave_DebouncedSingleSave_WithVersion_GaugeUpdates` | |
| T-WPFI-04 | `ordre de la fiche : acteur, finalite, entrees, deroule, sorties, outils, frequence, duree` | 1 | Les sections de la fiche suivent l'ordre acteur, finalité, entrées, déroulé, sorties, outils, fréquence, durée. | web unit | `SheetSections_FixedOrder` | |
| T-WPFI-05 | `deroule : Entree cree la ligne suivante, nature et outil facultatifs, ordre modifiable` | 1 | Dans le déroulé, Entrée crée la ligne suivante, nature et outil sont facultatifs et l'ordre se modifie. | web unit | `Procedure_EnterAddsLine_OptionalNatureTool_Reorderable` | |
| T-WPFI-06 | `frequence et duree, reprises en un clic : frequence de l etape precedente, outils deja cites` | 1 | Fréquence du prédécesseur et outils déjà cités se reprennent en un clic. | web unit | `Suggestions_OneClickReuseFrequencyAndTools` | |
| T-WPFI-07 | `ecriture libre : le texte est conserve et les reponses guidees aussi` | 1 | En écriture libre, le texte et les réponses guidées sont conservés ensemble. | web unit | `FreeText_KeptAlongsideGuidedAnswers` | |
| T-WPFI-08 | `fermer le panneau envoie tout de suite ce qui reste a enregistrer` | 1 | Fermer le panneau envoie tout de suite ce qui reste à enregistrer. | web unit | `ClosePanel_FlushesPendingSave` | |
| T-WPFI-09 | `Echap ou la croix ferment le panneau` | 1 | Échap ou la croix ferment le panneau. | web unit | `ClosePanel_EscapeOrCloseButton` | |
| T-WPFI-10 | `les sorties des etapes precedentes sont proposees, non cochees ; cocher = utiliser, puis attente` | 1 | Les sorties des prédécesseurs sont proposées non cochées, cocher les utilise puis l'attente s'envoie. | web unit | `Inputs_PredecessorOutputsOffered_CheckToUse_ThenWait` | |
| T-WPFI-11 | `qualite : %C&A puis, sous 100 %, l irritant qui l explique ; envoye en quittant le champ` | 1 | Le %C&A s'envoie en quittant le champ, et sous 100 % l'irritant et le temps perdu apparaissent. | web unit | `InputQuality_PctThenPainPointBelow100_SentOnBlur` | |
| T-WPFI-12 | `cote producteur : la sortie montre le %C&A et l irritant signales par l etape qui l utilise` | 1 | Côté producteur, la sortie montre le %C&A et l'irritant signalés par l'étape qui l'utilise. | web unit | `Outputs_ShowConsumerQualityAndPainPoint` | |
| T-WPFI-13 | `decocher : l etape n utilise plus le document` | 1 | Décocher retire l'usage du document. | web unit | `Inputs_UncheckRemovesUsage` | |
| T-WPFI-14 | `Autre : tableau des etapes et de leurs sorties, cocher les ajoute ; ils s affichent comme venant d ailleurs` | 1 | « Autre » ouvre le tableau des étapes et de leurs sorties, cocher les ajoute et ils s'affichent comme venant d'ailleurs. | web unit | `OtherInputs_DialogCheckAdds_ShownAsRemote` | |
| T-WPFI-15 | `entree exterieure : formulaire avec attente, sans destination` | 1 | Une entrée extérieure se déclare avec un formulaire qui porte l'attente, sans destination. | web unit | `ExternalInput_FormWithWait_NoDestination` | |
| T-WPFI-16 | `sortie : sans destination, on voit qui l utilise ; modifier puis supprimer, une erreur garde la saisie` | 1 | Une sortie montre qui l'utilise, se modifie et se supprime, son lien s'ouvre dans un nouvel onglet et une erreur garde la saisie. | web unit | `Output_ShowsConsumers_EditDelete_ErrorKeepsInput` | 5.4 : vérifier aussi `rel="noopener noreferrer"` sur le lien ; le message simulé « adresse https:// » ne reflète pas la règle serveur (http ou chemin) |
| T-WPFI-17 | `enregistre d abord la saisie en cours puis valide avec la nouvelle version` | 1 | Valider enregistre d'abord la saisie en cours puis valide avec la nouvelle version. | web unit | `Validate_SavesPendingThenValidatesWithNewVersion` | |
| T-WPFI-18 | `brouillon sur une fiche deja validee : l equipe voit encore la version validee` | 1 | Sur une fiche déjà validée en brouillon, le panneau rappelle que l'équipe voit la version validée. | web unit | `DraftOverValidated_NoticeTeamSeesValidated` | classes CSS d'état alignées sur `validated` / `needs_review` (section 13, 9.4) |
| T-WPFI-19 | `modification concurrente : message du serveur, la saisie reste et repart avec la nouvelle version` | 1 | Sur conflit, le message du serveur s'affiche, la saisie reste et repart avec la nouvelle version. | web unit | `ConcurrentEdit_KeepsInput_RetriesWithNewVersion` | |
| T-WPFI-20 | `enregistrement en echec : la validation n est pas envoyee` | 1 | Si l'enregistrement échoue, la validation n'est pas envoyée et la saisie reste. | web unit | `SaveFailed_ValidationNotSent` | |
| T-WPFI-21 | `sans droit de remplir : la version validee, deroule numerote, documents, sans champ de saisie` | 1 | Sans droit de remplir, le panneau montre la version validée, le déroulé numéroté et les documents, sans aucune saisie. | web unit | `ReadOnlySheet_ValidatedVersionNoInputs` | |
| T-WPFI-22 | `rien de valide : un message, mais les documents des fleches restent visibles` | 1 | Sans version validée, un message s'affiche mais les documents des flèches restent visibles. | web unit | `ReadOnlySheet_NothingValidated_DocumentsStillVisible` | |
| T-WPFI-23 | `demande une precision sur une fiche validee` | 1 | L'animateur demande une précision sur une fiche validée et l'état passe à « à revoir ». | web unit | `Facilitator_RequestsReview_StateNeedsReview` | |
| T-WPFI-24 | `choisit le responsable parmi les membres qui peuvent remplir` | 1 | Le responsable se choisit parmi les seuls membres qui peuvent remplir. | web unit | `Facilitator_AssignsResponsibleAmongFillers` | |

### format/pmap.test.ts

| N° | Test 1.x | Cas | Intention (ce que le test prouve, une phrase) | Cible 2.0 | Nom 2.0 proposé (anglais, PascalCase avec _ ) | Remarque |
|---|---|---|---|---|---|---|
| T-WPMA-01 | `les exemples respectent le schema` | 1 | Les exemples du dossier `format/` respectent le schéma JSON. | Interchange.Tests, web unit | `PmapExamples_MatchSchema` | schéma 1.1 (7.5) ; le contrôle peut vivre côté .NET |
| T-WPMA-02 | `l exemple de FORMAT_PMAP.md est exemples/minimal.pmap` | 1 | L'exemple de `FORMAT_PMAP.md` est identique à `exemples/minimal.pmap`. | Interchange.Tests, web unit | `PmapDoc_ExampleEqualsMinimalFile` | |
| T-WPMA-03 | `la consigne pour l IA embarque le schema exact et un exemple valide` | 1 | La consigne pour l'IA embarque une copie exacte du schéma et un exemple valide. | Interchange.Tests, web unit | `AiPrompt_EmbedsExactSchemaAndValidExample` | consignes mises à jour (7.5) |
| T-WPMA-04 | `le guide pour IA a un seul exemple, valide` | 1 | Le guide pour IA contient un seul exemple, valide. | Interchange.Tests, web unit | `AiGuide_SingleValidExample` | |
| T-WPMA-05 | `le schema refuse les fautes courantes` | 1 | Le schéma refuse onze fautes courantes (propriété inconnue, nom manquant, type, colonne, couleur, HTML, libellé vide, longueur, couloirs, RACI, code). | Interchange.Tests, web unit | `PmapSchema_RejectsCommonMistakes` | ajouter le RACI sur début, fin et losanges (7.5) |
| T-WPMA-06 | `format et version obligatoires, version 1.x seulement` | 1 | Format et version sont obligatoires et seules les versions 1.x passent le schéma. | Interchange.Tests, web unit | `PmapSchema_FormatAndVersion1xRequired` | |

### projet/Fichiers.test.tsx

| N° | Test 1.x | Cas | Intention (ce que le test prouve, une phrase) | Cible 2.0 | Nom 2.0 proposé (anglais, PascalCase avec _ ) | Remarque |
|---|---|---|---|---|---|---|
| T-WFIC-01 | `retire le bloc de code Markdown et la marque d ordre des octets` | 1 | Le contenu collé est débarrassé du bloc de code Markdown et du BOM. | web unit | `PastedContent_StripsCodeFenceAndBom` | |
| T-WFIC-02 | `code et nom du projet, vides si absents ou illisibles` | 1 | Code et nom sont lus dans le fichier, vides s'ils sont absents, invalides ou illisibles. | web unit | `FileHeader_CodeAndName_EmptyWhenMissingOrInvalid` | |
| T-WFIC-03 | `contenu colle : code et nom pre-remplis, envoi au serveur puis ouverture de la carte` | 1 | Un contenu collé pré-remplit code et nom, est envoyé au serveur puis la carte s'ouvre. | web unit | `OpenPasted_PrefillsCodeAndName_ThenOpensMap` | |
| T-WFIC-04 | `code saisi conserve quand le contenu change ; code invalide bloque l envoi` | 1 | Un code saisi est conservé quand le contenu change et un code invalide bloque l'envoi. | web unit | `OpenPasted_TypedCodeKept_InvalidCodeBlocks` | |
| T-WFIC-05 | `fichier choisi sur le disque` | 1 | Un fichier choisi sur le disque est lu et pré-remplit le code. | web unit | `OpenFromDisk_ReadsFileAndPrefills` | |
| T-WFIC-06 | `fichier refuse : message et liste des problemes avec leur emplacement` | 1 | Un fichier refusé affiche le message et la liste des problèmes avec leur emplacement, sans quitter la page. | web unit | `OpenRejected_ShowsLocatedProblems` | |
| T-WFIC-07 | `points a verifier affiches avant d ouvrir la carte` | 1 | Les compteurs et points à vérifier s'affichent avant d'ouvrir la carte. | web unit | `OpenSucceeded_ShowsUncertaintiesBeforeMap` | |
| T-WFIC-08 | `dupliquer : nom propose, code obligatoire, ouverture de la copie` | 1 | Dupliquer propose un nom, exige un code et ouvre la copie. | web unit | `Duplicate_ProposesNameRequiresCode_OpensCopy` | |
| T-WFIC-09 | `copie telechargee sous le nom CODE_nom_date.pmap, contenu du serveur intact` | 1 | La copie est téléchargée sous `CODE_nom_date.pmap` avec le contenu du serveur intact. | web unit | `DownloadCopy_FileNameAndContentIntact` | |

### traduction/classeur.test.ts

| N° | Test 1.x | Cas | Intention (ce que le test prouve, une phrase) | Cible 2.0 | Nom 2.0 proposé (anglais, PascalCase avec _ ) | Remarque |
|---|---|---|---|---|---|---|
| T-WCLA-01 | `une ligne par texte, original dans sa langue, autre colonne vide, cle cachee` | 1 | Le classeur a une ligne par texte, l'original dans sa langue, l'autre colonne vide, la clé cachée et une feuille de consigne. | web unit | `TranslationWorkbook_OneRowPerText_HiddenKey_Instructions` | une colonne par langue configurée |
| T-WCLA-02 | `relu apres traduction et tri des lignes : chaque traduction revient a son objet` | 1 | Relu après traduction et tri des lignes (texte enrichi compris), chaque traduction revient à son objet. | web unit | `TranslationWorkbook_ReadBackAfterSort_MatchesObjects` | |
| T-WCLA-03 | `fichier d un autre projet, ou sans la feuille Ariane, ou illisible : refuse` | 1 | Un classeur d'un autre projet, sans la feuille de l'application ou illisible est refusé. | web unit | `TranslationWorkbook_OtherProjectMissingSheetOrUnreadable_Rejected` | nom de la feuille de contrôle tiré de la clé i18n du nom du produit ; `FICHIER_SANS_ARIANE` devient `FICHIER_SANS_FEUILLE_PROJET` ; la 2.0 ne relit pas les classeurs 1.x, qui sont refusés avec ce code (AR-28) |

## Partie 4 - Parcours Playwright (`web\e2e\*.spec.ts`)

Les parcours se jouent sur la base SQLite (mode Poste) et, en mode Serveur, sur SQL Server
(section 11). `preparer.mjs` et `purger.sql` (préparation et purge de la base de recette 1.x,
en T-SQL) ne contiennent pas de test : ils sont remplacés par une préparation portable des deux
bases. Tous les parcours 1.x purgent la base par `sqlcmd` et préparent leurs données par
`fetch` avec l'en-tête `X-Ariane` : ces deux points sont à adapter (préparation portable,
routes et en-tête 2.0, jeton de lancement en mode Poste).

### alignement.spec.ts

| N° | Test 1.x | Cas | Intention (ce que le test prouve, une phrase) | Cible 2.0 | Nom 2.0 proposé (anglais, PascalCase avec _ ) | Remarque |
|---|---|---|---|---|---|---|
| T-E2E-ALI-01 | `la baguette aligne la carte en une action annulable ; le losange a une pointe par sortie` | 1 | La baguette aligne la carte (flèches droites, message), ne fait rien une seconde fois, s'annule d'un seul Ctrl+Z, et chaque sortie du losange a sa pointe. | e2e | `AlignWand_AlignsInOneUndoableAction_GatewayOneTipPerExit` | Ctrl+Z servi par undo serveur |

### branches.spec.ts

| N° | Test 1.x | Cas | Intention (ce que le test prouve, une phrase) | Cible 2.0 | Nom 2.0 proposé (anglais, PascalCase avec _ ) | Remarque |
|---|---|---|---|---|---|---|
| T-E2E-BRA-01 | `ruban en zones nommees ; branche parallele sous l etape choisie, couloir agrandi, memes fleches` | 1 | Le ruban a ses zones nommées ; « Branche » pose une étape parallèle sous l'étape choisie, agrandit le couloir, copie les flèches et s'annule. | e2e | `Ribbon_NamedGroups_ParallelBranchUnderSelected_Undoable` | ruban 2.0 (9.5) : icônes Lucide au lieu des caractères ×, +, ○ |
| T-E2E-BRA-02 | `losanges : ET et OU inclusif poses depuis la palette, avec leur marqueur, sans fiche` | 1 | Les losanges ET et OU inclusif s'affichent avec leur marqueur et n'ouvrent pas de fiche. | e2e | `Gateways_ParallelAndInclusive_MarkersNoSheet` | marqueurs de la charte (9.4) |
| T-E2E-BRA-03 | `qualite : %C&A et irritant sur le document recu, visibles par l etape qui le produit` | 1 | Le %C&A et l'irritant saisis sur un document reçu se voient dans la fiche de l'étape qui le produit. | e2e | `UsageQuality_PctAndPainPoint_VisibleToProducer` | |
| T-E2E-BRA-04 | `outil Selection : un rectangle trace sur le fond choisit un bloc, qu un seul glisser deplace ; annulable` | 1 | L'outil Sélection trace un rectangle qui choisit un bloc, un seul glisser le déplace en une requête, Ctrl+Z l'annule et H revient à la main. | e2e | `SelectTool_MarqueeSelectsBlock_SingleDragMove_Undoable` | |

### corbeille.spec.ts

| N° | Test 1.x | Cas | Intention (ce que le test prouve, une phrase) | Cible 2.0 | Nom 2.0 proposé (anglais, PascalCase avec _ ) | Remarque |
|---|---|---|---|---|---|---|
| T-E2E-COR-01 | `supprimer un projet : confirmation par le code, corbeille, restauration, suppression definitive` | 1 | Un projet se met à la corbeille avec son code, disparaît de l'accueil, se restaure, puis se supprime définitivement et libère son code. | e2e | `TrashProject_ConfirmByCode_RestoreThenPurge` | la saisie « vieux » active le bouton côté interface alors que le serveur exige la casse exacte (annexe A, 2.15) : à vérifier |

### export.spec.ts

| N° | Test 1.x | Cas | Intention (ce que le test prouve, une phrase) | Cible 2.0 | Nom 2.0 proposé (anglais, PascalCase avec _ ) | Remarque |
|---|---|---|---|---|---|---|
| T-E2E-EXP-01 | `export PNG : carte entiere, y compris les etapes hors ecran, en haute resolution` | 1 | L'export PNG contient toute la carte, étapes hors écran comprises, en haute résolution et aussi nettes que les autres. | e2e | `ExportPng_WholeMapIncludingOffscreen_HighResolution` | à jouer sous la CSP stricte (5.4, section 11) ; dimensions à recaler sur la charte 2.0 |
| T-E2E-EXP-02 | `export PDF : A3 paysage, titre du projet` | 1 | L'export PDF est en A3 paysage avec le titre du projet. | e2e | `ExportPdf_A3Landscape_WithTitle` | sous CSP stricte |
| T-E2E-EXP-03 | `export Excel : trois onglets, etapes avec acteur et RACI, matrice` | 1 | L'export Excel a trois onglets avec étapes, acteur, RACI et matrice. | e2e | `ExportExcel_ThreeSheets_StepsRaciMatrix` | sous CSP stricte |

### fiches.spec.ts

| N° | Test 1.x | Cas | Intention (ce que le test prouve, une phrase) | Cible 2.0 | Nom 2.0 proposé (anglais, PascalCase avec _ ) | Remarque |
|---|---|---|---|---|---|---|
| T-E2E-FIC-01 | `remplir une fiche : deroule, enregistrement automatique, jauge sur l etape, validation, relecture apres rechargement` | 1 | Le début n'ouvre pas de fiche ; une fiche se remplit (déroulé, fréquence, durée) avec enregistrement automatique et jauge, se valide, se relit après rechargement, et l'étape suivante reprend la fréquence. | e2e | `FillSheet_AutoSaveGaugeValidateReload_NextStepReusesFrequency` | jauge aux couleurs d'état de la charte (9.4) |
| T-E2E-FIC-02 | `les sorties de l etape precedente sont proposees a cocher ; cocher = utiliser, la fleche porte le document` | 1 | Une sortie déclarée est proposée non cochée à l'étape suivante ; la cocher l'utilise, la flèche porte le document, une entrée extérieure se déclare et décocher retire le document de la flèche. | e2e | `PredecessorOutputs_CheckToUse_FlowCarriesDocument` | lien externe : vérifier `rel="noopener noreferrer"` (5.4) |
| T-E2E-FIC-03 | `ecriture libre conservee, fermeture pendant la saisie sans perte` | 1 | Le texte libre est conservé à la fermeture immédiate et la fiche se rouvre en écriture libre. | e2e | `FreeText_KeptOnImmediateClose_ReopensInFreeMode` | |
| T-E2E-FIC-04 | `animateur : responsable de la fiche, demande de precision, reprise par l auteur` | 1 | L'animateur choisit le responsable, valide, demande une précision, et la reprise repasse en brouillon jusqu'à la nouvelle validation. | e2e | `Facilitator_AssignResponsible_RequestReview_AuthorResumes` | |
| T-E2E-FIC-05 | `une fleche qui saute des etapes les contourne ; une etape reliee a rien est signalee ; Autre donne ses documents` | 1 | Une flèche qui saute une étape la contourne sans la traverser, l'étape isolée est signalée et « Autre » donne ses documents, affichés comme venant d'ailleurs. | e2e | `SkippingFlow_Bypasses_IsolatedStepFlagged_OtherGivesDocuments` | |

### fichiers.spec.ts

| N° | Test 1.x | Cas | Intention (ce que le test prouve, une phrase) | Cible 2.0 | Nom 2.0 proposé (anglais, PascalCase avec _ ) | Remarque |
|---|---|---|---|---|---|---|
| T-E2E-FIR-01 | `ouvrir l exemple de la documentation depuis l accueil : points a verifier, carte et detail` | 1 | L'exemple ouvert depuis l'accueil montre le bilan et les points à vérifier, puis la carte, les couloirs, la macro-étape, la décision et le détail. | e2e | `OpenPmapFromHome_SummaryThenMapAndDetail` | |
| T-E2E-FIR-02 | `reponse d une IA collee : erreur signalee a sa place, corrigee, carte placee automatiquement` | 1 | Une réponse d'IA collée avec une erreur la signale à sa place, une fois corrigée la carte s'ouvre placée automatiquement. | e2e | `PasteAiAnswer_ErrorLocated_FixedThenAutoPlaced` | |
| T-E2E-FIR-03 | `copie telechargee conforme au schema, rouverte a l identique, puis projet duplique` | 1 | Le menu Fichier propose les six actions, la copie téléchargée est conforme au schéma, se rouvre à l'identique sous un autre code et le projet se duplique. | e2e | `DownloadCopy_SchemaValid_ReopenedIdentical_ThenDuplicated` | `generated_by` attendu « Topogenia » et schéma 1.1 ; menu Fichier 2.0 complété par l'import / export `.topogenia` (section 11) |
| T-E2E-FIR-04 | `Ctrl+S : rien a enregistrer, tout l est deja ; aucune fenetre du navigateur` | 1 | Ctrl+S explique que tout est déjà enregistré et n'ouvre jamais la fenêtre du navigateur, même pendant une saisie. | e2e | `CtrlS_ExplainsAutoSave_NoBrowserDialog` | |

### liens-et-insertion.spec.ts

| N° | Test 1.x | Cas | Intention (ce que le test prouve, une phrase) | Cible 2.0 | Nom 2.0 proposé (anglais, PascalCase avec _ ) | Remarque |
|---|---|---|---|---|---|---|
| T-E2E-LIE-01 | `1. le trait tire de A vers B cree la fleche A -> B, meme lache au milieu de B` | 1 | Un trait tiré de A et lâché au milieu de B crée la flèche A vers B, y compris entre couloirs. | e2e | `DragHandleToStepCentre_CreatesFlow` | |
| T-E2E-LIE-02 | `2. une fleche entre couloirs ne passe pas derriere une autre etape` | 1 | Une flèche entre couloirs ne passe derrière aucune autre étape. | e2e | `CrossLaneFlow_NeverBehindAnotherStep` | |
| T-E2E-LIE-03 | `3. clic droit : inserer une etape avant puis apres, dans le flux` | 1 | Le menu contextuel insère une étape avant ou après dans le flux, et chaque insertion s'annule (renommage puis insertion). | e2e | `ContextMenu_InsertBeforeAfterInFlow_Undoable` | Ctrl+Z servi par undo serveur |
| T-E2E-LIE-04 | `4. ajout rapide au survol : + a droite, a gauche, en bas, en haut` | 1 | Les boutons + au survol ajoutent une étape à droite, à gauche, dessous et dessus, absents vers l'extérieur de la carte. | e2e | `HoverQuickAdd_PlusOnFourSides` | |
| T-E2E-LIE-05 | `5. lecture seule et mode relier : aucun +` | 1 | En mode Relier, aucun bouton + n'apparaît. | e2e | `LinkMode_NoQuickAddButtons` | le titre annonce aussi la lecture seule, que le test ne vérifie pas : ajouter un cas lecteur |

### parcours-animateur.spec.ts

| N° | Test 1.x | Cas | Intention (ce que le test prouve, une phrase) | Cible 2.0 | Nom 2.0 proposé (anglais, PascalCase avec _ ) | Remarque |
|---|---|---|---|---|---|---|
| T-E2E-PAR-01 | `parcours de l animateur : du projet vide a une carte detaillee` | 1 | De l'accueil vide à une carte détaillée : création du projet, couloirs, étapes, flèches par Ctrl+K et en deux clics, annuler et rétablir, glisser, supprimer recousu, détailler, fil d'Ariane, bascule de langue mémorisée, retour à l'accueil. | e2e | `FacilitatorJourney_EmptyProjectToDetailedMap` | authentification Windows remplacée par le jeton de lancement en mode Poste ; lien d'en-tête « Topogenia » ; bouton principal vers le dernier projet ouvert (section 10) |

### pictos.spec.ts

| N° | Test 1.x | Cas | Intention (ce que le test prouve, une phrase) | Cible 2.0 | Nom 2.0 proposé (anglais, PascalCase avec _ ) | Remarque |
|---|---|---|---|---|---|---|
| T-E2E-PIC-01 | `pictogrammes des etapes, affichage au survol memorise, outil Main a l ouverture` | 1 | La carte s'ouvre avec l'outil Main, les pictogrammes (documents, irritants, durée) s'affichent, et le mode « au survol » est mémorisé après rechargement. | e2e | `StepMarkers_HoverOnlyPreferencePersisted_HandToolOnOpen` | pictogrammes de la charte (9.4) |

### raci.spec.ts

| N° | Test 1.x | Cas | Intention (ce que le test prouve, une phrase) | Cible 2.0 | Nom 2.0 proposé (anglais, PascalCase avec _ ) | Remarque |
|---|---|---|---|---|---|---|
| T-E2E-RAC-01 | `RACI : calque, attribution en deux clics, A unique, matrice copiable, annulation` | 1 | Le calque RACI, éteint par défaut, signale l'approbateur manquant, attribue un rôle en deux clics, garde un seul A, s'annule par Ctrl+Z, donne une matrice copiable et reste mémorisé. | e2e | `RaciLayer_TwoClickAssign_SingleApprover_CopyMatrix_Undo` | Ctrl+Z servi par undo serveur |
| T-E2E-RAC-02 | `RACI : le calque masque les + d ajout rapide` | 1 | Le calque RACI masque les boutons + d'ajout rapide. | e2e | `RaciLayer_HidesQuickAddButtons` | |

### regressions.spec.ts

Le premier test est déclaré dans une boucle `for (essai = 1..4)` : Playwright en compte quatre.

| N° | Test 1.x | Cas | Intention (ce que le test prouve, une phrase) | Cible 2.0 | Nom 2.0 proposé (anglais, PascalCase avec _ ) | Remarque |
|---|---|---|---|---|---|---|
| T-E2E-REG-01 | `detailler plusieurs etapes garde toutes les fleches visibles (essai ${essai})` | 4 | Détailler plusieurs étapes garde toutes les flèches visibles pendant et après le nommage, sur quatre essais (défaut intermittent). | e2e | `DetailSelection_AllFlowsStayVisible_Repeated` | les attentes fixes (`waitForTimeout`) sont à remplacer par une attente sur un état |
| T-E2E-REG-02 | `glisser l etape seule de la 2e rangee d un couloir la garde dans son couloir` | 1 | Glisser l'étape seule de la deuxième rangée la garde dans son couloir. | e2e | `DragSecondRowStep_StaysInItsLane` | |
| T-E2E-REG-03 | `une fleche se choisit d un clic et se supprime avec Suppr` | 1 | Une flèche se sélectionne d'un clic et se supprime avec Suppr. | e2e | `ClickFlowThenDelete_RemovesFlow` | |
| T-E2E-REG-04 | `deux appuis rapides sur la fleche droite avancent de deux colonnes` | 1 | Deux appuis rapides sur la flèche droite avancent l'étape de deux colonnes. | e2e | `TwoQuickRightArrows_MoveTwoColumns` | file d'envoi des commandes côté client |

### remontee.spec.ts

| N° | Test 1.x | Cas | Intention (ce que le test prouve, une phrase) | Cible 2.0 | Nom 2.0 proposé (anglais, PascalCase avec _ ) | Remarque |
|---|---|---|---|---|---|---|
| T-E2E-REM-01 | `remonter un detail, ventiler son reliquat dans le cadre, puis tout defaire par Ctrl+Z` | 1 | Remonter un détail ouvre le cadre et son reliquat, la ventilation le ferme, et trois Ctrl+Z défont, dans l'ordre, l'irritant, le document puis la remontée. | e2e | `PromoteDetail_DistributeLeftover_UndoStepByStep` | remplacé par undo serveur (affectations et remontée annulables, 4.6) ; positions du flux remises par le serveur |
| T-E2E-REM-02 | `le ruban : Detailler une etape choisie, ou plusieurs ensemble ; Terminer range le reste` | 1 | Le bouton Détailler du ruban s'active selon la sélection, et « Terminer » range le reste et l'annonce. | e2e | `RibbonDetail_EnabledBySelection_FinishAssignsRemainder` | |
| T-E2E-REM-03 | `sans reliquat, le cadre montre les etapes arrivees jusqu a Fermer le cadre ; Ctrl+Z defait la remontee` | 1 | Sans reliquat, le cadre reste jusqu'à « Fermer le cadre », puis Ctrl+Z défait la remontée. | e2e | `PromoteDetail_NoLeftover_CloseFrame_ThenUndo` | la fermeture d'un cadre vide reste non annulable : Ctrl+Z porte sur la remontée (4.6) |

### selection.spec.ts

| N° | Test 1.x | Cas | Intention (ce que le test prouve, une phrase) | Cible 2.0 | Nom 2.0 proposé (anglais, PascalCase avec _ ) | Remarque |
|---|---|---|---|---|---|---|
| T-E2E-SEL-01 | `copier puis coller avec une etape choisie : la copie s insere dans le flux apres elle ; annulable` | 1 | Copier puis coller avec une étape choisie insère la copie dans le flux après elle, la sélectionne et s'annule. | e2e | `CopyPasteWithSelectedStep_InsertedAfterIt_Undoable` | coller devient annulable par undo serveur (4.6) |
| T-E2E-SEL-02 | `couper puis coller ici : l etape garde son code, sort du flux recousu ; Echap renonce ; annulable` | 1 | Couper grise l'étape, Échap y renonce, « Coller ici » la déplace avec son code en recousant le flux, et Ctrl+Z l'annule. | e2e | `CutPasteHere_KeepsCode_StitchesFlow_EscapeCancels_Undoable` | |
| T-E2E-SEL-03 | `supprimer une etape au milieu du flux recoud les fleches, comme Visio` | 1 | Supprimer une étape au milieu du flux recoud les flèches et Ctrl+Z la rend. | e2e | `DeleteMiddleStep_StitchesFlows_Undoable` | |
| T-E2E-SEL-04 | `detailler la selection : une etape a nommer remplace le bloc, sa carte detaillee le contient` | 1 | « Détailler la sélection » remplace le bloc par une étape à nommer dont la carte détaillée contient le bloc. | e2e | `DetailSelection_NamedStepReplacesBlock_SubMapContainsIt` | |
| T-E2E-SEL-05 | `tout selectionner et fleches du clavier : la selection change de couloir et de colonne` | 1 | Ctrl+A sélectionne tout, les flèches du clavier changent l'étape de couloir et de colonne, et deux Ctrl+Z la ramènent. | e2e | `SelectAllAndArrowKeys_MoveLaneAndColumn_Undoable` | |

### traductions.spec.ts

| N° | Test 1.x | Cas | Intention (ce que le test prouve, une phrase) | Cible 2.0 | Nom 2.0 proposé (anglais, PascalCase avec _ ) | Remarque |
|---|---|---|---|---|---|---|
| T-E2E-TRA-01 | `telecharger les textes, completer la colonne EN, rapporter le fichier : la carte passe en anglais` | 1 | Le classeur des textes se télécharge sans les couloirs traduits à la main, complété il se rapporte, et la carte passe en anglais. | e2e | `TranslationsExcelRoundTrip_MapSwitchesToEnglish` | |

### types-et-detail.spec.ts

| N° | Test 1.x | Cas | Intention (ce que le test prouve, une phrase) | Cible 2.0 | Nom 2.0 proposé (anglais, PascalCase avec _ ) | Remarque |
|---|---|---|---|---|---|---|
| T-E2E-TYP-01 | `changer le type par le clic droit, annulable ; aucune barre flottante` | 1 | Le type change par le menu contextuel (coché dans le menu), sans barre flottante, sans toucher la palette, et s'annule. | e2e | `ChangeTypeViaContextMenu_Undoable_NoFloatingBar` | |
| T-E2E-TYP-02 | `bug du 17/10 : la palette donne le type des nouvelles etapes, meme avec une etape selectionnee` | 1 | La palette fixe le type des nouvelles étapes et ne change pas celui de l'étape sélectionnée. | e2e | `Palette_SetsNewStepType_DoesNotChangeSelected` | |
| T-E2E-TYP-03 | `supprimer le detail : vide sans question puis passage en Fin ; rempli avec confirmation et annulable` | 1 | Un détail vide se supprime sans question et l'étape peut passer en Fin ; un détail rempli demande confirmation et Ctrl+Z le rend avec son contenu. | e2e | `DeleteDetail_EmptyNoPrompt_FilledConfirmedAndUndoable` | `waitForTimeout` à remplacer par une attente sur un état |
| T-E2E-TYP-04 | `carte detaillee restee vide : etape ordinaire au retour, reouverte ensuite` | 1 | Une étape à sous-carte vide reste ordinaire, « Détailler » rouvre la même carte et, remplie, l'étape devient détaillée. | e2e | `EmptySubMap_StepStaysPlain_ReopenedThenDetailed` | |

### vue-accrochee.spec.ts

| N° | Test 1.x | Cas | Intention (ce que le test prouve, une phrase) | Cible 2.0 | Nom 2.0 proposé (anglais, PascalCase avec _ ) | Remarque |
|---|---|---|---|---|---|---|
| T-E2E-VUE-01 | `petite carte : ne descend pas, ne monte pas, ne part pas a gauche ; zoom ancre en haut` | 1 | Une petite carte ne se déplace ni verticalement ni à gauche et le zoom reste ancré en haut. | e2e | `SmallMap_ViewPinned_ZoomAnchoredTop` | |
| T-E2E-VUE-02 | `carte haute et large : on defile jusqu au bout, pas au-dela, puis on revient au debut` | 1 | Une carte haute et large défile jusqu'au bout sans aller au-delà, et « Revenir au début » la remet en place. | e2e | `LargeMap_ScrollToEdgesOnly_BackToStart` | |

## Totaux

### Par fichier

| Groupe | Fichier 1.x | Méthodes | Cas |
|---|---|---|---|
| C# intégration | BranchesTests.cs | 5 | 5 |
| C# intégration | CorbeilleTests.cs | 4 | 4 |
| C# intégration | DocumentsFluxTests.cs | 9 | 14 |
| C# intégration | FichesTests.cs | 13 | 19 |
| C# intégration | FichiersTests.cs | 9 | 9 |
| C# intégration | FrequenceTests.cs | 1 | 1 |
| C# intégration | InsertionTests.cs | 9 | 9 |
| C# intégration | ModeleTests.cs | 14 | 14 |
| C# intégration | ProjetsEtSecuriteTests.cs | 24 | 28 |
| C# intégration | RaciTests.cs | 7 | 7 |
| C# intégration | RemonteeTests.cs | 13 | 13 |
| C# intégration | ReperesTests.cs | 1 | 1 |
| C# intégration | ScenariosFluxTests.cs | 7 | 7 |
| C# intégration | SelectionTests.cs | 7 | 7 |
| C# intégration | SousCarteVideTests.cs | 4 | 4 |
| C# intégration | SousCartesEtAnnulationTests.cs | 24 | 24 |
| C# intégration | StructureTests.cs | 32 | 32 |
| C# intégration | TraductionsTests.cs | 6 | 6 |
| **Sous-total** | **C# intégration (18 fichiers)** | **189** | **204** |
| C# unitaires | DomaineTests.cs | 26 | 50 |
| C# unitaires | FichesTests.cs | 8 | 21 |
| C# unitaires | FormatPmapTests.cs | 22 | 22 |
| C# unitaires | InfrastructureTests.cs | 17 | 37 |
| **Sous-total** | **C# unitaires (4 fichiers)** | **73** | **130** |
| vitest | carte/alignement.test.ts | 16 | 16 |
| vitest | carte/export.test.ts | 9 | 9 |
| vitest | carte/geometrie.test.ts | 38 | 38 |
| vitest | carte/historique.test.ts | 7 | 7 |
| vitest | carte/magasin.test.ts | 33 | 33 |
| vitest | carte/pressePapiers.test.ts | 5 | 5 |
| vitest | carte/raci.test.ts | 5 | 5 |
| vitest | commun/commun.test.tsx | 21 | 21 |
| vitest | fiche/PanneauFiche.test.tsx | 24 | 24 |
| vitest | format/pmap.test.ts | 6 | 6 |
| vitest | projet/Fichiers.test.tsx | 9 | 9 |
| vitest | traduction/classeur.test.ts | 3 | 3 |
| e2e | alignement.spec.ts | 1 | 1 |
| e2e | branches.spec.ts | 4 | 4 |
| e2e | corbeille.spec.ts | 1 | 1 |
| e2e | export.spec.ts | 3 | 3 |
| e2e | fiches.spec.ts | 5 | 5 |
| e2e | fichiers.spec.ts | 4 | 4 |
| e2e | liens-et-insertion.spec.ts | 5 | 5 |
| e2e | parcours-animateur.spec.ts | 1 | 1 |
| e2e | pictos.spec.ts | 1 | 1 |
| e2e | raci.spec.ts | 2 | 2 |
| e2e | regressions.spec.ts | 4 | 7 |
| e2e | remontee.spec.ts | 3 | 3 |
| e2e | selection.spec.ts | 5 | 5 |
| e2e | traductions.spec.ts | 1 | 1 |
| e2e | types-et-detail.spec.ts | 4 | 4 |
| e2e | vue-accrochee.spec.ts | 2 | 2 |

### Par technologie

| Technologie | Fichiers | Méthodes / déclarations | Cas exécutés | Lignes du présent inventaire |
|---|---|---|---|---|
| C# xUnit (intégration + unitaires) | 22 (+ 2 fichiers d'outillage sans test) | 262 (247 `[Fact]`, 15 `[Theory]`) | 334 | 262 |
| vitest | 12 | 176 | 176 | 176 |
| Playwright | 16 (+ `preparer.mjs`, `purger.sql`) | 46 | 49 | 46 |
| **Total** | **50** | **484** | **559** | **484** |

### Comparaison avec la spécification

| Chiffre | Spécification | Compté | Écart | Explication |
|---|---|---|---|---|
| Tests C# | 334 (section 11) | 334 cas | 0 | Le chiffre de la spécification compte les cas exécutés : 247 `[Fact]` plus 87 jeux `InlineData` répartis sur 15 `[Theory]` (aucun `MemberData`). En méthodes, on compte 262. L'annexe A, section 6, annonce « environ 240 méthodes réparties en 24 fichiers » : 262 méthodes réparties en 22 fichiers de test (24 fichiers avec `FabriqueApi.cs` et `Scenario.cs`, qui n'en contiennent aucune). |
| Tests vitest | 176 | 176 | 0 | Un `it(...)` par cas, sans `it.each` ni test ignoré. |
| Parcours e2e | 49 | 46 déclarations, 49 cas | 0 en cas exécutés | Le test « détailler plusieurs étapes garde toutes les flèches visibles » de `regressions.spec.ts` est déclaré dans une boucle `for (essai = 1..4)` : une déclaration, quatre parcours exécutés (46 - 1 + 4 = 49). |

Le portage doit donc produire au moins 334 cas .NET (répartis entre `Domain.Tests`,
`Application.Tests`, `Persistence.Tests`, `Host.Tests` et `Interchange.Tests`), les 176 cas
vitest (en `web unit` ou, pour l'annulation, en tests serveur selon la colonne « Cible 2.0 ») et
49 cas `e2e`, plus les nouveautés de la section 11. Les trois cas T-WGEO-27 à 29 (instantanés
de positions côté client, `instantane` / `dispositionVers`) ne sont pas retirés : ils sont
remplacés par des tests d'undo serveur en `Application.Tests` et `Host.Tests`, sans test
`web unit` d'instantané (AR-48).

## Tests dont l'intention contredit la spécification 2.0

Assertion à inverser ou à changer (défaut 1.x corrigé) :

| N° | Test 1.x | Ce que prouve la 1.x | Ce qu'exige la 2.0 | Référence |
|---|---|---|---|---|
| T-PSEC-24 | `Sante_accessible_sans_authentification` | `/api/sante` anonyme renvoie l'état de la base | Mode Serveur : `/api/health` anonyme ne renvoie que `{status}`, le détail exige l'authentification ; mode Poste : cookie de session exigé (AR-46) | 5.2, 5.3, 5.6 point 8 |
| T-UINF-15 | `If_match_absent_donne_null` | Sans `If-Match`, aucun contrôle de version | En mode Serveur, `If-Match` absent sur un objet versionné = 428 | 5.3, 5.6 point 3 |
| T-FICR-05 | `Fichier_invalide_liste_des_problemes_et_rien_n_est_cree` | Taille contrôlée après lecture, borne 8 M caractères, message « 8 Mo » | Limite 10 Mo pour `.pmap`, appliquée avant la lecture du corps | 5.4, 5.6 point 7 |
| T-WCOM-17 | `projets, participants avec sponsors, bouton principal vers la carte` | Bouton principal et participants sur le premier projet de la liste | Sur le dernier projet ouvert, mémorisé dans le profil serveur ; avant toute ouverture, sur le premier projet par code (AR-49) | 10, 13 |
| T-WHIS-05, T-WHIS-06 | échec d'annulation ou de rétablissement | Un échec vide toute la pile de la carte | Refus `OPERATION_DEPASSEE` (409) avec auteur et heure, ou `CARTE_MODIFIEE`, seuls codes de refus (AR-43) ; un refus d'undo ou de redo vide la pile de l'utilisateur sur la carte, message selon le code, « impossible » pour tout autre échec (AR-05) | 4.6 |
| T-WMAG-06 | couper dans une carte et coller dans une autre | La pile de la carte d'origine est vidée | Opération inscrite au journal serveur sur la carte d'arrivée ; pile d'origine non vidée (AR-06) | 4.6 |

Test à compléter parce qu'il valide un comportement que la 2.0 corrige sans le contredire
frontalement (le test 1.x reste vrai, mais il manque l'assertion de la correction) :

| N° | Correction à ajouter | Référence |
|---|---|---|
| T-REMO-01 | Le lecteur ne voit dans le reliquat que la fiche validée | 5.6 point 1 |
| T-SCAN-04 | Un code d'un autre projet donne `ETAPE_INCONNUE` (et non `ETAPE_HORS_CARTE`) | 5.6 point 2 |
| T-PSEC-14 | Refus `PROJET_SANS_ANIMATEUR` | 5.6 point 4 |
| T-PSEC-15 | Aucun invité créé quand la requête échoue | 5.6 point 5 |
| T-PSEC-01 | Cache du profil, pas d'écriture à chaque requête | 5.6 point 6 |
| T-TRAD-01 | Écriture idempotente sous le verrou de projet | 5.6 point 11 |
| T-STRU-25, T-TRAD-05 | Un libellé inchangé n'efface pas les traductions | 13 |
| T-DOCF-06 | Règle unique de retrait d'irritant : un irritant déjà traité est détaché vers la carte | 13 |
| T-UPMA-14, T-WPMA-05 | RACI refusé sur début, fin et losanges dans le `.pmap` | 7.5 |
| T-UDOM-22, T-STRU-03 | Casse de la couleur : minuscules acceptées et normalisées partout (AR-11) | annexe A, 6.8 |

Aucun test 1.x ne couvre la troncature silencieuse de l'export `.pmap` à 12 niveaux ; la 2.0
doit en ajouter un qui vérifie l'avertissement (section 13).


## Persistance, partie A (P3-T07)

Tests d'intégration 1.x (`api.Tests\Integration`) de cible `Persistence.Tests` qui tournaient sur la base réelle
(concurrence, case, version, flèche, disposition, lot, déclencheur), portés sur l'unité de travail et le magasin de
l'espace (`IUnitOfWork.RunAsync`, `IWorkspaceStore`), en théories exécutées sur SQLite et SQL Server. Préfixe des noms :
`Topogenia.Persistence.Tests.Porting.`. La part HTTP de ces tests (codes de statut, `If-Match`, `VERSION_INVALIDE`,
messages) reste à porter en `Host.Tests` (phase 4). Les tests de cible `Persistence.Tests` sur la purge, les parcours
récursifs et les vues `bi` (T-CORB-03, T-CORB-04, T-REMO-06, T-SCAN-10, T-FICI-05, T-MODE-14) relèvent de la partie B
et de P3-T06.

| N° | Test 1.x | Test 2.0 | Remarque |
|---|---|---|---|
| T-STRU-24 | `EtapesTests.Creations_concurrentes_dans_la_meme_case` | `StructurePortedTests.Ten_creations_in_one_cell_at_the_same_time_all_succeed_with_distinct_columns_and_codes` | Dix tâches simultanées, une unité chacune ; sérialisation par le verrou de projet (`structure_version`, 4.7) au lieu d'UPDLOCK |
| T-STRU-13 | `EtapesTests.Codes_sequentiels_et_immuables` | `StructurePortedTests.Step_codes_are_sequential_kept_on_a_change_and_never_given_again_after_a_deletion` | Compteur écrit par le `ChangeSet` sous le verrou (plus d'`UPDATE ... OUTPUT`) |
| T-STRU-19 | `EtapesTests.Deplacement_dans_le_couloir_vers_la_gauche_et_vers_la_droite` | `StructurePortedTests.A_step_moved_left_then_right_in_its_lane_shifts_its_neighbours_without_blocking_itself` | Stationnement en deux temps (ADR 016, 030) |
| T-STRU-21 | `EtapesTests.Modification_concurrente_detectee` | `StructurePortedTests.A_step_changed_since_its_reading_is_refused_with_the_author_and_keeps_the_first_change` | Part base : `CONFLIT_VERSION` avec l'auteur ; la version illisible (`VERSION_INVALIDE`) relève de l'hôte |
| T-STRU-06 | `CouloirsTests.Version_perimee_renvoie_409_avec_auteur` | `StructurePortedTests.A_lane_changed_since_its_reading_is_refused_with_the_author_and_the_time_of_the_change` | Part base : `updated_by`, `updated_at` ; la durée relative du message relève de l'hôte |
| T-STRU-26 | `LiensTests.Doublon_refuse_mais_sens_inverse_et_boucle_acceptes` | `StructurePortedTests.A_duplicate_flow_is_refused_and_nothing_written_while_the_reverse_flow_is_written` ; `Topogenia.Persistence.Tests.Concurrency.StructureConcurrencyTests.Two_creations_of_one_flow_computed_on_one_state_are_refused_by_the_partial_unique_index` | La « boucle » du nom 1.x est l'aller-retour X > Y > X, accepté ; la flèche réflexive reste refusée (`LIEN_REFLEXIF`) ; index partiel sur les deux bases |
| T-STRU-30 | `DispositionTests.Echange_de_deux_etapes_et_ordre_des_couloirs_en_un_appel` | `StructurePortedTests.A_layout_that_exchanges_two_steps_and_reorders_the_lanes_is_written_in_one_change_set` | Échange de cases : aucun ordre possible sans le stationnement |
| T-MODE-01 | `Un_lot_garde_qui_supprime_et_qui_restaure_et_ne_se_restaure_qu_une_fois` | `ModelPortedTests.A_batch_keeps_who_deleted_and_who_restored_and_is_restored_once` | Remplacé par undo serveur : second undo `OPERATION_DEPASSEE` (au lieu de `LOT_INTROUVABLE`, AR-43) |
| T-MODE-02 | `Une_operation_qui_ne_supprime_rien_ne_laisse_pas_de_lot` | `ModelPortedTests.An_operation_that_deletes_nothing_leaves_no_batch` | |
| T-MODE-13 | `La_base_refuse_une_etape_supprimee_qui_garde_des_fleches_vivantes` | `ModelPortedTests.A_deleted_step_keeps_no_live_flow_in_the_database_its_flows_go_in_its_batch` ; `ModelPortedTests.The_database_has_no_trigger_a_deleted_step_with_a_live_flow_is_only_refused_by_the_domain` | Assertion inversée (D04) : plus de déclencheur, la base accepte la ligne et les invariants du domaine la refusent (`CARTE_MODIFIEE`) |
| T-FICI-09 | `FichesTests.Modification_concurrente_signalee_avec_son_auteur` | `ModelPortedTests.A_sheet_saved_or_validated_on_a_stale_reading_is_refused_with_the_author_of_the_change` | Jeton de version portable sur la fiche et la validation |
| T-DOCF-08 | `Un_document_coche_par_l_etape_voisine_ne_provoque_pas_de_conflit_sur_la_fiche_en_cours` | `ModelPortedTests.A_use_ticked_by_the_neighbour_step_does_not_change_the_version_of_the_sheet_of_the_producer` | La complétude affichée (29) relève de l'hôte |
| T-SCAN-18 | `RestaurationTests.Un_lien_recree_entre_temps_n_est_pas_double` | `ModelPortedTests.Undoing_a_flow_deletion_after_the_flow_was_drawn_again_never_doubles_it` | Remplacé par undo serveur : l'undo réussit et ne double pas la flèche |

## Persistance, partie B

Tests 1.x dont la part persistance est portée par P3-T07 partie B (nombre de requêtes, purge, vues de rapport). Le
nom 2.0 proposé en phase 0 est remplacé par les tests réels ci-dessous (préfixe `Topogenia.Persistence.Tests.`) ;
les parts Application et Hôte restent dans leur phase.

| N° | Test 1.x | Test 2.0 | Correspondance |
|---|---|---|---|
| T-MODE-14 | `Les_vues_des_rapports_se_lisent` | `Bi.ReportViewTests.Each_report_view_has_the_columns_of_the_1x_view_in_their_order` ; `Bi.ReportViewTests.Each_report_view_gives_the_live_content_of_a_project_and_nothing_deleted_or_hidden` ; `Bi.ReportViewTests.Nothing_of_a_project_in_the_trash_reaches_the_report_views` ; `Bi.RoleTests.The_report_role_reads_every_report_view_and_no_table` | Remplace `BiViews_AreReadable_DepthAndLiveStepsOnly` (A-6-43) : chacune des 11 vues se lit sous le rôle `topogenia_bi`, avec les colonnes de la 1.x et le contenu calculé par le domaine (profondeur et chemin des cartes, étapes vivantes seulement) ; SQL Server seulement |
| T-CORB-03 | `Suppression_definitive_efface_tout_le_projet_et_seulement_lui` | `Purge.TrashPurgeTests.A_trashed_project_past_the_retention_is_purged_whole_in_short_transactions_and_nothing_else` ; `Purge.TrashPurgeTests.A_plan_of_another_project_or_a_whole_purge_of_a_live_project_is_rejected_before_any_command` ; `Purge.PurgeRightsTests.A_whole_purge_runs_with_the_rights_of_the_application_role_and_not_with_those_of_the_reports` | Part persistance de `PurgeProject_DeletesWholeProjectOnly_AndFreesCode` : tout le projet effacé (traductions comprises), projet témoin intact, code libéré, sur les deux bases ; le droit de l'administrateur et la route relèvent de P4-T02 et P4-T08 |
| T-CORB-04 | `Purge_au_dela_de_la_duree_de_conservation_projets_et_objets_supprimes` | `Purge.TrashPurgeTests.The_projects_to_purge_are_those_trashed_or_holding_a_batch_before_the_limit` ; `Purge.BatchPurgeTests.An_expired_batch_goes_with_its_objects_and_its_log_entries_while_the_open_frame_and_another_project_stay` | Part persistance de `Purge_AfterRetention_RemovesTrashedProjectsAndBatches_ThenUndoRefused` : candidats par la limite, lot expiré purgé avec les entrées du journal qui y renvoient (spec 4.6). « Pas avant 30 jours » est la règle du domaine (`PurgePlannerTests.Nothing_is_purged_before_the_retention`) ; l'annulation refusée après la purge relève de P4-T03 |
| T-REMO-06 | `La_purge_attend_la_fin_du_cadre` | `Purge.BatchPurgeTests.An_expired_batch_goes_with_its_objects_and_its_log_entries_while_the_open_frame_and_another_project_stay` ; `Purge.BatchPurgeTests.The_batch_of_a_frame_is_purged_once_the_frame_is_finished_and_what_was_arranged_stays` | Part persistance de `Purge_KeepsOpenFrameBatch_ThenDeletesAfterFinish` : le lot du cadre ouvert reste, puis est purgé après la fin du cadre sans rien perdre de ce qui a été rangé |
| T-SCAN-06 | `SousCartesTests.Detailler_reprend_le_couloir_parent_et_construit_l_ariane` | `Queries.QueryCountTests.The_breadcrumb_is_read_in_the_same_number_of_queries_at_depth_one_and_at_depth_fifty` ; `Queries.RecursiveWalkTests.A_chain_of_sixty_maps_read_back_stops_its_depth_and_breadcrumb_at_fifty_levels` | Complément persistance de la remarque « fil d'Ariane sans N+1 » (dette 6.6) ; le test lui-même reste aux niveaux Domain, Application et Host |
| T-FICI-05 | `Completude_sur_sept_champs_l_acteur_prerempli_ne_compte_pas` | `Bi.ReportViewTests.The_completeness_of_the_report_view_is_the_one_of_the_domain_for_every_step_with_content` | Part `bi.step_sheet` : la complétude de la vue est celle du domaine (`SheetCompleteness`, sept champs, l'acteur prérempli ne compte pas ; étape à acteur et quatre rubriques : 57) ; SQL Server seulement ; la part de la vue de la carte est au domaine |
| T-PSEC-09 | `ProjetsTests.Code_projet_unique` | `Concurrency.StructureConcurrencyTests.Two_creations_of_one_project_code_at_the_same_time_leave_one_project_and_refuse_the_other` | Part persistance : l'index unique du code refuse la seconde création sur les deux bases ; le 409 relève de l'hôte (phase 4) |
| T-RACI-02 | `Un_seul_approbateur_poser_A_ailleurs_le_deplace` | `RoundTrips.StructureRoundTripTests.Setting_an_approval_replaces_in_the_database_a_hidden_approval_left_on_a_lane_of_another_map` | Part persistance : l'ancien A est retiré avant que le nouveau soit écrit (stationnement, ADR 030) ; la règle reste au domaine |
| T-STRU-16 | `EtapesTests.Couloir_d_une_autre_carte_refuse_RG_C02` | `Model.ForeignKeyTests.A_step_on_a_lane_of_another_map_is_refused_by_the_composite_key` ; `Guards.WritePathGuardTests.A_step_put_on_a_lane_of_another_map_is_refused_by_the_composite_foreign_key` | Clé étrangère composée `FK_step_lane` (lane_id, map_id), deux moteurs ; la règle RG-C02 reste au domaine |
| T-WHIS-03 | `garde au moins ${NIVEAUX_HISTORIQUE} niveaux et oublie les plus anciens` | (phase 4) | Écarté de la phase 3 : la pile des 50 niveaux est tenue par le journal des opérations (`IOperationLog`, P4), dont la persistance n'a pas encore d'implémentation |
| T-WHIS-07 | `pas de double annulation pendant qu une operation est en cours` | `Writing.UnitOfWorkTests.Structure_operations_on_one_project_run_at_the_same_time_never_interleave` | Part persistance : deux opérations sur un projet sont sérialisées par son verrou ; le refus côté client relève de l'interface (phase 6) |
| T-SCAN-10 | `SousCartesTests.Supprimer_une_macro_etape_avec_toute_sa_descendance` | aucun test de persistance propre ; domaine : `SubMapDeletionTests.Deleting_a_macro_step_with_delete_removes_every_level_below_in_the_same_batch` ; aller-retour du lot : `RoundTrips.StructureRoundTripTests` | La « CTE récursive portable » est remplacée par le parcours du domaine sur l'espace chargé (ADR 034) : aucune requête récursive n'existe à porter ; les parts Application et Hôte restent en phase 4 |

## Échange, `.pmap` 1.1 (P5-T04)

Les tests 1.x du format (`FormatPmapTests`) et des fichiers (`FichiersTests`, `BranchesTests.Fichier_pmap...`), portés sur le
lecteur, l'écrivain et la construction de `Topogenia.Interchange.Pmap` et sur les routes `.pmap` (ADR 054). Les messages
1.x en dur deviennent des codes rendus par l'hôte (A-3-72) : les assertions portent sur le chemin et le code, et les
messages français et anglais sont vérifiés à l'hôte. Préfixes : `Topogenia.Interchange.Tests.Pmap.`,
`Topogenia.Host.Tests.Ported.PmapFilesPortedTests.`, `Topogenia.Host.Tests.Exchange.PmapRoutesTests.`.

| N° | Test 1.x | Test 2.0 | Remarque |
|---|---|---|---|
| T-UPMA-01 | `Exemple_minimal_de_la_documentation` | `PmapReaderTests.The_minimal_sample_reads_with_its_defaults` | `samples/minimal.pmap` |
| T-UPMA-02 | `Exemple_complet_sous_carte_raci_traductions_incertitudes` | `PmapReaderTests.The_complete_sample_reads_its_sub_map_raci_translations_sheets_and_uncertainties` | exemple fictif refait (couloirs Nord et Sud, ERP), fiches et traductions d'étapes en plus |
| T-UPMA-03 | `Exemple_du_guide_pour_IA_accepte_tel_quel` | `PmapDocumentationTests.The_example_of_the_guide_for_an_ai_opens_as_it_is_with_its_rows_sheets_and_uncertainties` | guide 1.1 dans `docs/pmap/` |
| T-UPMA-04 | `Copier_coller_depuis_une_IA_bloc_de_code_BOM_virgules_finales_commentaires` | `PmapReaderTests.A_copy_paste_from_an_ai_is_read_code_block_byte_order_mark_trailing_commas_and_comments` | |
| T-UPMA-05 | `Ni_JSON_ni_Process_map_ni_version_lisible` | `PmapReaderTests.Neither_json_nor_the_format_nor_its_version_is_read` | écart voulu : seule la 1.1 est lue (AR-93, A-2.17-50), une 1.0, 1.2 ou 2.0 est refusée ; marqueur `topogenia` |
| T-UPMA-06 | `Propriete_inconnue_signalee_avec_les_proprietes_possibles` | `PmapReaderTests.An_unknown_property_is_reported_with_the_possible_ones` | liste des propriétés 1.1 |
| T-UPMA-07 | `Champs_obligatoires_types_et_valeurs` | `PmapReaderTests.Required_fields_types_and_values_are_checked_each_at_its_place` | |
| T-UPMA-08 | `Textes_bornes_sans_HTML_ni_caracteres_de_controle` | `PmapReaderTests.Texts_follow_the_rules_of_every_entered_text_of_the_domain` | un seul contrôle de texte, `TextRules` (dette 6.4) |
| T-UPMA-09 | `Identifiants_uniques_dans_tout_le_fichier` | `PmapReaderTests.Identifiers_are_unique_in_the_whole_file_and_never_empty` | |
| T-UPMA-10 | `Regle_1_une_etape_dans_un_couloir_de_sa_carte` | `PmapReaderTests.Rule_1_a_step_is_on_a_lane_of_its_own_map` | |
| T-UPMA-11 | `Regles_2_et_3_liens_dans_la_meme_carte_sans_boucle_ni_doublon` | `PmapReaderTests.Rules_2_and_3_flows_stay_in_their_map_without_loop_nor_duplicate` | |
| T-UPMA-12 | `Regle_4_colonnes_pour_toutes_ou_pour_aucune_et_sans_case_en_double` | `PmapReaderTests.Rule_4_columns_for_all_steps_or_none_and_never_two_steps_in_one_cell` | rangées en plus |
| T-UPMA-13 | `Regle_5_ni_debut_ni_fin_detailles_et_sous_carte_vide_ignoree` | `PmapReaderTests.Rule_5_no_detail_on_a_step_without_content_and_an_empty_sub_map_is_left_out` | |
| T-UPMA-14 | `Regle_6_raci` | `PmapReaderTests.Rule_6_raci` | refus d'un RACI sur début, fin, ET, OU inclusif ajouté (7.5) |
| T-UPMA-15 | `Messages_en_anglais` | `PmapRoutesTests.A_file_with_faults_is_refused_with_each_fault_at_its_place_in_the_language_of_the_user_and_nothing_is_written` | messages tirés des fichiers de l'hôte (dette 6.5) |
| T-UPMA-16 | `Nombre_d_erreurs_borne` | `PmapReaderTests.Limits_lanes_of_a_map_steps_of_the_file_and_the_faults_reported` | |
| T-UPMA-17 à 20 | `Placement_*` | `Topogenia.Domain.Tests.Coverage.AutomaticColumnsTests.*` (phase 2) ; `PmapBuildTests.Without_columns_the_steps_take_the_automatic_placement` | le placement est dans le domaine |
| T-UPMA-21 | `Ecriture_relue_a_l_identique_et_lisible` | `PmapCopyTests.The_writing_is_readable_lf_two_spaces_accents_as_they_are_and_no_default_value` | écriture 1.1 |
| T-UPMA-22 | `Ecriture_echappe_ce_qui_doit_l_etre` | `PmapCopyTests.The_writing_escapes_what_must_be` | |
| T-FICR-01 | `Ouvrir_l_exemple_du_guide_pour_IA` | `PmapFilesPortedTests.The_example_of_the_guide_for_an_ai_opens_as_a_project_with_its_counts_and_its_raci` | compteurs du guide 1.1 |
| T-FICR-02 | `Ouvrir_l_exemple_complet_de_la_documentation` | `PmapFilesPortedTests.The_complete_example_creates_every_content_and_the_opener_is_the_only_facilitator` | même intention sur les deux bases (porte P5 round 1) : traduction et couleurs des couloirs (teintes de la charte en alternance, AR-45), types et automatisation des étapes, libellés des flèches de la décision, RACI, contenu, chemins et codes de la sous-carte |
| T-FICR-03 | `Ouvrir_un_fichier_sans_colonnes_place_les_etapes_selon_les_fleches` | `PmapFilesPortedTests.A_file_without_columns_is_placed_by_its_flows` | |
| T-FICR-04 | `Copie_telechargee_puis_rouverte_a_l_identique` | `PmapFilesPortedTests.A_copy_opened_again_and_copied_gives_the_same_file_with_its_rows_gateways_and_translations` ; `PmapFilesPortedTests.A_project_drawn_by_the_api_comes_back_from_its_copy_with_its_lanes_and_its_steps_in_reading_order` | traductions d'étapes et de flèches vérifiées (7.5) ; comme la 1.x (`FichiersTests.cs:160`), un projet dessiné par l'API revient de sa copie avec ses couloirs (nom, type, couleur, traduction) et ses étapes, la copie suivant l'ordre de lecture (porte P5 round 1) |
| T-FICR-05 | `Fichier_invalide_liste_des_problemes_et_rien_n_est_cree` | `PmapFilesPortedTests.An_invalid_file_lists_its_problems_and_creates_nothing_and_a_body_too_long_is_refused_before_it_is_read` | défaut 1.x corrigé : 10 Mo refusés avant lecture (413), puis 8 M caractères (`FICHIER_TROP_GROS`) |
| T-FICR-06 | `Code_et_nom_du_projet_a_l_ouverture` | `PmapFilesPortedTests.The_code_and_the_name_of_the_project_at_the_opening` | le nom du fichier saisi entre espaces est rogné et garde ses traductions (`FichiersTests.cs:210`, porte P5 round 1) |
| T-FICR-07 | `Dupliquer_un_projet` | `PmapFilesPortedTests.A_duplication_creates_a_distinct_project_of_the_same_content_and_leaves_the_source_unchanged` | |
| T-FICR-08 | `Droits_ouvrir_et_dupliquer_reserves_aux_administrateurs` | `PmapFilesPortedTests.Opening_and_duplicating_are_for_the_administrator_the_copy_for_every_member` ; `Topogenia.Host.Tests.Api.RightsMatrixTests.Each_role_is_refused_or_granted_as_the_right_of_the_route_says` | anti-CSRF : `EN_TETE_MANQUANT` |
| T-FICR-09 | `Lettres_RACI_du_parametre_appliquees_a_l_ouverture` | `PmapFilesPortedTests.The_raci_letters_of_an_opening_follow_the_setting` | réglage `RaciLetters` de la configuration au lieu du SQL |
| T-BRAN-05 | `Fichier_pmap_garde_les_rangees_et_les_losanges` | `PmapFilesPortedTests.A_copy_opened_again_and_copied_gives_the_same_file_with_its_rows_gateways_and_translations` | |
