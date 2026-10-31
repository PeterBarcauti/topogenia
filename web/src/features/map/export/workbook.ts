// workbook.ts
// The workbook of a map built in the browser: its steps, its links and its RACI matrix, the value stream with its layer on, frozen bold headers on the colour of the tables, letters centred on their colour, the classification in the print header (annex B 2.14, S-4.1-08, ADR 066, spec 14.9).

import type { Worksheet } from 'exceljs';
import type { Schema } from '../../../api/client.ts';
import { WORKBOOK_TYPE } from '../../../shared/download.ts';
import { PRODUCT_NAME, type Translate } from '../../../shared/i18n/i18n.ts';
import { flowText, laneName, stepLabel } from '../model/mapLayout.ts';
import { raciLetters, raciMatrix, rolesOf, type RaciLetter } from '../model/raci.ts';
import { laneRanks, readingOrder } from '../model/readingOrder.ts';
import { streamSheets } from './streamSheets.ts';
import { isMacro } from '../model/stepFacts.ts';

type MapView = Schema<'MapView'>;
type StepView = Schema<'StepView'>;

/** The longest name of a sheet Excel accepts. */
const SHEET_NAME_MAX = 31;

/** A cell: a text, or a number written as a number at full precision (the figures of the value stream, spec 14.9). */
export type CellValue = string | number;

/** A sheet of the workbook, whatever the library that writes it: its name, its headers, its rows of cells, its widths. */
export interface Sheet {
  readonly name: string;
  readonly headers: readonly string[];
  readonly rows: readonly (readonly CellValue[])[];
  readonly widths: readonly number[];
  /** The columns of the RACI letters, from 0: centred, each letter on its colour (B-2.14-25). */
  readonly letters?: readonly number[];
}

/** Excel refuses some characters in the name of a sheet and stops it at 31 characters (B-2.14-28). */
export const sheetName = (name: string): string => name.replace(/[\\/?*[\]:]/g, ' ').slice(0, SHEET_NAME_MAX);

/** The steps of the map with their lanes and their sources and targets, in the language shown. */
function namesOf(map: MapView, language: string) {
  const lanes = new Map(map.lanes.map((lane) => [lane.id, lane]));
  const steps = new Map(map.steps.map((step) => [step.id, step]));
  const ranks = laneRanks(map.lanes);
  const order = readingOrder(ranks);
  const lane = (id: string) => {
    const found = lanes.get(id);
    return found === undefined ? '' : laneName(found, language);
  };
  return { lanes, steps, ranks, order, lane };
}

/**
 * « Étapes »: every step in the reading order, start and end included (B-2.14-22): code, path, label, type, the lane that
 * performs it, one column per letter of the map with its lanes, detailed, next steps (B-2.14-21).
 */
function stepsSheet(map: MapView, language: string, t: Translate): Sheet {
  const { steps, ranks, order, lane } = namesOf(map, language);
  const letters = raciLetters(map);
  const next = (step: StepView) =>
    map.flows
      .filter((flow) => flow.source_step_id === step.id)
      .flatMap((flow) => steps.get(flow.target_step_id) ?? [])
      .sort(order)
      .map((target) => target.code)
      .join(', ');
  const withLetter = (step: StepView, letter: RaciLetter) =>
    [...rolesOf(map, step)]
      .filter(([, role]) => role === letter)
      .map(([id]) => id)
      .sort((a, b) => (ranks.get(a) ?? 0) - (ranks.get(b) ?? 0))
      .map(lane)
      .join(', ');
  return {
    name: t('export.sheet_steps'),
    headers: [t('raci.column_code'), t('export.path'), t('raci.column_step'), t('export.type'), t('raci.names.R'), ...letters.map((letter) => t(`raci.names.${letter}`)),
      t('export.detailed'), t('export.next')],
    rows: [...map.steps].sort(order).map((step) => [
      step.code, step.path, stepLabel(step, language), t(`map.step_types.${step.type}`), lane(step.lane_id), ...letters.map((letter) => withLetter(step, letter)),
      isMacro(step) ? t('export.yes') : '', next(step),
    ]),
    widths: [12, 30, 40, 12, 22, ...letters.map(() => 22), 10, 24],
  };
}

/** « Liens »: from, its step, to, its step, label (B-2.14-23), in the reading order of their sources, then of their targets. */
function flowsSheet(map: MapView, language: string, t: Translate): Sheet {
  const { steps, order } = namesOf(map, language);
  const links = map.flows.flatMap((flow) => {
    const source = steps.get(flow.source_step_id);
    const target = steps.get(flow.target_step_id);
    return source === undefined || target === undefined ? [] : [{ flow, source, target }];
  });
  links.sort((a, b) => order(a.source, b.source) || order(a.target, b.target));
  return {
    name: t('export.sheet_flows'),
    headers: [t('export.from'), t('raci.column_step'), t('export.to'), t('raci.column_step'), t('export.label')],
    rows: links.map(({ flow, source, target }) => [source.code, stepLabel(source, language), target.code, stepLabel(target, language), flowText(flow, source, language)]),
    widths: [12, 40, 12, 40, 16],
  };
}

/** « Matrice RACI »: the matrix of the dialog, without the steps without content (B-2.14-24), its letters centred (B-2.14-25). */
function matrixSheet(map: MapView, language: string, t: Translate): Sheet {
  const matrix = raciMatrix(map, (lane) => laneName(lane, language), (step) => stepLabel(step, language));
  return {
    name: t('export.sheet_raci'),
    headers: [t('raci.column_code'), t('raci.column_step'), ...matrix.columns.map((column) => column.name)],
    rows: matrix.rows.map((row) => [row.code, row.label, ...row.cells]),
    widths: [12, 40, ...matrix.columns.map((column) => Math.max(10, Math.min(24, column.name.length + 2)))],
    letters: matrix.columns.map((_, index) => index + 2),
  };
}

/** The three sheets of the export of a map, in the language shown (B-2.14-20); the two of the value stream after them with its layer on (spec 14.9). */
export const workbookSheets = (map: MapView, language: string, t: Translate, valueStream = false): Sheet[] =>
  [stepsSheet(map, language, t), flowsSheet(map, language, t), matrixSheet(map, language, t), ...(valueStream ? streamSheets(map, language, t) : [])];

/**
 * The colours of the workbook, read from the tokens of the charter when it is built (spec 9.2: no colour is written in
 * the code): the headers and each letter, in ARGB; a colour missing leaves its fill out.
 */
export interface WorkbookLook {
  readonly header: string | null;
  readonly letters: Readonly<Partial<Record<RaciLetter, string>>>;
}

/** A colour of a token in the ARGB of Excel: hexadecimal of three or six digits, or red, green and blue in the functional notation; null for any other value. */
export function argbOf(colour: string): string | null {
  const text = colour.trim();
  const short = /^#([0-9A-Fa-f])([0-9A-Fa-f])([0-9A-Fa-f])$/.exec(text);
  if (short !== null) return `FF${short.slice(1).map((digit) => digit + digit).join('')}`.toUpperCase();
  if (/^#[0-9A-Fa-f]{6}$/.test(text)) return `FF${text.slice(1)}`.toUpperCase();
  const channels = /^rgba?\(\s*(\d{1,3})\s*,\s*(\d{1,3})\s*,\s*(\d{1,3})\s*(?:,\s*1\s*)?\)$/.exec(text);
  if (channels === null || channels.slice(1).some((value) => Number(value) > 255)) return null;
  return `FF${channels.slice(1).map((value) => Number(value).toString(16).padStart(2, '0')).join('')}`.toUpperCase();
}

/** The header of a printed page carries the classification on the right; an ampersand is written twice, as Excel reads it. */
const printHeader = (classification: string): string => `&R${classification.replace(/&/g, '&&')}`;

const fill = (argb: string) => ({ type: 'pattern' as const, pattern: 'solid' as const, fgColor: { argb } });

/** One sheet: frozen bold headers on their colour, filter, empty texts as empty cells, letters centred on their colour (B-2.14-25 to B-2.14-27). */
function writeSheet(sheet: Worksheet, content: Sheet, look: WorkbookLook, classification: string): void {
  sheet.columns = content.headers.map((header, index) => ({ header, width: content.widths[index] ?? 15 }));
  // An empty text written in Excel is not an empty cell (filters, COUNTA): nothing is written.
  for (const row of content.rows) sheet.addRow(row.map((value) => (value === '' ? null : value)));
  const head = sheet.getRow(1);
  head.font = { bold: true };
  if (look.header !== null) head.fill = fill(look.header);
  sheet.autoFilter = { from: { row: 1, column: 1 }, to: { row: 1, column: content.headers.length } };
  sheet.headerFooter.oddHeader = printHeader(classification);
  for (const index of content.letters ?? []) {
    const column = sheet.getColumn(index + 1);
    column.alignment = { horizontal: 'center' };
    column.eachCell((cell, row) => {
      const colour = typeof cell.value === 'string' ? look.letters[cell.value as RaciLetter] : undefined;
      if (row === 1 || colour === undefined) return;
      cell.fill = fill(colour);
      cell.font = { bold: true };
    });
  }
}

/** What the workbook says of itself: the name of the map as its title, the classification of the project printed on each sheet (S-4.1-08). */
export interface WorkbookFacts {
  readonly title: string;
  readonly classification: string;
}

/** The workbook to download, written by exceljs loaded on demand (B-0-04); its creator is the product (B-2.14-31). */
export async function buildMapWorkbook(sheets: readonly Sheet[], facts: WorkbookFacts, look: WorkbookLook): Promise<Blob> {
  const { default: ExcelJS } = await import('exceljs');
  const book = new ExcelJS.Workbook();
  book.creator = PRODUCT_NAME;
  book.title = facts.title;
  book.created = new Date();
  for (const sheet of sheets) writeSheet(book.addWorksheet(sheetName(sheet.name), { views: [{ state: 'frozen', ySplit: 1 }] }), sheet, look, facts.classification);
  const buffer = await book.xlsx.writeBuffer();
  return new Blob([buffer], { type: WORKBOOK_TYPE });
}
