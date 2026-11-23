// map-gestures-34.spec.ts
// Journeys of the gestures of the map of the 3.4 (lot 1, ADR 109): Show all by its button and by Shift+1, the map and a selection copied as an image and read back from the clipboard (framed on the steps chosen, R8), the keyboard shortcuts by ? and by the Help menu, a presentation from the step of its menu, and the gestures of a reader.

import { readFileSync } from 'node:fs';
import type { Page } from '@playwright/test';
import type { Api, ProjectRead } from './support/api.ts';
import { expect, openMap, stepOf, test } from './support/journey.ts';
import { boxOf, laneOf } from './support/map-editing.ts';
import { fileEntry } from './support/file-menu.ts';

const COPIED = 'Image copiée : la coller (Ctrl+V) dans un courriel ou une diapositive.';

interface ImageRead {
  type: string;
  width: number;
  height: number;
}

/** `count` steps in a row on the first lane, each leading to the next; the codes are `<code>-E001`... */
async function layOut(api: Api, code: string, count: number): Promise<{ id: string; mapId: string }> {
  const { id, mapId } = await api.project(code, 'Gestes', 'fr', 'confidential');
  const lane = await laneOf(api, mapId);
  const ids: string[] = [];
  for (let column = 0; column < count; column++) ids.push(await api.step(mapId, lane, column, `Étape ${String(column + 1)}`));
  for (let index = 0; index + 1 < count; index++) await api.flow(mapId, ids[index] ?? '', ids[index + 1] ?? '');
  return { id, mapId };
}

const codeOf = (prefix: string, rank: number): string => `${prefix}-E${String(rank).padStart(3, '0')}`;

/** Every step of the map is whole inside the canvas. */
async function expectAllShown(page: Page, prefix: string, count: number): Promise<void> {
  const area = await boxOf(page.getByTestId('map-canvas'));
  for (let rank = 1; rank <= count; rank++) {
    const box = await boxOf(stepOf(page, codeOf(prefix, rank)));
    expect(box.x, `step ${String(rank)} left`).toBeGreaterThanOrEqual(area.x);
    expect(box.x + box.width, `step ${String(rank)} right`).toBeLessThanOrEqual(area.x + area.width);
    expect(box.y, `step ${String(rank)} top`).toBeGreaterThanOrEqual(area.y);
    expect(box.y + box.height, `step ${String(rank)} bottom`).toBeLessThanOrEqual(area.y + area.height);
  }
}

/** The size of the PNG downloaded by « Exporter en image PNG ». */
async function exportedPng(page: Page): Promise<{ width: number; height: number }> {
  const download = page.waitForEvent('download');
  await page.getByTestId('header-file').click();
  await (await fileEntry(page, 'file.png')).click();
  const content = readFileSync(await (await download).path());
  await expect(page.getByText('Export terminé : le fichier est dans le dossier Téléchargements.')).toBeVisible();
  return { width: content.readUInt32BE(16), height: content.readUInt32BE(20) };
}

/** The image now on the clipboard, read back as a page that pastes it would. */
async function clipboardImage(page: Page): Promise<ImageRead> {
  return page.evaluate(async () => {
    const [item] = await navigator.clipboard.read();
    const type = item?.types.find((name) => name.startsWith('image/')) ?? '';
    if (item === undefined || type === '') return { type, width: 0, height: 0 };
    const bitmap = await createImageBitmap(await item.getType(type));
    return { type, width: bitmap.width, height: bitmap.height };
  });
}

/**
 * Copies by the gesture given, waits for its notice, then gives the image copied. The notices stack and stay 3.5 s: the
 * one of an earlier copy is first let go, and the clipboard emptied, so that the notice and the image read are this copy's.
 */
async function copiedImage(page: Page, copy: () => Promise<void>): Promise<ImageRead> {
  await expect(page.getByText(COPIED)).toHaveCount(0);
  await page.evaluate(() => navigator.clipboard.writeText('vide'));
  await copy();
  await expect(page.getByText(COPIED)).toBeVisible();
  return clipboardImage(page);
}

/** A box of the image, in shares of its width and its height. */
interface Share {
  left: number;
  top: number;
  right: number;
  bottom: number;
}

/** The dark pixels (the texts of a step) of each box of the image now on the clipboard. */
async function darkPixels(page: Page, boxes: Share[]): Promise<number[]> {
  return page.evaluate(async (all) => {
    const [item] = await navigator.clipboard.read();
    if (item === undefined) return [];
    const bitmap = await createImageBitmap(await item.getType('image/png'));
    const canvas = document.createElement('canvas');
    [canvas.width, canvas.height] = [bitmap.width, bitmap.height];
    const context = canvas.getContext('2d');
    if (context === null) return [];
    context.drawImage(bitmap, 0, 0);
    return all.map((share) => {
      const [x, y] = [Math.round(share.left * bitmap.width), Math.round(share.top * bitmap.height)];
      const { data } = context.getImageData(x, y, Math.round(share.right * bitmap.width) - x, Math.round(share.bottom * bitmap.height) - y);
      let dark = 0;
      for (let index = 0; index < data.length; index += 4) if (0.299 * (data[index] ?? 255) + 0.587 * (data[index + 1] ?? 255) + 0.114 * (data[index + 2] ?? 255) < 110) dark++;
      return dark;
    });
  }, boxes);
}

/**
 * Two steps chosen and those two only, the menu of the selection opened on the second. The selection is first emptied by
 * a click on the background: a click on a step already in a selection of several keeps that selection (the group stays
 * chosen to be dragged), so after a copy of E001 and E002 a click on E001 would still hold E002.
 */
async function selectionMenu(page: Page, first: string, second: string): Promise<void> {
  await page.locator('.react-flow__pane').click({ position: { x: 700, y: 600 } });
  await expect(page.locator('.react-flow__node-step.selected')).toHaveCount(0);
  await stepOf(page, first).click();
  await stepOf(page, second).click({ modifiers: ['Shift'] });
  await expect(page.locator('.react-flow__node-step.selected')).toHaveCount(2);
  await expect(stepOf(page, first)).toHaveClass(/is-selected/);
  await expect(stepOf(page, second)).toHaveClass(/is-selected/);
  await stepOf(page, second).click({ button: 'right' });
  await expect(page.getByRole('menu', { name: 'Menu de la sélection' })).toBeVisible();
}

/** The panel of the keyboard shortcuts. */
const panelOf = (page: Page) => page.getByTestId('shortcuts');

/** The panel is shown, and holds what the 3.4 adds: Show all, the panel itself and the presentation from a step. */
async function expectSheet(page: Page): Promise<void> {
  await expect(panelOf(page)).toBeVisible();
  await expect(panelOf(page).getByRole('heading', { name: 'Raccourcis clavier' })).toBeVisible();
  await expect(page.getByTestId('shortcut-fit').locator('kbd')).toHaveText('Maj+1');
  await expect(page.getByTestId('shortcut-help.keys').locator('kbd')).toHaveText('?');
  await expect(page.getByTestId('shortcut-present.from').locator('kbd')).toHaveText('Maj+F5');
}

/** The role of a reader given to the page for the project, as in the properties (ADR 067): the bench is an administrator. */
async function readAs(page: Page, api: Api, id: string): Promise<void> {
  await page.route(`**/api/projects/${id}`, async (route) => {
    await route.fulfill({ json: { ...(await api.get<ProjectRead>(`/api/projects/${id}`)), role: 'reader' } });
  });
}

test('Show all and Shift+1 frame the whole map, the opening view unchanged', async ({ page, api }) => {
  const count = 12;
  const { mapId } = await layOut(api, 'TVO', count);
  await openMap(page, mapId, codeOf('TVO', 1));
  // Wider than the screen: the last step is off it, and the map opens at its start.
  await expect(stepOf(page, codeOf('TVO', count))).not.toBeInViewport();
  const opening = await boxOf(stepOf(page, codeOf('TVO', 1)));

  // Once the view is framed, every step is whole inside the canvas, measured by its box: the ratio of the browser for a
  // step drawn at a zoom such as 0.34 is a float a hair under 1 (0.9999996) though the step is 280 px from the edge.
  await page.getByTestId('map-fit').click();
  await expect(stepOf(page, codeOf('TVO', count))).toBeInViewport();
  await expectAllShown(page, 'TVO', count);
  // A smaller view of the same map: the first step is drawn smaller, never larger than at 100 %.
  const framed = await boxOf(stepOf(page, codeOf('TVO', 1)));
  expect(framed.width).toBeLessThan(opening.width);

  // Reopened, the map is at its start as before; Shift+1 then frames it as the button did.
  await openMap(page, mapId, codeOf('TVO', 1));
  const again = await boxOf(stepOf(page, codeOf('TVO', 1)));
  expect([Math.round(again.x), Math.round(again.y), Math.round(again.width)]).toEqual([Math.round(opening.x), Math.round(opening.y), Math.round(opening.width)]);
  await expect(stepOf(page, codeOf('TVO', count))).not.toBeInViewport();
  await page.locator('.react-flow__pane').click({ position: { x: 700, y: 600 } });
  await page.keyboard.press('Shift+1');
  await expect(stepOf(page, codeOf('TVO', count))).toBeInViewport();
  await expectAllShown(page, 'TVO', count);
});

test('the map copied as an image, read back from the clipboard, has the size of the PNG export', async ({ page, context, api }) => {
  await context.grantPermissions(['clipboard-read', 'clipboard-write']);
  const { mapId } = await layOut(api, 'TCI', 6);
  await openMap(page, mapId, codeOf('TCI', 1));

  const png = await exportedPng(page);
  await page.getByTestId('header-file').click();
  const copied = await copiedImage(page, () => page.getByTestId('menu-file-file.copy-image').click());
  expect(copied.type).toBe('image/png');
  expect([copied.width, copied.height]).toEqual([png.width, png.height]);

  // The menu of the background copies the same map; a selection gives a smaller image.
  await page.locator('.react-flow__pane').click({ button: 'right', position: { x: 700, y: 600 } });
  const again = await copiedImage(page, () => page.getByTestId('menu-pane-pane.copy-image').click());
  expect([again.width, again.height]).toEqual([png.width, png.height]);

  await selectionMenu(page, codeOf('TCI', 1), codeOf('TCI', 2));
  const part = await copiedImage(page, () => page.getByTestId('menu-selection-selection.copy-image').click());
  expect(part.type).toBe('image/png');
  expect(part.width).toBeGreaterThan(0);
  expect(part.width).toBeLessThan(png.width);

  // The first and the third steps chosen (R8): the image is framed on them, lower than the lane, and the second step,
  // between them, is not in it. The three steps share the width in 24 + 170 + 50 + 170 + 50 + 170 + 24: the texts of the
  // first one start at 6 % of it, those of the second one at 40 %, in the height of the steps (from 15 % to 56 %).
  await selectionMenu(page, codeOf('TCI', 1), codeOf('TCI', 3));
  const apart = await copiedImage(page, () => page.getByTestId('menu-selection-selection.copy-image').click());
  expect(apart.width).toBeGreaterThan(part.width);
  expect(apart.height).toBeLessThan(png.height);
  const [first, second] = await darkPixels(page, [{ left: 0.06, top: 0.2, right: 0.29, bottom: 0.5 }, { left: 0.4, top: 0.2, right: 0.6, bottom: 0.5 }]);
  expect(first).toBeGreaterThan(0);
  expect(second).toBe(0);
});

test('? and the Help menu open the shortcuts', async ({ page, api }) => {
  const { mapId } = await layOut(api, 'TKY', 3);
  await openMap(page, mapId, codeOf('TKY', 1));
  await page.locator('.react-flow__pane').click({ position: { x: 700, y: 600 } });

  // ? with no step chosen opens the panel, Échap closes it.
  await page.keyboard.press('?');
  await expectSheet(page);
  await page.keyboard.press('Escape');
  await expect(panelOf(page)).toHaveCount(0);

  // The Help menu of the header holds the online help and the shortcuts.
  await page.getByTestId('header-help').click();
  await expect(page.getByRole('menu', { name: 'Menu Aide' })).toBeVisible();
  await expect(page.getByTestId('help-menu-online')).toContainText('Aide en ligne');
  await page.getByTestId('help-menu-shortcuts').click();
  await expectSheet(page);
  await page.keyboard.press('Escape');
  await expect(panelOf(page)).toHaveCount(0);

  // The menu is on every page: the home page opens the same panel.
  await page.getByTestId('header-brand').click();
  await page.getByTestId('header-help').click();
  await page.getByTestId('help-menu-shortcuts').click();
  await expect(panelOf(page)).toBeVisible();
  await expect(page.getByTestId('shortcut-fit').locator('kbd')).toHaveText('Maj+1');
});

test('Present from this step, from the menu of a step', async ({ page, api }) => {
  const { mapId } = await layOut(api, 'TPF', 5);
  await openMap(page, mapId, codeOf('TPF', 1));
  // Another step is chosen first: the presentation starts at the step of the menu, not at that one.
  await stepOf(page, codeOf('TPF', 1)).click();
  await stepOf(page, codeOf('TPF', 4)).click({ button: 'right' });
  await expect(page.getByTestId('menu-step-present.from').locator('kbd')).toHaveText('Maj+F5');
  await page.getByTestId('menu-step-present.from').click();
  await expect(page.getByTestId('presenter')).toBeVisible();
  // The presentation opens on its overview, as from the ribbon: its tour holds steps 4 and 5, then the first shown is 4.
  await expect(page.getByTestId('presenter-caption')).toHaveText('Le parcours compte 2 étapes.');
  await expect(page.locator('.presenter-flow .react-flow__node.is-presented')).toHaveCount(0);
  await page.keyboard.press('ArrowRight');
  await expect(page.locator('.presenter-flow .react-flow__node.is-presented')).toContainText('Étape 4');
  await page.keyboard.press('Escape');
  await expect(page.getByTestId('presenter')).toHaveCount(0);
  await expect(page.getByTestId('map-canvas')).toBeVisible();
});

test('a reader does the four gestures', async ({ page, context, api }) => {
  await context.grantPermissions(['clipboard-read', 'clipboard-write']);
  const count = 12;
  const { id, mapId } = await layOut(api, 'TRD', count);
  await readAs(page, api, id);
  await openMap(page, mapId, codeOf('TRD', 1));

  // Show all, by the button and by Shift+1.
  await page.getByTestId('map-fit').click();
  await expectAllShown(page, 'TRD', count);
  await openMap(page, mapId, codeOf('TRD', 1));
  await page.locator('.react-flow__pane').click({ position: { x: 700, y: 600 } });
  await page.keyboard.press('Shift+1');
  await expectAllShown(page, 'TRD', count);

  // The map copied as an image, then a selection.
  await page.getByTestId('header-file').click();
  await expect(page.getByTestId('menu-file-file.copy-image')).not.toHaveAttribute('aria-disabled', 'true');
  const whole = await copiedImage(page, () => page.getByTestId('menu-file-file.copy-image').click());
  expect(whole.type).toBe('image/png');
  await selectionMenu(page, codeOf('TRD', 1), codeOf('TRD', 2));
  await expect(page.getByTestId('menu-selection-selection.copy-image')).not.toHaveAttribute('aria-disabled', 'true');
  const part = await copiedImage(page, () => page.getByTestId('menu-selection-selection.copy-image').click());
  expect(part.type).toBe('image/png');
  expect(part.width).toBeGreaterThan(0);
  expect(part.width).toBeLessThan(whole.width);

  // The shortcuts, and the presentation from a step.
  await page.locator('.react-flow__pane').click({ position: { x: 700, y: 600 } });
  await page.keyboard.press('?');
  await expectSheet(page);
  await page.keyboard.press('Escape');
  await stepOf(page, codeOf('TRD', 1)).click({ button: 'right' });
  await page.getByTestId('menu-step-present.from').click();
  await expect(page.getByTestId('presenter')).toBeVisible();
  await page.keyboard.press('Escape');
  await expect(page.getByTestId('presenter')).toHaveCount(0);
});
