// workbook.test.ts
// The workbook of the translations built in the browser: texts and hidden keys, one column per configured language, layout, instruction and glossary of the project, sheet of control (B-5-07 to B-5-13, B-8.1-45).

import { i18next, type Translate } from '../../../shared/i18n/i18n.ts';
import { opened, PROJECT, STEP, textsOf, french as t } from '../../../test/translations.ts';
import { buildWorkbook, CONTROL_SHEET, instructionOf, instructionText } from './workbook.ts';

describe('the sheet of the texts (B-5-07, B-5-08, B-5-09)', () => {
  it('one row a text: hidden key, item, code, context, the original in the column of its language and the other empty', async () => {
    const sheet = (await opened(await buildWorkbook(textsOf(), null, t))).getWorksheet('Traductions');
    expect(sheet?.getRow(1).values).toEqual([undefined, 'id', 'Élément', 'Code', 'Contexte', 'FR', 'EN']);
    expect(sheet?.getColumn(1).hidden).toBe(true);
    expect(sheet?.getRow(2).values).toEqual([undefined, `project:${PROJECT}`, 'Projet', 'VTE', undefined, 'Ventes']);
    expect(sheet?.getRow(3).values).toEqual([undefined, `step:${STEP}`, 'Étape', 'VTE-E001', 'Ventes / Commercial', 'Saisir la commande']);
    expect([sheet?.getRow(4).getCell(5).value, sheet?.getRow(4).getCell(6).value]).toEqual([null, 'yes']);
    expect(sheet?.getRow(4).getCell(2).value).toBe('Flèche');
    expect(sheet?.rowCount).toBe(4);
  });

  it('one column a configured language (AR-29): a third language is a third column, empty, inside the filter', async () => {
    const sheet = (await opened(await buildWorkbook(textsOf(['fr', 'en', 'es']), null, t))).getWorksheet('Traductions');
    expect(sheet?.getRow(1).values).toEqual([undefined, 'id', 'Élément', 'Code', 'Contexte', 'FR', 'EN', 'ES']);
    expect([3, 4].map((row) => sheet?.getRow(row).getCell(7).value)).toEqual([null, null]);
    expect(sheet?.autoFilter).toBe('B1:G1');
  });

  it('pane frozen under the header, filter from the item to the last language, languages wrapped, header in bold', async () => {
    const sheet = (await opened(await buildWorkbook(textsOf(), null, t))).getWorksheet('Traductions');
    expect(sheet?.views[0]).toMatchObject({ state: 'frozen', ySplit: 1 });
    expect(sheet?.autoFilter).toBe('B1:F1');
    expect([5, 6].map((column) => sheet?.getColumn(column).alignment?.wrapText)).toEqual([true, true]);
    expect(sheet?.getRow(1).font.bold).toBe(true);
  });

  it('in English the headers, the items and the sheets follow the language of the interface', async () => {
    const english: Translate = (key, options) => i18next.getFixedT('en')(key, options);
    const book = await opened(await buildWorkbook(textsOf(), null, english));
    expect(book.worksheets.map((sheet) => sheet.name)).toEqual(['Translations', 'Instruction', CONTROL_SHEET]);
    expect(book.getWorksheet('Translations')?.getRow(1).values).toEqual([undefined, 'id', 'Item', 'Code', 'Context', 'FR', 'EN']);
    expect(book.getWorksheet('Translations')?.getRow(4).getCell(2).value).toBe('Arrow');
  });
});

describe('the sheet of control (B-5-12)', () => {
  it('very hidden, named after the product, it holds the project, the scope, the languages and the sheet of the texts', async () => {
    const book = await opened(await buildWorkbook(textsOf(['fr', 'en'], 'auto'), null, t));
    const control = book.getWorksheet(CONTROL_SHEET);
    expect(control?.state).toBe('veryHidden');
    expect(CONTROL_SHEET).toBe(i18next.t('app.name'));
    const rows: unknown[] = [];
    control?.eachRow((row) => rows.push(row.values));
    expect(rows).toEqual([[undefined, 'project', PROJECT], [undefined, 'scope', 'auto'], [undefined, 'languages', 'fr,en'], [undefined, 'sheet', 'Traductions']]);
  });

  it('the creator of the workbook is the product, from the key of its name (B-2.14-31)', async () => {
    const book = await opened(await buildWorkbook(textsOf(['fr', 'en'], 'auto'), null, t));
    expect(book.creator).toBe(i18next.t('app.name'));
  });
});

describe('the instruction and the glossary of the project (B-5-10, B-5-11, B-8.1-45)', () => {
  const glossary = 'clôture mensuelle = monthly closing\n\nrapprochement = reconciliation';

  it('the instruction names the language columns; the glossary follows it, one term a row, in the sheet « Consigne »', async () => {
    const sheet = (await opened(await buildWorkbook(textsOf(['fr', 'en', 'es']), glossary, t))).getWorksheet('Consigne');
    const cells: string[] = [];
    sheet?.eachRow((row) => cells.push(row.getCell(1).text));
    expect(cells[0]).toBe('Consigne pour Copilot (ou une autre IA)');
    expect(cells[1]).toContain('colonnes de langue (FR, EN, ES)');
    expect(cells).toContain('Respecter le glossaire du projet, donné à la suite de cette consigne.');
    expect(cells.slice(-3)).toEqual(['Glossaire du projet', 'clôture mensuelle = monthly closing', 'rapprochement = reconciliation']);
    expect(sheet?.getRow(1).font.bold).toBe(true);
  });

  it('the instruction asks to fill only the empty cells, translating the text of the same row into the language of the column (B-5-10)', async () => {
    const sheet = (await opened(await buildWorkbook(textsOf(), null, t))).getWorksheet('Consigne');
    const cells: string[] = [];
    sheet?.eachRow((row) => cells.push(row.getCell(1).text));
    expect(cells).toContain('Compléter uniquement ces cellules vides, en traduisant le texte de la même ligne dans la langue de la colonne.');
  });

  it('without a glossary, neither the sentence that cites it nor its title: nothing is written in the code', async () => {
    const sheet = (await opened(await buildWorkbook(textsOf(), '  \n ', t))).getWorksheet('Consigne');
    const cells: string[] = [];
    sheet?.eachRow((row) => cells.push(row.getCell(1).text));
    expect(cells.join('\n')).not.toMatch(/glossaire/i);
    expect(cells.join('\n')).not.toMatch(/rapprochement|monthly closing/);
    expect(instructionOf(['fr', 'en'], null, t).glossary).toEqual([]);
  });

  it('the text copied for Copilot is the instruction, then the title and the terms of the glossary', () => {
    const text = instructionText(instructionOf(['fr', 'en'], glossary, t), t);
    expect(text.split('\n').slice(0, 1)).toEqual(['Dans la feuille « Traductions », chaque ligne a un texte dans l’une des colonnes de langue (FR, EN) et les autres colonnes de langue vides.']);
    expect(text.endsWith('\n\nGlossaire du projet\nclôture mensuelle = monthly closing\nrapprochement = reconciliation')).toBe(true);
    expect(instructionText(instructionOf(['fr', 'en'], null, t), t)).not.toContain('Glossaire');
  });
});
