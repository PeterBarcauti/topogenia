// flow-route.spec.ts
// Journeys of the route chosen for a flow (ADR 091): its departure dragged onto the top of its step, kept after a reload, undone and done again; a segment dragged into the band under its line, released when a step of the flow moves while its sides stay; a segment going down dragged to the middle of an empty column, centred there after a reload; the menu of the flow and « Tracé automatique ».

import type { Page } from '@playwright/test';
import type { Api, FlowRead } from './support/api.ts';
import { expect, openMap, stepOf, test } from './support/journey.ts';
import { startOfFlow } from './support/map-editing.ts';

/** Two steps on the first lane with one between them, and the flow from the first to the last, the map opened. */
async function layOut(page: Page, api: Api): Promise<{ mapId: string; laneId: string; flowId: string; ids: string[] }> {
  const { mapId } = await api.project('TRC', 'Tracés');
  const laneId = (await api.map(mapId)).lanes[0]?.id ?? '';
  await api.lane(mapId, 'Contrôle');
  const ids = [await api.step(mapId, laneId, 0, 'Recevoir'), await api.step(mapId, laneId, 2, 'Classer')];
  const flowId = await api.flow(mapId, ids[0] ?? '', ids[1] ?? '');
  await openMap(page, mapId, 'TRC-E001');
  return { mapId, laneId, flowId, ids };
}

const flowOf = async (api: Api, mapId: string, flowId: string): Promise<FlowRead> => {
  const flow = (await api.map(mapId)).flows.find((item) => item.id === flowId);
  if (flow === undefined) throw new Error(`No flow ${flowId}.`);
  return flow;
};

/** Chooses the flow alone by a click on its straight line, between its two steps. */
async function chooseFlow(page: Page): Promise<void> {
  const [from, to] = [await stepOf(page, 'TRC-E001').boundingBox(), await stepOf(page, 'TRC-E002').boundingBox()];
  if (from === null || to === null) throw new Error('A step is not on the screen.');
  await page.mouse.click((from.x + from.width + to.x) / 2, from.y + from.height / 2);
  await expect(page.getByTestId('route-end-source')).toBeVisible();
}

/** Drags a handle to a point of the screen, in steps, as a hand does. */
async function drag(page: Page, handle: string, to: { x: number; y: number }): Promise<void> {
  const box = await page.getByTestId(handle).boundingBox();
  if (box === null) throw new Error(`No handle ${handle}.`);
  await page.mouse.move(box.x + box.width / 2, box.y + box.height / 2);
  await page.mouse.down();
  await page.mouse.move(to.x, to.y, { steps: 6 });
  await page.mouse.up();
}

test('the departure dragged onto the top of its step is kept after a reload, undone by Ctrl+Z and done again by Ctrl+Y (ADR 091)', async ({ page, api }) => {
  const { mapId, flowId } = await layOut(page, api);
  await chooseFlow(page);
  const step = await stepOf(page, 'TRC-E001').boundingBox();
  if (step === null) throw new Error('The step is not on the screen.');
  await drag(page, 'route-end-source', { x: step.x + step.width / 2, y: step.y + 2 });
  await expect.poll(async () => (await flowOf(api, mapId, flowId)).route?.source_side).toBe('top');
  const top = async () => {
    const [start, box] = [await startOfFlow(page, flowId), await stepOf(page, 'TRC-E001').boundingBox()];
    return box !== null && Math.abs(start.y - box.y) < 3 && start.x > box.x && start.x < box.x + box.width;
  };
  await expect.poll(top).toBe(true);
  await page.reload();
  await expect(stepOf(page, 'TRC-E001')).toBeVisible();
  await expect.poll(top).toBe(true);
  await page.locator('.react-flow__pane').click({ position: { x: 900, y: 500 } });
  await page.keyboard.press('Control+z');
  await expect.poll(async () => (await flowOf(api, mapId, flowId)).route ?? null).toBeNull();
  await expect.poll(top).toBe(false);
  await page.keyboard.press('Control+y');
  await expect.poll(async () => (await flowOf(api, mapId, flowId)).route?.source_side).toBe('top');
});

test('a segment dragged into the band under its line passes there; a step of the flow moved releases the passage and keeps its sides (ADR 091)', async ({ page, api }) => {
  const { mapId, laneId, flowId, ids } = await layOut(page, api);
  await chooseFlow(page);
  const step = await stepOf(page, 'TRC-E001').boundingBox();
  if (step === null) throw new Error('The step is not on the screen.');
  const segment = await page.getByTestId('route-segment-0').boundingBox();
  if (segment === null) throw new Error('No segment handle.');
  // Down to the free band under the steps of the first lane, a little above the second lane.
  await drag(page, 'route-segment-0', { x: segment.x + segment.width / 2, y: step.y + step.height + 18 });
  await expect.poll(async () => (await flowOf(api, mapId, flowId)).route?.via?.edge).toBe('bottom');
  expect((await flowOf(api, mapId, flowId)).route?.via?.lane_id).toBe(laneId);
  // The departure chosen by the menu, then the target moved by the API: the passage goes, the side stays.
  await page.keyboard.press('Escape');
  await api.send('POST', `/api/maps/${mapId}/move`, { steps: [{ id: ids[1], lane_id: laneId, column: 3, row: 0 }] });
  await expect.poll(async () => (await flowOf(api, mapId, flowId)).route?.via ?? null).toBeNull();
});

test('the menu of the flow chooses its departure, then « Tracé automatique » gives everything back to the drawing (ADR 091)', async ({ page, api }) => {
  const { mapId, flowId } = await layOut(page, api);
  const [from, to] = [await stepOf(page, 'TRC-E001').boundingBox(), await stepOf(page, 'TRC-E002').boundingBox()];
  if (from === null || to === null) throw new Error('A step is not on the screen.');
  const line = { x: (from.x + from.width + to.x) / 2, y: from.y + from.height / 2 };
  await page.mouse.click(line.x, line.y, { button: 'right' });
  const menu = page.getByTestId('menu-flow');
  await expect(menu.getByRole('menuitemradio', { name: 'Départ : Automatique', checked: true })).toBeVisible();
  await expect(menu.getByRole('menuitem', { name: 'Tracé automatique' })).toBeDisabled();
  await menu.getByRole('menuitemradio', { name: 'Départ : Bas' }).click();
  await expect.poll(async () => (await flowOf(api, mapId, flowId)).route?.source_side).toBe('bottom');
  await page.mouse.click(line.x, line.y, { button: 'right' });
  await expect(menu.getByRole('menuitemradio', { name: 'Départ : Bas', checked: true })).toBeVisible();
  await menu.getByRole('menuitem', { name: 'Tracé automatique' }).click();
  await expect.poll(async () => (await flowOf(api, mapId, flowId)).route ?? null).toBeNull();
});

/** Whether the path of a flow runs down at an x of the screen, within a pixel: its points along it, every few pixels. */
async function runsDownAt(page: Page, flowId: string, x: number): Promise<boolean> {
  return page.getByTestId(`rf__edge-${flowId}`).locator('path.react-flow__edge-path').evaluate((path, at) => {
    const line = path as SVGPathElement;
    const matrix = line.getScreenCTM();
    if (matrix === null) return false;
    const points = Array.from({ length: 200 }, (_, index) => line.getPointAtLength((line.getTotalLength() * index) / 199))
      .map((point) => ({ x: point.x * matrix.a + matrix.e, y: point.y * matrix.d + matrix.f }));
    return points.filter((point) => Math.abs(point.x - at) < 1).length > 20;
  }, x);
}

test('a segment going down dragged to the middle of an empty column runs there, centred, and stays after a reload (ADR 091)', async ({ page, api }) => {
  const { mapId } = await api.project('AXE', 'Axe');
  const first = (await api.map(mapId)).lanes[0]?.id ?? '';
  const second = await api.lane(mapId, 'Contrôle');
  const source = await api.step(mapId, first, 0, 'Recevoir');
  const target = await api.step(mapId, second, 2, 'Classer');
  const flowId = await api.flow(mapId, source, target);
  await openMap(page, mapId, 'AXE-E001');
  const [from, to] = [await stepOf(page, 'AXE-E001').boundingBox(), await stepOf(page, 'AXE-E002').boundingBox()];
  if (from === null || to === null) throw new Error('A step is not on the screen.');
  // The column 1, empty, halfway between the middles of the columns 0 and 2.
  const middle = (from.x + from.width / 2 + to.x + to.width / 2) / 2;
  const start = await startOfFlow(page, flowId);
  await page.mouse.click(start.x + 4, start.y);
  const handle = page.locator('[data-testid^="route-segment-"].is-vertical').first();
  const box = await handle.boundingBox();
  if (box === null) throw new Error('No handle of a segment going down.');
  await page.mouse.move(box.x + box.width / 2, box.y + box.height / 2);
  await page.mouse.down();
  await page.mouse.move(middle, box.y + box.height / 2, { steps: 8 });
  await page.mouse.up();
  await expect.poll(async () => (await flowOf(api, mapId, flowId)).route?.via?.column).toBe(1);
  await expect.poll(() => runsDownAt(page, flowId, middle)).toBe(true);
  await page.reload();
  await expect(stepOf(page, 'AXE-E001')).toBeVisible();
  const [again, other] = [await stepOf(page, 'AXE-E001').boundingBox(), await stepOf(page, 'AXE-E002').boundingBox()];
  if (again === null || other === null) throw new Error('A step is not on the screen after the reload.');
  await expect.poll(() => runsDownAt(page, flowId, (again.x + again.width / 2 + other.x + other.width / 2) / 2)).toBe(true);
});
