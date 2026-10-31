// languages.test.ts
// NF12, S-NF-17, B-8.1-20: a language of the interface is one JSON file in locales/ and one line of the configuration; no list nor code of a language written elsewhere.

import { readFileSync, readdirSync, statSync } from 'node:fs';
import { join, relative, sep } from 'node:path';
import ts from 'typescript';
import { BUNDLED_LANGUAGES, DEFAULT_LANGUAGE } from '../shared/i18n/i18n.ts';

const src = join(import.meta.dirname, '..');
const LOCALES = join(src, 'shared/i18n/locales');

const filesUnder = (dir: string): string[] => readdirSync(dir).flatMap((name) => {
  const path = join(dir, name);
  return statSync(path).isDirectory() ? filesUnder(path) : [path];
});

const scripts = filesUnder(src)
  .map((file) => relative(src, file).split(sep).join('/'))
  .filter((file) => /\.tsx?$/.test(file) && !/\.test\.tsx?$/.test(file) && !file.startsWith('test/') && !file.startsWith('api/generated/'));

/** The language files of locales/: their names are the codes of the languages. */
const files = readdirSync(LOCALES).filter((name) => name.endsWith('.json')).map((name) => name.replace(/\.json$/, ''));

/** The codes of languages a script writes: a string, or a literal type (the cast of the 1.x), equal to the code of a language file. */
function writtenLanguages(source: string, file: string, codes: readonly string[]): string[] {
  const tree = ts.createSourceFile(file, source, ts.ScriptTarget.Latest, true, file.endsWith('.tsx') ? ts.ScriptKind.TSX : ts.ScriptKind.TS);
  const found: string[] = [];
  const line = (node: ts.Node) => String(tree.getLineAndCharacterOfPosition(node.getStart(tree)).line + 1);
  const visit = (node: ts.Node): void => {
    if ((ts.isStringLiteral(node) || ts.isNoSubstitutionTemplateLiteral(node)) && codes.includes(node.text)) found.push(`${file}:${line(node)} '${node.text}'`);
    ts.forEachChild(node, visit);
  };
  visit(tree);
  return found;
}

describe('one language, one file and one line of the configuration (NF12, S-NF-17, B-8.1-20)', () => {
  it('the languages of the interface are the files of locales/, read by one pattern, French the default', () => {
    expect([...BUNDLED_LANGUAGES].sort()).toEqual([...files].sort());
    expect(readFileSync(join(src, 'shared/i18n/i18n.ts'), 'utf8')).toMatch(/import\.meta\.glob<Messages>\('\.\/locales\/\*\.json'/);
    expect(files).toContain(DEFAULT_LANGUAGE);
  });

  it('no script but the configuration of i18n writes the code of a language, nor casts a language to a list of them', () => {
    expect(scripts.length).toBeGreaterThan(150);
    const found = scripts.flatMap((file) => writtenLanguages(readFileSync(join(src, file), 'utf8'), file, files));
    expect(found.filter((place) => !place.startsWith('shared/i18n/i18n.ts:'))).toEqual([]);
    expect(found.map((place) => place.replace(/:\d+ /, ' '))).toEqual([`shared/i18n/i18n.ts '${DEFAULT_LANGUAGE}'`]);
  });

  it('every language file has the keys of the default one, the plural forms of its language and the same variables: a new file is complete', () => {
    const entriesOf = (node: object, prefix = ''): [string, string][] => Object.entries(node)
      .flatMap(([key, value]): [string, string][] => (typeof value === 'object' && value !== null ? entriesOf(value as object, `${prefix}${key}.`) : [[`${prefix}${key}`, String(value)]]));
    const read = (code: string) => new Map(entriesOf(JSON.parse(readFileSync(join(LOCALES, `${code}.json`), 'utf8')) as object));
    const variables = (text: string) => [...new Set([...text.matchAll(/{{\s*([^}]+?)\s*}}/g)].map((match) => match[1]))].sort().join();
    const french = read(DEFAULT_LANGUAGE);
    // A plural is a key whose `_other` form is in French (`too_many` beside `too_large` is a plain key).
    const baseOf = (key: string) => /^(.*)_(?:zero|one|two|few|many|other)$/.exec(key)?.[1];
    const plurals = new Set([...french.keys()].map(baseOf).filter((base): base is string => base !== undefined && french.has(`${base}_other`)));
    const isPlural = (key: string) => plurals.has(baseOf(key) ?? '');
    for (const code of files) {
      const mine = read(code);
      const rules = new Intl.PluralRules(code);
      const forms = new Set(['other', ...Array.from({ length: 200 }, (_, count) => rules.select(count))]);
      const problems = [
        ...[...french].filter(([key]) => !isPlural(key)).flatMap(([key, text]) => {
          const translated = mine.get(key);
          if (translated === undefined) return [`missing ${key}`];
          return variables(translated) === variables(text) ? [] : [`variables of ${key}`];
        }),
        ...[...plurals].flatMap((base) => [...forms].filter((form) => !mine.has(`${base}_${form}`)).map((form) => `missing ${base}_${form}`)),
        ...[...mine.keys()].filter((key) => !french.has(key) && !isPlural(key)).map((key) => `unknown ${key}`),
      ];
      expect([code, problems]).toEqual([code, []]);
    }
  });

  it('a plural form a language needs and its file lacks is found, as a variable lost', () => {
    const forms = (code: string) => new Set(Array.from({ length: 200 }, (_, count) => new Intl.PluralRules(code).select(count)));
    expect(forms('pl')).toEqual(new Set(['one', 'few', 'many']));
    expect(forms('ja')).toEqual(new Set(['other']));
  });

  it('detects a code of a language written in a script, and a cast to a list of languages', () => {
    expect(writtenLanguages("const english = language === 'en';", 'a.ts', ['fr', 'en'])).toEqual(["a.ts:1 'en'"]);
    expect(writtenLanguages("const shown = language as 'fr' | 'en';", 'a.ts', ['fr', 'en'])).toEqual(["a.ts:1 'fr'", "a.ts:1 'en'"]);
    expect(writtenLanguages('const a = <p lang="fr">{t(\'k\')}</p>;', 'a.tsx', ['fr', 'en'])).toHaveLength(1);
    expect(writtenLanguages("const a = i18n.language; const b = t('languages.fr'); type Side = 'top' | 'left';", 'a.ts', ['fr', 'en'])).toEqual([]);
  });
});
