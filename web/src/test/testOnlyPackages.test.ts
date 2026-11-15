// testOnlyPackages.test.ts
// Plan 3.1, principle 5: the packages kept for the tests (Mermaid, which proves each diagram the exports write) are never imported by the code shipped, and stay among the dependencies of development only.

import { readFileSync, readdirSync, statSync } from 'node:fs';
import { join, relative, sep } from 'node:path';

const src = join(import.meta.dirname, '..');

const filesUnder = (dir: string): string[] => readdirSync(dir).flatMap((name) => {
  const path = join(dir, name);
  return statSync(path).isDirectory() ? filesUnder(path) : [path];
});

/** The packages of the tests only. */
const TEST_ONLY = ['mermaid'];

const shipped = filesUnder(src)
  .map((file) => relative(src, file).split(sep).join('/'))
  .filter((file) => /\.tsx?$/.test(file) && !/\.test\.tsx?$/.test(file) && !file.startsWith('test/'));

describe('the packages of the tests only (plan 3.1)', () => {
  it('are imported by no file of the code shipped', () => {
    const importing = shipped.filter((file) => {
      const source = readFileSync(join(src, file), 'utf8');
      return TEST_ONLY.some((name) => new RegExp(`(from\\s+|import\\s*\\(\\s*)['"]${name}(/[^'"]*)?['"]`).test(source));
    });
    expect(importing).toEqual([]);
  });

  it('are dependencies of development, never of the application', () => {
    const manifest = JSON.parse(readFileSync(join(src, '..', 'package.json'), 'utf8')) as { dependencies?: Record<string, string>; devDependencies?: Record<string, string> };
    expect(TEST_ONLY.filter((name) => manifest.dependencies?.[name] !== undefined)).toEqual([]);
    expect(TEST_ONLY.filter((name) => manifest.devDependencies?.[name] === undefined)).toEqual([]);
  });
});
