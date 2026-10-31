// testIds.test.ts
// Every native interactive element written in the components carries a stable data-testid, so that the journeys find it without a text of the interface (spec 3.5, S-3.5-04).

import { readFileSync, readdirSync, statSync } from 'node:fs';
import { join, relative, sep } from 'node:path';

const SRC = join(import.meta.dirname, '..');

const sources = (dir: string): string[] => readdirSync(dir).flatMap((name) => {
  const path = join(dir, name);
  if (statSync(path).isDirectory()) return name === 'generated' ? [] : sources(path);
  return name.endsWith('.tsx') && !name.endsWith('.test.tsx') ? [path] : [];
});

/** The native elements a person acts on: a button, a link, a field, a list, an area of text. */
const NATIVE = /<(button|a|input|select|textarea)(?=[\s/>])/g;

/** The opening tag that starts at an index, up to its `>`, the braces and the quotes of its attributes skipped. */
function openingTag(text: string, start: number): string {
  let depth = 0;
  let quote: string | null = null;
  for (let index = start + 1; index < text.length; index++) {
    const char = text[index] ?? '';
    if (quote !== null) {
      if (char === quote && text[index - 1] !== '\\') quote = null;
    } else if (char === '"' || char === "'" || char === '`') quote = char;
    else if (char === '{') depth++;
    else if (char === '}') depth--;
    else if (char === '>' && depth === 0) return text.slice(start, index + 1);
  }
  return text.slice(start);
}

/** The source of the attributes a tag receives by a spread: the control of a field of shared/ui, or a helper of its own file. */
const FIELD = readFileSync(join(SRC, 'shared/ui/Field.tsx'), 'utf8');
const TEST_ID_KEY = /'data-testid':/;

/** Whether a tag carries a data-testid: written on it, or given by the spread of an object that sets one. */
function tagged(tag: string, file: string): boolean {
  if (/\sdata-testid=/.test(tag)) return true;
  const spread = /\{\.\.\.(\w+)/.exec(tag)?.[1];
  if (spread === undefined) return false;
  return TEST_ID_KEY.test(spread === 'control' ? FIELD : file);
}

/** The tags without a data-testid, as `file:line <tag`. */
function untagged(files: readonly string[]): string[] {
  return files.flatMap((file) => {
    const text = readFileSync(file, 'utf8');
    return [...text.matchAll(NATIVE)]
      .filter((match) => !tagged(openingTag(text, match.index), text))
      .map((match) => `${relative(SRC, file).split(sep).join('/')}:${String(text.slice(0, match.index).split('\n').length)} <${match[1] ?? ''}`);
  });
}

describe('the test identifiers of the interface (spec 3.5, S-3.5-04)', () => {
  it('every native interactive element of the components carries a data-testid', () => {
    const files = sources(SRC);
    expect(files.length).toBeGreaterThan(50);
    expect(untagged(files)).toEqual([]);
  });

  it('finds the tags without one, through their braces and their quotes, and leaves the fields of shared/ui alone', () => {
    const sample = (body: string) => {
      const text = `const a = () => (${body});`;
      return [...text.matchAll(NATIVE)].map((match) => openingTag(text, match.index));
    };
    expect(sample('<a href={`/maps/${id}`} title="a > b" onClick={() => { go(x > 1); }}>x</a>')).toEqual(['<a href={`/maps/${id}`} title="a > b" onClick={() => { go(x > 1); }}>']);
    const tags = sample('<button type="button" data-testid="ok">x</button><input {...control} /><input {...field(\'wait\')} /><a href="#">x</a>');
    expect(tags.map((tag) => tagged(tag, 'const field = () => ({ value });'))).toEqual([true, true, false, false]);
    expect(tagged(tags[2] ?? '', "const field = () => ({ 'data-testid': 'use' });")).toBe(true);
    expect(sample('<abbr title="x">y</abbr><textarea rows={2} />')).toEqual(['<textarea rows={2} />']);
  });
});
