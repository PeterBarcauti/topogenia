// flowRoutes.test.ts
// The route of a flow between the API and the drawing (ADR 091): read part by part, written whole, compared, a side or a passage changed, the drawing alone when nothing is chosen.

import { chosenRouteOf, isEmpty, routeBody, routeView, sameRoute, withPassage, withSide } from './flowRoutes.ts';

describe('the route of a flow between the API and the drawing (ADR 091)', () => {
  it('reads the parts the API gives, and nothing for a route of nothing or a band not whole', () => {
    expect(chosenRouteOf({ source_side: 'top', target_side: null, via: { aisle: -1, column: null, lane_id: null, row: null, edge: null } })).toEqual({ source: 'top', via: { aisle: -1 } });
    expect(chosenRouteOf({ source_side: null, target_side: 'left', via: { aisle: null, column: null, lane_id: 'l1', row: 0, edge: 'bottom' } }))
      .toEqual({ target: 'left', via: { laneId: 'l1', row: 0, edge: 'bottom' } });
    expect(chosenRouteOf({ source_side: null, target_side: null, via: { aisle: null, column: 2, lane_id: null, row: null, edge: null } })).toEqual({ via: { column: 2 } });
    expect(chosenRouteOf({ source_side: null, target_side: null, via: { aisle: null, column: null, lane_id: 'l1', row: 1, edge: null } })).toEqual({ via: { laneId: 'l1', row: 1 } });
    expect(chosenRouteOf({ source_side: null, target_side: null, via: { aisle: null, column: null, lane_id: 'l1', row: 0, edge: 'left' } })).toBeUndefined();
    expect(chosenRouteOf({ source_side: null, target_side: null, via: { aisle: null, column: null, lane_id: 'l1', row: null, edge: 'top' } })).toBeUndefined();
    expect(chosenRouteOf({ source_side: null, target_side: null, via: null })).toBeUndefined();
    expect(chosenRouteOf(null)).toBeUndefined();
  });

  it('writes the route whole, every part named, and gives back the shape of the API', () => {
    expect(routeBody({ source: 'bottom', via: { laneId: 'l2', row: 1, edge: 'top' } }))
      .toEqual({ source_side: 'bottom', target_side: null, via: { aisle: null, column: null, lane_id: 'l2', row: 1, edge: 'top' } });
    expect(routeBody({ via: { column: 4 } })).toEqual({ source_side: null, target_side: null, via: { aisle: null, column: 4, lane_id: null, row: null, edge: null } });
    expect(routeBody({ via: { laneId: 'l2', row: 0 } })).toEqual({ source_side: null, target_side: null, via: { aisle: null, column: null, lane_id: 'l2', row: 0, edge: null } });
    expect(routeBody(undefined)).toEqual({ source_side: null, target_side: null, via: null });
    expect(routeView({ target: 'right', via: { aisle: 3 } })).toEqual({ source_side: null, target_side: 'right', via: { aisle: 3, column: null, lane_id: null, row: null, edge: null } });
    expect(routeView({})).toBeNull();
  });

  it('changes a side or the passage alone, the drawing alone when nothing is left', () => {
    expect(sameRoute({ via: { aisle: 1 } }, { via: { column: 1 } })).toBe(false);
    expect(sameRoute({ via: { column: 1 } }, { via: { column: 1 } })).toBe(true);
    expect(sameRoute({ via: { laneId: 'l1', row: 0 } }, { via: { laneId: 'l1', row: 0, edge: 'top' } })).toBe(false);
    expect(sameRoute({ via: { laneId: 'l1', row: 0 } }, { via: { laneId: 'l1', row: 0 } })).toBe(true);
    const route = { source: 'top' as const, via: { aisle: 2 } };
    expect(withSide(route, 'target', 'left')).toEqual({ source: 'top', target: 'left', via: { aisle: 2 } });
    expect(withSide(route, 'source', null)).toEqual({ via: { aisle: 2 } });
    expect(withPassage(route, null)).toEqual({ source: 'top' });
    expect(withPassage(withSide(route, 'source', null), null)).toBeUndefined();
    expect(withPassage(undefined, { laneId: 'l1', row: 0, edge: 'top' })).toEqual({ via: { laneId: 'l1', row: 0, edge: 'top' } });
  });

  it('compares two routes part by part, an empty one as the drawing alone', () => {
    expect(sameRoute({ source: 'top', via: { aisle: 1 } }, { via: { aisle: 1 }, source: 'top' })).toBe(true);
    expect(sameRoute({ via: { aisle: 1 } }, { via: { aisle: 2 } })).toBe(false);
    expect(sameRoute({ via: { laneId: 'l1', row: 0, edge: 'top' } }, { via: { laneId: 'l1', row: 0, edge: 'bottom' } })).toBe(false);
    expect(sameRoute({ via: { aisle: 1 } }, { via: { laneId: 'l1', row: 0, edge: 'top' } })).toBe(false);
    expect(sameRoute({}, undefined)).toBe(true);
    expect([isEmpty({}), isEmpty(undefined), isEmpty({ target: 'top' })]).toEqual([true, true, false]);
  });
});
