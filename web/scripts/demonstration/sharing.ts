// sharing.ts
// The last chapters of the walk of the demonstration: the translations by a workbook and by hand, the interface in English, the exports of the File menu, the package taken to another project, the trash and the members.

import { createRequire } from 'node:module';
import type ExcelType from 'exceljs';
import type { Page } from '@playwright/test';
import { expect, stepOf } from '../../e2e/support/journey.ts';
import { english } from './english.ts';
import { backToStart, code, CODE, keep, rest, shot, shotWithoutToasts, stepMenu } from './walk.ts';

// exceljs is a CommonJS module: loaded as such in this ES module.
const Excel = createRequire(import.meta.url)('exceljs') as typeof ExcelType;

/** The column EN of the sheet of the texts completed, as a translator would; the workbook written aside. */
async function translate(path: string): Promise<{ path: string; rows: number }> {
  const book = new Excel.Workbook();
  await book.xlsx.readFile(path);
  const sheet = book.getWorksheet('Traductions');
  if (sheet === undefined) throw new Error('No sheet « Traductions » in the workbook.');
  expect([sheet.getRow(1).getCell(5).text, sheet.getRow(1).getCell(6).text]).toEqual(['FR', 'EN']);
  let rows = 0;
  sheet.eachRow((row, line) => {
    if (line === 1) return;
    row.getCell(6).value = english(row.getCell(5).text);
    rows++;
  });
  const done = path.replace(/\.xlsx$/, '_EN.xlsx');
  await book.xlsx.writeFile(done);
  return { path: done, rows };
}

/** Step 11: the glossary of the project, the workbook of the texts downloaded, completed in English, brought back. */
export async function translations(page: Page): Promise<void> {
  await page.getByRole('button', { name: 'Traductions', exact: true }).click();
  const dialog = page.getByRole('dialog', { name: 'Traduire le projet' });
  await dialog.getByRole('textbox', { name: 'Glossaire du projet' }).fill('note de frais = expense report\njustificatif = receipt\nvirement = bank transfer');
  await dialog.getByTestId('translations-glossary-save').click();
  await expect(page.getByText('Glossaire enregistré.')).toBeVisible();
  const download = page.waitForEvent('download');
  await dialog.getByTestId('translations-download').click();
  const workbook = await keep(download);
  await expect(dialog.getByTestId('translations-status')).toContainText('textes à traduire');
  await dialog.getByTestId('translations-status').scrollIntoViewIfNeeded();
  await shotWithoutToasts(page, '21-traductions');
  const translated = await translate(workbook);
  await dialog.getByTestId('translations-file').setInputFiles(translated.path);
  await expect(dialog.getByTestId('translations-report')).toContainText(`${String(translated.rows)} traductions enregistrées.`);
  await dialog.getByTestId('translations-report').scrollIntoViewIfNeeded();
  await shotWithoutToasts(page, '22-traductions-rapportees');
  await dialog.getByTestId('translations-done').click();
  await expect(dialog).toHaveCount(0);
}

/** Step 12: the translation of one step seen by « Traduire… » of its menu, then the whole interface in English. */
export async function inEnglish(page: Page): Promise<void> {
  const menu = await stepMenu(page, 4);
  await menu.getByRole('menuitem', { name: 'Traduire…' }).click();
  const dialog = page.getByTestId('manual-translations');
  await expect(dialog.getByRole('textbox', { name: 'Traduction (EN)' })).toHaveValue('Report compliant?');
  await shot(page, '23-traduire-une-etape');
  await dialog.getByTestId('manual-translations-cancel').click();
  await page.getByRole('button', { name: 'EN', exact: true }).click();
  await expect(stepOf(page, code(2))).toContainText('Enter the expense report');
  await backToStart(page);
  await rest(page);
  await shotWithoutToasts(page, '24-carte-en-anglais');
  await page.getByRole('button', { name: 'FR', exact: true }).click();
  await expect(stepOf(page, code(2))).toContainText('Saisir la note de frais');
}

/** An entry of the File menu that downloads a file: the file kept, the notification of the end awaited. */
async function exportFrom(page: Page, entry: string, done: string): Promise<string> {
  await page.getByTestId('header-file').click();
  const download = page.waitForEvent('download');
  await page.getByTestId(`menu-file-file.${entry}`).click();
  const path = await keep(download);
  await expect(page.getByText(done).last()).toBeVisible();
  return path;
}

/** Step 13: the File menu, then the map exported as an image, a PDF A3 and an Excel workbook. */
export async function exports(page: Page): Promise<void> {
  await page.getByTestId('header-file').click();
  await expect(page.getByRole('menu')).toBeVisible();
  await shot(page, '25-menu-fichier');
  await page.keyboard.press('Escape');
  for (const entry of ['png', 'pdf', 'xlsx']) await exportFrom(page, entry, 'Export terminé : le fichier est dans le dossier Téléchargements.');
}

/** Step 14: the project exported as a package, without the people; gives the file. */
export async function exportPackage(page: Page): Promise<string> {
  await page.getByTestId('header-file').click();
  await page.getByTestId('menu-file-file.export').click();
  await expect(page.getByTestId('export-package-without-people')).toBeChecked();
  await shotWithoutToasts(page, '26-export-du-paquet');
  const download = page.waitForEvent('download');
  await page.getByTestId('export-package-confirm').click();
  return keep(download);
}

/** Step 15: the package imported from the home page as a new project under another code, through its preview. */
export async function importPackage(page: Page, path: string): Promise<void> {
  await page.getByTestId('header-brand').click();
  await page.getByTestId('home-import-package').click();
  await page.getByTestId('import-package-file').setInputFiles(path);
  await page.getByTestId('import-package-check').click();
  await expect(page.getByText(`Le code ${CODE} est déjà celui d’un projet : en choisir un autre.`)).toBeVisible();
  await page.getByTestId('import-package-code').fill('ndfb');
  await page.getByTestId('import-package-check').click();
  await expect(page.getByText('Le projet prendra le code NDFB.')).toBeVisible();
  await shotWithoutToasts(page, '27-import-apercu');
  await page.getByTestId('import-package-confirm').click();
  await expect(stepOf(page, 'NDFB-E001')).toBeVisible();
}

/** Step 16: the copy put in the trash from the home page; the home page with its project, its participants and its trash. */
export async function trash(page: Page): Promise<void> {
  await page.getByTestId('header-brand').click();
  await page.getByTestId('project-NDFB').getByRole('button', { name: 'Supprimer…' }).click();
  const dialog = page.getByRole('dialog', { name: 'Supprimer le projet NDFB' });
  await dialog.getByRole('textbox').fill('ndfb');
  await shotWithoutToasts(page, '28-mise-a-la-corbeille');
  await dialog.getByRole('button', { name: 'Mettre à la corbeille' }).click();
  await expect(page.getByTestId('trash')).toBeVisible();
  await rest(page);
  await shotWithoutToasts(page, '29-accueil-et-corbeille');
}

/** Step 17: the members of the project, from its line of the home page. */
export async function members(page: Page): Promise<void> {
  await page.getByTestId(`project-${CODE}`).getByRole('button', { name: 'Membres' }).click();
  await expect(page.getByTestId('members-save')).toBeVisible();
  await rest(page);
  await shot(page, '30-membres');
}
