// camera.test.ts
// The camera of a presentation: boxes framed in the screen less its bands, the zoom within its bounds, a view that shows everything already kept, a travel timed by its length; the exact trace of a flow for the light.

import { describe, expect, it } from 'vitest';
import { frameOf, insideMap, keptOrFramed, travelMs, type Insets } from './camera.ts';
import { pointOn, traceOf } from './route.ts';

const SCREEN = { width: 1200, height: 800 };
const INSETS: Insets = { top: 0, right: 0, bottom: 200, left: 200 };

describe('the frame of the camera', () => {
  it('centres the boxes in the screen less its bands, at the zoom that fits them with their margin', () => {
    // 1000 × 600 of room, less 2 × 50 of margin: 900 × 500 for a box of 450 × 100, zoom 2 by the width, kept at 1.5 the most.
    const view = frameOf([{ x: 0, y: 0, width: 450, height: 100 }], SCREEN, INSETS, { min: 0.5, max: 1.5 }) ?? { x: 0, y: 0, zoom: 0 };
    expect(view.zoom).toBe(1.5);
    // Its centre (225, 50) at the centre of the room (700, 300).
    expect({ x: 225 * view.zoom + view.x, y: 50 * view.zoom + view.y }).toEqual({ x: 700, y: 300 });
  });

  it('takes the union of the boxes and zooms out down to its floor for a wide one', () => {
    const view = frameOf([{ x: 0, y: 0, width: 100, height: 50 }, { x: 4000, y: 300, width: 100, height: 50 }], SCREEN, INSETS, { min: 0.5, max: 1.5 }) ?? { x: 0, y: 0, zoom: 0 };
    expect(view.zoom).toBe(0.5);
    expect(frameOf([], SCREEN, INSETS, { min: 0.5, max: 1.5 })).toBeNull();
  });

  it('keeps the view of now when it already shows the boxes at a zoom close to the frame, else takes the frame', () => {
    const boxes = [{ x: 0, y: 0, width: 450, height: 100 }];
    const frame = frameOf(boxes, SCREEN, INSETS, { min: 0.5, max: 1.5 });
    const near = { x: (frame?.x ?? 0) + 40, y: (frame?.y ?? 0) - 20, zoom: 1.4 };
    expect(keptOrFramed(near, boxes, SCREEN, INSETS, { min: 0.5, max: 1.5 })).toEqual(near);
    const far = { x: -3000, y: 0, zoom: 1.5 };
    expect(keptOrFramed(far, boxes, SCREEN, INSETS, { min: 0.5, max: 1.5 })).toEqual(frame);
    const small = { x: frame?.x ?? 0, y: frame?.y ?? 0, zoom: 0.6 };
    expect(keptOrFramed(small, boxes, SCREEN, INSETS, { min: 0.5, max: 1.5 })).toEqual(frame);
  });

  it('travels longer for a longer way, in pan and in zoom, between its floor and its ceiling', () => {
    const at = { x: 0, y: 0, zoom: 1 };
    expect(travelMs(at, at, SCREEN)).toBe(0);
    const short = travelMs(at, { x: -200, y: 0, zoom: 1 }, SCREEN);
    const long = travelMs(at, { x: -2400, y: 0, zoom: 1 }, SCREEN);
    expect(short).toBeGreaterThanOrEqual(600);
    expect(long).toBeGreaterThan(short);
    expect(travelMs(at, { x: -90000, y: 0, zoom: 0.2 }, SCREEN)).toBe(1800);
    expect(travelMs(at, { x: 0, y: 0, zoom: 2 }, SCREEN)).toBeGreaterThan(600);
  });
});

describe('the trace of a flow', () => {
  it('follows an orthogonal line around its rounded corners, from its first point to its last', () => {
    const trace = traceOf({ points: [{ x: 0, y: 0 }, { x: 100, y: 0 }, { x: 100, y: 100 }], curve: null });
    expect(pointOn(trace, 0)).toEqual({ x: 0, y: 0 });
    expect(pointOn(trace, 1)).toEqual({ x: 100, y: 100 });
    // The corner is cut by its rounding of 10: a little shorter than the 200 of the two sides.
    expect(trace.total).toBeGreaterThan(195);
    expect(trace.total).toBeLessThan(200);
    const middle = pointOn(trace, 0.5);
    expect(Math.hypot(middle.x - 100, middle.y)).toBeLessThan(10);
  });

  it('follows the curve of a loop, below its two ends, and moves on at a steady pace', () => {
    const trace = traceOf({ points: [{ x: 300, y: 50 }, { x: 0, y: 50 }], curve: { start: { x: 300, y: 50 }, end: { x: 0, y: 50 }, low: 200 } });
    expect(pointOn(trace, 0)).toEqual({ x: 300, y: 50 });
    expect(pointOn(trace, 1)).toEqual({ x: 0, y: 50 });
    expect(pointOn(trace, 0.5).y).toBeGreaterThan(150);
    const steps = [0, 0.25, 0.5, 0.75, 1].map((fraction) => pointOn(trace, fraction));
    const gaps = steps.slice(1).map((point, index) => Math.hypot(point.x - (steps[index]?.x ?? 0), point.y - (steps[index]?.y ?? 0)));
    expect(Math.max(...gaps) / Math.min(...gaps)).toBeLessThan(1.15);
  });

  it('stays at its one point for a flow of a single point, and reads a fraction outside 0..1 as its end', () => {
    const trace = traceOf({ points: [{ x: 5, y: 5 }], curve: null });
    expect(pointOn(trace, 0.5)).toEqual({ x: 5, y: 5 });
    const line = traceOf({ points: [{ x: 0, y: 0 }, { x: 10, y: 0 }], curve: null });
    expect([pointOn(line, -1), pointOn(line, 2)]).toEqual([{ x: 0, y: 0 }, { x: 10, y: 0 }]);
  });
});

describe('the camera kept on the map', () => {
  const MAP = { x: 0, y: 0, width: 3000, height: 1500 };

  it('never shows the void past an edge of a map larger than the room, its boxes still in view', () => {
    // A box near the top left of the map, framed at the centre of the room: the map would start inside the room.
    const boxes = [{ x: 40, y: 30, width: 170, height: 64 }];
    const frame = frameOf(boxes, SCREEN, INSETS, { min: 0.5, max: 1.25 }) ?? { x: 0, y: 0, zoom: 1 };
    const kept = insideMap(frame, MAP, boxes, SCREEN, INSETS);
    expect([kept.x, kept.y, kept.zoom]).toEqual([INSETS.left, INSETS.top, frame.zoom]);
  });

  it('gives the boxes the edge of the room before the edge of the map: a box at the very corner keeps a small margin', () => {
    const boxes = [{ x: 0, y: 0, width: 170, height: 64 }];
    const kept = insideMap({ x: 600, y: 300, zoom: 1 }, MAP, boxes, SCREEN, INSETS);
    expect([kept.x, kept.y]).toEqual([INSETS.left + 12, INSETS.top + 12]);
  });

  it('leaves a map smaller than the room where its frame put it', () => {
    const small = { x: 0, y: 0, width: 400, height: 200 };
    const view = { x: 500, y: 150, zoom: 1 };
    expect(insideMap(view, small, [small], SCREEN, INSETS)).toEqual(view);
  });

  it('keeps the boxes in view before the edges of the map', () => {
    const boxes = [{ x: 2900, y: 1400, width: 100, height: 100 }];
    const view = { x: -2700, y: -1200, zoom: 1 };
    const kept = insideMap(view, MAP, boxes, SCREEN, INSETS);
    const room = { right: SCREEN.width - INSETS.right, bottom: SCREEN.height - INSETS.bottom };
    expect(3000 * kept.zoom + kept.x).toBeLessThanOrEqual(room.right - 12);
    expect(1500 * kept.zoom + kept.y).toBeLessThanOrEqual(room.bottom - 12);
  });
});
