// sheet.ts
// What the sheet panel asks of the editor of the map: the gauge of its step without reading the map, the person responsible through the writes of the map, one panel docked at a time (annex B 4, B-2.6-11).

import { api, type Schema } from '../../../../api/client.ts';
import { peekEdit, planStore } from '../../plan/planStore.ts';
import { closePlan, planShownIn, readPlan, rootMapOf } from '../../plan/usePlan.ts';
import { searchStore } from '../../search/searchStore.ts';
import { closeSearch } from '../../search/useSearch.ts';
import { sendPendingWrites, writesWaiting } from '../../../../shared/state/pendingWrites.ts';
import type { MapEditor } from '../editor.ts';

/**
 * The summary of a sheet read or saved, shown on its step without reading the map again: its gauge, its frequency, its
 * duration (B-4-50, B-2.6-11: the only change of the map that is not a reply of the host for the map).
 */
export function showSheetSummary(editor: MapEditor, stepId: string, summary: Schema<'SheetSummary'>): void {
  editor.store.getState().summarize(stepId, summary);
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
 * closes the sheet and the remainder; the value stream layer or the layer « Analysis » turned on closes the three, its
 * panel shown under any of them opened later (spec 3.0 section 15.2, plan 3.1, audit 3.1 V2 5.1). The search opened closes
 * the sheet, the assistant and the remainder; a sheet opened covers the search, which comes back as it closes; the assistant
 * opened or a layer turned on closes the search (ADR 110 point 5). The action plan takes the place of the search: the two
 * exclude each other (ADR 112 point 5, `watchPlan`). Gives the function that stops watching.
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
    // The plan closed first: the sheet that closes after it uncovers nothing to read.
    if (planStore.getState().open) closePlan();
    if (editor.store.getState().openSheet !== null) editor.store.setState({ openSheet: null });
    if (editor.floating.getState().floating?.kind === 'remainder') editor.floating.getState().close();
    if (searchStore.getState().open) closeSearch();
  });
  const stopSearch = searchStore.subscribe((state, before) => {
    if (!state.open || before.open) return;
    if (planStore.getState().open) closePlan();
    if (editor.store.getState().openSheet !== null) editor.store.setState({ openSheet: null });
    if (editor.floating.getState().floating?.kind === 'remainder') editor.floating.getState().close();
    editor.assistant.getState().setOpen(false);
  });
  const stopPlan = watchPlan(editor);
  const stopLayer = editor.preferences.subscribe((state, before) => {
    const turnedOn = (state.showValueStream && !before.showValueStream) || (state.showAnalysis && !before.showAnalysis);
    if (!turnedOn) return;
    if (planStore.getState().open) closePlan();
    if (editor.store.getState().openSheet !== null) editor.store.setState({ openSheet: null });
    if (editor.floating.getState().floating?.kind === 'remainder') editor.floating.getState().close();
    editor.assistant.getState().setOpen(false);
    if (searchStore.getState().open) closeSearch();
  });
  return () => {
    stopFloating();
    stopStore();
    stopAssistant();
    stopSearch();
    stopPlan();
    stopLayer();
  };
}

/**
 * The action plan in the panel of the right (ADR 112 point 5): opened, it closes the sheet, the remainder, the assistant
 * and the search; covered by a sheet or the remainder, it is read again as it is uncovered (the plan written in the
 * sheet shows), on this map or on the map shown next (each map mounts a new editor), never while it stays covered, and
 * only once what was typed in the sheet is written (the last field left as the sheet closed).
 */
function watchPlan(editor: MapEditor): () => void {
  // The panels closed by the opening uncover nothing: the opening reads the plan itself (`openPlan`).
  let opening = false;
  let watching = true;
  const shown = () => planShownIn(planStore.getState(), editor.store.getState().map?.project_id ?? null);
  const cover = () => {
    if (shown() && !planStore.getState().covered) planStore.setState({ covered: true });
  };
  // Uncovered on this map: no sheet nor remainder over it, none asked for this map by « Edit the plan » either.
  const uncovered = () => {
    const { map, openSheet } = editor.store.getState();
    return watching && map !== null && shown() && planStore.getState().covered && openSheet === null
      && editor.floating.getState().floating?.kind !== 'remainder' && peekEdit(map.id) === null;
  };
  const read = () => {
    const map = editor.store.getState().map;
    if (map !== null && uncovered()) void readPlan(rootMapOf(map));
  };
  const readAgain = () => {
    if (opening || !uncovered()) return;
    if (!writesWaiting()) {
      read();
      return;
    }
    void sendPendingWrites().then(read);
  };
  const stopOpening = planStore.subscribe((state, before) => {
    if (!state.open || before.open) return;
    opening = true;
    if (editor.store.getState().openSheet !== null) editor.store.setState({ openSheet: null });
    if (editor.floating.getState().floating?.kind === 'remainder') editor.floating.getState().close();
    editor.assistant.getState().setOpen(false);
    if (searchStore.getState().open) closeSearch();
    opening = false;
  });
  const stopSheet = editor.store.subscribe((state, before) => {
    if (state.openSheet !== null && before.openSheet === null) cover();
    if ((state.openSheet === null && before.openSheet !== null) || (state.map !== null && before.map === null)) readAgain();
  });
  const stopRemainder = editor.floating.subscribe((state, before) => {
    if (state.floating?.kind === 'remainder' && before.floating?.kind !== 'remainder') cover();
    if (state.floating?.kind !== 'remainder' && before.floating?.kind === 'remainder') readAgain();
  });
  // The map may already be there as the watching starts.
  readAgain();
  return () => {
    watching = false;
    stopOpening();
    stopSheet();
    stopRemainder();
  };
}
