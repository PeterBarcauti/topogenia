// dissolution.spec.ts
// Journeys of the detail brought back into the flow (annex B 8.3, spec 11): the frame keeps the remainder of the detailed step until it is assigned, « Grouper » and « Dissocier » of the ribbon and their keys (AR-154), « Terminer » and « Fermer le cadre », Ctrl+Z undoes it all in order.

import type { Locator, Page } from '@playwright/test';
import type { Api } from './support/api.ts';
import { expect, openMap, stepOf, test } from './support/journey.ts';

/** The reply of a command on a map: the object made (the sub-map of a detailed step). */
interface Made {
  readonly id: string | null;
}

/** The steps of a map read again, by their identifier. */
async function stepVersion(api: Api, mapId: string, stepId: string): Promise<number | undefined> {
  return (await api.map(mapId)).steps.find((step) => step.id === stepId)?.version;
}

/**
 * Extraction -> Saisie -> Envoi; Saisie detailed (Début -> Préparer -> Vérifier -> Fin); with a remainder, Saisie produces
 * the workbook Envoi uses with a pain point (1.x `preparer`); without, Saisie carries nothing.
 */
async function layOut(page: Page, api: Api, remainder = true): Promise<string> {
  const { mapId } = await api.project('REM', 'Remontee');
  const lane = (await api.map(mapId)).lanes[0]?.id ?? '';
  const extraction = await api.step(mapId, lane, 0, 'Extraction');
  const entry = await api.step(mapId, lane, 1, 'Saisie');
  const sending = await api.step(mapId, lane, 2, 'Envoi');
  await api.flow(mapId, extraction, entry);
  await api.flow(mapId, entry, sending);
  const subMap = (await api.send<Made>('POST', `/api/steps/${entry}/detail`, undefined, await stepVersion(api, mapId, entry))).id ?? '';
  const subLane = (await api.map(subMap)).lanes[0]?.id ?? '';
  const inner = [
    await api.step(subMap, subLane, 0, 'Début', 'start'), await api.step(subMap, subLane, 1, 'Préparer'),
    await api.step(subMap, subLane, 2, 'Vérifier'), await api.step(subMap, subLane, 3, 'Fin', 'end'),
  ];
  for (let index = 0; index + 1 < inner.length; index++) await api.flow(subMap, inner[index] ?? '', inner[index + 1] ?? '');
  if (remainder) {
    const sheet = await api.send<{ outputs: { document: { id: string } }[] }>('POST', `/api/steps/${entry}/documents`, {
      direction: 'output', name: 'Classeur des ventes', location: null, wait_hours: null, share_confirmed: null,
    });
    const workbook = sheet.outputs[0]?.document.id ?? '';
    // A first use goes with the version 0 (ADR 064 point 3).
    await api.send('PUT', `/api/steps/${sending}/uses/${workbook}`, {
      wait_hours: null, percent_complete_accurate: 50, pain_point: 'Colonnes en double', lost_hours_per_month: null,
    }, 0);
  }
  await openMap(page, mapId, 'REM-E002');
  return mapId;
}

const frameOf = (page: Page): Locator => page.getByTestId('dissolution-frame');

/** The panel of the remainder, docked on the right like the sheet (ADR 063 point 5). */
const remainderOf = (page: Page): Locator => page.getByRole('complementary', { name: new RegExp('^Reliquat de REM-E002') });

/** Right click on the detailed step: « Dissocier le sous-processus » (B-2.4-16, AR-154). */
async function dissolve(page: Page): Promise<void> {
  await stepOf(page, 'REM-E002').click({ button: 'right' });
  await page.getByRole('menuitem', { name: 'Dissocier le sous-processus' }).click();
  await expect(stepOf(page, 'REM-E002')).toHaveCount(0);
}

/** A click on the label of the frame opens the panel of the remainder (B-2.9-21, B-2.9-24). */
async function openRemainder(page: Page): Promise<void> {
  await frameOf(page).getByTestId('dissolution-label').click();
  await expect(remainderOf(page)).toBeVisible();
}

/** Ctrl+Z from the canvas, the focus taken by a click on its background. */
async function undo(page: Page): Promise<void> {
  await page.locator('.react-flow__pane').click({ position: { x: 600, y: 450 } });
  await page.keyboard.press('Control+z');
}

test('a detail brought back, its remainder assigned from the frame, then all undone by Ctrl+Z (B-8.3-51)', async ({ page, api }) => {
  await layOut(page, api);
  await dissolve(page);
  await expect(stepOf(page, 'REM-E005')).toBeVisible();
  await expect(stepOf(page, 'REM-E006')).toBeVisible();
  await expect(frameOf(page)).toBeVisible();
  // The label counts the remainder: the workbook (used by Envoi) and the pain point (B-2.9-21).
  const label = frameOf(page).getByTestId('dissolution-label');
  await expect(label).toHaveText('Reliquat de REM-E002 · 2');

  // Once assigned, the frame goes (B-2.9-29 to B-2.9-31, B-2.9-40).
  await openRemainder(page);
  const panel = remainderOf(page);
  await expect(panel).toContainText('Classeur des ventes');
  await expect(panel).toContainText('utilisé par REM-E003');
  await panel.getByRole('combobox', { name: 'Affecter « Classeur des ventes »' }).selectOption({ label: 'REM-E006 · Vérifier' });
  await expect(panel).not.toContainText('Classeur des ventes');
  await expect(label).toHaveText('Reliquat de REM-E002 · 1');
  await panel.getByRole('combobox', { name: 'Affecter « Colonnes en double »' }).selectOption({ label: 'La carte (sans étape)' });
  await expect(frameOf(page)).toHaveCount(0);
  await expect(panel).toHaveCount(0);
  // The document produced by Vérifier and used by Envoi: the flow carries it.
  await expect(page.getByRole('img', { name: '1 document : Classeur des ventes' })).toBeVisible();

  // Ctrl+Z: the pain point comes back (the frame too), then the document, then the whole dissolution (B-2.9-41).
  await undo(page);
  await expect(frameOf(page)).toBeVisible();
  await expect(label).toHaveText('Reliquat de REM-E002 · 1');
  await undo(page);
  await expect(label).toHaveText('Reliquat de REM-E002 · 2');
  await undo(page);
  await expect(stepOf(page, 'REM-E002')).toBeVisible();
  await expect(frameOf(page)).toHaveCount(0);
  await expect(stepOf(page, 'REM-E005')).toHaveCount(0);
  await expect(stepOf(page, 'REM-E003')).toBeVisible();
});

test('« Détailler » of the ribbon on one step, « Grouper » on several, « Dissocier » on the detailed step; « Terminer » stores the rest (B-8.3-52, AR-154)', async ({ page, api }) => {
  const mapId = await layOut(page, api);
  const button = (name: string) => page.getByRole('toolbar').getByRole('button', { name, exact: true });
  await expect(button('Détailler')).toBeDisabled();
  await expect(button('Grouper')).toBeDisabled();
  // The detailed step is not detailed again, it may be ungrouped; Extraction alone is detailed, with Envoi grouped (B-2.1-28, B-2.1-29).
  await stepOf(page, 'REM-E002').click();
  await expect(button('Détailler')).toBeDisabled();
  await expect(button('Dissocier')).toBeEnabled();
  await page.keyboard.press('Escape');
  await stepOf(page, 'REM-E001').click();
  await expect(button('Détailler')).toBeEnabled();
  await expect(button('Grouper')).toBeDisabled();
  await stepOf(page, 'REM-E003').click({ modifiers: ['Shift'] });
  await expect(button('Détailler')).toBeDisabled();
  await expect(button('Grouper')).toHaveAttribute('title', 'Grouper (Ctrl+G)');
  await button('Grouper').click();
  // The new step « Sous-processus » goes to renaming (B-2.9-10, ADR 063 point 9).
  await expect(page.getByTestId('step-label-input')).toBeFocused();
  // The block went into the detailed map of the new step (B-2.9-09).
  await expect(stepOf(page, 'REM-E001')).toHaveCount(0);
  await expect(stepOf(page, 'REM-E003')).toHaveCount(0);
  const group = (await api.map(mapId)).steps.find((step) => step.label === 'Sous-processus');
  expect((await api.map(group?.sub_map_id ?? '')).steps.map((step) => step.label).sort()).toEqual(['Envoi', 'Extraction']);
  await page.keyboard.press('Escape');
  await expect(page.getByTestId('step-label-input')).toHaveCount(0);

  // Brought back by « Dissocier » then finished: the workbook goes to the last step of the frame, the pain point to the map (B-2.9-33 to B-2.9-35).
  await undo(page);
  await expect(stepOf(page, 'REM-E001')).toBeVisible();
  await stepOf(page, 'REM-E002').click();
  await expect(button('Dissocier')).toHaveAttribute('title', 'Dissocier (Ctrl+Maj+G)');
  await button('Dissocier').click();
  await expect(stepOf(page, 'REM-E002')).toHaveCount(0);
  await openRemainder(page);
  await remainderOf(page).getByRole('button', { name: 'Terminer' }).click();
  const stored = page.getByRole('dialog', { name: 'Reliquat de REM-E002 rangé' });
  await expect(stored).toContainText('Classeur des ventes → REM-E006');
  await expect(stored).toContainText('Colonnes en double → la carte (sans étape)');
  await stored.getByTestId('arrangements-done').click();
  await expect(stored).toHaveCount(0);
  await expect(frameOf(page)).toHaveCount(0);
});

test('Ctrl+G groups the steps chosen, Ctrl+Shift+G ungroups the detailed step chosen, as in Office (AR-154)', async ({ page, api }) => {
  const mapId = await layOut(page, api, false);
  await stepOf(page, 'REM-E001').click();
  await stepOf(page, 'REM-E003').click({ modifiers: ['Shift'] });
  await page.keyboard.press('Control+g');
  await expect(page.getByTestId('step-label-input')).toBeFocused();
  await page.keyboard.press('Escape');
  await expect(stepOf(page, 'REM-E001')).toHaveCount(0);
  const group = (await api.map(mapId)).steps.find((step) => step.label === 'Sous-processus');
  expect((await api.map(group?.sub_map_id ?? '')).steps.map((step) => step.label).sort()).toEqual(['Envoi', 'Extraction']);
  // The background, then the new step, takes the focus of the canvas: Ctrl+Shift+G brings its detail back, in a frame.
  await page.locator('.react-flow__pane').click({ position: { x: 600, y: 450 } });
  await stepOf(page, group?.code ?? '').click();
  await page.keyboard.press('Control+Shift+G');
  await expect(stepOf(page, group?.code ?? '')).toHaveCount(0);
  await expect(stepOf(page, 'REM-E001')).toBeVisible();
  await expect(stepOf(page, 'REM-E003')).toBeVisible();
  await expect(frameOf(page)).toBeVisible();
});

test('without remainder, the frame shows the steps brought back until « Fermer le cadre »; Ctrl+Z undoes the dissolution (B-8.3-53)', async ({ page, api }) => {
  await layOut(page, api, false);
  await dissolve(page);
  await expect(frameOf(page).getByRole('button', { name: 'Remonté de REM-E002 · rien à ventiler' })).toBeVisible();
  await openRemainder(page);
  const panel = remainderOf(page);
  await expect(panel).toContainText('Rien à ventiler.');
  // An empty remainder: « Terminer » becomes « Fermer le cadre » (B-2.9-36); nothing stored, no dialog.
  await panel.getByRole('button', { name: 'Fermer le cadre' }).click();
  await expect(frameOf(page)).toHaveCount(0);
  await expect(panel).toHaveCount(0);
  await expect(page.getByRole('dialog')).toHaveCount(0);
  await undo(page);
  await expect(stepOf(page, 'REM-E002')).toBeVisible();
});
