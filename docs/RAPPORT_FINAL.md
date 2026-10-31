<!-- RAPPORT_FINAL.md -->
<!-- Rapport de fin de mission de la 2.0 (prompt 9) : verdict, ce qui est livré et où, chiffres des tests et de la couverture, écarts à la spécification, ce qui attend le porteur du projet. -->

# Rapport final de la 2.0

**Verdict : la 2.0 est construite et prête pour la recette du porteur du projet.** Les phases 0 à 6 sont fermées par
leur porte ; la phase 7 se ferme par la porte P7-T06 (Architecture, Sécurité, Intégrité des tests), jouée sur cette
version (résultat au journal). Tous les tests sont verts sur les deux bases, sans avertissement ni test sauté (suite .NET
5 123/5 123, interface 1 792/1 792, parcours 90/90 dans chacun des deux modes) ; chaque ligne des annexes A et B est
reliée à un test vert, hors écarts écrits (section 3). Ce qui reste ne se fait pas sur le poste de construction : les
validations du porteur, l'installation sous IIS, la signature et la première intégration continue.

## 1. Ce qui est livré et où

| Quoi | Où |
|---|---|
| Solution (.NET 10, React) ; la copie de travail tient lieu de dépôt privé (AR-147) | `topogenia/` |
| Mode Poste : `Topogenia-2.0.0-portable.zip` (exécutable autonome, rien à installer) | `topogenia/artifacts/` |
| Mode Serveur : `Topogenia-2.0.0-server.zip` (IIS, Hosting Bundle .NET 10) | `topogenia/artifacts/` |
| SBOM CycloneDX 1.5 et empreinte SHA-256 de chaque ZIP | `topogenia/artifacts/*.cdx.json`, `*.zip.sha256` |
| Cœur fermé : paquet `Topogenia.Core` 2.0.0 (domaine signé par nom fort, obscurci, licence propriétaire, ADR 048 et 074) | `topogenia/artifacts/core-feed/` ; clé hors du dépôt : `45_Topogenia/Cle-coeur/core.snk` |
| Dépôt public (Apache-2.0, sans le domaine ni ses tests), écrit et prouvé par `build/split.mjs` | `45_Topogenia/topogenia-public/` |
| Fichiers du dépôt : `README.md` (français et anglais), `LICENSE`, `NOTICE`, `SECURITY.md`, `CONTRIBUTING.md`, `THIRD-PARTY-NOTICES.md`, `.gitignore`, `.github/workflows/ci.yml` | `topogenia/` |
| Exploitation : `docs/exploitation-poste.md`, `docs/exploitation-serveur.md` ; sécurité : `docs/securite.md` (liste de la section 5 cochée avec ses preuves, revue ASVS niveau 2 et TISAX) | `topogenia/docs/` |
| Recette : maquette, démonstration (17 pas, 30 captures), recette finale | `topogenia/docs/recette/` |
| Décisions : ADR 001 à 074, arbitrages AR-01 à AR-148 ; suivi : `journal.md`, `etat.md`, `plan.md`, `couverture.md`, `questions.md`, `a-peaufiner.md` | `topogenia/docs/` |

Aucun dépôt Git n'est créé et rien n'est publié : la mise sur GitHub (privée) reste la décision du porteur.

## 2. Tests et couverture

Dernier passage complet le 29/10/2026 (P7-T05), sur le poste de construction : 0 échec, 0 test sauté, 0 avertissement
de construction.

| Niveau | Tests | Bases |
|---|---|---|
| Domaine | 1 385 | aucune |
| Application | 405 | aucune (dépôts en mémoire) |
| Persistance | 1 119 | 375 joués sur SQLite et les mêmes 375 sur SQL Server ; les autres propres à un moteur (sauvegarde et restauration SQLite, catalogue) ou sans base |
| Échange | 262 | aucune ; les allers-retours, imports et paquets hostiles sur les deux bases sont dans les tests de l'API (`tests/Topogenia.Host.Tests/Exchange/`, 49 sur chaque base) |
| API (intégration, sécurité, droits par rôle) | 1 172 | 468 joués sur SQLite et les mêmes 468 sur SQL Server ; les autres sur l'hôte sans base (en-têtes, `Host`, démarrage) |
| Architecture | 780 | aucune (règles 3.2, D1 à D13, en-têtes, nom du produit, tailles) |
| **Suite .NET** | **5 123** | |
| Interface (vitest) | 1 792 | aucune ; plus `tsc -b` et ESLint sans avertissement |
| Bout en bout (Playwright, axe-core compris) | 90 + 90 | mode Poste sur SQLite, mode Serveur sur SQL Server (`Topogenia_E2E`, HTTPS, Negotiate) |
| Dépôt public (avec le seul paquet du cœur) | 3 692 verts, 46 sautés | les sautés sont les règles qui lisent les sources du domaine, absentes par construction (`CoreSources.Require`) ; 0 échec |

La charge de 30 clients simultanés est tenue sur l'hôte publié (`docs/couverture-p7/verification-serveur.md`) ; le
démarrage du mode Poste est mesuré dans `docs/couverture-p7/verification-poste.md`.

Couverture (`docs/couverture.md`) : aucune ligne `à faire` ni `partiel`.

| Partie | Lignes | Reliées à un test vert | Le reste |
|---|---|---|---|
| Annexe A (serveur 1.x) | 787 | 782 (dont 1 au domaine, 1 à l'application) | 4 écarts AR-108, 1 sans objet |
| Annexe B (interface 1.x) | 964 | 958 (dont 20 au niveau de l'API, AR-112) | 3 renvois vers la ligne qui les couvre, 2 écarts, 1 sans objet (AR-94) |
| Exigences de la spécification et du prompt | 324 | 292 (dont 51 par revue, 4 par mesure) | 5 renvoyées à la recette, 7 écarts, 20 sans objet |

## 3. Écarts à la spécification

| Écart | Raison | Lignes |
|---|---|---|
| Pas de MSIX, paquets non signés : le ZIP portable est le seul livrable du mode Poste, empreintes SHA-256 publiées ; la chaîne signe dès qu'un certificat est donné | Pas de certificat de la DSI ni de signature payante (Q-02, ADR 049) | S-6.1-01 à 03, S-6.1-08, S-5.4-19 |
| Intégration continue écrite (`.github/workflows/ci.yml` : construction, tests sur les deux bases, audits `dotnet list package --vulnerable` et `npm audit`), jamais jouée | Aucun dépôt ni envoi vers Internet depuis le poste de construction (prompt 4.3) | S-11-22, S-11-23, S-5.4-16, S-5.4-17 |
| CodeQL : étape de l'intégration continue désactivée ; `build/codeql-local.mjs` prêt, non joué faute de CLI installée | Option payante non activée (Q-07, ADR 049) ; aucune installation (prompt 4.4) | S-11-24 |
| Pas de migration 1.x dans l'application ; fichiers `.pmap` 1.0 non lus | Ariane n'a jamais été publiée : le flux du porteur sera migré une fois, à sa demande, hors du dépôt (AR-94) | S-7.6-01 à 12, S-11-15, S-P-17, B-6-32, A-2.17-50 |
| IIS non essayé : la charge de 30 clients est mesurée sur l'hôte publié sous Kestrel ; IIS et le compte du pool vont à la recette | IIS non installable sur le poste (prompt 4.4, AR-145) | S-5.3-01 |
| Le dépôt privé est la copie de travail entière, pas un dépôt `topogenia-core` réduit | Un seul dossier de travail pour le titulaire (AR-147) | ADR 048 |
| Extension `.topogenia` figée : elle ne suit pas la constante du nom du produit | Un fichier échangé doit rester lisible si le produit change de nom (ADR 051) | S-D13-02 |
| Codes de refus de l'annulation : `INTROUVABLE` (404) et `OPERATION_DEPASSEE` (409) à la place de `LOT_INTROUVABLE`, `LOT_REMONTEE` et d'une partie de `CARTE_MODIFIEE` | Annulation par la file (AR-43, AR-66, AR-108) | A-1.5-18, A-2.15-04, A-3-52, A-3-59 |
| Lecture seule non jouée de bout en bout, prouvée dans les tests de l'interface | Le banc des parcours n'a qu'un compte, administrateur (ADR 067) | B-2.3-46, B-8.3-36 |
| Renvoyé à la recette sur un vrai poste : arrêt par la fermeture de la console, démarrage sous 5 s jusqu'à l'accueil (4,2 s jusqu'à « Prêt » mesuré), décompression et lancement du ZIP | Se juge sur la machine du porteur | S-5.2-15, S-NF-08, S-11-04 |

Mineurs : le registre `docs/a-peaufiner.md` (F-01 à F-43) donne à chacun son issue (fait, gardé ou recette) ; ceux à
juger sur l'application livrée sont repris dans les contrôles de `docs/recette/finale/LISEZMOI.md`.

## 4. Ce qui attend le porteur du projet

1. **La recette finale** : `docs/recette/finale/LISEZMOI.md` (installer et lancer les deux modes, contrôles à jouer,
   liste de sécurité, SBOM, couverture).
2. **Les validations** de `docs/recette/` : la maquette (`maquette/`) et la démonstration (`demonstration/`, lire
   `LISEZMOI.md` puis `scenario.md`). La validation de la migration est remplacée par la migration de son flux, à sa
   demande (AR-94).
3. **Les questions** de `docs/questions.md` : la licence est tranchée (Q-01 : Apache-2.0, cœur propriétaire,
   ADR 048) ; restent à relire les défauts appliqués, dont Q-04 (axe-core, MPL-2.0, outil de développement jamais
   livré) et Q-05 (code de couloir à l'encre pleine pour le contraste), et les arbitrages qui changent un comportement
   visible de la 1.x.
4. **Plus tard, s'il le souhaite** : un certificat de signature (la chaîne le prend, ADR 049), la publication du dépôt
   sur GitHub et sa première intégration continue, une clé de licence au-dessus du cœur fermé (option (d) de
   l'ADR 048).
