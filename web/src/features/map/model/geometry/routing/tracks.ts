// tracks.ts
// The tracks of 6 px: two segments of different flows that share a passage get distinct tracks, so that they never run on each other, but the flows into one side of a step end on one point (spec 9.7, AR-24, AR-25, AR-129).

import { FREE_BAND, loopDeeper, TRACK } from '../constants.ts';
import type { Draft, Leg } from './routes.ts';

/** The farthest a segment moves from its place: its neighbours move as far, so overlaps are judged with this margin. */
const REACH = 4 * TRACK;

/** The tracks of a line, an aisle or an axis, the middle first, then alternately above and under (0, −6, 6, −12, 12...). */
const AROUND = [0, -1, 1, -2, 2, -3, 3, -4, 4].map((step) => step * TRACK);

/**
 * The tracks of a free band, from the boundary between two rows: high in the band above a line for a detour (top of the row
 * + 14 first, B-3.4-17, then 6 px nearer the boundary, 6 px farther, and so on), low under it for a loop.
 */
const DETOUR = [14, 8, 20, 2, 26, -4, -10, -16, -22];
const LOOP = DETOUR.map((offset) => -offset);

/** The stubs of a curve: its ends go down this far, the depth of the curve under the steps (B-3.4-27). */
const STUB = FREE_BAND - 4;

interface Item {
  readonly draft: number;
  readonly leg: number;
  readonly group: string;
  /** For the last segment of a flow, the side of the step it ends on: those of one side share one track (AR-129). */
  readonly arrival: string | null;
  readonly channel: string;
  readonly offsets: readonly number[];
  readonly from: number;
  readonly to: number;
}

/** The tracks a leg may take: those of its passage; in a free band, those inside it (the first and last band have one side). */
function offsetsOf(leg: Leg, boundary: number, last: number): readonly number[] {
  if (!leg.channel.startsWith('h:gap:')) return AROUND;
  const list = leg.use === 'loop' ? LOOP : DETOUR;
  return list.filter((offset) => (boundary > 0 || offset > 0) && (boundary < last || offset < 0));
}

/** The extent of the segment of a leg along its passage, from the points of its draft; a curve deeper by the band under the steps. */
export function extentOf(draft: Draft, leg: number, horizontal: boolean, below = 0): [number, number] {
  if (draft.curve) {
    const end = draft.points[leg === 0 ? 0 : draft.points.length - 1] ?? { x: 0, y: 0 };
    return [end.y, end.y + STUB + loopDeeper(below)];
  }
  const [a, b] = [draft.points[leg], draft.points[leg + 1]];
  if (a === undefined || b === undefined) return [0, 0];
  return horizontal ? [Math.min(a.x, b.x), Math.max(a.x, b.x)] : [Math.min(a.y, b.y), Math.max(a.y, b.y)];
}

/** Two items may run on one track: of one group, or ending on one side of a step. */
const shares = (a: Item, b: Item): boolean => a.group === b.group || (a.arrival !== null && a.arrival === b.arrival);

/**
 * Gives each item the first track of its list that no item it does not share with takes on an overlapping extent. The
 * last segments into one side of a step are placed together, on the track that suits them all, so that they end on one
 * point and show one arrowhead (AR-129).
 */
function assign(items: Item[]): Map<Item, number> {
  const chosen = new Map<Item, number>();
  const taken = new Map<string, { item: Item; offset: number }[]>();
  items.sort((a, b) => (a.channel < b.channel ? -1 : a.channel > b.channel ? 1 : a.from - b.from || a.draft - b.draft || a.leg - b.leg));
  for (const item of items) {
    if (chosen.has(item)) continue;
    const members = item.arrival === null ? [item] : items.filter((other) => other.channel === item.channel && other.arrival === item.arrival);
    const others = taken.get(item.channel) ?? [];
    const clashes = (offset: number) => members.reduce((count, member) => count + others.filter((other) => other.offset === offset
      && !shares(other.item, member) && other.item.from < member.to && member.from < other.item.to).length, 0);
    let best = item.offsets[0] ?? 0;
    let fewest = Number.POSITIVE_INFINITY;
    for (const offset of item.offsets) {
      const count = clashes(offset);
      if (count < fewest) [best, fewest] = [offset, count];
      if (count === 0) break;
    }
    for (const member of members) chosen.set(member, best);
    taken.set(item.channel, [...others, ...members.map((member) => ({ item: member, offset: best }))]);
  }
  return chosen;
}

function itemsOf(drafts: readonly Draft[], horizontal: boolean, boundaries: readonly number[], margin: number, below: number): Item[] {
  const items: Item[] = [];
  drafts.forEach((draft, index) => {
    draft.legs.forEach((leg, at) => {
      if (leg.horizontal !== horizontal) return;
      const [from, to] = extentOf(draft, at, horizontal, below);
      const boundary = Number(leg.channel.split(':')[2] ?? 0);
      const arrival = at === draft.legs.length - 1 ? `${draft.flow.targetId}:${draft.targetSide}` : null;
      items.push({ draft: index, leg: at, group: draft.group, arrival, channel: leg.channel, offsets: offsetsOf(leg, boundary, boundaries.length - 1),
        from: from - margin, to: to + margin });
    });
  });
  return items;
}

/** Moves the points of the segment of a leg (both ends) to its track; a curve moves the end of its stub. */
function move(draft: Draft, leg: number, horizontal: boolean, value: number): void {
  const points = draft.points;
  const indexes = draft.curve ? [leg === 0 ? 0 : points.length - 1] : [leg, leg + 1];
  for (const index of indexes) {
    const point = points[index];
    if (point !== undefined) points[index] = horizontal ? { x: point.x, y: value } : { x: value, y: point.y };
  }
}

/**
 * Puts every segment on its track, the horizontal ones first (their extents widened by the farthest move of the vertical
 * ones), then the vertical ones on the extents left by the first move. The drafts are changed in place.
 */
export function placeOnTracks(drafts: readonly Draft[], boundaries: readonly number[], below = 0): void {
  for (const horizontal of [true, false]) {
    const chosen = assign(itemsOf(drafts, horizontal, boundaries, horizontal ? REACH : 0, below));
    for (const [item, offset] of chosen) {
      const draft = drafts[item.draft];
      const leg = draft?.legs[item.leg];
      if (draft !== undefined && leg !== undefined) move(draft, item.leg, horizontal, leg.base + offset);
    }
  }
}
