// wallPdf.ts
// The PDF of the poster of a map (Makigami M9): a sheet A4 or A3 landscape for each page of the plan, the image of its window in its printed area, its cut marks and the ticks of its neighbours in a thin black line, under it the title of the map with its classification and the name of the page with its rank; the image SVG of each window, the whole map seen through it; texts in Inter written into the file, those whose letters Inter lacks drawn by the browser (AR-52, ADR 083, S-4.1-08).

import { PRODUCT_NAME } from '../../../shared/i18n/i18n.ts';
import { fitted, GREY, PDF_FACES, registerFonts, writerOf, type LineDrawer, type PdfFonts } from './pdf.ts';
import { num } from './svgDraw.ts';
import { printedArea, WALL_MARGIN, WALL_OVERLAP, type Box, type Size, type WallPage, type WallPlan } from './wallPrint.ts';

/** The texts under each page, in the language shown. */
export interface WallTexts {
  /** « CODE - nom de la carte » (B-2.14-14). */
  readonly title: string;
  /** « Classification : Interne » (S-4.1-08). */
  readonly classification: string;
}

/** The image of the window of a page at the size of its pixels: the data address of a JPEG or of a PNG. */
export type WindowPainter = (page: WallPage) => Promise<string>;

/** The width of the lines of the marks, in millimetres. */
export const MARK_LINE = 0.2;

/** The baseline of the footer in its band, from the top of the band, in millimetres. */
const FOOTER_BASELINE = 4.5;

/**
 * The image SVG of a map seen through a window, at a size in pixels: its root given the size and the view of the window,
 * stretched to it (the window and the pixels have the same proportions but for the rounding); nothing else changes.
 */
export function windowSvg(svg: string, window: Box, pixels: Size): string {
  return svg.replace(/<svg\b[^>]*>/, (root) => root
    .replace(/\swidth="[^"]*"/, ` width="${String(pixels.width)}"`)
    .replace(/\sheight="[^"]*"/, ` height="${String(pixels.height)}"`)
    .replace(/\sviewBox="[^"]*"/, ` viewBox="${num(window.x)} ${num(window.y)} ${num(window.width)} ${num(window.height)}" preserveAspectRatio="none"`));
}

/**
 * The PDF of a plan, built by jsPDF loaded on demand (B-0-04), each image asked of the painter in the order of the pages;
 * its creator is the product (B-2.14-31). The name of the page stands left of the strip its right neighbour covers, its
 * line of title cut so as not to reach it.
 */
export async function buildWallPdf(plan: WallPlan, paint: WindowPainter, texts: WallTexts, fonts: PdfFonts, draw?: LineDrawer): Promise<Blob> {
  const { jsPDF } = await import('jspdf');
  const doc = new jsPDF({ orientation: 'landscape', unit: 'mm', format: plan.paper });
  registerFonts(doc, fonts);
  doc.setProperties({ title: texts.title, creator: PRODUCT_NAME });
  const write = writerOf(doc, draw);
  const room = printedArea(plan.paper);
  const [left, right, baseline] = [WALL_MARGIN + 2, WALL_MARGIN + room.width - WALL_OVERLAP - 2, WALL_MARGIN + room.height + FOOTER_BASELINE];
  const line = `${texts.title} · ${texts.classification}`;
  for (const page of plan.pages) {
    if (page.number > 1) doc.addPage(plan.paper, 'landscape');
    const image = await paint(page);
    doc.addImage(image, image.startsWith('data:image/jpeg') ? 'JPEG' : 'PNG', page.area.x, page.area.y, page.area.width, page.area.height, undefined, 'FAST');
    doc.setDrawColor(0);
    doc.setLineWidth(MARK_LINE);
    for (const mark of page.marks) doc.line(mark.x1, mark.y1, mark.x2, mark.y2);
    const name = `${page.name} (${String(page.number)}/${String(plan.pages.length)})`;
    doc.setFont(PDF_FACES.bold, 'normal');
    doc.setFontSize(9);
    const named = doc.getTextWidth(name);
    doc.setTextColor(0);
    write(name, right, baseline, { face: 'bold', size: 9, grey: false, right: true });
    doc.setFont(PDF_FACES.regular, 'normal');
    doc.setFontSize(8);
    doc.setTextColor(GREY);
    write(fitted(doc, line, right - named - 6 - left), left, baseline, { face: 'regular', size: 8, grey: true });
  }
  return doc.output('blob');
}
