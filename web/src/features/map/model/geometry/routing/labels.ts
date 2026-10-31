// labels.ts
// The labels of the flows: off the steps and off the other labels, on or near their flow, on a horizontal segment when one has room (spec 9.7, B-3.4-26, B-3.4-36).

import { LOOP_DEPTH, LOOP_LABEL_RISE, loopDeeper } from '../constants.ts';
import type { Point, Rect } from '../grid.ts';
import type { Size } from './model.ts';

/** The space kept between a label and a step or another label. */
const CLEARANCE = 2;

/**
 * The steps a label slides by along a segment, and how far above or under it it may move: on the segment or near it, then
 * in the free band over or under the steps of its line (46: the middle of the band; 60: the boundary between two rows).
 */
const SLIDE = 20;
const NEAR = [0, -12, 12, -24, 24];
const BAND = [-46, 46, -60, 60];

/** A flow to label: its points (an orthogonal line, or the two ends of a curve) and the size of its label. */
export interface LabelRequest {
  readonly id: string;
  readonly points: readonly Point[];
  readonly curve: boolean;
  readonly size: Size;
  /** The band under the steps, which makes a loop deeper (the value stream layer, spec 14.5). */
  readonly below?: number;
}

export const overlaps = (a: Rect, b: Rect, clearance = 0): boolean =>
  a.x < b.x + b.width + clearance && b.x < a.x + a.width + clearance && a.y < b.y + b.height + clearance && b.y < a.y + a.height + clearance;

const centred = (x: number, y: number, size: Size): Rect => ({ x: x - size.width / 2, y: y - size.height / 2, width: size.width, height: size.height });

/** Positions along a segment, its middle first, then farther on each side by the step given, inside it. */
function* along(from: number, to: number, slide = SLIDE): Generator<number> {
  const [low, high] = [Math.min(from, to), Math.max(from, to)];
  const middle = (low + high) / 2;
  yield middle;
  for (let step = slide; middle - step >= low || middle + step <= high; step += slide) {
    if (middle - step >= low) yield middle - step;
    if (middle + step <= high) yield middle + step;
  }
}

/** Beside a vertical segment: on it, just left or right of it (the first two), then farther along the free band it crosses. */
const besides = (size: Size, steps: readonly number[]): number[] =>
  steps.flatMap((step) => (step === 0 ? [0] : [-1, 1].map((sign) => sign * (size.width / 2 + 6 + (step - 1) * SLIDE))));

type Segment = readonly [Point, Point];

function* onHorizontals(horizontal: readonly Segment[], shifts: readonly number[], size: Size): Generator<Rect> {
  for (const shift of shifts) for (const [a, b] of horizontal) for (const x of along(a.x, b.x)) yield centred(x, a.y + shift, size);
}

function* onVerticals(vertical: readonly Segment[], steps: readonly number[], size: Size): Generator<Rect> {
  for (const [a, b] of vertical) for (const shift of besides(size, steps)) for (const y of along(a.y, b.y, SLIDE / 2)) yield centred(a.x + shift, y, size);
}

/**
 * The places tried for a label, in their order: the one of a loop (B-3.4-26); else on or near the horizontal segments,
 * longest first, then on or just beside the vertical ones; only then in the free band, away from the flow (spec 9.7: near
 * its flow, on a horizontal segment by preference).
 */
function* candidates(request: LabelRequest): Generator<Rect> {
  const { points, size } = request;
  if (request.curve) {
    const [start, end] = [points[0], points.at(-1)];
    if (start === undefined || end === undefined) return;
    // The lowest point of the curve: three quarters of its depth (its two control points at the depth).
    const y = Math.max(start.y, end.y) + LOOP_DEPTH - LOOP_LABEL_RISE + (loopDeeper(request.below ?? 0) * 3) / 4;
    for (const shift of [...NEAR, ...BAND]) for (const x of along(start.x, end.x)) yield centred(x, y + shift, size);
    return;
  }
  const segments = points.slice(1).map((point, index): Segment => [points[index] as Point, point]);
  const horizontal = segments.filter(([a, b]) => a.y === b.y).sort(([a, b], [c, d]) => Math.abs(d.x - c.x) - Math.abs(b.x - a.x));
  const vertical = segments.filter(([a, b]) => a.x === b.x);
  yield* onHorizontals(horizontal, NEAR, size);
  yield* onVerticals(vertical, [0, 1], size);
  yield* onHorizontals(horizontal, BAND, size);
  yield* onVerticals(vertical, [2, 3, 4, 5], size);
}

/**
 * Places the labels in the order given (the reading order of the flows): each at the first place that touches no obstacle
 * (the steps, the labels of the gateways) and no label placed before; the first place tried when none is free.
 */
export function placeLabels(requests: readonly LabelRequest[], obstacles: readonly Rect[]): Map<string, Rect> {
  const placed = new Map<string, Rect>();
  for (const request of requests) {
    let first: Rect | undefined;
    let found: Rect | undefined;
    for (const rect of candidates(request)) {
      first ??= rect;
      if (obstacles.some((obstacle) => overlaps(rect, obstacle, CLEARANCE))) continue;
      if ([...placed.values()].some((other) => overlaps(rect, other, CLEARANCE))) continue;
      found = rect;
      break;
    }
    const chosen = found ?? first;
    if (chosen !== undefined) placed.set(request.id, chosen);
  }
  return placed;
}
