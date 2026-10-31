// alignment.spec.ts
// Journey of the Align wand (annex B 8.3): the map aligned in one undoable action, straight flows between aligned steps, one point of the diamond per exit.

import type { Page } from '@playwright/test';
import type { Api } from './support/api.ts';
import { expect, openMap, stepOf, test } from './support/journey.ts';
import { boxOf, canvas, laneOf, middleY, startOfFlow, stepIn, toasts, undoFromBackground, zone } from './support/map-editing.ts';

/**
 * Extraction (column 0) -> Decision (column 2, an empty column before) -oui-> Saisie (column 3, row 1: moved down by hand)
 * -> Envoi (column 6); Decision -non-> Envoi.
 */
async function layOut(page: Page, api: Api): Promise<{ mapId: string; yes: string; no: string }> {
  const { mapId } = await api.project('ALI', 'Alignement');
  const lane = await laneOf(api, mapId);
  const extraction = await api.step(mapId, lane, 0, 'Extraction');
  const decision = await api.step(mapId, lane, 2, 'Nouveau payeur ?', 'decision');
  const entry = await api.step(mapId, lane, 3, 'Saisie', 'task', 1);
  const sending = await api.step(mapId, lane, 6, 'Envoi');
  await api.flow(mapId, extraction, decision);
  const yes = await api.flow(mapId, decision, entry, 'oui');
  const no = await api.flow(mapId, decision, sending, 'non');
  await api.flow(mapId, entry, sending);
  await openMap(page, mapId, 'ALI-E003');
  return { mapId, yes, no };
}

/** The gap between the middles of Saisie and Extraction: more than 50 px while Saisie is a row below. */
const gap = async (page: Page) => Math.abs((await middleY(page, 'ALI-E003')) - (await middleY(page, 'ALI-E001')));

/** Every flow without a corner (no Q in its path) is straight: all its points on one ordinate. */
async function expectStraightFlows(page: Page): Promise<void> {
  const paths = await page.locator('.react-flow__edge path.react-flow__edge-path').evaluateAll((all) => all.map((path) => path.getAttribute('d') ?? ''));
  expect(paths).toHaveLength(4);
  for (const path of paths) {
    if (path.includes('Q')) continue;
    const ordinates = new Set([...path.matchAll(/[ML]\s*(-?[\d.]+)[ ,]\s*(-?[\d.]+)/g)].map((match) => Math.round(Number(match[2]))));
    expect(ordinates.size, path).toBe(1);
  }
}

test('the wand aligns the map in one undoable action; a diamond has one point per exit (B-8.3-01, B-8.3-02)', async ({ page, api }) => {
  const { mapId, yes, no } = await layOut(page, api);
  const wand = zone(page, 'Édition').getByRole('button', { name: 'Aligner' });
  await expect(wand).toBeEnabled();
  expect(await gap(page)).toBeGreaterThan(50);

  await wand.click();
  await expect(toasts(page).getByText('Carte alignée : 3 étapes replacées.')).toBeVisible();
  const aligned = await api.map(mapId);
  expect(['ALI-E002', 'ALI-E003', 'ALI-E004'].map((code) => [stepIn(aligned, code).column, stepIn(aligned, code).row])).toEqual([[1, 0], [2, 0], [3, 0]]);
  // All on one line, straight flows: no vertical segment outside the detour.
  await expect.poll(() => gap(page)).toBeLessThan(1);
  await expectStraightFlows(page);
  // « non » goes round by the top point of the diamond, « oui » leaves by its right point: one point an exit.
  const diamond = await boxOf(stepOf(page, 'ALI-E002'));
  const noStart = await startOfFlow(page, no);
  expect(Math.abs(noStart.x - (diamond.x + diamond.width / 2))).toBeLessThan(3);
  expect(noStart.y).toBeLessThan(diamond.y + 3);
  const yesStart = await startOfFlow(page, yes);
  expect(Math.abs(yesStart.x - (diamond.x + diamond.width))).toBeLessThan(3);
  expect(Math.abs(yesStart.y - (diamond.y + diamond.height / 2))).toBeLessThan(3);

  // Already aligned: nothing moves.
  await wand.click();
  await expect(toasts(page).getByText('La carte est déjà alignée.')).toBeVisible();

  // One undo puts everything back.
  const area = await boxOf(canvas(page));
  await undoFromBackground(page, { x: area.x + 300, y: area.y + 450 });
  await expect.poll(() => gap(page)).toBeGreaterThan(50);
  expect(stepIn(await api.map(mapId), 'ALI-E004').column).toBe(6);
});
