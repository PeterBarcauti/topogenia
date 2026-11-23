// pageFlow.test.ts
// The blocks of the sheets laid on pages of the size given: several short sheets to a page, a title never alone at the foot, a long sheet going on under its title continued, the cut of a line at a space or at a character (Chinese, Japanese), every page on its own size.

import { PDF_MARGIN } from '../pdf.ts';
import type { Block } from './sheetBlocks.ts';
import { flowSheets, wrapText, type FlowLine, type FlowSheet, type Measure } from './pageFlow.ts';

/** A measure without a font: a character is a fifth of its size in millimetres, a letter of Chinese, Japanese or full-width twice that. */
const measure: Measure = (text, _face, size) => Array.from(text).reduce((total, character) => total + (/[\u2E80-\u9FFF\uFF00-\uFFEF]/u.test(character) ? 2 : 1) * size * 0.2, 0);
const A4 = { width: 210, height: 297 };

const sheetOf = (title: string, lines: number): FlowSheet => ({
  title, continued: `${title} (suite)`, blocks: [{ kind: 'heading', text: 'Repères' }, ...Array.from({ length: lines }, (_, index): Block => ({ kind: 'text', text: `ligne ${String(index)}` }))],
});
const flat = (pages: readonly (readonly FlowLine[])[]): FlowLine[] => pages.flat();
const right = (line: FlowLine): number => line.x + measure(line.text, line.face, line.size);

describe('the cut of a text into lines', () => {
  it('cuts at the spaces, a line within its width, no word lost', () => {
    const lines = wrapText('un deux trois quatre cinq six sept huit', 20, measure, 'regular', 10);
    expect(lines.length).toBeGreaterThan(1);
    expect(lines.every((line) => measure(line, 'regular', 10) <= 20)).toBe(true);
    expect(lines.join(' ')).toBe('un deux trois quatre cinq six sept huit');
  });

  it('cuts a word wider than its column at a character', () => {
    const lines = wrapText('x'.repeat(100), 30, measure, 'regular', 10);
    expect(lines).toHaveLength(Math.ceil(100 / 15));
    expect(lines.join('')).toBe('x'.repeat(100));
  });

  it('cuts a Chinese or Japanese text without space at a character within its width, never leaving a closing sign to start a line', () => {
    const chinese = '销售流程的每一步都需要负责人确认之后才能继续进行下一个环节，并且记录在系统里面'.repeat(3);
    const lines = wrapText(chinese, 60, measure, 'regular', 10);
    expect(lines.length).toBeGreaterThan(2);
    expect(lines.every((line) => measure(line, 'regular', 10) <= 60)).toBe(true);
    expect(lines.join('')).toBe(chinese);
    // Fifteen letters fill 60 mm: the sign that would start the second line takes a letter down with it.
    const edge = `${'販'.repeat(15)}。${'売'.repeat(10)}`;
    const cut = wrapText(edge, 60, measure, 'regular', 10);
    expect(cut.join('')).toBe(edge);
    expect(cut.every((line) => !line.startsWith('。') && measure(line, 'regular', 10) <= 60)).toBe(true);
    expect(cut.length).toBeGreaterThan(1);
  });

  it('keeps the lines of a text and the blank ones between its paragraphs', () => {
    expect(wrapText('un\n\ndeux', 100, measure, 'regular', 10)).toEqual(['un', '', 'deux']);
  });
});

describe('the sheets on the pages', () => {
  it('several short sheets share a page, in their order, each under its title', () => {
    const pages = flowSheets([sheetOf('VIS-E001 Accueillir', 3), sheetOf('VIS-E002 Vérifier', 3), sheetOf('VIS-E003 Facturer', 3)], { page: A4, measure });
    expect(pages).toHaveLength(1);
    const titles = flat(pages).filter((line) => line.role === 'title');
    expect(titles.map((line) => line.text)).toEqual(['VIS-E001 Accueillir', 'VIS-E002 Vérifier', 'VIS-E003 Facturer']);
    const ys = flat(pages).map((line) => line.y);
    expect(ys).toEqual([...ys].sort((a, b) => a - b));
    expect(titles.every((line) => line.face === 'bold' && line.x === PDF_MARGIN)).toBe(true);
  });

  it('a title is never alone at the foot of a page, nor a heading', () => {
    let moved = 0;
    for (let lines = 1; lines <= 90; lines++) {
      const pages = flowSheets([sheetOf('A', lines), sheetOf('B', 4)], { page: A4, measure });
      for (const page of pages) expect(['title', 'heading']).not.toContain(page.at(-1)?.role);
      if (pages.length > 1 && pages[0]?.every((line) => line.text !== 'B') === true && pages[1]?.[0]?.text === 'B') moved++;
    }
    // The test bites: for some lengths of the first sheet, the second goes whole to the next page.
    expect(moved).toBeGreaterThan(0);
  });

  it('a sheet longer than a page goes on at the top of the next, under its title and « continued »', () => {
    const pages = flowSheets([sheetOf('VIS-E004 Longue', 200)], { page: A4, measure });
    expect(pages.length).toBeGreaterThan(2);
    expect(pages[0]?.[0]).toMatchObject({ text: 'VIS-E004 Longue', role: 'title' });
    for (const page of pages.slice(1)) expect(page[0]).toMatchObject({ text: 'VIS-E004 Longue (suite)', role: 'title' });
    // No line lost, none twice.
    expect(flat(pages).filter((line) => line.text.startsWith('ligne ')).map((line) => line.text)).toEqual(Array.from({ length: 200 }, (_, index) => `ligne ${String(index)}`));
  });

  it('every page is laid out on the size it is given, A4 portrait', () => {
    for (const page of [A4, { width: 148, height: 210 }]) {
      const pages = flowSheets([sheetOf('Titre long '.repeat(6), 60), sheetOf('B', 30)], { page, measure, top: 40 });
      expect(pages.length).toBeGreaterThan(1);
      for (const line of flat(pages)) {
        expect(line.x).toBeGreaterThanOrEqual(PDF_MARGIN);
        expect(right(line)).toBeLessThanOrEqual(page.width - PDF_MARGIN + 1e-9);
        expect(line.y).toBeLessThanOrEqual(page.height - PDF_MARGIN);
      }
      expect(pages[0]?.[0]?.y).toBeGreaterThanOrEqual(40);
      expect(pages.every((lines) => (lines[0]?.y ?? 0) >= 40)).toBe(true);
    }
    // The smaller the page, the more of them.
    const count = (page: { width: number; height: number }) => flowSheets([sheetOf('A', 80)], { page, measure }).length;
    expect(count({ width: 148, height: 210 })).toBeGreaterThan(count(A4));
  });

  it('a Chinese text without space is cut at a character within its width, in a free text and in a fact', () => {
    const chinese = '销售流程的每一步都需要负责人确认'.repeat(12);
    const sheet: FlowSheet = { title: '销售 E001', continued: '销售 E001（续）', blocks: [{ kind: 'text', text: chinese }, { kind: 'fact', label: '目的', text: chinese }] };
    const lines = flat(flowSheets([sheet], { page: A4, measure })).filter((line) => line.role === 'body');
    expect(lines.every((line) => right(line) <= A4.width - PDF_MARGIN + 1e-9)).toBe(true);
    expect(lines.filter((line) => line.text !== '目的').map((line) => line.text).join('')).toBe(chinese + chinese);
  });

  it('a fact puts its name in bold on the left and its value, wrapped, in a column; an item hangs its number and its detail in grey', () => {
    const long = 'mot '.repeat(80).trim();
    const blocks: Block[] = [{ kind: 'fact', label: 'Acteur', text: long }, { kind: 'item', number: '1.', text: long, detail: 'ERP · Extraire' }];
    const lines = flat(flowSheets([{ title: 'T', continued: 'T (suite)', blocks }], { page: A4, measure })).filter((line) => line.role === 'body');
    const label = lines.find((line) => line.text === 'Acteur');
    const number = lines.find((line) => line.text === '1.');
    const values = lines.filter((line) => line.text.startsWith('mot'));
    expect(label).toMatchObject({ face: 'bold', x: PDF_MARGIN });
    expect(values.length).toBeGreaterThan(2);
    expect(new Set(values.map((line) => line.x)).size).toBe(2);
    expect(values[0]?.x).toBeGreaterThan(PDF_MARGIN);
    expect(values[0]?.y).toBe(label?.y);
    expect(number?.y).toBe(values.find((line) => line.y === number?.y)?.y);
    expect(lines.find((line) => line.text === 'ERP · Extraire')).toMatchObject({ grey: true });
    expect(lines.every((line) => right(line) <= A4.width - PDF_MARGIN + 1e-9)).toBe(true);
  });
});
