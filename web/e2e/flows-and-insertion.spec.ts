// flows-and-insertion.spec.ts
// Journeys of the flows and of the insertion (1.x liens-et-insertion.spec.ts): a line drawn from A to B makes A -> B, a flow reversed from its menu (AR-155), a flow between lanes never runs behind a step, insert before and after, the four quick « + », none in the link mode, its cursors (AR-153).

import type { Page } from '@playwright/test';
import type { Api } from './support/api.ts';
import { expect, openMap, stepOf, test } from './support/journey.ts';

interface Layout {
  readonly mapId: string;
  /** The steps by their short code: E001 to E003 in the first lane, E004 in « Logistique ». */
  readonly ids: Readonly<Record<string, string>>;
}

/** The layout of the 1.x capture: first lane E001 E002 E003, « Logistique » E004; the flows given, then the map opened. */
async function layOut(page: Page, api: Api, flows: readonly (readonly [string, string])[] = []): Promise<Layout> {
  const { mapId } = await api.project('SALES', 'Ventes');
  const first = (await api.map(mapId)).lanes[0]?.id ?? '';
  const logistics = await api.lane(mapId, 'Logistique');
  const ids: Record<string, string> = {
    E001: await api.step(mapId, first, 0, 'Extraction SAP - Ventes'),
    E002: await api.step(mapId, first, 1, 'Transformation données'),
    E003: await api.step(mapId, first, 2, 'Ajouts objectifs'),
    E004: await api.step(mapId, logistics, 0, 'Extraction SAP - Volume'),
  };
  for (const [source, target] of flows) await api.flow(mapId, ids[source] ?? '', ids[target] ?? '');
  await openMap(page, mapId, 'SALES-E004');
  return { mapId, ids };
}

/** The flows of the map read on the host, as « E001>E002 », sorted. */
async function arrows(api: Api, mapId: string): Promise<string[]> {
  const map = await api.map(mapId);
  const code = new Map(map.steps.map((step) => [step.id, step.code.replace('SALES-', '')]));
  return map.flows.map((flow) => `${code.get(flow.source_step_id) ?? ''}>${code.get(flow.target_step_id) ?? ''}`).sort();
}

/** Draws a line from a handle of a step and drops it in the middle of another step, on its body. */
async function draw(page: Page, from: string, side: 'right' | 'bottom', to: string): Promise<void> {
  const source = stepOf(page, from);
  await source.hover();
  const handle = await source.locator(`.react-flow__handle-${side}`).boundingBox();
  const target = await stepOf(page, to).boundingBox();
  if (handle === null || target === null) throw new Error('A handle or a step is not on the screen.');
  await page.mouse.move(handle.x + handle.width / 2, handle.y + handle.height / 2);
  await page.mouse.down();
  await page.mouse.move(target.x + target.width / 2, target.y + target.height / 2, { steps: 12 });
  await page.mouse.up();
}

test('the line drawn from A to B makes the flow A -> B, even dropped in the middle of B (B-8.3-32)', async ({ page, api }) => {
  const { mapId, ids } = await layOut(page, api);

  await draw(page, 'SALES-E001', 'right', 'SALES-E002');
  await expect.poll(async () => (await api.map(mapId)).flows.length).toBe(1);
  let flow = (await api.map(mapId)).flows[0];
  expect([flow?.source_step_id, flow?.target_step_id]).toEqual([ids.E001, ids.E002]);

  // The case of the capture: from step 3 to step 1 of another lane, here from the handle below (any handle, B-2.3-19).
  await draw(page, 'SALES-E003', 'bottom', 'SALES-E004');
  await expect.poll(async () => (await api.map(mapId)).flows.length).toBe(2);
  flow = (await api.map(mapId)).flows.find((candidate) => candidate.source_step_id === ids.E003);
  expect(flow?.target_step_id).toBe(ids.E004);
  expect(await arrows(api, mapId)).toEqual(['E001>E002', 'E003>E004']);
});

test('right click on a flow: « Inverser le sens » turns it the other way; Ctrl+Z puts it back (AR-155)', async ({ page, api }) => {
  const { mapId } = await layOut(page, api, [['E001', 'E002']]);
  const flowId = (await api.map(mapId)).flows[0]?.id ?? '';
  const [from, to] = [await stepOf(page, 'SALES-E001').boundingBox(), await stepOf(page, 'SALES-E002').boundingBox()];
  if (from === null || to === null) throw new Error('A step is not on the screen.');
  // A straight flow has no height, which Playwright takes for hidden: the right click lands on its line, between the steps.
  await page.mouse.click((from.x + from.width + to.x) / 2, from.y + from.height / 2, { button: 'right' });
  const menu = page.getByTestId('menu-flow');
  await expect(menu.getByRole('menuitem')).toHaveText(['Libellé de la flèche', 'Inverser le sens', 'Supprimer la flèche']);
  await menu.getByRole('menuitem', { name: 'Inverser le sens' }).click();
  await expect.poll(() => arrows(api, mapId)).toEqual(['E002>E001']);
  expect((await api.map(mapId)).flows.map((flow) => flow.id)).toEqual([flowId]);
  await page.locator('.react-flow__pane').click({ position: { x: 600, y: 450 } });
  await page.keyboard.press('Control+z');
  await expect.poll(() => arrows(api, mapId)).toEqual(['E001>E002']);
});

test('a flow between lanes does not run behind another step (B-8.3-33)', async ({ page, api }) => {
  const { mapId, ids } = await layOut(page, api);
  const flowId = await api.flow(mapId, ids.E004 ?? '', ids.E003 ?? '');
  await page.reload();
  await expect(page.getByTestId(`rf__edge-${flowId}`).locator('path.react-flow__edge-path')).toHaveCount(1);
  await expect(stepOf(page, 'SALES-E002')).toBeVisible();

  // Samples the drawn path and checks that no point falls in the box of E002.
  const inside = await page.evaluate((id) => {
    const path = document.querySelector<SVGPathElement>(`[data-testid="rf__edge-${id}"] path.react-flow__edge-path`);
    const box = document.querySelector('[data-testid="step-SALES-E002"]')?.getBoundingClientRect();
    const screen = path?.getScreenCTM();
    if (path == null || box === undefined || screen == null) return ['missing'];
    const points: string[] = [];
    const length = path.getTotalLength();
    for (let at = 0; at <= length; at += 4) {
      const point = path.getPointAtLength(at).matrixTransform(screen);
      if (point.x > box.left + 2 && point.x < box.right - 2 && point.y > box.top + 2 && point.y < box.bottom - 2) points.push(`${Math.round(point.x)},${Math.round(point.y)}`);
    }
    return length > 0 ? points : ['empty'];
  }, flowId);
  expect(inside).toEqual([]);
});

/** Types the name of the step just made, its field focused at once, and validates it. */
async function typeLabel(page: Page, text: string): Promise<void> {
  const field = page.getByTestId('step-label-input');
  await expect(field).toBeFocused();
  await field.fill(text);
  await field.press('Enter');
  await expect(field).toHaveCount(0);
}

/** The columns of the steps of the first lane, as « column:label », in their order. */
async function firstLane(api: Api, mapId: string): Promise<string[]> {
  const map = await api.map(mapId);
  const lane = map.lanes[0]?.id;
  return map.steps.filter((step) => step.lane_id === lane).sort((a, b) => a.column - b.column).map((step) => `${step.column}:${step.label}`);
}

const columnOf = async (api: Api, mapId: string, code: string) => (await api.map(mapId)).steps.find((step) => step.code === code)?.column;

test('right click: insert a step before then after, in the flow; undone as the rest (B-8.3-34)', async ({ page, api }) => {
  const { mapId } = await layOut(page, api, [['E001', 'E002'], ['E002', 'E003']]);

  await stepOf(page, 'SALES-E002').click({ button: 'right' });
  await page.getByRole('menuitem', { name: 'Insérer une étape avant' }).click();
  await typeLabel(page, 'Contrôle des ventes');
  await expect(stepOf(page, 'SALES-E005')).toContainText('Contrôle des ventes');
  // E001 > E002 is cut: E001 > E005 > E002.
  await expect.poll(() => arrows(api, mapId)).toEqual(['E001>E005', 'E002>E003', 'E005>E002']);

  await stepOf(page, 'SALES-E003').click({ button: 'right' });
  await page.getByRole('menuitem', { name: 'Insérer une étape après' }).click();
  await typeLabel(page, 'Envoi');
  await expect(stepOf(page, 'SALES-E006')).toContainText('Envoi');
  // E003 had no next step: the new one is simply joined to it.
  await expect.poll(() => arrows(api, mapId)).toEqual(['E001>E005', 'E002>E003', 'E003>E006', 'E005>E002']);
  expect(await firstLane(api, mapId)).toEqual(['0:Extraction SAP - Ventes', '1:Contrôle des ventes', '2:Transformation données', '3:Ajouts objectifs', '4:Envoi']);

  // An insertion is undone as the rest: first the renaming, then the insertion.
  await page.keyboard.press('Control+z');
  await expect(stepOf(page, 'SALES-E006')).toContainText('Nouvelle étape');
  await page.keyboard.press('Control+z');
  await expect(stepOf(page, 'SALES-E006')).toHaveCount(0);
  await expect.poll(() => columnOf(api, mapId, 'SALES-E003')).toBe(3);

  // The first insertion undone (renaming, then insertion): the flow of the start is back.
  await page.keyboard.press('Control+z');
  await page.keyboard.press('Control+z');
  await expect(stepOf(page, 'SALES-E005')).toHaveCount(0);
  await expect.poll(() => arrows(api, mapId)).toEqual(['E001>E002', 'E002>E003']);
  await expect.poll(() => columnOf(api, mapId, 'SALES-E002')).toBe(1);
});

/** Hovers the step, then moves the mouse pixel by pixel to its « + » and clicks it, as a hand crosses each pixel. */
async function clickPlus(page: Page, code: string, position: 'after' | 'before' | 'below' | 'above'): Promise<void> {
  const box = await stepOf(page, code).boundingBox();
  if (box === null) throw new Error(`${code} is not on the screen.`);
  await page.mouse.move(box.x + box.width / 2, box.y + box.height / 2);
  const plus = stepOf(page, code).getByTestId(`quick-add-${position}`);
  await expect(plus).toHaveCSS('opacity', '1');
  const button = await plus.boundingBox();
  if (button === null) throw new Error(`The « + » ${position} of ${code} is not on the screen.`);
  await page.mouse.move(button.x + button.width / 2, button.y + button.height / 2, { steps: 120 });
  await page.mouse.down();
  await page.mouse.up();
}

test('quick add on hover: « + » right, left, below, above (B-8.3-35)', async ({ page, api }) => {
  const { mapId } = await layOut(page, api, [['E001', 'E002']]);

  // The « + » show only on hover; none above in the first lane, none below in the last one (B-2.3-42, B-2.3-43).
  await page.mouse.move(5, 700);
  await expect(stepOf(page, 'SALES-E001').getByTestId('quick-add-after')).toHaveCSS('opacity', '0');
  await expect(stepOf(page, 'SALES-E001').getByTestId('quick-add-above')).toHaveCount(0);
  await expect(stepOf(page, 'SALES-E001').getByTestId('quick-add-below')).toHaveCount(1);
  await expect(stepOf(page, 'SALES-E004').getByTestId('quick-add-below')).toHaveCount(0);
  await expect(stepOf(page, 'SALES-E004').getByTestId('quick-add-above')).toHaveCount(1);

  await clickPlus(page, 'SALES-E001', 'after');
  await typeLabel(page, 'Vérification');
  await expect.poll(() => arrows(api, mapId)).toEqual(['E001>E005', 'E005>E002']);

  await clickPlus(page, 'SALES-E001', 'before');
  await typeLabel(page, 'Début de mois');
  await expect.poll(() => arrows(api, mapId)).toEqual(['E001>E005', 'E005>E002', 'E006>E001']);

  await clickPlus(page, 'SALES-E002', 'below');
  await typeLabel(page, 'Contrôle logistique');
  await expect.poll(() => arrows(api, mapId)).toContain('E002>E007');
  const map = await api.map(mapId);
  expect(map.steps.find((step) => step.code === 'SALES-E007')?.lane_id).toBe(map.lanes[1]?.id);

  await clickPlus(page, 'SALES-E007', 'above');
  await typeLabel(page, 'Retour controlling');
  await expect.poll(() => arrows(api, mapId)).toContain('E007>E008');
  expect((await api.map(mapId)).steps.find((step) => step.code === 'SALES-E008')?.lane_id).toBe(map.lanes[0]?.id);
});

test('no « + » in the link mode (B-8.3-36)', async ({ page, api }) => {
  await layOut(page, api);
  const plus = page.locator('[data-testid^="quick-add-"]');
  await stepOf(page, 'SALES-E001').hover();
  await expect(stepOf(page, 'SALES-E001').getByTestId('quick-add-after')).toHaveCSS('opacity', '1');

  // The link mode (B-2.3-47).
  await page.getByRole('toolbar').getByRole('button', { name: 'Relier', exact: true }).click();
  await stepOf(page, 'SALES-E001').hover();
  await expect(plus).toHaveCount(0);
  await page.keyboard.press('Escape');
  await expect(plus.first()).toBeAttached();

  // The RACI layer on (B-2.3-48).
  await page.getByRole('toolbar').getByRole('button', { name: 'RACI', exact: true }).click();
  await stepOf(page, 'SALES-E001').hover();
  await expect(plus).toHaveCount(0);
});

/** The image of the cursor of an element, read as the browser computes it, and whether the browser draws it under the policy of the host. */
async function cursorImage(page: Page, selector: string): Promise<{ url: string; drawn: boolean }> {
  return page.locator(selector).first().evaluate(async (element) => {
    const url = /url\("([^"]+)"\) 2 2, crosshair/.exec(getComputedStyle(element).cursor)?.[1] ?? '';
    const image = new Image();
    image.src = url;
    const drawn = url !== '' && (await image.decode().then(() => image.naturalWidth === 24, () => false));
    return { url, drawn };
  });
}

test('the link mode: a spool of thread over the map, the thread unwound once the start is chosen, under the policy of the host (AR-153)', async ({ page, api }) => {
  const refused: string[] = [];
  page.on('console', (message) => {
    if (message.text().includes('Content Security Policy')) refused.push(message.text());
  });
  await layOut(page, api, [['E001', 'E002']]);
  await page.getByRole('toolbar').getByRole('button', { name: 'Relier', exact: true }).click();
  const spool = await cursorImage(page, '.react-flow__pane');
  expect(spool.drawn).toBe(true);
  for (const selector of ['[data-testid="step-SALES-E001"]', '.react-flow__node-step .step-handle', '.react-flow__edge'])
    expect((await cursorImage(page, selector)).url).toBe(spool.url);
  await stepOf(page, 'SALES-E001').click();
  await expect(stepOf(page, 'SALES-E001')).toHaveClass(/is-link-source/);
  const thread = await cursorImage(page, '.react-flow__pane');
  expect([thread.drawn, thread.url === spool.url, (await cursorImage(page, '[data-testid="step-SALES-E004"]')).url === thread.url]).toEqual([true, false, true]);
  await page.keyboard.press('Escape');
  await expect(page.locator('.react-flow__pane')).not.toHaveCSS('cursor', /url\(/);
  expect(refused).toEqual([]);
});
