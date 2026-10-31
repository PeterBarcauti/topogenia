// profileUse.test.ts
// The profile has no screen: the name in the header and the language kept by the server; the first visit, e-mail and roles stay unused (annex B 1.8).

import { readFileSync, readdirSync, statSync } from 'node:fs';
import { join, relative, sep } from 'node:path';
import { routeOf } from '../router.ts';

const src = join(import.meta.dirname, '../..');

const sources = (dir: string): string[] => readdirSync(dir).flatMap((name) => {
  const path = join(dir, name);
  return statSync(path).isDirectory() ? sources(path) : [path];
});

/** The modules of the interface, the generated client and the tests aside. */
const shipped = sources(src)
  .filter((file) => /\.tsx?$/.test(file) && !/\.test\.tsx?$/.test(file))
  .map((file) => relative(src, file).split(sep).join('/'))
  .filter((file) => !file.startsWith('api/generated/') && !file.startsWith('test/'));

describe('the profile (B-1.8-01, B-1.8-02)', () => {
  it('has no screen of its own', () => {
    expect(['/profile', '/me', '/settings'].map((path) => routeOf(path).screen)).toEqual(['not-found', 'not-found', 'not-found']);
  });

  it('only its name and its language are shown; the first visit, the e-mail and the roles are read by no module', () => {
    const readers = shipped.filter((file) => /\.(first_visit_done|email|roles)\b/.test(readFileSync(join(src, file), 'utf8')));
    expect(readers).toEqual([]);
    expect(shipped.filter((file) => readFileSync(join(src, file), 'utf8').includes('display_name'))).toContain('app/Header.tsx');
  });
});
