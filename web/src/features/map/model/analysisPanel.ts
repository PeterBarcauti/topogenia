// analysisPanel.ts
// What the panel « Analysis » tells of a map (Makigami, plan 3.1): the lead time of a case in work, wait after a handoff and other wait, and the places where the team can act with their causes in plain words. Nothing is computed here but the words: the core gives every figure and the order.

import type { Schema } from '../../../api/client.ts';
import { laneName, stepLabel } from './mapLayout.ts';
import { hasContent, isMacro } from './stepFacts.ts';
import { leadQuantity, type Quantity, type StreamWriting } from './valueStream.ts';

type MapView = Schema<'MapView'>;
type StepView = Schema<'StepView'>;
type ActionPlace = Schema<'ActionPlace'>;

const joined = (quantity: Quantity): string => `${quantity.value} ${quantity.unit}`;

/** Whole percents of parts, by the largest remainder, so that they add up to 100 as the bar they caption. */
export function wholePercents(parts: readonly number[]): number[] {
  const total = parts.reduce((sum, part) => sum + part, 0);
  if (!(total > 0)) return parts.map(() => 0);
  const exact = parts.map((part) => (part / total) * 100);
  const whole = exact.map(Math.floor);
  const order = exact.map((value, index) => ({ index, rest: value - Math.floor(value) })).sort((a, b) => b.rest - a.rest || a.index - b.index);
  for (const { index } of order.slice(0, 100 - whole.reduce((sum, value) => sum + value, 0))) whole[index] = (whole[index] ?? 0) + 1;
  return whole;
}

/** The lead time in its parts: its work, the three parts of the bar in whole percents (work, wait after a handoff, other wait), the wait and its part after a handoff (null without any). */
export interface LeadSplit {
  readonly work: string;
  readonly percents: readonly number[];
  readonly wait: string | null;
  readonly handoff: string | null;
}

/**
 * The lead time of a case as the panel tells it: its total, its parts (null while one is unknown), and the steps whose
 * lead time is entered when some take the default rule. Null while the lead time is unknown.
 */
export interface LeadStory {
  readonly lead: string;
  readonly split: LeadSplit | null;
  readonly entered: { readonly count: number; readonly total: number } | null;
}

export function leadStoryOf(stream: Schema<'MapStream'>, writing: StreamWriting): LeadStory | null {
  const expected = stream.totals.expected_lead_hours;
  if (expected === null || !(expected > 0)) return null;
  const text = (hours: number) => joined(leadQuantity(hours, writing));
  const parts = stream.analysis.lead;
  const work = parts === null ? 0 : parts.work_hours + parts.waste_work_hours;
  const wait = parts === null ? 0 : parts.handoff_wait_hours + parts.other_wait_hours;
  const { leads_entered: entered } = stream.analysis;
  const total = stream.totals.steps_with_content;
  return {
    lead: text(expected),
    split: parts === null ? null : {
      work: text(work), percents: wholePercents([work, parts.handoff_wait_hours, parts.other_wait_hours]),
      wait: wait > 0 ? text(wait) : null, handoff: parts.handoff_wait_hours > 0 ? text(parts.handoff_wait_hours) : null,
    },
    entered: entered < total ? { count: entered, total } : null,
  };
}

/** The mark at the head of a place: its first cause (the handoffs of a detail with their count), else a clock for a wait alone. */
export type PlaceBadge = 'detail' | 'handoff' | 'break' | 'waste' | 'wait';

/** A cause of a place in plain words, its kind giving its colour. */
export interface PlaceCause {
  readonly kind: 'handoff' | 'break' | 'waste' | 'usable' | 'pains' | 'visits';
  readonly text: string;
}

/**
 * A place of the list: its step, the time lost there (null: unknown), by case when the cases are known, the measure that
 * ranks the list (F-106), by passage otherwise; its mark and its causes.
 */
export interface ActionRow {
  readonly stepId: string;
  readonly code: string;
  readonly name: string;
  readonly lost: string | null;
  readonly perCase: boolean;
  readonly badge: PlaceBadge;
  readonly count: number | null;
  readonly causes: readonly PlaceCause[];
}

/** The lanes that hand the case to a step: the sources of the marked flows that reach it, directly or through steps without content. */
function sendersOf(map: MapView, step: StepView, marked: ReadonlySet<string>): string[] {
  const steps = new Map(map.steps.map((one) => [one.id, one]));
  const lanes = new Set<string>();
  const seen = new Set([step.id]);
  const pending = [step.id];
  for (let at = pending.pop(); at !== undefined; at = pending.pop()) {
    for (const flow of map.flows.filter((one) => one.target_step_id === at)) {
      const source = steps.get(flow.source_step_id);
      if (source === undefined) continue;
      if (marked.has(flow.id)) lanes.add(source.lane_id);
      else if (!hasContent(source.type) && !isMacro(source) && !seen.has(source.id)) {
        seen.add(source.id);
        pending.push(source.id);
      }
    }
  }
  return [...lanes];
}

function causesOf(map: MapView, step: StepView, place: ActionPlace, writing: StreamWriting, shown: string): PlaceCause[] {
  const { t, language } = writing;
  const lane = (id: string) => {
    const found = map.lanes.find((one) => one.id === id);
    return found === undefined ? '' : laneName(found, shown);
  };
  const percent = new Intl.NumberFormat(language, { style: 'percent', maximumFractionDigits: 0 });
  const causes: PlaceCause[] = [];
  if (place.after_handoff) {
    const from = sendersOf(map, step, new Set(map.value_stream.handoffs.flows)).map(lane).join(', ');
    causes.push({ kind: 'handoff', text: from === '' ? t('analysis.cause.relay_entry', { to: lane(step.lane_id) }) : t('analysis.cause.relay', { from, to: lane(step.lane_id) }) });
  }
  if (place.handoffs > 0) causes.push({ kind: 'handoff', text: t('analysis.cause.detail_relays', { value: place.handoffs }) });
  const change = map.value_stream.media_breaks.steps.find((found) => found.step_id === step.id)?.changes[0];
  if (change !== undefined) causes.push({ kind: 'break', text: t('analysis.cause.break', { from: t(`sheet.document.media.${change.from}`), to: t(`sheet.document.media.${change.to}`) }) });
  else if (place.media_breaks > 0) causes.push({ kind: 'break', text: t('analysis.cause.detail_breaks', { value: place.media_breaks }) });
  if (place.waste) causes.push({ kind: 'waste', text: t('menu.value.waste') });
  if (place.usable_as_is !== null && place.usable_as_is < 1) causes.push({ kind: 'usable', text: t('analysis.cause.usable', { value: percent.format(place.usable_as_is) }) });
  if (place.open_pain_points > 0) causes.push({ kind: 'pains', text: t('analysis.cause.pains', { value: place.open_pain_points }) });
  const visits = place.visits;
  if (visits !== null && Math.abs(visits - 1) > 0.005) {
    const text = visits < 1
      ? t('analysis.cause.share', { value: percent.format(visits) })
      : t('analysis.cause.repeat', { value: new Intl.NumberFormat(language, { maximumFractionDigits: 1 }).format(visits) });
    causes.push({ kind: 'visits', text });
  }
  return causes;
}

/** The places where the team can act, in the order of the core, each in words. */
export function actionRowsOf(map: MapView, writing: StreamWriting, shown: string): ActionRow[] {
  return map.value_stream.analysis.places.flatMap((place) => {
    const step = map.steps.find((one) => one.id === place.step_id);
    if (step === undefined) return [];
    const badge: PlaceBadge = place.handoffs > 0 ? 'detail' : place.after_handoff ? 'handoff' : place.media_breaks > 0 ? 'break' : place.waste ? 'waste' : 'wait';
    const lost = place.lost_per_case ?? place.lost_hours;
    return [{
      stepId: step.id, code: step.code, name: stepLabel(step, shown),
      lost: lost === null ? null : joined(leadQuantity(lost, writing)), perCase: place.lost_per_case !== null,
      badge, count: badge === 'detail' ? place.handoffs : null, causes: causesOf(map, step, place, writing, shown),
    }];
  });
}
