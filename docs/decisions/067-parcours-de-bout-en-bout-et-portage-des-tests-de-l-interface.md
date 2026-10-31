<!-- 067-parcours-de-bout-en-bout-et-portage-des-tests-de-l-interface.md -->
<!-- Le banc des parcours de bout en bout contre le vrai hôte dans les deux modes, axe-core, le portage des 176 tests unitaires et des 49 parcours 1.x, les défauts trouvés (P6-T10) -->

# 067 - Parcours de bout en bout et portage des tests de l'interface

Statut : acceptée (date 28/10/2026)

## Contexte

P6-T10 (`plan.md`, phase 6) porte la spécification 11 pour l'interface : les 176 tests unitaires 1.x (S-11-02), les
tests des commandes, menus et raccourcis (S-11-03), les 49 parcours de bout en bout 1.x (annexe B 8.3, 69 lignes) et
les nouveaux (jeton de lancement, paquet `.topogenia`, annulation après rechargement, exports sous la CSP), axe-core sur
les écrans principaux (S-11-09), le banc lui-même (B-0-06 à B-0-08), et les 145 lignes que P6-T01 à P6-T09 avaient
laissées à « bout en bout » ou « axe-core : P6-T10 ». La 1.x jouait ses parcours contre Kestrel en NTLM sur la base
`Ariane_E2E`, recréée par `sqlcmd`, ses données posées par `fetch` depuis la page (lecture seule :
`43_Outil_Collaboratif/carto/web/e2e/`). La 2.0 a deux modes : le mode Poste ne donne son jeton de lancement qu'au
navigateur qu'il ouvre (spec 5.2, ADR 044), le mode Serveur refuse le HTTP (ADR 045).

## Décision

1. **Le banc, contre le vrai hôte** : `tests/Topogenia.EndToEnd.Host`, un exécutable de test (jamais livré, sans test à
   lui, dans la solution) construit l'application par `HostApplication.Build`, comme l'exécutable du produit, avec les
   fichiers de l'interface construite (`--webroot`), dans un dossier du passage vidé à chaque démarrage :
   - **mode Poste** (défaut) : SQLite dans le dossier, `127.0.0.1` et le port, aucun navigateur ouvert. Le lanceur lit le
     jeton dans la session de l'exécution, comme les tests de l'hôte (`DesktopRun`), et écrit l'adresse de lancement
     dans `launch-address.txt` du dossier ; la mise en place de Playwright l'ouvre dans Chromium (vrai échange, cookie
     `desktop_session`), garde l'état du navigateur, et chaque parcours le reprend. Le produit n'a aucune autre sortie
     pour le jeton ;
   - **mode Serveur** (`--mode server`, `E2E_MODE=server` côté Playwright) : la base `Topogenia_E2E` migrée d'abord par la
     commande du produit (`--migrate`, qui la crée), puis HTTPS sur `localhost` et le port avec un certificat fait pour
     le passage, gardé en mémoire et jamais installé (comme `ServerRun`), Negotiate, le compte qui lance les parcours
     administrateur. Chromium reçoit `--auth-server-allowlist=localhost` (comme la 1.x) et ignore le certificat du passage.
2. **Playwright** (`web/playwright.config.ts`, ADR 019) : 1.64.1 et le Chromium déjà installé, viewport 1366x768,
   `fr-FR`, un seul travailleur, 60 s par parcours ; `webServer` = `e2e/support/start-host.mjs` (construit puis lance le
   lanceur ; `E2E_NO_BUILD=1` pour des passages côte à côte, `E2E_PORT` pour leur port) ; `npm run e2e` construit
   l'interface puis joue les parcours. Les parcours sont vérifiés par `tsc` et ESLint comme le reste du web.
3. **Les données** : chaque parcours part d'une base vide (projets mis à la corbeille puis supprimés par l'API) et d'un
   profil en français, et pose ses données par la vraie API (`e2e/support/api.ts`) : en mode Poste par la requête de
   Playwright qui porte le cookie, en mode Serveur depuis une page du navigateur, seul à porter l'identité Windows. Aucun
   SQL. Une écriture déclare sa version quand la route lit `If-Match` (0 pour un élément jamais enregistré), ce que le
   mode Serveur exige.
4. **axe-core** (ADR 020) : `@axe-core/playwright` 4.13.0 (axe-core 4.13.0, MPL-2.0, développement), règles WCAG 2.0,
   2.1 et 2.2 aux niveaux A et AA, sur l'accueil et la corbeille, l'import d'un paquet, l'ouverture d'un `.pmap`, le
   nouveau projet, les membres, la carte, le menu d'une étape, la barre de commande, la fiche, le calque et le panneau
   RACI, la matrice, le menu Fichier, les traductions, une notification, la carte en anglais, le menu d'un couloir.
   L'analyse attend la fin des animations finies : un dialogue entre par un fondu de 150 à 200 ms (spec 9.5), et un
   contraste lu pendant ce fondu est faux.
5. **ajv** 8.20.0 (MIT, développement) valide la copie `.pmap` téléchargée contre le schéma publié (JSON Schema 2020-12),
   à la place d'une dépendance indirecte.
6. **Écarts** :
   - la lecture seule n'est pas atteignable de bout en bout : le banc n'a qu'un compte, administrateur de tout projet
     (ADR 044, ADR 047). B-2.3-46 et la part « lecture seule » de B-8.3-36 restent prouvées au niveau de l'interface
     (`editing.test.tsx`), état `écart` ; la 1.x ne testait d'ailleurs que le mode Relier ;
   - l'installation Poste (S-11-04) demande l'exécutable publié : elle va à P7-T01 ;
   - le canevas 2.0 dessine tous ses éléments (ADR 066 § 2) : une étape hors de l'écran est vérifiée hors du viewport,
     non absente du DOM ;
   - les attentes fixes de la 1.x (`waitForTimeout`) sont remplacées par des attentes sur un état ;
   - les gestes changés par une décision suivent la décision (ruban en icônes, en-têtes de 184 px, `layout`,
     `step-label-input`, langue dans l'en-tête), écrits au tableau de portage.
7. **Défauts trouvés et corrigés** :
   - le pont invisible des « + » (`.quick-add-bridge`, 30 px autour de chaque étape) captait le pointeur en permanence
     (la 1.x ne l'affichait qu'au survol) : une flèche entre deux étapes voisines ne se cliquait plus, un rectangle
     partant près d'une étape la glissait, l'étiquette du cadre de remontée était couverte aux deux tiers. Il n'existe
     plus qu'au survol (`styles/map.css`, test `styles/map.test.ts`) ;
   - la fiche ouverte gardait Échap pour elle : une coupe n'était pas abandonnée. Le panneau se ferme et laisse la touche
     à la carte, qui abandonne la coupe, le mode Relier et le renommage, comme la 1.x en un seul appui (`SheetPanel`,
     test `sheetDock.test.tsx`) ;
   - les boutons radio du dialogue Traductions étaient trop proches (WCAG 2.5.8, taille de cible) : 12 px entre les
     options (`styles/pages.css`) ;
   - deux éléments portaient `data-testid="translations-close"` : le bouton du pied devient `translations-done` ;
   - la consigne pour l'IA (`docs/pmap/PROMPT_croquis_vers_pmap.md`) n'embarquait plus le schéma (1.x : oui) : copie
     exacte rétablie, gardée par `PmapDocumentationTests`.
8. **Écart relevé, traité à part** : AR-52 (police Inter intégrée au PDF) n'était pas appliqué, le PDF écrivait en
   Latin-1 (ADR 066 § 10). Corrigé en P6-T09b (révision de l'ADR 066).
9. **Portage des unitaires** : le relevé des 176 tests 1.x a trouvé 143 tests portés, 28 partiels, 2 manquants,
   3 sans objet ; les 30 lignes ont reçu leurs tests (vitest et xUnit). Tableau : `docs/couverture-p6/portage-interface.md`
   (173 portés, dont 11 côté hôte, 3 sans objet par décision écrite) ; parcours : `docs/couverture-p6/portage-parcours.md`
   (49 parcours 1.x portés, 21 nouveaux, 70 en tout).

## Conséquences

- Les parcours ne sont pas dans `dotnet test` : ils se jouent par `npm run e2e` (ou `npx playwright test` avec
  l'interface construite), environ 4 min en mode Poste et 5 min en mode Serveur ; l'intégration continue les ajoutera
  (ADR 019, phase 7).
- Le lanceur ajoute un projet à la solution, sans test ; `dotnet sln add` réécrit la solution avec un BOM et des CRLF,
  à remettre en UTF-8 sans BOM et en LF (règle d'architecture).
- Une préparation de parcours qui écrit sans version passe en mode Poste et échoue en mode Serveur (428) : les deux modes
  se jouent.

## Vérification

- `web/e2e/` : 70 parcours, verts en mode Poste et en mode Serveur ; chacun des parcours portés a passé trois fois de
  suite. `web/e2e/host.spec.ts` (B-0-06 à B-0-08), `launch.spec.ts` (S-11-05), `package.spec.ts` (S-11-06),
  `undo-after-reload.spec.ts` (S-11-07, B-2.6-02, B-2.6-03), `accessibility.spec.ts` (S-11-09), `exports.spec.ts`
  (B-8.3-11 à 13 et le contenu du PNG, sous la CSP de l'hôte, politique prouvée en vigueur).
- `docs/couverture.md` : 223 lignes écrites par `Travail/outils-p6-t10/coverage_t10.py`.
- Suite web vitest, `tsc -b`, ESLint ; suite complète ; Architecture.
