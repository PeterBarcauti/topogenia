// gridShape.test.ts
// The grid shown (ADR 096): a row exists by a step on it or by a flow pinned to its middle or a band of it, as the host counts it; the empty rows a lane shows at least; the band of the value stream layer.

import { flowOf, laneOf, mapOf, stepOf } from '../../../test/maps.ts';
import { gridOfMap, holdingCells, PLAIN_SHAPE } from './gridShape.ts';

const route = (via: Record<string, unknown>) => ({ source_side: null, target_side: null, via: { aisle: null, column: null, lane_id: null, row: null, edge: null, ...via } });

const map = mapOf({
  lanes: [laneOf('A', 0), laneOf('B', 1)],
  steps: [stepOf('a', 'A', 0), stepOf('b', 'B', 1)],
  flows: [
    flowOf('pinned', 'a', 'b', { route: route({ lane_id: 'A', row: 2, edge: 'bottom' }) }),
    flowOf('aisle', 'b', 'a', { route: route({ aisle: 1 }) }),
  ],
});

describe('the grid shown (ADR 096)', () => {
  it('what holds a row: each step, and each flow pinned to a row; a flow through an aisle holds none', () => {
    expect(holdingCells(map)).toEqual([{ laneId: 'A', column: 0, row: 0 }, { laneId: 'B', column: 1, row: 0 }, { laneId: 'A', column: 0, row: 2 }]);
  });

  it('a lane down to the row a flow is pinned to; more empty rows when it shows them; rows of 156 px under the value stream layer', () => {
    expect([...gridOfMap(map).rows]).toEqual([['A', 3], ['B', 1]]);
    expect([...gridOfMap(map, { below: 0, rows: { B: 2 } }).rows]).toEqual([['A', 3], ['B', 2]]);
    expect([gridOfMap(map, { below: 36, rows: {} }).height, gridOfMap(map, PLAIN_SHAPE).height]).toEqual([624, 480]);
  });
});
