// laneLines.test.ts
// The number of lines a lane name may take in the overview of a presentation (F-209): as many as its band holds, three at most, none under one line.

import { describe, expect, it } from 'vitest';
import { laneLines } from './laneLines.ts';

describe('the lines of a lane name (F-209)', () => {
  it('follows the height of the band, three at most', () => {
    expect([laneLines(200, false), laneLines(60, false), laneLines(54, false), laneLines(40, false), laneLines(36, false), laneLines(20, false)]).toEqual([3, 3, 3, 2, 2, 1]);
  });

  it('is none under one line high, the name masked rather than cut in the middle of a line', () => {
    expect([laneLines(17.9, false), laneLines(10, false), laneLines(0, false), laneLines(-4, false)]).toEqual([0, 0, 0, 0]);
  });

  it('counts a taller line for the lane lit: 36 px give one line, 41 give two', () => {
    expect([laneLines(36, true), laneLines(20, true), laneLines(18, true), laneLines(41, true), laneLines(62, true)]).toEqual([1, 0, 0, 2, 3]);
  });
});
