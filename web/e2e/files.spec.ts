// files.spec.ts
// Journeys of the .pmap files (1.x fichiers.spec.ts): the sample of the documentation opened from the home page, the answer of an AI pasted and corrected, a copy downloaded, valid and reopened alike, a project duplicated, Ctrl+S explained.

import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import type { Locator, Page } from '@playwright/test';
import Ajv2020 from 'ajv/dist/2020.js';
import type { Api } from './support/api.ts';
import { expect, stepOf, test } from './support/journey.ts';

const REPOSITORY = new URL('../../', import.meta.url);
const SAMPLE = fileURLToPath(new URL('samples/cloture-mensuelle.pmap', REPOSITORY));
const MINIMAL = readFileSync(new URL('samples/minimal.pmap', REPOSITORY), 'utf-8');
const validate = new Ajv2020({ allErrors: true, strictTypes: false })
  .compile(JSON.parse(readFileSync(new URL('schemas/pmap-1.2.schema.json', REPOSITORY), 'utf-8')) as object);
/** The name of the application, read where the interface reads it (it is the author of the copies). */
const APP_NAME = (JSON.parse(readFileSync(new URL('../src/shared/i18n/locales/fr.json', import.meta.url), 'utf-8')) as { app: { name: string } }).app.name;

const mapIdOf = (page: Page): string => new URL(page.url()).pathname.replace('/maps/', '');

async function openDialogFromHome(page: Page): Promise<Locator> {
  await page.goto('/');
  await page.getByRole('button', { name: 'Ouvrir un fichier .pmap…' }).click();
  return page.getByRole('dialog', { name: 'Ouvrir un fichier .pmap' });
}

/** The sample of the documentation opened from the home page, its points to check passed, its root map shown. */
async function openSample(page: Page): Promise<void> {
  const dialog = await openDialogFromHome(page);
  await dialog.locator('input[type="file"]').setInputFiles(SAMPLE);
  await dialog.getByRole('button', { name: 'Ouvrir', exact: true }).click();
  await page.getByRole('dialog', { name: 'Projet créé' }).getByRole('button', { name: 'Ouvrir la carte' }).click();
  await expect(stepOf(page, 'VTE-E004')).toBeVisible();
}

/**
 * What a map holds, without the code of its project, read on the host: lanes, steps with their place and type, flows
 * with their label, and the maps of the detailed steps; two maps alike give the same lines.
 */
async function shapeOf(api: Api, mapId: string, prefix: string): Promise<string[]> {
  const map = await api.map(mapId);
  const short = (code: string) => code.replace(`${prefix}-`, '');
  const lanes = new Map(map.lanes.map((lane) => [lane.id, short(lane.code)]));
  const steps = new Map(map.steps.map((step) => [step.id, short(step.code)]));
  const lines = [
    ...map.lanes.map((lane) => `lane ${short(lane.code)} ${lane.name} ${lane.type} ${lane.sort_order}`),
    ...map.steps.map((step) => `step ${short(step.code)} ${step.label} ${step.type} ${lanes.get(step.lane_id) ?? ''} ${step.column} ${step.row}`),
    ...map.flows.map((flow) => `flow ${steps.get(flow.source_step_id) ?? ''}>${steps.get(flow.target_step_id) ?? ''} ${flow.label ?? ''}`),
  ];
  for (const step of map.steps)
    if (step.sub_map_id !== null) lines.push(...(await shapeOf(api, step.sub_map_id, prefix)).map((line) => `${short(step.code)}/${line}`));
  return lines.sort();
}

async function openFileMenu(page: Page): Promise<Locator> {
  await page.getByRole('button', { name: 'Fichier', exact: true }).click();
  return page.getByRole('menu', { name: 'Menu Fichier' });
}

test('the sample of the documentation opens from the home page: points to check, map and detail (B-8.3-26)', async ({ page }) => {
  // A file chosen (B-1.6-03): its code and name fill the dialog.
  const dialog = await openDialogFromHome(page);
  await dialog.locator('input[type="file"]').setInputFiles(SAMPLE);
  await expect(dialog.getByText('cloture-mensuelle.pmap')).toBeVisible();
  await expect(dialog.getByRole('textbox', { name: 'Code court' })).toHaveValue('VTE');
  await expect(dialog.getByRole('textbox', { name: 'Nom du projet' })).toHaveValue('Clôture mensuelle des ventes');
  await dialog.getByRole('button', { name: 'Ouvrir', exact: true }).click();

  const created = page.getByRole('dialog', { name: 'Projet créé' });
  await expect(created.getByRole('status')).toHaveText('Étapes : 11 · Couloirs : 5 · Liens : 10 · Cartes : 2');
  await expect(created.getByText('Le seuil de 5 % est à confirmer avec la direction.')).toBeVisible();
  await created.getByRole('button', { name: 'Ouvrir la carte' }).click();

  await expect(stepOf(page, 'VTE-E004')).toBeVisible();
  await expect(page.locator('.lane-head-name')).toHaveText(['Contrôle de gestion Nord', 'ERP', 'Contrôle de gestion Sud', 'Direction']);
  await expect(stepOf(page, 'VTE-E003')).toHaveClass(/\bis-macro\b/);
  await expect(stepOf(page, 'VTE-E005')).toHaveClass(/\bstep--decision\b/);
  expect((await page.locator('.edge-label-text').allTextContents()).sort()).toEqual(['non', 'oui']);
  // A double click on a detailed step opens its map (B-2.3-10).
  await stepOf(page, 'VTE-E003').dblclick();
  await expect(page.getByTestId('breadcrumb').locator('[aria-current="page"]')).toHaveText('Construire le tableau croisé');
  await expect(stepOf(page, 'VTE-E011')).toContainText('Comparer au mois précédent');
});

test('the answer of an AI pasted: the fault said at its place, corrected, the map laid out by itself (B-8.3-27)', async ({ page, api }) => {
  const dialog = await openDialogFromHome(page);
  const wrong = MINIMAL.replace('"lane": "c2", "label": "Passer', '"lane": "c3", "label": "Passer');
  expect(wrong).not.toBe(MINIMAL);
  const answer = '```json\n' + wrong + '\n```';
  // A pasted text (B-1.6-05), in its Markdown block as an AI writes it.
  const area = dialog.getByRole('textbox', { name: 'coller son contenu' });
  await area.fill(answer);
  await expect(dialog.getByRole('textbox', { name: 'Nom du projet' })).toHaveValue("Demande d'achat");
  // No code in the file: one must be typed.
  const open = dialog.getByRole('button', { name: 'Ouvrir', exact: true });
  await expect(open).toBeDisabled();
  await dialog.getByRole('textbox', { name: 'Code court' }).fill('ach');
  await expect(dialog.getByRole('textbox', { name: 'Code court' })).toHaveValue('ACH');
  await open.click();
  // The refusal of the host: its message, then each fault with its path (B-1.6-13).
  const alert = dialog.getByRole('alert');
  await expect(alert).toContainText("Le fichier n'a pas pu être ouvert : 1 problème(s) à corriger.");
  await expect(alert.getByRole('listitem')).toHaveText(["map.steps[2].lane Le couloir « c3 » n'existe pas dans cette carte."]);
  await expect(alert.locator('code')).toHaveText('map.steps[2].lane');
  expect((await api.get<unknown[]>('/api/projects')).length).toBe(0);

  await area.fill(answer.replace('"c3"', '"c2"'));
  await expect(alert).toHaveCount(0);
  await open.click();
  // Without a point to check, the map opens at once (B-1.6-17); the steps follow the arrows.
  await expect(stepOf(page, 'ACH-E004')).toBeVisible();
  await expect(page.getByRole('dialog')).toHaveCount(0);
  const labels = ['Besoin identifié', 'Saisir la demande', 'Passer la commande', 'Commande envoyée'];
  for (const [index, label] of labels.entries()) await expect(stepOf(page, `ACH-E00${index + 1}`).getByTestId('step-label')).toHaveText(label);
  const boxes = await Promise.all(labels.map(async (_label, index) => stepOf(page, `ACH-E00${index + 1}`).boundingBox()));
  expect(new Set(boxes.map((box) => `${box?.x ?? 0},${box?.y ?? 0}`)).size).toBe(4);
  const steps = (await api.map(mapIdOf(page))).steps.sort((a, b) => a.code.localeCompare(b.code));
  expect(steps.map((step) => step.column)).toEqual([0, 1, 2, 3]);
});

/** The copy downloaded from the File menu: its name and its text. */
async function downloadCopy(page: Page): Promise<{ name: string; text: string }> {
  const menu = await openFileMenu(page);
  const entries = await menu.getByRole('menuitem').allTextContents();
  expect(entries.slice(0, 7)).toEqual([
    'Ouvrir un fichier .pmap…', expect.stringMatching(new RegExp('^Importer un paquet \\.\\w+…$')), 'Télécharger une copie (.pmap)', 'Dupliquer le projet…',
    'Exporter en PDF (A3 paysage)', 'Exporter en image PNG', 'Exporter en Excel',
  ]);
  // Then the package of exchange of the facilitator (ADR 058 § 8, ADR 062 § 5), and the properties of the project last (ADR 069).
  expect(entries.slice(7)).toEqual([
    expect.stringMatching(new RegExp('^Exporter le projet \\(\\.\\w+\\)…$')), expect.stringMatching(new RegExp('^Remplacer par un paquet \\.\\w+…$')),
    'Propriétés du projet…',
  ]);
  const download = page.waitForEvent('download');
  await menu.getByRole('menuitem', { name: 'Télécharger une copie (.pmap)' }).click();
  const file = await download;
  await expect(page.getByText('Copie des cartes téléchargée')).toBeVisible();
  return { name: file.suggestedFilename(), text: readFileSync(await file.path(), 'utf-8') };
}

/** The copy follows the schema of the format and carries the codes of the project. */
function checkCopy(text: string): void {
  const document = JSON.parse(text) as { format: string; generated_by: string; map: { steps: { id: string; raci?: object }[] } };
  expect(validate(document), JSON.stringify(validate.errors)).toBe(true);
  expect(document.format).toBe(APP_NAME.toLowerCase());
  expect(document.generated_by).toBe(APP_NAME);
  expect(document.map.steps.map((step) => step.id)).toEqual(['VTE-E001', 'VTE-E002', 'VTE-E003', 'VTE-E004', 'VTE-E005', 'VTE-E006', 'VTE-E007', 'VTE-E008']);
  expect(document.map.steps[2]?.raci).toEqual({ 'VTE-C03': 'I', 'VTE-C04': 'A' });
  // The maps without the sheets (B-2.15-01): no step carries a block « sheet ».
  expect(document.map.steps.filter((step) => 'sheet' in step)).toEqual([]);
}

/** The copy reopened from the File menu of the map, under another code. */
async function reopen(page: Page, copy: { name: string; text: string }, code: string): Promise<void> {
  const menu = await openFileMenu(page);
  await menu.getByRole('menuitem', { name: 'Ouvrir un fichier .pmap…' }).click();
  const dialog = page.getByRole('dialog', { name: 'Ouvrir un fichier .pmap' });
  await dialog.locator('input[type="file"]').setInputFiles({ name: copy.name, mimeType: 'application/json', buffer: Buffer.from(copy.text) });
  await expect(dialog.getByRole('textbox', { name: 'Code court' })).toHaveValue('VTE');
  await dialog.getByRole('textbox', { name: 'Code court' }).fill(code);
  await dialog.getByRole('button', { name: 'Ouvrir', exact: true }).click();
  await expect(stepOf(page, `${code}-E004`)).toBeVisible();
}

/** The project of the map duplicated from the File menu: the name proposed, the code typed; the copy opens (B-1.6-22). */
async function duplicateAs(page: Page, code: string): Promise<void> {
  const menu = await openFileMenu(page);
  await menu.getByRole('menuitem', { name: 'Dupliquer le projet…' }).click();
  const dialog = page.getByRole('dialog', { name: 'Dupliquer le projet' });
  await expect(dialog.getByRole('textbox', { name: 'Nom du projet' })).toHaveValue('Clôture mensuelle des ventes (copie)');
  await dialog.getByRole('textbox', { name: 'Code court' }).fill(code);
  await dialog.getByRole('button', { name: 'Dupliquer' }).click();
  await expect(stepOf(page, `${code}-E004`)).toBeVisible();
}

test('a copy downloaded follows the schema, reopens alike, then the project is duplicated (B-8.3-28, B-8.3-29, B-8.3-30)', async ({ page, api }) => {
  await openSample(page);
  const original = await shapeOf(api, mapIdOf(page), 'VTE');
  // 4 lanes, 8 steps and 8 flows on the root map, 1 lane, 3 steps and 2 flows on the detailed one.
  expect(original).toHaveLength(26);
  // The sample brings sheets, as drafts: the copy below leaves them out.
  expect((await api.map(mapIdOf(page))).steps.filter((step) => step.sheet.status !== 'empty').length).toBeGreaterThan(0);

  const copy = await downloadCopy(page);
  expect(copy.name).toMatch(/^VTE_Cloture-mensuelle-des-ventes_\d{4}-\d{2}-\d{2}\.pmap$/);
  checkCopy(copy.text);

  // Reopened from the map, under another code: the same map, down to the detailed ones.
  await reopen(page, copy, 'VTB');
  expect(await shapeOf(api, mapIdOf(page), 'VTB')).toEqual(original);
  await expect(stepOf(page, 'VTB-E003')).toHaveClass(/\bis-macro\b/);

  await duplicateAs(page, 'VTC');
  expect(await shapeOf(api, mapIdOf(page), 'VTC')).toEqual(original);
  await page.goto('/');
  await expect(page.locator('.project-list .chip-code')).toHaveText(['VTB', 'VTC', 'VTE']);
});

/** Records whether the browser was kept from its own action of the last Ctrl+S (its window to save the page). */
async function watchSaveKey(page: Page): Promise<void> {
  await page.evaluate(() => {
    window.addEventListener('keydown', (event) => {
      if (event.ctrlKey && event.key.toLowerCase() === 's') document.body.dataset.saveKey = event.defaultPrevented ? 'kept' : 'free';
    }, true);
  });
}

test('Ctrl+S: nothing to save, all is saved already; no window of the browser (B-8.3-31)', async ({ page }) => {
  const dialog = await openDialogFromHome(page);
  await dialog.getByRole('textbox', { name: 'coller son contenu' }).fill(MINIMAL);
  await dialog.getByRole('textbox', { name: 'Code court' }).fill('ACH');
  await dialog.getByRole('button', { name: 'Ouvrir', exact: true }).click();
  await expect(stepOf(page, 'ACH-E001')).toBeVisible();
  await watchSaveKey(page);

  const toasts = page.getByTestId('toasts');
  await page.keyboard.press('Control+s');
  await expect(toasts.getByRole('status')).toHaveText(/Tout est enregistré automatiquement, à chaque modification\./);
  await expect(page.locator('body')).toHaveAttribute('data-save-key', 'kept');
  await toasts.getByRole('button', { name: 'Fermer la notification' }).click();
  await expect(toasts.getByRole('status')).toHaveCount(0);

  // During a typing too (B-2.5-05): the shortcut never saves the web page, and the field keeps its text.
  await stepOf(page, 'ACH-E002').dblclick();
  const field = page.getByTestId('step-label-input');
  await expect(field).toBeFocused();
  await page.evaluate(() => {
    delete document.body.dataset.saveKey;
  });
  await field.press('Control+s');
  await expect(toasts.getByRole('status')).toHaveText(/Tout est enregistré automatiquement/);
  await expect(page.locator('body')).toHaveAttribute('data-save-key', 'kept');
  await expect(field).toBeFocused();
  await expect(field).toHaveValue('Saisir la demande');
});
