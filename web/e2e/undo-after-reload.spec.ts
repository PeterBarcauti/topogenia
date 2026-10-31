// undo-after-reload.spec.ts
// Journeys of the history kept by the host (spec 4.6, 11; S-11-07, B-2.6-02, B-2.6-03): a change undone after the page is loaded again or after a detailed map was shown, then done again.

import type { Page } from '@playwright/test';
import type { Api } from './support/api.ts';
import { expect, openMap, stepOf, test } from './support/journey.ts';

/** Three steps in a row: E001 -> E002 -> E003. */
async function layOut(api: Api): Promise<string> {
  const { mapId } = await api.project('UND', 'Annulation');
  const lane = (await api.map(mapId)).lanes[0]?.id ?? '';
  const ids = [await api.step(mapId, lane, 0, 'Recevoir'), await api.step(mapId, lane, 1, 'Traiter'), await api.step(mapId, lane, 2, 'Classer')];
  await api.flow(mapId, ids[0] ?? '', ids[1] ?? '');
  await api.flow(mapId, ids[1] ?? '', ids[2] ?? '');
  return mapId;
}

/** The steps and flows of the map, by readable codes. */
async function shapeOf(api: Api, mapId: string): Promise<{ steps: string[]; flows: string[] }> {
  const map = await api.map(mapId);
  const codeOf = (id: string) => map.steps.find((step) => step.id === id)?.code ?? '?';
  return {
    steps: map.steps.map((step) => step.code).sort(),
    flows: map.flows.map((flow) => `${codeOf(flow.source_step_id)}>${codeOf(flow.target_step_id)}`).sort(),
  };
}

async function reloaded(page: Page): Promise<void> {
  await page.reload();
  await expect(stepOf(page, 'UND-E001')).toBeVisible();
}

test('a change is undone after the page is loaded again, and done again after another load (S-11-07, B-2.6-03)', async ({ page, api }) => {
  const mapId = await layOut(api);
  const whole = { steps: ['UND-E001', 'UND-E002', 'UND-E003'], flows: ['UND-E001>UND-E002', 'UND-E002>UND-E003'] };
  const stitched = { steps: ['UND-E001', 'UND-E003'], flows: ['UND-E001>UND-E003'] };
  // The writes of the set-up are already in the history of the host: the deletion below is its last entry.
  await openMap(page, mapId, 'UND-E001');

  // The middle step deleted by its menu: the flow is stitched (B-2.5-23).
  await stepOf(page, 'UND-E002').click({ button: 'right' });
  await page.getByRole('menuitem', { name: 'Supprimer' }).click();
  await expect(stepOf(page, 'UND-E002')).toHaveCount(0);
  await expect.poll(() => shapeOf(api, mapId)).toEqual(stitched);

  // A new load of the page: the history is read from the host, Undo is offered, and Ctrl+Z gives the step back.
  await reloaded(page);
  await expect(page.getByTestId('ribbon-undo')).toBeEnabled();
  await expect(page.getByTestId('ribbon-redo')).toBeDisabled();
  await page.locator('.react-flow__pane').click({ position: { x: 600, y: 600 } });
  await page.keyboard.press('Control+z');
  await expect(page.getByText(/^Annulé : /)).toBeVisible();
  await expect(stepOf(page, 'UND-E002')).toBeVisible();
  await expect.poll(() => shapeOf(api, mapId)).toEqual(whole);

  // Another load: Redo is offered and deletes the step again.
  await reloaded(page);
  await expect(page.getByTestId('ribbon-redo')).toBeEnabled();
  await page.getByTestId('ribbon-redo').click();
  await expect(page.getByText(/^Rétabli : /)).toBeVisible();
  await expect(stepOf(page, 'UND-E002')).toHaveCount(0);
  await expect.poll(() => shapeOf(api, mapId)).toEqual(stitched);
});

test('the history stays while the map shown goes into a detailed map and comes back (B-2.6-02)', async ({ page, api }) => {
  const mapId = await layOut(api);
  const last = (await api.map(mapId)).steps.find((step) => step.code === 'UND-E003');
  const subMapId = (await api.send<{ id: string | null }>('POST', `/api/steps/${last?.id ?? ''}/detail`, undefined, last?.version)).id ?? '';
  await api.step(subMapId, (await api.map(subMapId)).lanes[0]?.id ?? '', 0, 'Ranger');
  await openMap(page, mapId, 'UND-E001');
  await stepOf(page, 'UND-E002').click({ button: 'right' });
  await page.getByRole('menuitem', { name: 'Supprimer' }).click();
  await expect.poll(() => shapeOf(api, mapId)).toEqual({ steps: ['UND-E001', 'UND-E003'], flows: ['UND-E001>UND-E003'] });

  // Into the detailed map, then back by the breadcrumb: the deletion is still the one to undo.
  await stepOf(page, 'UND-E003').dblclick();
  await expect(stepOf(page, 'UND-E004')).toBeVisible();
  await page.getByTestId('breadcrumb').getByRole('link', { name: 'Annulation' }).click();
  await expect(stepOf(page, 'UND-E001')).toBeVisible();
  await page.locator('.react-flow__pane').click({ position: { x: 600, y: 600 } });
  await page.keyboard.press('Control+z');
  await expect(page.getByText(/^Annulé : /)).toBeVisible();
  await expect.poll(() => shapeOf(api, mapId)).toEqual({ steps: ['UND-E001', 'UND-E002', 'UND-E003'], flows: ['UND-E001>UND-E002', 'UND-E002>UND-E003'] });
});
