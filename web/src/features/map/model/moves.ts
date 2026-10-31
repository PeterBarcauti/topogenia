// moves.ts
// Where steps go: by the arrows of the keyboard, dropped after a drag; the moves that change something, and a block put down on free cells (annex B 2.3, 2.5).

import type { Schema } from '../../../api/client.ts';
import { STEP_HEIGHT, STEP_WIDTH } from './geometry/constants.ts';
import { cellAt, gridOf, type GridCell, type Point } from './geometry/grid.ts';
import { lanesInOrder } from './readingOrder.ts';

type MapView = Schema<'MapView'>;

/** A step and the cell it goes to. */
export interface StepCell extends GridCell {
  readonly id: string;
}

/** The body of the cell of a step for the host (`StepCellBody`). */
export const cellBody = (cell: StepCell): { id: string; lane_id: string; column: number; row: number } => ({
  id: cell.id, lane_id: cell.laneId, column: cell.column, row: cell.row,
});

const cellKey = (cell: GridCell): string => `${cell.laneId}:${String(cell.column)}:${String(cell.row)}`;

/** The grid of the map as it is shown, with the band under the steps of the value stream layer when it is on (spec 14.5). */
export const gridOfMap = (map: Pick<MapView, 'lanes' | 'steps'>, below = 0) =>
  gridOf(map.lanes.map((lane) => ({ id: lane.id, order: lane.sort_order })), map.steps.map((step) => ({ laneId: step.lane_id, column: step.column, row: step.row })), below);

/** The cell aimed at a point of the canvas: a double click on the background, the pointer before a paste (B-2.3-09, B-2.7-20). */
export const cellAtPoint = (map: Pick<MapView, 'lanes' | 'steps'>, point: Point, below = 0): GridCell | null =>
  cellAt(gridOfMap(map, below), point, map.steps.length > 0);

/**
 * The moves of the chosen steps by the arrows of the keyboard (B-2.5-18 to B-2.5-20): a column left or right in the same
 * row, or the lane above or below, in its row 0 (1.x `deplacerParPas`). Null, nothing moved, when a column would go
 * under 0 or when there is no lane to go to.
 */
export function keyMoves(map: Pick<MapView, 'lanes' | 'steps'>, ids: readonly string[], columns: number, lanes: number): StepCell[] | null {
  const ordered = lanesInOrder(map.lanes);
  const moves: StepCell[] = [];
  for (const step of map.steps.filter((candidate) => ids.includes(candidate.id))) {
    const rank = ordered.indexOf(step.lane_id);
    const laneId = rank < 0 ? undefined : ordered[rank + lanes];
    if (laneId === undefined || step.column + columns < 0) return null;
    moves.push({ id: step.id, laneId, column: step.column + columns, row: lanes === 0 ? step.row : 0 });
  }
  return moves;
}

/** A step as React Flow gives it at the end of a drag: its identifier and the top left corner of its box. */
export interface DroppedStep {
  readonly id: string;
  readonly position: Point;
}

/**
 * The cells of the steps dropped: the cell of the centre of each box, its corner plus half its size, on the grid as it is
 * shown (B-2.3-22), a drop low in the last row of a lane making a new row (B-2.3-23).
 */
export function dropCells(map: Pick<MapView, 'lanes' | 'steps'>, dropped: readonly DroppedStep[], below = 0): StepCell[] {
  const grid = gridOfMap(map, below);
  const known = new Set(map.steps.map((step) => step.id));
  return dropped.flatMap((step) => {
    if (!known.has(step.id)) return [];
    const cell = cellAt(grid, { x: step.position.x + STEP_WIDTH / 2, y: step.position.y + STEP_HEIGHT / 2 }, map.steps.length > 0);
    return cell === null ? [] : [{ id: step.id, ...cell }];
  });
}

/** The moves that change the cell of their step, sorted by column (B-2.3-24). */
export function usefulMoves(map: Pick<MapView, 'steps'>, moves: readonly StepCell[]): StepCell[] {
  const steps = new Map(map.steps.map((step) => [step.id, step]));
  return moves
    .filter((move) => {
      const step = steps.get(move.id);
      return step !== undefined && (step.lane_id !== move.laneId || step.column !== move.column || step.row !== move.row);
    })
    .sort((a, b) => a.column - b.column);
}

/**
 * Whether a block of several steps lands on cells all distinct and free of the steps that stay: it is then laid out as it
 * is, in one layout (B-2.3-25); otherwise the host moves it and shifts what is in the way (AR-44, B-2.3-26).
 */
export function landsOnFreeCells(map: Pick<MapView, 'steps'>, moves: readonly StepCell[]): boolean {
  const moving = new Set(moves.map((move) => move.id));
  const taken = new Set(map.steps.filter((step) => !moving.has(step.id)).map((step) => cellKey({ laneId: step.lane_id, column: step.column, row: step.row })));
  const targets = moves.map(cellKey);
  return moves.length > 1 && new Set(targets).size === targets.length && targets.every((target) => !taken.has(target));
}
