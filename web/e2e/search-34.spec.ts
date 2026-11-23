// search-34.spec.ts
// Journeys of the search of the 3.4 (lot 2, ADR 110): Ctrl+F and the panel « Rechercher » over a project of two maps, Enter and Shift+Enter across the maps, the word without its accents, two words each marked (R9b), the second Ctrl+F left to the browser, the filter of the home page, and the gestures of a reader.

import type { Page } from '@playwright/test';
import type { Api, ProjectRead } from './support/api.ts';
import { expect, openMap, stepOf, test } from './support/journey.ts';

const PANEL = 'search-panel';
const FIELD = 'search-field';

interface Project {
  id: string;
  mapId: string;
  /** The codes of the steps: the two of the main map, then the two of the detailed map. */
  entry: string;
  enter: string;
  fill: string;
  warn: string;
}

/**
 * The sample's case, built by the API (the server mode has no sample): the main map holds « Besoin reçu » and « Saisir la
 * demande » (detailed, with the document « Formulaire de demande »); the detailed map holds « Remplir le formulaire »
 * (with the document « Planning d’équipe ») and « Prévenir l’équipe ».
 */
async function layOut(api: Api, code: string): Promise<Project> {
  const { id, mapId } = await api.project(code, 'Recherche', 'fr', 'confidential');
  const lane = (await api.map(mapId)).lanes[0]?.id ?? '';
  const entry = await api.step(mapId, lane, 0, 'Besoin reçu', 'start');
  const enter = await api.step(mapId, lane, 1, 'Saisir la demande');
  await api.flow(mapId, entry, enter);
  const version = (await api.map(mapId)).steps.find((candidate) => candidate.id === enter)?.version;
  const subMapId = (await api.send<{ id: string | null }>('POST', `/api/steps/${enter}/detail`, undefined, version)).id ?? '';
  const subLane = (await api.map(subMapId)).lanes[0]?.id ?? '';
  const fill = await api.step(subMapId, subLane, 0, 'Remplir le formulaire');
  const warn = await api.step(subMapId, subLane, 1, 'Prévenir l’équipe');
  await api.flow(subMapId, fill, warn);
  const document = (name: string) => ({ direction: 'output', name, location: null, wait_hours: null, share_confirmed: null });
  await api.send('POST', `/api/steps/${enter}/documents`, document('Formulaire de demande'));
  await api.send('POST', `/api/steps/${fill}/documents`, document('Planning d’équipe'));
  const codeOf = async (map: string, step: string) => (await api.map(map)).steps.find((candidate) => candidate.id === step)?.code ?? '';
  return { id, mapId, entry: await codeOf(mapId, entry), enter: await codeOf(mapId, enter), fill: await codeOf(subMapId, fill), warn: await codeOf(subMapId, warn) };
}

/** The focus on the background of the map, where a shortcut of the map is heard. */
async function onBackground(page: Page): Promise<void> {
  await page.locator('.react-flow__pane').click({ position: { x: 700, y: 600 } });
}

/** Ctrl+F from the map, the field of the panel then holding the focus. */
async function openByShortcut(page: Page): Promise<void> {
  await onBackground(page);
  await page.keyboard.press('Control+f');
  await expect(page.getByTestId(PANEL)).toBeVisible();
  await expect(page.getByTestId(FIELD)).toBeFocused();
}

/** A word typed in the field, the results shown (the first row drawn). */
async function typeWord(page: Page, word: string): Promise<void> {
  await page.getByTestId(FIELD).fill(word);
  await expect(page.getByTestId('search-hit-0')).toBeVisible();
}

const hitsOf = (page: Page) => page.locator('[data-testid^="search-hit-"]');
const positionOf = (page: Page) => page.getByTestId(PANEL).getByRole('status');

/**
 * Records each Ctrl+F reaching the page, to tell afterwards whether the page cancelled it (the find of the browser is then
 * not opened). The keyboard manager listens on the window in the capture phase and stops the propagation of the keys it
 * takes, so a listener of the bubble phase never sees those; this one listens in the capture phase too, and keeps the event
 * to read `defaultPrevented` once the page has handled it.
 */
async function recordCtrlF(page: Page): Promise<void> {
  await page.evaluate(() => {
    const seen: KeyboardEvent[] = [];
    (window as unknown as { ctrlF: KeyboardEvent[] }).ctrlF = seen;
    window.addEventListener('keydown', (event) => {
      if (event.ctrlKey && event.key.toLowerCase() === 'f') seen.push(event);
    }, true);
  });
}

const ctrlFSeen = (page: Page): Promise<boolean[]> =>
  page.evaluate(() => (window as unknown as { ctrlF: KeyboardEvent[] }).ctrlF.map((event) => event.defaultPrevented));

/** The role of a reader given to the page for the project, as in the properties (ADR 067): the bench is an administrator. */
async function readAs(page: Page, api: Api, id: string): Promise<void> {
  await page.route(`**/api/projects/${id}`, async (route) => {
    await route.fulfill({ json: { ...(await api.get<ProjectRead>(`/api/projects/${id}`)), role: 'reader' } });
  });
}

test('from the main map, Ctrl+F then "planning" finds the document of the detailed map and opens it there, the step chosen', async ({ page, api }) => {
  const project = await layOut(api, 'SRP');
  await openMap(page, project.mapId, project.entry);
  await openByShortcut(page);
  await typeWord(page, 'planning');

  // One result, under the detailed map, with its mark and the step it belongs to.
  await expect(hitsOf(page)).toHaveCount(1);
  await expect(page.getByTestId(PANEL).getByRole('region', { name: 'Saisir la demande' })).toBeVisible();
  await expect(page.getByTestId('search-hit-0').locator('mark')).toHaveText(/planning/i);
  await expect(page.getByTestId('search-hit-0')).toContainText(project.fill);

  await page.getByTestId('search-hit-0').click();
  await expect(page.getByTestId('breadcrumb-level-1')).toHaveAttribute('aria-current', 'page');
  await expect(stepOf(page, project.fill)).toHaveClass(/is-selected/);
  await expect(stepOf(page, project.fill)).toBeInViewport();
  // The panel stays open over the new map.
  await expect(page.getByTestId(PANEL)).toBeVisible();
});

test('Enter walks the results across two maps, Shift+Enter comes back, Escape closes', async ({ page, api }) => {
  const project = await layOut(api, 'SRW');
  await openMap(page, project.mapId, project.entry);
  await openByShortcut(page);
  await typeWord(page, 'formulaire');
  await expect(hitsOf(page)).toHaveCount(2);

  // The first result is on this map (the document of Saisir), the second on the detailed one (the step Remplir).
  await page.keyboard.press('Enter');
  await expect(positionOf(page)).toHaveText('1 sur 2');
  await expect(stepOf(page, project.enter)).toHaveClass(/is-selected/);
  await page.keyboard.press('Enter');
  await expect(positionOf(page)).toHaveText('2 sur 2');
  await expect(page.getByTestId('breadcrumb-level-1')).toHaveAttribute('aria-current', 'page');
  await expect(stepOf(page, project.fill)).toHaveClass(/is-selected/);
  // The field holds the focus again after the change of map: the next key is the field's.
  await expect(page.getByTestId(FIELD)).toBeFocused();
  await page.keyboard.press('Shift+Enter');
  await expect(positionOf(page)).toHaveText('1 sur 2');
  await expect(page.getByTestId('breadcrumb-level-0')).toHaveAttribute('aria-current', 'page');
  await expect(stepOf(page, project.enter)).toHaveClass(/is-selected/);

  await page.keyboard.press('Escape');
  await expect(page.getByTestId(PANEL)).toHaveCount(0);
});

test('"equipe" without its accent finds "Planning d’équipe" and "Prévenir l’équipe"', async ({ page, api }) => {
  const project = await layOut(api, 'SRA');
  await openMap(page, project.mapId, project.entry);
  await openByShortcut(page);
  await typeWord(page, 'equipe');
  await expect(hitsOf(page)).toHaveCount(2);
  await expect(hitsOf(page).filter({ hasText: /Planning d.équipe/ })).toHaveCount(1);
  await expect(hitsOf(page).filter({ hasText: /Prévenir l.équipe/ })).toHaveCount(1);
  // The word is marked as it was written in the project, accent included.
  await expect(page.getByTestId(PANEL).locator('mark')).toHaveText(['équipe', 'équipe']);
  // Capitals and accents typed give the same two results.
  await typeWord(page, 'ÉQUIPE');
  await expect(hitsOf(page)).toHaveCount(2);
});

test('two words in any order find the one text that holds both, each word marked, and the home field keeps the project that holds both', async ({ page, api }) => {
  const project = await layOut(api, 'SRM');
  await openMap(page, project.mapId, project.entry);
  await openByShortcut(page);
  // « Planning d’équipe » holds « equipe » but not « prevenir »: only « Prévenir l’équipe » is found (R9).
  await typeWord(page, 'EQUIPE prevenir');
  await expect(hitsOf(page)).toHaveCount(1);
  await expect(page.getByTestId('search-hit-0')).toContainText('Prévenir l’équipe');
  // Each word found is marked as it is written in the project (R9b).
  await expect(page.getByTestId('search-hit-0').locator('mark')).toHaveText(['Prévenir', 'équipe']);
  // A step found by its name is the step itself: no line of « its step » under it (ADR 110, `SearchHitView`: only a
  // document, a tool, a pain point and an action carry the code of their step). The result leads to that step.
  await expect(page.getByTestId('search-hit-0').locator('.search-hit-step')).toHaveCount(0);
  await page.getByTestId('search-hit-0').click();
  await expect(page.getByTestId('breadcrumb-level-1')).toHaveAttribute('aria-current', 'page');
  await expect(stepOf(page, project.warn)).toHaveClass(/is-selected/);

  await api.project('HFM', 'Congés payés');
  await page.goto('/');
  const filter = page.getByTestId('home-filter');
  await filter.fill('payes CONGES');
  await expect(page.getByTestId('project-HFM')).toBeVisible();
  await expect(page.getByTestId('project-SRM')).toHaveCount(0);
  await filter.fill('conges recherche');
  await expect(page.getByTestId('home-filter-none')).toHaveText('Aucun projet ne correspond.');
});

test('the first Ctrl+F is taken by the page, a second one in the field is left to the browser and changes nothing', async ({ page, api }) => {
  const project = await layOut(api, 'SRB');
  await openMap(page, project.mapId, project.entry);
  await recordCtrlF(page);
  await openByShortcut(page);
  await typeWord(page, 'equipe');
  await expect(hitsOf(page)).toHaveCount(2);

  await page.keyboard.press('Control+f');
  // The first press was cancelled by the page; the second, typed in a field, is left alone.
  expect(await ctrlFSeen(page)).toEqual([true, false]);
  await expect(page.getByTestId(PANEL)).toBeVisible();
  await expect(page.getByTestId(FIELD)).toHaveValue('equipe');
  await expect(page.getByTestId(FIELD)).toBeFocused();
  await expect(hitsOf(page)).toHaveCount(2);
});

test('the home field filters the projects by code or by name, accents and capitals aside', async ({ page, api }) => {
  await api.project('HFA', 'Congés payés');
  await api.project('HFB', 'Achats');
  await page.goto('/');
  await expect(page.getByTestId('project-HFA')).toBeVisible();
  await expect(page.getByTestId('project-HFB')).toBeVisible();

  const filter = page.getByTestId('home-filter');
  await filter.fill('CONGES');
  await expect(page.getByTestId('project-HFA')).toBeVisible();
  await expect(page.getByTestId('project-HFB')).toHaveCount(0);
  await expect(page.locator('.home-col-head .count')).toHaveText('1 sur 2');

  // By code.
  await filter.fill('hfb');
  await expect(page.getByTestId('project-HFB')).toBeVisible();
  await expect(page.getByTestId('project-HFA')).toHaveCount(0);

  // Nothing matches.
  await filter.fill('zzzz');
  await expect(page.getByTestId('home-filter-none')).toHaveText('Aucun projet ne correspond.');

  // Escape empties the field and gives the list back.
  await filter.press('Escape');
  await expect(filter).toHaveValue('');
  await expect(page.getByTestId('project-HFA')).toBeVisible();
  await expect(page.getByTestId('project-HFB')).toBeVisible();
  await expect(page.getByTestId('home-filter-none')).toHaveCount(0);
});

test('a reader has the button and Ctrl+F', async ({ page, api }) => {
  const project = await layOut(api, 'SRR');
  await readAs(page, api, project.id);
  await openMap(page, project.mapId, project.entry);

  // The button of the Display zone.
  const button = page.getByTestId('ribbon-find');
  await expect(button).toBeVisible();
  await expect(button).toBeEnabled();
  await button.click();
  await expect(page.getByTestId(PANEL)).toBeVisible();
  await expect(page.getByTestId(FIELD)).toBeFocused();
  await typeWord(page, 'planning');
  await expect(hitsOf(page)).toHaveCount(1);
  await page.keyboard.press('Escape');
  await expect(page.getByTestId(PANEL)).toHaveCount(0);

  // Ctrl+F from the map, then a result opens the detailed map.
  await openByShortcut(page);
  await typeWord(page, 'planning');
  await page.getByTestId('search-hit-0').click();
  await expect(page.getByTestId('breadcrumb-level-1')).toHaveAttribute('aria-current', 'page');
  await expect(stepOf(page, project.fill)).toHaveClass(/is-selected/);
});
