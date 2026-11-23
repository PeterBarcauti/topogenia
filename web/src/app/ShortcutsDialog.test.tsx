// ShortcutsDialog.test.tsx
// The panel of the keyboard shortcuts: ? on the map opens it, for a reader too, Escape closes it and gives the focus back; the keys in the language of the interface; the section Changing the map said the facilitator's; ? begins the name of the step chosen alone (lot 1 of the 3.4, ADR 109).

import { act, fireEvent, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { Schema } from '../api/client.ts';
import { i18next } from '../shared/i18n/i18n.ts';
import { openProject } from '../shared/state/openProject.ts';
import { shortcutsPanel } from '../shared/state/shortcutsPanel.ts';
import { fakeApi } from '../test/fakeApi.ts';
import { sampleMap } from '../test/maps.ts';
import { fakeSource, profileOf } from '../test/profiles.ts';
import { App } from './App.tsx';

beforeEach(async () => {
  await i18next.changeLanguage('fr');
});

afterEach(() => {
  act(() => {
    shortcutsPanel.setState({ open: false });
    openProject.setState({ project: null });
  });
  vi.unstubAllGlobals();
  window.history.replaceState(null, '', '/');
});

/** The application on a map read from the fake API; gives its canvas. */
async function startOnMap(map: Schema<'MapView'> = sampleMap()): Promise<HTMLElement> {
  window.history.replaceState(null, '', `/maps/${map.id}`);
  fakeApi({ [`GET /api/maps/${map.id}`]: { json: { map, history: { undo: [], redo: [] } } } });
  render(<App source={fakeSource([profileOf()]).source} />);
  return screen.findByTestId('map-canvas');
}

const node = (canvas: HTMLElement, id: string) => canvas.querySelector(`.react-flow__node[data-id="${id}"]`) as HTMLElement;

/** The keys shown on the line of a command or of another key. */
const keysOn = (id: string) => [...screen.getByTestId(`shortcut-${id}`).querySelectorAll('kbd')].map((key) => key.textContent);

describe('the panel of the keyboard shortcuts', () => {
  it('? on the map opens the sheet, Escape closes it and gives the focus back', async () => {
    const canvas = await startOnMap(sampleMap({ rights: { structure: false, role: 'reader' } }));
    act(() => {
      node(canvas, 's2').focus();
    });
    await userEvent.keyboard('?');
    const dialog = screen.getByRole('dialog', { name: 'Raccourcis clavier' });
    expect(within(dialog).getAllByRole('heading', { level: 3 }).map((heading) => heading.textContent))
      .toEqual(['Se déplacer', 'Choisir', 'Modifier la carte', 'Afficher', 'Présenter', 'Aide']);
    expect([keysOn('fit'), keysOn('help.keys')]).toEqual([['Maj+1'], ['?']]);
    await userEvent.keyboard('{Escape}');
    expect(screen.queryByRole('dialog')).toBeNull();
    expect(document.activeElement).toBe(node(canvas, 's2'));
  });

  it('the keys are written in the language of the interface (Maj, Suppr, Échap ; Shift, Del, Esc)', async () => {
    await startOnMap();
    act(() => {
      shortcutsPanel.getState().setOpen(true);
    });
    const shown = () => [keysOn('fit'), keysOn('delete'), keysOn('escape'), keysOn('move.Left'), keysOn('level.up'), keysOn('wheel')];
    expect(shown()).toEqual([['Maj+1'], ['Suppr', 'Retour arrière'], ['Échap'], ['Gauche', 'Droite', 'Haut', 'Bas'], ['Alt+Haut'], ['Molette', 'Maj+Molette']]);
    expect(screen.getByTestId('shortcut-delete')).toHaveTextContent('SupprimerSuppr ou Retour arrière');
    await act(async () => {
      await i18next.changeLanguage('en');
    });
    expect(shown()).toEqual([['Shift+1'], ['Del', 'Backspace'], ['Esc'], ['Left', 'Right', 'Up', 'Down'], ['Alt+Up'], ['Wheel', 'Shift+Wheel']]);
    expect(screen.getByRole('dialog', { name: 'Keyboard shortcuts' })).toBeInTheDocument();
  });

  it('the section Changing the map says it is the facilitator\'s', async () => {
    await startOnMap(sampleMap({ rights: { structure: false, role: 'reader' } }));
    act(() => {
      shortcutsPanel.getState().setOpen(true);
    });
    const edit = screen.getByRole('region', { name: 'Modifier la carte' });
    expect(within(edit).getByText('Réservé à l’animateur du projet.')).toBeInTheDocument();
    expect(screen.getAllByText('Réservé à l’animateur du projet.')).toHaveLength(1);
    expect(within(edit).getByTestId('shortcut-rename')).toHaveTextContent('F2');
  });

  it('with one step alone chosen, ? begins its name', async () => {
    const canvas = await startOnMap();
    fireEvent.click(node(canvas, 's2'));
    act(() => {
      node(canvas, 's2').focus();
    });
    await userEvent.keyboard('?');
    expect(screen.queryByRole('dialog')).toBeNull();
    expect(screen.getByTestId<HTMLTextAreaElement>('step-label-input').value).toBe('?');
  });
});

describe('the panel of the keyboard shortcuts: the key as sent, the conditions, the letter', () => {
  it('? with Shift, as the keyboard sends it, opens the sheet', async () => {
    const canvas = await startOnMap();
    act(() => {
      node(canvas, 's2').focus();
    });
    await userEvent.keyboard('{Shift>}?{/Shift}');
    expect(screen.getByRole('dialog', { name: 'Raccourcis clavier' })).toBeInTheDocument();
  });

  it('the lines of Show all, ?, V and H say they need no step alone chosen; the others do not', async () => {
    await startOnMap();
    act(() => {
      shortcutsPanel.getState().setOpen(true);
    });
    const condition = 'Sans étape seule choisie (sinon, la touche commence son nom)';
    for (const id of ['fit', 'help.keys', 'tool.select', 'tool.hand']) expect(within(screen.getByTestId(`shortcut-${id}`)).getByText(condition)).toBeInTheDocument();
    expect(screen.getAllByText(condition)).toHaveLength(4);
    await act(async () => {
      await i18next.changeLanguage('en');
    });
    expect(within(screen.getByTestId('shortcut-fit')).getByText('Without a single step chosen (otherwise the key starts its name)')).toBeInTheDocument();
  });

  it('the line of a character typed shows « une lettre », not the key A', async () => {
    await startOnMap();
    act(() => {
      shortcutsPanel.getState().setOpen(true);
    });
    expect(keysOn('type')).toEqual(['une lettre']);
    await act(async () => {
      await i18next.changeLanguage('en');
    });
    expect(keysOn('type')).toEqual(['a letter']);
  });
});
