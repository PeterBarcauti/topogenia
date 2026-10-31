// pictograms.spec.ts
// Journey of the pictograms of the steps (annex B 2.10, 8.3): documents, pain points and duration in the bottom right corner, always shown or on hover only, the choice kept; the Hand at each opening.

import type { Page } from '@playwright/test';
import type { components } from '../src/api/generated/schema.ts';
import type { Api } from './support/api.ts';
import { expect, openMap, stepOf, test } from './support/journey.ts';
import { laneOf, ribbon, zone } from './support/map-editing.ts';

type SheetRead = components['schemas']['SheetView'];

/** Extraction produces « Extraction SAP », which Tableau uses at 0 % with a pain point; Tableau lasts 90 minutes. */
async function layOut(page: Page, api: Api): Promise<void> {
  const { mapId } = await api.project('PIC', 'Pictogrammes');
  const lane = await laneOf(api, mapId);
  const extraction = await api.step(mapId, lane, 0, 'Extraction');
  const table = await api.step(mapId, lane, 1, 'Tableau');
  await api.flow(mapId, extraction, table);
  const produced = await api.send<SheetRead>('POST', `/api/steps/${extraction}/documents`, {
    direction: 'output', name: 'Extraction SAP', location: null, wait_hours: null, share_confirmed: false,
  });
  const documentId = produced.outputs[0]?.document.id ?? '';
  // A use never saved, a sheet never saved: version 0, which the server mode requires (ADR 045).
  await api.send('PUT', `/api/steps/${table}/uses/${documentId}`, { wait_hours: null, percent_complete_accurate: 0, pain_point: 'Requête périmée', lost_hours_per_month: 2 }, 0);
  const sheet = await api.get<SheetRead>(`/api/steps/${table}/sheet`);
  await api.send('PUT', `/api/steps/${table}/sheet`, { fields: { duration_minutes: { value: null, number: 90 } }, procedure: [], free_text: null }, sheet.version ?? 0);
  await openMap(page, mapId, 'PIC-E002');
}

const pictosOf = (page: Page, code: string) => stepOf(page, code).getByTestId('step-pictos');
const hand = (page: Page) => zone(page, 'Outils').getByRole('radio', { name: 'Main' });
const pictosButton = (page: Page) => ribbon(page).getByRole('button', { name: 'Pictogrammes toujours affichés' });

test('the pictograms of the steps, on hover as a kept choice, the Hand at the opening (B-8.3-39, B-8.3-40, B-2.1-11)', async ({ page, api }) => {
  await layOut(page, api);

  // The Hand by default.
  await expect(hand(page)).toHaveAttribute('aria-checked', 'true');

  // Extraction: 1 document produced, 1 pain point (raised on the producer). Tableau: 1 received, 1 h 30.
  await expect(pictosOf(page, 'PIC-E001').getByRole('img', { name: 'Documents : Extraction SAP (produit)' })).toHaveText('1');
  await expect(pictosOf(page, 'PIC-E001').getByRole('img', { name: /^Irritants ouverts : PIC-I001 Requête périmée/ })).toHaveText('1');
  await expect(pictosOf(page, 'PIC-E002').getByRole('img', { name: 'Documents : Extraction SAP (reçu)' })).toBeVisible();
  await expect(pictosOf(page, 'PIC-E002').getByRole('img', { name: 'Durée : 1 h 30' })).toHaveText('1 h 30');

  // On hover only: hidden, shown when the pointer passes; the choice is kept.
  await expect(pictosButton(page)).toHaveAttribute('aria-pressed', 'true');
  await pictosButton(page).click();
  await expect(pictosButton(page)).toHaveAttribute('aria-pressed', 'false');
  await expect(pictosOf(page, 'PIC-E002')).toHaveCSS('opacity', '0');
  await stepOf(page, 'PIC-E002').hover();
  await expect(pictosOf(page, 'PIC-E002')).toHaveCSS('opacity', '1');
  // The tool is not kept: Selection now, the Hand again after the reload (B-2.1-11).
  await zone(page, 'Outils').getByRole('radio', { name: 'Sélection' }).click();
  await expect(zone(page, 'Outils').getByRole('radio', { name: 'Sélection' })).toHaveAttribute('aria-checked', 'true');
  await page.reload();
  await expect(pictosButton(page)).toHaveAttribute('aria-pressed', 'false');
  await expect(pictosOf(page, 'PIC-E001')).toHaveCSS('opacity', '0');
  await expect(hand(page)).toHaveAttribute('aria-checked', 'true');
  await pictosButton(page).click();
  await expect(pictosOf(page, 'PIC-E001')).toHaveCSS('opacity', '1');
});
