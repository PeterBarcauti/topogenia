// spotlight.test.ts
// What a scene lights on the map: the step shown and its halo, its exits shown, the way already run half lit, the rest faded; the passage and its flows, ended by the head of the light; the whole tour at the closing.

import { describe, expect, it } from 'vitest';
import { ARROW_MARKER, LIT_MARKER, type FlowEdge, type MapNode } from '../../canvas/flowElements.ts';
import { spotOf, spotlit } from './spotlight.ts';
import type { Tour } from './tour.ts';

const TOUR: Tour = {
  stops: [
    { stepId: 'a', next: { flows: ['f1'], to: 'b' } }, { stepId: 'b', next: { flows: ['f2', 'f3'], to: 'c' } }, { stepId: 'c', next: null },
  ],
};

const stepNode = (id: string) => ({ id, type: 'step', position: { x: 0, y: 0 }, data: { faded: false } }) as unknown as MapNode;
const laneNode = { id: 'lane:l1', type: 'lane', position: { x: 0, y: 0 }, data: {} } as unknown as MapNode;
const edge = (id: string, source: string, target: string) => ({ id, source, target, data: { faded: false } }) as unknown as FlowEdge;
const NODES = [laneNode, ...['a', 'b', 'c', 'd'].map(stepNode)];
const EDGES = [edge('f1', 'a', 'b'), edge('f2', 'b', 'c'), edge('f3', 'b', 'd'), edge('f4', 'c', 'd')];

const look = (elements: { nodes: MapNode[]; edges: FlowEdge[] }) => ({
  nodes: Object.fromEntries(elements.nodes.filter((node) => node.type === 'step').map((node) => [node.id, `${node.className ?? ''}${(node.data as { faded: boolean }).faded ? ' faded' : ''}`.trim()])),
  edges: Object.fromEntries(elements.edges.map((item) => [item.id, `${item.className ?? ''}${item.data?.faded === true ? ' faded' : ''}`.trim()])),
});

describe('the spotlight of a scene', () => {
  it('lights nothing apart on the opening: the whole map as it is', () => {
    expect(look(spotlit(NODES, EDGES, spotOf({ kind: 'opening' }, TOUR)))).toEqual({ nodes: { a: '', b: '', c: '', d: '' }, edges: { f1: '', f2: '', f3: '', f4: '' } });
  });

  it('on a step: its halo, the stops and the flows already run half lit, its exits shown (where the case goes, the numbers of a decision on them), the rest faded, the lanes left alone', () => {
    const lit = spotlit(NODES, EDGES, spotOf({ kind: 'step', stop: 1 }, TOUR, EDGES));
    expect(look(lit)).toEqual({ nodes: { a: 'is-visited', b: 'is-presented', c: 'faded', d: 'faded' }, edges: { f1: 'is-visited', f2: 'is-ahead', f3: 'is-ahead', f4: 'faded' } });
    expect(lit.nodes[0]).toBe(laneNode);
  });

  it('ends a flow lit by the head of the light, as large as the others; the other flows keep theirs (audit 3.2, BUG-03)', () => {
    const marked = EDGES.map((item) => ({ ...item, markerEnd: ARROW_MARKER }));
    const ends = Object.fromEntries(spotlit(NODES, marked, spotOf({ kind: 'passage', stop: 1 }, TOUR, marked)).edges.map((item) => [item.id, item.markerEnd]));
    expect(ends).toEqual({ f1: ARROW_MARKER, f2: LIT_MARKER, f3: LIT_MARKER, f4: ARROW_MARKER });
  });

  it('on a passage: its flows lit with both their ends, the one it leaves still shown', () => {
    expect(look(spotlit(NODES, EDGES, spotOf({ kind: 'passage', stop: 1 }, TOUR, EDGES)))).toEqual({
      nodes: { a: 'is-visited', b: 'is-presented', c: 'is-lit', d: 'is-lit' }, edges: { f1: 'is-visited', f2: 'is-lit', f3: 'is-lit', f4: 'faded' },
    });
  });

  it('on the closing: the whole tour lit, the rest faded', () => {
    expect(look(spotlit(NODES, EDGES, spotOf({ kind: 'closing' }, TOUR)))).toEqual({
      nodes: { a: 'is-lit', b: 'is-lit', c: 'is-lit', d: 'faded' }, edges: { f1: 'is-lit', f2: 'is-lit', f3: 'is-lit', f4: 'faded' },
    });
  });
});
