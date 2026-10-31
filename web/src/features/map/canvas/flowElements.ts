// flowElements.ts
// The map laid out, as React Flow draws it: lane backgrounds, the frame of a dissolution, steps with their four handles and their states, flows on their routed paths; with the value stream layer, the cartouches under the steps and the block under the lanes (annex B 2.9, 3.1 to 3.4, spec 14.5).

import { Position, type Edge, type Node, type NodeHandle } from '@xyflow/react';
import type { Schema } from '../../../api/client.ts';
import { LANE_HEADER_WIDTH, STEP_HEIGHT, STEP_WIDTH, STREAM_CARD } from '../model/geometry/constants.ts';
import type { Rect } from '../model/geometry/grid.ts';
import type { FlowKind, Side } from '../model/geometry/routing/model.ts';
import type { FrameFigure, LaneBand, MapLayout, StepFigure } from '../model/mapLayout.ts';
import type { FlowShareText, StreamFigure } from '../model/valueStream.ts';
import type { Selection, StepMark } from '../state/mapStore.ts';

/** The quick-add buttons of a step: before and after always, below and above when a lane is there (B-2.3-42, B-2.3-43). */
export interface QuickAdd {
  readonly above: boolean;
  readonly below: boolean;
}

/** What a step node shows: its figure, the display of the codes (B-2.10-01) and the states of the editor (annex B 3.2). */
export interface StepNodeData extends Record<string, unknown> {
  readonly figure: StepFigure;
  readonly showCodes: boolean;
  /** Cut and waiting for its paste: greyed and dashed (B-2.7-09, B-3.2-16). */
  readonly cut: boolean;
  /** The source chosen in the link mode (B-3.2-15). */
  readonly linkSource: boolean;
  /** Its name is being typed (B-2.3-11). */
  readonly editing: boolean;
  /** Its handles draw a flow: only for who may change the map (B-2.3-19). */
  readonly connectable: boolean;
  /** Its quick-add buttons, null when they are hidden (B-2.3-46 to B-2.3-48). */
  readonly quickAdd: QuickAdd | null;
  /** The RACI layer is on: its badges are shown (B-2.11-04, B-2.11-05). */
  readonly showRaci: boolean;
  /** The value stream layer is on: the cartouche under the step stands for the frequency of a detailed step (spec 14.5). */
  readonly showStream: boolean;
  /** What a preview of a proposal of the assistant does to the step, null without one (spec 3.0 section 3.1-5). */
  readonly mark: StepMark | null;
}

/** A lane background: its band and its width, the map and the margin of the headers (B-3.1-20). */
export interface LaneNodeData extends Record<string, unknown> {
  readonly band: LaneBand;
}

/** The frame of a dissolution: the frame open on the map and the size of its remainder (B-2.9-21, B-2.9-22). */
export interface FrameNodeData extends Record<string, unknown> {
  readonly frame: Schema<'FrameView'>;
  readonly remainder: number;
}

/** A flow drawn on the path of the routing, with its label box, its text and the documents it carries. */
export interface FlowEdgeData extends Record<string, unknown> {
  readonly path: string;
  readonly label: Rect | null;
  readonly text: string;
  /** Its label not written in the language shown, shown to translate (ADR 083). */
  readonly untranslated: boolean;
  readonly documents: readonly string[];
  /** The share of the cases of an exit of a gateway under the value stream layer, after the text (spec 14.5). */
  readonly share: FlowShareText | null;
}

/** What the value stream layer hangs under a step, drawn under the flows (spec 14.5). */
export interface CardNodeData extends Record<string, unknown> {
  readonly figure: StreamFigure;
  readonly code: string;
}

export type StepNode = Node<StepNodeData, 'step'>;
export type CardNode = Node<CardNodeData, 'card'>;
export type LaneNode = Node<LaneNodeData, 'lane'>;
export type FrameNode = Node<FrameNodeData, 'frame'>;
export type MapNode = StepNode | LaneNode | FrameNode | CardNode;
export type FlowEdge = Edge<FlowEdgeData, FlowKind>;

/** The state of the editor the elements show. */
export interface ElementStates {
  readonly showCodes: boolean;
  readonly canEdit: boolean;
  readonly selection: Selection;
  readonly cut: ReadonlySet<string>;
  readonly linkSource: string | null;
  readonly editing: string | null;
  /** The quick-add buttons are offered: a map that may be changed, out of the link mode and of the RACI layer (B-2.3-46 to B-2.3-48). */
  readonly quickAdd: boolean;
  readonly showRaci: boolean;
  readonly showStream: boolean;
  /** An image of the map is being captured: the steps outside the frame are drawn as the others (B-2.14-11). */
  readonly exporting: boolean;
  /** The steps a preview of a proposal marks. */
  readonly marks: Readonly<Record<string, StepMark>>;
}

/** The display of a map that cannot be changed, nothing chosen: the elements as the lot 1 drew them. */
export const VIEW_ONLY: ElementStates = {
  showCodes: true, canEdit: false, selection: { steps: [], flows: [] }, cut: new Set(), linkSource: null, editing: null, quickAdd: false, showRaci: false,
  showStream: false, exporting: false, marks: {},
};

/** The one frame of a map (A-2.9: one open frame a map). */
export const FRAME_NODE = 'dissolution-frame';

/** The identifier of the marker that ends every flow (B-3.4-31), drawn once by the canvas. */
export const ARROW_MARKER = 'map-arrow';

/** The side of React Flow of each side of a step. */
export const HANDLE_POSITIONS: Readonly<Record<Side, Position>> = { left: Position.Left, right: Position.Right, top: Position.Top, bottom: Position.Bottom };

/** The middle of each side of a step, as the routing starts and ends its flows there. */
const HANDLE_POINTS: Readonly<Record<Side, { x: number; y: number }>> = {
  left: { x: 0, y: STEP_HEIGHT / 2 },
  right: { x: STEP_WIDTH, y: STEP_HEIGHT / 2 },
  top: { x: STEP_WIDTH / 2, y: 0 },
  bottom: { x: STEP_WIDTH / 2, y: STEP_HEIGHT },
};

export const SIDES: readonly Side[] = ['left', 'right', 'top', 'bottom'];

/**
 * The four handles of a step, all sources (B-3.3-01): the connection mode « loose » draws from any of them. They are
 * declared with the node, so that React Flow places the flows before it has measured the steps on the page.
 */
export const stepHandles = (): NodeHandle[] =>
  SIDES.map((side) => ({ id: side, type: 'source', position: HANDLE_POSITIONS[side], x: HANDLE_POINTS[side].x, y: HANDLE_POINTS[side].y, width: 1, height: 1 }));

/**
 * The background of a lane: from the margin of the headers to the end of the map, behind everything; the pointer goes
 * through it, so that a rectangle of selection or a double click starts anywhere on the background (B-2.3-08, B-3.1-20).
 */
function laneNode(band: LaneBand, mapWidth: number): LaneNode {
  return {
    id: `lane:${band.lane.id}`, type: 'lane', position: { x: -LANE_HEADER_WIDTH, y: band.top }, width: mapWidth + LANE_HEADER_WIDTH, height: band.height,
    zIndex: -1, draggable: false, selectable: false, connectable: false, focusable: false, style: { pointerEvents: 'none' }, data: { band },
  };
}

/**
 * The frame of a dissolution: over the lanes, under the steps; the pointer goes through it but for its label, so that a
 * rectangle of selection or a double click starts anywhere inside it (B-2.3-08, B-2.9-17).
 */
function frameNode(figure: FrameFigure): FrameNode {
  const { box } = figure;
  return {
    id: FRAME_NODE, type: 'frame', position: { x: box.x, y: box.y }, width: box.width, height: box.height, zIndex: -1, draggable: false, selectable: false,
    connectable: false, focusable: false, style: { pointerEvents: 'none' }, data: { frame: figure.frame, remainder: figure.remainder },
  };
}

function stepNode(figure: StepFigure, states: ElementStates, ranks: { rank: number; count: number }): StepNode {
  const id = figure.step.id;
  return {
    id, type: 'step', position: { x: figure.box.x, y: figure.box.y }, width: STEP_WIDTH, height: STEP_HEIGHT, handles: stepHandles(),
    selected: states.selection.steps.includes(id), draggable: states.canEdit, connectable: states.canEdit,
    data: {
      figure: states.exporting && figure.outside ? { ...figure, outside: false } : figure, showCodes: states.showCodes, cut: states.cut.has(id), linkSource: states.linkSource === id, editing: states.editing === id, connectable: states.canEdit,
      quickAdd: states.quickAdd ? { above: ranks.rank > 0, below: ranks.rank < ranks.count - 1 } : null, showRaci: states.showRaci,
      showStream: states.showStream, mark: states.marks[id] ?? null,
    },
  };
}

/**
 * What the value stream layer hangs under a step: under the flows as the lanes, so that a flow that passes stays seen;
 * centred on the axis of the step, under the label of a gateway (spec 14.5). Its own cases take the pointer.
 */
function cardNode(figure: StepFigure, stream: StreamFigure): CardNode {
  const height = stream.kind === 'card' ? STREAM_CARD.height : STREAM_LINE_HEIGHT;
  return {
    id: `card:${figure.step.id}`, type: 'card', position: { x: figure.box.x + (STEP_WIDTH - STREAM_CARD.width) / 2, y: figure.box.y + STEP_HEIGHT + figure.streamTop },
    width: STREAM_CARD.width, height, zIndex: -1, draggable: false, selectable: false, connectable: false, focusable: false, data: { figure: stream, code: figure.step.code },
  };
}

/** The line of the entry of a start or the exit of an end under the value stream layer. */
export const STREAM_LINE_HEIGHT = 18;

/**
 * The nodes and edges of the canvas: the lanes first, behind, then the frame, then what the value stream layer hangs
 * under the steps, then the steps, then the flows of the routing as they are.
 */
export function flowElements(layout: MapLayout, states: ElementStates = VIEW_ONLY): { nodes: MapNode[]; edges: FlowEdge[] } {
  const rank = new Map(layout.lanes.map((band, index) => [band.lane.id, index]));
  const nodes: MapNode[] = [
    ...layout.lanes.map((band) => laneNode(band, layout.width)),
    ...(layout.frame === null ? [] : [frameNode(layout.frame)]),
    ...layout.steps.flatMap((figure) => (figure.stream === null ? [] : [cardNode(figure, figure.stream)])),
    ...layout.steps.map((figure) => stepNode(figure, states, { rank: rank.get(figure.step.lane_id) ?? 0, count: layout.lanes.length })),
  ];
  const edges = layout.flows.map((flow): FlowEdge => ({
    id: flow.route.id, source: flow.route.sourceId, target: flow.route.targetId, sourceHandle: flow.route.sourceSide, targetHandle: flow.route.targetSide,
    type: flow.route.kind, markerEnd: ARROW_MARKER, selected: states.selection.flows.includes(flow.route.id),
    data: { path: flow.route.path, label: flow.route.label, text: flow.text, untranslated: flow.untranslated, documents: flow.documents, share: flow.share },
  }));
  return { nodes, edges };
}
