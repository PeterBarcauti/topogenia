// map-editing.ts
// The helpers of the journeys of the edition of the map: the ribbon and its zones, the boxes on the screen, the cells aimed at the opening view, the menus of a step, the undo from the background.

import type { Locator, Page } from '@playwright/test';
import { COLUMN_WIDTH, INITIAL_VIEW, LEFT_MARGIN, ROW_HEIGHT, STEP_WIDTH } from '../../src/features/map/model/geometry/constants.ts';
import type { Api, MapRead, StepRead } from './api.ts';
import { expect, stepOf } from './journey.ts';

export interface Box {
  readonly x: number;
  readonly y: number;
  readonly width: number;
  readonly height: number;
}

export interface Point {
  readonly x: number;
  readonly y: number;
}

/** The box of an element on the screen; an element not drawn fails the journey with its name. */
export async function boxOf(locator: Locator): Promise<Box> {
  const box = await locator.boundingBox();
  if (box === null) throw new Error(`Not drawn: ${locator.toString()}`);
  return box;
}

export const centreOf = (box: Box): Point => ({ x: box.x + box.width / 2, y: box.y + box.height / 2 });

/** The vertical middle of a step on the screen. */
export async function middleY(page: Page, code: string): Promise<number> {
  return centreOf(await boxOf(stepOf(page, code))).y;
}

export const ribbon = (page: Page): Locator => page.getByRole('toolbar', { name: 'Ruban de la carte' });
export const zone = (page: Page, name: string): Locator => ribbon(page).getByRole('group', { name, exact: true });
export const canvas = (page: Page): Locator => page.getByTestId('map-canvas');
export const laneHead = (page: Page, code: string): Locator => page.getByTestId(`lane-head-${code}`);
export const firstLaneHead = (page: Page): Locator => page.getByTestId('lane-headers').locator('.lane-head').first();
export const edges = (page: Page): Locator => page.locator('.react-flow__edge');
export const toasts = (page: Page): Locator => page.getByTestId('toasts');

/** The radio button of a shape or a diamond of the ribbon, for the type of the next steps. */
export const shape = (page: Page, name: string): Locator => ribbon(page).getByRole('radiogroup', { name: /prochaine étape/ }).getByRole('radio', { name, exact: true });

/** A step of the map read, by its code. */
export function stepIn(map: MapRead, code: string): StepRead {
  const found = map.steps.find((step) => step.code === code);
  if (found === undefined) throw new Error(`No step ${code} in ${map.steps.map((step) => step.code).join(', ')}`);
  return found;
}

/** The identifier of a lane of a map by its rank (0: the first lane, « Acteur 1 »). */
export async function laneOf(api: Api, mapId: string, rank = 0): Promise<string> {
  const lanes = [...(await api.map(mapId)).lanes].sort((a, b) => a.sort_order - b.sort_order);
  const lane = lanes[rank];
  if (lane === undefined) throw new Error(`No lane of rank ${String(rank)}`);
  return lane.id;
}

/**
 * The point of the screen at the centre of a cell of a lane, in its first row, with the view of the opening: the column 0
 * right of the headers at zoom 1 (B-3.1-09, AR-19); the top of the lane read from its header.
 */
export async function cellCentre(page: Page, head: Locator, column: number): Promise<Point> {
  const area = await boxOf(canvas(page));
  const top = (await boxOf(head)).y;
  return { x: area.x + INITIAL_VIEW.x + LEFT_MARGIN + COLUMN_WIDTH * column + STEP_WIDTH / 2, y: top + ROW_HEIGHT / 2 };
}

/** A double click on the background of a cell: a step of the type of the next steps is created there (B-2.3-09). */
export async function doubleClickCell(page: Page, head: Locator, column: number): Promise<void> {
  const point = await cellCentre(page, head, column);
  await page.mouse.dblclick(point.x, point.y);
}

/** The field where the name of a step is typed. */
export const labelInput = (page: Page): Locator => page.getByTestId('step-label-input');

/** Opens the menu of a step by a right click. */
export async function openStepMenu(page: Page, code: string): Promise<Locator> {
  await stepOf(page, code).click({ button: 'right' });
  const menu = page.getByRole('menu', { name: 'Menu de l’étape' });
  await expect(menu).toBeVisible();
  return menu;
}

/** « Changer le type : X » from the menu of a step (B-2.4-25). */
export async function changeType(page: Page, code: string, type: string): Promise<void> {
  const menu = await openStepMenu(page, code);
  await menu.getByRole('menuitemradio', { name: `Changer le type : ${type}` }).click();
}

/** A click on an empty place of the background, then Ctrl+Z: the gesture of the 1.x before undoing. */
export async function undoFromBackground(page: Page, at: Point): Promise<void> {
  await page.mouse.click(at.x, at.y);
  await page.keyboard.press('Control+z');
}

/** The point of the screen at the middle of the path of a flow (a path of SVG is no button to click). */
export async function middleOfFlow(page: Page, flowId: string): Promise<Point> {
  return page.getByTestId(`rf__edge-${flowId}`).locator('path.react-flow__edge-path').evaluate((path) => {
    const line = path as SVGPathElement;
    const point = line.getPointAtLength(line.getTotalLength() / 2);
    const matrix = line.getScreenCTM();
    if (matrix === null) throw new Error('The flow is not drawn');
    return { x: point.x * matrix.a + matrix.e, y: point.y * matrix.d + matrix.f };
  });
}

/** The point of the screen where the path of a flow starts. */
export async function startOfFlow(page: Page, flowId: string): Promise<Point> {
  return page.getByTestId(`rf__edge-${flowId}`).locator('path.react-flow__edge-path').evaluate((path) => {
    const line = path as SVGPathElement;
    const point = line.getPointAtLength(0);
    const matrix = line.getScreenCTM();
    if (matrix === null) throw new Error('The flow is not drawn');
    return { x: point.x * matrix.a + matrix.e, y: point.y * matrix.d + matrix.f };
  });
}
