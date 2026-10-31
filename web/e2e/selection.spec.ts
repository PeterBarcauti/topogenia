// selection.spec.ts
// Journeys of the selection (ported from the 1.x): copy and paste in the flow, cut and paste elsewhere with the same code, Escape gives up, a deletion stitches the flow, detail the selection, choose all, the arrows of the keyboard.

import type { Page } from '@playwright/test';
import type { Api } from './support/api.ts';
import { expect, openMap, stepOf, test } from './support/journey.ts';

/** Lane « Controlling »: Extraction (E001) -> Tableau (E002) -> Envoi (E003); lane « Logistique » empty. Gives the map. */
async function layOut(page: Page, api: Api): Promise<string> {
  const { mapId } = await api.project('SEL', 'Sélection');
  const first = (await api.map(mapId)).lanes[0];
  await api.send('PATCH', `/api/lanes/${first?.id ?? ''}`, { name: 'Controlling' }, first?.version);
  await api.lane(mapId, 'Logistique');
  const ids: string[] = [];
  for (const [column, label] of ['Extraction', 'Tableau', 'Envoi'].entries()) ids.push(await api.step(mapId, first?.id ?? '', column, label));
  await api.flow(mapId, ids[0] ?? '', ids[1] ?? '');
  await api.flow(mapId, ids[1] ?? '', ids[2] ?? '');
  await openMap(page, mapId, 'SEL-E003');
  return mapId;
}

/** The flows of the map read on the host: « SEL-E001>SEL-E002 », sorted. */
async function arrows(api: Api, mapId: string): Promise<string[]> {
  const map = await api.map(mapId);
  const code = new Map(map.steps.map((step) => [step.id, step.code]));
  return map.flows.map((flow) => `${code.get(flow.source_step_id) ?? ''}>${code.get(flow.target_step_id) ?? ''}`).sort();
}

const chosen = (page: Page) => page.locator('.react-flow__node-step.selected');
const cut = (page: Page) => page.locator('.step.is-cut');

async function boxOf(page: Page, code: string): Promise<{ x: number; y: number; width: number; height: number }> {
  const box = await stepOf(page, code).boundingBox();
  if (box === null) throw new Error(`${code} is not on the screen.`);
  return box;
}

/** A rectangle drawn around the steps with the tool Selection (the map opens with the Hand, B-7-07). */
async function rectangle(page: Page, codes: readonly string[]): Promise<void> {
  await page.getByRole('toolbar').getByRole('radio', { name: 'Sélection' }).click();
  const boxes = await Promise.all(codes.map((code) => boxOf(page, code)));
  await page.mouse.move(Math.min(...boxes.map((box) => box.x)) - 15, Math.min(...boxes.map((box) => box.y)) - 15);
  await page.mouse.down();
  await page.mouse.move(Math.max(...boxes.map((box) => box.x + box.width)) + 15, Math.max(...boxes.map((box) => box.y + box.height)) + 15, { steps: 10 });
  await page.mouse.up();
  await expect(chosen(page)).toHaveCount(codes.length);
}

test('copy then paste with one step chosen: the copy goes in the flow after it; undoable (B-8.3-54)', async ({ page, api }) => {
  const mapId = await layOut(page, api);
  await stepOf(page, 'SEL-E001').click();
  await page.keyboard.press('Control+c');
  await expect(page.getByText('1 étape copiée.')).toBeVisible();
  await stepOf(page, 'SEL-E002').click();
  await page.keyboard.press('Control+v');
  await expect(stepOf(page, 'SEL-E004')).toContainText('Extraction');
  await expect.poll(() => arrows(api, mapId)).toEqual(['SEL-E001>SEL-E002', 'SEL-E002>SEL-E004', 'SEL-E004>SEL-E003']);
  // The copy is chosen, to be moved at once (B-2.3-30); Envoi was pushed one column.
  await expect(chosen(page)).toHaveCount(1);
  await expect(chosen(page).getByTestId('step-SEL-E004')).toHaveCount(1);
  expect((await boxOf(page, 'SEL-E003')).x).toBeGreaterThan((await boxOf(page, 'SEL-E004')).x);

  await page.keyboard.press('Control+z');
  await expect(stepOf(page, 'SEL-E004')).toHaveCount(0);
  await expect.poll(() => arrows(api, mapId)).toEqual(['SEL-E001>SEL-E002', 'SEL-E002>SEL-E003']);
});

test('cut then « Coller ici »: the step keeps its code and leaves the stitched flow; Escape gives up; undoable (B-8.3-55, B-8.3-56)', async ({ page, api }) => {
  const mapId = await layOut(page, api);
  await stepOf(page, 'SEL-E002').click();
  await page.keyboard.press('Control+x');
  await expect(cut(page)).toHaveCount(1);
  await page.keyboard.press('Escape');
  await expect(cut(page)).toHaveCount(0);

  await stepOf(page, 'SEL-E002').click();
  await page.getByRole('toolbar').getByRole('button', { name: 'Couper', exact: true }).click();
  await expect(cut(page)).toHaveCount(1);
  // Right click on the background of the lane « Logistique », column 2 (the sheet open on the right covers the rest).
  const logistics = await page.getByTestId('lane-head-SEL-C02').boundingBox();
  const table = await boxOf(page, 'SEL-E002');
  if (logistics === null) throw new Error('The lane Logistique is not on the screen.');
  await page.mouse.click(table.x + 220 + table.width / 2, logistics.y + logistics.height / 2, { button: 'right' });
  await page.getByRole('menuitem', { name: 'Coller ici' }).click();
  await expect.poll(async () => (await boxOf(page, 'SEL-E002')).y).toBeGreaterThan(table.y + 80);
  await expect(cut(page)).toHaveCount(0);
  await expect(stepOf(page, 'SEL-E002')).toContainText('Tableau');
  await expect.poll(() => arrows(api, mapId)).toEqual(['SEL-E001>SEL-E003']);
  const map = await api.map(mapId);
  expect(map.steps.find((step) => step.code === 'SEL-E002')).toMatchObject({ lane_id: map.lanes[1]?.id, column: 2 });

  await page.keyboard.press('Control+z');
  await expect.poll(async () => (await boxOf(page, 'SEL-E002')).y).toBeLessThan(table.y + 20);
  await expect.poll(() => arrows(api, mapId)).toEqual(['SEL-E001>SEL-E002', 'SEL-E002>SEL-E003']);
});

test('deleting a step in the middle of the flow stitches the flows, as Visio (B-8.3-57)', async ({ page, api }) => {
  const mapId = await layOut(page, api);
  await stepOf(page, 'SEL-E002').click();
  await page.keyboard.press('Delete');
  await expect(stepOf(page, 'SEL-E002')).toHaveCount(0);
  await expect.poll(() => arrows(api, mapId)).toEqual(['SEL-E001>SEL-E003']);
  await page.keyboard.press('Control+z');
  await expect(stepOf(page, 'SEL-E002')).toBeVisible();
  await expect.poll(() => arrows(api, mapId)).toEqual(['SEL-E001>SEL-E002', 'SEL-E002>SEL-E003']);

  // Backspace deletes as Delete does (B-2.5-23).
  await stepOf(page, 'SEL-E002').click();
  await page.keyboard.press('Backspace');
  await expect(stepOf(page, 'SEL-E002')).toHaveCount(0);
  await expect.poll(() => arrows(api, mapId)).toEqual(['SEL-E001>SEL-E003']);
  await page.keyboard.press('Control+z');
  await expect(stepOf(page, 'SEL-E002')).toBeVisible();
});

test('group the selection into a sub-process: a step to name takes the place of the block, its detailed map holds it (B-8.3-58, AR-154)', async ({ page, api }) => {
  const mapId = await layOut(page, api);
  await rectangle(page, ['SEL-E002', 'SEL-E003']);
  // The right click lands on the frame of the selection, over the steps.
  const table = await boxOf(page, 'SEL-E002');
  await page.mouse.click(table.x + table.width / 2, table.y + table.height / 2, { button: 'right' });
  await expect(page.getByRole('menuitem', { name: 'Supprimer les 2 étapes' })).toBeVisible();
  await page.getByRole('menuitem', { name: 'Grouper en sous-processus' }).click();
  const field = page.getByTestId('step-label-input');
  await expect(field).toBeFocused();
  await field.fill('Diffuser le tableau');
  await field.press('Enter');
  await expect(stepOf(page, 'SEL-E004')).toHaveClass(/\bis-macro\b/);
  await expect(stepOf(page, 'SEL-E004')).toContainText('Diffuser le tableau');
  await expect.poll(() => arrows(api, mapId)).toEqual(['SEL-E001>SEL-E004']);

  await stepOf(page, 'SEL-E004').dblclick();
  await expect(page.getByTestId('breadcrumb').locator('[aria-current="page"]')).toHaveText('Diffuser le tableau');
  await expect(stepOf(page, 'SEL-E002')).toBeVisible();
  await expect(stepOf(page, 'SEL-E003')).toBeVisible();
  await expect(page.locator('.react-flow__edge')).toHaveCount(1);
});

test('choose all and the arrows of the keyboard: the selection changes lane and column (B-8.3-59, B-8.3-60)', async ({ page, api }) => {
  await layOut(page, api);
  await page.locator('.react-flow__pane').click({ position: { x: 600, y: 300 } });
  await page.keyboard.press('Control+a');
  await expect(chosen(page)).toHaveCount(3);

  await stepOf(page, 'SEL-E001').click();
  const before = await boxOf(page, 'SEL-E001');
  await page.keyboard.press('ArrowDown');
  await expect.poll(async () => (await boxOf(page, 'SEL-E001')).y).toBeGreaterThan(before.y + 80);
  await page.keyboard.press('ArrowRight');
  await expect.poll(async () => (await boxOf(page, 'SEL-E001')).x).toBeGreaterThan(before.x + 200);
  await page.keyboard.press('Control+z');
  await page.keyboard.press('Control+z');
  await expect.poll(async () => (await boxOf(page, 'SEL-E001')).y).toBeLessThan(before.y + 20);
  await expect.poll(async () => (await boxOf(page, 'SEL-E001')).x).toBeLessThan(before.x + 20);
});
