// valueStream.test.ts
// The value stream as its layer writes it: formats of the numbers in the language of the interface, the four cases of a cartouche with their formulas and sources, the entry, the exit, the shares, the lanes (spec 14.5, ADR 085).

import type { Schema } from '../../../api/client.ts';
import { laneOf, sampleMap, streamOf } from '../../../test/maps.ts';
import {
  casesText, flowShareOf, fteText, laneStreamOf, leadQuantity, percentText, streamFigureOf, workQuantity, type StreamWriting,
} from './valueStream.ts';

type StepStream = Schema<'StepStream'>;

/** A translation that gives back the key and its values, so that each line says which text and which numbers it carries. */
const t = (key: string, values?: Record<string, unknown>): string => (values === undefined ? key : `${key} ${JSON.stringify(values)}`);
const fr: StreamWriting = { language: 'fr', workdayHours: 7, t };

/** The space before the percent sign, whichever one the version of ICU writes (U+00A0 or U+202F), as a plain space. */
const plain = (text: string | undefined): string | undefined => text?.replace(/[\u00a0\u202f]/g, ' ');

const stepStream = (id: string, overrides: Partial<StepStream> = {}): StepStream => ({
  step_id: id, throughput: null, entry: null, entry_volume: null, inflows: [], opened_by: null, executions: null, basis: 'none', work_minutes: null,
  work_origin: 'none', lead_hours: null, lead_origin: 'none', expected_lead_hours: null, work_per_case_minutes: null, charge_hours: null, fte: null,
  human: true, from_draft: false, ...overrides,
});

/** The claims of the mockup: 120 a month enter, 15 % are sent back by the decision, the check takes 20 minutes and 2 days. */
function claims() {
  const map = sampleMap();
  return {
    ...map,
    steps: map.steps.map((step) => (step.id === 's2' ? { ...step, sheet: { ...step.sheet, frequency: 'per_case' as const } } : step)),
    value_stream: streamOf({
      steps: {
        s1: stepStream('s1', { throughput: 120, entry: 'entered', entry_volume: 120 }),
        s2: stepStream('s2', {
          throughput: 141.176, inflows: [{ flow_id: 'f1', source_step_id: 's1', volume: 120 }, { flow_id: 'f4', source_step_id: 's3', volume: 21.176 }],
          executions: 141.176, basis: 'per_case', work_minutes: 20, work_origin: 'sheet', lead_hours: 14, lead_origin: 'sheet', work_per_case_minutes: 20,
          charge_hours: 47.06, fte: 0.3103,
        }),
        s3: stepStream('s3', {
          throughput: 141.176, inflows: [{ flow_id: 'f2', source_step_id: 's2', volume: 141.176 }], executions: 141.176, basis: 'default_per_case',
          work_minutes: 10, work_origin: 'sheet', lead_hours: 10, lead_origin: 'duration', charge_hours: 23.53, fte: 0.1551, from_draft: true,
        }),
        s4: stepStream('s4', { throughput: 120, inflows: [{ flow_id: 'f3', source_step_id: 's3', volume: 120 }] }),
      },
      flows: {
        f1: { flow_id: 'f1', share: 1, origin: 'whole' }, f2: { flow_id: 'f2', share: 1, origin: 'whole' },
        f3: { flow_id: 'f3', share: 0.85, origin: 'remainder' }, f4: { flow_id: 'f4', share: 0.15, origin: 'entered' },
      },
      lanes: {
        l1: { lane_id: 'l1', headcount: null, human: true, charge_hours: 70.59, fte: 0.4654, charge_complete: true },
        l2: { lane_id: 'l2', headcount: 3, human: true, charge_hours: 0, fte: 0, charge_complete: false },
      },
    }),
  };
}

const cardOf = (map: ReturnType<typeof claims>, id: string) => {
  const step = map.steps.find((candidate) => candidate.id === id);
  const figure = step === undefined ? null : streamFigureOf(map, step, fr);
  if (figure?.kind !== 'card') throw new Error(`no cartouche under ${id}`);
  return Object.fromEntries(figure.cases.map((item) => [item.kind, item]));
};

describe('the numbers of the layer, in the language of the interface (spec 14.5)', () => {
  it('writes cases a month whole above 100, with one decimal from 10 to 100, two below', () => {
    expect([casesText(141.176, 'fr'), casesText(100, 'fr'), casesText(12.345, 'fr'), casesText(12, 'fr'), casesText(0.756, 'fr')]).toEqual(['141', '100', '12,3', '12', '0,76']);
    expect(casesText(1234.5, 'en')).toBe('1,235');
  });

  it('writes the full-time equivalents with two decimals, the shares with one at most and no useless zero', () => {
    expect([fteText(0.7, 'fr'), fteText(0.3103, 'en')]).toEqual(['0,70', '0.31']);
    expect([percentText(0.15, 'fr'), percentText(1 / 3, 'fr'), percentText(0.85, 'en')].map(plain)).toEqual(['15 %', '33,3 %', '85%']);
  });

  it('writes a work in minutes under an hour, else in hours; a lead time in minutes, hours or working days of the project', () => {
    expect([workQuantity(20, fr), workQuantity(90, fr)]).toEqual([{ value: '20', unit: 'stream.units.minutes' }, { value: '1,5', unit: 'stream.units.hours' }]);
    expect([leadQuantity(0.5, fr), leadQuantity(2, fr), leadQuantity(14, fr), leadQuantity(14, { ...fr, workdayHours: 8 })]).toEqual([
      { value: '30', unit: 'stream.units.minutes' }, { value: '2', unit: 'stream.units.hours' }, { value: '2', unit: 'stream.units.days' },
      { value: '1,8', unit: 'stream.units.days' },
    ]);
  });
});

describe('the cartouche of a step with content (spec 14.3, 14.5)', () => {
  it('has four cases in their order, the throughput and the workload computed, with their units', () => {
    const card = cardOf(claims(), 's2');
    expect(Object.keys(card)).toEqual(['throughput', 'work', 'lead', 'fte']);
    expect([card.throughput?.quantity, card.work?.quantity, card.lead?.quantity, card.fte?.quantity]).toEqual([
      { value: '141', unit: 'stream.units.per_month' }, { value: '20', unit: 'stream.units.minutes' }, { value: '2', unit: 'stream.units.days' },
      { value: '0,31', unit: 'stream.units.fte' },
    ]);
    expect([card.throughput?.computed, card.work?.computed, card.lead?.computed, card.fte?.computed]).toEqual([true, false, false, true]);
  });

  it('gives the throughput as the sum of what each flow brings, named by the code of its source', () => {
    expect(cardOf(claims(), 's2').throughput?.formula).toEqual([
      'stream.formula.inflow {"code":"VTE-E001","volume":"120"}', 'stream.formula.inflow {"code":"VTE-E003","volume":"21,2"}', 'stream.formula.throughput {"value":"141"}',
    ]);
  });

  it('gives the workload by its executions, its work and the hours of a full-time equivalent', () => {
    expect(cardOf(claims(), 's2').fte?.formula).toEqual([
      'stream.formula.executions_per_case {"value":"141"}',
      'stream.formula.charge {"executions":"141","work":"20 stream.units.minutes","hours":"47,1"}',
      'stream.formula.fte {"hours":"47,1","fte_hours":"151,7","fte":"0,31"}',
    ]);
  });

  it('says where the work and the lead time come from; the values of a draft are marked, the lead time by default too', () => {
    const validated = cardOf(claims(), 's2');
    expect([validated.work?.draft, validated.lead?.draft]).toEqual([false, false]);
    expect(validated.work?.formula).toEqual(['stream.formula.work_sheet {"value":"20 stream.units.minutes"}', 'stream.formula.validated']);
    const draft = cardOf(claims(), 's3');
    expect([draft.work?.draft, draft.lead?.draft, draft.throughput?.draft]).toEqual([true, true, false]);
    expect(draft.lead?.formula).toEqual(['stream.formula.lead_duration {"value":"1,4 stream.units.days"}', 'stream.formula.draft']);
    expect(draft.fte?.formula[0]).toBe('stream.formula.executions_default {"value":"141"}');
  });

  it('shows a dash for an unknown value with the reason, and no workload for software', () => {
    const map = claims();
    const unknown = { ...map, value_stream: { ...map.value_stream, steps: { ...map.value_stream.steps, s2: stepStream('s2', { human: false }) } } };
    const card = cardOf(unknown, 's2');
    expect([card.throughput?.quantity, card.work?.quantity, card.lead?.quantity, card.fte?.quantity]).toEqual([null, null, null, null]);
    expect([card.throughput?.formula, card.work?.formula, card.lead?.formula, card.fte?.formula]).toEqual([
      ['stream.formula.throughput_unknown'], ['stream.formula.work_missing'], ['stream.formula.lead_missing'], ['stream.formula.software'],
    ]);
  });

  it('counts a step done at a cadence by its frequency, and gives its work by case', () => {
    const map = claims();
    const cadence = stepStream('s2', { throughput: 141.176, executions: 4.333, basis: 'cadence', work_minutes: 60, work_origin: 'sheet', work_per_case_minutes: 1.84,
      charge_hours: 4.333, fte: 0.0286 });
    const weekly = { ...map, steps: map.steps.map((step) => (step.id === 's2' ? { ...step, sheet: { ...step.sheet, frequency: 'weekly' as const } } : step)),
      value_stream: { ...map.value_stream, steps: { ...map.value_stream.steps, s2: cadence } } };
    const card = cardOf(weekly, 's2');
    expect(card.fte?.formula[0]).toBe('stream.formula.executions_cadence {"value":"4,33","frequency":"map.frequencies.weekly"}');
    expect(card.work?.formula.at(-1)).toBe('stream.formula.work_per_case {"value":"1,8 stream.units.minutes"}');
  });
});

describe('under the events, on the exits, in the lane headers (spec 14.5)', () => {
  it('writes the entry of a start, or that its volume is to enter, and the exit of an end with its sources', () => {
    const map = claims();
    const [start, , , end] = map.steps;
    expect(start === undefined ? null : streamFigureOf(map, start, fr)).toEqual({
      kind: 'entry', quantity: { value: '120', unit: 'stream.units.per_month' }, formula: ['stream.formula.entry_entered {"volume":"120"}'],
    });
    expect(end === undefined ? null : streamFigureOf(map, end, fr)).toEqual({
      kind: 'exit', quantity: { value: '120', unit: 'stream.units.per_month' },
      formula: ['stream.formula.inflow {"code":"VTE-E003","volume":"120"}', 'stream.formula.throughput {"value":"120"}'],
    });
    const missing = { ...map, value_stream: { ...map.value_stream, steps: { ...map.value_stream.steps, s1: stepStream('s1') } } };
    expect(start === undefined ? null : streamFigureOf(missing, start, fr)).toEqual({ kind: 'entry', quantity: null, formula: ['stream.formula.entry_missing'] });
  });

  it('hangs nothing under a gateway without content nor under a step the stream does not hold', () => {
    const map = claims();
    const parallel = { ...map.steps[2], type: 'parallel' as const, id: 's9' };
    expect(map.steps[2] === undefined ? undefined : streamFigureOf({ ...map, value_stream: streamOf() }, map.steps[2], fr)).toBeNull();
    expect(parallel.code === undefined ? undefined : streamFigureOf({ ...map, value_stream: { ...map.value_stream, steps: { s9: stepStream('s9') } } },
      { ...(map.steps[2] as Schema<'StepView'>), type: 'parallel', id: 's9' }, fr)).toBeNull();
  });

  it('writes the share entered on an exit, the remainder of a decision apart, an unknown share as a question, nothing for a whole flow', () => {
    const map = claims();
    expect(plain(flowShareOf(map, 'f4', 'fr')?.text)).toBe('15 %');
    expect(flowShareOf(map, 'f4', 'fr')).toMatchObject({ remainder: false, unknown: false });
    expect(plain(flowShareOf(map, 'f3', 'fr')?.text)).toBe('85 %');
    expect(flowShareOf(map, 'f3', 'fr')).toMatchObject({ remainder: true, unknown: false });
    expect(flowShareOf(map, 'f1', 'fr')).toBeNull();
    expect(flowShareOf({ ...map, value_stream: { ...map.value_stream, flows: { f4: { flow_id: 'f4', share: null, origin: 'unknown' } } } }, 'f4', 'fr'))
      .toEqual({ text: '?', remainder: false, unknown: true });
  });

  it('gives a lane of people its headcount, or none to enter, and its workload, a minimum when a step lacks it, unknown when none is known; nothing for software', () => {
    const map = claims();
    expect(laneStreamOf(map, 'l1', 'fr')).toEqual({ headcount: null, fte: '0,47', incomplete: false, unknown: false });
    // No workload known in the lane: a dash, not « ≥ 0,00 » (F-63).
    expect(laneStreamOf(map, 'l2', 'fr')).toEqual({ headcount: 3, fte: '0,00', incomplete: true, unknown: true });
    const some = { ...map, value_stream: { ...map.value_stream, lanes: { ...map.value_stream.lanes, l2: { lane_id: 'l2', headcount: 3, human: true, charge_hours: 12, fte: 0.08, charge_complete: false } } } };
    expect(laneStreamOf(some, 'l2', 'fr')).toEqual({ headcount: 3, fte: '0,08', incomplete: true, unknown: false });
    const software = { ...map, lanes: [...map.lanes, laneOf('l3', 2, { type: 'system' })],
      value_stream: { ...map.value_stream, lanes: { l3: { lane_id: 'l3', headcount: null, human: false, charge_hours: 0, fte: 0, charge_complete: true } } } };
    expect(laneStreamOf(software, 'l3', 'fr')).toBeNull();
    // A lane of an outside party, without a headcount nor a step with content of its own (only the end s4 here): nothing
    // to enter, as the check says; with a step of its own, its headcount is asked.
    const outside = { ...map, steps: map.steps.filter((step) => step.id !== 's3'), value_stream: { ...map.value_stream,
      lanes: { l2: { lane_id: 'l2', headcount: null, human: true, charge_hours: 0, fte: 0, charge_complete: true } } } };
    expect(laneStreamOf(outside, 'l2', 'fr')).toBeNull();
    const own = { ...outside, steps: map.steps.map((step) => (step.id === 's3' ? { ...step, lane_id: 'l2' } : step)) };
    expect(laneStreamOf(own, 'l2', 'fr')).toEqual({ headcount: null, fte: '0,00', incomplete: false, unknown: false });
  });
});
