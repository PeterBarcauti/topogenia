// workbook.ts
// The workbook of the translations built in the browser: the texts with their hidden key, one column per configured language, the instruction with the glossary of the project, the hidden sheet of control (annex B 5, ADR 065).

import type { Worksheet } from 'exceljs';
import type { Schema } from '../../../api/client.ts';
import { WORKBOOK_TYPE } from '../../../shared/download.ts';
import { PRODUCT_NAME, type Translate } from '../../../shared/i18n/i18n.ts';
import { LANE_TINTS } from '../../../shared/theme/laneTints.ts';

type TextsToTranslate = Schema<'TranslationList'>;

/** The columns of the sheet of the texts (1 = A): the hidden key `type:id`, the item, the code, the context, then the languages (B-5-07). */
export const COLUMNS = { key: 1, type: 2, code: 3, context: 4, firstLanguage: 5 } as const;

/** The hidden sheet that ties a workbook to its project, named after the product in every language (B-5-12, AR-28). */
export const CONTROL_SHEET = PRODUCT_NAME;

/** The rows of the sheet of control, read back by their name in the first column. */
export const CONTROL = { project: 'project', scope: 'scope', languages: 'languages', sheet: 'sheet' } as const;

/** The header of the texts: the light blue of the charter (the 1.x used the same shade). */
const HEADER_FILL = `FF${LANE_TINTS.find((tint) => tint.key === 'sky')?.pastel.slice(1) ?? 'FFFFFF'}`;

/** The instruction for the AI: its paragraphs, then the glossary of the project, one term a line (B-5-10, B-5-11). */
export interface Instruction {
  readonly paragraphs: readonly string[];
  readonly glossary: readonly string[];
}

/** The instruction in the languages of the workbook; the glossary is a setting of the project, never a text of the code (B-8.1-45). */
export function instructionOf(languages: readonly string[], glossary: string | null, t: Translate): Instruction {
  const terms = (glossary ?? '').split(/\r?\n/).filter((line) => line.trim() !== '');
  const paragraphs = t('translations.instruction', {
    sheet: t('translations.sheet_texts'), languages: languages.map((language) => language.toUpperCase()).join(', '),
  }).split('\n');
  return { paragraphs: terms.length === 0 ? paragraphs : [...paragraphs, t('translations.instruction_glossary')], glossary: terms };
}

/** The instruction as one text, as « Copy the instruction » puts it in the clipboard (B-5-15). */
export function instructionText(instruction: Instruction, t: Translate): string {
  const glossary = instruction.glossary.length === 0 ? [] : ['', t('translations.glossary'), ...instruction.glossary];
  return [...instruction.paragraphs, ...glossary].join('\n');
}

/** One row a text, its original in the column of its language and the other languages empty; frozen pane, filter, wrapping (B-5-07 to B-5-09). */
function writeTexts(sheet: Worksheet, list: TextsToTranslate, t: Translate): void {
  sheet.columns = [
    { header: 'id', width: 10 },
    { header: t('translations.column_type'), width: 14 },
    { header: t('translations.column_code'), width: 18 },
    { header: t('translations.column_context'), width: 40 },
    ...list.languages.map((language) => ({ header: language.toUpperCase(), width: 50 })),
  ];
  for (const text of list.texts) {
    const row: (string | null)[] = [`${text.type}:${text.id}`, t(`translations.types.${text.type}`), text.code, text.context === '' ? null : text.context];
    for (const language of list.languages) row.push(language === text.language ? text.original : null);
    sheet.addRow(row);
  }
  const head = sheet.getRow(1);
  head.font = { bold: true };
  head.fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: HEADER_FILL } };
  sheet.getColumn(COLUMNS.key).hidden = true;
  list.languages.forEach((_, index) => {
    sheet.getColumn(COLUMNS.firstLanguage + index).alignment = { wrapText: true, vertical: 'top' };
  });
  sheet.autoFilter = { from: { row: 1, column: COLUMNS.type }, to: { row: 1, column: COLUMNS.firstLanguage + list.languages.length - 1 } };
}

function writeInstruction(sheet: Worksheet, instruction: Instruction, t: Translate): void {
  sheet.getColumn(1).width = 110;
  sheet.getColumn(1).alignment = { wrapText: true, vertical: 'top' };
  sheet.addRow([t('translations.instruction_title')]).font = { bold: true, size: 13 };
  sheet.addRow([]);
  for (const paragraph of instruction.paragraphs) sheet.addRow([paragraph]);
  if (instruction.glossary.length === 0) return;
  sheet.addRow([]);
  sheet.addRow([t('translations.glossary')]).font = { bold: true };
  for (const term of instruction.glossary) sheet.addRow([term]);
}

/** The workbook to download: the texts, the instruction with the glossary, the sheet of control kept out of sight (`veryHidden`). */
export async function buildWorkbook(list: TextsToTranslate, glossary: string | null, t: Translate): Promise<Blob> {
  const { default: ExcelJS } = await import('exceljs');
  const book = new ExcelJS.Workbook();
  book.creator = PRODUCT_NAME;
  book.created = new Date();
  const textsSheet = t('translations.sheet_texts');
  writeTexts(book.addWorksheet(textsSheet, { views: [{ state: 'frozen', ySplit: 1 }] }), list, t);
  writeInstruction(book.addWorksheet(t('translations.sheet_instruction')), instructionOf(list.languages, glossary, t), t);
  const control = book.addWorksheet(CONTROL_SHEET, { state: 'veryHidden' });
  control.addRow([CONTROL.project, list.project_id]);
  control.addRow([CONTROL.scope, list.scope]);
  control.addRow([CONTROL.languages, list.languages.join(',')]);
  control.addRow([CONTROL.sheet, textsSheet]);
  const buffer = await book.xlsx.writeBuffer();
  return new Blob([buffer], { type: WORKBOOK_TYPE });
}
