// useExport.ts
// The canvas in the hands of the exports: its element and the bounds of its nodes given to the editor while it is shown, those of the steps of a selection while its image is captured, its state while an image of it is captured (annex B 2.14, B-7-20, ADR 066, ADR 109).

import { useEffect, useMemo, type RefObject } from 'react';
import { mapBounds, selectionBounds, type Bounds, type SelectionFlow } from '../export/capture.ts';
import { useMapState } from '../state/EditorContext.tsx';
import type { MapEditor } from '../state/editor.ts';
import type { MapState } from '../state/mapStore.ts';
import type { MapNode } from './flowElements.ts';

/** An image of the map is being captured, for a PNG or a PDF; the workbook, the Markdown and the image SVG, drawn from the layout, need none (B-2.14-02). */
export const capturing = (state: Pick<MapState, 'exporting'>): boolean => state.exporting !== null && (state.exporting.format === 'pdf' || state.exporting.format === 'png');

/**
 * Gives the editor the element of the canvas and the bounds of its nodes while the canvas is shown, and gives back the
 * bounds: those of the steps of a selection and the flows between them while its image is captured, the line of the
 * classification placed under them (ADR 109, R8).
 */
export function useCaptureTarget(editor: MapEditor, root: RefObject<HTMLElement | null>, nodes: readonly MapNode[], flows: readonly { readonly route: SelectionFlow }[]): Bounds | null {
  const steps = useMapState((state) => (capturing(state) ? state.exporting?.steps : undefined));
  const bounds = useMemo(() => (steps === undefined ? mapBounds(nodes) : selectionBounds(nodes, steps, flows.map((flow) => flow.route))), [nodes, steps, flows]);
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
