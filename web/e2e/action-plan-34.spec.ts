// action-plan-34.spec.ts
// Journeys of the action plan of the 3.4 (lot 4, ADR 112): the panel of the Display zone over a project of two maps, the sorts and the filters, the late mark to the day on the clock of the browser, a line that leads to the step of the detailed map, « Edit the plan » through the sheet, the sheet « Plan d'actions » of the workbook, and a reader.

import { createHash } from 'node:crypto';
import { readFileSync } from 'node:fs';
import { createRequire } from 'node:module';
import type ExcelType from 'exceljs';
import { strFromU8, strToU8, unzipSync, zipSync } from 'fflate';
import type { Page } from '@playwright/test';
import type { components } from '../src/api/generated/schema.ts';
import type { Api } from './support/api.ts';
import { expect, openMap, stepOf, test } from './support/journey.ts';
import { fileEntry } from './support/file-menu.ts';

// exceljs is a CommonJS module: loaded as such in this ES module.
const Excel = createRequire(import.meta.url)('exceljs') as typeof ExcelType;

type ActionPlanView = components['schemas']['ActionPlanView'];

/** The day of the browser unless a journey sets another: the due dates of the project are all after it. */
const DAY = new Date(2026, 11, 1, 12, 0, 0);

/** The codes of what the project holds: its pain points and the steps they are raised on (the producers of the documents). */
interface Project {
  id: string;
  mapId: string;
  subMapId: string;
  /** The pain points in the order of the project: main map, then the detailed map. */
  form: string;
  planning: string;
  notice: string;
  /** The step that produces each document: where the pain point is raised. */
  enter: string;
  fill: string;
  notify: string;
}

const PAIN_FORM = 'Champs ressaisis à la main';
const PAIN_PLANNING = 'Planning illisible';
const PAIN_NOTICE = 'Avis de passage perdus';

/** A document produced by a step, its identifier. */
async function output(api: Api, stepId: string, name: string): Promise<string> {
  const made = await api.send<{ outputs: { document: { id: string } }[] }>('POST', `/api/steps/${stepId}/documents`, {
    direction: 'output', name, location: null, wait_hours: null, share_confirmed: null,
  });
  return made.outputs[0]?.document.id ?? '';
}

/** The use of a document by the step that receives it, with a pain point and the plan given (a first use goes with the version 0). */
async function receive(api: Api, stepId: string, documentId: string, pain: string, hours: number, plan: { action?: string; owner?: string; due_date?: string } = {}): Promise<void> {
  await api.send('PUT', `/api/steps/${stepId}/uses/${documentId}`, {
    wait_hours: null, percent_complete_accurate: null, pain_point: pain, lost_hours_per_month: hours, ...plan,
  }, 0);
}

/**
 * The case, built by the API (the server mode has no sample). The main map: « Besoin reçu », « Saisir la demande »
 * (detailed), « Valider la demande ». The detailed map: « Remplir le formulaire », « Prévenir l’équipe », « Contrôler ».
 * Three pain points, in the order of the project: 2 h with an action due on 15/12/2026; 3,5 h without action; 1 h with an
 * action due on 20/12/2026. All three are signalled, so open.
 */
async function layOut(api: Api, code: string): Promise<Project> {
  const { id, mapId } = await api.project(code, 'Plan', 'fr', 'confidential');
  const lane = (await api.map(mapId)).lanes[0]?.id ?? '';
  const start = await api.step(mapId, lane, 0, 'Besoin reçu', 'start');
  const enter = await api.step(mapId, lane, 1, 'Saisir la demande');
  const check = await api.step(mapId, lane, 2, 'Valider la demande');
  await api.flow(mapId, start, enter);
  await api.flow(mapId, enter, check);
  const version = (await api.map(mapId)).steps.find((candidate) => candidate.id === enter)?.version;
  const subMapId = (await api.send<{ id: string | null }>('POST', `/api/steps/${enter}/detail`, undefined, version)).id ?? '';
  const subLane = (await api.map(subMapId)).lanes[0]?.id ?? '';
  const fill = await api.step(subMapId, subLane, 0, 'Remplir le formulaire');
  const notify = await api.step(subMapId, subLane, 1, 'Prévenir l’équipe');
  const control = await api.step(subMapId, subLane, 2, 'Contrôler');
  await api.flow(subMapId, fill, notify);
  await api.flow(subMapId, notify, control);
  await receive(api, check, await output(api, enter, 'Formulaire de demande'), PAIN_FORM, 2, { action: 'Rappel automatique', owner: 'Service RH', due_date: '2026-12-15' });
  await receive(api, notify, await output(api, fill, 'Planning d’équipe'), PAIN_PLANNING, 3.5);
  await receive(api, control, await output(api, notify, 'Avis de passage'), PAIN_NOTICE, 1, { action: 'Mettre une alerte', due_date: '2026-12-20' });
  const plan = await api.get<ActionPlanView>(`/api/maps/${mapId}/action-plan`);
  const line = (description: string) => plan.lines.find((candidate) => candidate.description === description);
  return {
    id, mapId, subMapId, form: line(PAIN_FORM)?.code ?? '', planning: line(PAIN_PLANNING)?.code ?? '', notice: line(PAIN_NOTICE)?.code ?? '',
    enter: line(PAIN_FORM)?.step_code ?? '', fill: line(PAIN_PLANNING)?.step_code ?? '', notify: line(PAIN_NOTICE)?.step_code ?? '',
  };
}

const panelOf = (page: Page) => page.getByTestId('plan-panel');
const linesOf = (page: Page) => panelOf(page).locator('[data-testid^="plan-line-"]');
const lineOf = (page: Page, code: string) => page.getByTestId(`plan-line-${code}`);
const filterOf = (page: Page, id: 'open' | 'late' | 'no-action') => page.getByTestId(`plan-filter-${id}`);

/** The codes of the lines shown, in their order. */
const codesShown = (page: Page): Promise<string[]> =>
  linesOf(page).evaluateAll((lines) => lines.map((line) => (line.getAttribute('data-testid') ?? '').slice('plan-line-'.length)));

/** The panel opened by the button of the Display zone, over the map shown. */
async function openPanel(page: Page): Promise<void> {
  await page.getByTestId('ribbon-plan').click();
  await expect(panelOf(page)).toBeVisible();
  await expect(page.getByTestId('plan-total')).toBeVisible();
}

/** The main map of a project shown at a day and an hour of the clock of the browser, the panel open on it. */
async function planAt(page: Page, project: Project, at: Date): Promise<void> {
  await page.clock.setFixedTime(at);
  await openMap(page, project.mapId, project.enter);
  await openPanel(page);
}

test.beforeEach(async ({ page }) => {
  await page.clock.setFixedTime(DAY);
});

test('the panel lists the pain points of both levels and the total of the open hours, in the order of the project; the button opens and closes it', async ({ page, api }) => {
  const project = await layOut(api, 'PLA');
  await openMap(page, project.mapId, project.enter);
  const button = page.getByTestId('ribbon-plan');
  await expect(button).toBeVisible();
  await expect(button).toHaveAttribute('aria-pressed', 'false');
  await openPanel(page);
  await expect(button).toHaveAttribute('aria-pressed', 'true');

  // Two lines of the main level and the detailed one, 6,5 h, none late; the order is that of the project.
  await expect(page.getByTestId('plan-total')).toHaveText('6,5 h perdues par mois sur les irritants ouverts');
  await expect(panelOf(page).getByRole('status')).toHaveText('En retard : 0 · Irritants montrés : 3 sur 3');
  await expect.poll(() => codesShown(page)).toEqual([project.form, project.planning, project.notice]);
  await expect(filterOf(page, 'open')).toHaveAttribute('aria-pressed', 'true');
  await expect(filterOf(page, 'late')).toHaveAttribute('aria-pressed', 'false');
  await expect(page.getByTestId('plan-sort')).toHaveValue('project');

  // The line of the main level carries its plan; the one of the detailed level its map and its step.
  const form = lineOf(page, project.form);
  await expect(form).toContainText(PAIN_FORM);
  await expect(form).toContainText('Action : Rappel automatique');
  await expect(form).toContainText('Responsable : Service RH');
  await expect(form).toContainText('Échéance : 15/12/2026');
  await expect(form).toContainText('2 h perdues par mois');
  await expect(lineOf(page, project.planning)).toContainText(project.fill);
  await expect(lineOf(page, project.planning)).toContainText('Remplir le formulaire');
  await expect(lineOf(page, project.planning)).toContainText('3,5 h perdues par mois');
  // The animator may write the three plans.
  await expect(panelOf(page).locator('[data-testid^="plan-edit-"]')).toHaveCount(3);

  await button.click();
  await expect(panelOf(page)).toHaveCount(0);
  await expect(button).toHaveAttribute('aria-pressed', 'false');
});

test('"Without action" keeps one line and says so; the sorts reorder the lines, the order of the project comes back', async ({ page, api }) => {
  const project = await layOut(api, 'PLB');
  await openMap(page, project.mapId, project.enter);
  await openPanel(page);

  await filterOf(page, 'no-action').click();
  await expect(filterOf(page, 'no-action')).toHaveAttribute('aria-pressed', 'true');
  await expect.poll(() => codesShown(page)).toEqual([project.planning]);
  await expect(lineOf(page, project.planning)).toContainText('Aucune action décidée');
  await expect(panelOf(page).getByRole('status')).toHaveText('En retard : 0 · Irritants montrés : 1 sur 3');
  // The total counts the open pain points whatever the filters.
  await expect(page.getByTestId('plan-total')).toHaveText('6,5 h perdues par mois sur les irritants ouverts');

  // The filters narrow together: nothing is late and without action.
  await filterOf(page, 'late').click();
  await expect(linesOf(page)).toHaveCount(0);
  await expect(panelOf(page).getByText('Aucun irritant ne correspond aux filtres.')).toBeVisible();
  await filterOf(page, 'late').click();
  await filterOf(page, 'no-action').click();
  await expect.poll(() => codesShown(page)).toEqual([project.form, project.planning, project.notice]);

  // Hours lost, the greatest first; due date, the soonest first and those without one last; then the order of the project.
  await page.getByTestId('plan-sort').selectOption('hours');
  await expect.poll(() => codesShown(page)).toEqual([project.planning, project.form, project.notice]);
  await page.getByTestId('plan-sort').selectOption('due');
  await expect.poll(() => codesShown(page)).toEqual([project.form, project.notice, project.planning]);
  await page.getByTestId('plan-sort').selectOption('project');
  await expect.poll(() => codesShown(page)).toEqual([project.form, project.planning, project.notice]);
});

test('a pain point is late from the day after its due date on the calendar of the browser, at midnight to the minute', async ({ page, api }) => {
  const project = await layOut(api, 'PLC');
  // Due on 15/12 and on 20/12: late on the 16th at 0:30 and on the 21st at 0:30, not the evening before (local time, never universal).
  const cases: readonly (readonly [Date, string[]])[] = [
    [new Date(2026, 11, 15, 23, 30), []],
    [new Date(2026, 11, 16, 0, 30), [project.form]],
    [new Date(2026, 11, 20, 23, 30), [project.form]],
    [new Date(2026, 11, 21, 0, 30), [project.form, project.notice]],
  ];
  for (const [at, late] of cases) {
    await planAt(page, project, at);
    await expect(page.getByTestId('plan-late')).toHaveCount(late.length);
    await expect(panelOf(page).getByRole('status')).toHaveText(`En retard : ${String(late.length)} · Irritants montrés : 3 sur 3`);
    for (const code of late) await expect(lineOf(page, code).getByTestId('plan-late')).toHaveText('En retard');
    await filterOf(page, 'late').click();
    await expect.poll(() => codesShown(page)).toEqual(late);
  }
});

test('a line of the main map chooses its step; a line of the detailed map opens that map with the step chosen, the panel still open', async ({ page, api }) => {
  const project = await layOut(api, 'PLD');
  await openMap(page, project.mapId, project.enter);
  await openPanel(page);

  await page.getByTestId(`plan-go-${project.form}`).click();
  await expect(stepOf(page, project.enter)).toHaveClass(/is-selected/);
  await expect(page.getByTestId('breadcrumb-level-0')).toHaveAttribute('aria-current', 'page');

  await page.getByTestId(`plan-go-${project.planning}`).click();
  await expect(page.getByTestId('breadcrumb-level-1')).toHaveAttribute('aria-current', 'page');
  await expect(stepOf(page, project.fill)).toHaveClass(/is-selected/);
  await expect(stepOf(page, project.fill)).toBeInViewport();
  await expect(panelOf(page)).toBeVisible();
  await expect(linesOf(page)).toHaveCount(3);

  // Back to the main map by a line of that level.
  await page.getByTestId(`plan-go-${project.form}`).click();
  await expect(page.getByTestId('breadcrumb-level-0')).toHaveAttribute('aria-current', 'page');
  await expect(stepOf(page, project.enter)).toHaveClass(/is-selected/);
});

test('Edit the plan opens the sheet on the pain point of the detailed map; a due date of yesterday and the line comes back late', async ({ page, api }) => {
  const project = await layOut(api, 'PLE');
  await openMap(page, project.mapId, project.enter);
  await openPanel(page);
  await expect(page.getByTestId('plan-late')).toHaveCount(0);

  // From the main map: the detailed map opens, then the sheet of the step that receives, the caret in the field of the document.
  await page.getByTestId(`plan-edit-${project.planning}`).click();
  await expect(page.getByTestId('breadcrumb-level-1')).toHaveAttribute('aria-current', 'page');
  await expect(page.getByTestId('sheet-panel')).toBeVisible();
  await expect(page.getByRole('textbox', { name: 'Irritant sur « Planning d’équipe »' })).toBeFocused();

  // Yesterday of the browser (its clock is fixed on 01/12/2026).
  const saved = () => page.waitForResponse((response) => response.request().method() === 'PUT' && /\/api\/steps\/[^/]+\/uses\//.test(response.url()));
  const action = page.getByRole('textbox', { name: 'Action décidée sur « Planning d’équipe »' });
  const written = saved();
  await action.fill('Refaire le planning');
  await action.press('Tab');
  expect((await written).ok()).toBe(true);
  const due = page.getByLabel('Échéance de l’action sur « Planning d’équipe »');
  const dueWritten = saved();
  await due.fill('2026-11-30');
  // Enter leaves the field (B-4-75); Tab would walk the day, the month, the year and the calendar of a date field of Chromium.
  await due.press('Enter');
  expect((await dueWritten).ok()).toBe(true);

  // The sheet closed, the panel is back, read again: the line holds the plan and the late mark.
  await page.keyboard.press('Escape');
  await expect(page.getByTestId('sheet-panel')).toHaveCount(0);
  await expect(panelOf(page)).toBeVisible();
  const line = lineOf(page, project.planning);
  await expect(line).toContainText('Action : Refaire le planning');
  await expect(line).toContainText('Échéance : 30/11/2026');
  await expect(line.getByTestId('plan-late')).toHaveText('En retard');
  await expect(panelOf(page).getByRole('status')).toHaveText('En retard : 1 · Irritants montrés : 3 sur 3');
  await filterOf(page, 'no-action').click();
  await expect(linesOf(page)).toHaveCount(0);
});

/** The file of the package patched: the pain point of this description done (in progress, then implemented), the manifest following. */
function markedDone(file: Buffer, description: string): Buffer {
  const entries = unzipSync(new Uint8Array(file));
  const text = (name: string): string => strFromU8(entries[name] ?? new Uint8Array());
  const points = JSON.parse(text('painpoints.json')) as { painPoints: { description: string; status: string; history: { statusBefore?: string; statusAfter: string; changedAt: string }[] }[] };
  const point = points.painPoints.find((candidate) => candidate.description === description);
  if (point === undefined) throw new Error(`No pain point « ${description} » in the package.`);
  const at = point.history[point.history.length - 1]?.changedAt ?? '';
  point.history.push({ statusBefore: 'reported', statusAfter: 'in_progress', changedAt: at }, { statusBefore: 'in_progress', statusAfter: 'implemented', changedAt: at });
  point.status = 'implemented';
  const changed = strToU8(JSON.stringify(points));
  entries['painpoints.json'] = changed;
  const manifest = JSON.parse(text('manifest.json')) as { entries: { path: string; size: number; sha256: string }[] };
  manifest.entries = manifest.entries.map((entry) => entry.path === 'painpoints.json'
    ? { ...entry, size: changed.length, sha256: createHash('sha256').update(changed).digest('hex') } : entry);
  entries['manifest.json'] = strToU8(JSON.stringify(manifest));
  return Buffer.from(zipSync(entries));
}

/**
 * A pain point done, as the host lets it be (no gesture of the application closes one): the project exported from the
 * File menu, the status changed in the package, then imported as a new project under another code, whose main map opens.
 * The pain point « Avis de passage perdus » is the one done; the 1 h it holds no longer counts.
 */
async function layOutDone(page: Page, api: Api, code: string, copy: string): Promise<Project> {
  const project = await layOut(api, code);
  await openMap(page, project.mapId, project.enter);
  const download = page.waitForEvent('download');
  await page.getByTestId('header-file').click();
  await (await fileEntry(page, 'file.export')).click();
  await page.getByTestId('export-package-confirm').click();
  const file = await download;
  const patched = markedDone(readFileSync(await file.path()), PAIN_NOTICE);
  await page.goto('/');
  await page.getByTestId('home-import-package').click();
  await page.getByTestId('import-package-file').setInputFiles({ name: file.suggestedFilename(), mimeType: 'application/octet-stream', buffer: patched });
  await page.getByTestId('import-package-check').click();
  await expect(page.getByText(`Le code ${code} est déjà celui d’un projet : en choisir un autre.`)).toBeVisible();
  await page.getByTestId('import-package-code').fill(copy);
  await page.getByTestId('import-package-check').click();
  await expect(page.getByText(`Le projet prendra le code ${copy.toUpperCase()}.`)).toBeVisible();
  // The project is listed once the host has answered the import, not before.
  const answered = page.waitForResponse((response) => response.request().method() === 'POST' && /\/api\/projects\/import\?/.test(response.url()));
  await page.getByTestId('import-package-confirm').click();
  expect((await answered).ok()).toBe(true);
  const imported = (await api.get<{ id: string; code: string; root_map_id: string }[]>('/api/projects')).find((candidate) => candidate.code === copy.toUpperCase());
  if (imported === undefined) throw new Error(`No project ${copy.toUpperCase()} after the import.`);
  const mapId = imported.root_map_id;
  await expect(page).toHaveURL(new RegExp(`/maps/${mapId}`));
  const plan = await api.get<ActionPlanView>(`/api/maps/${mapId}/action-plan`);
  const line = (description: string) => plan.lines.find((candidate) => candidate.description === description);
  return {
    ...project, id: imported.id, mapId, form: line(PAIN_FORM)?.code ?? '', planning: line(PAIN_PLANNING)?.code ?? '', notice: line(PAIN_NOTICE)?.code ?? '',
    enter: line(PAIN_FORM)?.step_code ?? '', fill: line(PAIN_PLANNING)?.step_code ?? '', notify: line(PAIN_NOTICE)?.step_code ?? '',
  };
}

test('"Open" hides the pain point done and its hours; without it the three lines are there and the total stays', async ({ page, api }) => {
  const project = await layOutDone(page, api, 'PLF', 'plg');
  await expect(stepOf(page, project.enter)).toBeVisible();
  await openPanel(page);

  await expect(filterOf(page, 'open')).toHaveAttribute('aria-pressed', 'true');
  await expect.poll(() => codesShown(page)).toEqual([project.form, project.planning]);
  await expect(page.getByTestId('plan-total')).toHaveText('5,5 h perdues par mois sur les irritants ouverts');
  await expect(panelOf(page).getByRole('status')).toHaveText('En retard : 0 · Irritants montrés : 2 sur 3');

  await filterOf(page, 'open').click();
  await expect.poll(() => codesShown(page)).toEqual([project.form, project.planning, project.notice]);
  await expect(lineOf(page, project.notice)).toContainText('réalisé');
  await expect(page.getByTestId('plan-total')).toHaveText('5,5 h perdues par mois sur les irritants ouverts');
  await expect(panelOf(page).getByRole('status')).toHaveText('En retard : 0 · Irritants montrés : 3 sur 3');
  // A done pain point is never late, whatever its due date (20/12/2026) and the day.
  await page.clock.setFixedTime(new Date(2026, 11, 28, 12, 0, 0));
  await page.reload();
  await openPanel(page);
  await filterOf(page, 'open').click();
  await expect(page.getByTestId('plan-late')).toHaveCount(1);
  await expect(lineOf(page, project.form).getByTestId('plan-late')).toBeVisible();
  await expect(lineOf(page, project.notice).getByTestId('plan-late')).toHaveCount(0);
});

test('the sheet "Plan d’actions" of the workbook of the root map holds the same lines in the same order as the panel in the order of the project', async ({ page, api }) => {
  const project = await layOutDone(page, api, 'PLH', 'pli');
  await expect(stepOf(page, project.enter)).toBeVisible();
  await openPanel(page);
  // All the lines, the done one too: the sheet keeps every line.
  await filterOf(page, 'open').click();
  const expected = [project.form, project.planning, project.notice];
  await expect.poll(() => codesShown(page)).toEqual(expected);
  const shown = await Promise.all(expected.map(async (code) => ({ code, text: (await lineOf(page, code).innerText()).replace(/\s+/g, ' ') })));
  // The panel in another sort: the sheet does not follow it.
  await page.getByTestId('plan-sort').selectOption('hours');
  await expect.poll(() => codesShown(page)).toEqual([project.planning, project.form, project.notice]);

  const download = page.waitForEvent('download');
  await page.getByTestId('header-file').click();
  await (await fileEntry(page, 'file.xlsx')).click();
  const file = await download;
  await expect(page.getByText('Export terminé : le fichier est dans le dossier Téléchargements.')).toBeVisible();
  const content = readFileSync(await file.path());
  const book = new Excel.Workbook();
  await book.xlsx.load(content.buffer.slice(content.byteOffset, content.byteOffset + content.byteLength));
  const sheet = book.getWorksheet('Plan d’actions');
  expect(sheet).toBeDefined();
  expect(sheet?.rowCount).toBe(expected.length + 1);
  expect((sheet?.getRow(1).values as unknown[]).slice(1)).toEqual(['Carte', 'Code', 'Étape', 'Couloir', 'Irritant', 'Description', 'Action', 'Responsable', 'Échéance', 'État', 'Heures perdues par mois']);
  const rows = [2, 3, 4].map((index) => (sheet?.getRow(index).values as unknown[]).slice(1));
  expect(rows.map((row) => row[4])).toEqual(expected);
  expect(rows.map((row) => row[5])).toEqual([PAIN_FORM, PAIN_PLANNING, PAIN_NOTICE]);
  expect(rows.map((row) => row[1])).toEqual([project.enter, project.fill, project.notify]);
  expect(rows.map((row) => row[8] ?? '')).toEqual(['2026-12-15', '', '2026-12-20']);
  expect(rows.map((row) => row[9])).toEqual(['signalé', 'signalé', 'réalisé']);
  expect(rows.map((row) => row[10])).toEqual([2, 3.5, 1]);
  // Each line of the panel says the same as its row of the sheet.
  for (const [index, row] of rows.entries()) {
    const text = shown[index]?.text ?? '';
    expect([text.includes(String(row[4])), text.includes(String(row[5])), text.includes(String(row[1]))]).toEqual([true, true, true]);
  }
});

test('a reader (simulated) has the button and the panel, the lines and the way to the step, and no "Edit the plan"', async ({ page, api }) => {
  const project = await layOut(api, 'PLR');
  // The role of a reader given to the page for the project and for the lines, as the host gives them (the bench is an
  // administrator; the rights themselves are held by the tests of the host): the line is read by the API of the journey,
  // since in server mode only a page carries the Windows identity.
  await page.route(`**/api/projects/${project.id}`, async (route) => {
    await route.fulfill({ json: { ...(await api.get<components['schemas']['ProjectDetail']>(`/api/projects/${project.id}`)), role: 'reader' } });
  });
  await page.route(`**/api/maps/${project.mapId}/action-plan`, async (route) => {
    const plan = await api.get<ActionPlanView>(`/api/maps/${project.mapId}/action-plan`);
    await route.fulfill({ json: { ...plan, lines: plan.lines.map((line) => ({ ...line, can_fill: false })) } });
  });
  await openMap(page, project.mapId, project.enter);
  const button = page.getByTestId('ribbon-plan');
  await expect(button).toBeEnabled();
  await openPanel(page);

  await expect.poll(() => codesShown(page)).toEqual([project.form, project.planning, project.notice]);
  await expect(page.getByTestId('plan-total')).toHaveText('6,5 h perdues par mois sur les irritants ouverts');
  await expect(panelOf(page).locator('[data-testid^="plan-edit-"]')).toHaveCount(0);
  await expect(panelOf(page).getByRole('button', { name: 'Modifier le plan' })).toHaveCount(0);

  // Reading and going to the step stay open to a reader.
  await filterOf(page, 'no-action').click();
  await expect.poll(() => codesShown(page)).toEqual([project.planning]);
  await page.getByTestId(`plan-go-${project.planning}`).click();
  await expect(page.getByTestId('breadcrumb-level-1')).toHaveAttribute('aria-current', 'page');
  await expect(stepOf(page, project.fill)).toHaveClass(/is-selected/);
  await expect(panelOf(page)).toBeVisible();
  // The routes read the host through this page in server mode: one still in flight when the page closes would fail the test.
  await page.unrouteAll({ behavior: 'wait' });
});
