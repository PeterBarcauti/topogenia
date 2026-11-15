// analysisPanel.test.ts
// What the panel « Analysis » tells (Makigami, plan 3.1): the whole percents of the bar, the lead time of a case in words, and the places where the team can act with their causes in plain words.

import type { Schema } from '../../../api/client.ts';
import { i18next } from '../../../shared/i18n/i18n.ts';
import { flowOf, laneOf, mapOf, stepOf, streamOf } from '../../../test/maps.ts';
import { french as t } from '../../../test/translations.ts';
import { actionRowsOf, leadStoryOf, wholePercents } from './analysisPanel.ts';

beforeEach(async () => {
  await i18next.changeLanguage('fr');
});

const writing = { language: 'fr', workdayHours: 7, t };
const plain = (text: string | null | undefined) => text?.replace(/[\u00a0\u202f]/g, ' ');

const place = (stepId: string, overrides: Partial<Schema<'ActionPlace'>> = {}): Schema<'ActionPlace'> => ({
  step_id: stepId, lost_hours: null, visits: null, lost_per_case: null, after_handoff: false, handoffs: 0, media_breaks: 0, waste: false, usable_as_is: null,
  open_pain_points: 0, ...overrides,
});

/** Two lanes: the visits hand the case over to the accounts through a parallel gateway; the accounts change its medium. */
const workshop = (places: Schema<'ActionPlace'>[]) => mapOf({
  lanes: [laneOf('l1', 0, { name: 'Commercial' }), laneOf('l2', 1, { name: 'Comptabilité', type: 'department' })],
  steps: [
    stepOf('s1', 'l1', 0, { type: 'start' }), stepOf('s2', 'l1', 1, { label: 'Saisir la commande' }), stepOf('g', 'l2', 2, { type: 'parallel' }),
    stepOf('s3', 'l2', 3, { label: 'Facturer' }), stepOf('s4', 'l2', 4, { type: 'end' }),
  ],
  flows: [flowOf('f1', 's1', 's2'), flowOf('f2', 's2', 'g'), flowOf('f3', 'g', 's3'), flowOf('f4', 's3', 's4')],
  value_stream: streamOf({
    handoffs: { flows: ['f2'], detailed_steps: {}, count: 1, per_case: 1 },
    media_breaks: { steps: [{ step_id: 's3', changes: [{ from: 'paper', to: 'email' }] }], detailed_steps: {}, count: 1, documents: 2, documents_with_medium: 2 },
    analysis: { lead: null, leads_entered: 0, places },
  }),
});

describe('the bar of the lead time (plan 3.1)', () => {
  it('rounds its parts to whole percents that add up to 100, the largest remainders first', () => {
    expect(wholePercents([3.5, 15.4, 5.6])).toEqual([14, 63, 23]);
    expect(wholePercents([1, 1, 1])).toEqual([34, 33, 33]);
    expect(wholePercents([0, 0, 0])).toEqual([0, 0, 0]);
  });

  it('tells the lead time, its work, its wait and the part after a handoff in the unit of the lead time, and the lead times entered', () => {
    const stream = streamOf({
      analysis: { lead: { work_hours: 3, waste_work_hours: 0.5, handoff_wait_hours: 15.4, other_wait_hours: 5.6 }, leads_entered: 3, places: [] },
      totals: { ...streamOf().totals, expected_lead_hours: 24.5, steps_with_content: 12 },
    });
    const story = leadStoryOf(stream, writing);
    expect([story?.lead, story?.split?.work, story?.split?.wait, story?.split?.handoff].map(plain)).toEqual(['3,5 j', '3,5 h', '3 j', '2,2 j']);
    expect(story?.split?.percents).toEqual([14, 63, 23]);
    expect(story?.entered).toEqual({ count: 3, total: 12 });
  });

  it('says nothing without a lead time, keeps the lead time while its parts are unknown, no wait without one', () => {
    expect(leadStoryOf(streamOf(), writing)).toBeNull();
    const unsplit = leadStoryOf(streamOf({ totals: { ...streamOf().totals, expected_lead_hours: 14 } }), writing);
    expect([plain(unsplit?.lead), unsplit?.split, unsplit?.entered]).toEqual(['2 j', null, null]);
    const working = leadStoryOf(streamOf({
      analysis: { lead: { work_hours: 2, waste_work_hours: 0, handoff_wait_hours: 0, other_wait_hours: 0 }, leads_entered: 0, places: [] },
      totals: { ...streamOf().totals, expected_lead_hours: 2 },
    }), writing);
    expect([working?.split?.wait, working?.split?.handoff]).toEqual([null, null]);
  });
});

describe('the places where the team can act (plan 3.1)', () => {
  it('writes each cause in plain words, the handoff named by the lanes it comes through a gateway from, in the order of the core; the time lost by case (F-106)', () => {
    const rows = actionRowsOf(workshop([
      place('s3', { lost_hours: 6, visits: 0.3, lost_per_case: 1.8, after_handoff: true, media_breaks: 1, usable_as_is: 0.6, open_pain_points: 1 }),
      place('s2', { lost_hours: 2, visits: 1, lost_per_case: 2, waste: true }),
    ]), writing, 'fr');
    expect(rows.map((row) => [row.code, row.name, plain(row.lost), row.badge, row.perCase])).toEqual([
      ['VIS-E004', 'Facturer', '1,8 h', 'handoff', true], ['VIS-E002', 'Saisir la commande', '2 h', 'waste', true],
    ]);
    expect(rows[0]?.causes.map((cause) => [cause.kind, plain(cause.text)])).toEqual([
      ['handoff', 'Relais Commercial → Comptabilité'], ['break', 'Rupture Papier → Courriel'], ['usable', 'Utilisable tel quel : 60 %'],
      ['pains', 'Irritants ouverts : 1'], ['visits', '30 % des dossiers'],
    ]);
    expect(rows[1]?.causes.map((cause) => cause.text)).toEqual(['Gaspillage']);
  });

  it('counts the handoffs of a detail on its badge, says a time unknown, a case that comes from the map above and a step passed again', () => {
    const rows = actionRowsOf(workshop([
      place('s2', { handoffs: 6, media_breaks: 2, usable_as_is: 1, lost_hours: 14 }),
      place('s1', { after_handoff: true, visits: 2.5 }),
    ]), writing, 'fr');
    expect(rows.map((row) => [row.badge, row.count, plain(row.lost), row.perCase])).toEqual([['detail', 6, '2 j', false], ['handoff', null, undefined, false]]);
    expect(rows[0]?.causes.map((cause) => cause.text)).toEqual(['Relais dans son détail : 6', 'Ruptures dans son détail : 2']);
    expect(rows[1]?.causes.map((cause) => plain(cause.text))).toEqual(['Relais vers Commercial', '2,5 fois par dossier']);
  });
});
