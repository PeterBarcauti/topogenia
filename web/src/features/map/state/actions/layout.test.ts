// layout.test.ts
// The writes of the places and of the deletion: a drag, the arrows, the Align wand, what is chosen deleted (annex B 2.3, 2.5, 2.8).

import { i18next } from '../../../../shared/i18n/i18n.ts';
import { callsTo, fakeApi } from '../../../../test/fakeApi.ts';
import { editorOf } from '../../../../test/editorStore.ts';
import { flowOf, laneOf, MAP_ID, mapOf, replyOf, sampleMap, stepOf } from '../../../../test/maps.ts';
import { align, moveByKeys, moveSteps } from './layout.ts';
import { deleteChosen, deleteSelection, selectAll } from './selection.ts';

beforeEach(async () => {
  await i18next.changeLanguage('fr');
});

afterEach(() => {
  vi.unstubAllGlobals();
});

const map = mapOf({ lanes: [laneOf('A', 0), laneOf('B', 1)], steps: [stepOf('1', 'A', 0), stepOf('2', 'A', 1), stepOf('3', 'B', 2)], flows: [flowOf('f', '1', '2')] });
const LAYOUT = `/api/maps/${MAP_ID}/layout`;
const MOVE = `/api/maps/${MAP_ID}/move`;
const routes = () => fakeApi({ [`PUT ${LAYOUT}`]: { json: replyOf(map) }, [`POST ${MOVE}`]: { json: replyOf(map) } });

describe('the drop of a drag (B-2.3-24 to B-2.3-26)', () => {
  it('sends only the useful moves, sorted by column; a block on free cells as it is, in one layout', async () => {
    const sent = routes();
    const { editor } = await editorOf(map);
    await moveSteps(editor, [{ id: '2', laneId: 'A', column: 6, row: 0 }, { id: '1', laneId: 'A', column: 5, row: 0 }, { id: '3', laneId: 'B', column: 2, row: 0 }]);
    expect(callsTo(sent, 'PUT', LAYOUT).map((call) => call.body)).toEqual([{
      lane_order: null, purpose: 'move', steps: [{ id: '1', lane_id: 'A', column: 5, row: 0 }, { id: '2', lane_id: 'A', column: 6, row: 0 }],
    }]);
  });

  it('one step, or a block on a cell taken: one grouped move, the host shifting what is in the way (AR-44)', async () => {
    const sent = routes();
    const { editor } = await editorOf(map);
    await moveSteps(editor, [{ id: '1', laneId: 'A', column: 4, row: 0 }]);
    await moveSteps(editor, [{ id: '1', laneId: 'B', column: 2, row: 0 }, { id: '2', laneId: 'B', column: 3, row: 0 }]);
    expect(callsTo(sent, 'POST', MOVE).map((call) => call.body)).toEqual([
      { steps: [{ id: '1', lane_id: 'A', column: 4, row: 0 }] },
      { steps: [{ id: '1', lane_id: 'B', column: 2, row: 0 }, { id: '2', lane_id: 'B', column: 3, row: 0 }] },
    ]);
    expect(callsTo(sent, 'PUT', LAYOUT)).toHaveLength(0);
  });

  it('a drop on its own cell sends nothing', async () => {
    const sent = routes();
    const { editor } = await editorOf(map);
    await moveSteps(editor, [{ id: '1', laneId: 'A', column: 0, row: 0 }]);
    expect(sent).toHaveLength(0);
  });
});

describe('the arrows of the keyboard (B-2.5-18 to B-2.5-20)', () => {
  it('move the steps chosen by a column or to the next lane', async () => {
    const sent = routes();
    const { editor } = await editorOf(map);
    await moveByKeys(editor, ['3'], 0, -1);
    expect(callsTo(sent, 'POST', MOVE)[0]?.body).toEqual({ steps: [{ id: '3', lane_id: 'A', column: 2, row: 0 }] });
  });

  it('send nothing when the move is refused', async () => {
    const sent = routes();
    const { editor } = await editorOf(map);
    await moveByKeys(editor, ['1', '2'], -1, 0);
    await moveByKeys(editor, ['3'], 0, 1);
    expect(sent).toHaveLength(0);
  });
});

describe('the Align wand (B-2.8-15 to B-2.8-17)', () => {
  it('sends the steps that move in one layout named align, and says how many', async () => {
    const sent = routes();
    const { editor, notes } = await editorOf(mapOf({ lanes: [laneOf('A', 0)], steps: [stepOf('1', 'A', 0), stepOf('2', 'A', 4)], flows: [flowOf('f', '1', '2')] }));
    await align(editor);
    expect(callsTo(sent, 'PUT', LAYOUT)[0]?.body).toEqual({ lane_order: null, purpose: 'align', steps: [{ id: '2', lane_id: 'A', column: 1, row: 0 }] });
    expect(notes).toEqual([['Carte alignée : 1 étape replacée.', 'success']]);
  });

  it('says « déjà alignée » and sends nothing when no step moves', async () => {
    const sent = routes();
    const { editor, notes } = await editorOf(mapOf({ lanes: [laneOf('A', 0)], steps: [stepOf('1', 'A', 0), stepOf('2', 'A', 1)], flows: [flowOf('f', '1', '2')] }));
    await align(editor);
    expect([sent, notes]).toEqual([[], [['La carte est déjà alignée.', 'info']]]);
  });
});

describe('the deletion of what is chosen (B-2.3-36 to B-2.3-40)', () => {
  const macro = () => {
    const sample = sampleMap();
    return { ...sample, steps: sample.steps.map((step) => (step.id === 's2' ? { ...step, sub_map_id: 'sub', sub_map_empty: false } : step)) };
  };

  it('sends only the steps and flows chosen, the flow stitched behind the steps', async () => {
    const sent = fakeApi({ [`POST /api/maps/${MAP_ID}/deletion`]: { json: replyOf(sampleMap()) } });
    const { editor } = await editorOf(sampleMap());
    deleteChosen(editor, ['s1'], ['f3']);
    await deleteSelection(editor, ['s4'], [], true);
    expect(callsTo(sent, 'POST', `/api/maps/${MAP_ID}/deletion`).map((call) => call.body)).toEqual([
      { steps: ['s1'], flows: ['f3'], stitch: true, sub_maps: null },
      { steps: ['s4'], flows: [], stitch: true, sub_maps: 'delete' },
    ]);
  });

  it('a detailed step alone asks what becomes of its map; a selection that holds one, a flow with it included, asks to confirm', async () => {
    const sent = fakeApi({});
    const { editor } = await editorOf(macro());
    deleteChosen(editor, ['s2'], []);
    expect(editor.floating.getState().floating).toEqual({ kind: 'macro-delete', stepId: 's2' });
    deleteChosen(editor, ['s2', 's3'], ['f1']);
    expect(editor.floating.getState().floating).toEqual({ kind: 'selection-delete', steps: ['s2', 's3'], flows: ['f1'] });
    deleteChosen(editor, ['s2'], ['f1']);
    expect(editor.floating.getState().floating).toEqual({ kind: 'selection-delete', steps: ['s2'], flows: ['f1'] });
    expect(sent).toHaveLength(0);
  });

  it('Ctrl+A chooses every step, no flow (B-2.5-10)', async () => {
    const { editor } = await editorOf(sampleMap());
    editor.store.setState({ selection: { steps: [], flows: ['f1'] } });
    selectAll(editor);
    expect(editor.store.getState().selection).toEqual({ steps: ['s1', 's2', 's3', 's4'], flows: [] });
  });
});
