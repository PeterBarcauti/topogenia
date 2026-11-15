// lanes.ts
// The writes of the lanes: add one at a place, rename it, change its type or colour, order the lanes, delete one with or without its steps (annex B 2.4, 2.12), make one taller or shorter by a row (ADR 096).

import { api, type Schema } from '../../../../api/client.ts';
import { i18next } from '../../../../shared/i18n/i18n.ts';
import type { LaneRows } from '../../model/gridShape.ts';
import { growth, shrinking, type LaneEnd, type RowChange } from '../../model/laneRows.ts';
import { lanesInOrder } from '../../model/readingOrder.ts';
import type { MapEditor } from '../editor.ts';

type LaneView = Schema<'LaneView'>;

/** The longest name of a lane, as the one of a step. */
export const LANE_NAME_MAX = 150;

/** A lane « Acteur N » of type person at a place of the order, its name typed at once (B-2.12-20 to B-2.12-22). */
export async function addLane(editor: MapEditor, order: number): Promise<void> {
  const reply = await editor.write((map) => api.post('/api/maps/{id}/lanes', {
    path: { id: map.id },
    body: { name: i18next.t('map.new_lane', { n: map.lanes.length + 1 }), type: 'person', color: null, sort_order: order, actor_user_id: null, translations: null },
  }));
  if (reply?.id != null) editor.store.setState({ editing: `lane:${reply.id}` });
}

/** The name, the type or the colour of a lane; nothing is sent when nothing changes. */
export async function updateLane(editor: MapEditor, laneId: string, changes: Partial<Pick<LaneView, 'name' | 'type' | 'color'>>): Promise<void> {
  await editor.write((map) => {
    const lane = map.lanes.find((candidate) => candidate.id === laneId);
    if (lane === undefined) return null;
    const body = Object.fromEntries(Object.entries(changes).filter(([field, value]) => lane[field as keyof typeof changes] !== value));
    return Object.keys(body).length === 0 ? null : api.patch('/api/lanes/{id}', { path: { id: laneId }, body, version: lane.version });
  });
}

/** The new order of the lanes, all of them (B-2.12-14); nothing when it does not change. */
export async function orderLanes(editor: MapEditor, order: readonly string[]): Promise<void> {
  await editor.write((map) => {
    const before = lanesInOrder(map.lanes);
    return before.every((id, index) => order[index] === id) ? null : api.put('/api/maps/{id}/lane-order', { path: { id: map.id }, body: [...order] });
  });
}

/** Deletes a lane: at once when it is empty, else the dialog that moves or deletes its steps (B-2.4-49, B-2.4-50). */
export function deleteLane(editor: MapEditor, laneId: string): void {
  const map = editor.store.getState().map;
  if (map === null || map.lanes.length <= 1) return;
  if (map.steps.some((step) => step.lane_id === laneId)) editor.floating.getState().open({ kind: 'lane-delete', laneId });
  else void deleteLaneWith(editor, laneId);
}

/** A lane deleted, its steps moved to another lane or deleted with it (B-2.4-50, B-2.4-51). */
export async function deleteLaneWith(editor: MapEditor, laneId: string, steps?: 'move' | 'delete', to?: string): Promise<void> {
  await editor.write((map) => {
    const lane = map.lanes.find((candidate) => candidate.id === laneId);
    return lane === undefined ? null : api.delete('/api/lanes/{id}', { path: { id: laneId }, query: { steps, to }, version: lane.version });
  });
}

/**
 * A change of the rows of a lane, after the writes sent before (ADR 096), planned on the map of the moment: rows the host
 * inserts or takes out where the lane holds something, in one operation that Ctrl+Z undoes; then the empty rows the
 * editor shows under it. True once done; false when nothing changes or the host refused.
 */
async function changeRows(editor: MapEditor, laneId: string, plan: (map: Schema<'MapView'>, rows: LaneRows) => RowChange | null): Promise<boolean> {
  const planned: { change: RowChange | null } = { change: null };
  const reply = await editor.write((map) => {
    const change = plan(map, editor.store.getState().rowsAtLeast);
    planned.change = change;
    if (change === null) return null;
    if (change.inserted > 0) return api.post('/api/lanes/{id}/rows', { path: { id: laneId }, body: { row: 0, count: change.inserted } });
    return change.removed === null ? null : api.delete('/api/lanes/{id}/rows/{row}', { path: { id: laneId, row: change.removed } });
  });
  const { change } = planned;
  if (change === null || ((change.inserted > 0 || change.removed !== null) && reply === null)) return false;
  // A pull drawn until now ends with its rows, in one drawing.
  editor.store.setState({ rowsAtLeast: change.rows, rowsDraft: null });
  return true;
}

/** A lane taller by rows at an end: at its bottom, or for a lane that holds nothing, empty rows the editor shows; at its top, rows the host inserts. */
export const growLane = (editor: MapEditor, laneId: string, end: LaneEnd, count: number): Promise<boolean> =>
  changeRows(editor, laneId, (map, rows) => growth(map, rows, laneId, end, count));

/** A lane shorter by its first or its last row when it is empty: one of its empty rows the editor shows, or a row the host takes out. */
export const shrinkLane = (editor: MapEditor, laneId: string, end: LaneEnd): Promise<boolean> =>
  changeRows(editor, laneId, (map, rows) => shrinking(map, rows, laneId, end));
