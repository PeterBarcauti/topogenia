// gridShape.ts
// The grid of a map as the canvas shows it, written once: what holds a row (its steps, the flows pinned to it), the band of the value stream layer under each row, the rows a lane shows at least while its map is open (annex B 3.1, spec 14.5, ADR 096).

import type { Schema } from '../../../api/client.ts';
import { gridOf, type GridCell } from './geometry/grid.ts';

type MapView = Schema<'MapView'>;

/**
 * The rows each lane shows at least while its map is open, by identifier of the lane: the empty rows a pull of its
 * border or its menu gave it under what it holds; never kept, never sent (a row exists by what holds it).
 */
export type LaneRows = Readonly<Record<string, number>>;

/** What the grid shown takes besides the map: the band under the steps of each row, the rows the lanes show at least. */
export interface GridShape {
  /** The band of the value stream layer when it is on, else none (spec 14.5). */
  readonly below: number;
  readonly rows: LaneRows;
}

/** The grid of the map alone: no band, no empty row. */
export const PLAIN_SHAPE: GridShape = { below: 0, rows: {} };

/**
 * What holds the rows of the lanes: the cell of each step, and the row each flow pinned to the middle or a band of a row
 * passes along (ADR 091). A row exists by them, as the host counts them before taking one out (ADR 096).
 */
export function holdingCells(map: Pick<MapView, 'steps' | 'flows'>): GridCell[] {
  const cells: GridCell[] = map.steps.map((step) => ({ laneId: step.lane_id, column: step.column, row: step.row }));
  for (const flow of map.flows) {
    const via = flow.route?.via;
    if (via?.lane_id != null && via.row != null) cells.push({ laneId: via.lane_id, column: 0, row: via.row });
  }
  return cells;
}

/** The grid of the map as it is shown: its lanes in their order, each with the rows it holds or shows at least. */
export const gridOfMap = (map: Pick<MapView, 'lanes' | 'steps' | 'flows'>, shape: GridShape = PLAIN_SHAPE) =>
  gridOf(map.lanes.map((lane) => ({ id: lane.id, order: lane.sort_order, rows: shape.rows[lane.id] ?? 1 })), holdingCells(map), shape.below);
