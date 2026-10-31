// lanes.ts
// The writes of the lanes: add one at a place, rename it, change its type or colour, order the lanes, delete one with or without its steps (annex B 2.4, 2.12).

import { api, type Schema } from '../../../../api/client.ts';
import { i18next } from '../../../../shared/i18n/i18n.ts';
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
