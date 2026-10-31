// alignment.test.ts
// The Align wand: columns after the furthest predecessor, loops back ignored, rows kept or packed, lanes untouched (annex B 2.8).

import { flowOf, laneOf, mapOf, stepOf } from '../../../test/maps.ts';
import { alignMap } from './alignment.ts';

const A = laneOf('A', 0);
const B = laneOf('B', 1);
const step = (id: string, lane: string, column: number, row = 0, type: 'task' | 'decision' | 'parallel' = 'task') => stepOf(id, lane, column, { row, type });
const flow = (source: string, target: string) => flowOf(`${source}>${target}`, source, target);
const chain = (...ids: string[]) => ids.slice(1).map((id, index) => flow(ids[index] as string, id));
const cellOf = (cells: ReturnType<typeof alignMap>, id: string) => cells.find((cell) => cell.id === id);

describe('the columns of the Align wand (B-2.8-01 to B-2.8-07)', () => {
  it('move nothing on a map already aligned (B-2.8-17)', () => {
    expect(alignMap(mapOf({ lanes: [A], steps: [step('1', 'A', 0), step('2', 'A', 1), step('3', 'A', 2)], flows: chain('1', '2', '3') }))).toEqual([]);
  });

  it('close the empty columns: each step one column after its predecessor (B-2.8-04)', () => {
    expect(alignMap(mapOf({ lanes: [A], steps: [step('1', 'A', 0), step('2', 'A', 3), step('3', 'A', 7)], flows: chain('1', '2', '3') }))).toEqual([
      { id: '2', laneId: 'A', column: 1, row: 0 },
      { id: '3', laneId: 'A', column: 2, row: 0 },
    ]);
  });

  it('push a step placed before its predecessor, in its own lane (B-2.8-14)', () => {
    expect(alignMap(mapOf({ lanes: [A, B], steps: [step('1', 'A', 0), step('2', 'A', 2), step('3', 'B', 1)], flows: chain('1', '2', '3') }))).toEqual([
      { id: '2', laneId: 'A', column: 1, row: 0 },
      { id: '3', laneId: 'B', column: 2, row: 0 },
    ]);
  });

  it('keep a vertical flow wanted and ignore the loops back (B-2.8-01, B-2.8-02, B-2.8-05)', () => {
    const map = mapOf({ lanes: [A, B], steps: [step('1', 'A', 0), step('2', 'B', 0), step('3', 'A', 1)], flows: [...chain('1', '2', '3'), flow('3', '1')] });
    expect(alignMap(map)).toEqual([]);
  });

  it('a loop back does not push its target: the decision returning to an earlier step keeps the flow in order', () => {
    const map = mapOf({ lanes: [A], steps: [step('1', 'A', 0), step('2', 'A', 1), step('D', 'A', 2, 0, 'decision'), step('3', 'A', 6)], flows: [...chain('1', '2', 'D', '3'), flow('D', '2')] });
    expect(alignMap(map)).toEqual([{ id: '3', laneId: 'A', column: 3, row: 0 }]);
  });

  it('a decision: the short branch and the long one stay readable, the reading order breaking the ties (B-2.8-03)', () => {
    const map = mapOf({ lanes: [A], steps: [step('1', 'A', 0), step('D', 'A', 1, 0, 'decision'), step('2', 'A', 2), step('3', 'A', 5)], flows: [...chain('1', 'D', '2', '3'), flow('D', '3')] });
    expect(alignMap(map)).toEqual([{ id: '3', laneId: 'A', column: 3, row: 0 }]);
  });

  it('a secondary start stands just before the step it feeds, not left of the map (B-2.8-06)', () => {
    const map = mapOf({ lanes: [A, B], steps: [step('a', 'A', 0), step('b', 'A', 1), step('c', 'A', 2), step('f', 'B', 1)], flows: [...chain('a', 'b', 'c'), flow('f', 'c')] });
    expect(alignMap(map)).toEqual([]);
  });

  it('a piece of map not linked to the rest keeps its first column (B-2.8-07)', () => {
    const map = mapOf({ lanes: [A, B], steps: [step('1', 'A', 0), step('2', 'A', 1), step('3', 'B', 4), step('4', 'B', 6)], flows: [flow('1', '2'), flow('3', '4')] });
    expect(alignMap(map)).toEqual([{ id: '4', laneId: 'B', column: 5, row: 0 }]);
  });

  it('a map without steps gives nothing', () => {
    expect(alignMap(mapOf({ lanes: [A] }))).toEqual([]);
  });
});

describe('the rows of the Align wand (B-2.8-08 to B-2.8-14)', () => {
  it('bring back on its row a step moved down by hand (B-2.8-08)', () => {
    expect(alignMap(mapOf({ lanes: [A], steps: [step('1', 'A', 0), step('2', 'A', 1, 1), step('3', 'A', 2)], flows: chain('1', '2', '3') }))).toEqual([
      { id: '2', laneId: 'A', column: 1, row: 0 },
    ]);
  });

  it('parallel branches of one lane: one row a branch, each followed (B-2.8-08, B-2.8-09)', () => {
    const map = mapOf({
      lanes: [A],
      steps: [step('P', 'A', 0, 0, 'parallel'), step('x1', 'A', 1), step('x2', 'A', 2), step('y1', 'A', 1, 1), step('y2', 'A', 4), step('J', 'A', 5, 0, 'parallel')],
      flows: [...chain('P', 'x1', 'x2', 'J'), ...chain('P', 'y1', 'y2', 'J')],
    });
    const cells = alignMap(map);
    expect([cellOf(cells, 'y2'), cellOf(cells, 'J'), cellOf(cells, 'x2')]).toEqual([
      { id: 'y2', laneId: 'A', column: 2, row: 1 },
      { id: 'J', laneId: 'A', column: 3, row: 0 },
      undefined,
    ]);
  });

  it('take the empty rows out (B-2.8-12)', () => {
    expect(alignMap(mapOf({ lanes: [A], steps: [step('1', 'A', 0, 2)] }))).toEqual([{ id: '1', laneId: 'A', column: 0, row: 0 }]);
  });

  it('a lone step never takes the cell of the flow: the linked steps are placed first (B-2.8-10, B-2.8-11)', () => {
    const cells = alignMap(mapOf({ lanes: [A], steps: [step('s', 'A', 0), step('x', 'A', 3), step('lone', 'A', 1)], flows: [flow('s', 'x')] }));
    expect(cellOf(cells, 'x')).toEqual({ id: 'x', laneId: 'A', column: 1, row: 0 });
    expect(cellOf(cells, 'lone')?.row).toBe(1);
  });

  it('never two steps in one cell (B-2.8-13)', () => {
    const map = mapOf({ lanes: [A], steps: [step('1', 'A', 0), step('2', 'A', 3), step('3', 'A', 1, 1)], flows: [flow('1', '2'), flow('1', '3')] });
    const final = new Map(map.steps.map((one) => [one.id, `${one.lane_id}:${String(one.column)}:${String(one.row)}`]));
    for (const cell of alignMap(map)) final.set(cell.id, `${cell.laneId}:${String(cell.column)}:${String(cell.row)}`);
    expect(new Set(final.values()).size).toBe(3);
  });

  it('never changes the lane of a step, even when its predecessor is in another lane (B-2.8-14)', () => {
    const map = mapOf({ lanes: [A, B], steps: [step('1', 'A', 0), step('2', 'B', 5, 2), step('3', 'A', 9)], flows: chain('1', '2', '3') });
    const cells = alignMap(map);
    expect(cells.map((cell) => [cell.id, cell.laneId])).toEqual([['2', 'B'], ['3', 'A']]);
    expect(cells.map((cell) => [cell.column, cell.row])).toEqual([[1, 0], [2, 0]]);
  });
});
