// target-state.spec.ts
// Journeys of the photos and the target state of the Makigami (M6, M8, P3e, ADR 095): a photo taken in the panel « Analysis » and compared with now; the target state created from the File menu, its reference compared with the process drawn in it, then read again on its source.

import type { Page } from '@playwright/test';
import type { Api } from './support/api.ts';
import { expect, openMap, test } from './support/journey.ts';
import { fileEntry } from './support/file-menu.ts';

/**
 * The expense claims with their sheets: 120 claims a month, entered by the employee in 2 days, validated by the manager in
 * 3 days, checked by accounting in 1 day, which sends 15 % back; the software prepares the transfer, paid in 2 days.
 */
async function claims(api: Api): Promise<{ mapId: string; code: string }> {
  const { mapId, code } = await api.project('NDF', 'Notes de frais');
  const employee = (await api.map(mapId)).lanes[0]?.id ?? '';
  const manager = await api.lane(mapId, 'Manager', 'person');
  const accounting = await api.lane(mapId, 'Comptabilité');
  const software = await api.lane(mapId, 'Logiciel de frais', 'system');
  const start = await api.step(mapId, employee, 0, 'Note de frais à rembourser', 'start');
  const enter = await api.step(mapId, employee, 1, 'Saisir la note de frais');
  const validate = await api.step(mapId, manager, 2, 'Valider la note');
  const check = await api.step(mapId, accounting, 3, 'Note conforme ?', 'decision');
  const transfer = await api.step(mapId, software, 4, 'Préparer le virement');
  const bank = await api.step(mapId, software, 5, 'Virement bancaire', 'wait');
  const end = await api.step(mapId, employee, 6, 'Note remboursée', 'end');
  for (const [source, target] of [[start, enter], [enter, validate], [validate, check], [transfer, bank], [bank, end]] as const) await api.flow(mapId, source, target);
  await api.flow(mapId, check, transfer, 'oui');
  const back = await api.flow(mapId, check, enter, 'non');
  const map = await api.map(mapId);
  const version = (list: readonly { id: string; version: number }[], id: string) => list.find((item) => item.id === id)?.version;
  await api.send('PATCH', `/api/steps/${start}`, { volume: { quantity: 120, period: 'month' } }, version(map.steps, start));
  await api.send('PUT', `/api/flows/${back}/share`, { share: 15 }, version(map.flows, back));
  await api.sheet(enter, 20, [2, 'day']);
  await api.sheet(validate, 15, [3, 'day']);
  await api.sheet(check, 10, [1, 'day']);
  await api.sheet(transfer, 0, [0, 'minute']);
  await api.sheet(bank, null, [2, 'day']);
  return { mapId, code };
}

const analysis = (page: Page) => page.getByRole('toolbar').getByRole('button', { name: 'Analyse', exact: true });

test('a photo taken in the panel « Analyse » is compared with now, each indicator in its unit', async ({ page, api }) => {
  const { mapId } = await claims(api);
  await openMap(page, mapId, 'NDF-E007');
  await analysis(page).click();
  const photos = page.getByTestId('snapshots');
  await expect(photos.getByTestId('snapshots-none')).toBeVisible();
  await photos.getByTestId('snapshot-take').click();
  await photos.getByTestId('snapshot-take-form-label').fill('Avant l’atelier');
  await photos.getByTestId('snapshot-take-form-send').click();
  const photo = photos.locator('.snapshot');
  await expect(photo).toHaveCount(1);
  await expect(photo).toContainText('Avant l’atelier');
  await photo.getByRole('button', { name: 'Comparer à maintenant' }).click();
  const lead = photo.locator('tr[data-testid$="-lead_hours"]');
  await expect(lead.locator('td')).toHaveText([/9,1\sj/, /9,1\sj/, /0/]);
  await expect(photo.locator('tr[data-testid$="-handoff_wait_hours"]')).toHaveClass('is-part');
});

test('the target state made from the File menu compares its process with its reference, read again on its source by its facilitator', async ({ page, api }) => {
  const { mapId, code } = await claims(api);
  await openMap(page, mapId, 'NDF-E007');
  await page.getByTestId('header-file').click();
  await (await fileEntry(page, 'file.target')).click();
  const dialog = page.getByRole('dialog', { name: 'Créer l’état cible' });
  await expect(dialog.getByLabel('Nom du projet')).toHaveValue('Notes de frais (état cible)');
  await expect(dialog.getByLabel('Libellé de la référence')).toHaveValue('État actuel');
  await dialog.getByLabel('Code court').fill('NDFC');
  await dialog.getByRole('button', { name: 'Créer' }).click();
  await expect(page).not.toHaveURL(new RegExp(mapId));
  const targetMapId = /\/maps\/([^/?#]+)/.exec(page.url())?.[1] ?? '';

  // The process wanted: accounting sends 5 % back, no longer 15 %.
  const target = await api.map(targetMapId);
  const back = target.flows.find((flow) => flow.label === 'non');
  await api.send('PUT', `/api/flows/${back?.id ?? ''}/share`, { share: 5 }, back?.version);
  await page.reload();
  await analysis(page).click();
  const state = page.getByTestId('target-state');
  await expect(state.getByTestId('target-reference')).toContainText('Référence : « État actuel »');
  await expect(state.getByTestId('target-source')).toHaveText(`Projet source : ${code}`);
  const handoffs = state.locator('[data-testid="target-comparison-handoffs_per_case"] td');
  await expect(handoffs.nth(2)).toHaveClass('is-better');
  await expect(state.locator('[data-testid="target-comparison-lead_hours"] td').nth(2)).toHaveClass('is-better');

  await state.getByTestId('target-refresh').click();
  await state.getByTestId('target-refresh-form-label').fill('État actuel, relu');
  await state.getByTestId('target-refresh-form-send').click();
  await expect(state.getByTestId('target-reference')).toContainText('Référence : « État actuel, relu »');
});
