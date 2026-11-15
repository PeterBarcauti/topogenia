// chosenProperties.test.ts
// The properties of the routing on 500 maps drawn at random with routes chosen at random (ADR 091): no flow across a step, the sides chosen kept, the passage taken when it names a place, no overlap but on a common arrival, one point per arrival and per exit of a gateway, labels off the steps, the same drawing every time.

import { pointOnCurve, routeMap } from '../../../../../test/routedMaps.ts';
import { randomMap, seeded, shuffled } from '../../../../../test/randomMaps.ts';
import { routedMap } from '../../../../../test/randomRoutes.ts';
import { aisleX, axisX } from './model.ts';
import { FREE_BAND, STEP_HEIGHT, STREAM_BAND } from '../constants.ts';
import { gridOf, stepBox, type Point, type Rect } from '../grid.ts';
import { bandOf, lineOfPassage, passageHolds } from './chosen.ts';
import { overlaps } from './labels.ts';
import { contextOf, isGateway, type RoutedFlow, type RoutingMap, type Side } from './model.ts';

const MAPS = 500;
const SEEDS = Array.from({ length: MAPS }, (_, index) => index + 1);
/** Each property routes 500 maps: a delay of its own, so that a busy machine never fails it (F-34). */
const DELAY = 60_000;

interface Segment {
  readonly a: Point;
  readonly b: Point;
}

const segmentsOf = (flow: RoutedFlow): Segment[] =>
  flow.curve === null ? flow.points.slice(1).map((b, index) => ({ a: flow.points[index] as Point, b })) : [];

const inside = (box: Rect): Rect => ({ x: box.x + 0.5, y: box.y + 0.5, width: box.width - 1, height: box.height - 1 });

function crosses({ a, b }: Segment, box: Rect): boolean {
  const room = inside(box);
  if (a.y === b.y) return a.y > room.y && a.y < room.y + room.height && Math.min(a.x, b.x) < room.x + room.width && Math.max(a.x, b.x) > room.x;
  return a.x > room.x && a.x < room.x + room.width && Math.min(a.y, b.y) < room.y + room.height && Math.max(a.y, b.y) > room.y;
}

function curveCrosses(flow: RoutedFlow, box: Rect): boolean {
  const room = inside(box);
  if (flow.curve === null) return false;
  for (let step = 1; step < 64; step++) {
    const point = pointOnCurve(flow.curve, step / 64);
    if (point.x > room.x && point.x < room.x + room.width && point.y > room.y && point.y < room.y + room.height) return true;
  }
  return false;
}

function collinear(s: Segment, t: Segment): number {
  const horizontal = (u: Segment) => u.a.y === u.b.y;
  if (horizontal(s) !== horizontal(t)) return 0;
  const [along, across] = horizontal(s) ? (['x', 'y'] as const) : (['y', 'x'] as const);
  if (Math.abs(s.a[across] - t.a[across]) > 0.01) return 0;
  const low = Math.max(Math.min(s.a[along], s.b[along]), Math.min(t.a[along], t.b[along]));
  const high = Math.min(Math.max(s.a[along], s.b[along]), Math.max(t.a[along], t.b[along]));
  return Math.max(0, high - low);
}

const same = (p: Point | undefined, q: Point | undefined) => p !== undefined && q !== undefined && Math.abs(p.x - q.x) < 0.01 && Math.abs(p.y - q.y) < 0.01;

const endsTogether = (f: RoutedFlow, g: RoutedFlow) => f.targetId === g.targetId && f.targetSide === g.targetSide;

function boxesOf(map: RoutingMap): Map<string, Rect> {
  const grid = gridOf(map.lanes, map.steps, map.below ?? 0);
  return new Map(map.steps.map((step) => [step.id, stepBox(grid, step)]));
}

/** Whether a point lies on a side of a box, within that side. */
function onSide(point: Point | undefined, box: Rect, side: Side): boolean {
  if (point === undefined) return false;
  const near = (a: number, b: number) => Math.abs(a - b) < 0.01;
  if (side === 'top' || side === 'bottom') return near(point.y, side === 'top' ? box.y : box.y + box.height) && point.x > box.x && point.x < box.x + box.width;
  return near(point.x, side === 'left' ? box.x : box.x + box.width) && point.y > box.y && point.y < box.y + box.height;
}

/**
 * The map of a seed with its routes at random and its drawing, made once for all the properties of the file, which only
 * read them: the file then weighs on the other files run beside it no more than it needs (the first render of React Flow
 * in each file stays within its 5 s, P6-T06).
 */
const drawings = new Map<number, { map: RoutingMap; flows: RoutedFlow[] }>();
function drawingOf(seed: number): { map: RoutingMap; flows: RoutedFlow[] } {
  const known = drawings.get(seed);
  if (known !== undefined) return known;
  const map = routedMap(seed);
  const drawing = { map, flows: routeMap(map) };
  drawings.set(seed, drawing);
  return drawing;
}

/** The failures of a property on the 500 maps: the maps with routes at random, or the maps given for each seed. */
function failures(check: (map: RoutingMap, flows: RoutedFlow[]) => string[], mapOf?: (seed: number) => RoutingMap): string[] {
  return SEEDS.flatMap((seed) => {
    const given = mapOf?.(seed);
    const { map, flows } = given === undefined ? drawingOf(seed) : { map: given, flows: routeMap(given) };
    return check(map, flows).map((failure) => `seed ${String(seed)}: ${failure}`);
  });
}

describe('the routing with routes chosen at random on 500 maps (ADR 091)', () => {
  it('draws routes of every kind: sides on the four points, aisles and bands', () => {
    const maps = SEEDS.map(routedMap);
    const chosen = maps.flatMap((map) => map.flows.filter((flow) => flow.route !== undefined));
    expect(chosen.length).toBeGreaterThan(1000);
    expect(new Set(chosen.flatMap((flow) => [flow.route?.source, flow.route?.target]).filter((side) => side !== undefined))).toEqual(new Set(['top', 'bottom', 'left', 'right']));
    expect(chosen.filter((flow) => flow.route?.via !== undefined && 'aisle' in flow.route.via).length).toBeGreaterThan(100);
    expect(chosen.filter((flow) => flow.route?.via !== undefined && 'laneId' in flow.route.via).length).toBeGreaterThan(100);
  }, DELAY);

  it('no segment and no curve crosses a step, with or without the band of the value stream layer', () => {
    const check = (map: RoutingMap, flows: RoutedFlow[]) => {
      const boxes = [...boxesOf(map)];
      return flows.flatMap((flow) => boxes
        .filter(([, box]) => segmentsOf(flow).some((segment) => crosses(segment, box)) || curveCrosses(flow, box))
        .map(([id]) => `${flow.id} crosses ${id}`));
    };
    expect(failures(check)).toEqual([]);
    expect(failures(check, (seed) => ({ ...routedMap(seed), below: STREAM_BAND }))).toEqual([]);
  }, DELAY);

  it('a side chosen is the side the flow leaves or reaches, a departure of a gateway when it is not its entry and no other exit chose its point', () => {
    expect(failures((map, flows) => {
      const boxes = boxesOf(map);
      const steps = new Map(map.steps.map((step) => [step.id, step]));
      // A point of a gateway chosen by one exit only: kept whatever the order of the exits.
      const choosers = new Map<string, number>();
      for (const flow of map.flows)
        if (flow.route?.source !== undefined) choosers.set(`${flow.sourceId}:${flow.route.source}`, (choosers.get(`${flow.sourceId}:${flow.route.source}`) ?? 0) + 1);
      const exits = (id: string) => map.flows.filter((flow) => flow.sourceId === id).length;
      return map.flows.flatMap((flow) => {
        const routed = flows.find((item) => item.id === flow.id);
        const [source, target] = [steps.get(flow.sourceId), steps.get(flow.targetId)];
        if (routed === undefined || source === undefined || target === undefined || flow.route === undefined) return [];
        const out = flow.route.source;
        // The left point of a gateway is its entry (AR-24): never kept as a departure.
        const entry = isGateway(source) && out === 'left';
        const kept = out !== undefined && !entry && (!isGateway(source) || exits(source.id) < 2 || choosers.get(`${source.id}:${out}`) === 1);
        return [
          ...(kept && (routed.sourceSide !== out || !onSide(routed.points[0], boxes.get(source.id) as Rect, out)) ? [`${flow.id} leaves by ${routed.sourceSide}, not ${out}`] : []),
          ...(flow.route.target !== undefined && (routed.targetSide !== flow.route.target || !onSide(routed.points.at(-1), boxes.get(target.id) as Rect, flow.route.target))
            ? [`${flow.id} arrives by ${routed.targetSide}, not ${flow.route.target}`] : []),
        ];
      });
    })).toEqual([]);
  }, DELAY);

});

describe('the passages chosen at random on 500 maps (ADR 091)', () => {
  it('a passage that names a place of the map is taken: a vertical segment in its aisle, a horizontal one in its band', () => {
    expect(failures((map, flows) => {
      const context = contextOf(map);
      return map.flows.flatMap((flow) => {
        const via = flow.route?.via;
        const routed = flows.find((item) => item.id === flow.id);
        if (via === undefined || routed === undefined) return [];
        const segments = segmentsOf(routed);
        if ('aisle' in via) {
          // An aisle beyond the last column names nothing: the flow would leave the map.
          if (via.aisle > Math.max(-1, ...map.steps.map((step) => step.column))) return [];
          const x = aisleX(via.aisle);
          return segments.some(({ a, b }) => a.x === b.x && a.y !== b.y && Math.abs(a.x - x) <= 25) ? [] : [`${flow.id} misses the aisle ${via.aisle}`];
        }
        // The middle of a column or of a row may be closed by steps: its own property below.
        if ('column' in via || via.edge === undefined) return [];
        const boundary = bandOf(context, via);
        if (boundary === null) return [];
        const y = context.boundaries[boundary] ?? 0;
        return segments.some(({ a, b }) => a.y === b.y && a.x !== b.x && Math.abs(a.y - y) <= FREE_BAND) ? [] : [`${flow.id} misses the band ${boundary}`];
      });
    })).toEqual([]);
  }, DELAY);

  it('the middle of a column, and the middle of a row, that names a place of the map is taken by four flows in five at least, the others closed by steps around the two ends', () => {
    const named = { column: 0, row: 0 };
    const taken = { column: 0, row: 0 };
    failures((map, flows) => {
      const context = contextOf(map);
      for (const flow of map.flows) {
        const via = flow.route?.via;
        const routed = flows.find((item) => item.id === flow.id);
        if (via === undefined || routed === undefined || 'aisle' in via || ('laneId' in via && via.edge !== undefined) || !passageHolds(context, via)) continue;
        const kind = 'column' in via ? 'column' : 'row';
        named[kind]++;
        const segments = segmentsOf(routed);
        const line = lineOfPassage(context, via);
        const y = line === null ? 0 : (context.boundaries[line] ?? 0) + FREE_BAND + STEP_HEIGHT / 2;
        const on = 'column' in via
          ? segments.some(({ a, b }) => a.x === b.x && a.y !== b.y && Math.abs(a.x - axisX(via.column)) <= 25)
          : segments.some(({ a, b }) => a.y === b.y && a.x !== b.x && Math.abs(a.y - y) <= 25);
        if (on) taken[kind]++;
      }
      return [];
    });
    expect([named.column > 50, named.row > 50]).toEqual([true, true]);
    expect([taken.column / named.column > 0.8, taken.row / named.row > 0.8]).toEqual([true, true]);
  }, DELAY);
});

describe('the flows with routes chosen at random among the others on 500 maps (ADR 091)', () => {
  it('routes that choose the sides the drawing already takes change no flow of the map, to the point', () => {
    expect(SEEDS.flatMap((seed) => {
      const map = randomMap(seed);
      const plain = routeMap(map);
      const random = seeded(seed * 31 + 7);
      const flows = map.flows.map((flow) => {
        const drawn = plain.find((item) => item.id === flow.id);
        if (drawn === undefined || random() >= 0.35) return flow;
        return { ...flow, route: { ...(random() < 0.7 ? { source: drawn.sourceSide } : {}), target: drawn.targetSide } };
      });
      return JSON.stringify(routeMap({ ...map, flows })) === JSON.stringify(plain) ? [] : [`seed ${String(seed)}: the drawing changes`];
    })).toEqual([]);
  }, DELAY);

  it('no two flows of different sources run on each other more than 8 px, nor two exits of a gateway, but on their common arrival (AR-24, AR-129)', () => {
    expect(failures((map, flows) => {
      const gateways = new Set(map.steps.filter(isGateway).map((step) => step.id));
      const apart = (f: RoutedFlow, g: RoutedFlow) => f.sourceId !== g.sourceId || gateways.has(f.sourceId);
      const arrival = (f: RoutedFlow, g: RoutedFlow, s: number, t: number) => endsTogether(f, g) && s === segmentsOf(f).length - 1 && t === segmentsOf(g).length - 1;
      return flows.flatMap((f, i) => flows.slice(i + 1).filter((g) => apart(f, g))
        .filter((g) => segmentsOf(f).some((s, si) => segmentsOf(g).some((t, ti) => collinear(s, t) > 8 && !arrival(f, g, si, ti))))
        .map((g) => `${f.id} on ${g.id}`));
    })).toEqual([]);
  }, DELAY);

  it('the flows into one side of a step end on one point, and the exits of a gateway leave from distinct points (AR-24, AR-129)', () => {
    expect(failures((map, flows) => {
      const gateways = new Set(map.steps.filter(isGateway).map((step) => step.id));
      return flows.flatMap((f, i) => flows.slice(i + 1).flatMap((g) => [
        ...(endsTogether(f, g) && !same(f.points.at(-1), g.points.at(-1)) ? [`${f.id} and ${g.id} end apart`] : []),
        ...(f.sourceId === g.sourceId && gateways.has(f.sourceId) && same(f.points[0], g.points[0]) ? [`${f.id} and ${g.id} leave together`] : []),
      ]));
    })).toEqual([]);
  }, DELAY);

  it('every label is placed off the steps and off the other labels', () => {
    expect(failures((map, flows) => {
      const boxes = [...boxesOf(map).values()];
      const labels = flows.flatMap((flow) => (flow.label === null ? [] : [[flow.id, flow.label] as const]));
      return [
        ...labels.filter(([, rect]) => boxes.some((box) => overlaps(rect, box))).map(([id]) => `label of ${id} on a step`),
        ...labels.flatMap(([id, rect], i) => labels.slice(i + 1).filter(([, other]) => overlaps(rect, other)).map(([other]) => `label of ${id} on ${other}`)),
      ];
    })).toEqual([]);
  }, DELAY);

  it('the drawing is the same every time, whatever the order of the lists received', () => {
    expect(failures((map, flows) => {
      const again = routeMap(shuffled(map, 11));
      const byId = (list: RoutedFlow[]) => [...list].sort((a, b) => (a.id < b.id ? -1 : 1));
      return JSON.stringify(byId(again)) === JSON.stringify(byId(flows)) && JSON.stringify(routeMap(map)) === JSON.stringify(flows) ? [] : ['the drawing changes'];
    })).toEqual([]);
  }, DELAY);
});
