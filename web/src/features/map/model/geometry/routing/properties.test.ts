// properties.test.ts
// The properties of the routing on 500 maps drawn at random: no flow across a step, no overlap of more than 8 px but on a common arrival, labels apart and near their flows, the same drawing every time, what hangs under a step clear of its flows; the same with the band of the value stream layer (spec 9.7, 14.5, AR-128, AR-129).

import { randomMap, shuffled } from '../../../../../test/randomMaps.ts';
import { pointOnCurve, routeMap } from '../../../../../test/routedMaps.ts';
import { STREAM_BAND, STREAM_CARD, UNDER_STEP } from '../constants.ts';
import { gridOf, stepBox, type Point, type Rect } from '../grid.ts';
import { overlaps } from './labels.ts';
import { isGateway, type RoutedFlow, type RoutingMap, type RoutingStep } from './model.ts';
import { layoutFlows } from './routing.ts';
import type { UnderSide } from './under.ts';

const MAPS = 500;
const SEEDS = Array.from({ length: MAPS }, (_, index) => index + 1);

interface Segment {
  readonly a: Point;
  readonly b: Point;
}

const segmentsOf = (flow: RoutedFlow): Segment[] =>
  flow.curve === null ? flow.points.slice(1).map((b, index) => ({ a: flow.points[index] as Point, b })) : [];

/** The inside of a box, half a pixel in: a flow may touch a step at its point, never enter it. */
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

/** The length two segments run on each other: on one line, the length their extents share. */
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

/** Two flows into one side of one step: they end together on its point (AR-129). */
const endsTogether = (f: RoutedFlow, g: RoutedFlow) => f.targetId === g.targetId && f.targetSide === g.targetSide;

function boxesOf(map: RoutingMap): Map<string, Rect> {
  const grid = gridOf(map.lanes, map.steps, map.below ?? 0);
  return new Map(map.steps.map((step) => [step.id, stepBox(grid, step)]));
}

/** The failures of a property over the maps, each named by its seed, so that a failure is replayed by its seed. */
function failures(check: (map: RoutingMap, flows: RoutedFlow[]) => string[], mapOf: (seed: number) => RoutingMap = randomMap): string[] {
  return SEEDS.flatMap((seed) => {
    const map = mapOf(seed);
    return check(map, routeMap(map)).map((failure) => `seed ${seed}: ${failure}`);
  });
}

describe('the routing on 500 maps drawn at random (spec 9.7)', () => {
  it('draws maps of every shape: lanes, rows, gateways, loops, detours, labels (S-9.7-01)', () => {
    const flows = SEEDS.flatMap((seed) => routeMap(randomMap(seed)));
    expect(SEEDS).toHaveLength(MAPS);
    expect(new Set(flows.map((flow) => flow.kind))).toEqual(new Set(['flow', 'loop', 'detour']));
    expect(flows.filter((flow) => flow.curve !== null).length).toBeGreaterThan(50);
    expect(flows.filter((flow) => flow.label !== null).length).toBeGreaterThan(500);
    expect(new Set(flows.map((flow) => flow.sourceSide))).toEqual(new Set(['right', 'top', 'bottom']));
  });

  it('no segment and no curve crosses a step (B-3.4-24)', () => {
    expect(failures((map, flows) => {
      const boxes = [...boxesOf(map)];
      return flows.flatMap((flow) => boxes
        .filter(([, box]) => segmentsOf(flow).some((segment) => crosses(segment, box)) || curveCrosses(flow, box))
        .map(([id]) => `${flow.id} crosses ${id}`));
    })).toEqual([]);
  });

  it('no two flows of different sources run on each other more than 8 px, nor two exits of a gateway, but on their common arrival (S-9.7-02, AR-24, AR-129)', () => {
    expect(failures((map, flows) => {
      const gateways = new Set(map.steps.filter(isGateway).map((step) => step.id));
      const apart = (f: RoutedFlow, g: RoutedFlow) => f.sourceId !== g.sourceId || gateways.has(f.sourceId);
      const arrival = (f: RoutedFlow, g: RoutedFlow, s: number, t: number) => endsTogether(f, g) && s === segmentsOf(f).length - 1 && t === segmentsOf(g).length - 1;
      return flows.flatMap((f, i) => flows.slice(i + 1).filter((g) => apart(f, g))
        .filter((g) => segmentsOf(f).some((s, si) => segmentsOf(g).some((t, ti) => collinear(s, t) > 8 && !arrival(f, g, si, ti))))
        .map((g) => `${f.id} on ${g.id}`));
    })).toEqual([]);
  });

  it('the flows into one side of a step end on one point, and the exits of a gateway leave from distinct points (AR-24, AR-129)', () => {
    expect(failures((map, flows) => {
      const gateways = new Set(map.steps.filter(isGateway).map((step) => step.id));
      return flows.flatMap((f, i) => flows.slice(i + 1).flatMap((g) => [
        ...(endsTogether(f, g) && !same(f.points.at(-1), g.points.at(-1)) ? [`${f.id} and ${g.id} end apart`] : []),
        ...(f.sourceId === g.sourceId && gateways.has(f.sourceId) && same(f.points[0], g.points[0]) ? [`${f.id} and ${g.id} leave together`] : []),
      ]));
    })).toEqual([]);
  });

  it('every label is placed off the steps and off the other labels (S-9.7-03, B-3.4-36)', () => {
    expect(failures((map, flows) => {
      const boxes = [...boxesOf(map).values()];
      const labelled = new Set(map.flows.filter((flow) => flow.label !== null).map((flow) => flow.id));
      const labels = flows.flatMap((flow) => (flow.label === null ? [] : [[flow.id, flow.label] as const]));
      return [
        ...flows.filter((flow) => labelled.has(flow.id) && flow.label === null).map((flow) => `${flow.id} has no label`),
        ...labels.filter(([, rect]) => boxes.some((box) => overlaps(rect, box))).map(([id]) => `label of ${id} on a step`),
        ...labels.flatMap(([id, rect], i) => labels.slice(i + 1).filter(([, other]) => overlaps(rect, other)).map(([other]) => `label of ${id} on ${other}`)),
      ];
    })).toEqual([]);
  });

  it('the drawing is the same every time, whatever the order of the lists received (S-9.7-04)', () => {
    expect(failures((map, flows) => {
      const again = routeMap(shuffled(map, 7));
      const byId = (list: RoutedFlow[]) => [...list].sort((a, b) => (a.id < b.id ? -1 : 1));
      return JSON.stringify(byId(again)) === JSON.stringify(byId(flows)) && JSON.stringify(routeMap(map)) === JSON.stringify(flows) ? [] : ['the drawing changes'];
    })).toEqual([]);
  });
});

/** A badge of 60 x 18 px hung under a step (the frequency of a detailed step, spec 9.4), on a side of its bottom point. */
function badgeUnder(box: Rect, side: Exclude<UnderSide, 'centre'>): Rect {
  const axis = box.x + box.width / 2;
  return { x: side === 'right' ? axis + 10 : axis - 70, y: box.y + box.height + 5, width: 60, height: 18 };
}

/**
 * The steps whose bottom point a flow uses and whose badge is not aside of the axis, or sits on a flow of that point
 * while the other side is free; and the steps left centred whose bottom point is not used.
 */
function badgesOnFlows(map: RoutingMap): string[] {
  const { flows, under } = layoutFlows(map);
  return [...boxesOf(map)].flatMap(([id, box]) => {
    const bottom = flows.filter((flow) => (flow.sourceSide === 'bottom' && flow.sourceId === id) || (flow.targetSide === 'bottom' && flow.targetId === id));
    const side = under.get(id) ?? 'centre';
    if (bottom.length === 0) return side === 'centre' ? [] : [`${id} moved with a free bottom`];
    if (side === 'centre') return [`${id} centred over its bottom flows`];
    const hit = (badge: Rect) => bottom.some((flow) => segmentsOf(flow).some((segment) => crosses(segment, badge)) || curveCrosses(flow, badge));
    return hit(badgeUnder(box, side)) && !hit(badgeUnder(box, side === 'right' ? 'left' : 'right')) ? [`${id}: badge ${side} on a flow, the other side free`] : [];
  });
}

describe('what hangs under a step on 500 maps drawn at random (AR-128)', () => {
  it('goes aside of the axis when a flow uses the bottom point, on a side that none of its flows runs under when there is one', () => {
    const moved = SEEDS.flatMap((seed) => [...layoutFlows(randomMap(seed)).under.values()]);
    expect(new Set(moved)).toEqual(new Set(['left', 'right']));
    expect(failures((map) => badgesOnFlows(map))).toEqual([]);
  });
});

/** The points of a flow, every pixel along its segments, or 129 points of its curve. */
function pointsOf(flow: RoutedFlow): Point[] {
  if (flow.curve !== null) {
    const curve = flow.curve;
    return Array.from({ length: 129 }, (_, step) => pointOnCurve(curve, step / 128));
  }
  return segmentsOf(flow).flatMap(({ a, b }) => {
    const count = Math.max(1, Math.ceil(Math.hypot(b.x - a.x, b.y - a.y)));
    return Array.from({ length: count + 1 }, (_, step) => ({ x: a.x + ((b.x - a.x) * step) / count, y: a.y + ((b.y - a.y) * step) / count }));
  });
}

const distance = (point: Point, box: Rect): number =>
  Math.hypot(Math.max(box.x - point.x, 0, point.x - box.x - box.width), Math.max(box.y - point.y, 0, point.y - box.y - box.height));

/** The farthest a label stands from its flow when nothing keeps it away: 24 px above or under a segment, less its half height. */
const NEAR = 16;
/** The farthest a label stands from its flow whatever keeps it away: the far side of the free band (60 px), less its half height. */
const FAR = 60;

describe('the labels near their flows on 500 maps drawn at random (spec 9.7, ADR 059 point 5)', () => {
  it('a label stands within 16 px of its flow, but over a straight flow too short for it, or when another label stands on its flow; never beyond 60 px', () => {
    expect(failures((_map, flows) => {
      const labelled = flows.filter((flow) => flow.label !== null);
      return labelled.flatMap((flow) => {
        const [label, points] = [flow.label as Rect, pointsOf(flow)];
        const gap = Math.min(...points.map((point) => distance(point, label)));
        const [a, b] = flow.points;
        // A straight flow between two steps of one line whose gap is narrower than its label: only the free band over it is free.
        const tooShort = flow.curve === null && flow.points.length === 2 && a?.y === b?.y && Math.abs((b?.x ?? 0) - (a?.x ?? 0)) < label.width + 4;
        const covered = labelled.some((other) => other !== flow && points.some((point) => distance(point, other.label as Rect) === 0));
        return gap <= NEAR || (gap <= FAR && (tooShort || covered)) ? [] : [`label of ${flow.id} ${gap.toFixed(1)} px from its flow`];
      });
    })).toEqual([]);
  });
});

/** The same maps with the band of the value stream layer under the steps of each row (spec 14.5). */
const banded = (seed: number): RoutingMap => ({ ...randomMap(seed), below: STREAM_BAND });

/** The cartouche of a step under the value stream layer: centred, 2 px under its box, or under the label of a gateway. */
function cardOf(box: Rect, step: RoutingStep): Rect {
  const top = isGateway(step) && step.below !== undefined ? UNDER_STEP.gatewayLabel + step.below.height + UNDER_STEP.gap : STREAM_CARD.top;
  return { x: box.x + (box.width - STREAM_CARD.width) / 2, y: box.y + box.height + top, width: STREAM_CARD.width, height: STREAM_CARD.height };
}

/** The cartouches of the steps of a map with the band, by step. */
function cardsOf(map: RoutingMap): Map<string, Rect> {
  const boxes = boxesOf(map);
  return new Map(map.steps.map((step) => [step.id, cardOf(boxes.get(step.id) as Rect, step)]));
}

/** The cartouches under the steps that are not gateways: those under the label of a gateway hang lower, the flows drawn over them. */
function cardsOfSteps(map: RoutingMap): [string, Rect][] {
  const cards = cardsOf(map);
  return map.steps.filter((step) => !isGateway(step)).map((step) => [step.id, cards.get(step.id) as Rect]);
}

describe('the routing with the band of the value stream layer on 500 maps drawn at random (spec 14.5)', () => {
  it('without the band, the drawing is the one of the rows of 120 px', () => {
    expect(failures((map, flows) => (JSON.stringify(routeMap({ ...map, below: 0 })) === JSON.stringify(flows) ? [] : ['the drawing changes']))).toEqual([]);
  });

  it('rows of 156 px: the steps keep their top band, the flows run under the cartouches', () => {
    const map = banded(1);
    const grid = gridOf(map.lanes, map.steps, map.below);
    expect(grid.rowHeight).toBe(156);
    const step = map.steps[0] as RoutingStep;
    expect(stepBox(grid, step).y - (grid.top.get(step.laneId) ?? 0)).toBe(28 + step.row * 156);
  });

  it('no segment and no curve crosses a step', () => {
    expect(failures((map, flows) => {
      const boxes = [...boxesOf(map)];
      return flows.flatMap((flow) => boxes
        .filter(([, box]) => segmentsOf(flow).some((segment) => crosses(segment, box)) || curveCrosses(flow, box))
        .map(([id]) => `${flow.id} crosses ${id}`));
    }, banded)).toEqual([]);
  });

  it('every label is placed off the steps, off the cartouches and off the other labels', () => {
    expect(failures((map, flows) => {
      const boxes = [...boxesOf(map).values(), ...cardsOf(map).values()];
      const labels = flows.flatMap((flow) => (flow.label === null ? [] : [[flow.id, flow.label] as const]));
      return [
        ...labels.filter(([, rect]) => boxes.some((box) => overlaps(rect, box))).map(([id]) => `label of ${id} on a step or a cartouche`),
        ...labels.flatMap(([id, rect], i) => labels.slice(i + 1).filter(([, other]) => overlaps(rect, other)).map(([other]) => `label of ${id} on ${other}`)),
      ];
    }, banded)).toEqual([]);
  });

  it('no horizontal segment runs through the cartouche of a step', () => {
    expect(failures((map, flows) => {
      const cards = cardsOfSteps(map);
      return flows.flatMap((flow) => cards
        .filter(([, card]) => segmentsOf(flow).some((segment) => segment.a.y === segment.b.y && crosses(segment, card)))
        .map(([id]) => `${flow.id} runs through the cartouche of ${id}`));
    }, banded)).toEqual([]);
  });

  it('a loop runs through the cartouche of none of the steps it passes under, only along the middle of those it joins', () => {
    expect(failures((map, flows) => {
      const cards = cardsOfSteps(map);
      return flows.flatMap((flow) => cards
        .filter(([id]) => flow.sourceId !== id && flow.targetId !== id)
        .filter(([, card]) => curveCrosses(flow, card)).map(([id]) => `${flow.id} curves through the cartouche of ${id}`));
    }, banded)).toEqual([]);
  });

  it('what hangs under a step goes aside of the axis as without the band', () => {
    expect(failures((map) => badgesOnFlows(map), banded)).toEqual([]);
  });
});
