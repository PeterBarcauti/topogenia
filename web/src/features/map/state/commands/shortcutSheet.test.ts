// shortcutSheet.test.ts
// The sheet of the keyboard shortcuts never differs from the keyboard: every key command of the map on one line and one only, no line for a command the keyboard does not run, every key shown a shortcut of its command; each other key of the sheet heard (lot 1 of the 3.4, ADR 109; the search field, ADR 110).

import { act, fireEvent, render, screen, within } from '@testing-library/react';
import { createElement } from 'react';
import { App } from '../../../../app/App.tsx';
import { i18next } from '../../../../shared/i18n/i18n.ts';
import { toasts } from '../../../../shared/ui/toasts.ts';
import { showEditor } from '../../../../test/editor.tsx';
import { fakeApi } from '../../../../test/fakeApi.ts';
import { PROJECT_ID, sampleMap } from '../../../../test/maps.ts';
import { fakeSource, profileOf } from '../../../../test/profiles.ts';
import { sheetViewOf } from '../../../../test/sheets.ts';
import { wheelView, type WheelTurn } from '../../canvas/useHeaderWheel.ts';
import { EMPTY_SEARCH, searchStore } from '../../search/searchStore.ts';
import { closeSearch } from '../../search/useSearch.ts';
import { KEY_COMMANDS } from './mapCommands.ts';
import { SHORTCUT_SHEET, keysOf, type CommandLine, type OtherLine, type SheetLine } from './shortcutSheet.ts';

beforeEach(async () => {
  await i18next.changeLanguage('fr');
});

afterEach(() => {
  act(() => {
    toasts.setState({ toasts: [] });
  });
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
});

const lines: readonly SheetLine[] = SHORTCUT_SHEET.flatMap((section) => section.lines);
const commandLines = lines.filter((line): line is CommandLine => 'ids' in line);
const otherLines = lines.filter((line): line is OtherLine => 'other' in line);
const keyIds = KEY_COMMANDS.map((command) => command.id);
const shortcutsOf = (id: string): readonly string[] => KEY_COMMANDS.find((command) => command.id === id)?.shortcuts ?? [];
const lineOf = (id: string): CommandLine | undefined => commandLines.find((line) => line.ids.includes(id));

describe('the sheet and the keyboard (ADR 109)', () => {
  it('every key command of the map is on one line of the sheet, and only one', () => {
    const named = commandLines.flatMap((line) => line.ids);
    expect(keyIds.filter((id) => named.filter((other) => other === id).length !== 1)).toEqual([]);
    expect(named).toHaveLength(keyIds.length);
  });

  it('no line names a command the keyboard does not run', () => {
    expect(commandLines.flatMap((line) => line.ids).filter((id) => !keyIds.includes(id))).toEqual([]);
    expect(commandLines.filter((line) => line.ids.length === 0)).toEqual([]);
  });

  it('every key shown is a shortcut of its command', () => {
    expect(commandLines.flatMap((line) => keysOf(line).filter((key) => !line.ids.some((id) => shortcutsOf(id).includes(key))))).toEqual([]);
    expect(lines.filter((line) => keysOf(line).length === 0)).toEqual([]);
    // A key that is not a command is not one a command takes.
    expect(otherLines.flatMap((line) => line.keys).filter((key) => KEY_COMMANDS.some((command) => command.shortcuts?.includes(key) === true))).toEqual([]);
    // Show all by Shift+1 and the sheet by ?, their doubles (Shift+! and Shift+?) left out, as Shift+V and Shift+H.
    expect(['fit', 'help.keys', 'tool.select', 'tool.hand'].map((id) => keysOf(lineOf(id) ?? { ids: [] }))).toEqual([['Shift+1'], ['?'], ['V'], ['H']]);
  });
});

const node = (canvas: HTMLElement, id: string) => canvas.querySelector(`.react-flow__node[data-id="${id}"]`) as HTMLElement;

/** A turn of the wheel on the headers of the lanes, with the modifiers given. */
const turn = (changes: Partial<WheelTurn>): WheelTurn => ({ deltaX: 0, deltaY: 100, deltaMode: 0, ctrlKey: false, shiftKey: false, pointer: { x: 0, y: 0 }, ...changes });

describe('the other keys of the sheet are each heard: Space held, Enter in the search, Enter on a step, a character typed, Shift+F10, F1, Ctrl+S', () => {
  it('they are the ones heard below', () => {
    expect(otherLines.map((line) => line.other)).toEqual(['space', 'wheel', 'zoom', 'search', 'enter', 'menu', 'type', 'online', 'saved']);
  });

  it('Space held gives the Hand, released with the key', async () => {
    const { editor, canvas } = await showEditor(sampleMap());
    fireEvent.keyDown(canvas, { key: ' ' });
    expect(editor.store.getState().spaceHand).toBe(true);
    fireEvent.keyUp(canvas, { key: ' ' });
    expect(editor.store.getState().spaceHand).toBe(false);
  });

  it('the wheel scrolls, sideways with Shift, and zooms with Ctrl', () => {
    const view = { x: 192, y: 16, zoom: 1 };
    const [down, sideways, zoomed] = [turn({}), turn({ shiftKey: true }), turn({ ctrlKey: true })].map((given) => wheelView(view, given));
    expect([down?.x, down?.y !== view.y, sideways?.x !== view.x, sideways?.y, zoomed?.zoom !== view.zoom]).toEqual([192, true, true, 16, true]);
  });

  it('Enter in the search field goes to the next result, Shift+Enter to the previous one (ADR 110)', async () => {
    const hits = ['s1', 's2'].map((id) => ({ kind: 'step' as const, step_id: id, lane_id: null, text: 'Demande', language: 'fr', start: 0, length: 7, marks: [{ start: 0, length: 7 }] }));
    fakeApi({ [`POST /api/projects/${PROJECT_ID}/search`]: { json: { total: 2, maps: [{ map_id: sampleMap().id, depth: 1, name: { text: 'Visites', source_language: 'fr', translations: {} }, hits }] } } });
    const { editor, canvas } = await showEditor(sampleMap());
    fireEvent.keyDown(canvas, { key: 'f', ctrlKey: true });
    fireEvent.change(screen.getByTestId('search-field'), { target: { value: 'demande' } });
    await vi.waitFor(() => {
      expect(screen.getAllByTestId(/^search-hit-/)).toHaveLength(2);
    });
    fireEvent.keyDown(screen.getByTestId('search-field'), { key: 'Enter' });
    fireEvent.keyDown(screen.getByTestId('search-field'), { key: 'Enter' });
    expect(editor.store.getState().selection.steps).toEqual(['s2']);
    fireEvent.keyDown(screen.getByTestId('search-field'), { key: 'Enter', shiftKey: true });
    expect(editor.store.getState().selection.steps).toEqual(['s1']);
    act(() => {
      closeSearch();
      searchStore.setState({ ...EMPTY_SEARCH });
    });
  });

  it('Enter on a step focused opens its sheet', async () => {
    fakeApi({ 'GET /api/steps/s2/sheet': { json: sheetViewOf() }, [`GET /api/projects/${PROJECT_ID}`]: { json: { members: [], sharing_group_label: null } } });
    const { editor, canvas } = await showEditor(sampleMap());
    act(() => {
      node(canvas, 's2').focus();
    });
    fireEvent.keyDown(node(canvas, 's2'), { key: 'Enter' });
    expect(editor.store.getState().openSheet).toBe('s2');
  });

  it('Shift+F10 is left to the browser, which opens the menu of the step', async () => {
    const { canvas } = await showEditor(sampleMap());
    expect(fireEvent.keyDown(node(canvas, 's2'), { key: 'F10', shiftKey: true })).toBe(true);
    fireEvent.contextMenu(node(canvas, 's2'), { clientX: 300, clientY: 120 });
    expect(screen.getByRole('menu', { name: 'Menu de l’étape' })).toBeInTheDocument();
  });

  it('a character typed on the step chosen alone begins its name', async () => {
    const { editor, canvas } = await showEditor(sampleMap());
    fireEvent.click(node(canvas, 's2'));
    fireEvent.keyDown(node(canvas, 's2'), { key: 'x' });
    expect([editor.store.getState().editing, editor.store.getState().editingSeed]).toEqual(['s2', { stepId: 's2', text: 'x' }]);
  });

  it('F1 opens the online help and Ctrl+S says everything is saved, on every page', async () => {
    fakeApi({ 'GET /api/projects': { json: [] }, 'GET /api/trash': { json: { retention_days: 30, projects: [] } } });
    const open = vi.spyOn(window, 'open').mockReturnValue(null);
    render(createElement(App, { source: fakeSource([profileOf()]).source }));
    const header = await screen.findByTestId('app-header');
    fireEvent.keyDown(document.body, { key: 'F1' });
    fireEvent.keyDown(within(header).getByTestId('header-brand'), { key: 's', ctrlKey: true });
    expect([open.mock.calls, toasts.getState().toasts.map((toast) => toast.text)]).toEqual([
      [['/aide/index.html', '_blank', 'noopener']], [i18next.t('app.autosaved')],
    ]);
  });
});
