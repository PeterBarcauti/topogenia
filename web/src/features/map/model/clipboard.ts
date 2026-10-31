// clipboard.ts
// A part of a map copied or cut, with its shape and its inner flows, and where to put it down (annex B 2.7).

import type { Schema } from '../../../api/client.ts';
import type { GridCell } from './geometry/grid.ts';
import { laneRanks, lanesInOrder, readingOrder } from './readingOrder.ts';

type MapView = Schema<'MapView'>;
type StepView = Schema<'StepView'>;

/** A copy makes new steps when pasted; a cut moves the steps themselves (B-2.7-07, B-2.7-10). */
export type ClipboardMode = 'copy' | 'cut';

/** A step of the block: its lane, column and row relative to the first step of the block (B-2.7-05). */
export interface ClipStep {
  /** A key of the paste for a copy (`c0`, `c1`...), the identifier of the step for a cut (B-2.7-07, B-2.7-08). */
  readonly key: string;
  readonly label: string;
  readonly type: Schema<'StepType'>;
  readonly lane: number;
  readonly column: number;
  readonly row: number;
}

/** A flow inside the block, between two keys (B-2.7-06). */
export interface ClipFlow {
  readonly source: string;
  readonly target: string;
  readonly label: string | null;
}

export interface ClipboardContent {
  readonly mode: ClipboardMode;
  readonly projectId: string;
  readonly mapId: string;
  readonly steps: readonly ClipStep[];
  readonly flows: readonly ClipFlow[];
}

/** Where a step of the block lands: the cell of its key. */
export interface Placement extends GridCell {
  readonly key: string;
}

/** Before or after a step, in the flow (B-2.7-22). */
export type FlowPosition = 'before' | 'after';

/**
 * The block of the steps chosen, relative to its first step in the reading order (B-2.7-04), with the flows whose both
 * ends are in it (B-2.7-06); null when none of the steps is on the map.
 */
export function extractBlock(map: MapView, ids: readonly string[], mode: ClipboardMode): ClipboardContent | null {
  const ranks = laneRanks(map.lanes);
  const chosen = new Set(ids);
  const steps = map.steps.filter((step) => chosen.has(step.id) && ranks.has(step.lane_id)).sort(readingOrder(ranks));
  const first = steps[0];
  if (first === undefined) return null;
  const keys = new Map(steps.map((step, index) => [step.id, mode === 'cut' ? step.id : `c${String(index)}`]));
  const keyOf = (id: string): string => keys.get(id) ?? id;
  const firstLane = ranks.get(first.lane_id) ?? 0;
  return {
    mode,
    projectId: map.project_id,
    mapId: map.id,
    steps: steps.map((step) => ({
      key: keyOf(step.id), label: step.label, type: step.type,
      lane: (ranks.get(step.lane_id) ?? 0) - firstLane, column: step.column - first.column, row: step.row - first.row,
    })),
    flows: map.flows
      .filter((flow) => keys.has(flow.source_step_id) && keys.has(flow.target_step_id))
      .map((flow) => ({ source: keyOf(flow.source_step_id), target: keyOf(flow.target_step_id), label: flow.label })),
  };
}

/**
 * The cells of the block when its first step lands in the cell given, the rest keeping its shape (B-2.7-15): a block that
 * would rise above the first lane goes down as much (B-2.7-16), the rows go up to stay positive (B-2.7-17). Null when lanes
 * are missing under the cell (B-2.7-18).
 */
export function placeBlock(steps: readonly ClipStep[], lanes: MapView['lanes'], anchor: GridCell): Placement[] | null {
  const ordered = lanesInOrder(lanes);
  const start = ordered.indexOf(anchor.laneId);
  if (start < 0 || steps.length === 0) return null;
  const highest = Math.min(...steps.map((step) => start + step.lane));
  const first = start + Math.max(0, -highest);
  const lowestRow = Math.min(...steps.map((step) => anchor.row + step.row));
  const lift = Math.max(0, -lowestRow);
  const placements: Placement[] = [];
  for (const step of steps) {
    const laneId = ordered[first + step.lane];
    if (laneId === undefined) return null;
    placements.push({ key: step.key, laneId, column: anchor.column + step.column, row: anchor.row + step.row + lift });
  }
  return placements;
}

/** The number of lanes a block spans, for the message of the lanes missing (B-2.7-18). */
export const laneSpan = (steps: readonly ClipStep[]): number => new Set(steps.map((step) => step.lane)).size;

/** The cell of a block inserted before or after a step, in its row (1.x `caseDInsertion`). */
export const insertionCell = (step: Pick<StepView, 'lane_id' | 'column' | 'row'>, position: FlowPosition): GridCell => ({
  laneId: step.lane_id, column: step.column + (position === 'after' ? 1 : 0), row: step.row,
});

/** The cell just right of a block (Duplicate, B-2.7-23): lane and row of its first step, the last column + 1. */
export function cellRightOf(map: MapView, ids: readonly string[]): GridCell | null {
  const ranks = laneRanks(map.lanes);
  const steps = map.steps.filter((step) => ids.includes(step.id) && ranks.has(step.lane_id)).sort(readingOrder(ranks));
  const first = steps[0];
  if (first === undefined) return null;
  return { laneId: first.lane_id, column: Math.max(...steps.map((step) => step.column)) + 1, row: first.row };
}

/** The end of the map (Ctrl+V with nothing aimed, B-2.7-21): first lane, the column after the last, row 0; null without a lane. */
export function endOfMap(map: MapView): GridCell | null {
  const first = lanesInOrder(map.lanes)[0];
  if (first === undefined) return null;
  return { laneId: first, column: map.steps.length === 0 ? 0 : Math.max(...map.steps.map((step) => step.column)) + 1, row: 0 };
}
