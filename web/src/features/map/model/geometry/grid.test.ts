// grid.test.ts
// The constants of the geometry, written once, and the grid: rows of a lane, positions of a step, the cell under a point, the width (annex B 3.1, AR-19).

import {
  AISLE, BOTTOM_MARGIN, COLUMN_WIDTH, FRAME_MARGIN, FRAME_MARGIN_TOP, FREE_BAND, FREE_COLUMNS, INITIAL_VIEW, LANE_HEADER_WIDTH, LEFT_MARGIN,
  NEW_ROW_THRESHOLD, RIGHT_MARGIN, ROW_HEIGHT, STEP_HEIGHT, STEP_WIDTH, TOP_MARGIN, geometryCssVariables,
} from './constants.ts';
import { cellAt, gridOf, mapWidth, orderedLanes, rowTop, stepBox } from './grid.ts';

const lanes = [{ id: 'b', order: 2 }, { id: 'a', order: 1 }, { id: 'c', order: 3 }];

describe('the constants of the grid (B-3.1-01 to B-3.1-12)', () => {
  it('keep the values of the 1.x', () => {
    expect([COLUMN_WIDTH, ROW_HEIGHT, LEFT_MARGIN, STEP_WIDTH, STEP_HEIGHT, FREE_COLUMNS, NEW_ROW_THRESHOLD]).toEqual([220, 120, 30, 170, 64, 3, 0.82]);
    expect([TOP_MARGIN, BOTTOM_MARGIN, RIGHT_MARGIN, FRAME_MARGIN, FRAME_MARGIN_TOP]).toEqual([16, 24, 40, 14, 26]);
  });

  it('derive the aisle (25) and the free band (28) from the column, the row and the step', () => {
    expect([AISLE, FREE_BAND]).toEqual([25, 28]);
  });

  it('take the margin of the lane headers of the charter, 184, and the view at x 192 (AR-19)', () => {
    expect(LANE_HEADER_WIDTH).toBe(184);
    expect(INITIAL_VIEW).toEqual({ x: 192, y: 16, zoom: 1 });
  });

  it('give the style sheets the sizes they need, from the same constants', () => {
    expect(geometryCssVariables()).toMatchObject({ '--step-width': '170px', '--step-height': '64px', '--lane-header-width': '184px', '--column-width': '220px', '--row-height': '120px' });
  });
});

describe('the grid (B-3.1-13 to B-3.1-19)', () => {
  it('orders the lanes by their place, then by identifier', () => {
    expect(orderedLanes([{ id: 'a2', order: 1 }, ...lanes])).toEqual(['a', 'a2', 'b', 'c']);
  });

  it('gives a lane as many rows as its lowest row + 1, at least one; the next lanes go down as much', () => {
    const grid = gridOf(lanes, [{ laneId: 'b', column: 0, row: 2 }, { laneId: 'b', column: 1, row: 0 }, { laneId: 'x', column: 0, row: 5 }]);
    expect([...grid.rows]).toEqual([['a', 1], ['b', 3], ['c', 1]]);
    expect([...grid.top]).toEqual([['a', 0], ['b', 120], ['c', 480]]);
    expect(grid.height).toBe(600);
    expect(rowTop(grid, 'b', 2)).toBe(360);
  });

  it('places a step at x = 30 + column × 220, y = top of its row + 28', () => {
    const grid = gridOf(lanes, [{ laneId: 'b', column: 0, row: 1 }]);
    expect(stepBox(grid, { laneId: 'b', column: 3, row: 1 })).toEqual({ x: 690, y: 268, width: 170, height: 64 });
  });

  it('finds the column under a point: round((x − 30 − 85) / 220), at least 0', () => {
    const grid = gridOf(lanes, []);
    expect([0, 224, 225, 444, 1000].map((x) => cellAt(grid, { x, y: 10 }, false)?.column)).toEqual([0, 0, 1, 1, 4]);
  });

  it('brings a point above or below the lanes back to the first or the last lane', () => {
    const grid = gridOf(lanes, []);
    expect([cellAt(grid, { x: 100, y: -50 }, false), cellAt(grid, { x: 100, y: 999 }, false)]).toEqual([
      { laneId: 'a', column: 0, row: 0 }, { laneId: 'c', column: 0, row: 0 }]);
    expect(cellAt(gridOf([], []), { x: 0, y: 0 }, false)).toBeNull();
  });

  it('makes a new row under the last one for a point in its lowest part, when the map has steps (B-3.1-07)', () => {
    const grid = gridOf(lanes, [{ laneId: 'a', column: 0, row: 0 }]);
    expect(cellAt(grid, { x: 100, y: 98 }, true)?.row).toBe(0);
    expect(cellAt(grid, { x: 100, y: 99 }, true)?.row).toBe(1);
    expect(cellAt(grid, { x: 100, y: 99 }, false)?.row).toBe(0);
  });

  it('gives the width of the map: 30 + (last column + 1 + 3) × 220', () => {
    expect(mapWidth([{ column: 2 }, { column: 5 }])).toBe(30 + 9 * 220);
    expect(mapWidth([])).toBe(30 + 3 * 220);
  });
});
