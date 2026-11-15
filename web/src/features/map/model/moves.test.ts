// moves.test.ts
// The moves of steps: by the arrows of the keyboard, at the drop of a drag and while dragged, the useful ones, a block on free cells (annex B 2.3, 2.5, lot G of the plan 3.1).

import { laneOf, mapOf, stepOf } from '../../../test/maps.ts';
import { COLUMN_WIDTH, FREE_BAND, LEFT_MARGIN, ROW_HEIGHT } from './geometry/constants.ts';
import { stepBox } from './geometry/grid.ts';
import { gridOfMap } from './gridShape.ts';
import { cellAtPoint, cellBody, dropCells, dropPreviewOf, keyMoves, landsOnFreeCells, usefulMoves } from './moves.ts';

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

  it('in the empty rows a lane shows, as the canvas draws them: lands in them, the lanes under them lower (lot G of the plan 3.1)', () => {
    const shape = { below: 0, rows: { A: 4 } };
    expect(dropCells(map, [{ id: '3', position: { x: corner(1, 0).x, y: corner(0, 3 * ROW_HEIGHT).y } }], shape)).toEqual([{ id: '3', laneId: 'A', column: 1, row: 3 }]);
    expect(dropCells(map, [{ id: '1', position: { x: corner(2, 0).x, y: corner(0, 4 * ROW_HEIGHT).y } }], shape)).toEqual([{ id: '1', laneId: 'B', column: 2, row: 0 }]);
    expect(cellAtPoint(map, { x: 100, y: 2 * ROW_HEIGHT + 50 }, shape)).toEqual({ laneId: 'A', column: 0, row: 2 });
  });

  it('while dragged, the box of the cell each step would go to; the row a drop low in a lane would make (lot G of the plan 3.1)', () => {
    const at = { id: '3', position: { x: corner(4, 0).x, y: corner(0, 0).y } };
    expect(dropPreviewOf(map, [at])).toEqual({ boxes: [stepBox(gridOfMap(map), { laneId: 'A', column: 4, row: 0 })], newRows: [] });
    const low = { id: '3', position: { x: corner(5, 0).x, y: ROW_HEIGHT + ROW_HEIGHT * 0.85 - 32 } };
    expect(dropPreviewOf(map, [low, { id: '1', position: { x: corner(6, 0).x, y: low.position.y } }])).toEqual({
      boxes: [{ x: corner(5, 0).x, y: 2 * ROW_HEIGHT + FREE_BAND, width: 170, height: 64 }, { x: corner(6, 0).x, y: 2 * ROW_HEIGHT + FREE_BAND, width: 170, height: 64 }],
      newRows: [{ laneId: 'A', from: 2, count: 1 }],
    });
    // With the band of the value stream layer, rows of 156 px: the centre at 450 is low in the third row of A (312 to 468).
    expect(dropPreviewOf(map, [{ id: '3', position: { x: 0, y: 450 - 32 } }], { below: 36, rows: { A: 3 } }).newRows).toEqual([{ laneId: 'A', from: 3, count: 1 }]);
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
