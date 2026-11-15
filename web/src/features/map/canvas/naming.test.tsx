// naming.test.tsx
// A step named in place without its menu: a second click on the step chosen alone once a double click is ruled out, a character typed on it, Enter for its sheet (R1, AR-151).

import { act, fireEvent, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { i18next } from '../../../shared/i18n/i18n.ts';
import { showEditor } from '../../../test/editor.tsx';
import { callsTo, fakeApi } from '../../../test/fakeApi.ts';
import { PROJECT_ID, replyOf, sampleMap, stepOf } from '../../../test/maps.ts';
import { sheetViewOf } from '../../../test/sheets.ts';
import { RENAME_DELAY_MS } from './useCanvasEvents.ts';

beforeEach(async () => {
  await i18next.changeLanguage('fr');
});

afterEach(() => {
  vi.unstubAllGlobals();
});

const node = (canvas: HTMLElement, id: string) => canvas.querySelector(`.react-flow__node[data-id="${id}"]`) as HTMLElement;

/** A click as the pointer gives it: the button down at (100, 100), the step focused, then the click where it is released. */
function press(canvas: HTMLElement, id: string, { shiftKey = false, releasedAt = 100 }: { shiftKey?: boolean; releasedAt?: number } = {}) {
  fireEvent.pointerDown(node(canvas, id), { button: 0, pointerId: 1, clientX: 100, clientY: 100, shiftKey });
  act(() => {
    node(canvas, id).focus();
  });
  fireEvent.click(node(canvas, id), { clientX: releasedAt, clientY: 100, shiftKey });
}

const waitPastDoubleClick = () =>
  act(async () => {
    await new Promise((resolve) => setTimeout(resolve, RENAME_DELAY_MS + 100));
  });

describe('a second click on the step chosen alone (AR-151)', () => {
  it('names it in place once the time of a double click is past, its label chosen whole; the first click chose it only', async () => {
    const { editor, canvas } = await showEditor(sampleMap());
    press(canvas, 's2');
    await waitPastDoubleClick();
    expect([editor.store.getState().selection.steps, editor.store.getState().editing, editor.store.getState().openSheet]).toEqual([['s2'], null, null]);
    press(canvas, 's2');
    expect(editor.store.getState().editing).toBeNull();
    await waitPastDoubleClick();
    expect(editor.store.getState().editing).toBe('s2');
    const field = screen.getByTestId<HTMLTextAreaElement>('step-label-input');
    expect([field.selectionStart, field.selectionEnd]).toEqual([0, field.value.length]);
  });

  it('names nothing after a drag, with Shift, on a step chosen with others, nor for a reader', async () => {
    const { editor, canvas } = await showEditor(sampleMap());
    const namedAfter = async (second: () => void) => {
      act(() => {
        editor.store.setState({ selection: { steps: ['s2'], flows: [] } });
      });
      second();
      await waitPastDoubleClick();
      return editor.store.getState().editing;
    };
    expect(await namedAfter(() => {
      press(canvas, 's2', { releasedAt: 140 });
    })).toBeNull();
    expect(await namedAfter(() => {
      press(canvas, 's2', { shiftKey: true });
    })).toBeNull();
    expect(await namedAfter(() => {
      editor.store.setState({ selection: { steps: ['s2', 's3'], flows: [] } });
      press(canvas, 's2');
    })).toBeNull();
    const reader = await showEditor(sampleMap({ rights: { structure: false, role: 'reader' } }));
    press(reader.canvas, 's2');
    press(reader.canvas, 's2');
    await waitPastDoubleClick();
    expect(reader.editor.store.getState().editing).toBeNull();
  }, 15_000);

  it('gives way to Enter pressed at once: the sheet opens and keeps the focus, nothing is named', async () => {
    fakeApi({ 'GET /api/steps/s2/sheet': { json: sheetViewOf() }, [`GET /api/projects/${PROJECT_ID}`]: { json: { members: [], sharing_group_label: null } } });
    const { editor, canvas } = await showEditor(sampleMap());
    press(canvas, 's2');
    press(canvas, 's2');
    await userEvent.keyboard('{Enter}');
    await waitPastDoubleClick();
    expect([editor.store.getState().openSheet, editor.store.getState().editing]).toEqual(['s2', null]);
    expect(document.activeElement).toBe(screen.getByTestId('sheet-panel'));
  });

  it('is for the mouse: a tap of a finger, which has no Enter, opens the sheet of the step as in the 1.x (S-NF-03)', async () => {
    fakeApi({ 'GET /api/steps/s2/sheet': { json: sheetViewOf() }, [`GET /api/projects/${PROJECT_ID}`]: { json: { members: [], sharing_group_label: null } } });
    const { editor, canvas } = await showEditor(sampleMap({ rights: { structure: false, role: 'reader' } }));
    fireEvent.pointerDown(node(canvas, 's2'), { button: 0, pointerId: 2, pointerType: 'touch', clientX: 100, clientY: 100 });
    fireEvent.click(node(canvas, 's2'), { clientX: 100, clientY: 100 });
    expect([editor.store.getState().openSheet, editor.store.getState().selection.steps]).toEqual(['s2', ['s2']]);
  });

  it('gives way to a double click: on a detailed step, its map opens and nothing is named', async () => {
    const detailed = sampleMap({ steps: sampleMap().steps.map((step) => (step.id === 's2' ? { ...step, sub_map_id: 'sub', sub_map_empty: false } : step)) });
    const { editor, canvas, opened } = await showEditor(detailed);
    press(canvas, 's2');
    press(canvas, 's2');
    fireEvent.doubleClick(node(canvas, 's2'));
    await waitPastDoubleClick();
    expect([opened, editor.store.getState().editing]).toEqual([['/maps/sub'], null]);
  });
});

describe('a character typed on the step chosen alone (AR-151)', () => {
  it('begins its name from that character, the caret after it; Enter keeps the name typed', async () => {
    const sent = fakeApi({ 'PATCH /api/steps/s2': { json: replyOf(sampleMap()) } });
    const { editor, canvas } = await showEditor(sampleMap());
    fireEvent.click(node(canvas, 's2'));
    act(() => {
      node(canvas, 's2').focus();
    });
    await userEvent.keyboard('P');
    const field = screen.getByTestId<HTMLTextAreaElement>('step-label-input');
    expect([editor.store.getState().editing, field.value, field.selectionStart]).toEqual(['s2', 'P', 1]);
    await userEvent.keyboard('aie{Enter}');
    await vi.waitFor(() => {
      expect(callsTo(sent, 'PATCH', '/api/steps/s2').map((call) => call.body)).toEqual([{ label: 'Paie' }]);
    });
    expect(editor.store.getState().editingSeed).toBeNull();
  });

  it('takes V and H too while a step alone is chosen; else they choose the tool, and a reader keeps them', async () => {
    const { editor, canvas } = await showEditor(sampleMap({ steps: [...sampleMap().steps, stepOf('s5', 'l2', 5, { type: 'wait' })] }));
    act(() => {
      node(canvas, 's3').focus();
      editor.store.setState({ selection: { steps: ['s3'], flows: [] } });
    });
    await userEvent.keyboard('V');
    expect([editor.store.getState().editing, screen.getByTestId<HTMLTextAreaElement>('step-label-input').value, editor.store.getState().tool]).toEqual(['s3', 'V', 'hand']);
    await userEvent.keyboard('{Escape}');
    act(() => {
      node(canvas, 's3').focus();
      editor.store.setState({ selection: { steps: ['s3', 's5'], flows: [] } });
    });
    await userEvent.keyboard('V');
    expect([editor.store.getState().editing, editor.store.getState().tool]).toEqual([null, 'select']);
    const reader = await showEditor(sampleMap({ rights: { structure: false, role: 'reader' } }));
    act(() => {
      node(reader.canvas, 's2').focus();
      reader.editor.store.setState({ selection: { steps: ['s2'], flows: [] } });
    });
    await userEvent.keyboard('V');
    expect([reader.editor.store.getState().editing, reader.editor.store.getState().tool]).toEqual([null, 'select']);
  });
});
