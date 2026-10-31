// dissolution.test.ts
// The detail brought back into the flow and its remainder, without React: the calls, where « Finish » stored things, the frame stored on its own once, the panel closed with the frame (annex B 2.9).

import { callsTo, fakeApi } from '../../../../test/fakeApi.ts';
import { editorOf } from '../../../../test/editorStore.ts';
import { frameOf, replyOf, sampleMap } from '../../../../test/maps.ts';
import { assignRemainder, dissolveStep, finishFrame, removeRemainder, watchFrame } from './dissolution.ts';

afterEach(() => {
  vi.unstubAllGlobals();
});

const framed = (stepIds: readonly string[] = ['s2', 's3']) => sampleMap({ dissolution: frameOf(stepIds) });
const stored = [{ kind: 'document', object_id: 'o', name: 'Devis', step_id: 's3', step_code: 'VTE-E003' }] as const;
const finishReply = (arrangements: readonly unknown[]) => ({ ...replyOf(sampleMap()), arrangements });

describe('the dissolution and its remainder (B-2.9-16, B-2.9-29, B-2.9-31, B-2.9-32)', () => {
  it('brings the detail of a step back into the flow, with the version of the step; nothing for a step gone', async () => {
    const sent = fakeApi({ 'POST /api/steps/s2/dissolve': { json: replyOf(framed()) } });
    const { editor } = await editorOf(sampleMap());
    await dissolveStep(editor, 's2');
    await dissolveStep(editor, 'gone');
    expect(sent.map((call) => [call.method, call.path, call.headers.get('If-Match')])).toEqual([['POST', '/api/steps/s2/dissolve', '"1"']]);
    expect(editor.store.getState().map?.dissolution?.frame_step_ids).toEqual(['s2', 's3']);
  });

  it('assigns an element to a step, a pain point to the map, and deletes an element for good, all on the open frame', async () => {
    const sent = fakeApi({ 'POST /api/dissolutions/d1/assign': { json: { ...replyOf(framed()), frame: null } }, 'POST /api/dissolutions/d1/remove': { json: { ...replyOf(framed()), frame: null } } });
    const { editor } = await editorOf(framed());
    await assignRemainder(editor, 'document', 'o', 's3');
    await assignRemainder(editor, 'pain_point', 'p', null);
    await removeRemainder(editor, 'usage', 'u');
    expect(sent.map((call) => [call.path, call.body])).toEqual([
      ['/api/dissolutions/d1/assign', { kind: 'document', object_id: 'o', step_id: 's3', from_step_id: null }],
      ['/api/dissolutions/d1/assign', { kind: 'pain_point', object_id: 'p', step_id: null, from_step_id: null }],
      ['/api/dissolutions/d1/remove', { kind: 'usage', object_id: 'u' }],
    ]);
  });

  it('sends nothing for a remainder when the map has no frame open', async () => {
    const sent = fakeApi({});
    const { editor } = await editorOf(sampleMap());
    await assignRemainder(editor, 'document', 'o', 's3');
    await removeRemainder(editor, 'document', 'o');
    await finishFrame(editor);
    expect(sent).toEqual([]);
  });
});

describe('« Finish » (B-2.9-36 to B-2.9-40)', () => {
  it('shows where the remainder went', async () => {
    fakeApi({ 'POST /api/dissolutions/d1/finish': { json: finishReply(stored) } });
    const { editor } = await editorOf(framed());
    editor.floating.getState().open({ kind: 'remainder' });
    await finishFrame(editor);
    expect(editor.floating.getState().floating).toEqual({ kind: 'arrangements', code: 'VTE-E009', arrangements: stored, auto: false });
  });

  it('with nothing stored, shows nothing: the panel closes with the frame (« Fermer le cadre »)', async () => {
    const sent = fakeApi({ 'POST /api/dissolutions/d1/finish': { json: finishReply([]) } });
    const { editor } = await editorOf(framed());
    const stop = watchFrame(editor);
    editor.floating.getState().open({ kind: 'remainder' });
    await finishFrame(editor);
    stop();
    expect([callsTo(sent, 'POST', '/api/dissolutions/d1/finish').length, editor.floating.getState().floating]).toEqual([1, null]);
  });

  it('refused, leaves the panel open', async () => {
    fakeApi({ 'POST /api/dissolutions/d1/finish': { refused: 409, code: 'CARTE_MODIFIEE' }, [`GET /api/maps/${framed().id}`]: { json: { map: framed(), history: { undo: [], redo: [] } } } });
    const { editor, notes } = await editorOf(framed());
    editor.floating.getState().open({ kind: 'remainder' });
    await finishFrame(editor);
    expect([editor.floating.getState().floating, notes.length]).toEqual([{ kind: 'remainder' }, 1]);
  });
});

describe('the frame watched (B-2.9-38 to B-2.9-40, B-8.1-15)', () => {
  it('a frame of one step on a map that may be changed is finished on its own, once a dissolution, and says so', async () => {
    const sent = fakeApi({ 'POST /api/dissolutions/d1/finish': { json: { ...finishReply(stored), map: framed(['s2']) } } });
    const { editor } = await editorOf(framed(['s2']));
    const stop = watchFrame(editor);
    await vi.waitFor(() => {
      expect(editor.floating.getState().floating).toEqual({ kind: 'arrangements', code: 'VTE-E009', arrangements: stored, auto: true });
    });
    editor.store.setState({ map: framed(['s2']) });
    stop();
    expect(callsTo(sent, 'POST', '/api/dissolutions/d1/finish')).toHaveLength(1);
  });

  it('nothing on its own for a frame of two steps, nor for a reader', async () => {
    const sent = fakeApi({});
    const two = await editorOf(framed());
    const reader = await editorOf(sampleMap({ dissolution: frameOf(['s2']), rights: { structure: false, role: 'reader' } }));
    const stops = [watchFrame(two.editor), watchFrame(reader.editor)];
    await Promise.resolve();
    stops.forEach((stop) => { stop(); });
    expect(sent).toEqual([]);
  });

  it('the frame closed closes its panel, and only that panel', async () => {
    fakeApi({});
    const { editor } = await editorOf(framed());
    const stop = watchFrame(editor);
    editor.floating.getState().open({ kind: 'remainder' });
    editor.store.setState({ map: sampleMap() });
    expect(editor.floating.getState().floating).toBeNull();
    editor.floating.getState().open({ kind: 'matrix' });
    editor.store.setState({ map: sampleMap({ name: 'Ventes 2' }) });
    stop();
    expect(editor.floating.getState().floating).toEqual({ kind: 'matrix' });
  });
});
