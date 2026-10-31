// editing.test.tsx
// The gestures of the canvas: a click chooses or links, a sheet open follows it, a double click creates, renames or opens, the name typed in place, the quick add, the states shown (annex B 2.3, 2.5, 3.2, AR-151).

import { act, fireEvent, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { i18next } from '../../../shared/i18n/i18n.ts';
import { showEditor } from '../../../test/editor.tsx';
import { callsTo, fakeApi } from '../../../test/fakeApi.ts';
import { laneOf, MAP_ID, mapOf, replyOf, sampleMap, stepOf } from '../../../test/maps.ts';
import { setClipboard } from '../state/clipboard.ts';

beforeEach(async () => {
  await i18next.changeLanguage('fr');
  setClipboard(null);
});

afterEach(() => {
  vi.unstubAllGlobals();
});

const node = (canvas: HTMLElement, id: string) => canvas.querySelector(`.react-flow__node[data-id="${id}"]`) as HTMLElement;
const pane = (canvas: HTMLElement) => canvas.querySelector('.react-flow__pane') as HTMLElement;

describe('a click (B-2.3-12 to B-2.3-18, B-2.3-03, AR-151)', () => {
  it('chooses a step and opens no sheet; a sheet open follows the step clicked, none for a start, an end or a joining gateway; a click on the background closes it', async () => {
    const map = sampleMap({ steps: [...sampleMap().steps, stepOf('s5', 'l2', 5, { type: 'wait' }), stepOf('s6', 'l2', 6, { type: 'parallel' })] });
    const { editor, canvas } = await showEditor(map);
    fireEvent.click(node(canvas, 's2'));
    expect([editor.store.getState().openSheet, editor.store.getState().selection.steps]).toEqual([null, ['s2']]);
    act(() => {
      editor.store.setState({ openSheet: 's2' });
    });
    const shownAfter = (id: string) => {
      fireEvent.click(node(canvas, id));
      return editor.store.getState().openSheet;
    };
    expect([shownAfter('s3'), shownAfter('s5'), shownAfter('s1'), shownAfter('s4')]).toEqual(['s3', 's5', null, null]);
    act(() => {
      editor.store.setState({ openSheet: 's2' });
    });
    expect(shownAfter('s6')).toBeNull();
    act(() => {
      editor.store.setState({ openSheet: 's2' });
    });
    fireEvent.click(pane(canvas));
    expect(editor.store.getState().openSheet).toBeNull();
  });

  it('with the RACI layer on, a click on a step concerned opens its RACI panel at (+12, −20) of the pointer, not its sheet; nothing on a start, an end or a joining gateway', async () => {
    const { editor, canvas } = await showEditor(sampleMap({ steps: [...sampleMap().steps, stepOf('s6', 'l2', 6, { type: 'parallel' })] }));
    act(() => {
      editor.preferences.getState().set('showRaci', true);
    });
    for (const id of ['s1', 's4', 's6']) {
      fireEvent.click(node(canvas, id), { clientX: 300, clientY: 200 });
      expect([id, editor.floating.getState().floating, editor.store.getState().openSheet]).toEqual([id, null, null]);
    }
    fireEvent.click(node(canvas, 's2'), { clientX: 300, clientY: 200 });
    expect([editor.store.getState().openSheet, editor.floating.getState().floating]).toEqual([null, { kind: 'raci', stepId: 's2', at: { x: 312, y: 180 } }]);
  });

  it('with the RACI layer on, a click of a reader chooses the step and opens neither its sheet nor a panel: the panel is for who may change the map', async () => {
    const { editor, canvas } = await showEditor(sampleMap({ rights: { structure: false, role: 'reader' } }));
    act(() => {
      editor.preferences.getState().set('showRaci', true);
    });
    fireEvent.click(node(canvas, 's2'));
    expect([editor.store.getState().openSheet, editor.floating.getState().floating, editor.store.getState().selection.steps]).toEqual([null, null, ['s2']]);
  });

  it('chooses the step; Shift+click adds or takes out a step; the chosen ones carry the ring', async () => {
    const { editor, canvas } = await showEditor(sampleMap());
    fireEvent.click(node(canvas, 's2'));
    await userEvent.keyboard('{Shift>}');
    fireEvent.click(node(canvas, 's3'));
    expect(editor.store.getState().selection.steps).toEqual(['s2', 's3']);
    expect(within(canvas).getByTestId('step-VTE-E002')).toHaveClass('is-selected');
    fireEvent.click(node(canvas, 's2'));
    await userEvent.keyboard('{/Shift}');
    expect(editor.store.getState().selection.steps).toEqual(['s3']);
  });

});

describe('the link mode (B-2.1-22, AR-153)', () => {
  it('the first click chooses the source, said and ringed, the thread unwound; the second creates the flow and ends the mode', async () => {
    const sent = fakeApi({ [`POST /api/maps/${MAP_ID}/flows`]: { json: replyOf(sampleMap()) } });
    const { editor, canvas, notes } = await showEditor(sampleMap());
    act(() => {
      editor.store.setState({ mode: 'link' });
    });
    expect(canvas).toHaveClass('is-linking');
    expect(canvas).not.toHaveClass('has-link-source');
    fireEvent.click(node(canvas, 's1'));
    // AR-153: the thread unwound once the start is chosen.
    expect(canvas).toHaveClass('has-link-source');
    expect(notes).toEqual([['Départ : VTE-E001. Cliquer maintenant sur l’étape d’arrivée.', undefined]]);
    expect(within(canvas).getByTestId('step-VTE-E001')).toHaveClass('is-link-source');
    expect(screen.getByTestId('map-link')).toHaveTextContent('Départ : VTE-E001.');
    fireEvent.click(node(canvas, 's1'));
    fireEvent.click(node(canvas, 's4'));
    await vi.waitFor(() => {
      expect(callsTo(sent, 'POST', `/api/maps/${MAP_ID}/flows`).map((call) => call.body)).toEqual([{ source_step_id: 's1', target_step_id: 's4', label: null }]);
    });
    expect([editor.store.getState().mode, editor.store.getState().openSheet]).toEqual(['normal', null]);
    expect(canvas).not.toHaveClass('is-linking');
    expect(canvas).not.toHaveClass('has-link-source');
  });
});

describe('a double click (B-2.3-08 to B-2.3-11)', () => {
  it('on the background, where the lanes let the pointer through, creates a step of the type of the next steps in the cell aimed', async () => {
    const sent = fakeApi({ [`POST /api/maps/${MAP_ID}/steps`]: { json: replyOf(sampleMap(), { id: 'new' }) } });
    const { editor, canvas } = await showEditor(sampleMap());
    expect(canvas.querySelector<HTMLElement>('.react-flow__node-lane')?.style.pointerEvents).toBe('none');
    fireEvent.doubleClick(pane(canvas), { clientX: 192 + 30 + 4 * 220 + 85, clientY: 16 + 120 + 60 });
    await vi.waitFor(() => {
      expect(callsTo(sent, 'POST', `/api/maps/${MAP_ID}/steps`)[0]?.body).toEqual({ lane_id: 'l2', column: 4, row: 0, label: 'Nouvelle étape', type: 'task' });
    });
    expect(editor.store.getState().editing).toBe('new');
  });

  it('on a detailed step opens its map; on another step types its name; a reader types nothing', async () => {
    const map = sampleMap();
    const { canvas, opened } = await showEditor({ ...map, steps: map.steps.map((step) => (step.id === 's3' ? { ...step, sub_map_id: 'sub', sub_map_empty: false } : step)) });
    fireEvent.doubleClick(node(canvas, 's3'));
    expect(opened).toEqual(['/maps/sub']);
    fireEvent.doubleClick(node(canvas, 's2'));
    expect(within(canvas).getByTestId('step-label-input')).toHaveValue('Vérifier la demande');
    const reader = await showEditor(sampleMap({ rights: { structure: false, role: 'reader' } }));
    fireEvent.doubleClick(node(reader.canvas, 's2'));
    expect(within(reader.canvas).queryByTestId('step-label-input')).toBeNull();
  });
});

describe('the name typed in place (B-2.5-28 to B-2.5-32)', () => {
  const typing = async () => {
    const sent = fakeApi({ 'PATCH /api/steps/s2': { json: replyOf(sampleMap()) } });
    const shown = await showEditor(sampleMap());
    act(() => {
      shown.editor.store.setState({ editing: 's2' });
    });
    return { ...shown, sent, field: within(shown.canvas).getByTestId('step-label-input') };
  };

  it('Enter validates, Shift+Enter goes to the line', async () => {
    const { field, sent, editor } = await typing();
    await userEvent.clear(field);
    await userEvent.type(field, 'Contrôler{Shift>}{Enter}{/Shift}la demande{Enter}');
    await vi.waitFor(() => {
      expect(callsTo(sent, 'PATCH', '/api/steps/s2')[0]?.body).toEqual({ label: 'Contrôler\nla demande' });
    });
    expect(editor.store.getState().editing).toBeNull();
  });

  it('Escape gives up, an empty text gives up, leaving the field validates', async () => {
    const first = await typing();
    await userEvent.type(first.field, ' bis{Escape}');
    expect(first.editor.store.getState().editing).toBeNull();
    act(() => {
      first.editor.store.setState({ editing: 's2' });
    });
    const again = within(first.canvas).getByTestId('step-label-input');
    await userEvent.clear(again);
    await userEvent.type(again, '   {Enter}');
    act(() => {
      first.editor.store.setState({ editing: 's2' });
    });
    await userEvent.type(within(first.canvas).getByTestId('step-label-input'), ' bis');
    fireEvent.blur(within(first.canvas).getByTestId('step-label-input'));
    await vi.waitFor(() => {
      expect(callsTo(first.sent, 'PATCH', '/api/steps/s2').map((call) => call.body)).toEqual([{ label: 'Vérifier la demande bis' }]);
    });
  });
});

describe('the quick add (B-2.3-41 to B-2.3-48)', () => {
  const buttons = (canvas: HTMLElement, code: string) => within(within(canvas).getByTestId(`step-${code}`)).queryAllByRole('button').map((button) => button.getAttribute('data-testid'));

  it('four « + » on a step with a lane above and below; under and above only where a lane is', async () => {
    const { canvas } = await showEditor(mapOf({ lanes: [laneOf('a', 0), laneOf('b', 1), laneOf('c', 2)], steps: [stepOf('x', 'b', 0), stepOf('y', 'a', 1), stepOf('z', 'c', 2)] }));
    expect(buttons(canvas, 'VTE-E001')).toEqual(['quick-add-after', 'quick-add-before', 'quick-add-below', 'quick-add-above']);
    expect(buttons(canvas, 'VTE-E002')).toEqual(['quick-add-after', 'quick-add-before', 'quick-add-below']);
    expect(buttons(canvas, 'VTE-E003')).toEqual(['quick-add-after', 'quick-add-before', 'quick-add-above']);
  });

  it('each « + » inserts a step at its place, named at once', async () => {
    const sent = fakeApi({ 'POST /api/steps/s2/insert': { json: replyOf(sampleMap(), { id: 'new' }) } });
    const { canvas, editor } = await showEditor(sampleMap());
    await userEvent.click(within(within(canvas).getByTestId('step-VTE-E002')).getByTestId('quick-add-below'));
    await vi.waitFor(() => {
      expect(callsTo(sent, 'POST', '/api/steps/s2/insert')[0]?.body).toEqual({ position: 'below', label: 'Nouvelle étape', type: 'task' });
    });
    expect(editor.store.getState().editing).toBe('new');
  });

  it('none for a reader, in the link mode, with the RACI layer on', async () => {
    const reader = await showEditor(sampleMap({ rights: { structure: false, role: 'reader' } }));
    expect(buttons(reader.canvas, 'VTE-E002')).toEqual([]);
    const { canvas, editor } = await showEditor(sampleMap());
    act(() => {
      editor.store.setState({ mode: 'link' });
    });
    expect(buttons(canvas, 'VTE-E002')).toEqual([]);
    act(() => {
      editor.store.setState({ mode: 'normal' });
      editor.preferences.getState().set('showRaci', true);
    });
    expect(buttons(canvas, 'VTE-E002')).toEqual([]);
  });
});

describe('the states shown and the flows chosen (B-2.3-29, B-2.3-31, B-2.7-09, B-3.2-16)', () => {
  it('a step cut is greyed until its paste, on its own map; Escape gives it up', async () => {
    const { canvas, editor } = await showEditor(sampleMap());
    act(() => {
      editor.store.setState({ selection: { steps: ['s4'], flows: [] } });
    });
    await userEvent.keyboard('{Control>}x{/Control}');
    expect(within(canvas).getByTestId('step-VTE-E004')).toHaveClass('is-cut');
    expect(within(canvas).getByTestId('step-VTE-E002')).not.toHaveClass('is-cut');
    await userEvent.keyboard('{Escape}');
    expect(within(canvas).getByTestId('step-VTE-E004')).not.toHaveClass('is-cut');
  });

  it('a click chooses a flow; the map of a reply keeps every flow drawn, the steps declaring their size and handles', async () => {
    fakeApi({ 'PATCH /api/steps/s2': { json: replyOf({ ...sampleMap(), version: 4 }) } });
    const { canvas, editor } = await showEditor(sampleMap());
    fireEvent.click(within(canvas).getByTestId('rf__edge-f3'));
    expect(editor.store.getState().selection.flows).toEqual(['f3']);
    act(() => {
      editor.store.setState({ editing: 's2' });
    });
    await userEvent.type(within(canvas).getByTestId('step-label-input'), ' bis{Enter}');
    await vi.waitFor(() => {
      expect(editor.store.getState().map?.version).toBe(4);
    });
    expect(['f1', 'f2', 'f3', 'f4'].map((id) => within(canvas).getByTestId(`rf__edge-${id}`).querySelector('path')?.getAttribute('d') !== '')).toEqual([true, true, true, true]);
  });
});
