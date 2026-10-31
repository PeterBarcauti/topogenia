// paste.test.ts
// Copy, cut, paste and duplicate: the clipboard of the module, the calls of a copy and of a cut, the refusals, the new steps chosen (annex B 2.7).

import { i18next } from '../../../../shared/i18n/i18n.ts';
import { callsTo, fakeApi } from '../../../../test/fakeApi.ts';
import { editorOf } from '../../../../test/editorStore.ts';
import { flowOf, laneOf, MAP_ID, mapOf, replyOf, stepOf } from '../../../../test/maps.ts';
import { clipboard, cutSteps, setClipboard } from '../clipboard.ts';
import { duplicate, giveUpCut, paste, takeToClipboard } from './paste.ts';

const map = mapOf({
  lanes: [laneOf('A', 0), laneOf('B', 1)],
  steps: [stepOf('a', 'A', 0, { label: 'Saisir' }), stepOf('b', 'B', 1, { type: 'decision', label: 'Conforme ?' }), stepOf('c', 'A', 3)],
  flows: [flowOf('ab', 'a', 'b', { label: 'oui' })],
});
const PASTE = `/api/maps/${MAP_ID}/paste`;
const TRANSFER = `/api/maps/${MAP_ID}/transfer`;

beforeEach(async () => {
  await i18next.changeLanguage('fr');
  setClipboard(null);
});

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('the clipboard (B-2.7-01 to B-2.7-03, B-2.7-09, B-2.7-13)', () => {
  it('is a store of the module that any editor reads: a copy taken in one map is there for another', async () => {
    const sent = fakeApi({});
    const stored = window.localStorage.length;
    const first = await editorOf(map);
    takeToClipboard(first.editor, ['a', 'b'], 'copy');
    takeToClipboard(first.editor, ['c'], 'cut');
    takeToClipboard(first.editor, ['a', 'b'], 'copy');
    expect([sent, window.localStorage.length]).toEqual([[], stored]);
    first.notes.splice(0, 2);
    expect(first.notes).toEqual([['2 étapes copiées.', undefined]]);
    const second = await editorOf(mapOf({ id: 'other', lanes: [laneOf('Z', 0)] }));
    expect([clipboard.getState().content?.mapId, second.editor.store.getState().map?.id]).toEqual([MAP_ID, 'other']);
  });

  it('a cut greys its steps on its own map only, and Escape gives it up', async () => {
    const { editor, notes } = await editorOf(map);
    takeToClipboard(editor, ['c'], 'cut');
    expect(notes[0]?.[0]).toBe('1 étape coupée : choisir l’étape après laquelle l’insérer ou viser une case, puis Ctrl+V. Échap pour renoncer.');
    expect([[...cutSteps(clipboard.getState().content, MAP_ID)], [...cutSteps(clipboard.getState().content, 'other')]]).toEqual([['c'], []]);
    giveUpCut();
    expect(clipboard.getState().content).toBeNull();
  });

  it('Escape leaves a copy in place', async () => {
    const { editor } = await editorOf(map);
    takeToClipboard(editor, ['c'], 'copy');
    giveUpCut();
    expect(clipboard.getState().content?.mode).toBe('copy');
  });
});

describe('the paste of a copy (B-2.7-07, B-2.7-15, B-2.7-18, B-2.7-22, B-2.3-30)', () => {
  it('makes new steps in the cell aimed, with the inner flows; the new steps are chosen', async () => {
    const sent = fakeApi({ [`POST ${PASTE}`]: { json: replyOf(map, { ids: ['n1', 'n2'] }) } });
    const { editor } = await editorOf(map);
    takeToClipboard(editor, ['a', 'b'], 'copy');
    await paste(editor, { cell: { laneId: 'A', column: 5, row: 0 } });
    expect(callsTo(sent, 'POST', PASTE)[0]?.body).toEqual({
      steps: [{ key: 'c0', label: 'Saisir', type: 'task', lane_id: 'A', column: 5, row: 0 }, { key: 'c1', label: 'Conforme ?', type: 'decision', lane_id: 'B', column: 6, row: 0 }],
      flows: [{ source: 'c0', target: 'c1', label: 'oui' }], anchor: null,
    });
    expect([editor.store.getState().selection, clipboard.getState().content?.mode]).toEqual([{ steps: ['n1', 'n2'], flows: [] }, 'copy']);
  });

  it('before or after a step: by an anchor of the flow, from the cell of the step', async () => {
    const sent = fakeApi({ [`POST ${PASTE}`]: { json: replyOf(map, { ids: ['n1'] }) } });
    const { editor } = await editorOf(map);
    takeToClipboard(editor, ['a'], 'copy');
    await paste(editor, { stepId: 'c', position: 'after' });
    expect(callsTo(sent, 'POST', PASTE)[0]?.body).toMatchObject({ steps: [{ lane_id: 'A', column: 4, row: 0 }], anchor: { step_id: 'c', position: 'after' } });
  });

  it('is refused, said, when lanes are missing under the cell; nothing to paste is said too', async () => {
    const sent = fakeApi({});
    const { editor, notes } = await editorOf(map);
    await paste(editor, { cell: { laneId: 'A', column: 5, row: 0 } });
    takeToClipboard(editor, ['a', 'b'], 'copy');
    await paste(editor, { cell: { laneId: 'B', column: 5, row: 0 } });
    expect(notes).toEqual([
      ['Rien à coller : copier ou couper d’abord des étapes.', undefined], ['2 étapes copiées.', undefined],
      ['Le bloc s’étend sur 2 couloirs : il en manque sous la case visée. Coller plus haut ou ajouter un couloir.', 'error'],
    ]);
    expect(sent).toHaveLength(0);
  });
});

describe('the paste of a cut (B-2.7-08, B-2.7-10, B-2.7-11, B-2.7-14)', () => {
  it('moves the steps themselves by their identifiers, then empties the clipboard', async () => {
    const sent = fakeApi({ [`POST ${TRANSFER}`]: { json: replyOf(map, { ids: ['c'] }) } });
    const { editor } = await editorOf(map);
    takeToClipboard(editor, ['c'], 'cut');
    await paste(editor, { cell: { laneId: 'B', column: 4, row: 0 } });
    expect(callsTo(sent, 'POST', TRANSFER)[0]?.body).toEqual({ steps: [{ id: 'c', lane_id: 'B', column: 4, row: 0 }], cut: true, anchor: null });
    expect([clipboard.getState().content, editor.store.getState().selection.steps]).toEqual([null, ['c']]);
  });

  it('from another map, reads that map to keep only the steps still alive', async () => {
    const sent = fakeApi({ 'GET /api/maps/source': { json: { map: mapOf({ id: 'source', steps: [stepOf('x', 'S', 0)] }), history: { undo: [], redo: [] } } }, [`POST ${TRANSFER}`]: { json: replyOf(map, { ids: ['x'] }) } });
    const { editor } = await editorOf(map);
    setClipboard({ mode: 'cut', projectId: map.project_id, mapId: 'source', steps: [{ key: 'x', label: 'X', type: 'task', lane: 0, column: 0, row: 0 }, { key: 'gone', label: 'G', type: 'task', lane: 0, column: 1, row: 0 }], flows: [] });
    await paste(editor, { cell: { laneId: 'A', column: 6, row: 0 } });
    expect(callsTo(sent, 'POST', TRANSFER)[0]?.body).toMatchObject({ steps: [{ id: 'x', lane_id: 'A', column: 6, row: 0 }] });
  });

  it('whose steps are all gone: « Les étapes coupées n’existent plus », and the clipboard emptied', async () => {
    const sent = fakeApi({ 'GET /api/maps/source': { refused: 404, code: 'INTROUVABLE' } });
    const { editor, notes } = await editorOf(map);
    setClipboard({ mode: 'cut', projectId: map.project_id, mapId: 'source', steps: [{ key: 'x', label: 'X', type: 'task', lane: 0, column: 0, row: 0 }], flows: [] });
    await paste(editor, { cell: { laneId: 'A', column: 6, row: 0 } });
    expect([notes, clipboard.getState().content, callsTo(sent, 'POST', TRANSFER)]).toEqual([[['Les étapes coupées n’existent plus.', 'error']], null, []]);
  });
});

describe('the duplication (B-2.7-23, B-2.7-24)', () => {
  it('pastes a copy just right of the block, chooses it, and leaves the clipboard alone', async () => {
    const sent = fakeApi({ [`POST ${PASTE}`]: { json: replyOf(map, { ids: ['d1', 'd2'] }) } });
    const { editor } = await editorOf(map);
    takeToClipboard(editor, ['c'], 'cut');
    await duplicate(editor, ['a', 'b']);
    expect(callsTo(sent, 'POST', PASTE)[0]?.body).toMatchObject({ steps: [{ key: 'c0', lane_id: 'A', column: 2, row: 0 }, { key: 'c1', lane_id: 'B', column: 3, row: 0 }], anchor: null });
    expect([editor.store.getState().selection.steps, clipboard.getState().content?.mode]).toEqual([['d1', 'd2'], 'cut']);
  });
});
