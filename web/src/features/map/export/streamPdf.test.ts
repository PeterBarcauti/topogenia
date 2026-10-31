// streamPdf.test.ts
// The content of the pages of the value stream in the PDF (spec 3.0 section 15.4): the texts of the panel, the summary with its precisions set in, the main path with its column heads, the check grouped by code with its objects; a map without figures says so.

import { i18next } from '../../../shared/i18n/i18n.ts';
import { MAP_ID, sampleMap, streamOf } from '../../../test/maps.ts';
import { streamPdfOf } from './streamPdf.ts';

beforeEach(async () => {
  await i18next.changeLanguage('fr');
});

const t = i18next.t.bind(i18next);
const plain = (text: string) => text.replace(/[\u00a0\u202f]/g, ' ');

const streamed = () => sampleMap({
  value_stream: streamOf({
    main_path: { steps: ['s2'], lead_hours: 14, lead_complete: true, work_minutes: 20, work_complete: true, work_ratio: 0.0238, segments: [{ step_id: 's2', lead_hours: 14, work_minutes: 20 }] },
    totals: {
      entry_volume: 141.2, expected_lead_hours: 63.7, work_per_case_minutes: 45, charge_hours: 106.2, charge_complete: true, fte: 0.7002,
      rolled_complete_accurate: null, rated_receptions: 0, receptions: 0, lost_hours_per_month: 0, open_pain_points: 0, steps_with_content: 2, steps_quantified: 1,
    },
    issues: [{ code: 'DUREE_MANQUANTE', severity: 'warning', map_id: MAP_ID, step_id: 's3', lane_id: null }, { code: 'DELAI_PAR_DEFAUT', severity: 'information', map_id: MAP_ID, step_id: 's2', lane_id: null }],
  }),
  stream_subjects: {
    s2: { code: 'VTE-E002', label: 'Vérifier la demande', source_language: 'fr', map_id: MAP_ID },
    s3: { code: 'VTE-E003', label: 'Conforme ?', source_language: 'fr', map_id: MAP_ID },
  },
});

describe('the pages of the value stream in the PDF (spec 3.0 section 15.4)', () => {
  it('give the title of the map, the steps quantified and the base, then the three sections of the panel', () => {
    const content = streamPdfOf(streamed(), 'fr', 'VTE - Ventes', 'fr', t);
    expect([content.title, plain(content.subtitle)]).toEqual(['Flux de valeur · VTE - Ventes', '1/2 étapes chiffrées · Base : 7 h/jour · 151,67 h/ETP/mois']);
    const [summary, path, check] = content.sections;
    expect(summary?.lines.slice(0, 2).map((line) => [line.text, line.figures?.map(plain)])).toEqual([['Entrée', ['141 /mois']], ['Délai moyen', ['9,1 j']]]);
    expect(summary?.lines.find((line) => line.text === '0,70 ETP')).toEqual({ text: '0,70 ETP', grey: true, indent: true });
    expect(path?.lines.map((line) => [line.text, line.figures?.map(plain)])).toEqual([
      ['Délai', ['2 j']], ['Travail', ['20 min']], ['Travail / délai', ['2,4 %']], ['Étape', ['Délai', 'Travail']], ['VTE-E002 Vérifier la demande', ['2 j', '20 min']],
    ]);
    expect(check?.lines).toEqual([
      { text: 'Durée manquante', figures: ['1'], bold: true, grey: false }, { text: 'VTE-E003 Conforme ?', indent: true },
      { text: 'Sans délai : durée retenue', figures: ['1'], bold: false, grey: true }, { text: 'VTE-E002 Vérifier la demande', indent: true },
    ]);
  });

  it('say there is no main path, and that nothing is missing', () => {
    const [, path, check] = streamPdfOf(sampleMap(), 'fr', 'VTE - Ventes', 'fr', t).sections;
    expect([path?.lines, check?.lines]).toEqual([[{ text: 'Aucun chemin d’un Début à une Fin', grey: true }], [{ text: 'Complet : rien ne manque' }]]);
  });
});
