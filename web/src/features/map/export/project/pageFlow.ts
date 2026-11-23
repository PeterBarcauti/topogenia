// pageFlow.ts
// The blocks of the sheets laid on pages of the size given, pure: lines already measured by an injected measure, several short sheets to a page, a title or a heading never alone at the foot, a sheet that goes on repeating its title « continued », a line cut at the spaces or, for Chinese and Japanese, at a character (ADR 113).

import { PDF_MARGIN } from '../pdf.ts';
import type { Block } from './sheetBlocks.ts';

export type FlowFace = 'regular' | 'bold';

/** The width in millimetres of a text in a face and a size in points: jsPDF for Inter, the canvas for a text Inter cannot write. */
export type Measure = (text: string, face: FlowFace, size: number) => number;

/** The size of the page the lines are laid on, in millimetres: each page is laid out on its own. */
export interface PageSize {
  readonly width: number;
  readonly height: number;
}

/** A sheet to lay out: its title, the title of its following pages, its blocks. */
export interface FlowSheet {
  readonly title: string;
  readonly continued: string;
  readonly blocks: readonly Block[];
}

/** A line placed: its left and its baseline in millimetres from the top of its page, and how to write it. */
export interface FlowLine {
  readonly x: number;
  readonly y: number;
  readonly text: string;
  readonly face: FlowFace;
  readonly size: number;
  readonly grey: boolean;
  readonly role: 'title' | 'heading' | 'body';
}

export interface FlowOptions {
  readonly page: PageSize;
  readonly measure: Measure;
  /** The top of the lines on every page, in millimetres; the margin and the room of a header by default. */
  readonly top?: number;
}

/** A point in millimetres. */
const POINT = 25.4 / 72;

/** The room kept above the bottom margin for the footer, as the other pages of the document. */
const FOOTER_ROOM = 6;

/** The default top of the lines: under the title and the classification of a page. */
export const FLOW_TOP = PDF_MARGIN + 17;

/** The column of the value of a fact, and the indent of the text of an item, in millimetres. */
const LABEL_WIDTH = 38;
const ITEM_INDENT = 8;

/** Letters of Chinese, Japanese and the full-width forms: a line may be cut after each of them. */
const CHARACTER_CUT = /[\u2E80-\u9FFF\uF900-\uFAFF\uFE30-\uFE4F\uFF00-\uFFEF]/u;

/** The signs that never start a line of Chinese or Japanese. */
const CLOSING = new Set(Array.from('，。、．）」』】〕〉》！？：；'));

interface Token {
  readonly text: string;
  readonly gap: boolean;
}

/** The words of a line, a letter of Chinese or Japanese a word of its own; `gap` says a space came before. */
function tokensOf(line: string): Token[] {
  const tokens: Token[] = [];
  let [word, gap] = ['', false];
  const flush = () => {
    if (word !== '') tokens.push({ text: word, gap });
    [word, gap] = ['', false];
  };
  for (const character of line) {
    if (/\s/u.test(character)) {
      flush();
      gap = true;
    } else if (CHARACTER_CUT.test(character)) {
      flush();
      tokens.push({ text: character, gap });
      gap = false;
    } else word += character;
  }
  flush();
  return tokens;
}

/** A word wider than its column, cut at the character where it no longer fits. */
function cutAtCharacters(word: string, fits: (text: string) => boolean): string[] {
  const pieces: string[] = [];
  let piece = '';
  for (const character of word) {
    if (piece !== '' && !fits(piece + character)) {
      pieces.push(piece);
      piece = '';
    }
    piece += character;
  }
  return [...pieces, piece];
}

/** One line of text cut to a width: at its spaces, at a character for a word too wide or a text of Chinese or Japanese. */
function wrapLine(line: string, fits: (text: string) => boolean): string[] {
  const lines: string[] = [];
  let current = '';
  for (const { text, gap } of tokensOf(line)) {
    const joined = current === '' ? text : current + (gap ? ' ' : '') + text;
    if (fits(joined)) {
      current = joined;
      continue;
    }
    let carried = '';
    if (current !== '') {
      const letters = Array.from(current);
      // A closing sign does not start a line: the letter before it comes down with it.
      if (!gap && CLOSING.has(text) && letters.length > 1) carried = letters.pop() ?? '';
      lines.push(letters.join(''));
    }
    const pieces = fits(carried + text) ? [carried + text] : cutAtCharacters(carried + text, fits);
    current = pieces.pop() ?? '';
    lines.push(...pieces);
  }
  return current === '' ? lines : [...lines, current];
}

/** A text cut to a width in a face and a size; its own lines kept, an empty line between two paragraphs kept as an empty line. */
export function wrapText(text: string, width: number, measure: Measure, face: FlowFace, size: number): string[] {
  const fits = (candidate: string) => measure(candidate, face, size) <= width;
  return text.split(/\r?\n/).flatMap((paragraph) => (paragraph.trim() === '' ? [''] : wrapLine(paragraph, fits)));
}

/** A line to place: how it is written, how far it is set in, the room it takes, and whether it must stay on the page of the line after it. */
interface Row {
  readonly text: string;
  readonly face: FlowFace;
  readonly size: number;
  readonly grey: boolean;
  readonly dx: number;
  readonly role: FlowLine['role'];
  readonly before: number;
  readonly advance: number;
  readonly keep: boolean;
  /** A text on the same baseline, at the left: the name of a fact, the number of an item. */
  readonly lead?: { readonly text: string; readonly face: FlowFace };
}

const BODY = { face: 'regular', size: 10, grey: false, dx: 0, role: 'body', before: 0, advance: 5, keep: false } as const satisfies Omit<Row, 'text'>;
const DETAIL = { ...BODY, size: 9, grey: true, advance: 4.5 } as const;
const HEADING = { ...BODY, face: 'bold', size: 11, role: 'heading', before: 3, advance: 6, keep: true } as const;
const TITLE = { ...BODY, face: 'bold', size: 13, role: 'title', before: 6, advance: 7, keep: true } as const;

/** A text wrapped into rows in a style, set in by `dx`, the first row carrying `lead`. */
function rowsOf(text: string, style: Omit<Row, 'text'>, width: number, measure: Measure, lead?: Row['lead']): Row[] {
  return wrapText(text, width - style.dx, measure, style.face, style.size).map((line, index) => ({ ...style, text: line, ...(index === 0 && lead !== undefined ? { lead } : {}) }));
}

/** The rows of a block; a fact puts its name on the left and its value in a column, or under it when the name is wide. */
function blockRows(block: Block, width: number, measure: Measure): Row[] {
  if (block.kind === 'heading') return rowsOf(block.text, HEADING, width, measure);
  if (block.kind === 'note') return rowsOf(block.text, DETAIL, width, measure);
  if (block.kind === 'fact' && block.label !== undefined) {
    const wide = measure(block.label, 'bold', BODY.size) > LABEL_WIDTH - 2;
    if (wide) return [...rowsOf(block.label, { ...BODY, face: 'bold', keep: true }, width, measure), ...rowsOf(block.text, { ...BODY, dx: 4 }, width, measure)];
    return rowsOf(block.text, { ...BODY, dx: LABEL_WIDTH }, width, measure, { text: block.label, face: 'bold' });
  }
  if (block.kind === 'item') {
    const lead = block.number === undefined ? undefined : { text: block.number, face: 'regular' as const };
    const detail = block.detail === undefined ? [] : rowsOf(block.detail, { ...DETAIL, dx: ITEM_INDENT }, width, measure);
    return [...rowsOf(block.text, { ...BODY, dx: ITEM_INDENT }, width, measure, lead), ...detail];
  }
  return rowsOf(block.text, BODY, width, measure);
}

/** The room a row and the rows it must stay with take, down to the first that may start a page. */
function chainHeight(rows: readonly Row[], from: number): number {
  let height = 0;
  for (let index = from; index < rows.length; index++) {
    const row = rows[index] as Row;
    height += row.before + row.advance;
    if (!row.keep) break;
  }
  return height;
}

/**
 * The sheets laid on pages of the size given, in their order: each sheet under its title, the sheets following one another
 * on a page; a title or a heading stays with the line after it; a sheet that does not end on its page goes on at the top of
 * the next under its title « continued ». Every page is laid out on `options.page`, whatever the size of the others.
 */
export function flowSheets(sheets: readonly FlowSheet[], options: FlowOptions): FlowLine[][] {
  const { page, measure } = options;
  const top = options.top ?? FLOW_TOP;
  const width = page.width - 2 * PDF_MARGIN;
  const bottom = page.height - PDF_MARGIN - FOOTER_ROOM;
  const pages: FlowLine[][] = [[]];
  let cursor = top;
  const place = (row: Row) => {
    if (cursor !== top) cursor += row.before;
    const y = cursor + row.size * POINT;
    const lines = pages.at(-1) as FlowLine[];
    if (row.lead !== undefined) lines.push({ x: PDF_MARGIN, y, text: row.lead.text, face: row.lead.face, size: row.size, grey: row.grey, role: row.role });
    if (row.text !== '') lines.push({ x: PDF_MARGIN + row.dx, y, text: row.text, face: row.face, size: row.size, grey: row.grey, role: row.role });
    cursor += row.advance;
  };
  for (const sheet of sheets) {
    const title = rowsOf(sheet.title, TITLE, width, measure);
    const again = rowsOf(sheet.continued, TITLE, width, measure);
    const rows = [...title, ...sheet.blocks.flatMap((block) => blockRows(block, width, measure))];
    rows.forEach((row, index) => {
      if (cursor !== top && cursor + chainHeight(rows, index) > bottom) {
        pages.push([]);
        cursor = top;
        if (index >= title.length) again.forEach(place);
      }
      place(row);
    });
  }
  return pages.filter((lines) => lines.length > 0);
}
