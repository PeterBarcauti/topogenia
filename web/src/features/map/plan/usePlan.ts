// usePlan.ts
// The reading of the action plan (ADR 112 point 5): the plan of the root map of the project, read each time the panel is shown (opened, or uncovered as the sheet over it closes, here or on the map shown next, once what was typed in the sheet is written, or after a status written from a line), a new read cancelling the one before and forgetting the change said before it; never read on a timer, the writes of others seen at the next showing.

import { api, type Schema } from '../../../api/client.ts';
import { EMPTY_PLAN, planStore, type PlanState } from './planStore.ts';

type MapView = Schema<'MapView'>;

/** The read under way, which a new read aborts. */
let reading: AbortController | null = null;

/** The root map of the project of a map: the first of its breadcrumb, the map itself at the root. */
export const rootMapOf = (map: MapView): string => map.breadcrumb[0]?.map_id ?? map.id;

/** The panel is open on the project given (the plan of another project is never shown). */
export const planShownIn = (state: PlanState, projectId: string | null): boolean => state.open && state.projectId === projectId;

/** Aborts the read under way: what it would bring is not taken. */
function stop(): void {
  reading?.abort();
  reading = null;
}

/**
 * Reads the plan of the root map given, the read before cancelled; the reply shown before stays until the new one is there.
 * The change said with « Undo » is forgotten: it stays until the next reading of the panel (ADR 116).
 */
export function readPlan(rootMapId: string): Promise<void> {
  stop();
  const controller = new AbortController();
  reading = controller;
  planStore.setState({ status: 'reading', error: null, covered: false, change: null });
  return api.get('/api/maps/{id}/action-plan', { path: { id: rootMapId }, signal: controller.signal }).then((reply) => {
    if (!controller.signal.aborted) planStore.setState({ reply, status: 'idle' });
  }, (error: unknown) => {
    if (!controller.signal.aborted) planStore.setState({ status: 'error', error });
  }).finally(() => {
    if (reading === controller) reading = null;
  });
}

/** Opens the panel on the project of the map and reads its plan: another project empties the plan first, its sort and filters with it. */
export function openPlan(map: MapView): void {
  if (planStore.getState().projectId !== map.project_id) {
    stop();
    planStore.setState({ ...EMPTY_PLAN, projectId: map.project_id });
  }
  planStore.setState({ open: true });
  void readPlan(rootMapOf(map));
}

/** Closes the panel: the read under way is cancelled, the sort, the filters and the last reply kept for the next opening. */
export function closePlan(): void {
  const wasReading = reading !== null;
  stop();
  planStore.setState(wasReading ? { open: false, status: 'idle', covered: false } : { open: false, covered: false });
}

/** The plan of another project than the one shown is forgotten (ADR 112 point 5: emptied when another project opens). */
export function leaveOtherPlan(projectId: string): void {
  const kept = planStore.getState().projectId;
  if (kept === null || kept === projectId) return;
  stop();
  planStore.setState({ ...EMPTY_PLAN });
}
