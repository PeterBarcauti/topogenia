// LaneEditing.test.tsx
// The lane headers edited: renamed in place, typed and coloured by their menu, reordered by a drag, a lane added between two; nothing of it for a reader (annex B 2.4, 2.12).

import { fireEvent, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { i18next } from '../../../shared/i18n/i18n.ts';
import { showEditor } from '../../../test/editor.tsx';
import { callsTo, fakeApi } from '../../../test/fakeApi.ts';
import { laneOf, MAP_ID, mapOf, replyOf, sampleMap } from '../../../test/maps.ts';
import { targetRank } from './laneDrag.ts';

beforeEach(async () => {
  await i18next.changeLanguage('fr');
});

afterEach(() => {
  vi.unstubAllGlobals();
});

const head = (code: string) => screen.getByTestId(`lane-head-${code}`);
const threeLanes = () => mapOf({ lanes: [laneOf('a', 0), laneOf('b', 1), laneOf('c', 2)] });

describe('renaming a lane (B-2.12-11 to B-2.12-13, B-2.12-22)', () => {
  it('a double click types its name in its header; Enter sends it', async () => {
    const sent = fakeApi({ 'PATCH /api/lanes/l1': { json: replyOf(sampleMap()) } });
    await showEditor(sampleMap());
    fireEvent.doubleClick(head('VTE-C01'));
    const field = within(head('VTE-C01')).getByTestId('lane-name-input');
    expect(field).toHaveValue('Commercial');
    await userEvent.clear(field);
    await userEvent.type(field, 'Ventes{Enter}');
    await vi.waitFor(() => {
      expect(callsTo(sent, 'PATCH', '/api/lanes/l1')[0]?.body).toEqual({ name: 'Ventes' });
    });
  });

  it('« Renommer » of its menu does the same; a new lane added is named at once', async () => {
    const sent = fakeApi({ [`POST /api/maps/${MAP_ID}/lanes`]: { json: replyOf(sampleMap(), { id: 'l2' }) } });
    const { editor } = await showEditor(sampleMap());
    fireEvent.contextMenu(head('VTE-C02'), { clientX: 50, clientY: 50 });
    await userEvent.click(screen.getByTestId('menu-lane-lane.rename'));
    expect(within(head('VTE-C02')).getByTestId('lane-name-input')).toHaveValue('Comptabilité');
    await userEvent.keyboard('{Escape}');
    await userEvent.click(screen.getByTestId('lane-add-1'));
    await vi.waitFor(() => {
      expect(callsTo(sent, 'POST', `/api/maps/${MAP_ID}/lanes`)[0]?.body).toMatchObject({ name: 'Acteur 3', type: 'person', sort_order: 1 });
    });
    expect(editor.store.getState().editing).toBe('lane:l2');
  });
});

describe('the menu of a lane (B-2.4-45 to B-2.4-48, B-2.12-24)', () => {
  it('opens from its « ⋯ » shown on hover, or a right click; its type and its colour change from it', async () => {
    const sent = fakeApi({ 'PATCH /api/lanes/l2': { json: replyOf(sampleMap()) } });
    await showEditor(sampleMap());
    await userEvent.click(screen.getByTestId('lane-more-VTE-C02'));
    const menu = screen.getByRole('menu', { name: 'Menu du couloir' });
    expect(within(menu).getAllByRole('menuitemradio').filter((item) => item.getAttribute('aria-checked') === 'true').map((item) => item.getAttribute('aria-label'))).toEqual(['Type : Service', 'Couleur : Lavande']);
    expect(within(menu).getByTestId('menu-lane-lane.colour.mint').querySelector('.menu-swatch')?.getAttribute('style')).toContain('--swatch: var(--lane-4-pastel)');
    await userEvent.click(within(menu).getByTestId('menu-lane-lane.colour.mint'));
    fireEvent.contextMenu(head('VTE-C02'));
    await userEvent.click(screen.getByTestId('menu-lane-lane.type.system'));
    await vi.waitFor(() => {
      expect(callsTo(sent, 'PATCH', '/api/lanes/l2').map((call) => call.body)).toEqual([{ color: '#D5F0E3' }, { type: 'system' }]);
    });
  });

  it('a reader has neither the menu, nor « ⋯ », nor « + », nor the field', async () => {
    await showEditor(sampleMap({ rights: { structure: false, role: 'reader' } }));
    fireEvent.contextMenu(head('VTE-C02'));
    fireEvent.doubleClick(head('VTE-C02'));
    expect([screen.queryByRole('menu'), screen.queryByTestId('lane-more-VTE-C02'), screen.queryAllByTestId(/^lane-add-/), screen.queryByTestId('lane-name-input')]).toEqual([null, null, [], null]);
  });
});

describe('the « + » between the lanes (B-2.12-20)', () => {
  it('one above the first lane, one between two lanes, one under the last, at their borders on the screen', async () => {
    await showEditor(threeLanes());
    expect(screen.getAllByTestId(/^lane-add-/).map((button) => [button.getAttribute('data-testid'), button.style.top])).toEqual([
      ['lane-add-0', '16px'], ['lane-add-1', '136px'], ['lane-add-2', '256px'], ['lane-add-3', '376px'],
    ]);
    expect(screen.getByTestId('lane-add-2')).toHaveAttribute('aria-label', 'Ajouter un couloir');
  });
});

describe('reordering the lanes (B-2.12-14 to B-2.12-18)', () => {
  it('the lane aimed is the one whose band holds the centre of the header moved', () => {
    const tops = [0, 120, 360];
    const heights = [120, 240, 120];
    expect([targetRank(0, 50, tops, heights), targetRank(0, 70, tops, heights), targetRank(0, 900, tops, heights), targetRank(2, -500, tops, heights)]).toEqual([0, 1, 2, 0]);
  });

  it('a header dragged follows the pointer with a shadow, the lane aimed underlined; released, the new order is sent', async () => {
    const sent = fakeApi({ [`PUT /api/maps/${MAP_ID}/lane-order`]: { json: replyOf(threeLanes()) } });
    await showEditor(threeLanes());
    fireEvent.pointerDown(head('VTE-C01'), { button: 0, clientY: 100, pointerId: 1 });
    fireEvent.pointerMove(head('VTE-C01'), { clientY: 230, pointerId: 1 });
    expect([head('VTE-C01').className, head('VTE-C01').style.top, head('VTE-C02').className]).toEqual(['lane-head is-dragging', '146px', 'lane-head is-drop-target']);
    fireEvent.pointerUp(head('VTE-C01'), { clientY: 230, pointerId: 1 });
    await vi.waitFor(() => {
      expect(callsTo(sent, 'PUT', `/api/maps/${MAP_ID}/lane-order`)[0]?.body).toEqual(['b', 'a', 'c']);
    });
  });

  it('a move of 4 px or less is no drag: nothing is sent', async () => {
    const sent = fakeApi({});
    await showEditor(threeLanes());
    fireEvent.pointerDown(head('VTE-C02'), { button: 0, clientY: 100, pointerId: 1 });
    fireEvent.pointerMove(head('VTE-C02'), { clientY: 104, pointerId: 1 });
    expect(head('VTE-C01').className).toBe('lane-head');
    fireEvent.pointerUp(head('VTE-C02'), { clientY: 104, pointerId: 1 });
    expect(sent).toEqual([]);
  });
});
