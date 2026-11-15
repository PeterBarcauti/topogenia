// readingOrder.ts
// The one reading order of the steps of a map: column, then lane in the order of the lanes, then row (spec 13, B-2.7-04, B-2.8-03).

import type { Schema } from '../../../api/client.ts';
import { orderedLanes } from './geometry/grid.ts';

/** A step as the reading order needs it. */
export type ReadableStep = Pick<Schema<'StepView'>, 'id' | 'lane_id' | 'column' | 'row'>;

/** The rank of each lane, from 0 at the top, in the order of the lanes (B-3.1-12). */
export function laneRanks(lanes: readonly Pick<Schema<'LaneView'>, 'id' | 'sort_order'>[]): Map<string, number> {
  return new Map(orderedLanes(lanes.map((lane) => ({ id: lane.id, order: lane.sort_order }))).map((id, rank) => [id, rank]));
}

/** The identifiers of the lanes in their order, from the top. */
export const lanesInOrder = (lanes: readonly Pick<Schema<'LaneView'>, 'id' | 'sort_order'>[]): string[] =>
  orderedLanes(lanes.map((lane) => ({ id: lane.id, order: lane.sort_order })));

/**
 * Compares two steps in the reading order: column, then rank of the lane, then row, then identifier so that two steps of
 * the same cell keep one order.
 */
export function readingOrder(ranks: ReadonlyMap<string, number>): (a: ReadableStep, b: ReadableStep) => number {
  return (a, b) =>
    a.column - b.column || (ranks.get(a.lane_id) ?? 0) - (ranks.get(b.lane_id) ?? 0) || a.row - b.row || (a.id < b.id ? -1 : a.id > b.id ? 1 : 0);
}
