// pdf.ts
// A PDF of the exports read back in the tests: the faces of Inter from the package on the disk, each text decoded by the table of its font, the strings of its information.

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

/** Each text drawn, in the order of the page, its glyphs turned back into characters. */
export function pdfTexts(pdf: string, pageHeight = 297): PdfText[] {
  const texts = /BT\n\/(F\d+) ([\d.]+) Tf\n[\d.]+ TL\n([\d.]+) g\n([\d.]+) ([\d.]+) Td\n<([0-9a-f]*)> Tj\nET/g;
  return [...pdf.matchAll(texts)].map((match) => {
    const font = fontOf(pdf, match[1] ?? '');
    const glyphs = (match[6] ?? '').match(/.{4}/g) ?? [];
    return {
      face: font.face, size: Number(match[2]), grey: Number(match[3]), x: Number(match[4]) / POINT, y: pageHeight - Number(match[5]) / POINT,
      text: glyphs.map((glyph) => font.glyphs.get(glyph.toLowerCase()) ?? NOT_A_CHARACTER).join(''),
    };
  });
}

/** A string of the information of the file (`/Title`, `/Creator`), read as written: UTF-16 after its mark, else one byte a character. */
export function pdfInfo(pdf: string, key: string): string {
  const raw = new RegExp(`/${key} \\(((?:[^)\\\\]|\\\\.)*)\\)`).exec(pdf)?.[1] ?? '';
  const bytes = raw.replace(/\\(.)/g, '$1');
  if (!bytes.startsWith(UTF16_MARK)) return bytes;
  const units = bytes.slice(2).match(/[\s\S]{2}/g) ?? [];
  return String.fromCharCode(...units.map((pair) => pair.charCodeAt(0) * 256 + pair.charCodeAt(1)));
}
