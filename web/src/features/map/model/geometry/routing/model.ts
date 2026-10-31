// model.ts
// What the routing of the flows reads and gives, and the lines of the grid it reasons on (annex B 3.4, spec 9.7), without React nor DOM.

import { COLUMN_WIDTH, FREE_BAND, LEFT_MARGIN, STEP_HEIGHT, STEP_WIDTH, AISLE } from '../constants.ts';
import { gridOf, rowTop, stepBox, type Grid, type GridCell, type GridLane, type Point, type Rect } from '../grid.ts';

export type StepKind = 'task' | 'wait' | 'start' | 'end' | 'decision' | 'parallel' | 'inclusive';

export interface Size {
  readonly width: number;
  readonly height: number;
}

/** A step: its cell, its type and, for a gateway, the size of its label written under the shape (AR-57), which no label covers. */
export interface RoutingStep extends GridCell {
  readonly id: string;
  readonly type: StepKind;
  readonly below?: Size;
}

/** A flow and the size of its label box, measured by the caller (null without a label nor documents). */
export interface RoutingFlow {
  readonly id: string;
  readonly sourceId: string;
  readonly targetId: string;
  readonly label: Size | null;
}

export interface RoutingMap {
  readonly lanes: readonly GridLane[];
  readonly steps: readonly RoutingStep[];
  readonly flows: readonly RoutingFlow[];
  /** The band under the steps of each row, none without the value stream layer (spec 14.5). */
  readonly below?: number;
}

/** The four sides of a step (1.x handles `g`, `d`, `h`, `b`). */
export type Side = 'left' | 'right' | 'top' | 'bottom';

/** A flow, a loop back to the left, a detour over the steps in the way (1.x `fleche`, `boucle`, `contour`, B-3.4-33). */
export type FlowKind = 'flow' | 'loop' | 'detour';

/** A loop drawn as a curve under the steps of one line: `M start C (start.x, low) (end.x, low) end` (B-3.4-25). */
export interface Curve {
  readonly start: Point;
  readonly end: Point;
  readonly low: number;
}

/** A flow routed: its sides, its points (an orthogonal line, or the two ends of a curve), its SVG path, its label box. */
export interface RoutedFlow {
  readonly id: string;
  readonly sourceId: string;
  readonly targetId: string;
  readonly kind: FlowKind;
  readonly sourceSide: Side;
  readonly targetSide: Side;
  readonly points: readonly Point[];
  readonly curve: Curve | null;
  readonly path: string;
  readonly label: Rect | null;
}

/** What the routing knows of a map: its grid, its steps, its lines (a row of a lane) in their vertical order. */
export interface Context {
  readonly grid: Grid;
  readonly steps: ReadonlyMap<string, RoutingStep>;
  /** Index of each line from the top: `laneId:row`. */
  readonly lineIndex: ReadonlyMap<string, number>;
  /** The top of each line, by index, and the bottom of the last one at the end. */
  readonly boundaries: readonly number[];
  /** The columns taken on each line. */
  readonly taken: ReadonlyMap<string, ReadonlySet<number>>;
}

export const lineOf = (cell: GridCell): string => `${cell.laneId}:${cell.row}`;

export function contextOf(map: RoutingMap): Context {
  const grid = gridOf(map.lanes, map.steps, map.below ?? 0);
  const lineIndex = new Map<string, number>();
  const boundaries: number[] = [];
  for (const laneId of grid.lanes)
    for (let row = 0; row < (grid.rows.get(laneId) ?? 1); row++) {
      lineIndex.set(`${laneId}:${row}`, boundaries.length);
      boundaries.push(rowTop(grid, laneId, row));
    }
  boundaries.push(grid.height);
  const taken = new Map<string, Set<number>>();
  const steps = new Map<string, RoutingStep>();
  for (const step of map.steps) {
    if (!grid.rank.has(step.laneId)) continue;
    steps.set(step.id, step);
    const line = lineOf(step);
    taken.set(line, (taken.get(line) ?? new Set<number>()).add(step.column));
  }
  return { grid, steps, lineIndex, boundaries, taken };
}

export const indexOf = (context: Context, cell: GridCell): number => context.lineIndex.get(lineOf(cell)) ?? 0;

export const boxOf = (context: Context, step: RoutingStep): Rect => stepBox(context.grid, step);

/** The centre of a side of a step: every type, the gateways included, has its points at the middle of its sides (mockup). */
export function portOf(context: Context, step: RoutingStep, side: Side): Point {
  const box = boxOf(context, step);
  if (side === 'left') return { x: box.x, y: box.y + STEP_HEIGHT / 2 };
  if (side === 'right') return { x: box.x + STEP_WIDTH, y: box.y + STEP_HEIGHT / 2 };
  if (side === 'top') return { x: box.x + STEP_WIDTH / 2, y: box.y };
  return { x: box.x + STEP_WIDTH / 2, y: box.y + STEP_HEIGHT };
}

/** The middle of the aisle right of a column (column −1: before the column 0). */
export const aisleX = (column: number): number => LEFT_MARGIN + column * COLUMN_WIDTH + STEP_WIDTH + AISLE;

/** The vertical axis of a column, through the middle of its steps. */
export const axisX = (column: number): number => LEFT_MARGIN + column * COLUMN_WIDTH + STEP_WIDTH / 2;

/** The horizontal axis of a line, through the middle of its steps. */
export const centreY = (context: Context, cell: GridCell): number => (context.boundaries[indexOf(context, cell)] ?? 0) + FREE_BAND + STEP_HEIGHT / 2;

export const isGateway = (step: Pick<RoutingStep, 'type'>): boolean => step.type === 'decision' || step.type === 'parallel' || step.type === 'inclusive';

/** Whether a step stands on the line of a cell strictly between two columns (the line is not free between them). */
export function lineTaken(context: Context, cell: GridCell, from: number, to: number): boolean {
  const columns = context.taken.get(lineOf(cell));
  if (columns === undefined) return false;
  for (const column of columns) if (column > Math.min(from, to) && column < Math.max(from, to)) return true;
  return false;
}

/**
 * Whether a column is free between two lines: no step in it on the lines strictly between, and, when asked, none on the
 * line of the second but the second itself (annex B 3.4 rule 2, « voie libre »).
 */
export function columnFree(context: Context, column: number, from: GridCell, to: GridCell, toIncluded = false): boolean {
  const [a, b] = [indexOf(context, from), indexOf(context, to)];
  for (const [line, columns] of context.taken) {
    if (!columns.has(column)) continue;
    const index = context.lineIndex.get(line) ?? -1;
    if (index > Math.min(a, b) && index < Math.max(a, b)) return false;
    if (toIncluded && index === b && column !== to.column) return false;
  }
  return true;
}
