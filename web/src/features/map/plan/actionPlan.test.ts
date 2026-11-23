// actionPlan.test.ts
// The action plan as data (ADR 112): the entries the panel and the sheet of the export both read, the late mark on the local day, the sorts, the filters and the counts.

import type { Schema } from '../../../api/client.ts';
import { i18next } from '../../../shared/i18n/i18n.ts';
import { french as t } from '../../../test/translations.ts';
import { actionPlanSheet } from '../export/actionPlanSheet.ts';
import { filterEntries, isLate, planCounts, planEntries, sortEntries, type PlanEntry } from './actionPlan.ts';

beforeEach(async () => {
  await i18next.changeLanguage('fr');
});

const named = (text: string, translations: Record<string, string> = {}): Schema<'NamedText'> => ({ text, source_language: 'fr', translations });

let counter = 0;
const line = (overrides: Partial<Schema<'ActionPlanLine'>> = {}): Schema<'ActionPlanLine'> => {
  counter += 1;
  return {
    pain_point_id: `p${counter}`, code: `PRJ-I00${counter}`, description: 'Saisie en double', status: 'reported', lost_hours_per_month: 6,
    action: 'Lire l’extraction', owner: 'Service paie', due_date: '2026-12-15', map_id: 'm1', map: named('Demande de congés', { en: 'Leave request' }),
    step_id: 's1', step_code: 'PRJ-E001', step: named('Extraire', { en: 'Extract' }), lane: named('Paie', { en: 'Payroll' }), open: true,
    receiving_step_id: 's1', receiving_map_id: 'm1', document_id: null, can_fill: false, status_step_id: 's1', version: 1, status_changed_at: null, ...overrides,
  };
};

const planOf = (...lines: Schema<'ActionPlanLine'>[]): Schema<'ActionPlanView'> => ({ lines, open_lost_hours_per_month: 0 });
const codes = (entries: ReturnType<typeof planEntries>): string[] => entries.map((entry) => entry.line.code);

describe('the entries of the plan', () => {
  it('the panel and the sheet read the same entries, cell by cell, in the order of the host', () => {
    const plan = planOf(
      line({ code: 'B', lane: named('Paie', { en: 'Payroll' }), map: named('Demande de congés', { en: 'Leave request' }) }),
      line({ code: 'A', lane: null, action: null, owner: null, due_date: null, lost_hours_per_month: null, status: 'adopted', open: false }),
      line({ code: 'C', map: named('Détail', { en: 'Detail' }), step: named('Valider', { en: 'Validate' }) }),
    );
    for (const language of ['fr', 'en']) {
      const entries = planEntries(plan, language, t);
      expect(codes(entries)).toEqual(['B', 'A', 'C']);
      expect(actionPlanSheet(plan, language, t)?.rows).toEqual(entries.map((entry) => entry.cells));
    }
    expect(planEntries(plan, 'en', t)[0]?.cells.slice(0, 4)).toEqual(['Leave request', 'PRJ-E001', 'Extract', 'Payroll']);
    expect(planEntries(plan, 'fr', t)[1]?.cells).toEqual(['Demande de congés', 'PRJ-E001', 'Extraire', '', 'A', 'Saisie en double', '', '', '', 'adopté', '']);
  });

  it('takes open from the host and says without action when the action is not written', () => {
    const entries = planEntries(planOf(line({ open: false }), line({ action: null }), line({ action: '   ' }), line()), 'fr', t);
    expect(entries.map((entry) => entry.open)).toEqual([false, true, true, true]);
    expect(entries.map((entry) => entry.noAction)).toEqual([false, true, true, false]);
  });
});

describe('a pain point late', () => {
  const entryOf = (overrides: Partial<Schema<'ActionPlanLine'>>): PlanEntry => {
    const [entry] = planEntries(planOf(line(overrides)), 'fr', t);
    if (entry === undefined) throw new Error('no entry');
    return entry;
  };

  it('open whose due date is yesterday is late; today, tomorrow, no due date, or dealt with, it is not', () => {
    expect(isLate(entryOf({ due_date: '2026-11-20' }), '2026-11-21')).toBe(true);
    expect(isLate(entryOf({ due_date: '2026-11-21' }), '2026-11-21')).toBe(false);
    expect(isLate(entryOf({ due_date: '2026-11-22' }), '2026-11-21')).toBe(false);
    expect(isLate(entryOf({ due_date: null }), '2026-11-21')).toBe(false);
    expect(isLate(entryOf({ due_date: '2026-11-20', open: false, status: 'implemented' }), '2026-11-21')).toBe(false);
  });

  it('is judged by the days as written, over a month, a year and the zero of a month', () => {
    expect(isLate(entryOf({ due_date: '2026-09-30' }), '2026-10-01')).toBe(true);
    expect(isLate(entryOf({ due_date: '2025-12-31' }), '2026-01-01')).toBe(true);
    expect(isLate(entryOf({ due_date: '2026-10-01' }), '2026-09-30')).toBe(false);
  });
});

describe('the sorts', () => {
  const entries = planEntries(planOf(
    line({ code: 'A', lost_hours_per_month: 2, due_date: null }), line({ code: 'B', lost_hours_per_month: 8, due_date: '2026-12-20' }),
    line({ code: 'C', lost_hours_per_month: null, due_date: '2026-12-01' }), line({ code: 'D', lost_hours_per_month: 8, due_date: '2026-12-01' }),
  ), 'fr', t);

  it('sorting by hours lost puts the largest first and the empty last, the order of the host among equals', () => {
    expect(codes(sortEntries(entries, 'hours'))).toEqual(['B', 'D', 'A', 'C']);
  });

  it('sorting by due date puts the nearest first and the empty last', () => {
    expect(codes(sortEntries(entries, 'due'))).toEqual(['C', 'D', 'B', 'A']);
  });

  it('the order of the project is the order of the host, and a sort leaves the list it is given as it was', () => {
    expect(codes(sortEntries(entries, 'project'))).toEqual(['A', 'B', 'C', 'D']);
    sortEntries(entries, 'hours');
    expect(codes(entries)).toEqual(['A', 'B', 'C', 'D']);
  });
});

describe('the filters and the counts', () => {
  const entries = planEntries(planOf(
    line({ code: 'A', due_date: '2026-11-01' }), line({ code: 'B', action: null }), line({ code: 'C', open: false, status: 'adopted', due_date: '2026-11-01', action: null }),
  ), 'fr', t);
  const today = '2026-11-21';

  it('open, late and without action add up', () => {
    expect(planCounts(entries, today)).toEqual({ total: 3, open: 2, late: 1, noAction: 2 });
  });

  it('narrow the list together, each one alone and none at all', () => {
    expect(codes(filterEntries(entries, { open: false, late: false, noAction: false }, today))).toEqual(['A', 'B', 'C']);
    expect(codes(filterEntries(entries, { open: true, late: false, noAction: false }, today))).toEqual(['A', 'B']);
    expect(codes(filterEntries(entries, { open: false, late: true, noAction: false }, today))).toEqual(['A']);
    expect(codes(filterEntries(entries, { open: false, late: false, noAction: true }, today))).toEqual(['B', 'C']);
    expect(codes(filterEntries(entries, { open: true, late: false, noAction: true }, today))).toEqual(['B']);
  });
});
