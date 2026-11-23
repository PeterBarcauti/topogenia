// galerie.ts
// The templates of the gallery opened for good on the real host: every published .pmap opened as a project, its counts compared to its file and its image taken at a readable zoom; every one read by the preview of the insertion, one per language inserted as a detailed step; every .pmaps opened by the dialog of the collection and all its processes created.

import { createHash } from 'node:crypto';
import { mkdirSync, readFileSync, readdirSync, statSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import type { Page } from '@playwright/test';
import type { components } from '../../src/api/generated/schema.ts';
import type { Api } from '../../e2e/support/api.ts';
import { expect, openMap, stepOf, test } from '../../e2e/support/journey.ts';

type Opened = components['schemas']['PmapOpened'];
type Preview = components['schemas']['PmapInsertionPreview'];
type ProjectItem = components['schemas']['ProjectListItem'];

const SITE = process.env.GALERIE_SITE ?? '';
const IMAGES = process.env.GALERIE_IMAGES ?? '';
const LANGUAGES = ['fr', 'en'] as const;
type Language = (typeof LANGUAGES)[number];

/** A domain of the gallery: the folder of the images and of the English pages, the slug of the French pages. */
interface Domain {
  readonly folder: string;
  readonly slug: string;
}
const DOMAINS = (JSON.parse(readFileSync(join(SITE, 'outils', 'galerie-domaines.json'), 'utf-8')) as { domains: Domain[] }).domains;

/** The folder of a domain on the site, in a language: the French pages are at the root, the English ones under `en/templates`. */
const siteFolder = (domain: Domain, language: Language): string => (language === 'fr' ? join(SITE, 'modeles', domain.slug) : join(SITE, 'en', 'templates', domain.folder));

/** One published file, as the site serves it. */
interface Published {
  readonly domain: Domain;
  readonly name: string;
  readonly text: string;
}

/** The files of a kind (`.pmap` or `.pmaps`) the site publishes in a language, in the order of their domains and names. */
function published(language: Language, extension: '.pmap' | '.pmaps'): Published[] {
  return DOMAINS.flatMap((domain) => {
    const folder = siteFolder(domain, language);
    return readdirSync(folder).filter((file) => file.endsWith(`.${language}${extension}`)).sort()
      .map((name) => ({ domain, name, text: readFileSync(join(folder, name), 'utf-8') }));
  });
}

/** What a file holds, counted in the text of the file: the root map and every detailed map under it. */
interface Counts {
  maps: number;
  lanes: number;
  steps: number;
  flows: number;
  sheets: number;
}
interface MapText {
  lanes?: unknown[];
  steps?: { sheet?: unknown; sub_map?: MapText }[];
  flows?: unknown[];
}
function countsOf(text: string): Counts {
  const counts: Counts = { maps: 0, lanes: 0, steps: 0, flows: 0, sheets: 0 };
  const walk = (map: MapText): void => {
    counts.maps += 1;
    counts.lanes += map.lanes?.length ?? 0;
    counts.flows += map.flows?.length ?? 0;
    for (const step of map.steps ?? []) {
      counts.steps += 1;
      if (step.sheet !== undefined) counts.sheets += 1;
      if (step.sub_map !== undefined) walk(step.sub_map);
    }
  };
  walk((JSON.parse(text) as { map: MapText }).map);
  return counts;
}
const seen = (reply: Counts): Counts => ({ maps: reply.maps, lanes: reply.lanes, steps: reply.steps, flows: reply.flows, sheets: reply.sheets });

/** The interface and the profile in a language, as a person chooses it. */
async function speak(api: Api, language: Language): Promise<void> {
  const me = await api.get<{ version: number }>('/api/me');
  await api.send('PATCH', '/api/me', { language }, me.version);
}

/** The code and the name a file proposes for its project. */
function headerOf(text: string): { code: string; name: string } {
  const project = (JSON.parse(text) as { project: { code: string; name: string } }).project;
  return { code: project.code, name: project.name };
}

/** The codes of the projects of the base, sorted. */
async function codesOf(api: Api): Promise<string[]> {
  return (await api.get<ProjectItem[]>('/api/projects')).map((project) => project.code).sort();
}

/** The path of the image of a file: `<folder>/<file>.<language>.png`, where the generator of the site reads it. */
function imagePath(file: Published): string {
  const folder = join(IMAGES, file.domain.folder);
  mkdirSync(folder, { recursive: true });
  return join(folder, file.name.replace(/\.pmap$/, '.png'));
}

/** The map shown whole and still: the view fitted by « Tout voir », the animations ended, the pointer away. */
async function fitted(page: Page): Promise<void> {
  await page.getByTestId('map-fit').click();
  await page.mouse.move(2, 2);
  await page.evaluate(() => Promise.all(document.getAnimations().filter((animation) => animation.effect?.getComputedTiming().iterations !== Infinity).map((animation) => animation.finished.catch(() => undefined))));
  await page.waitForTimeout(400);
}

/** The page of the journeys, and the widest page an image is taken in (at 1.5 times the density, under the limit of Chromium). */
const PAGE = { width: 1440, height: 900 };
const WIDEST = 8000;
/**
 * The zoom of the images: the texts of the steps are made for 100 %; at 80 % they stay readable on the image, and each
 * lane keeps the height its fixed header needs for its name and its code (pre-audit 68, P3-07: fitted in the page of the
 * journeys, a wide map fell near 40 %, its texts some five pixels high and the headers of its lanes cut).
 */
const IMAGE_ZOOM = 0.8;
/** What « Tout voir » leaves around the map: the fixed headers and the right margin across, the top and bottom margins down. */
const FIT_ACROSS = 192 + 40;
const FIT_DOWN = 16 + 24;

/** The map fitted in a page made as large as the map at the zoom of the images: every lane whole, every text readable. */
async function sized(page: Page): Promise<void> {
  await page.setViewportSize(PAGE);
  await fitted(page);
  const zoom = await page.locator('.react-flow__viewport').evaluate((element) => new DOMMatrixReadOnly(getComputedStyle(element).transform).a);
  const canvas = await page.getByTestId('map-canvas').boundingBox();
  if (canvas === null) throw new Error('The canvas is not on the screen.');
  const extent = await page.locator('.react-flow__node').evaluateAll((nodes) => {
    const boxes = nodes.map((node) => node.getBoundingClientRect());
    return { width: Math.max(...boxes.map((box) => box.right)) - Math.min(...boxes.map((box) => box.left)), height: Math.max(...boxes.map((box) => box.bottom)) - Math.min(...boxes.map((box) => box.top)) };
  });
  const width = Math.min(WIDEST, Math.ceil(PAGE.width - canvas.width + FIT_ACROSS + (extent.width / zoom) * IMAGE_ZOOM));
  const height = Math.ceil(PAGE.height - canvas.height + FIT_DOWN + (extent.height / zoom) * IMAGE_ZOOM);
  await page.setViewportSize({ width: Math.max(PAGE.width, width), height: Math.max(PAGE.height, height) });
  await page.waitForTimeout(300);
  await fitted(page);
}

/** The zone of the canvas that holds the map, without the empty space under it nor the controls and the overview, which are put away. */
async function drawn(page: Page): Promise<{ x: number; y: number; width: number; height: number }> {
  // Set on the elements, not by a style sheet: the page refuses an inline style.
  await page.locator('.react-flow__controls, .react-flow__minimap').evaluateAll((parts) => { for (const part of parts) (part as HTMLElement).style.visibility = 'hidden'; });
  const canvas = await page.getByTestId('map-canvas').boundingBox();
  if (canvas === null) throw new Error('The canvas is not on the screen.');
  const bottom = await page.locator('.react-flow__node').evaluateAll((nodes) => Math.max(...nodes.map((node) => node.getBoundingClientRect().bottom)));
  return { x: canvas.x, y: canvas.y, width: canvas.width, height: Math.min(canvas.height, bottom - canvas.y + 16) };
}

/** A file opened as a project by « Open a file », the way a person does: the reply of the host is given back, the map of the project shown. */
async function openFile(page: Page, file: Published): Promise<Opened> {
  await page.goto('/');
  await page.getByTestId('home-open-pmap').click();
  await page.getByTestId('open-pmap-file').setInputFiles({ name: file.name, mimeType: 'application/json', buffer: Buffer.from(file.text) });
  await expect(page.getByTestId('open-pmap-code')).toHaveValue(headerOf(file.text).code);
  const answer = page.waitForResponse((response) => response.url().endsWith('/api/projects/import-pmap') && response.request().method() === 'POST');
  await page.getByTestId('open-pmap-confirm').click();
  const reply = await answer;
  expect(reply.status(), `${file.name} refused: ${await reply.text()}`).toBe(201);
  const opened = (await reply.json()) as Opened;
  // The points to check are listed in a dialog first: its button leads to the map.
  await page.getByTestId('pmap-created-open').click();
  await expect(page.getByTestId('map-canvas')).toBeVisible();
  await expect(page.locator('.react-flow__node').first()).toBeVisible();
  return opened;
}

test('every_published_pmap_opens_and_matches_its_counts', async ({ page, api }) => {
  let taken = 0;
  for (const language of LANGUAGES) {
    await speak(api, language);
    for (const file of published(language, '.pmap')) {
      await api.clearProjects();
      const opened = await openFile(page, file);
      // The counts of the project made are the ones of the file, and no model is empty.
      expect(seen(opened), file.name).toEqual(countsOf(file.text));
      expect(opened.code, file.name).toBe(headerOf(file.text).code);
      expect(opened.steps, file.name).toBeGreaterThan(5);
      await sized(page);
      // Readable: the map shown at the zoom of the images, nearly (the widest page may hold a very wide map lower).
      const zoom = await page.locator('.react-flow__viewport').evaluate((element) => new DOMMatrixReadOnly(getComputedStyle(element).transform).a);
      expect(zoom, file.name).toBeGreaterThanOrEqual(IMAGE_ZOOM - 0.05);
      // A capture of the zone of the map, not the export of the application, which prints the classification on the image.
      await page.screenshot({ path: imagePath(file), clip: await drawn(page) });
      expect(statSync(imagePath(file)).size, file.name).toBeGreaterThan(10_000);
      // Beside it, the SHA-256 of the model opened: the generator of the site refuses an image taken from another state of its model.
      writeFileSync(`${imagePath(file)}.source.sha256`, `${createHash('sha256').update(file.text, 'utf-8').digest('hex')}\n`);
      taken += 1;
    }
  }
  expect(taken).toBe(DOMAINS.length * LANGUAGES.length);
});

/** A project of one lane with two steps joined by a flow, to insert a file in. */
async function layOut(api: Api): Promise<{ mapId: string; laneId: string; flowId: string }> {
  const { mapId } = await api.project('INS', 'Insertion');
  const laneId = (await api.map(mapId)).lanes[0]?.id ?? '';
  const first = await api.step(mapId, laneId, 0, 'Start');
  const second = await api.step(mapId, laneId, 1, 'End');
  return { mapId, laneId, flowId: await api.flow(mapId, first, second) };
}

test('every_published_pmap_can_be_inserted_as_a_detailed_step', async ({ page, api }) => {
  for (const language of LANGUAGES) {
    await speak(api, language);
    await api.clearProjects();
    const layout = await layOut(api);
    const files = published(language, '.pmap');
    // Every file read by the preview of the insertion: what it holds is what the file says, in its language.
    for (const file of files) {
      const preview = await api.send<Preview>('POST', `/api/maps/${layout.mapId}/insert-pmap/preview`, { content: file.text });
      expect(seen(preview), file.name).toEqual(countsOf(file.text));
      expect(preview.language, file.name).toBe(language);
      expect(preview.label, file.name).not.toBe('');
    }
    expect(files).toHaveLength(DOMAINS.length);
    // One per language inserted for good, on the flow, the way the dialog asks it.
    const file = files[0];
    if (file === undefined) throw new Error('The site publishes no template.');
    const preview = await api.send<Preview>('POST', `/api/maps/${layout.mapId}/insert-pmap/preview`, { content: file.text });
    await api.send('POST', `/api/maps/${layout.mapId}/insert-pmap`, {
      content: file.text, with_sheets: true, label: preview.label, lane_id: layout.laneId, column: null, row: null, flow_id: layout.flowId,
    }, preview.structure_version);
    const inserted = (await api.map(layout.mapId)).steps.find((step) => step.sub_map_id != null && step.label === preview.label);
    expect(inserted, file.name).toBeDefined();
    const detail = await api.map(inserted?.sub_map_id ?? '');
    expect(detail.steps.length, file.name).toBeGreaterThan(5);
    await openMap(page, layout.mapId);
    await expect(stepOf(page, inserted?.code ?? '')).toBeVisible();
  }
});

test('every_published_collection_creates_all_its_projects', async ({ page, api }) => {
  for (const language of LANGUAGES) {
    await speak(api, language);
    for (const collection of published(language, '.pmaps')) {
      await api.clearProjects();
      // The models alone first: their codes are taken, so the code of the file is no longer the free one.
      const singles = published(language, '.pmap').filter((file) => file.domain.folder === collection.domain.folder);
      for (const single of singles) await api.send('POST', '/api/projects/import-pmap', { content: single.text, ...headerOf(single.text) });
      expect(await codesOf(api), collection.name).toHaveLength(singles.length);

      await page.goto('/');
      await page.getByTestId('home-open-pmap').click();
      await page.getByTestId('open-pmap-file').setInputFiles({ name: collection.name, mimeType: 'application/json', buffer: Buffer.from(collection.text) });
      const dialog = page.getByTestId('collection');
      await expect(dialog.getByTestId('collection-row-0')).toBeVisible();
      await expect(dialog.locator('[data-testid^="collection-row-"]'), collection.name).toHaveCount(singles.length);
      // Every process is ticked, none is refused, and each proposes a code that is not the one of its file.
      for (const [index, single] of singles.entries()) {
        await expect(dialog.getByTestId(`collection-tick-${String(index)}`)).toBeChecked();
        await expect(dialog.getByTestId(`collection-code-input-${String(index)}`)).not.toHaveValue(headerOf(single.text).code);
      }
      await dialog.getByTestId('collection-create').click();
      await expect(dialog.getByTestId('collection-done')).toBeVisible();
      await expect(dialog.locator('[data-testid^="collection-result-"]')).toHaveCount(singles.length);
      // Each line is told created, none refused.
      for (const index of singles.keys()) await expect(dialog.getByTestId(`collection-result-${String(index)}`).getByRole('status'), collection.name).toBeVisible();
      const after = await codesOf(api);
      expect(after, collection.name).toHaveLength(singles.length * 2);
      expect(new Set(after).size, collection.name).toBe(after.length);
      await dialog.getByTestId('collection-done').click();
      await expect(dialog).toHaveCount(0);
    }
  }
});
