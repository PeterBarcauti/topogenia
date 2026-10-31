// work.ts
// The chapters of the walk of the demonstration on the content of the map: two sheets (one validated), the RACI and its matrix, a step detailed, a detail brought back into the flow with its remainder.

import type { Locator, Page } from '@playwright/test';
import { expect, openSheetOf, stepOf } from '../../e2e/support/journey.ts';
import { ribbon } from '../../e2e/support/map-editing.ts';
import { addStep, backToStart, clickBackground, code, linkByCommand, pan, rest, shot, shotWithoutToasts, stepMenu } from './walk.ts';

/** The sheet docked for a step, named after its code. */
const sheetOf = (page: Page, number: number): Locator => page.getByRole('complementary', { name: `Fiche de l’étape ${code(number)}` });

/** An operation of the procedure typed, its nature chosen. */
async function operation(sheet: Locator, rank: number, text: string, nature: string): Promise<void> {
  await sheet.getByRole('textbox', { name: `Opération ${String(rank)}`, exact: true }).fill(text);
  await sheet.getByRole('combobox', { name: `Nature de l’opération ${String(rank)}` }).selectOption({ label: nature });
}

/** The form of a sheet: purpose, tools, operations, frequency, duration. */
async function typeForm(sheet: Locator, purpose: string, tools: string, operations: [string, string][], frequency: string, minutes: number): Promise<void> {
  await sheet.getByRole('textbox', { name: 'Finalité', exact: true }).fill(purpose);
  await sheet.getByRole('textbox', { name: 'Outils', exact: true }).fill(tools);
  for (const [index, [text, nature]] of operations.entries()) {
    if (index > 0) await sheet.getByRole('textbox', { name: `Opération ${String(index)}`, exact: true }).press('Enter');
    await operation(sheet, index + 1, text, nature);
  }
  await sheet.getByRole('combobox', { name: 'Fréquence', exact: true }).selectOption({ label: frequency });
  await sheet.getByRole('spinbutton', { name: 'Durée par exécution' }).fill(String(minutes));
}

/** An output declared in the section Sorties of a sheet. */
async function declareOutput(sheet: Locator, name: string): Promise<void> {
  const outputs = sheet.getByTestId('sheet-outputs');
  await outputs.getByRole('button', { name: 'Ajouter une sortie' }).click();
  await outputs.getByRole('textbox', { name: 'Nom du document' }).fill(name);
  await outputs.getByRole('textbox', { name: 'Nom du document' }).press('Enter');
  await expect(outputs.getByText(name, { exact: true })).toBeVisible();
}

/** Step 6: the sheet of « Saisir la note de frais » typed by the form; it produces the expense report. */
export async function firstSheet(page: Page): Promise<void> {
  await openSheetOf(page, code(2));
  const sheet = sheetOf(page, 2);
  await typeForm(sheet, 'Déclarer les dépenses engagées pour être remboursé.', 'Logiciel de frais, téléphone', [
    ['Photographier chaque justificatif', 'Classer'],
    ['Saisir chaque dépense dans le logiciel', 'Ressaisir'],
    ['Envoyer la note au manager', 'Envoyer'],
  ], 'Mensuelle', 20);
  await declareOutput(sheet, 'Note de frais');
  await expect(sheet.getByTestId('sheet-saving')).toHaveText('Enregistré');
  await sheet.getByRole('textbox', { name: 'Finalité', exact: true }).scrollIntoViewIfNeeded();
  await shot(page, '09-fiche-formulaire');
  await sheet.getByTestId('sheet-outputs').scrollIntoViewIfNeeded();
  await shot(page, '10-fiche-sorties');
  await page.keyboard.press('Escape');
}

/** A field of the use of an input typed then left: the use is sent, the next field typed once the host has answered (B-4-75). */
async function typeUse(page: Page, field: Locator, value: string): Promise<void> {
  await field.fill(value);
  const sent = page.waitForResponse((response) => response.request().method() === 'PUT' && response.url().includes('/uses/'));
  await field.blur();
  await sent;
  await expect(field).toBeEnabled();
  await expect(field).toHaveValue(value);
}

/** Step 7: the sheet of « Valider la note » uses the expense report, says what is wrong with it, and is validated. */
export async function secondSheet(page: Page): Promise<void> {
  await openSheetOf(page, code(3));
  const sheet = sheetOf(page, 3);
  const inputs = sheet.getByTestId('sheet-inputs');
  await inputs.getByRole('checkbox', { name: 'Utiliser « Note de frais »' }).click();
  await typeUse(page, inputs.getByRole('spinbutton', { name: 'Part de « Note de frais » utilisable telle quelle, en %' }), '70');
  await typeUse(page, inputs.getByRole('textbox', { name: 'Irritant sur « Note de frais »' }), 'Justificatifs illisibles ou manquants');
  await typeUse(page, inputs.getByRole('spinbutton', { name: 'Temps perdu à cause de « Note de frais », en heures par mois' }), '4');
  await typeForm(sheet, 'Vérifier que chaque dépense est justifiée et conforme à la politique de frais.', 'Logiciel de frais', [
    ['Contrôler les montants et les justificatifs', 'Contrôler'],
    ['Valider la note dans le logiciel', 'Envoyer'],
  ], 'Hebdomadaire', 15);
  await expect(sheet.getByTestId('sheet-saving')).toHaveText('Enregistré');
  await sheet.getByRole('button', { name: 'Je valide ma fiche' }).click();
  await expect(sheet.getByTestId('sheet-state')).toHaveText('validée');
  await inputs.scrollIntoViewIfNeeded();
  await rest(page);
  await shot(page, '11-fiche-validee');
  await page.keyboard.press('Escape');
  await expect(page.getByRole('img', { name: '1 document : Note de frais' })).toBeVisible();
}

/** A letter of the RACI panel of a step, by its role and its lane: the step clicked, the letter, then Escape. */
async function giveRoles(page: Page, number: number, roles: string[], capture?: string): Promise<void> {
  await stepOf(page, code(number)).click();
  const panel = page.getByTestId('raci-panel');
  for (const role of roles) await panel.getByRole('button', { name: role, exact: true }).click();
  if (capture !== undefined) await shotWithoutToasts(page, capture);
  await page.keyboard.press('Escape');
  await expect(panel).toHaveCount(0);
}

/** Step 8: the RACI layer, roles given in two clicks, the matrix of the map; the layer put off again. */
export async function raci(page: Page): Promise<void> {
  await ribbon(page).getByRole('button', { name: 'RACI', exact: true }).click();
  await giveRoles(page, 2, ['Approuve : Manager', 'Informé : Comptabilité'], '12-raci-panneau');
  await giveRoles(page, 3, ['Consulté : Comptabilité', 'Informé : Salarié']);
  await pan(page, -600);
  await giveRoles(page, 6, ['Approuve : Comptabilité', 'Informé : Salarié']);
  await backToStart(page);
  await clickBackground(page);
  await rest(page);
  await shotWithoutToasts(page, '13-raci-calque');
  await ribbon(page).getByRole('button', { name: 'Matrice', exact: true }).click();
  await expect(page.getByTestId('raci-matrix')).toBeVisible();
  await shot(page, '14-raci-matrice');
  await page.keyboard.press('Escape');
  await ribbon(page).getByRole('button', { name: 'RACI', exact: true }).click();
  await expect(page.locator('.raci-badge')).toHaveCount(0);
}

/** The code of the first lane of the map shown (a detailed map has lanes of its own). */
async function firstLaneCode(page: Page): Promise<string> {
  const testId = await page.locator('[data-testid^="lane-body-"]').first().getAttribute('data-testid');
  return (testId ?? '').replace('lane-body-', '');
}

/** The captures of a detail: the menu of the step, then its detailed map. */
interface DetailShots { readonly menu: string; readonly map: string }

/** The map of a step detailed from its menu, its steps drawn and linked; back to the main map by the breadcrumb. */
async function detail(page: Page, number: number, labels: string[], first: number, captures?: DetailShots): Promise<void> {
  const menu = await stepMenu(page, number);
  if (captures !== undefined) await shot(page, captures.menu);
  const label = await stepOf(page, code(number)).getByTestId('step-label').innerText();
  await menu.getByRole('menuitem', { name: 'Détailler cette étape' }).click();
  // The detailed map is drawn once its name ends the breadcrumb: a map of one lane, that of the step.
  await expect(page.getByTestId('breadcrumb').locator('[aria-current="page"]')).toHaveText(label);
  await expect(page.locator('[data-testid^="lane-body-"]')).toHaveCount(1);
  const laneCode = await firstLaneCode(page);
  for (const [column, label] of labels.entries()) await addStep(page, laneCode, column, label);
  await linkByCommand(page, labels.slice(1).map((_, index) => `E${String(first + index)} > E${String(first + index + 1)}`));
  if (captures !== undefined) await shotWithoutToasts(page, captures.map);
  await page.getByTestId('breadcrumb').getByRole('link', { name: 'Notes de frais' }).click();
  await expect(stepOf(page, code(number))).toContainText('détaillée');
}

/** Step 9: « Saisir la note de frais » detailed into four steps of its own map. */
export async function detailStep(page: Page): Promise<void> {
  await detail(page, 2, ['Photographier les justificatifs', 'Saisir les lignes de dépense', 'Joindre les photos aux lignes', 'Envoyer la note au manager'], 11, {
    menu: '15-menu-de-l-etape', map: '16-carte-detaillee',
  });
  await rest(page);
  await shotWithoutToasts(page, '17-etape-detaillee');
}

/** Step 10: « Préparer le virement » detailed into two steps, then brought back into the flow; its output goes to one of them. */
export async function dissolve(page: Page): Promise<void> {
  await pan(page, -600);
  await openSheetOf(page, code(6));
  await declareOutput(sheetOf(page, 6), 'Fichier de virement');
  await page.keyboard.press('Escape');
  await detail(page, 6, ['Générer le fichier de virement', 'Transmettre le fichier à la banque'], 15);
  await pan(page, -600);
  const menu = await stepMenu(page, 6);
  await menu.getByRole('menuitem', { name: 'Dissocier le sous-processus' }).click();
  const frame = page.getByTestId('dissolution-frame');
  await expect(frame.getByTestId('dissolution-label')).toHaveText(`Reliquat de ${code(6)} · 1`);
  await rest(page);
  await shotWithoutToasts(page, '18-remontee-cadre');
  await frame.getByTestId('dissolution-label').click();
  const panel = page.getByRole('complementary', { name: new RegExp(`^Reliquat de ${code(6)}`) });
  await expect(panel).toContainText('Fichier de virement');
  await shot(page, '19-remontee-reliquat');
  await panel.getByRole('combobox', { name: 'Affecter « Fichier de virement »' }).selectOption({ label: `${code(15)} · Générer le fichier de virement` });
  await expect(frame).toHaveCount(0);
  await rest(page);
  await shotWithoutToasts(page, '20-remontee-faite');
  await backToStart(page);
}
