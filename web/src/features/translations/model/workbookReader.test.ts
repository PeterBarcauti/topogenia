// workbookReader.test.ts
// The workbook brought back: sorted and completed as by Copilot, every form of cell, the refusals, the keys it did not write, the report by reason (B-5-16 to B-5-21, B-5-23).

import ExcelJS from 'exceljs';
import { buildWorkbook, CONTROL_SHEET } from './workbook.ts';
import { cellText, parseKey, readWorkbook, WorkbookError } from './workbookReader.ts';
import { linesByReason } from './report.ts';
import { bytesOf, FLOW, french, opened, PROJECT, STEP, textsOf } from '../../../test/translations.ts';

async function downloaded(languages?: readonly string[], scope?: string): Promise<ExcelJS.Workbook> {
  return opened(await buildWorkbook(textsOf(languages, scope), null, french));
}

const texts = (book: ExcelJS.Workbook) => book.getWorksheet('Traductions') as ExcelJS.Worksheet;
const control = (book: ExcelJS.Workbook) => book.getWorksheet(CONTROL_SHEET) as ExcelJS.Worksheet;

describe('the rows read back by their hidden key (B-5-20, B-5-21)', () => {
  it('completed and sorted as by Copilot: each translation goes back to its object with its line in the file', async () => {
    const book = await downloaded();
    const sheet = texts(book);
    sheet.getRow(2).getCell(6).value = 'Sales';
    sheet.getRow(3).getCell(6).value = { richText: [{ text: 'Enter ' }, { text: 'the order' }] };
    sheet.getRow(4).getCell(5).value = 'oui';
    const values = [2, 3, 4].map((line) => sheet.getRow(line).values);
    [4, 3, 2].forEach((line, index) => {
      sheet.getRow(line).values = values[index] ?? [];
    });
    const read = await readWorkbook(await bytesOf(book), PROJECT);
    expect(read.scope).toBe('missing');
    expect(read.rows).toEqual([
      { line: 2, type: 'flow', id: FLOW, original: null, translations: { fr: 'oui', en: 'yes' } },
      { line: 3, type: 'step', id: STEP, original: null, translations: { fr: 'Saisir la commande', en: 'Enter the order' } },
      { line: 4, type: 'project', id: PROJECT, original: null, translations: { fr: 'Ventes', en: 'Sales' } },
    ]);
    expect(read.unknownKeys).toEqual([]);
  });

  it('a formula gives its result, a number its digits; an empty or blank cell is not sent; the languages follow the control', async () => {
    const book = await downloaded(['fr', 'en', 'es']);
    const sheet = texts(book);
    sheet.getRow(2).getCell(6).value = { formula: 'UPPER("sales")', result: 'SALES' };
    sheet.getRow(2).getCell(7).value = '   ';
    sheet.getRow(3).getCell(7).value = 42;
    const read = await readWorkbook(await bytesOf(book), PROJECT);
    expect(read.rows.map((row) => row.translations)).toEqual([{ fr: 'Ventes', en: 'SALES' }, { fr: 'Saisir la commande', es: '42' }, { en: 'yes' }]);
  });

  it('a row without key is passed over; a key the workbook did not write is given back with its line', async () => {
    const book = await downloaded();
    const sheet = texts(book);
    sheet.addRow([null, 'Étape', 'VTE-E009', '', 'Ligne ajoutée', 'Added row']);
    sheet.getRow(3).getCell(1).value = 'step:not-an-identifier';
    sheet.getRow(4).getCell(1).value = `sheet:${FLOW}`;
    const read = await readWorkbook(await bytesOf(book), PROJECT);
    expect(read.rows.map((row) => row.line)).toEqual([2]);
    expect(read.unknownKeys).toEqual([3, 4]);
  });

  it('the scope comes from the sheet of control; one changed by hand is read as « missing »', async () => {
    expect((await readWorkbook(await bytesOf(await downloaded(['fr', 'en'], 'all')), PROJECT)).scope).toBe('all');
    const book = await downloaded(['fr', 'en'], 'auto');
    control(book).getRow(2).getCell(2).value = 'everything';
    expect((await readWorkbook(await bytesOf(book), PROJECT)).scope).toBe('missing');
  });
});

describe('the workbooks refused (B-5-16 to B-5-19)', () => {
  const refusal = async (data: ArrayBuffer, project = PROJECT) => (await readWorkbook(data, project).catch((error: unknown) => error)) as WorkbookError;

  it('another project, a workbook without the sheet of control, of the 1.x, or whose sheet of texts was renamed, a file that is no workbook', async () => {
    expect((await refusal(await bytesOf(await downloaded()), STEP)).code).toBe('FICHIER_AUTRE_PROJET');
    const older = new ExcelJS.Workbook();
    older.addWorksheet('Traductions').addRow(['id', 'Élément', 'Code', 'Contexte', 'FR', 'EN']);
    older.addWorksheet('Ariane', { state: 'veryHidden' }).addRow(['projet', PROJECT]);
    expect((await refusal(await bytesOf(older))).code).toBe('FICHIER_SANS_FEUILLE_PROJET');
    const renamed = await downloaded();
    texts(renamed).name = 'Feuil1';
    expect((await refusal(await bytesOf(renamed))).code).toBe('FICHIER_SANS_FEUILLE_PROJET');
    const unreadable = await refusal(new TextEncoder().encode('pas un classeur').slice().buffer);
    expect([unreadable instanceof WorkbookError, unreadable.code]).toEqual([true, 'FICHIER_ILLISIBLE']);
  });

  it('a sheet of control without its project or its languages is no sheet of control', async () => {
    for (const row of [1, 3]) {
      const book = await downloaded();
      control(book).getRow(row).getCell(2).value = null;
      expect((await refusal(await bytesOf(book))).code).toBe('FICHIER_SANS_FEUILLE_PROJET');
    }
  });
});

describe('the cells and the keys', () => {
  it('reads text, rich text, formula, link, number, boolean; a date or an error is no text', () => {
    expect([cellText(' a '), cellText(''), cellText(null), cellText(undefined), cellText(3.5), cellText(false)]).toEqual(['a', null, null, null, '3.5', 'false']);
    expect(cellText({ richText: [{ text: ' A' }, { text: 'b ' }] })).toBe('Ab');
    expect(cellText({ formula: 'A1', result: 7 })).toBe('7');
    expect(cellText({ text: 'lien', hyperlink: 'http://localhost/' })).toBe('lien');
    expect([cellText(new Date(0)), cellText({ error: '#N/A' })]).toEqual([null, null]);
  });

  it('a key is a known type and an identifier of the contract', () => {
    expect(parseKey(`lane:${STEP}`)).toEqual({ type: 'lane', id: STEP });
    expect([parseKey(STEP), parseKey(`:${STEP}`), parseKey(`Step:${STEP}`), parseKey('map:12'), parseKey(`map:${STEP}x`)]).toEqual([null, null, null, null, null]);
  });
});

describe('the report by reason (B-5-23)', () => {
  it('reasons in a fixed order, lines sorted, keys not written and unknown reasons counted with the items not found', () => {
    const ignored = [
      { line: 9, reason: 'SANS_TRADUCTION' }, { line: 4, reason: 'TEXTE_MODIFIE' }, { line: 2, reason: 'SANS_TRADUCTION' }, { line: 7, reason: 'AUTRE' },
    ];
    expect(linesByReason(ignored, [5])).toEqual([
      { reason: 'OBJET_INTROUVABLE', lines: [5, 7] }, { reason: 'TEXTE_MODIFIE', lines: [4] }, { reason: 'SANS_TRADUCTION', lines: [2, 9] },
    ]);
    expect(linesByReason([], [])).toEqual([]);
  });
});
