// alignment.ts
// The Align wand: the flow read from end to end along its flows, and what is out of place put back in order, each step in its lane (annex B 2.8).

import type { Schema } from '../../../api/client.ts';
import type { StepCell } from './moves.ts';
import { laneRanks, readingOrder } from './readingOrder.ts';

type StepView = Schema<'StepView'>;

/** The flows of the map between its steps, without the loops on one step, followed both ways. */
interface Graph {
  readonly byId: ReadonlyMap<string, StepView>;
  readonly reading: readonly StepView[];
  readonly order: (a: StepView, b: StepView) => number;
  readonly outgoing: ReadonlyMap<string, string[]>;
  readonly incoming: ReadonlyMap<string, string[]>;
}

function graphOf(map: Pick<Schema<'MapView'>, 'lanes' | 'steps' | 'flows'>): Graph {
  const byId = new Map(map.steps.map((step) => [step.id, step]));
  const order = readingOrder(laneRanks(map.lanes));
  const outgoing = new Map<string, string[]>(map.steps.map((step) => [step.id, []]));
  const incoming = new Map<string, string[]>(map.steps.map((step) => [step.id, []]));
  for (const flow of map.flows) {
    if (!byId.has(flow.source_step_id) || !byId.has(flow.target_step_id) || flow.source_step_id === flow.target_step_id) continue;
    outgoing.get(flow.source_step_id)?.push(flow.target_step_id);
    incoming.get(flow.target_step_id)?.push(flow.source_step_id);
  }
  const step = (id: string): StepView => byId.get(id) as StepView;
  for (const list of outgoing.values()) list.sort((a, b) => order(step(a), step(b)));
  return { byId, reading: [...map.steps].sort(order), order, outgoing, incoming };
}

/**
 * The loops back: the flows that close a cycle in a depth-first walk led in the reading order from the steps without a
 * predecessor (B-2.8-01), then from those not reached yet.
 */
function loopsBack(graph: Graph): Set<string> {
  const loops = new Set<string>();
  const state = new Map<string, 'open' | 'done'>();
  const walk = (start: string) => {
    const stack = [{ id: start, next: 0 }];
    state.set(start, 'open');
    while (stack.length > 0) {
      const top = stack[stack.length - 1] as { id: string; next: number };
      const targets = graph.outgoing.get(top.id) ?? [];
      const target = targets[top.next++];
      if (target === undefined) {
        state.set(top.id, 'done');
        stack.pop();
      } else if (state.get(target) === 'open') loops.add(`${top.id}>${target}`);
      else if (!state.has(target)) {
        state.set(target, 'open');
        stack.push({ id: target, next: 0 });
      }
    }
  };
  for (const step of graph.reading) if ((graph.incoming.get(step.id) ?? []).length === 0 && !state.has(step.id)) walk(step.id);
  for (const step of graph.reading) if (!state.has(step.id)) walk(step.id);
  return loops;
}

/** The flows without the loops back (B-2.8-02). */
interface Flow {
  readonly before: (id: string) => string[];
  readonly after: (id: string) => string[];
}

const flowOf = (graph: Graph, loops: ReadonlySet<string>): Flow => ({
  before: (id) => (graph.incoming.get(id) ?? []).filter((source) => !loops.has(`${source}>${id}`)),
  after: (id) => (graph.outgoing.get(id) ?? []).filter((target) => !loops.has(`${id}>${target}`)),
});

/** The steps in a topological order of the flow, the reading order breaking the ties (B-2.8-03). */
function topologicalOrder(graph: Graph, flow: Flow): StepView[] {
  const waiting = new Map(graph.reading.map((step) => [step.id, flow.before(step.id).length]));
  const ready = graph.reading.filter((step) => waiting.get(step.id) === 0);
  const ordered: StepView[] = [];
  while (ready.length > 0) {
    ready.sort(graph.order);
    const step = ready.shift() as StepView;
    ordered.push(step);
    for (const next of flow.after(step.id)) {
      const left = (waiting.get(next) ?? 0) - 1;
      waiting.set(next, left);
      if (left === 0) ready.push(graph.byId.get(next) as StepView);
    }
  }
  return ordered;
}

/** 0 for a vertical flow wanted (same column, another lane, B-2.8-05), else 1. */
const gapOf = (from: StepView, to: StepView): number => (from.column === to.column && from.lane_id !== to.lane_id ? 0 : 1);

/**
 * The columns: one after the furthest predecessor (B-2.8-04, B-2.8-05); a secondary start just before its first successor
 * (B-2.8-06); each piece of map not linked to the rest keeps its first column (B-2.8-07).
 */
function columnsOf(graph: Graph, flow: Flow, ordered: readonly StepView[]): Map<string, number> {
  const column = new Map<string, number>();
  const at = (id: string): number => column.get(id) ?? 0;
  for (const step of ordered) {
    const before = flow.before(step.id).map((id) => graph.byId.get(id) as StepView);
    column.set(step.id, before.length === 0 ? 0 : Math.max(...before.map((previous) => at(previous.id) + gapOf(previous, step))));
  }
  for (const step of [...ordered].reverse()) {
    const next = flow.after(step.id).map((id) => graph.byId.get(id) as StepView);
    if (flow.before(step.id).length > 0 || next.length === 0) continue;
    column.set(step.id, Math.max(at(step.id), Math.min(...next.map((successor) => at(successor.id) - gapOf(step, successor)))));
  }
  for (const piece of piecesOf(graph)) {
    const shift = Math.min(...piece.map((step) => step.column)) - Math.min(...piece.map((step) => at(step.id)));
    for (const step of piece) column.set(step.id, at(step.id) + shift);
  }
  return column;
}

/** The pieces of the map linked by flows, loops back included, in the reading order. */
function piecesOf(graph: Graph): StepView[][] {
  const seen = new Set<string>();
  const pieces: StepView[][] = [];
  for (const start of graph.reading) {
    if (seen.has(start.id)) continue;
    const piece: StepView[] = [];
    const stack = [start.id];
    seen.add(start.id);
    while (stack.length > 0) {
      const id = stack.pop() as string;
      piece.push(graph.byId.get(id) as StepView);
      for (const next of [...(graph.outgoing.get(id) ?? []), ...(graph.incoming.get(id) ?? [])]) {
        if (seen.has(next)) continue;
        seen.add(next);
        stack.push(next);
      }
    }
    pieces.push(piece);
  }
  return pieces;
}

/**
 * The rows: the next step of a lane stays on the row of its predecessor in that lane (B-2.8-08), else its row, else the
 * first row free in its cell (B-2.8-09); the linked steps are placed before the lone ones, which never take the cell of the
 * flow (B-2.8-10, B-2.8-11); never two steps in one cell (B-2.8-13).
 */
function rowsOf(graph: Graph, flow: Flow, ordered: readonly StepView[], column: ReadonlyMap<string, number>): Map<string, number> {
  const row = new Map<string, number>();
  const taken = new Set<string>();
  const key = (laneId: string, at: number, line: number) => `${laneId}:${String(at)}:${String(line)}`;
  const linked = (step: StepView) => flow.before(step.id).length + flow.after(step.id).length > 0;
  for (const step of [...ordered.filter(linked), ...ordered.filter((candidate) => !linked(candidate))]) {
    const at = column.get(step.id) ?? 0;
    const sameLane = flow.before(step.id).map((id) => graph.byId.get(id) as StepView).filter((previous) => previous.lane_id === step.lane_id);
    const candidates = [...sameLane.map((previous) => row.get(previous.id) ?? 0).sort((a, b) => a - b), step.row];
    let chosen = candidates.find((candidate) => !taken.has(key(step.lane_id, at, candidate)));
    if (chosen === undefined) for (chosen = 0; taken.has(key(step.lane_id, at, chosen)); chosen++);
    taken.add(key(step.lane_id, at, chosen));
    row.set(step.id, chosen);
  }
  return row;
}

/** The empty rows of each lane taken out, the others numbered again in their order (B-2.8-12). */
function packRows(steps: readonly StepView[], row: ReadonlyMap<string, number>): Map<string, number> {
  const used = new Map<string, Set<number>>();
  for (const step of steps) used.set(step.lane_id, (used.get(step.lane_id) ?? new Set<number>()).add(row.get(step.id) ?? 0));
  const renumber = new Map([...used].map(([laneId, rows]) => [laneId, new Map([...rows].sort((a, b) => a - b).map((value, index) => [value, index]))]));
  return new Map(steps.map((step) => [step.id, renumber.get(step.lane_id)?.get(row.get(step.id) ?? 0) ?? 0]));
}

/**
 * The cells that align the map, only for the steps that move: none when the map is already aligned (B-2.8-17). A step
 * never changes lane (B-2.8-14).
 */
export function alignMap(map: Pick<Schema<'MapView'>, 'lanes' | 'steps' | 'flows'>): StepCell[] {
  if (map.steps.length === 0) return [];
  const graph = graphOf(map);
  const flow = flowOf(graph, loopsBack(graph));
  const ordered = topologicalOrder(graph, flow);
  const column = columnsOf(graph, flow, ordered);
  const row = packRows(map.steps, rowsOf(graph, flow, ordered, column));
  return map.steps
    .map((step) => ({ id: step.id, laneId: step.lane_id, column: column.get(step.id) ?? step.column, row: row.get(step.id) ?? step.row }))
    .filter((cell) => {
      const step = graph.byId.get(cell.id) as StepView;
      return cell.column !== step.column || cell.row !== step.row;
    });
}
