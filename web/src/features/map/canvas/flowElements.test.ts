// flowElements.test.ts
// The nodes and edges React Flow draws: three kinds of nodes, lane backgrounds, the frame of a dissolution, steps with four source handles, routed flows (annex B 2.9, 3).

import { Position } from '@xyflow/react';
import { flowOf, frameOf, laneOf, mapOf, sampleMap, stepOf } from '../../../test/maps.ts';
import { layoutMap } from '../model/mapLayout.ts';
import type { TextMeasure } from '../model/textSize.ts';
import { ARROW_MARKER, flowElements, FRAME_NODE, stepHandles, VIEW_ONLY, type ElementStates, type FrameNode, type LaneNode, type StepNode } from './flowElements.ts';

const six: TextMeasure = (text) => text.length * 6;
const layout = layoutMap(sampleMap(), 'fr', six);
const { nodes, edges } = flowElements(layout, { ...VIEW_ONLY, showCodes: true });
const lanes = nodes.filter((node): node is LaneNode => node.type === 'lane');
const steps = nodes.filter((node): node is StepNode => node.type === 'step');
const stepsOf = (states: ElementStates) => flowElements(layout, states).nodes.filter((node): node is StepNode => node.type === 'step');

describe('the nodes (B-3.2-01 to B-3.2-03)', () => {
  it('are lane backgrounds and steps; start, end and gateways are types of step, not nodes of their own', () => {
    expect(nodes.map((node) => node.type)).toEqual(['lane', 'lane', 'step', 'step', 'step', 'step']);
    expect(steps.map((node) => node.data.figure.step.type)).toEqual(['start', 'task', 'decision', 'end']);
  });

  it('a lane background starts at x = −184 under the headers, runs to the end of the map and stays behind (B-3.1-20)', () => {
    expect(lanes.map((node) => [node.position.x, node.position.y, node.width, node.height, node.zIndex])).toEqual([
      [-184, 0, layout.width + 184, 120, -1],
      [-184, 120, layout.width + 184, 120, -1],
    ]);
    expect(lanes.every((node) => node.selectable === false && node.draggable === false && node.connectable === false && node.focusable === false)).toBe(true);
  });

  it('the frame of a dissolution: after the lanes, behind the steps, at its box, the pointer going through it, never chosen nor moved (B-2.3-08)', () => {
    const framed = flowElements(layoutMap(sampleMap({ dissolution: frameOf(['s2']) }), 'fr', six), VIEW_ONLY).nodes;
    expect(framed.map((node) => node.type)).toEqual(['lane', 'lane', 'frame', 'step', 'step', 'step', 'step']);
    const frame = framed.find((node): node is FrameNode => node.type === 'frame');
    expect(frame).toMatchObject({ id: FRAME_NODE, position: { x: 236, y: 2 }, width: 198, height: 104, zIndex: -1, selectable: false, draggable: false, connectable: false, focusable: false });
    expect([frame?.style?.pointerEvents, frame?.data.remainder, frame?.data.frame.step_code]).toEqual(['none', 0, 'VTE-E009']);
  });

  it('a step stands at its box, 170 × 64, with the codes preference', () => {
    expect(steps.map((node) => [node.id, node.position.x, node.position.y, node.width, node.height, node.data.showCodes])).toEqual([
      ['s1', 30, 28, 170, 64, true], ['s2', 250, 28, 170, 64, true], ['s3', 470, 28, 170, 64, true], ['s4', 690, 148, 170, 64, true],
    ]);
    expect(flowElements(layout, { ...VIEW_ONLY, showCodes: false }).nodes.filter((node): node is StepNode => node.type === 'step').map((node) => node.data.showCodes)).toEqual([false, false, false, false]);
  });

  it('a step shows its RACI badges only with the layer on (B-2.11-04)', () => {
    const shown = (showRaci: boolean) => flowElements(layout, { ...VIEW_ONLY, showRaci }).nodes.filter((node): node is StepNode => node.type === 'step').map((node) => node.data.showRaci);
    expect([shown(false), shown(true)]).toEqual([[false, false, false, false], [true, true, true, true]]);
  });

  it('only the step whose name is typed is in edition; on a map that may be changed, every step may be linked (B-2.3-11, B-2.3-19)', () => {
    const edited = stepsOf({ ...VIEW_ONLY, canEdit: true, editing: 's2' });
    expect(edited.map((node) => [node.id, node.data.editing, node.data.connectable])).toEqual([['s1', false, true], ['s2', true, true], ['s3', false, true], ['s4', false, true]]);
  });

  it('read only, no step can be moved nor linked; with the right to change the map, every one can', () => {
    const flags = (canEdit: boolean) => stepsOf({ ...VIEW_ONLY, canEdit }).map((node) => [node.draggable, node.connectable, node.data.connectable]);
    expect(flags(false)).toEqual([[false, false, false], [false, false, false], [false, false, false], [false, false, false]]);
    expect(flags(true)).toEqual([[true, true, true], [true, true, true], [true, true, true], [true, true, true]]);
  });
});

describe('two rows in one lane (B-3.1-13, B-3.1-14, B-3.1-16)', () => {
  it('a vertical between the rows of a column, the lane two rows high, the step of the row 1 at its top + 28', () => {
    const map = mapOf({
      lanes: [laneOf('l1', 0), laneOf('l2', 1)],
      steps: [stepOf('s1', 'l1', 0), stepOf('s2', 'l1', 1), stepOf('s3', 'l1', 1, { row: 1 }), stepOf('s4', 'l2', 2)],
      flows: [flowOf('f1', 's1', 's2'), flowOf('f2', 's1', 's3'), flowOf('f3', 's2', 's3')],
    });
    const rows = flowElements(layoutMap(map, 'fr', six), VIEW_ONLY);
    expect(rows.nodes.filter((node) => node.type === 'lane').map((node) => [node.id, node.position.y, node.height])).toEqual([['lane:l1', 0, 240], ['lane:l2', 240, 120]]);
    expect(rows.nodes.find((node) => node.id === 's3')?.position).toEqual({ x: 250, y: 148 });
    expect(rows.edges.find((edge) => edge.id === 'f3')).toMatchObject({ sourceHandle: 'bottom', targetHandle: 'top', type: 'flow' });
    expect(rows.edges.find((edge) => edge.id === 'f2')).toMatchObject({ sourceHandle: 'right', targetHandle: 'left', type: 'flow' });
  });
});

describe('the handles (B-3.3-01)', () => {
  it('four handles, left, right, top, bottom, all sources, at the middle of each side', () => {
    expect(stepHandles().map((handle) => [handle.id, handle.type, handle.position, handle.x, handle.y])).toEqual([
      ['left', 'source', Position.Left, 0, 32], ['right', 'source', Position.Right, 170, 32], ['top', 'source', Position.Top, 85, 0], ['bottom', 'source', Position.Bottom, 85, 64],
    ]);
    expect(steps.every((node) => node.handles?.length === 4)).toBe(true);
  });

  it('each step has handles of its own (React Flow completes them in place)', () => {
    expect(steps[0]?.handles?.[0]).not.toBe(steps[1]?.handles?.[0]);
  });
});

describe('the edges (B-3.4-31, B-3.4-33)', () => {
  it('follow the routing: sides as handles, kind as type, path and label box as they are, the arrow of the canvas at the end', () => {
    for (const flow of layout.flows) {
      const edge = edges.find((candidate) => candidate.id === flow.route.id);
      expect(edge).toMatchObject({
        source: flow.route.sourceId, target: flow.route.targetId, sourceHandle: flow.route.sourceSide, targetHandle: flow.route.targetSide,
        type: flow.route.kind, markerEnd: ARROW_MARKER, data: { path: flow.route.path, label: flow.route.label, text: flow.text, documents: flow.documents },
      });
    }
    expect(new Set(edges.map((edge) => edge.type))).toEqual(new Set(['flow', 'loop']));
  });
});
