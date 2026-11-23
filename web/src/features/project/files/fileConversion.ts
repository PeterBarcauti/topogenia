// fileConversion.ts
// The files the opening of a process reads besides the .pmap (plan 3.1, L5 to L7): a table (.xlsx, .csv) or a Mermaid flowchart (.mmd, .md) made into the text of a .pmap before it is sent, the copy an image SVG carries read out of it, so that the host reads it with its own rules; a workbook of macros or of the former Excel refused plainly.

import type { Translate } from '../../../shared/i18n/i18n.ts';
import { mermaidToPmap } from './mermaidImport.ts';
import { pmapOfSvg } from './svgCopy.ts';
import { TableError, tableToPmap } from './tableImport.ts';
import { csvRows, decodeCsv, workbookSheets } from './tableRead.ts';

/** The extensions the dialog of the opening takes, the .pmap first. */
export const OPENED_FILES = '.pmap,.pmaps,application/json,.xlsx,.csv,.mmd,.md,.svg';

/** The first bytes of a ZIP archive, which a package of exchange is: read as text, never the start of a .pmap (AR-154). */
export const ZIP_SIGNATURE = 'PK\u0003\u0004';

/** The name of a file without its extension: the name of the project proposed. */
const baseOf = (name: string): string => name.replace(/\.[^.]+$/, '');

/**
 * The text of the `.pmap` of a file that is not one: a workbook, a CSV or a Mermaid flowchart as a process, the copy an image carries; null for any other file, read as
 * it is. TableError for a table not understood, or for a workbook of macros or of the former format of Excel.
 */
export async function pmapOfFile(file: File, t: Translate, raciLetters?: string): Promise<string | null> {
  const name = file.name.toLowerCase();
  if (name.endsWith('.xlsx')) return tableToPmap(await workbookSheets(await file.arrayBuffer()), baseOf(file.name), t, raciLetters);
  if (name.endsWith('.csv')) return tableToPmap([{ name: baseOf(file.name), rows: csvRows(decodeCsv(await file.arrayBuffer())) }], baseOf(file.name), t, raciLetters);
  if (name.endsWith('.mmd') || name.endsWith('.md')) return mermaidToPmap(await file.text(), name.endsWith('.md'), baseOf(file.name), t);
  if (name.endsWith('.svg')) return pmapOfSvg(await file.text());
  if (name.endsWith('.xlsm') || name.endsWith('.xls')) throw new TableError('files.table.not_xlsx');
  return null;
}

/** The message of a refusal of a file converted, in the language of the interface; null for another failure. */
export const conversionMessage = (failure: unknown, t: Translate): string | null => (failure instanceof TableError ? t(failure.key, failure.values) : null);
