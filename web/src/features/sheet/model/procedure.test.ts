// procedure.test.ts
// The lines of the procedure: at least one shown, a line inserted, removed, moved by a button or by the handle (annex B 4, AR-89).

import { EMPTY_LINE, insertLine, isBlank, moveLine, removeLine, shownLines } from './procedure.ts';

const line = (text: string) => ({ text, tool: '', nature: null });
const texts = (lines: readonly { text: string }[]) => lines.map((one) => one.text);

describe('the lines of the procedure (B-4-29, B-4-33, B-4-35, B-4-36, AR-89)', () => {
  it('shows one empty line when the procedure has none, the lines as they are otherwise', () => {
    const lines = [line('a')];
    expect([shownLines([]), shownLines(lines)]).toEqual([[EMPTY_LINE], lines]);
  });

  it('inserts an empty line at a place, at the end included', () => {
    const lines = [line('a'), line('b')];
    expect([texts(insertLine(lines, 1)), texts(insertLine(lines, 2)), texts(insertLine(lines, 0))]).toEqual([['a', '', 'b'], ['a', 'b', ''], ['', 'a', 'b']]);
  });

  it('removes a line', () => {
    expect([texts(removeLine([line('a'), line('b'), line('c')], 1)), texts(removeLine([line('a')], 0))]).toEqual([['a', 'c'], []]);
  });

  it('moves a line up, down or several places, and leaves the list alone for a move out of it or onto itself', () => {
    const lines = [line('a'), line('b'), line('c'), line('d')];
    expect([texts(moveLine(lines, 1, 0)), texts(moveLine(lines, 1, 2)), texts(moveLine(lines, 0, 3)), texts(moveLine(lines, 3, 1))])
      .toEqual([['b', 'a', 'c', 'd'], ['a', 'c', 'b', 'd'], ['b', 'c', 'd', 'a'], ['a', 'd', 'b', 'c']]);
    expect([moveLine(lines, 2, 2), moveLine(lines, 0, -1), moveLine(lines, 3, 4), moveLine(lines, -1, 0)]).toEqual([lines, lines, lines, lines]);
  });

  it('a line is blank without text, tool nor nature', () => {
    expect([isBlank(EMPTY_LINE), isBlank(line('a')), isBlank({ text: '', tool: 'ERP', nature: null }), isBlank({ text: '', tool: '', nature: 'check' })])
      .toEqual([true, false, false, false]);
  });
});
