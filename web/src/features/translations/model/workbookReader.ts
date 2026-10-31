// workbookReader.ts
// The workbook brought back: checked against its project by the hidden sheet of control, its rows read by their hidden key whatever their order, every form of cell read as text (annex B 5, B-5-16 to B-5-21).

import type { CellValue, Workbook, Worksheet } from 'exceljs';
import type { Schema } from '../../../api/client.ts';
import { COLUMNS, CONTROL, CONTROL_SHEET } from './workbook.ts';

type RowSent = Schema<'TranslationRowBody'>;

/** The codes of a workbook refused before it is sent (B-5-17 to B-5-19; 1.x `FICHIER_SANS_ARIANE` is replaced, AR-28). */
export type WorkbookFault = 'FICHIER_ILLISIBLE' | 'FICHIER_SANS_FEUILLE_PROJET' | 'FICHIER_AUTRE_PROJET';

export class WorkbookError extends Error {
  readonly code: WorkbookFault;

  constructor(code: WorkbookFault) {
    super(code);
    this.name = 'WorkbookError';
    this.code = code;
  }
}

export type ScopeCode = 'missing' | 'auto' | 'all';

const SCOPES: readonly ScopeCode[] = ['missing', 'auto', 'all'];

const OBJECT_TYPES: readonly string[] = ['project', 'map', 'lane', 'step', 'flow'] satisfies readonly Schema<'TranslationObject'>[];

const IDENTIFIER = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

export interface WorkbookRows {
  readonly scope: ScopeCode;
  /** The rows to send, in the order of the sheet, each with its line and the text of each language column filled. */
  readonly rows: readonly RowSent[];
  /** The lines whose key is not one the workbook wrote: they cannot be sent, the report counts them with the items not found. */
  readonly unknownKeys: readonly number[];
}

/** The text of a cell whatever its form: rich text, formula, number, link (B-5-21); null for an empty cell, a date or an error. */
export function cellText(value: CellValue | undefined): string | null {
  if (value === null || value === undefined) return null;
  if (typeof value === 'string') return value.trim() === '' ? null : value.trim();
  if (typeof value === 'number' || typeof value === 'boolean') return String(value);
  if ('richText' in value) return cellText(value.richText.map((run) => run.text).join(''));
  if ('result' in value) return cellText(value.result);
  if ('text' in value && typeof value.text === 'string') return cellText(value.text);
  return null;
}

/** The type and identifier of a hidden key `type:id`, null when the key is not one the workbook writes. */
export function parseKey(key: string): { readonly type: string; readonly id: string } | null {
  const at = key.indexOf(':');
  const type = key.slice(0, at);
  const id = key.slice(at + 1);
  return OBJECT_TYPES.includes(type) && IDENTIFIER.test(id) ? { type, id } : null;
}

interface Control {
  readonly project: string;
  readonly scope: ScopeCode;
  readonly languages: readonly string[];
  readonly sheet: string;
}

/** The settings of the sheet of control, null without it or without one of the values the reading needs. */
function controlOf(book: Workbook): Control | null {
  const sheet = book.getWorksheet(CONTROL_SHEET);
  if (sheet === undefined) return null;
  const values = new Map<string, string>();
  sheet.eachRow((row) => {
    const name = cellText(row.getCell(1).value);
    const value = cellText(row.getCell(2).value);
    if (name !== null && value !== null) values.set(name, value);
  });
  const project = values.get(CONTROL.project);
  const languages = (values.get(CONTROL.languages) ?? '').split(',').filter((language) => language !== '');
  const textsSheet = values.get(CONTROL.sheet);
  if (project === undefined || languages.length === 0 || textsSheet === undefined) return null;
  return { project, scope: SCOPES.find((scope) => scope === values.get(CONTROL.scope)) ?? 'missing', languages, sheet: textsSheet };
}

/** Every row after the header with a key; the original text is left to the host, which reads the column of its language. */
function rowsOf(sheet: Worksheet, languages: readonly string[]): Pick<WorkbookRows, 'rows' | 'unknownKeys'> {
  const rows: RowSent[] = [];
  const unknownKeys: number[] = [];
  sheet.eachRow((row, line) => {
    const key = line === 1 ? null : cellText(row.getCell(COLUMNS.key).value);
    if (key === null) return;
    const parsed = parseKey(key);
    if (parsed === null) {
      unknownKeys.push(line);
      return;
    }
    const translations: Record<string, string> = {};
    languages.forEach((language, index) => {
      const text = cellText(row.getCell(COLUMNS.firstLanguage + index).value);
      if (text !== null) translations[language] = text;
    });
    rows.push({ line, type: parsed.type, id: parsed.id, original: null, translations });
  });
  return { rows, unknownKeys };
}

/** Reads a workbook brought back for a project; a workbook of another project or without its sheets is refused (B-5-16). */
export async function readWorkbook(data: ArrayBuffer, projectId: string): Promise<WorkbookRows> {
  const { default: ExcelJS } = await import('exceljs');
  const book = new ExcelJS.Workbook();
  try {
    await book.xlsx.load(data);
  } catch {
    throw new WorkbookError('FICHIER_ILLISIBLE');
  }
  const control = controlOf(book);
  const texts = control === null ? undefined : book.getWorksheet(control.sheet);
  if (control === null || texts === undefined) throw new WorkbookError('FICHIER_SANS_FEUILLE_PROJET');
  if (control.project !== projectId) throw new WorkbookError('FICHIER_AUTRE_PROJET');
  return { scope: control.scope, ...rowsOf(texts, control.languages) };
}
