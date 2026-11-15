// map-gestures.spec.ts
// Journeys of gestures of the map that the journeys of the 1.x did not hold (annex B 2.1, 2.3, 2.5): the ribbon on one line and folded, the view moved by the middle and right buttons, a new row by a drop, a border of lanes pulled or double-clicked (ADR 096), the arrows up and down.

import type { Page } from '@playwright/test';
import { ROW_HEIGHT } from '../src/features/map/model/geometry/constants.ts';
import type { Api } from './support/api.ts';
import { expect, openMap, stepOf, test } from './support/journey.ts';
import { boxOf, canvas, cellCentre, laneHead, laneOf, ribbon, stepIn, zone } from './support/map-editing.ts';

/** The horizontal translation of the view of React Flow, read from its transform. */
const viewX = (page: Page): Promise<number> =>
  page.locator('.react-flow__viewport').evaluate((element) => new DOMMatrixReadOnly(getComputedStyle(element).transform).m41);

/** A drag of the pointer with one button, in steps, as a hand does. */
async function drag(page: Page, from: { x: number; y: number }, dx: number, button: 'middle' | 'right'): Promise<void> {
  await page.mouse.move(from.x, from.y);
  await page.mouse.down({ button });
  await page.mouse.move(from.x + dx, from.y, { steps: 12 });
  await page.mouse.up({ button });
}

/** Twelve steps in a row on one lane, wider than the screen. */
async function wideMap(api: Api): Promise<string> {
  const { mapId } = await api.project('GES', 'Gestes');
  const lane = await laneOf(api, mapId);
  for (let column = 0; column < 12; column++) await api.step(mapId, lane, column, `Étape ${column + 1}`);
  return mapId;
}

test('the ribbon holds on one line at 1366 px, unfolded by default, folded and unfolded by its button and by Ctrl+F1 (B-2.1-33, AR-90)', async ({ page, api }) => {
  await openMap(page, await wideMap(api), 'GES-E001');
  const buttons = ribbon(page).getByRole('group').getByRole('button');
  const tops = await buttons.evaluateAll((all) => [...new Set(all.map((button) => Math.round(button.getBoundingClientRect().top)))]);
  expect(tops).toHaveLength(1);
  expect(await ribbon(page).evaluate((element) => element.scrollWidth <= element.clientWidth)).toBe(true);
  await expect(page.getByTestId('ribbon-fold')).toHaveAttribute('aria-expanded', 'true');

  await page.getByTestId('ribbon-fold').click();
  await expect(page.getByTestId('ribbon-folded')).toBeVisible();
  await expect(ribbon(page)).toHaveCount(0);
  await page.locator('.react-flow__pane').click({ position: { x: 700, y: 500 } });
  await page.keyboard.press('Control+F1');
  await expect(ribbon(page)).toBeVisible();
  await expect(page.getByTestId('ribbon-fold')).toHaveAttribute('aria-expanded', 'true');
});

test('with the Selection tool, the middle and the right buttons move the view (B-2.3-04)', async ({ page, api }) => {
  await openMap(page, await wideMap(api), 'GES-E001');
  await zone(page, 'Outils').getByRole('radio', { name: 'Sélection' }).click();
  const area = await boxOf(canvas(page));
  const empty = { x: area.x + 900, y: area.y + area.height - 60 };
  const start = await viewX(page);

  await drag(page, empty, -300, 'middle');
  await expect.poll(() => viewX(page)).toBeLessThan(start - 250);
  const middle = await viewX(page);
  await drag(page, empty, -200, 'right');
  await expect.poll(() => viewX(page)).toBeLessThan(middle - 150);
  // Nothing chosen on the way: the buttons other than the left one draw no rectangle.
  await expect(page.locator('.react-flow__node.selected')).toHaveCount(0);
});

test('a step dropped low in the last row of its lane makes a new row there (B-2.3-23)', async ({ page, api }) => {
  const { mapId } = await api.project('ROW', 'Rangées');
  const lane = await laneOf(api, mapId);
  await api.step(mapId, lane, 0, 'Recevoir');
  await api.step(mapId, lane, 1, 'Traiter');
  await openMap(page, mapId, 'ROW-E002');
  const map = await api.map(mapId);
  const code = map.lanes[0]?.code ?? '';
  const body = await boxOf(page.getByTestId(`lane-body-${code}`));
  const target = await cellCentre(page, laneHead(page, code), 3);
  const step = await boxOf(stepOf(page, 'ROW-E002'));

  // In the lowest 18 % of the last row (threshold 0.82): a new row under it.
  await page.mouse.move(step.x + step.width / 2, step.y + step.height / 2);
  await page.mouse.down();
  await page.mouse.move(target.x, body.y + body.height * 0.93, { steps: 15 });
  await page.mouse.up();
  await expect.poll(async () => {
    const moved = stepIn(await api.map(mapId), 'ROW-E002');
    return [moved.lane_id, moved.column, moved.row];
  }).toEqual([lane, 3, 1]);
});

test('a border of lanes pulled, what is under it going down with the hand; a step dropped in the empty row; pulled up, the host inserts the row, Ctrl+Z; double-clicked, the empty row taken out (ADR 096)', async ({ page, api }) => {
  const { mapId } = await api.project('GRW', 'Rangées tirées');
  const first = await laneOf(api, mapId);
  const second = await api.lane(mapId, 'Direction');
  await api.step(mapId, first, 0, 'Recevoir');
  await api.step(mapId, first, 1, 'Traiter');
  await api.step(mapId, second, 2, 'Valider');
  await openMap(page, mapId, 'GRW-E002');
  const head = laneHead(page, 'GRW-C01');
  const lower = laneHead(page, 'GRW-C02');
  const height = async (locator: typeof head) => Math.round((await boxOf(locator)).height);
  const before = await boxOf(head);
  const border = before.y + before.height;
  const x = before.x + 30;

  // Pulled down by a row: the first lane is taller while the hand holds the border, its new row in its colour; released, it stays.
  await page.mouse.move(x, border);
  await page.mouse.down();
  await page.mouse.move(x, border + 130, { steps: 10 });
  await expect(page.getByTestId('row-band-GRW-C01')).toBeVisible();
  expect(await height(head)).toBe(2 * ROW_HEIGHT);
  await page.mouse.up();
  await expect(page.getByTestId('row-band-GRW-C01')).toHaveCount(0);
  expect(await height(head)).toBe(2 * ROW_HEIGHT);

  // « Traiter » dragged into the empty row: its cell is drawn in dashes while held, and it lands there.
  const step = await boxOf(stepOf(page, 'GRW-E002'));
  const target = await cellCentre(page, head, 1);
  await page.mouse.move(step.x + step.width / 2, step.y + step.height / 2);
  await page.mouse.down();
  await page.mouse.move(target.x, target.y + ROW_HEIGHT, { steps: 15 });
  const cell = await boxOf(page.getByTestId('drop-box'));
  expect([Math.round(cell.x - step.x), Math.round(cell.y - step.y)]).toEqual([0, ROW_HEIGHT]);
  await page.mouse.up();
  await expect.poll(async () => {
    const moved = stepIn(await api.map(mapId), 'GRW-E002');
    return [moved.lane_id, moved.column, moved.row];
  }).toEqual([first, 1, 1]);
  await expect(page.getByTestId('drop-box')).toHaveCount(0);

  // The same border pulled up: the second lane gains a row at its top at once, « Valider » drawn lower; released, the host inserts it.
  const top = (await boxOf(lower)).y;
  await page.mouse.move(x, top);
  await page.mouse.down();
  await page.mouse.move(x, top - 60, { steps: 10 });
  await expect(page.getByTestId('row-band-GRW-C02')).toBeVisible();
  expect(await height(lower)).toBe(2 * ROW_HEIGHT);
  await page.mouse.up();
  await expect.poll(async () => stepIn(await api.map(mapId), 'GRW-E003').row).toBe(1);
  await expect.poll(() => height(lower)).toBe(2 * ROW_HEIGHT);

  // One Ctrl+Z takes the inserted row back, Ctrl+Y gives it again.
  await canvas(page).click({ position: { x: 600, y: 20 } });
  await page.keyboard.press('Control+z');
  await expect.poll(async () => stepIn(await api.map(mapId), 'GRW-E003').row).toBe(0);
  await page.keyboard.press('Control+y');
  await expect.poll(async () => stepIn(await api.map(mapId), 'GRW-E003').row).toBe(1);

  // A double click on the border: the last row of the first lane holds « Traiter » and stays; the empty first row of the second goes.
  const again = (await boxOf(lower)).y;
  await page.mouse.dblclick(x, again);
  await expect.poll(async () => stepIn(await api.map(mapId), 'GRW-E003').row).toBe(0);
  await expect.poll(() => height(lower)).toBe(ROW_HEIGHT);
  expect(await height(head)).toBe(2 * ROW_HEIGHT);
});

test('the arrow down takes the step chosen from the row 1 of its lane to the row 0 of the lane below (B-2.5-19)', async ({ page, api }) => {
  const { mapId } = await api.project('ARW', 'Flèches');
  const first = await laneOf(api, mapId);
  const second = await api.lane(mapId, 'Direction');
  await api.step(mapId, first, 0, 'Haut');
  await api.step(mapId, first, 0, 'Bas', 'task', 1);
  await api.step(mapId, second, 2, 'Valider');
  await api.step(mapId, second, 2, 'Archiver', 'task', 1);
  await openMap(page, mapId, 'ARW-E002');

  await stepOf(page, 'ARW-E002').click();
  await page.keyboard.press('ArrowDown');
  await expect.poll(async () => {
    const moved = stepIn(await api.map(mapId), 'ARW-E002');
    return [moved.lane_id, moved.column, moved.row];
  }).toEqual([second, 0, 0]);
});
