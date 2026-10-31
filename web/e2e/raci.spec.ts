// raci.spec.ts
// Journeys of the RACI (annex B 8.3, spec 11): the layer off by default, a role given in two clicks, one A a step, the matrix copied for Excel or PowerPoint, Ctrl+Z, the quick add hidden by the layer.

import type { Locator, Page } from '@playwright/test';
import type { Api } from './support/api.ts';
import { expect, openMap, openSheetOf, stepOf, test } from './support/journey.ts';

/**
 * Lanes Controlling (the first one, renamed), Direction and Logistique; Début, Clôture mensuelle and Envoi du rapport on
 * Controlling (1.x `preparer`). Every journey has a new browser context: the layer starts off.
 */
async function layOut(page: Page, api: Api): Promise<void> {
  const { mapId } = await api.project('RAC', 'Ventes');
  const first = (await api.map(mapId)).lanes[0];
  await api.send('PATCH', `/api/lanes/${first?.id ?? ''}`, { name: 'Controlling' }, first?.version);
  await api.lane(mapId, 'Direction');
  await api.lane(mapId, 'Logistique');
  const lane = first?.id ?? '';
  await api.step(mapId, lane, 0, 'Début', 'start');
  await api.step(mapId, lane, 1, 'Clôture mensuelle');
  await api.step(mapId, lane, 2, 'Envoi du rapport');
  await openMap(page, mapId, 'RAC-E003');
}

const panelOf = (page: Page): Locator => page.getByTestId('raci-panel');

/** A letter of the panel, by its role and its lane (« Approuve : Direction »). */
const letterOf = (page: Page, role: string, lane: string): Locator => panelOf(page).getByRole('button', { name: `${role} : ${lane}`, exact: true });

/** The badges under a step, « A Direction », « I Logistique »... */
const badgesOf = (page: Page, code: string): Locator => stepOf(page, code).locator('.raci-badge');

/** Two clicks: the step, at its middle, opens its panel at (+12, -20) of the click (B-2.3-12); then the letter. */
async function openPanel(page: Page, code: string): Promise<void> {
  const step = await stepOf(page, code).boundingBox();
  if (step === null) throw new Error(`The step ${code} is not drawn.`);
  const at = { x: step.x + step.width / 2, y: step.y + step.height / 2 };
  await page.mouse.click(at.x, at.y);
  const panel = await panelOf(page).boundingBox();
  expect(panel === null ? null : [Math.round(panel.x), Math.round(panel.y)]).toEqual([Math.round(at.x + 12), Math.round(at.y - 20)]);
}

/** The matrix of the map: the start left out, the R given by the lane of the step, copied as an HTML table and as tabulated text. */
async function checkMatrix(page: Page): Promise<void> {
  await page.getByRole('toolbar').getByRole('button', { name: 'Matrice', exact: true }).click();
  const matrix = page.getByTestId('raci-matrix').locator('table.raci-matrix');
  await expect(matrix.locator('thead th')).toHaveText(['Code', 'Étape', 'Controlling', 'Direction', 'Logistique']);
  await expect(matrix.locator('tbody tr')).toHaveCount(2);
  await expect(matrix.locator('tbody tr').first().locator('td')).toHaveText(['RAC-E002', 'Clôture mensuelle', 'R', 'A', 'I']);
  await expect(matrix.locator('tbody tr').last().locator('td')).toHaveText(['RAC-E003', 'Envoi du rapport', 'R', '', '']);
  await page.getByRole('button', { name: 'Copier pour Excel ou PowerPoint' }).click();
  await expect(page.getByText('Matrice copiée : la coller dans Excel, Word ou PowerPoint.')).toBeVisible();
  const copied = await page.evaluate(async () => {
    const [item] = await navigator.clipboard.read();
    const html = item === undefined || !item.types.includes('text/html') ? '' : await (await item.getType('text/html')).text();
    return { text: await navigator.clipboard.readText(), html };
  });
  expect(copied.text.split(/\r?\n/)[1]).toBe('RAC-E002\tClôture mensuelle\tR\tA\tI');
  // A real table for Word and PowerPoint besides the text (B-2.11-15).
  expect(copied.html).toMatch(/^<table[^>]*>.*<td[^>]*>RAC-E002<\/td>.*<\/table>$/s);
  await page.keyboard.press('Escape');
  await expect(page.getByTestId('raci-matrix')).toHaveCount(0);
}

test('the RACI layer, a role in two clicks, one A a step, the matrix copied, Ctrl+Z (B-8.3-41 to B-8.3-45)', async ({ page, context, api }) => {
  await context.grantPermissions(['clipboard-read', 'clipboard-write']);
  await layOut(page, api);

  // Off by default: nothing shown, a click opens the sheet, not the panel (B-2.11-04, B-2.3-13).
  await expect(page.getByTestId('raci-badges')).toHaveCount(0);
  await openSheetOf(page, 'RAC-E002');
  await expect(page.getByTestId('sheet-panel')).toBeVisible();
  await expect(panelOf(page)).toHaveCount(0);
  await page.keyboard.press('Escape');
  await expect(page.getByTestId('sheet-panel')).toHaveCount(0);

  await page.getByRole('toolbar').getByRole('button', { name: 'RACI', exact: true }).click();
  // The missing approver is flagged, but not on the start (B-2.11-07, B-2.11-03).
  await expect(stepOf(page, 'RAC-E002').getByTestId('raci-missing')).toHaveText('A ?');
  await expect(stepOf(page, 'RAC-E001').getByTestId('raci-missing')).toHaveCount(0);

  // Two clicks: the step, then the letter (B-2.11-09 to B-2.11-11); the letters of the map are A, C and I.
  await openPanel(page, 'RAC-E002');
  await expect(page.getByTestId('sheet-panel')).toHaveCount(0);
  await expect(panelOf(page)).toContainText('Controlling');
  await expect(panelOf(page)).toContainText('réalise');
  await expect(panelOf(page).getByRole('button', { name: 'Soutient : Direction' })).toHaveCount(0);
  await letterOf(page, 'Approuve', 'Direction').click();
  await letterOf(page, 'Informé', 'Logistique').click();
  await expect(letterOf(page, 'Approuve', 'Direction')).toHaveAttribute('aria-pressed', 'true');
  await page.keyboard.press('Escape');
  await expect(panelOf(page)).toHaveCount(0);
  await expect(badgesOf(page, 'RAC-E002')).toHaveText(['A Direction', 'I Logistique']);
  await expect(stepOf(page, 'RAC-E002').getByTestId('raci-missing')).toHaveCount(0);

  // One A: given to Logistique, it is taken from Direction (B-2.11-12).
  await openPanel(page, 'RAC-E002');
  await letterOf(page, 'Approuve', 'Logistique').click();
  await expect(letterOf(page, 'Approuve', 'Direction')).toHaveAttribute('aria-pressed', 'false');
  await page.keyboard.press('Escape');
  await expect(badgesOf(page, 'RAC-E002')).toHaveText(['A Logistique']);

  // Ctrl+Z: the A goes back to Direction, Logistique is Informé again (B-2.11-13).
  await page.keyboard.press('Control+z');
  await expect(badgesOf(page, 'RAC-E002')).toHaveText(['A Direction', 'I Logistique']);

  await checkMatrix(page);

  // The choice of showing the layer is kept (B-2.11-04).
  await page.reload();
  await expect(badgesOf(page, 'RAC-E002')).toHaveText(['A Direction', 'I Logistique']);

  // A letter given is taken back by a click on it (B-2.11-11).
  await openPanel(page, 'RAC-E002');
  await letterOf(page, 'Informé', 'Logistique').click();
  await expect(letterOf(page, 'Informé', 'Logistique')).toHaveAttribute('aria-pressed', 'false');
  await page.keyboard.press('Escape');
  await expect(badgesOf(page, 'RAC-E002')).toHaveText(['A Direction']);
});

test('the RACI layer hides the « + » of the quick add (B-8.3-46)', async ({ page, api }) => {
  await layOut(page, api);
  // Without the layer, a step hovered offers its « + » (B-2.3-41).
  await stepOf(page, 'RAC-E002').hover();
  await expect(stepOf(page, 'RAC-E002').getByTestId('quick-add-after')).toBeVisible();
  await page.getByRole('toolbar').getByRole('button', { name: 'RACI', exact: true }).click();
  await stepOf(page, 'RAC-E002').hover();
  await expect(page.locator('.quick-add')).toHaveCount(0);
});
