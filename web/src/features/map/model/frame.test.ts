// frame.test.ts
// The frame of a dissolution: its box around the steps of the frame with the margins 14 and 26, the steps of other flows it covers (annex B 2.9, B-3.2-17).

import { frameOf } from '../../../test/maps.ts';
import { frameBox } from './frame.ts';

const box = (x: number, y: number) => ({ x, y, width: 170, height: 64 });

describe('the frame of a dissolution (B-2.9-17 to B-2.9-19, B-3.2-17)', () => {
  const boxes = new Map([['a', box(250, 28)], ['b', box(470, 148)], ['inside', box(250, 148)], ['edge', box(600, 28)], ['touching', box(654, 28)], ['above', box(250, -100)]]);

  it('holds the boxes of its steps, 14 off their sides and the bottom, 26 above for the label', () => {
    expect(frameBox(frameOf(['a', 'b']), boxes)?.box).toEqual({ x: 250 - 14, y: 28 - 26, width: 470 + 170 + 14 - 236, height: 148 + 64 + 14 - 2 });
  });

  it('greys the steps of other flows that it covers, even by an edge, and those only', () => {
    expect([...(frameBox(frameOf(['a', 'b']), boxes)?.outside ?? [])]).toEqual(['inside', 'edge']);
  });

  it('is absent without an open frame, or when none of its steps is on the map', () => {
    expect([frameBox(null, boxes), frameBox(frameOf(['gone']), boxes)]).toEqual([null, null]);
  });

  it('a step of the frame that is gone from the map leaves it around the others', () => {
    expect(frameBox(frameOf(['a', 'gone']), boxes)?.box).toEqual({ x: 236, y: 2, width: 198, height: 104 });
  });
});
