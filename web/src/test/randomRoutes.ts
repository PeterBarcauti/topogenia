// randomRoutes.ts
// The maps drawn at random with routes chosen at random on about a third of their flows: sides, aisles and bands of the map, and passages that name nothing (ADR 091).

import type { ChosenRoute, Passage, RoutingMap, Side } from '../features/map/model/geometry/routing/model.ts';
import { randomMap, seeded } from './randomMaps.ts';

const SIDES: readonly Side[] = ['top', 'bottom', 'left', 'right'];

const pick = <T>(random: () => number, list: readonly T[]): T => list[Math.floor(random() * list.length)] as T;

/**
 * A passage of the map: an aisle from −1 to one beyond the last column, the middle of a column up to one beyond the last,
 * or a row of a lane, along a band or through its middle; one in ten names nothing.
 */
function passageOf(random: () => number, map: RoutingMap): Passage {
  const columns = Math.max(0, ...map.steps.map((step) => step.column));
  if (random() < 0.1) return random() < 0.5 ? { laneId: 'nowhere', row: 0, edge: 'top' } : { laneId: map.lanes[0]?.id ?? 'l0', row: 9, edge: 'bottom' };
  const kind = random();
  if (kind < 0.3) return { aisle: Math.floor(random() * (columns + 3)) - 1 };
  if (kind < 0.5) return { column: Math.floor(random() * (columns + 2)) };
  const lane = pick(random, map.lanes).id;
  const rows = Math.max(1, ...map.steps.filter((step) => step.laneId === lane).map((step) => step.row + 1));
  const row = Math.floor(random() * rows);
  return kind < 0.75 ? { laneId: lane, row, edge: random() < 0.5 ? 'top' : 'bottom' } : { laneId: lane, row };
}

/** A route of one to three parts. */
function routeOf(random: () => number, map: RoutingMap): ChosenRoute {
  for (;;) {
    const route: ChosenRoute = {
      ...(random() < 0.5 ? { source: pick(random, SIDES) } : {}),
      ...(random() < 0.5 ? { target: pick(random, SIDES) } : {}),
      ...(random() < 0.4 ? { via: passageOf(random, map) } : {}),
    };
    if (route.source !== undefined || route.target !== undefined || route.via !== undefined) return route;
  }
}

/** The random map of a seed, with routes chosen at random on about a third of its flows. */
export function routedMap(seed: number): RoutingMap {
  const map = randomMap(seed);
  const random = seeded(seed * 7919 + 13);
  return { ...map, flows: map.flows.map((flow) => (random() < 0.35 ? { ...flow, route: routeOf(random, map) } : flow)) };
}
