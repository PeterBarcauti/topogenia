// pdf.ts
// The PDF of a map: A3 landscape, its title, breadcrumb, date and classification, the image of the map fitted to the page, the footer; with the value stream layer on, its pages of summary, main path and check; texts in Inter written into the file, those whose letters Inter lacks (Chinese, Japanese) drawn as images, so that every language configured prints (annex B 2.14, S-4.1-08, ADR 066, AR-52, ADR 083, spec 3.0 section 15.4).

import type { jsPDF as JsPdf } from 'jspdf';
import { PRODUCT_NAME } from '../../../shared/i18n/i18n.ts';

/** The margin of the page, in millimetres (B-2.14-13). */
export const PDF_MARGIN = 12;

/** The top of the image: under the title and the breadcrumb. */
const IMAGE_TOP = PDF_MARGIN + 17;

/** The room kept above the bottom margin for the footer. */
const FOOTER_ROOM = 6;

/** The texts printed round the image, in the language shown. */
export interface PdfHeader {
  /** « CODE - nom de la carte » (B-2.14-14). */
  readonly title: string;
  /** The breadcrumb joined by « > » (B-2.14-15). */
  readonly subtitle: string;
  /** The long date of the export (B-2.14-16). */
  readonly date: string;
  /** « Classification : Interne », under the date (S-4.1-08). */
  readonly classification: string;
  /** « Généré par » and the product (B-2.14-18). */
  readonly footer: string;
}

/** The image of the map: a PNG as a data address, and its size in pixels of the page before the pixel ratio. */
export interface MapImage {
  readonly dataUrl: string;
  readonly width: number;
  readonly height: number;
}

/**
 * The two faces of Inter written into the PDF (AR-52): TrueType files as binary strings, one character a byte, as jsPDF
 * keeps its files (read by `pdfFonts.ts`). jsPDF writes only the glyphs used, and their text for search and copy.
 */
export interface PdfFonts {
  readonly regular: string;
  readonly bold: string;
}

/** The names of the faces in the document, which a reader of PDF shows (one family a face). */
export const PDF_FACES = { regular: 'Inter-Regular', bold: 'Inter-Bold' } as const;

/** The letters Inter has not: Hebrew, Arabic, the scripts of India and of South-East Asia, Korean, Chinese, Japanese, the full-width forms. */
const BEYOND_INTER = /[\u0590-\u08FF\u0900-\u0DFF\u0E00-\u0FFF\u1100-\u11FF\u2E80-\u9FFF\uA000-\uA4CF\uAC00-\uD7AF\uF900-\uFAFF\uFE30-\uFE4F\uFF00-\uFFEF]/u;

/** A text Inter cannot write: the PDF draws it as an image (ADR 083). */
export const beyondInter = (text: string): boolean => BEYOND_INTER.test(text);

/** A line drawn as an image by the browser, in its fonts: a PNG, its size and the height of its baseline from its top, in millimetres. */
export interface DrawnLine {
  readonly dataUrl: string;
  readonly width: number;
  readonly height: number;
  readonly baseline: number;
}

/** Draws a line of the header in a size in points, bold or not, black or grey; null when the browser cannot draw it (the text stays text). */
export type LineDrawer = (text: string, size: number, bold: boolean, grey: boolean) => DrawnLine | null;

/** A box on the page, in millimetres. */
export interface Placement {
  readonly x: number;
  readonly y: number;
  readonly width: number;
  readonly height: number;
}

/** The image fitted to the room left by the header and the footer, its proportions kept, from the left margin (B-2.14-17). */
export function imagePlacement(page: { readonly width: number; readonly height: number }, image: { readonly width: number; readonly height: number }): Placement {
  const width = page.width - 2 * PDF_MARGIN;
  const height = page.height - IMAGE_TOP - PDF_MARGIN - FOOTER_ROOM;
  const scale = Math.min(width / image.width, height / image.height);
  return { x: PDF_MARGIN, y: IMAGE_TOP, width: image.width * scale, height: image.height * scale };
}

/** A line of the page of the value stream: its text, its figures aligned on the right of its column, bold or grey, set in or not. */
export interface StreamPdfLine {
  readonly text: string;
  readonly figures?: readonly string[];
  readonly bold?: boolean;
  readonly grey?: boolean;
  readonly indent?: boolean;
}

/** The pages of the value stream after the map, with its layer on (spec 3.0 section 15.4): a title, a line under it, the sections. */
export interface StreamPdfContent {
  readonly title: string;
  readonly subtitle: string;
  readonly sections: readonly { readonly title: string; readonly lines: readonly StreamPdfLine[] }[];
}

/** The pages of the value stream: three columns, a line of 5 mm, a section title of 9 mm, a figure of 26 mm, a line set in by 4 mm. */
export const STREAM_PAGE = { columns: 3, gap: 10, line: 5, section: 9, figure: 26, indent: 4, top: IMAGE_TOP } as const;

/** A line placed: its page among those of the value stream (from 0), its left and its baseline, the width of its column. */
export interface PlacedLine {
  readonly page: number;
  readonly x: number;
  readonly y: number;
  readonly width: number;
  readonly heading: boolean;
  readonly line: StreamPdfLine;
}

/** The lines of the sections down the columns, then onto a new page; a section title never alone at the foot of a column. */
export function streamPlacement(content: StreamPdfContent, page: { readonly width: number; readonly height: number }): PlacedLine[] {
  const width = (page.width - 2 * PDF_MARGIN - (STREAM_PAGE.columns - 1) * STREAM_PAGE.gap) / STREAM_PAGE.columns;
  const bottom = page.height - PDF_MARGIN - FOOTER_ROOM;
  const placed: PlacedLine[] = [];
  let [sheet, column, y] = [0, 0, STREAM_PAGE.top];
  const place = (line: StreamPdfLine, heading: boolean) => {
    const step = heading ? STREAM_PAGE.section : STREAM_PAGE.line;
    if (y + step + (heading ? STREAM_PAGE.line : 0) > bottom) {
      [column, y] = [column + 1, STREAM_PAGE.top];
      if (column === STREAM_PAGE.columns) [sheet, column] = [sheet + 1, 0];
    }
    y += step;
    placed.push({ page: sheet, x: PDF_MARGIN + column * (width + STREAM_PAGE.gap), y, width, heading, line });
  };
  for (const section of content.sections) {
    place({ text: section.title, bold: true }, true);
    for (const line of section.lines) place(line, false);
  }
  return placed;
}

type Face = keyof typeof PDF_FACES;

/** A line at its baseline, in a face and a size: its text, or its image when Inter lacks its letters and the drawer draws it. */
type Write = (text: string, x: number, y: number, line: { face: Face; size: number; grey: boolean; right?: boolean }) => void;

function writerOf(doc: JsPdf, draw?: LineDrawer): Write {
  return (text, x, y, line) => {
    doc.setFont(PDF_FACES[line.face], 'normal');
    doc.setFontSize(line.size);
    const drawn = draw !== undefined && beyondInter(text) ? draw(text, line.size, line.face === 'bold', line.grey) : null;
    if (drawn === null) doc.text(text, x, y, line.right === true ? { align: 'right' } : {});
    else doc.addImage(drawn.dataUrl, 'PNG', line.right === true ? x - drawn.width : x, y - drawn.baseline, drawn.width, drawn.height);
  };
}

/** The grey of the texts round the image and of the precisions. */
const GREY = 80;

/** A text cut to a width in the face and size set, an ellipsis at its end; a text drawn as an image is left whole. */
function fitted(doc: JsPdf, text: string, width: number): string {
  if (beyondInter(text) || doc.getTextWidth(text) <= width) return text;
  let cut = text;
  while (cut.length > 1 && doc.getTextWidth(`${cut}…`) > width) cut = cut.slice(0, -1);
  return `${cut.trimEnd()}…`;
}

/** A line of the value stream at its place: its figures on the right of its column, its text cut to the room left. */
function writeStreamLine(doc: JsPdf, write: Write, placed: PlacedLine): void {
  const { x, y, width, heading, line } = placed;
  const face: Face = heading || line.bold === true ? 'bold' : 'regular';
  const size = heading ? 11 : 9;
  const grey = line.grey === true;
  doc.setTextColor(grey ? GREY : 0);
  const figures = line.figures ?? [];
  figures.forEach((figure, rank) => {
    write(figure, x + width - (figures.length - 1 - rank) * STREAM_PAGE.figure, y, { face, size, grey, right: true });
  });
  const left = x + (line.indent === true ? STREAM_PAGE.indent : 0);
  doc.setFont(PDF_FACES[face], 'normal');
  doc.setFontSize(size);
  write(fitted(doc, line.text, x + width - figures.length * STREAM_PAGE.figure - left - 2), left, y, { face, size, grey });
}

/** The pages of the value stream: the title and the line under it, the sections down three columns, the footer on each. */
function writeStreamPages(doc: JsPdf, write: Write, content: StreamPdfContent, footer: string): void {
  const page = { width: doc.internal.pageSize.getWidth(), height: doc.internal.pageSize.getHeight() };
  const placed = streamPlacement(content, page);
  const pages = Math.max(1, ...placed.map((line) => line.page + 1));
  for (let index = 0; index < pages; index++) {
    doc.addPage('a3', 'landscape');
    doc.setTextColor(0);
    write(content.title, PDF_MARGIN, PDF_MARGIN + 4, { face: 'bold', size: 16, grey: false });
    doc.setTextColor(GREY);
    write(content.subtitle, PDF_MARGIN, PDF_MARGIN + 11, { face: 'regular', size: 10, grey: true });
    write(footer, PDF_MARGIN, page.height - PDF_MARGIN + 2, { face: 'regular', size: 8, grey: true });
    for (const line of placed.filter((candidate) => candidate.page === index)) writeStreamLine(doc, write, line);
  }
}

/**
 * The PDF A3 landscape of a map, built by jsPDF loaded on demand (B-0-04), in Inter (AR-52), a line whose letters Inter
 * lacks drawn by the drawer given (ADR 083); its creator is the product (B-2.14-31); the pages of the value stream after
 * it when given (spec 3.0 section 15.4).
 */
export async function buildPdf(image: MapImage, header: PdfHeader, fonts: PdfFonts, draw?: LineDrawer, stream: StreamPdfContent | null = null): Promise<Blob> {
  const { jsPDF } = await import('jspdf');
  const doc = new jsPDF({ orientation: 'landscape', unit: 'mm', format: 'a3' });
  for (const face of ['regular', 'bold'] as const) {
    doc.addFileToVFS(`${PDF_FACES[face]}.ttf`, fonts[face]);
    doc.addFont(`${PDF_FACES[face]}.ttf`, PDF_FACES[face], 'normal');
  }
  const page = { width: doc.internal.pageSize.getWidth(), height: doc.internal.pageSize.getHeight() };
  const right = page.width - PDF_MARGIN;
  const write = writerOf(doc, draw);
  doc.setProperties({ title: header.title, creator: PRODUCT_NAME });
  write(header.title, PDF_MARGIN, PDF_MARGIN + 4, { face: 'bold', size: 16, grey: false });
  doc.setTextColor(GREY);
  write(header.subtitle, PDF_MARGIN, PDF_MARGIN + 11, { face: 'regular', size: 10, grey: true });
  write(header.date, right, PDF_MARGIN + 4, { face: 'regular', size: 10, grey: true, right: true });
  write(header.classification, right, PDF_MARGIN + 11, { face: 'bold', size: 10, grey: true, right: true });
  const place = imagePlacement(page, image);
  doc.addImage(image.dataUrl, 'PNG', place.x, place.y, place.width, place.height, undefined, 'FAST');
  write(header.footer, PDF_MARGIN, page.height - PDF_MARGIN + 2, { face: 'regular', size: 8, grey: true });
  if (stream !== null) writeStreamPages(doc, write, stream, header.footer);
  return doc.output('blob');
}
