// paths.ts
// The SVG path of a flow: an orthogonal line with rounded corners of 10, or the curve of a loop (B-3.4-18, B-3.4-25, B-3.4-28).

import { CORNER_RADIUS } from '../constants.ts';
import type { Point } from '../grid.ts';
import type { Curve } from './model.ts';

const number = (value: number): string => String(Math.round(value * 100) / 100);
const at = (point: Point): string => `${number(point.x)} ${number(point.y)}`;

/** An orthogonal line whose corners are rounded with the radius given, never more than half of the shorter side of a corner. */
export function roundedPath(points: readonly Point[], radius = CORNER_RADIUS): string {
  const [first] = points;
  if (first === undefined) return '';
  let path = `M ${at(first)}`;
  for (let index = 1; index < points.length - 1; index++) {
    const [previous, corner, next] = [points[index - 1] as Point, points[index] as Point, points[index + 1] as Point];
    const before = Math.hypot(corner.x - previous.x, corner.y - previous.y);
    const after = Math.hypot(next.x - corner.x, next.y - corner.y);
    const r = Math.min(radius, before / 2, after / 2);
    const entry = { x: corner.x - ((corner.x - previous.x) / before) * r, y: corner.y - ((corner.y - previous.y) / before) * r };
    const exit = { x: corner.x + ((next.x - corner.x) / after) * r, y: corner.y + ((next.y - corner.y) / after) * r };
    path += ` L ${at(entry)} Q ${at(corner)} ${at(exit)}`;
  }
  const last = points.at(-1) as Point;
  return points.length > 1 ? `${path} L ${at(last)}` : path;
}

/** The curve of a loop: `M start C (start.x, low) (end.x, low) end` (B-3.4-25). */
export function curvePath(curve: Curve): string {
  return `M ${at(curve.start)} C ${number(curve.start.x)} ${number(curve.low)} ${number(curve.end.x)} ${number(curve.low)} ${at(curve.end)}`;
}
