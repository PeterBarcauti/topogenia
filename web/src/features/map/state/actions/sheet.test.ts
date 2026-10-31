// sheet.test.ts
// What the sheet asks of the editor of the map: the gauge shown without reading the map, the person responsible by a write of the map, the map read again after the writes, one panel docked (annex B 4, B-2.6-11).

import { callsTo, fakeApi } from '../../../../test/fakeApi.ts';
import { editorOf } from '../../../../test/editorStore.ts';
import { replyOf, sampleMap, sheetOf } from '../../../../test/maps.ts';
import { assignStep, showSheetSummary, watchSidePanel } from './sheet.ts';

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('the gauge of the step (B-4-50, B-2.6-11)', () => {
  it('takes the summary of the sheet on its step without reading the map, and changes nothing for the same summary or a step gone', async () => {
    const { editor } = await editorOf(sampleMap(), sampleMap({ name: 'Ventes relues' }));
    const summary = sheetOf({ status: 'draft', completeness: 40, frequency: 'weekly', duration_minutes: 20 });
    showSheetSummary(editor, 's2', summary);
    await new Promise((resolve) => setTimeout(resolve, 20));
    const map = editor.store.getState().map;
    expect([map?.steps.find((step) => step.id === 's2')?.sheet, map?.steps.find((step) => step.id === 's3')?.sheet, map?.name]).toEqual([summary, sheetOf(), 'Ventes']);
    showSheetSummary(editor, 's2', { ...summary });
    showSheetSummary(editor, 'gone', summary);
    expect(editor.store.getState().map).toBe(map);
  });
});

describe('the person responsible (B-4-15)', () => {
  it('is sent by PATCH with the version of the step, the map of the reply is taken; null gives the sheet to nobody in particular', async () => {
    const answered = sampleMap({ steps: sampleMap().steps.map((step) => (step.id === 's2' ? { ...step, assignee_user_id: 'u1', version: 2 } : step)) });
    const sent = fakeApi({ 'PATCH /api/steps/s2': { json: replyOf(answered) } });
    const { editor } = await editorOf(sampleMap());
    expect(await assignStep(editor, 's2', 'u1')).toBe(true);
    expect(await assignStep(editor, 's2', null)).toBe(true);
    const calls = callsTo(sent, 'PATCH', '/api/steps/s2');
    expect(calls.map((call) => [call.body, call.headers.get('If-Match')])).toEqual([[{ assignee_user_id: 'u1' }, '"1"'], [{ assignee_user_id: null }, '"2"']]);
    expect(editor.store.getState().map?.steps.find((step) => step.id === 's2')?.assignee_user_id).toBe('u1');
  });

  it('a refusal, or a step gone, gives false', async () => {
    fakeApi({ 'PATCH /api/steps/s2': { refused: 403, code: 'DROITS_INSUFFISANTS' } });
    const { editor, notes } = await editorOf(sampleMap());
    expect([await assignStep(editor, 's2', 'u1'), await assignStep(editor, 'gone', 'u1')]).toEqual([false, false]);
    expect(notes).toHaveLength(1);
  });
});

describe('the map read again after a write of the sheet', () => {
  it('waits for the writes sent before it, then reads the map of the editor', async () => {
    let answer: (response: Response) => void = () => undefined;
    vi.stubGlobal('fetch', vi.fn(() => new Promise<Response>((resolve) => { answer = resolve; })));
    const changed = sampleMap({ name: 'Ventes relues' });
    const { editor } = await editorOf(sampleMap(), changed);
    const writing = assignStep(editor, 's2', 'u1');
    const reading = editor.reload();
    await new Promise((resolve) => setTimeout(resolve, 20));
    expect(editor.store.getState().map?.name).toBe('Ventes');
    answer(new Response(JSON.stringify(replyOf(sampleMap())), { status: 200 }));
    await writing;
    await reading;
    expect(editor.store.getState().map?.name).toBe('Ventes relues');
  });
});

describe('one panel docked on the right (spec 9.5)', () => {
  it('a sheet opened closes the remainder, the remainder opened closes the sheet; the other floating elements leave the sheet open', async () => {
    const { editor } = await editorOf(sampleMap());
    const stop = watchSidePanel(editor);
    editor.floating.getState().open({ kind: 'remainder' });
    editor.store.setState({ openSheet: 's2' });
    expect([editor.store.getState().openSheet, editor.floating.getState().floating]).toEqual(['s2', null]);
    editor.floating.getState().open({ kind: 'matrix' });
    editor.store.setState({ openSheet: 's3' });
    expect([editor.store.getState().openSheet, editor.floating.getState().floating]).toEqual(['s3', { kind: 'matrix' }]);
    editor.floating.getState().open({ kind: 'remainder' });
    expect([editor.store.getState().openSheet, editor.floating.getState().floating]).toEqual([null, { kind: 'remainder' }]);
    stop();
    editor.store.setState({ openSheet: 's3' });
    expect(editor.floating.getState().floating).toEqual({ kind: 'remainder' });
  });

  it('the value stream layer turned on closes the sheet, the remainder and the assistant; its panel lies under those opened later (spec 3.0 section 15.2)', async () => {
    const { editor } = await editorOf(sampleMap());
    const stop = watchSidePanel(editor);
    editor.store.setState({ openSheet: 's2' });
    editor.assistant.getState().setOpen(true);
    editor.floating.getState().open({ kind: 'remainder' });
    editor.preferences.getState().set('showValueStream', true);
    expect([editor.store.getState().openSheet, editor.floating.getState().floating, editor.assistant.getState().open]).toEqual([null, null, false]);
    editor.store.setState({ openSheet: 's3' });
    editor.assistant.getState().setOpen(true);
    expect([editor.store.getState().openSheet, editor.assistant.getState().open, editor.preferences.getState().showValueStream]).toEqual([null, true, true]);
    stop();
  });
});
