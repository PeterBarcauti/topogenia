// tools.test.tsx
// The tools of the canvas through the real events: Hand and Selection, the Space bar held, the rectangle, the arrows heard before React Flow, the drag and drop (annex B 2.1, 2.3, 2.5).

import { act, fireEvent, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { i18next } from '../../../shared/i18n/i18n.ts';
import { showEditor } from '../../../test/editor.tsx';
import { callsTo, fakeApi } from '../../../test/fakeApi.ts';
import { MAP_ID, replyOf, sampleMap } from '../../../test/maps.ts';

beforeEach(async () => {
  await i18next.changeLanguage('fr');
});

afterEach(() => {
  vi.unstubAllGlobals();
});

const node = (canvas: HTMLElement, id: string) => within(canvas).getByTestId(`rf__node-${id}`);

describe('Hand and Selection (B-2.1-10, B-2.3-01 to B-2.3-07, B-7-07)', () => {
  it('the Hand at each opening; V gives Selection, H the Hand back', async () => {
    const { canvas } = await showEditor(sampleMap());
    expect(canvas).toHaveClass('tool-hand');
    await userEvent.keyboard('v');
    expect(canvas).toHaveClass('tool-select');
    await userEvent.keyboard('h');
    expect(canvas).toHaveClass('tool-hand');
  });

  it('with Selection, the Space bar held gives the Hand for the time being, released with the key or when the window loses the focus', async () => {
    const user = userEvent.setup();
    const { canvas } = await showEditor(sampleMap());
    await user.keyboard('v');
    await user.keyboard('[Space>]');
    expect(canvas).toHaveClass('tool-hand');
    await user.keyboard('[/Space]');
    expect(canvas).toHaveClass('tool-select');
    await user.keyboard('[Space>]');
    expect(canvas).toHaveClass('tool-hand');
    act(() => {
      window.dispatchEvent(new Event('blur'));
    });
    expect(canvas).toHaveClass('tool-select');
  });

  it('Space on a focused button stays the key of the button', async () => {
    const { canvas } = await showEditor(sampleMap());
    await userEvent.keyboard('v');
    screen.getByTestId('ribbon-cut').focus();
    fireEvent.keyDown(screen.getByTestId('ribbon-cut'), { key: ' ' });
    expect(canvas).toHaveClass('tool-select');
  });

  it('with Selection, a rectangle drawn on the background takes the steps wholly inside it (B-2.3-01, B-2.3-02)', async () => {
    const { canvas, editor } = await showEditor(sampleMap());
    await userEvent.keyboard('v');
    const pane = canvas.querySelector('.react-flow__pane') as HTMLElement;
    // The view starts at x 192, y 16: s1 spans 222-392 and s2 442-612 on x, both 44-108 on y; s3 starts at 662.
    const pointer = { pointerId: 1, isPrimary: true, pointerType: 'mouse' };
    fireEvent.pointerDown(pane, { ...pointer, button: 0, clientX: 210, clientY: 30 });
    fireEvent.pointerMove(pane, { ...pointer, button: 0, buttons: 1, clientX: 400, clientY: 120 });
    fireEvent.pointerMove(pane, { ...pointer, button: 0, buttons: 1, clientX: 700, clientY: 120 });
    fireEvent.pointerUp(pane, { ...pointer, button: 0, clientX: 700, clientY: 120 });
    expect(editor.store.getState().selection.steps).toEqual(['s1', 's2']);
  });
});

describe('the arrows of the keyboard (B-2.5-21, B-2.5-22)', () => {
  it('are heard before React Flow: the chosen step moves by a cell on the host, not by a few pixels on the screen', async () => {
    const sent = fakeApi({ [`POST /api/maps/${MAP_ID}/move`]: { json: replyOf(sampleMap()) } });
    const { canvas, editor } = await showEditor(sampleMap());
    act(() => {
      editor.store.setState({ selection: { steps: ['s2'], flows: [] } });
    });
    const before = node(canvas, 's2').style.transform;
    node(canvas, 's2').focus();
    fireEvent.keyDown(node(canvas, 's2'), { key: 'ArrowRight' });
    await vi.waitFor(() => {
      expect(callsTo(sent, 'POST', `/api/maps/${MAP_ID}/move`)[0]?.body).toEqual({ steps: [{ id: 's2', lane_id: 'l1', column: 2, row: 0 }] });
    });
    expect(node(canvas, 's2').style.transform).toBe(before);
  });
});

describe('the drag and drop of steps (B-2.3-22, B-2.3-27)', () => {
  it('a step dropped lands in the cell of its centre, sent as one move; it stays where it was dropped until the host answers', async () => {
    let answer: (response: Response) => void = () => undefined;
    vi.stubGlobal('fetch', vi.fn(() => new Promise<Response>((resolve) => { answer = resolve; })));
    const { canvas } = await showEditor(sampleMap());
    const step = node(canvas, 's2');
    // The drag of React Flow (d3) follows the pointer on the window of the event (`view`), which jsdom will not take at construction.
    const mouse = (target: EventTarget, type: string, clientX: number, clientY: number) => {
      const event = new MouseEvent(type, { bubbles: true, cancelable: true, button: 0, buttons: type === 'mouseup' ? 0 : 1, clientX, clientY });
      Object.defineProperty(event, 'view', { value: window });
      target.dispatchEvent(event);
    };
    await act(async () => {
      // React Flow starts the drag at the first move past its threshold, and follows the pointer from there.
      mouse(step, 'mousedown', 500, 70);
      mouse(window, 'mousemove', 502, 70);
      mouse(window, 'mousemove', 962, 190);
      await Promise.resolve();
    });
    await act(async () => {
      mouse(window, 'mouseup', 962, 190);
      await Promise.resolve();
    });
    await vi.waitFor(() => {
      expect(fetch).toHaveBeenCalled();
    });
    const [url, init] = vi.mocked(fetch).mock.calls[0] as unknown as [string, RequestInit];
    expect([url, JSON.parse(init.body as string)]).toEqual([`/api/maps/${MAP_ID}/move`, { steps: [{ id: 's2', lane_id: 'l2', column: 3, row: 0 }] }]);
    expect(node(canvas, 's2').style.transform).toBe('translate(710px,148px)');
    answer(new Response(JSON.stringify(replyOf(sampleMap())), { status: 200 }));
    await vi.waitFor(() => {
      expect(node(canvas, 's2').style.transform).toBe('translate(250px,28px)');
    });
  });
});

/** A mouse event of the drag of React Flow (d3), with the window of the event that jsdom will not take at construction. */
function mouse(target: EventTarget, type: string, clientX: number, clientY: number): void {
  const event = new MouseEvent(type, { bubbles: true, cancelable: true, button: 0, buttons: type === 'mouseup' ? 0 : 1, clientX, clientY });
  Object.defineProperty(event, 'view', { value: window });
  target.dispatchEvent(event);
}

describe('a map that may not be changed (B-2.3-19, B-2.3-22)', () => {
  it('no step can be moved nor linked from its handles, and a drag sends nothing; with the right, every step can', async () => {
    vi.stubGlobal('fetch', vi.fn(() => new Promise<Response>(() => undefined)));
    const ids = ['s1', 's2', 's3', 's4'];
    const draggable = (canvas: HTMLElement) => ids.map((id) => node(canvas, id).classList.contains('draggable'));
    const linkable = (canvas: HTMLElement) => ids.flatMap((id) => [...node(canvas, id).querySelectorAll('.react-flow__handle')].map((handle) => handle.classList.contains('connectable')));
    const reader = await showEditor(sampleMap({ rights: { structure: false, role: 'reader' } }));
    expect([draggable(reader.canvas), linkable(reader.canvas)]).toEqual([Array<boolean>(4).fill(false), Array<boolean>(16).fill(false)]);
    const step = node(reader.canvas, 's2');
    await act(async () => {
      mouse(step, 'mousedown', 500, 70);
      mouse(window, 'mousemove', 502, 70);
      mouse(window, 'mousemove', 962, 190);
      await Promise.resolve();
    });
    await act(async () => {
      mouse(window, 'mouseup', 962, 190);
      await Promise.resolve();
    });
    expect(step.style.transform).toBe('translate(250px,28px)');
    expect(fetch).not.toHaveBeenCalled();
    const facilitator = await showEditor(sampleMap());
    expect([draggable(facilitator.canvas), linkable(facilitator.canvas)]).toEqual([Array<boolean>(4).fill(true), Array<boolean>(16).fill(true)]);
  });
});

describe('React Flow deletes nothing on its own (B-2.3-35, B-2.4-37)', () => {
  it('a step chosen and deleted stays drawn until the host answers', async () => {
    vi.stubGlobal('fetch', vi.fn(() => new Promise<Response>(() => undefined)));
    const { canvas, editor } = await showEditor(sampleMap());
    act(() => {
      editor.store.setState({ selection: { steps: ['s2'], flows: [] } });
    });
    fireEvent.keyDown(node(canvas, 's2'), { key: 'Delete' });
    fireEvent.keyDown(node(canvas, 's2'), { key: 'Backspace' });
    await vi.waitFor(() => {
      expect(fetch).toHaveBeenCalled();
    });
    expect(node(canvas, 's2')).toBeInTheDocument();
  });

  it('the frame of the steps taken by a rectangle opens the menu of the selection on a right click', async () => {
    const { canvas } = await showEditor(sampleMap());
    await userEvent.keyboard('v');
    const pane = canvas.querySelector('.react-flow__pane') as HTMLElement;
    const pointer = { pointerId: 1, isPrimary: true, pointerType: 'mouse' };
    fireEvent.pointerDown(pane, { ...pointer, button: 0, clientX: 210, clientY: 30 });
    fireEvent.pointerMove(pane, { ...pointer, button: 0, buttons: 1, clientX: 700, clientY: 120 });
    fireEvent.pointerUp(pane, { ...pointer, button: 0, clientX: 700, clientY: 120 });
    fireEvent.contextMenu(canvas.querySelector('.react-flow__nodesselection-rect') as HTMLElement, { clientX: 300, clientY: 80 });
    expect(within(screen.getByRole('menu', { name: 'Menu de la sélection' })).getByTestId('menu-selection-selection.delete')).toHaveTextContent('Supprimer les 2 étapes');
  });
});
