// randomMaps.ts
// Maps drawn at random from a seed, as a user builds them: lanes and rows, steps of every type on free cells, a reading flow, gateways, loops, labels.

import type { RoutingFlow, RoutingMap, RoutingStep, StepKind } from '../features/map/model/geometry/routing/model.ts';

/** A generator of numbers in [0, 1) from a seed (mulberry32): the same seed gives the same maps. */
export function seeded(seed: number): () => number {
  let state = seed >>> 0;
  return () => {
    state = (state + 0x6d2b79f5) >>> 0;
    let t = state;
    t = Math.imul(t ^ (t >>> 15), t | 1);
    t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

const KINDS: readonly [StepKind, number][] = [['task', 0.5], ['wait', 0.1], ['start', 0.06], ['end', 0.06], ['decision', 0.14], ['parallel', 0.08], ['inclusive', 0.06]];

function kindOf(random: () => number): StepKind {
  let value = random();
  for (const [kind, share] of KINDS) {
    if (value < share) return kind;
    value -= share;
  }
  return 'task';
}

const isGatewayKind = (kind: StepKind) => kind === 'decision' || kind === 'parallel' || kind === 'inclusive';
const between = (random: () => number, low: number, high: number) => low + Math.floor(random() * (high - low + 1));

function stepsOf(random: () => number, lanes: { id: string; rows: number }[], columns: number): RoutingStep[] {
  const steps: RoutingStep[] = [];
  for (const lane of lanes)
    for (let row = 0; row < lane.rows; row++)
      for (let column = 0; column < columns; column++) {
        if (random() >= 0.42) continue;
        const type = kindOf(random);
        const below = isGatewayKind(type) ? { width: between(random, 40, 140), height: 16 } : undefined;
        steps.push({ id: `s${steps.length}`, laneId: lane.id, column, row, type, ...(below === undefined ? {} : { below }) });
      }
  return steps;
}

function flowsOf(random: () => number, steps: readonly RoutingStep[]): RoutingFlow[] {
  const reading = [...steps].sort((a, b) => a.column - b.column || (a.laneId < b.laneId ? -1 : a.laneId > b.laneId ? 1 : a.row - b.row));
  const flows: RoutingFlow[] = [];
  const pairs = new Set<string>();
  const add = (source: RoutingStep | undefined, target: RoutingStep | undefined) => {
    if (source === undefined || target === undefined || source === target || pairs.has(`${source.id}>${target.id}`)) return;
    pairs.add(`${source.id}>${target.id}`);
    const label = random() < 0.35 ? { width: between(random, 20, 110), height: 16 } : null;
    flows.push({ id: `f${flows.length}`, sourceId: source.id, targetId: target.id, label });
  };
  reading.forEach((step, index) => {
    if (index < reading.length - 1 && random() < 0.85) add(step, reading[index + between(random, 1, Math.min(3, reading.length - 1 - index))]);
    // The exits of a gateway go on in the reading most of the time, back sometimes.
    if (isGatewayKind(step.type))
      for (let exit = between(random, 1, 3); exit > 0; exit--) add(step, reading[random() < 0.75 ? between(random, index, reading.length - 1) : between(random, 0, index)]);
    if (index > 0 && random() < 0.12) add(step, reading[between(random, 0, index - 1)]);
  });
  return flows;
}

/** A map of 1 to 4 lanes of 1 or 2 rows, 4 to 10 columns, about two cells in five taken, and the flows a user would draw. */
export function randomMap(seed: number): RoutingMap {
  const random = seeded(seed);
  const lanes = Array.from({ length: between(random, 1, 4) }, (_, index) => ({ id: `l${index}`, order: index, rows: random() < 0.25 ? 2 : 1 }));
  const steps = stepsOf(random, lanes, between(random, 4, 10));
  return { lanes: lanes.map(({ id, order }) => ({ id, order })), steps, flows: flowsOf(random, steps) };
}

/** The same map with its lists in another order. */
export function shuffled(map: RoutingMap, seed: number): RoutingMap {
  const random = seeded(seed);
  const mix = <T>(list: readonly T[]): T[] => list.map((item) => [random(), item] as const).sort((a, b) => a[0] - b[0]).map(([, item]) => item);
  return { lanes: mix(map.lanes), steps: mix(map.steps), flows: mix(map.flows) };
}
