// route-golden.spec.ts
// The drawing of every flow of the sample projects, read on the canvas, is the one of the version 3.0.2 captured before the route chosen by the user (ADR 091): paths and label places, with and without the value stream layer. `ROUTES_GOLDEN_WRITE=1` writes the capture again. Written again once (08/11/2026, F-110): the samples gave shares to the two flows out of E021, whose labels in the value stream layer moved with their longer text; every path is the one of 3.0.2.

import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import type { Page } from '@playwright/test';
import type { Api, MapRead } from './support/api.ts';
import { expect, openMap, stepOf, test } from './support/journey.ts';
import { MODE } from './support/run.ts';

const SAMPLES = join(import.meta.dirname, '..', '..', 'samples');
/** The extension of a package: the name of the product, read where it is written (D13). */
const PACKAGE = `.${(JSON.parse(readFileSync(new URL('../src/shared/i18n/locales/fr.json', import.meta.url), 'utf-8')) as { app: { name: string } }).app.name.toLowerCase()}`;
const GOLDEN = join(import.meta.dirname, 'fixtures', 'routes-3.0.2.json');
const WRITE = process.env.ROUTES_GOLDEN_WRITE === '1';

/** The sample projects, each under a code of its own: the two packages and the two .pmap files of the repository. */
const PROJECTS = [
  { code: 'GLA', file: `conges${PACKAGE}` },
  { code: 'GLB', file: `leave${PACKAGE}` },
  { code: 'GLC', file: 'cloture-mensuelle.pmap' },
  { code: 'GLD', file: 'minimal.pmap' },
];

type Drawing = Record<string, { path: string; label: string | null }>;

/** The name of a sample without its extension: the key of its flows in the capture. */
const stemOf = (file: string) => file.replace(/\.[a-z]+$/, '');

/** A project imported from its file: a package as its bytes, a .pmap as its text; the identifier of its root map. */
async function imported(page: Page, api: Api, code: string, file: string): Promise<string> {
  const path = join(SAMPLES, file);
  if (file.endsWith('.pmap')) {
    const opened = await api.send<{ id: string }>('POST', '/api/projects/import-pmap', { content: readFileSync(path, 'utf8'), code, name: null });
    return (await api.get<{ root_map_id: string }>(`/api/projects/${opened.id}`)).root_map_id;
  }
  const response = await page.request.post(`/api/projects/import?code=${code}&with_members=false`, {
    headers: { 'X-Requested-With': 'XMLHttpRequest', 'Content-Type': 'application/octet-stream' },
    data: readFileSync(path),
  });
  expect(response.status(), await response.text()).toBe(201);
  const project = (await response.json()) as { id: string };
  return (await api.get<{ root_map_id: string }>(`/api/projects/${project.id}`)).root_map_id;
}

/** The maps of a project from its root, each with the path of step codes that leads to it. */
async function mapsOf(api: Api, rootId: string): Promise<{ id: string; name: string; read: MapRead }[]> {
  const found: { id: string; name: string; read: MapRead }[] = [];
  const queue = [{ id: rootId, name: 'root' }];
  while (queue.length > 0) {
    const next = queue.shift();
    if (next === undefined) break;
    const read = await api.map(next.id);
    found.push({ ...next, read });
    for (const step of read.steps) if (step.sub_map_id !== null) queue.push({ id: step.sub_map_id, name: step.code });
  }
  return found;
}

/** The path and the label place of every flow of the map shown, by the codes of its two steps. */
async function drawingOf(page: Page, map: MapRead, prefix: string, into: Drawing): Promise<void> {
  const codeOf = new Map(map.steps.map((step) => [step.id, step.code]));
  await expect(page.locator('.react-flow__edge')).toHaveCount(map.flows.length);
  for (const flow of map.flows) {
    const edge = page.getByTestId(`rf__edge-${flow.id}`);
    const path = (await edge.locator('path.map-edge').getAttribute('d')) ?? '';
    const label = page.getByTestId(`edge-label-${flow.id}`);
    const place = (await label.count()) === 0 ? null : await label.evaluate((element) => (element as HTMLElement).style.transform);
    into[`${prefix}:${codeOf.get(flow.source_step_id) ?? '?'}>${codeOf.get(flow.target_step_id) ?? '?'}`] = { path, label: place };
  }
}

/** Two frames, so that the canvas is drawn again after a change of the layer. */
const settled = (page: Page) => page.evaluate(() => new Promise<void>((done) => requestAnimationFrame(() => { requestAnimationFrame(() => { done(); }); })));

const layerButton = (page: Page) => page.getByRole('toolbar').getByRole('button', { name: 'Flux de valeur', exact: true });

test('the flows of the sample projects are drawn as in 3.0.2, with and without the value stream layer (ADR 091)', async ({ page, api }) => {
  test.skip(MODE === 'server', 'The drawing is computed by the browser: one mode is enough.');
  test.setTimeout(240_000);
  await page.goto('/');
  const drawing: Drawing = {};
  for (const project of PROJECTS) {
    const rootId = await imported(page, api, project.code, project.file);
    for (const map of await mapsOf(api, rootId)) {
      const first = map.read.steps[0]?.code;
      await openMap(page, map.id, first);
      await settled(page);
      await drawingOf(page, map.read, `${stemOf(project.file)}/${map.name}`, drawing);
      if (map.read.steps.length === 0) continue;
      await layerButton(page).click();
      await expect(layerButton(page)).toHaveAttribute('aria-pressed', 'true');
      if (first !== undefined) await expect(stepOf(page, first)).toBeVisible();
      await settled(page);
      await drawingOf(page, map.read, `${stemOf(project.file)}/${map.name}#stream`, drawing);
      await layerButton(page).click();
      await expect(layerButton(page)).toHaveAttribute('aria-pressed', 'false');
    }
  }
  const sorted = Object.fromEntries(Object.entries(drawing).sort(([a], [b]) => (a < b ? -1 : a > b ? 1 : 0)));
  expect(Object.keys(sorted).length).toBeGreaterThan(40);
  if (WRITE) {
    if (!existsSync(dirname(GOLDEN))) mkdirSync(dirname(GOLDEN), { recursive: true });
    writeFileSync(GOLDEN, `${JSON.stringify(sorted, null, 2)}\n`, 'utf8');
    return;
  }
  expect(sorted).toEqual(JSON.parse(readFileSync(GOLDEN, 'utf8')) as Drawing);
});
