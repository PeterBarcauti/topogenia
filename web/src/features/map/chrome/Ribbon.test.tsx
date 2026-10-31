// Ribbon.test.tsx
// The ribbon drawn: named zones, radio choices, switches, icon buttons named with their shortcut, folded and unfolded, the zones of a reader (annex B 2.1, spec 9.5, AR-90).

import { act, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { i18next } from '../../../shared/i18n/i18n.ts';
import { memoryStorage, showEditor, showMap } from '../../../test/editor.tsx';
import { callsTo, fakeApi } from '../../../test/fakeApi.ts';
import { replyOf, sampleMap } from '../../../test/maps.ts';

beforeEach(async () => {
  await i18next.changeLanguage('fr');
});

afterEach(() => {
  vi.unstubAllGlobals();
});

const zones = () => within(screen.getByTestId('ribbon')).getAllByRole('group').filter((group) => group.classList.contains('toolbar-group'));

describe('the ribbon (B-2.1-07 to B-2.1-17, B-2.1-34)', () => {
  it('is a toolbar of named zones, each titled under it: Niveau, Formes, Branchements, Liens, Édition, Affichage, Outils (AR-155)', async () => {
    await showMap(sampleMap());
    const ribbon = screen.getByTestId('ribbon');
    expect(ribbon).toHaveAttribute('role', 'toolbar');
    expect(zones().map((zone) => [zone.getAttribute('aria-label'), zone.querySelector('.toolbar-caption')?.textContent])).toEqual([
      ['Niveau', 'Niveau'], ['Formes', 'Formes'], ['Branchements', 'Branchements'], ['Liens', 'Liens'], ['Édition', 'Édition'], ['Affichage', 'Affichage'],
      ['Outils', 'Outils'],
    ]);
    expect(screen.getByTestId('ribbon-level.up')).toHaveAttribute('title', 'Remonter d’un niveau (Alt+Haut)');
  });

  it('the tools, the shapes and the diamonds are radio buttons, Hand and Étape on at the opening (B-2.1-11)', async () => {
    await showMap(sampleMap());
    const groups = screen.getAllByRole('radiogroup').map((group) => [group.getAttribute('aria-label'), within(group).getAllByRole('radio').map((radio) => `${radio.getAttribute('aria-label') ?? ''}${radio.getAttribute('aria-checked') === 'true' ? ' *' : ''}`)]);
    expect(groups).toEqual([
      ['Type de la prochaine étape', ['Étape *', 'Attente', 'Début', 'Fin']],
      ['Losange de la prochaine étape', ['Décision', 'Parallèle', 'Inclusif']],
      ['Outil actif', ['Sélection', 'Main *']],
    ]);
  });

  it('a shape chosen becomes the type of the next steps, and only one is on (B-2.1-15, B-2.1-17)', async () => {
    const { editor } = await showEditor(sampleMap());
    await userEvent.click(screen.getByTestId('ribbon-shape.decision'));
    expect(editor.store.getState().newStepType).toBe('decision');
    expect(screen.getAllByRole('radio', { checked: true }).map((radio) => radio.getAttribute('aria-label'))).toEqual(['Décision', 'Main']);
  });

  it('icon buttons only, named for assistive technologies, the tooltip giving the name and the shortcut (B-2.1-12, B-2.1-34)', async () => {
    await showMap(sampleMap());
    const select = screen.getByTestId('ribbon-tool.select');
    expect([select.textContent, select.getAttribute('aria-label'), select.getAttribute('title'), select.querySelector('svg.i') !== null]).toEqual(['', 'Sélection', 'Sélection (V)', true]);
    expect(screen.getByTestId('ribbon-cut')).toHaveAttribute('title', 'Couper (Ctrl+X)');
    expect(screen.getByTestId('ribbon-shape.wait').querySelector('rect')?.getAttribute('stroke-dasharray')).toBe('3 2.4');
    const buttons = within(screen.getByTestId('ribbon')).getAllByRole('button').concat(within(screen.getByTestId('ribbon')).getAllByRole('radio'));
    expect(buttons.filter((button) => button.textContent !== '' || button.querySelector('svg') === null)).toEqual([]);
  });

  it('Branche adds a step in parallel under the one step chosen, of the type of the next steps (B-2.1-18, B-2.1-20)', async () => {
    const sent = fakeApi({ 'POST /api/steps/s2/insert': { json: replyOf(sampleMap(), { id: 'n' }) } });
    const { editor } = await showEditor(sampleMap());
    act(() => {
      editor.store.setState({ selection: { steps: ['s2'], flows: [] } });
    });
    await userEvent.click(screen.getByTestId('ribbon-branch'));
    await vi.waitFor(() => {
      expect(callsTo(sent, 'POST', '/api/steps/s2/insert')[0]?.body).toEqual({ position: 'parallel', label: 'Nouvelle étape', type: 'task' });
    });
  });

  it('Relier is a switch pressed in the link mode, which a banner explains (B-2.1-21, B-2.1-22, B-2.1-37)', async () => {
    const { editor } = await showEditor(sampleMap());
    const link = screen.getByTestId('ribbon-link');
    expect(link).toHaveAttribute('aria-pressed', 'false');
    await userEvent.click(link);
    expect([link.getAttribute('aria-pressed'), editor.store.getState().mode]).toEqual(['true', 'link']);
    expect(screen.getByTestId('map-link')).toHaveTextContent('Cliquer sur l’étape de départ, puis sur l’étape d’arrivée. Échap pour arrêter.');
  });

  it('Codes, Pictogrammes and RACI are switches kept as preferences, never sent to the host (B-2.1-31, B-2.6-47)', async () => {
    const sent = fakeApi({});
    const storage = memoryStorage();
    await showMap(sampleMap(), storage);
    expect(['codes', 'pictos', 'raci'].map((id) => screen.getByTestId(`ribbon-${id}`).getAttribute('aria-pressed'))).toEqual(['true', 'true', 'false']);
    await userEvent.click(screen.getByTestId('ribbon-codes'));
    expect([screen.getByTestId('ribbon-codes').getAttribute('aria-pressed'), storage.written.get('app.showCodes')]).toEqual(['false', '0']);
    expect(screen.queryAllByTestId('step-code')).toHaveLength(0);
    expect(sent).toEqual([]);
  });

  it('a reader sees Niveau, Affichage and Outils only (B-2.1-09, B-2.1-14, B-2.1-31, AR-155)', async () => {
    await showMap(sampleMap({ rights: { structure: false, role: 'reader' } }));
    expect(zones().map((zone) => zone.getAttribute('aria-label'))).toEqual(['Niveau', 'Affichage', 'Outils']);
  });
});

describe('the zones Liens and Édition (B-2.1-23, B-2.1-24)', () => {
  it('Liens holds Relier and Commande, which opens the command bar (B-2.1-23)', async () => {
    const { editor } = await showEditor(sampleMap());
    const command = screen.getByTestId('ribbon-command');
    expect([command.getAttribute('aria-label'), command.getAttribute('title')]).toEqual(['Barre de commande', 'Barre de commande (Ctrl+K)']);
    expect(within(zones()[3] as HTMLElement).getAllByRole('button').map((button) => button.dataset.testid)).toEqual(['ribbon-link', 'ribbon-command']);
    await userEvent.click(command);
    expect(editor.floating.getState().floating).toEqual({ kind: 'command' });
    expect(screen.getByRole('dialog', { name: 'Relier par les codes' })).toBeInTheDocument();
  });

  it('Édition starts with Annuler and Rétablir, enabled by the history of the host (B-2.1-24)', async () => {
    const reply = replyOf(sampleMap(), { code: 'deplacer', history: { undo: [], redo: [{ operation_id: 'o1', kind: 'deplacer', at: '2026-10-28T08:00:00Z' }] } });
    const sent = fakeApi({ 'POST /api/maps/0190a1b2-0000-7000-8000-00000000a001/undo': { json: reply } });
    const { editor, notes } = await showEditor(sampleMap());
    const edit = within(zones()[4] as HTMLElement).getAllByRole('button');
    expect(edit.map((button) => [button.dataset.testid, button.hasAttribute('disabled'), button.previousElementSibling?.classList.contains('toolbar-sep') === true]).slice(0, 4)).toEqual([
      ['ribbon-undo', true, false], ['ribbon-redo', true, false], ['ribbon-cut', true, true], ['ribbon-copy', true, false],
    ]);
    expect([screen.getByTestId('ribbon-undo').getAttribute('title'), screen.getByTestId('ribbon-redo').getAttribute('title')]).toEqual(['Annuler (Ctrl+Z)', 'Rétablir (Ctrl+Y)']);
    act(() => {
      editor.store.setState({ history: { undo: [{ operation_id: 'o1', kind: 'deplacer', at: '2026-10-28T08:00:00Z' }], redo: [] } });
    });
    expect([screen.getByTestId('ribbon-undo').hasAttribute('disabled'), screen.getByTestId('ribbon-redo').hasAttribute('disabled')]).toEqual([false, true]);
    await userEvent.click(screen.getByTestId('ribbon-undo'));
    await vi.waitFor(() => {
      expect(notes).toEqual([['Annulé : déplacement', undefined]]);
    });
    expect(callsTo(sent, 'POST', '/api/maps/0190a1b2-0000-7000-8000-00000000a001/undo')).toHaveLength(1);
    expect([screen.getByTestId('ribbon-undo').hasAttribute('disabled'), screen.getByTestId('ribbon-redo').hasAttribute('disabled')]).toEqual([true, false]);
  });
});

describe('the ribbon folded (B-2.1-33, AR-90)', () => {
  it('unfolded by default; its button and Ctrl+F1 fold it to a thin line with the button, kept as a preference', async () => {
    const storage = memoryStorage();
    await showMap(sampleMap(), storage);
    expect(screen.getByTestId('ribbon-fold')).toHaveAttribute('title', 'Réduire le ruban (Ctrl+F1)');
    await userEvent.click(screen.getByTestId('ribbon-fold'));
    expect(screen.queryByTestId('ribbon')).toBeNull();
    expect(within(screen.getByTestId('ribbon-folded')).getByTestId('ribbon-fold')).toHaveAttribute('aria-expanded', 'false');
    expect(storage.written.get('app.ribbonCollapsed')).toBe('1');
    await userEvent.keyboard('{Control>}{F1}{/Control}');
    expect(screen.getByTestId('ribbon')).toBeInTheDocument();
  });

  it('folded at the opening when the preference says so; the shortcuts still work', async () => {
    const { editor } = await showEditor(sampleMap());
    editor.preferences.getState().set('ribbonCollapsed', true);
    await userEvent.keyboard('v');
    expect(editor.store.getState().tool).toBe('select');
  });
});
