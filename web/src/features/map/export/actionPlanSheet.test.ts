// actionPlanSheet.test.ts
// « Plan d'actions » in the workbook (Makigami M7): a row a pain point in the order of the host, its names in the language shown, its plan, status and hours lost; no sheet without a pain point.

import type { Schema } from '../../../api/client.ts';
import { i18next } from '../../../shared/i18n/i18n.ts';
import { french as t } from '../../../test/translations.ts';
import { actionPlanSheet } from './actionPlanSheet.ts';

beforeEach(async () => {
  await i18next.changeLanguage('fr');
});

const named = (text: string, translations: Record<string, string> = {}): Schema<'NamedText'> => ({ text, source_language: 'fr', translations });

const line = (overrides: Partial<Schema<'ActionPlanLine'>> = {}): Schema<'ActionPlanLine'> => ({
  pain_point_id: 'p1', code: 'PRJ-I001', description: 'Saisie en double', status: 'reported', lost_hours_per_month: 6, action: 'Lire l’extraction',
  owner: 'Service paie', due_date: '2026-12-15', map_id: 'm1', map: named('Demande de congés', { en: 'Leave request' }), step_id: 's1',
  step_code: 'PRJ-E001', step: named('Extraire', { en: 'Extract' }), lane: named('Paie', { en: 'Payroll' }), open: true, receiving_step_id: 's1', receiving_map_id: 'm1',
  document_id: null, can_fill: false, status_step_id: 's1', version: 1, status_changed_at: null, ...overrides,
});

describe('the action plan of the workbook (Makigami M7)', () => {
  it('writes a row a pain point with its map, step, lane, plan, status and hours lost', () => {
    const sheet = actionPlanSheet({ lines: [line()], open_lost_hours_per_month: 6 }, 'fr', t);
    expect(sheet?.name).toBe('Plan d’actions');
    expect(sheet?.headers).toEqual(['Carte', 'Code', 'Étape', 'Couloir', 'Irritant', 'Description', 'Action', 'Responsable', 'Échéance', 'État',
      'Heures perdues par mois']);
    expect(sheet?.rows).toEqual([['Demande de congés', 'PRJ-E001', 'Extraire', 'Paie', 'PRJ-I001', 'Saisie en double', 'Lire l’extraction', 'Service paie',
      '2026-12-15', 'signalé', 6]]);
    expect(sheet?.widths).toHaveLength(sheet?.headers.length ?? 0);
  });

  it('names the map, the step and the lane in the language shown, and leaves the parts of a plan not given empty', () => {
    const sheet = actionPlanSheet({ lines: [line({ action: null, owner: null, due_date: null, lost_hours_per_month: null, lane: null, status: 'adopted' })], open_lost_hours_per_month: 0 }, 'en', t);
    expect(sheet?.rows[0]).toEqual(['Leave request', 'PRJ-E001', 'Extract', '', 'PRJ-I001', 'Saisie en double', '', '', '', 'adopté', '']);
  });

  it('gives no sheet when the plan holds no pain point', () => {
    expect(actionPlanSheet({ lines: [], open_lost_hours_per_month: 0 }, 'fr', t)).toBeNull();
  });
});
