// sideDock.test.tsx
// The frame of the panels docked on the right (spec 3.0 section 15.2, ADR 086): the width by the handle, the mouse or the keys, kept; folded to a strip, its title and its warnings, opened again with the panel as it was; Escape left to the map while folded; one panel at a time.

import { act, fireEvent, screen, within } from '@testing-library/react';
import { i18next } from '../../../shared/i18n/i18n.ts';
import { openSheetOf, showEditor } from '../../../test/editor.tsx';
import { fakeApi } from '../../../test/fakeApi.ts';
import { PROJECT_ID, sampleMap, streamOf } from '../../../test/maps.ts';
import { memberOf, OTHER_ID, sheetViewOf } from '../../../test/sheets.ts';
import { DOCK, DOCK_WIDTH_KEY } from '../state/dock.ts';

beforeEach(async () => {
  await i18next.changeLanguage('fr');
});

afterEach(() => {
  vi.unstubAllGlobals();
});

/** Two warnings and an information in the check, so that the strip counts the warnings only. */
const checked = () => sampleMap({
  value_stream: streamOf({
    issues: [
      { code: 'DUREE_MANQUANTE', severity: 'warning', map_id: '', step_id: 's2', lane_id: null },
      { code: 'DUREE_MANQUANTE', severity: 'warning', map_id: '', step_id: 's3', lane_id: null },
      { code: 'DELAI_PAR_DEFAUT', severity: 'information', map_id: '', step_id: 's2', lane_id: null },
    ],
  }),
});

async function withStream(map = checked()) {
  const shown = await showEditor(map);
  act(() => {
    shown.editor.preferences.getState().set('showValueStream', true);
  });
  return { ...shown, frame: screen.getByTestId('side-dock') };
}

describe('the width of a panel (spec 3.0 section 15.2)', () => {
  it('is chosen by a drag of the handle of its left edge, kept at the end of the drag, bounded', async () => {
    const { editor, frame } = await withStream();
    expect(frame).toHaveStyle({ width: `${String(DOCK.width)}px` });
    vi.spyOn(frame, 'getBoundingClientRect').mockReturnValue(new DOMRect(1024 - DOCK.width, 0, DOCK.width, 700));
    const handle = within(frame).getByTestId('side-dock-handle');
    fireEvent.pointerDown(handle, { button: 0, pointerId: 1, clientX: 1024 - DOCK.width });
    fireEvent.pointerMove(handle, { pointerId: 1, clientX: 1024 - 500 });
    expect(frame).toHaveStyle({ width: '500px' });
    fireEvent.pointerUp(handle, { pointerId: 1, clientX: 1024 - 520 });
    expect(editor.dock.getState().width).toBe(520);
    // 70 % of the window at most (1 024 px in the tests).
    fireEvent.pointerDown(handle, { button: 0, pointerId: 1, clientX: 500 });
    fireEvent.pointerUp(handle, { pointerId: 1, clientX: 0 });
    expect(handle).toHaveAttribute('aria-valuenow', '717');
  });

  it('moves by the arrow keys, goes to its bounds by Home and End, back to 440 px by a double click', async () => {
    const { frame } = await withStream();
    const handle = within(frame).getByRole('separator', { name: 'Largeur du volet' });
    handle.focus();
    fireEvent.keyDown(handle, { key: 'ArrowLeft' });
    expect(handle).toHaveAttribute('aria-valuenow', String(DOCK.width + DOCK.step));
    fireEvent.keyDown(handle, { key: 'ArrowRight' });
    fireEvent.keyDown(handle, { key: 'ArrowRight' });
    expect(handle).toHaveAttribute('aria-valuenow', String(DOCK.width - DOCK.step));
    fireEvent.keyDown(handle, { key: 'Home' });
    expect(handle).toHaveAttribute('aria-valuenow', String(DOCK.min));
    fireEvent.keyDown(handle, { key: 'End' });
    expect(handle).toHaveAttribute('aria-valuenow', '717');
    fireEvent.doubleClick(handle);
    expect(handle).toHaveAttribute('aria-valuenow', String(DOCK.width));
  });

  it('is kept in the browser and shared by the panels', async () => {
    const { editor, frame } = await withStream();
    const handle = within(frame).getByTestId('side-dock-handle');
    handle.focus();
    fireEvent.keyDown(handle, { key: 'ArrowLeft' });
    expect(editor.dock.getState().width).toBe(DOCK.width + DOCK.step);
    act(() => {
      editor.assistant.getState().setOpen(true);
    });
    expect(screen.getByTestId('side-dock')).toHaveAttribute('data-panel', 'assistant');
    expect(screen.getByTestId('side-dock')).toHaveStyle({ width: `${String(DOCK.width + DOCK.step)}px` });
    // The panel that replaces another keeps the room of the notifications.
    expect(document.documentElement.style.getPropertyValue('--dock-room')).toBe(`${String(DOCK.width + DOCK.step)}px`);
    expect(localStorage.getItem(DOCK_WIDTH_KEY)).toBeNull();
  });
});

describe('a panel folded (spec 3.0 section 15.2)', () => {
  it('becomes a strip with its title and its warnings, the room of the notifications following; a click opens it again', async () => {
    await withStream();
    expect(document.documentElement.style.getPropertyValue('--dock-room')).toBe(`${String(DOCK.width)}px`);
    fireEvent.click(screen.getByTestId('panel-fold'));
    const strip = screen.getByTestId('side-dock-strip');
    expect(strip).toHaveTextContent('Flux de valeur2');
    expect(strip).toHaveAccessibleName('Ouvrir le volet « Flux de valeur »');
    expect(strip).toHaveFocus();
    expect(screen.getByTestId('stream-panel')).not.toBeVisible();
    expect(screen.getByTestId('side-dock')).toHaveStyle({ width: `${String(DOCK.strip)}px` });
    expect(document.documentElement.style.getPropertyValue('--dock-room')).toBe(`${String(DOCK.strip)}px`);
    fireEvent.click(strip);
    expect(screen.getByTestId('stream-panel')).toBeVisible();
    expect(screen.getByTestId('panel-fold')).toHaveFocus();
    // The layer turned off, the frame leaves: the notifications take their corner again.
    fireEvent.click(screen.getByTestId('stream-panel-close'));
    expect(screen.queryByTestId('side-dock')).toBeNull();
    expect(document.documentElement.style.getPropertyValue('--dock-room')).toBe('');
  });

  it('a sheet folded is kept as it is, leaves Escape to the map, and comes back as it was left after the other panels', async () => {
    fakeApi({ 'GET /api/steps/s2/sheet': { json: sheetViewOf() }, [`GET /api/projects/${PROJECT_ID}`]: { json: { members: [memberOf(OTHER_ID, 'Alex Martin')], sharing_group_label: null } } });
    const { editor, canvas } = await withStream();
    fireEvent.click(screen.getByTestId('panel-fold'));
    openSheetOf(canvas, 's2');
    const sheet = await screen.findByTestId('sheet-panel');
    expect(screen.getByTestId('side-dock')).toHaveAttribute('data-panel', 'sheet');
    fireEvent.click(within(sheet).getByTestId('panel-fold'));
    fireEvent.keyDown(document.body, { key: 'Escape' });
    expect(editor.store.getState().openSheet).toBe('s2');
    fireEvent.click(screen.getByTestId('side-dock-strip'));
    expect(screen.getByTestId('sheet-panel')).toBe(sheet);
    fireEvent.click(within(sheet).getByTestId('sheet-close'));
    // The panel of the stream comes back folded, as it was left.
    expect(screen.getByTestId('side-dock')).toHaveAttribute('data-panel', 'stream');
    expect(screen.getByTestId('side-dock-strip')).toBeInTheDocument();
  });
});
