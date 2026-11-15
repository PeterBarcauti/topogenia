// chosen.ts
// The drawing of a flow whose route the user chose (ADR 091): from the side chosen at its source to the side chosen at its target, through the aisles and the bands of the grid, its passage included, with the fewest bends; never across a step.

import { FREE_BAND, STEP_HEIGHT } from '../constants.ts';
import type { Point, Rect } from '../grid.ts';
import { aisleX, axisX, boxOf, centreY, indexOf, isGateway, portOf, type Context, type Passage, type RoutingFlow, type RoutingStep, type Side } from './model.ts';
import type { Draft, Leg, Load } from './routes.ts';

/** How far the search looks around the two steps: the aisles and bands of two more columns and lines on each side. */
const AROUND = 2;

/** The most passages between the two ends: enough to go round any step, few enough to stay readable. */
const MOST_PASSAGES = 5;

/** A passage of the grid: a horizontal one (a band between two lines, the middle of a line) or a vertical one (an aisle, the axis of a column). */
interface Channel {
  readonly name: string;
  readonly horizontal: boolean;
  /** Its y for a horizontal passage, its x for a vertical one. */
  readonly at: number;
  /** A band or an aisle: free of steps along its whole length. */
  readonly free: boolean;
  /** For a band, the index of its boundary between two lines. */
  readonly boundary?: number;
}

/** The middle of a line, by its index: where its steps stand, so not free of them. */
const lineChannelAt = (context: Context, line: number): Channel =>
  ({ name: `h:line:${String(line)}`, horizontal: true, at: (context.boundaries[line] ?? 0) + FREE_BAND + STEP_HEIGHT / 2, free: false });
const lineChannel = (context: Context, step: RoutingStep): Channel => ({ ...lineChannelAt(context, indexOf(context, step)), at: centreY(context, step) });
const axisChannel = (column: number): Channel => ({ name: `v:axis:${column}`, horizontal: false, at: axisX(column), free: false });
const aisleChannel = (column: number): Channel => ({ name: `v:aisle:${column}`, horizontal: false, at: aisleX(column), free: true });
const bandChannel = (context: Context, boundary: number): Channel =>
  ({ name: `h:gap:${boundary}`, horizontal: true, at: context.boundaries[boundary] ?? 0, free: true, boundary });

/** The channel a side of a step leaves or reaches by: the middle of its line, or the axis of its column. */
const channelOf = (context: Context, step: RoutingStep, side: Side): Channel =>
  side === 'left' || side === 'right' ? lineChannel(context, step) : axisChannel(step.column);

/** The way out of a side, as the sign of the move along its channel: +1 to the right or down, −1 to the left or up. */
const outward = (side: Side): number => (side === 'right' || side === 'bottom' ? 1 : -1);

const cross = (a: Channel, b: Channel): Point => (a.horizontal ? { x: b.at, y: a.at } : { x: a.at, y: b.at });

/** The boxes of the steps of a map, by the y of the middle of their line and by the x of their axis: a segment of a line or an axis meets only those. */
interface Boxes {
  readonly byLine: ReadonlyMap<number, readonly Rect[]>;
  readonly byAxis: ReadonlyMap<number, readonly Rect[]>;
}

const boxesOfContext = new WeakMap<Context, Boxes>();

function boxesOf(context: Context): Boxes {
  const known = boxesOfContext.get(context);
  if (known !== undefined) return known;
  const byLine = new Map<number, Rect[]>();
  const byAxis = new Map<number, Rect[]>();
  for (const step of context.steps.values()) {
    const box = boxOf(context, step);
    byLine.set(centreY(context, step), [...(byLine.get(centreY(context, step)) ?? []), box]);
    byAxis.set(axisX(step.column), [...(byAxis.get(axisX(step.column)) ?? []), box]);
  }
  const boxes = { byLine, byAxis };
  boxesOfContext.set(context, boxes);
  return boxes;
}

/** Whether a segment of a line or of an axis runs through a step: its open extent meets the inside of a box of that line or axis. */
function blockedOn(boxes: Boxes, from: Point, to: Point): boolean {
  return blocked((from.y === to.y ? boxes.byLine.get(from.y) : boxes.byAxis.get(from.x)) ?? [], from, to);
}

/** Whether a segment runs through a step: its open extent meets the inside of a box. */
function blocked(boxes: readonly Rect[], from: Point, to: Point): boolean {
  if (from.y === to.y) {
    const [low, high] = [Math.min(from.x, to.x), Math.max(from.x, to.x)];
    return boxes.some((box) => from.y > box.y && from.y < box.y + box.height && low < box.x + box.width && high > box.x);
  }
  const [low, high] = [Math.min(from.y, to.y), Math.max(from.y, to.y)];
  return boxes.some((box) => from.x > box.x && from.x < box.x + box.width && low < box.y + box.height && high > box.y);
}

/** The line a passage in a row names, or null when its lane or its row is not on the map (the drawing then chooses). */
export function lineOfPassage(context: Context, passage: Passage): number | null {
  if (!('laneId' in passage)) return null;
  return context.lineIndex.get(`${passage.laneId}:${String(passage.row)}`) ?? null;
}

/** The boundary a passage along a band names: the top of its line or the bottom; null for another passage or a line not on the map. */
export function bandOf(context: Context, passage: Passage): number | null {
  if (!('laneId' in passage) || passage.edge === undefined) return null;
  const line = lineOfPassage(context, passage);
  return line === null ? null : line + (passage.edge === 'bottom' ? 1 : 0);
}

/** The last column of the steps of a map, −1 without any step. */
const lastColumnOf = (context: Context): number => [...context.steps.values()].reduce((last, step) => Math.max(last, step.column), -1);

/**
 * The channel of the passage chosen, or null when it names nothing of the map: an aisle from the one before the column 0
 * to the one right of the last column, which never leads out of the map; the middle of a column from the first to the
 * last; a band, or the middle of a line, of a row of a lane of the map.
 */
function passageChannel(context: Context, passage: Passage | undefined): Channel | null {
  if (passage === undefined) return null;
  if ('aisle' in passage)
    return Number.isInteger(passage.aisle) && passage.aisle >= -1 && passage.aisle <= lastColumnOf(context) ? aisleChannel(passage.aisle) : null;
  if ('column' in passage)
    return Number.isInteger(passage.column) && passage.column >= 0 && passage.column <= lastColumnOf(context) ? axisChannel(passage.column) : null;
  if (passage.edge === undefined) {
    const line = lineOfPassage(context, passage);
    return line === null ? null : lineChannelAt(context, line);
  }
  const boundary = bandOf(context, passage);
  return boundary === null ? null : bandChannel(context, boundary);
}

/** Whether a passage names a place of the map: an aisle, the middle of a column, or a row of a lane of the map, along a band or through its middle. */
export const passageHolds = (context: Context, passage: Passage | undefined): boolean => passageChannel(context, passage) !== null;

/** The cost of a path, compared in this order: its bends, its crowd (the segments drawn before on its passages), its length. */
type Cost = readonly [number, number, number];

const less = (a: Cost, b: Cost): boolean => a[0] < b[0] || (a[0] === b[0] && (a[1] < b[1] || (a[1] === b[1] && a[2] < b[2])));

const plus = (cost: Cost, bends: number, crowd: number, length: number): Cost => [cost[0] + bends, cost[1] + crowd, cost[2] + length];

interface State {
  readonly channel: Channel;
  readonly previous: Channel;
  readonly via: boolean;
  readonly cost: Cost;
  readonly path: readonly Channel[];
}

/** The passages the search may take: the aisles and bands around the two steps, and the passage chosen. */
function candidates(context: Context, source: RoutingStep, target: RoutingStep, chosen: Channel | null): Channel[] {
  const [low, high] = [Math.min(source.column, target.column) - 1 - AROUND, Math.max(source.column, target.column) + AROUND];
  const aisles = Array.from({ length: high - low + 1 }, (_, index) => low + index).filter((column) => column >= -1).map(aisleChannel);
  const [top, bottom] = [Math.min(indexOf(context, source), indexOf(context, target)) - AROUND, Math.max(indexOf(context, source), indexOf(context, target)) + 1 + AROUND];
  const last = context.boundaries.length - 1;
  const bands = Array.from({ length: bottom - top + 1 }, (_, index) => top + index).filter((boundary) => boundary >= 0 && boundary <= last)
    .map((boundary) => bandChannel(context, boundary));
  const list = [...aisles, ...bands];
  if (chosen !== null && !list.some((channel) => channel.name === chosen.name)) list.push(chosen);
  return list;
}

/** The way a segment of a band is placed on its tracks: low under the line above for a loop, high above the line below otherwise. */
function bandUse(context: Context, channel: Channel, source: RoutingStep, target: RoutingStep, via: Channel | null, passage: Passage | undefined): Leg['use'] {
  if (via !== null && channel.name === via.name && passage !== undefined && 'edge' in passage) return passage.edge === 'bottom' ? 'loop' : 'detour';
  return channel.boundary === indexOf(context, source) + 1 || channel.boundary === indexOf(context, target) + 1 ? 'loop' : 'detour';
}

/** What the search knows of the two ends of a flow: its ports, their passages, the passage chosen, and its checks. */
interface Ends {
  readonly start: Point;
  readonly end: Point;
  readonly first: Channel;
  readonly final: Channel;
  readonly chosen: Channel | null;
  /** The first segment leaves outward, free of steps. */
  readonly leaves: (corner: Point) => boolean;
  /** The last segment arrives from outside, free of steps. */
  readonly arrives: (corner: Point) => boolean;
  readonly crowd: (channel: Channel, from: Point, to: Point) => number;
  /** A segment of a passage that meets no step: always on an aisle or a band, checked on a line or an axis. */
  readonly clear: (channel: Channel, from: Point, to: Point) => boolean;
  /** Whether a channel is the passage chosen. */
  readonly takes: (channel: Channel) => boolean;
}

interface Found {
  readonly cost: Cost;
  readonly path: readonly Channel[];
}

const along = (channel: Channel, from: Point, to: Point): number => (channel.horizontal ? Math.abs(to.x - from.x) : Math.abs(to.y - from.y));

const better = (found: Found | null, cost: Cost, path: readonly Channel[]): Found | null => (found === null || less(cost, found.cost) ? { cost, path } : found);

function endsOf(context: Context, flow: RoutingFlow, source: RoutingStep, target: RoutingStep, sides: readonly [Side, Side], load: Load): Ends {
  const [sourceSide, targetSide] = sides;
  const boxes = boxesOf(context);
  const start = portOf(context, source, sourceSide);
  const end = portOf(context, target, targetSide);
  const first = channelOf(context, source, sourceSide);
  const final = channelOf(context, target, targetSide);
  const chosen = passageChannel(context, flow.route?.via);
  return {
    start, end, first, final, chosen,
    clear: (channel, from, to) => channel.free || !blockedOn(boxes, from, to),
    takes: (channel) => chosen !== null && channel.name === chosen.name,
    leaves: (corner) => Math.sign(first.horizontal ? corner.x - start.x : corner.y - start.y) === outward(sourceSide) && !blockedOn(boxes, start, corner),
    arrives: (corner) => Math.sign(final.horizontal ? end.x - corner.x : end.y - corner.y) === -outward(targetSide) && !blockedOn(boxes, corner, end),
    crowd: (channel, from, to) => {
      const [a, b] = channel.horizontal ? [from.x, to.x] : [from.y, to.y];
      return load(channel.name, Math.min(a, b), Math.max(a, b));
    },
  };
}

/**
 * Straight on one line or one axis, or one bend where the passages of the two ends cross: without a passage chosen, or
 * when the passage chosen is the line or the axis of an end itself.
 */
function direct(ends: Ends): Found | null {
  const { start, end, first, final, crowd } = ends;
  let found: Found | null = null;
  if (ends.chosen !== null && !ends.takes(first) && !ends.takes(final)) return found;
  if (first.name === final.name && ends.leaves(end) && ends.arrives(start)) found = better(found, [0, crowd(first, start, end), along(first, start, end)], [first]);
  if (first.horizontal !== final.horizontal) {
    const corner = cross(first, final);
    if (ends.leaves(corner) && ends.arrives(corner))
      found = better(found, [1, crowd(first, start, corner) + crowd(final, corner, end), along(first, start, corner) + along(final, corner, end)], [first, final]);
  }
  return found;
}

/** The path of a state that ends at the target, which it reaches from outside, the passage chosen taken. */
function ending(ends: Ends, state: State, found: Found | null): Found | null {
  const { final, end, crowd } = ends;
  if (state.channel.horizontal === final.horizontal || (ends.chosen !== null && !state.via && !ends.takes(final))) return found;
  const entry = cross(state.previous, state.channel);
  const corner = cross(state.channel, final);
  if (!ends.arrives(corner) || (corner.x === entry.x && corner.y === entry.y) || !ends.clear(state.channel, entry, corner)) return found;
  return better(found, plus(state.cost, 1, crowd(state.channel, entry, corner) + crowd(final, corner, end), along(state.channel, entry, corner) + along(final, corner, end)),
    [...state.path, final]);
}

/** The states one passage further, each pair of passages kept at its least cost. */
function further(ends: Ends, states: readonly State[], passages: readonly Channel[], reached: Map<string, Cost>): State[] {
  const next: State[] = [];
  for (const state of states) {
    const entry = cross(state.previous, state.channel);
    for (const channel of passages) {
      if (channel.horizontal === state.channel.horizontal || channel.name === state.previous.name) continue;
      const corner = cross(state.channel, channel);
      if ((corner.x === entry.x && corner.y === entry.y) || !ends.clear(state.channel, entry, corner)) continue;
      const via = state.via || ends.takes(channel);
      const cost = plus(state.cost, 1, ends.crowd(state.channel, entry, corner), along(state.channel, entry, corner));
      const key = `${channel.name}|${state.channel.name}|${via ? 1 : 0}`;
      const known = reached.get(key);
      if (known !== undefined && !less(cost, known)) continue;
      reached.set(key, cost);
      next.push({ channel, previous: state.channel, via, cost, path: [...state.path, channel] });
    }
  }
  return next;
}

/**
 * The path of the fewest bends, the least crowded, then the shortest: direct, or through the passages. A path of one more
 * passage has one more bend, so the search ends at the depth where a path is found.
 */
function bestPath(ends: Ends, passages: readonly Channel[]): Found | null {
  const { first, start, crowd } = ends;
  let found = direct(ends);
  let states: State[] = passages.filter((channel) => channel.horizontal !== first.horizontal && ends.leaves(cross(first, channel))).map((channel) => {
    const corner = cross(first, channel);
    return { channel, previous: first, via: ends.takes(first) || ends.takes(channel), cost: [1, crowd(first, start, corner), along(first, start, corner)] as Cost, path: [first, channel] };
  });
  const reached = new Map<string, Cost>();
  const bends = () => found?.cost[0] ?? Number.POSITIVE_INFINITY;
  for (let depth = 1; depth <= MOST_PASSAGES && states.length > 0 && bends() > depth; depth++) {
    for (const state of states) found = ending(ends, state, found);
    if (depth === MOST_PASSAGES || bends() <= depth + 1) break;
    states = further(ends, states, passages, reached);
  }
  return found;
}

/**
 * The drawing of a flow from a side of its source to a side of its target (ADR 091), through the passage chosen when it
 * names a place of the map: the path of the fewest bends, the least crowded, then the shortest, among the middle of the
 * two lines or axes of the two steps, free up to the first passage, and the aisles and bands around them. The same
 * map gives the same path: the passages are tried in a fixed order. Null only when no path exists, which a map never
 * gives, an aisle and a band crossing everywhere.
 */
export function chosenDraft(context: Context, flow: RoutingFlow, source: RoutingStep, target: RoutingStep, sourceSide: Side, targetSide: Side, load: Load): Draft | null {
  const ends = endsOf(context, flow, source, target, [sourceSide, targetSide], load);
  const found = bestPath(ends, candidates(context, source, target, ends.chosen));
  if (found === null) return null;
  const path = found.path;
  const points = [ends.start, ...path.slice(0, -1).map((channel, index) => cross(channel, path[index + 1] as Channel)), ends.end];
  const legs: Leg[] = path.map((channel) => ({
    channel: channel.name,
    base: channel.at,
    use: channel.free && channel.horizontal ? bandUse(context, channel, source, target, ends.chosen, flow.route?.via) : 'centre',
    horizontal: channel.horizontal,
  }));
  return {
    flow, group: isGateway(source) ? `flow:${flow.id}` : `step:${source.id}`, kind: target.column < source.column ? 'loop' : 'flow',
    sourceSide, targetSide, points, legs, curve: false,
  };
}
