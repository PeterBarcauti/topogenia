// selection.ts
// The selection of the editor and its deletion: choose, choose all, delete what is chosen, asking first when detailed steps are in it (annex B 2.3, 2.5).

import { api } from '../../../../api/client.ts';
import { isMacro } from '../../model/stepFacts.ts';
import type { MapEditor } from '../editor.ts';
import type { Selection } from '../mapStore.ts';

export function select(editor: MapEditor, selection: Selection): void {
  editor.store.setState({ selection });
}

/** Every step of the map, no flow (B-2.5-10, B-2.4-40). */
export function selectAll(editor: MapEditor): void {
  select(editor, { steps: editor.store.getState().map?.steps.map((step) => step.id) ?? [], flows: [] });
}

/**
 * Deletes steps and flows: a detailed step alone asks what becomes of its map (B-2.3-39), a selection that holds detailed
 * steps asks to confirm that their maps go too (B-2.3-40), the rest is sent at once (B-2.3-36).
 */
export function deleteChosen(editor: MapEditor, steps: readonly string[], flows: readonly string[]): void {
  const map = editor.store.getState().map;
  if (map === null || steps.length + flows.length === 0) return;
  const macros = map.steps.filter((step) => steps.includes(step.id) && isMacro(step));
  const [macro] = macros;
  if (macro !== undefined && macros.length === 1 && steps.length === 1 && flows.length === 0) editor.floating.getState().open({ kind: 'macro-delete', stepId: macro.id });
  else if (macros.length > 0) editor.floating.getState().open({ kind: 'selection-delete', steps, flows });
  else void deleteSelection(editor, steps, flows);
}

/** The steps and flows deleted in one batch, the flow stitched behind the steps (B-2.3-38); their filled maps too when told. */
export async function deleteSelection(editor: MapEditor, steps: readonly string[], flows: readonly string[], withMaps = false): Promise<void> {
  await editor.write((map) =>
    api.post('/api/maps/{id}/deletion', { path: { id: map.id }, body: { steps: [...steps], flows: [...flows], stitch: true, sub_maps: withMaps ? 'delete' : null } }));
}
