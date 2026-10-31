// bounded-view.spec.ts
// Journeys of the view hooked under the ribbon (annex B 2.2, 8.3): top fixed, vertical scroll only when the lanes pass the screen, never left of the first column, zoom anchored at the top, back to the start; the wheel scrolls, Shift and the wheel go sideways, Ctrl and the wheel zoom (spec 3.0 section 15.1).

import type { Page } from '@playwright/test';
import { COLUMN_WIDTH, FREE_COLUMNS, LANE_HEADER_WIDTH, RIGHT_MARGIN, STEP_WIDTH } from '../src/features/map/model/geometry/constants.ts';
import type { Api } from './support/api.ts';
import { expect, openMap, stepOf, test } from './support/journey.ts';
import { boxOf, canvas, laneOf, type Point } from './support/map-editing.ts';

/** A map of `lanes` lanes and `columns` steps in the first lane. */
async function layOut(page: Page, api: Api, lanes: number, columns: number): Promise<void> {
  const { mapId } = await api.project('VUE', 'Vue');
  for (let rank = 1; rank < lanes; rank++) await api.lane(mapId, `Couloir ${String(rank + 1)}`);
  const first = await laneOf(api, mapId);
  for (let column = 0; column < columns; column++) await api.step(mapId, first, column, `Etape ${String(column + 1)}`);
  await openMap(page, mapId, 'VUE-E001');
}

const heads = (page: Page) => page.getByTestId('lane-headers').locator('.lane-head');

/** The top of the first lane, from the top of the canvas. */
async function firstLaneTop(page: Page): Promise<number> {
  const area = await boxOf(canvas(page));
  return (await boxOf(heads(page).first())).y - area.y;
}

const firstStepLeft = async (page: Page) => (await boxOf(stepOf(page, 'VUE-E001'))).x;

/** Moves the view: the Space bar held (the Hand for the time being), whatever the tool chosen. */
async function drag(page: Page, from: Point, dx: number, dy: number): Promise<void> {
  await page.mouse.move(from.x, from.y);
  await page.keyboard.down('Space');
  await page.mouse.down();
  await page.mouse.move(from.x + dx, from.y + dy, { steps: 10 });
  await page.mouse.up();
  await page.keyboard.up('Space');
}

/** Turns of the wheel at a point of the screen, a key held or not (spec 3.0 section 15.1). */
async function wheel(page: Page, at: Point, dx: number, dy: number, key?: 'Control' | 'Shift'): Promise<void> {
  await page.mouse.move(at.x, at.y);
  if (key !== undefined) await page.keyboard.down(key);
  await page.mouse.wheel(dx, dy);
  if (key !== undefined) await page.keyboard.up(key);
}

test('a small map does not go down, up nor left; the zoom stays anchored at the top (B-8.3-68, B-2.2-10, B-2.2-11)', async ({ page, api }) => {
  await layOut(page, api, 2, 3);
  const top = await firstLaneTop(page);
  const left = await firstStepLeft(page);
  const area = await boxOf(canvas(page));
  const empty = { x: area.x + 700, y: area.y + 450 };

  await drag(page, empty, 0, 200);
  expect(await firstLaneTop(page)).toBeCloseTo(top, 0);
  await drag(page, empty, 0, -250);
  expect(await firstLaneTop(page)).toBeCloseTo(top, 0);
  await drag(page, empty, 300, 0);
  expect(await firstStepLeft(page)).toBeCloseTo(left, 0);

  // The wheel alone scrolls: a small map does not go up, nor right of its first column (spec 3.0 section 15.1).
  await wheel(page, empty, 0, 300);
  await wheel(page, empty, 0, -300);
  await wheel(page, empty, 0, -300, 'Shift');
  expect(await firstLaneTop(page)).toBeCloseTo(top, 0);
  expect(await firstStepLeft(page)).toBeCloseTo(left, 0);

  // Ctrl and the wheel in the middle of the screen: the zoom changes, the top stays hooked.
  await wheel(page, { x: area.x + 600, y: area.y + 300 }, 0, 400, 'Control');
  await expect.poll(async () => (await boxOf(stepOf(page, 'VUE-E001'))).width).toBeLessThan(160);
  expect(await firstLaneTop(page)).toBeCloseTo(top, 0);
});

test('the wheel scrolls a large map down and up, Shift and the wheel sideways, Ctrl and the wheel zoom on the pointer, on the headers too (spec 3.0 section 15.1)', async ({ page, api }) => {
  await layOut(page, api, 8, 12);
  const top = await firstLaneTop(page);
  const left = await firstStepLeft(page);
  const area = await boxOf(canvas(page));
  const onLane = { x: area.x + 900, y: area.y + 300 };
  const width = (await boxOf(stepOf(page, 'VUE-E001'))).width;

  // Down by the pixels of the turn, as a web page; back up, hooked at the top again.
  await wheel(page, onLane, 0, 200);
  await expect.poll(() => firstLaneTop(page)).toBeCloseTo(top - 200, 0);
  await wheel(page, onLane, 0, -500);
  await expect.poll(() => firstLaneTop(page)).toBeCloseTo(top, 0);

  // Sideways with Shift; never left of the first column.
  await wheel(page, onLane, 0, 300, 'Shift');
  await expect.poll(() => firstStepLeft(page)).toBeCloseTo(left - 300, 0);
  await wheel(page, onLane, 0, -900, 'Shift');
  await expect.poll(() => firstStepLeft(page)).toBeCloseTo(left, 0);
  expect(await firstLaneTop(page)).toBeCloseTo(top, 0);

  // On the fixed headers the wheel scrolls the map as well.
  const head = await boxOf(heads(page).nth(1));
  await wheel(page, { x: head.x + head.width / 2, y: head.y + head.height / 2 }, 0, 150);
  await expect.poll(() => firstLaneTop(page)).toBeCloseTo(top - 150, 0);
  await wheel(page, { x: head.x + head.width / 2, y: head.y + head.height / 2 }, 0, -150);
  await expect.poll(() => firstLaneTop(page)).toBeCloseTo(top, 0);

  // Ctrl and the wheel zoom in; the step under the pointer stays under it.
  const step = await boxOf(stepOf(page, 'VUE-E003'));
  const pointer = { x: step.x + step.width / 2, y: step.y + step.height / 2 };
  await wheel(page, pointer, 0, -200, 'Control');
  await expect.poll(async () => (await boxOf(stepOf(page, 'VUE-E003'))).width).toBeGreaterThan(width * 1.2);
  const zoomed = await boxOf(stepOf(page, 'VUE-E003'));
  expect(pointer.y).toBeGreaterThan(zoomed.y);
  expect(pointer.y).toBeLessThan(zoomed.y + zoomed.height);
  expect(pointer.x).toBeGreaterThan(zoomed.x);
  expect(pointer.x).toBeLessThan(zoomed.x + zoomed.width);
});

test('a tall and wide map scrolls to its end, not past it, then back to the start (B-8.3-69, B-2.2-12)', async ({ page, api }) => {
  await layOut(page, api, 8, 12);
  // The Hand: a drag moves the view without a key.
  await page.getByRole('radio', { name: 'Main' }).click();
  const top = await firstLaneTop(page);
  const area = await boxOf(canvas(page));
  // No empty place: the drag starts on the background of a lane.
  const onLane = { x: area.x + 900, y: area.y + 300 };

  await drag(page, onLane, 0, -200);
  expect(await firstLaneTop(page)).toBeLessThan(top - 150);

  // At the bottom: the last lane stops at the bottom of the canvas, nothing empty under it.
  for (let index = 0; index < 4; index++) await drag(page, onLane, 0, -300);
  const last = await boxOf(heads(page).last());
  expect(last.y + last.height).toBeGreaterThan(area.y + area.height - 40);
  expect(last.y + last.height).toBeLessThanOrEqual(area.y + area.height);

  // Back at the top (hooked), then right to the end: the view stops after the last column.
  for (let index = 0; index < 4; index++) await drag(page, onLane, 0, 400);
  expect(await firstLaneTop(page)).toBeCloseTo(top, 0);
  for (let index = 0; index < 6; index++) await drag(page, onLane, -400, 0);
  const lastStep = await boxOf(stepOf(page, 'VUE-E012'));
  expect(lastStep.x + lastStep.width).toBeLessThan(area.x + area.width);
  // The end of the map, the rest of the last column and its free columns included, stays 40 px from the right edge (B-2.2-07).
  expect(area.x + area.width - (lastStep.x + lastStep.width)).toBeCloseTo(COLUMN_WIDTH - STEP_WIDTH + FREE_COLUMNS * COLUMN_WIDTH + RIGHT_MARGIN, 0);
  // Never under the fixed headers of 184 px (AR-19; 160 in the 1.x).
  expect(lastStep.x).toBeGreaterThan(area.x + LANE_HEADER_WIDTH);

  await page.getByRole('button', { name: 'Revenir au début de la carte' }).click();
  await expect.poll(() => firstLaneTop(page)).toBeCloseTo(top, 0);
  await expect(stepOf(page, 'VUE-E001')).toBeInViewport();
});
