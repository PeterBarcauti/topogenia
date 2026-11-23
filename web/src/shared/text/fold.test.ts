// fold.test.ts
// The fold of the interface held equal to the host's by the table of cases they share (ADR 110 point 3): every case is found or not, at its place of origin; every query of several words holds or not.

import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { findFoldedWord, foldedTerms, foldedWord, foldText, holdsTerms, type TextPlace } from './fold.ts';

interface Case {
  readonly name: string;
  readonly text: string;
  readonly word: string;
  readonly found: boolean;
  readonly start: number | null;
  readonly length: number | null;
}

/** A query of several words (R9): whether every word is in the text; its marks are the host's alone, not read here. */
interface Query {
  readonly name: string;
  readonly text: string;
  readonly query: string;
  readonly found: boolean;
}

const table = JSON.parse(readFileSync(join(import.meta.dirname, '../../test/oracle/search-fold.json'), 'utf8')) as {
  readonly cases: readonly Case[];
  readonly queries: readonly Query[];
};

/** The first place of a word in a text, both folded as the host folds them (« fact » finds « Facture »), or null. */
const findFolded = (text: string, word: string): TextPlace | null => {
  const wanted = foldedWord(word);
  return wanted === null ? null : findFoldedWord(text, wanted);
};

/** Whether a query holds in a text, by the cut of the words of the interface. */
const holds = (text: string, query: string): boolean => {
  const terms = foldedTerms(query);
  return terms !== null && holdsTerms(text, terms);
};

describe('the fold of the search', () => {
  it('the table is read whole', () => {
    expect(table.cases.length).toBeGreaterThan(50);
  });

  it.each(table.cases.map((entry) => [entry.name, entry] as const))('every case of the shared table folds as the host folds it: %s', (_name, entry) => {
    const place = findFolded(entry.text, entry.word);
    if (entry.found) expect(place).toEqual({ start: entry.start, length: entry.length });
    else expect(place).toBeNull();
  });

  it.each(table.queries.map((entry) => [entry.name, entry] as const))('every query of the shared table holds or not as on the host: %s', (_name, entry) => {
    expect(table.queries.length).toBeGreaterThanOrEqual(10);
    expect(holds(entry.text, entry.query)).toBe(entry.found);
  });

  it('a query of one word (no space inside) holds where the word alone is found, for every case of the table', () => {
    const single = table.cases.filter((entry) => !/\p{White_Space}/u.test(entry.word.trim()));
    expect(single.length).toBeGreaterThan(40);
    for (const entry of single) expect([entry.name, holds(entry.text, entry.word)]).toEqual([entry.name, findFolded(entry.text, entry.word) !== null]);
  });

  it('folds rune by rune: accents and capitals dropped, ligatures spelled out, spaces made plain, the dakuten kept', () => {
    expect(foldText('Équipe')).toBe('equipe');
    expect(foldText('Straße cœur')).toBe('strasse coeur');
    expect(foldText('a b\tc')).toBe('a b c');
    expect(foldText('が')).not.toBe(foldText('か'));
  });
});
