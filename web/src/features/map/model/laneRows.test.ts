// laneRows.test.ts
// A lane taller or shorter by a row (ADR 096): the rows it holds (steps, flows pinned to a row), shows and has room for; the rows of a pull and the lane it grows; what the editor and the host do; the map drawn while a pull is under way.

import { flowOf, laneOf, mapOf, stepOf } from '../../../test/maps.ts';
import { growth, heldRows, holds, pulledLane, roomOf, rowsPulled, rowsShown, shrinking, withRowsDraft, type RowsDraft } from './laneRows.ts';

const pinned = (row: number) => ({ source_side: null, target_side: null, via: { aisle: null, column: null, lane_id: 'b', row, edge: null } });

const map = mapOf({
  lanes: [laneOf('a', 0), laneOf('b', 1), laneOf('c', 2)],
  steps: [stepOf('a1', 'a', 0), stepOf('b1', 'b', 0), stepOf('b2', 'b', 1, { row: 2 }), stepOf('b3', 'b', 3)],
  flows: [flowOf('f', 'a1', 'b2', { route: pinned(3) })],
});

describe('the rows of a lane (ADR 096)', () => {
  it('held down to its lowest step or flow pinned to a row; shown at least one, or as many as the editor keeps; room up to the row 50', () => {
    expect([heldRows(map, 'a'), heldRows(map, 'b'), heldRows(map, 'c')]).toEqual([1, 4, 0]);
    expect([holds(map, 'b', 3), holds(map, 'b', 1), holds(map, 'b', 2)]).toEqual([true, false, true]);
    expect([rowsShown(map, 'a', { a: 4 }), rowsShown(map, 'b', { b: 2 }), rowsShown(map, 'c', {}), rowsShown(map, 'c', { c: 2 })]).toEqual([4, 4, 1, 2]);
    expect([roomOf(map, 'a', {}), roomOf(map, 'b', { b: 6 }), roomOf(map, 'a', { a: 51 }), roomOf(map, 'a', { a: 60 })]).toEqual([50, 45, 0, 0]);
  });

  it('a pull gives none under 4 px, one at least past it, one more each height of a row pulled, either way', () => {
    expect([0, 3.9, 4, 59, 61, 179, 181, -4, -300].map((dy) => rowsPulled(dy, 120))).toEqual([0, 0, 1, 1, 1, 1, 2, 1, 3]);
    expect([rowsPulled(150, 60), rowsPulled(150, 0)]).toEqual([3, 0]);
  });

  it('the border follows the hand: down, the lane above grows at its bottom; up, the lane below at its top; nothing past the ends', () => {
    const lanes = ['a', 'b', 'c'];
    expect([pulledLane(lanes, 1, 10), pulledLane(lanes, 1, -10), pulledLane(lanes, 3, 10), pulledLane(lanes, 0, -10)]).toEqual([
      { laneId: 'a', end: 'bottom' }, { laneId: 'b', end: 'top' }, { laneId: 'c', end: 'bottom' }, { laneId: 'a', end: 'top' },
    ]);
    expect([pulledLane(lanes, 0, 10), pulledLane(lanes, 3, -10), pulledLane(lanes, 1, 0)]).toEqual([null, null, null]);
  });
});

describe('a lane grown (ADR 096)', () => {
  it('at its bottom, or a lane that holds nothing at its top: empty rows the editor shows, nothing for the host; bounded by its room', () => {
    expect(growth(map, {}, 'b', 'bottom', 2)).toEqual({ rows: { b: 6 }, inserted: 0, removed: null });
    expect(growth(map, {}, 'c', 'top', 1)).toEqual({ rows: { c: 2 }, inserted: 0, removed: null });
    expect(growth(map, {}, 'a', 'bottom', 80)?.rows).toEqual({ a: 51 });
    expect(growth(map, { a: 51 }, 'a', 'bottom', 1)).toBeNull();
  });

  it('at the top of a lane that holds something: rows the host inserts; the empty rows under it keep their number, none appear', () => {
    expect(growth(map, { a: 1 }, 'b', 'top', 2)).toEqual({ rows: { a: 1 }, inserted: 2, removed: null });
    expect(growth(map, { b: 6 }, 'b', 'top', 1)).toEqual({ rows: { b: 7 }, inserted: 1, removed: null });
    expect(growth(map, { b: 3 }, 'b', 'top', 1)).toEqual({ rows: { b: 3 }, inserted: 1, removed: null });
  });
});

describe('a lane made shorter (ADR 096)', () => {
  it('its empty last row under what it holds: the editor takes it away', () => {
    expect(shrinking(map, { b: 6 }, 'b', 'bottom')).toEqual({ rows: { b: 5 }, inserted: 0, removed: null });
    expect(shrinking(map, { c: 3 }, 'c', 'top')).toEqual({ rows: { c: 2 }, inserted: 0, removed: null });
  });

  it('its empty first row where it holds something under: the host takes it out, the empty rows under it keep their number', () => {
    const lower = { ...map, steps: map.steps.map((step) => (step.lane_id === 'b' ? { ...step, row: step.row + 1 } : step)), flows: [] };
    expect(shrinking(lower, {}, 'b', 'top')).toEqual({ rows: {}, inserted: 0, removed: 0 });
    expect(shrinking(lower, { b: 7 }, 'b', 'top')).toEqual({ rows: { b: 6 }, inserted: 0, removed: 0 });
    expect(shrinking(lower, { b: 5 }, 'b', 'top')).toEqual({ rows: { b: 4 }, inserted: 0, removed: 0 });
  });

  it('never a row that holds a step or a flow pinned to it, nor the only row of a lane', () => {
    expect([shrinking(map, {}, 'b', 'top'), shrinking(map, {}, 'b', 'bottom'), shrinking(map, {}, 'c', 'top'), shrinking(map, {}, 'a', 'bottom')]).toEqual([null, null, null, null]);
  });
});

describe('the map drawn while a pull is under way (ADR 096)', () => {
  const draft = (end: 'top' | 'bottom', laneId: string, count: number, version = map.structure_version): RowsDraft => ({ laneId, end, count, sent: false, version });

  it('at the bottom: the lane shows its new rows, its band under what it showed', () => {
    const drawn = withRowsDraft(map, {}, draft('bottom', 'a', 2));
    expect([drawn.map, drawn.rows, drawn.band]).toEqual([map, { a: 3 }, { laneId: 'a', from: 1, count: 2 }]);
  });

  it('at the top of a lane that holds something: its steps and the flows pinned to its rows drawn lower, its band at its top', () => {
    const drawn = withRowsDraft(map, {}, draft('top', 'b', 1));
    expect(drawn.map.steps.map((step) => step.row)).toEqual([0, 1, 3, 1]);
    expect([drawn.map.flows[0]?.route?.via?.row, drawn.rows, drawn.band]).toEqual([4, { b: 5 }, { laneId: 'b', from: 0, count: 1 }]);
  });

  it('nothing drawn without a pull, without a row to give, or on the map its write gave back', () => {
    expect(withRowsDraft(map, {}, null)).toEqual({ map, rows: {}, band: null });
    expect(withRowsDraft(map, { a: 51 }, draft('bottom', 'a', 1)).band).toBeNull();
    expect(withRowsDraft(map, {}, draft('top', 'b', 1, map.structure_version - 1))).toEqual({ map, rows: {}, band: null });
  });
});
