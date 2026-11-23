// BpmnDrawing.tsx
// One drawing of a BPMN 2.0 diagram in SVG (ADR 106): the pool and the lanes in the tints of the map, the flows with their arrows and labels, the forms of the steps, each a button that opens its sheet (Enter, a click) or dives into its detail (a double click on a [+]), the documents and the notes with the details on.

import type { KeyboardEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { useTextMeasure } from '../../canvas/useTextMeasure.ts';
import type { Point } from '../../model/geometry/grid.ts';
import type { BpmnDefinitions, BpmnDiagram, BpmnEdge, BpmnNode, BpmnProcess, BpmnShape } from '../model/bpmnModel.ts';
import { DataForm, Lines, NodeForm, NoteForm } from './BpmnShapes.tsx';

/** What a person does on a form: open the sheet of its step, or dive into the detail of a detailed step. */
export interface BpmnActions {
  readonly onOpen: (stepId: string) => void;
  readonly onDive: (stepId: string) => void;
}

/** Every element of a process and of the processes below it, by identifier, with the kind of each. */
interface Index {
  readonly nodes: Map<string, BpmnNode>;
  readonly lanes: Map<string, { name: string; tint: number }>;
  readonly data: Map<string, { name: string; store: boolean }>;
  readonly notes: Map<string, string>;
  readonly flows: Set<string>;
}

function indexOf(definitions: BpmnDefinitions): Index {
  const index: Index = { nodes: new Map(), lanes: new Map(), data: new Map(), notes: new Map(), flows: new Set() };
  const walk = (process: BpmnProcess) => {
    process.lanes.forEach((lane, order) => index.lanes.set(lane.id, { name: lane.name, tint: (order % 8) + 1 }));
    process.references.forEach((reference) => index.data.set(reference.id, { name: reference.name, store: reference.store }));
    process.annotations.forEach((note) => index.notes.set(note.id, note.text));
    process.flows.forEach((flow) => index.flows.add(flow.id));
    process.nodes.forEach((node) => {
      index.nodes.set(node.id, node);
      if (node.detail !== undefined) walk(node.detail);
    });
  };
  walk(definitions.process);
  return index;
}

const pathOf = (points: readonly Point[]): string => points.map((point, at) => `${at === 0 ? 'M' : 'L'}${String(point.x)} ${String(point.y)}`).join(' ');

/** A flow: a sequence flow with its arrow and its label, a data association dashed, the link of a note dotted. */
function Edge({ edge, index }: { edge: BpmnEdge; index: Index }) {
  const sequence = index.flows.has(edge.elementId);
  const note = edge.elementId.startsWith('Association_');
  const look = sequence ? 'bpmn-flow' : note ? 'bpmn-association' : 'bpmn-data-flow';
  return (
    <g data-testid={sequence ? 'bpmn-flow' : undefined}>
      <path className={look} d={pathOf(edge.waypoints)} markerEnd={note ? undefined : 'url(#bpmn-arrow)'} />
    </g>
  );
}

/** The label of a sequence flow where the map writes it: a box with its text. */
function EdgeLabel({ edge, text }: { edge: BpmnEdge; text: string | null }) {
  if (edge.label === undefined || text === null) return null;
  const box = edge.label;
  return (
    <g className="bpmn-edge-label">
      <rect x={box.x} y={box.y} width={box.width} height={box.height} rx={3} />
      <Lines text={text} x={box.x + box.width / 2} y={box.y + box.height / 2} width={box.width - 8} lines={2} centred small />
    </g>
  );
}

/**
 * A form of a step: a button named by its kind and its name, that Enter or Space opens as a click does, and that
 * Alt+Down dives into when it is detailed, as a double click does (audit of the copy 64, P2-02); its label centred under
 * it for a gateway or an event.
 */
function NodeShape({ shape, node, actions }: { shape: BpmnShape; node: BpmnNode; actions: BpmnActions }) {
  const { t } = useTranslation();
  const label = t('bpmn.node_label', { kind: t(`bpmn.kinds.${node.kind}`), name: node.name });
  const keys = (event: KeyboardEvent) => {
    const dive = node.detailed && event.key === 'ArrowDown' && event.altKey && !event.ctrlKey && !event.shiftKey && !event.metaKey;
    const open = (event.key === 'Enter' || event.key === ' ') && !event.altKey && !event.ctrlKey && !event.metaKey;
    if (!dive && !open) return;
    event.preventDefault();
    event.stopPropagation();
    if (dive) actions.onDive(node.stepId);
    else actions.onOpen(node.stepId);
  };
  const classes = ['bpmn-node', `bpmn-node--${node.kind}`, node.translated ? null : 'is-untranslated'].filter(Boolean).join(' ');
  return (
    <g className={classes} role="button" tabIndex={0} aria-label={label} aria-keyshortcuts={node.detailed ? 'Enter Space Alt+ArrowDown' : 'Enter Space'}
      data-testid="bpmn-node" data-step={node.stepId} data-kind={node.kind}
      onClick={() => { actions.onOpen(node.stepId); }} onKeyDown={keys}
      onDoubleClick={() => { if (node.detailed) actions.onDive(node.stepId); }}>
      <title>{label}</title>
      <NodeForm kind={node.kind} bounds={shape.bounds} name={node.name} timer={node.timer !== undefined} />
      {shape.label !== undefined && (
        <Lines text={node.name} x={shape.label.x + shape.label.width / 2} y={shape.label.y} width={shape.label.width} lines={2} centred={false} anchor="middle" small />
      )}
    </g>
  );
}

/** A lane or the pool: its frame in the tint of the lane, its name written up its band on the left. */
function Frame({ shape, name, tint, pool }: { shape: BpmnShape; name: string; tint: number; pool: boolean }) {
  const { x, y, width, height } = shape.bounds;
  const cx = x + 15;
  const cy = y + height / 2;
  return (
    <g className={pool ? 'bpmn-pool' : `bpmn-lane bpmn-lane--${String(tint)}`} data-testid={pool ? 'bpmn-pool' : 'bpmn-lane'}>
      <rect className="bpmn-lane-body" x={x} y={y} width={width} height={height} />
      <rect className="bpmn-lane-band" x={x} y={y} width={30} height={height} />
      <text className="bpmn-lane-name" x={cx} y={cy} textAnchor="middle" dominantBaseline="central" transform={`rotate(-90 ${String(cx)} ${String(cy)})`}>{name}</text>
    </g>
  );
}

/** The width and the height of a drawing: the right and the bottom of its farthest shape. */
export function extentOf(diagram: BpmnDiagram): { width: number; height: number } {
  const rights = diagram.shapes.map((shape) => Math.max(shape.bounds.x + shape.bounds.width, (shape.label?.x ?? 0) + (shape.label?.width ?? 0)));
  const bottoms = diagram.shapes.map((shape) => Math.max(shape.bounds.y + shape.bounds.height, (shape.label?.y ?? 0) + (shape.label?.height ?? 0)) + 24);
  return { width: Math.max(1, ...rights) + 8, height: Math.max(1, ...bottoms) };
}

export function BpmnDrawing({ definitions, diagram, actions }: { definitions: BpmnDefinitions; diagram: BpmnDiagram; actions: BpmnActions }) {
  const index = indexOf(definitions);
  // The names of the documents measured in the font of the page, so that each holds in its place (R8).
  const measure = useTextMeasure();
  const { width, height } = extentOf(diagram);
  const frames = diagram.shapes.filter((shape) => shape.elementId === definitions.participantId || index.lanes.has(shape.elementId));
  const flowNames = new Map<string, string | null>();
  const walk = (process: BpmnProcess) => {
    process.flows.forEach((flow) => flowNames.set(flow.id, flow.name));
    process.nodes.forEach((node) => { if (node.detail !== undefined) walk(node.detail); });
  };
  walk(definitions.process);
  return (
    <svg className="bpmn-drawing" width={width} height={height} viewBox={`0 0 ${String(width)} ${String(height)}`} data-testid="bpmn-drawing">
      <defs>
        <marker id="bpmn-arrow" viewBox="0 0 10 10" refX="10" refY="5" markerWidth="8" markerHeight="8" orient="auto-start-reverse">
          <path className="bpmn-arrow" d="M0 0L10 5L0 10z" />
        </marker>
      </defs>
      {frames.map((shape) => (
        <Frame key={shape.id} shape={shape} pool={shape.elementId === definitions.participantId}
          name={shape.elementId === definitions.participantId ? definitions.name : index.lanes.get(shape.elementId)?.name ?? ''} tint={index.lanes.get(shape.elementId)?.tint ?? 1} />
      ))}
      {diagram.edges.map((edge) => <Edge key={edge.id} edge={edge} index={index} />)}
      {diagram.shapes.map((shape) => {
        const node = index.nodes.get(shape.elementId);
        if (node !== undefined) return <NodeShape key={shape.id} shape={shape} node={node} actions={actions} />;
        const data = index.data.get(shape.elementId);
        if (data !== undefined) return <g key={shape.id} className="bpmn-datum" data-testid="bpmn-data"><DataForm bounds={shape.bounds} store={data.store} name={data.name} measure={measure} /></g>;
        const note = index.notes.get(shape.elementId);
        if (note !== undefined) return <g key={shape.id} data-testid="bpmn-note"><NoteForm bounds={shape.bounds} text={note} /></g>;
        return null;
      })}
      {diagram.edges.map((edge) => <EdgeLabel key={`${edge.id}-label`} edge={edge} text={flowNames.get(edge.elementId) ?? null} />)}
    </svg>
  );
}
