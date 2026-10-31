// streamSheets.test.ts
// The value stream in the workbook (spec 14.9): its two sheets with the layer on only, a row a step with content in the reading order, its figures in numbers at full precision with their origins, the totals and the settings, the completeness check; the numbers read back as numbers.

import ExcelJS from 'exceljs';
import type { Schema } from '../../../api/client.ts';
import { i18next } from '../../../shared/i18n/i18n.ts';
import { sampleMap, streamOf } from '../../../test/maps.ts';
import { french as t } from '../../../test/translations.ts';
import { buildMapWorkbook, workbookSheets } from './workbook.ts';

beforeEach(async () => {
  await i18next.changeLanguage('fr');
});

const stepStream = (id: string, overrides: Partial<Schema<'StepStream'>> = {}): Schema<'StepStream'> => ({
  step_id: id, throughput: null, entry: null, entry_volume: null, inflows: [], opened_by: null, executions: null, basis: 'none', work_minutes: null,
  work_origin: 'none', lead_hours: null, lead_origin: 'none', expected_lead_hours: null, work_per_case_minutes: null, charge_hours: null, fte: null,
  human: true, from_draft: false, ...overrides,
});

/** The sample map with the figures of the claims: s2 checked in 20 minutes, the decision s3 a draft without a lead time. */
const streamed = () => sampleMap({
  value_stream: streamOf({
    steps: {
      s1: stepStream('s1', { throughput: 120, entry: 'entered', entry_volume: 120 }),
      s2: stepStream('s2', { throughput: 141.17647058823528, executions: 141.17647058823528, basis: 'per_case', work_minutes: 20, work_origin: 'sheet', lead_hours: 14,
        lead_origin: 'sheet', work_per_case_minutes: 20, charge_hours: 47.05882352941176, fte: 0.31027 }),
      s3: stepStream('s3', { throughput: 141.17647058823528, executions: 141.17647058823528, basis: 'default_per_case', work_minutes: 10, work_origin: 'sheet',
        lead_hours: 10, lead_origin: 'duration', from_draft: true }),
      s4: stepStream('s4', { throughput: 120 }),
    },
    issues: [{ code: 'VALEUR_BROUILLON', severity: 'warning', map_id: 'm', step_id: 's3', lane_id: null }],
    totals: {
      entry_volume: 120, expected_lead_hours: 63.7, work_per_case_minutes: 52.9, charge_hours: 105.88, charge_complete: false, fte: 0.698, rolled_complete_accurate: 0.85,
      rated_receptions: 1, receptions: 2, lost_hours_per_month: 4, open_pain_points: 1, steps_with_content: 2, steps_quantified: 2,
    },
  }),
  stream_subjects: { s3: { code: 'VTE-E003', label: 'Conforme ?', source_language: 'fr', map_id: 'm' } },
});

describe('the sheets of the value stream (spec 14.9)', () => {
  it('come after the three of the map with the layer on only', () => {
    expect(workbookSheets(streamed(), 'fr', t).map((sheet) => sheet.name)).toEqual(['Étapes', 'Liens', 'Matrice RACI']);
    expect(workbookSheets(streamed(), 'fr', t, true).map((sheet) => sheet.name)).toEqual(['Étapes', 'Liens', 'Matrice RACI', 'Flux de valeur', 'Bilan']);
  });

  it('« Flux de valeur »: a row a step with content in the reading order, numbers at full precision, origins and the state of the sheet', () => {
    const sheet = workbookSheets(streamed(), 'fr', t, true)[3];
    expect(sheet?.headers.slice(0, 5)).toEqual(['Code', 'Étape', 'Couloir', 'Débit (dossiers par mois)', 'Exécutions par mois']);
    expect(sheet?.rows).toEqual([
      ['VTE-E002', 'Vérifier la demande', 'Commercial', 141.17647058823528, 141.17647058823528, 'une par dossier', 20, 'fiche', 14, 'fiche', 20, 47.05882352941176, 0.31027,
        'à remplir', ''],
      ['VTE-E003', 'Conforme ?', 'Commercial', 141.17647058823528, 141.17647058823528, 'une par dossier, faute de fréquence', 10, 'fiche', 10, 'durée, faute de délai', '', '', '',
        'à remplir', 'Oui'],
    ]);
  });

  it('« Bilan »: each total with its unit, a total that lacks a value said a minimum, the settings, then the check naming its object', () => {
    const rows = workbookSheets(streamed(), 'fr', t, true)[4]?.rows ?? [];
    expect(rows.find((row) => row[0] === 'Charge')).toEqual(['Charge', 105.88, 'heures par mois · minimum, une valeur manque']);
    expect(rows.find((row) => row[0] === '%C&A cumulé')).toEqual(['%C&A cumulé', 0.85, '1 réceptions renseignées sur 2']);
    expect(rows.find((row) => row[0] === 'Heures par ETP et par mois')).toEqual(['Heures par ETP et par mois', 151.67, 'heures']);
    // The object, then the short label of the panel (spec 3.0 section 15.4).
    expect(rows.at(-1)).toEqual(['Contrôle : Avertissement', 'VALEUR_BROUILLON', 'VTE-E003 Conforme ? : Fiche non validée']);
  });

  it('writes the figures as numbers in the file, an unknown one as an empty cell', async () => {
    const blob = await buildMapWorkbook(workbookSheets(streamed(), 'fr', t, true), { title: 'Ventes', classification: 'Interne' }, { header: null, letters: {} });
    const book = new ExcelJS.Workbook();
    await book.xlsx.load(await blob.arrayBuffer());
    const sheet = book.getWorksheet('Flux de valeur');
    expect([sheet?.getCell('D2').value, sheet?.getCell('M2').value, sheet?.getCell('M3').value]).toEqual([141.17647058823528, 0.31027, null]);
  });
});
