// valueStream.ts
// The value stream of a map as its layer writes it (spec 14.5, ADR 085): the four cases of the cartouche of a step with content, each with its formula and its sources; the entry of a start, the exit of an end; the share of an exit of a gateway; the headcount and the workload of a lane. Nothing is computed here: the core gives every figure.

import type { Schema } from '../../../api/client.ts';
import type { Translate } from '../../../shared/i18n/i18n.ts';
import { hasContent, isMacro } from './stepFacts.ts';

type MapView = Schema<'MapView'>;
type StepView = Schema<'StepView'>;
type StepStream = Schema<'StepStream'>;

/** What the numbers of the layer are written with: the language of the interface and the working day of the project. */
export interface StreamWriting {
  readonly language: string;
  readonly workdayHours: number;
  readonly t: Translate;
}

/** A number and its unit, apart, so that the unit is drawn smaller (validated mockup). */
export interface Quantity {
  readonly value: string;
  readonly unit: string;
}

const number = (value: number, language: string, digits: number, fixed = false): string =>
  new Intl.NumberFormat(language, { maximumFractionDigits: digits, minimumFractionDigits: fixed ? digits : 0 }).format(value);

/** Cases a month: whole above 100, one decimal from 10 to 100, two below (spec 14.5). */
export const casesText = (value: number, language: string): string => number(value, language, value > 100 ? 0 : value >= 10 ? 1 : 2);

/** Full-time equivalents: two decimals, always. */
export const fteText = (value: number, language: string): string => number(value, language, 2, true);

/** Hours of work: one decimal at most. */
export const hoursText = (value: number, language: string): string => number(value, language, 1);

/** A share from 0 to 1 as a percentage, one decimal at most, without a useless zero. */
export const percentText = (share: number, language: string): string =>
  new Intl.NumberFormat(language, { style: 'percent', maximumFractionDigits: 1 }).format(share);

/** A work in minutes: in minutes under an hour, else in hours (one decimal at most). */
export function workQuantity(minutes: number, writing: StreamWriting): Quantity {
  return minutes < 60
    ? { value: number(minutes, writing.language, 1), unit: writing.t('stream.units.minutes') }
    : { value: hoursText(minutes / 60, writing.language), unit: writing.t('stream.units.hours') };
}

/** A lead time in working hours: in minutes under an hour, in hours under a working day, else in working days (spec 14.5). */
export function leadQuantity(hours: number, writing: StreamWriting): Quantity {
  if (hours < 1) return { value: number(hours * 60, writing.language, 1), unit: writing.t('stream.units.minutes') };
  if (hours < writing.workdayHours) return { value: hoursText(hours, writing.language), unit: writing.t('stream.units.hours') };
  return { value: number(hours / writing.workdayHours, writing.language, 1), unit: writing.t('stream.units.days') };
}

const joined = (quantity: Quantity): string => `${quantity.value} ${quantity.unit}`;

/** The kinds of the four cases of a cartouche, in their order: throughput and workload computed, work and lead time entered. */
export type CaseKind = 'throughput' | 'work' | 'lead' | 'fte';

/** A case of a cartouche: its number (null: unknown, a dash), its unit, whether it is computed or comes from a draft, its formula. */
export interface StreamCase {
  readonly kind: CaseKind;
  readonly quantity: Quantity | null;
  readonly computed: boolean;
  readonly draft: boolean;
  /** The formula and its sources, line by line: the tooltip of the case and its description for a screen reader. */
  readonly formula: readonly string[];
}

/** What the layer hangs under a step: the cartouche of a step with content, the entry of a start, the exit of an end, nothing else. */
export type StreamFigure =
  | { readonly kind: 'card'; readonly cases: readonly StreamCase[] }
  | { readonly kind: 'entry' | 'exit'; readonly quantity: Quantity | null; readonly formula: readonly string[] };

/** The readable code of a step of the map, else nothing: the source of an inflow or the gateway of a join. */
const codeOf = (map: MapView, stepId: string | null): string => map.steps.find((step) => step.id === stepId)?.code ?? '';

function throughputFormula(map: MapView, stream: StepStream, writing: StreamWriting): string[] {
  const { t, language } = writing;
  const lines: string[] = [];
  if (stream.entry !== null && stream.entry_volume !== null)
    lines.push(t(`stream.formula.entry_${stream.entry}`, { volume: casesText(stream.entry_volume, language) }));
  for (const inflow of stream.inflows)
    lines.push(inflow.volume === null ? t('stream.formula.inflow_unknown', { code: codeOf(map, inflow.source_step_id) })
      : t('stream.formula.inflow', { code: codeOf(map, inflow.source_step_id), volume: casesText(inflow.volume, language) }));
  if (stream.opened_by !== null) lines.push(t('stream.formula.join', { code: codeOf(map, stream.opened_by) }));
  lines.push(stream.throughput === null ? t('stream.formula.throughput_unknown') : t('stream.formula.throughput', { value: casesText(stream.throughput, language) }));
  return lines;
}

/** Where a value of the sheet comes from: its draft or its latest validated version (spec 14.4). */
const sheetSource = (stream: StepStream, t: Translate): string => t(stream.from_draft ? 'stream.formula.draft' : 'stream.formula.validated');

function workCase(stream: StepStream, writing: StreamWriting): StreamCase {
  const { t } = writing;
  const quantity = stream.work_minutes === null ? null : workQuantity(stream.work_minutes, writing);
  const fromSheet = stream.work_origin === 'sheet';
  const formula = {
    sheet: () => [t('stream.formula.work_sheet', { value: quantity === null ? '' : joined(quantity) }), sheetSource(stream, t)],
    detailed: () => [t('stream.formula.work_detailed', { value: quantity === null ? '' : joined(quantity) })],
    no_work: () => [t('stream.formula.work_wait')],
    duration: () => [t('stream.formula.work_missing')],
    none: () => [t('stream.formula.work_missing')],
  }[stream.work_origin]();
  if (stream.work_per_case_minutes !== null && stream.basis === 'cadence')
    formula.push(t('stream.formula.work_per_case', { value: joined(workQuantity(stream.work_per_case_minutes, writing)) }));
  return { kind: 'work', quantity: stream.work_origin === 'no_work' ? null : quantity, computed: false, draft: fromSheet && stream.from_draft, formula };
}

function leadCase(stream: StepStream, writing: StreamWriting): StreamCase {
  const { t } = writing;
  const quantity = stream.lead_hours === null ? null : leadQuantity(stream.lead_hours, writing);
  const value = quantity === null ? '' : joined(quantity);
  const formula = {
    sheet: () => [t('stream.formula.lead_sheet', { value }), sheetSource(stream, t)],
    duration: () => [t('stream.formula.lead_duration', { value }), sheetSource(stream, t)],
    detailed: () => [t('stream.formula.lead_detailed', { value })],
    no_work: () => [t('stream.formula.lead_missing')],
    none: () => [t('stream.formula.lead_missing')],
  }[stream.lead_origin]();
  const fromSheet = stream.lead_origin === 'sheet' || stream.lead_origin === 'duration';
  return { kind: 'lead', quantity, computed: false, draft: fromSheet && stream.from_draft, formula };
}

/** The executions of a step in a month and how they are counted (spec 14.3, 6). */
function executionsLine(stream: StepStream, frequency: string | null, writing: StreamWriting): string {
  const { t, language } = writing;
  const value = stream.executions === null ? '' : casesText(stream.executions, language);
  if (stream.basis === 'occasional') return t('stream.formula.executions_occasional');
  if (stream.basis === 'detailed') return t('stream.formula.executions_detailed');
  if (stream.executions === null) return t('stream.formula.executions_unknown');
  if (stream.basis === 'cadence') return t('stream.formula.executions_cadence', { value, frequency: frequency === null ? '' : t(`map.frequencies.${frequency}`) });
  return t(stream.basis === 'default_per_case' ? 'stream.formula.executions_default' : 'stream.formula.executions_per_case', { value });
}

function fteCase(stream: StepStream, step: StepView, fteHours: number, writing: StreamWriting): StreamCase {
  const { t, language } = writing;
  if (!stream.human) return { kind: 'fte', quantity: null, computed: true, draft: false, formula: [t('stream.formula.software')] };
  const formula = [executionsLine(stream, step.sheet.frequency, writing)];
  if (stream.charge_hours === null || stream.fte === null) formula.push(t('stream.formula.charge_unknown'));
  else {
    const hours = hoursText(stream.charge_hours, language);
    formula.push(stream.basis === 'detailed' ? t('stream.formula.charge_detailed', { hours })
      : t('stream.formula.charge', { executions: casesText(stream.executions ?? 0, language), work: joined(workQuantity(stream.work_minutes ?? 0, writing)), hours }));
    formula.push(t('stream.formula.fte', { hours, fte_hours: hoursText(fteHours, language), fte: fteText(stream.fte, language) }));
  }
  const quantity = stream.fte === null ? null : { value: fteText(stream.fte, language), unit: t('stream.units.fte') };
  return { kind: 'fte', quantity, computed: true, draft: false, formula };
}

/** What the layer hangs under a step; null for a gateway without content or a step the stream does not hold. */
export function streamFigureOf(map: MapView, step: StepView, writing: StreamWriting): StreamFigure | null {
  const stream = map.value_stream.steps[step.id];
  if (stream === undefined) return null;
  const { t, language } = writing;
  if (step.type === 'start') {
    const quantity = stream.entry_volume === null ? null : { value: casesText(stream.entry_volume, language), unit: t('stream.units.per_month') };
    const formula = stream.entry === null || stream.entry_volume === null ? [t('stream.formula.entry_missing')]
      : [t(`stream.formula.entry_${stream.entry}`, { volume: casesText(stream.entry_volume, language) })];
    return { kind: 'entry', quantity, formula };
  }
  if (step.type === 'end') {
    const quantity = stream.throughput === null ? null : { value: casesText(stream.throughput, language), unit: t('stream.units.per_month') };
    return { kind: 'exit', quantity, formula: throughputFormula(map, stream, writing) };
  }
  if (!hasContent(step.type)) return null;
  const throughput = stream.throughput === null ? null : { value: casesText(stream.throughput, language), unit: t('stream.units.per_month') };
  return {
    kind: 'card',
    cases: [
      { kind: 'throughput', quantity: throughput, computed: true, draft: false, formula: throughputFormula(map, stream, writing) },
      workCase(stream, writing),
      leadCase(stream, writing),
      fteCase(stream, step, map.value_stream.work_time.fte_hours_per_month, writing),
    ],
  };
}

/** The share an exit of a decision or an inclusive gateway carries, written after its label; null for any other flow. */
export interface FlowShareText {
  readonly text: string;
  /** The remainder of a decision, computed: written in italics (spec 14.5). */
  readonly remainder: boolean;
  readonly unknown: boolean;
}

export function flowShareOf(map: MapView, flowId: string, language: string): FlowShareText | null {
  const share = map.value_stream.flows[flowId];
  if (share === undefined || share.origin === 'whole') return null;
  if (share.share === null || share.origin === 'unknown') return { text: '?', remainder: false, unknown: true };
  return { text: percentText(share.share, language), remainder: share.origin === 'remainder', unknown: false };
}

/**
 * The headcount and the workload of a lane of people, in its header; null for a lane of software, and for a lane without
 * a headcount that holds no step with content of its own (an outside party, an end): the check asks the headcount of a
 * lane with such a step only, a detailed step counting in its detailed map (spec 14.3, 16).
 */
export interface LaneStreamText {
  /** The headcount, or null when it is to enter. */
  readonly headcount: number | null;
  readonly fte: string;
  /** Some step of the lane has no known workload: its workload is a minimum. */
  readonly incomplete: boolean;
  /** No workload of the lane is known: a dash, not « ≥ 0,00 » (F-63). */
  readonly unknown: boolean;
}

export function laneStreamOf(map: MapView, laneId: string, language: string): LaneStreamText | null {
  const lane = map.value_stream.lanes[laneId];
  if (lane === undefined || !lane.human) return null;
  const own = map.steps.some((step) => step.lane_id === laneId && hasContent(step.type) && !isMacro(step));
  if (lane.headcount === null && !own) return null;
  return { headcount: lane.headcount, fte: fteText(lane.fte, language), incomplete: !lane.charge_complete, unknown: !lane.charge_complete && lane.charge_hours === 0 };
}
