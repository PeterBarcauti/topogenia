// sources.test.ts
// Read in the sources of the interface: the libraries of the exports loaded on demand only, the type of the translator declared once (B-0-04, B-8.1-26).

import { readdirSync, readFileSync } from 'node:fs';
import { join, relative } from 'node:path';

const SRC = join(import.meta.dirname, '..', '..', '..');

/** The sources shipped: every .ts and .tsx of src but the tests and their helpers. */
function sources(folder = SRC): string[] {
  return readdirSync(folder, { withFileTypes: true }).flatMap((entry) => {
    const path = join(folder, entry.name);
    if (entry.isDirectory()) return entry.name === 'test' ? [] : sources(path);
    return /\.tsx?$/.test(entry.name) && !/\.test\.tsx?$/.test(entry.name) ? [path] : [];
  });
}

const files = sources().map((path) => ({ path: relative(SRC, path).split('\\').join('/'), text: readFileSync(path, 'utf8') }));

const LIBRARIES = ['exceljs', 'jspdf', 'html-to-image'];

describe('the libraries of the exports (B-0-04)', () => {
  it('exceljs, jsPDF and html-to-image are never imported with the application, only by import() when a file is made', () => {
    expect(files.length).toBeGreaterThan(100);
    const statics = files.flatMap(({ path, text }) =>
      LIBRARIES.filter((library) => new RegExp(`^import (?!type )[^;]*from '${library}'`, 'm').test(text)).map((library) => `${path}: ${library}`));
    expect(statics).toEqual([]);
    const loading = (library: string) => files.filter(({ text }) => text.includes(`await import('${library}')`)).map(({ path }) => path).sort();
    expect(LIBRARIES.map(loading)).toEqual([
      ['features/map/export/workbook.ts', 'features/translations/model/workbook.ts', 'features/translations/model/workbookReader.ts'], ['features/map/export/pdf.ts'], ['features/map/export/capture.ts'],
    ]);
  });

  it('a type taken from them is no import of the library', () => {
    expect(new RegExp("^import (?!type )[^;]*from 'exceljs'", 'm').test("import type { Worksheet } from 'exceljs';")).toBe(false);
    expect(new RegExp("^import (?!type )[^;]*from 'exceljs'", 'm').test("import {\n  Workbook,\n} from 'exceljs';")).toBe(true);
  });
});

describe('the type of the translator (B-8.1-26)', () => {
  it('is declared once, with the languages of the interface, for the two workbooks', () => {
    expect(files.filter(({ text }) => /type Translate\s*=/.test(text)).map(({ path }) => path)).toEqual(['shared/i18n/i18n.ts']);
    // The files that name the type in their code, its word in a comment aside.
    const users = files.filter(({ path, text }) => path !== 'shared/i18n/i18n.ts' && /^(?!\s*(\/\/|\*)).*\bTranslate\b/m.test(text));
    expect(users.map(({ path }) => path).sort()).toEqual(['features/map/export/streamPdf.ts', 'features/map/export/streamSheets.ts', 'features/map/export/workbook.ts', 'features/map/model/valueStream.ts', 'features/map/state/actions/exports.ts', 'features/translations/model/workbook.ts', 'features/translations/useWorkbook.ts']);
    expect(users.filter(({ text }) => !/import [^;]*\bTranslate\b[^;]*from '(\.\.\/)+shared\/i18n\/i18n\.ts'/.test(text)).map(({ path }) => path)).toEqual([]);
  });
});
