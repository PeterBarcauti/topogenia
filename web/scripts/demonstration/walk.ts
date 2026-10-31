// walk.ts
// The tools of the walk of the demonstration: its folders, its captures of the screen, its files saved, and the gestures on the map aimed at a cell of a lane.

import { mkdirSync, rmSync } from 'node:fs';
import { join, resolve } from 'node:path';
import type { Download, Locator, Page } from '@playwright/test';
import { COLUMN_WIDTH, LANE_HEADER_WIDTH, LEFT_MARGIN, ROW_HEIGHT, STEP_WIDTH } from '../../src/features/map/model/geometry/constants.ts';
import { expect, stepOf } from '../../e2e/support/journey.ts';
import { boxOf, canvas, ribbon } from '../../e2e/support/map-editing.ts';

/** The folder of the demonstration in the recipe of the project holder (prompt 8). */
export const FOLDER = resolve(import.meta.dirname, '..', '..', '..', 'docs', 'recette', 'demonstration');
const CAPTURES = join(FOLDER, 'captures');
const EXPORTS = join(FOLDER, 'exports');

/** The fictitious project of the scenario: the expense reports of a company with no name. */
export const CODE = 'NDF';
export const code = (number: number): string => `${CODE}-E${String(number).padStart(3, '0')}`;
export const lane = (number: number): string => `${CODE}-C${String(number).padStart(2, '0')}`;

/** The captures and the files of an earlier walk are removed: each walk writes them all again. */
export function emptyFolders(): void {
  for (const folder of [CAPTURES, EXPORTS]) {
    rmSync(folder, { recursive: true, force: true });
    mkdirSync(folder, { recursive: true });
  }
}

/** The page as it stays once its dialogs, menus and notifications have faded in (an animation cancelled meanwhile is done too). */
async function settled(page: Page): Promise<void> {
  await page.evaluate(() => Promise.all(document.getAnimations()
    .filter((animation) => animation.effect?.getComputedTiming().iterations !== Infinity)
    .map((animation) => animation.finished.catch(() => undefined))));
}

/** A capture of the screen at 1366 x 768, as the user sees it. */
export async function shot(page: Page, name: string): Promise<void> {
  await settled(page);
  await page.screenshot({ path: join(CAPTURES, `${name}.png`) });
}

/** A capture of the screen once the notifications are gone (they last a few seconds), when they would hide the map. */
export async function shotWithoutToasts(page: Page, name: string): Promise<void> {
  await expect(page.getByTestId('toasts').getByRole('status')).toHaveCount(0, { timeout: 15_000 });
  await shot(page, name);
}

/** The pointer put away on the header, so that no step shows its « + » nor its hover. */
export async function rest(page: Page): Promise<void> {
  await page.mouse.move(700, 20);
}

/** The file the browser receives, kept in the folder of the exports under its own name. */
export async function keep(download: Promise<Download>): Promise<string> {
  const file = await download;
  const path = join(EXPORTS, file.suggestedFilename());
  await file.saveAs(path);
  return path;
}

/** The shift and the zoom of the view of the map, read from the transform of React Flow. */
async function viewOf(page: Page): Promise<{ x: number; y: number; zoom: number }> {
  return page.locator('.react-flow__viewport').evaluate((element) => {
    const matrix = new DOMMatrixReadOnly(getComputedStyle(element).transform);
    return { x: matrix.m41, y: matrix.m42, zoom: matrix.m11 };
  });
}

/** The point of the screen at the middle of a cell of a lane (its first row), wherever the view is. */
async function cellAt(page: Page, laneCode: string, column: number): Promise<{ x: number; y: number }> {
  const view = await viewOf(page);
  const area = await boxOf(canvas(page));
  const body = await boxOf(page.getByTestId(`lane-body-${laneCode}`));
  return {
    x: area.x + view.x + view.zoom * (LEFT_MARGIN + column * COLUMN_WIDTH + STEP_WIDTH / 2),
    y: body.y + (view.zoom * ROW_HEIGHT) / 2,
  };
}

/** A name typed in the field opened in place, then Enter. */
export async function typeName(page: Page, testId: string, text: string): Promise<void> {
  const field = page.getByTestId(testId);
  await expect(field).toBeFocused();
  await field.fill(text);
  await field.press('Enter');
  await expect(field).toHaveCount(0);
}

/** The type of the next steps chosen on the ribbon, then a double click on the cell: the step is made and named (B-2.3-09). */
export async function addStep(page: Page, laneCode: string, column: number, label: string, type = 'Étape'): Promise<void> {
  const radio = ribbon(page).getByRole('radio', { name: type, exact: true });
  if ((await radio.getAttribute('aria-checked')) !== 'true') await radio.click();
  // A cell off the screen is brought to its middle first, as the user moves the view.
  const area = await boxOf(canvas(page));
  let at = await cellAt(page, laneCode, column);
  if (at.x > area.x + area.width - 120 || at.x < area.x + LANE_HEADER_WIDTH + 100) {
    await pan(page, area.x + area.width / 2 - at.x);
    at = await cellAt(page, laneCode, column);
  }
  await page.mouse.dblclick(at.x, at.y);
  await typeName(page, 'step-label-input', label);
}

/** The view moved sideways by a drag of the middle button on the background (B-2.2-01). */
export async function pan(page: Page, dx: number): Promise<void> {
  const area = await boxOf(canvas(page));
  const from = { x: area.x + area.width / 2, y: area.y + area.height - 40 };
  await page.mouse.move(from.x, from.y);
  await page.mouse.down({ button: 'middle' });
  await page.mouse.move(from.x + dx, from.y, { steps: 12 });
  await page.mouse.up({ button: 'middle' });
}

/** A click on an empty place of the background, left of the zoom buttons: nothing chosen, the keyboard on the canvas. */
export async function clickBackground(page: Page): Promise<void> {
  const area = await boxOf(canvas(page));
  await page.mouse.click(area.x + 400, area.y + area.height - 30);
}

/** The view back to the start of the map, by the button of the zoom (B-2.2-09). */
export async function backToStart(page: Page): Promise<void> {
  await page.getByTestId('map-back-to-start').click();
}

/** The menu of a step opened by a right click. */
export async function stepMenu(page: Page, number: number): Promise<Locator> {
  await stepOf(page, code(number)).click({ button: 'right' });
  const menu = page.getByRole('menu', { name: 'Menu de l’étape' });
  await expect(menu).toBeVisible();
  return menu;
}

/** Links typed in the command bar (Ctrl+K), one per line; the capture taken before they are made, when asked. */
export async function linkByCommand(page: Page, lines: string[], capture?: string): Promise<void> {
  await clickBackground(page);
  await page.keyboard.press('Control+k');
  const field = page.getByRole('textbox', { name: 'Relier par les codes' });
  await field.fill(lines.join('\n'));
  if (capture !== undefined) await shot(page, capture);
  await field.press('Enter');
  await expect(page.getByText(`${String(lines.length)} flèche${lines.length > 1 ? 's' : ''} ajoutée${lines.length > 1 ? 's' : ''}`)).toBeVisible();
}
