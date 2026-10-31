// sheet.ts
// What the sheet panel asks of the editor of the map: the gauge of its step without reading the map, the person responsible through the writes of the map, one panel docked at a time (annex B 4, B-2.6-11).

import { api, type Schema } from '../../../../api/client.ts';
import type { MapEditor } from '../editor.ts';

/**
 * The summary of a sheet read or saved, shown on its step without reading the map again: its gauge, its frequency, its
 * duration (B-4-50, B-2.6-11: the only change of the map that is not a reply of the host for the map).
 */
export function showSheetSummary(editor: MapEditor, stepId: string, summary: Schema<'SheetSummary'>): void {
  const map = editor.store.getState().map;
  const step = map?.steps.find((candidate) => candidate.id === stepId);
  if (map === null || step === undefined || JSON.stringify(step.sheet) === JSON.stringify(summary)) return;
  editor.store.setState({ map: { ...map, steps: map.steps.map((candidate) => (candidate === step ? { ...step, sheet: summary } : candidate)) } });
}

/** Gives the sheet of a step to a member, or to nobody in particular (null), with the version of the step; true once the map took the reply (B-4-15). */
export async function assignStep(editor: MapEditor, stepId: string, userId: string | null): Promise<boolean> {
  const reply = await editor.write((map) => {
    const step = map.steps.find((candidate) => candidate.id === stepId);
    return step === undefined ? null : api.patch('/api/steps/{id}', { path: { id: stepId }, body: { assignee_user_id: userId }, version: step.version });
  });
  return reply !== null;
}

/**
 * One panel docked on the right at a time (spec 9.5): a sheet opened closes the remainder and the assistant, the remainder
 * opened closes the sheet and the assistant (1.x: the remainder floated on the map beside the sheet), the assistant opened
 * closes the sheet and the remainder; the value stream layer turned on closes the three, its panel shown under any of
 * them opened later (spec 3.0 section 15.2). Gives the function that stops watching.
 */
export function watchSidePanel(editor: MapEditor): () => void {
  const stopFloating = editor.floating.subscribe((state, before) => {
    if (state.floating?.kind === 'remainder' && before.floating?.kind !== 'remainder') {
      editor.store.setState({ openSheet: null });
      editor.assistant.getState().setOpen(false);
    }
  });
  const stopStore = editor.store.subscribe((state, before) => {
    if (state.openSheet === null || state.openSheet === before.openSheet) return;
    if (editor.floating.getState().floating?.kind === 'remainder') editor.floating.getState().close();
    editor.assistant.getState().setOpen(false);
  });
  // The assistant opened closes the sheet and the remainder; its thread lives on while it is closed (spec 3.0 section 3.1-8).
  const stopAssistant = editor.assistant.subscribe((state, before) => {
    if (!state.open || before.open) return;
    if (editor.store.getState().openSheet !== null) editor.store.setState({ openSheet: null });
    if (editor.floating.getState().floating?.kind === 'remainder') editor.floating.getState().close();
  });
  const stopLayer = editor.preferences.subscribe((state, before) => {
    if (!state.showValueStream || before.showValueStream) return;
    if (editor.store.getState().openSheet !== null) editor.store.setState({ openSheet: null });
    if (editor.floating.getState().floating?.kind === 'remainder') editor.floating.getState().close();
    editor.assistant.getState().setOpen(false);
  });
  return () => {
    stopFloating();
    stopStore();
    stopAssistant();
    stopLayer();
  };
}
