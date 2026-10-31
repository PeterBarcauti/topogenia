// structure.test.ts
// The structure of the editor of the map read on its sources: one reading order of the steps, the generic dialogs in shared/ui apart from the RACI and the breadcrumb (B-8.1-21, B-8.1-29).

import { readFileSync, readdirSync, statSync } from 'node:fs';
import { join, relative, sep } from 'node:path';

const src = join(import.meta.dirname, '../..');
const read = (file: string) => readFileSync(join(src, file), 'utf8');

const sources = (dir: string): string[] => readdirSync(dir).flatMap((name) => {
  const path = join(dir, name);
  return statSync(path).isDirectory() ? sources(path) : [path];
});

/** The modules of the editor of the map, the tests aside. */
const shipped = sources(join(src, 'features/map'))
  .filter((file) => /\.tsx?$/.test(file) && !/\.test\.tsx?$/.test(file))
  .map((file) => relative(src, file).split(sep).join('/'));

describe('one reading order of the steps (B-8.1-29; 1.x: one in the alignment, one in the clipboard, one in the remainder)', () => {
  it('the alignment, the clipboard, the remainder and the RACI matrix sort their steps with the function of model/readingOrder', () => {
    for (const file of ['alignment.ts', 'clipboard.ts', 'remainder.ts', 'raci.ts'].map((name) => `features/map/model/${name}`)) {
      expect([file, /from '\.\/readingOrder\.ts'/.test(read(file)), /\breadingOrder\(/.test(read(file))]).toEqual([file, true, true]);
    }
  });

  it('no other module compares two steps by their column, then the rank of their lane', () => {
    expect(shipped.filter((file) => /a\.column - b\.column \|\|/.test(read(file)))).toEqual(['features/map/model/readingOrder.ts']);
  });
});

describe('the generic dialogs (B-8.1-21)', () => {
  it('Dialog and Confirm live in shared/ui and know nothing of the RACI nor of the breadcrumb', () => {
    for (const file of ['shared/ui/Dialog.tsx', 'shared/ui/Confirm.tsx']) expect([file, /raci|breadcrumb|crumb/i.test(read(file))]).toEqual([file, false]);
  });

  it('the matrix, the stored remainder and the deletion of an element take them from shared/ui', () => {
    expect([
      /import \{ Dialog \} from '\.\.\/\.\.\/\.\.\/shared\/ui\/Dialog\.tsx'/.test(read('features/map/panels/RaciMatrixDialog.tsx')),
      /import \{ Dialog \} from '\.\.\/\.\.\/\.\.\/shared\/ui\/Dialog\.tsx'/.test(read('features/map/panels/ArrangementDialog.tsx')),
      /import \{ Confirm \} from '\.\.\/\.\.\/\.\.\/shared\/ui\/Confirm\.tsx'/.test(read('features/map/panels/RemainderPanel.tsx')),
    ]).toEqual([true, true, true]);
  });
});
