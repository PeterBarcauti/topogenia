// translationKeys.test.ts
// S-D12-02, NF12: every key of the language files a component names exists in each language file; a key mistyped would show itself on the screen.

import { readFileSync, readdirSync, statSync } from 'node:fs';
import { join, relative, sep } from 'node:path';
import ts from 'typescript';

const src = join(import.meta.dirname, '..');
const LOCALES = join(src, 'shared/i18n/locales');

const filesUnder = (dir: string): string[] => readdirSync(dir).flatMap((name) => {
  const path = join(dir, name);
  return statSync(path).isDirectory() ? filesUnder(path) : [path];
});

const scripts = filesUnder(src)
  .map((file) => relative(src, file).split(sep).join('/'))
  .filter((file) => /\.tsx?$/.test(file) && !/\.test\.tsx?$/.test(file) && !file.startsWith('test/') && !file.startsWith('api/generated/'));

type Texts = { readonly [key: string]: string | Texts };
const languages = readdirSync(LOCALES).filter((name) => name.endsWith('.json'))
  .map((name) => [name, JSON.parse(readFileSync(join(LOCALES, name), 'utf8')) as Texts] as const);

/** Whether a key names a text, a group of texts, or a text with plural forms (`key_one`, `key_other`) of a language file. */
function exists(texts: Texts, key: string): boolean {
  const parts = key.split('.');
  const last = parts.pop() ?? '';
  let group: Texts | string | undefined = texts;
  for (const part of parts) group = typeof group === 'object' ? group[part] : undefined;
  if (typeof group !== 'object') return false;
  return last in group || `${last}_one` in group || `${last}_other` in group;
}

/** The strings of a script written like a key of the language files: a first word that is a section of them, then a dot. */
function keysOf(source: string, file: string, sections: ReadonlySet<string>): { key: string; line: number }[] {
  const tree = ts.createSourceFile(file, source, ts.ScriptTarget.Latest, true, file.endsWith('.tsx') ? ts.ScriptKind.TSX : ts.ScriptKind.TS);
  const found: { key: string; line: number }[] = [];
  const visit = (node: ts.Node): void => {
    if ((ts.isStringLiteral(node) || ts.isNoSubstitutionTemplateLiteral(node)) && /^[a-z][a-z_]*(\.[a-z0-9_]+)+$/.test(node.text)
      && sections.has(node.text.split('.')[0] ?? '')) found.push({ key: node.text, line: tree.getLineAndCharacterOfPosition(node.getStart(tree)).line + 1 });
    ts.forEachChild(node, visit);
  };
  visit(tree);
  return found;
}

describe('the keys of the language files named by the components (S-D12-02, NF12)', () => {
  it('every key a component names is in each language file', () => {
    // The scripts are read once for the eighteen languages (ADR 083): the sections are those of any language file.
    const sections = new Set(languages.flatMap(([, texts]) => Object.keys(texts)));
    const named = scripts.flatMap((file) => keysOf(readFileSync(join(src, file), 'utf8'), file, sections).map((place) => ({ ...place, file })));
    const missing = languages.flatMap(([name, texts]) => named
      .filter(({ key }) => !exists(texts, key))
      .map(({ key, line, file }) => `${name}: ${file}:${String(line)} '${key}'`));
    expect(missing).toEqual([]);
  });

  it('finds a key mistyped, in a call, a property or an attribute, and leaves the plural forms and the other strings alone', () => {
    const texts: Texts = { sheet: { document: { open: 'Ouvrir', count_one: 'un', count_other: 'plusieurs' } }, app: { name: 'Nom' } };
    const source = [
      "t('sheet.document.opne');",
      "const entry = { label: 'app.nmae' };",
      "const view = <Menu label=\"sheet.document.open\" />;",
      "t('sheet.document.count', { count: 2 });",
      "import { x } from './map.ts'; const file = 'export.pdf'; const word = 'app';",
    ].join('\n');
    const keys = keysOf(source, 'probe.tsx', new Set(Object.keys(texts)));
    expect(keys.filter(({ key }) => !exists(texts, key)).map(({ key }) => key)).toEqual(['sheet.document.opne', 'app.nmae']);
    expect(keys.map(({ key }) => key)).toEqual(['sheet.document.opne', 'app.nmae', 'sheet.document.open', 'sheet.document.count']);
  });
});
