// routes.ts
// The drawing of each flow before its tracks: an orthogonal line through the aisles, the axes and the free bands, or a curve (annex B 3.4).

import type { Point } from '../grid.ts';
import {
  aisleX, axisX, centreY, columnFree, indexOf, isGateway, lineOf, lineTaken, portOf,
  type Context, type FlowKind, type RoutingFlow, type RoutingStep, type Side,
} from './model.ts';
import { needsDetour } from './sides.ts';

/** How a horizontal segment in a free band prefers to sit: a detour high in the band above a line, a loop low in the one under it. */
export type Use = 'centre' | 'detour' | 'loop';

/** The passage a segment runs in (a line, a free band, an aisle, the axis of a column) and its place there before its track. */
export interface Leg {
  readonly channel: string;
  readonly base: number;
  readonly use: Use;
  readonly horizontal: boolean;
}

/**
 * A flow drawn on the grid: its kind, sides and points (for a curve, its two ends), one leg per segment (for a curve, one
 * stub per end, so that its ends take tracks like those of the other flows). The group is what may share a track: the
 * flows of one step, but never two exits of a gateway (AR-24); the last segments into one side of a step share theirs
 * whatever their group (AR-129).
 */
export interface Draft {
  readonly flow: RoutingFlow;
  readonly group: string;
  readonly kind: FlowKind;
  readonly sourceSide: Side;
  readonly targetSide: Side;
  readonly points: Point[];
  readonly legs: readonly Leg[];
  readonly curve: boolean;
}

const centre = (context: Context, step: RoutingStep): Leg =>
  ({ channel: `h:line:${indexOf(context, step)}`, base: centreY(context, step), use: 'centre', horizontal: true });
const gap = (context: Context, boundary: number, use: Use): Leg =>
  ({ channel: `h:gap:${boundary}`, base: context.boundaries[boundary] ?? 0, use, horizontal: true });
const aisle = (column: number): Leg => ({ channel: `v:aisle:${column}`, base: aisleX(column), use: 'centre', horizontal: false });
const axis = (column: number): Leg => ({ channel: `v:axis:${column}`, base: axisX(column), use: 'centre', horizontal: false });

/**
 * The boundary above a line, where a detour runs, and the one under it, where a loop runs: the tracks then put the detour
 * at the top of the row + 14 (B-3.4-17) and the loop 14 above the next row, or on the next tracks of the band.
 */
const detourY = (context: Context, step: RoutingStep) => context.boundaries[indexOf(context, step)] ?? 0;
const loopY = (context: Context, step: RoutingStep) => context.boundaries[indexOf(context, step) + 1] ?? 0;

type Route = Pick<Draft, 'kind' | 'targetSide' | 'points' | 'legs'> & { readonly curve?: boolean };

/** How many segments drawn before run in a passage on an extent: a loop to another line takes its aisle by it. */
export type Load = (channel: string, from: number, to: number) => number;

/** Right to left on one free line, or with an elbow between two lines (B-3.4-21 to B-3.4-23). */
function forward(context: Context, source: RoutingStep, target: RoutingStep): Route {
  const [from, to] = [portOf(context, source, 'right'), portOf(context, target, 'left')];
  if (lineOf(source) === lineOf(target)) return { kind: 'flow', targetSide: 'left', points: [from, to], legs: [centre(context, source)] };
  const beforeArrival = !lineTaken(context, source, source.column, target.column) || lineTaken(context, target, source.column, target.column);
  const column = beforeArrival ? target.column - 1 : source.column;
  const x = aisleX(column);
  return {
    kind: 'flow', targetSide: 'left', points: [from, { x, y: from.y }, { x, y: to.y }, to],
    legs: [centre(context, source), aisle(column), centre(context, target)],
  };
}

/** Round the steps in the way, through the free band above the line of the target (B-3.4-15 to B-3.4-19). */
function detour(context: Context, source: RoutingStep, target: RoutingStep, side: Side): Route {
  const to = portOf(context, target, 'left');
  const y = detourY(context, target);
  const last = aisleX(target.column - 1);
  const band = gap(context, indexOf(context, target), 'detour');
  const tail = [{ x: last, y }, { x: last, y: to.y }, to];
  if (side === 'top') {
    const from = portOf(context, source, 'top');
    return { kind: 'detour', targetSide: 'left', points: [from, { x: from.x, y }, ...tail], legs: [axis(source.column), band, aisle(target.column - 1), centre(context, target)] };
  }
  const from = portOf(context, source, 'right');
  const first = aisleX(source.column);
  return {
    kind: 'detour', targetSide: 'left', points: [from, { x: first, y: from.y }, { x: first, y }, ...tail],
    legs: [centre(context, source), aisle(source.column), band, aisle(target.column - 1), centre(context, target)],
  };
}

/** A gateway leaving by its top or bottom point towards another line: along its column, then along the line of the target. */
function alongColumn(context: Context, source: RoutingStep, target: RoutingStep, side: Side): Route {
  const [from, to] = [portOf(context, source, side), portOf(context, target, 'left')];
  return { kind: 'flow', targetSide: 'left', points: [from, { x: from.x, y: to.y }, to], legs: [axis(source.column), centre(context, target)] };
}

/** Same column: straight down or up, else through the aisle on the right, right to right (B-3.4-01, B-3.4-02, B-3.4-14). */
function vertical(context: Context, source: RoutingStep, target: RoutingStep, side: Side): Route {
  const down = indexOf(context, target) > indexOf(context, source);
  if (side !== 'right' && columnFree(context, source.column, source, target)) {
    const [from, to] = down ? [portOf(context, source, 'bottom'), portOf(context, target, 'top')] : [portOf(context, source, 'top'), portOf(context, target, 'bottom')];
    return { kind: 'flow', targetSide: down ? 'top' : 'bottom', points: [from, to], legs: [axis(source.column)] };
  }
  const [from, to] = [portOf(context, source, 'right'), portOf(context, target, 'right')];
  const x = aisleX(source.column);
  return { kind: 'flow', targetSide: 'right', points: [from, { x, y: from.y }, { x, y: to.y }, to], legs: [centre(context, source), aisle(source.column), centre(context, target)] };
}

/**
 * Back to the left (B-3.4-03, B-3.4-25 to B-3.4-27): on one line, a curve under the steps, in the free band; to another
 * line, or from the right point of a gateway, an orthogonal line through the free bands under the two lines and an aisle
 * between the two columns, never across a step (spec 9.7; the curve of the 1.x crossed the lines between). The aisle is the
 * one where the flows drawn before leave the most room, the nearest to the target first.
 */
function loop(context: Context, source: RoutingStep, target: RoutingStep, side: Side, load: Load): Route {
  const end = portOf(context, target, 'bottom');
  const sameLine = lineOf(source) === lineOf(target);
  if (sameLine && side === 'bottom') {
    const start = portOf(context, source, 'bottom');
    return { kind: 'loop', targetSide: 'bottom', points: [start, end], legs: [axis(source.column), axis(target.column)], curve: true };
  }
  const y = loopY(context, source);
  const under = gap(context, indexOf(context, source) + 1, 'loop');
  const head: { points: Point[]; legs: Leg[] } = side === 'bottom'
    ? { points: [portOf(context, source, 'bottom'), { x: axisX(source.column), y }], legs: [axis(source.column)] }
    : (() => {
      const from = portOf(context, source, 'right');
      const x = aisleX(source.column);
      return { points: [from, { x, y: from.y }, { x, y }], legs: [centre(context, source), aisle(source.column)] };
    })();
  if (sameLine)
    return { kind: 'loop', targetSide: 'bottom', points: [...head.points, { x: end.x, y }, end], legs: [...head.legs, under, axis(target.column)] };
  const y2 = loopY(context, target);
  const over = gap(context, indexOf(context, target) + 1, 'loop');
  const headX = head.points.at(-1)?.x ?? end.x;
  let column = target.column;
  let fewest = Number.POSITIVE_INFINITY;
  for (let candidate = target.column; candidate < source.column; candidate++) {
    const x = aisleX(candidate);
    const crowd = load(`v:aisle:${candidate}`, Math.min(y, y2), Math.max(y, y2)) + load(under.channel, x, headX) + load(over.channel, end.x, x);
    if (crowd < fewest) [column, fewest] = [candidate, crowd];
  }
  const x = aisleX(column);
  return {
    kind: 'loop', targetSide: 'bottom', points: [...head.points, { x, y }, { x, y: y2 }, { x: end.x, y: y2 }, end],
    legs: [...head.legs, under, aisle(column), over, axis(target.column)],
  };
}

/** The drawing of a flow, leaving by the side given (its base side, or the one a gateway gave it). */
export function draftOf(context: Context, flow: RoutingFlow, source: RoutingStep, target: RoutingStep, side: Side, load: Load = () => 0): Draft {
  let route: Route;
  if (target.column < source.column) route = loop(context, source, target, side === 'bottom' ? 'bottom' : 'right', load);
  else if (target.column === source.column) route = vertical(context, source, target, side);
  else if ((side === 'top' || side === 'bottom') && lineOf(source) !== lineOf(target)) route = alongColumn(context, source, target, side);
  else if (side === 'top' || needsDetour(context, source, target)) route = detour(context, source, target, side === 'top' ? 'top' : 'right');
  else route = forward(context, source, target);
  const sourceSide = startSide(route);
  return {
    flow, group: isGateway(source) ? `flow:${flow.id}` : `step:${source.id}`, kind: route.kind, sourceSide, targetSide: route.targetSide,
    points: [...route.points], legs: route.legs, curve: route.curve === true,
  };
}

/** The side a route leaves by, read on its first segment: a horizontal start leaves by the right, a vertical one up or down. */
function startSide(route: Route): Side {
  const [first, second] = route.points;
  if (route.curve === true || first === undefined || second === undefined) return 'bottom';
  if (first.y === second.y) return 'right';
  return second.y > first.y ? 'bottom' : 'top';
}
