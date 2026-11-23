// BpmnShapes.tsx
// The forms of BPMN 2.0 the view draws (ADR 106), in SVG with the icons of the interface and the tokens of the charter: a task with its kind, a sub-process with its [+], a start, an end and a timer, three gateways, a document, a store, a note; the texts wrapped in their form, the name of a document in its place (R8).

import type { ReactNode } from 'react';
import { Clock, Cog, Hand, UserRound, type LucideIcon } from 'lucide-react';
import type { Rect } from '../../model/geometry/grid.ts';
import { clampLines, estimateMeasure, wrapLines, type TextMeasure } from '../../model/textSize.ts';
import { DATA_NAME } from '../model/bpmnLayout.ts';
import type { BpmnNodeKind } from '../model/bpmnModel.ts';

const TASK_FONT = { size: 12, weight: 500 } as const;
const SMALL_FONT = { size: 11, weight: 400 } as const;
const LINE = 14;

const TASK_ICONS: Partial<Record<BpmnNodeKind, LucideIcon>> = { manualTask: Hand, userTask: UserRound, serviceTask: Cog };

/**
 * A text in lines within a width, at most so many lines: centred on a point, or hung from a top. Hung, it is written from
 * its left, or centred on its point when the anchor says so: the label under a gateway or an event (audit of the copy 64,
 * P1-02: written from the middle of the form, it ran off to the right).
 */
export function Lines({ text, x, y, width, lines, centred, anchor, small = false }: {
  text: string; x: number; y: number; width: number; lines: number; centred: boolean; anchor?: 'middle' | 'start'; small?: boolean;
}) {
  const font = small ? SMALL_FONT : TASK_FONT;
  const wrapped = wrapLines(text, width, estimateMeasure, font);
  const shown = wrapped.length > lines ? [...wrapped.slice(0, lines - 1), `${wrapped[lines - 1] ?? ''}…`] : wrapped;
  const top = centred ? y - ((shown.length - 1) * LINE) / 2 : y;
  const textAnchor = anchor ?? (centred ? 'middle' : 'start');
  return (
    <text className={small ? 'bpmn-text bpmn-text--small' : 'bpmn-text'} x={x} y={top} textAnchor={textAnchor} dominantBaseline={centred ? 'central' : 'hanging'}>
      {shown.map((line, index) => <tspan key={index} x={x} y={top + index * LINE}>{line}</tspan>)}
    </text>
  );
}

/** A task or a sub-process: its box, its kind in its top left corner, its name in its middle, the [+] of a detail. */
function Activity({ kind, bounds, name }: { kind: BpmnNodeKind; bounds: Rect; name: string }) {
  const Icon = TASK_ICONS[kind];
  const cx = bounds.x + bounds.width / 2;
  return (
    <>
      <rect className="bpmn-form bpmn-form--activity" x={bounds.x} y={bounds.y} width={bounds.width} height={bounds.height} rx={10} />
      {Icon !== undefined && <Icon className="bpmn-icon" x={bounds.x + 6} y={bounds.y + 5} width={14} height={14} aria-hidden="true" />}
      <Lines text={name} x={cx} y={bounds.y + bounds.height / 2 + (kind === 'subProcess' ? -5 : 2)} width={bounds.width - 16} lines={kind === 'subProcess' ? 2 : 3} centred />
      {kind === 'subProcess' && (
        <g className="bpmn-marker">
          <rect x={cx - 7} y={bounds.y + bounds.height - 16} width={14} height={14} />
          <path d={`M${String(cx - 4)} ${String(bounds.y + bounds.height - 9)}h8M${String(cx)} ${String(bounds.y + bounds.height - 13)}v8`} />
        </g>
      )}
    </>
  );
}

/** An event: a thin circle to start, a thick one to end, a double one with a clock for a wait. */
function Event({ kind, bounds, timer }: { kind: BpmnNodeKind; bounds: Rect; timer: boolean }) {
  const [cx, cy, r] = [bounds.x + bounds.width / 2, bounds.y + bounds.height / 2, bounds.width / 2];
  const look = kind === 'endEvent' ? 'end' : kind === 'intermediateCatchEvent' ? 'intermediate' : 'start';
  return (
    <>
      <circle className={`bpmn-form bpmn-event bpmn-event--${look}`} cx={cx} cy={cy} r={r} />
      {look === 'intermediate' && <circle className="bpmn-event-inner" cx={cx} cy={cy} r={r - 3} />}
      {timer && <Clock className="bpmn-icon" x={cx - 9} y={cy - 9} width={18} height={18} aria-hidden="true" />}
    </>
  );
}

/** A gateway: its diamond and its marker, X for an exclusive one, + for a parallel one, O for an inclusive one. */
function Gateway({ kind, bounds }: { kind: BpmnNodeKind; bounds: Rect }) {
  const [x, y, w, h] = [bounds.x, bounds.y, bounds.width, bounds.height];
  const [cx, cy] = [x + w / 2, y + h / 2];
  const points = `${String(cx)},${String(y)} ${String(x + w)},${String(cy)} ${String(cx)},${String(y + h)} ${String(x)},${String(cy)}`;
  const marker = kind === 'exclusiveGateway'
    ? <path d={`M${String(cx - 8)} ${String(cy - 8)}l16 16M${String(cx + 8)} ${String(cy - 8)}l-16 16`} />
    : kind === 'parallelGateway' ? <path d={`M${String(cx)} ${String(cy - 11)}v22M${String(cx - 11)} ${String(cy)}h22`} /> : <circle cx={cx} cy={cy} r={10} />;
  return (
    <>
      <polygon className="bpmn-form bpmn-form--gateway" points={points} />
      <g className="bpmn-gateway-marker">{marker}</g>
    </>
  );
}

/** The form of a flow node of a kind, in its bounds. */
export function NodeForm({ kind, bounds, name, timer }: { kind: BpmnNodeKind; bounds: Rect; name: string; timer: boolean }): ReactNode {
  if (kind.endsWith('Gateway')) return <Gateway kind={kind} bounds={bounds} />;
  if (kind.endsWith('Event')) return <Event kind={kind} bounds={bounds} timer={timer} />;
  return <Activity kind={kind} bounds={bounds} name={name} />;
}

/**
 * A document: a sheet with a folded corner; a store: a cylinder. Its name under it, centred, held in its place on two lines
 * at most and cut by an ellipsis, by the measure given (R8); the whole name on hover.
 */
export function DataForm({ bounds, store, name, measure }: { bounds: Rect; store: boolean; name: string; measure: TextMeasure }) {
  const [x, y, w, h] = [bounds.x, bounds.y, bounds.width, bounds.height];
  const path = store
    ? `M${String(x)} ${String(y + 6)}a${String(w / 2)} 6 0 0 1 ${String(w)} 0v${String(h - 12)}a${String(w / 2)} 6 0 0 1 ${String(-w)} 0zM${String(x)} ${String(y + 6)}a${String(w / 2)} 6 0 0 0 ${String(w)} 0`
    : `M${String(x)} ${String(y)}h${String(w - 10)}l10 10v${String(h - 10)}h${String(-w)}zM${String(x + w - 10)} ${String(y)}v10h10`;
  const [cx, top] = [x + w / 2, y + h + DATA_NAME.top];
  const lines = clampLines(name, DATA_NAME.width, DATA_NAME.lines, measure, SMALL_FONT);
  return (
    <>
      <title>{name}</title>
      <path className="bpmn-form bpmn-data" d={path} />
      <text className="bpmn-text bpmn-text--small" x={cx} y={top} textAnchor="middle" dominantBaseline="hanging">
        {lines.map((line, index) => <tspan key={index} x={cx} y={top + index * DATA_NAME.line}>{line}</tspan>)}
      </text>
    </>
  );
}

/** A note: the bracket on its left, its text inside. */
export function NoteForm({ bounds, text }: { bounds: Rect; text: string }) {
  const [x, y, w, h] = [bounds.x, bounds.y, bounds.width, bounds.height];
  return (
    <>
      <rect className="bpmn-note-back" x={x} y={y} width={w} height={h} />
      <path className="bpmn-note" d={`M${String(x + 10)} ${String(y)}h-10v${String(h)}h10`} />
      <Lines text={text.replace(/\n/g, ' · ')} x={x + 5} y={y + 4} width={w - 8} lines={3} centred={false} small />
    </>
  );
}
