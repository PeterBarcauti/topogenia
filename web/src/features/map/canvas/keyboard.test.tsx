// keyboard.test.tsx
// A step reached by the keyboard: Enter does what a click does, its sheet or its RACI panel taking the focus; its menu offers both; Escape gives the focus back to the step (NF08, S-NF-12).

import { act, fireEvent, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { i18next } from '../../../shared/i18n/i18n.ts';
import { showEditor } from '../../../test/editor.tsx';
import { callsTo, fakeApi } from '../../../test/fakeApi.ts';
import { PROJECT_ID, sampleMap } from '../../../test/maps.ts';
import { sheetViewOf } from '../../../test/sheets.ts';

let sent: ReturnType<typeof fakeApi>;

beforeEach(async () => {
  await i18next.changeLanguage('fr');
  sent = fakeApi({ 'GET /api/steps/s2/sheet': { json: sheetViewOf() }, [`GET /api/projects/${PROJECT_ID}`]: { json: { members: [], sharing_group_label: null } } });
});

afterEach(() => {
  vi.unstubAllGlobals();
});

const node = (canvas: HTMLElement, id: string) => canvas.querySelector(`.react-flow__node[data-id="${id}"]`) as HTMLElement;

/** The map shown (by default the sample one), the RACI layer on when asked, a step focused as the Tab key leaves it. */
async function focused(stepId: string, raci = false, map = sampleMap()) {
  const shown = await showEditor(map);
  if (raci)
    act(() => {
      shown.editor.preferences.getState().set('showRaci', true);
    });
  act(() => {
    node(shown.canvas, stepId).focus();
  });
  return shown;
}

describe('Enter on a step focused (NF08, S-NF-12, B-2.3-12)', () => {
  it('chooses the step and opens its sheet, which takes the focus; Escape closes it and gives the focus back to the step', async () => {
    const { canvas, editor } = await focused('s2');
    await userEvent.keyboard('{Enter}');
    const panel = await screen.findByTestId('sheet-panel');
    expect([editor.store.getState().openSheet, editor.store.getState().selection.steps]).toEqual(['s2', ['s2']]);
    expect(document.activeElement).toBe(panel);
    await userEvent.keyboard('{Escape}');
    expect(screen.queryByTestId('sheet-panel')).toBeNull();
    expect(document.activeElement).toBe(node(canvas, 's2'));
  });

  it('opens nothing on a start, as a click', async () => {
    const { editor } = await focused('s1');
    await userEvent.keyboard('{Enter}');
    expect([editor.store.getState().openSheet, editor.floating.getState().floating]).toEqual([null, null]);
  });

  it('on the RACI layer, opens the RACI panel next to the step, its first letter focused but not pressed by the key; Escape gives the focus back to the step', async () => {
    const { canvas, editor } = await focused('s2', true);
    await userEvent.keyboard('{Enter}');
    const panel = screen.getByTestId('raci-panel');
    expect([editor.store.getState().openSheet, editor.floating.getState().floating?.kind]).toEqual([null, 'raci']);
    expect(document.activeElement).toBe(within(panel).getAllByRole('button')[0]);
    expect(callsTo(sent, 'PUT', '/api/steps/s2/raci')).toEqual([]);
    await userEvent.keyboard('{Escape}');
    expect(screen.queryByTestId('raci-panel')).toBeNull();
    expect(document.activeElement).toBe(node(canvas, 's2'));
  });

  it('on the RACI layer, a step of the only lane has its R alone: the panel itself takes the focus, and gives it back to the step', async () => {
    const lone = sampleMap({ lanes: sampleMap().lanes.slice(0, 1), steps: sampleMap().steps.filter((step) => step.lane_id === 'l1'), flows: [] });
    const { canvas } = await focused('s2', true, lone);
    await userEvent.keyboard('{Enter}');
    const panel = screen.getByTestId('raci-panel');
    expect([within(panel).queryAllByRole('button'), document.activeElement]).toEqual([[], panel]);
    await userEvent.keyboard('{Escape}');
    expect([screen.queryByTestId('raci-panel'), document.activeElement]).toEqual([null, node(canvas, 's2')]);
  });

  it('in the link mode, chooses the source then the target, as a click', async () => {
    const { canvas, editor } = await focused('s2');
    act(() => {
      editor.store.setState({ mode: 'link' });
    });
    await userEvent.keyboard('{Enter}');
    expect(editor.store.getState().linkSource).toBe('s2');
    act(() => {
      node(canvas, 's3').focus();
    });
    await userEvent.keyboard('{Enter}');
    expect([editor.store.getState().mode, editor.store.getState().linkSource]).toEqual(['normal', null]);
  });
});

describe('the menu of a step from the keyboard (NF08, S-NF-12, B-2.4-01)', () => {
  it('Shift+F10 opens it on the step; « Ouvrir la fiche » opens the sheet, focused; Escape on the menu gives the focus back to the step', async () => {
    const { canvas } = await focused('s2');
    fireEvent.contextMenu(node(canvas, 's2'), { clientX: 300, clientY: 120 });
    const menu = screen.getByRole('menu', { name: 'Menu de l’étape' });
    expect(document.activeElement).toBe(within(menu).getByRole('menuitem', { name: 'Ouvrir la fiche' }));
    await userEvent.keyboard('{Escape}');
    expect(document.activeElement).toBe(node(canvas, 's2'));
    fireEvent.contextMenu(node(canvas, 's2'), { clientX: 300, clientY: 120 });
    await userEvent.keyboard('{Enter}');
    expect(document.activeElement).toBe(await screen.findByTestId('sheet-panel'));
  });

  it('on the RACI layer, « Rôles RACI… » opens the RACI panel by the menu, at (+12, −20) of it', async () => {
    const { canvas, editor } = await focused('s2', true);
    fireEvent.contextMenu(node(canvas, 's2'), { clientX: 300, clientY: 120 });
    await userEvent.click(within(screen.getByRole('menu')).getByRole('menuitem', { name: 'Rôles RACI…' }));
    expect(editor.floating.getState().floating).toEqual({ kind: 'raci', stepId: 's2', at: { x: 312, y: 100 } });
  });
});
