// moves.test.ts
// The moves of steps: by the arrows of the keyboard, at the drop of a drag, the useful ones, a block on free cells (annex B 2.3, 2.5).

import { laneOf, mapOf, stepOf } from '../../../test/maps.ts';
import { COLUMN_WIDTH, FREE_BAND, LEFT_MARGIN, ROW_HEIGHT } from './geometry/constants.ts';
import { stepBox } from './geometry/grid.ts';
import { cellAtPoint, cellBody, dropCells, gridOfMap, keyMoves, landsOnFreeCells, usefulMoves } from './moves.ts';

const map = mapOf({
  lanes: [laneOf('A', 0), laneOf('B', 1)],
  steps: [stepOf('1', 'A', 0), stepOf('2', 'A', 1, { row: 1 }), stepOf('3', 'B', 2)],
});

describe('the arrows of the keyboard (B-2.5-18 to B-2.5-20)', () => {
  it('left and right move the selection by a column, in its row', () => {
    expect(keyMoves(map, ['2', '3'], 1, 0)).toEqual([{ id: '2', laneId: 'A', column: 2, row: 1 }, { id: '3', laneId: 'B', column: 3, row: 0 }]);
    expect(keyMoves(map, ['2'], -1, 0)).toEqual([{ id: '2', laneId: 'A', column: 0, row: 1 }]);
  });

  it('up and down take the selection to the next lane, in its row 0 (B-2.5-19)', () => {
    expect(keyMoves(map, ['2'], 0, 1)).toEqual([{ id: '2', laneId: 'B', column: 1, row: 0 }]);
    expect(keyMoves(map, ['3'], 0, -1)).toEqual([{ id: '3', laneId: 'A', column: 2, row: 0 }]);
  });

  it('are refused when a column would go under 0 or when there is no lane to go to (B-2.5-20)', () => {
    expect(keyMoves(map, ['1', '2'], -1, 0)).toBeNull();
    expect(keyMoves(map, ['1'], 0, -1)).toBeNull();
    expect(keyMoves(map, ['3'], 0, 1)).toBeNull();
  });
});

describe('the drop of a drag (B-2.3-22 to B-2.3-25)', () => {
  const corner = (column: number, rowTop: number) => ({ x: LEFT_MARGIN + column * COLUMN_WIDTH, y: rowTop + FREE_BAND });

  it('lands in the cell of the centre of the step: corner plus half its size (B-2.3-22)', () => {
    expect(dropCells(map, [{ id: '1', position: { x: corner(3, 0).x + 100, y: corner(3, 0).y } }])).toEqual([{ id: '1', laneId: 'A', column: 3, row: 0 }]);
    expect(dropCells(map, [{ id: '1', position: { x: corner(3, 0).x + 120, y: corner(3, 0).y } }])).toEqual([{ id: '1', laneId: 'A', column: 4, row: 0 }]);
  });

  it('low in the last row of a lane, a drop makes a new row (B-2.3-23)', () => {
    const lastRowOfA = ROW_HEIGHT;
    expect(dropCells(map, [{ id: '3', position: { x: corner(5, 0).x, y: lastRowOfA + ROW_HEIGHT * 0.85 - 32 } }])).toEqual([{ id: '3', laneId: 'A', column: 5, row: 2 }]);
    expect(dropCells(map, [{ id: '3', position: { x: corner(5, 0).x, y: lastRowOfA + ROW_HEIGHT * 0.8 - 32 } }])).toEqual([{ id: '3', laneId: 'A', column: 5, row: 1 }]);
  });

  it('the box of a cell, dropped as it is, gives that cell back: lanes of rank 0 and 1, columns 0, 1 and 5 (B-2.3-22)', () => {
    const two = mapOf({ lanes: [laneOf('B', 1), laneOf('A', 0)], steps: [stepOf('s', 'A', 0)] });
    for (const laneId of ['A', 'B'])
      for (const column of [0, 1, 5]) {
        const box = stepBox(gridOfMap(two), { laneId, column, row: 0 });
        expect(dropCells(two, [{ id: 's', position: { x: box.x, y: box.y } }])).toEqual([{ id: 's', laneId, column, row: 0 }]);
      }
  });

  it('a node that is not a step of the map is left out', () => {
    expect(dropCells(map, [{ id: 'lane:A', position: { x: 0, y: 0 } }])).toEqual([]);
  });

  it('only the useful moves are kept, sorted by column (B-2.3-24)', () => {
    const moves = [{ id: '3', laneId: 'B', column: 5, row: 0 }, { id: '1', laneId: 'A', column: 0, row: 0 }, { id: '2', laneId: 'B', column: 4, row: 0 }];
    expect(usefulMoves(map, moves).map((move) => move.id)).toEqual(['2', '3']);
  });

  it('a block of several steps on cells free and distinct is laid out as it is (B-2.3-25), else moved by the host (B-2.3-26)', () => {
    expect(landsOnFreeCells(map, [{ id: '1', laneId: 'A', column: 5, row: 0 }, { id: '2', laneId: 'A', column: 6, row: 0 }])).toBe(true);
    expect(landsOnFreeCells(map, [{ id: '1', laneId: 'A', column: 1, row: 0 }, { id: '2', laneId: 'A', column: 0, row: 0 }])).toBe(true);
    expect(landsOnFreeCells(map, [{ id: '1', laneId: 'B', column: 2, row: 0 }, { id: '2', laneId: 'A', column: 6, row: 0 }])).toBe(false);
    expect(landsOnFreeCells(map, [{ id: '1', laneId: 'A', column: 6, row: 0 }, { id: '2', laneId: 'A', column: 6, row: 0 }])).toBe(false);
    expect(landsOnFreeCells(map, [{ id: '1', laneId: 'A', column: 6, row: 0 }])).toBe(false);
  });
});

describe('the cells for the host', () => {
  it('the cell aimed by a point; none without a lane', () => {
    expect(cellAtPoint(map, { x: LEFT_MARGIN + 2 * COLUMN_WIDTH + 85, y: ROW_HEIGHT * 2 + 50 })).toEqual({ laneId: 'B', column: 2, row: 0 });
    expect(cellAtPoint(mapOf(), { x: 0, y: 0 })).toBeNull();
  });

  it('the body of a cell is written as the host reads it', () => {
    expect(cellBody({ id: 's', laneId: 'l', column: 2, row: 1 })).toEqual({ id: 's', lane_id: 'l', column: 2, row: 1 });
  });
});
