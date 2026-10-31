// steps.test.ts
// The writes of the steps, flows and lanes: what is sent, and what the editor does with the reply (annex B 2.1, 2.3, 2.4, 2.12).

import { i18next } from '../../../../shared/i18n/i18n.ts';
import { callsTo, fakeApi } from '../../../../test/fakeApi.ts';
import { editorOf } from '../../../../test/editorStore.ts';
import { laneOf, MAP_ID, mapOf, replyOf, sampleMap, stepOf } from '../../../../test/maps.ts';
import { addFlow, relabelFlow, reverseFlow } from './flows.ts';
import { addLane, deleteLane, deleteLaneWith, orderLanes, updateLane } from './lanes.ts';
import { createStep, deleteDetail, deleteMacro, detailStep, groupSteps, insertStep } from './steps.ts';

beforeEach(async () => {
  await i18next.changeLanguage('fr');
});

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('the creation of steps (B-2.3-09, B-2.1-15, B-2.1-17, B-2.3-44)', () => {
  it('a new step takes the type of the next step, in the cell given, its name is typed at once, and the type goes back to « Étape » (AR-152)', async () => {
    const sent = fakeApi({ [`POST /api/maps/${MAP_ID}/steps`]: { json: replyOf(sampleMap(), { id: 'new' }) } });
    const { editor } = await editorOf(sampleMap());
    editor.store.setState({ newStepType: 'decision' });
    await createStep(editor, { laneId: 'l2', column: 4, row: 1 });
    expect(callsTo(sent, 'POST', `/api/maps/${MAP_ID}/steps`)[0]?.body).toEqual({ lane_id: 'l2', column: 4, row: 1, label: 'Nouvelle étape', type: 'decision' });
    expect([editor.store.getState().editing, editor.store.getState().newStepType]).toEqual(['new', 'task']);
  });

  it('a creation or an insertion refused keeps the type chosen, to try again (AR-152)', async () => {
    fakeApi({ [`POST /api/maps/${MAP_ID}/steps`]: { refused: 409, code: 'CELLULE_OCCUPEE' }, 'POST /api/steps/s2/insert': { refused: 409, code: 'CELLULE_OCCUPEE' } });
    const { editor } = await editorOf(sampleMap());
    editor.store.setState({ newStepType: 'wait' });
    await createStep(editor, { laneId: 'l2', column: 1, row: 0 });
    await insertStep(editor, 's2', 'after');
    expect([editor.store.getState().editing, editor.store.getState().newStepType]).toEqual([null, 'wait']);
  });

  it('an event or a gateway that joins is named after its type', async () => {
    const sent = fakeApi({ [`POST /api/maps/${MAP_ID}/steps`]: { json: replyOf(sampleMap(), { id: 'new' }) } });
    const { editor } = await editorOf(sampleMap());
    editor.store.setState({ newStepType: 'end' });
    await createStep(editor, { laneId: 'l1', column: 0, row: 0 });
    expect(callsTo(sent, 'POST', `/api/maps/${MAP_ID}/steps`)[0]?.body).toMatchObject({ label: 'Fin', type: 'end' });
  });

  it('an insertion next to a step names its position and the type of the next step, which then goes back to « Étape » (AR-152)', async () => {
    const sent = fakeApi({ 'POST /api/steps/s2/insert': { json: replyOf(sampleMap(), { id: 'new' }) } });
    const { editor } = await editorOf(sampleMap());
    editor.store.setState({ newStepType: 'end' });
    await insertStep(editor, 's2', 'below');
    expect(callsTo(sent, 'POST', '/api/steps/s2/insert')[0]?.body).toEqual({ position: 'below', label: 'Fin', type: 'end' });
    expect([editor.store.getState().editing, editor.store.getState().newStepType]).toEqual(['new', 'task']);
  });
});

describe('the detail of steps (B-2.1-28, B-2.1-29, B-2.3-39, B-2.4-18)', () => {
  it('a step detailed opens its new map; a map deeper than advised is said', async () => {
    const sent = fakeApi({ 'POST /api/steps/s2/detail': { json: replyOf(sampleMap(), { id: 'sub', warnings: ['PROFONDEUR_DEPASSEE'] }) } });
    const { editor, notes, opened } = await editorOf(sampleMap({ depth: 4 }));
    await detailStep(editor, 's2');
    expect(callsTo(sent, 'POST', '/api/steps/s2/detail')[0]?.headers.get('If-Match')).toBe('"1"');
    expect([opened, notes]).toEqual([['/maps/sub'], [['Cette carte est au niveau 5. Au-delà de 4 niveaux, la lecture devient difficile.', undefined]]]);
  });

  it('a step whose empty detail exists already: its map opened again, nothing said (B-2.9-02, SOUS_CARTE_EXISTANTE)', async () => {
    fakeApi({ 'POST /api/steps/s2/detail': { json: replyOf(sampleMap(), { id: 'kept', warnings: ['SOUS_CARTE_EXISTANTE'], operation_id: null }) } });
    const { editor, notes, opened } = await editorOf(sampleMap());
    await detailStep(editor, 's2');
    expect([opened, notes]).toEqual([['/maps/kept'], []]);
  });

  it('steps gathered in a new step « Sous-processus »: it is chosen and its name typed at once (B-2.9-09, B-2.9-10)', async () => {
    const sent = fakeApi({ [`POST /api/maps/${MAP_ID}/group`]: { json: replyOf(sampleMap(), { id: 's2' }) } });
    const { editor } = await editorOf(sampleMap());
    await groupSteps(editor, ['s2', 's3']);
    expect(callsTo(sent, 'POST', `/api/maps/${MAP_ID}/group`)[0]?.body).toEqual({ steps: ['s2', 's3'], label: 'Sous-processus' });
    expect([editor.store.getState().selection, editor.store.getState().editing]).toEqual([{ steps: ['s2'], flows: [] }, 's2']);
  });

  it('a detailed step deleted names what becomes of its map, and stitches the flow', async () => {
    const sent = fakeApi({ 'DELETE /api/steps/s2': { json: replyOf(sampleMap()) } });
    const { editor } = await editorOf(sampleMap());
    await deleteMacro(editor, 's2', 'reattach', 's3');
    const [call] = callsTo(sent, 'DELETE', '/api/steps/s2');
    expect([call?.query.get('sub_map'), call?.query.get('to'), call?.query.get('stitch'), call?.headers.get('If-Match')]).toEqual(['reattach', 's3', 'true', '"1"']);
  });

  it('the detail deleted: its map read for its version, then deleted', async () => {
    const map = sampleMap();
    const withDetail = { ...map, steps: map.steps.map((step) => (step.id === 's2' ? { ...step, sub_map_id: 'sub', sub_map_empty: false } : step)) };
    const sent = fakeApi({ 'GET /api/maps/sub': { json: { map: mapOf({ id: 'sub', version: 9 }), history: { undo: [], redo: [] } } }, 'DELETE /api/maps/sub': { json: replyOf(map) } });
    const { editor } = await editorOf(withDetail);
    await deleteDetail(editor, 's2');
    expect(callsTo(sent, 'DELETE', '/api/maps/sub')[0]?.headers.get('If-Match')).toBe('"9"');
  });

  it('the detail of a step without one, or of a step gone: nothing read, nothing deleted', async () => {
    const sent = fakeApi({});
    const { editor, notes } = await editorOf(sampleMap());
    await deleteDetail(editor, 's2');
    await deleteDetail(editor, 'gone');
    expect([sent, notes]).toEqual([[], []]);
  });
});

describe('the flows (B-2.3-17, B-2.3-21, B-2.3-33, B-2.3-34)', () => {
  it('link two steps; never a step to itself', async () => {
    const sent = fakeApi({ [`POST /api/maps/${MAP_ID}/flows`]: { json: replyOf(sampleMap()) } });
    const { editor } = await editorOf(sampleMap());
    await addFlow(editor, 's1', 's1');
    await addFlow(editor, 's1', 's4');
    expect(callsTo(sent, 'POST', `/api/maps/${MAP_ID}/flows`).map((call) => call.body)).toEqual([{ source_step_id: 's1', target_step_id: 's4', label: null }]);
  });

  it('an empty label removes the label; the same label sends nothing', async () => {
    const sent = fakeApi({ 'PATCH /api/flows/f3': { json: replyOf(sampleMap()) } });
    const { editor } = await editorOf(sampleMap());
    await relabelFlow(editor, 'f3', ' oui ');
    await relabelFlow(editor, 'f3', '   ');
    expect(callsTo(sent, 'PATCH', '/api/flows/f3').map((call) => [call.body, call.headers.get('If-Match')])).toEqual([[{ label: null }, '"1"']]);
  });

  it('a flow reversed with its version, a flow gone sends nothing; the refusal of a reverse already drawn is said (AR-155)', async () => {
    const sent = fakeApi({ 'POST /api/flows/f3/reverse': [{ json: replyOf(sampleMap()) }, { refused: 409, code: 'LIEN_DOUBLON', message: 'Ces deux étapes sont déjà reliées dans ce sens.' }] });
    const { editor, notes } = await editorOf(sampleMap());
    await reverseFlow(editor, 'f3');
    await reverseFlow(editor, 'gone');
    await reverseFlow(editor, 'f3');
    expect(callsTo(sent, 'POST', '/api/flows/f3/reverse').map((call) => call.headers.get('If-Match'))).toEqual(['"1"', '"1"']);
    expect(notes).toEqual([['Ces deux étapes sont déjà reliées dans ce sens.', 'error']]);
  });
});

describe('the lanes (B-2.4-46 to B-2.4-51, B-2.12-14, B-2.12-21, B-2.12-22)', () => {
  it('a new lane « Acteur N », a person, at its place, its name typed at once', async () => {
    const sent = fakeApi({ [`POST /api/maps/${MAP_ID}/lanes`]: { json: replyOf(sampleMap(), { id: 'l9' }) } });
    const { editor } = await editorOf(sampleMap());
    await addLane(editor, 1);
    expect(callsTo(sent, 'POST', `/api/maps/${MAP_ID}/lanes`)[0]?.body).toEqual({ name: 'Acteur 3', type: 'person', color: null, sort_order: 1, actor_user_id: null, translations: null });
    expect(editor.store.getState().editing).toBe('lane:l9');
  });

  it('a change sends only what changes; the order only when it changes', async () => {
    const sent = fakeApi({ 'PATCH /api/lanes/l1': { json: replyOf(sampleMap()) }, [`PUT /api/maps/${MAP_ID}/lane-order`]: { json: replyOf(sampleMap()) } });
    const { editor } = await editorOf(sampleMap());
    await updateLane(editor, 'l1', { name: 'Commercial', type: 'system' });
    await orderLanes(editor, ['l1', 'l2']);
    await orderLanes(editor, ['l2', 'l1']);
    expect(callsTo(sent, 'PATCH', '/api/lanes/l1').map((call) => call.body)).toEqual([{ type: 'system' }]);
    expect(callsTo(sent, 'PUT', `/api/maps/${MAP_ID}/lane-order`).map((call) => call.body)).toEqual([['l2', 'l1']]);
  });

  it('an empty lane goes at once; a lane with steps asks first; the last lane stays', async () => {
    const sent = fakeApi({ 'DELETE /api/lanes/l3': { json: replyOf(sampleMap()) }, 'DELETE /api/lanes/l2': { json: replyOf(sampleMap()) } });
    const { editor } = await editorOf(sampleMap({ lanes: [...sampleMap().lanes, laneOf('l3', 2)] }));
    deleteLane(editor, 'l3');
    deleteLane(editor, 'l2');
    expect(editor.floating.getState().floating).toEqual({ kind: 'lane-delete', laneId: 'l2' });
    await deleteLaneWith(editor, 'l2', 'move', 'l1');
    expect(callsTo(sent, 'DELETE', '/api/lanes/l3')).toHaveLength(1);
    expect([...(callsTo(sent, 'DELETE', '/api/lanes/l2')[0]?.query.entries() ?? [])]).toEqual([['steps', 'move'], ['to', 'l1']]);
    const alone = await editorOf(mapOf({ lanes: [laneOf('A', 0)], steps: [stepOf('a', 'A', 0)] }));
    deleteLane(alone.editor, 'A');
    expect(alone.editor.floating.getState().floating).toBeNull();
  });
});
