// subtitles.ts
// A sentence cut into subtitles of two lines (ADR 097): by the columns its characters take, at a space or after a punctuation, never inside a character; shared by the script, which cuts, and by the times, which read.

/** The columns of a subtitle: two lines of 42 (Netflix, French and English); a wide character takes two. */
const SUBTITLE_MAX = 84;

/** The scripts whose characters take two columns: Hangul, the CJK ideographs and syllabaries, the full-width forms. */
const WIDE = /[ᄀ-ᅟ⺀-꓏가-힣豈-﫿︰-﹏＀-｠￠-￦]/u;

/** The punctuation of the scripts written without spaces: a piece may end right after one. */
const FULL_WIDTH_SIGN = /[、。，；：！？]/u;

const LETTER = /\p{L}/u;

/** The columns of one character (a code point): two for a wide script and beyond the basic plane (rare ideographs, pictographs). */
const columnsOf = (character: string): number => ((character.codePointAt(0) ?? 0) > 0xffff || WIDE.test(character) ? 2 : 1);

/** The columns a text takes on the screen. */
export function widthOf(text: string): number {
  let width = 0;
  for (const character of text) width += columnsOf(character);
  return width;
}

/** How many of the first characters fit in the columns given: one at least, so that a cut always moves on. */
function fitting(characters: readonly string[], max: number): number {
  let width = 0;
  let count = 0;
  for (const character of characters) {
    width += columnsOf(character);
    if (width > max) break;
    count += 1;
  }
  return Math.max(1, count);
}

/** True for the point of an initial or of a one-letter abbreviation (« M. Martin », « z. B. »): no place to cut. */
const afterInitial = (head: readonly string[], index: number): boolean =>
  head[index] === '.' && LETTER.test(head[index - 1] ?? '') && (index < 2 || head[index - 2] === ' ');

/** The last place a piece may end on a punctuation: a sign followed by a space, or a full-width sign; -1 without one. */
function lastSign(head: readonly string[]): number {
  for (let index = head.length - 2; index >= 0; index -= 1) {
    const character = head[index] ?? '';
    if (FULL_WIDTH_SIGN.test(character)) return index;
    if ([',', ';', ':', '.', '!', '?'].includes(character) && head[index + 1] === ' ' && !afterInitial(head, index)) return index;
  }
  return -1;
}

/**
 * A sentence cut into subtitles of two lines at most: after a punctuation when one stands in the second half of the
 * piece, else at a space, a word longer than a piece cut where it must; always between two characters, never inside
 * one written on two code units.
 */
export function chunksOf(line: string, max = SUBTITLE_MAX): string[] {
  const pieces: string[] = [];
  let rest = Array.from(line.trim());
  while (widthOf(rest.join('')) > max) {
    const fit = fitting(rest, max);
    const head = rest.slice(0, fit + 1);
    const sign = lastSign(head);
    const space = head.lastIndexOf(' ');
    const cut = sign >= fit * 0.4 ? sign + 1 : space > 0 ? space : fit;
    pieces.push(rest.slice(0, cut).join('').trim());
    rest = Array.from(rest.slice(cut).join('').trim());
  }
  return rest.length === 0 ? pieces : [...pieces, rest.join('')];
}
