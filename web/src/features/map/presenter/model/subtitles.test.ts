// subtitles.test.ts
// A sentence cut into subtitles of two lines (ADR 097, ADR 099): at a space or after a punctuation for the alphabets, after a full-width punctuation for the scripts written without spaces, by the columns a character takes, never inside a character; the time a sentence stays follows its width.

import { chunksOf, widthOf } from './subtitles.ts';
import { lineMs } from './timing.ts';

describe('a sentence cut into subtitles (Netflix: 42 characters a line, two lines)', () => {
  it('keeps a sentence of two lines whole', () => {
    expect(chunksOf('x'.repeat(84))).toEqual(['x'.repeat(84)]);
  });

  it('cuts a longer one at a space, after a punctuation when there is one near the end of the piece', () => {
    const sentence = 'Irritant : le bon de commande arrive par courriel, il est ressaisi à la main dans le progiciel puis imprimé pour la signature du chef.';
    const pieces = chunksOf(sentence);
    expect(pieces.length).toBe(2);
    expect(pieces.every((piece) => piece.length <= 84)).toBe(true);
    expect(pieces.join(' ')).toBe(sentence);
    expect(pieces[0]?.endsWith(',')).toBe(true);
  });

  it('cuts a word longer than a piece where it must', () => {
    expect(chunksOf('y'.repeat(100))).toEqual(['y'.repeat(84), 'y'.repeat(16)]);
  });

  it('does not cut after the initial of a name or a one-letter abbreviation', () => {
    const sentence = 'Chaque lundi matin, le responsable du service envoie le tableau à M. Martin pour la validation des écarts constatés.';
    const pieces = chunksOf(sentence);
    expect(pieces.join(' ')).toBe(sentence);
    expect(pieces.some((piece) => piece.endsWith(' M.'))).toBe(false);
    // A sentence that ends on its point is still a place to cut.
    expect(chunksOf('La demande part au service des ressources humaines pour un premier contrôle. Elle revient ensuite au responsable.')[0])
      .toBe('La demande part au service des ressources humaines pour un premier contrôle.');
  });
});

describe('a script written without spaces (Japanese, Chinese): two lines of 21 wide characters', () => {
  const japanese = '申請者は休暇申請書に記入し、上長に提出します。上長は内容を確認し、問題がなければ承認して人事部に送ります。人事部は残日数を確認し、休暇台帳に記録して申請者に通知します。';

  it('counts two columns for a wide character, one for a letter', () => {
    expect([widthOf('abc'), widthOf('休暇'), widthOf('a休'), widthOf('𠀋')]).toEqual([3, 4, 3, 2]);
  });

  it('cuts after a full-width punctuation, every piece within two lines, nothing lost', () => {
    const pieces = chunksOf(japanese);
    expect(pieces.length).toBeGreaterThan(1);
    expect(pieces.join('')).toBe(japanese);
    expect(pieces.every((piece) => widthOf(piece) <= 84)).toBe(true);
    expect(pieces.slice(0, -1).every((piece) => /[、。]$/u.test(piece))).toBe(true);
  });

  it('never cuts inside a character written on two code units', () => {
    // A letter first: the columns of the pieces then fall on an odd code unit, where a cut by code units breaks a character.
    const rare = `a${'𠀋'.repeat(60)}`;
    const pieces = chunksOf(rare);
    expect(pieces.join('')).toBe(rare);
    expect(pieces.map((piece) => Array.from(piece).length)).toEqual([42, 19]);
    expect(pieces.every((piece) => Array.from(piece).every((character) => character === '𠀋' || character === 'a'))).toBe(true);
  });

  it('a sentence stays the time its width asks: 42 wide characters as long as 84 letters', () => {
    expect(lineMs('休'.repeat(42))).toBe(lineMs('x'.repeat(84)));
    expect(lineMs('休'.repeat(20))).toBeGreaterThan(lineMs('x'.repeat(20)));
  });
});
