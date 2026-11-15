// ArrowMarker.tsx
// The arrow heads of the flows, drawn once: a closed arrow of 18 × 18 in `--edge`, oriented along the end of the path (B-3.4-31); the same in the colour of the analysis for a handoff, as large on its thicker stroke (plan 3.1); the same in the accent for a flow lit by a presentation (ADR 097).

import { ARROW_SIZE, EDGE_STROKE, HANDOFF_STROKE, LIT_STROKE } from '../model/geometry/constants.ts';
import { ARROW_MARKER, HANDOFF_MARKER, LIT_MARKER } from './flowElements.ts';

/** The size of the head of a handoff, in units of its stroke, so that it is drawn as large as the others. */
const HANDOFF_SIZE = (ARROW_SIZE * EDGE_STROKE) / HANDOFF_STROKE;

/** The size of the head of a flow lit, in units of its stroke, for the same reason. */
const LIT_SIZE = (ARROW_SIZE * EDGE_STROKE) / LIT_STROKE;

/** The closed arrow of React Flow (`ArrowClosed`), scaled by the stroke of the flow as React Flow scales it. */
export function ArrowMarker() {
  return (
    <svg className="map-defs" aria-hidden="true" focusable="false">
      <defs>
        <marker id={ARROW_MARKER} className="map-arrow" viewBox="-10 -10 20 20" refX="0" refY="0" markerWidth={ARROW_SIZE} markerHeight={ARROW_SIZE}
          markerUnits="strokeWidth" orient="auto-start-reverse">
          <polyline points="-5,-4 0,0 -5,4 -5,-4" />
        </marker>
        <marker id={HANDOFF_MARKER} className="map-arrow-handoff" viewBox="-10 -10 20 20" refX="0" refY="0" markerWidth={HANDOFF_SIZE} markerHeight={HANDOFF_SIZE}
          markerUnits="strokeWidth" orient="auto-start-reverse">
          <polyline points="-5,-4 0,0 -5,4 -5,-4" />
        </marker>
        <marker id={LIT_MARKER} className="map-arrow-lit" viewBox="-10 -10 20 20" refX="0" refY="0" markerWidth={LIT_SIZE} markerHeight={LIT_SIZE}
          markerUnits="strokeWidth" orient="auto-start-reverse">
          <polyline points="-5,-4 0,0 -5,4 -5,-4" />
        </marker>
      </defs>
    </svg>
  );
}
