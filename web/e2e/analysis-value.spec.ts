// analysis-value.spec.ts
// Journeys of the value of the steps and the breaks of medium under the layer « Analysis » (Makigami M1, M3, plan 3.1, ADR 095): a class chosen in the menu of a step, a break found from the media of the documents, their tiles and their focus.

import type { Page } from '@playwright/test';
import type { Api } from './support/api.ts';
import { expect, openMap, stepOf, test } from './support/journey.ts';

/**
 * A request entered by the employee on a paper form, validated by the manager who answers by email: a break of medium at
 * the validation; no step classified yet.
 */
async function request(api: Api): Promise<string> {
  const { mapId } = await api.project('VAL', 'Valeur et supports');
  const employee = (await api.map(mapId)).lanes[0]?.id ?? '';
  const manager = await api.lane(mapId, 'Manager', 'person');
  const start = await api.step(mapId, employee, 0, 'Demande', 'start');
  const enter = await api.step(mapId, employee, 1, 'Remplir le formulaire');
  const validate = await api.step(mapId, manager, 2, 'Valider la demande');
  const end = await api.step(mapId, manager, 3, 'Demande traitée', 'end');
  for (const [source, target] of [[start, enter], [enter, validate], [validate, end]] as const) await api.flow(mapId, source, target);
  const form = await api.send<{ outputs: { document: { id: string } }[] }>('POST', `/api/steps/${enter}/documents`,
    { direction: 'output', name: 'Formulaire papier', location: null, wait_hours: null, share_confirmed: false, medium: 'paper' });
  const formId = form.outputs[0]?.document.id ?? '';
  await api.send('PUT', `/api/steps/${validate}/uses/${formId}`, { wait_hours: null, percent_complete_accurate: null, pain_point: null, lost_hours_per_month: null }, 0);
  await api.send('POST', `/api/steps/${validate}/documents`,
    { direction: 'output', name: 'Réponse', location: null, wait_hours: null, share_confirmed: false, medium: 'email' });
  return mapId;
}

const layer = (page: Page) => page.getByRole('toolbar').getByRole('button', { name: 'Analyse', exact: true });

test('a break of medium is found from the media of the documents, and a class chosen in the menu of a step is drawn and counted', async ({ page, api }) => {
  const mapId = await request(api);
  await openMap(page, mapId, 'VAL-E004');
  await layer(page).click();
  const panel = page.getByTestId('analysis-panel');
  await expect(panel.getByTestId('analysis-value-breaks')).toHaveText('1');
  await expect(panel.getByTestId('analysis-note-breaks')).toHaveText('Supports renseignés : 2 / 2');
  await expect(stepOf(page, 'VAL-E003')).toHaveClass(/is-media-break/);
  await expect(panel.getByTestId('analysis-value-value')).toHaveText('0 / 2');

  await stepOf(page, 'VAL-E002').click({ button: 'right' });
  await page.getByRole('menuitemradio', { name: /Gaspillage/ }).click();
  await expect(stepOf(page, 'VAL-E002').getByTestId('step-value')).toHaveAttribute('aria-label', 'Gaspillage');
  await expect(panel.getByTestId('analysis-note-value')).toHaveText('Non classées : 1');

  await panel.getByTestId('analysis-tile-breaks').hover();
  await expect(page.locator('.step.is-faded')).toHaveCount(3);
  await expect(stepOf(page, 'VAL-E003')).not.toHaveClass(/is-faded/);
  await panel.getByTestId('analysis-tile-value').hover();
  await expect(stepOf(page, 'VAL-E002')).not.toHaveClass(/is-faded/);
  await expect(page.locator('.step.is-faded')).toHaveCount(3);
});
