// raci.test.tsx
// The RACI panel of a step and the matrix of the map: the lane R first, a button a letter, the call, the closing; the Matrix button with the layer, the table, the legend, the copy (annex B 2.11, B-2.1-32).

import { act, fireEvent, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { Schema } from '../../../api/client.ts';
import { i18next } from '../../../shared/i18n/i18n.ts';
import { showEditor } from '../../../test/editor.tsx';
import { callsTo, fakeApi } from '../../../test/fakeApi.ts';
import { laneOf, replyOf, sampleMap } from '../../../test/maps.ts';

beforeEach(async () => {
  await i18next.changeLanguage('fr');
});

afterEach(() => {
  vi.unstubAllGlobals();
});

/** Three lanes; the decision s3 of l1 is approved by l2. */
const map = (overrides: Partial<Schema<'MapView'>> = {}) => {
  const sample = sampleMap();
  return sampleMap({ lanes: [...sample.lanes, laneOf('l3', 2, { name: 'Direction' })], raci: [{ step_id: 's3', lane_id: 'l2', role: 'A' }], ...overrides });
};

/** The map shown with the RACI layer on, the panel of a step opened by a click on it. */
async function openPanel(stepId: string, overrides: Partial<Schema<'MapView'>> = {}) {
  const shown = await showEditor(map(overrides));
  act(() => {
    shown.editor.preferences.getState().set('showRaci', true);
  });
  fireEvent.click(shown.canvas.querySelector(`.react-flow__node[data-id="${stepId}"]`) as HTMLElement, { clientX: 200, clientY: 300 });
  return { ...shown, panel: screen.getByTestId('raci-panel') };
}

describe('the RACI panel (B-2.11-10, B-2.11-11, B-2.3-12)', () => {
  it('next to the click, titled by the step; its lane R first on its token, then the other lanes with a button a letter of the map', async () => {
    const { panel } = await openPanel('s3');
    expect([panel.style.left, panel.style.top, panel.getAttribute('aria-label')]).toEqual(['212px', '280px', 'Rôles RACI de Conforme ?']);
    const rows = within(panel).getAllByRole('row').map((row) => [within(row).getByRole('rowheader').textContent, within(row).queryAllByRole('button').map((button) => `${button.textContent}${button.getAttribute('aria-pressed') === 'true' ? ' *' : ''}`)]);
    expect(rows).toEqual([['Commercial', []], ['Comptabilité', ['A *', 'C', 'I']], ['Direction', ['A', 'C', 'I']]]);
    expect(within(panel).getByTitle('Réalise')).toHaveClass('raci-letter--R');
    expect(within(panel).getByRole('button', { name: 'Consulté : Direction' })).toHaveAttribute('title', 'Consulté');
  });

  it('offers the S when the map is set to ASCI (B-2.11-02)', async () => {
    const { panel } = await openPanel('s2', { raci_letters: 'ASCI' });
    const [, secondRow] = within(panel).getAllByRole('row');
    expect(within(secondRow as HTMLElement).getAllByRole('button').map((button) => button.textContent)).toEqual(['A', 'S', 'C', 'I']);
  });

  it('a letter not given is set, the letter given is taken back, with the version of the step; nothing before the reply', async () => {
    const sent = fakeApi({ 'PUT /api/steps/s3/raci': { json: replyOf(map()) } });
    const { panel } = await openPanel('s3');
    await userEvent.click(within(panel).getByRole('button', { name: 'Approuve : Direction' }));
    await userEvent.click(within(screen.getByTestId('raci-panel')).getByRole('button', { name: 'Approuve : Comptabilité' }));
    expect(callsTo(sent, 'PUT', '/api/steps/s3/raci').map((call) => [call.body, call.headers.get('If-Match')])).toEqual([[{ lane_id: 'l3', role: 'A' }, '"1"'], [{ lane_id: 'l2', role: null }, '"1"']]);
  });

  it('closes on Escape and on a press outside it, not on the release of a key', async () => {
    const { editor } = await openPanel('s3');
    fireEvent.keyUp(document.body, { key: 'Escape' });
    expect(editor.floating.getState().floating).not.toBeNull();
    await userEvent.keyboard('{Escape}');
    expect(editor.floating.getState().floating).toBeNull();
    act(() => {
      editor.floating.getState().open({ kind: 'raci', stepId: 's3', at: { x: 10, y: 10 } });
    });
    fireEvent.pointerDown(document.body);
    expect(editor.floating.getState().floating).toBeNull();
  });
});

describe('the Matrix button and the matrix (B-2.1-32, B-2.11-14, B-2.11-18, B-6-30)', () => {
  it('the button stands in the zone Affichage only with the layer on, and opens the matrix', async () => {
    const { editor } = await showEditor(map());
    expect(screen.queryByTestId('ribbon-matrix')).toBeNull();
    act(() => {
      editor.preferences.getState().set('showRaci', true);
    });
    await userEvent.click(screen.getByTestId('ribbon-matrix'));
    expect(screen.getByTestId('raci-matrix')).toHaveClass('dialog--wide');
  });

  it('the steps concerned in rows, the lanes in columns with their letters, the legend of the letters of the map', async () => {
    const { editor } = await showEditor(map());
    act(() => {
      editor.floating.getState().open({ kind: 'matrix' });
    });
    const dialog = screen.getByTestId('raci-matrix');
    expect(within(dialog).getByRole('heading', { level: 2 })).toHaveTextContent('Matrice RACI – Ventes');
    expect(within(dialog).getAllByRole('columnheader').map((cell) => cell.textContent)).toEqual(['Code', 'Étape', 'Commercial', 'Comptabilité', 'Direction']);
    expect(within(dialog).getAllByRole('row').slice(1).map((row) => within(row).getAllByRole('cell').map((cell) => cell.textContent))).toEqual([
      ['VTE-E002', 'Vérifier la demande', 'R', '', ''], ['VTE-E003', 'Conforme ?', 'R', 'A', ''],
    ]);
    expect(within(dialog).getByText('A', { selector: 'td' })).toHaveClass('raci-cell', 'raci-cell--A');
    expect(within(dialog).getByTestId('raci-matrix-legend')).toHaveTextContent('R : réalise · A : approuve · C : consulté · I : informé');
  });

  it('copies the table for Excel or PowerPoint and says so (B-2.11-15)', async () => {
    const written: unknown[] = [];
    vi.stubGlobal('ClipboardItem', class { constructor(readonly parts: Record<string, Blob>) {} });
    Object.defineProperty(navigator, 'clipboard', { configurable: true, value: { write: (items: unknown[]) => Promise.resolve(void written.push(...items)), writeText: () => Promise.reject(new Error('unused')) } });
    const { editor, notes } = await showEditor(map());
    act(() => {
      editor.floating.getState().open({ kind: 'matrix' });
    });
    await userEvent.click(screen.getByTestId('raci-matrix-copy'));
    await vi.waitFor(() => {
      expect(notes).toEqual([['Matrice copiée : la coller dans Excel, Word ou PowerPoint.', 'success']]);
    });
    expect(written).toHaveLength(1);
  });

  it('without a step concerned, says so and offers no copy', async () => {
    const { editor } = await showEditor(map({ steps: sampleMap().steps.filter((step) => step.type === 'start' || step.type === 'end') }));
    act(() => {
      editor.floating.getState().open({ kind: 'matrix' });
    });
    expect([screen.getByText('Aucune étape à afficher.').tagName, screen.getByTestId('raci-matrix-copy')]).toEqual(['P', expect.objectContaining({ disabled: true })]);
  });
});
