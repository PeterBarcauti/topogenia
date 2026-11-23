<!-- 111-accueil-et-mise-a-jour.md -->
<!-- Home and update (3.4, lot 3): starts that launch (sample, gallery, blank map, describe, open), a code proposed from the name, knowing a version exists without any outgoing call (link opened by the browser, age of the version, operating switch), a narrow exception to the rule on addresses -->

# 111 - Accueil et mise à jour

Statut : acceptée le 21/11/2026 (3.4, lot 3). S'appuie sur les ADR 013 (nom du produit écrit une seule fois), 014
(aucun appel réseau sortant), 044 (hôte du mode Poste), 046 (en-têtes), 069 (projet d'exemple), 088 (paquet du Store),
098 (table des fournisseurs, seules adresses écrites du code livré) et 110 (recherche : le modèle d'une route lue sans
journal, le corps d'un `POST`). Demande du porteur du projet : qu'un premier lancement mène en deux clics à une carte
complète, et savoir qu'une version plus récente existe sans que l'application appelle internet.

## Contexte

- Sans projet, « Décrire un processus » est grisé pour l'administrateur (`features\home\HomeScreen.tsx`) ; le projet
  d'exemple n'est proposé que sur une liste vide, en mode Poste. Son import prend le code du paquet (CONGES ou LEAVE) :
  un second exemple est refusé (409), et sa carte ne s'ouvre pas.
- « Nouveau projet » demande le code d'abord, saisi à la main ; aucun code n'est proposé nulle part.
- Aucune date de version n'existe : `Directory.Build.props` ne porte que `<Version>`, suivi par `web\package.json`
  (`QualityRulesTests`), et la construction est déterministe (aucune heure n'entre dans les assemblages).
- Rien ne dit à la personne qu'une version existe. L'application n'appelle jamais internet (ADR 014) : la règle de
  sécurité est `connect-src 'self'` (`src\Topogenia.Host\Security\SecurityHeaders.cs`), et les essais
  `SourceRulesTests.Shipped_code_calls_no_external_address` (aucune adresse extérieure dans le C# livré ni dans
  `web\src`, sauf la table des fournisseurs de l'ADR 098) et `The_interface_writes_the_product_name_only_in_its_translation_files`
  (le nom du produit, donc `topogenia.com`, n'est pas écrit dans `web\src`) le tiennent.
- `CommonHeadersTests` compare l'en-tête à `SecurityHeaders.Policy` et vérifie quatre directives, **pas `connect-src`** :
  un essai manque.
- La règle d'un code libre existe dans le cœur fermé (`ProjectCodeRules.TrashCode`) et est déjà appelée par l'import
  (`PackageImportReplies.TrashCodeAsync`, sur `IProjectReader.CodesStartingWithAsync`, codes vivants et à la corbeille).

## Décision

1. **Aucune requête sortante.** L'application n'interroge ni le site, ni un flux, ni le Store. « Rechercher une mise à
   jour » ouvre une adresse **dans le navigateur**, sur le geste de la personne, par
   `window.open(adresse, '_blank', 'noopener,noreferrer')`, comme l'aide en ligne. Une navigation du navigateur n'est pas
   une requête de la page : `connect-src` ne régit que `fetch`, XHR, WebSocket, EventSource et `sendBeacon`, et il
   **reste `'self'`, inchangé** ; `SecurityHeaders.cs` n'est pas touché. L'onglet ouvert est séparé de l'application
   (`noopener,noreferrer`, et `Cross-Origin-Opener-Policy: same-origin` déjà posé) ; `Referrer-Policy: no-referrer` : le
   site ne reçoit ni l'adresse locale ni le port. La version installée voyage dans le **fragment** de l'adresse
   (`#v=3.4.0&d=portable`, `d` valant `portable` ou `server`), que le navigateur n'envoie jamais au serveur (RFC 3986,
   section 3.5) : le site ne voit qu'une visite ordinaire, et c'est la page des versions, dans le navigateur, qui lit
   `location.hash` et compare à sa propre liste. Adresses ouvertes (le lot 6 crée les pages) :
   page des versions `https://topogenia.com/versions/` (français, `fr` et `fr-*`) ou `https://topogenia.com/en/versions/`
   (toute autre langue, règle de l'aide reprise de `help.ts`) ; galerie `https://topogenia.com/modeles/` ou
   `https://topogenia.com/en/templates/` ; fiche du Store `https://apps.microsoft.com/detail/9NZ56VV4N6QR`, la même en
   toute langue.

2. **Les deux adresses de base sont écrites dans `ProductIdentity.cs`, donnée par le profil ; exception nommée de la
   règle des adresses.** `src\Topogenia.Application\ProductIdentity.cs`, le seul fichier où le nom du produit est permis
   (ADR 013), gagne `Site = "https://topogenia.com"` et `StorePage = "https://apps.microsoft.com/detail/9NZ56VV4N6QR"`.
   L'hôte les donne à l'interface dans le profil (`links`, point 3) ; rien d'autre n'écrit d'adresse du site, ni dans
   l'hôte ni dans l'interface, qui bâtit les chemins (`versions/`, `modeles/`) sur la base reçue.
   **L'exception, décrite exactement** (arbitrage du chef d'orchestre du 21/11/2026, étroite, à porter à la validation du
   lot) : `Shipped_code_calls_no_external_address` admet, sous une constante `SiteFile`, **un seul fichier**,
   `ProductIdentity.cs`, et dans ce fichier **deux hôtes exactement**, `topogenia.com` et `apps.microsoft.com`, **en
   HTTPS seulement** (`http://` est refusé), comme la table des fournisseurs de l'ADR 098 est admise par sa constante.
   Le document `docs\securite.md` liste ces deux hôtes (en `code`) et dit en une phrase que les liens s'ouvrent dans le
   navigateur. Un essai d'architecture, `The_addresses_of_the_site_are_written_in_the_identity_of_the_product_alone_and_listed_by_the_security_document`,
   tient l'exception : les hôtes trouvés dans `ProductIdentity.cs` sont ceux de la liste de `securite.md`, deux, en HTTPS ;
   et **un exemple fautif** la prouve : la même adresse écrite dans un autre fichier (`ProfileApi.cs`) fait tomber la
   règle des adresses. Toute autre adresse extérieure du code livré reste refusée, comme avant. Les essais de l'interface
   n'ont pour adresses que `http://localhost/…` (profils de test).
   **Un lien ouvert dans le navigateur n'est pas un appel de l'application** : ces deux adresses ne sont jamais
   passées à `fetch` ni à aucun type qui ouvre une connexion (D14 : `ShippedCodeDetectors`), et `connect-src 'self'`
   ne change pas. L'essai `CommonHeadersTests.The_page_reaches_the_application_only_connect_src_self` l'écrit pour la
   première fois : la directive vaut `'self'` et aucun hôte du site n'y figure.

3. **Réglage d'exploitation `SiteLinks`, `links` nul.** Booléen, vrai par défaut, dans `ProductOptions`, lu au démarrage
   comme les autres réglages. Poste : clé admise de `settings.json` (vrai ou faux JSON seulement, toute autre valeur arrête
   le démarrage, `PARAMETRES_INVALIDES` et la propriété). Serveur : `appsettings.Production.json`, une ligne commentée
   dans l'exemple livré. Le profil (`ProfileView`) gagne `links` : `{ site, store_page }`, **nul quand `SiteLinks` est
   faux**. L'interface ne montre alors ni « Rechercher une mise à jour », ni rappel, ni « Galerie de modèles ». Une seule
   décision, prise par l'hôte, qui seul lit le réglage ; le reste ne change pas, dans les deux modes.

4. **La version du Store.** Le profil porte aussi `store` (booléen) : le Poste tourne depuis le paquet du Store
   (`DesktopPackage.FamilyName` non nul, ADR 088) ; faux en Serveur. La fonction pure `ProfileLinks.Of(options, familyName)`
   le décide. Avec `store` vrai, « Rechercher une mise à jour » ouvre la fiche du Store (par l'identifiant du produit
   seul : le nom de famille du paquet porte le pseudonyme de l'éditeur, il n'est jamais écrit ni envoyé), et **aucun
   rappel n'est montré** : le Store met à jour lui-même. Où : menu Aide, dernière ligne, et menu du compte, avant
   « Quitter » ; la personne du poste en Poste, l'administrateur seul en Serveur (un autre rôle ne peut rien installer) ;
   module unique `app\siteLinks.ts` (`updateAddress`, `galleryAddress`, `openSite`).

5. **La date de la version et le rappel.** `"releaseDate": "AAAA-MM-JJ"` dans `web\package.json`, à côté de `version`,
   lu par `vite.config.ts` et écrit dans le paquet sous `__APP_RELEASE_DATE__` : un jour seul, ni heure ni fuseau, écrit à
   la main, jamais calculé par la construction (aucune heure de construction n'entre dans un fichier) ; récrit au jour de
   la publication au lot 8c. Le rappel (`features\home\updateReminder.ts`, règle pure) est montré à l'administrateur
   quand `links` n'est pas nul, `store` faux, et que la version a au moins **90 jours** (`UPDATE_REMINDER_DAYS`,
   en jours du calendrier local) ; le rappel ne connaît que l'âge de la version, pas l'existence d'une plus récente.
   Une ligne discrète de l'accueil, sans `role="alert"`. Fermé, il ne revient qu'après 90 jours : **le jour de fermeture
   est gardé par le navigateur**, clé `app.updateReminderClosed` (`AAAA-MM-JJ`), dans `features\map\state\preferences.ts`,
   le seul module qui touche ce stockage ; l'essai B-7-05 (« nothing else is stored ») est étendu à cette clé, nommée ici.
   Stockage refusé : la ligne se ferme pour la visite. Un jour de fermeture illisible ou à venir compte comme aucun.
   Rien n'est écrit dans la base : aucune migration.

6. **Les départs et l'exemple.** Un menu des départs (`features\home\StartMenu.tsx`, liste pure `startItems.ts`) : projet
   d'exemple (Poste), galerie de modèles (si `links`), carte vide, décrire avec l'IA, ouvrir un fichier. Il est ouvert
   par le bouton principal quand l'administrateur n'a aucun projet, et par « Nouveau projet », devenu bouton à menu avec
   « Carte vide… » en tête (comme « Nouveau » d'Office et de Visio). Un non-administrateur (Serveur) ne voit rien changer :
   bouton principal grisé. Le projet d'exemple reste proposé quand des projets existent (Poste seul, 404 en Serveur) : s'il
   n'est pas libre, `SampleProjectApi` prend le premier code libre de la règle de la corbeille (CONGES, puis CONGEA,
   CONGEB…) par `FreeProjectCodeQuery`, appelle `TrashCode` telle quelle, et la carte de l'exemple s'ouvre à sa
   création. Le refus 409 du second exemple devient un code libre : comportement changé par la décision, pas un essai
   affaibli. La ligne « Galerie de modèles » ouvre le site dans le navigateur (point 1) ; le lot 6 n'ajoute que
   l'ouverture d'un paquet.

7. **La proposition du code.** Route `POST /api/projects/code-proposal` (administrateur), corps `{ "name": "…" }`, réponse
   `{ "code": "…" }` ou `{ "code": null }` ; lecture seule, rien n'est écrit ; le nom reste dans le corps, jamais dans
   l'adresse ni dans le journal (comme la recherche de l'ADR 110) ; corps de 4 Ko au plus, nom de plus de 200 caractères
   refusé (`VALEUR_INVALIDE`, champ `name`) ; 403 `ACCES_REFUSE` pour qui n'est pas administrateur, 401 anonyme ; placée
   avant les routes `{id:guid}`. La règle vit dans `Application`
   (`Projects\ProjectCodeProposal.FromName`, pure ; `ProposeProjectCodeQuery` et `FreeProjectCodeQuery`,
   `[RequiresAdmin]`) : le nom replié par `SearchText.Fold` (ADR 110), mis en majuscules ; les mots sont les suites de A
   à Z (chiffres et autres écritures écartés : un code ne porte que des lettres) ; les mots de moins de trois lettres
   sont écartés **sauf s'il ne reste aucun autre mot**, qui servent alors (« La RH » propose « LARH », arbitrage 8 du
   lot) ; un mot : ses six premières lettres ; deux mots : trois lettres de chacun ; trois à
   six mots : leurs initiales ; plus de six : les initiales des six premiers ; aucun mot (« 2026 ») ou moins de trois
   lettres au bout de la règle (« RH ») : pas de proposition, jamais un code que la création refuserait (la personne tape
   son code) ; puis le premier code libre par
   `ProjectCodeRules.TrashCode` (vivants et corbeille), **appelée, jamais réécrite**. Le cœur n'est pas modifié. Côté
   interface, « Nouveau projet » demande le nom d'abord, puis le code, qui suit le nom 300 ms après la dernière frappe
   (`useCodeProposal`, requête précédente annulée) tant que la personne n'a rien tapé dans le champ du code. Un code
   vidé reste vide : la proposition ne revient que lorsque le nom change ensuite, pour ce nouveau nom ; un code tapé n'est
   jamais écrasé. Puis « Plus d'options » replié ; l'hôte reste juge à la création (409 comme avant). La table de cas de la proposition
   est tenue par l'essai de l'application (« Traitement des factures fournisseurs » donne TDFF).

## Écarté

- **Interroger le site ou un flux pour connaître la dernière version** : un appel sortant, contraire à l'ADR 014 et à
  `connect-src 'self'`. Le flux Atom des versions existe pour la personne qui le lit hors de l'application.
- **L'API de mise à jour du Store (`StoreContext`)** : un appel sortant, et réservé à la version du Store.
- **Télécharger ou installer** : l'application ne le fait jamais ; la personne télécharge depuis le site.
- **Garder la fermeture du rappel en base** : une migration pour un jour de confort ; le navigateur le garde, comme les
  préférences.
- **Une règle d'entreprise dans le registre de Windows** (le geste des DSI pour Firefox ou VS Code) : un second canal de
  réglage ; le fichier de réglages suffit, inscrit dans `docs\a-peaufiner.md` (non publié).
- **La proposition du code dans l'interface** : la règle du code libre serait écrite deux fois ; elle vit dans
  l'application, qui appelle celle du cœur.
- **La règle du code proposé dans le cœur fermé, à côté de `ProjectCodeRules`** : le cœur ne se modifie pas dans ce lot ;
  le rapprochement est noté dans `docs\a-peaufiner.md` (non publié).
- **La date de la version tirée de l'horloge de la construction** : elle romprait la construction déterministe et
  écrirait une heure dans les fichiers.
- **Un second fichier ou une liste d'hôtes plus large dans l'exception des adresses** : l'exception reste un fichier,
  deux hôtes, HTTPS.
- **Le nom de famille du paquet du Store dans l'adresse** : il porte le pseudonyme de l'éditeur.

## Conséquences

- Une exception étroite à une règle d'essai (`Shipped_code_calls_no_external_address`), bâtie sur le modèle de la table
  des fournisseurs de l'ADR 098 : à porter à la validation du lot.
- Aucun fichier du cœur fermé, aucune migration, aucune dépendance ; `connect-src 'self'` et `SecurityHeaders.cs`
  inchangés.
- Le profil (`ProfileView`) gagne `store` et `links` ; une route de plus (`code-proposal`, administrateur) : une ligne
  dans la matrice des droits et dans la table des routes, le contrat OpenAPI et `schema.ts` réécrits.
- Un réglage de plus (`SiteLinks`) dans `settings.json` (clé admise) et dans l'exemple du mode Serveur ; les documents
  d'exploitation le décrivent.
- Le rappel ne sait pas si une version plus récente existe : il invite à regarder, rien de plus. Le Store n'en montre
  jamais.
- Les pages du site que les liens ouvrent (versions, galerie, flux) sont écrites au lot 6 ; d'ici là l'adresse seule compte.
- La date de la version est un jour écrit à la main : à récrire à chaque publication (ligne de la procédure du lot 8).

## Vérification

- Hôte : `ProfileLinksRoutesTests` (liens donnés par défaut dans les deux modes ; `SiteLinks` faux, aucun lien ; `store`
  seulement en Poste depuis son paquet), `SiteLinksSettingsTests` (vrai ou faux, toute autre valeur refusée),
  `ProjectCodeProposalRoutesTests` (deux bases, corbeille comprise, nom absent du journal, droits), `SampleProjectRoutesTests`
  (second exemple au code libre, ouvert comme le premier), `CommonHeadersTests` (`connect-src 'self'`), `RightsMatrixTests`,
  `RouteTableTests`, `ApiContractTests` à jour. Application : `ProjectCodeProposalTests` (table de cas), `ProjectCodeQueriesTests`.
- Architecture : l'essai des adresses du site (liste de `securite.md`, deux hôtes, HTTPS) et son exemple fautif ; les règles
  `Shipped_code_calls_no_external_address` et du nom du produit restent vertes.
- Interface : `vite-config.test.ts` (jour de version sans heure), `siteLinks.test.ts` (langue, fragment, Store, aucun lien
  sans `links`, onglet séparé, aucun appel du réseau), `outgoingCalls.test.ts` (seuls `api\request.ts`,
  `features\map\export\capture.ts` et `features\map\export\pdfFonts.ts` appellent le réseau, à une adresse relative ; seuls `app\help.ts` et `app\siteLinks.ts`
  ouvrent un onglet), `updateReminder.test.ts`, `HomeUpdate.test.tsx`, `preferences.test.ts` (B-7-05 étendu),
  `startItems.test.ts`, `HomeStart.test.tsx`, `useCodeProposal.test.ts`, `ProjectScreens.test.tsx`.
- Parcours : `web\e2e\home-update-34.spec.ts` et `sample-project.spec.ts`, en mode Poste et en mode Serveur : chaque requête
  de la page va à l'hôte et la règle n'en refuse aucune ; contrôle positif : une requête de la page vers le site est refusée
  et vue.
- Témoins de mutation vus rouges puis remis : un `fetch` ajouté dans `openSite` fait tomber les deux essais de
  l'interface sur l'absence d'appel sortant ; `links` donné quel que soit `SiteLinks` fait tomber l'essai du réglage ;
  une adresse du site copiée dans `ProfileApi.cs` fait tomber la règle des adresses ; la condition d'administrateur retirée
  du rappel fait tomber l'essai du non-administrateur ; `[RequiresAdmin]` retiré de la proposition fait tomber l'essai du refus.
