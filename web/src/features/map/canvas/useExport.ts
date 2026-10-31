// useExport.ts
// The canvas in the hands of the exports: its element and the bounds of its nodes given to the editor while it is shown, its state while an image of it is captured (annex B 2.14, B-7-20, ADR 066).

import { useEffect, useMemo, type RefObject } from 'react';
import { mapBounds, type Bounds } from '../export/capture.ts';
import type { MapEditor } from '../state/editor.ts';
import type { MapState } from '../state/mapStore.ts';
import type { MapNode } from './flowElements.ts';

/** An image of the map is being captured, for a PNG or a PDF; the workbook needs none (B-2.14-02). */
export const capturing = (state: Pick<MapState, 'exporting'>): boolean => state.exporting !== null && state.exporting.format !== 'xlsx';

/** Gives the editor the element of the canvas and the bounds of its nodes while the canvas is shown, and gives back the bounds. */
export function useCaptureTarget(editor: MapEditor, root: RefObject<HTMLElement | null>, nodes: readonly MapNode[]): Bounds | null {
  const bounds = useMemo(() => mapBounds(nodes), [nodes]);
  useEffect(() => {
    const element = root.current;
    if (element === null) return undefined;
    editor.canvas.show({ element, bounds });
    return () => {
      if (editor.canvas.target()?.element === element) editor.canvas.show(null);
    };
  }, [editor, root, bounds]);
  return bounds;
}
