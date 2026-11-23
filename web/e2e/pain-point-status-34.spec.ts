// pain-point-status-34.spec.ts
// Journeys of the status of a pain point of the 3.4 (lot 4b, ADR 116): the status button of the sheet and of the panel « Plan d'actions » and its list, its help on every choice, the keyboard (the arrows write nothing), « Undo », the day of the change, a reader, two pages from the same version in server mode, the package and the .pmap copy, a pain point without a document on the sheet of its step (R10).

import { createHash } from 'node:crypto';
import { readFileSync } from 'node:fs';
import { strFromU8, strToU8, unzipSync, zipSync } from 'fflate';
import type { Locator, Page } from '@playwright/test';
import type { components } from '../src/api/generated/schema.ts';
import type { Api } from './support/api.ts';
import { expect, openMap, openSheetOf, stepOf, test } from './support/journey.ts';
import { MODE } from './support/run.ts';
import { fileEntry } from './support/file-menu.ts';

type ActionPlanView = components['schemas']['ActionPlanView'];
type Line = components['schemas']['ActionPlanLine'];
type Status = 'reported' | 'scored' | 'in_progress' | 'implemented' | 'adopted' | 'dropped';

/** The day of the browser: the clock of the page is fixed on it, the day of a change comes from the host. */
const DAY = new Date(2026, 11, 1, 12, 0, 0);

const PAIN_FORM = 'Champs ressaisis à la main';
const PAIN_PLANNING = 'Planning illisible';
const PAIN_NOTICE = 'Avis de passage perdus';

/** The five choices of a list of a pain point reported, each with its help on the same line. */
const CHOICES = [
  'Signalé — problème repéré, action à prévoir',
  'En cours — action en cours de mise en œuvre',
  'Réalisé — action faite, résultat à vérifier',
  'Adopté — vérifié, c’est la nouvelle façon de faire',
  'Abandonné — on décide de ne pas le traiter',
];
const SCORED_CHOICE = 'Pris en compte — irritant évalué, action à décider';
/** The entry of each status in a list, and what its button shows. */
const CHOICE: Record<Status, string> = {
  reported: CHOICES[0] ?? '', scored: SCORED_CHOICE, in_progress: CHOICES[1] ?? '', implemented: CHOICES[2] ?? '', adopted: CHOICES[3] ?? '', dropped: CHOICES[4] ?? '',
};
const SHOWN: Record<Status, string> = { reported: 'Signalé', scored: 'Pris en compte', in_progress: 'En cours', implemented: 'Réalisé', adopted: 'Adopté', dropped: 'Abandonné' };

/** What the project holds: the codes of its steps and the three pain points, their text, and the project itself. */
interface Project {
  id: string;
  mapId: string;
  /** The steps: the first one, the producer of the form and the planning, the receiver of both and producer of the notice, the receiver of the notice. */
  start: string;
  enter: string;
  check: string;
  archive: string;
}

/**
 * A document produced by a step, its identifier. The answer is the sheet of the step with all its outputs: the one made
 * is found by its name (« Saisir la demande » produces two documents, the first output is not the one just made).
 */
async function output(api: Api, stepId: string, name: string): Promise<string> {
  const made = await api.send<{ outputs: { document: { id: string; name: string } }[] }>('POST', `/api/steps/${stepId}/documents`, {
    direction: 'output', name, location: null, wait_hours: null, share_confirmed: null,
  });
  const document = made.outputs.find((candidate) => candidate.document.name === name)?.document;
  if (document === undefined) throw new Error(`No output « ${name} » on the step after its creation.`);
  return document.id;
}

/** The use of a document by the step that receives it, with its pain point (a first use goes with the version 0). */
async function receive(api: Api, stepId: string, documentId: string, pain: string, hours: number): Promise<void> {
  await api.send('PUT', `/api/steps/${stepId}/uses/${documentId}`, {
    wait_hours: null, percent_complete_accurate: null, pain_point: pain, lost_hours_per_month: hours,
  }, 0);
}

/**
 * The case, built by the API (the server mode has no sample): « Besoin reçu », « Saisir la demande », « Valider la
 * demande », « Archiver ». « Saisir » produces the form and the planning, « Valider » receives both (2 h and 3,5 h) and
 * produces the notice, « Archiver » receives it (1 h). Three pain points reported, 6,5 h open.
 */
async function layOut(api: Api, code: string): Promise<Project> {
  const { id, mapId } = await api.project(code, 'Statuts', 'fr', 'confidential');
  const lane = (await api.map(mapId)).lanes[0]?.id ?? '';
  const start = await api.step(mapId, lane, 0, 'Besoin reçu', 'start');
  const enter = await api.step(mapId, lane, 1, 'Saisir la demande');
  const check = await api.step(mapId, lane, 2, 'Valider la demande');
  const archive = await api.step(mapId, lane, 3, 'Archiver');
  await api.flow(mapId, start, enter);
  await api.flow(mapId, enter, check);
  await api.flow(mapId, check, archive);
  await receive(api, check, await output(api, enter, 'Formulaire de demande'), PAIN_FORM, 2);
  await receive(api, check, await output(api, enter, 'Planning d’équipe'), PAIN_PLANNING, 3.5);
  await receive(api, archive, await output(api, check, 'Avis de passage'), PAIN_NOTICE, 1);
  const codes = new Map((await api.map(mapId)).steps.map((step) => [step.id, step.code]));
  const of = (stepId: string): string => codes.get(stepId) ?? '';
  return { id, mapId, start: of(start), enter: of(enter), check: of(check), archive: of(archive) };
}

const planOf = (api: Api, mapId: string): Promise<ActionPlanView> => api.get<ActionPlanView>(`/api/maps/${mapId}/action-plan`);

/** The line of a pain point of the plan of the map, read now (its version is the one to send). */
async function lineOf(api: Api, mapId: string, description: string): Promise<Line> {
  const line = (await planOf(api, mapId)).lines.find((candidate) => candidate.description === description);
  if (line === undefined) throw new Error(`No pain point « ${description} » in the plan.`);
  return line;
}

/** A status written by the API, as the host lets it be written by the person who fills the sheet where it is chosen. */
async function setStatus(api: Api, mapId: string, description: string, status: Status): Promise<void> {
  const line = await lineOf(api, mapId, description);
  await api.send('PUT', `/api/steps/${line.status_step_id}/pain-points/${line.pain_point_id}/status`, { status }, line.version);
}

const panelOf = (page: Page) => page.getByTestId('plan-panel');
const linesOf = (page: Page) => panelOf(page).locator('[data-testid^="plan-line-"]');
const lineIn = (page: Page, code: string) => page.getByTestId(`plan-line-${code}`);
/** The status button of a line of the panel. */
const listOf = (page: Page, code: string) => page.getByTestId(`plan-status-of-${code}`);
const filterOpen = (page: Page) => page.getByTestId('plan-filter-open');
const totalOf = (page: Page) => page.getByTestId('plan-total');
const hours = (value: string): string => `${value} h perdues par mois sur les irritants ouverts`;

/** The codes of the lines shown, in their order. */
const codesShown = (page: Page): Promise<string[]> =>
  linesOf(page).evaluateAll((lines) => lines.map((line) => (line.getAttribute('data-testid') ?? '').slice('plan-line-'.length)));

/** The panel opened by the button of the Display zone, over the map shown. */
async function openPanel(page: Page): Promise<void> {
  await page.getByTestId('ribbon-plan').click();
  await expect(panelOf(page)).toBeVisible();
  await expect(totalOf(page)).toBeVisible();
}

/** The main map opened with the panel on it. */
async function planOn(page: Page, project: Project): Promise<void> {
  await openMap(page, project.mapId, project.check);
  await openPanel(page);
}

/** The answer of the host to a status written, awaited before the gesture that asks it. */
const statusWritten = (page: Page) =>
  page.waitForResponse((response) => response.request().method() === 'PUT' && /\/api\/steps\/[^/]+\/pain-points\/[^/]+\/status$/.test(response.url()));

/** The list a status button opened (one at a time). */
const menuOf = (page: Page) => page.getByRole('menu');

/** A status chosen as a person does with the mouse: the button opens the list, a click on the entry; the answer of the host awaited. */
async function choose(page: Page, button: Locator, status: Status): Promise<number> {
  const written = statusWritten(page);
  await button.click();
  await menuOf(page).getByRole('menuitemradio', { name: CHOICE[status], exact: true }).click();
  return (await written).status();
}

/**
 * The choices of a status button as the person reads them, each by its name and its help (the help seen on its own line),
 * the one of the moment marked; the list closed again by Escape, which gives the focus back to the button.
 */
async function choicesOf(page: Page, button: Locator): Promise<{ names: string[]; marked: string[] }> {
  await button.click();
  const entries = menuOf(page).getByRole('menuitemradio');
  await expect(entries.first().locator('.menu-help')).toBeVisible();
  const read = await entries.evaluateAll((items) => items.map((item) => [item.getAttribute('aria-label') ?? '', item.getAttribute('aria-checked') ?? '']));
  await page.keyboard.press('Escape');
  await expect(menuOf(page)).toHaveCount(0);
  await expect(button).toBeFocused();
  return { names: read.map(([name]) => name ?? ''), marked: read.filter(([, checked]) => checked === 'true').map(([name]) => name ?? '') };
}

/** The status a button shows, alone, and its name, which holds it. */
async function shows(button: Locator, status: Status): Promise<void> {
  await expect(button).toHaveText(SHOWN[status]);
  await expect(button).toHaveAccessibleName(new RegExp(` : ${SHOWN[status]}$`));
}

/** The writes of a status the page asks, counted from now. */
function countWrites(page: Page): () => number {
  let count = 0;
  page.on('request', (request) => {
    if (request.method() === 'PUT' && /\/api\/steps\/[^/]+\/pain-points\/[^/]+\/status$/.test(request.url())) count += 1;
  });
  return () => count;
}

/** The day of an instant as the browser shows it, by the same calendar as the line of the panel (never cut from the universal text). */
const dayOf = (page: Page, instant: string): Promise<string> =>
  page.evaluate((iso) => new Date(iso).toLocaleDateString('fr', { day: '2-digit', month: '2-digit', year: 'numeric' }), instant);

/** The status button of the sheet of a pain point, by its name, which begins with the pain point. */
const sheetList = (page: Page, code: string) => page.getByRole('button', { name: new RegExp(`^État de l’irritant ${code} : `) });

test.beforeEach(async ({ page }) => {
  await page.clock.setFixedTime(DAY);
});

test('the status chosen in the sheet: the mark leaves the step, the line leaves « Open » in the panel and the total of the hours drops', async ({ page, api }) => {
  const project = await layOut(api, 'PSA');
  const notice = await lineOf(api, project.mapId, PAIN_NOTICE);
  await openMap(page, project.mapId, project.check);
  // The notice is raised on « Valider la demande »: its flame is there, with the two documents of the step.
  await expect(stepOf(page, project.check).getByTestId('picto-pain-points')).toBeVisible();

  await openSheetOf(page, project.archive);
  const list = sheetList(page, notice.code);
  await shows(list, 'reported');
  expect(await choicesOf(page, list)).toEqual({ names: CHOICES, marked: [CHOICES[0]] });
  expect(await choose(page, list, 'implemented')).toBe(200);
  await shows(list, 'implemented');
  await expect(list).toBeFocused();
  await page.keyboard.press('Escape');
  await expect(page.getByTestId('sheet-panel')).toHaveCount(0);
  await expect(stepOf(page, project.check).getByTestId('picto-pain-points')).toHaveCount(0);

  await openPanel(page);
  await expect(totalOf(page)).toHaveText(hours('5,5'));
  await expect.poll(() => codesShown(page)).toHaveLength(2);
  await expect(lineIn(page, notice.code)).toHaveCount(0);
  await filterOpen(page).click();
  await expect(lineIn(page, notice.code)).toContainText('réalisé');
  await expect(totalOf(page)).toHaveText(hours('5,5'));
  expect((await lineOf(api, project.mapId, PAIN_NOTICE)).status).toBe('implemented');
});

test('every choice of the list shows its help on its own line, in the sheet and in the panel; a status « scored » is offered second', async ({ page, api }) => {
  const project = await layOut(api, 'PSB');
  await setStatus(api, project.mapId, PAIN_PLANNING, 'scored');
  const form = await lineOf(api, project.mapId, PAIN_FORM);
  const planning = await lineOf(api, project.mapId, PAIN_PLANNING);
  await openMap(page, project.mapId, project.check);

  await openSheetOf(page, project.check);
  expect(await choicesOf(page, sheetList(page, form.code))).toEqual({ names: CHOICES, marked: [CHOICES[0]] });
  const scored = [CHOICES[0] ?? '', SCORED_CHOICE, ...CHOICES.slice(1)];
  expect(await choicesOf(page, sheetList(page, planning.code))).toEqual({ names: scored, marked: [SCORED_CHOICE] });
  await shows(sheetList(page, planning.code), 'scored');
  await page.keyboard.press('Escape');
  await expect(page.getByTestId('sheet-panel')).toHaveCount(0);

  await openPanel(page);
  expect(await choicesOf(page, listOf(page, form.code))).toEqual({ names: CHOICES, marked: [CHOICES[0]] });
  expect(await choicesOf(page, listOf(page, planning.code))).toEqual({ names: scored, marked: [SCORED_CHOICE] });
  await shows(listOf(page, planning.code), 'scored');
});

test('from the keyboard, in the panel and in the sheet: the arrows then Escape write nothing; the arrows then Enter write one status', async ({ page, api }) => {
  const project = await layOut(api, 'PSK');
  const form = await lineOf(api, project.mapId, PAIN_FORM);
  const notice = await lineOf(api, project.mapId, PAIN_NOTICE);
  const writes = countWrites(page);
  await planOn(page, project);

  // The panel: Enter opens the list on the status of the moment, the arrows move, Escape closes it; nothing is written.
  const button = listOf(page, form.code);
  await button.focus();
  await page.keyboard.press('Enter');
  await expect(menuOf(page).getByRole('menuitemradio', { name: CHOICE.reported, exact: true })).toBeFocused();
  await page.keyboard.press('ArrowDown');
  await page.keyboard.press('ArrowDown');
  await expect(menuOf(page).getByRole('menuitemradio', { name: CHOICE.implemented, exact: true })).toBeFocused();
  await page.keyboard.press('Escape');
  await expect(menuOf(page)).toHaveCount(0);
  await expect(button).toBeFocused();
  await shows(button, 'reported');
  await expect(panelOf(page)).toBeVisible();
  expect(writes()).toBe(0);

  // Alt+Down opens it, the arrows move, Enter writes the one chosen, once.
  const written = statusWritten(page);
  await page.keyboard.press('Alt+ArrowDown');
  await page.keyboard.press('ArrowDown');
  await page.keyboard.press('ArrowDown');
  await page.keyboard.press('Enter');
  expect((await written).ok()).toBe(true);
  await expect(lineIn(page, form.code)).toHaveCount(0);
  await expect(page.getByTestId('plan-change')).toContainText(`${form.code} : réalisé.`);
  expect([writes(), (await lineOf(api, project.mapId, PAIN_FORM)).status]).toEqual([1, 'implemented']);
  await page.getByTestId('plan-close').click();

  // The sheet: Space opens the list, the arrows then Escape close it, the sheet stays open; nothing more is written.
  await openSheetOf(page, project.archive);
  const list = sheetList(page, notice.code);
  await list.focus();
  await page.keyboard.press('Space');
  await page.keyboard.press('ArrowDown');
  await page.keyboard.press('Escape');
  await expect(menuOf(page)).toHaveCount(0);
  await expect(page.getByTestId('sheet-panel')).toBeVisible();
  await expect(list).toBeFocused();
  expect(writes()).toBe(1);
  // Then Alt+Down, three times Down and Enter: adopted, written once.
  const adopted = statusWritten(page);
  await page.keyboard.press('Alt+ArrowDown');
  for (let step = 0; step < 3; step += 1) await page.keyboard.press('ArrowDown');
  await page.keyboard.press('Enter');
  expect((await adopted).ok()).toBe(true);
  await shows(list, 'adopted');
  expect([writes(), (await lineOf(api, project.mapId, PAIN_NOTICE)).status]).toEqual([2, 'adopted']);
});

test('from the panel, « implemented » then « Undo »: the line leaves « Open » and the hours with it, the message says it, Undo takes the focus and brings the status before back', async ({ page, api }) => {
  const project = await layOut(api, 'PSC');
  const form = await lineOf(api, project.mapId, PAIN_FORM);
  const planning = await lineOf(api, project.mapId, PAIN_PLANNING);
  const notice = await lineOf(api, project.mapId, PAIN_NOTICE);
  await planOn(page, project);
  await expect(totalOf(page)).toHaveText(hours('6,5'));
  // The form and the planning are raised on the same step: two flames.
  await expect(stepOf(page, project.enter).getByTestId('picto-pain-points')).toHaveText('2');

  expect(await choose(page, listOf(page, form.code), 'implemented')).toBe(200);
  await expect.poll(() => codesShown(page)).toEqual([planning.code, notice.code]);
  await expect(totalOf(page)).toHaveText(hours('4,5'));
  const message = page.getByTestId('plan-change');
  await expect(message).toContainText(`${form.code} : réalisé.`);
  const undo = page.getByRole('button', { name: `Annuler : remettre ${form.code} à l’état « signalé »` });
  await expect(undo).toBeFocused();
  await expect(stepOf(page, project.enter).getByTestId('picto-pain-points')).toHaveText('1');

  const undone = statusWritten(page);
  await undo.click();
  expect((await undone).ok()).toBe(true);
  await expect.poll(() => codesShown(page)).toEqual([form.code, planning.code, notice.code]);
  await expect(totalOf(page)).toHaveText(hours('6,5'));
  await shows(listOf(page, form.code), 'reported');
  await expect(message).toContainText(`${form.code} : signalé.`);
  await expect(stepOf(page, project.enter).getByTestId('picto-pain-points')).toHaveText('2');
  expect((await lineOf(api, project.mapId, PAIN_FORM)).status).toBe('reported');
});

test('from the panel, « adopted » then « Undo »: the status before comes back, with the hours', async ({ page, api }) => {
  const project = await layOut(api, 'PSD');
  const planning = await lineOf(api, project.mapId, PAIN_PLANNING);
  await setStatus(api, project.mapId, PAIN_PLANNING, 'in_progress');
  await planOn(page, project);
  await shows(listOf(page, planning.code), 'in_progress');

  expect(await choose(page, listOf(page, planning.code), 'adopted')).toBe(200);
  await expect(lineIn(page, planning.code)).toHaveCount(0);
  await expect(totalOf(page)).toHaveText(hours('3'));
  await expect(page.getByTestId('plan-change')).toContainText(`${planning.code} : adopté.`);

  const undone = statusWritten(page);
  await page.getByRole('button', { name: `Annuler : remettre ${planning.code} à l’état « en cours »` }).click();
  expect((await undone).ok()).toBe(true);
  await shows(listOf(page, planning.code), 'in_progress');
  await expect(totalOf(page)).toHaveText(hours('6,5'));
  expect((await lineOf(api, project.mapId, PAIN_PLANNING)).status).toBe('in_progress');
});

test('a line dealt with says « since » and the day of the change on the calendar of the browser, once « Open » is released', async ({ page, api }) => {
  const project = await layOut(api, 'PSE');
  const form = await lineOf(api, project.mapId, PAIN_FORM);
  await planOn(page, project);
  expect(await choose(page, listOf(page, form.code), 'implemented')).toBe(200);
  await expect(lineIn(page, form.code)).toHaveCount(0);

  await filterOpen(page).click();
  const changed = (await lineOf(api, project.mapId, PAIN_FORM)).status_changed_at ?? '';
  expect(changed).not.toBe('');
  await expect(lineIn(page, form.code).getByTestId('plan-status')).toHaveText(`réalisé depuis le ${await dayOf(page, changed)}`);
  // A line still open says its status alone.
  const planning = await lineOf(api, project.mapId, PAIN_PLANNING);
  await expect(lineIn(page, planning.code).getByTestId('plan-status')).toHaveText('signalé');
});

test('a reader (simulated) reads the status in the sheet and in the panel, and has no status button', async ({ page, api }) => {
  const project = await layOut(api, 'PSR');
  await setStatus(api, project.mapId, PAIN_NOTICE, 'implemented');
  const notice = await lineOf(api, project.mapId, PAIN_NOTICE);
  const form = await lineOf(api, project.mapId, PAIN_FORM);
  // The role and the rights given to the page as the host gives them to a reader (the bench is an administrator; the
  // rights themselves are held by the tests of the host); read by the API of the journey, which carries the identity.
  await page.route(`**/api/projects/${project.id}`, async (route) => {
    await route.fulfill({ json: { ...(await api.get<components['schemas']['ProjectDetail']>(`/api/projects/${project.id}`)), role: 'reader' } });
  });
  await page.route(`**/api/maps/${project.mapId}/action-plan`, async (route) => {
    const plan = await planOf(api, project.mapId);
    await route.fulfill({ json: { ...plan, lines: plan.lines.map((line) => ({ ...line, can_fill: false })) } });
  });
  const sheetPath = `/api/steps/${notice.receiving_step_id ?? ''}/sheet`;
  await page.route(`**${sheetPath}`, async (route) => {
    if (route.request().method() !== 'GET') {
      await route.fallback();
      return;
    }
    const view = await api.get<components['schemas']['SheetView']>(sheetPath);
    await route.fulfill({ json: { ...view, permissions: { fill: false, validate: false, request_clarification: false, assign: false } } });
  });
  await openMap(page, project.mapId, project.check);

  // The sheet: the status is read on the line of the pain point, no status button.
  await openSheetOf(page, project.archive);
  await expect(page.getByTestId(`pain-point-${notice.pain_point_id}`)).toContainText(`${notice.code} · réalisé`);
  await expect(page.getByRole('button', { name: new RegExp('^État de l’irritant') })).toHaveCount(0);
  await page.keyboard.press('Escape');
  await expect(page.getByTestId('sheet-panel')).toHaveCount(0);

  // The panel: the open lines, then all of them; the status is read, nothing can be chosen.
  await openPanel(page);
  await expect(totalOf(page)).toHaveText(hours('5,5'));
  await expect.poll(() => codesShown(page)).toHaveLength(2);
  await expect(lineIn(page, form.code).getByTestId('plan-status')).toHaveText('signalé');
  await filterOpen(page).click();
  await expect(lineIn(page, notice.code).getByTestId('plan-status')).toContainText('réalisé depuis le');
  await expect(panelOf(page).locator('[data-testid^="plan-status-of-"]')).toHaveCount(0);
  await expect(page.getByTestId('plan-change')).toHaveCount(0);
});

test('in server mode, two pages from the same version: the second is refused, told it changed, and its plan is read again', async ({ page, api }) => {
  test.skip(MODE !== 'server', 'The concurrent write refused is the one of the server mode, where If-Match is required (ADR 116 point 4); the host tests hold the desktop mode.');
  const project = await layOut(api, 'PSS');
  const planning = await lineOf(api, project.mapId, PAIN_PLANNING);
  // Both pages read the plan, at the version of the pain point, before anyone writes.
  const other = await page.context().newPage();
  await other.clock.setFixedTime(DAY);
  await planOn(page, project);
  await openMap(other, project.mapId, project.check);
  await openPanel(other);
  await shows(listOf(other, planning.code), 'reported');

  expect(await choose(page, listOf(page, planning.code), 'in_progress')).toBe(200);
  // The second page still holds the version before: its choice is refused, said, and the plan read again shows the status the pain point has now.
  expect(await choose(other, listOf(other, planning.code), 'implemented')).toBe(409);
  await expect(other.getByTestId('toast-error')).toBeVisible();
  await shows(listOf(other, planning.code), 'in_progress');
  await expect(totalOf(other)).toHaveText(hours('6,5'));
  expect((await lineOf(api, project.mapId, PAIN_PLANNING)).status).toBe('in_progress');
  await other.close();
});

/** The package of the project exported from the File menu, its bytes. */
async function exportPackage(page: Page): Promise<{ name: string; bytes: Uint8Array }> {
  const download = page.waitForEvent('download');
  await page.getByTestId('header-file').click();
  await (await fileEntry(page, 'file.export')).click();
  await page.getByTestId('export-package-confirm').click();
  const file = await download;
  return { name: file.suggestedFilename(), bytes: new Uint8Array(readFileSync(await file.path())) };
}

/** The package imported from the home page as a new project under the code given, its main map opened; the map shown is returned. */
async function importPackage(page: Page, api: Api, file: { name: string; bytes: Uint8Array }, taken: string, code: string): Promise<string> {
  await page.goto('/');
  await page.getByTestId('home-import-package').click();
  await page.getByTestId('import-package-file').setInputFiles({ name: file.name, mimeType: 'application/octet-stream', buffer: Buffer.from(file.bytes) });
  await page.getByTestId('import-package-check').click();
  await expect(page.getByText(`Le code ${taken} est déjà celui d’un projet : en choisir un autre.`)).toBeVisible();
  await page.getByTestId('import-package-code').fill(code);
  await page.getByTestId('import-package-check').click();
  await expect(page.getByText(`Le projet prendra le code ${code.toUpperCase()}.`)).toBeVisible();
  // The project is listed once the host has answered the import, not before.
  const answered = page.waitForResponse((response) => response.request().method() === 'POST' && /\/api\/projects\/import\?/.test(response.url()));
  await page.getByTestId('import-package-confirm').click();
  expect((await answered).ok()).toBe(true);
  const imported = (await api.get<{ code: string; root_map_id: string }[]>('/api/projects')).find((candidate) => candidate.code === code.toUpperCase());
  if (imported === undefined) throw new Error(`No project ${code.toUpperCase()} after the import.`);
  await expect(page).toHaveURL(new RegExp(`/maps/${imported.root_map_id}`));
  return imported.root_map_id;
}

test('the status and its history go through the export and the opening of the package', async ({ page, api }) => {
  const project = await layOut(api, 'PSP');
  await setStatus(api, project.mapId, PAIN_FORM, 'in_progress');
  await setStatus(api, project.mapId, PAIN_FORM, 'implemented');
  await setStatus(api, project.mapId, PAIN_NOTICE, 'dropped');
  const source = await lineOf(api, project.mapId, PAIN_FORM);
  await openMap(page, project.mapId, project.check);

  const file = await exportPackage(page);
  // The history of the pain point in the package: its creation, then the two changes, in this order.
  const points = JSON.parse(strFromU8(unzipSync(file.bytes)['painpoints.json'] ?? new Uint8Array())) as
    { painPoints: { description: string; status: string; history: { statusBefore?: string; statusAfter: string }[] }[] };
  const exported = points.painPoints.find((candidate) => candidate.description === PAIN_FORM);
  expect(exported?.status).toBe('implemented');
  expect(exported?.history.map((record) => record.statusAfter)).toEqual(['reported', 'in_progress', 'implemented']);
  expect(exported?.history.map((record) => record.statusBefore ?? null)).toEqual([null, 'reported', 'in_progress']);

  const mapId = await importPackage(page, api, file, 'PSP', 'psq');
  const form = await lineOf(api, mapId, PAIN_FORM);
  expect([form.status, (await lineOf(api, mapId, PAIN_NOTICE)).status, (await lineOf(api, mapId, PAIN_PLANNING)).status]).toEqual(['implemented', 'dropped', 'reported']);
  // The instant of the last change is the one of the source.
  expect(Math.abs(new Date(form.status_changed_at ?? '').getTime() - new Date(source.status_changed_at ?? '').getTime())).toBeLessThan(1000);

  await openMap(page, mapId);
  await openPanel(page);
  await expect(totalOf(page)).toHaveText(hours('3,5'));
  await expect.poll(() => codesShown(page)).toEqual([(await lineOf(api, mapId, PAIN_PLANNING)).code]);
  await filterOpen(page).click();
  await expect(lineIn(page, form.code).getByTestId('plan-status')).toHaveText(`réalisé depuis le ${await dayOf(page, form.status_changed_at ?? '')}`);
  const notice = await lineOf(api, mapId, PAIN_NOTICE);
  await expect(lineIn(page, notice.code).getByTestId('plan-status')).toContainText('abandonné depuis le');
});

test('the status survives a .pmap copy with its sheets, opened as a new project', async ({ page, api }) => {
  const project = await layOut(api, 'PSM');
  await setStatus(api, project.mapId, PAIN_FORM, 'implemented');
  await setStatus(api, project.mapId, PAIN_PLANNING, 'in_progress');
  // The copy with its sheets, which carry the pain points: the File menu copy leaves them out by default (AR-93).
  const copy = JSON.stringify(await api.get<unknown>(`/api/projects/${project.id}/pmap?with_sheets=true`));
  expect(copy).toContain('"pain_point_status":"implemented"');
  expect(copy).toContain('"pain_point_status":"in_progress"');
  await openMap(page, project.mapId, project.check);

  await page.getByRole('button', { name: 'Fichier', exact: true }).click();
  await page.getByRole('menu', { name: 'Menu Fichier' }).getByRole('menuitem', { name: 'Ouvrir un fichier…' }).click();
  const dialog = page.getByRole('dialog', { name: 'Ouvrir un fichier' });
  await dialog.locator('input[type="file"]').setInputFiles({ name: 'copie.pmap', mimeType: 'application/json', buffer: Buffer.from(copy) });
  await dialog.getByRole('textbox', { name: 'Code court' }).fill('PSN');
  await dialog.getByRole('button', { name: 'Ouvrir', exact: true }).click();
  await expect.poll(() => new URL(page.url()).pathname.replace('/maps/', '')).not.toBe(project.mapId);
  const mapId = new URL(page.url()).pathname.replace('/maps/', '');

  // The reopened project has the statuses of its source and the hours they leave open (3,5 h and 1 h).
  expect((await lineOf(api, mapId, PAIN_FORM)).status).toBe('implemented');
  expect((await lineOf(api, mapId, PAIN_PLANNING)).status).toBe('in_progress');
  expect((await lineOf(api, mapId, PAIN_NOTICE)).status).toBe('reported');
  await openMap(page, mapId);
  await openPanel(page);
  await expect(totalOf(page)).toHaveText(hours('4,5'));
  const planning = await lineOf(api, mapId, PAIN_PLANNING);
  await shows(listOf(page, planning.code), 'in_progress');
  await expect(lineIn(page, (await lineOf(api, mapId, PAIN_FORM)).code)).toHaveCount(0);
});

/** A call of the API whose body or answer is a file, from the page (it carries the identity in both modes), its status and bytes. */
async function rawCall(page: Page, path: string, body: { type: string; bytes: number[] }): Promise<{ status: number; bytes: number[] }> {
  return page.evaluate(async ([address, type, bytes]) => {
    const response = await fetch(address, {
      method: 'POST', credentials: 'same-origin', headers: { 'X-Requested-With': 'XMLHttpRequest', 'Content-Type': type }, body: new Uint8Array(bytes),
    });
    return { status: response.status, bytes: [...new Uint8Array(await response.arrayBuffer())] };
  }, [path, body.type, body.bytes] as const);
}

/**
 * The package with no use carrying the pain point of this description any more, as a package written by hand brings it
 * (the data of the trials hold such ones): its use loses it, the manifest follows.
 */
function withoutItsUse(file: Uint8Array, description: string): Uint8Array {
  const entries = unzipSync(file);
  const text = (name: string): string => strFromU8(entries[name] ?? new Uint8Array());
  const code = (JSON.parse(text('painpoints.json')) as { painPoints: { code: string; description: string }[] }).painPoints
    .find((candidate) => candidate.description === description)?.code;
  if (code === undefined) throw new Error(`No pain point « ${description} » in the package.`);
  const documents = JSON.parse(text('documents.json')) as { documents: { uses: { painPoint?: string }[] }[] };
  for (const use of documents.documents.flatMap((document) => document.uses)) {
    if (use.painPoint === code) delete use.painPoint;
  }
  const changed = strToU8(JSON.stringify(documents));
  entries['documents.json'] = changed;
  const manifest = JSON.parse(text('manifest.json')) as { entries: { path: string; size: number; sha256: string }[] };
  manifest.entries = manifest.entries.map((entry) => entry.path === 'documents.json'
    ? { ...entry, size: changed.length, sha256: createHash('sha256').update(changed).digest('hex') } : entry);
  entries['manifest.json'] = strToU8(JSON.stringify(manifest));
  return zipSync(entries);
}

test('a pain point without a document, laid by the API: in « Irritants de l’étape » of the sheet of its step, made « implemented » there, it leaves « Open » in the panel (R10)', async ({ page, api }) => {
  const project = await layOut(api, 'PSD');
  await openMap(page, project.mapId, project.check);
  const exported = await rawCall(page, `/api/projects/${project.id}/export`, { type: 'application/json', bytes: [...strToU8(JSON.stringify({ without_people: false }))] });
  expect(exported.status).toBe(200);
  // The options of an import are all required in its address (`ExchangeApi`): the members of the package are not taken.
  const created = await rawCall(page, '/api/projects/import?code=PSE&with_members=false', { type: 'application/octet-stream', bytes: [...withoutItsUse(new Uint8Array(exported.bytes), PAIN_NOTICE)] });
  expect(created.status).toBe(201);
  const mapId = (JSON.parse(strFromU8(new Uint8Array(created.bytes))) as { root_map_id: string }).root_map_id;
  const notice = await lineOf(api, mapId, PAIN_NOTICE);
  // No sheet receives it: its status is chosen on the sheet of its own step, « Valider la demande ».
  expect([notice.receiving_step_id, notice.document_id, notice.status_step_id, notice.can_fill]).toEqual([null, null, notice.step_id, true]);

  await openMap(page, mapId, notice.step_code);
  await openSheetOf(page, notice.step_code);
  const sheet = page.getByTestId('sheet-panel');
  await expect(sheet.getByTestId('sheet-fold-pain_points')).toContainText('Irritants de l’étape');
  await expect(sheet.getByTestId('sheet-summary-pain_points')).toHaveText('1 irritant');
  const section = sheet.getByTestId('sheet-section-pain_points');
  await expect(section).toContainText(`${notice.code} · signalé : ${PAIN_NOTICE} · 1 h perdues par mois`);
  // Its description and its plan are read only: written through the use of a document only.
  await expect(section.getByRole('textbox')).toHaveCount(0);
  const list = sheetList(page, notice.code);
  await shows(list, 'reported');
  expect(await choose(page, list, 'implemented')).toBe(200);
  await shows(list, 'implemented');
  await expect(section).toContainText(`${notice.code} · réalisé : ${PAIN_NOTICE}`);
  await page.keyboard.press('Escape');
  await expect(page.getByTestId('sheet-panel')).toHaveCount(0);

  await openPanel(page);
  await expect(lineIn(page, notice.code)).toHaveCount(0);
  await filterOpen(page).click();
  await expect(lineIn(page, notice.code)).toContainText('réalisé');
  expect((await lineOf(api, mapId, PAIN_NOTICE)).status).toBe('implemented');
});
