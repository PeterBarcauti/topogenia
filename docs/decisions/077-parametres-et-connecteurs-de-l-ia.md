<!-- 077-parametres-et-connecteurs-de-l-ia.md -->
<!-- The settings of the assistant (3.0, lot 1): a list of connectors, the key typed by the administrator and kept protected, Bedrock without any key, the test of the connection, the bounded exception to D14 -->

# 077 - Paramètres et connecteurs de l'IA

Statut : acceptée (date 30/10/2026). Révise l'ADR 014 (D14) par une exception bornée. Spécification :
`TOPOGENIA_3.0_SPECIFICATION_IA.md`, sections 4 à 8 et 12 (décisions du porteur du projet du 29/10/2026).

## Contexte

La 3.0 intègre l'IA (volet de la carte, du récit au schéma). Son premier lot pose le socle : où l'administrateur choisit
l'IA et la manière de l'atteindre, où vit le secret, comment vérifier la connexion avant tout usage. Le porteur du projet
a décidé (29/10/2026) : une **liste de connecteurs** dans le menu des paramètres, en commençant par Claude sur AWS
Bedrock (la voie de l'entreprise, sous contrat AWS) et Claude par l'API Anthropic (la voie des essais, sur ce poste) ;
l'accord de la DSI se décide au département DX/IT, qui distribue les clés. Jusqu'ici, aucun code livré n'appelait
l'extérieur (D14).

## Décision

1. **Une liste fermée de connecteurs** (`AssistantConnector`) : `none` (par défaut : rien ne sort), `copy_paste` (la
   consigne se copie dans l'IA de son choix, rien ne sort de l'application), `anthropic` et `bedrock`. La route de
   lecture rend la liste (`connectors`) : le dialogue la montre sans la connaître, et un connecteur ajouté n'y change
   rien.
2. **Les paramètres de l'installation dans la base** : la table `assistant_settings`, une seule ligne, enregistrement de
   la persistance hors du cœur fermé (comme `schema_history`), avec sa version pour jeton de concurrence et ses CHECK
   (`docs/modele-donnees.md` § 6.25) ; migrations des deux fournisseurs, chacune retirant sa ligne d'historique à la
   descente (ADR 027). Connecteur, modèle (claude-opus-5-5 par défaut pour Anthropic), région et profil AWS, plafond
   d'une réponse (256 à 128 000 jetons, 16 000 par défaut, 64 000 depuis le 30/10/2026 : la traduction d'un processus
   entier, ADR 083), classifications qui peuvent partir (Interne seule par défaut, spec 3.0 section 6).
3. **La clé de l'API Anthropic saisie dans le dialogue, gardée chiffrée** : protégée par la protection des données
   d'ASP.NET Core (port `ISecretProtector`, but propre au secret), sous le nom du produit dans les deux modes ; en mode
   Poste, les clés de protection sont dans le dossier du produit, chiffrées pour le compte Windows (DPAPI). La clé
   n'est jamais rendue : la vue donne seulement `has_secret` et ses quatre derniers caractères ; le champ s'écrit
   seulement (vide garde la clé, une case la supprime, un texte la remplace) ; caractères ASCII imprimables seulement
   (elle part dans un en-tête HTTP). Choisir un autre connecteur supprime la clé. Une clé devenue illisible (clés de
   protection perdues) est dite (`key_unreadable`) et se saisit de nouveau. La spécification la voulait en variable
   d'environnement : le porteur du projet veut la saisir dans le dialogue.
4. **Bedrock sans aucune clé** : un profil AWS nommé du poste (SSO, `aws configure sso`), lu par le SDK AWS
   (`CredentialProfileStoreChain`), ou, sans profil, l'identité AWS de l'hôte par la chaîne par défaut du SDK (rôle d'un
   serveur). Un profil nommé est lu seul : des variables `AWS_ACCESS_KEY_ID` laissées par un lanceur ne comptent pas ;
   sans profil, la chaîne du SDK les lirait, et le guide du serveur demande de n'en poser aucune. Une session expirée est dite
   (« lancer `aws sso login --profile …` »), jamais contournée. SDK : `AWSSDK.BedrockRuntime` (API `Converse`), avec
   `AWSSDK.SSO` et `AWSSDK.SSOOIDC` ; le modèle est l'identifiant d'un profil d'inférence (`global.anthropic.…`).
5. **L'API Anthropic par son SDK officiel** (`Anthropic` 13.3.0), un client par appel, sans nouvelle tentative pour le
   test.
6. **Routes de l'administrateur** : `GET` et `PUT /api/admin/assistant` (le `PUT` avec `If-Match`), `POST
   /api/admin/assistant/test`. Le test envoie le formulaire tel qu'il est saisi, sans l'enregistrer (la clé saisie, ou
   la clé gardée) : « Réponds seulement par le mot OK. », 1 024 jetons au plus (effort `low` par l'API Anthropic), 90 s
   au plus ; il rend le
   modèle qui a répondu, la durée et les jetons, ou un code d'échec (`no_connector`, `key_missing`, `key_unreadable`,
   `refused`, `profile_missing`, `session_expired`, `model_unknown`, `limited`, `unreachable`, `other`) et le détail du
   fournisseur, 300 caractères au plus, sans secret. Il n'écrit que l'audit : pas d'`If-Match`.
7. **Audit** : `assistant.settings_changed` (le connecteur et les noms des champs changés, jamais leurs valeurs) et
   `assistant.connection_tested` (connecteur, modèle, succès ou code d'échec, durée, jetons) ; la clé n'y figure jamais.
8. **Exception bornée à D14** : seul le projet `src/Topogenia.Assistant/` (les connecteurs) tient un client de modèle ou
   un client AWS, et lui seul est exempté de la règle des appels sortants
   (`SourceRulesTests.Only_the_connectors_of_the_assistant_call_a_model`, qui vérifie aussi que les deux connecteurs
   sont bien lus). Rien ne sort tant que l'administrateur n'a pas choisi un connecteur qui appelle un modèle et que
   personne ne le demande (au lot 1 : « Tester la connexion »). L'appel part de l'hôte, jamais du navigateur : la CSP
   ne change pas, et l'IT n'a qu'une adresse à ouvrir au proxy (`api.anthropic.com`, ou Bedrock Runtime de la région).
9. **Aucun test n'appelle un vrai service** : les hôtes des tests et le lanceur des parcours remplacent les connecteurs
   (`TestConnector`, `JourneyConnector`) ; le connecteur Anthropic est éprouvé contre un faux gestionnaire HTTP. Le
   premier appel réel est fait par le porteur du projet, avec sa clé, sur ce poste.

## Options écartées

- **Le secret dans une variable d'environnement ou un fichier de réglages** : une manipulation hors de l'application, et
  un secret en clair sur le disque ; la protection des données chiffre pour le poste ou le serveur.
- **`Anthropic.Bedrock`** (client Bedrock du SDK Anthropic) : ses identifiants n'ont pas de jeton de session, donc pas
  d'identifiants temporaires d'un profil SSO, la voie de l'entreprise.
- **Les paramètres dans le cœur fermé** : ils ne sont pas une règle du métier ; les connecteurs, sous Apache-2.0,
  restent partageables (ADR 048).
- **Une clé AWS saisie dans le dialogue** : la consigne de l'entreprise est de n'enregistrer aucune clé AWS ; le profil
  SSO ou l'identité de l'hôte suffit.

## Conséquences

- Les guides d'exploitation nomment l'adresse sortante de chaque connecteur, le profil AWS et la session SSO en mode
  Poste, l'identité AWS du serveur, et l'emplacement des clés de protection en mode Serveur (le profil du compte du pool
  d'applications doit être chargé pour qu'elles survivent à un recyclage ; sinon une clé Anthropic saisie devient
  illisible, ce que le dialogue dit).
- Les lots 2 et 3 (le volet) passent par les mêmes paramètres : connecteur, modèle, plafond et classifications ; la
  documentation des formats `.pmap` et `.topogenia` accompagne le contexte envoyé (spec 3.0 sections 3.1 et 5).

## Tests

`AssistantRulesTests`, `AssistantRequestsTests` (Application) ; `AssistantRoutesTests`, `AnthropicConnectorTests`,
`RightsMatrixTests`, `IfMatchRequirementTests` (Hôte) ; `CheckConstraintTests`, `ColumnTests`, `RoundTripTests`,
`MigrationDescentTests` (Persistance) ; `SourceRulesTests.Only_the_connectors_of_the_assistant_call_a_model`
(Architecture) ; `AssistantSettings.test.tsx` (interface) ; `e2e/assistant-settings.spec.ts` (parcours, deux modes).
