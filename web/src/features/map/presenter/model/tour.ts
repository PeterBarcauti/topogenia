// tour.ts
// The tour of a presentation (ADR 097, revised for the lot J10 of 3.2): every step of the map told once, in an order a room can follow. The main path of the core is the thread; where the map divides, the other branches are told first, each to the step it ends on or joins, then the thread goes on; the other entries before the thread, what no flow reaches after it. A rule of presentation and not of the business.

import type { Schema } from '../../../../api/client.ts';
import { laneRanks, readingOrder } from '../../model/readingOrder.ts';

type MapView = Schema<'MapView'>;
type FlowView = Schema<'FlowView'>;
type StepView = Schema<'StepView'>;

/**
 * The way from a stop: the flows lit, the one that leads to the next stop first when one does, none for a jump without a
 * flow; and the step it ends on: the next stop, or, after the last stop, the step its flow closes on.
 */
export interface Passage {
  readonly flows: readonly string[];
  readonly to: string;
}

/** A step shown, and the way to the next stop, null at the last one when no flow is left to light. */
export interface Stop {
  readonly stepId: string;
  readonly next: Passage | null;
  /** Off the thread of the tour: a step of a branch, of another entry, or one no flow reaches. The counters leave it out. */
  readonly aside?: boolean;
  /** A decision shown again, back from one of its exits, to go on by the next one: it says that exit only, and counts for no step. */
  readonly again?: boolean;
}

export interface Tour {
  readonly stops: readonly Stop[];
}

/** A map read once for its tour: its steps and the flows that leave each one, the main path of the core, the reading order. */
interface Chart {
  readonly map: MapView;
  readonly steps: ReadonlyMap<string, StepView>;
  /** The flows that leave a step towards a step of the map, in the order of the map. */
  readonly exits: ReadonlyMap<string, readonly FlowView[]>;
  readonly main: readonly string[];
  readonly rank: ReadonlyMap<string, number>;
  readonly order: (a: StepView, b: StepView) => number;
}

function chartOf(map: MapView): Chart {
  const steps = new Map(map.steps.map((step) => [step.id, step]));
  const exits = new Map<string, FlowView[]>(map.steps.map((step) => [step.id, []]));
  for (const flow of map.flows) if (steps.has(flow.target_step_id)) exits.get(flow.source_step_id)?.push(flow);
  const main = map.value_stream.main_path?.steps.filter((id) => steps.has(id)) ?? [];
  return { map, steps, exits, main, rank: new Map(main.map((id, index) => [id, index])), order: readingOrder(laneRanks(map.lanes)) };
}

/** A flow back: its target stands in a column before its source, as the routing draws a loop (`chosen.ts`). */
const isBack = (chart: Chart, flow: FlowView): boolean => {
  const source = chart.steps.get(flow.source_step_id);
  const target = chart.steps.get(flow.target_step_id);
  return source !== undefined && target !== undefined && target.column < source.column;
};

/** The effective share of a flow given by the core, below any known share when unknown. */
const shareOf = (chart: Chart, flowId: string): number => chart.map.value_stream.flows[flowId]?.share ?? -1;

/**
 * The exit a line goes on by from a step: the next step of the main path of the core first, even against a stronger
 * share; else never to a step taken already; a flow back only when it is the one way on; the one that reaches the main
 * path first, then the strongest share of the core, then the reading order. Null when no exit leads to a step not taken.
 */
function onwardOf(chart: Chart, stepId: string, taken: ReadonlySet<string>): FlowView | null {
  const exits = chart.exits.get(stepId) ?? [];
  const onPath = chart.rank.get(stepId);
  const ahead = onPath === undefined ? undefined : chart.main[onPath + 1];
  const link = ahead === undefined || taken.has(ahead) ? undefined : exits.find((flow) => flow.target_step_id === ahead);
  if (link !== undefined) return link;
  const open = exits.filter((flow) => !taken.has(flow.target_step_id));
  const forward = open.filter((flow) => !isBack(chart, flow));
  const candidates = [...(forward.length > 0 ? forward : open)];
  candidates.sort((a, b) => Number(chart.rank.has(b.target_step_id)) - Number(chart.rank.has(a.target_step_id))
    || shareOf(chart, b.id) - shareOf(chart, a.id)
    || chart.order(chart.steps.get(a.target_step_id) as StepView, chart.steps.get(b.target_step_id) as StepView));
  return candidates[0] ?? null;
}

/** A line: steps one after the other, and the flow from each to the next (one less than the steps). */
interface Line {
  readonly steps: readonly string[];
  readonly links: readonly FlowView[];
}

/** The line from a step: on by `onwardOf` until no exit leads to a step that is neither taken already nor on this line. */
function lineFrom(chart: Chart, start: string, taken: ReadonlySet<string>): Line {
  const seen = new Set([...taken, start]);
  const steps = [start];
  const links: FlowView[] = [];
  for (;;) {
    const onward = onwardOf(chart, steps.at(-1) as string, seen);
    if (onward === null) return { steps, links };
    links.push(onward);
    steps.push(onward.target_step_id);
    seen.add(onward.target_step_id);
  }
}

/** A gateway whose exits are taken one at a time, each shown from the gateway: a decision, or an inclusive one (one or several, by the case). */
const isChoice = (step: StepView | undefined): boolean => step?.type === 'decision' || step?.type === 'inclusive';

/**
 * The exits of a step in the order they are told and numbered: for a decision, the stronger share of the core first,
 * then the order of the map (`exitsOf` of the script); else in the reading order of the steps they lead to.
 */
function exitsInOrder(chart: Chart, stepId: string): FlowView[] {
  const exits = [...(chart.exits.get(stepId) ?? [])];
  if (isChoice(chart.steps.get(stepId))) return exits.sort((a, b) => shareOf(chart, b.id) - shareOf(chart, a.id));
  return exits.sort((a, b) => chart.order(chart.steps.get(a.target_step_id) as StepView, chart.steps.get(b.target_step_id) as StepView));
}

/** A stop being written: the flows lit on the way that leaves it, known once the next stop is. */
interface Draft {
  readonly stepId: string;
  readonly lit: string[];
  readonly aside: boolean;
  readonly again: boolean;
}

/** The tour being written: its stops, the steps told, and the steps of the lines begun and not told yet, kept for them. */
interface Telling {
  readonly chart: Chart;
  readonly stops: Draft[];
  readonly told: Set<string>;
  readonly kept: Set<string>;
}

const stopAt = (telling: Telling, stepId: string, aside: boolean, again: boolean): Draft => {
  const draft: Draft = { stepId, lit: [], aside, again };
  telling.stops.push(draft);
  telling.told.add(stepId);
  return draft;
};

/** Whether no line has taken a step: neither told, nor kept for a line begun. */
const free = (telling: Telling, stepId: string): boolean => !telling.told.has(stepId) && !telling.kept.has(stepId);

/** Whether a flow leads to a step no line has taken: its branch is still to tell. */
const opens = (telling: Telling, flow: FlowView): boolean => free(telling, flow.target_step_id);

/** A line planned from a step, its steps kept for it against the branches told before them. */
function planned(telling: Telling, start: string): Line {
  const line = lineFrom(telling.chart, start, new Set([...telling.told, ...telling.kept]));
  for (const id of line.steps) telling.kept.add(id);
  return line;
}

/** Tells a line planned: each of its steps, with the branches that leave it, then the flow to its next step. */
function tellLine(telling: Telling, line: Line, aside: boolean): void {
  line.steps.forEach((stepId, index) => {
    telling.kept.delete(stepId);
    tellStep(telling, stepId, line.links[index] ?? null, aside);
  });
}

/**
 * Tells a step and what leaves it besides the flow its line goes on by (`onward`, null at the end of a line).
 * A decision or an inclusive gateway is shown once for each exit, the one of its line last: an exit that opens a branch
 * tells that branch; one that leads to a step told already, or kept for a line, is only lit. Any other step sends all
 * its exits at once (the core counts them as a parallel split), then each branch they open is told in turn.
 */
function tellStep(telling: Telling, stepId: string, onward: FlowView | null, aside: boolean): void {
  const { chart } = telling;
  const exits = exitsInOrder(chart, stepId);
  const others = exits.filter((flow) => flow.id !== onward?.id);
  if (isChoice(chart.steps.get(stepId)) && exits.length > 1) {
    [...others, ...(onward === null ? [] : [onward])].forEach((exit, index) => {
      stopAt(telling, stepId, aside, index > 0).lit.push(exit.id);
      if (exit !== onward && opens(telling, exit)) tellLine(telling, planned(telling, exit.target_step_id), true);
    });
    return;
  }
  const shown = stopAt(telling, stepId, aside, false);
  // The flow that leads to the next stop first: the first branch told, else the flow of the line.
  const first = others.find((flow) => opens(telling, flow)) ?? onward;
  shown.lit.push(...(first === null ? exits : [first, ...exits.filter((flow) => flow !== first)]).map((flow) => flow.id));
  for (const exit of others) if (opens(telling, exit)) tellLine(telling, planned(telling, exit.target_step_id), true);
}

/**
 * The first stop: the step chosen if it is on the map, else the start of the main path, the first start, the first entry,
 * the first step of the reading order. A main path that is one step alone, tied to nothing, on a map that has flows is
 * not a thread: without a start the core counts every step no flow reaches as an entry, and a step alone may win over
 * the process; the tour then starts as if the core gave no path (independent review of the lot J10, M9).
 */
function firstOf(chart: Chart, from: string | null): string | null {
  if (from !== null && chart.steps.has(from)) return from;
  const ordered = [...chart.map.steps].sort(chart.order);
  const head = chart.main[0];
  const alone = head !== undefined && chart.main.length === 1 && (chart.exits.get(head)?.length ?? 0) === 0
    && !chart.map.flows.some((flow) => flow.target_step_id === head);
  return (alone ? undefined : head) ?? ordered.find((step) => step.type === 'start')?.id ?? entriesOf(chart.map)[0] ?? ordered[0]?.id ?? null;
}

/** The steps a case may reach from a step, that step included, by the flows of the map. */
function reachedFrom(chart: Chart, start: string): Set<string> {
  const reached = new Set([start]);
  const pending = [start];
  for (let stepId = pending.pop(); stepId !== undefined; stepId = pending.pop()) {
    for (const flow of chart.exits.get(stepId) ?? []) {
      if (reached.has(flow.target_step_id)) continue;
      reached.add(flow.target_step_id);
      pending.push(flow.target_step_id);
    }
  }
  return reached;
}

/**
 * The entries of a map: its steps that no flow reaches and that a flow leaves, in the reading order. A case comes in by
 * one of them; the tour tells each one.
 */
export function entriesOf(map: MapView): string[] {
  const reached = new Set(map.flows.map((flow) => flow.target_step_id));
  const left = new Set(map.flows.filter((flow) => map.steps.some((step) => step.id === flow.target_step_id)).map((flow) => flow.source_step_id));
  return [...map.steps].sort(readingOrder(laneRanks(map.lanes))).filter((step) => !reached.has(step.id) && left.has(step.id)).map((step) => step.id);
}

/**
 * Every step in the reading order, a jump from one to the next: a map without any flow. From the step chosen when one
 * is, else from the first: without flows the main path of the core is one step alone, the one of the longest lead time,
 * and starting from it would leave out the steps read before it (R6-09).
 */
function readingTour(chart: Chart, from: string | null): Tour {
  const ordered = [...chart.map.steps].sort(chart.order).map((step) => step.id);
  const steps = ordered.slice(Math.max(0, from === null ? 0 : ordered.indexOf(from)));
  return { stops: steps.map((stepId, index) => ({ stepId, next: index + 1 < steps.length ? { flows: [], to: steps[index + 1] as string } : null })) };
}

/** The stops written, each with its way to the next; the last one keeps a way only for a flow still to light, which ends where that flow does. */
function written(telling: Telling): Tour {
  const { chart, stops } = telling;
  return {
    stops: stops.map((draft, index): Stop => {
      const after = stops[index + 1];
      const closes = chart.map.flows.find((flow) => flow.id === draft.lit[0])?.target_step_id;
      const to = after?.stepId ?? closes;
      return {
        stepId: draft.stepId, next: to === undefined ? null : { flows: draft.lit, to },
        ...(draft.aside ? { aside: true } : {}), ...(draft.again ? { again: true } : {}),
      };
    }),
  };
}

/**
 * The tour of a map (ADR 097 revised). From its start: the other entries first, each to the step where it joins the
 * rest, that is any step a case reaches from the start of the thread: a branch of the thread is told with the thread,
 * at the step it leaves, never before it with an entry that joins it (independent review of the lot J10, S1); then the
 * thread, the main path of the core, with the branches that leave each of its steps; then the steps no flow reaches, in
 * the reading order. From a step chosen: that step and what a case reaches from it, by the same rules, and nothing
 * else: a step before it is told only when a flow back leads to it.
 */
export function tourOf(map: MapView, from: string | null): Tour {
  const chart = chartOf(map);
  const chosen = from !== null && chart.steps.has(from) ? from : null;
  const first = firstOf(chart, chosen);
  if (first === null) return { stops: [] };
  if (map.flows.length === 0) return readingTour(chart, chosen);
  const telling: Telling = { chart, stops: [], told: new Set(), kept: new Set() };
  const thread = planned(telling, first);
  if (chosen === null) {
    // What the thread reaches is kept for it while the other entries are told, then given back but the thread itself.
    const rest = [...reachedFrom(chart, first)].filter((id) => free(telling, id));
    for (const id of rest) telling.kept.add(id);
    for (const entry of entriesOf(map)) if (free(telling, entry)) tellLine(telling, planned(telling, entry), true);
    for (const id of rest) telling.kept.delete(id);
  }
  tellLine(telling, thread, false);
  if (chosen === null) {
    for (const step of [...map.steps].sort(chart.order)) if (!telling.told.has(step.id)) tellLine(telling, planned(telling, step.id), true);
  }
  return written(telling);
}
