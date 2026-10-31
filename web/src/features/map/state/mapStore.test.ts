// mapStore.test.ts
// The store of an editor: the reading of the map, a reading cancelled by the next one, its failure, the state not kept (annex B 7).

import type { Schema } from '../../../api/client.ts';
import { callsTo, fakeApi } from '../../../test/fakeApi.ts';
import { MAP_ID, mapOf } from '../../../test/maps.ts';
import { createMapStore, hostSource, type MapSource } from './mapStore.ts';

const history: Schema<'HistoryView'> = { undo: [{ operation_id: 'op1', kind: 'step.created', at: '2026-10-20T08:00:00Z' }], redo: [] };

/** A source whose readings the test answers one by one, in their order. */
function controlledSource() {
  const pending: { mapId: string; signal: AbortSignal; resolve: (reply: Schema<'MapReply'>) => void; reject: (failure: unknown) => void }[] = [];
  const source: MapSource = (mapId, signal) => new Promise((resolve, reject) => pending.push({ mapId, signal, resolve, reject }));
  return { source, pending };
}

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('the store of an editor (B-7-16)', () => {
  it('starts without a map, in the normal mode, with no source to link, nothing typed, and steps of type task (B-7-08, B-7-11)', () => {
    const state = createMapStore(controlledSource().source).getState();
    expect([state.map, state.history, state.loading, state.error, state.mode, state.linkSource, state.editing, state.newStepType]).toEqual([null, null, false, null, 'normal', null, null, 'task']);
  });

  it('starts with the Hand, nothing chosen, no sheet open, no Space held: none of it is kept (B-7-07, B-7-10)', () => {
    const state = createMapStore(controlledSource().source).getState();
    expect([state.tool, state.selection, state.openSheet, state.spaceHand]).toEqual(['hand', { steps: [], flows: [] }, null, false]);
  });

  it('keeps the map and the history read', async () => {
    const { source, pending } = controlledSource();
    const store = createMapStore(source);
    const loading = store.getState().load(MAP_ID);
    expect(store.getState().loading).toBe(true);
    pending[0]?.resolve({ map: mapOf({ name: 'Achats' }), history });
    await loading;
    expect([store.getState().map?.name, store.getState().history, store.getState().loading]).toEqual(['Achats', history, false]);
  });

  it('keeps the failure of a reading, and forgets it after a reading that succeeds', async () => {
    const { source, pending } = controlledSource();
    const store = createMapStore(source);
    const failed = store.getState().load(MAP_ID);
    pending[0]?.reject(new Error('down'));
    await failed;
    expect((store.getState().error as Error).message).toBe('down');
    const again = store.getState().load(MAP_ID);
    pending[1]?.resolve({ map: mapOf(), history });
    await again;
    expect([store.getState().error, store.getState().map?.id]).toEqual([null, MAP_ID]);
  });
});

describe('concurrent readings (B-7-18)', () => {
  it('cancel the reading running: only the last one is kept, even when the first answers after it', async () => {
    const { source, pending } = controlledSource();
    const store = createMapStore(source);
    const first = store.getState().load('m1');
    const second = store.getState().load('m2');
    expect([pending[0]?.signal.aborted, pending[1]?.signal.aborted]).toEqual([true, false]);
    pending[1]?.resolve({ map: mapOf({ name: 'Second' }), history });
    pending[0]?.resolve({ map: mapOf({ name: 'Premier' }), history });
    await Promise.all([first, second]);
    expect(store.getState().map?.name).toBe('Second');
  });

  it('a cancelled reading that fails leaves no failure behind', async () => {
    const { source, pending } = controlledSource();
    const store = createMapStore(source);
    const reading = store.getState().load('m1');
    store.getState().cancel();
    pending[0]?.reject(new DOMException('aborted', 'AbortError'));
    await reading;
    expect([pending[0]?.signal.aborted, store.getState().error]).toEqual([true, null]);
  });
});

describe('the reading from the host', () => {
  it('reads GET /api/maps/{id}', async () => {
    const sent = fakeApi({ [`GET /api/maps/${MAP_ID}`]: { json: { map: mapOf(), history } } });
    const reply = await hostSource(MAP_ID, new AbortController().signal);
    expect([reply.map.id, callsTo(sent, 'GET', `/api/maps/${MAP_ID}`).length]).toEqual([MAP_ID, 1]);
  });
});
