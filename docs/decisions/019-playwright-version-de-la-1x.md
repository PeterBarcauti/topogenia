<!-- 019-playwright-version-de-la-1x.md -->
<!-- C6 : Playwright 1.64.1, même version que la 1.x, pour réutiliser les navigateurs installés -->

# 019 - Playwright à la version de la 1.x

Statut : acceptée (date 23/10/2026)

## Contexte

Les parcours de bout en bout et les captures de la maquette demandent un navigateur piloté.
Le prompt 4-4 interdit d'installer un logiciel sur la machine et demande de réutiliser les
navigateurs Playwright déjà installés pour la 1.x : l'antivirus met en quarantaine les
installations silencieuses. Chaque version de Playwright attend une révision précise de
Chromium.

## Décision

- Interface : Vite + React 19 + TypeScript strict ; parcours et captures avec
  `@playwright/test` 1.64.1, la version de la 1.x.
- Navigateur : la révision déjà installée `chromium-1248`, celle qu'attend la 1.64.1.
- Aucune commande `playwright install`, ni en local ni dans les scripts du dépôt.

## Conséquences

- Pas de téléchargement ni d'installation de navigateur.
- La montée de version de Playwright est liée à une nouvelle révision de navigateur : elle
  demandera une ADR et l'accord du porteur du projet.
- Les parcours visent Chromium (Edge et Chrome, NF01).

## Vérification

- `web/package.json` et `web/package-lock.json` (version exacte 1.64.1) relus par la porte de
  contrôle Interface et charte.
- Captures de la maquette (P1-T08) produites sans installation.

## Intégration continue

L'interdiction d'installer vise le poste du porteur du projet (prompt 4-4). Sur les machines jetables de
l'intégration continue, l'étape e2e (ajoutée en phase 6) installe le Chromium de la même
version de Playwright par `npx playwright install --with-deps chromium` : rien n'est installé
sur un poste de l'entreprise.
