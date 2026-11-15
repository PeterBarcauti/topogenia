// RouteHandles.test.tsx
// The handles of the route of a flow chosen (ADR 091): shown for the flow chosen alone by who may change the map, never otherwise nor during an export; an end dragged onto the top of its step writes that side, a segment dragged writes its band or the middle of a column; Escape, a click, another button, a gesture cancelled or the handles gone write nothing.

import { act, fireEvent, within } from '@testing-library/react';
import { i18next } from '../../../shared/i18n/i18n.ts';
import { showEditor } from '../../../test/editor.tsx';
import { callsTo, fakeApi } from '../../../test/fakeApi.ts';
import { replyOf, sampleMap } from '../../../test/maps.ts';

beforeEach(async () => {
  await i18next.changeLanguage('fr');
});

/** The point of the screen of a point of the canvas, by the transform of the view. */
function client(canvas: HTMLElement, point: { x: number; y: number }) {
  const transform = canvas.querySelector<HTMLElement>('.react-flow__viewport')?.style.transform ?? '';
  const [x, y, zoom] = (/translate\(([-\d.]+)px, ?([-\d.]+)px\) scale\(([-\d.]+)\)/.exec(transform) ?? []).slice(1).map(Number);
  return { clientX: point.x * (zoom ?? 1) + (x ?? 0), clientY: point.y * (zoom ?? 1) + (y ?? 0), pointerId: 1 };
}

/** The top left corner of the box of a step, from its node. */
function cornerOf(canvas: HTMLElement, stepId: string) {
  const transform = canvas.querySelector<HTMLElement>(`.react-flow__node[data-id="${stepId}"]`)?.style.transform ?? '';
  const [x, y] = (/translate\(([-\d.]+)px, ?([-\d.]+)px\)/.exec(transform) ?? []).slice(1).map(Number);
  return { x: x ?? 0, y: y ?? 0 };
}

/** The writes queued so far, all answered: a write that sends nothing waits for them (B-2.6-09). */
const settled = async (editor: Awaited<ReturnType<typeof showEditor>>['editor']) => {
  await act(async () => {
    await editor.write(() => null);
  });
};

/** The point of the screen of the middle of a side of the box of a step. */
function sideOf(canvas: HTMLElement, stepId: string, side: 'top' | 'right') {
  const corner = cornerOf(canvas, stepId);
  return client(canvas, side === 'top' ? { x: corner.x + 85, y: corner.y + 2 } : { x: corner.x + 170, y: corner.y + 32 });
}

const choose = (editor: Awaited<ReturnType<typeof showEditor>>['editor'], flows: string[], steps: string[] = []) => {
  act(() => {
    editor.store.setState({ selection: { steps, flows } });
  });
};

describe('the handles of the route of a flow (ADR 091)', () => {
  it('are shown for one flow chosen alone, by who may change the map, and never with another object chosen, in the link mode or during an export', async () => {
    const { editor, canvas } = await showEditor(sampleMap());
    const view = within(canvas);
    expect(view.queryByTestId('route-handles')).toBeNull();
    choose(editor, ['f2']);
    expect([view.getByTestId('route-end-source').getAttribute('aria-label'), view.getByTestId('route-end-target').getAttribute('aria-label')])
      .toEqual(['Départ de la flèche', 'Arrivée de la flèche']);
    expect(view.getAllByTestId(/^route-segment-/).length).toBeGreaterThan(0);
    choose(editor, ['f2', 'f3']);
    expect(view.queryByTestId('route-handles')).toBeNull();
    choose(editor, ['f2'], ['s1']);
    expect(view.queryByTestId('route-handles')).toBeNull();
    choose(editor, ['f2']);
    act(() => {
      editor.store.setState({ mode: 'link' });
    });
    expect(view.queryByTestId('route-handles')).toBeNull();
    act(() => {
      editor.store.setState({ mode: 'normal', exporting: { format: 'png', caption: null } });
    });
    expect(view.queryByTestId('route-handles')).toBeNull();
  });

  it('are not shown to a reader', async () => {
    const { editor, canvas } = await showEditor(sampleMap({ rights: { structure: false, role: 'reader' } }));
    choose(editor, ['f2']);
    expect(within(canvas).queryByTestId('route-handles')).toBeNull();
  });

});

describe('the gestures on the handles of the route of a flow (ADR 091)', () => {
  it('an end dragged onto the top point of its step writes that side, the four points of the step shown meanwhile', async () => {
    const sent = fakeApi({ 'PUT /api/flows/f1/route': { json: replyOf(sampleMap()) } });
    const { editor, canvas } = await showEditor(sampleMap());
    choose(editor, ['f1']);
    const handle = within(canvas).getByTestId('route-end-source');
    const top = sideOf(canvas, 's1', 'top');
    fireEvent.pointerDown(handle, sideOf(canvas, 's1', 'right'));
    fireEvent.pointerMove(handle, top);
    expect(['top', 'right', 'bottom', 'left'].map((side) => within(canvas).queryByTestId(`route-point-${side}`) !== null)).toEqual([true, true, true, true]);
    expect(editor.store.getState().routeDraft).toEqual({ flowId: 'f1', route: { source: 'top' }, sent: false });
    fireEvent.pointerUp(handle, top);
    await vi.waitFor(() => {
      expect(callsTo(sent, 'PUT', '/api/flows/f1/route').map((call) => call.body)).toEqual([{ source_side: 'top', target_side: null, via: null }]);
    });
  });

  it('an end held snaps to the point of its step it would take, and stays on its own point out of reach, where a release writes nothing (audit 3.1.0 3.7, F-81)', async () => {
    const sent = fakeApi({});
    const { editor, canvas } = await showEditor(sampleMap());
    choose(editor, ['f1']);
    const view = within(canvas);
    const handle = view.getByTestId('route-end-source');
    const corner = cornerOf(canvas, 's1');
    fireEvent.pointerDown(handle, sideOf(canvas, 's1', 'right'));
    // Near the top point, off it: the end shows where it would go.
    fireEvent.pointerMove(handle, client(canvas, { x: corner.x + 100, y: corner.y - 12 }));
    expect(handle.getAttribute('style')).toBe(view.getByTestId('route-point-top').getAttribute('style'));
    // Far from every point: the end goes back to its point, the right one, and a release there changes nothing.
    fireEvent.pointerMove(handle, client(canvas, { x: corner.x + 85, y: corner.y - 300 }));
    expect(handle.getAttribute('style')).toBe(view.getByTestId('route-point-right').getAttribute('style'));
    fireEvent.pointerUp(handle, client(canvas, { x: corner.x + 85, y: corner.y - 300 }));
    await settled(editor);
    expect(callsTo(sent, 'PUT', '/api/flows/f1/route')).toEqual([]);
  });

  it('a segment dragged down to the band under its line writes that band', async () => {
    const sent = fakeApi({ 'PUT /api/flows/f1/route': { json: replyOf(sampleMap()) } });
    const { editor, canvas } = await showEditor(sampleMap());
    choose(editor, ['f1']);
    const handle = within(canvas).getAllByTestId(/^route-segment-/)[0] as HTMLElement;
    const corner = cornerOf(canvas, 's1');
    const under = client(canvas, { x: corner.x + 200, y: corner.y + 64 + 18 });
    fireEvent.pointerDown(handle, { ...under, clientY: under.clientY - 20 });
    fireEvent.pointerMove(handle, under);
    fireEvent.pointerUp(handle, under);
    await vi.waitFor(() => {
      expect(callsTo(sent, 'PUT', '/api/flows/f1/route').map((call) => call.body))
        .toEqual([{ source_side: null, target_side: null, via: { aisle: null, column: null, lane_id: 'l1', row: 0, edge: 'bottom' } }]);
    });
  });

  it('a segment going down dragged to the middle of a column writes that column, not an aisle', async () => {
    const sent = fakeApi({ 'PUT /api/flows/f3/route': { json: replyOf(sampleMap()) } });
    const { editor, canvas } = await showEditor(sampleMap());
    choose(editor, ['f3']);
    const handle = within(canvas).getAllByTestId(/^route-segment-/).find((item) => item.classList.contains('is-vertical')) as HTMLElement;
    const corner = cornerOf(canvas, 's2');
    // The middle of the column of s2, under it.
    const middle = client(canvas, { x: corner.x + 85, y: corner.y + 150 });
    fireEvent.pointerDown(handle, { ...middle, clientX: middle.clientX + 60 });
    fireEvent.pointerMove(handle, middle);
    fireEvent.pointerUp(handle, middle);
    await vi.waitFor(() => {
      expect(callsTo(sent, 'PUT', '/api/flows/f3/route').map((call) => call.body))
        .toEqual([{ source_side: null, target_side: null, via: { aisle: null, column: 1, lane_id: null, row: null, edge: null } }]);
    });
  });
});

describe('the gestures on the handles of the route of a flow that write nothing (ADR 091)', () => {
  it('Escape gives the gesture up: the map drawn as it is, nothing written', async () => {
    const sent = fakeApi({});
    const { editor, canvas } = await showEditor(sampleMap());
    choose(editor, ['f1']);
    const handle = within(canvas).getByTestId('route-end-source');
    const top = sideOf(canvas, 's1', 'top');
    fireEvent.pointerDown(handle, sideOf(canvas, 's1', 'right'));
    fireEvent.pointerMove(handle, top);
    expect(editor.store.getState().routeDraft?.route).toEqual({ source: 'top' });
    fireEvent.keyDown(window, { key: 'Escape' });
    expect(editor.store.getState().routeDraft).toBeNull();
    fireEvent.pointerUp(handle, top);
    await settled(editor);
    expect(sent).toEqual([]);
  });

  it('a click on a handle, a press of another button, or a gesture the browser cancels writes nothing and leaves the map as it is', async () => {
    const sent = fakeApi({});
    const { editor, canvas } = await showEditor(sampleMap());
    choose(editor, ['f1']);
    const view = within(canvas);
    const right = sideOf(canvas, 's1', 'right');
    fireEvent.pointerDown(view.getByTestId('route-end-source'), right);
    fireEvent.pointerMove(view.getByTestId('route-end-source'), { ...right, clientX: right.clientX + 2 });
    fireEvent.pointerUp(view.getByTestId('route-end-source'), right);
    const segment = view.getAllByTestId(/^route-segment-/)[0] as HTMLElement;
    fireEvent.pointerDown(segment, { ...right, button: 0 });
    fireEvent.pointerUp(segment, right);
    fireEvent.pointerDown(view.getByTestId('route-end-source'), { ...right, button: 2 });
    expect(view.queryByTestId('route-point-top')).toBeNull();
    fireEvent.pointerMove(view.getByTestId('route-end-source'), sideOf(canvas, 's1', 'top'));
    fireEvent.pointerUp(view.getByTestId('route-end-source'), sideOf(canvas, 's1', 'top'));
    fireEvent.pointerDown(view.getByTestId('route-end-source'), right);
    fireEvent.pointerMove(view.getByTestId('route-end-source'), sideOf(canvas, 's1', 'top'));
    fireEvent.pointerCancel(view.getByTestId('route-end-source'), sideOf(canvas, 's1', 'top'));
    await settled(editor);
    expect(sent).toEqual([]);
    expect(editor.store.getState().routeDraft).toBeNull();
  });

  it('another choice during a gesture takes the handles and the route drawn away, nothing written', async () => {
    const sent = fakeApi({});
    const { editor, canvas } = await showEditor(sampleMap());
    choose(editor, ['f1']);
    fireEvent.pointerDown(within(canvas).getByTestId('route-end-source'), sideOf(canvas, 's1', 'right'));
    fireEvent.pointerMove(within(canvas).getByTestId('route-end-source'), sideOf(canvas, 's1', 'top'));
    expect(editor.store.getState().routeDraft?.flowId).toBe('f1');
    choose(editor, [], ['s2']);
    expect(within(canvas).queryByTestId('route-handles')).toBeNull();
    expect(editor.store.getState().routeDraft).toBeNull();
    await settled(editor);
    expect(sent).toEqual([]);
  });

});
