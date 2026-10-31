<!-- LISEZMOI.md -->
<!-- Mode d'emploi de la recette finale de la 2.0 (P7-T05) : ce qui est livré, comment installer et lancer les deux modes, les contrôles à jouer sur la version livrée, la liste de sécurité, la SBOM et l'état de la couverture. -->

# Recette finale de la 2.0 (P7-T05)

Ce dossier est la recette que le porteur du projet joue sur la version livrée (prompt 8) : comment installer et lancer
le mode Poste et le mode Serveur, ce qui reste à vérifier sur un vrai poste et sur un vrai serveur, la liste de sécurité
cochée avec ses preuves, la SBOM et l'état de la couverture. Les deux autres validations du dossier `docs/recette/` sont
la maquette (`maquette/`) et la démonstration (`demonstration/`). La migration de la base 1.x n'en fait plus partie
(AR-94).

## 1. Ce qui est livré

Dans `artifacts/` (dossier de construction, hors du dépôt Git) :

| Fichier | Contenu |
|---|---|
| `Topogenia-2.0.0-portable.zip` | Mode Poste : exécutable autonome `Topogenia.exe` (win-x64, rien à installer), `README.txt`, licences, `sbom.cdx.json` |
| `Topogenia-2.0.0-server.zip` | Mode Serveur pour IIS (Hosting Bundle .NET 10) : application, `web.config`, `appsettings.Production.example.json`, guide `exploitation-serveur.md`, `README.txt`, licences, `sbom.cdx.json` |
| `*.zip.sha256` | Empreinte SHA-256 de chaque ZIP, à vérifier sur la copie prise dans le dossier de téléchargement |
| `*.cdx.json` | SBOM CycloneDX 1.5 de chaque ZIP (la même que celle du ZIP) |
| `*.files.sha256` | Empreinte SHA-256 de chaque fichier de chaque ZIP, pour la liste blanche d'un antivirus ou d'un EDR (ADR 076) |
| `core-feed/Topogenia.Core.2.0.0.nupkg` | Le cœur fermé en paquet (domaine signé par nom fort et obscurci, ADR 074), pour le dépôt public ; privé |

Les ZIP et l'exécutable ne sont pas signés, faute de certificat (ADR 049) : Windows peut afficher l'écran SmartScreen
au premier lancement (« Informations complémentaires », puis « Exécuter quand même », `docs/exploitation-poste.md`
§ 1.3). Les empreintes de cette version (SHA-256, ZIP construits le 29/10/2026) : `Topogenia-2.0.0-portable.zip` :
`02f32bf6f78079ddf5e6b2f15c64f805c05c6b356bf7f613ba5c8effcc3e91f0` ; `Topogenia-2.0.0-server.zip` :
`b23760475f6e9bd186cb2a28cc50f8c85e2c27697245e223ab1c33353f7c402d`.

## 2. Mode Poste : installer et lancer

Le guide complet est `docs/exploitation-poste.md`. En bref :

1. Copier `Topogenia-2.0.0-portable.zip` et son `.sha256` dans un dossier local, **non synchronisé** (pas OneDrive ni
   SharePoint : l'application le refuse pour son fichier de données). Vérifier l'empreinte :
   `(Get-FileHash .\Topogenia-2.0.0-portable.zip -Algorithm SHA256).Hash` égale le contenu du `.sha256`.
2. Décompresser, double-cliquer sur `Topogenia.exe`. La console écrit « Prêt : http://127.0.0.1:<port> » et le
   navigateur par défaut s'ouvre sur l'accueil. Le premier lancement crée `%LOCALAPPDATA%\Topogenia\` (données,
   sauvegardes, journaux) et propose le « Projet d'exemple ».
3. Un second double-clic ramène l'application dans le navigateur, sans seconde instance.
4. Quitter : menu du compte, « Quitter Topogenia » ; ou fermer la fenêtre de la console.

## 3. Mode Serveur : installer et lancer

Le guide complet est `docs/exploitation-serveur.md` (prérequis, comptes, réglages, base et `--migrate`, IIS, HTTPS,
`--check`, lecture des refus, sauvegardes, supervision, mise à jour). Son § 15 est la **recette d'installation** :
15 contrôles que le poste de construction ne pouvait pas faire (IIS, compte du pool membre du seul rôle `topogenia_app`,
authentification Windows d'IIS, paquet de 100 Mo à travers IIS, rapports `bi`). Les résultats se notent dans la
colonne « Résultat » du § 4 ci-dessous (contrôles S-01 à S-15).

## 4. Contrôles de la recette

Ce que les tests automatiques ne pouvaient pas prouver sur le poste de construction, ou ce qui se juge à l'œil. Chaque
ligne dit où elle vient (couverture `docs/couverture.md`, registre `docs/a-peaufiner.md`).

### 4.1 Mode Poste

| N° | Contrôle | Attendu | Origine | Résultat |
|---|---|---|---|---|
| P-01 | Premier lancement de l'exécutable décompressé, chronométré jusqu'à l'accueil | Moins de 5 s (4,2 s mesurées, davantage quand l'antivirus examine une copie neuve) | S-NF-08, S-11-04 | |
| P-02 | Second double-clic pendant que l'application tourne | Le navigateur se rouvre sur l'application ; la seconde console se ferme seule | S-D10-01, AR-144 | |
| P-03 | Fermer la fenêtre de la console, puis relancer | L'application s'arrête ; au lancement suivant, rien de ce qui était enregistré n'est perdu | S-5.2-15 | |
| P-04 | Copier la matrice RACI et la coller dans Excel, avec un libellé qui commence par `=` | Le libellé reste un texte, aucune formule ne s'exécute | AR-130 | |
| P-05 | Jouer le scénario de la démonstration (`docs/recette/demonstration/scenario.md`) sur le poste | Chaque pas donne le résultat attendu du scénario | S-11-16 | |
| P-06 | Fonctionnement sans réseau (câble ou Wi-Fi coupé) | L'application s'ouvre et enregistre normalement | S-NF-16 | |
| P-07 | Analyse des deux ZIP par l'antivirus du poste, puis premier lancement de `Topogenia.exe` décompressé ; ensuite, la même chose sur un poste pilote de l'entreprise, sous son EDR | Aucune alerte, aucune mise en quarantaine (`docs/exploitation-poste.md` § 1.4, `exploitation-serveur.md` § 2.1) | ADR 076, AR-150 | |

### 4.2 Interface, à l'œil (recette visuelle)

| N° | Contrôle | Origine | Résultat |
|---|---|---|---|
| V-01 | Badges de fréquence et pastilles RACI près des flèches, sur des cartes réelles | F-25 | |
| V-02 | Espace tenu sur une étape qui a le focus (la Main ne vient pas) : gênant ou non | F-27 | |
| V-03 | Menu d'une étape (19 entrées) sur un écran de moins de 700 px de haut | F-28 | |
| V-04 | « + » d'ajout rapide à droite d'une étape, près de l'étiquette de la flèche sortante | F-29 | |
| V-05 | Libellés du menu Fichier de la carte sur deux lignes | F-30 | |
| V-06 | Notification « Export en cours… » qui reste après « Export terminé » | F-33 | |
| V-07 | Écarts à la maquette : titres des zones du ruban, zoom en pourcentage, « Enregistré automatiquement », « Dernier ouvert » | F-37 | |
| V-08 | Couleurs des niveaux de classification à l'accueil | F-39 | |
| V-09 | Étiquette d'une flèche posée au milieu d'un segment vertical, près du coude d'une voisine | F-40 | |

### 4.3 Mode Serveur

| N° | Contrôle | Origine | Résultat |
|---|---|---|---|
| S-01 à S-15 | Les 15 contrôles de `docs/exploitation-serveur.md` § 15 (IIS, pool, `topogenia_app` seul, HSTS, 401 anonyme, Windows de bout en bout, administrateurs relus, HTTP, journal, 100 Mo, 413, `bi`) | S-5.3-01, AR-110, ADR 072, F-43 | |
| S-16 | Journal du jour : les lignes `Microsoft.AspNetCore` suivent la règle `Warning` de l'exemple de réglages | F-43 | |

### 4.4 Composants tiers

| N° | Contrôle | Origine | Résultat |
|---|---|---|---|
| C-01 | Confirmer en ligne les 38 licences « relevées » de `THIRD-PARTY-NOTICES.md` (paquets embarqués dans le paquet navigateur d'exceljs) | F-31 | |

## 5. Liste de sécurité

`docs/securite.md` : les 57 exigences de la section 5 de la spécification, chacune avec ses preuves, écrites depuis la
couverture par `build/security-list.mjs` (vérifié par `ThirdPartyNoticesTests`). **52 cochées** ; les 5 autres :
S-5.2-15 et S-5.3-01 se vérifient ici (P-03, S-01 à S-15) ; S-5.4-16 et S-5.4-17 (audits `dotnet list package
--vulnerable` et `npm audit`) sont écrits dans l'intégration continue, jamais jouée faute de dépôt (prompt 4.3) ;
S-5.4-19 (signature) attend un certificat (ADR 049). Le même document porte la revue OWASP ASVS 4.0 niveau 2 et
TISAX / VDA ISA.

## 6. SBOM et composants

Une SBOM CycloneDX 1.5 par ZIP (`*.cdx.json`, et `sbom.cdx.json` dans le ZIP), écrite par `build/sbom.mjs` depuis ce
que la publication porte ; `THIRD-PARTY-NOTICES.md` (écrit par `build/third-party-notices.mjs`, `--check`) liste chaque
composant tiers, son usage et sa licence (ADR 073). Toutes les licences livrées sont permissives.

## 7. État de la couverture

État au 29/10/2026 (`docs/couverture.md`, ligne S-11-11). Aucune ligne `à faire` ni `partiel`.

| Partie | Lignes | États |
|---|---|---|
| Annexe A (serveur 1.x) | 787 | 780 `couvert`, 1 `couvert (domaine)`, 1 `couvert (application)`, 4 `écart (AR-108)` (codes de refus de l'annulation), 1 `sans objet` (fichiers `.pmap` 1.0) |
| Annexe B (interface 1.x) | 964 | 938 `couvert`, 20 `couvert (API, AR-112)`, 3 `renvoi` vers la ligne qui les couvre, 2 `écart` (lecture seule jouée dans les tests de l'interface, pas de bout en bout : le banc n'a qu'un compte), 1 `sans objet (AR-94)` |
| Exigences de la spécification et du prompt (`S-*`) | 324 | 237 `couvert`, 51 `couvert (revue)`, 4 `couvert (mesure)`, 5 `renvoi (recette)` (contrôles de la section 4), 7 `écart` (intégration continue jamais jouée, signature et CodeQL, extension `.topogenia` figée), 20 `sans objet` (MSIX, migration 1.x, attente de Q3) |

Tests (dernier passage complet) : suite .NET 5 123/5 123, 0 sauté, 0 avertissement, sur SQLite et SQL Server ;
interface 1 792/1 792 ; parcours 90/90 dans chacun des deux modes. Les écarts et leurs raisons : `docs/RAPPORT_FINAL.md`,
section 3.

## 8. Le cœur fermé et le dépôt public

Le domaine et ses tests restent dans la copie de travail, qui tient lieu de dépôt privé ; `node build/split.mjs
<dossier>` écrit le dépôt public sans eux et prouve qu'il compile et passe ses tests avec le seul paquet du cœur
(ADR 074). La clé du nom fort est hors du dépôt, à garder : `45_Topogenia\Cle-coeur\core.snk` (une autre clé se crée
par `sn.exe -k 2048` ; seul le jeton de l'assemblage change). Aucun dépôt Git n'est créé : la publication sur GitHub
(privée) reste la décision du porteur du projet.
