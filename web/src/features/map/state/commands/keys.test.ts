// keys.test.ts
// The shortcuts of the map run by the one keyboard manager, without React: level, tools, undo and redo, the command bar, clipboard, group and ungroup, choose all, delete, rename, Escape, the arrows (annex B 2.5, B-8.2-07, AR-154, AR-155).

import { i18next } from '../../../../shared/i18n/i18n.ts';
import { createKeyboard } from '../../../../shared/keyboard/keyboard.ts';
import { callsTo, fakeApi } from '../../../../test/fakeApi.ts';
import { editorOf } from '../../../../test/editorStore.ts';
import { frameOf, MAP_ID, replyOf, sampleMap } from '../../../../test/maps.ts';
import { clipboard, setClipboard } from '../clipboard.ts';
import type { MapEditor } from '../editor.ts';
import { mapKeyHandler } from './mapCommands.ts';
import type { Point } from '../../model/geometry/grid.ts';

beforeEach(async () => {
  await i18next.changeLanguage('fr');
  setClipboard(null);
});

afterEach(() => {
  vi.unstubAllGlobals();
});

/** A keyboard whose canvas layer runs the commands of the map with the context of the moment; gives the function that presses a chord. */
function keysOf(editor: MapEditor, pointer: () => Point | null = () => null) {
  const keyboard = createKeyboard();
  keyboard.add('canvas', mapKeyHandler(editor, pointer));
  return (init: KeyboardEventInit) => {
    const event = new KeyboardEvent('keydown', { bubbles: true, cancelable: true, ...init });
    keyboard.dispatch(event);
    return event.defaultPrevented;
  };
}

const selected = (editor: MapEditor, steps: string[], flows: string[] = []) => {
  editor.store.setState({ selection: { steps, flows } });
};

describe('the tools and the display (B-2.5-16, AR-90)', () => {
  it('V takes Selection, H the Hand, without Ctrl nor Alt; Ctrl+F1 folds and unfolds the ribbon', async () => {
    const { editor } = await editorOf(sampleMap());
    const press = keysOf(editor);
    press({ key: 'v' });
    expect(editor.store.getState().tool).toBe('select');
    expect(press({ key: 'h', ctrlKey: true })).toBe(false);
    expect(editor.store.getState().tool).toBe('select');
    press({ key: 'H', shiftKey: true });
    expect(editor.store.getState().tool).toBe('hand');
    press({ key: 'F1', ctrlKey: true });
    expect(editor.preferences.getState().ribbonCollapsed).toBe(true);
  });
});

describe('the level by the keyboard (AR-155)', () => {
  it('Alt+Up opens the map above a detailed map, for a reader too; on the root map, or without Alt, it moves nothing', async () => {
    const breadcrumb = [{ map_id: 'root', name: 'Ventes', source_language: 'fr', translations: {} }, { map_id: MAP_ID, name: 'Traiter', source_language: 'fr', translations: {} }];
    const reader = await editorOf(sampleMap({ breadcrumb, rights: { structure: false, role: 'reader' } }));
    expect(keysOf(reader.editor)({ key: 'ArrowUp', altKey: true })).toBe(true);
    expect(reader.opened).toEqual(['/maps/root']);
    const root = await editorOf(sampleMap());
    expect(keysOf(root.editor)({ key: 'ArrowUp', altKey: true })).toBe(false);
    expect(root.opened).toEqual([]);
    const { editor, opened } = await editorOf(sampleMap({ breadcrumb }));
    selected(editor, ['s2']);
    keysOf(editor)({ key: 'ArrowUp' });
    expect(opened).toEqual([]);
  });
});

describe('the clipboard by the keyboard (B-2.5-10 to B-2.5-14)', () => {
  it('Ctrl+A chooses every step; Ctrl+C copies them; with nothing chosen Ctrl+C is left to the browser', async () => {
    const { editor } = await editorOf(sampleMap());
    const press = keysOf(editor);
    expect(press({ key: 'c', ctrlKey: true })).toBe(false);
    press({ key: 'a', ctrlKey: true });
    press({ key: 'c', ctrlKey: true });
    expect(clipboard.getState().content?.steps).toHaveLength(4);
  });

  it('Ctrl+X cuts, Ctrl+D duplicates, only for who may change the map', async () => {
    fakeApi({ [`POST /api/maps/${MAP_ID}/paste`]: { json: replyOf(sampleMap(), { ids: ['n1'] }) } });
    const reader = await editorOf(sampleMap({ rights: { structure: false, role: 'reader' } }));
    selected(reader.editor, ['s2']);
    expect([keysOf(reader.editor)({ key: 'x', ctrlKey: true }), keysOf(reader.editor)({ key: 'd', ctrlKey: true })]).toEqual([false, false]);
    const { editor } = await editorOf(sampleMap());
    selected(editor, ['s2']);
    const press = keysOf(editor);
    press({ key: 'd', ctrlKey: true });
    await vi.waitFor(() => {
      expect(editor.store.getState().selection.steps).toEqual(['n1']);
    });
    selected(editor, ['s2']);
    press({ key: 'x', ctrlKey: true });
    expect(clipboard.getState().content?.mode).toBe('cut');
  });

  it('Ctrl+V: after the one step chosen; else in the cell under the pointer; else at the end of the map (B-2.7-19 to B-2.7-21)', async () => {
    const sent = fakeApi({ [`POST /api/maps/${MAP_ID}/paste`]: { json: replyOf(sampleMap(), { ids: [] }) } });
    const { editor } = await editorOf(sampleMap());
    let pointer: Point | null = null;
    const press = keysOf(editor, () => pointer);
    selected(editor, ['s1']);
    press({ key: 'c', ctrlKey: true });
    press({ key: 'v', ctrlKey: true });
    await vi.waitFor(() => {
      expect(callsTo(sent, 'POST', `/api/maps/${MAP_ID}/paste`)).toHaveLength(1);
    });
    pointer = { x: 30 + 6 * 220 + 85, y: 180 };
    selected(editor, []);
    press({ key: 'v', ctrlKey: true });
    pointer = null;
    press({ key: 'v', ctrlKey: true });
    await vi.waitFor(() => {
      expect(callsTo(sent, 'POST', `/api/maps/${MAP_ID}/paste`)).toHaveLength(3);
    });
    const bodies = callsTo(sent, 'POST', `/api/maps/${MAP_ID}/paste`).map((call) => call.body as { anchor: unknown; steps: { lane_id: string; column: number }[] });
    expect(bodies.map((body) => [body.anchor, body.steps[0]?.lane_id, body.steps[0]?.column])).toEqual([
      [{ step_id: 's1', position: 'after' }, 'l1', 1], [null, 'l2', 6], [null, 'l1', 4],
    ]);
  });

  it('Ctrl+V with the one step chosen being the step cut: not after itself, at the end of the map', async () => {
    const sent = fakeApi({ [`POST /api/maps/${MAP_ID}/transfer`]: { json: replyOf(sampleMap(), { ids: ['s2'] }) } });
    const { editor } = await editorOf(sampleMap());
    const press = keysOf(editor);
    selected(editor, ['s2']);
    press({ key: 'x', ctrlKey: true });
    press({ key: 'v', ctrlKey: true });
    await vi.waitFor(() => {
      expect(callsTo(sent, 'POST', `/api/maps/${MAP_ID}/transfer`)[0]?.body).toEqual({ steps: [{ id: 's2', lane_id: 'l1', column: 4, row: 0 }], cut: true, anchor: null });
    });
  });

  it('Ctrl+V with nothing to paste says it, for who may change the map', async () => {
    const { editor, notes } = await editorOf(sampleMap());
    expect(keysOf(editor)({ key: 'v', ctrlKey: true })).toBe(true);
    expect(notes).toEqual([['Rien à coller : copier ou couper d’abord des étapes.', undefined]]);
  });
});

describe('rename, delete, Escape and the arrows (B-2.5-15, B-2.5-18 to B-2.5-27)', () => {
  it('F2 renames the first step chosen; Escape stops the name typed, the link mode, the sheet open, and gives up a cut', async () => {
    const { editor } = await editorOf(sampleMap());
    const press = keysOf(editor);
    selected(editor, ['s3', 's2']);
    press({ key: 'F2' });
    expect(editor.store.getState().editing).toBe('s3');
    press({ key: 'x', ctrlKey: true });
    editor.store.setState({ mode: 'link', linkSource: 's1', openSheet: 's2' });
    press({ key: 'Escape' });
    const state = editor.store.getState();
    expect([state.editing, state.mode, state.linkSource, state.openSheet, clipboard.getState().content]).toEqual([null, 'normal', null, null, null]);
  });

  it('Delete and Backspace delete the steps and flows chosen, the flow stitched', async () => {
    const sent = fakeApi({ [`POST /api/maps/${MAP_ID}/deletion`]: { json: replyOf(sampleMap()) } });
    const { editor } = await editorOf(sampleMap());
    const press = keysOf(editor);
    selected(editor, ['s2'], ['f3']);
    press({ key: 'Delete' });
    selected(editor, [], ['f1']);
    press({ key: 'Backspace' });
    await vi.waitFor(() => {
      expect(callsTo(sent, 'POST', `/api/maps/${MAP_ID}/deletion`).map((call) => call.body)).toEqual([
        { steps: ['s2'], flows: ['f3'], stitch: true, sub_maps: null }, { steps: [], flows: ['f1'], stitch: true, sub_maps: null },
      ]);
    });
  });

  it('the arrows move the steps chosen, without modifier, only for who may change the map', async () => {
    const sent = fakeApi({ [`POST /api/maps/${MAP_ID}/move`]: { json: replyOf(sampleMap()) } });
    const { editor } = await editorOf(sampleMap());
    const press = keysOf(editor);
    expect(press({ key: 'ArrowRight' })).toBe(false);
    selected(editor, ['s4']);
    expect(press({ key: 'ArrowRight', shiftKey: true })).toBe(false);
    expect(press({ key: 'ArrowUp' })).toBe(true);
    await vi.waitFor(() => {
      expect(callsTo(sent, 'POST', `/api/maps/${MAP_ID}/move`)[0]?.body).toEqual({ steps: [{ id: 's4', lane_id: 'l1', column: 3, row: 0 }] });
    });
    const reader = await editorOf(sampleMap({ rights: { structure: false, role: 'reader' } }));
    selected(reader.editor, ['s4']);
    expect(keysOf(reader.editor)({ key: 'ArrowUp' })).toBe(false);
  });
});

describe('undo, redo and the command bar (B-2.5-06 to B-2.5-09)', () => {
  const history = { undo: [{ operation_id: 'o1', kind: 'deplacer', at: '2026-10-28T08:00:00Z' }], redo: [{ operation_id: 'o2', kind: 'aligner', at: '2026-10-28T08:00:00Z' }] };

  it('Ctrl+Z undoes, Ctrl+Y and Ctrl+Shift+Z redo', async () => {
    const sent = fakeApi({ [`POST /api/maps/${MAP_ID}/undo`]: { json: replyOf(sampleMap(), { code: 'deplacer', history }) }, [`POST /api/maps/${MAP_ID}/redo`]: { json: replyOf(sampleMap(), { code: 'aligner', history }) } });
    const { editor } = await editorOf(sampleMap());
    editor.store.setState({ history });
    const press = keysOf(editor);
    expect(press({ key: 'z', ctrlKey: true })).toBe(true);
    expect(press({ key: 'y', ctrlKey: true })).toBe(true);
    expect(press({ key: 'Z', ctrlKey: true, shiftKey: true })).toBe(true);
    await vi.waitFor(() => {
      expect(sent.map((call) => call.path.split('/').pop())).toEqual(['undo', 'redo', 'redo']);
    });
  });

  it('Ctrl+K opens the command bar; a reader keeps none of these keys', async () => {
    const { editor } = await editorOf(sampleMap());
    keysOf(editor)({ key: 'k', ctrlKey: true });
    expect(editor.floating.getState().floating).toEqual({ kind: 'command' });
    const reader = await editorOf(sampleMap({ rights: { structure: false, role: 'reader' } }));
    reader.editor.store.setState({ history });
    const press = keysOf(reader.editor);
    expect([press({ key: 'k', ctrlKey: true }), press({ key: 'z', ctrlKey: true }), press({ key: 'y', ctrlKey: true }), press({ key: 'Z', ctrlKey: true, shiftKey: true })])
      .toEqual([false, false, false, false]);
    expect(reader.editor.floating.getState().floating).toBeNull();
  });
});

describe('group and ungroup by the keyboard, as in Office (AR-154)', () => {
  const withMacro = () => {
    const map = sampleMap();
    return { ...map, steps: map.steps.map((step) => (step.id === 's2' ? { ...step, sub_map_id: 'sub', sub_map_empty: false } : step)) };
  };

  it('Ctrl+G groups the steps chosen in a sub-process; with one step chosen, or for a reader, it is left to the browser', async () => {
    const sent = fakeApi({ [`POST /api/maps/${MAP_ID}/group`]: { json: replyOf(sampleMap(), { id: 's2' }) } });
    const reader = await editorOf(sampleMap({ rights: { structure: false, role: 'reader' } }));
    selected(reader.editor, ['s2', 's3']);
    const { editor } = await editorOf(sampleMap());
    const press = keysOf(editor);
    selected(editor, ['s2']);
    expect([keysOf(reader.editor)({ key: 'g', ctrlKey: true }), press({ key: 'g', ctrlKey: true })]).toEqual([false, false]);
    selected(editor, ['s2', 's3']);
    expect(press({ key: 'g', ctrlKey: true })).toBe(true);
    await vi.waitFor(() => {
      expect(callsTo(sent, 'POST', `/api/maps/${MAP_ID}/group`).map((call) => call.body)).toEqual([{ steps: ['s2', 's3'], label: 'Sous-processus' }]);
    });
  });

  it('Ctrl+Shift+G brings back into the flow the detail of the one detailed step chosen; not on a step without detail, nor while a frame is open', async () => {
    const sent = fakeApi({ 'POST /api/steps/s2/dissolve': { json: replyOf(withMacro()) } });
    const plain = await editorOf(sampleMap());
    selected(plain.editor, ['s2']);
    const framed = await editorOf({ ...withMacro(), dissolution: frameOf(['s3']) });
    selected(framed.editor, ['s2']);
    const ungroup = { key: 'G', ctrlKey: true, shiftKey: true };
    expect([keysOf(plain.editor)(ungroup), keysOf(framed.editor)(ungroup)]).toEqual([false, false]);
    const { editor } = await editorOf(withMacro());
    selected(editor, ['s2', 's3']);
    expect(keysOf(editor)(ungroup)).toBe(false);
    selected(editor, ['s2']);
    expect(keysOf(editor)(ungroup)).toBe(true);
    await vi.waitFor(() => {
      expect(callsTo(sent, 'POST', '/api/steps/s2/dissolve')).toHaveLength(1);
    });
  });
});
