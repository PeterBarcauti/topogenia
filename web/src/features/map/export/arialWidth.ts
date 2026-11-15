// arialWidth.ts
// The widths of the characters of Arial, the font every diagram of the exports is drawn in (mermaid.ts): a text measured the same way on every computer, so that the same map always gives the same diagram (plan 3.1, rendu Mermaid).

import type { TextMeasure } from '../model/textSize.ts';

/** The advance of the characters of Arial, in thousandths of an em, by groups of the same width (its metrics are those of Helvetica). */
const GROUPS: readonly (readonly [number, string])[] = [
  [222, "ijl'‘’"], [260, '|'], [278, ' ft!,./:;I[\\]·'], [333, 'r()-`{}“”'], [469, '*^"'], [500, 'cksvxyzJ'],
  [556, 'abdeghnopqu0123456789$#?_L«»–'], [584, '+<=>~'], [611, 'FTZ'], [667, 'ABEKPSVXY&'], [722, 'CDHNRUw'], [778, 'GOQ'],
  [833, 'Mm'], [889, '%æ'], [944, 'Wœ'], [1000, '…—ÆŒ'], [1015, '@'],
];

/** The characters of a text as a reader sees them: a letter with its accents, a cluster of emoji, one each. */
const SEGMENTER = new Intl.Segmenter(undefined, { granularity: 'grapheme' });

export const graphemes = (text: string): string[] => Array.from(SEGMENTER.segment(text), (part) => part.segment);

const WIDTHS = new Map(GROUPS.flatMap(([width, characters]) => graphemes(characters).map((character) => [character, width] as const)));

/** A character of a wide script (Chinese, Japanese, Korean, the forms of full width): one em, and a line may break before it. */
export const WIDE_CHARACTER = /[\u1100-\u11FF\u2E80-\u9FFF\uA960-\uA97F\uAC00-\uD7FF\uF900-\uFAFF\uFE30-\uFE4F\uFF00-\uFFEF]/u;

/**
 * The advance of a character: its own; a letter with an accent, that of its letter; a character of a wide script, one em;
 * another letter (Cyrillic, Greek...), on the wide side of the letters of Arial, so that a text is never measured too short.
 */
function advance(character: string): number {
  const known = WIDTHS.get(character) ?? WIDTHS.get(character.normalize('NFD').charAt(0));
  if (known !== undefined) return known;
  if (WIDE_CHARACTER.test(character)) return 1000;
  return character === character.toLowerCase() ? 611 : 778;
}

/** The width of a text written in Arial, in pixels. */
export const arialMeasure: TextMeasure = (text, font) => (graphemes(text).reduce((sum, character) => sum + advance(character), 0) * font.size) / 1000;
