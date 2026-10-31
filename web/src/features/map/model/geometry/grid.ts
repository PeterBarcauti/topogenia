// grid.ts
// The grid of a map: a lane is a horizontal band of rows, a column a position of reading; cells to coordinates and back (annex B 3.1).

import { COLUMN_WIDTH, FREE_BAND, FREE_COLUMNS, LEFT_MARGIN, NEW_ROW_THRESHOLD, ROW_HEIGHT, STEP_HEIGHT, STEP_WIDTH } from './constants.ts';

export interface Point {
  readonly x: number;
  readonly y: number;
}

export interface Rect {
  readonly x: number;
  readonly y: number;
  readonly width: number;
  readonly height: number;
}

/** A lane as the grid needs it: its identifier and its place (`sort_order`). */
export interface GridLane {
  readonly id: string;
  readonly order: number;
}

/** A step as the grid needs it: its lane, column and row. */
export interface GridCell {
  readonly laneId: string;
  readonly column: number;
  readonly row: number;
}

/** The vertical layout: the lanes in their order, the rows and the top of each. */
export interface Grid {
  readonly lanes: readonly string[];
  readonly rank: ReadonlyMap<string, number>;
  readonly rows: ReadonlyMap<string, number>;
  readonly top: ReadonlyMap<string, number>;
  readonly height: number;
  /** The band under the steps of each row: 0, or the one of the value stream layer (spec 14.5). */
  readonly below: number;
  /** The height of a row: 120 px and the band under its steps. */
  readonly rowHeight: number;
}

/** The lanes by their order, then by identifier, so that two lanes of the same order keep one place. */
export function orderedLanes(lanes: readonly GridLane[]): string[] {
  return [...lanes].sort((a, b) => a.order - b.order || (a.id < b.id ? -1 : a.id > b.id ? 1 : 0)).map((lane) => lane.id);
}

/**
 * A lane has as many rows as its lowest row + 1, at least one; the next lanes go down as much (B-3.1-13, B-3.1-14). The
 * band given goes under the steps of each row (the value stream layer, spec 14.5), their top band unchanged.
 */
export function gridOf(lanes: readonly GridLane[], steps: readonly GridCell[], below = 0): Grid {
  const rowHeight = ROW_HEIGHT + below;
  const order = orderedLanes(lanes);
  const rows = new Map(order.map((id) => [id, 1]));
  for (const step of steps) if (rows.has(step.laneId)) rows.set(step.laneId, Math.max(rows.get(step.laneId) ?? 1, step.row + 1));
  const top = new Map<string, number>();
  let y = 0;
  for (const id of order) {
    top.set(id, y);
    y += (rows.get(id) ?? 1) * rowHeight;
  }
  return { lanes: order, rank: new Map(order.map((id, index) => [id, index])), rows, top, height: y, below, rowHeight };
}

/** The top of a row of a lane. */
export function rowTop(grid: Grid, laneId: string, row: number): number {
  return (grid.top.get(laneId) ?? 0) + row * grid.rowHeight;
}

/** The box of the step of a cell: x = 30 + column × 220, y = top of its row + 28 (B-3.1-15, B-3.1-16). */
export function stepBox(grid: Grid, cell: GridCell): Rect {
  return { x: LEFT_MARGIN + cell.column * COLUMN_WIDTH, y: rowTop(grid, cell.laneId, cell.row) + FREE_BAND, width: STEP_WIDTH, height: STEP_HEIGHT };
}

/**
 * The cell nearest to a point of the canvas (the centre of a dropped step, a double click): column = round((x − 30 − 85)
 * / 220), at least 0; a point above or below the lanes is brought back to the first or last lane (B-3.1-17, B-3.1-18). With
 * steps on the map, a point in the lowest part of the last row of a lane makes a new row under it (B-3.1-07).
 */
export function cellAt(grid: Grid, point: Point, hasSteps: boolean): GridCell | null {
  const last = grid.lanes.at(-1);
  if (last === undefined) return null;
  const column = Math.max(0, Math.round((point.x - LEFT_MARGIN - STEP_WIDTH / 2) / COLUMN_WIDTH));
  const y = Math.min(grid.height - 1, Math.max(0, point.y));
  const laneId = grid.lanes.find((id) => y < (grid.top.get(id) ?? 0) + (grid.rows.get(id) ?? 1) * grid.rowHeight) ?? last;
  const within = y - (grid.top.get(laneId) ?? 0);
  const lastRow = (grid.rows.get(laneId) ?? 1) - 1;
  let row = Math.min(lastRow, Math.floor(within / grid.rowHeight));
  if (hasSteps && row === lastRow && within - lastRow * grid.rowHeight > grid.rowHeight * NEW_ROW_THRESHOLD) row = lastRow + 1;
  return { laneId, column, row };
}

/** The width of the map: 30 + (last column + 1 + 3) × 220 (B-3.1-19). */
export function mapWidth(steps: readonly Pick<GridCell, 'column'>[]): number {
  const lastColumn = steps.reduce((max, step) => Math.max(max, step.column), -1);
  return LEFT_MARGIN + (lastColumn + 1 + FREE_COLUMNS) * COLUMN_WIDTH;
}
