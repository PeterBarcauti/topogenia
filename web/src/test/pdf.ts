// pdf.ts
// A PDF of the exports read back in the tests: the faces of Inter from the package on the disk, each text decoded by the table of its font, the strings of its information; page by page, each with its own size, its texts, images and links, and the bookmarks of the file.

import { readFileSync } from 'node:fs';
import { createRequire } from 'node:module';
import type { PdfFonts } from '../features/map/export/pdf.ts';

const resolve = createRequire(import.meta.url).resolve;

/** The two faces as the application reads them (one character a byte), from the package installed. */
export function interFonts(): PdfFonts {
  const read = (face: string) => readFileSync(resolve(`inter-font/ttf/Inter-${face}.ttf`)).toString('latin1');
  return { regular: read('Regular'), bold: read('Bold') };
}

/** The mark of a string in UTF-16 (big end first), and the character of a glyph without a character. */
const UTF16_MARK = String.fromCharCode(0xfe, 0xff);
const NOT_A_CHARACTER = String.fromCharCode(0xfffd);

/** One millimetre in points, the unit of the page of a PDF. */
export const POINT = 72 / 25.4;

/** A text drawn: its face (the name of its font), size, grey, place in millimetres from the top left corner, and its characters. */
export interface PdfText {
  readonly face: string;
  readonly size: number;
  readonly grey: number;
  readonly x: number;
  readonly y: number;
  readonly text: string;
}

/** The body of an object of the file. */
const objectOf = (pdf: string, number: string | undefined): string => new RegExp(`(?:^|\\n)${number ?? 'none'} 0 obj\\n([\\s\\S]*?)\\nendobj`).exec(pdf)?.[1] ?? '';

/** The name of a font resource and the characters of its glyphs, read from its table ToUnicode. */
function fontOf(pdf: string, resource: string): { readonly face: string; readonly glyphs: ReadonlyMap<string, string> } {
  const body = objectOf(pdf, new RegExp(`/${resource} (\\d+) 0 R`).exec(pdf)?.[1]);
  const table = objectOf(pdf, /\/ToUnicode (\d+) 0 R/.exec(body)?.[1]);
  const glyphs = new Map([...table.matchAll(/<([0-9a-f]{4})><([0-9a-f]{4,8})>/gi)].map((entry) => {
    const units = (entry[2] ?? '').match(/.{4}/g) ?? [];
    return [(entry[1] ?? '').toLowerCase(), String.fromCharCode(...units.map((unit) => parseInt(unit, 16)))] as const;
  }));
  return { face: /\/BaseFont \/([\w-]+)/.exec(body)?.[1] ?? '', glyphs };
}

/** Each text drawn in a stream of contents, in its order, its glyphs turned back into characters, on a page of the height given. */
function textsIn(pdf: string, contents: string, pageHeight: number): PdfText[] {
  const texts = /BT\n\/(F\d+) ([\d.]+) Tf\n[\d.]+ TL\n([\d.]+) g\n([\d.]+) ([\d.]+) Td\n<([0-9a-f]*)> Tj\nET/g;
  return [...contents.matchAll(texts)].map((match) => {
    const font = fontOf(pdf, match[1] ?? '');
    const glyphs = (match[6] ?? '').match(/.{4}/g) ?? [];
    return {
      face: font.face, size: Number(match[2]), grey: Number(match[3]), x: Number(match[4]) / POINT, y: pageHeight - Number(match[5]) / POINT,
      text: glyphs.map((glyph) => font.glyphs.get(glyph.toLowerCase()) ?? NOT_A_CHARACTER).join(''),
    };
  });
}

/** Each text drawn, in the order of the page, its glyphs turned back into characters; the pages all of one height. */
export function pdfTexts(pdf: string, pageHeight = 297): PdfText[] {
  return textsIn(pdf, pdf, pageHeight);
}

/** An image drawn on a page: the filter of its object (`DCTDecode` for a JPEG, `FlateDecode` for a PNG), its box in millimetres from the top left corner. */
export interface PdfImage {
  readonly filter: string;
  readonly x: number;
  readonly y: number;
  readonly width: number;
  readonly height: number;
}

/** A page of the file: its size in millimetres read from its own MediaBox, its texts read with its own height, its images, the pages its links go to (from 1). */
export interface PdfPage {
  readonly width: number;
  readonly height: number;
  readonly texts: PdfText[];
  readonly images: PdfImage[];
  readonly links: number[];
}

/** The numbers of the objects of the pages, in their order. */
const pageObjects = (pdf: string): string[] => [...(/\/Type \/Pages\n\/Kids \[([^\]]*)\]/.exec(pdf)?.[1] ?? '').matchAll(/(\d+) 0 R/g)].map((match) => match[1] ?? '');

/** The images of a stream of contents: each placement and the filter of the object its resource names. */
function imagesIn(pdf: string, contents: string, pageHeight: number): PdfImage[] {
  return [...contents.matchAll(/([\d.]+) 0 0 ([\d.]+) ([\d.]+) ([\d.]+) cm\n\/(I\d+) Do/g)].map((match) => {
    const [width, height, x, y] = [1, 2, 3, 4].map((index) => Number(match[index]) / POINT) as [number, number, number, number];
    const object = objectOf(pdf, new RegExp(`/${match[5] ?? 'none'} (\\d+) 0 R`).exec(pdf)?.[1]);
    return { filter: /\/Filter \/(\w+)/.exec(object)?.[1] ?? '', x, y: pageHeight - y - height, width, height };
  });
}

/** Each page of the file in its order: its size, its texts, images and links read on that page alone. */
export function pdfPages(pdf: string): PdfPage[] {
  const pages = pageObjects(pdf);
  return pages.map((number) => {
    const body = objectOf(pdf, number);
    const [, , width = 0, height = 0] = (/\/MediaBox \[([^\]]*)\]/.exec(body)?.[1] ?? '').trim().split(/\s+/).map(Number);
    const contents = objectOf(pdf, /\/Contents (\d+) 0 R/.exec(body)?.[1]);
    const links = [...body.matchAll(/\/Dest \[(\d+) 0 R/g)].map((match) => pages.indexOf(match[1] ?? '') + 1);
    const millimetres = height / POINT;
    return { width: width / POINT, height: millimetres, texts: textsIn(pdf, contents, millimetres), images: imagesIn(pdf, contents, millimetres), links };
  });
}

/** A string of the file as written between its parentheses: UTF-16 after its mark, else one byte a character. */
function decoded(raw: string): string {
  const bytes = raw.replace(/\\(.)/g, '$1');
  if (!bytes.startsWith(UTF16_MARK)) return bytes;
  const units = bytes.slice(2).match(/[\s\S]{2}/g) ?? [];
  return String.fromCharCode(...units.map((pair) => pair.charCodeAt(0) * 256 + pair.charCodeAt(1)));
}

/** A string of the information of the file (`/Title`, `/Creator`), read in its dictionary as written: UTF-16 after its mark, else one byte a character. */
export function pdfInfo(pdf: string, key: string): string {
  const info = objectOf(pdf, /\/Info (\d+) 0 R/.exec(pdf)?.[1]);
  return decoded(new RegExp(`/${key} \\(((?:[^)\\\\]|\\\\.)*)\\)`).exec(info)?.[1] ?? '');
}

/** A bookmark of the file: its title, its level (0 at the top), the page it goes to (from 1). */
export interface PdfBookmark {
  readonly title: string;
  readonly level: number;
  readonly page: number;
}

/** The bookmarks of the file, the tree read depth first: each under its parent, in the order of the parent. */
export function pdfOutline(pdf: string): PdfBookmark[] {
  const pages = pageObjects(pdf);
  const items = [...pdf.matchAll(/(\d+) 0 obj\r?\n<<\r?\n\/Title \(((?:[^)\\]|\\.)*)\)\r?\n\/Parent (\d+) 0 R[\s\S]*?\/Dest \[(\d+) 0 R/g)]
    .map((match) => ({ id: match[1] ?? '', title: decoded(match[2] ?? ''), parent: match[3] ?? '', page: pages.indexOf(match[4] ?? '') + 1 }));
  const known = new Set(items.map((item) => item.id));
  const below = (parent: string, level: number): PdfBookmark[] => items.filter((item) => item.parent === parent)
    .flatMap((item) => [{ title: item.title, level, page: item.page }, ...below(item.id, level + 1)]);
  const root = items.find((item) => !known.has(item.parent))?.parent;
  return root === undefined ? [] : below(root, 0);
}
