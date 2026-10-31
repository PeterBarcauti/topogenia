// iconCharacters.test.ts
// Spec 9.5, S-9.5-02: no Unicode character stands for an icon, in the code nor in the texts of the languages; the icons are those of Lucide, embedded.

import { readFileSync, readdirSync, statSync } from 'node:fs';
import { join, relative, sep } from 'node:path';
import ts from 'typescript';

const src = join(import.meta.dirname, '..');

const filesUnder = (dir: string): string[] => readdirSync(dir).flatMap((name) => {
  const path = join(dir, name);
  return statSync(path).isDirectory() ? filesUnder(path) : [path];
});

const shipped = filesUnder(src)
  .map((file) => relative(src, file).split(sep).join('/'))
  .filter((file) => !/\.test\.tsx?$/.test(file) && !file.startsWith('test/') && !file.startsWith('api/generated/'));

/**
 * The blocks of pictographic signs (the 1.x drew its tools with a raised hand, a dotted square and an arrow to a bar):
 * arrows, mathematical dots, technical signs, enclosed signs, box drawing and shapes, symbols, dingbats, arrows and
 * symbols, emoji (and, apart, the selector that asks for one). Written by their code points: the signs themselves have no
 * place in a source.
 */
const SIGNS = '\u2190-\u21FF\u22EE-\u22F1\u2300-\u23FF\u2460-\u24FF\u2500-\u27BF\u27F0-\u27FF\u2900-\u297F\u2B00-\u2BFF\u{1F000}-\u{1FAFF}';
const PICTOGRAPH = new RegExp(`[${SIGNS}]`, 'u');
const EMOJI = /[\uFE0F\u{1F000}-\u{1FAFF}]/u;

/** The literal texts of a script, comments aside: strings, templates, JSX texts. */
function scriptTexts(source: string, file: string): string[] {
  const tree = ts.createSourceFile(file, source, ts.ScriptTarget.Latest, true, file.endsWith('.tsx') ? ts.ScriptKind.TSX : ts.ScriptKind.TS);
  const texts: string[] = [];
  const visit = (node: ts.Node): void => {
    if (ts.isStringLiteral(node) || ts.isNoSubstitutionTemplateLiteral(node) || ts.isJsxText(node)) texts.push(node.text);
    else if (ts.isTemplateExpression(node)) texts.push(node.head.text, ...node.templateSpans.map((span) => span.literal.text));
    ts.forEachChild(node, visit);
  };
  visit(tree);
  return texts;
}

/** The signs written as icons in a source: in a script, any literal text holding one; in a style sheet, any `content`. */
function iconCharacters(source: string, file: string): string[] {
  if (file.endsWith('.css')) return [...source.replace(/\/\*[\s\S]*?\*\//g, '').matchAll(/content\s*:\s*([^;}]+)/g)].map((m) => m[1] ?? '').filter((value) => PICTOGRAPH.test(value));
  return scriptTexts(source, file).filter((text) => PICTOGRAPH.test(text) || EMOJI.test(text)).map((text) => `${file}: ${text.trim()}`);
}

/**
 * The texts of a language file that carry a sign as an icon: a text made of signs, or that begins or ends with one, or
 * any emoji. An arrow between two words of a sentence (a name, an arrow, a code; Alt and an arrow key) is typography.
 */
function iconTexts(messages: unknown, path = ''): string[] {
  if (typeof messages === 'string') {
    const text = messages.trim();
    const edge = new RegExp(`^[${SIGNS}]|[${SIGNS}]$`, 'u');
    const lettered = /\p{L}/u.test(text.replace(/\{\{[^}]*\}\}/g, 'x'));
    return (PICTOGRAPH.test(text) && (!lettered || edge.test(text))) || EMOJI.test(text) ? [`${path}: ${text}`] : [];
  }
  if (typeof messages !== 'object' || messages === null) return [];
  return Object.entries(messages).flatMap(([key, value]) => iconTexts(value, path === '' ? key : `${path}.${key}`));
}

describe('no Unicode character as an icon (spec 9.5, S-9.5-02)', () => {
  it('no script nor style sheet of the interface writes a sign as an icon', () => {
    const sources = shipped.filter((file) => /\.(tsx?|css)$/.test(file));
    expect(sources.length).toBeGreaterThan(150);
    expect(sources.flatMap((file) => iconCharacters(readFileSync(join(src, file), 'utf8'), file))).toEqual([]);
  });

  it('no text of a language file carries a sign as an icon', () => {
    const languages = shipped.filter((file) => file.startsWith('shared/i18n/locales/') && file.endsWith('.json'));
    expect(languages.length).toBeGreaterThan(1);
    expect(languages.flatMap((file) => iconTexts(JSON.parse(readFileSync(join(src, file), 'utf8')) as unknown, file))).toEqual([]);
  });

  it('the icons of the interface are drawn by Lucide, whose components the application bundles', () => {
    expect(readFileSync(join(src, 'shared/ui/Icon.tsx'), 'utf8')).toMatch(/import type \{ LucideIcon \} from 'lucide-react'/);
    const icons = shipped.filter((file) => file.endsWith('.tsx') && /from 'lucide-react'/.test(readFileSync(join(src, file), 'utf8')));
    expect(icons.length).toBeGreaterThan(15);
  });

  it('detects a sign written as an icon, and lets typography and comments pass', () => {
    expect(iconCharacters('const a = <button>\u22EF</button>;', 'a.tsx')).toHaveLength(1);
    expect(iconCharacters("const tool = { label: '\u270B Main' };", 'a.ts')).toHaveLength(1);
    expect(iconCharacters('const a = `\u21E4 ${x}`;', 'a.ts')).toHaveLength(1);
    expect(iconCharacters('.done::before { content: "\u2713"; }', 'a.css')).toHaveLength(1);
    expect(iconCharacters("// the \u22EF of the menu\nconst a = t('k'); const b = '170 \u00D7 64 \u00B7 \u00AB x \u00BB \u2026';", 'a.ts')).toEqual([]);
    expect(iconTexts({ a: { b: '\u2B1A S\u00E9lection', c: '\u21E4', d: 'Valid\u00E9e \u2713', e: '\u{1F44D}' } })).toHaveLength(4);
    expect(iconTexts({ a: '{{name}} \u2192 {{code}}', b: 'Alt+\u2191 et Alt+\u2193 d\u00E9placent la ligne.', c: 'Carte \u00AB X \u00BB \u2014 1 \u00D7 2' })).toEqual([]);
  });
});
