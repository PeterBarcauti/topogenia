// routeGestures.ts
// What a gesture on the route of a flow reads (ADR 091): the handles a flow chosen shows, the side of a step nearest to the pointer, and the passage nearest to a segment moved: an aisle or the middle of a column, a band or the middle of a row.

import type { Schema } from '../../../api/client.ts';
import { COLUMN_WIDTH, FREE_BAND, STEP_HEIGHT } from './geometry/constants.ts';
import type { Point, Rect } from './geometry/grid.ts';
import { axisX, type Passage, type RoutedFlow, type Side } from './geometry/routing/model.ts';
import type { LaneBand } from './mapLayout.ts';

/** How far from a point of a step the pointer still takes it, in units of the canvas. */
export const SIDE_REACH = 48;

export const SIDES: readonly Side[] = ['top', 'right', 'bottom', 'left'];

/** The sides a flow drawn leaves its source by and reaches its target by, read on the drawing. */
export interface DrawnEnds {
  readonly source: Side;
  readonly target: Side;
}

/** The ends of each flow drawn; a flow not drawn is absent. */
export type DrawnSides = ReadonlyMap<string, DrawnEnds>;

/** The ends of the flows of a drawing. */
export const drawnSidesOf = (flows: readonly { readonly route: Pick<RoutedFlow, 'id' | 'sourceSide' | 'targetSide'> }[]): DrawnSides =>
  new Map(flows.map((figure) => [figure.route.id, { source: figure.route.sourceSide, target: figure.route.targetSide }]));

const GATEWAYS = ['decision', 'parallel', 'inclusive'];

/**
 * The sides an end of a flow may take, on a gateway only (any side of any other step):
 * - a departure, neither its entry, the left point, nor one that another of its exits chose (AR-24), nor one where an
 *   arrival ends, as drawn (as chosen when the drawing is not given): an automatic arrival on a vertex too (audit 3.1.0
 *   3.3, audit 3.1 V2 3.3);
 * - an arrival, not a point one of its exits leaves by, as drawn (as chosen when the drawing is not given): the arrowhead
 *   and the exit would share a vertex of the diamond, and one of them would leave it for its track (audit 3.1.0 3.3, F-85).
 */
export function allowedSides(map: Schema<'MapView'>, flowId: string, end: 'source' | 'target', drawn: DrawnSides = new Map()): readonly Side[] {
  const flow = map.flows.find((item) => item.id === flowId);
  const stepId = end === 'source' ? flow?.source_step_id : flow?.target_step_id;
  const step = map.steps.find((candidate) => candidate.id === stepId);
  if (flow === undefined || step === undefined || !GATEWAYS.includes(step.type)) return SIDES;
  const others = map.flows.filter((item) => item.id !== flowId);
  const exits = others.filter((item) => item.source_step_id === step.id);
  const taken = new Set<string | null>(end === 'source'
    ? ['left', ...exits.map((item) => item.route?.source_side ?? null),
      ...others.filter((item) => item.target_step_id === step.id).map((item) => drawn.get(item.id)?.target ?? item.route?.target_side ?? null)]
    : exits.map((item) => drawn.get(item.id)?.source ?? item.route?.source_side ?? null));
  return SIDES.filter((side) => !taken.has(side));
}

/** The point of each side of a box: the middle of the side, where a flow leaves or arrives. */
export function sidePoints(box: Rect): Record<Side, Point> {
  return {
    top: { x: box.x + box.width / 2, y: box.y },
    right: { x: box.x + box.width, y: box.y + box.height / 2 },
    bottom: { x: box.x + box.width / 2, y: box.y + box.height },
    left: { x: box.x, y: box.y + box.height / 2 },
  };
}

/** The side whose point is nearest to the pointer, within reach, among the sides allowed; null when none is near. */
export function nearestSide(box: Rect, pointer: Point, allowed: readonly Side[]): Side | null {
  const points = sidePoints(box);
  let best: { side: Side; distance: number } | null = null;
  for (const side of allowed) {
    const point = points[side];
    const distance = Math.hypot(point.x - pointer.x, point.y - pointer.y);
    if (distance <= SIDE_REACH && (best === null || distance < best.distance)) best = { side, distance };
  }
  return best?.side ?? null;
}

/**
 * The vertical passage nearest to an x (ADR 091): an aisle, the one right of a column, or the middle of a column, every
 * half column in turn; from the aisle before the column 0 to the one right of the last column of the map, so that a
 * passage never leads out of it.
 */
export function passageNearX(x: number, lastColumn: number): Passage {
  const half = COLUMN_WIDTH / 2;
  const highest = 2 * Math.max(-1, lastColumn) + 1;
  // The half columns from the middle of the column 0: even, the middle of a column; odd, the aisle that follows it.
  const index = Math.min(highest, Math.max(-1, Math.round((x - axisX(0)) / half)));
  return index % 2 === 0 ? { column: index / 2 } : { aisle: (index - 1) / 2 };
}

/** The tops of the lines of the map from the top, a line being a row of a lane, and the bottom of the last one. */
function linesOf(lanes: readonly LaneBand[]): { laneId: string; row: number; top: number; height: number }[] {
  return lanes.flatMap((band) => Array.from({ length: band.rows }, (_, row) =>
    ({ laneId: band.lane.id, row, top: band.top + (row * band.height) / band.rows, height: band.height / band.rows })));
}

/**
 * The horizontal passage nearest to a y (ADR 091): the middle of the line the pointer is in when it is nearer than the
 * boundary nearest to it, else that band: the top of the line under the boundary or the bottom of the line above it,
 * by the half the pointer is in; the top of the first line and the bottom of the last one at the edges of the map.
 */
export function passageNearY(lanes: readonly LaneBand[], y: number): Passage | null {
  const band = bandNear(lanes, y);
  const line = linesOf(lanes).find((item) => y >= item.top && y < item.top + item.height);
  if (line === undefined) return band;
  // The boundary nearest to the pointer is the top or the bottom of its line. The middle of the line is the axis of its
  // steps, where the drawing runs it, not the middle of its height, which the band of the value stream lengthens under the
  // steps (audit 3.1.0 3.1): without that band both are one.
  const axis = line.top + FREE_BAND + STEP_HEIGHT / 2;
  const toBoundary = Math.min(y - line.top, line.top + line.height - y);
  return Math.abs(y - axis) < toBoundary ? { laneId: line.laneId, row: line.row } : band;
}

/**
 * The band nearest to a y: the boundary between two lines nearest to it, then the half of the band the pointer is in,
 * the top of the line under the boundary or the bottom of the line above it; the top of the first line and the bottom of
 * the last one at the edges of the map.
 */
function bandNear(lanes: readonly LaneBand[], y: number): Passage | null {
  const lines = linesOf(lanes);
  const last = lines.at(-1);
  const lastBand = lanes.at(-1);
  if (last === undefined || lastBand === undefined) return null;
  const boundaries = [...lines.map((line) => line.top), last.top + lastBand.height / lastBand.rows];
  let index = 0;
  boundaries.forEach((boundary, at) => {
    if (Math.abs(boundary - y) < Math.abs((boundaries[index] ?? 0) - y)) index = at;
  });
  const below = lines[index];
  const above = lines[index - 1];
  if (below !== undefined && (y >= (boundaries[index] ?? 0) || above === undefined)) return { laneId: below.laneId, row: below.row, edge: 'top' };
  return above === undefined ? null : { laneId: above.laneId, row: above.row, edge: 'bottom' };
}

/** A segment of a flow a gesture may move: its place in the path, its middle, and its direction. */
export interface SegmentHandle {
  readonly index: number;
  readonly middle: Point;
  readonly horizontal: boolean;
}

/**
 * The segments of a flow a gesture may move: those inside its path, between its first and its last; the longest when it
 * has none inside; for a loop drawn as a curve, its bottom, moved up or down to a band.
 */
export function segmentHandles(flow: RoutedFlow): SegmentHandle[] {
  if (flow.curve !== null) return [{ index: 0, middle: { x: (flow.curve.start.x + flow.curve.end.x) / 2, y: curveBottom(flow.curve) }, horizontal: true }];
  const segments = flow.points.slice(1).map((b, at) => {
    const a = flow.points[at] as Point;
    return { index: at, middle: { x: (a.x + b.x) / 2, y: (a.y + b.y) / 2 }, horizontal: a.y === b.y, length: Math.abs(a.x - b.x) + Math.abs(a.y - b.y) };
  });
  const inside = segments.slice(1, -1).filter((segment) => segment.length > 0);
  const chosen = inside.length > 0 ? inside : [...segments].sort((a, b) => b.length - a.length).slice(0, 1);
  return chosen.map(({ index, middle, horizontal }) => ({ index, middle, horizontal }));
}

/**
 * The lowest point of the curve of a loop, M start C (start.x, low) (end.x, low) end: its point at t = 1/2, three
 * quarters of the way to low (audit 3.1.0 3.5); the handle sits on the line drawn.
 */
const curveBottom = (curve: NonNullable<RoutedFlow['curve']>): number => (curve.start.y + curve.end.y) / 8 + (3 * curve.low) / 4;

/** The two ends of a flow drawn, where its handles are. */
export function endsOf(flow: RoutedFlow): { source: Point; target: Point } | null {
  if (flow.curve !== null) return { source: flow.curve.start, target: flow.curve.end };
  const [source, target] = [flow.points[0], flow.points.at(-1)];
  return source === undefined || target === undefined ? null : { source, target };
}
