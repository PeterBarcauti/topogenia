// branches.spec.ts
// Journeys of the branches of a lane and of the ribbon (annex B 2.1, 2.3, 8.3): named zones, a parallel branch under a step, the three diamonds, the quality of a document received, the Selection tool.

import type { Page } from '@playwright/test';
import type { Api } from './support/api.ts';
import { expect, openMap, openSheetOf, stepOf, test } from './support/journey.ts';
import { boxOf, canvas, centreOf, doubleClickCell, edges, labelInput, laneHead, ribbon, shape, stepIn, undoFromBackground, zone } from './support/map-editing.ts';

/** The zones of the ribbon in their order: Niveau first, Outils last (AR-155). */
const ZONES = ['Niveau', 'Formes', 'Branchements', 'Liens', 'Édition', 'Affichage', 'Outils'];

/** Lane Controlling: Extraction (E001) -> Tableau (E002) -> Envoi (E003); lane Logistique empty. */
async function layOut(page: Page, api: Api): Promise<{ mapId: string; ids: string[] }> {
  const { mapId } = await api.project('BRA', 'Branches');
  const first = (await api.map(mapId)).lanes[0];
  if (first === undefined) throw new Error('A new map has its first lane');
  await api.send('PATCH', `/api/lanes/${first.id}`, { name: 'Controlling' }, first.version);
  await api.lane(mapId, 'Logistique');
  const ids: string[] = [];
  for (const [column, label] of ['Extraction', 'Tableau', 'Envoi'].entries()) ids.push(await api.step(mapId, first.id, column, label));
  await api.flow(mapId, ids[0] ?? '', ids[1] ?? '');
  await api.flow(mapId, ids[1] ?? '', ids[2] ?? '');
  await openMap(page, mapId, 'BRA-E003');
  return { mapId, ids };
}

/** The six zones in their order, each titled under its buttons (B-2.1-08). */
async function expectNamedZones(page: Page): Promise<void> {
  const zones = ribbon(page).getByRole('group');
  await expect(zones).toHaveCount(ZONES.length);
  expect(await zones.evaluateAll((all) => all.map((group) => group.getAttribute('aria-label')))).toEqual(ZONES);
  for (const name of ZONES) {
    await expect(zone(page, name)).toBeVisible();
    await expect(zone(page, name).locator('.toolbar-caption')).toHaveText(name);
    const buttons = await boxOf(zone(page, name).locator('.toolbar-buttons'));
    expect((await boxOf(zone(page, name).locator('.toolbar-caption'))).y).toBeGreaterThanOrEqual(buttons.y + buttons.height - 1);
  }
}

test('a ribbon of named zones; a parallel branch under the step chosen, its lane grown, the same flows (B-8.3-03, B-8.3-04, B-2.1-08, B-2.1-18)', async ({ page, api }) => {
  const { mapId, ids } = await layOut(page, api);
  await expectNamedZones(page);
  // The diamonds are icons named for assistive technologies (ADR 061 section 6; the 1.x wrote a cross, a plus, a circle before their names).
  expect(await zone(page, 'Branchements').getByRole('radio').evaluateAll((all) => all.map((radio) => radio.getAttribute('aria-label')))).toEqual(['Décision', 'Parallèle', 'Inclusif']);
  const branch = zone(page, 'Branchements').getByRole('button', { name: 'Ajouter une branche en parallèle (même acteur)' });
  await expect(branch).toBeDisabled();

  const head = laneHead(page, 'BRA-C01');
  const before = (await boxOf(head)).height;
  const logisticsTop = (await boxOf(laneHead(page, 'BRA-C02'))).y;
  await stepOf(page, 'BRA-E002').click();
  await page.keyboard.press('Escape');
  await stepOf(page, 'BRA-E002').click();
  await expect(branch).toBeEnabled();
  await branch.click();
  const added = stepOf(page, 'BRA-E004');
  await expect(added).toBeVisible();
  // Same column, under it; the lane twice as high, Logistique goes down.
  const upper = await boxOf(stepOf(page, 'BRA-E002'));
  const lower = await boxOf(added);
  expect(Math.abs(lower.x - upper.x)).toBeLessThan(2);
  expect(lower.y).toBeGreaterThan(upper.y + upper.height);
  await expect.poll(async () => (await boxOf(head)).height).toBeGreaterThan(before * 1.8);
  expect((await boxOf(laneHead(page, 'BRA-C02'))).y).toBeGreaterThan(logisticsTop + 100);
  // Same flows: two more (Extraction -> branch, branch -> Envoi).
  await expect(edges(page)).toHaveCount(4);
  const map = await api.map(mapId);
  const branchId = stepIn(map, 'BRA-E004').id;
  expect(map.flows.filter((flow) => flow.source_step_id === branchId || flow.target_step_id === branchId).map((flow) => [flow.source_step_id, flow.target_step_id]))
    .toEqual(expect.arrayContaining([[ids[0], branchId], [branchId, ids[2]]]));

  // Undoable.
  const area = await boxOf(canvas(page));
  await undoFromBackground(page, { x: area.x + 300, y: area.y + 450 });
  await expect(added).toHaveCount(0);
  await expect.poll(async () => (await boxOf(head)).height).toBeLessThan(before * 1.2);
});

test('the diamonds AND and inclusive OR laid from the shapes, with their marker, without a sheet (B-8.3-05, B-2.3-09)', async ({ page, api }) => {
  const { mapId } = await layOut(page, api);
  const lay = async (name: string, column: number) => {
    await shape(page, name).click();
    await doubleClickCell(page, laneHead(page, 'BRA-C02'), column);
    await expect(labelInput(page)).toBeFocused();
    await labelInput(page).press('Enter');
  };
  await lay('Parallèle', 1);
  await lay('Inclusif', 2);

  // The marker of the diamond: + for AND, a circle for the inclusive OR (Lucide icons in place of the characters of the 1.x).
  for (const [code, type, icon] of [['BRA-E004', 'parallel', 'lucide-plus'], ['BRA-E005', 'inclusive', 'lucide-circle']] as const) {
    await expect(stepOf(page, code)).toHaveClass(new RegExp(`step--${type}`));
    await expect(stepOf(page, code)).toHaveClass(/step--gateway/);
    await expect(stepOf(page, code).locator('.gateway-marker svg')).toHaveClass(new RegExp(`\\b${icon}\\b`));
  }
  const map = await api.map(mapId);
  const logistics = map.lanes.find((lane) => lane.code === 'BRA-C02')?.id;
  expect(['BRA-E004', 'BRA-E005'].map((code) => [stepIn(map, code).type, stepIn(map, code).lane_id, stepIn(map, code).column]))
    .toEqual([['parallel', logistics, 1], ['inclusive', logistics, 2]]);
  // No sheet for a diamond that joins.
  await stepOf(page, 'BRA-E004').click();
  await expect(stepOf(page, 'BRA-E004')).toHaveClass(/is-selected/);
  await expect(page.getByTestId('sheet-panel')).toHaveCount(0);
});

test('quality: %C&A and pain point on the document received, seen by the step that produces it (B-8.3-06)', async ({ page, api }) => {
  const { ids } = await layOut(page, api);
  await api.send('POST', `/api/steps/${ids[0] ?? ''}/documents`, { direction: 'output', name: 'Extraction SAP', location: null, wait_hours: null, share_confirmed: false });
  await page.reload();
  await openSheetOf(page, 'BRA-E002');
  const inputs = page.getByRole('complementary', { name: 'Fiche de l’étape BRA-E002' }).getByTestId('sheet-inputs');
  await inputs.getByRole('checkbox', { name: 'Utiliser « Extraction SAP »' }).click();
  const percent = inputs.getByRole('spinbutton', { name: 'Part de « Extraction SAP » utilisable telle quelle, en %' });
  await percent.fill('0');
  const painPoint = inputs.getByRole('textbox', { name: 'Irritant sur « Extraction SAP »' });
  await expect(painPoint).toBeVisible();
  await percent.press('Tab');
  await painPoint.fill('Requête non mise à jour : nouveaux types de ventes non traités');
  await painPoint.press('Tab');
  await expect(inputs.getByText('Irritant BRA-I001')).toBeVisible();

  // On the side of the producer.
  await openSheetOf(page, 'BRA-E001');
  const producer = page.getByRole('complementary', { name: 'Fiche de l’étape BRA-E001' });
  await expect(producer.getByText(/utilisé par BRA-E002 \(0 % utilisable tel quel\)/)).toBeVisible();
  await expect(producer.getByText(/BRA-I001 · signalé : Requête non mise à jour/)).toBeVisible();
});

test('the Selection tool: a rectangle drawn on the background takes a block, one drag moves it; undoable (B-8.3-07, B-2.1-10, B-2.3-01, B-2.3-02, B-2.3-05, B-2.3-08)', async ({ page, api }) => {
  const { mapId } = await layOut(page, api);
  const tools = zone(page, 'Outils');
  // The map opens with the Hand; the rectangle asks for the Selection tool.
  await expect(tools.getByRole('radio', { name: 'Main' })).toHaveAttribute('aria-checked', 'true');
  await tools.getByRole('radio', { name: 'Sélection' }).click();
  await expect(tools.getByRole('radio', { name: 'Sélection' })).toHaveAttribute('aria-checked', 'true');

  // A rectangle round Tableau and Envoi, from the background of the lane.
  const table = await boxOf(stepOf(page, 'BRA-E002'));
  const sending = await boxOf(stepOf(page, 'BRA-E003'));
  await page.mouse.move(table.x - 15, table.y - 15);
  await page.mouse.down();
  await page.mouse.move(sending.x + sending.width + 15, sending.y + sending.height + 15, { steps: 10 });
  await page.mouse.up();
  await expect(page.locator('.react-flow__node-step.selected')).toHaveCount(2);

  // The block slides at once into the lane Logistique, by one write of the layout.
  const writes: string[] = [];
  page.on('request', (request) => {
    if (request.method() !== 'GET' && request.url().includes('/api/')) writes.push(`${request.method()} ${new URL(request.url()).pathname}`);
  });
  const centre = centreOf(table);
  await page.mouse.move(centre.x, centre.y);
  await page.mouse.down();
  await page.mouse.move(centre.x, centre.y + 120, { steps: 12 });
  await page.mouse.up();
  await expect.poll(async () => (await boxOf(stepOf(page, 'BRA-E002'))).y).toBeGreaterThan(table.y + 100);
  expect((await boxOf(stepOf(page, 'BRA-E003'))).y).toBeGreaterThan(sending.y + 100);
  const logistics = (await api.map(mapId)).lanes.find((lane) => lane.code === 'BRA-C02')?.id;
  await expect.poll(async () => {
    const moved = await api.map(mapId);
    return ['BRA-E002', 'BRA-E003'].map((code) => stepIn(moved, code).lane_id);
  }).toEqual([logistics, logistics]);
  expect(writes.filter((write) => write === `PUT /api/maps/${mapId}/layout`)).toHaveLength(1);

  await page.keyboard.press('Control+z');
  await expect.poll(async () => (await boxOf(stepOf(page, 'BRA-E002'))).y).toBeLessThan(table.y + 20);

  // H: the Hand; a drag of the background moves the view instead of choosing.
  await page.keyboard.press('h');
  await expect(tools.getByRole('radio', { name: 'Main' })).toHaveAttribute('aria-checked', 'true');
  const area = await boxOf(canvas(page));
  const left = (await boxOf(stepOf(page, 'BRA-E001'))).x;
  await page.mouse.move(area.x + 700, area.y + 450);
  await page.mouse.down();
  await page.mouse.move(area.x + 600, area.y + 450, { steps: 10 });
  await page.mouse.up();
  await expect.poll(async () => (await boxOf(stepOf(page, 'BRA-E001'))).x).toBeCloseTo(left - 100, 0);
});
