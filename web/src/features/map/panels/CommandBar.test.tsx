// CommandBar.test.tsx
// The command bar: opened by Ctrl+K, its field of one to six lines at fixed pitch, Enter sends and Shift+Enter goes to the line, the refusal line by line, « N flèches ajoutées » (annex B 2.13, B-2.5-06, B-2.5-33, B-2.5-34).

import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { act, fireEvent, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ApiError } from '../../../api/errors.ts';
import { i18next } from '../../../shared/i18n/i18n.ts';
import { memoryStorage, showEditor, sourceOf } from '../../../test/editor.tsx';
import { callsTo, fakeApi } from '../../../test/fakeApi.ts';
import { MAP_ID, replyOf, sampleMap } from '../../../test/maps.ts';
import { lineMessages, runCommandBar } from '../state/actions/commandBar.ts';
import { createMapEditor } from '../state/editor.ts';
import { rowsOf } from './CommandBar.tsx';

beforeEach(async () => {
  await i18next.changeLanguage('fr');
});

afterEach(() => {
  vi.unstubAllGlobals();
});

const COMMAND = `/api/maps/${MAP_ID}/command`;

/** The map shown, the command bar opened by Ctrl+K. */
async function openBar() {
  const shown = await showEditor(sampleMap());
  await userEvent.keyboard('{Control>}k{/Control}');
  return { ...shown, bar: screen.getByRole('dialog', { name: 'Relier par les codes' }), field: screen.getByTestId('command-bar-input') };
}

describe('the bar (B-2.13-01, B-2.13-02, B-2.5-06)', () => {
  it('opens on Ctrl+K, its field focused, with the help of the syntax in the codes of the project', async () => {
    const { bar, field } = await openBar();
    expect(bar).toHaveClass('command-bar');
    expect(document.activeElement).toBe(field);
    expect(field).toHaveAttribute('placeholder', 'VTE-E012 > VTE-E013');
    expect(within(bar).getByText('Une ligne par lien : VTE-E012 > VTE-E013, ou E12 > E13 : oui')).toBeInTheDocument();
  });

  it('is fixed at 18 % of the height, 560 px wide; its field is at fixed pitch', () => {
    const css = readFileSync(join(import.meta.dirname, '../../../styles/map.css'), 'utf8');
    const rule = (selector: string) => new RegExp(`(^|\\n)${selector.replace('.', '\\.')} \\{([^}]*)\\}`).exec(css)?.[2] ?? '';
    expect(rule('.command-bar')).toMatch(/position: fixed;.*top: var\(--command-bar-top\);.*width: var\(--command-bar-width\);/s);
    expect(rule('.command-bar textarea')).toMatch(/font-family: var\(--font-mono\);/);
  });

  it('its field grows with its lines, up to six', async () => {
    expect([rowsOf(''), rowsOf('a\nb\nc'), rowsOf('1\n2\n3\n4\n5\n6\n7\n8')]).toEqual([1, 3, 6]);
    const { field } = await openBar();
    await userEvent.type(field, 'E1 > E2{Shift>}{Enter}{/Shift}E2 > E3');
    expect(field).toHaveAttribute('rows', '2');
  });

  it('closes on Escape, on Annuler and on a click outside; the keys of the map are silent while it is open', async () => {
    const sent = fakeApi({});
    const { editor } = await openBar();
    act(() => {
      editor.store.setState({ selection: { steps: ['s2'], flows: [] } });
    });
    expect(fireEvent.keyDown(document.body, { key: 'Delete' })).toBe(true);
    await new Promise((resolve) => setTimeout(resolve, 20));
    expect(sent).toEqual([]);
    await userEvent.keyboard('{Escape}');
    expect(screen.queryByTestId('command-bar')).toBeNull();
    await userEvent.keyboard('{Control>}k{/Control}');
    await userEvent.click(screen.getByTestId('command-bar-cancel'));
    expect(screen.queryByTestId('command-bar')).toBeNull();
    await userEvent.keyboard('{Control>}k{/Control}');
    fireEvent.pointerDown(document.body);
    expect(screen.queryByTestId('command-bar')).toBeNull();
  });
});

describe('sending (B-2.5-33, B-2.5-34, B-2.13-05, B-2.13-06)', () => {
  it('Shift+Enter goes to the line, Enter sends the text; success closes the bar and says « 2 flèches ajoutées »', async () => {
    const after = { ...sampleMap(), name: 'Avec les flèches' };
    const sent = fakeApi({ [`POST ${COMMAND}`]: { json: replyOf(after, { ids: ['f9', 'f10'] }) } });
    const { field, editor, notes } = await openBar();
    expect(screen.getByTestId('command-bar-run')).toBeDisabled();
    await userEvent.type(field, 'E1 > E2{Shift>}{Enter}{/Shift}E2 > E3 : oui');
    expect(callsTo(sent, 'POST', COMMAND)).toHaveLength(0);
    await userEvent.keyboard('{Enter}');
    await vi.waitFor(() => {
      expect(screen.queryByTestId('command-bar')).toBeNull();
    });
    expect(callsTo(sent, 'POST', COMMAND).map((call) => call.body)).toEqual([{ text: 'E1 > E2\nE2 > E3 : oui' }]);
    expect(notes).toEqual([['2 flèches ajoutées', 'success']]);
    expect(editor.store.getState().map?.name).toBe('Avec les flèches');
  });

  it('a refusal shows its message and its lines in the bar, which stays open with the text, without a notification', async () => {
    const details = { errors: [{ line: 1, code: 'ETAPE_INCONNUE', detail: 'VTE-E099', message: 'L1 : Étape inconnue : VTE-E099.' }, { line: 3, code: 'LIEN_DOUBLON', detail: 'VTE-E001 > VTE-E002', message: 'L3 : Ce lien existe déjà.' }] };
    fakeApi({ [`POST ${COMMAND}`]: { refused: 422, code: 'COMMANDE_ERREURS', message: 'La commande contient des erreurs.', details } });
    const { field, notes } = await openBar();
    await userEvent.type(field, 'E99 > E2');
    await userEvent.click(screen.getByTestId('command-bar-run'));
    const error = await screen.findByTestId('command-bar-error');
    expect(error).toHaveAttribute('role', 'alert');
    expect([within(error).getByText('La commande contient des erreurs.'), ...within(error).getAllByRole('listitem')].map((item) => item.textContent)).toEqual([
      'La commande contient des erreurs.', 'L1 : Étape inconnue : VTE-E099.', 'L3 : Ce lien existe déjà.',
    ]);
    expect(screen.getByTestId('command-bar-input')).toHaveValue('E99 > E2');
    expect(notes).toEqual([]);
  });

  it('Enter on an empty text sends nothing', async () => {
    const sent = fakeApi({});
    const { field } = await openBar();
    await userEvent.type(field, '   {Enter}');
    expect(sent).toEqual([]);
    expect(screen.getByTestId('command-bar')).toBeInTheDocument();
  });

  it('a conflict is shown in the bar, and the map is read again', async () => {
    fakeApi({ [`POST ${COMMAND}`]: { refused: 409, code: 'CONFLIT_INTEGRITE', message: 'La carte a changé entre-temps.' } });
    const notes: [string, string | undefined][] = [];
    const editor = createMapEditor({ source: sourceOf(sampleMap(), { ...sampleMap(), name: 'Relue' }), storage: memoryStorage(), notify: (text, kind) => notes.push([text, kind]) });
    await expect(editor.store.getState().load(MAP_ID).then(() => runCommandBar(editor, 'E1 > E2'))).resolves.toEqual({ ok: false, message: 'La carte a changé entre-temps.', lines: [] });
    expect([editor.store.getState().map?.name, notes]).toEqual(['Relue', []]);
  });

  it('closed while the text is sent, the bar leaves alone the element opened since; the map of the reply is applied', async () => {
    let answer: (response: Response) => void = () => undefined;
    vi.stubGlobal('fetch', vi.fn(() => new Promise<Response>((resolve) => { answer = resolve; })));
    const { field, editor, notes } = await openBar();
    await userEvent.type(field, 'E1 > E2{Enter}');
    await userEvent.keyboard('{Escape}');
    const menu = { kind: 'menu', target: { kind: 'pane', point: { x: 10, y: 10 } }, at: { x: 10, y: 10 } } as const;
    act(() => {
      editor.floating.getState().open(menu);
    });
    await act(async () => {
      answer(new Response(JSON.stringify(replyOf({ ...sampleMap(), name: 'Avec la flèche' }, { ids: ['f9'] })), { status: 200 }));
      await new Promise((resolve) => setTimeout(resolve, 20));
    });
    expect([editor.floating.getState().floating, editor.store.getState().map?.name, notes]).toEqual([menu, 'Avec la flèche', []]);
  });

  it('a failure without lines shows its message alone; the lines are read only from a list of errors', () => {
    expect(lineMessages(new Error('x'))).toEqual([]);
    expect(lineMessages({ details: { errors: [{ message: 'L1 : x' }] } })).toEqual([]);
    expect(lineMessages(new ApiError(400, 'COMMANDE_ERREURS', 'Erreurs.', { errors: [{ line: 1, message: 'L1 : x' }, { line: 2 }, 'L3', null] }))).toEqual(['L1 : x']);
  });

  it('a reader has no command bar', async () => {
    await showEditor(sampleMap({ rights: { structure: false, role: 'reader' } }));
    await userEvent.keyboard('{Control>}k{/Control}');
    expect(screen.queryByTestId('command-bar')).toBeNull();
  });
});
