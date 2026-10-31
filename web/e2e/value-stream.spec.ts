// value-stream.spec.ts
// Journeys of the value stream layer (spec 14.5, 14.6, 14.12, ADR 085): the map of the expense claims of the mockup and its exact figures on the canvas, the layer alone under the steps, the check leading to its object, the entries by the menus, the map drawn as before once the layer is off.

import type { Page } from '@playwright/test';
import type { Api } from './support/api.ts';
import { expect, openMap, stepOf, test } from './support/journey.ts';
import { middleOfFlow } from './support/map-editing.ts';

/** The captures of the journeys of the layer, kept out of the repository for the eye of the owner of the product. */
const CAPTURES = process.env.E2E_CAPTURES ?? '';

/** The fields of a sheet: a duration, a frequency for each case, a lead time; validated unless told otherwise. */
async function sheet(api: Api, stepId: string, minutes: number | null, lead: [number, string] | null, validated = true): Promise<void> {
  const read = await api.get<{ version: number | null }>(`/api/steps/${stepId}/sheet`);
  const fields = {
    frequency: { value: 'per_case' }, ...(minutes === null ? {} : { duration_minutes: { number: minutes } }),
    ...(lead === null ? {} : { lead_time: { number: lead[0], value: lead[1] } }),
  };
  const saved = await api.send<{ version: number }>('PUT', `/api/steps/${stepId}/sheet`, { fields }, read.version ?? 0);
  if (validated) await api.send('POST', `/api/steps/${stepId}/sheet/validate`, undefined, saved.version);
}

/**
 * The expense claims of the mockup: 120 claims a month enter; the employee enters them (20 min, 2 days), the manager
 * validates them (15 min, 3 days), accounting checks them (10 min, 1 day, a draft) and sends 15 % back; the software
 * prepares the transfer at once, the bank transfer waits 2 days. Working day of 7 hours, 151.67 hours a full-time equivalent.
 */
async function claims(api: Api): Promise<string> {
  const { mapId } = await api.project('NDF', 'Notes de frais');
  const employee = (await api.map(mapId)).lanes[0];
  await api.send('PATCH', `/api/lanes/${employee?.id ?? ''}`, { name: 'Salarié' }, employee?.version);
  const manager = await api.lane(mapId, 'Manager', 'person');
  const accounting = await api.lane(mapId, 'Comptabilité');
  const software = await api.lane(mapId, 'Logiciel de frais', 'system');
  const lane = employee?.id ?? '';
  const start = await api.step(mapId, lane, 0, 'Note de frais à rembourser', 'start');
  const enter = await api.step(mapId, lane, 1, 'Saisir la note de frais');
  const validate = await api.step(mapId, manager, 2, 'Valider la note');
  const check = await api.step(mapId, accounting, 3, 'Note conforme ?', 'decision');
  const transfer = await api.step(mapId, software, 4, 'Préparer le virement');
  const bank = await api.step(mapId, software, 5, 'Virement bancaire', 'wait');
  const end = await api.step(mapId, lane, 6, 'Note remboursée', 'end');
  for (const [source, target] of [[start, enter], [enter, validate], [validate, check], [transfer, bank], [bank, end]] as const) await api.flow(mapId, source, target);
  await api.flow(mapId, check, transfer, 'oui');
  const back = await api.flow(mapId, check, enter, 'non');
  const map = await api.map(mapId);
  const version = (list: readonly { id: string; version: number }[], id: string) => list.find((item) => item.id === id)?.version;
  await api.send('PATCH', `/api/steps/${start}`, { volume: { quantity: 120, period: 'month' } }, version(map.steps, start));
  await api.send('PATCH', `/api/steps/${transfer}`, { automation_status: 'automated' }, version(map.steps, transfer));
  await api.send('PUT', `/api/flows/${back}/share`, { share: 15 }, version(map.flows, back));
  await api.send('PATCH', `/api/lanes/${manager}`, { headcount: 40 }, version(map.lanes, manager));
  await api.send('PATCH', `/api/lanes/${accounting}`, { headcount: 3 }, (await api.map(mapId)).lanes.find((item) => item.id === accounting)?.version);
  await sheet(api, enter, 20, [2, 'day']);
  await sheet(api, validate, 15, [3, 'day']);
  await sheet(api, check, 10, [1, 'day'], false);
  await sheet(api, transfer, 0, [0, 'minute']);
  await sheet(api, bank, null, [2, 'day']);
  return mapId;
}

const layerButton = (page: Page) => page.getByRole('toolbar').getByRole('button', { name: 'Flux de valeur', exact: true });

/** The four cases of the cartouche of a step, as written: « 141 / mois », « 20 min », « 2 j », « 0,31 ETP ». */
const casesOf = (page: Page, code: string) => page.getByTestId(`stream-card-${code}`).locator('.stream-case');

test('the layer writes the figures of the mockup on the canvas and the map is drawn as before once it is off (spec 14.12)', async ({ page, api }) => {
  const mapId = await claims(api);
  await openMap(page, mapId, 'NDF-E007');
  const before = await stepOf(page, 'NDF-E004').boundingBox();
  await expect(layerButton(page)).toHaveAttribute('aria-pressed', 'false');
  await expect(page.locator('.stream-card')).toHaveCount(0);

  await layerButton(page).click();
  await expect(layerButton(page)).toHaveAttribute('aria-pressed', 'true');
  await expect(casesOf(page, 'NDF-E002')).toHaveText(['141/mois', '20min', '2j', '0,31ETP']);
  await expect(casesOf(page, 'NDF-E003')).toHaveText(['141/mois', '15min', '3j', '0,23ETP']);
  await expect(casesOf(page, 'NDF-E004')).toHaveText(['141/mois', '10min', '1j', '0,16ETP']);
  await expect(casesOf(page, 'NDF-E004').nth(2)).toHaveClass(/is-draft/);
  await expect(casesOf(page, 'NDF-E005')).toHaveText(['120/mois', '0min', '0min', '–']);
  await expect(casesOf(page, 'NDF-E006')).toHaveText(['120/mois', '–', '2j', '–']);
  await expect(page.getByTestId('stream-entry-NDF-E001')).toHaveText('Entrée120/mois');
  await expect(page.getByTestId('stream-exit-NDF-E007')).toHaveText('Sortie120/mois');
  // « non · 15 % » entered, « oui · 85 % » the remainder of the decision, in italics.
  await expect(page.getByTestId('edge-share')).toHaveCount(2);
  const shares = (await page.getByTestId('edge-share').evaluateAll((items) => items.map((item) => `${item.textContent}|${item.getAttribute('class') ?? ''}`)))
    .map((text) => text.replace(/\s/g, ' ')).sort();
  expect(shares).toEqual(['15 %|edge-share', '85 %|edge-share is-remainder']);
  await expect(page.getByTestId('lane-stream-NDF-C02')).toHaveText('40 personnes0,23 ETP');
  await expect(page.getByTestId('lane-stream-NDF-C01')).toHaveText('Effectif à saisir0,31 ETP');
  await expect(page.getByTestId('lane-stream-NDF-C04')).toHaveCount(0);
  // The panel on the right, the map beside it (spec 3.0 section 15.3): 8 working days on the main path, 9.1 on average with
  // the rework, 0.70 FTE; nothing drawn under the lanes.
  const panel = page.getByTestId('stream-panel');
  await expect(panel).toBeVisible();
  await expect(page.getByTestId('stream-path-totals')).toContainText('Délai8 j');
  await expect(page.getByTestId('stream-tile-expected_lead')).toContainText('9,1 j');
  await expect(page.getByTestId('stream-tile-charge')).toContainText('0,70 ETP');
  await expect(page.getByTestId('stream-quantified')).toHaveText('5/5 étapes chiffrées');
  const [canvasBox, panelBox] = [await page.getByTestId('map-canvas').boundingBox(), await panel.boundingBox()];
  expect((canvasBox?.x ?? 0) + (canvasBox?.width ?? 0)).toBeLessThanOrEqual((panelBox?.x ?? 0) + 1);
  await expect(page.getByTestId('stream-block')).toHaveCount(0);
  if (CAPTURES !== '') await page.screenshot({ path: `${CAPTURES}/flux-de-valeur.png` });

  await layerButton(page).click();
  await expect(page.locator('.stream-card')).toHaveCount(0);
  await expect(page.getByTestId('stream-panel')).toHaveCount(0);
  expect(await stepOf(page, 'NDF-E004').boundingBox()).toEqual(before);
});

test('each mention of the check leads to its object, chosen and put in view (spec 14.4, 14.5)', async ({ page, api }) => {
  const mapId = await claims(api);
  await openMap(page, mapId, 'NDF-E007');
  await layerButton(page).click();
  // Grouped by code, a short label, the object of each mention in a pill (spec 3.0 section 15.3).
  await expect(page.getByTestId('stream-check-VALEUR_BROUILLON')).toContainText('Fiche non validée');
  await expect(page.getByTestId('stream-check-EFFECTIF_MANQUANT')).toContainText('Effectif manquant');
  await expect(page.getByTestId('stream-chip-VALEUR_BROUILLON-NDF-E004')).toHaveAttribute('title', 'NDF-E004 Note conforme ?');
  await page.getByTestId('stream-chip-VALEUR_BROUILLON-NDF-E004').click();
  await expect(stepOf(page, 'NDF-E004')).toHaveClass(/is-selected/);
  await page.getByTestId('stream-chip-EFFECTIF_MANQUANT-NDF-C01').click();
  for (const code of ['NDF-E001', 'NDF-E002', 'NDF-E007']) await expect(stepOf(page, code)).toHaveClass(/is-selected/);
  await expect(stepOf(page, 'NDF-E004')).not.toHaveClass(/is-selected/);
  // A row of the main path leads to its step too.
  await page.getByTestId('stream-step-NDF-E003').click();
  await expect(stepOf(page, 'NDF-E003')).toHaveClass(/is-selected/);
  if (CAPTURES !== '') {
    // The whole map beside its panel, the view zoomed out.
    for (let click = 0; click < 3; click++) await page.getByRole('button', { name: 'Zoom arrière' }).click();
    await page.screenshot({ path: `${CAPTURES}/flux-de-valeur-ensemble.png` });
  }
});

test('the panel folds to its strip and opens again; its width follows the handle and is kept after a reload (spec 3.0 section 15.2)', async ({ page, api }) => {
  const mapId = await claims(api);
  await openMap(page, mapId, 'NDF-E007');
  await layerButton(page).click();
  const frame = page.getByTestId('side-dock');
  const width = (await frame.boundingBox())?.width ?? 0;
  expect(width).toBeCloseTo(440, 0);

  const canvasBefore = (await page.getByTestId('map-canvas').boundingBox())?.width ?? 0;
  await page.getByTestId('panel-fold').click();
  await expect(page.getByTestId('side-dock-strip')).toBeVisible();
  await expect(page.getByTestId('side-dock-strip')).toContainText('Flux de valeur');
  await expect(page.getByTestId('stream-panel')).toBeHidden();
  // The canvas takes the room the panel left; the layer stays on.
  await expect.poll(async () => (await page.getByTestId('map-canvas').boundingBox())?.width ?? 0).toBeGreaterThan(canvasBefore + 300);
  await expect(page.locator('.stream-card').first()).toBeVisible();
  await page.getByTestId('side-dock-strip').click();
  await expect(page.getByTestId('stream-panel')).toBeVisible();

  // The handle of the left edge, dragged 200 px to the left: the panel 640 px wide, the same once the page is read again.
  const handle = await page.getByTestId('side-dock-handle').boundingBox();
  const at = { x: (handle?.x ?? 0) + (handle?.width ?? 0) / 2, y: (handle?.y ?? 0) + 200 };
  await page.mouse.move(at.x, at.y);
  await page.mouse.down();
  await page.mouse.move(at.x - 200, at.y, { steps: 8 });
  await page.mouse.up();
  await expect.poll(async () => (await frame.boundingBox())?.width ?? 0).toBeCloseTo(width + 200, -1);
  const chosen = (await frame.boundingBox())?.width ?? 0;
  await page.reload();
  await expect(page.getByTestId('stream-panel')).toBeVisible();
  expect((await page.getByTestId('side-dock').boundingBox())?.width ?? 0).toBeCloseTo(chosen, 0);
});

test('one layer at a time under the steps: the value stream turns the RACI off and the RACI turns it off (spec 14.5)', async ({ page, api }) => {
  const mapId = await claims(api);
  await openMap(page, mapId, 'NDF-E007');
  const raci = page.getByRole('toolbar').getByRole('button', { name: 'RACI', exact: true });
  await raci.click();
  await expect(raci).toHaveAttribute('aria-pressed', 'true');
  await layerButton(page).click();
  await expect(layerButton(page)).toHaveAttribute('aria-pressed', 'true');
  await expect(raci).toHaveAttribute('aria-pressed', 'false');
  await expect(page.locator('.raci-badges')).toHaveCount(0);
  await raci.click();
  await expect(layerButton(page)).toHaveAttribute('aria-pressed', 'false');
  await expect(page.locator('.stream-card')).toHaveCount(0);
});

test('the entries by the menus: the volume of the start, the share of an exit with the remainder shown, the headcount of a lane; one Ctrl+Z each (spec 14.6)', async ({ page, api }) => {
  const mapId = await claims(api);
  await openMap(page, mapId, 'NDF-E007');
  await layerButton(page).click();

  await stepOf(page, 'NDF-E001').click({ button: 'right' });
  await page.getByRole('menuitem', { name: 'Volume entrant…' }).click();
  const volume = page.getByTestId('stream-entry-volume');
  await expect(page.getByTestId('stream-entry-input')).toHaveValue('120');
  await page.getByTestId('stream-entry-input').fill('6');
  await page.getByTestId('stream-entry-period').selectOption('day');
  await page.getByTestId('stream-entry-input').press('Enter');
  await expect(volume).toHaveCount(0);
  // 6 a working day: 151.67 / 7 working days a month, 130 a month.
  await expect(page.getByTestId('stream-entry-NDF-E001')).toHaveText('Entrée130/mois');

  const back = (await api.map(mapId)).flows.find((flow) => flow.label === 'non')?.id ?? '';
  const middle = await middleOfFlow(page, back);
  await page.mouse.click(middle.x, middle.y, { button: 'right' });
  await page.getByRole('menuitem', { name: 'Part des dossiers…' }).click();
  await page.getByTestId('stream-entry-input').fill('20');
  await expect(page.getByTestId('stream-entry-remainder')).toHaveText('Reste pour « oui » : 80 %');
  await page.getByTestId('stream-entry-input').press('Enter');
  await expect(casesOf(page, 'NDF-E002').first()).toHaveText('163/mois');

  await page.getByTestId('lane-head-NDF-C01').click({ button: 'right' });
  await page.getByRole('menuitem', { name: 'Effectif…' }).click();
  await page.getByTestId('stream-entry-input').fill('25');
  await page.getByTestId('stream-entry-input').press('Enter');
  await expect(page.getByTestId('lane-stream-NDF-C01')).toHaveText(/^25 personnes0,3\d ETP$/);

  // One Ctrl+Z a write: the headcount goes, then the share.
  await page.keyboard.press('Control+z');
  await expect(page.getByTestId('lane-stream-NDF-C01')).toHaveText(/^Effectif à saisir/);
  await page.keyboard.press('Control+z');
  await expect(casesOf(page, 'NDF-E002').first()).toHaveText('153/mois');
});
