// wallPdf.test.ts
// The PDF of the poster read back (Makigami M9): a sheet of the paper chosen for each page, landscape; each image asked in the order of the pages and placed in its area; the marks drawn; under each page the title with the classification and the name with its rank, in Inter; the creator; the window of the image SVG.

import { PRODUCT_NAME } from '../../../shared/i18n/i18n.ts';
import { interFonts, pdfInfo, pdfTexts, POINT } from '../../../test/pdf.ts';
import { buildWallPdf, windowSvg, type WallTexts } from './wallPdf.ts';
import { PAPERS, printedArea, WALL_MARGIN, WALL_OVERLAP, wallPlan, type WallPage } from './wallPrint.ts';

/** A white pixel. */
const PIXEL = 'data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==';

const TEXTS: WallTexts = { title: 'VIS - Clôture mensuelle', classification: 'Classification : Confidentiel' };

/** The file of a plan as jsPDF wrote it, one character a byte, and the pages the painter was asked for. */
async function pdfOf(plan = wallPlan({ width: 1000, height: 600 }, 'a4', 2), texts = TEXTS): Promise<{ pdf: string; painted: string[] }> {
  const painted: string[] = [];
  const blob = await buildWallPdf(plan, (page: WallPage) => {
    painted.push(page.name);
    return Promise.resolve(PIXEL);
  }, texts, interFonts());
  expect(blob.type).toBe('application/pdf');
  return { pdf: new TextDecoder('latin1').decode(await blob.arrayBuffer()), painted };
}

/** The size of each sheet, in whole millimetres, read from its box. */
const sheetsOf = (pdf: string): number[][] =>
  [...pdf.matchAll(/\/MediaBox \[0 0 ([\d.]+) ([\d.]+)\]/g)].map((match) => [Math.round(Number(match[1]) / POINT), Math.round(Number(match[2]) / POINT)]);

describe('the PDF of the poster (Makigami M9)', () => {
  it('has a sheet landscape of the paper chosen for each page, each image asked in the order of the pages', async () => {
    const { pdf, painted } = await pdfOf();
    expect(painted).toEqual(['A1', 'A2', 'B1', 'B2']);
    expect(pdf.match(/\/Type \/Page\b/g)).toHaveLength(4);
    expect(sheetsOf(pdf)).toEqual(Array(4).fill([PAPERS.a4.width, PAPERS.a4.height]));
    const a3 = await pdfOf(wallPlan({ width: 3000, height: 600 }, 'a3', 3));
    expect(a3.painted).toEqual(['A1', 'A2', 'A3']);
    expect(sheetsOf(a3.pdf)).toEqual(Array(3).fill([PAPERS.a3.width, PAPERS.a3.height]));
    expect(pdfInfo(a3.pdf, 'Creator')).toBe(PRODUCT_NAME);
    expect(pdfInfo(a3.pdf, 'Title')).toBe('VIS - Clôture mensuelle');
  });

  it('places each image in its area and draws its marks', async () => {
    const plan = wallPlan({ width: 1000, height: 600 }, 'a4', 2);
    const { pdf } = await pdfOf(plan);
    const images = [...pdf.matchAll(/([\d.]+) 0 0 ([\d.]+) ([\d.]+) ([\d.]+) cm\n\/I\d+ Do/g)].map((match) => match.slice(1, 5).map(Number));
    expect(images).toHaveLength(4);
    plan.pages.forEach((page, index) => {
      const [width = 0, height = 0, x = 0] = images[index] ?? [];
      expect(width / POINT).toBeCloseTo(page.area.width, 1);
      expect(height / POINT).toBeCloseTo(page.area.height, 1);
      expect(x / POINT).toBeCloseTo(WALL_MARGIN, 1);
    });
    const lines = pdf.match(/[\d.]+ [\d.]+ m\n[\d.]+ [\d.]+ l\nS/g) ?? [];
    expect(lines).toHaveLength(plan.pages.reduce((sum, page) => sum + page.marks.length, 0));
  });

  it('writes under each page the title with the classification and the name of the page with its rank, in Inter', async () => {
    const { pdf } = await pdfOf();
    const texts = pdfTexts(pdf, PAPERS.a4.height);
    expect(texts.filter((text) => text.face === 'Inter-Bold').map((text) => text.text)).toEqual(['A1 (1/4)', 'A2 (2/4)', 'B1 (3/4)', 'B2 (4/4)']);
    const lines = texts.filter((text) => text.face === 'Inter-Regular');
    expect(lines.map((text) => text.text)).toEqual(Array(4).fill('VIS - Clôture mensuelle · Classification : Confidentiel'));
    const room = printedArea('a4');
    for (const text of texts) expect(text.y).toBeCloseTo(WALL_MARGIN + room.height + 4.5, 1);
    expect(lines[0]?.x).toBeCloseTo(WALL_MARGIN + 2, 1);
  });

  it('cuts a long title so that it never reaches the name of the page, left of the strip of the right neighbour', async () => {
    const { pdf } = await pdfOf(wallPlan({ width: 1000, height: 600 }, 'a4', 2), { title: `VIS - ${'Une carte au nom très long '.repeat(12)}`, classification: 'Classification : Interne' });
    const [title] = pdfTexts(pdf, PAPERS.a4.height).filter((text) => text.face === 'Inter-Regular');
    expect(title?.text.endsWith('…')).toBe(true);
    expect(title?.text.length).toBeLessThan(250);
    const name = pdfTexts(pdf, PAPERS.a4.height).find((text) => text.face === 'Inter-Bold');
    expect(name?.x).toBeLessThan(WALL_MARGIN + printedArea('a4').width - WALL_OVERLAP);
  });
});

describe('the window of the image SVG', () => {
  const svg = [
    '<?xml version="1.0" encoding="UTF-8"?>',
    '<svg xmlns="http://www.w3.org/2000/svg" width="1200" height="500" viewBox="0 0 1200 500" font-family="\'Inter Variable\', Inter">',
    '<title>VIS</title>', '<rect width="1200" height="500" fill="#fff"/>', '<g transform="translate(24 24)"><rect x="1" y="2" width="3" height="4"/></g>', '</svg>', '',
  ].join('\n');

  it('gives the root the size in pixels and the view of the window, stretched to it; the rest unchanged', () => {
    const seen = windowSvg(svg, { x: 590.123, y: 0, width: 610, height: 250.5 }, { width: 2400, height: 986 });
    expect(seen.split('\n')[1]).toBe('<svg xmlns="http://www.w3.org/2000/svg" width="2400" height="986" viewBox="590.12 0 610 250.5" preserveAspectRatio="none" font-family="\'Inter Variable\', Inter">');
    expect(seen.split('\n').filter((_, index) => index !== 1)).toEqual(svg.split('\n').filter((_, index) => index !== 1));
  });
});
