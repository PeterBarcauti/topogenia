// tablePages.ts
// The tables of the PDF of the project laid on pages, pure: the RACI matrix of a map (a column per lane, a row per step concerned) and the action plan (the eleven cells of its entries), the header repeated on every page, a long cell wrapped, a row never cut between two pages (ADR 113).

import type { Schema } from '../../../../api/client.ts';
import { PDF_MARGIN } from '../pdf.ts';
import { raciMatrix } from '../../model/raci.ts';
import type { PlanEntry } from '../../plan/actionPlan.ts';
import { FLOW_TOP, wrapText, type Measure, type PageSize } from './pageFlow.ts';

type MapView = Schema<'MapView'>;
type LaneView = Schema<'LaneView'>;
type StepView = Schema<'StepView'>;

export type CellAlign = 'left' | 'center' | 'right';

/** A table to lay out: its header, its rows of texts and numbers, the relative width and the alignment of each column. */
export interface TableData {
  readonly headers: readonly string[];
  readonly rows: readonly (readonly (string | number)[])[];
  readonly weights: readonly number[];
  readonly align: readonly CellAlign[];
}

/** A text of a table placed: its left and its baseline in millimetres from the top of its page. */
export interface TableCell {
  readonly x: number;
  readonly y: number;
  readonly text: string;
  readonly face: 'regular' | 'bold';
  readonly size: number;
}

/** A page of a table: its texts and the baselines of the rules under its header and under each row. */
export interface TablePage {
  readonly cells: TableCell[];
  readonly rules: number[];
}

export interface TableOptions {
  readonly page: PageSize;
  readonly measure: Measure;
  /** The language the numbers are written in. */
  readonly language: string;
  /** The top of the table on every page, in millimetres. */
  readonly top?: number;
}

const SIZE = 9;
const LINE = 4.2;
const PAD = 1.2;
const POINT = 25.4 / 72;
const FOOTER_ROOM = 6;

/** The headers of the two first columns of the matrix, in the language shown. */
export interface RaciHeaders {
  readonly code: string;
  readonly step: string;
}

/** The RACI matrix of a map as a table: a column per lane in the order of the lanes, a row per step concerned in the reading order, the letters centred. */
export function raciTable(map: MapView, laneName: (lane: LaneView) => string, stepLabel: (step: StepView) => string, headers: RaciHeaders): TableData {
  const matrix = raciMatrix(map, laneName, stepLabel);
  return {
    headers: [headers.code, headers.step, ...matrix.columns.map((column) => column.name)],
    rows: matrix.rows.map((row) => [row.code, row.label, ...row.cells]),
    weights: [1.4, 4, ...matrix.columns.map(() => 1.2)],
    align: ['left', 'left', ...matrix.columns.map((): CellAlign => 'center')],
  };
}

/** The relative width of the eleven columns of the plan: map, step code, step, lane, pain point, description, action, owner, due date, status, hours lost. */
const PLAN_WEIGHTS = [3, 1.4, 3, 2, 1.6, 5, 5, 2.2, 1.8, 2, 1.4];

/** The action plan as a table: the eleven cells of each entry, in the order of the host, under the headers given. */
export function planTable(entries: readonly PlanEntry[], headers: readonly string[]): TableData {
  return { headers, rows: entries.map((entry) => entry.cells), weights: PLAN_WEIGHTS, align: PLAN_WEIGHTS.map((_, index): CellAlign => (index === PLAN_WEIGHTS.length - 1 ? 'right' : 'left')) };
}

/** A line cut to a width with an ellipsis at its end. */
function ellipsis(line: string, width: number, measure: Measure): string {
  let cut = line;
  while (cut.length > 1 && measure(`${cut}…`, 'regular', SIZE) > width) cut = cut.slice(0, -1);
  return `${cut.trimEnd()}…`;
}

/** The lines of a cell in its column, at most `limit` of them, the last ending in an ellipsis when the text is cut. */
function cellLines(text: string, width: number, face: 'regular' | 'bold', measure: Measure, limit: number): string[] {
  const lines = wrapText(text, width, measure, face, SIZE);
  if (lines.length <= limit) return lines;
  return [...lines.slice(0, limit - 1), ellipsis(lines[limit - 1] ?? '', width, measure)];
}

/**
 * The table on pages of the size given: the columns share the width by their weights, the header on every page, a row on one
 * page only (a cell longer than a page is cut with an ellipsis rather than lose its row), a rule under each row.
 */
export function tablePages(data: TableData, options: TableOptions): TablePage[] {
  const { page, measure, language } = options;
  const top = options.top ?? FLOW_TOP;
  const bottom = page.height - PDF_MARGIN - FOOTER_ROOM;
  const total = data.weights.reduce((sum, weight) => sum + weight, 0);
  const widths = data.weights.map((weight) => ((page.width - 2 * PDF_MARGIN) * weight) / total);
  const lefts = widths.map((_, index) => PDF_MARGIN + widths.slice(0, index).reduce((sum, width) => sum + width, 0));
  const limit = Math.max(1, Math.floor((bottom - top - 2 * PAD) / LINE) - 8);
  const numberText = new Intl.NumberFormat(language, { maximumFractionDigits: 2 });
  const pages: TablePage[] = [];
  let [current, cursor]: [TablePage, number] = [{ cells: [], rules: [] }, top];
  const writeRow = (texts: readonly (string | number)[], face: 'regular' | 'bold', aligned: boolean): void => {
    const columns = texts.map((value, index) => cellLines(typeof value === 'number' ? numberText.format(value) : value, (widths[index] ?? 0) - 2 * PAD, face, measure, limit));
    const height = Math.max(1, ...columns.map((lines) => lines.length)) * LINE + 2 * PAD;
    if (cursor !== top && cursor + height > bottom) {
      pages.push(current);
      current = { cells: [], rules: [] };
      cursor = top;
      writeRow(data.headers, 'bold', false);
    }
    columns.forEach((lines, index) => {
      const [left, width, align] = [lefts[index] ?? 0, widths[index] ?? 0, aligned ? (data.align[index] ?? 'left') : 'left'];
      lines.forEach((text, rank) => {
        if (text === '') return;
        const room = width - measure(text, face, SIZE);
        const x = align === 'center' ? left + room / 2 : align === 'right' ? left + room - PAD : left + PAD;
        current.cells.push({ x, y: cursor + PAD + rank * LINE + SIZE * POINT, text, face, size: SIZE });
      });
    });
    cursor += height;
    current.rules.push(cursor);
  };
  writeRow(data.headers, 'bold', false);
  for (const row of data.rows) writeRow(row, 'regular', true);
  return [...pages, current];
}
