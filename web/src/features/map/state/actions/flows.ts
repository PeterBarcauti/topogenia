// flows.ts
// The writes of the flows: link two steps, label a flow, reverse it, choose its route (annex B 2.3, AR-155, ADR 091).

import { api } from '../../../../api/client.ts';
import { chosenRouteOf, routeBody, sameRoute } from '../../model/flowRoutes.ts';
import type { ChosenRoute } from '../../model/geometry/routing/model.ts';
import type { MapEditor } from '../editor.ts';

/** The longest label of a flow (B-2.3-33). */
export const FLOW_LABEL_MAX = 60;

/**
 * A flow from a step to another; never from a step to itself (B-2.3-21), nor one the map already has in that direction:
 * nothing is sent. The writes are serialised, so a second creation of one gesture is read once the first is answered,
 * and finds it (it was refused by the host before, a red message for a flow drawn once).
 */
export async function addFlow(editor: MapEditor, sourceId: string, targetId: string): Promise<void> {
  if (sourceId === targetId) return;
  await editor.write((map) => map.flows.some((flow) => flow.source_step_id === sourceId && flow.target_step_id === targetId) ? null
    : api.post('/api/maps/{id}/flows', { path: { id: map.id }, body: { source_step_id: sourceId, target_step_id: targetId, label: null } }));
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

/** The route of a flow as it is drawn now: the route of a gesture under way, or the one of the map. */
export function routeShown(editor: MapEditor, flowId: string): ChosenRoute | undefined {
  const { map, routeDraft } = editor.store.getState();
  if (routeDraft !== null && routeDraft.flowId === flowId) return routeDraft.route;
  return chosenRouteOf(map?.flows.find((flow) => flow.id === flowId)?.route);
}

/**
 * A change of the route of a flow, from the route it has to the one it gets (ADR 091): applied to the route of the map
 * when the write leaves, so that what the host changed meanwhile (a passage released) is never written back.
 */
export type RouteChange = (route: ChosenRoute | undefined) => ChosenRoute | undefined;

/**
 * Draws a flow with a route while a gesture moves it, or as the map has it again (null), nothing written (ADR 091); the
 * same route again changes nothing, so that the map is drawn again only when the route drawn changes.
 */
export function previewRoute(editor: MapEditor, flowId: string | null, route?: ChosenRoute): void {
  const { routeDraft } = editor.store.getState();
  if (flowId === null) {
    if (routeDraft !== null) editor.store.setState({ routeDraft: null });
    return;
  }
  if (routeDraft?.flowId === flowId && sameRoute(routeDraft.route, route)) return;
  editor.store.setState({ routeDraft: { flowId, route, sent: false } });
}

/**
 * The route of a flow changed and written whole (ADR 091): one operation, undone by one Ctrl+Z; nothing is sent when it
 * does not change. The flow stays drawn with it until the host answers, then as the host gives it; another write
 * answered meanwhile leaves it drawn.
 */
export async function setFlowRoute(editor: MapEditor, flowId: string, change: RouteChange): Promise<void> {
  const draft = { flowId, route: change(routeShown(editor, flowId)), sent: true };
  editor.store.setState({ routeDraft: draft });
  try {
    await editor.write((map) => {
      const flow = map.flows.find((candidate) => candidate.id === flowId);
      if (flow === undefined) return null;
      const current = chosenRouteOf(flow.route);
      const route = change(current);
      return sameRoute(current, route) ? null
        : api.put('/api/flows/{id}/route', { path: { id: flowId }, body: routeBody(route), version: flow.version });
    });
  } finally {
    if (editor.store.getState().routeDraft === draft) editor.store.setState({ routeDraft: null });
  }
}

/**
 * The drawing given back to several flows at once, their sides and passages left to it (plan 3.1, L2 C, draw.io « Clear
 * Waypoints »): one operation, undone by one Ctrl+Z; only the flows that carry a choice are sent, nothing when none does.
 */
export async function autoRoutes(editor: MapEditor, flowIds: readonly string[]): Promise<void> {
  await editor.write((map) => {
    const routed = map.flows.filter((flow) => flowIds.includes(flow.id) && flow.route != null).map((flow) => flow.id);
    return routed.length === 0 ? null : api.post('/api/maps/{id}/routes/auto', { path: { id: map.id }, body: { flows: routed } });
  });
}
