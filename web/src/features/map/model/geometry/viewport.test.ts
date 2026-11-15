// viewport.test.ts
// The view hooked to the top of the map: the zoom range and the four bounds, the zoom kept (annex B 2.2, AR-19).

import { BOTTOM_MARGIN, INITIAL_VIEW, MAX_ZOOM, MIN_ZOOM, RIGHT_MARGIN, TOP_MARGIN } from './constants.ts';
import { boundView } from './viewport.ts';

const screen = { width: 1000, height: 600 };
const small = { width: 500, height: 240 };
const large = { width: 3000, height: 1500 };

describe('the zoom (B-2.2-01)', () => {
  it('goes from 0.2 to 2: the bounds React Flow is given by the canvas', () => {
    expect([MIN_ZOOM, MAX_ZOOM]).toEqual([0.2, 2]);
  });
});

describe('the bounded view (B-2.2-04 to B-2.2-08)', () => {
  it('never lets the first lane go down under the top margin of 16 px', () => {
    expect(boundView({ x: INITIAL_VIEW.x, y: 300, zoom: 1 }, large, screen).y).toBe(TOP_MARGIN);
    expect(boundView({ x: INITIAL_VIEW.x, y: 300, zoom: 1 }, small, screen).y).toBe(16);
  });

  it('goes up past the last lane only when the map is taller than the screen, down to a bottom margin of 24 px', () => {
    expect(boundView({ x: INITIAL_VIEW.x, y: -500, zoom: 1 }, small, screen).y).toBe(TOP_MARGIN);
    expect(boundView({ x: INITIAL_VIEW.x, y: -5000, zoom: 1 }, large, screen).y).toBe(screen.height - large.height - BOTTOM_MARGIN);
    expect(boundView({ x: INITIAL_VIEW.x, y: -400, zoom: 1 }, large, screen).y).toBe(-400);
  });

  it('never goes left of the column 0: x at most 192 (AR-19)', () => {
    expect(boundView({ x: 700, y: 0, zoom: 1 }, large, screen).x).toBe(192);
    expect(boundView({ x: 700, y: 0, zoom: 1 }, small, screen).x).toBe(INITIAL_VIEW.x);
  });

  it('goes right up to the end of the map, free columns included, with a right margin of 40 px', () => {
    expect(boundView({ x: -9000, y: 0, zoom: 1 }, large, screen).x).toBe(screen.width - large.width - RIGHT_MARGIN);
    expect(boundView({ x: -9000, y: 0, zoom: 1 }, small, screen).x).toBe(INITIAL_VIEW.x);
    expect(boundView({ x: -1200, y: 0, zoom: 1 }, large, screen).x).toBe(-1200);
  });

  it('keeps the zoom, and bounds with the size of the map at that zoom', () => {
    const bounded = boundView({ x: -9000, y: -9000, zoom: 0.5 }, large, screen);
    expect(bounded.zoom).toBe(0.5);
    expect(bounded).toEqual({ x: screen.width - large.width * 0.5 - RIGHT_MARGIN, y: screen.height - large.height * 0.5 - BOTTOM_MARGIN, zoom: 0.5 });
  });

  it('leaves the view as it is while the screen is not measured', () => {
    expect(boundView({ x: 900, y: 900, zoom: 1.5 }, large, { width: 0, height: 0 })).toEqual({ x: 900, y: 900, zoom: 1.5 });
  });
});
