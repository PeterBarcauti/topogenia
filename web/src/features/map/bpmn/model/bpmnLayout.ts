// bpmnLayout.ts
// The drawing of a BPMN diagram (BPMNDI), the one of the map (ADR 106): the grid and the routing of the map give the cells and the paths, each form centred in the cell of its step, the ends of each flow brought onto the edge of its form; the band of the details under each row, where the documents and the notes hang.

import type { PmapMap, PmapStep } from '../../../../shared/pmap.ts';
import { COLUMN_WIDTH, FREE_BAND, FREE_COLUMNS, STEP_HEIGHT, STEP_WIDTH, streamCardTop, UNDER_STEP } from '../../model/geometry/constants.ts';
import { gridOf, mapWidth, rowTop, stepBox, type Grid, type Point, type Rect } from '../../model/geometry/grid.ts';
import type { RoutedFlow, RoutingStep, Side, StepKind } from '../../model/geometry/routing/model.ts';
import { layoutFlows } from '../../model/geometry/routing/routing.ts';
import type { UnderSide } from '../../model/geometry/routing/under.ts';
import { edgeLabelSize, estimateMeasure, gatewayLabelSize } from '../../model/textSize.ts';
import { compacted, cutsOf, takenSpans, type CutRow } from './bpmnCompact.ts';
import type { MappedProcess } from './bpmnMapping.ts';
import type { BpmnDiagram, BpmnEdge, BpmnNode, BpmnNodeKind, BpmnShape } from './bpmnModel.ts';
import { placedColumns } from './bpmnPlacement.ts';

/** The band of the title of a pool, and of the name of a lane, on their left, as the modellers draw them. */
export const POOL_BAND = 30;
export const LANE_BAND = 30;

/**
 * The band under each row when the details are drawn: the documents and the note of a step under its form and the label
 * of a gateway (36 px at most); then cut in each row where nothing is drawn across the whole width (bpmnCompact.ts).
 */
export const DETAIL_BAND = 80;

/** The sizes of the forms of BPMN: a gateway, an event, a document, a store; a task takes the box of its step. */
export const GATEWAY_SIZE = 50;
export const EVENT_SIZE = 36;
export const DATA_SIZE = { width: 30, height: 38 } as const;
export const STORE_SIZE = { width: 36, height: 36 } as const;

/** The space between two documents under a step, and their pitch: a document, the space and 4 px. */
const DATA_GAP = 10;
export const DATA_SLOT = DATA_SIZE.width + DATA_GAP + 4;

/**
 * The name of a document or a store written under its form (BpmnShapes.tsx, R8): centred on it, as wide as the pitch less
 * the difference of the two forms, so that two names side by side never meet (3 px between them at least) nor the note
 * after them; two lines of 12 px at most, 2 px under the form: 26 px under it, which keeps the drawings with the details a
 * third higher at most than without (bpmnProperties.test.ts). The height it takes under the form.
 */
export const DATA_NAME = { width: DATA_SLOT - (STORE_SIZE.width - DATA_SIZE.width), lines: 2, top: 2, line: 12 } as const;
const DATA_NAME_HEIGHT = DATA_NAME.top + DATA_NAME.lines * DATA_NAME.line;

/**
 * The room of the details under a step, within the cartouche of the value stream which the routing keeps free: centred,
 * or right of the axis of the step when a flow uses its bottom point; 120 px, so that two rooms never meet, the room of
 * the last column stays in its lane, and one of the first column moved aside too (300 maps drawn at random).
 */
const DETAIL_ROOM = { width: 120, height: 44 } as const;

/** The size of the form of a node: its step box for an activity, a square for a gateway, a circle for an event. */
export function formSize(kind: BpmnNodeKind): { width: number; height: number } {
  if (kind.endsWith('Gateway')) return { width: GATEWAY_SIZE, height: GATEWAY_SIZE };
  if (kind.endsWith('Event')) return { width: EVENT_SIZE, height: EVENT_SIZE };
  return { width: STEP_WIDTH, height: STEP_HEIGHT };
}

/** The form of a node, centred in the box of its step. */
export const formIn = (box: Rect, kind: BpmnNodeKind): Rect => {
  const size = formSize(kind);
  return { x: box.x + (box.width - size.width) / 2, y: box.y + (box.height - size.height) / 2, width: size.width, height: size.height };
};

const moved = (rect: Rect, dx: number): Rect => ({ ...rect, x: rect.x + dx });

/** An end of a flow brought from the side of the box of its step onto the edge of its form, along the side it leaves by. */
function onForm(point: Point, side: Side, form: Rect): Point {
  if (side === 'left') return { x: form.x, y: point.y };
  if (side === 'right') return { x: form.x + form.width, y: point.y };
  if (side === 'top') return { x: point.x, y: form.y };
  return { x: point.x, y: form.y + form.height };
}

/** The points of a routed flow: its orthogonal line, or its loop under the line made a broken line (BPMNDI draws no curve). */
function pointsOf(flow: RoutedFlow): Point[] {
  if (flow.curve === null) return [...flow.points];
  const { start, end, low } = flow.curve;
  return [start, { x: start.x, y: low }, { x: end.x, y: low }, end];
}

/** The kind of a step for the routing, its label under it measured for a gateway. */
function routingStepOf(step: PmapStep, column: number, name: string): RoutingStep {
  const type: StepKind = step.type ?? 'task';
  const gateway = type === 'decision' || type === 'parallel' || type === 'inclusive';
  const below = gateway ? gatewayLabelSize(name, estimateMeasure) : undefined;
  return { id: step.id, laneId: step.lane, column, row: step.row ?? 0, type, ...(below === undefined ? {} : { below }) };
}

/** What is placed for one map: its grid, its routed flows, the side of what hangs under each step, its steps placed. */
interface Placed {
  readonly grid: Grid;
  readonly steps: readonly RoutingStep[];
  readonly flows: readonly RoutedFlow[];
  readonly under: ReadonlyMap<string, UnderSide>;
}

function place(mapped: MappedProcess, withDetails: boolean): Placed {
  const map: PmapMap = mapped.map;
  const steps = map.steps ?? [];
  const columns = placedColumns(map);
  const nodes = new Map(mapped.process.nodes.map((node) => [node.stepId, node]));
  const routing = steps.map((step) => routingStepOf(step, columns.get(step.id) ?? 0, nodes.get(step.id)?.name ?? step.label));
  const lanes = map.lanes.map((lane, order) => ({ id: lane.id, order }));
  const flows = (map.flows ?? []).map((flow, index) => {
    const name = mapped.process.flows[index]?.name ?? null;
    const route = flow.route === undefined ? undefined : {
      ...(flow.route.from === undefined ? {} : { source: flow.route.from }), ...(flow.route.to === undefined ? {} : { target: flow.route.to }),
      ...(flow.route.via === undefined ? {} : { via: 'lane' in flow.route.via ? { laneId: flow.route.via.lane, row: flow.route.via.row, ...(flow.route.via.edge === undefined ? {} : { edge: flow.route.via.edge }) } : flow.route.via }),
    };
    return { id: String(index), sourceId: flow.source, targetId: flow.target, label: name === null ? null : edgeLabelSize(name, 0, estimateMeasure), ...(route === undefined ? {} : { route }) };
  });
  const below = withDetails ? DETAIL_BAND : 0;
  const { flows: routed, under } = layoutFlows({ lanes, steps: routing, flows, below });
  return { grid: gridOf(lanes, routing, below), steps: routing, flows: routed, under };
}

/** The shape of a node, and the label written under a gateway or an event. */
function nodeShape(node: BpmnNode, box: Rect, step: RoutingStep, ids: (key: string) => string): BpmnShape {
  const form = formIn(box, node.kind);
  const gateway = node.kind.endsWith('Gateway');
  const event = node.kind.endsWith('Event');
  const size = gateway ? step.below : event ? gatewayLabelSize(node.name, estimateMeasure) : undefined;
  const label = size === undefined ? undefined : { x: box.x + (box.width - size.width) / 2, y: form.y + form.height + UNDER_STEP.gatewayLabel, width: size.width, height: size.height };
  return {
    id: ids(node.id), elementId: node.id, bounds: form,
    ...(node.kind === 'subProcess' ? { expanded: false } : {}), ...(node.kind === 'exclusiveGateway' ? { markerVisible: true } : {}),
    ...(label === undefined ? {} : { label }),
  };
}

/**
 * The x of the room of the details under a step: centred, or right of the axis when a flow leaves or reaches its bottom
 * point, whose vertical runs on the axis; always right, so that the rooms of two steps side by side never face each other.
 */
function roomX(box: Rect, side: UnderSide): number {
  const axis = box.x + STEP_WIDTH / 2;
  return side === 'centre' ? axis - DETAIL_ROOM.width / 2 : axis + UNDER_STEP.shift;
}

/** The documents and the note of one node, in the room under it: the note on the right, as many documents as fit on its left. */
function detailsOf(mapped: MappedProcess, node: BpmnNode, box: Rect, step: RoutingStep, side: UnderSide, ids: (key: string) => string): { shapes: BpmnShape[]; edges: BpmnEdge[] } {
  const top = box.y + STEP_HEIGHT + streamCardTop(step.below === undefined ? null : step.below.height);
  const left = roomX(box, side);
  const note = mapped.process.annotations.find((annotation) => annotation.nodeId === node.id);
  const references = mapped.process.references.filter((reference) => reference.nodeId === node.id);
  const slot = DATA_SLOT;
  const fit = Math.max(0, Math.floor((DETAIL_ROOM.width - (note === undefined ? 0 : 70)) / slot));
  const shapes: BpmnShape[] = [];
  const edges: BpmnEdge[] = [];
  const form = formIn(box, node.kind);
  const bottom = { x: form.x + form.width / 2, y: form.y + form.height };
  references.slice(0, fit).forEach((reference, index) => {
    const size = reference.store ? STORE_SIZE : DATA_SIZE;
    const bounds = { x: left + index * slot, y: top, width: size.width, height: size.height };
    shapes.push({ id: ids(reference.id), elementId: reference.id, bounds });
    const link = [...node.inputs, ...node.outputs].find((candidate) => candidate.referenceId === reference.id);
    const at = { x: bounds.x + bounds.width / 2, y: bounds.y };
    if (link !== undefined)
      edges.push({ id: ids(link.id), elementId: link.id, waypoints: node.inputs.includes(link) ? [at, { x: at.x, y: bottom.y }] : [{ x: at.x, y: bottom.y }, at] });
  });
  if (note !== undefined) {
    const x = left + Math.min(references.length, fit) * slot;
    const bounds = { x, y: top, width: left + DETAIL_ROOM.width - x, height: DETAIL_ROOM.height };
    shapes.push({ id: ids(note.id), elementId: note.id, bounds });
    edges.push({ id: ids(note.associationId), elementId: note.associationId, waypoints: [{ x: bounds.x + bounds.width / 2, y: bounds.y }, { x: bounds.x + bounds.width / 2, y: bottom.y }] });
  }
  return { shapes, edges };
}

/** The edge of a sequence flow: the path of the map, its ends on the edges of the forms, its label where the map writes it. */
function flowEdge(mapped: MappedProcess, flow: RoutedFlow, boxes: ReadonlyMap<string, Rect>, kinds: ReadonlyMap<string, BpmnNodeKind>, offset: number, ids: (key: string) => string): BpmnEdge | null {
  const element = mapped.process.flows[Number(flow.id)];
  const [source, target] = [boxes.get(flow.sourceId), boxes.get(flow.targetId)];
  if (element === undefined || source === undefined || target === undefined) return null;
  const points = pointsOf(flow).map((point) => ({ x: point.x + offset, y: point.y }));
  const first = points[0];
  const last = points.at(-1);
  if (first === undefined || last === undefined || points.length < 2) return null;
  points[0] = onForm(first, flow.sourceSide, formIn(source, kinds.get(flow.sourceId) ?? 'manualTask'));
  points[points.length - 1] = onForm(last, flow.targetSide, formIn(target, kinds.get(flow.targetId) ?? 'manualTask'));
  return { id: ids(element.id), elementId: element.id, waypoints: points, ...(flow.label === null ? {} : { label: moved(flow.label, offset) }) };
}

/**
 * The drawing of one map on a plane: the frame of its pool (on the root map only) and of its lanes, a shape per node, an
 * edge per flow, the documents and the notes in the band of the details; `offset` is the x of the column 0 minus 30.
 */
export function diagramOf(mapped: MappedProcess, plane: { id: string; planeId: string; elementId: string; name: string; poolId: string | null },
  withDetails: boolean, ids: (key: string) => string): BpmnDiagram {
  const placed = place(mapped, withDetails);
  const offset = (plane.poolId === null ? 0 : POOL_BAND) + LANE_BAND;
  const steps = new Map(placed.steps.map((step) => [step.id, step]));
  const width = contentWidth(placed.steps) + LANE_BAND;
  const laneX = plane.poolId === null ? 0 : POOL_BAND;
  const shapes: BpmnShape[] = [];
  if (plane.poolId !== null) shapes.push({ id: ids(plane.poolId), elementId: plane.poolId, bounds: { x: 0, y: 0, width: width + POOL_BAND, height: placed.grid.height }, horizontal: true });
  for (const lane of mapped.process.lanes) {
    const y = rowTop(placed.grid, lane.laneKey, 0);
    shapes.push({ id: ids(lane.id), elementId: lane.id, bounds: { x: laneX, y, width, height: (placed.grid.rows.get(lane.laneKey) ?? 1) * placed.grid.rowHeight }, horizontal: true });
  }
  const boxes = new Map<string, Rect>();
  const kinds = new Map<string, BpmnNodeKind>();
  const edges: BpmnEdge[] = [];
  for (const node of mapped.process.nodes) {
    const step = steps.get(node.stepId);
    if (step === undefined) continue;
    const box = moved(stepBox(placed.grid, step), offset);
    boxes.set(node.stepId, box);
    kinds.set(node.stepId, node.kind);
    shapes.push(nodeShape(node, box, step, ids));
    if (withDetails) {
      const details = detailsOf(mapped, node, box, step, placed.under.get(step.id) ?? 'centre', ids);
      shapes.push(...details.shapes);
      edges.push(...details.edges);
    }
  }
  for (const flow of placed.flows) {
    const edge = flowEdge(mapped, flow, boxes, kinds, offset, ids);
    if (edge !== null) edges.unshift(edge);
  }
  const frames = new Set([...(plane.poolId === null ? [] : [plane.poolId]), ...mapped.process.lanes.map((lane) => lane.id)]);
  const data = new Set(mapped.process.references.map((reference) => reference.id));
  return lowRows({ id: plane.id, planeId: plane.planeId, elementId: plane.elementId, name: plane.name, shapes, edges }, placed.grid, frames, data);
}

/** The rows as the cuts take them: from under the steps of each row down to its bottom, its band at most. */
function cutRows(grid: Grid): CutRow[] {
  return grid.lanes.flatMap((lane) => Array.from({ length: grid.rows.get(lane) ?? 1 }, (_, row) => {
    const top = rowTop(grid, lane, row);
    return { span: { top: top + FREE_BAND + STEP_HEIGHT, bottom: top + grid.rowHeight }, most: grid.below };
  }));
}

/** The drawing with each row as low as what it holds: the band of the details cut where nothing is drawn (bpmnCompact.ts). */
function lowRows(diagram: BpmnDiagram, grid: Grid, frames: ReadonlySet<string>, data: ReadonlySet<string>): BpmnDiagram {
  const rects = diagram.shapes.filter((shape) => !frames.has(shape.elementId)).flatMap((shape) => [
    data.has(shape.elementId) ? { ...shape.bounds, height: shape.bounds.height + DATA_NAME_HEIGHT } : shape.bounds,
    ...(shape.label === undefined ? [] : [shape.label]),
  ]);
  return compacted(diagram, cutsOf(cutRows(grid), takenSpans(rects, diagram.edges)));
}

/** The width of the steps of a map and of the aisle after its last column, without the free columns the editor offers. */
function contentWidth(steps: readonly RoutingStep[]): number {
  return mapWidth(steps) - FREE_COLUMNS * COLUMN_WIDTH;
}
