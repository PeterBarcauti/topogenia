// wallPrint.test.tsx
// « Imprimer en grand… » of the File menu (Makigami M9): its dialog on A4 landscape and the fewest pages across that print the map at its size, the count, the size and the scale said again at each choice, the marks of the analysis ticked when the layer is on, the poster asked with the choice made, a poster of too many pages refused before anything is made.

import { act, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { Schema } from '../../../api/client.ts';
import { i18next } from '../../../shared/i18n/i18n.ts';
import { memoryStorage, renderMapScreen, sourceOf } from '../../../test/editor.tsx';
import { fakeApi } from '../../../test/fakeApi.ts';
import { laneOf, MAP_ID, PROJECT_ID, sampleMap, stepOf } from '../../../test/maps.ts';
import { defaultAcross, wallPlan, type Paper } from '../export/wallPrint.ts';
import { exportMap, wallSize } from '../state/actions/exports.ts';
import { createMapEditor, type MapEditor } from '../state/editor.ts';

vi.mock('../state/actions/exports.ts', async (importOriginal) => ({ ...await importOriginal<typeof import('../state/actions/exports.ts')>(), exportMap: vi.fn(() => Promise.resolve()) }));

beforeEach(async () => {
  await i18next.changeLanguage('fr');
  vi.mocked(exportMap).mockClear();
  fakeApi({ [`GET /api/projects/${PROJECT_ID}`]: { json: { id: PROJECT_ID, code: 'VIS', classification: 'internal', version: 4 } } });
});

/** The map shown by an editor, the dialog opened from its File menu; gives the editor and the dialog. */
async function wallDialog(map: Schema<'MapView'> = sampleMap(), analysis = false): Promise<{ editor: MapEditor; dialog: HTMLElement }> {
  const editor = createMapEditor({ source: sourceOf(map), storage: memoryStorage(), admin: true });
  if (analysis) editor.preferences.getState().set('showAnalysis', true);
  renderMapScreen({ mapId: MAP_ID, editor });
  await screen.findByTestId('map-canvas');
  act(() => {
    editor.floating.getState().open({ kind: 'menu', target: { kind: 'file' }, at: { x: 10, y: 40 } });
  });
  await userEvent.click(screen.getByTestId('menu-file-file.wall'));
  return { editor, dialog: screen.getByRole('dialog', { name: 'Imprimer en grand' }) };
}

/** The line that says the count, the size and the scale of the poster, as the dialog must write it. */
function summaryOf(editor: MapEditor, map: Schema<'MapView'>, paper: Paper, across: number): string {
  const plan = wallPlan(wallSize(editor, map), paper, across);
  const centimetres = (millimetres: number) => new Intl.NumberFormat('fr', { maximumFractionDigits: 0 }).format(millimetres / 10);
  return i18next.t('export.wall_summary', { count: plan.across * plan.down, across: plan.across, down: plan.down, width: centimetres(plan.wall.width), height: centimetres(plan.wall.height), percent: plan.percent });
}

describe('« Imprimer en grand… » (Makigami M9)', () => {
  it('opens on A4 landscape and the fewest pages across that print the map at its size, and says the poster again at each choice', async () => {
    const map = sampleMap();
    const { editor, dialog } = await wallDialog(map);
    expect(within(dialog).getByRole('checkbox', { name: 'Avec les marques du calque Analyse' })).not.toBeChecked();
    expect(within(dialog).getByRole('radio', { name: 'A4 paysage' })).toBeChecked();
    const across = within(dialog).getByRole('combobox', { name: 'Pages en largeur' });
    const offered = defaultAcross(wallSize(editor, map), 'a4');
    expect(across).toHaveValue(String(offered));
    expect(within(across).getAllByRole('option').map((option) => option.textContent)).toEqual(['1', '2', '3', '4', '5', '6', '7', '8', '9', '10']);
    const summary = within(dialog).getByTestId('wall-print-summary');
    expect(summary).toHaveTextContent(summaryOf(editor, map, 'a4', offered));
    expect(summary.textContent).toMatch(/^Pages : \d+ \(\d+ × \d+\) · affiche de \d+ × \d+ cm · échelle \d+ %$/);
    await userEvent.click(within(dialog).getByRole('radio', { name: 'A3 paysage' }));
    expect(summary).toHaveTextContent(summaryOf(editor, map, 'a3', offered));
    await userEvent.selectOptions(across, '3');
    expect(summary).toHaveTextContent(summaryOf(editor, map, 'a3', 3));
    expect(within(dialog).getByText(/Deux pages voisines partagent 10 mm/)).toBeInTheDocument();
  });

  it('ticks the marks of the analysis when the layer is on, and asks the poster with the choice made', async () => {
    const { editor, dialog } = await wallDialog(sampleMap(), true);
    const marks = within(dialog).getByRole('checkbox', { name: 'Avec les marques du calque Analyse' });
    expect(marks).toBeChecked();
    await userEvent.click(within(dialog).getByRole('radio', { name: 'A3 paysage' }));
    await userEvent.selectOptions(within(dialog).getByRole('combobox', { name: 'Pages en largeur' }), '2');
    await userEvent.click(marks);
    await userEvent.click(within(dialog).getByRole('button', { name: 'Créer le PDF' }));
    expect(vi.mocked(exportMap).mock.calls).toEqual([[editor, 'wall', { wall: { paper: 'a3', across: 2, analysis: false } }]]);
    expect(screen.queryByRole('dialog', { name: 'Imprimer en grand' })).toBeNull();
  });

  it('refuses a poster of too many pages before anything is made, and asks fewer pages across', async () => {
    // Eight lanes on a short map: ten pages across make it taller than a hundred pages.
    const lanes = Array.from({ length: 8 }, (_, index) => laneOf(`l${String(index + 1)}`, index));
    const map = sampleMap({ lanes, steps: [stepOf('s1', 'l1', 0, { type: 'start', label: 'Début' }), stepOf('s2', 'l8', 1, { label: 'Fin du travail' })], flows: [] });
    const { editor, dialog } = await wallDialog(map);
    expect(wallPlan(wallSize(editor, map), 'a4', 10).tooMany).toBe(true);
    await userEvent.selectOptions(within(dialog).getByRole('combobox', { name: 'Pages en largeur' }), '10');
    expect(within(dialog).getByRole('alert')).toHaveTextContent('Plus de 100 pages : choisissez moins de pages en largeur.');
    const confirm = within(dialog).getByRole('button', { name: 'Créer le PDF' });
    expect(confirm).toBeDisabled();
    await userEvent.click(confirm);
    expect(exportMap).not.toHaveBeenCalled();
    await userEvent.selectOptions(within(dialog).getByRole('combobox', { name: 'Pages en largeur' }), '1');
    expect(within(dialog).queryByRole('alert')).toBeNull();
    expect(within(dialog).getByRole('button', { name: 'Créer le PDF' })).toBeEnabled();
  });
});
