// history-replies.spec.ts
// The history of the host and the map each command answers (spec 3.4, 4.6): one drag of a block undone by one undo, a lane renamed in its header and undone, the buttons that follow the history of the host, no reading of the whole map after a command.

import type { Page } from '@playwright/test';
import type { Api } from './support/api.ts';
import { expect, openMap, stepOf, test } from './support/journey.ts';
import { boxOf, canvas, centreOf, doubleClickCell, labelInput, laneHead, laneOf, ribbon, stepIn, zone } from './support/map-editing.ts';

/** Saisie (HIS-E001), Contrôle (HIS-E002) and Envoi (HIS-E003) on the lane Ventes, linked; an empty lane Logistique below. */
async function layOut(page: Page, api: Api): Promise<string> {
  const { mapId } = await api.project('HIS', 'Historique');
  const sales = await laneOf(api, mapId);
  await api.send('PATCH', `/api/lanes/${sales}`, { name: 'Ventes' }, (await api.map(mapId)).lanes[0]?.version);
  await api.lane(mapId, 'Logistique');
  const ids = [await api.step(mapId, sales, 0, 'Saisie'), await api.step(mapId, sales, 1, 'Contrôle'), await api.step(mapId, sales, 2, 'Envoi')];
  await api.flow(mapId, ids[0] ?? '', ids[1] ?? '');
  await api.flow(mapId, ids[1] ?? '', ids[2] ?? '');
  await openMap(page, mapId, 'HIS-E003');
  return mapId;
}

/** The calls to the API from now on, as `METHOD /path`. */
function callsOf(page: Page): string[] {
  const calls: string[] = [];
  page.on('request', (request) => {
    const url = new URL(request.url());
    if (url.pathname.startsWith('/api/')) calls.push(`${request.method()} ${url.pathname}`);
  });
  return calls;
}

/** Where the host keeps each step: its lane, its column and its row. */
const cells = async (api: Api, mapId: string, codes: readonly string[]) => {
  const map = await api.map(mapId);
  return codes.map((code) => { const step = stepIn(map, code); return [step.lane_id, step.column, step.row]; });
};

test('one drag of two steps is one operation of the host, and one Ctrl+Z puts both back where they were (B-2.6-21)', async ({ page, api }) => {
  const mapId = await layOut(page, api);
  const before = await cells(api, mapId, ['HIS-E002', 'HIS-E003']);
  await zone(page, 'Outils').getByRole('radio', { name: 'Sélection' }).click();
  const control = await boxOf(stepOf(page, 'HIS-E002'));
  const sending = await boxOf(stepOf(page, 'HIS-E003'));
  await page.mouse.move(control.x - 15, control.y - 15);
  await page.mouse.down();
  await page.mouse.move(sending.x + sending.width + 15, sending.y + sending.height + 15, { steps: 10 });
  await page.mouse.up();
  await expect(page.locator('.react-flow__node-step.selected')).toHaveCount(2);

  const calls = callsOf(page);
  const centre = centreOf(control);
  await page.mouse.move(centre.x, centre.y);
  await page.mouse.down();
  await page.mouse.move(centre.x, centre.y + 120, { steps: 12 });
  await page.mouse.up();
  const logistics = (await api.map(mapId)).lanes.find((lane) => lane.code === 'HIS-C02')?.id;
  await expect.poll(async () => (await cells(api, mapId, ['HIS-E002', 'HIS-E003'])).map(([lane]) => lane)).toEqual([logistics, logistics]);
  expect(calls.filter((call) => call.startsWith('PUT') || call.startsWith('POST') || call.startsWith('PATCH'))).toEqual([`PUT /api/maps/${mapId}/layout`]);

  await page.keyboard.press('Control+z');
  await expect.poll(() => cells(api, mapId, ['HIS-E002', 'HIS-E003'])).toEqual(before);
  await expect.poll(async () => (await boxOf(stepOf(page, 'HIS-E003'))).y).toBeLessThan(sending.y + 5);
  expect(calls.filter((call) => call.endsWith('/undo'))).toEqual([`POST /api/maps/${mapId}/undo`]);
});

test('a lane renamed in its header goes into the history of the host: Ctrl+Z and Ctrl+Y, the buttons following it, after a reload too (B-2.12-13, B-7-17, B-8.1-12)', async ({ page, api }) => {
  const mapId = await layOut(page, api);
  const undo = ribbon(page).getByTestId('ribbon-undo');
  const redo = ribbon(page).getByTestId('ribbon-redo');
  // The history read with the map: the writes of the layout are there to undo, nothing to redo.
  await expect(undo).toBeEnabled();
  await expect(redo).toBeDisabled();
  const name = async () => (await api.map(mapId)).lanes.find((lane) => lane.code === 'HIS-C01')?.name;

  await laneHead(page, 'HIS-C01').dblclick();
  await page.getByTestId('lane-name-input').fill('Ventes France');
  await page.keyboard.press('Enter');
  await expect.poll(name).toBe('Ventes France');

  const calls = callsOf(page);
  await canvas(page).locator('.react-flow__pane').click({ position: { x: 700, y: 450 } });
  await page.keyboard.press('Control+z');
  await expect.poll(name).toBe('Ventes');
  await expect(laneHead(page, 'HIS-C01')).toContainText('Ventes');
  await expect(redo).toBeEnabled();
  // The undo is the one of the host: one call, no write of the client to take the change back.
  const writes = () => calls.filter((call) => !call.startsWith('GET'));
  expect(writes()).toEqual([`POST /api/maps/${mapId}/undo`]);

  await page.keyboard.press('Control+y');
  await expect.poll(name).toBe('Ventes France');
  await expect(redo).toBeDisabled();
  expect(writes()).toEqual([`POST /api/maps/${mapId}/undo`, `POST /api/maps/${mapId}/redo`]);

  // The history is kept by the host: after a reload, the rename is still there to undo.
  await page.reload();
  await expect(laneHead(page, 'HIS-C01')).toContainText('Ventes France');
  await expect(undo).toBeEnabled();
  await expect(redo).toBeDisabled();
});

test('each command answers the map: after the opening, no reading of the whole map for a step created, named, moved and deleted (B-8.1-46)', async ({ page, api }) => {
  const mapId = await layOut(page, api);
  const calls = callsOf(page);
  await doubleClickCell(page, laneHead(page, 'HIS-C02'), 1);
  await labelInput(page).fill('Préparer');
  await page.keyboard.press('Enter');
  await expect(stepOf(page, 'HIS-E004')).toContainText('Préparer');
  await stepOf(page, 'HIS-E004').click();
  await page.keyboard.press('ArrowRight');
  await expect.poll(async () => stepIn(await api.map(mapId), 'HIS-E004').column).toBe(2);
  await page.keyboard.press('Delete');
  await expect(stepOf(page, 'HIS-E004')).toHaveCount(0);

  const writes = calls.filter((call) => !call.startsWith('GET'));
  expect(writes.length).toBeGreaterThanOrEqual(4);
  expect(calls.filter((call) => call === `GET /api/maps/${mapId}`)).toEqual([]);
});
