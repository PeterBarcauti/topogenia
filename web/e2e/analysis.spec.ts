// analysis.spec.ts
// Journeys of the layer « Analysis » of the Makigami (plan 3.1, ADR 095): the handoffs of the expense claims counted by the core, marked on the canvas, put forward and numbered by their tile, the layer alone among the layers; the lead time of a claim in work and waits, and the places where the team can act.

import type { Page } from '@playwright/test';
import type { Api } from './support/api.ts';
import { expect, openMap, stepOf, test } from './support/journey.ts';

/**
 * The expense claims of the mockup, without their sheets: 120 claims a month; the employee enters them, the manager
 * validates them, accounting checks them and sends 15 % back to the employee; the software prepares the transfer, the bank
 * transfer follows, the employee is paid back. Four handoffs: employee to manager, manager to accounting, accounting back
 * to the employee, accounting to the employee through the software.
 */
async function claims(api: Api, figures = false): Promise<string> {
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
  await api.send('PUT', `/api/flows/${back}/share`, { share: 15 }, version(map.flows, back));
  if (figures) {
    await api.sheet(enter, 20, [2, 'day']);
    await api.sheet(validate, 15, [3, 'day']);
    await api.sheet(check, 10, [1, 'day'], false);
    await api.sheet(transfer, 0, [0, 'minute']);
    await api.sheet(bank, null, [2, 'day']);
  }
  return mapId;
}

const layer = (page: Page, name: string) => page.getByRole('toolbar').getByRole('button', { name, exact: true });

test('the layer marks the four handoffs of the claims, its tile counts them by case and numbers them on the map', async ({ page, api }) => {
  const mapId = await claims(api);
  await openMap(page, mapId, 'NDF-E007');
  const handoffs = page.locator('.react-flow__edge-path.is-handoff');
  await expect(layer(page, 'Analyse')).toHaveAttribute('aria-pressed', 'false');
  await expect(handoffs).toHaveCount(0);

  await layer(page, 'Analyse').click();
  await expect(layer(page, 'Analyse')).toHaveAttribute('aria-pressed', 'true');
  const panel = page.getByTestId('analysis-panel');
  await expect(panel.getByTestId('analysis-value-handoffs')).toHaveText('4');
  // 141 claims a month pass the employee, the manager and accounting (120 / 0.85), 21 go back, 120 are paid back.
  await expect(panel.getByTestId('analysis-note-handoffs')).toHaveText('3,5 par dossier');
  await expect(handoffs).toHaveCount(4);
  await expect(page.getByTestId('handoff-rank')).toHaveCount(0);

  const tile = panel.getByTestId('analysis-tile-handoffs');
  await tile.hover();
  const ranks = page.getByTestId('handoff-rank');
  await expect.poll(async () => (await ranks.allTextContents()).sort()).toEqual(['1', '2', '3', '4']);
  // The start and the preparation of the transfer play no part: they fade, the others stay.
  await expect(page.locator('.step.is-faded')).toHaveCount(2);
  await tile.click();
  await page.mouse.move(4, 4);
  await expect(tile).toHaveAttribute('aria-pressed', 'true');
  await expect(ranks).toHaveCount(4);
  await tile.click();
  await expect(page.locator('.step.is-faded')).toHaveCount(0);

  await layer(page, 'Flux de valeur').click();
  await expect(panel).toHaveCount(0);
  await expect(page.getByTestId('stream-panel')).toBeVisible();
  await expect(handoffs).toHaveCount(0);
});

/**
 * The claims with their sheets: 2, 3 and 1 days at the employee, the manager and accounting, 2 days of bank transfer. A
 * claim takes 9,1 days, rework included; it waits 8,9 of them, 5 right after a handoff: all its wait at the manager and at
 * accounting, the 15 % of the passages at the employee that come back from accounting.
 */
test('the panel tells the lead time of a claim in work and waits, and lists where the team can act, each place leading to its step', async ({ page, api }) => {
  const mapId = await claims(api, true);
  await openMap(page, mapId, 'NDF-E007');
  await layer(page, 'Analyse').click();
  const panel = page.getByTestId('analysis-panel');
  await expect(panel.getByTestId('analysis-lead')).toHaveText(/Délai\s*9,1 j\s*Travail\s*52,9 min/);
  await expect(panel.getByTestId('analysis-legend').locator('li')).toHaveText([/Travail 1\s%/, /Attente après un relais 55\s%/, /Autre attente 44\s%/]);
  await expect(panel.getByTestId('analysis-say')).toHaveText('Il attend 8,9 j sur 9,1 j, dont 5 j juste après un relais.');
  await expect(panel.getByTestId('analysis-say').locator('strong')).toHaveText('5 j juste après un relais');

  const places = panel.locator('.analysis-place');
  await expect(places).toHaveCount(3);
  await expect(places.first()).toHaveAttribute('data-testid', 'analysis-place-NDF-E003');
  // The time lost by case ranks the list and is the one told (F-106): 3 days at each passage, 141 passages for 120 claims.
  await expect(panel.getByTestId('analysis-places-measure')).toHaveText('temps perdu par dossier');
  await expect(places.first()).toContainText('3,5 j');
  await expect(places.first()).toContainText('Relais Salarié → Manager');
  await expect(panel.getByTestId('analysis-place-NDF-E002')).toContainText('Relais Comptabilité → Salarié');
  await panel.getByTestId('analysis-places-more').click();
  await expect(places).toHaveCount(4);
  await panel.getByTestId('analysis-place-NDF-E006').click();
  await expect(stepOf(page, 'NDF-E006')).toHaveClass(/is-selected/);
});
