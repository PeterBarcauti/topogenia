// pdf.test.ts
// The PDF of a map read back: A3 landscape, title, breadcrumb, date, classification, image fitted to the page, footer, creator, texts in Inter written into the file whatever the language; the pages of the value stream (B-2.14-13 to B-2.14-19, B-2.14-31, S-4.1-08, AR-52, spec 3.0 section 15.4).

import { PRODUCT_NAME } from '../../../shared/i18n/i18n.ts';
import { interFonts, pdfInfo, pdfTexts, POINT } from '../../../test/pdf.ts';
import { beyondInter, buildPdf, imagePlacement, PDF_FACES, PDF_MARGIN, STREAM_PAGE, streamPlacement, type PdfHeader, type StreamPdfContent } from './pdf.ts';

/** A white pixel. */
const PIXEL = 'data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==';

const HEADER: PdfHeader = {
  title: 'VTE - Clôture – mensuelle', subtitle: 'Ventes > Clôture', date: '28 octobre 2026 à 14:05', classification: 'Classification : Confidentiel', footer: `Généré par ${PRODUCT_NAME}`,
};

/** The file as jsPDF wrote it, one character a byte. */
async function pdfOf(header: PdfHeader = HEADER, image = { dataUrl: PIXEL, width: 2000, height: 500 }): Promise<string> {
  const blob = await buildPdf(image, header, interFonts());
  expect(blob.type).toBe('application/pdf');
  return new TextDecoder('latin1').decode(await blob.arrayBuffer());
}

describe('the letters Inter lacks (ADR 083)', () => {
  it('are found in Chinese and Japanese, not in the Latin, Greek and Cyrillic scripts', () => {
    expect(['销售流程', '販売プロセス', 'Português', 'Ελληνικά', 'Українська', 'Čeština – Türkçe'].map(beyondInter)).toEqual([true, true, false, false, false, false]);
  });

  it('a line of them is drawn as an image at its place, right aligned on the right; the others stay text', async () => {
    const drawn: string[] = [];
    const draw = (text: string, size: number, bold: boolean, grey: boolean) => {
      drawn.push(`${text}|${String(size)}|${String(bold)}|${String(grey)}`);
      return { dataUrl: PIXEL, width: 30, height: 6, baseline: 5 };
    };
    const header = { ...HEADER, title: 'VTE - 销售流程', classification: '分类：内部' };
    const blob = await buildPdf({ dataUrl: PIXEL, width: 2000, height: 500 }, header, interFonts(), draw);
    const pdf = new TextDecoder('latin1').decode(await blob.arrayBuffer());
    expect(drawn).toEqual(['VTE - 销售流程|16|true|false', '分类：内部|10|true|true']);
    expect(pdfTexts(pdf).map((text) => text.text)).toEqual([HEADER.subtitle, HEADER.date, HEADER.footer]);
    // Three images: the two lines and the map; the classification ends on the right margin.
    expect(pdf.match(/\/I\d+ Do/g)).toHaveLength(3);
    const placements = [...pdf.matchAll(/([\d.]+) 0 0 ([\d.]+) ([\d.]+) ([\d.]+) cm\n\/I\d+ Do/g)].map((match) => Number(match[3]) / POINT);
    expect(placements).toContainEqual(expect.closeTo(PDF_MARGIN, 3));
    expect(placements).toContainEqual(expect.closeTo(420 - PDF_MARGIN - 30, 3));
  });
});

describe('the PDF A3 landscape (B-2.14-13 to B-2.14-18, S-4.1-08)', () => {
  it('an A3 landscape page; the title in Inter bold 16, then the breadcrumb, the date on the right, the classification under it, the footer in 8 pt', async () => {
    const pdf = await pdfOf();
    expect(pdf).toMatch(/\/MediaBox \[0 0 1190\.5\d* 841\.8\d*\]/);
    const [title, subtitle, date, classification, footer] = pdfTexts(pdf);
    expect([title?.text, title?.face, title?.size]).toEqual(['VTE - Clôture – mensuelle', PDF_FACES.bold, 16]);
    expect([subtitle?.text, subtitle?.face, subtitle?.size]).toEqual(['Ventes > Clôture', PDF_FACES.regular, 10]);
    expect([date?.text, date?.face, classification?.text, classification?.face]).toEqual(['28 octobre 2026 à 14:05', PDF_FACES.regular, 'Classification : Confidentiel', PDF_FACES.bold]);
    expect([footer?.text, footer?.face, footer?.size]).toEqual([`Généré par ${PRODUCT_NAME}`, PDF_FACES.regular, 8]);
    expect(title?.x).toBeCloseTo(PDF_MARGIN, 3);
    expect(title?.y).toBeCloseTo(PDF_MARGIN + 4, 3);
    expect(subtitle?.y).toBeCloseTo(PDF_MARGIN + 11, 3);
    expect(footer?.y).toBeCloseTo(297 - PDF_MARGIN + 2, 3);
  });

  it('the date and the classification end on the right margin; the texts in grey', async () => {
    const [, subtitle, date, classification] = pdfTexts(await pdfOf());
    // The widths of the texts in Inter 10, regular and bold, as jsPDF measures them.
    const { jsPDF } = await import('jspdf');
    const measure = new jsPDF({ orientation: 'landscape', unit: 'mm', format: 'a3' });
    const fonts = interFonts();
    const width = (face: 'regular' | 'bold', text: string) => {
      measure.addFileToVFS(`${face}.ttf`, fonts[face]);
      measure.addFont(`${face}.ttf`, face, 'normal');
      measure.setFont(face, 'normal');
      measure.setFontSize(10);
      return measure.getTextWidth(text);
    };
    expect((date?.x ?? 0) + width('regular', HEADER.date)).toBeCloseTo(420 - PDF_MARGIN, 3);
    expect((classification?.x ?? 0) + width('bold', HEADER.classification)).toBeCloseTo(420 - PDF_MARGIN, 3);
    expect(classification?.y).toBeCloseTo(PDF_MARGIN + 11, 3);
    expect([subtitle?.grey, date?.grey]).toEqual([0.314, 0.314]);
  });

  it('the image of the map fills the room between the header and the footer; its title and its creator in the information', async () => {
    const pdf = await pdfOf();
    const [, width = 0, height = 0, x = 0, y = 0] = /([\d.]+) 0 0 ([\d.]+) ([\d.]+) ([\d.]+) cm\n\/I0 Do/.exec(pdf)?.map(Number) ?? [];
    // 2000 × 500 in a room of 396 × 250 mm: the width fills it, the height follows (99 mm), the top under the header (29 mm).
    expect([width / POINT, height / POINT, x / POINT, 297 - (y + height) / POINT]).toEqual([expect.closeTo(396, 3), expect.closeTo(99, 3), expect.closeTo(PDF_MARGIN, 3), expect.closeTo(29, 3)]);
    expect([pdfInfo(pdf, 'Creator'), pdfInfo(pdf, 'Title')]).toEqual([PRODUCT_NAME, 'VTE - Clôture – mensuelle']);
  });
});

describe('the place of the image (B-2.14-17)', () => {
  it('a wide map takes the width, a high one the height, the proportions kept, from the left margin under the header', () => {
    const page = { width: 420, height: 297 };
    expect(imagePlacement(page, { width: 3960, height: 500 })).toEqual({ x: 12, y: 29, width: 396, height: 50 });
    const high = imagePlacement(page, { width: 500, height: 1000 });
    expect([high.x, high.y, high.height, high.width]).toEqual([12, 29, 297 - 29 - 12 - 6, (297 - 29 - 12 - 6) / 2]);
  });
});

describe('the texts in Inter, written into the file (B-2.14-19, AR-52)', () => {
  it('prints the letters of every language configured and the fine typography as written, in the two faces of Inter only', async () => {
    const header: PdfHeader = {
      title: 'KSG - Księgowość i płatności', subtitle: 'Muhasebe › Ödeme ağı « été »', date: '28 października 2026', classification: 'Sınıflandırma : Gizli',
      footer: '“Oui” — c’est fait…',
    };
    const pdf = await pdfOf(header);
    expect(pdfTexts(pdf).map((text) => text.text)).toEqual([header.title, header.subtitle, header.date, header.classification, header.footer]);
    // Every text drawn in Inter; jsPDF writes each face once, with the glyphs used only: the file weighs less than half the two faces.
    expect(new Set(pdfTexts(pdf).map((text) => text.face))).toEqual(new Set(Object.values(PDF_FACES)));
    expect(pdf.match(/\/FontFile2 \d+ 0 R/g)).toHaveLength(2);
    expect(pdf.length).toBeLessThan((interFonts().regular.length + interFonts().bold.length) / 2);
    expect(pdfInfo(pdf, 'Title')).toBe(header.title);
  });
});

/** The pages of the value stream: a few lines of each section, the mention of a long label to cut. */
const STREAM: StreamPdfContent = {
  title: 'Flux de valeur · VTE - Ventes',
  subtitle: '4/5 étapes chiffrées · Base : 7 h/jour · 151,67 h/ETP/mois',
  sections: [
    { title: 'Bilan', lines: [{ text: 'Charge', figures: ['≥ 106,2 h/mois'] }, { text: '0,70 ETP', grey: true, indent: true }] },
    { title: 'Chemin principal', lines: [{ text: 'VTE-E002 Vérifier la demande', figures: ['2 j', '20 min'] }] },
    { title: 'Contrôle', lines: [{ text: 'Durée manquante', figures: ['1'], bold: true }, { text: `VTE-E003 ${'Contrôler '.repeat(20)}`, indent: true }] },
  ],
};

describe('the pages of the value stream (spec 3.0 section 15.4)', () => {
  it('flows the sections down three columns, then onto a new page, a section title never alone at the foot of a column', () => {
    const many: StreamPdfContent = { ...STREAM, sections: [{ title: 'Contrôle', lines: Array.from({ length: 150 }, (_, index) => ({ text: String(index) })) }, { title: 'Fin', lines: [{ text: 'x' }] }] };
    const placed = streamPlacement(many, { width: 420, height: 297 });
    const width = (420 - 2 * PDF_MARGIN - 2 * STREAM_PAGE.gap) / 3;
    expect(placed[0]).toMatchObject({ page: 0, x: PDF_MARGIN, y: STREAM_PAGE.top + STREAM_PAGE.section, heading: true });
    expect(placed[1]?.y).toBeCloseTo(STREAM_PAGE.top + STREAM_PAGE.section + STREAM_PAGE.line, 6);
    expect(new Set(placed.map((line) => line.x))).toEqual(new Set([PDF_MARGIN, PDF_MARGIN + width + STREAM_PAGE.gap, PDF_MARGIN + 2 * (width + STREAM_PAGE.gap)]));
    expect(Math.max(...placed.map((line) => line.page))).toBe(1);
    expect(placed.every((line) => line.y <= 297 - PDF_MARGIN - 6)).toBe(true);
    // The title « Fin » goes with its line.
    const end = placed.findIndex((line) => line.line.text === 'Fin');
    expect([placed[end]?.page, placed[end]?.x]).toEqual([placed[end + 1]?.page, placed[end + 1]?.x]);
    // 46 lines leave room for a title, not for its line: the title goes to the top of the next column with it.
    const tight = streamPlacement({ ...STREAM, sections: [{ title: 'A', lines: Array.from({ length: 46 }, (_, index) => ({ text: String(index) })) }, { title: 'Fin', lines: [{ text: 'x' }] }] },
      { width: 420, height: 297 });
    const title = tight.find((line) => line.line.text === 'Fin');
    expect([title?.x, title?.y]).toEqual([tight.at(-1)?.x, STREAM_PAGE.top + STREAM_PAGE.section]);
    expect(title?.x).toBeGreaterThan(PDF_MARGIN);
  });

  it('adds a page after the map: its title, the line under it, the sections, the figures on the right of their column, a long text cut, the footer', async () => {
    const blob = await buildPdf({ dataUrl: PIXEL, width: 2000, height: 500 }, HEADER, interFonts(), undefined, STREAM);
    const pdf = new TextDecoder('latin1').decode(await blob.arrayBuffer());
    expect(pdf.match(/\/Type \/Page[^s]/g)).toHaveLength(2);
    const texts = pdfTexts(pdf).slice(5);
    expect(texts.slice(0, 3).map((text) => [text.text, text.face, text.size])).toEqual([
      [STREAM.title, PDF_FACES.bold, 16], [STREAM.subtitle, PDF_FACES.regular, 10], [HEADER.footer, PDF_FACES.regular, 8],
    ]);
    expect(texts.map((text) => text.text)).toEqual(expect.arrayContaining(['Bilan', 'Charge', '≥ 106,2 h/mois', '0,70 ETP', '2 j', '20 min', 'Durée manquante']));
    const cut = texts.find((text) => text.text.startsWith('VTE-E003'));
    expect(cut?.text.endsWith('…')).toBe(true);
    // Set in under its group, in the first column: the sections follow each other down the columns.
    expect(cut?.x).toBeCloseTo(PDF_MARGIN + STREAM_PAGE.indent, 3);
    expect([texts.find((text) => text.text === '0,70 ETP')?.grey, texts.find((text) => text.text === 'Charge')?.grey]).toEqual([0.314, 0]);
  });
});
