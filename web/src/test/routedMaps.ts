// routedMaps.ts
// Helpers of the routing tests: the flows of a map routed alone, a point of the curve of a loop to look where it runs.

import type { Point } from '../features/map/model/geometry/grid.ts';
import type { Curve, RoutedFlow, RoutingMap } from '../features/map/model/geometry/routing/model.ts';
import { layoutFlows } from '../features/map/model/geometry/routing/routing.ts';

/** The drawing of the flows of a map whose two steps are on it; a flow to a step of another map is left out. */
export const routeMap = (map: RoutingMap): RoutedFlow[] => layoutFlows(map).flows;

/** A point of the curve of a loop, from 0 (its start) to 1 (its end). */
export function pointOnCurve(curve: Curve, t: number): Point {
  const u = 1 - t;
  const x = u * u * u * curve.start.x + 3 * u * u * t * curve.start.x + 3 * u * t * t * curve.end.x + t * t * t * curve.end.x;
  const y = u * u * u * curve.start.y + 3 * u * u * t * curve.low + 3 * u * t * t * curve.low + t * t * t * curve.end.y;
  return { x, y };
}
