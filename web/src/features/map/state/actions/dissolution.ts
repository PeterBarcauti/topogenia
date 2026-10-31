// dissolution.ts
// The detail brought back into the flow and its remainder: dissolve, assign or delete an element, finish; the frame stored on its own once, its panel closed with it (annex B 2.9).

import { api, type Schema } from '../../../../api/client.ts';
import { storedOnItsOwn } from '../../model/remainder.ts';
import type { MapEditor } from '../editor.ts';

type MapView = Schema<'MapView'>;
type RemainderKind = Schema<'RemainderKind'>;

/** The steps of the map detailed back into the flow, in a frame with what the detailed step carried (B-2.4-16, B-2.9-16). */
export async function dissolveStep(editor: MapEditor, stepId: string): Promise<void> {
  await editor.write((map) => {
    const step = map.steps.find((candidate) => candidate.id === stepId);
    return step === undefined ? null : api.post('/api/steps/{id}/dissolve', { path: { id: stepId }, version: step.version });
  });
}

/** An element of the remainder given to a step, or to the map for a pain point (B-2.9-29, B-2.9-31); the frame closes once empty. */
export async function assignRemainder(editor: MapEditor, kind: RemainderKind, objectId: string, stepId: string | null): Promise<void> {
  await editor.write((map) => {
    const frame = map.dissolution;
    if (frame === null) return null;
    return api.post('/api/dissolutions/{id}/assign', { path: { id: frame.id }, body: { kind, object_id: objectId, step_id: stepId, from_step_id: null } });
  });
}

/** An element of the remainder deleted for good, with the uses of a document (B-2.9-32, B-2.6-39). */
export async function removeRemainder(editor: MapEditor, kind: RemainderKind, objectId: string): Promise<void> {
  await editor.write((map) => {
    const frame = map.dissolution;
    return frame === null ? null : api.post('/api/dissolutions/{id}/remove', { path: { id: frame.id }, body: { kind, object_id: objectId } });
  });
}

/**
 * « Finish » stores what is left (B-2.9-33 to B-2.9-35): where it went is shown when something was stored, said to be
 * stored on its own after the automatic finish (B-2.9-39); else nothing is shown, and the panel closes with the frame as
 * the frame watched closes it (B-2.9-36, B-2.9-40). A refusal leaves the panel open on the frame read again (1.x: closed it).
 */
export async function finishFrame(editor: MapEditor, auto = false): Promise<void> {
  const finished: { code: string; arrangements: readonly Schema<'Arrangement'>[] }[] = [];
  await editor.write((map) => {
    const frame = map.dissolution;
    if (frame === null) return null;
    return api.post('/api/dissolutions/{id}/finish', { path: { id: frame.id } }).then((reply) => {
      finished.push({ code: frame.step_code, arrangements: reply.arrangements });
      return reply;
    });
  });
  const [done] = finished;
  if (done !== undefined && done.arrangements.length > 0) editor.floating.getState().open({ kind: 'arrangements', code: done.code, arrangements: done.arrangements, auto });
}

/** The frames an editor stored on its own: once a dissolution (B-2.9-38); never kept beyond the editor. */
const storedFrames = new WeakMap<MapEditor, Set<string>>();

function checkFrame(editor: MapEditor, map: MapView | null): void {
  const frame = map?.dissolution ?? null;
  const floating = editor.floating.getState();
  if (frame === null && floating.floating?.kind === 'remainder') floating.close();
  if (map === null || frame === null || !map.rights.structure || !storedOnItsOwn(frame)) return;
  const done = storedFrames.get(editor) ?? new Set<string>();
  if (done.has(frame.id)) return;
  storedFrames.set(editor, done.add(frame.id));
  void finishFrame(editor, true);
}

/**
 * Watches the map of an editor: a frame closed closes its panel (B-2.9-40); a frame left with one step or none, on a map
 * that may be changed, is finished on its own, once a dissolution (B-2.9-38). Gives the function that stops watching.
 */
export function watchFrame(editor: MapEditor): () => void {
  checkFrame(editor, editor.store.getState().map);
  return editor.store.subscribe((state, before) => {
    if (state.map !== before.map) checkFrame(editor, state.map);
  });
}
