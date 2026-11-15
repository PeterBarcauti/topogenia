<!-- 056-client-d-api-genere.md -->
<!-- Client TypeScript de l'API généré depuis le contrat OpenAPI de l'hôte : chaîne de génération, erreurs typées, version et en-têtes -->

# 056 - Client d'API généré depuis le contrat de l'hôte

Statut : acceptée (date 27/10/2026)

## Contexte

D09 : le contrat d'API est décrit en OpenAPI, généré par l'hôte (`/openapi/v1.json`, phase 4), et les types et le client
TypeScript en sont générés, « par exemple `openapi-typescript` et un petit client maison » ; plus aucun type d'API écrit
à la main (S-D09-02). L'annexe B 0 fixe ce que le client 1.x faisait (`api/client.ts`) : authentification portée par le
navigateur, en-tête anti-CSRF sur les écritures (`X-Requested-With`, AR-13), `If-Match` avec la version, `Accept-Language`,
erreurs normalisées en `ErreurApi(statut, code, message, details)` avec les codes du client `RESEAU`,
`REPONSE_INVALIDE` et `HTTP_xxx` (B-0-14 à B-0-21). D06 rend `If-Match` obligatoire en mode Serveur (428
`VERSION_REQUISE`). Le contrat ne décrivait pas le corps des refus, qu'aucune route ne déclare.

## Décision

### 1. La chaîne de génération

1. **Le contrat est figé dans le dépôt** : `web/src/api/generated/openapi.json` est le document que sert l'hôte, écrit
   indenté, sans `servers` (l'adresse de la requête) ni `info` (le titre, fait du nom du produit : un renommage reste
   une ligne, D13). `ApiContractTests` (tests de l'hôte) compare le document servi au
   fichier, **sur les deux bases et donc dans les deux modes** : le contrat est le même en mode Poste et en mode Serveur.
   Pour le réécrire après un changement des routes ou des corps : `CONTRACT_WRITE=1` sur cette classe de tests (même
   usage que `SAMPLES_WRITE` pour l'exemple du paquet), puis `npm run api`.
2. **Les fichiers du client en sont générés** par `npm run api` (`web/scripts/generate-api.mjs`, logique dans
   `scripts/api-generation.ts`) : `schema.ts` (types de `openapi-typescript` 7.13.0, MIT) et `exchanges.ts` (la table
   des opérations dont la réponse ou le corps n'est pas du JSON : la copie `.pmap` en texte, l'export en archive, les
   quatre imports en octets). Un test vitest régénère les deux fichiers depuis `openapi.json` et les compare à ceux du
   dépôt : aucun ne peut dériver du contrat, et le contrat ne peut pas dériver de l'hôte.
3. **Aucun type d'API à la main** : un test refuse toute déclaration (`interface`, `type`, `class`, `enum`) qui porte le
   nom d'un schéma du contrat ailleurs que dans `generated/`, sauf l'alias du type généré.
4. Le dossier `web/src/api/generated/` est déjà traité comme généré par les tests d'architecture et ignoré par ESLint ;
   `tsc` le vérifie (les types générés compilent sous TypeScript strict).

### 2. Le corps des erreurs dans le contrat

`ErrorSchemaTransformer` (hôte) ajoute aux composants le schéma `ErrorBody` (`{code, message, details}`, les trois
requis), dont le code est l'un des codes du catalogue des messages, qui contient chaque code qu'un refus peut nommer (un
test vérifie que chaque code a un message dans chaque langue, spec 3.6). Le client tire de ce schéma le type des codes
de l'hôte (`ServerErrorCode`) ; ses trois codes à lui (`RESEAU`, `REPONSE_INVALIDE`, `HTTP_<statut>`) s'y ajoutent.
Les codes restent en français, comme ceux de l'hôte (annexe A 3).

### 3. Le client maison

- `api/request.ts` : le seul échange (`fetch`) : `credentials: 'same-origin'` (Windows en mode Serveur, cookie de session
  en mode Poste, B-0-14) ; `X-Requested-With: XMLHttpRequest` sur toute méthode autre que GET ; `If-Match: "<version>"`
  quand une version est donnée ; `Accept-Language` dès que la langue de l'interface est connue ; réponse lue selon la
  table générée (JSON par défaut, texte, fichier) ; un refus donne son code, son message et ses détails, ou
  `HTTP_<statut>` quand son corps n'est pas un refus de l'API ; pas de réponse donne `RESEAU` ; un succès qui n'est pas
  du JSON donne `REPONSE_INVALIDE` (statut 0 pour les deux, comme en 1.x) ; une annulation passe telle quelle.
- `api/client.ts` : la façade typée `api.get/post/put/patch/delete(route, options)` : la route est l'une de celles du
  contrat pour la méthode ; les valeurs du chemin, de la requête et le corps sont ceux de l'opération ; la réponse est
  son schéma (texte pour la copie `.pmap`, `Blob` pour l'export, rien pour un 204).
- **Intercepteur If-Match** : toute opération du contrat qui lit `If-Match` exige `version` dans ses options (le type ne
  compile pas sans elle), et le client la pose en en-tête. Le client l'envoie dans les deux modes, bien que le mode Poste
  l'accepte absente (D06) : une interface qui marche en mode Poste marche aussi en mode Serveur.

### 4. Dépendance et version de TypeScript

`openapi-typescript` 7.13.0 (février 2026, MIT) annonce TypeScript `^5.x` en dépendance paire ; le projet est en
TypeScript 6.0.3, dont l'API du compilateur utilisée par le générateur (fabrique et impression d'arbres) n'a pas changé.
`package.json` fixe la paire sur la version du projet (`overrides`) plutôt que `--legacy-peer-deps`, qui relâcherait
toute la résolution. Le test de génération prouve que la sortie est correcte. Ses dépendances transitives sont de
développement seulement (jamais livrées) et sous licences permissives (`THIRD-PARTY-NOTICES.md` régénéré).

## Conséquences

- Un changement d'une route ou d'un corps côté hôte casse `ApiContractTests` tant que le contrat n'est pas réécrit, puis
  le test de génération tant que `npm run api` n'est pas relancé, puis `tsc` partout où l'interface utilise l'ancien
  type : la dérive entre C# et TypeScript est arrêtée à la construction.
- Les parcours e2e (P6-T10) éprouveront le client contre l'hôte réel ; les tests unitaires remplacent `fetch`.
- B-0-09 : le proxy de développement de Vite garde une seule connexion (agent `keepAlive`, `maxSockets: 1`), pour
  l'échange NTLM du mode Serveur, comme en 1.x.

## Complément du 28/10/2026 (P6-T03) : énumérations et listes du contrat

En écrivant les écrans de projet, 24 schémas du contrat se sont révélés vides, que le générateur typait `unknown` : les
17 énumérations, écrites par leurs codes (`CodeEnumConverterFactory`), et les 7 listes des corps de l'API, lues élément
par élément (`NonNullItemsConverterFactory`), deux convertisseurs que le générateur d'ASP.NET Core ne sait pas lire.
L'interface n'aurait pu typer ni les rôles ni la liste des membres sans écrire de type à la main, ce que D09 interdit.
Deux transformateurs de schéma de l'hôte les décrivent (`Json/CodeEnumSchemaTransformer` : chaîne parmi les codes de
`Codes.All<T>`, dans leur ordre ; `Json/NonNullItemsSchemaTransformer` : tableau du schéma de son élément), et
`ApiContractTests.Every_schema_of_the_contract_has_a_shape_the_enumerations_their_codes_and_the_lists_their_items`
refuse désormais tout schéma vide, sur les deux bases. Contrat et types régénérés (`MemberRole` vaut
`"reader" | "contributor" | "facilitator"`, `ListOfMemberBody` vaut `MemberBody[]`).

