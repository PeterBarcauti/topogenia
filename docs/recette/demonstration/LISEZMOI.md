<!-- LISEZMOI.md -->
<!-- Mode d'emploi du dossier de démonstration de la phase 6 (P6-T11) : captures des écrans principaux, scénario pas à pas, fichiers exportés, et comment tout refaire. -->

# Démonstration de l'interface (P6-T11)

Ce dossier est la démonstration que le porteur du projet valide à la fin (prompt 8) : **une capture de chaque écran principal**
(S-P-18) et **un scénario pas à pas sur un projet fictif** (S-P-19), joués sur le vrai hôte, en mode Poste, dans
Chromium, à 1366 × 768.

- `scenario.md` : le scénario, pas par pas (geste, résultat attendu, capture) : un projet de notes de frais, du projet
  vide à la carte détaillée, traduite, exportée, passée en paquet et importée.
- `captures/` : les 30 captures, numérotées dans l'ordre du scénario.
- `exports/` : les fichiers que l'application a produits pendant le jeu : l'image, le PDF et le classeur de la carte,
  le paquet du projet, le classeur des traductions téléchargé et le même complété en anglais.

Données entièrement fictives (prompt 4-5) : le projet « Notes de frais » (code NDF) d'une entreprise sans nom, les
comptes `DEMO\camille` (l'animateur, la personne qui lance l'application), `DEMO\karim` et `DEMO\ines`. Le lanceur des
parcours reçoit ce compte par `--account` (mode Poste seulement) à la place du compte Windows de la machine : aucune
capture ne montre un vrai identifiant ni un nom de machine.

## Les écrans principaux

| Écran | Captures |
|---|---|
| Accueil (premier lancement ; projets, « Qui participe », corbeille) | `01`, `29` |
| Nouveau projet | `02` |
| Carte vide, couloirs et leur menu | `03`, `04` |
| Carte : ruban, couloirs, étapes de plusieurs types, avertissements | `05` |
| Barre de commande Ctrl+K | `06` |
| Carte reliée : flèches routées, libellés, retour sous les étapes | `07`, `08` |
| Fiche ancrée : formulaire, déroulé, sorties, entrée utilisée avec irritant, fiche validée, jauges | `09`, `10`, `11` |
| Calque et panneau RACI, matrice | `12`, `13`, `14` |
| Menu d'une étape | `15` |
| Carte détaillée, fil d'Ariane, étape détaillée et sa fréquence | `16`, `17` |
| Remontée du détail : cadre, panneau du reliquat, résultat | `18`, `19`, `20` |
| Traductions : dialogue, classeur rapporté, traduction à la main | `21`, `22`, `23` |
| L'interface et la carte en anglais | `24` |
| Menu Fichier et exports (PNG, PDF A3, classeur) | `25` et `exports/` |
| Export du paquet, import par l'aperçu | `26`, `27` |
| Mise à la corbeille | `28` |
| Membres du projet | `30` |

## Refaire la démonstration

Depuis `web/` :

```
npm run demonstration
```

La commande construit l'interface, démarre le vrai hôte par le lanceur des parcours (`tests/Topogenia.EndToEnd.Host`,
ADR 067) en mode Poste, sous le compte fictif, sur une base SQLite neuve, puis joue le scénario
(`web/scripts/demonstration/scenario.ts`) : chaque pas fait les gestes de l'utilisateur (clics, double-clics, clavier,
fichiers choisis), vérifie le résultat attendu, prend sa capture et garde les fichiers téléchargés. Les captures et les
exports précédents sont effacés au départ ; les dates des fichiers et des écrans sont celles du jour du jeu. Compter
une minute de jeu après la construction (le jeu attend que les notifications aient disparu avant de capturer la
carte). Ne pas la lancer pendant
les parcours (`npm run e2e`) : elle prend le même port. Avec l'interface déjà construite : `E2E_NO_BUILD=1 npx
playwright test -c scripts/demonstration/playwright.config.ts`.

Sources : `web/scripts/demonstration/` (`playwright.config.ts` : la configuration des parcours, le compte fictif ;
`walk.ts` : dossiers, captures, gestes sur la carte ; `drawing.ts`, `work.ts`, `sharing.ts` : les chapitres ;
`english.ts` : l'anglais des textes, comme le compléterait un traducteur). Ce n'est pas un parcours : `npm run e2e` ne
le joue pas.

## Ce que la démonstration ne montre pas

- **La fiche en lecture** (vue d'un lecteur, sans saisie) : le banc n'a qu'un compte, animateur et administrateur (même
  limite que les parcours, ADR 067). Elle est vérifiée par les tests de l'interface (`features/sheet/`).
- **Le mode Serveur** (authentification Windows, plusieurs personnes) : les écrans sont les mêmes ; les parcours le
  jouent (`E2E_MODE=server`), la recette finale (P7) le documentera.
- **Les fichiers `.pmap`** (ouvrir, télécharger une copie, dupliquer) : présents au menu Fichier (capture `25`) et à
  l'accueil ; prouvés par les parcours `files.spec.ts`.

## Défauts relevés en jouant la démonstration

- **Une pointe de flèche cachée sous un badge** (registre `docs/a-peaufiner.md`, F-25) : la flèche « non » qui revient à
  NDF-E002 par le bas avait sa pointe sous le badge « Mensuelle » de l'étape détaillée, et les pastilles RACI, sous
  l'étape, cachaient le segment de cette flèche. **Corrigé** par P6-T11b (ADR 068, AR-128) : ce qui pend sous une étape
  s'écarte de 10 px de l'axe de sa pointe basse dès qu'une flèche l'emprunte, du côté où ses flèches ne passent pas ;
  ici à gauche (captures `13`, `17`, `24` et l'image de `exports/`, refaites après le correctif).
- **Deux pointes de flèche devant un losange** (relevé par le porteur du projet sur la capture `07`) : les deux flèches qui arrivent
  sur la pointe gauche de NDF-E009 finissaient à 6 px l'une de l'autre, la seconde à côté de la pointe. **Corrigé** par
  P6-T11c (AR-129, révision de l'ADR 059) : les flèches qui arrivent sur un même côté d'une étape partagent leur dernier
  segment et finissent sur un seul point, comme en 1.x (captures refaites après le correctif).
- **« Export en cours… » reste affichée** après « Export terminé » (F-33, mineur, repris en fin de projet).
