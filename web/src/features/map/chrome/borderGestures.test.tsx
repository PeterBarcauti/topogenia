// borderGestures.test.tsx
// The border between two lane headers (ADR 096): pulled, what is under it goes down with the hand, the lane above taller at its bottom or the lane below at its top; double-clicked, the empty rows on each side taken out; its « + » adds a lane once no double click came; Escape gives up; the same from the menu of a lane.

import { fireEvent, screen } from '@testing-library/react';
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

const head = (code: string) => screen.getByTestId(`lane-head-${code}`);
const place = (code: string) => [head(code).style.top, head(code).style.height];
const grip = (border: number) => screen.getByTestId(`lane-grip-${String(border)}`);
const band = (code: string) => screen.queryByTestId(`row-band-${code}`);
const pull = (element: HTMLElement, from: number, to: number) => {
  fireEvent.pointerDown(element, { button: 0, clientY: from, pointerId: 1 });
  fireEvent.pointerMove(element, { clientY: to, pointerId: 1 });
};
/** The sample map with the end step VIS-E004 on the row 1 of the second lane: its row 0 is empty. */
const lowered = () => sampleMap({ steps: sampleMap().steps.map((step) => (step.id === 's4' ? { ...step, row: 1 } : step)) });

describe('a border pulled (ADR 096)', () => {
  it('down: the lane above grows with the hand, a row each height of a row, its new rows in its colour; released, they stay, nothing sent', async () => {
    const sent = fakeApi({});
    const { editor } = await showEditor(sampleMap());
    expect(grip(1)).toHaveAttribute('title', 'Tirer pour ajouter une rangée, double-cliquer pour retirer une rangée vide');
    pull(grip(1), 136, 170);
    expect([place('VIS-C01'), place('VIS-C02'), band('VIS-C01') !== null]).toEqual([['16px', '240px'], ['256px', '120px'], true]);
    fireEvent.pointerMove(grip(1), { clientY: 400, pointerId: 1 });
    expect(place('VIS-C01')).toEqual(['16px', '360px']);
    fireEvent.pointerUp(grip(1), { clientY: 400, pointerId: 1 });
    await vi.waitFor(() => {
      expect([editor.store.getState().rowsAtLeast, editor.store.getState().rowsDraft]).toEqual([{ l1: 3 }, null]);
    });
    expect([place('VIS-C01'), band('VIS-C01'), sent]).toEqual([['16px', '360px'], null, []]);
  });

  it('up: the lane below gains a row at its top, its steps drawn lower at once; released, the host inserts it', async () => {
    const sent = fakeApi({ 'POST /api/lanes/l2/rows': { json: replyOf(lowered()) } });
    const { editor } = await showEditor(sampleMap());
    pull(grip(1), 136, 100);
    expect([place('VIS-C01'), place('VIS-C02'), band('VIS-C02') !== null]).toEqual([['16px', '120px'], ['136px', '240px'], true]);
    expect(screen.getByTestId('rf__node-s4').style.transform).toContain('268px');
    fireEvent.pointerUp(grip(1), { clientY: 100, pointerId: 1 });
    await vi.waitFor(() => {
      expect(callsTo(sent, 'POST', '/api/lanes/l2/rows')[0]?.body).toEqual({ row: 0, count: 1 });
    });
    await vi.waitFor(() => {
      expect([editor.store.getState().rowsDraft, place('VIS-C02'), band('VIS-C02')]).toEqual([null, ['136px', '240px'], null]);
    });
  });

  it('Escape gives the pull up; above the first lane pulled down, under the last pulled up, nothing is drawn', async () => {
    const sent = fakeApi({});
    const { editor } = await showEditor(sampleMap());
    pull(grip(1), 136, 300);
    expect(band('VIS-C01')).not.toBeNull();
    await userEvent.keyboard('{Escape}');
    expect([editor.store.getState().rowsDraft, place('VIS-C01')]).toEqual([null, ['16px', '120px']]);
    fireEvent.pointerUp(grip(1), { clientY: 300, pointerId: 1 });
    pull(grip(0), 16, 200);
    const downAbove = editor.store.getState().rowsDraft;
    fireEvent.pointerUp(grip(0), { clientY: 200, pointerId: 1 });
    pull(grip(2), 256, 100);
    const upUnder = editor.store.getState().rowsDraft;
    fireEvent.pointerUp(grip(2), { clientY: 100, pointerId: 1 });
    expect([downAbove, upUnder, editor.store.getState().rowsAtLeast, sent]).toEqual([null, null, {}, []]);
  });
});

describe('the « + » and a double click on a border (ADR 096)', () => {
  it('the « + » is pulled as the border, its click then adds no lane; a click alone adds one once no second click came', async () => {
    const sent = fakeApi({ [`POST /api/maps/${MAP_ID}/lanes`]: { json: replyOf(sampleMap(), { id: 'l3' }) } });
    const { editor } = await showEditor(sampleMap());
    const plus = screen.getByTestId('lane-add-2');
    pull(plus, 256, 300);
    fireEvent.pointerUp(plus, { clientY: 300, pointerId: 1 });
    // The click of the mouse that ends the pull (a click of a mouse counts one at least).
    fireEvent.click(plus, { detail: 1 });
    await vi.waitFor(() => {
      expect(editor.store.getState().rowsAtLeast).toEqual({ l2: 2 });
    });
    await userEvent.click(screen.getByTestId('lane-add-2'));
    expect(callsTo(sent, 'POST', `/api/maps/${MAP_ID}/lanes`)).toHaveLength(0);
    await vi.waitFor(() => {
      expect(callsTo(sent, 'POST', `/api/maps/${MAP_ID}/lanes`)).toHaveLength(1);
    });
  });

  it('a line pulled then given up leaves the « + » to the keyboard: Enter on it adds a lane (audit 3.1 V2, 5.4.4)', async () => {
    const sent = fakeApi({ [`POST /api/maps/${MAP_ID}/lanes`]: { json: replyOf(sampleMap(), { id: 'l3' }) } });
    await showEditor(sampleMap());
    pull(grip(1), 136, 300);
    await userEvent.keyboard('{Escape}');
    fireEvent.pointerUp(grip(1), { clientY: 300, pointerId: 1 });
    screen.getByTestId('lane-add-2').focus();
    await userEvent.keyboard('{Enter}');
    await vi.waitFor(() => {
      expect(callsTo(sent, 'POST', `/api/maps/${MAP_ID}/lanes`)).toHaveLength(1);
    });
  });

  it('double-clicked: the empty last row of the lane above goes, and the empty first row of the lane below, its steps going up', async () => {
    const sent = fakeApi({ 'DELETE /api/lanes/l2/rows/0': { json: replyOf(sampleMap()) } });
    const { editor } = await showEditor(lowered());
    editor.store.setState({ rowsAtLeast: { l1: 2 } });
    await userEvent.dblClick(grip(1));
    await vi.waitFor(() => {
      expect(callsTo(sent, 'DELETE', '/api/lanes/l2/rows/0')).toHaveLength(1);
    });
    expect(editor.store.getState().rowsAtLeast).toEqual({ l1: 1 });
  });

  it('double-clicked where both rows hold something, or on the « + »: nothing goes and no lane is added', async () => {
    const sent = fakeApi({});
    const { editor } = await showEditor(sampleMap());
    await userEvent.dblClick(grip(1));
    await userEvent.dblClick(screen.getByTestId('lane-add-1'));
    await new Promise((resolve) => {
      setTimeout(resolve, 700);
    });
    expect([sent, editor.store.getState().rowsAtLeast]).toEqual([[], {}]);
  });
});

describe('a lane taller or shorter from its menu (ADR 096)', () => {
  it('a row at its bottom kept by the editor, taken away again; a row at its top inserted by the host; the empty top row taken out', async () => {
    const sent = fakeApi({ 'POST /api/lanes/l2/rows': { json: replyOf(lowered()) }, 'DELETE /api/lanes/l2/rows/0': { json: replyOf(sampleMap()) } });
    const { editor } = await showEditor(sampleMap());
    const choose = async (code: string, id: string) => {
      fireEvent.contextMenu(head(code));
      await userEvent.click(screen.getByTestId(`menu-lane-lane.row.${id}`));
    };
    await choose('VIS-C01', 'bottom');
    await vi.waitFor(() => {
      expect(editor.store.getState().rowsAtLeast).toEqual({ l1: 2 });
    });
    await choose('VIS-C01', 'remove_bottom');
    await vi.waitFor(() => {
      expect(editor.store.getState().rowsAtLeast).toEqual({ l1: 1 });
    });
    await choose('VIS-C02', 'top');
    await vi.waitFor(() => {
      expect(callsTo(sent, 'POST', '/api/lanes/l2/rows')[0]?.body).toEqual({ row: 0, count: 1 });
    });
    await vi.waitFor(() => {
      expect(place('VIS-C02')).toEqual(['136px', '240px']);
    });
    await choose('VIS-C02', 'remove_top');
    await vi.waitFor(() => {
      expect(callsTo(sent, 'DELETE', '/api/lanes/l2/rows/0')).toHaveLength(1);
    });
  });

  it('refused by the host (a step put on the row since): the empty rows the lane shows stay as they were', async () => {
    const sent = fakeApi({ 'DELETE /api/lanes/l2/rows/0': { refused: 409, code: 'RANGEE_NON_VIDE', message: 'Cette rangée n’est plus vide.' } });
    const { editor } = await showEditor(lowered());
    editor.store.setState({ rowsAtLeast: { l2: 4 } });
    fireEvent.contextMenu(head('VIS-C02'));
    await userEvent.click(screen.getByTestId('menu-lane-lane.row.remove_top'));
    await vi.waitFor(() => {
      expect(callsTo(sent, 'DELETE', '/api/lanes/l2/rows/0')).toHaveLength(1);
    });
    await new Promise((resolve) => {
      setTimeout(resolve, 50);
    });
    expect(editor.store.getState().rowsAtLeast).toEqual({ l2: 4 });
  });
});
