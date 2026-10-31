// menus.test.tsx
// The context menus of the canvas and their behaviour, the field of the label of a flow, the questions before a deletion (annex B 2.3, 2.4).

import { act, fireEvent, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { i18next } from '../../../shared/i18n/i18n.ts';
import { fitInWindow, menuPlacement } from '../../../shared/ui/menuPlacement.ts';
import { showEditor } from '../../../test/editor.tsx';
import { callsTo, fakeApi } from '../../../test/fakeApi.ts';
import { MAP_ID, replyOf, sampleMap } from '../../../test/maps.ts';
import { setClipboard } from '../state/clipboard.ts';

beforeEach(async () => {
  await i18next.changeLanguage('fr');
  setClipboard(null);
});

afterEach(() => {
  vi.unstubAllGlobals();
});

const macroMap = () => {
  const map = sampleMap();
  return { ...map, steps: map.steps.map((step) => (step.id === 's2' ? { ...step, sub_map_id: 'sub', sub_map_empty: false } : step)) };
};
const rightClick = (element: Element, x = 300, y = 200) => fireEvent.contextMenu(element, { clientX: x, clientY: y });
const node = (canvas: HTMLElement, id: string) => within(canvas).getByTestId(`rf__node-${id}`);

describe('a context menu (B-2.4-01 to B-2.4-09)', () => {
  it('opens at the pointer, named, its first enabled entry focused; its entries show their choice, their red, their groups and their shortcuts', async () => {
    const { canvas } = await showEditor(sampleMap());
    rightClick(node(canvas, 's2'), 300, 50);
    const menu = screen.getByRole('menu', { name: 'Menu de l’étape' });
    expect([menu.style.left, menu.style.top]).toEqual(['300px', '50px']);
    expect(document.activeElement).toBe(within(menu).getByTestId('menu-step-step.sheet'));
    const task = within(menu).getByTestId('menu-step-step.type.task');
    expect([task.getAttribute('role'), task.getAttribute('aria-checked'), task.querySelector('.menu-check.is-on') !== null]).toEqual(['menuitemradio', 'true', true]);
    expect(within(menu).getByTestId('menu-step-paste.before')).toHaveAttribute('aria-disabled', 'true');
    expect(within(menu).getByTestId('menu-step-step.delete')).toHaveClass('menu-item--danger');
    expect(within(menu).getByTestId('menu-step-cut')).toHaveClass('menu-item--group');
    expect(within(menu).getByTestId('menu-step-cut').querySelector('kbd')).toHaveTextContent('Ctrl+X');
  });

  it('« Ouvrir la fiche » shows its key, Entrée, as « Renommer » shows F2 (AR-155)', async () => {
    const { canvas } = await showEditor(sampleMap());
    rightClick(node(canvas, 's2'), 300, 50);
    const menu = screen.getByRole('menu', { name: 'Menu de l’étape' });
    expect(['step.sheet', 'rename'].map((id) => within(menu).getByTestId(`menu-step-${id}`).querySelector('kbd')?.textContent)).toEqual(['Entrée', 'F2']);
  });

  it('stays in the window: 240 px wide, rows of 34 px for the reckoning; drawn higher than reckoned, it goes up as much', () => {
    expect(menuPlacement({ x: 900, y: 700 }, 10, { width: 1000, height: 768 })).toEqual({ x: 760, y: 412 });
    expect(menuPlacement({ x: 10, y: 10 }, 30, { width: 1000, height: 768 })).toEqual({ x: 10, y: 8 });
    expect([fitInWindow({ top: 106, bottom: 790, height: 684 }, 768), fitInWindow({ top: 10, bottom: 300, height: 290 }, 768), fitInWindow({ top: 0, bottom: 900, height: 900 }, 768)]).toEqual([76, null, 8]);
  });

  it('closes on Escape and on a click outside; an entry runs and closes it', async () => {
    const sent = fakeApi({ 'POST /api/steps/s2/insert': { json: replyOf(sampleMap(), { id: 'n' }) } });
    const { canvas } = await showEditor(sampleMap());
    rightClick(node(canvas, 's2'));
    await userEvent.keyboard('{Escape}');
    expect(screen.queryByRole('menu')).toBeNull();
    rightClick(node(canvas, 's2'));
    fireEvent.pointerDown(document.body);
    expect(screen.queryByRole('menu')).toBeNull();
    rightClick(node(canvas, 's2'));
    await userEvent.click(screen.getByTestId('menu-step-step.insert-after'));
    expect(screen.queryByRole('menu')).toBeNull();
    await vi.waitFor(() => {
      expect(callsTo(sent, 'POST', '/api/steps/s2/insert')[0]?.body).toMatchObject({ position: 'after' });
    });
  });

  it('« Ouvrir sa carte détaillée » opens the map of a detailed step (B-2.4-10)', async () => {
    const { canvas, opened } = await showEditor(macroMap());
    rightClick(node(canvas, 's2'), 300, 50);
    await userEvent.click(screen.getByTestId('menu-step-step.open'));
    expect(opened).toEqual(['/maps/sub']);
  });

  it('on a step of a selection of several: the menu of the selection (B-2.4-31)', async () => {
    const { canvas, editor } = await showEditor(sampleMap());
    act(() => {
      editor.store.setState({ selection: { steps: ['s2', 's3'], flows: [] } });
    });
    rightClick(node(canvas, 's3'));
    expect(within(screen.getByRole('menu', { name: 'Menu de la sélection' })).getByTestId('menu-selection-selection.delete')).toHaveTextContent('Supprimer les 2 étapes');
    await userEvent.keyboard('{Escape}');
    act(() => {
      editor.store.setState({ selection: { steps: ['s3'], flows: [] } });
    });
    rightClick(node(canvas, 's3'), 300, 50);
    expect(screen.getByRole('menu', { name: 'Menu de l’étape' })).toBeInTheDocument();
  });

  it('« Copier le code » writes the code to the clipboard of the system and says it (B-2.4-27)', async () => {
    const writeText = vi.fn(() => Promise.resolve());
    Object.defineProperty(navigator, 'clipboard', { configurable: true, value: { writeText } });
    const { canvas, notes } = await showEditor(sampleMap());
    rightClick(node(canvas, 's2'));
    await userEvent.click(screen.getByTestId('menu-step-step.copy-code'));
    await vi.waitFor(() => {
      expect(notes).toEqual([['Code VTE-E002 copié', 'success']]);
    });
    expect(writeText).toHaveBeenCalledWith('VTE-E002');
  });
});

describe('the menus of the background and of a flow (B-2.4-38 to B-2.4-44)', () => {
  it('the background: paste here and choose all; no menu when the right button moved the view by more than 5 px', async () => {
    const { canvas, editor } = await showEditor(sampleMap());
    const background = canvas.querySelector('.react-flow__pane') as HTMLElement;
    fireEvent.mouseDown(background, { button: 2, clientX: 400, clientY: 300 });
    rightClick(background, 410, 300);
    expect(screen.queryByRole('menu')).toBeNull();
    fireEvent.mouseDown(background, { button: 2, clientX: 400, clientY: 300 });
    rightClick(background, 403, 300);
    const menu = screen.getByRole('menu', { name: 'Menu de la carte' });
    expect(within(menu).getByTestId('menu-pane-paste.here')).toHaveAttribute('aria-disabled', 'true');
    await userEvent.click(within(menu).getByTestId('menu-pane-selectAll'));
    expect(editor.store.getState().selection.steps).toEqual(['s1', 's2', 's3', 's4']);
  });

  it('a flow, for who may change the map: its label typed in a floating field, Enter sends it; none for a reader', async () => {
    const sent = fakeApi({ 'PATCH /api/flows/f3': { json: replyOf(sampleMap()) } });
    const { canvas } = await showEditor(sampleMap());
    rightClick(within(canvas).getByTestId('rf__edge-f3'));
    await userEvent.click(screen.getByTestId('menu-flow-flow.label'));
    const field = within(screen.getByTestId('flow-label')).getByTestId('flow-label-input');
    expect([field, document.activeElement === field, field.getAttribute('maxlength')]).toEqual([field, true, '60']);
    await userEvent.clear(field);
    await userEvent.type(field, 'si conforme{Enter}');
    await vi.waitFor(() => {
      expect(callsTo(sent, 'PATCH', '/api/flows/f3')[0]?.body).toEqual({ label: 'si conforme' });
    });
    const reader = await showEditor(sampleMap({ rights: { structure: false, role: 'reader' } }));
    rightClick(within(reader.canvas).getByTestId('rf__edge-f3'));
    expect(screen.queryByRole('menu')).toBeNull();
  });

  it('a double click on a flow opens its field too; Escape gives it up (B-2.3-33)', async () => {
    const { canvas } = await showEditor(sampleMap());
    fireEvent.doubleClick(within(canvas).getByTestId('rf__edge-f3'));
    expect(screen.getByTestId('flow-label-input')).toHaveValue('oui');
    await userEvent.keyboard('{Escape}');
    expect(screen.queryByTestId('flow-label')).toBeNull();
  });
});

describe('the questions before a deletion (B-2.3-39, B-2.3-40, B-2.4-18, B-2.4-50, B-2.4-51)', () => {
  it('a detailed step alone: its map given to another step, then the step deleted, the flow stitched', async () => {
    const sent = fakeApi({ 'DELETE /api/steps/s2': { json: replyOf(sampleMap()) } });
    const { canvas, editor } = await showEditor(macroMap());
    act(() => {
      editor.store.setState({ selection: { steps: ['s2'], flows: [] } });
    });
    fireEvent.keyDown(node(canvas, 's1'), { key: 'Delete' });
    const dialog = screen.getByRole('dialog', { name: '« Vérifier la demande » a sa propre carte détaillée' });
    expect(within(dialog).getByTestId('macro-dialog-reattach')).toBeDisabled();
    await userEvent.selectOptions(within(dialog).getByTestId('macro-dialog-to'), 's3');
    await userEvent.click(within(dialog).getByTestId('macro-dialog-reattach'));
    await vi.waitFor(() => {
      expect([...(callsTo(sent, 'DELETE', '/api/steps/s2')[0]?.query.entries() ?? [])]).toEqual([['sub_map', 'reattach'], ['to', 's3'], ['stitch', 'true']]);
    });
  });

  it('a selection that holds a detailed step: confirmed, the maps deleted with it', async () => {
    const sent = fakeApi({ [`POST /api/maps/${MAP_ID}/deletion`]: { json: replyOf(sampleMap()) } });
    const { editor } = await showEditor(macroMap());
    act(() => {
      editor.store.setState({ selection: { steps: ['s2', 's3'], flows: [] } });
    });
    await userEvent.keyboard('{Delete}');
    await userEvent.click(within(screen.getByTestId('selection-delete')).getByTestId('selection-delete-confirm'));
    await vi.waitFor(() => {
      expect(callsTo(sent, 'POST', `/api/maps/${MAP_ID}/deletion`)[0]?.body).toEqual({ steps: ['s2', 's3'], flows: [], stitch: true, sub_maps: 'delete' });
    });
  });

  it('the detail of a detailed step: confirmed, then its map read and deleted', async () => {
    const sent = fakeApi({ 'GET /api/maps/sub': { json: { map: { ...sampleMap(), id: 'sub', version: 4 }, history: { undo: [], redo: [] } } }, 'DELETE /api/maps/sub': { json: replyOf(sampleMap()) } });
    const { canvas } = await showEditor(macroMap());
    rightClick(node(canvas, 's2'));
    await userEvent.click(screen.getByTestId('menu-step-step.delete-detail'));
    expect(screen.getByTestId('detail-delete')).toHaveTextContent('La carte détaillée de « Vérifier la demande » et toutes ses étapes seront supprimées.');
    await userEvent.click(screen.getByTestId('detail-delete-confirm'));
    await vi.waitFor(() => {
      expect(callsTo(sent, 'DELETE', '/api/maps/sub')[0]?.headers.get('If-Match')).toBe('"4"');
    });
  });

  it('the detail of a step whose map is empty goes without a question (B-2.4-18)', async () => {
    const sent = fakeApi({ 'GET /api/maps/sub': { json: { map: { ...sampleMap(), id: 'sub', version: 2 }, history: { undo: [], redo: [] } } }, 'DELETE /api/maps/sub': { json: replyOf(sampleMap()) } });
    const map = sampleMap();
    const { canvas } = await showEditor({ ...map, steps: map.steps.map((step) => (step.id === 's2' ? { ...step, sub_map_id: 'sub', sub_map_empty: true } : step)) });
    rightClick(node(canvas, 's2'), 300, 50);
    await userEvent.click(screen.getByTestId('menu-step-step.delete-detail'));
    expect(screen.queryByTestId('detail-delete')).toBeNull();
    await vi.waitFor(() => {
      expect(callsTo(sent, 'DELETE', '/api/maps/sub')[0]?.headers.get('If-Match')).toBe('"2"');
    });
  });

  it('a lane that holds steps: its steps moved to the lane chosen, or deleted with it', async () => {
    const sent = fakeApi({ 'DELETE /api/lanes/l2': { json: replyOf(sampleMap()) } });
    await showEditor(sampleMap());
    rightClick(screen.getByTestId('lane-head-VTE-C02'));
    await userEvent.click(screen.getByTestId('menu-lane-lane.delete'));
    expect(screen.getByRole('dialog', { name: 'Le couloir « Comptabilité » contient 1 étape' })).toBeInTheDocument();
    await userEvent.click(screen.getByTestId('lane-dialog-move'));
    rightClick(screen.getByTestId('lane-head-VTE-C02'));
    await userEvent.click(screen.getByTestId('menu-lane-lane.delete'));
    await userEvent.click(screen.getByTestId('lane-dialog-delete'));
    await vi.waitFor(() => {
      expect(callsTo(sent, 'DELETE', '/api/lanes/l2').map((call) => [...call.query.entries()])).toEqual([[['steps', 'move'], ['to', 'l1']], [['steps', 'delete']]]);
    });
  });
});
