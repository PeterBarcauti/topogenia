// chosen.test.ts
// The routes chosen by the user, one map drawn by hand for each (ADR 091): a side the drawing already took changes nothing, not even for the flows around, a loop by the top, a departure upward round a step, an aisle and a band imposed, a passage that names nothing, the points of a gateway and its entry, the measure of the drawing.

import { routeMap } from '../../../../../test/routedMaps.ts';
import { routedMap } from '../../../../../test/randomRoutes.ts';
import type { Point } from '../grid.ts';
import type { ChosenRoute, RoutedFlow, RoutingFlow, RoutingMap, RoutingStep, StepKind } from './model.ts';
import { layoutFlows } from './routing.ts';

// Lines: l0 from 0 (centre 60, steps 28 to 92), l1 from 120 (centre 180), l2 from 240 (centre 300).
// Columns: c starts at 30 + 220c, its axis at 115 + 220c, its right side at 200 + 220c, the aisle right of it at 225 + 220c.
const LANES = [0, 1, 2].map((order) => ({ id: `l${order}`, order }));

const step = (id: string, lane: number, column: number, type: StepKind = 'task', row = 0): RoutingStep => ({ id, laneId: `l${lane}`, column, row, type });
const flow = (id: string, sourceId: string, targetId: string, route?: ChosenRoute): RoutingFlow =>
  ({ id, sourceId, targetId, label: null, ...(route === undefined ? {} : { route }) });

function route(steps: RoutingStep[], flows: RoutingFlow[]): Map<string, RoutedFlow> {
  return new Map(routeMap({ lanes: LANES, steps, flows }).map((routed) => [routed.id, routed]));
}

function get(routes: Map<string, RoutedFlow>, id: string): RoutedFlow {
  const routed = routes.get(id);
  if (routed === undefined) throw new Error(`No flow ${id}.`);
  return routed;
}

const xy = (points: readonly Point[]) => points.map((point) => [point.x, point.y]);

const vertical = (flow: RoutedFlow) => flow.points.slice(1).flatMap((b, index) => {
  const a = flow.points[index] as Point;
  return a.x === b.x && a.y !== b.y ? [a.x] : [];
});

const horizontal = (flow: RoutedFlow) => flow.points.slice(1).flatMap((b, index) => {
  const a = flow.points[index] as Point;
  return a.y === b.y && a.x !== b.x ? [a.y] : [];
});

describe('a route chosen by the user (ADR 091)', () => {
  it('a side that the drawing already takes changes nothing: the drawing of 3.0.2, to the point', () => {
    const steps = [step('a', 0, 0), step('b', 0, 2), step('c', 1, 1)];
    const plain = route(steps, [flow('f', 'a', 'b'), flow('g', 'b', 'c')]);
    const chosen = route(steps, [flow('f', 'a', 'b', { source: 'right', target: 'left' }), flow('g', 'b', 'c', { target: 'bottom' })]);
    expect(get(chosen, 'f')).toEqual(get(plain, 'f'));
    expect(get(chosen, 'g')).toEqual(get(plain, 'g'));
  });

  it('a side that the drawing already takes changes nothing for the flows around either: the aisles they share keep their load', () => {
    // Two flows up to one step, from two steps further on the line below: each takes its aisle by the load of the others.
    const steps = [step('t', 0, 1), step('s1', 1, 3), step('s2', 1, 4)];
    const plain = route(steps, [flow('a', 's1', 't'), flow('b', 's2', 't')]);
    const chosen = route(steps, [flow('a', 's1', 't', { target: get(plain, 'a').targetSide }), flow('b', 's2', 't')]);
    expect([get(chosen, 'a'), get(chosen, 'b')]).toEqual([get(plain, 'a'), get(plain, 'b')]);
  });

  it('a loop back on its line leaves by the top and arrives by the top, through the band above: no curve', () => {
    const routes = route([step('a', 1, 0), step('b', 1, 2)], [flow('back', 'b', 'a', { source: 'top', target: 'top' })]);
    const back = get(routes, 'back');
    expect([back.sourceSide, back.targetSide, back.curve]).toEqual(['top', 'top', null]);
    expect(xy(back.points)).toEqual([[555, 148], [555, 134], [115, 134], [115, 148]]);
  });

  it('a departure upward goes round the step in its way through the band above its line', () => {
    const routes = route([step('a', 0, 0), step('x', 0, 1), step('b', 0, 2)], [flow('f', 'a', 'b', { source: 'top' })]);
    const f = get(routes, 'f');
    expect([f.sourceSide, f.targetSide]).toEqual(['top', 'left']);
    expect(xy(f.points)).toEqual([[115, 28], [115, 14], [445, 14], [445, 60], [470, 60]]);
  });

});

describe('the passages and the gateways of a route chosen by the user (ADR 091)', () => {
  it('an aisle chosen holds the vertical segment of the flow, and a band chosen its horizontal one', () => {
    const steps = [step('a', 0, 0), step('b', 2, 3)];
    const aisle = get(route(steps, [flow('f', 'a', 'b', { via: { aisle: 1 } })]), 'f');
    expect(vertical(aisle)).toEqual([445]);
    const band = get(route(steps, [flow('f', 'a', 'b', { via: { laneId: 'l1', row: 0, edge: 'bottom' } })]), 'f');
    expect(horizontal(band).some((y) => y > 212 && y < 240)).toBe(true);
    const top = get(route(steps, [flow('f', 'a', 'b', { via: { laneId: 'l1', row: 0, edge: 'top' } })]), 'f');
    expect(horizontal(top).some((y) => y > 120 && y < 148)).toBe(true);
  });

  it('a passage that names nothing of the map leaves the drawing as it is', () => {
    const steps = [step('a', 0, 0), step('b', 1, 2)];
    const plain = get(route(steps, [flow('f', 'a', 'b')]), 'f');
    expect(get(route(steps, [flow('f', 'a', 'b', { via: { laneId: 'nowhere', row: 0, edge: 'top' } })]), 'f')).toEqual(plain);
    expect(get(route(steps, [flow('f', 'a', 'b', { via: { laneId: 'l1', row: 4, edge: 'top' } })]), 'f')).toEqual(plain);
    // An aisle right of the last column of the map is the last one: further, the flow would leave the map.
    expect(get(route(steps, [flow('f', 'a', 'b', { via: { aisle: 4 } })]), 'f')).toEqual(plain);
    expect(vertical(get(route(steps, [flow('f', 'a', 'b', { via: { aisle: 2 } })]), 'f'))).toContain(665);
  });

  it('an exit of a gateway takes the point chosen and its sister another one; a second exit on that point is drawn by the rules', () => {
    const steps = [step('g', 1, 0, 'decision'), step('up', 0, 2), step('right', 1, 2)];
    const plain = route(steps, [flow('e1', 'g', 'up'), flow('e2', 'g', 'right')]);
    expect([get(plain, 'e1').sourceSide, get(plain, 'e2').sourceSide]).toEqual(['top', 'right']);
    const swapped = route(steps, [flow('e1', 'g', 'up'), flow('e2', 'g', 'right', { source: 'top' })]);
    expect([get(swapped, 'e1').sourceSide, get(swapped, 'e2').sourceSide]).toEqual(['right', 'top']);
    const twice = route(steps, [flow('e1', 'g', 'up', { source: 'bottom' }), flow('e2', 'g', 'right', { source: 'bottom' })]);
    expect([get(twice, 'e1').sourceSide, get(twice, 'e2').sourceSide]).toEqual(['bottom', 'right']);
  });

  it('the left point of a gateway is its entry: an exit that chose it is drawn by the rules, whatever the number of exits', () => {
    const steps = [step('g', 1, 0, 'decision'), step('up', 0, 2), step('right', 1, 2)];
    const plain = route(steps, [flow('e1', 'g', 'up'), flow('e2', 'g', 'right')]);
    expect(route(steps, [flow('e1', 'g', 'up', { source: 'left' }), flow('e2', 'g', 'right')])).toEqual(plain);
    const alone = route(steps, [flow('e1', 'g', 'up')]);
    expect(route(steps, [flow('e1', 'g', 'up', { source: 'left' })])).toEqual(alone);
  });

  it('an exit that chose the entry keeps its point of the rules, which a sister moved by another choice never takes (AR-24)', () => {
    // Up and down: verticals; far: on the line of the gateway, a step in the way, so the top point then the right one.
    const steps = [step('g', 1, 0, 'decision'), step('up', 0, 0), step('down', 2, 0), step('k', 1, 1), step('far', 1, 2)];
    const sides = (routes: Map<string, RoutedFlow>) => ['e1', 'x', 'y'].map((id) => get(routes, id).sourceSide);
    expect(sides(route(steps, [flow('e1', 'g', 'up'), flow('x', 'g', 'far'), flow('y', 'g', 'down')]))).toEqual(['top', 'right', 'bottom']);
    const chosen = route(steps, [flow('e1', 'g', 'up', { source: 'left' }), flow('x', 'g', 'far'), flow('y', 'g', 'down', { source: 'right' })]);
    expect(sides(chosen)).toEqual(['top', 'right', 'right']);
  });

});

describe('the middle of a column or of a row chosen by the user (ADR 091)', () => {
  it('centres a flow in an empty column between two others, as on the map of the trial of the porteur du projet', () => {
    // Three steps one under the other in the column 0, three in the column 2, the column 1 empty between them.
    const steps = [step('e13', 0, 0), step('e14', 0, 0, 'task', 1), step('e15', 0, 0, 'task', 2), step('e16', 1, 2), step('e56', 1, 2, 'task', 1), step('e18', 2, 2)];
    const flows = (route?: ChosenRoute) => [flow('a', 'e14', 'e16', route), flow('b', 'e15', 'e56'), flow('c', 'e13', 'e18')];
    // Drawn alone, the three flows go down the aisle before the column 2 (at 445), side by side on their tracks.
    const inAisle = (routes: Map<string, RoutedFlow>, id: string) => vertical(get(routes, id)).every((x) => Math.abs(x - 445) <= 24);
    const plain = route(steps, flows());
    expect(['a', 'b', 'c'].every((id) => inAisle(plain, id))).toBe(true);
    // The first one in the middle of the empty column, exactly; the two others keep their aisle.
    const centred = route(steps, flows({ via: { column: 1 } }));
    expect(vertical(get(centred, 'a'))).toEqual([335]);
    expect([inAisle(centred, 'b'), inAisle(centred, 'c')]).toEqual([true, true]);
  });

  it('a flow on the middle of a line chosen takes its track after the ends of the others there: an arrowhead stays in the middle of its side', () => {
    // The line of l1: steps in the columns 0, 2, 3 and 4, the column 1 free; t in the column 2, its left side at 470, the middle of its line at 180.
    const steps = [step('s0', 1, 0), step('t', 1, 2, 'decision'), step('s3', 1, 3), step('s4', 1, 4), step('y0', 0, 1), step('x0', 0, 0), step('x1', 2, 2)];
    const routes = route(steps, [flow('y', 'y0', 't'), flow('x', 'x0', 'x1', { via: { laneId: 'l1', row: 0 } })]);
    expect(get(routes, 'y').points.at(-1)).toEqual({ x: 470, y: 180 });
    expect(horizontal(get(routes, 'x')).some((y) => Math.abs(y - 180) <= 24)).toBe(true);
  });

  it('runs a flow through the middle of an empty row, between the bands of its two edges', () => {
    const steps = [step('a', 0, 0), step('b', 2, 2)];
    const middle = get(route(steps, [flow('f', 'a', 'b', { via: { laneId: 'l1', row: 0 } })]), 'f');
    expect(horizontal(middle)).toContain(180);
  });

  it('never runs through a step standing on the middle chosen: it passes there where it is free', () => {
    const steps = [step('a', 0, 0), step('x', 1, 1), step('b', 2, 2)];
    const routed = get(route(steps, [flow('f', 'a', 'b', { via: { column: 1 } })]), 'f');
    expect(vertical(routed)).toContain(335);
    // The box of x: from 250 to 420 across, from 148 to 212 down.
    const crosses = routed.points.slice(1).some((b, index) => {
      const a = routed.points[index] as Point;
      return a.x === b.x ? a.x > 250 && a.x < 420 && Math.min(a.y, b.y) < 212 && Math.max(a.y, b.y) > 148
        : a.y > 148 && a.y < 212 && Math.min(a.x, b.x) < 420 && Math.max(a.x, b.x) > 250;
    });
    expect(crosses).toBe(false);
  });

  it('a middle closed by steps all around the two ends keeps the sides chosen and leaves the passage to the drawing; without sides, the drawing as it is', () => {
    // The line of l1 taken from the column 0 to the column 6: no way along its middle.
    const wall = Array.from({ length: 7 }, (_, column) => step(`w${String(column)}`, 1, column));
    const steps = [step('a', 0, 2), step('b', 2, 4), ...wall];
    const plain = get(route(steps, [flow('f', 'a', 'b')]), 'f');
    const closed = route(steps, [flow('f', 'a', 'b', { via: { laneId: 'l1', row: 0 } })]);
    expect(get(closed, 'f')).toEqual(plain);
    const sides = get(route(steps, [flow('f', 'a', 'b', { target: 'top', via: { laneId: 'l1', row: 0 } })]), 'f');
    expect(sides.targetSide).toBe('top');
    expect(horizontal(sides).includes(180)).toBe(false);
  });

  it('keeps the drawing of a flow whose end already runs on the middle chosen, and a middle beyond the map changes nothing', () => {
    const steps = [step('a', 0, 1), step('b', 2, 1)];
    const plain = get(route(steps, [flow('f', 'a', 'b')]), 'f');
    expect(get(route(steps, [flow('f', 'a', 'b', { via: { column: 1 } })]), 'f')).toEqual(plain);
    expect(get(route(steps, [flow('f', 'a', 'b', { via: { column: 2 } })]), 'f')).toEqual(plain);
    expect(get(route(steps, [flow('f', 'a', 'b', { via: { laneId: 'l1', row: 3 } })]), 'f')).toEqual(plain);
  });
});

describe('the measure of a drawing with routes chosen (ADR 091)', () => {
  it('draws a map of about 150 steps and 180 flows, 20 of them with routes of every kind, at a small cost over the drawing alone', () => {
    const build = (routes: boolean): RoutingMap => {
      const maps = Array.from({ length: 12 }, (_, index) => routedMap(400 + index));
      const lanes = maps.flatMap((map, index) => map.lanes.map((lane) => ({ id: `${index}-${lane.id}`, order: index * 10 + lane.order })));
      const steps = maps.flatMap((map, index) => map.steps.map((item) => ({ ...item, id: `${index}-${item.id}`, laneId: `${index}-${item.laneId}` })));
      // The routes of the random maps, of every kind, on one flow in ten; the others drawn alone.
      const flows = maps.flatMap((map, index) => map.flows.map(({ route: chosen, ...item }, at): RoutingFlow => {
        const via = chosen?.via !== undefined && 'laneId' in chosen.via ? { ...chosen.via, laneId: `${String(index)}-${chosen.via.laneId}` } : chosen?.via;
        const kept: ChosenRoute = { source: chosen?.source ?? 'top', ...(chosen?.target === undefined ? {} : { target: chosen.target }), via: via ?? { aisle: at % 5 } };
        return {
          ...item, id: `${String(index)}-${item.id}`, sourceId: `${String(index)}-${item.sourceId}`, targetId: `${String(index)}-${item.targetId}`,
          ...(routes && at % 10 === 0 ? { route: kept } : {}),
        };
      }));
      return { lanes, steps, flows };
    };
    const routed = build(true);
    const plain = build(false);
    expect(routed.steps.length).toBeGreaterThan(120);
    expect(routed.flows.filter((item) => item.route !== undefined).length).toBeGreaterThanOrEqual(15);
    const time = (map: RoutingMap) => {
      const start = performance.now();
      layoutFlows(map);
      return performance.now() - start;
    };
    time(routed);
    time(plain);
    // Measured in turn, so that a busy machine slows both alike; the medians of eleven drawings.
    const pairs = Array.from({ length: 11 }, () => [time(plain), time(routed)] as const);
    const median = (list: number[]) => [...list].sort((a, b) => a - b)[5] ?? 0;
    expect(median(pairs.map(([, b]) => b))).toBeLessThan(median(pairs.map(([a]) => a)) * 4 + 30);
  }, 60_000);
});
