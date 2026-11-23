// sample-project.spec.ts
// Journey of the first launch (spec 6.1, S-6.1-10, ADR 111): the desktop mode offers the fictitious sample project on an empty home page, created on demand, its map opened at once with its lanes, a second one offered by « New project » under a free code; the server mode never offers it.

import type { Page } from '@playwright/test';
import type { components } from '../src/api/generated/schema.ts';
import type { Api } from './support/api.ts';
import { expect, stepOf, test } from './support/journey.ts';
import { MODE } from './support/run.ts';

type ProjectItem = components['schemas']['ProjectListItem'];

/** The lanes of the root map of the sample package (`samples/conges.*`), in their order. */
const SAMPLE_LANES = ['Salarié', 'Manager', 'Ressources humaines', 'Logiciel de paie', 'Comptabilité'];

/** The home page of an empty base, read whole: the projects of the administrator listed (none), his sentence and his buttons. */
async function emptyHome(page: Page): Promise<void> {
  await page.goto('/');
  await expect(page.getByTestId('home-empty')).toHaveText('Aucun projet pour l’instant. Créer le premier.');
  await expect(page.getByRole('button', { name: 'Nouveau projet' })).toBeEnabled();
  // The administrator without a project is offered the starts by the main button, no longer a disabled one (ADR 111).
  await expect(page.getByRole('button', { name: 'Décrire un processus' })).toBeEnabled();
}

/** Desktop mode: the card in the place of the list, the project created by the host, then its map with its lanes. */
async function offeredThenCreated(page: Page, api: Api): Promise<void> {
  const offer = page.getByTestId('home-sample');
  await expect(offer.getByRole('heading', { name: 'Projet d’exemple' })).toBeVisible();
  await expect(offer).toContainText('Un projet fictif, déjà décrit');
  await offer.getByRole('button', { name: 'Créer le projet d’exemple' }).click();
  // The map of the sample opens by itself, with its lanes and its steps; the message says the code taken.
  await expect(page.getByText('Projet d’exemple CONGES créé.')).toBeVisible();
  await expectSampleMap(page, 'CONGES');
  // Home page again: the list read holds the project like any other, with its classification; the offer is gone with the empty home page.
  await page.getByTestId('header-brand').click();
  await expect(page.getByTestId('project-CONGES')).toContainText('Demande de congés');
  await expect(page.getByTestId('project-CONGES-classification')).toHaveText('Confidentiel');
  await expect(offer).toHaveCount(0);
  await expect(page.getByTestId('home-empty')).toHaveCount(0);
  // On the host: one project, imported without the people of the package, its user its only facilitator.
  const projects = await api.get<ProjectItem[]>('/api/projects');
  expect(projects.map((project) => [project.code, project.classification, project.role])).toEqual([['CONGES', 'confidential', 'facilitator']]);
  // With a project the main button opens its map, as before (B-1.3-02), and offers no start.
  await page.getByRole('button', { name: 'Décrire un processus' }).click();
  await expect(page.getByTestId('home-start-menu')).toHaveCount(0);
  await expect(page.getByTestId('breadcrumb-level-0')).toHaveText('Demande de congés');
  await secondSample(page, api);
}

/** « New project » still offers the sample: a second one under the next free code, its map opened (ADR 111). */
async function secondSample(page: Page, api: Api): Promise<void> {
  await page.getByTestId('header-brand').click();
  // The message of the first creation is gone before the second is waited for.
  await expect(page.getByTestId('toast-success')).toHaveCount(0);
  await page.getByRole('button', { name: 'Nouveau projet' }).click();
  await expect(page.getByTestId('home-start-menu-blank')).toBeVisible();
  await page.getByTestId('home-start-menu-sample').click();
  await expect(page.getByText('Projet d’exemple CONGEA créé.')).toBeVisible();
  await expectSampleMap(page, 'CONGEA');
  const projects = await api.get<ProjectItem[]>('/api/projects');
  expect(projects.map((project) => project.code).sort()).toEqual(['CONGEA', 'CONGES']);
}

/** The root map of a sample project, open: its lanes in their order and its first step. */
async function expectSampleMap(page: Page, code: string): Promise<void> {
  await expect(page.getByTestId('breadcrumb-level-0')).toHaveText('Demande de congés');
  await expect(page.getByTestId('lane-headers').locator('.lane-head-name')).toHaveText(SAMPLE_LANES);
  await expect(stepOf(page, `${code}-E001`)).toContainText('Besoin de congés');
  await expect(page.getByTestId('header-classification')).toHaveText('Confidentiel');
}

/** Server mode: no card, even for an administrator on an empty base, and the route of the desktop mode creates nothing. */
async function neverOffered(page: Page, api: Api): Promise<void> {
  await expect(page.getByTestId('home-sample')).toHaveCount(0);
  // Neither do the starts of the main button nor those of « New project » hold it (ADR 111).
  await page.getByRole('button', { name: 'Décrire un processus' }).click();
  await expect(page.getByTestId('home-start-menu-blank')).toBeVisible();
  await expect(page.getByTestId('home-start-menu-sample')).toHaveCount(0);
  await page.keyboard.press('Escape');
  await expect(page.getByTestId('home-start-menu')).toHaveCount(0);
  await page.getByRole('button', { name: 'Nouveau projet' }).click();
  await expect(page.getByTestId('home-start-menu-blank')).toBeVisible();
  await expect(page.getByTestId('home-start-menu-sample')).toHaveCount(0);
  await page.keyboard.press('Escape');
  await expect(api.send('POST', '/api/desktop/sample-project')).rejects.toThrow(': 404 ');
  expect(await api.get<ProjectItem[]>('/api/projects')).toEqual([]);
}

test('the first launch: the desktop mode offers the sample project, created on demand, its map opened with its lanes, a second one under a free code; never the server mode (S-6.1-10)', async ({ page, api }) => {
  await emptyHome(page);
  await (MODE === 'server' ? neverOffered(page, api) : offeredThenCreated(page, api));
});
