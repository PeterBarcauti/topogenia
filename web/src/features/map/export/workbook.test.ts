// workbook.test.ts
// The workbook of a map: its three sheets (steps, links, RACI matrix) in the language shown, then the file built and read back: frozen bold headers on their colour, filter, empty cells, letters on their colour, names of sheets, creator, classification printed (B-2.14-20 to B-2.14-28, B-2.14-31, S-4.1-08).

import ExcelJS from 'exceljs';
import { i18next, PRODUCT_NAME } from '../../../shared/i18n/i18n.ts';
import { flowOf, laneOf, sampleMap } from '../../../test/maps.ts';
import { french as t } from '../../../test/translations.ts';
import { argbOf, buildMapWorkbook, sheetName, workbookSheets, type WorkbookLook } from './workbook.ts';

beforeEach(async () => {
  await i18next.changeLanguage('fr');
});

/** The sample map with a third lane, roles given, a detailed step, and its steps given out of their reading order. */
function raciMap() {
  const map = sampleMap({ raci_letters: 'ASCI' });
  return {
    ...map,
    lanes: [...map.lanes, laneOf('l3', 2, { name: 'Direction' })],
    steps: [...map.steps.slice(2), ...map.steps.slice(0, 2).map((step) => (step.id === 's2' ? { ...step, sub_map_id: 'sub', sub_map_empty: false } : step))],
    raci: [
      { step_id: 's2', lane_id: 'l3', role: 'A' as const }, { step_id: 's2', lane_id: 'l2', role: 'C' as const },
      // Given out of the order of the lanes: the sheet puts them back in it.
      { step_id: 's3', lane_id: 'l3', role: 'S' as const }, { step_id: 's3', lane_id: 'l2', role: 'S' as const },
    ],
  };
}

describe('the sheet « Étapes » (B-2.14-21, B-2.14-22)', () => {
  it('every step in the reading order, start and end included: code, path, label, type, lane R, one column a letter, detailed, next steps', () => {
    const [steps] = workbookSheets(raciMap(), 'fr', t);
    expect(steps?.name).toBe('Étapes');
    expect(steps?.headers).toEqual(['Code', 'Chemin', 'Étape', 'Type', 'Réalise', 'Approuve', 'Soutient', 'Consulté', 'Informé', 'Détaillée', 'Étapes suivantes']);
    expect(steps?.rows).toEqual([
      ['VTE-E001', 'Ventes › Étape s1', 'Demande reçue', 'Début', 'Commercial', '', '', '', '', '', 'VTE-E002'],
      ['VTE-E002', 'Ventes › Étape s2', 'Vérifier la demande', 'Étape', 'Commercial', 'Direction', '', 'Comptabilité', '', 'Oui', 'VTE-E003'],
      ['VTE-E003', 'Ventes › Étape s3', 'Conforme ?', 'Décision', 'Commercial', '', 'Comptabilité, Direction', '', '', '', 'VTE-E002, VTE-E004'],
      ['VTE-E004', 'Ventes › Étape s4', 'Facture émise', 'Fin', 'Comptabilité', '', '', '', '', '', ''],
    ]);
  });

  it('the letters are those of the map, the names in the language shown', () => {
    const [steps] = workbookSheets(sampleMap(), 'en', t);
    expect(steps?.headers.slice(4, 8)).toEqual(['Réalise', 'Approuve', 'Consulté', 'Informé']);
    expect(steps?.rows[1]?.slice(2, 5)).toEqual(['Check the request', 'Étape', 'Sales']);
  });
});

describe('the sheet « Liens » (B-2.14-23)', () => {
  it('from, its step, to, its step, label in the language shown, in the reading order of the sources then of the targets', () => {
    const map = sampleMap({ flows: [flowOf('f9', 's3', 's4', { label: 'oui', translations: { en: 'yes' } }), ...sampleMap().flows.filter((flow) => flow.id !== 'f3'), flowOf('fx', 's2', 'gone')] });
    const [, flows] = workbookSheets(map, 'fr', t);
    expect([flows?.name, flows?.headers]).toEqual(['Liens', ['De', 'Étape', 'Vers', 'Étape', 'Libellé']]);
    expect(flows?.rows).toEqual([
      ['VTE-E001', 'Demande reçue', 'VTE-E002', 'Vérifier la demande', ''],
      ['VTE-E002', 'Vérifier la demande', 'VTE-E003', 'Conforme ?', ''],
      ['VTE-E003', 'Conforme ?', 'VTE-E002', 'Vérifier la demande', 'non'],
      ['VTE-E003', 'Conforme ?', 'VTE-E004', 'Facture émise', 'oui'],
    ]);
    expect(workbookSheets(map, 'en', t)[1]?.rows[3]?.[4]).toBe('yes');
  });
});

describe('the sheet « Matrice RACI » (B-2.14-24, B-2.14-25)', () => {
  it('the steps with content only, the lanes in columns, the letters centred', () => {
    const [, , matrix] = workbookSheets(raciMap(), 'fr', t);
    expect([matrix?.name, matrix?.headers]).toEqual(['Matrice RACI', ['Code', 'Étape', 'Commercial', 'Comptabilité', 'Direction']]);
    expect(matrix?.rows).toEqual([['VTE-E002', 'Vérifier la demande', 'R', 'C', 'A'], ['VTE-E003', 'Conforme ?', 'R', 'S', 'S']]);
    expect(matrix?.letters).toEqual([2, 3, 4]);
  });
});

describe('the names of the sheets and the colours (B-2.14-25, B-2.14-26, B-2.14-28)', () => {
  it('a name of sheet without the characters Excel refuses, 31 characters at most', () => {
    expect(sheetName('Étapes : mars/avril [v2]?*\\')).toBe('Étapes   mars avril  v2    ');
    expect(sheetName('x'.repeat(40))).toHaveLength(31);
  });

  it('a colour of a token in ARGB: #RGB, #RRGGBB, rgb(); any other value gives no fill', () => {
    expect([argbOf(' #d5f0e3 '), argbOf('#fab'), argbOf('rgb(245, 245, 247)'), argbOf('rgba(0, 10, 255, 1)')]).toEqual(['FFD5F0E3', 'FFFFAABB', 'FFF5F5F7', 'FF000AFF']);
    expect([argbOf(''), argbOf('var(--lane-3-pastel)'), argbOf('rgb(256, 0, 0)'), argbOf('rgba(0, 0, 0, .5)'), argbOf('#12345')]).toEqual([null, null, null, null, null]);
  });
});

const LOOK: WorkbookLook = { header: 'FFF5F5F7', letters: { R: 'FFD5F0E3', A: 'FFD9E9FA', S: 'FFFCE3D4' } };

/** The workbook of the sample map built, then opened again as Excel would. */
async function opened(look: WorkbookLook = LOOK, map = raciMap()) {
  const blob = await buildMapWorkbook(workbookSheets(map, 'fr', t), { title: 'Ventes', classification: 'Classification : Interne & sensible' }, look);
  expect(blob.type).toBe('application/vnd.openxmlformats-officedocument.spreadsheetml.sheet');
  const book = new ExcelJS.Workbook();
  await book.xlsx.load(await blob.arrayBuffer());
  return book;
}

describe('the workbook built (B-2.14-20, B-2.14-26, B-2.14-27, B-2.14-31, S-4.1-08)', () => {
  it('three sheets; headers frozen, bold, on the colour of the tables, with a filter; the classification printed on each', { timeout: 15_000 }, async () => {
    const book = await opened();
    expect(book.worksheets.map((sheet) => sheet.name)).toEqual(['Étapes', 'Liens', 'Matrice RACI']);
    for (const sheet of book.worksheets) {
      expect(sheet.views[0]).toMatchObject({ state: 'frozen', ySplit: 1 });
      expect(sheet.getCell('A1').font.bold).toBe(true);
      expect((sheet.getCell('B1').fill as { fgColor?: { argb?: string } }).fgColor?.argb).toBe('FFF5F5F7');
      expect(sheet.autoFilter).toBeTruthy();
      expect(sheet.headerFooter.oddHeader).toBe('&RClassification : Interne && sensible');
    }
    expect([book.getWorksheet('Étapes')?.autoFilter, book.getWorksheet('Liens')?.autoFilter, book.getWorksheet('Matrice RACI')?.autoFilter]).toEqual(['A1:K1', 'A1:E1', 'A1:E1']);
    expect([book.creator, book.title]).toEqual([PRODUCT_NAME, 'Ventes']);
  });

  it('an empty text is an empty cell; the letters are centred on their colour, in bold, a letter without colour left plain', { timeout: 15_000 }, async () => {
    const book = await opened();
    const steps = book.getWorksheet('Étapes');
    expect([steps?.getCell('F2').value, steps?.getCell('K5').value, steps?.getCell('J3').value]).toEqual([null, null, 'Oui']);
    const matrix = book.getWorksheet('Matrice RACI');
    expect(matrix?.getRow(2).values).toEqual([undefined, 'VTE-E002', 'Vérifier la demande', 'R', 'C', 'A']);
    const colour = (cell: string) => (matrix?.getCell(cell).fill as { fgColor?: { argb?: string } } | undefined)?.fgColor?.argb;
    expect([colour('C2'), colour('E2'), colour('D3'), colour('D2')]).toEqual(['FFD5F0E3', 'FFD9E9FA', 'FFFCE3D4', undefined]);
    // exceljs types every cell with an alignment; a cell never aligned is read back without one.
    const horizontal = (cell: string) => (matrix?.getCell(cell).alignment as { readonly horizontal?: string } | undefined)?.horizontal;
    expect([matrix?.getCell('E2').font.bold, matrix?.getCell('D2').font.bold, horizontal('E2'), horizontal('B2')]).toEqual([true, undefined, 'center', undefined]);
  });

  it('a lane named as a letter keeps the colour of the headers in the matrix', { timeout: 15_000 }, async () => {
    const map = raciMap();
    const book = await opened(LOOK, { ...map, lanes: map.lanes.map((lane) => (lane.id === 'l3' ? { ...lane, name: 'A' } : lane)) });
    const matrix = book.getWorksheet('Matrice RACI');
    expect([matrix?.getCell('E1').value, (matrix?.getCell('E1').fill as { fgColor?: { argb?: string } }).fgColor?.argb]).toEqual(['A', 'FFF5F5F7']);
  });

  it('a header without colour leaves its fill out', { timeout: 15_000 }, async () => {
    const book = await opened({ header: null, letters: {} });
    const fillOf = (sheet: string, cell: string) => book.getWorksheet(sheet)?.getCell(cell).fill as { pattern?: string; fgColor?: unknown } | undefined;
    expect([fillOf('Liens', 'A1')?.pattern, fillOf('Liens', 'A1')?.fgColor, fillOf('Matrice RACI', 'C2')?.fgColor]).toEqual(['none', undefined, undefined]);
  });
});
