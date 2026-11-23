// bpmnCompact.ts
// The rows of a BPMN drawing as low as what they hold (trial of the 3.3): the band of the details the routing gives every row is cut where nothing is drawn across the whole width, so that a row without a document or a note keeps about the height of the map; what lies under a cut goes up, a vertical segment is shortened, nothing else moves.

import type { Point, Rect } from '../../model/geometry/grid.ts';
import type { BpmnDiagram, BpmnEdge, BpmnShape } from './bpmnModel.ts';

/** A span of heights, from its top to its bottom. */
export interface Span {
  readonly top: number;
  readonly bottom: number;
}

/** A row of the drawing: the span under its steps where a cut may be made, and the most that may be cut there (its band). */
export interface CutRow {
  readonly span: Span;
  readonly most: number;
}

/** The room kept around what is drawn: two things a cut brings together stay twice this apart. */
export const CLEARANCE = 8;

/** The heights taken by what is drawn: the forms and their labels given, each horizontal segment of a flow, each label of a flow. */
export function takenSpans(rects: readonly Rect[], edges: readonly BpmnEdge[]): Span[] {
  const spans = rects.map((rect) => ({ top: rect.y, bottom: rect.y + rect.height }));
  for (const edge of edges) {
    edge.waypoints.forEach((point, at) => {
      const next = edge.waypoints[at + 1];
      if (next !== undefined && Math.abs(next.y - point.y) < 0.5) spans.push({ top: point.y, bottom: point.y });
    });
    if (edge.label !== undefined) spans.push({ top: edge.label.y, bottom: edge.label.y + edge.label.height });
  }
  return spans;
}

/** The free spans of a row: its span without what is taken, each taken span widened by the clearance. */
function freeSpans(row: Span, taken: readonly Span[]): Span[] {
  const blocks = taken
    .map((span) => ({ top: span.top - CLEARANCE, bottom: span.bottom + CLEARANCE }))
    .filter((span) => span.bottom > row.top && span.top < row.bottom)
    .sort((a, b) => a.top - b.top);
  const free: Span[] = [];
  let from = row.top;
  for (const block of blocks) {
    if (block.top > from) free.push({ top: from, bottom: block.top });
    from = Math.max(from, block.bottom);
  }
  if (row.bottom > from) free.push({ top: from, bottom: row.bottom });
  return free;
}

/** The cuts of the rows: in each, its free spans, the largest first, until its band is given back; in the order of the heights. */
export function cutsOf(rows: readonly CutRow[], taken: readonly Span[]): Span[] {
  const cuts: Span[] = [];
  for (const row of rows) {
    let left = row.most;
    for (const span of freeSpans(row.span, taken).sort((a, b) => b.bottom - b.top - (a.bottom - a.top))) {
      if (left <= 0) break;
      const length = Math.min(left, span.bottom - span.top);
      cuts.push({ top: span.top, bottom: span.top + length });
      left -= length;
    }
  }
  return cuts.sort((a, b) => a.top - b.top);
}

/** The height of a point once the cuts are made: up by every cut above it, a point within a cut brought to its top. */
export function liftOf(cuts: readonly Span[]): (y: number) => number {
  return (y) => {
    let lifted = 0;
    for (const cut of cuts) {
      if (y >= cut.bottom) lifted += cut.bottom - cut.top;
      else if (y > cut.top) lifted += y - cut.top;
    }
    return y - lifted;
  };
}

const liftedRect = (rect: Rect, lift: (y: number) => number): Rect => {
  const top = lift(rect.y);
  return { ...rect, y: top, height: lift(rect.y + rect.height) - top };
};

/** The drawing with the cuts made: every shape, label and point lifted, the frames of the lanes and of the pool shortened. */
export function compacted(diagram: BpmnDiagram, cuts: readonly Span[]): BpmnDiagram {
  if (cuts.length === 0) return diagram;
  const lift = liftOf(cuts);
  const point = (at: Point): Point => ({ x: at.x, y: lift(at.y) });
  const shape = (item: BpmnShape): BpmnShape => ({
    ...item, bounds: liftedRect(item.bounds, lift), ...(item.label === undefined ? {} : { label: liftedRect(item.label, lift) }),
  });
  const edge = (item: BpmnEdge): BpmnEdge => ({
    ...item, waypoints: item.waypoints.map(point), ...(item.label === undefined ? {} : { label: liftedRect(item.label, lift) }),
  });
  return { ...diagram, shapes: diagram.shapes.map(shape), edges: diagram.edges.map(edge) };
}
