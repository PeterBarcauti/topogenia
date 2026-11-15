// clipboard.spec.ts
// Journeys of the clipboard between the maps of a project (annex B 2.7): a copy taken in one map and pasted in another; a cut moved with its code, its sheet and its documents, undone on the map it came to.

import type { Page } from '@playwright/test';
import type { Api } from './support/api.ts';
import { expect, openMap, stepOf, test } from './support/journey.ts';

interface Laid { rootId: string; subMapId: string }

/** Recevoir -> Contrôler -> Détail; Contrôler has a sheet and an output; Détail is detailed, its map holds « Intérieur ». */
async function layOut(api: Api): Promise<Laid> {
  const { mapId: rootId } = await api.project('CLP', 'Presse-papiers');
  const lane = (await api.map(rootId)).lanes[0]?.id ?? '';
  const ids = [await api.step(rootId, lane, 0, 'Recevoir'), await api.step(rootId, lane, 1, 'Contrôler'), await api.step(rootId, lane, 2, 'Détail')];
  await api.flow(rootId, ids[0] ?? '', ids[1] ?? '');
  await api.flow(rootId, ids[1] ?? '', ids[2] ?? '');
  await api.send('PUT', `/api/steps/${ids[1] ?? ''}/sheet`, { fields: {}, procedure: [], free_text: 'Vérifier le bon de commande.' }, 0);
  await api.send('POST', `/api/steps/${ids[1] ?? ''}/documents`, { direction: 'output', name: 'Rapport de contrôle', location: null, wait_hours: null, share_confirmed: null });
  const version = (await api.map(rootId)).steps.find((step) => step.id === ids[2])?.version;
  const subMapId = (await api.send<{ id: string | null }>('POST', `/api/steps/${ids[2] ?? ''}/detail`, undefined, version)).id ?? '';
  await api.step(subMapId, (await api.map(subMapId)).lanes[0]?.id ?? '', 0, 'Intérieur');
  return { rootId, subMapId };
}

const codesOf = async (api: Api, mapId: string): Promise<string[]> => (await api.map(mapId)).steps.map((step) => step.code).sort();

/** The detailed step opened by a double click: its map is shown. */
async function intoDetail(page: Page): Promise<void> {
  await stepOf(page, 'CLP-E003').dblclick();
  await expect(stepOf(page, 'CLP-E004')).toBeVisible();
}

/** A right click on the background of the map shown, then « Coller ici » (B-2.4-38). */
async function pasteHere(page: Page): Promise<void> {
  await page.locator('.react-flow__pane').click({ button: 'right', position: { x: 700, y: 170 } });
  await page.getByRole('menuitem', { name: 'Coller ici' }).click();
}

test('a copy taken in one map is pasted in another map of the project (B-2.7-02)', async ({ page, api }) => {
  const { rootId, subMapId } = await layOut(api);
  await openMap(page, rootId, 'CLP-E001');
  await stepOf(page, 'CLP-E001').click();
  await page.keyboard.press('Control+c');
  await intoDetail(page);
  await pasteHere(page);
  await expect.poll(() => codesOf(api, subMapId)).toEqual(['CLP-E004', 'CLP-E005']);
  const copy = (await api.map(subMapId)).steps.find((step) => step.code === 'CLP-E005');
  expect(copy?.label).toBe('Recevoir');
  expect(await codesOf(api, rootId)).toEqual(['CLP-E001', 'CLP-E002', 'CLP-E003']);
});

test('a cut pasted in another map moves the step with its code, its sheet and its documents; undone on the map it came to, the stack of the other kept (B-2.7-10, B-2.7-11)', async ({ page, api }) => {
  const { rootId, subMapId } = await layOut(api);
  await openMap(page, rootId, 'CLP-E002');
  await stepOf(page, 'CLP-E002').click();
  await page.keyboard.press('Control+x');
  await intoDetail(page);
  await pasteHere(page);
  await expect.poll(() => codesOf(api, subMapId)).toEqual(['CLP-E002', 'CLP-E004']);
  expect(await codesOf(api, rootId)).toEqual(['CLP-E001', 'CLP-E003']);
  const moved = (await api.map(subMapId)).steps.find((step) => step.code === 'CLP-E002');
  const sheet = await api.get<{ content: { free_text: string | null }; outputs: { document: { name: string } }[] }>(`/api/steps/${moved?.id ?? ''}/sheet`);
  expect([sheet.content.free_text, sheet.outputs.map((output) => output.document.name)]).toEqual(['Vérifier le bon de commande.', ['Rapport de contrôle']]);

  // The paste is written on the map it came to: undone there, the step goes back.
  await page.locator('.react-flow__pane').click({ position: { x: 700, y: 400 } });
  await page.keyboard.press('Control+z');
  await expect(page.getByText(/^Annulé : /)).toBeVisible();
  await expect.poll(() => codesOf(api, rootId)).toEqual(['CLP-E001', 'CLP-E002', 'CLP-E003']);
  expect(await codesOf(api, subMapId)).toEqual(['CLP-E004']);

  // Back on the map it left, its own stack is kept (1.x: emptied).
  await page.getByTestId('breadcrumb').getByRole('link', { name: 'Presse-papiers' }).click();
  await expect(stepOf(page, 'CLP-E002')).toBeVisible();
  await expect(page.getByTestId('ribbon-undo')).toBeEnabled();
});
