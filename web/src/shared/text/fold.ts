// fold.ts
// The fold of a text for a search, the same rule as the host's SearchText.cs (ADR 110 point 3), held equal by the table of cases web/src/test/oracle/search-fold.json: accents, capitals and ligatures do not matter, the place found is given in the text of origin; the words of a query cut as the host cuts them (R9).

/** The place of a word found in a text of origin, in UTF-16 units. */
export interface TextPlace {
  readonly start: number;
  readonly length: number;
}

/** The letters the decomposition leaves whole and the fold spells otherwise (after the lower case). */
const REPLACED: ReadonlyMap<number, string> = new Map([
  [0x00df, 'ss'], [0x0153, 'oe'], [0x00e6, 'ae'], [0x00f8, 'o'], [0x0142, 'l'], [0x0111, 'd'], [0x0131, 'i'], [0x03c2, 'σ'],
  [0x2018, "'"], [0x2019, "'"], [0x2013, '-'], [0x2014, '-'],
]);

const SPACE = /^\p{White_Space}$/u;
const MARK = /^\p{M}$/u;

/** A mark of the accents the fold removes: U+0300 to U+036F only (the dakuten U+3099 is kept: が is not か). */
const isRemovedMark = (code: number): boolean => code >= 0x0300 && code <= 0x036f;

/** The folded form of one character: nothing for a mark removed. */
function foldedOf(character: string): string {
  let folded = '';
  for (const part of character.normalize('NFKD')) {
    for (const lower of part.toLowerCase()) {
      const code = lower.codePointAt(0) ?? 0;
      if (isRemovedMark(code)) continue;
      if (SPACE.test(lower)) folded += ' ';
      else folded += REPLACED.get(code) ?? lower;
    }
  }
  return folded;
}

/** A text folded, with for each folded UTF-16 unit the place of origin (start, end) of the character it comes from. */
interface FoldedText {
  readonly text: string;
  readonly starts: readonly number[];
  readonly ends: readonly number[];
}

function foldWithPlaces(text: string): FoldedText {
  let folded = '';
  const starts: number[] = [];
  const ends: number[] = [];
  let [lastFrom, lastEnd] = [-1, -1];
  let index = 0;
  for (const character of text) {
    const end = index + character.length;
    const part = foldedOf(character);
    if (part.length > 0) {
      for (let unit = 0; unit < part.length; unit += 1) {
        starts.push(index);
        ends.push(end);
      }
      [lastFrom, lastEnd] = [folded.length, end];
      folded += part;
    } else if (lastFrom >= 0 && lastEnd === index) {
      // A mark removed that directly follows a letter belongs to its place.
      for (let unit = lastFrom; unit < ends.length; unit += 1) ends[unit] = end;
      lastEnd = end;
    }
    index = end;
  }
  return { text: folded, starts, ends };
}

/** The fold of a text: the single rule of the search of the interface. */
export function foldText(text: string): string {
  let folded = '';
  for (const character of text) folded += foldedOf(character);
  return folded;
}

/** The word of a search folded and cut of the spaces at its edges; null when nothing is left, which finds nothing. */
export function foldedWord(word: string): string | null {
  const folded = foldText(word).replace(/^ +| +$/g, '');
  return folded === '' ? null : folded;
}

/** A mark kept by the fold (the dakuten, the marks of other scripts) belongs to the letter before it. */
function isKeptMark(folded: string, index: number): boolean {
  const code = folded.codePointAt(index);
  return code !== undefined && MARK.test(String.fromCodePoint(code));
}

/** The first place of a word already folded in a text, or null: a place that starts or ends between a letter and a mark kept is passed over. */
export function findFoldedWord(text: string, word: string): TextPlace | null {
  // The fold without places first: most texts do not hold the word.
  if (!foldText(text).includes(word)) return null;
  const folded = foldWithPlaces(text);
  let from = 0;
  while (from <= folded.text.length - word.length) {
    const at = folded.text.indexOf(word, from);
    if (at < 0) return null;
    const end = at + word.length;
    const cutsBefore = at > 0 && isKeptMark(folded.text, at);
    const cutsAfter = end < folded.text.length && isKeptMark(folded.text, end);
    if (!cutsBefore && !cutsAfter) {
      const start = folded.starts[at] ?? 0;
      return { start, length: (folded.ends[end - 1] ?? start) - start };
    }
    from = at + 1;
  }
  return null;
}


const GRAPHEMES = new Intl.Segmenter(undefined, { granularity: 'grapheme' });

/** The characters of a part as a person counts them (a letter and its accent, an ideogram: one each). */
const charactersIn = (part: string): number => Array.from(GRAPHEMES.segment(part)).length;

/**
 * The words of a query, folded (retouch R9, the rule of `SearchTerms` on the host, held equal by the queries of the shared
 * table): the text cut at its spaces (every white space of Unicode), each part folded as a word, a word repeated kept
 * once; a word of one character is dropped when a longer word is there, kept when all are of one character. Null when
 * no word is left, which finds nothing.
 */
export function foldedTerms(query: string): readonly string[] | null {
  const words: { readonly folded: string; readonly characters: number }[] = [];
  for (const part of query.split(/\p{White_Space}+/u)) {
    const folded = foldedWord(part);
    if (folded !== null && !words.some((seen) => seen.folded === folded)) words.push({ folded, characters: charactersIn(part) });
  }
  if (words.length === 0) return null;
  const longer = words.some((word) => word.characters >= 2);
  return words.filter((word) => !longer || word.characters >= 2).map((word) => word.folded);
}

/** Whether a text holds each word of a query, in any order, all in this one text. */
export const holdsTerms = (text: string, terms: readonly string[]): boolean => terms.every((term) => findFoldedWord(text, term) !== null);
