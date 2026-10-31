// clipboard.test.ts
// The block copied or cut: relative to its first step in the reading order, with its inner flows; where it lands (annex B 2.7).

import { flowOf, laneOf, mapOf, PROJECT_ID, MAP_ID, stepOf } from '../../../test/maps.ts';
import { cellRightOf, endOfMap, extractBlock, insertionCell, laneSpan, placeBlock, type ClipStep } from './clipboard.ts';
import { laneRanks, readingOrder } from './readingOrder.ts';

const lanes = [laneOf('A', 0), laneOf('B', 1), laneOf('C', 2)];
const map = mapOf({
  lanes,
  steps: [stepOf('a', 'A', 2, { label: 'Saisir' }), stepOf('b', 'B', 3, { row: 1, type: 'decision', label: 'Conforme ?' }), stepOf('c', 'A', 4), stepOf('x', 'C', 0)],
  flows: [flowOf('ab', 'a', 'b', { label: 'oui' }), flowOf('bc', 'b', 'c'), flowOf('cx', 'c', 'x')],
});

describe('the reading order (spec 13)', () => {
  it('sorts by column, then lane in the order of the lanes, then row', () => {
    const steps = [stepOf('3', 'A', 1, { row: 1 }), stepOf('2', 'B', 0), stepOf('4', 'B', 1), stepOf('1', 'A', 0), stepOf('5', 'A', 1)];
    expect([...steps].sort(readingOrder(laneRanks([laneOf('B', 1), laneOf('A', 0)]))).map((step) => step.id)).toEqual(['1', '2', '5', '3', '4']);
  });
});

describe('the block of the clipboard (B-2.7-04 to B-2.7-08)', () => {
  it('is relative to its first step in the reading order: lane, column and row (B-2.7-04, B-2.7-05)', () => {
    const block = extractBlock(map, ['c', 'b', 'a'], 'copy');
    expect(block?.steps.map((step) => [step.label, step.type, step.lane, step.column, step.row])).toEqual([
      ['Saisir', 'task', 0, 0, 0], ['Conforme ?', 'decision', 1, 1, 1], ['Étape c', 'task', 0, 2, 0],
    ]);
    expect([block?.mode, block?.projectId, block?.mapId]).toEqual(['copy', PROJECT_ID, MAP_ID]);
  });

  it('carries only the flows whose both ends are in it (B-2.7-06)', () => {
    expect(extractBlock(map, ['a', 'b', 'c'], 'copy')?.flows).toEqual([{ source: 'c0', target: 'c1', label: 'oui' }, { source: 'c1', target: 'c2', label: null }]);
  });

  it('a copy takes keys of the paste, c0 and the next ones; a cut the identifiers of the steps (B-2.7-07, B-2.7-08)', () => {
    expect(extractBlock(map, ['b', 'a'], 'copy')?.steps.map((step) => step.key)).toEqual(['c0', 'c1']);
    expect(extractBlock(map, ['b', 'a'], 'cut')?.steps.map((step) => step.key)).toEqual(['a', 'b']);
    expect(extractBlock(map, ['b', 'a'], 'cut')?.flows).toEqual([{ source: 'a', target: 'b', label: 'oui' }]);
  });

  it('is nothing when no step chosen is on the map', () => {
    expect(extractBlock(map, ['gone'], 'copy')).toBeNull();
  });
});

describe('the block put down (B-2.7-15 to B-2.7-18)', () => {
  const block = (extractBlock(map, ['a', 'b', 'c'], 'copy')?.steps ?? []) as ClipStep[];

  it('its first step lands in the cell aimed, the rest keeps its shape (B-2.7-15)', () => {
    expect(placeBlock(block, lanes, { laneId: 'A', column: 6, row: 0 })).toEqual([
      { key: 'c0', laneId: 'A', column: 6, row: 0 }, { key: 'c1', laneId: 'B', column: 7, row: 1 }, { key: 'c2', laneId: 'A', column: 8, row: 0 },
    ]);
  });

  it('a block that would rise above the first lane goes down as much (B-2.7-16)', () => {
    const rising: ClipStep[] = [{ key: 'k0', label: '', type: 'task', lane: 0, column: 0, row: 0 }, { key: 'k1', label: '', type: 'task', lane: -1, column: 1, row: 0 }];
    expect(placeBlock(rising, lanes, { laneId: 'A', column: 0, row: 0 })?.map((cell) => cell.laneId)).toEqual(['B', 'A']);
  });

  it('the rows go up to stay positive (B-2.7-17)', () => {
    const lower: ClipStep[] = [{ key: 'k0', label: '', type: 'task', lane: 0, column: 0, row: 0 }, { key: 'k1', label: '', type: 'task', lane: 0, column: 1, row: -2 }];
    expect(placeBlock(lower, lanes, { laneId: 'A', column: 0, row: 1 })?.map((cell) => cell.row)).toEqual([2, 0]);
  });

  it('is refused when lanes are missing under the cell aimed (B-2.7-18), or in a lane unknown', () => {
    expect(placeBlock(block, lanes, { laneId: 'C', column: 0, row: 0 })).toBeNull();
    expect(placeBlock(block, lanes, { laneId: 'Z', column: 0, row: 0 })).toBeNull();
    expect(laneSpan(block)).toBe(2);
  });
});

describe('the cells of a paste and of a duplication (B-2.7-21 to B-2.7-23)', () => {
  it('before or after a step: in its row, at its column or the next one', () => {
    const step = stepOf('s', 'B', 3, { row: 2 });
    expect([insertionCell(step, 'before'), insertionCell(step, 'after')]).toEqual([{ laneId: 'B', column: 3, row: 2 }, { laneId: 'B', column: 4, row: 2 }]);
  });

  it('the duplicate goes just right of the block: lane and row of its first step, last column + 1 (B-2.7-23)', () => {
    expect(cellRightOf(map, ['c', 'b'])).toEqual({ laneId: 'B', column: 5, row: 1 });
    expect(cellRightOf(map, ['gone'])).toBeNull();
  });

  it('the end of the map: first lane, the column after the last, row 0 (B-2.7-21)', () => {
    expect(endOfMap(map)).toEqual({ laneId: 'A', column: 5, row: 0 });
    expect(endOfMap(mapOf({ lanes: [laneOf('B', 1), laneOf('A', 0)] }))).toEqual({ laneId: 'A', column: 0, row: 0 });
    expect(endOfMap(mapOf())).toBeNull();
  });
});
