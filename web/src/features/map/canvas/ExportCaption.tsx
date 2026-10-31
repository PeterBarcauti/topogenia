// ExportCaption.tsx
// The line of the classification of the project under the map, drawn in the view while the image of a PNG is captured (S-4.1-08, AR-127).

import { ViewportPortal } from '@xyflow/react';
import { captionPosition, type Bounds } from '../export/capture.ts';
import { useMapState } from '../state/EditorContext.tsx';

/** Nothing out of an export, nor for a map without lanes or steps. */
export function ExportCaption({ bounds }: { readonly bounds: Bounds | null }) {
  const caption = useMapState((state) => state.exporting?.caption ?? null);
  if (caption === null || bounds === null) return null;
  const at = captionPosition(bounds);
  return (
    <ViewportPortal>
      <p className="export-caption" style={{ transform: `translate(${String(at.x)}px, ${String(at.y)}px)` }} data-testid="export-caption">{caption}</p>
    </ViewportPortal>
  );
}
