// route.test.ts
// The holes of the mask of the light of a passage (essai J3 of 3.2): each step widened by the outer ring a step shown wears, so that the light is born at the outer frame of a step, never on its rings.

import { describe, expect, it } from 'vitest';
import { PRESENTED_RING } from '../../model/geometry/constants.ts';
import { lightHoles } from './route.ts';

describe('the holes of the light of a passage', () => {
  it('are the boxes of the steps widened by the outer ring of a step shown, on every side', () => {
    const holes = lightHoles([{ x: 100, y: 50, width: 200, height: 80 }, { x: 400, y: 50, width: 96, height: 96 }]);
    const ring = PRESENTED_RING.outer;
    expect(holes).toEqual([
      { x: 100 - ring, y: 50 - ring, width: 200 + 2 * ring, height: 80 + 2 * ring },
      { x: 400 - ring, y: 50 - ring, width: 96 + 2 * ring, height: 96 + 2 * ring },
    ]);
    // The outer ring is the light gap then the ink of the lane, as the stage draws it.
    expect([PRESENTED_RING.gap, PRESENTED_RING.outer]).toEqual([3, 6]);
  });
});
