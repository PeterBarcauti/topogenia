// laneRows.ts
// A lane taller or shorter by a row (ADR 096): its border pulled, its border double-clicked, its menu; what each asks of the editor (empty rows under what the lane holds) and of the host (rows inserted or taken out where it holds something), and the map drawn while a pull is under way.

import type { Schema } from '../../../api/client.ts';
import { LAST_ROW, PULL_THRESHOLD } from './geometry/constants.ts';
import { holdingCells, type LaneRows } from './gridShape.ts';
import type { RowBand } from './moves.ts';

type MapView = Schema<'MapView'>;
type HeldMap = Pick<MapView, 'steps' | 'flows'>;

/** The end of a lane that grows: its bottom, or its top. */
export type LaneEnd = 'top' | 'bottom';

/** The rows a lane holds: down to its lowest step or flow pinned to a row, none when it holds nothing. */
export const heldRows = (map: HeldMap, laneId: string): number =>
  holdingCells(map).reduce((rows, cell) => (cell.laneId === laneId ? Math.max(rows, cell.row + 1) : rows), 0);

/** The rows a lane shows: those it holds, at least one, or those it shows at least when more. */
export const rowsShown = (map: HeldMap, laneId: string, rows: LaneRows): number => Math.max(1, heldRows(map, laneId), rows[laneId] ?? 1);

/** The rows a lane can still take, up to the last row the host knows (rows 0 to 50). */
export const roomOf = (map: HeldMap, laneId: string, rows: LaneRows): number => Math.max(0, LAST_ROW + 1 - rowsShown(map, laneId, rows));

/** Whether a row of a lane holds a step or a flow pinned to it: such a row is never taken out. */
export const holds = (map: HeldMap, laneId: string, row: number): boolean => holdingCells(map).some((cell) => cell.laneId === laneId && cell.row === row);

/** The rows a pull of `dy` pixels of the screen gives, a row being `rowPx` pixels: none under 4 px, else one at least, one more each row pulled. */
export function rowsPulled(dy: number, rowPx: number): number {
  const distance = Math.abs(dy);
  return distance < PULL_THRESHOLD || rowPx <= 0 ? 0 : Math.max(1, Math.round(distance / rowPx));
}

/** The lane a pull of a border grows and its end: the borders count from the top of the first lane (0) to the bottom of the last. */
export interface Pulled {
  readonly laneId: string;
  readonly end: LaneEnd;
}

/**
 * The border follows the hand: pulled down, the lane above it grows at its bottom; pulled up, the lane below it grows at
 * its top. Null when no lane is on that side, above the first lane pulled down or under the last one pulled up.
 */
export function pulledLane(lanes: readonly string[], border: number, dy: number): Pulled | null {
  const laneId = dy > 0 ? lanes[border - 1] : dy < 0 ? lanes[border] : undefined;
  return laneId === undefined ? null : { laneId, end: dy > 0 ? 'bottom' : 'top' };
}

/**
 * The rows a lane shows at least once what it holds went from `before` to `after` rows on the host: its empty rows under
 * what it holds keep their number; without any, the rows it showed at least never exceed what it holds, so that an undo
 * leaves the lane as it was.
 */
function keptEmpty(rows: LaneRows, laneId: string, before: number, after: number): LaneRows {
  const least = rows[laneId];
  if (least === undefined) return rows;
  return { ...rows, [laneId]: least > before ? after + least - before : Math.min(least, after) };
}

/** A change of the rows of a lane: the rows the lanes show at least, and what the host does. */
export interface RowChange {
  readonly rows: LaneRows;
  /** Rows the host inserts at the top of the lane, what it holds going down; 0 when the editor alone gives them. */
  readonly inserted: number;
  /** The row the host takes out, what the lane holds under it going up; null when the editor alone takes it. */
  readonly removed: number | null;
}

/**
 * A lane taller by `count` rows at an end, as many as there is room for; null when none. At the bottom, or for a lane
 * that holds nothing, the editor shows them; at the top of a lane that holds something, the host inserts them.
 */
export function growth(map: HeldMap, rows: LaneRows, laneId: string, end: LaneEnd, count: number): RowChange | null {
  const shown = rowsShown(map, laneId, rows);
  const added = Math.min(count, LAST_ROW + 1 - shown);
  if (added <= 0) return null;
  const held = heldRows(map, laneId);
  if (end === 'bottom' || held === 0) return { rows: { ...rows, [laneId]: shown + added }, inserted: 0, removed: null };
  return { rows: keptEmpty(rows, laneId, held, held + added), inserted: added, removed: null };
}

/**
 * A lane shorter by its first or its last row: null when that row holds something or is the only row of the lane. A row
 * under what the lane holds is one of its empty rows, which the editor takes away; the host takes out another one.
 */
export function shrinking(map: HeldMap, rows: LaneRows, laneId: string, end: LaneEnd): RowChange | null {
  const shown = rowsShown(map, laneId, rows);
  const row = end === 'top' ? 0 : shown - 1;
  if (shown <= 1 || holds(map, laneId, row)) return null;
  const held = heldRows(map, laneId);
  if (row >= held) return { rows: { ...rows, [laneId]: shown - 1 }, inserted: 0, removed: null };
  return { rows: keptEmpty(rows, laneId, held, held - 1), inserted: 0, removed: row };
}

/** A pull under way: its lane, its end and its rows; sent once released, until the host has answered. */
export interface RowsDraft extends Pulled {
  readonly count: number;
  readonly sent: boolean;
  /** The structure version of the map the pull started on: the map its write gives back is drawn as it is. */
  readonly version: number;
}

/** The map and the rows drawn with a pull under way, and the band of its new rows in the colour of the lane. */
export interface DrawnRows {
  readonly map: MapView;
  readonly rows: LaneRows;
  readonly band: RowBand | null;
}

/** What a lane holds drawn lower by new rows at its top: its steps and the flows pinned to its rows. */
function drawnLower(map: MapView, laneId: string, by: number): MapView {
  return {
    ...map,
    steps: map.steps.map((step) => (step.lane_id === laneId ? { ...step, row: step.row + by } : step)),
    flows: map.flows.map((flow) => {
      const via = flow.route?.via;
      return flow.route != null && via != null && via.lane_id === laneId && via.row != null ? { ...flow, route: { ...flow.route, via: { ...via, row: via.row + by } } } : flow;
    }),
  };
}

/**
 * The map as a pull under way would leave it, so that what is under the border goes down with the hand: the lane shows
 * its new rows, at its bottom or at its top, what it holds drawn lower then. Nothing changes without a pull or a row, nor
 * on another map than the one the pull started on (the one its write gave back is drawn as it is).
 */
export function withRowsDraft(map: MapView, rows: LaneRows, draft: RowsDraft | null): DrawnRows {
  const change = draft === null || draft.version !== map.structure_version ? null : growth(map, rows, draft.laneId, draft.end, draft.count);
  if (draft === null || change === null) return { map, rows, band: null };
  const shown = rowsShown(map, draft.laneId, rows);
  const added = Math.min(draft.count, LAST_ROW + 1 - shown);
  return {
    map: change.inserted > 0 ? drawnLower(map, draft.laneId, change.inserted) : map,
    rows: { ...rows, [draft.laneId]: shown + added },
    band: { laneId: draft.laneId, from: draft.end === 'top' ? 0 : shown, count: added },
  };
}
