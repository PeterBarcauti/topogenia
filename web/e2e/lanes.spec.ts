// lanes.spec.ts
// Journeys of the lanes and their headers (annex B 2.12, 2.4, 2.1): renamed, reordered, coloured; the headers with the view; the mini map.

import type { Page } from '@playwright/test';
import { ROW_HEIGHT } from '../src/features/map/model/geometry/constants.ts';
import { LANE_TINTS } from '../src/shared/theme/laneTints.ts';
import type { Api } from './support/api.ts';
import { expectAccessible } from './support/axe.ts';
import { expect, openMap, stepOf, test } from './support/journey.ts';
import { boxOf, canvas, laneHead, laneOf, zone } from './support/map-editing.ts';

/** The translation and the zoom of the view of React Flow, read from its transform. */
const viewOf = (page: Page): Promise<{ x: number; y: number; zoom: number }> =>
  page.locator('.react-flow__viewport').evaluate((element) => {
    const matrix = new DOMMatrixReadOnly(getComputedStyle(element).transform);
    return { x: matrix.m41, y: matrix.m42, zoom: matrix.m11 };
  });

/** A drag of the left button, in steps, as a hand does. */
async function drag(page: Page, from: { x: number; y: number }, to: { x: number; y: number }): Promise<void> {
  await page.mouse.move(from.x, from.y);
  await page.mouse.down();
  await page.mouse.move(to.x, to.y, { steps: 15 });
  await page.mouse.up();
}

/** The lanes of the map by their order: code, name, colour. */
async function lanesOf(api: Api, mapId: string): Promise<string[]> {
  const map = await api.map(mapId);
  return [...map.lanes].sort((a, b) => a.sort_order - b.sort_order).map((lane) => `${lane.code} ${lane.name} ${lane.color.toUpperCase()}`);
}

test('a lane renamed by a double click on its header, reordered by dragging it, its colour chosen among eight dots (B-2.12-11, B-2.12-14, B-2.4-48)', async ({ page, api }) => {
  const { mapId } = await api.project('LAN', 'Couloirs');
  const lanes = [await laneOf(api, mapId), await api.lane(mapId, 'Achats'), await api.lane(mapId, 'Comptabilité')];
  for (const [index, lane] of lanes.entries()) await api.step(mapId, lane, index, `Étape ${index + 1}`);
  await openMap(page, mapId, 'LAN-E001');
  const colours = (await api.map(mapId)).lanes.map((lane) => lane.color.toUpperCase());

  await laneHead(page, 'LAN-C01').dblclick();
  await page.getByTestId('lane-name-input').fill('Ventes');
  await page.keyboard.press('Enter');
  await expect.poll(() => lanesOf(api, mapId)).toEqual([`LAN-C01 Ventes ${colours[0] ?? ''}`, `LAN-C02 Achats ${colours[1] ?? ''}`, `LAN-C03 Comptabilité ${colours[2] ?? ''}`]);

  // The header of the third lane dragged above the first one: the lanes take that order.
  const third = await boxOf(laneHead(page, 'LAN-C03'));
  const first = await boxOf(laneHead(page, 'LAN-C01'));
  await drag(page, { x: third.x + third.width / 2, y: third.y + third.height / 2 }, { x: first.x + first.width / 2, y: first.y + 8 });
  await expect.poll(async () => (await lanesOf(api, mapId)).map((lane) => lane.slice(0, 7))).toEqual(['LAN-C03', 'LAN-C01', 'LAN-C02']);

  // Its menu: eight colours as radio entries, each with its dot, the current one checked.
  await laneHead(page, 'LAN-C02').hover();
  await page.getByTestId('lane-more-LAN-C02').click();
  const entries = page.getByRole('menuitemradio', { name: /^Couleur : / });
  await expect(entries).toHaveCount(LANE_TINTS.length);
  await expect(page.getByRole('menuitemradio', { name: /^Couleur : /, checked: true })).toHaveCount(1);
  await expectAccessible(page, 'menu of a lane');
  const mint = LANE_TINTS.find((tint) => tint.key === 'mint');
  await page.getByRole('menuitemradio', { name: 'Couleur : Menthe' }).click();
  await expect.poll(async () => (await lanesOf(api, mapId)).find((lane) => lane.startsWith('LAN-C02'))).toBe(`LAN-C02 Achats ${mint?.pastel.toUpperCase() ?? ''}`);
});

test('the lane headers follow the vertical scroll and the zoom, not the horizontal one; a header is as high as its rows (B-2.12-02, B-2.12-03, B-2.12-10)', async ({ page, api }) => {
  const { mapId } = await api.project('HDR', 'En-têtes');
  const first = await laneOf(api, mapId);
  for (let column = 0; column < 12; column++) await api.step(mapId, first, column, `Étape ${column + 1}`);
  await api.step(mapId, first, 0, 'Seconde rangée', 'task', 1);
  for (let rank = 2; rank <= 6; rank++) await api.step(mapId, await api.lane(mapId, `Service ${rank}`), 0, `Tâche ${rank}`);
  await openMap(page, mapId, 'HDR-E001');
  const head = laneHead(page, 'HDR-C01');
  const second = laneHead(page, 'HDR-C02');
  expect((await boxOf(head)).height).toBeCloseTo(2 * ROW_HEIGHT, 0);
  expect((await boxOf(second)).height).toBeCloseTo(ROW_HEIGHT, 0);

  // The Hand drags the view left: the headers stay, the steps go.
  await zone(page, 'Outils').getByRole('radio', { name: 'Main' }).click();
  const area = await boxOf(canvas(page));
  const onLane = { x: area.x + 900, y: area.y + 200 };
  const [headBefore, stepBefore] = [await boxOf(head), await boxOf(stepOf(page, 'HDR-E001'))];
  await drag(page, onLane, { x: onLane.x - 400, y: onLane.y });
  expect((await boxOf(head)).x).toBeCloseTo(headBefore.x, 0);
  expect((await boxOf(stepOf(page, 'HDR-E001'))).x).toBeLessThan(stepBefore.x - 300);

  // Up: the headers go up with their lanes.
  const stepTop = (await boxOf(stepOf(page, 'HDR-E001'))).y;
  await drag(page, onLane, { x: onLane.x, y: onLane.y - 150 });
  const moved = (await boxOf(stepOf(page, 'HDR-E001'))).y - stepTop;
  expect(moved).toBeLessThan(-50);
  expect((await boxOf(head)).y - headBefore.y).toBeCloseTo(moved, 0);

  // Ctrl and the wheel zoom (spec 3.0 section 15.1): a header is as high as its rows at that zoom.
  await page.mouse.move(area.x + 700, area.y + 300);
  await page.keyboard.down('Control');
  await page.mouse.wheel(0, 300);
  await page.keyboard.up('Control');
  await expect.poll(async () => (await viewOf(page)).zoom).toBeLessThan(0.95);
  const { zoom } = await viewOf(page);
  expect((await boxOf(second)).height).toBeCloseTo(ROW_HEIGHT * zoom, 0);
});

test('the mini map moves the view by a drag and zooms it by the wheel (B-2.1-45)', async ({ page, api }) => {
  const { mapId } = await api.project('MIN', 'Mini-carte');
  const lane = await laneOf(api, mapId);
  for (let column = 0; column < 16; column++) await api.step(mapId, lane, column, `Étape ${column + 1}`);
  await openMap(page, mapId, 'MIN-E001');
  const mini = await boxOf(page.locator('.map-minimap'));
  const before = await viewOf(page);

  await drag(page, { x: mini.x + mini.width / 3, y: mini.y + mini.height / 2 }, { x: mini.x + mini.width / 3 + 40, y: mini.y + mini.height / 2 });
  await expect.poll(async () => (await viewOf(page)).x).toBeLessThan(before.x - 50);
  const panned = await viewOf(page);
  expect(panned.zoom).toBeCloseTo(before.zoom, 3);

  await page.mouse.move(mini.x + mini.width / 2, mini.y + mini.height / 2);
  await page.mouse.wheel(0, 300);
  await expect.poll(async () => (await viewOf(page)).zoom).toBeLessThan(panned.zoom - 0.05);
});
