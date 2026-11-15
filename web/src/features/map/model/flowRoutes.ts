// flowRoutes.ts
// The route a user chose for a flow, between the shape of the API (`route` of a flow, the body of `PUT /api/flows/{id}/route`) and the one the drawing reads (ADR 091).

import type { Schema } from '../../../api/client.ts';
import type { ChosenRoute, Passage, Side } from './geometry/routing/model.ts';

type RouteView = Schema<'FlowRouteView'>;
export type RouteBody = Schema<'FlowRouteBody'>;

/** The sides a route chooses in the menu of a flow, the drawing first (ADR 091). */
export const ROUTE_CHOICES = ['auto', 'top', 'bottom', 'left', 'right'] as const;

const isEdge = (edge: string | null): edge is 'top' | 'bottom' => edge === 'top' || edge === 'bottom';

/** The route of a flow of the map as the drawing reads it; none when the drawing chooses everything. */
export function chosenRouteOf(route: RouteView | null | undefined): ChosenRoute | undefined {
  if (route === null || route === undefined) return undefined;
  return composed(route.source_side ?? undefined, route.target_side ?? undefined, passageOf(route.via));
}

/** The passage of a route of the API: an aisle, the middle of a column, a row along a band or through its middle. */
function passageOf(via: RouteView['via']): Passage | undefined {
  if (via === null) return undefined;
  if (via.aisle !== null) return { aisle: via.aisle };
  if (via.column !== null) return { column: via.column };
  if (via.lane_id === null || via.row === null) return undefined;
  if (via.edge === null) return { laneId: via.lane_id, row: via.row };
  return isEdge(via.edge) ? { laneId: via.lane_id, row: via.row, edge: via.edge } : undefined;
}

/** Whether a route chooses nothing: the drawing alone. */
export const isEmpty = (route: ChosenRoute | undefined): boolean => route === undefined || (route.source === undefined && route.target === undefined && route.via === undefined);

/** Two routes the same, part by part. */
export function sameRoute(a: ChosenRoute | undefined, b: ChosenRoute | undefined): boolean {
  const left = isEmpty(a) ? undefined : a;
  const right = isEmpty(b) ? undefined : b;
  return left?.source === right?.source && left?.target === right?.target && samePassage(left?.via, right?.via);
}

function samePassage(a: Passage | undefined, b: Passage | undefined): boolean {
  if (a === undefined || b === undefined) return a === b;
  if ('aisle' in a) return 'aisle' in b && a.aisle === b.aisle;
  if ('column' in a) return 'column' in b && a.column === b.column;
  return 'laneId' in b && a.laneId === b.laneId && a.row === b.row && a.edge === b.edge;
}

/** The body that writes a route, every part named, null for those left to the drawing. */
export function routeBody(route: ChosenRoute | undefined): RouteBody {
  const via = route?.via;
  return {
    source_side: route?.source ?? null,
    target_side: route?.target ?? null,
    via: via === undefined ? null
      : 'aisle' in via ? { aisle: via.aisle, column: null, lane_id: null, row: null, edge: null }
        : 'column' in via ? { aisle: null, column: via.column, lane_id: null, row: null, edge: null }
          : { aisle: null, column: null, lane_id: via.laneId, row: via.row, edge: via.edge ?? null },
  };
}

/** A route of its parts, none when it has none. */
function composed(source: Side | undefined, target: Side | undefined, via: Passage | undefined): ChosenRoute | undefined {
  const route: ChosenRoute = {
    ...(source === undefined ? {} : { source }),
    ...(target === undefined ? {} : { target }),
    ...(via === undefined ? {} : { via }),
  };
  return isEmpty(route) ? undefined : route;
}

/** The same route, a side changed: a side given back to the drawing when it is null. */
export const withSide = (route: ChosenRoute | undefined, end: 'source' | 'target', side: Side | null): ChosenRoute | undefined =>
  composed(end === 'source' ? side ?? undefined : route?.source, end === 'target' ? side ?? undefined : route?.target, route?.via);

/** The same route, its passage changed: given back to the drawing when it is null. */
export const withPassage = (route: ChosenRoute | undefined, via: Passage | null): ChosenRoute | undefined =>
  composed(route?.source, route?.target, via ?? undefined);

/** The route of the API shape for a route of the drawing, the map shown with it before the host answers. */
export function routeView(route: ChosenRoute | undefined): RouteView | null {
  if (isEmpty(route)) return null;
  const body = routeBody(route);
  return {
    source_side: body.source_side,
    target_side: body.target_side,
    via: body.via === null ? null : { aisle: body.via.aisle, column: body.via.column, lane_id: body.via.lane_id, row: body.via.row, edge: body.via.edge },
  };
}
