// project-pdf-34.spec.ts
// Journeys of the PDF of the project of the 3.4 (lot 5, ADR 113): « Exporter le projet en PDF… » of the File menu over a project of three levels with an empty map, the title page, the contents, the maps level by level, the classification and « Page n sur N » on every page, the sheets as the person sees them, the scope of a detailed map, Cancel, a reader, and a project of 100 maps.

import { readFileSync, writeFileSync } from 'node:fs';
import type { Page, TestInfo } from '@playwright/test';
import { pdfInfo, pdfOutline, pdfPages, type PdfPage } from '../src/test/pdf.ts';
import type { Api, ProjectRead } from './support/api.ts';
import { expect, openMap, test } from './support/journey.ts';
import { MODE } from './support/run.ts';
import { fileEntry } from './support/file-menu.ts';

/** The French texts of the journeys, read from the language file: the name of the product, the refusal past the maps and the scope it names. */
const FRENCH = JSON.parse(readFileSync(new URL('../src/shared/i18n/locales/fr.json', import.meta.url), 'utf-8')) as {
  app: { name: string };
  project_pdf: { too_many_maps_below: string; scope_branch: string };
};
const APP_NAME = FRENCH.app.name;
/** The refusal past 100 maps on the main map, where the dialog offers the whole project only: open a detailed map further down, then its branch. */
const TOO_MANY_BELOW = FRENCH.project_pdf.too_many_maps_below.replace('{{max}}', '100').replace('{{scope}}', FRENCH.project_pdf.scope_branch);
const DONE = 'Export terminé : le fichier est dans le dossier Téléchargements.';
const CLASSIFICATION = 'Classification : Confidentiel';
const PAIN = 'Champs ressaisis à la main';
const ACTION = 'Rappel automatique';
const SAISIE_DRAFT = 'Saisie en brouillon';
const FORM_VALIDATED = 'Formulaire validé';
const CHECK_NEVER = 'Pièces jamais validées';

/** The project: its code, its maps and the readable names of its maps, by their path of detail. */
interface Project {
  id: string;
  code: string;
  name: string;
  /** The main map, the map of « Saisir la demande », the empty map of « Valider la demande », the map of « Contrôler » (level 2). */
  main: string;
  enter: string;
  empty: string;
  control: string;
  /** The code of the first step of the main map, drawn when the map opens. */
  first: string;
}

/** The sheet of a step as the journeys write it: the fields of the value stream and a free text, validated or left as a draft. */
async function writeSheet(api: Api, step: string, text: string, validate: boolean): Promise<void> {
  const read = await api.get<{ version: number | null }>(`/api/steps/${step}/sheet`);
  const fields = { frequency: { value: 'per_case' }, duration_minutes: { number: 15 } };
  const saved = await api.send<{ version: number }>('PUT', `/api/steps/${step}/sheet`, { fields, procedure: [], free_text: text }, read.version ?? 0);
  if (validate) await api.send('POST', `/api/steps/${step}/sheet/validate`, undefined, saved.version);
}

/** The detailed map of a step, made empty by the host; its identifier. */
async function detail(api: Api, mapId: string, step: string): Promise<string> {
  const version = (await api.map(mapId)).steps.find((candidate) => candidate.id === step)?.version;
  return (await api.send<{ id: string | null }>('POST', `/api/steps/${step}/detail`, undefined, version)).id ?? '';
}

/** A document produced by a step, its identifier. */
async function output(api: Api, stepId: string, name: string): Promise<string> {
  const made = await api.send<{ outputs: { document: { id: string } }[] }>('POST', `/api/steps/${stepId}/documents`, {
    direction: 'output', name, location: null, wait_hours: null, share_confirmed: null,
  });
  return made.outputs[0]?.document.id ?? '';
}

/** The letter A given to a step by a lane. */
async function letter(api: Api, mapId: string, step: string, lane: string): Promise<void> {
  const version = (await api.map(mapId)).steps.find((candidate) => candidate.id === step)?.version;
  await api.send('PUT', `/api/steps/${step}/raci`, { lane_id: lane, role: 'A' }, version);
}

/**
 * The case, built by the API (the server mode has no sample). Level 0: « Saisir la demande » (detailed, its sheet validated
 * then changed: a draft) and « Valider la demande » (detailed, its map left empty; it uses the document of the first with a
 * pain point and an action, and holds the letter A of the lane « Direction »). Level 1, under the first: « Remplir le
 * formulaire » (sheet validated) and « Contrôler » (detailed). Level 2: « Vérifier les pièces » (sheet never validated).
 */
async function layOut(api: Api, code: string): Promise<Project> {
  const { id, mapId } = await api.project(code, 'Demande client', 'fr', 'confidential');
  const lane = (await api.map(mapId)).lanes[0]?.id ?? '';
  const direction = await api.lane(mapId, 'Direction');
  const enter = await api.step(mapId, lane, 0, 'Saisir la demande');
  const check = await api.step(mapId, lane, 1, 'Valider la demande');
  await api.flow(mapId, enter, check);
  const enterMap = await detail(api, mapId, enter);
  const emptyMap = await detail(api, mapId, check);
  const subLane = (await api.map(enterMap)).lanes[0]?.id ?? '';
  const fill = await api.step(enterMap, subLane, 0, 'Remplir le formulaire');
  const control = await api.step(enterMap, subLane, 1, 'Contrôler');
  await api.flow(enterMap, fill, control);
  const controlMap = await detail(api, enterMap, control);
  const verify = await api.step(controlMap, (await api.map(controlMap)).lanes[0]?.id ?? '', 0, 'Vérifier les pièces');
  await writeSheet(api, enter, 'Saisie validée', true);
  await writeSheet(api, enter, SAISIE_DRAFT, false);
  await writeSheet(api, fill, FORM_VALIDATED, true);
  await writeSheet(api, verify, CHECK_NEVER, false);
  const document = await output(api, enter, 'Formulaire de demande');
  await api.send('PUT', `/api/steps/${check}/uses/${document}`, {
    wait_hours: null, percent_complete_accurate: null, pain_point: PAIN, lost_hours_per_month: 2, action: ACTION, due_date: '2026-12-15',
  }, 0);
  await letter(api, mapId, check, direction);
  const first = (await api.map(mapId)).steps.find((candidate) => candidate.id === enter)?.code ?? '';
  return { id, code, name: 'Demande client', main: mapId, enter: enterMap, empty: emptyMap, control: controlMap, first };
}

/** The title of the page of a map in the document and its path, as the PDF of a map writes them. */
async function headerOf(api: Api, mapId: string): Promise<{ title: string; path: string }> {
  const map = await api.map(mapId);
  return { title: `${map.project_code} - ${map.name}`, path: map.breadcrumb.map((crumb) => crumb.name).join(' > ') };
}

/** The code of the first step of a map, drawn when the map opens. */
async function firstCodeOf(api: Api, mapId: string): Promise<string> {
  return (await api.map(mapId)).steps[0]?.code ?? '';
}

/** The role of a reader given to the page for the project, as in the properties (ADR 067): the bench is an administrator. */
async function readAs(page: Page, api: Api, id: string): Promise<void> {
  await page.route(`**/api/projects/${id}`, async (route) => {
    await route.fulfill({ json: { ...(await api.get<ProjectRead>(`/api/projects/${id}`)), role: 'reader' } });
  });
}

/** The dialog opened by the File menu. */
async function openDialog(page: Page) {
  await page.getByTestId('header-file').click();
  await (await fileEntry(page, 'file.project-pdf')).click();
  const dialog = page.getByTestId('project-pdf');
  await expect(dialog).toBeVisible();
  return dialog;
}

/** The file the browser receives when the dialog creates it; the dialog closed after it, its notification said. */
async function create(page: Page, testInfo: TestInfo): Promise<{ name: string; content: Buffer; text: string }> {
  const download = page.waitForEvent('download');
  await page.getByTestId('project-pdf-confirm').click();
  const file = await download;
  await expect(page.getByText(DONE)).toBeVisible();
  await expect(page.getByTestId('project-pdf')).toHaveCount(0);
  const content = readFileSync(await file.path());
  // Kept in the results of the run, for the eye.
  writeFileSync(testInfo.outputPath('project.pdf'), content);
  return { name: file.suggestedFilename(), content, text: content.toString('latin1') };
}

const wordsOf = (page: PdfPage): string[] => page.texts.map((text) => text.text);
const isA3 = (page: PdfPage): boolean => Math.abs(page.width - 420) < 1 && Math.abs(page.height - 297) < 1;
const isA4 = (page: PdfPage): boolean => Math.abs(page.width - 210) < 1 && Math.abs(page.height - 297) < 1;
const pageOf = (pages: PdfPage[], text: string): number => pages.findIndex((candidate) => wordsOf(candidate).some((word) => word.includes(text)));

test('the dialog opens from the File menu with the sheets and the plan ticked, the RACI matrix and the value stream not; the scope of the branch is offered on a detailed map only', async ({ page, api }) => {
  const project = await layOut(api, 'PDA');
  await openMap(page, project.main, project.first);
  const dialog = await openDialog(page);
  await expect(dialog.getByTestId('project-pdf-sheets')).toBeChecked();
  await expect(dialog.getByTestId('project-pdf-plan')).toBeChecked();
  await expect(dialog.getByTestId('project-pdf-raci')).not.toBeChecked();
  await expect(dialog.getByTestId('project-pdf-stream')).not.toBeChecked();
  await expect(dialog.getByTestId('project-pdf-scope-project')).toBeChecked();
  await expect(dialog.getByTestId('project-pdf-scope-branch')).toHaveCount(0);
  await page.keyboard.press('Escape');
  await expect(page.getByTestId('project-pdf')).toHaveCount(0);

  await openMap(page, project.enter, await firstCodeOf(api, project.enter));
  await openDialog(page);
  await expect(page.getByTestId('project-pdf-scope-branch')).toBeVisible();
});

test('the PDF holds the title page, the contents and the maps level by level with their paths, the sheets after their map, the plan last', async ({ page, api }, testInfo) => {
  const project = await layOut(api, 'PDB');
  await openMap(page, project.main, project.first);
  const dialog = await openDialog(page);
  await dialog.getByTestId('project-pdf-raci').check();
  const { name, text } = await create(page, testInfo);

  expect(name).toMatch(/^PDB_.*_\d{4}-\d{2}-\d{2}\.pdf$/);
  expect(name).toContain('projet_');
  expect(pdfInfo(text, 'Title')).toBe('PDB - projet');
  expect(pdfInfo(text, 'Creator')).toBe(APP_NAME);
  const pages = pdfPages(text);
  // The title page and the contents, A4 portrait, first.
  expect(isA4(pages[0] as PdfPage) && isA4(pages[1] as PdfPage)).toBe(true);
  expect(wordsOf(pages[0] as PdfPage)).toEqual(expect.arrayContaining([project.code, project.name, CLASSIFICATION]));
  const contents = wordsOf(pages[1] as PdfPage);
  expect(contents).toContain('Sommaire');
  expect(contents.some((word) => /^Plan d.actions$/.test(word))).toBe(true);

  // The maps level by level: the main map, the map of the first step, the empty map, the map under the first one.
  const order = [project.main, project.enter, project.empty, project.control];
  const headers = await Promise.all(order.map((id) => headerOf(api, id)));
  const mapPages = pages.filter((candidate) => isA3(candidate) && /^PDB - /.test(candidate.texts[0]?.text ?? ''));
  expect(mapPages.map((candidate) => candidate.texts[0]?.text)).toEqual(headers.map((header) => header.title));
  expect(mapPages.map((candidate) => candidate.texts[1]?.text)).toEqual(headers.map((header) => header.path));
  for (const header of headers) expect(contents.some((word) => word.includes(header.title.slice('PDB - '.length)))).toBe(true);
  expect(wordsOf(mapPages[2] as PdfPage)).toContain('Carte sans étape.');

  // Bookmarks nested by level, the plan at the top level.
  const outline = pdfOutline(text);
  expect(outline.map((mark) => mark.level)).toEqual([0, 1, 2, 1, 0]);
  expect(outline[4]?.title).toMatch(/^Plan d.actions$/);

  // The RACI matrix of the main map after its page; the plan, the last page, with the pain point and its action.
  expect(pages.some((candidate) => isA3(candidate) && wordsOf(candidate).some((word) => /^Matrice RACI/.test(word)))).toBe(true);
  const last = pages[pages.length - 1] as PdfPage;
  expect(isA3(last)).toBe(true);
  expect(wordsOf(last)).toEqual(expect.arrayContaining([PAIN, ACTION]));

  // The sheets, A4 portrait, each after its map and before the next map; a draft says so, a validated one too.
  const enterPage = pageOf(pages, SAISIE_DRAFT);
  expect(isA4(pages[enterPage] as PdfPage)).toBe(true);
  expect(enterPage).toBeGreaterThan(pages.indexOf(mapPages[0] as PdfPage));
  expect(enterPage).toBeLessThan(pages.indexOf(mapPages[1] as PdfPage));
  expect(pageOf(pages, FORM_VALIDATED)).toBeGreaterThan(pages.indexOf(mapPages[1] as PdfPage));
  expect(pageOf(pages, FORM_VALIDATED)).toBeLessThan(pages.indexOf(mapPages[2] as PdfPage));
  expect(pageOf(pages, CHECK_NEVER)).toBeGreaterThan(pages.indexOf(mapPages[3] as PdfPage));
  const states = pages.flatMap(wordsOf).filter((word) => word.startsWith('État de la fiche'));
  expect(states.some((word) => word.endsWith('brouillon'))).toBe(true);
  expect(states.some((word) => word.endsWith('validée'))).toBe(true);
  // The draft written over a validated sheet is what the facilitator reads, not the version validated before it.
  expect(pages.flatMap(wordsOf)).not.toContain('Saisie validée');
});

test('every page carries the classification, the product with the code and « Page n sur N », on A3 and on A4 pages', async ({ page, api }, testInfo) => {
  const project = await layOut(api, 'PDC');
  await openMap(page, project.main, project.first);
  await openDialog(page);
  const { text } = await create(page, testInfo);
  const pages = pdfPages(text);
  expect(pages.some(isA3) && pages.some(isA4)).toBe(true);
  expect(pages.every((candidate) => isA3(candidate) || isA4(candidate))).toBe(true);
  pages.forEach((candidate, index) => {
    const words = wordsOf(candidate);
    expect(words, `page ${String(index + 1)}`).toContain(CLASSIFICATION);
    expect(words, `page ${String(index + 1)}`).toContain(`Généré par ${APP_NAME} · PDC`);
    expect(words, `page ${String(index + 1)}`).toContain(`Page ${String(index + 1)} sur ${String(pages.length)}`);
  });
});

test('with the sheets the request asks the purpose pdf once and the sheets follow; without them no copy is asked', async ({ page, api }, testInfo) => {
  const project = await layOut(api, 'PDD');
  const copies: string[] = [];
  page.on('request', (request) => {
    if (/\/api\/maps\/[^/]+\/pmap\b/.test(request.url())) copies.push(request.url());
  });
  await openMap(page, project.main, project.first);
  await openDialog(page);
  const withSheets = await create(page, testInfo);
  expect(copies).toHaveLength(1);
  expect(copies[0]).toContain(`/api/maps/${project.main}/pmap`);
  expect(copies[0]).toContain('purpose=pdf');
  expect(pageOf(pdfPages(withSheets.text), SAISIE_DRAFT)).toBeGreaterThan(1);

  // The notification of the first export goes before the second starts (one at a time on the screen).
  await expect(page.getByText(DONE)).toHaveCount(0, { timeout: 10_000 });
  copies.length = 0;
  await openDialog(page);
  await page.getByTestId('project-pdf-sheets').uncheck();
  const without = await create(page, testInfo);
  expect(copies).toEqual([]);
  const pages = pdfPages(without.text);
  expect(pageOf(pages, SAISIE_DRAFT)).toBe(-1);
  // Only the title page and the contents are A4.
  expect(pages.filter(isA4)).toHaveLength(2);
});

test('« This map and its levels » starts at the detailed map: its title page says so and the main map is not in the file', async ({ page, api }, testInfo) => {
  const project = await layOut(api, 'PDE');
  await openMap(page, project.enter, await firstCodeOf(api, project.enter));
  const dialog = await openDialog(page);
  await dialog.getByTestId('project-pdf-scope-branch').check();
  await dialog.getByTestId('project-pdf-sheets').uncheck();
  const { text } = await create(page, testInfo);
  const start = await headerOf(api, project.enter);
  const main = await headerOf(api, project.main);
  const pages = pdfPages(text);
  const mapTitles = pages.filter((candidate) => isA3(candidate) && /^PDE - /.test(candidate.texts[0]?.text ?? '')).map((candidate) => candidate.texts[0]?.text);
  expect(mapTitles).toEqual([start.title, (await headerOf(api, project.control)).title]);
  expect(mapTitles).not.toContain(main.title);
  const name = start.title.slice('PDE - '.length);
  expect(wordsOf(pages[0] as PdfPage).some((word) => word.startsWith('Carte') && word.includes(name) && word.endsWith('et ses niveaux'))).toBe(true);
});

test('Cancel during the construction downloads nothing and says « Export annulé. »', async ({ page, api }) => {
  const project = await layOut(api, 'PDF');
  let release: () => void = () => undefined;
  const held = new Promise<void>((resolve) => { release = resolve; });
  // The reading of the sheets is held: the task is running when Cancel is clicked.
  await page.route(/\/api\/maps\/[^/]+\/pmap\?.*purpose=pdf/, async (route) => {
    await held;
    await route.continue().catch(() => undefined);
  });
  let downloads = 0;
  page.on('download', () => { downloads += 1; });
  await openMap(page, project.main, project.first);
  const dialog = await openDialog(page);
  await dialog.getByTestId('project-pdf-confirm').click();
  await expect(dialog.getByTestId('project-pdf-progress')).toBeVisible();
  await expect(dialog.getByRole('status')).toHaveText('Lecture des fiches…');
  await expect(dialog.getByTestId('project-pdf-sheets')).toBeDisabled();
  await dialog.getByTestId('project-pdf-cancel').click();
  await expect(page.getByText('Export annulé.')).toBeVisible();
  await expect(page.getByTestId('project-pdf')).toHaveCount(0);
  release();
  await expect(page.getByText(DONE)).toHaveCount(0);
  expect(downloads).toBe(0);
});

test('a reader (simulated) has the entry and the dialog', async ({ page, api }) => {
  const project = await layOut(api, 'PDR');
  await readAs(page, api, project.id);
  await openMap(page, project.main, project.first);
  await page.getByTestId('header-file').click();
  await expect(await fileEntry(page, 'file.project-pdf')).toBeEnabled();
  await (await fileEntry(page, 'file.project-pdf')).click();
  const dialog = page.getByTestId('project-pdf');
  await expect(dialog).toBeVisible();
  await expect(dialog.getByTestId('project-pdf-sheets')).toBeChecked();
  await expect(dialog.getByTestId('project-pdf-confirm')).toBeEnabled();
});

/** The maps of the very large project: 20 steps on each, the first twenty of the main map detailed, then those of the next maps in turn, 100 maps in all. */
async function layOutLarge(api: Api, code: string, target: number): Promise<{ mapId: string; maps: string[] }> {
  const { mapId } = await api.project(code, 'Grand projet', 'fr', 'internal');
  const maps = [mapId];
  for (let index = 0; index < maps.length; index++) {
    const current = maps[index] ?? '';
    const lane = (await api.map(current)).lanes[0]?.id ?? '';
    const steps: string[] = [];
    for (let column = 0; column < 20; column++) steps.push(await api.step(current, lane, column, `Étape ${String(column + 1)}`));
    const children = Math.min(20, target - maps.length);
    for (let child = 0; child < children; child++) maps.push(await detail(api, current, steps[child] ?? ''));
  }
  return { mapId, maps };
}

test('very large: 100 maps of 20 steps give a PDF, its time and size are written; the 101st map is refused', async ({ page, api }, testInfo) => {
  test.skip(MODE === 'server', 'The duration of the journey: desktop mode only.');
  test.setTimeout(30 * 60 * 1000);
  const { mapId, maps } = await layOutLarge(api, 'PDL', 100);
  expect(maps).toHaveLength(100);
  await openMap(page, mapId, 'PDL-E001');
  await openDialog(page);
  const started = Date.now();
  const download = page.waitForEvent('download', { timeout: 20 * 60 * 1000 });
  await page.getByTestId('project-pdf-confirm').click();
  const file = await download;
  const seconds = Math.round((Date.now() - started) / 1000);
  const content = readFileSync(await file.path());
  const summary = `100 maps of 20 steps: ${String(seconds)} s, ${(content.length / 1_048_576).toFixed(1)} MB, ${String(content.toString('latin1').match(/\/Type \/Page\b/g)?.length ?? 0)} pages`;
  console.log(summary);
  testInfo.annotations.push({ type: 'very-large', description: summary });
  expect(content.subarray(0, 5).toString('latin1')).toBe('%PDF-');
  // A title page, the contents and one page for each map, at least.
  expect(content.toString('latin1').match(/\/Type \/Page\b/g)?.length ?? 0).toBeGreaterThanOrEqual(102);

  // One map more: refused, said in the dialog, nothing downloaded.
  const leaf = maps[99] ?? '';
  const lane = (await api.map(leaf)).lanes[0]?.id ?? '';
  await detail(api, leaf, await api.step(leaf, lane, 0, 'Une de plus', 'task', 1));
  await openMap(page, mapId, 'PDL-E001');
  await openDialog(page);
  let downloads = 0;
  page.on('download', () => { downloads += 1; });
  await page.getByTestId('project-pdf-confirm').click();
  // The 101st map is known only once the 100th, the last of the walk, is read: the refusal comes after the 100 readings.
  await expect(page.getByTestId('project-pdf-refusal')).toHaveText(TOO_MANY_BELOW, { timeout: 5 * 60 * 1000 });
  await expect(page.getByTestId('project-pdf-confirm')).toBeVisible();
  expect(downloads).toBe(0);
});
