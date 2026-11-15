// spotlight.ts
// What a scene of a presentation lights on the map (ADR 097): the step shown with its halo and its exits shown, the passage and the ends of its flows (their heads in the accent), the way already run half lit, the rest faded; the lanes left as they are.

import { LIT_MARKER, type FlowEdge, type MapNode } from '../../canvas/flowElements.ts';
import type { Scene } from './script.ts';
import type { Tour } from './tour.ts';

/** The steps and the flows a scene lights; `dim` false shows the map as it is. */
export interface Spot {
  readonly dim: boolean;
  readonly presented: string | null;
  readonly lit: ReadonlySet<string>;
  readonly litFlows: ReadonlySet<string>;
  /** The exits of the step shown: where the case goes, the numbers of a decision on them (audit 3.2, BUG-03). */
  readonly ahead: ReadonlySet<string>;
  readonly visited: ReadonlySet<string>;
  readonly visitedFlows: ReadonlySet<string>;
}

/** The ends of the flows of a passage, from the edges of the map. */
const endsOf = (flows: readonly string[], edges: readonly FlowEdge[]): string[] =>
  edges.filter((edge) => flows.includes(edge.id)).flatMap((edge) => [edge.source, edge.target]);

/** The spot of a scene: nothing apart at the opening; the way run up to a stop; the whole tour lit at the closing. */
export function spotOf(scene: Scene | undefined, tour: Tour, edges: readonly FlowEdge[] = []): Spot {
  const none = new Set<string>();
  if (scene === undefined || scene.kind === 'opening') return { dim: false, presented: null, lit: none, litFlows: none, ahead: none, visited: none, visitedFlows: none };
  const upTo = scene.kind === 'closing' ? tour.stops.length : scene.stop;
  const visited = new Set(tour.stops.slice(0, upTo).map((stop) => stop.stepId));
  const visitedFlows = new Set(tour.stops.slice(0, scene.kind === 'closing' ? upTo : scene.stop).flatMap((stop) => stop.next?.flows ?? []));
  // The closing lights the whole way run, its steps and its flows, over the map faded: the story at one glance.
  if (scene.kind === 'closing') return { dim: true, presented: null, lit: visited, litFlows: visitedFlows, ahead: none, visited: none, visitedFlows: none };
  const stop = tour.stops[scene.stop];
  const presented = stop?.stepId ?? null;
  if (scene.kind === 'step') {
    const ahead = new Set(edges.filter((edge) => edge.source === presented).map((edge) => edge.id));
    return { dim: true, presented, lit: none, litFlows: none, ahead, visited, visitedFlows };
  }
  const flows = stop?.next?.flows ?? [];
  const lit = new Set([...endsOf(flows, edges), ...(stop?.next === null || stop?.next === undefined ? [] : [stop.next.to])]);
  return { dim: true, presented, lit, litFlows: new Set(flows), ahead: none, visited, visitedFlows };
}

/** The class a step or a flow takes under the spot, null for faded (or nothing when the map is shown as it is). */
function classOf(id: string, spot: Spot, flow: boolean): string | null {
  if (flow) return spot.litFlows.has(id) ? 'is-lit' : spot.ahead.has(id) ? 'is-ahead' : spot.visitedFlows.has(id) ? 'is-visited' : null;
  return id === spot.presented ? 'is-presented' : spot.lit.has(id) ? 'is-lit' : spot.visited.has(id) ? 'is-visited' : null;
}

/** The nodes and the edges of the canvas under the spot of a scene: their class, a flow lit ended by the head of the light, and faded when the scene lights them not. */
export function spotlit(nodes: readonly MapNode[], edges: readonly FlowEdge[], spot: Spot): { nodes: MapNode[]; edges: FlowEdge[] } {
  if (!spot.dim) return { nodes: [...nodes], edges: [...edges] };
  return {
    nodes: nodes.map((node) => {
      if (node.type !== 'step') return node;
      const name = classOf(node.id, spot, false);
      return name === null ? { ...node, data: { ...node.data, faded: true } } : { ...node, className: name };
    }),
    edges: edges.map((edge) => {
      const name = classOf(edge.id, spot, true);
      if (name === 'is-lit') return { ...edge, className: name, markerEnd: LIT_MARKER };
      return name === null ? { ...edge, ...(edge.data === undefined ? {} : { data: { ...edge.data, faded: true } }) } : { ...edge, className: name };
    }),
  };
}
