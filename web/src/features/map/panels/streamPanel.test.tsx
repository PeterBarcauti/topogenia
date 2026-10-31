// streamPanel.test.tsx
// The panel of the value stream (spec 3.0 section 15.3, ADR 086): opened by the layer, the summary in tiles with its base, the main path a row a step, the check grouped by code with the object of each mention; each leads to its object; closing turns the layer off; nothing under the lanes.

import { act, fireEvent, screen, within } from '@testing-library/react';
import type { Schema } from '../../../api/client.ts';
import { i18next } from '../../../shared/i18n/i18n.ts';
import { showEditor } from '../../../test/editor.tsx';
import { MAP_ID, sampleMap, streamOf } from '../../../test/maps.ts';
import { checkLinesOf, streamPanelOf } from '../model/streamPanel.ts';

beforeEach(async () => {
  await i18next.changeLanguage('fr');
});

const OTHER_MAP = '9a8b7c6d-0000-4000-8000-000000000099';

/** The figures of the expense claims of the validated mockup: 141 cases a month, 0,70 FTE, 8 days on the main path, 9,1 on average. */
const claims = (overrides: Partial<Schema<'MapStream'>> = {}) => sampleMap({
  value_stream: streamOf({
    main_path: {
      steps: ['s2', 's3'], lead_hours: 56, lead_complete: true, work_minutes: 138, work_complete: false, work_ratio: 0.0411,
      segments: [{ step_id: 's2', lead_hours: 14, work_minutes: 20 }, { step_id: 's3', lead_hours: null, work_minutes: 10 }],
    },
    totals: {
      entry_volume: 141.2, expected_lead_hours: 63.7, work_per_case_minutes: 45, charge_hours: 106.2, charge_complete: true, fte: 0.7002,
      rolled_complete_accurate: 0.85, rated_receptions: 1, receptions: 2, lost_hours_per_month: 12, open_pain_points: 2, steps_with_content: 5, steps_quantified: 4,
    },
    issues: [
      { code: 'VALEUR_BROUILLON', severity: 'warning', map_id: MAP_ID, step_id: 's3', lane_id: null },
      { code: 'EFFECTIF_MANQUANT', severity: 'warning', map_id: MAP_ID, step_id: null, lane_id: 'l2' },
      { code: 'DUREE_MANQUANTE', severity: 'warning', map_id: OTHER_MAP, step_id: null, lane_id: null },
      { code: 'VALEUR_BROUILLON', severity: 'warning', map_id: MAP_ID, step_id: 's2', lane_id: null },
      { code: 'DELAI_PAR_DEFAUT', severity: 'information', map_id: MAP_ID, step_id: 's3', lane_id: null },
    ],
    ...overrides,
  }),
  stream_subjects: {
    s2: { code: 'VTE-E002', label: 'Vérifier la demande', source_language: 'fr', map_id: MAP_ID },
    s3: { code: 'VTE-E003', label: 'Conforme ?', source_language: 'fr', map_id: MAP_ID },
    l2: { code: 'VTE-C02', label: 'Comptabilité', source_language: 'fr', map_id: MAP_ID },
    [OTHER_MAP]: { code: 'VTE-E005', label: 'Facturer', source_language: 'fr', map_id: OTHER_MAP },
  },
});

const writing = { language: 'fr', workdayHours: 7, t: i18next.t.bind(i18next) };
const plain = (text: string | null | undefined) => text?.replace(/[\u00a0\u202f]/g, ' ');

describe('the figures of the panel (spec 3.0 section 15.3)', () => {
  it('writes the summary in six short tiles, a minimum after « ≥ », an unknown as a dash, and the base as entered', () => {
    const figure = streamPanelOf(claims(), 'fr', writing);
    expect(figure.quantified).toBe('4/5 étapes chiffrées');
    expect(figure.tiles.map((tile) => [tile.key, plain(tile.value), plain(tile.note)])).toEqual([
      ['entry', '141 /mois', undefined], ['expected_lead', '9,1 j', undefined], ['work_per_case', '45 min', undefined], ['charge', '106,2 h/mois', '0,70 ETP'],
      ['rolled', '85 %', '1/2 réceptions renseignées'], ['lost', '12 h/mois', '2 irritants ouverts'],
    ]);
    expect(plain(figure.basis)).toBe('Base : 7 h/jour · 151,67 h/ETP/mois');
    const unknown = streamPanelOf(claims({ totals: { ...claims().value_stream.totals, entry_volume: null, charge_complete: false } }), 'fr', writing);
    expect(unknown.tiles.map((tile) => plain(tile.value)).slice(0, 4)).toEqual(['–', '9,1 j', '45 min', '≥ 106,2 h/mois']);
  });

  it('gives the main path its totals and a row a step, a dash for a value unknown', () => {
    const path = streamPanelOf(claims(), 'fr', writing).path;
    expect([path?.lead, plain(path?.work), plain(path?.ratio)]).toEqual(['8 j', '≥ 2,3 h', '4,1 %']);
    expect(path?.rows.map((row) => [row.code, row.label, row.lead, row.work])).toEqual([['VTE-E002', 'Vérifier la demande', '2 j', '20 min'], ['VTE-E003', 'Conforme ?', '–', '10 min']]);
    expect(streamPanelOf(claims({ main_path: null }), 'fr', writing).path).toBeNull();
  });

  it('groups the mentions by code in the order of the check, counts the warnings, writes each whole for the files', () => {
    const figure = streamPanelOf(claims(), 'fr', writing);
    expect(figure.checks.map((group) => [group.code, group.label, group.subjects.map((subject) => subject.code)])).toEqual([
      ['VALEUR_BROUILLON', 'Fiche non validée', ['VTE-E003', 'VTE-E002']], ['EFFECTIF_MANQUANT', 'Effectif manquant', ['VTE-C02']],
      ['DUREE_MANQUANTE', 'Durée manquante', ['VTE-E005']], ['DELAI_PAR_DEFAUT', 'Sans délai : durée retenue', ['VTE-E003']],
    ]);
    expect(figure.warnings).toBe(4);
    expect(checkLinesOf(claims(), writing).map((line) => plain(line.text))).toContain('VTE-C02 Comptabilité : Effectif manquant');
  });
});

async function withPanel(map = claims()) {
  const shown = await showEditor(map);
  act(() => {
    shown.editor.preferences.getState().set('showValueStream', true);
  });
  return { ...shown, panel: screen.getByTestId('stream-panel') };
}

describe('the panel on the map (spec 3.0 section 15.3)', () => {
  it('opens with the layer, beside the canvas, nothing drawn under the lanes', async () => {
    const { canvas, panel } = await withPanel();
    expect(within(panel).getByRole('heading', { level: 2 })).toHaveTextContent('Flux de valeur');
    expect(within(panel).getAllByRole('heading', { level: 3 }).map((heading) => heading.textContent)).toEqual(['Bilan', 'Chemin principal', 'Contrôle']);
    expect(plain(within(panel).getByTestId('stream-tile-charge').textContent)).toBe('Charge106,2 h/mois0,70 ETP');
    expect(within(panel).getByTestId('stream-tile-expected_lead')).toHaveAttribute('title', 'Délai moyen d’un dossier, reprises comprises');
    expect(canvas.contains(panel)).toBe(false);
    expect(canvas.querySelector('[data-testid="stream-block"]')).toBeNull();
  });

  it('leads a row of the path and each object of the check to the map: a step, the steps of a lane, a detailed map opened', async () => {
    const { editor, panel, opened } = await withPanel();
    const reveal = vi.spyOn(editor.canvas, 'reveal');
    fireEvent.click(within(panel).getByTestId('stream-step-VTE-E003'));
    expect(editor.store.getState().selection.steps).toEqual(['s3']);
    expect(reveal).toHaveBeenLastCalledWith(['s3']);
    fireEvent.click(within(panel).getByTestId('stream-chip-EFFECTIF_MANQUANT-VTE-C02'));
    expect(editor.store.getState().selection.steps).toEqual(['s4']);
    fireEvent.click(within(panel).getByTestId('stream-chip-VALEUR_BROUILLON-VTE-E002'));
    expect(editor.store.getState().selection.steps).toEqual(['s2']);
    expect(within(panel).getByTestId('stream-chip-VALEUR_BROUILLON-VTE-E002')).toHaveAttribute('title', 'VTE-E002 Vérifier la demande');
    fireEvent.click(within(panel).getByTestId('stream-chip-DUREE_MANQUANTE-VTE-E005'));
    expect(opened).toEqual([`/maps/${OTHER_MAP}`]);
  });

  it('says « Complet » when nothing is missing, and turns the layer off when closed', async () => {
    const { editor, panel } = await withPanel(claims({ issues: [] }));
    expect(within(panel).getByTestId('stream-check-none')).toHaveTextContent('Complet : rien ne manque');
    fireEvent.click(within(panel).getByTestId('stream-panel-close'));
    expect(editor.preferences.getState().showValueStream).toBe(false);
    expect(screen.queryByTestId('stream-panel')).toBeNull();
  });
});
