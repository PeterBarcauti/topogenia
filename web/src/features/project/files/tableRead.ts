// tableRead.ts
// The rows of a table to open as a process (plan 3.1, L5): a CSV of the RFC 4180, its separator read on its first line and its encoding UTF-8 or Windows-1252, or the sheets of a workbook read by exceljs loaded on demand, the values computed and never the formulas.

/** A sheet as rows of texts, its name kept. */
export interface TableSheet {
  readonly name: string;
  readonly rows: readonly (readonly string[])[];
}

/** Rows without the empty ones that end them. */
const withoutEmptyEnd = (rows: string[][]): string[][] => {
  let end = rows.length;
  while (end > 0 && (rows[end - 1] ?? []).length === 0) end--;
  return rows.slice(0, end);
};

/** The separators a CSV may use, the first that splits its first line the most. */
const SEPARATORS = [';', ',', '\t'] as const;

/** The separator of a CSV: the one found most often outside quotes on its first line; the semicolon of Excel in French by default. */
export function separatorOf(text: string): string {
  const line = text.split(/\r?\n/, 1)[0] ?? '';
  let quoted = false;
  const counts = new Map<string, number>(SEPARATORS.map((separator) => [separator, 0]));
  for (const char of line) {
    if (char === '"') quoted = !quoted;
    else if (!quoted && counts.has(char)) counts.set(char, (counts.get(char) ?? 0) + 1);
  }
  return [...counts.entries()].reduce<[string, number]>((best, entry) => (entry[1] > best[1] ? entry : best), [';', 0])[0];
}

/** The rows of a CSV (RFC 4180): fields in quotes, a quote doubled inside, line ends inside a quoted field; an empty line kept empty, so that each row keeps its line, the empty ones at the end left out. */
export function csvRows(text: string, separator = separatorOf(text)): string[][] {
  const rows: string[][] = [];
  let row: string[] = [];
  let field = '';
  let quoted = false;
  const end = () => {
    row.push(field);
    field = '';
  };
  for (let index = 0; index < text.length; index++) {
    const char = text[index] ?? '';
    if (quoted) {
      if (char === '"' && text[index + 1] === '"') {
        field += '"';
        index++;
      } else if (char === '"') quoted = false;
      else field += char;
    } else if (char === '"' && field === '') quoted = true;
    else if (char === separator) end();
    else if (char === '\n' || char === '\r') {
      if (char === '\r' && text[index + 1] === '\n') index++;
      end();
      rows.push(row);
      row = [];
    } else field += char;
  }
  if (field !== '' || row.length > 0) {
    end();
    rows.push(row);
  }
  return withoutEmptyEnd(rows.map((cells) => (cells.some((cell) => cell.trim() !== '') ? cells : [])));
}

/** The text of a CSV: UTF-8 when its bytes are, its mark of order left out; Windows-1252 otherwise, as Excel writes it in Western Europe. */
export function decodeCsv(bytes: ArrayBuffer): string {
  try {
    return new TextDecoder('utf-8', { fatal: true }).decode(bytes).replace(/^\uFEFF/, '');
  } catch {
    return new TextDecoder('windows-1252').decode(bytes);
  }
}

/**
 * A date of a cell: a duration of Excel (`[h]:mm`, a date of its epoch of 1899) in hours, which a delay reads; a day in ISO;
 * nothing for a date out of range.
 */
function dateText(date: Date): string {
  const time = date.getTime();
  if (!Number.isFinite(time)) return '';
  if (date.getUTCFullYear() < 1900) return `${String(Math.round(((time - Date.UTC(1899, 11, 30)) / 3_600_000) * 100) / 100)} h`;
  return date.toISOString().slice(0, 10);
}

/** The text of a cell of exceljs: its value, the result of its formula, its rich text joined, the text of its link, a date in ISO. */
export function cellText(value: unknown): string {
  if (value === null || value === undefined) return '';
  if (typeof value === 'string') return value;
  if (typeof value === 'number' || typeof value === 'boolean') return String(value);
  if (value instanceof Date) return dateText(value);
  if (typeof value === 'object') {
    const cell = value as { result?: unknown; richText?: readonly { text: string }[]; text?: unknown };
    if ('result' in cell) return cellText(cell.result);
    if (cell.richText !== undefined) return cell.richText.map((part) => part.text).join('');
    if ('text' in cell) return cellText(cell.text);
  }
  return '';
}

/** The sheets of a workbook (.xlsx), each as rows of texts; exceljs loaded only when a workbook is opened. */
export async function workbookSheets(bytes: ArrayBuffer): Promise<TableSheet[]> {
  const { default: ExcelJS } = await import('exceljs');
  const book = new ExcelJS.Workbook();
  await book.xlsx.load(bytes);
  return book.worksheets.map((sheet) => {
    // Each row at its number, the empty ones kept empty: an uncertainty names the line of the sheet.
    const rows: (string[] | undefined)[] = [];
    sheet.eachRow((row, number) => {
      const values = Array.isArray(row.values) ? row.values.slice(1) : [];
      rows[number - 1] = values.map((value) => cellText(value).trim());
    });
    return { name: sheet.name, rows: withoutEmptyEnd(Array.from(rows, (cells) => (cells !== undefined && cells.some((cell) => cell !== '') ? cells : []))) };
  });
}
