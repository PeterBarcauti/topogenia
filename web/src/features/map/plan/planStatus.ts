// planStatus.ts
// The status of a pain point chosen on a line of the action plan (ADR 116): written with the version of the line under the right to fill the sheet where its status is chosen (the receiving sheet, or its own step when no use carries it, R10), followed with the other writes waiting (« Quit », the plan read again), then the plan and the map read again and the change kept for « Undo », which writes the status before as one more record; a refusal said, a conflict read again.

import { api, type Schema } from '../../../api/client.ts';
import { isApiError } from '../../../api/errors.ts';
import { messageOf } from '../../../shared/i18n/errorMessage.ts';
import { trackWrites, type PendingWrites } from '../../../shared/state/pendingWrites.ts';
import type { StatusCode } from '../../sheet/model/painPointStatus.ts';
import type { MapEditor } from '../state/editor.ts';
import { planStore } from './planStore.ts';
import { readPlan } from './usePlan.ts';

type Line = Schema<'ActionPlanLine'>;

/**
 * The line may take a status: the host says the person may fill the sheet where its status is chosen (the right of the
 * sheets, ADR 112): the receiving step, or the step of a pain point no use carries (R10); the route is under that step.
 */
export const fillable = (line: Line): boolean => line.can_fill;

/** The writes of the statuses asked and not answered. */
const asked = new Set<Promise<unknown>>();

/** The writes of the panel, a queue of the page as its store is: waited for by « Quit » and by the plan read again. */
const statusWrites: PendingWrites = {
  unsaved: () => asked.size > 0,
  flush: () => Promise.allSettled([...asked]).then(() => undefined),
};
trackWrites(statusWrites);

/** The status being written for a pain point, or none any more. */
function showWriting(painPointId: string, status: StatusCode | null): void {
  const others = Object.fromEntries(Object.entries(planStore.getState().writing).filter(([id]) => id !== painPointId));
  planStore.setState({ writing: status === null ? others : { ...others, [painPointId]: status } });
}

/** The write itself, followed until it is answered. */
function send(line: Line, status: StatusCode): Promise<unknown> {
  const write = api.put('/api/steps/{id}/pain-points/{painPointId}/status', {
    path: { id: line.status_step_id, painPointId: line.pain_point_id }, body: { status }, version: line.version,
  });
  asked.add(write);
  void write.catch(() => undefined).finally(() => asked.delete(write));
  return write;
}

/**
 * Writes the status chosen on a line, one write a line at a time: the plan of the root map read again once it is written
 * (the line may leave « Open » and the total), the map with it (its flames), the change kept for « Undo ». Refused: said;
 * changed meanwhile (409): the plan read again, so that the list shows the status the pain point has now.
 */
export async function chooseStatus(editor: MapEditor, rootMapId: string, line: Line, status: StatusCode): Promise<void> {
  if (!fillable(line) || status === line.status || line.pain_point_id in planStore.getState().writing) return;
  showWriting(line.pain_point_id, status);
  try {
    await send(line, status);
  } catch (failure) {
    editor.notify(messageOf(failure), 'error');
    if (isApiError(failure) && failure.status === 409) await readPlan(rootMapId);
    showWriting(line.pain_point_id, null);
    return;
  }
  void editor.reload();
  await readPlan(rootMapId);
  showWriting(line.pain_point_id, null);
  planStore.setState({ change: { painPointId: line.pain_point_id, code: line.code, before: line.status, after: status } });
}

/** « Undo »: the status before written back on the line as last read, with its version, one more record of the history. */
export function undoChange(editor: MapEditor, rootMapId: string): Promise<void> {
  const { change, reply } = planStore.getState();
  const line = reply?.lines.find((candidate) => candidate.pain_point_id === change?.painPointId);
  if (change === null || change.before === null || line === undefined) return Promise.resolve();
  return chooseStatus(editor, rootMapId, line, change.before);
}
