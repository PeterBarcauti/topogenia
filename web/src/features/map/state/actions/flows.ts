// flows.ts
// The writes of the flows: link two steps, label a flow, reverse it (annex B 2.3, AR-155).

import { api } from '../../../../api/client.ts';
import type { MapEditor } from '../editor.ts';

/** The longest label of a flow (B-2.3-33). */
export const FLOW_LABEL_MAX = 60;

/** A flow from a step to another; never from a step to itself (B-2.3-21). */
export async function addFlow(editor: MapEditor, sourceId: string, targetId: string): Promise<void> {
  if (sourceId === targetId) return;
  await editor.write((map) => api.post('/api/maps/{id}/flows', { path: { id: map.id }, body: { source_step_id: sourceId, target_step_id: targetId, label: null } }));
}

/** The label of a flow; an empty text removes it (B-2.3-34); nothing is sent when it does not change. */
export async function relabelFlow(editor: MapEditor, flowId: string, text: string): Promise<void> {
  const label = text.trim() === '' ? null : text.trim();
  await editor.write((map) => {
    const flow = map.flows.find((candidate) => candidate.id === flowId);
    if (flow === undefined || flow.label === label) return null;
    return api.patch('/api/flows/{id}', { path: { id: flowId }, body: { label }, version: flow.version });
  });
}

/** A flow in the other direction, its label kept; refused by the host when its reverse is already drawn (AR-155). */
export async function reverseFlow(editor: MapEditor, flowId: string): Promise<void> {
  await editor.write((map) => {
    const flow = map.flows.find((candidate) => candidate.id === flowId);
    return flow === undefined ? null : api.post('/api/flows/{id}/reverse', { path: { id: flowId }, version: flow.version });
  });
}
