// ArrowMarker.tsx
// The arrow head of every flow, drawn once: a closed arrow of 18 × 18 in `--edge`, oriented along the end of the path (B-3.4-31).

import { ARROW_SIZE } from '../model/geometry/constants.ts';
import { ARROW_MARKER } from './flowElements.ts';

/** The closed arrow of React Flow (`ArrowClosed`), scaled by the stroke of the flow as React Flow scales it. */
export function ArrowMarker() {
  return (
    <svg className="map-defs" aria-hidden="true" focusable="false">
      <defs>
        <marker id={ARROW_MARKER} className="map-arrow" viewBox="-10 -10 20 20" refX="0" refY="0" markerWidth={ARROW_SIZE} markerHeight={ARROW_SIZE}
          markerUnits="strokeWidth" orient="auto-start-reverse">
          <polyline points="-5,-4 0,0 -5,4 -5,-4" />
        </marker>
      </defs>
    </svg>
  );
}
