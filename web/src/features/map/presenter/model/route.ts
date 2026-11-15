// route.ts
// The exact trace of a flow for the light of a presentation (ADR 097): the orthogonal line with its corners rounded as roundedPath draws them, or the curve of a loop, cut into short pieces; the point at a fraction of its length.

import { CORNER_RADIUS, PRESENTED_RING } from '../../model/geometry/constants.ts';
import type { Point, Rect } from '../../model/geometry/grid.ts';
import type { RoutedFlow } from '../../model/geometry/routing/model.ts';

/** A trace: its points and the length run at each one, from 0 to its total. */
export interface Trace {
  readonly points: readonly Point[];
  readonly lengths: readonly number[];
  readonly total: number;
}

/** The pieces a rounded corner and a curve are cut into: enough for a light that never leaves the stroke. */
const CORNER_PIECES = 6;
const CURVE_PIECES = 32;

const quadratic = (a: Point, control: Point, b: Point, t: number): Point => ({
  x: (1 - t) ** 2 * a.x + 2 * (1 - t) * t * control.x + t ** 2 * b.x,
  y: (1 - t) ** 2 * a.y + 2 * (1 - t) * t * control.y + t ** 2 * b.y,
});

const cubic = (a: Point, c1: Point, c2: Point, b: Point, t: number): Point => ({
  x: (1 - t) ** 3 * a.x + 3 * (1 - t) ** 2 * t * c1.x + 3 * (1 - t) * t ** 2 * c2.x + t ** 3 * b.x,
  y: (1 - t) ** 3 * a.y + 3 * (1 - t) ** 2 * t * c1.y + 3 * (1 - t) * t ** 2 * c2.y + t ** 3 * b.y,
});

/** The orthogonal line as `roundedPath` draws it: each corner rounded by a quadratic curve of its radius, never more than half a side. */
function roundedPoints(points: readonly Point[]): Point[] {
  const [first] = points;
  if (first === undefined) return [];
  const out: Point[] = [first];
  for (let index = 1; index < points.length - 1; index++) {
    const [previous, corner, next] = [points[index - 1] as Point, points[index] as Point, points[index + 1] as Point];
    const before = Math.hypot(corner.x - previous.x, corner.y - previous.y);
    const after = Math.hypot(next.x - corner.x, next.y - corner.y);
    const r = Math.min(CORNER_RADIUS, before / 2, after / 2);
    if (before === 0 || after === 0) continue;
    const entry = { x: corner.x - ((corner.x - previous.x) / before) * r, y: corner.y - ((corner.y - previous.y) / before) * r };
    const exit = { x: corner.x + ((next.x - corner.x) / after) * r, y: corner.y + ((next.y - corner.y) / after) * r };
    for (let piece = 0; piece <= CORNER_PIECES; piece++) out.push(quadratic(entry, corner, exit, piece / CORNER_PIECES));
  }
  if (points.length > 1) out.push(points[points.length - 1] as Point);
  return out;
}

/** The curve of a loop, `M start C (start.x, low) (end.x, low) end`, as `curvePath` draws it. */
function curvePoints(curve: NonNullable<RoutedFlow['curve']>): Point[] {
  const c1 = { x: curve.start.x, y: curve.low };
  const c2 = { x: curve.end.x, y: curve.low };
  return Array.from({ length: CURVE_PIECES + 1 }, (_, piece) => cubic(curve.start, c1, c2, curve.end, piece / CURVE_PIECES));
}

/** The trace of a flow as the canvas draws it. */
export function traceOf(route: Pick<RoutedFlow, 'points' | 'curve'>): Trace {
  const points = route.curve === null ? roundedPoints(route.points) : curvePoints(route.curve);
  const lengths: number[] = [];
  let total = 0;
  points.forEach((point, index) => {
    const before = points[index - 1];
    total += before === undefined ? 0 : Math.hypot(point.x - before.x, point.y - before.y);
    lengths.push(total);
  });
  return { points, lengths, total };
}

/**
 * The holes of the mask of the light: each step widened by the outer ring a step shown wears, so that the light is born at
 * the outer frame of the step it leaves and never crosses its rings (essai J3 of 3.2); a step without a ring keeps the same
 * gap, under which its flow, lit too, joins it.
 */
export function lightHoles(boxes: readonly Rect[]): Rect[] {
  const ring = PRESENTED_RING.outer;
  return boxes.map((box) => ({ x: box.x - ring, y: box.y - ring, width: box.width + 2 * ring, height: box.height + 2 * ring }));
}

/** The point at a fraction of the length of a trace, read within 0 and 1. */
export function pointOn(trace: Trace, fraction: number): Point {
  const first = trace.points[0] ?? { x: 0, y: 0 };
  if (trace.total === 0) return first;
  const target = Math.min(1, Math.max(0, fraction)) * trace.total;
  const index = trace.lengths.findIndex((length) => length >= target);
  if (index <= 0) return first;
  const [a, b] = [trace.points[index - 1] as Point, trace.points[index] as Point];
  const [from, to] = [trace.lengths[index - 1] as number, trace.lengths[index] as number];
  const t = to === from ? 1 : (target - from) / (to - from);
  return { x: a.x + (b.x - a.x) * t, y: a.y + (b.y - a.y) * t };
}
