// dock.test.ts
// The panels docked on the right (spec 3.0 section 15.2, ADR 086): one width kept in the browser, bounded; each panel folded for itself, unfolded when it is opened.

import { memoryStorage } from '../../../test/editor.tsx';
import { createMapEditor } from './editor.ts';
import { DOCK, DOCK_WIDTH_KEY, shownWidth } from './dock.ts';

const editorWith = (values: Record<string, string> = {}) => {
  const storage = memoryStorage(values);
  return { editor: createMapEditor({ storage }), storage };
};

describe('the width of the panels', () => {
  it('is 440 px at first, the width kept when it is a whole number of the least at least', () => {
    expect(editorWith().editor.dock.getState().width).toBe(DOCK.width);
    expect(editorWith({ [DOCK_WIDTH_KEY]: '612' }).editor.dock.getState().width).toBe(612);
    for (const kept of ['abc', '200', '512.5', ''])
      expect(editorWith({ [DOCK_WIDTH_KEY]: kept }).editor.dock.getState().width).toBe(DOCK.width);
  });

  it('is written to the browser at the end of a move only, the least at least', () => {
    const { editor, storage } = editorWith();
    editor.dock.getState().setWidth(500.4, false);
    expect([editor.dock.getState().width, storage.written.has(DOCK_WIDTH_KEY)]).toEqual([500, false]);
    editor.dock.getState().setWidth(100, true);
    expect([editor.dock.getState().width, storage.written.get(DOCK_WIDTH_KEY)]).toEqual([DOCK.min, String(DOCK.min)]);
  });

  it('holds for the session when the storage refuses', () => {
    const editor = createMapEditor({ storage: { getItem: () => { throw new Error('blocked'); }, setItem: () => { throw new Error('full'); } } });
    expect(editor.dock.getState().width).toBe(DOCK.width);
    editor.dock.getState().setWidth(600, true);
    expect(editor.dock.getState().width).toBe(600);
  });

  it('is shown at 70 % of the window at most, never under the least', () => {
    expect(shownWidth(440, 1920)).toBe(440);
    expect(shownWidth(1800, 1920)).toBe(1344);
    expect(shownWidth(440, 400)).toBe(DOCK.min);
  });
});

describe('the folding of the panels', () => {
  it('folds each panel for itself', () => {
    const { editor } = editorWith();
    editor.dock.getState().fold('stream', true);
    editor.dock.getState().fold('assistant', true);
    editor.dock.getState().fold('assistant', false);
    expect(editor.dock.getState().folded).toEqual({ stream: true });
  });

  it('shows a panel unfolded when it is opened: a sheet of another step, the assistant, the layer, the remainder', () => {
    const { editor } = editorWith();
    const fold = () => {
      for (const panel of ['sheet', 'assistant', 'stream', 'remainder'] as const) editor.dock.getState().fold(panel, true);
    };
    fold();
    editor.store.setState({ openSheet: 's2' });
    editor.assistant.getState().setOpen(true);
    editor.floating.getState().open({ kind: 'remainder' });
    expect(editor.dock.getState().folded).toEqual({ stream: true });
    editor.preferences.getState().set('showValueStream', true);
    expect(editor.dock.getState().folded).toEqual({});
    // The same sheet still open, the layer still on: nothing is opened again, the folding holds.
    fold();
    editor.store.setState({ openSheet: 's2' });
    editor.preferences.getState().set('showCodes', false);
    expect(Object.keys(editor.dock.getState().folded)).toHaveLength(4);
    // The sheet of another step opened over it: shown unfolded.
    editor.store.setState({ openSheet: 's3' });
    expect(editor.dock.getState().folded.sheet).toBeUndefined();
  });
});
