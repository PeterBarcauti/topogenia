// screens.spec.ts
// NF02 (S-NF-02, S-NF-03): every screen holds in 1366 x 768 without a horizontal scroll, the ribbon on one line and the sheet whole; a tablet reads the maps and their sheets by touch.

import type { Locator, Page } from '@playwright/test';
import type { Api } from './support/api.ts';
import { expect, openMap, openSheetOf, stepOf, test } from './support/journey.ts';
import { boxOf, canvas, laneOf, ribbon } from './support/map-editing.ts';

/** Four lanes of five steps with their sheets, linked in a row: a map wider than the screen. */
async function layOut(api: Api): Promise<{ projectId: string; mapId: string }> {
  const { id, mapId } = await api.project('ECR', 'Écrans');
  const lanes = [await laneOf(api, mapId), await api.lane(mapId, 'Achats'), await api.lane(mapId, 'Comptabilité'), await api.lane(mapId, 'Direction')];
  let before = '';
  for (let column = 0; column < 20; column++) {
    const step = await api.step(mapId, lanes[column % 4] ?? '', column, `Étape ${String(column + 1)}`);
    if (before !== '') await api.flow(mapId, before, step);
    before = step;
  }
  return { projectId: id, mapId };
}

/** The page does not scroll sideways: its content is as wide as the window at most. */
async function expectNoSideScroll(page: Page, what: string): Promise<void> {
  const [content, width] = await page.evaluate(() => [document.documentElement.scrollWidth, window.innerWidth]);
  expect(content, what).toBeLessThanOrEqual(width);
}

/** An element wholly inside the window. */
async function expectInWindow(page: Page, locator: Locator, what: string): Promise<void> {
  const box = await boxOf(locator);
  const size = page.viewportSize() ?? { width: 0, height: 0 };
  expect([box.x >= 0, box.y >= 0, box.x + box.width <= size.width + 0.5, box.y + box.height <= size.height + 0.5], what).toEqual([true, true, true, true]);
}

test('in 1366 x 768: the home page, the screens of a project, the map with its ribbon on one line and its sheet whole (S-NF-02)', async ({ page, api }) => {
  const { projectId, mapId } = await layOut(api);
  expect(page.viewportSize()).toEqual({ width: 1366, height: 768 });
  for (const [path, what] of [['/', 'home'], ['/projects/new', 'new project'], [`/projects/${projectId}/members`, 'members']] as const) {
    await page.goto(path);
    await expect(page.getByTestId('app-header')).toBeVisible();
    await expectNoSideScroll(page, what);
  }
  await openMap(page, mapId, 'ECR-E001');
  expect((await boxOf(page.getByTestId('app-header'))).height).toBe(48);
  const tops = await ribbon(page).getByRole('group').getByRole('button').evaluateAll((all) => [...new Set(all.map((button) => Math.round(button.getBoundingClientRect().top)))]);
  expect(tops).toHaveLength(1);
  await expectInWindow(page, ribbon(page), 'ribbon');
  await openSheetOf(page, 'ECR-E002');
  const sheet = page.getByTestId('sheet-panel');
  await expectInWindow(page, sheet, 'sheet');
  await expectInWindow(page, sheet.getByRole('button', { name: 'Je valide ma fiche' }), 'foot of the sheet');
  await expectNoSideScroll(page, 'map and sheet');
  await page.getByTestId('header-file').click();
  await expectInWindow(page, page.getByRole('menu', { name: 'Menu Fichier' }), 'File menu');
});

test.describe('a tablet in reading (S-NF-03)', () => {
  test.use({ viewport: { width: 1024, height: 768 }, hasTouch: true, isMobile: true, deviceScaleFactor: 2 });

  test('the map opens whole on the screen of a tablet, moves under the finger, zooms by its button, and a tap opens the sheet of a step', async ({ page, api }) => {
    const { mapId } = await layOut(api);
    await openMap(page, mapId, 'ECR-E001');
    await expectNoSideScroll(page, 'map on a tablet');
    await expectInWindow(page, canvas(page), 'canvas');
    const viewX = () => page.locator('.react-flow__viewport').evaluate((element) => new DOMMatrixReadOnly(getComputedStyle(element).transform).m41);
    const before = await viewX();
    const area = await boxOf(canvas(page));
    const touch = await page.context().newCDPSession(page);
    const at = (x: number) => [{ x: area.x + x, y: area.y + area.height - 60 }];
    await touch.send('Input.dispatchTouchEvent', { type: 'touchStart', touchPoints: at(700) });
    for (let step = 1; step <= 10; step++) await touch.send('Input.dispatchTouchEvent', { type: 'touchMove', touchPoints: at(700 - step * 30) });
    await touch.send('Input.dispatchTouchEvent', { type: 'touchEnd', touchPoints: [] });
    await expect.poll(viewX).toBeLessThan(before - 100);

    const scale = () => page.locator('.react-flow__viewport').evaluate((element) => new DOMMatrixReadOnly(getComputedStyle(element).transform).a);
    await page.getByRole('button', { name: 'Zoom avant' }).tap();
    await expect.poll(scale).toBeGreaterThan(1);

    await openMap(page, mapId, 'ECR-E001');
    await stepOf(page, 'ECR-E002').tap();
    const sheet = page.getByTestId('sheet-panel');
    await expectInWindow(page, sheet, 'sheet on a tablet');
    await expect(sheet.getByRole('textbox', { name: 'Finalité', exact: true })).toBeVisible();
  });
});
