// types-and-detail.spec.ts
// Journeys of the type of a step and of its detail (annex B 2.4, 8.3): the type changed by the menu, the shapes for the new steps only, a detail deleted empty or filled, a detailed map left empty, back to the map above (AR-155).

import type { Page } from '@playwright/test';
import type { Api } from './support/api.ts';
import { expect, openMap, stepOf, test } from './support/journey.ts';
import {
  boxOf, canvas, changeType, doubleClickCell, firstLaneHead, labelInput, laneHead, laneOf, openStepMenu, shape, stepIn, undoFromBackground,
} from './support/map-editing.ts';

/** Saisie, Contrôle, Envoi in the first lane. */
async function layOut(page: Page, api: Api): Promise<string> {
  const { mapId } = await api.project('TYP', 'Types');
  const lane = await laneOf(api, mapId);
  for (const [column, label] of ['Saisie', 'Contrôle', 'Envoi'].entries()) await api.step(mapId, lane, column, label);
  await openMap(page, mapId, 'TYP-E003');
  return mapId;
}

const crumbs = (page: Page) => page.getByTestId('breadcrumb');

/** An empty place of the background, right of the steps and under the first lane. */
async function background(page: Page) {
  const area = await boxOf(canvas(page));
  return { x: area.x + 600, y: area.y + 300 };
}

/** « Détailler cette étape » from the menu: its detailed map is shown, empty, and may be worked on. */
async function detail(page: Page, code: string): Promise<void> {
  const menu = await openStepMenu(page, code);
  await menu.getByRole('menuitem', { name: 'Détailler cette étape' }).click();
  await expect(crumbs(page).locator('[aria-current="page"]')).toBeVisible();
  await expect(page.getByTestId('map-empty')).toContainText('Cette carte est vide.');
}

/** Back to the root map by the breadcrumb. */
async function back(page: Page): Promise<void> {
  await crumbs(page).getByRole('link', { name: 'Types' }).click();
  await expect(stepOf(page, 'TYP-E003')).toBeVisible();
}

/** A step created by a double click in the first lane of the map shown, then named. */
async function createNamed(page: Page, column: number, label: string): Promise<void> {
  await doubleClickCell(page, firstLaneHead(page), column);
  await expect(labelInput(page)).toBeFocused();
  await labelInput(page).fill(label);
  await labelInput(page).press('Enter');
}

test('the type of a step is changed by its menu, undoable; no floating bar (B-8.3-63, B-2.4-25)', async ({ page, api }) => {
  await layOut(page, api);
  await stepOf(page, 'TYP-E002').click();
  // No bar of types floating over the selection: the ribbon is the only toolbar.
  await expect(page.getByRole('toolbar')).toHaveCount(1);

  await changeType(page, 'TYP-E002', 'Décision');
  await expect(stepOf(page, 'TYP-E002')).toHaveClass(/step--decision/);
  await changeType(page, 'TYP-E003', 'Attente');
  await expect(stepOf(page, 'TYP-E003')).toHaveClass(/step--wait/);
  // The seven types in the menu, the type of the moment marked.
  const menu = await openStepMenu(page, 'TYP-E003');
  await expect(menu.getByRole('menuitemradio', { name: /^Changer le type : / })).toHaveCount(7);
  await expect(menu.getByRole('menuitemradio', { name: 'Changer le type : Attente' })).toHaveAttribute('aria-checked', 'true');
  await page.keyboard.press('Escape');
  await expect(menu).toHaveCount(0);
  // The shape of the next steps did not move.
  await expect(shape(page, 'Étape')).toHaveAttribute('aria-checked', 'true');

  await undoFromBackground(page, await background(page));
  await expect(stepOf(page, 'TYP-E003')).toHaveClass(/step--task/);
  await expect(stepOf(page, 'TYP-E002')).toHaveClass(/step--decision/);
});

test('the shapes give the type of the next step, even with a step chosen, then « Étape » again (B-8.3-64, B-2.1-15, B-2.3-09, AR-152)', async ({ page, api }) => {
  const mapId = await layOut(page, api);
  const create = async (column: number) => {
    await doubleClickCell(page, laneHead(page, 'TYP-C01'), column);
    await expect(labelInput(page)).toBeFocused();
    await labelInput(page).press('Enter');
  };

  await shape(page, 'Début').click();
  await create(3);
  await expect(stepOf(page, 'TYP-E004')).toHaveClass(/step--start/);
  // AR-152: the type chosen was for that step only.
  await expect(shape(page, 'Étape')).toHaveAttribute('aria-checked', 'true');
  await shape(page, 'Début').click();
  // The gesture of the report of the 1.x: a click on the new step (it is chosen), then « Étape » in the shapes.
  await stepOf(page, 'TYP-E004').click();
  await expect(stepOf(page, 'TYP-E004')).toHaveClass(/is-selected/);
  await shape(page, 'Étape').click();
  await expect(stepOf(page, 'TYP-E004')).toHaveClass(/step--start/);
  await create(4);
  await expect(stepOf(page, 'TYP-E005')).toHaveClass(/step--task/);
  const map = await api.map(mapId);
  expect([stepIn(map, 'TYP-E004'), stepIn(map, 'TYP-E005')].map((step) => `${step.code}:${step.type}:${String(step.column)}`)).toEqual(['TYP-E004:start:3', 'TYP-E005:task:4']);
});

test('deleting a detail: an empty one at once, then the type End; a filled one after a question, undoable (B-8.3-65, B-8.3-66, B-2.4-18, B-2.3-10)', async ({ page, api }) => {
  const mapId = await layOut(page, api);

  // The case of the 1.x report: a detail opened, then left empty.
  await detail(page, 'TYP-E003');
  await back(page);
  const menu = await openStepMenu(page, 'TYP-E003');
  await menu.getByRole('menuitem', { name: 'Supprimer le détail' }).click();
  await expect(page.getByRole('dialog')).toHaveCount(0);
  // An empty detail changes nothing on the screen: its menu says when the reply of the host is there.
  await expect(async () => {
    const again = await openStepMenu(page, 'TYP-E003');
    await expect(again.getByRole('menuitem', { name: 'Supprimer le détail' })).toHaveCount(0, { timeout: 1000 });
  }).toPass({ timeout: 10_000 });
  expect(stepIn(await api.map(mapId), 'TYP-E003').sub_map_id).toBeNull();
  await page.getByRole('menuitemradio', { name: 'Changer le type : Fin' }).click();
  await expect(stepOf(page, 'TYP-E003')).toHaveClass(/step--end/);

  // A filled detail: a question first, then Ctrl+Z gives the detailed map back with what it holds.
  await detail(page, 'TYP-E002');
  await createNamed(page, 0, 'Vérifier');
  await expect(stepOf(page, 'TYP-E004')).toContainText('Vérifier');
  await back(page);
  await expect(stepOf(page, 'TYP-E002')).toHaveClass(/is-macro/);
  const filled = await openStepMenu(page, 'TYP-E002');
  await filled.getByRole('menuitem', { name: 'Supprimer le détail' }).click();
  await page.getByRole('dialog').getByRole('button', { name: 'Supprimer' }).click();
  await expect(stepOf(page, 'TYP-E002')).not.toHaveClass(/is-macro/);
  await undoFromBackground(page, await background(page));
  await expect(stepOf(page, 'TYP-E002')).toHaveClass(/is-macro/);
  await stepOf(page, 'TYP-E002').dblclick();
  await expect(stepOf(page, 'TYP-E004')).toContainText('Vérifier');
});

test('« Remonter d’un niveau » of the ribbon, or Alt+Up, goes back from a detailed map to the map above; disabled on the root map (AR-155)', async ({ page, api }) => {
  await layOut(page, api);
  const up = page.getByRole('toolbar').getByRole('button', { name: 'Remonter d’un niveau', exact: true });
  await expect(up).toBeDisabled();
  await expect(up).toHaveAttribute('title', 'Remonter d’un niveau (Alt+Haut)');
  await detail(page, 'TYP-E002');
  await expect(up).toBeEnabled();
  await up.click();
  await expect(stepOf(page, 'TYP-E003')).toBeVisible();
  await expect(crumbs(page).locator('[aria-current="page"]')).toHaveText('Types');
  await detail(page, 'TYP-E002');
  await page.locator('.react-flow__pane').click({ position: { x: 600, y: 300 } });
  await page.keyboard.press('Alt+ArrowUp');
  await expect(stepOf(page, 'TYP-E003')).toBeVisible();
});

test('a detailed map left empty: an ordinary step back, the same map opened again (B-8.3-67, B-2.3-11)', async ({ page, api }) => {
  await layOut(page, api);
  const menu = await openStepMenu(page, 'TYP-E002');
  await menu.getByRole('menuitem', { name: 'Détailler cette étape' }).click();
  await expect(crumbs(page).locator('[aria-current="page"]')).toHaveText('Contrôle');
  const subMapAddress = page.url();

  // Back without typing anything: no badge, the double click renames.
  await back(page);
  await expect(stepOf(page, 'TYP-E002')).not.toHaveClass(/is-macro/);
  await expect(stepOf(page, 'TYP-E002').getByTestId('step-detailed')).toHaveCount(0);
  await stepOf(page, 'TYP-E002').dblclick();
  await expect(labelInput(page)).toBeFocused();
  await page.keyboard.press('Escape');
  await expect(labelInput(page)).toHaveCount(0);

  // « Détailler » opens the same map again instead of making another one.
  const again = await openStepMenu(page, 'TYP-E002');
  await again.getByRole('menuitem', { name: 'Détailler cette étape' }).click();
  await expect(page).toHaveURL(subMapAddress);

  // Once filled, the step is detailed and the double click opens its map.
  await createNamed(page, 0, 'Vérifier les totaux');
  await expect(stepOf(page, 'TYP-E004')).toContainText('Vérifier les totaux');
  await back(page);
  await expect(stepOf(page, 'TYP-E002')).toHaveClass(/is-macro/);
  await expect(stepOf(page, 'TYP-E002').getByTestId('step-detailed')).toBeVisible();
  await stepOf(page, 'TYP-E002').dblclick();
  await expect(page).toHaveURL(subMapAddress);
});
