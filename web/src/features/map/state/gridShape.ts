// gridShape.ts
// The shape of the grid the canvas shows, read from the editor in one place for every gesture that aims at a cell and every drawing: the band of the value stream layer when it is on, the empty rows the lanes show (spec 14.5, ADR 096).

import { useMemo } from 'react';
import { useStore } from 'zustand';
import { bandOf } from '../model/geometry/constants.ts';
import type { GridShape } from '../model/gridShape.ts';
import type { MapEditor } from './editor.ts';
import { useMapState } from './EditorContext.tsx';

/** The shape of the grid shown now, for a gesture: what it aims at is the cell drawn under the pointer. */
export const shapeOf = (editor: MapEditor): GridShape => ({
  below: bandOf(editor.preferences.getState().showValueStream), rows: editor.store.getState().rowsAtLeast,
});

/** The shape of the grid shown, followed by a component: the same values give the same shape. */
export function useGridShape(editor: MapEditor): GridShape {
  const showStream = useStore(editor.preferences, (state) => state.showValueStream);
  const rows = useMapState((state) => state.rowsAtLeast);
  return useMemo(() => ({ below: bandOf(showStream), rows }), [showStream, rows]);
}
