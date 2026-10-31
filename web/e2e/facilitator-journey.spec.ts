// facilitator-journey.spec.ts
// Journey of the facilitator from the empty project to the detailed map (1.x parcours-animateur.spec.ts): create a project, draw lanes, steps and flows with the mouse and by command, undo and redo, move, detail a step, switch the language.

import type { Locator, Page } from '@playwright/test';
import { COLUMN_WIDTH, LANE_HEADER_WIDTH, LEFT_MARGIN, ROW_HEIGHT, STEP_WIDTH } from '../src/features/map/model/geometry/constants.ts';
import type { Api } from './support/api.ts';
import { chooseInterfaceLanguage, chooseProcessLanguage, expect, stepOf, test } from './support/journey.ts';

const edges = (page: Page) => page.locator('.react-flow__edge');
const crumbShown = (page: Page) => page.getByTestId('breadcrumb').locator('[aria-current="page"]');
const mapIdOf = (page: Page): string => new URL(page.url()).pathname.replace('/maps/', '');

/** Double click on the background of a lane, in the middle of a cell of its first row: the pointer goes through the lane to the pane. */
async function doubleClickCell(page: Page, lane: Locator, column: number): Promise<void> {
  const body = await lane.boundingBox();
  if (body === null) throw new Error('The lane is not on the screen.');
  await page.mouse.dblclick(body.x + LANE_HEADER_WIDTH + LEFT_MARGIN + column * COLUMN_WIDTH + STEP_WIDTH / 2, body.y + ROW_HEIGHT / 2);
}

/** Types a name in the field opened in place, focused at once, and validates it. */
async function typeName(page: Page, testId: string, text: string): Promise<void> {
  const field = page.getByTestId(testId);
  await expect(field).toBeFocused();
  await field.fill(text);
  await field.press('Enter');
  await expect(field).toHaveCount(0);
}

async function homeBeforeAnything(page: Page): Promise<void> {
  await page.goto('/');
  // The texts of the home page changed by AR-88 (« Décrire un processus », 1.x « Apporter ma pierre »).
  await expect(page.getByRole('heading', { level: 1 })).toHaveText('Cartographiez vos processus réels pour automatiser les actions répétitives et gagner en efficacité.');
  await expect(page.getByRole('button', { name: 'Décrire un processus' })).toBeDisabled();
  await expect(page.getByTestId('home-empty')).toContainText('Aucun projet pour l’instant');
  // No Translations button without an open map (B-1.2-12).
  await expect(page.getByTestId('header-translations')).toHaveCount(0);
}

async function createProject(page: Page): Promise<void> {
  await page.getByRole('button', { name: 'Nouveau projet' }).click();
  await page.getByLabel('Code court').fill('vte9');
  await expect(page.getByLabel('Code court')).toHaveValue('VTE');
  // The language of the process is the one of the interface; the name translated into English at once (ADR 083).
  await expect(page.getByLabel('Langue du processus')).toHaveValue('fr');
  await page.getByLabel('Nom en français').fill('Processus ventes');
  await page.getByLabel('Traduire le nom en…').selectOption('en');
  await page.getByLabel('Nom en anglais').fill('Sales process');
  await page.getByRole('button', { name: 'Créer le projet' }).click();
  // The root map opens after the creation (B-1.4-15).
  await expect(page).toHaveURL(/\/maps\//);
  await expect(crumbShown(page)).toHaveText('Processus ventes');
  await expect(page.locator('.lane-head').first()).toContainText('Acteur 1');
  await expect(page.getByTestId('map-empty')).toContainText('Cette carte est vide.');
  // The map of a project the user may translate shows the Translations button (B-1.2-12).
  await expect(page.getByRole('button', { name: 'Traductions' })).toBeVisible();
}

async function drawLanes(page: Page): Promise<void> {
  await page.locator('[data-testid^="lane-add-"]').last().click();
  await typeName(page, 'lane-name-input', 'SAP');
  await expect(page.locator('.lane-head').nth(1)).toContainText('SAP');
  await page.getByTestId('lane-head-VTE-C01').dblclick();
  await typeName(page, 'lane-name-input', 'CDG Angers');
  await expect(page.locator('.lane-head').first()).toContainText('CDG Angers');
}

async function drawSteps(page: Page): Promise<void> {
  // A double click on the background makes a step of the current type in the cell aimed, its name typed at once (B-2.3-09).
  await doubleClickCell(page, page.getByTestId('lane-body-VTE-C01'), 0);
  await typeName(page, 'step-label-input', 'Extraction des ventes');
  await expect(stepOf(page, 'VTE-E001')).toContainText('Extraction des ventes');
  await expect(stepOf(page, 'VTE-E001')).toContainText('VTE-E001');

  await page.getByRole('toolbar').getByRole('radio', { name: 'Décision' }).click();
  await doubleClickCell(page, page.getByTestId('lane-body-VTE-C01'), 1);
  await typeName(page, 'step-label-input', 'Total conforme ?');
  await expect(stepOf(page, 'VTE-E002')).toHaveClass(/\bstep--decision\b/);
  await page.getByRole('toolbar').getByRole('radio', { name: 'Étape' }).click();
  await doubleClickCell(page, page.getByTestId('lane-body-VTE-C02'), 2);
  await typeName(page, 'step-label-input', 'Tableau croisé');
  await expect(stepOf(page, 'VTE-E003')).toContainText('Tableau croisé');
  await expect(page.getByTestId('map-empty')).toHaveCount(0);
}

async function linkByCommand(page: Page): Promise<void> {
  await page.locator('.react-flow__pane').click({ position: { x: 700, y: 400 } });
  await page.keyboard.press('Control+k');
  const field = page.getByRole('textbox', { name: 'Relier par les codes' });
  await field.fill('E1 > E2\nE2 > E3 : oui');
  await field.press('Enter');
  await expect(page.getByText('2 flèches ajoutées')).toBeVisible();
  await expect(edges(page)).toHaveCount(2);
  await expect(page.locator('.edge-label-text')).toHaveText('oui');

  // The errors of a command are said by line, nothing is made.
  await page.keyboard.press('Control+k');
  await field.fill('E1 > E99');
  await field.press('Enter');
  await expect(page.getByTestId('command-bar').getByRole('alert')).toContainText('VTE-E099');
  await page.keyboard.press('Escape');
  await expect(page.getByTestId('command-bar')).toHaveCount(0);
  await expect(edges(page)).toHaveCount(2);
}

async function linkByClicks(page: Page): Promise<void> {
  const link = page.getByRole('toolbar').getByRole('button', { name: 'Relier', exact: true });
  await link.click();
  // The first click chooses the source and says it (B-2.3-16), the second makes the flow and leaves the mode (B-2.3-17).
  await stepOf(page, 'VTE-E003').click();
  await expect(page.getByTestId('toasts')).toContainText('Départ : VTE-E003. Cliquer maintenant sur l’étape d’arrivée.');
  await expect(page.getByTestId('map-link')).toContainText('Départ : VTE-E003');
  await stepOf(page, 'VTE-E001').click();
  await expect(edges(page)).toHaveCount(3);
  await expect(page.getByTestId('map-link')).toHaveCount(0);
  await expect(link).toHaveAttribute('aria-pressed', 'false');
  // A way back: drawn as a loop under the steps.
  await expect(page.locator('.react-flow__edge-loop')).toHaveCount(1);

  await page.keyboard.press('Control+z');
  await expect(edges(page)).toHaveCount(2);
  await page.keyboard.press('Control+y');
  await expect(edges(page)).toHaveCount(3);
}

const laneOfStep = async (api: Api, mapId: string, code: string, lane: string): Promise<boolean> => {
  const map = await api.map(mapId);
  return map.steps.find((step) => step.code === code)?.lane_id === map.lanes.find((candidate) => candidate.code === lane)?.id;
};

async function dragToLane(page: Page, api: Api): Promise<void> {
  const box = await stepOf(page, 'VTE-E001').boundingBox();
  const target = await page.getByTestId('lane-body-VTE-C02').boundingBox();
  if (box === null || target === null) throw new Error('The step or the lane is not on the screen.');
  await page.mouse.move(box.x + box.width / 2, box.y + box.height / 2);
  await page.mouse.down();
  await page.mouse.move(box.x + box.width / 2 + 20, target.y + ROW_HEIGHT / 2, { steps: 8 });
  await page.mouse.up();
  await expect.poll(() => laneOfStep(api, mapIdOf(page), 'VTE-E001', 'VTE-C02')).toBe(true);
  await page.keyboard.press('Control+z');
  await expect.poll(() => laneOfStep(api, mapIdOf(page), 'VTE-E001', 'VTE-C01')).toBe(true);
}

async function deleteAndUndo(page: Page): Promise<void> {
  await stepOf(page, 'VTE-E003').click();
  await page.keyboard.press('Delete');
  await expect(stepOf(page, 'VTE-E003')).toHaveCount(0);
  // The flow is stitched (as Visio): E2 -> E3 -> E1 becomes E2 -> E1.
  await expect(edges(page)).toHaveCount(2);
  await page.keyboard.press('Control+z');
  await expect(stepOf(page, 'VTE-E003')).toBeVisible();
  await expect(edges(page)).toHaveCount(3);
}

async function detailStep(page: Page): Promise<void> {
  await stepOf(page, 'VTE-E001').click({ button: 'right' });
  await page.getByRole('menuitem', { name: 'Détailler cette étape' }).click();
  await expect(page.getByTestId('breadcrumb')).toContainText('Processus ventes');
  await expect(crumbShown(page)).toHaveText('Extraction des ventes');
  await expect(page.locator('.lane-head').first()).toContainText('CDG Angers');
  await doubleClickCell(page, page.locator('[data-testid^="lane-body-"]').first(), 0);
  await typeName(page, 'step-label-input', 'Lancer la transaction');
  await expect(stepOf(page, 'VTE-E004')).toBeVisible();

  // The levels of the breadcrumb by their depth from the root map (S-3.5-04): the map shown, then the root map as a link.
  await expect(page.getByTestId('breadcrumb-level-1')).toHaveAttribute('aria-current', 'page');
  const root = page.getByTestId('breadcrumb-level-0');
  await expect(root).toHaveRole('link');
  await expect(root).toHaveText('Processus ventes');
  await root.click();
  await expect(stepOf(page, 'VTE-E001')).toContainText('détaillée');
  // A double click on a detailed step opens its map (B-2.3-10); the browser goes back.
  await stepOf(page, 'VTE-E001').dblclick();
  await expect(stepOf(page, 'VTE-E004')).toBeVisible();
  await page.goBack();
  await expect(stepOf(page, 'VTE-E001')).toBeVisible();
}

async function switchToEnglish(page: Page): Promise<void> {
  // The language of the interface in the menu of the user (ADR 083; AR-29); the process stays in its language of origin,
  // translated only by its name, until its language is chosen in the header.
  await chooseInterfaceLanguage(page, 'en');
  await expect(page.getByRole('toolbar').getByRole('radio', { name: 'Decision' })).toBeVisible();
  await expect(crumbShown(page)).toHaveText('Processus ventes');
  await chooseProcessLanguage(page, 'en');
  await expect(crumbShown(page)).toHaveText('Sales process');
  await expect(page.getByTestId('map-translation')).toContainText('Shown in English');
  await page.reload();
  await expect(page.locator('html')).toHaveAttribute('lang', 'en');
  await expect(page.getByRole('toolbar').getByRole('button', { name: 'Link', exact: true })).toBeVisible();
  await chooseInterfaceLanguage(page, 'fr', 'Interface language');
  await expect(page.getByRole('toolbar').getByRole('button', { name: 'Relier', exact: true })).toBeVisible();
}

async function finalState(page: Page, api: Api): Promise<void> {
  const map = await api.map(mapIdOf(page));
  expect([...map.lanes].sort((a, b) => a.sort_order - b.sort_order).map((lane) => lane.name)).toEqual(['CDG Angers', 'SAP']);
  expect(map.steps.map((step) => step.code).sort()).toEqual(['VTE-E001', 'VTE-E002', 'VTE-E003']);
  expect(map.steps.find((step) => step.code === 'VTE-E001')?.sub_map_id).not.toBeNull();
  expect(map.flows.map((flow) => flow.label).sort()).toEqual([null, null, 'oui']);
}

async function backHome(page: Page): Promise<void> {
  await page.getByTestId('header-brand').click();
  await expect(page.getByTestId('project-VTE')).toBeVisible();
  await expect(page.getByTestId('project-VTE')).toContainText(new RegExp('4 étapes.*2 cartes.*Animateur'));
  await expect(page.getByTestId('header-translations')).toHaveCount(0);
  // The main button opens the map of the last project opened (B-1.3-02).
  await page.getByRole('button', { name: 'Décrire un processus' }).click();
  await expect(crumbShown(page)).toHaveText('Processus ventes');
}

test('journey of the facilitator: from the empty project to a detailed map, then the interface in English (B-8.3-37, B-8.3-38)', async ({ page, api }) => {
  await test.step('home page, the user known by Windows', () => homeBeforeAnything(page));
  await test.step('the administrator creates the project VTE', () => createProject(page));
  await test.step('a lane added and renamed', () => drawLanes(page));
  await test.step('double click in a lane: a step made, its label typed at once', () => drawSteps(page));
  await test.step('link by the command bar Ctrl+K, its errors by line', () => linkByCommand(page));
  await test.step('link in two clicks, then undo and redo', () => linkByClicks(page));
  await test.step('drag a step to another lane, undone', () => dragToLane(page, api));
  await test.step('delete by the keyboard then undo: same code, same flows', () => deleteAndUndo(page));
  await test.step('detail a step: detailed map, breadcrumb, way back', () => detailStep(page));
  await test.step('switch to English, kept after a reload', () => switchToEnglish(page));
  await test.step('final state consistent on the host', () => finalState(page, api));
  await test.step('the home page shows the project and opens its map', () => backHome(page));
});
