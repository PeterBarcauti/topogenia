<!-- 043-hote-http-routes-et-gestionnaires.md -->
<!-- Forme de l'hote HTTP : routes sans regle, gestionnaires d'API, JSON, erreurs, utilisateur de la requete, tests de bout en bout -->

# 043 - Hôte HTTP : routes, gestionnaires d'API, JSON et erreurs

Statut : acceptée (date 24/10/2026)

## Contexte

P4-T04 expose les commandes et lectures de l'application (P4-T01 à P4-T03) selon la table des routes 1.x -> 2.0
(`docs/couverture.md`, spec 3.6). Les tests d'architecture imposent déjà deux règles : toute déclaration de route vit
dans `Host/Endpoints/`, et ce dossier ne dépend ni du domaine, ni de la persistance, ni des formats d'échange
(spec 3.2 : un point d'entrée ne contient aucune règle). Or les commandes prennent des requêtes du domaine
(`AddLaneRequest`...) et rendent des résultats du domaine (`LaneResult`). Il fallait aussi fixer le JSON, la forme des
erreurs, le passage de l'utilisateur authentifié aux gabarits, et la manière de tester l'hôte entier sur les deux bases.

## Décision

- **Deux parties par fonctionnalité.** `Endpoints/<Lot>Endpoints.cs` déclare les routes ; chaque délégué lit la route,
  la chaîne de requête, `If-Match` et le corps, appelle un gestionnaire et rend `TypedResults`. `Api/<Fonctionnalité>/<Fonctionnalité>Api.cs`
  est le gestionnaire : il lit le corps (`record <Action>Body`, types simples) dans la requête du domaine, lance la
  commande par son gabarit et rend la réponse. Les gestionnaires (classes `*Api` de `Api/`) sont enregistrés par leur
  nom, en portée de requête.
- **Lectures de la requête hors du domaine** (`Api/ApiInput`) : version de `If-Match`, guillemets tolérés, illisible
  `VERSION_INVALIDE` (A-2.16-02, 03) ; champ absent dont le domaine a besoin comme identifiant ou nombre
  `CHAMP_OBLIGATOIRE` sur son nom, jamais `Guid.Empty` ni 0 (A-2.18-07) ; code de liste fermée d'une chaîne de requête
  `VALEUR_INVALIDE` sur son nom. Un texte ou un code du corps reste une chaîne, que le domaine contrôle.
- **PATCH** : un champ est `Optional<T>` (absent : rien ne change ; présent : sa valeur, `null` compris), lu par un
  convertisseur appelé seulement pour un champ présent ; le contrat OpenAPI le décrit comme sa valeur, nullable.
- **JSON** : noms en snake_case (A-1.1-01), clés des dictionnaires gardées (langues, détails), énumérations écrites
  par leurs codes `Codes` (`task`, `facilitator`), nombres stricts.
- **Réponse d'une commande sur une carte** : `StructureReply {id, code, ids, operation_id, warnings,
  structure_version, map, history}` (AR-50). Lecture d'une carte : `{map, history}`.
- **Erreurs** : un seul intergiciel, le premier du pipeline (`Errors/ErrorMiddleware`) : refus du domaine ou de
  l'application → statut et `{code, message, details}` dans la langue effective (profil, sinon `Accept-Language`,
  sinon le français, AR-51), nom affiché de l'auteur d'un `CONFLIT_VERSION` ; corps illisible ou de mauvaise forme →
  `REQUETE_INVALIDE` ; toute autre panne → `ERREUR_INTERNE`, journalisée par son seul type (jamais son message ni son
  `ToString()`, ADR 036). Route inconnue sous `/api` → `INTROUVABLE` au même format.
- **Utilisateur de la requête** : l'authentification du mode (P4-T05, P4-T06) nomme un login ; `SignInMiddleware` le
  connecte par `UserRunner.SignInAsync` (création à la première requête, drapeau administrateur relu, langue
  effective) et le pose dans `RequestUser` (`ICurrentUser`, portée de requête). Sans login, les gabarits refusent
  `NON_AUTHENTIFIE` (401). Les administrateurs sont relus à chaque changement du fichier de configuration
  (`IOptionsMonitor`, spec 5.3).
- **Configuration** : section nommée d'après le produit (ADR 013), `Database` (moteur, chaîne), `Administrators`,
  `Languages` (langues, langue par défaut, nom du premier couloir par langue), réglages de l'application.
- **Routes nommées par l'hôte** (la spec n'en donne pas) : `PUT /api/me/last-project` (AR-49 : un GET ne doit pas
  écrire, le client dit le projet ouvert), `PUT /api/projects/{id}/sharing-group-label` (AR-64), `POST /api/maps/{id}/move`
  (glisser groupé, AR-44).
- **Tests de bout en bout** : `Topogenia.Host.Tests` démarre l'hôte entier en mémoire (`WebApplicationFactory`) sur une
  base migrée par le code, SQLite et SQL Server, avec une authentification de test par en-tête ; les bases de test
  réutilisent les aides de la persistance (même préfixe, création bornée, nettoyage), liées comme sources.

## Conséquences

- Les points d'entrée ne contiennent aucune règle et ne voient aucun type du domaine : la règle d'architecture reste
  vraie sans exception.
- Une écriture dont la 1.x rendait une vue (fiche, détail du projet) enchaîne la lecture correspondante : deux
  transactions, mais la visibilité selon le rôle reste celle de la lecture.
- Les routes du `.pmap` et du paquet `.topogenia` (export, import, aperçu, duplication) viennent avec leurs formats en
  phase 5 ; la santé avec les modes (P4-T05, P4-T06) ; l'audit en P4-T07.

## Vérification

`tests/Topogenia.Host.Tests` (règles communes, une classe par fonctionnalité, deux bases) ;
`DependencyRulesTests.Http_entry_points_depend_on_handlers_only`, `ExtraRulesTests.Http_entry_points_are_declared_only_in_the_endpoints_folder`.

## Révision (25/10/2026) : champ de mauvais type

Constat Fidélité M3 de la porte P4-T10 (round 1). La 1.x lisait chaque champ d'un PATCH à part (`CorpsPatch.Lire`,
`Validation.cs` 87-95) et refusait un champ de mauvais type par `VALEUR_INVALIDE` avec son nom ; elle liait tout autre
corps d'un bloc et le refusait par `REQUETE_INVALIDE` (`ErreurMetier.cs`, `BadHttpRequestException or JsonException`),
champ de mauvais type compris. La 2.0 fait de même : `ErrorMiddleware.PatchFieldOf` ne nomme le champ que pour un PATCH ;
un POST ou un PUT de mauvaise forme reste `REQUETE_INVALIDE`, un corps qui n'est pas du JSON aussi. Tests :
`Errors.JsonFieldErrorsTests`, `Api.CommonRulesTests.A_body_that_is_not_json_or_of_the_wrong_shape_is_an_invalid_request`,
`Ported.SheetsAndDocumentsPortedTests` (procédure `[null]`).

## Révision (25/10/2026, porte P4 round 3) : aucun fichier du pipeline exempté

La règle des routes (`ExtraRulesTests.Http_entry_points_are_declared_only_in_the_endpoints_folder`, détecteur
`RouteDetectors` sur symboles résolus) n'exempte plus aucun fichier hors de `Host/Endpoints/`. Le round 3 a montré que
l'exemption de `Composition/HostPipeline` (préfixe sans extension) laissait passer une route posée dans ce fichier ou
dans un fichier nommé d'après lui. Le pipeline passe la main aux routes par `MapApi`, méthode de l'espace de noms
`Topogenia.Host.Endpoints` : un appel à une méthode de cet espace de noms ne déclare aucune route, où qu'il soit, les
routes elles-mêmes étant contrôlées dans `Endpoints/` ; limite connue (round 4) : une méthode de cet espace de noms qui prendrait une adresse et un délégué pour les passer à `Map*` relaierait une route posée ailleurs ; aucune ne le fait (les méthodes d'`Endpoints/` ne prennent que le constructeur de routes), une règle qui l'interdit est au registre `a-peaufiner.md` (F-10). Un intergiciel ajouté par `Use` ou `UseMiddleware` n'est pas un
point d'entrée, même quand il répond à une adresse (le lancement du mode Poste, `GET /launch`, spec 5.2) : c'est une
étape du pipeline, ordonnée dans `Composition/HostPipeline`. Tests :
`ExtraRulesTests.A_route_in_the_file_of_the_pipeline_or_in_a_file_named_after_it_is_misplaced`,
`ExtraRulesTests.A_call_to_a_method_of_the_endpoints_namespace_is_no_misplaced_route` ; deux mutations (exemption remise,
exception de l'espace de noms retirée) tuées.
