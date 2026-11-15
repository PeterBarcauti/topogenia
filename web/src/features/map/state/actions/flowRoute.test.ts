// flowRoute.test.ts
// The route chosen for a flow written (ADR 091): one write with the version of the flow, its change applied to the route of the map when the write leaves, nothing for the same route; the flow drawn with the route of the gesture until the host answers, whatever other write is answered meanwhile, then as the host gives it.

import { api } from '../../../../api/client.ts';
import { withPassage, withSide } from '../../model/flowRoutes.ts';
import { editorOf } from '../../../../test/editorStore.ts';
import { callsTo, fakeApi } from '../../../../test/fakeApi.ts';
import { flowOf, replyOf, sampleMap } from '../../../../test/maps.ts';
import { previewRoute, routeShown, setFlowRoute } from './flows.ts';

/** The sample map, its flow f1 with the route of the API given. */
const routed = (route: { source_side: 'top' | null; via: { aisle: number } | null }, version = 1) => sampleMap({
  flows: sampleMap().flows.map((flow) => (flow.id === 'f1' ? {
    ...flowOf('f1', 's1', 's2'), version,
    route: { source_side: route.source_side, target_side: null, via: route.via === null ? null : { aisle: route.via.aisle, column: null, lane_id: null, row: null, edge: null } },
  } : flow)),
});

describe('the route of a flow (ADR 091)', () => {
  it('is written whole with the version of the flow, the map taken from the reply, the gesture forgotten', async () => {
    const answered = routed({ source_side: 'top', via: null }, 2);
    const sent = fakeApi({ 'PUT /api/flows/f1/route': { json: replyOf(answered) } });
    const { editor } = await editorOf(sampleMap());
    previewRoute(editor, 'f1', { source: 'top' });
    expect(routeShown(editor, 'f1')).toEqual({ source: 'top' });
    await setFlowRoute(editor, 'f1', (route) => withSide(route, 'source', 'top'));
    expect(callsTo(sent, 'PUT', '/api/flows/f1/route').map((call) => [call.body, call.headers.get('If-Match')]))
      .toEqual([[{ source_side: 'top', target_side: null, via: null }, '"1"']]);
    expect(editor.store.getState().routeDraft).toBeNull();
    expect(routeShown(editor, 'f1')).toEqual({ source: 'top' });
  });

  it('sends nothing for the route the flow has, and a gesture given up draws the map as it is', async () => {
    const sent = fakeApi({});
    const { editor } = await editorOf(sampleMap());
    await setFlowRoute(editor, 'f1', () => undefined);
    await setFlowRoute(editor, 'gone', () => ({ source: 'top' }));
    expect(sent).toEqual([]);
    previewRoute(editor, 'f2', { via: { aisle: 1 } });
    expect(editor.store.getState().routeDraft).toEqual({ flowId: 'f2', route: { via: { aisle: 1 } }, sent: false });
    previewRoute(editor, null);
    expect(editor.store.getState().routeDraft).toBeNull();
    expect(routeShown(editor, 'f2')).toBeUndefined();
  });

  it('applies its change to the route of the map when the write leaves: a passage the host released meanwhile is not written back', async () => {
    const released = routed({ source_side: null, via: null }, 2);
    const sent = fakeApi({
      'PATCH /api/steps/s1': { json: replyOf(released) },
      'PUT /api/flows/f1/route': { json: replyOf(routed({ source_side: 'top', via: null }, 3)) },
    });
    const { editor } = await editorOf(routed({ source_side: null, via: { aisle: 0 } }));
    // A move of the step whose answer releases the passage, then the departure chosen before that answer.
    const moved = editor.write(() => api.patch('/api/steps/{id}', { path: { id: 's1' }, body: { label: 'Déplacée' }, version: 1 }));
    const chosen = setFlowRoute(editor, 'f1', (route) => withSide(route, 'source', 'top'));
    expect(routeShown(editor, 'f1')).toEqual({ source: 'top', via: { aisle: 0 } });
    await Promise.all([moved, chosen]);
    expect(callsTo(sent, 'PUT', '/api/flows/f1/route').map((call) => [call.body, call.headers.get('If-Match')]))
      .toEqual([[{ source_side: 'top', target_side: null, via: null }, '"2"']]);
  });

  it('keeps the flow drawn with its route while another write is answered, and only its own write takes it away', async () => {
    let answer: () => void = () => undefined;
    const held = new Promise<void>((resolve) => {
      answer = resolve;
    });
    const sent = fakeApi({ 'PATCH /api/steps/s2': { json: replyOf(sampleMap()) }, 'PUT /api/flows/f1/route': { json: replyOf(routed({ source_side: null, via: { aisle: 1 } }, 2)) } });
    const { editor } = await editorOf(sampleMap());
    previewRoute(editor, 'f1', { via: { aisle: 1 } });
    await editor.write(() => api.patch('/api/steps/{id}', { path: { id: 's2' }, body: { label: 'Autre' }, version: 1 }));
    expect(editor.store.getState().routeDraft).toEqual({ flowId: 'f1', route: { via: { aisle: 1 } }, sent: false });
    const slow = editor.write(async () => {
      await held;
      return null;
    });
    const chosen = setFlowRoute(editor, 'f1', (route) => withPassage(route, { aisle: 1 }));
    expect(editor.store.getState().routeDraft).toEqual({ flowId: 'f1', route: { via: { aisle: 1 } }, sent: true });
    answer();
    await Promise.all([slow, chosen]);
    expect(callsTo(sent, 'PUT', '/api/flows/f1/route')).toHaveLength(1);
    expect(editor.store.getState().routeDraft).toBeNull();
  });

  it('draws the map again only when the route of the gesture changes', async () => {
    fakeApi({});
    const { editor } = await editorOf(sampleMap());
    previewRoute(editor, 'f1', { source: 'top' });
    const first = editor.store.getState().routeDraft;
    previewRoute(editor, 'f1', { source: 'top' });
    expect(editor.store.getState().routeDraft).toBe(first);
    previewRoute(editor, 'f1', { source: 'bottom' });
    expect(editor.store.getState().routeDraft).not.toBe(first);
  });
});
