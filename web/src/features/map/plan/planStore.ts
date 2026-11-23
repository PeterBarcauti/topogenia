// planStore.ts
// The panel « Plan d'actions » (ADR 112 point 5), a store of the page and not of the editor, so that it outlives the change of map (each map mounts a new editor): the project, the panel open, the sort, the filters, the reply and the state of its reading, the plan to edit on another map, whether a sheet covered it since, the statuses being written from its lines and the last one written (ADR 116); never kept in the browser.

import { createStore, type StoreApi } from 'zustand/vanilla';
import type { Schema } from '../../../api/client.ts';
import type { StatusCode } from '../../sheet/model/painPointStatus.ts';
import type { PlanFilters, PlanSort } from './actionPlan.ts';

/** A plan to edit on another map (ADR 112 point 6): the sheet of the step that receives the document, the field of the document. */
export interface PlanEdit {
  readonly mapId: string;
  readonly stepId: string;
  readonly documentId: string;
}

/** A status chosen on a line of the panel and written (ADR 116): what « Undo » writes back, the status before. */
export interface PlanChange {
  readonly painPointId: string;
  readonly code: string;
  readonly before: Schema<'PainPointStatus'>;
  readonly after: StatusCode;
}

/** Nothing under way; the plan asked to the host; a refusal of the host or a failure. */
export type PlanStatus = 'idle' | 'reading' | 'error';

export interface PlanState {
  /** The project of the plan, null before the first opening; another project opened empties it. */
  readonly projectId: string | null;
  readonly open: boolean;
  readonly sort: PlanSort;
  readonly filters: PlanFilters;
  /** The plan of the root map last read, null before the first reply. */
  readonly reply: Schema<'ActionPlanView'> | null;
  readonly status: PlanStatus;
  readonly error: unknown;
  /** Asked from a line of another map, waiting for that map to be shown; null when none. */
  readonly edit: PlanEdit | null;
  /**
   * A sheet or the remainder covered the panel since its last reading: what was written there may change the plan, read
   * again as soon as the panel shows again, on this map or on the next one shown (ADR 112 point 5).
   */
  readonly covered: boolean;
  /** The statuses being written, by pain point: its list disabled and showing the choice until the plan is read again. */
  readonly writing: Readonly<Record<string, StatusCode>>;
  /** The last status written from the panel, said with « Undo » until the next reading of the panel; null when none. */
  readonly change: PlanChange | null;
}

/** The plan of a page before its first opening, and of a project left: the order of the project, the open pain points only. */
export const EMPTY_PLAN: PlanState = {
  projectId: null, open: false, sort: 'project', filters: { open: true, late: false, noAction: false }, reply: null, status: 'idle', error: null, edit: null, covered: false, writing: {},
  change: null,
};

export const planStore: StoreApi<PlanState> = createStore<PlanState>()(() => ({ ...EMPTY_PLAN }));

/** Keeps the plan to edit until its map is shown; asked again, the last request replaces the one before. */
export function askEdit(edit: PlanEdit): void {
  planStore.setState({ edit });
}

/** The plan to edit asked for the map given, left in the store; null for any other map. */
export function peekEdit(mapId: string): PlanEdit | null {
  const { edit } = planStore.getState();
  return edit?.mapId === mapId ? edit : null;
}

/** The plan to edit asked for the map shown, given once; forgotten by any other map shown first. */
export function takeEdit(mapId: string): PlanEdit | null {
  const { edit } = planStore.getState();
  if (edit === null) return null;
  planStore.setState({ edit: null });
  return edit.mapId === mapId ? edit : null;
}
