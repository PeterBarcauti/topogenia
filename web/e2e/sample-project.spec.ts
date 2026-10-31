// sample-project.spec.ts
// Journey of the first launch (spec 6.1, S-6.1-10): the desktop mode offers the fictitious sample project on an empty home page, created on demand, then its map opened with its lanes; the server mode never offers it.

import type { Page } from '@playwright/test';
import type { components } from '../src/api/generated/schema.ts';
import type { Api } from './support/api.ts';
import { expect, stepOf, test } from './support/journey.ts';
import { MODE } from './support/run.ts';

type ProjectItem = components['schemas']['ProjectListItem'];

/** The lanes of the root map of the sample package (`samples/conges.*`), in their order. */
const SAMPLE_LANES = ['Salarié', 'Manager', 'Ressources humaines', 'Logiciel de paie', 'Contrôle de gestion'];

/** The home page of an empty base, read whole: the projects of the administrator listed (none), his sentence and his buttons. */
async function emptyHome(page: Page): Promise<void> {
  await page.goto('/');
  await expect(page.getByTestId('home-empty')).toHaveText('Aucun projet pour l’instant. Créer le premier.');
  await expect(page.getByRole('button', { name: 'Nouveau projet' })).toBeEnabled();
  await expect(page.getByRole('button', { name: 'Décrire un processus' })).toBeDisabled();
}

/** Desktop mode: the card in the place of the list, the project created by the host, then its map with its lanes. */
async function offeredThenCreated(page: Page, api: Api): Promise<void> {
  const offer = page.getByTestId('home-sample');
  await expect(offer.getByRole('heading', { name: 'Projet d’exemple' })).toBeVisible();
  await expect(offer).toContainText('Un projet fictif, déjà décrit');
  await offer.getByRole('button', { name: 'Créer le projet d’exemple' }).click();
  await expect(page.getByText('Projet d’exemple CONGES créé.')).toBeVisible();
  // The list read again holds the project like any other, with its classification; the offer is gone with the empty home page.
  await expect(page.getByTestId('project-CONGES')).toContainText('Demande de congés');
  await expect(page.getByTestId('project-CONGES-classification')).toHaveText('Confidentiel');
  await expect(offer).toHaveCount(0);
  await expect(page.getByTestId('home-empty')).toHaveCount(0);
  // On the host: one project, imported without the people of the package, its user its only facilitator.
  const projects = await api.get<ProjectItem[]>('/api/projects');
  expect(projects.map((project) => [project.code, project.classification, project.role])).toEqual([['CONGES', 'confidential', 'facilitator']]);
  // The project is the one of the main button, which opens its map with its lanes and its steps.
  await page.getByRole('button', { name: 'Décrire un processus' }).click();
  await expect(page.getByTestId('breadcrumb-level-0')).toHaveText('Demande de congés');
  await expect(page.getByTestId('lane-headers').locator('.lane-head-name')).toHaveText(SAMPLE_LANES);
  await expect(stepOf(page, 'CONGES-E001')).toContainText('Besoin de congés');
  await expect(page.getByTestId('header-classification')).toHaveText('Confidentiel');
}

/** Server mode: no card, even for an administrator on an empty base, and the route of the desktop mode creates nothing. */
async function neverOffered(page: Page, api: Api): Promise<void> {
  await expect(page.getByTestId('home-sample')).toHaveCount(0);
  await expect(api.send('POST', '/api/desktop/sample-project')).rejects.toThrow(': 404 ');
  expect(await api.get<ProjectItem[]>('/api/projects')).toEqual([]);
}

test('the first launch: the desktop mode offers the sample project, created on demand, its map opened with its lanes; never the server mode (S-6.1-10)', async ({ page, api }) => {
  await emptyHome(page);
  await (MODE === 'server' ? neverOffered(page, api) : offeredThenCreated(page, api));
});
