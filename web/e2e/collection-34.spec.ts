// collection-34.spec.ts
// Journeys of the collection of templates (.pmaps) of the 3.4 (lot 6, ADR 114): the list of its processes ticked, one project per process ticked, a code taken replaced by the next free one, an invalid process left out, nothing written before « Create the projects », a collection pasted, and in server mode who has the opening (the facilitator, the contributor and the reader who are not administrators have not).

import { readFileSync } from 'node:fs';
import type { Locator, Page } from '@playwright/test';
import type { components } from '../src/api/generated/schema.ts';
import type { Api } from './support/api.ts';
import { expect, openMap, stepOf, test } from './support/journey.ts';
import { MODE } from './support/run.ts';
import { fileEntry } from './support/file-menu.ts';

type Me = components['schemas']['ProfileView'];
type ProjectItem = components['schemas']['ProjectListItem'];
type ProjectDetail = components['schemas']['ProjectDetail'];

/** The collection of the samples, as the file is on disk. */
const EXAMPLE = readFileSync(new URL('../../samples/exemple.pmaps', import.meta.url), 'utf-8');
/** The texts of the interface in French, read where the interface reads them: what a journey expects is never written twice. */
const FR: unknown = JSON.parse(readFileSync(new URL('../src/shared/i18n/locales/fr.json', import.meta.url), 'utf-8'));
/** The form of a code of a project, as the dialog checks it. */
const CODE = /^[A-Z]{3,6}$/;

/** A text of the interface by its key, its values put in. */
function say(key: string, values: Record<string, string | number> = {}): string {
  const found = key.split('.').reduce<unknown>((node, part) => (typeof node === 'object' && node !== null ? (node as Record<string, unknown>)[part] : undefined), FR);
  if (typeof found !== 'string') throw new Error(`No text « ${key} » in the file of the language.`);
  return found.replace(/\{\{(\w+)\}\}/g, (_match, name: string) => String(values[name] ?? ''));
}

/** What a journey reads of a process of the file, to make another one from it. */
interface Process {
  project: { name: string };
  map: { steps: { lane: string }[] };
}
interface Collection {
  processes: Process[];
}

/** The collection of the samples with a process added in the middle that the host refuses: a step in a lane that does not exist. */
function withInvalidProcess(): string {
  const collection = JSON.parse(EXAMPLE) as Collection;
  const first = collection.processes[0];
  if (first === undefined) throw new Error('The sample holds no process.');
  const broken = structuredClone(first);
  broken.project.name = 'Processus bancal';
  const step = broken.map.steps[2];
  if (step === undefined) throw new Error('The first process of the sample holds fewer than three steps.');
  step.lane = 'c9';
  collection.processes.splice(1, 0, broken);
  return JSON.stringify(collection, null, 2);
}

/** The requests of the page that write a project from a file; the preview is a read and is not counted. */
function trackCreations(page: Page): string[] {
  const written: string[] = [];
  page.on('request', (request) => {
    if (request.method() === 'POST' && request.url().includes('/api/projects/import-pmap')) written.push(request.url());
  });
  return written;
}

/** The dialog of the file, opened from the home page, a file given as text under its name. */
async function chooseCollection(page: Page, text: string): Promise<Locator> {
  await page.goto('/');
  await page.getByTestId('home-open-pmap').click();
  await page.getByTestId('open-pmap-file').setInputFiles({ name: 'exemple.pmaps', mimeType: 'application/json', buffer: Buffer.from(text) });
  return listed(page);
}

/** The dialog of the collection, its list drawn. */
async function listed(page: Page): Promise<Locator> {
  const dialog = page.getByTestId('collection');
  await expect(dialog.getByTestId('collection-row-0')).toBeVisible();
  return dialog;
}

const rowOf = (dialog: Locator, index: number): Locator => dialog.getByTestId(`collection-row-${String(index)}`);
const resultOf = (dialog: Locator, index: number): Locator => dialog.getByTestId(`collection-result-${String(index)}`);
const ticked = (dialog: Locator, count: number, total: number): Locator => dialog.getByText(say('files.collection_ticked', { count, total }));

/** The creation asked and ended: the dialog tells its result and offers to close. */
async function create(dialog: Locator): Promise<void> {
  await dialog.getByTestId('collection-create').click();
  await expect(dialog.getByTestId('collection-done')).toBeVisible();
  await expect(dialog).toHaveAccessibleName(say('files.collection_done'));
}

/** The codes of the projects of the base, in order. */
async function codesOf(api: Api): Promise<string[]> {
  return (await api.get<ProjectItem[]>('/api/projects')).map((project) => project.code).sort();
}

test('an_administrator_opens_a_collection_unticks_one_and_creates_the_others', async ({ page, api }) => {
  const written = trackCreations(page);
  const dialog = await chooseCollection(page, EXAMPLE);

  // The list: the name of the collection, every process ticked, what each holds and a code to each.
  await expect(dialog).toHaveAccessibleName(say('files.collection_title', { name: 'Exemples fictifs' }));
  await expect(dialog.locator('[data-testid^="collection-row-"]')).toHaveCount(2);
  await expect(rowOf(dialog, 0)).toContainText("Demande d'achat");
  await expect(rowOf(dialog, 0)).toContainText(say('files.created_summary', { steps: 4, lanes: 2, flows: 3, maps: 1 }));
  await expect(rowOf(dialog, 1)).toContainText('Clôture mensuelle des visites');
  await expect(rowOf(dialog, 1)).toContainText(say('files.created_summary', { steps: 11, lanes: 5, flows: 10, maps: 2 }));
  await expect(rowOf(dialog, 1)).toContainText('fr · ');
  await expect(rowOf(dialog, 0).getByRole('checkbox')).toBeChecked();
  await expect(rowOf(dialog, 1).getByRole('checkbox')).toBeChecked();
  await expect(ticked(dialog, 2, 2)).toBeVisible();
  expect(await rowOf(dialog, 0).getByRole('textbox').inputValue()).toMatch(CODE);
  await expect(rowOf(dialog, 1).getByRole('textbox')).toHaveValue('VIS');
  // The description and the points to check are folded, and unfold.
  await rowOf(dialog, 1).getByText(say('files.collection_details')).click();
  await expect(rowOf(dialog, 1).getByText('Le seuil de 5 % est à confirmer avec la direction.')).toBeVisible();
  // Nothing was sent to write a project: the list is a read.
  expect(written).toEqual([]);
  expect(await codesOf(api)).toEqual([]);

  // The box of the header unticks and ticks all; one line unticked leaves it half ticked.
  const all = dialog.getByRole('checkbox', { name: say('files.collection_all') });
  await all.uncheck();
  await expect(ticked(dialog, 0, 2)).toBeVisible();
  await expect(dialog.getByTestId('collection-create')).toBeDisabled();
  await all.check();
  await expect(ticked(dialog, 2, 2)).toBeVisible();
  await rowOf(dialog, 0).getByRole('checkbox').uncheck();
  await expect(ticked(dialog, 1, 2)).toBeVisible();
  await expect(all).toHaveJSProperty('indeterminate', true);

  // « Create »: one project for the line ticked, none for the other.
  await create(dialog);
  await expect(dialog.locator('[data-testid^="collection-result-"]')).toHaveCount(1);
  await expect(resultOf(dialog, 1).getByRole('status')).toHaveText(say('files.collection_created', { code: 'VIS' }));
  expect(written).toHaveLength(1);
  expect(await codesOf(api)).toEqual(['VIS']);

  // The way to the map of the project created.
  await resultOf(dialog, 1).getByRole('button', { name: say('files.open_map') }).click();
  await expect(stepOf(page, 'VIS-E004')).toBeVisible();
});

test('a_taken_code_is_proposed_free_and_the_map_opens', async ({ page, api }) => {
  await api.project('VIS', 'Déjà pris');
  const dialog = await chooseCollection(page, EXAMPLE);

  // The code of the file is taken: the next free one is proposed, not the code of the file.
  await expect(rowOf(dialog, 1).getByRole('textbox')).toHaveValue('VISA');
  await rowOf(dialog, 0).getByRole('checkbox').uncheck();
  await create(dialog);
  await expect(resultOf(dialog, 1).getByRole('status')).toHaveText(say('files.collection_created', { code: 'VISA' }));
  expect(await codesOf(api)).toEqual(['VIS', 'VISA']);

  await resultOf(dialog, 1).getByRole('button', { name: say('files.open_map') }).click();
  await expect(stepOf(page, 'VISA-E004')).toBeVisible();
  await expect(page.getByTestId('breadcrumb').locator('[aria-current="page"]')).toBeVisible();
});

test('an_invalid_process_is_listed_and_left_out', async ({ page, api }) => {
  const written = trackCreations(page);
  const dialog = await chooseCollection(page, withInvalidProcess());

  // Three lines; the one the host refuses is unticked, greyed, with no code to type and its fault said at its place.
  await expect(dialog.locator('[data-testid^="collection-row-"]')).toHaveCount(3);
  const bad = rowOf(dialog, 1);
  await expect(bad).toContainText('Processus bancal');
  await expect(bad).toHaveClass(/\bmuted\b/);
  await expect(bad.getByRole('checkbox')).not.toBeChecked();
  await expect(bad.getByRole('checkbox')).toBeDisabled();
  await expect(bad.getByRole('textbox')).toHaveCount(0);
  await bad.getByText(say('files.collection_invalid')).click();
  await expect(bad.locator('code')).toHaveText('map.steps[2].lane');
  await expect(bad).toContainText('« c9 »');
  // The others stay ticked, and the box of the header counts only what can be ticked.
  await expect(rowOf(dialog, 0).getByRole('checkbox')).toBeChecked();
  await expect(rowOf(dialog, 2).getByRole('checkbox')).toBeChecked();
  await expect(ticked(dialog, 2, 2)).toBeVisible();

  await create(dialog);
  await expect(dialog.locator('[data-testid^="collection-result-"]')).toHaveCount(2);
  await expect(resultOf(dialog, 0).getByRole('status')).toBeVisible();
  await expect(resultOf(dialog, 2).getByRole('status')).toHaveText(say('files.collection_created', { code: 'VIS' }));
  await expect(resultOf(dialog, 1)).toHaveCount(0);
  expect(written).toHaveLength(2);
  const codes = await codesOf(api);
  expect(codes).toHaveLength(2);
  expect(codes).toContain('VIS');
});

test('nothing_is_written_when_the_dialog_is_closed_without_create', async ({ page, api }) => {
  const written = trackCreations(page);
  const dialog = await chooseCollection(page, EXAMPLE);
  await rowOf(dialog, 0).getByRole('checkbox').uncheck();
  await rowOf(dialog, 1).getByRole('textbox').fill('abc');
  await expect(rowOf(dialog, 1).getByRole('textbox')).toHaveValue('ABC');

  // Escape closes, the home page is back.
  await page.keyboard.press('Escape');
  await expect(page.getByTestId('collection')).toHaveCount(0);
  await expect(page.getByTestId('home-open-pmap')).toBeVisible();
  expect(await codesOf(api)).toEqual([]);

  // « Cancel » closes too.
  const again = await chooseCollection(page, EXAMPLE);
  await again.getByTestId('collection-cancel').click();
  await expect(page.getByTestId('collection')).toHaveCount(0);
  expect(await codesOf(api)).toEqual([]);
  expect(written).toEqual([]);
});

test('a_collection_pasted_as_text_opens_like_a_file', async ({ page, api }) => {
  await page.goto('/');
  await page.getByTestId('home-open-pmap').click();
  // Pasted in its Markdown block, as an AI or a page of a site gives it.
  await page.getByRole('textbox', { name: 'coller son contenu' }).fill('```json\n' + EXAMPLE + '\n```');
  const dialog = await listed(page);
  await expect(dialog.locator('[data-testid^="collection-row-"]')).toHaveCount(2);
  await expect(ticked(dialog, 2, 2)).toBeVisible();
  await create(dialog);
  await expect(dialog.locator('[data-testid^="collection-result-"]')).toHaveCount(2);
  const codes = await codesOf(api);
  expect(codes).toHaveLength(2);
  expect(codes).toContain('VIS');
});

/** A project of the base with one step, built by the API, and the page given the profile of someone who is not an administrator. */
type Role = 'facilitator' | 'contributor' | 'reader';

async function simulateNonAdministrator(page: Page, api: Api): Promise<{ id: string; mapId: string; setRole: (role: Role) => void }> {
  const { id, mapId } = await api.project('CLS', 'Collection', 'fr', 'confidential');
  const lane = (await api.map(mapId)).lanes[0]?.id ?? '';
  await api.step(mapId, lane, 0, 'Besoin reçu', 'start');
  const me = await api.get<Me>('/api/me');
  let role: Role = 'contributor';
  // The bench is an administrator; the rights themselves are held by the tests of the host: the page is given the role of another user.
  await page.route('**/api/me', async (route) => {
    if (route.request().method() !== 'GET') await route.fallback();
    else await route.fulfill({ json: { ...me, is_admin: false } });
  });
  await page.route(`**/api/projects/${id}`, async (route) => {
    if (route.request().method() !== 'GET') await route.fallback();
    else await route.fulfill({ json: { ...(await api.get<ProjectDetail>(`/api/projects/${id}`)), role } });
  });
  return { id, mapId, setRole: (next) => { role = next; } };
}

test('a_contributor_and_a_reader_see_no_opening_of_a_file', async ({ page, api }) => {
  test.skip(MODE !== 'server', 'The roles of a project are held by the server mode only.');
  const project = await simulateNonAdministrator(page, api);
  for (const role of ['contributor', 'reader'] as const) {
    project.setRole(role);
    // The home page: the project is listed, the opening of a file is not offered.
    await page.goto('/');
    await expect(page.locator('.project-list .chip-code')).toHaveText(['CLS']);
    await expect(page.getByTestId('home-open-pmap')).toHaveCount(0);
    // The File menu of the map: its other lines are there, the opening is not.
    await openMap(page, project.mapId, 'CLS-E001');
    await page.getByTestId('header-file').click();
    await expect(await fileEntry(page, 'file.download')).toBeVisible();
    await expect(page.getByTestId('menu-file-file.open')).toHaveCount(0);
    await page.keyboard.press('Escape');
  }
});

test('a_facilitator_who_is_not_an_administrator_has_no_opening_of_a_collection', async ({ page, api }) => {
  test.skip(MODE !== 'server', 'The roles of a project are held by the server mode only.');
  const project = await simulateNonAdministrator(page, api);
  project.setRole('facilitator');
  const previews: string[] = [];
  page.on('request', (request) => {
    if (request.url().includes('/api/projects/import-collection/preview')) previews.push(request.url());
  });
  // The home page: the project is listed, the opening of a file is not offered.
  await page.goto('/');
  await expect(page.locator('.project-list .chip-code')).toHaveText(['CLS']);
  await expect(page.getByTestId('home-open-pmap')).toHaveCount(0);
  // The File menu of the map: the insertion of the facilitator is there, the opening is not.
  await openMap(page, project.mapId, 'CLS-E001');
  await page.getByTestId('header-file').click();
  await expect(await fileEntry(page, 'file.insert')).toBeVisible();
  await expect(page.getByTestId('menu-file-file.open')).toHaveCount(0);
  // A collection given to the insertion is refused at once, nothing sent.
  await (await fileEntry(page, 'file.insert')).click();
  const dialog = page.getByTestId('insert-pmap');
  await dialog.getByTestId('insert-pmap-file').setInputFiles({ name: 'exemple.pmaps', mimeType: 'application/json', buffer: Buffer.from(EXAMPLE) });
  await expect(dialog.getByTestId('insert-pmap-error')).toHaveText(say('files.insert_collection'));
  expect(previews).toEqual([]);
  expect(await codesOf(api)).toEqual(['CLS']);
});

test('the_administrator_is_the_only_facilitator_of_each_project_created', async ({ page, api }) => {
  test.skip(MODE !== 'server', 'Who the facilitator is matters to the server mode only.');
  const me = await api.get<Me>('/api/me');
  const dialog = await chooseCollection(page, EXAMPLE);
  await create(dialog);
  await expect(dialog.locator('[data-testid^="collection-result-"]')).toHaveCount(2);

  const items = await api.get<ProjectItem[]>('/api/projects');
  expect(items).toHaveLength(2);
  for (const item of items) {
    expect(item.role).toBe('facilitator');
    const detail = await api.get<ProjectDetail>(`/api/projects/${item.id}`);
    expect(detail.role).toBe('facilitator');
    // No member but the administrator who opened the collection, and he is the facilitator.
    expect(detail.members.filter((member) => member.role !== 'facilitator')).toEqual([]);
    expect(detail.members.map((member) => member.login.toLowerCase())).toEqual(detail.members.map(() => me.login.toLowerCase()));
  }
});
