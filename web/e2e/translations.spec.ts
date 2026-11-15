// translations.spec.ts
// Journey of the translations by an Excel workbook (annex B 8.3, spec 11): the texts downloaded from the button Translations of the header, the column EN completed as an AI would, the workbook brought back, the map read again and shown in English.

import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';
import type ExcelType from 'exceljs';
import type { Page } from '@playwright/test';
import type { Api } from './support/api.ts';
import { chooseProcessLanguage, expect, stepOf, test } from './support/journey.ts';

// exceljs is a CommonJS module: loaded as such in this ES module.
const Excel = createRequire(import.meta.url)('exceljs') as typeof ExcelType;

/** The example of the documentation: the project VIS of the 1.x example, a lane and a step already translated by the file. */
const SAMPLE = fileURLToPath(new URL('../../samples/cloture-mensuelle.pmap', import.meta.url));

/** A translation « as Copilot would »: a few labels known, else the text prefixed with EN (1.x `TRADUCTIONS`). */
const KNOWN: Readonly<Record<string, string>> = {
  'Extraire les visites du mois': 'Extract the monthly visits',
  'Valider le reporting': 'Approve the reporting',
};

/** The sample opened from the home page as a new project (B-1.6); gives its root map. */
async function openSample(page: Page): Promise<string> {
  await page.goto('/');
  await page.getByRole('button', { name: 'Ouvrir un fichier…' }).click();
  const dialog = page.getByRole('dialog', { name: 'Ouvrir un fichier' });
  await dialog.getByTestId('open-pmap-file').setInputFiles(SAMPLE);
  await dialog.getByRole('button', { name: 'Ouvrir', exact: true }).click();
  await page.getByRole('dialog', { name: 'Projet créé' }).getByRole('button', { name: 'Ouvrir la carte' }).click();
  await expect(stepOf(page, 'VIS-E002')).toContainText('Extraire les visites du mois');
  return new URL(page.url()).pathname.split('/').pop() ?? '';
}

/** The code of a lane of the map by its name in French. */
async function laneCode(api: Api, mapId: string, name: string): Promise<string> {
  return (await api.map(mapId)).lanes.find((lane) => lane.name === name)?.code ?? '';
}

/** Completes the column EN of the sheet of the texts, writes the workbook aside; gives the codes of the rows given. */
async function translate(path: string, target: string): Promise<string[]> {
  const book = new Excel.Workbook();
  await book.xlsx.readFile(path);
  const sheet = book.getWorksheet('Traductions');
  if (sheet === undefined) throw new Error('No sheet « Traductions » in the workbook.');
  expect([sheet.getRow(1).getCell(3).text, sheet.getRow(1).getCell(5).text, sheet.getRow(1).getCell(6).text]).toEqual(['Code', 'FR', 'EN']);
  const codes: string[] = [];
  sheet.eachRow((row, line) => {
    if (line === 1) return;
    codes.push(row.getCell(3).text);
    const french = row.getCell(5).text;
    row.getCell(6).value = KNOWN[french] ?? `EN ${french}`;
  });
  await book.xlsx.writeFile(target);
  return codes;
}

test('the texts downloaded, the column EN completed, the workbook brought back: the map is read again and shows in English, then English becomes the language of origin (B-8.3-61, B-8.3-62, B-5-24, ADR 100)', async ({ page, api }) => {
  const mapId = await openSample(page);
  const ticketing = await laneCode(api, mapId, 'Billetterie Nord');
  const erp = await laneCode(api, mapId, 'ERP');

  await page.getByRole('button', { name: 'Traductions', exact: true }).click();
  const dialog = page.getByRole('dialog', { name: 'Traduire le projet' });
  const download = page.waitForEvent('download');
  await dialog.getByRole('button', { name: 'Télécharger le classeur' }).click();
  const file = await download;
  expect(file.suggestedFilename()).toMatch(/^VIS_traductions_\d{4}-\d{2}-\d{2}\.xlsx$/);
  await expect(dialog.getByTestId('translations-status')).toContainText('textes à traduire');

  const codes = await translate(await file.path(), test.info().outputPath('translated.xlsx'));
  // « Sans traduction »: the lane and the step the file translated already are not given.
  expect(codes).toContain('VIS-E002');
  expect(codes).not.toContain(ticketing);
  expect(codes).not.toContain('VIS-E003');

  // The workbook brought back: every row saved, then the map read again (B-5-22, B-5-24).
  const reread = page.waitForResponse((response) => response.request().method() === 'GET' && new URL(response.url()).pathname === `/api/maps/${mapId}`);
  await dialog.getByTestId('translations-file').setInputFiles(test.info().outputPath('translated.xlsx'));
  await expect(dialog.getByTestId('translations-report')).toContainText(`${String(codes.length)} traductions enregistrées.`);
  await reread;
  await dialog.getByText('Fermer', { exact: true }).click();
  await expect(dialog).toHaveCount(0);

  // The process shown in English from the list of the header, the interface staying in French (ADR 083).
  await chooseProcessLanguage(page, 'en');
  await expect(page.getByTestId('map-translation')).toContainText('Affiché en English');
  await expect(stepOf(page, 'VIS-E002')).toContainText('Extract the monthly visits');
  await expect(stepOf(page, 'VIS-E004')).toContainText('EN Compléter le fichier du mois');
  await expect(page.getByTestId(`lane-head-${erp}`)).toContainText('EN ERP');
  // What the file had translated stays: the step, the lane, the labels of the flows (B-5-25 to B-5-27).
  await expect(stepOf(page, 'VIS-E003')).toContainText('Build the pivot table');
  await expect(page.getByTestId(`lane-head-${ticketing}`)).toContainText('Ticketing North');
  expect((await page.locator('.edge-label-text').allTextContents()).sort()).toEqual(['no', 'yes']);

  // Wholly translated, English may become the language of origin (ADR 100): confirmed, the project is written in
  // English, shown in it without the banner of a translation, and its former French is now a translation, locked.
  await page.getByRole('button', { name: 'Traductions', exact: true }).click();
  await page.getByTestId('translations-origin-make-en').click();
  const question = page.getByRole('dialog', { name: 'Changer la langue d’origine' });
  await expect(question).toContainText('Ctrl+Z ne défait pas ce changement');
  await question.getByRole('button', { name: 'Changer' }).click();
  await expect(page.getByRole('dialog')).toHaveCount(0);
  await expect(page.getByTestId('map-translation')).toHaveCount(0);
  await expect(stepOf(page, 'VIS-E002')).toContainText('Extract the monthly visits');
  expect((await api.get<{ source_language: string }>(`/api/projects/${(await api.get<{ map: { project_id: string } }>(`/api/maps/${mapId}`)).map.project_id}`)).source_language).toBe('en');
  await chooseProcessLanguage(page, 'fr');
  await expect(page.getByTestId('map-translation')).toContainText('Affiché en Français');
  await expect(stepOf(page, 'VIS-E004')).toContainText('Compléter le fichier du mois');
});
