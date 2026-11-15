// routeGestures.test.ts
// What a gesture on the route of a flow reads (ADR 091): the side of a step near the pointer, the aisle and the band near a segment moved, the handles of a flow, the points of a gateway its exits share out.

import { flowOf, sampleMap } from '../../../test/maps.ts';
import type { RoutedFlow } from './geometry/routing/model.ts';
import type { LaneBand } from './mapLayout.ts';
import { allowedSides, drawnSidesOf, endsOf, nearestSide, passageNearX, passageNearY, segmentHandles, SIDE_REACH, sidePoints } from './routeGestures.ts';

const box = { x: 30, y: 28, width: 170, height: 64 };

const band = (id: string, top: number, rows: number, rowHeight = 120) => ({ lane: { id }, top, rows, height: rows * rowHeight }) as unknown as LaneBand;

const routed = (points: { x: number; y: number }[], curve: RoutedFlow['curve'] = null): RoutedFlow =>
  ({ id: 'f', sourceId: 'a', targetId: 'b', kind: 'flow', sourceSide: 'right', targetSide: 'left', points, curve, path: '', label: null });

describe('a gesture on the route of a flow (ADR 091)', () => {
  it('takes the side whose point is nearest to the pointer within reach, among those allowed', () => {
    expect(sidePoints(box)).toEqual({ top: { x: 115, y: 28 }, right: { x: 200, y: 60 }, bottom: { x: 115, y: 92 }, left: { x: 30, y: 60 } });
    expect(nearestSide(box, { x: 120, y: 10 }, ['top', 'right', 'bottom', 'left'])).toBe('top');
    expect(nearestSide(box, { x: 200 + SIDE_REACH - 1, y: 60 }, ['top', 'right', 'bottom', 'left'])).toBe('right');
    expect(nearestSide(box, { x: 200 + SIDE_REACH + 1, y: 60 }, ['top', 'right', 'bottom', 'left'])).toBeNull();
    expect(nearestSide(box, { x: 120, y: 10 }, ['right', 'bottom', 'left'])).toBeNull();
  });

  it('takes the aisle or the middle of a column nearest to an x, every half column, from the aisle before the column 0 to the one right of the last column', () => {
    // The middle of the column c at 115 + 220c, the aisle right of it at 225 + 220c.
    expect([115, 225, 300, 340, 445, 5, -400].map((x) => passageNearX(x, 3)))
      .toEqual([{ column: 0 }, { aisle: 0 }, { column: 1 }, { column: 1 }, { aisle: 1 }, { aisle: -1 }, { aisle: -1 }]);
    expect([passageNearX(5000, 3), passageNearX(5000, -1), passageNearX(340, 0)]).toEqual([{ aisle: 3 }, { aisle: -1 }, { aisle: 0 }]);
  });

  it('takes the band of the boundary nearest to a y, in the half the pointer is in, or the middle of the line in its central half', () => {
    const lanes = [band('l1', 0, 2), band('l2', 240, 1)];
    expect(passageNearY(lanes, 125)).toEqual({ laneId: 'l1', row: 1, edge: 'top' });
    expect(passageNearY(lanes, 115)).toEqual({ laneId: 'l1', row: 0, edge: 'bottom' });
    expect(passageNearY(lanes, 250)).toEqual({ laneId: 'l2', row: 0, edge: 'top' });
    expect(passageNearY(lanes, 230)).toEqual({ laneId: 'l1', row: 1, edge: 'bottom' });
    expect(passageNearY(lanes, 145)).toEqual({ laneId: 'l1', row: 1, edge: 'top' });
    expect(passageNearY(lanes, -20)).toEqual({ laneId: 'l1', row: 0, edge: 'top' });
    expect(passageNearY(lanes, 400)).toEqual({ laneId: 'l2', row: 0, edge: 'bottom' });
    expect([60, 170, 300].map((y) => passageNearY(lanes, y))).toEqual([{ laneId: 'l1', row: 0 }, { laneId: 'l1', row: 1 }, { laneId: 'l2', row: 0 }]);
    expect(passageNearY([], 10)).toBeNull();
  });

  it('judges the middle of a line on the axis of its steps: unchanged without the value stream, the band under the steps longer with it (audit 3.1.0 3.1)', () => {
    // Without the band of the value stream, the middle of the height is the axis: the middle wins in the central half.
    const plain = [band('l1', 0, 2), band('l2', 240, 1)];
    for (let y = 0; y < 360; y++) {
      const top = Math.floor(y / 120) * 120;
      const middle = Math.abs(y - (top + 60)) < Math.min(y - top, top + 120 - y);
      expect('edge' in (passageNearY(plain, y) ?? { edge: 'none' })).toBe(!middle);
    }
    // With it, a line is 156 px high and its steps stand at 28 to 92: the middle runs at 60, so the half under the steps
    // belongs to the band of the bottom sooner.
    const stream = [band('l1', 0, 1, 156), band('l2', 156, 1, 156)];
    expect(passageNearY(stream, 35)).toEqual({ laneId: 'l1', row: 0 });
    expect(passageNearY(stream, 100)).toEqual({ laneId: 'l1', row: 0 });
    expect(passageNearY(stream, 115)).toEqual({ laneId: 'l1', row: 0, edge: 'bottom' });
    expect(passageNearY(stream, 25)).toEqual({ laneId: 'l1', row: 0, edge: 'top' });
  });

  it('shows a handle on each segment inside the path, on the longest of a path without one, and at the bottom of a curve', () => {
    const elbow = routed([{ x: 200, y: 60 }, { x: 225, y: 60 }, { x: 225, y: 180 }, { x: 250, y: 180 }]);
    expect(segmentHandles(elbow)).toEqual([{ index: 1, middle: { x: 225, y: 120 }, horizontal: false }]);
    expect(segmentHandles(routed([{ x: 200, y: 60 }, { x: 470, y: 60 }]))).toEqual([{ index: 0, middle: { x: 335, y: 60 }, horizontal: true }]);
    const curve = routed([{ x: 555, y: 92 }, { x: 115, y: 92 }], { start: { x: 555, y: 92 }, end: { x: 115, y: 92 }, low: 124 });
    // The bottom of the curve drawn, three quarters of the way to its low (audit 3.1.0 3.5): 8 px above it, 26 px with the value stream.
    expect(segmentHandles(curve)).toEqual([{ index: 0, middle: { x: 335, y: 116 }, horizontal: true }]);
    const deeper = routed([{ x: 555, y: 92 }, { x: 115, y: 92 }], { start: { x: 555, y: 92 }, end: { x: 115, y: 92 }, low: 196 });
    expect(segmentHandles(deeper)[0]?.middle).toEqual({ x: 335, y: 170 });
    expect(endsOf(elbow)).toEqual({ source: { x: 200, y: 60 }, target: { x: 250, y: 180 } });
    expect(endsOf(curve)).toEqual({ source: { x: 555, y: 92 }, target: { x: 115, y: 92 } });
  });
});

describe('the points a flow may take on a gateway (ADR 091, F-85)', () => {
  it('leaves to the other exits of a gateway the points they chose and to its entry the left point, never to an arrival nor to a step', () => {
    const map = sampleMap({ flows: [...sampleMap().flows.filter((flow) => flow.id !== 'f4'), flowOf('f4', 's3', 's2', { route: { source_side: 'top', target_side: null, via: null } })] });
    expect(allowedSides(map, 'f3', 'source')).toEqual(['right', 'bottom']);
    expect(allowedSides(map, 'f4', 'source')).toEqual(['top', 'right', 'bottom']);
    expect(allowedSides(map, 'f3', 'target')).toEqual(['top', 'right', 'bottom', 'left']);
    expect(allowedSides(map, 'f1', 'source')).toEqual(['top', 'right', 'bottom', 'left']);
  });

  it('keeps an arrival on a gateway off the points its exits leave by, as drawn, and a departure off the point an arrival chose (audit 3.1.0 3.3, F-85)', () => {
    const map = sampleMap({ flows: [...sampleMap().flows.filter((flow) => flow.id !== 'f4'), flowOf('f4', 's3', 's2', { route: { source_side: 'top', target_side: null, via: null } })] });
    // Without the drawing, the points the exits chose; with it, the points they are drawn from.
    expect(allowedSides(map, 'f2', 'target')).toEqual(['right', 'bottom', 'left']);
    expect(allowedSides(map, 'f2', 'target', new Map([['f3', { source: 'right', target: 'left' }], ['f4', { source: 'top', target: 'right' }]]))).toEqual(['bottom', 'left']);
    // An arrival that chose the bottom point leaves it to no exit.
    const arriving = sampleMap({ flows: map.flows.map((flow) => (flow.id === 'f2' ? { ...flow, route: { source_side: null, target_side: 'bottom', via: null } } : flow)) });
    expect(allowedSides(arriving, 'f3', 'source')).toEqual(['right']);
    expect(drawnSidesOf([{ route: { id: 'f3', sourceSide: 'right', targetSide: 'left' } }])).toEqual(new Map([['f3', { source: 'right', target: 'left' }]]));
    // An arrival drawn on a vertex without being chosen (an automatic one) leaves it to no exit either (audit 3.1 V2, 3.3).
    expect(allowedSides(map, 'f3', 'source', new Map([['f2', { source: 'right', target: 'top' }]]))).toEqual(['right', 'bottom']);
    expect(allowedSides(map, 'f3', 'source', new Map([['f2', { source: 'right', target: 'bottom' }]]))).toEqual(['right']);
  });
});
