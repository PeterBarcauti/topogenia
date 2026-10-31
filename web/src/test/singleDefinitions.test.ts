// singleDefinitions.test.ts
// The duplications of the 1.x gone (annex B 8.1, spec 3.5): one place for each rule of the interface, read on its sources; the undo left to the host (B-8.2-02, B-8.2-03).

import { readFileSync, readdirSync, statSync } from 'node:fs';
import { join, relative, sep } from 'node:path';

const src = join(import.meta.dirname, '..');
const code = (file: string): string => readFileSync(join(src, file), 'utf8').replace(/\/\*[\s\S]*?\*\//g, '').replace(/^\s*\/\/.*$/gm, '');

const filesUnder = (dir: string): string[] => readdirSync(dir).flatMap((name) => {
  const path = join(dir, name);
  return statSync(path).isDirectory() ? filesUnder(path) : [path];
});

const shipped = filesUnder(src)
  .map((file) => relative(src, file).split(sep).join('/'))
  .filter((file) => /\.tsx?$/.test(file) && !/\.test\.tsx?$/.test(file) && !file.startsWith('test/') && !file.startsWith('api/generated/'));

/** The modules whose code matches, each with the number of times. */
const where = (pattern: RegExp, files: readonly string[] = shipped): string[] =>
  files.flatMap((file) => {
    const count = code(file).match(new RegExp(pattern.source, `${pattern.flags.replace('g', '')}g`))?.length ?? 0;
    return count === 0 ? [] : [`${file} ${String(count)}`];
  });

describe('one place for each rule (annex B 8.1)', () => {
  it('the default label of a new step is chosen at one place, for every way to create one (B-8.1-14)', () => {
    expect(where(/'map\.new_step'/)).toEqual(['features/map/state/actions/steps.ts 1']);
  });

  it('the text of an error is made by messageOf alone, which the screens import (B-8.1-16)', () => {
    expect(where(/function messageOf\(/)).toEqual(['shared/i18n/errorMessage.ts 1']);
    expect(where(/`errors\.\$\{/)).toEqual(['shared/i18n/errorMessage.ts 1']);
    expect(where(/import \{[^}]*\bmessageOf\b[^}]*\} from '[./]+\/shared\/i18n\/errorMessage\.ts'/).length).toBeGreaterThan(10);
    expect(where(/messageOf/, ['features/map/state/mapStore.ts'])).toEqual([]);
  });

  it('a name is translated by localizedName alone, its translation else its name (B-8.1-23)', () => {
    expect(where(/translations\[[^\]]+\]\s*\?\?\s*[\w.]+\.(name|label)\b/)).toEqual(['shared/i18n/localizedName.ts 1']);
  });

  it('the pattern and the filter of a project code are written once, for the four forms that take one (B-8.1-24)', () => {
    expect(where(/\[A-Z\]\{3,6\}/)).toEqual(['features/project/files/pmapText.ts 1']);
    expect(where(/\[\^A-Z\]/)).toEqual(['features/project/files/pmapText.ts 1']);
    for (const form of ['features/project/NewProjectScreen.tsx', 'features/project/files/DuplicateDialog.tsx', 'features/project/files/OpenPmapDialog.tsx', 'features/project/package/ImportPackageDialog.tsx']) {
      expect([form, /import \{ PROJECT_CODE, (headerOf, )?typedCode \}/.test(code(form))]).toEqual([form, true]);
    }
  });

  it('a date is written in the language of the interface, its day by formatDate alone; no locale tag in the code (B-8.1-25)', () => {
    expect(where(/['"`][a-z]{2}-[A-Z]{2}['"`]/)).toEqual([]);
    expect(where(/toLocaleDateString\(/)).toEqual(['shared/i18n/formatDate.ts 1']);
    expect(where(/toLocaleString\(/)).toEqual(['features/map/state/actions/exports.ts 1']);
  });

  it('the function that notifies is typed once (B-8.1-27)', () => {
    expect(where(/notify\??: \(text: string/)).toEqual(['shared/ui/toasts.ts 1']);
  });

  it('one type of notification, with its kind (B-8.1-28)', () => {
    expect(where(/^\s*(export )?(type|interface) \w*(Toast|Notification|Notice)\w*\b/m)).toEqual(['shared/ui/toasts.ts 4']);
    expect(code('shared/ui/toasts.ts')).toMatch(/export type ToastKind = 'info' \| 'success' \| 'error';/);
  });

  it('« a line taken between two columns » is one predicate of the routing, used by the routes and the sides (B-8.1-30)', () => {
    const routing = shipped.filter((file) => file.startsWith('features/map/model/geometry/routing/'));
    expect(where(/column > Math\.min\(from, to\) && column < Math\.max\(from, to\)/, routing)).toEqual(['features/map/model/geometry/routing/model.ts 1']);
    expect(where(/\blineTaken\(/, routing)).toEqual(['features/map/model/geometry/routing/model.ts 1', 'features/map/model/geometry/routing/routes.ts 2', 'features/map/model/geometry/routing/sides.ts 3']);
  });
});

describe('the undo is the host’s (B-8.1-12, B-8.2-02, B-8.2-03, B-8.2-08)', () => {
  it('only the action of the history calls the undo and the redo of the host; the history of the editor is the one the host gives', () => {
    expect(where(/\/undo'|\/redo'/)).toEqual(['features/map/state/actions/history.ts 2']);
    expect(code('features/map/state/mapStore.ts')).toMatch(/readonly history: Schema<'HistoryView'> \| null;/);
  });

  it('no module of the interface keeps a stack of operations to undo: no helper of undo, no history of the client', () => {
    expect(shipped.filter((file) => /(undo|redo|annul|histor)/i.test(file))).toEqual(['features/map/state/actions/history.ts']);
    expect(code('features/map/state/actions/history.ts')).not.toMatch(/\.push\(|\.pop\(|\bstack\b/);
  });

  it('the operations of the map write through the queue of the editor and none undoes itself', () => {
    const actions = shipped.filter((file) => file.startsWith('features/map/state/actions/') && file !== 'features/map/state/actions/history.ts');
    expect(where(/\b(undo|redo)\b/i, actions)).toEqual([]);
    expect(where(/editor\.write\(/, actions).length).toBeGreaterThan(8);
  });

  it('detects what each rule refuses', () => {
    expect('crumb.translations[language] ?? crumb.name').toMatch(/translations\[[^\]]+\]\s*\?\?\s*[\w.]+\.(name|label)\b/);
    expect("new Date(iso).toLocaleDateString('fr-FR')").toMatch(/['"`][a-z]{2}-[A-Z]{2}['"`]/);
    expect('readonly notify: (text: string, kind?: ToastKind) => void;').toMatch(/notify\??: \(text: string/);
    expect('export interface Notification { text: string }').toMatch(/^\s*(export )?(type|interface) \w*(Toast|Notification|Notice)\w*\b/m);
    expect("import { type Toast } from './toasts.ts';").not.toMatch(/^\s*(export )?(type|interface) \w*(Toast|Notification|Notice)\w*\b/m);
  });
});
