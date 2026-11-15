// routing.test.ts
// The rules of the routing, one map drawn by hand for each: sides, gateways, aisles, detours, elbows, loops, tracks, labels, paths (annex B 3.4, AR-24, AR-25).

import { readFileSync, readdirSync, statSync } from 'node:fs';
import { join } from 'node:path';
import { pointOnCurve, routeMap } from '../../../../../test/routedMaps.ts';
import type { Point } from '../grid.ts';
import { overlaps } from './labels.ts';
import { contextOf, type RoutedFlow, type RoutingFlow, type RoutingStep, type Size, type StepKind } from './model.ts';
import { curvePath, roundedPath } from './paths.ts';
import { gatewaySides } from './sides.ts';

// Lines: l0 from 0 (centre 60, steps 28 to 92), l1 from 120 (centre 180), l2 from 240 (centre 300), l3 from 360.
// Columns: c starts at 30 + 220c, its axis at 115 + 220c, its right side at 200 + 220c, the aisle right of it at 225 + 220c.
const LANES = [0, 1, 2, 3].map((order) => ({ id: `l${order}`, order }));

const step = (id: string, lane: number, column: number, type: StepKind = 'task', row = 0): RoutingStep => ({ id, laneId: `l${lane}`, column, row, type });
const flow = (id: string, sourceId: string, targetId: string, label: Size | null = null): RoutingFlow => ({ id, sourceId, targetId, label });

function route(steps: RoutingStep[], flows: RoutingFlow[]): Map<string, RoutedFlow> {
  return new Map(routeMap({ lanes: LANES, steps, flows }).map((routed) => [routed.id, routed]));
}

function get(routes: Map<string, RoutedFlow>, id: string): RoutedFlow {
  const routed = routes.get(id);
  if (routed === undefined) throw new Error(`No flow ${id}.`);
  return routed;
}

const xy = (points: readonly Point[]) => points.map((point) => [point.x, point.y]);

describe('the sides of a flow (B-3.4-01 to B-3.4-04)', () => {
  it('same column, a line under: straight down from the bottom to the top', () => {
    const f = get(route([step('A', 0, 0), step('B', 1, 0)], [flow('f', 'A', 'B')]), 'f');
    expect([f.sourceSide, f.targetSide, f.kind, xy(f.points)]).toEqual(['bottom', 'top', 'flow', [[115, 92], [115, 148]]]);
  });

  it('same column, a line above: straight up from the top to the bottom', () => {
    const f = get(route([step('A', 0, 0), step('B', 1, 0)], [flow('f', 'B', 'A')]), 'f');
    expect([f.sourceSide, f.targetSide, xy(f.points)]).toEqual(['top', 'bottom', [[115, 148], [115, 92]]]);
  });

  it('a target on the left: a loop from the bottom to the bottom', () => {
    const f = get(route([step('A', 0, 0), step('B', 0, 2)], [flow('f', 'B', 'A')]), 'f');
    expect([f.sourceSide, f.targetSide, f.kind]).toEqual(['bottom', 'bottom', 'loop']);
  });

  it('else from the right to the left, straight on a free line', () => {
    const f = get(route([step('A', 0, 0), step('B', 0, 1)], [flow('f', 'A', 'B')]), 'f');
    expect([f.sourceSide, f.targetSide, f.kind, xy(f.points)]).toEqual(['right', 'left', 'flow', [[200, 60], [250, 60]]]);
  });

  it('two rows of one lane: straight down between the rows of a column, right to left to the row 1 of the next column', () => {
    const steps = [step('A', 0, 0), step('B', 0, 1), step('C', 0, 1, 'task', 1)];
    const routes = route(steps, [flow('ab', 'A', 'B'), flow('ac', 'A', 'C'), flow('bc', 'B', 'C')]);
    const [bc, ac] = [get(routes, 'bc'), get(routes, 'ac')];
    expect([bc.kind, bc.sourceSide, bc.targetSide, xy(bc.points)]).toEqual(['flow', 'bottom', 'top', [[335, 92], [335, 148]]]);
    expect([ac.kind, ac.sourceSide, ac.targetSide, xy(ac.points.slice(-2))]).toEqual(['flow', 'right', 'left', [[225, 180], [250, 180]]]);
  });
});

describe('the gateways (B-3.4-05 to B-3.4-13, AR-24)', () => {
  it('a gateway of one exit keeps the sides of a step', () => {
    const f = get(route([step('G', 0, 0, 'decision'), step('B', 0, 1)], [flow('f', 'G', 'B')]), 'f');
    expect([f.sourceSide, f.targetSide]).toEqual(['right', 'left']);
  });

  it('one point per exit: right to the nearest of its line, then bottom and top when the way is free; one more leaves from the right on its own track', () => {
    const steps = [step('G', 1, 1, 'decision'), step('X', 1, 2), step('Y', 1, 4), step('Z', 2, 3), step('W', 0, 3)];
    const routes = route(steps, [flow('x', 'G', 'X'), flow('y', 'G', 'Y'), flow('z', 'G', 'Z'), flow('w', 'G', 'W')]);
    expect(['x', 'z', 'w', 'y'].map((id) => get(routes, id).sourceSide)).toEqual(['right', 'bottom', 'top', 'right']);
    expect(xy(get(routes, 'z').points)).toEqual([[335, 212], [335, 300], [690, 300]]);
    expect(xy(get(routes, 'w').points)).toEqual([[335, 148], [335, 60], [690, 60]]);
    const starts = ['x', 'y', 'z', 'w'].map((id) => JSON.stringify(get(routes, id).points[0]));
    expect(new Set(starts).size).toBe(4);
  });

  it('« voie libre »: a step in the column of the gateway on the line of the target keeps the exit on the right (B-3.4-12)', () => {
    const steps = [step('G', 1, 1, 'decision'), step('X', 1, 2), step('Z', 2, 3), step('K', 2, 1)];
    const routes = route(steps, [flow('x', 'G', 'X'), flow('z', 'G', 'Z')]);
    expect(get(routes, 'z').sourceSide).toBe('right');
    expect(get(routes, 'z').points[0]).not.toEqual(get(routes, 'x').points[0]);
  });

  it('« voie libre »: a step on the line of the target, between the gateway and the target, keeps the exit on the right (B-3.4-12)', () => {
    const exits = [flow('x', 'G', 'X'), flow('z', 'G', 'Z')];
    const routes = route([step('G', 1, 1, 'decision'), step('X', 1, 2), step('Z', 2, 3), step('K', 2, 2)], exits);
    expect([get(routes, 'x').sourceSide, get(routes, 'z').sourceSide]).toEqual(['right', 'right']);
    expect(get(routes, 'z').points[0]).not.toEqual(get(routes, 'x').points[0]);
    expect(get(route([step('G', 1, 1, 'decision'), step('X', 1, 2), step('Z', 2, 3)], exits), 'z').sourceSide).toBe('bottom');
  });

  it('« voie libre »: a step in the column of the gateway on a line between keeps an exit up on the right, not on the top point (B-3.4-12)', () => {
    const exits = [flow('x', 'G', 'X'), flow('w', 'G', 'W')];
    const routes = route([step('G', 2, 0, 'decision'), step('X', 2, 1), step('K', 1, 0), step('W', 0, 3)], exits);
    expect([get(routes, 'x').sourceSide, get(routes, 'w').sourceSide]).toEqual(['right', 'right']);
    expect(get(routes, 'w').points[0]).not.toEqual(get(routes, 'x').points[0]);
    expect(get(route([step('G', 2, 0, 'decision'), step('X', 2, 1), step('W', 0, 3)], exits), 'w').sourceSide).toBe('top');
  });

  it('a step that is not a gateway keeps the sides of a step for each of its exits; at its place a gateway shares its points out', () => {
    const exits = [flow('x', 'S', 'X'), flow('y', 'S', 'Y'), flow('z', 'S', 'Z')];
    const steps = (type: StepKind) => [step('S', 1, 0, type), step('X', 1, 1), step('Y', 1, 3), step('Z', 2, 2)];
    for (const type of ['task', 'wait', 'start'] as const) {
      const routes = route(steps(type), exits);
      expect(['x', 'y', 'z'].map((id) => [get(routes, id).sourceSide, get(routes, id).kind])).toEqual([['right', 'flow'], ['right', 'detour'], ['right', 'flow']]);
      expect(gatewaySides(contextOf({ lanes: LANES, steps: steps(type), flows: exits }), exits).size).toBe(0);
    }
    const gateway = route(steps('decision'), exits);
    expect(['x', 'y', 'z'].map((id) => get(gateway, id).sourceSide)).toEqual(['right', 'top', 'bottom']);
  });

  it('a second exit imposed on the bottom point leaves from the right: no point is shared, the bottom one included (B-3.4-06)', () => {
    const steps = [step('A', 0, 0), step('B', 0, 1), step('G', 0, 2, 'decision')];
    const routes = route(steps, [flow('a', 'G', 'A'), flow('b', 'G', 'B')]);
    expect([get(routes, 'a').sourceSide, get(routes, 'b').sourceSide].sort()).toEqual(['bottom', 'right']);
    expect(get(routes, 'a').points[0]).not.toEqual(get(routes, 'b').points[0]);
  });

  it('the imposed exits take their point first: straight up the top, back to the left the bottom (B-3.4-07)', () => {
    const steps = [step('G', 1, 1, 'decision'), step('U', 0, 1), step('A', 1, 0), step('Z', 2, 3), step('W', 0, 3)];
    const routes = route(steps, [flow('u', 'G', 'U'), flow('a', 'G', 'A'), flow('z', 'G', 'Z'), flow('w', 'G', 'W')]);
    expect(['u', 'a', 'z', 'w'].map((id) => get(routes, id).sourceSide)).toEqual(['top', 'bottom', 'right', 'right']);
    expect(new Set(['u', 'a', 'z', 'w'].map((id) => JSON.stringify(get(routes, id).points[0]))).size).toBe(4);
  });

  it('a gateway of one exit goes round from its right point like a step, not from its top point (B-3.4-05)', () => {
    const f = get(route([step('G', 0, 0, 'decision'), step('M', 0, 1), step('B', 0, 3)], [flow('f', 'G', 'B')]), 'f');
    expect([f.sourceSide, f.kind]).toEqual(['right', 'detour']);
  });

  it('an exit from the top point to its own line goes round from the point (B-3.4-19)', () => {
    const steps = [step('G', 0, 0, 'decision'), step('X', 0, 1), step('Y', 0, 3)];
    const y = get(route(steps, [flow('x', 'G', 'X'), flow('y', 'G', 'Y')]), 'y');
    expect([y.sourceSide, y.kind, xy(y.points)]).toEqual(['top', 'detour', [[115, 28], [115, 14], [665, 14], [665, 60], [690, 60]]]);
  });
});

describe('the aisles, detours and elbows (B-3.4-14 to B-3.4-23)', () => {
  it('a vertical that would cross a step of its column runs in the aisle on the right, right to right', () => {
    const f = get(route([step('A', 0, 0), step('M', 1, 0), step('B', 2, 0)], [flow('f', 'A', 'B')]), 'f');
    expect([f.sourceSide, f.targetSide, xy(f.points)]).toEqual(['right', 'right', [[200, 60], [225, 60], [225, 300], [200, 300]]]);
  });

  it('going up too: a vertical that would cross a step of its column runs in the aisle on the right, right to right', () => {
    const f = get(route([step('A', 0, 0), step('M', 1, 0), step('B', 2, 0)], [flow('f', 'B', 'A')]), 'f');
    expect([f.kind, f.sourceSide, f.targetSide, xy(f.points)]).toEqual(['flow', 'right', 'right', [[200, 300], [225, 300], [225, 60], [200, 60]]]);
  });

  it('a step between on one line: out on the right, up the aisle, the free band at the top of the row + 14, down before the target', () => {
    const f = get(route([step('A', 0, 0), step('M', 0, 1), step('B', 0, 3)], [flow('f', 'A', 'B')]), 'f');
    expect([f.kind, f.targetSide, xy(f.points)]).toEqual(['detour', 'left', [[200, 60], [225, 60], [225, 14], [665, 14], [665, 60], [690, 60]]]);
    expect(f.path.startsWith('M 200 60 L 215 60 Q 225 60 225 50')).toBe(true);
  });

  it('between two lines, a detour only when both are taken; through the band above the line of the target', () => {
    const f = get(route([step('A', 0, 0), step('M', 0, 1), step('N', 1, 1), step('B', 1, 3)], [flow('f', 'A', 'B')]), 'f');
    expect([f.kind, xy(f.points)]).toEqual(['detour', [[200, 60], [225, 60], [225, 134], [665, 134], [665, 180], [690, 180]]]);
  });

  it('the elbow is just before the arrival when the line of the source is free (B-3.4-21, B-3.4-23)', () => {
    const f = get(route([step('A', 0, 0), step('B', 1, 3)], [flow('f', 'A', 'B')]), 'f');
    expect([f.kind, xy(f.points)]).toEqual(['flow', [[200, 60], [665, 60], [665, 180], [690, 180]]]);
    const g = get(route([step('A', 0, 0), step('N', 1, 1), step('B', 1, 3)], [flow('f', 'A', 'B')]), 'f');
    expect(xy(g.points)).toEqual([[200, 60], [665, 60], [665, 180], [690, 180]]);
  });

  it('else just after the departure when the line of the target is free (B-3.4-22)', () => {
    const f = get(route([step('A', 0, 0), step('M', 0, 1), step('B', 1, 3)], [flow('f', 'A', 'B')]), 'f');
    expect(xy(f.points)).toEqual([[200, 60], [225, 60], [225, 180], [690, 180]]);
  });
});

describe('the loops (B-3.4-03, B-3.4-25 to B-3.4-27)', () => {
  it('on one line: the curve M s C (sx, low) (tx, low) t, low = max(sy, ty) + 32, in the free band under the steps', () => {
    const f = get(route([step('A', 0, 0), step('B', 0, 2)], [flow('f', 'B', 'A')]), 'f');
    expect(f.curve).toEqual({ start: { x: 555, y: 92 }, end: { x: 115, y: 92 }, low: 124 });
    expect(f.path).toBe('M 555 92 C 555 124 115 124 115 92');
    expect(pointOnCurve(f.curve ?? { start: { x: 0, y: 0 }, end: { x: 0, y: 0 }, low: 0 }, 0.5).y).toBeLessThanOrEqual(92 + 28);
  });

  it('its label sits at low − 8 (B-3.4-26)', () => {
    const f = get(route([step('A', 0, 0), step('B', 0, 2)], [flow('f', 'B', 'A', { width: 40, height: 16 })]), 'f');
    expect(f.label).toEqual({ x: 315, y: 108, width: 40, height: 16 });
  });

  it('to another line: no curve, an orthogonal line through the bands under the two lines and an aisle between, never across a step', () => {
    const f = get(route([step('A', 0, 0), step('M', 1, 1), step('B', 1, 3)], [flow('f', 'B', 'A')]), 'f');
    expect([f.kind, f.curve, f.sourceSide, f.targetSide]).toEqual(['loop', null, 'bottom', 'bottom']);
    const [first, second] = f.points;
    expect([first, second]).toEqual([{ x: 775, y: 212 }, { x: 775, y: 226 }]);
    expect(xy(f.points.slice(-2))).toEqual([[115, 106], [115, 92]]);
    expect(f.points[2]?.x).toBe(225);
  });

  it('takes the aisle nearest to the target where the flows drawn before leave the most room', () => {
    const steps = [step('A', 0, 0), step('C', 1, 2), step('B', 1, 3)];
    const routes = route(steps, [flow('ca', 'C', 'A'), flow('ba', 'B', 'A')]);
    expect([get(routes, 'ca').points[2]?.x, get(routes, 'ba').points[2]?.x]).toEqual([225, 665]);
  });
});

describe('the tracks of 6 px (B-3.4-20, B-3.4-34, spec 9.7)', () => {
  it('detours to one line over one stretch run on distinct tracks, 6 px apart', () => {
    const steps = [step('A', 0, 0), step('M', 0, 1), step('B', 0, 2), step('N', 0, 3), step('C', 0, 4)];
    const routes = route(steps, [flow('a', 'A', 'C'), flow('b', 'B', 'C'), flow('m', 'M', 'C')]);
    expect(['a', 'm', 'b'].map((id) => get(routes, id).points[2]?.y)).toEqual([14, 8, 20]);
  });

  it('two vertical segments of different flows in one aisle take distinct tracks', () => {
    const routes = route([step('A', 0, 0), step('C', 1, 0), step('B', 2, 2), step('E', 0, 2)], [flow('ab', 'A', 'B'), flow('ce', 'C', 'E')]);
    const [ab, ce] = [get(routes, 'ab').points[1]?.x, get(routes, 'ce').points[1]?.x];
    expect(new Set([ab, ce]).size).toBe(2);
    expect(Math.abs((ab ?? 0) - (ce ?? 0))).toBe(6);
  });

  it('two flows of different steps into one side of a step end on one point, on one last segment: one arrowhead (AR-129)', () => {
    for (const type of ['task', 'decision'] as const) {
      const routes = route([step('A', 0, 0), step('B', 0, 1, type), step('C', 1, 0)], [flow('ab', 'A', 'B'), flow('cb', 'C', 'B')]);
      const [ab, cb] = [get(routes, 'ab'), get(routes, 'cb')];
      expect(xy(ab.points)).toEqual([[200, 60], [250, 60]]);
      expect(xy(cb.points.slice(-2))).toEqual([[225, 60], [250, 60]]);
    }
  });

  it('the loops into the bottom of a step end on its point, a curve of its line as an orthogonal loop of another line (AR-129)', () => {
    const steps = [step('A', 0, 0), step('B', 0, 2), step('D', 0, 3), step('C', 1, 2)];
    const routes = route(steps, [flow('ba', 'B', 'A'), flow('da', 'D', 'A'), flow('ca', 'C', 'A')]);
    const [ba, da, ca] = [get(routes, 'ba'), get(routes, 'da'), get(routes, 'ca')];
    expect([ba.curve?.end, da.curve?.end, ca.points.at(-1)]).toEqual([{ x: 115, y: 92 }, { x: 115, y: 92 }, { x: 115, y: 92 }]);
    expect(ca.points.at(-2)?.x).toBe(115);
  });

  it('two exits of a gateway into one side of a step still leave from distinct points, then end on one (AR-24, AR-129)', () => {
    const routes = route([step('G', 1, 0, 'decision'), step('B', 0, 2)], [flow('g1', 'G', 'B'), flow('g2', 'G', 'B')]);
    const [g1, g2] = [get(routes, 'g1'), get(routes, 'g2')];
    expect(g1.points[0]).not.toEqual(g2.points[0]);
    expect(g1.points.at(-1)).toEqual(g2.points.at(-1));
  });
});

describe('the map holds every flow', () => {
  it('six detours over the first line stay in its band, never above the map', () => {
    const steps = [0, 1, 2, 3, 4, 5, 6, 7].map((column) => step(`s${column}`, 0, column));
    const routes = route(steps, [0, 1, 2, 3, 4, 5].map((column) => flow(`f${column}`, `s${column}`, 's7')));
    const heights = [...routes.values()].map((routed) => routed.points[2]?.y ?? -1);
    expect(heights.filter((height) => height < 0 || height > 28)).toEqual([]);
  });
});

describe('the labels (B-3.4-36)', () => {
  it('never on the label of a gateway, written under its shape (AR-57)', () => {
    const steps = [step('A', 0, 0), { ...step('G', 0, 1, 'decision'), below: { width: 120, height: 16 } }, step('B', 0, 2)];
    const f = get(route(steps, [flow('f', 'B', 'A', { width: 40, height: 16 })]), 'f');
    const gatewayLabel = { x: 335 - 60, y: 92 + 3, width: 120, height: 16 };
    expect(f.label === null || overlaps(f.label, gatewayLabel)).toBe(false);
  });

  it('on its horizontal segment when it has room', () => {
    const f = get(route([step('A', 0, 0), step('B', 0, 3)], [flow('f', 'A', 'B', { width: 60, height: 16 })]), 'f');
    expect(f.label).toEqual({ x: 445 - 30, y: 52, width: 60, height: 16 });
  });

  it('on its vertical segment when its horizontal segments are too short, never away in the free band (spec 9.7, ADR 059 point 5)', () => {
    // A document carried from a step to the next one of the lane under (NDF-E002 to NDF-E003 of the demonstration).
    const f = get(route([step('A', 0, 0), step('B', 0, 1), step('C', 1, 2)], [flow('f', 'B', 'C', { width: 35, height: 19 })]), 'f');
    expect(xy(f.points)).toEqual([[420, 60], [445, 60], [445, 180], [470, 180]]);
    expect(f.label).toEqual({ x: 445 - 17.5, y: 120 - 9.5, width: 35, height: 19 });
  });

  it('else off the steps, in the free band, and off the other labels', () => {
    const steps = [step('A', 0, 0), step('B', 0, 1), step('C', 0, 2)];
    const routes = route(steps, [flow('ab', 'A', 'B', { width: 110, height: 16 }), flow('bc', 'B', 'C', { width: 110, height: 16 })]);
    const labels = ['ab', 'bc'].flatMap((id) => get(routes, id).label ?? []);
    const boxes = steps.map((item) => ({ x: 30 + 220 * item.column, y: 28, width: 170, height: 64 }));
    expect(labels).toHaveLength(2);
    expect(labels.filter((label) => boxes.some((box) => overlaps(label, box)))).toEqual([]);
    expect(labels.filter((label, index) => labels.some((other, at) => at !== index && overlaps(label, other)))).toEqual([]);
  });
});

describe('the paths and the kinds (B-3.4-18, B-3.4-28, B-3.4-33)', () => {
  it('round each corner with a radius of 10, at most half of the shorter side', () => {
    expect(roundedPath([{ x: 0, y: 0 }, { x: 100, y: 0 }, { x: 100, y: 100 }])).toBe('M 0 0 L 90 0 Q 100 0 100 10 L 100 100');
    expect(roundedPath([{ x: 0, y: 0 }, { x: 8, y: 0 }, { x: 8, y: 100 }])).toBe('M 0 0 L 4 0 Q 8 0 8 4 L 8 100');
    expect(curvePath({ start: { x: 10, y: 0 }, end: { x: 0, y: 0 }, low: 32 })).toBe('M 10 0 C 10 32 0 32 0 0');
  });

  it('three kinds: flow, loop, detour; a flow to a step that is not on the map is left out', () => {
    const steps = [step('A', 0, 0), step('M', 0, 1), step('B', 0, 3)];
    const routes = route(steps, [flow('f', 'A', 'M'), flow('d', 'A', 'B'), flow('l', 'B', 'A'), flow('x', 'A', 'elsewhere')]);
    expect([...routes.values()].map((routed) => [routed.id, routed.kind])).toEqual([['f', 'flow'], ['d', 'detour'], ['l', 'loop']]);
  });
});

describe('the routing is a pure module (S-3.5-03)', () => {
  it('reads neither React nor the DOM', () => {
    const folder = join(import.meta.dirname, '..');
    const files = (dir: string): string[] => readdirSync(dir).flatMap((name) => {
      const path = join(dir, name);
      return statSync(path).isDirectory() ? files(path) : [path];
    });
    const sources = files(folder).filter((file) => file.endsWith('.ts') && !file.endsWith('.test.ts'));
    expect(sources.length).toBeGreaterThanOrEqual(8);
    expect(sources.filter((file) => /from 'react|\bdocument\.|\bwindow\.|HTMLElement/.test(readFileSync(file, 'utf8')))).toEqual([]);
  });
});
