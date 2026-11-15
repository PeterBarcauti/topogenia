// editor.test.ts
// The editor of a map: what it composes; its writes one after the other, applied when the host answers, a refusal said, a conflict read again; the selection kept (annex B 2.6, B-2.3-28, B-7-16).

import { i18next } from '../../../shared/i18n/i18n.ts';
import { toasts } from '../../../shared/ui/toasts.ts';
import { memoryStorage } from '../../../test/editor.tsx';
import { callsTo, fakeApi } from '../../../test/fakeApi.ts';
import { editorOf } from '../../../test/editorStore.ts';
import { flowOf, laneOf, MAP_ID, mapOf, replyOf, sampleMap, stepOf } from '../../../test/maps.ts';
import { addFlow, relabelFlow } from './actions/flows.ts';
import { updateStep } from './actions/steps.ts';
import { createMapEditor } from './editor.ts';

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('the editor of a map (B-7-16)', () => {
  it('composes the store of its map, the preferences read from the browser, the floating element and the notifications, its own for each map', () => {
    const notes: string[] = [];
    const editor = createMapEditor({ storage: memoryStorage({ 'app.showCodes': '0', 'app.ribbonCollapsed': '1' }), notify: (text) => notes.push(text) });
    expect(editor.preferences.getState()).toMatchObject({ showCodes: false, alwaysShowPictos: true, showRaci: false, ribbonCollapsed: true });
    expect([editor.store.getState().map, editor.store.getState().replaying, editor.floating.getState().floating, editor.admin]).toEqual([null, false, null, false]);
    editor.notify('Bonjour');
    expect(notes).toEqual(['Bonjour']);
    const other = createMapEditor({ storage: null, admin: true });
    expect([other.store === editor.store, other.preferences === editor.preferences, other.floating === editor.floating, other.admin]).toEqual([false, false, false, true]);
    expect(other.preferences.getState()).toMatchObject({ showCodes: true, ribbonCollapsed: false });
  });

  it('without a way of its own, it notifies in the notifications of the application', () => {
    createMapEditor({ storage: null }).notify('Tout est enregistré.', 'success');
    expect(toasts.getState().toasts.map((toast) => [toast.text, toast.kind])).toEqual([['Tout est enregistré.', 'success']]);
    toasts.setState({ toasts: [] });
  });
});

const renamed = (label: string) => {
  const map = sampleMap();
  return { ...map, steps: map.steps.map((step) => (step.id === 's2' ? { ...step, label, version: 2 } : step)) };
};

describe('the writes of the map (B-2.6-09 to B-2.6-13)', () => {
  it('are sent one after the other: the second waits for the answer to the first (B-2.6-09)', async () => {
    const sent = fakeApi({ 'PATCH /api/steps/s2': [{ pending: true }], 'PATCH /api/flows/f3': { json: replyOf(sampleMap()) } });
    const { editor } = await editorOf(sampleMap());
    void updateStep(editor, 's2', { label: 'Contrôler' });
    void relabelFlow(editor, 'f3', 'si oui');
    await vi.waitFor(() => {
      expect(callsTo(sent, 'PATCH', '/api/steps/s2')).toHaveLength(1);
    });
    await new Promise((resolve) => setTimeout(resolve, 20));
    expect(callsTo(sent, 'PATCH', '/api/flows/f3')).toHaveLength(0);
  });

  it('change nothing before the host answers, then take the map of its reply (B-2.6-11)', async () => {
    let answer: (response: Response) => void = () => undefined;
    vi.stubGlobal('fetch', vi.fn(() => new Promise<Response>((resolve) => { answer = resolve; })));
    const { editor } = await editorOf(sampleMap());
    const writing = updateStep(editor, 's2', { label: 'Contrôler' });
    await vi.waitFor(() => {
      expect(fetch).toHaveBeenCalled();
    });
    expect(editor.store.getState().map?.steps.find((step) => step.id === 's2')?.label).toBe('Vérifier la demande');
    answer(new Response(JSON.stringify(replyOf(renamed('Contrôler'), { history: { undo: [{ operation_id: 'o', kind: 'modifier_etape', at: '2026-10-28T08:00:00Z' }], redo: [] } })), { status: 200 }));
    await writing;
    expect(editor.store.getState().map?.steps.find((step) => step.id === 's2')?.label).toBe('Contrôler');
    expect(editor.store.getState().history?.undo).toHaveLength(1);
  });

  it('show the message of a refusal (B-2.6-12) and leave the map as it was', async () => {
    fakeApi({ 'PATCH /api/steps/s2': { refused: 422, code: 'CHAMP_OBLIGATOIRE', message: 'Le libellé est obligatoire.' } });
    const { editor, notes } = await editorOf(sampleMap());
    await updateStep(editor, 's2', { label: 'x' });
    expect(notes).toEqual([['Le libellé est obligatoire.', 'error']]);
    expect(editor.store.getState().map?.steps.find((step) => step.id === 's2')?.label).toBe('Vérifier la demande');
  });

  it('without an answer of the host, say so in the language shown, and leave the map as it was, not read again (B-0-19)', async () => {
    await i18next.changeLanguage('fr');
    vi.stubGlobal('fetch', vi.fn(() => Promise.reject(new TypeError('Failed to fetch'))));
    const { editor, notes } = await editorOf(sampleMap(), renamed('Lu de nouveau'));
    const before = editor.store.getState().map;
    await addFlow(editor, 's1', 's4');
    expect(fetch).toHaveBeenCalledTimes(1);
    expect(notes).toEqual([[i18next.t('errors.RESEAU'), 'error']]);
    expect(notes[0]?.[0]).toContain('ne répond pas');
    expect(editor.store.getState().map).toBe(before);
  });

  it('read the map again after a conflict (B-2.6-13)', async () => {
    fakeApi({ 'PATCH /api/steps/s2': { refused: 409, code: 'CONFLIT_VERSION', message: 'Modifiée entre-temps.' } });
    const { editor, notes } = await editorOf(sampleMap(), renamed('Lu de nouveau'));
    await updateStep(editor, 's2', { label: 'x' });
    expect(notes).toEqual([['Modifiée entre-temps.', 'error']]);
    expect(editor.store.getState().map?.steps.find((step) => step.id === 's2')?.label).toBe('Lu de nouveau');
  });

  it('read the map again when the reply carries another map, or none', async () => {
    fakeApi({ 'PATCH /api/steps/s2': { json: replyOf({ ...sampleMap(), id: 'other' }) }, 'PATCH /api/flows/f3': { json: replyOf(null) } });
    const { editor } = await editorOf(sampleMap(), renamed('Première relecture'), renamed('Seconde relecture'));
    await updateStep(editor, 's2', { label: 'x' });
    expect(editor.store.getState().map?.steps.find((step) => step.id === 's2')?.label).toBe('Première relecture');
    await relabelFlow(editor, 'f3', 'si');
    expect(editor.store.getState().map?.steps.find((step) => step.id === 's2')?.label).toBe('Seconde relecture');
  });

  it('send If-Match with the version of the object changed, and only the fields that change', async () => {
    const sent = fakeApi({ 'PATCH /api/steps/s2': { json: replyOf(sampleMap()) } });
    const { editor } = await editorOf(sampleMap());
    await updateStep(editor, 's2', { label: 'Vérifier la demande', type: 'wait' });
    await updateStep(editor, 's2', { label: 'Vérifier la demande', type: 'task' });
    const calls = callsTo(sent, 'PATCH', '/api/steps/s2');
    expect(calls.map((call) => [call.body, call.headers.get('If-Match')])).toEqual([[{ type: 'wait' }, '"1"']]);
  });
});

describe('the state the editor keeps through the readings (B-2.3-28)', () => {
  it('the selection survives a new map, without the steps and flows gone; so do the sheet open, the source and the name typed', async () => {
    const map = mapOf({ lanes: [laneOf('A', 0)], steps: [stepOf('a', 'A', 0), stepOf('b', 'A', 1)], flows: [flowOf('ab', 'a', 'b')] });
    const { editor } = await editorOf(map, { ...map, steps: [stepOf('a', 'A', 0)], flows: [] });
    editor.store.setState({ selection: { steps: ['a', 'b'], flows: ['ab'] }, openSheet: 'b', linkSource: 'a', editing: 'lane:A' });
    await editor.store.getState().load(MAP_ID);
    const state = editor.store.getState();
    expect([state.selection, state.openSheet, state.linkSource, state.editing]).toEqual([{ steps: ['a'], flows: [] }, null, 'a', 'lane:A']);
  });
});
