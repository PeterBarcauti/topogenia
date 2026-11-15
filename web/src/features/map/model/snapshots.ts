// snapshots.ts
// What the panel « Analysis » tells of the photos of a project and of its target state (Makigami M6, M8, plan 3.1): each indicator named, its values and its gain written in the unit of the indicator. Nothing is computed here but the words: the core gives every figure and which way is better.

import type { Schema } from '../../../api/client.ts';
import { hoursText, leadQuantity, percentText, workQuantity, type StreamWriting } from './valueStream.ts';

type IndicatorChange = Schema<'IndicatorChange'>;

/** The unit of an indicator: a lead time, a work by case, a share, a count, a count by case, hours. */
type IndicatorKind = 'lead' | 'work' | 'share' | 'count' | 'ratio' | 'hours';

/** The unit of each indicator of the core (`IndicatorComparison.Indicators`), by its code. */
const KINDS: Readonly<Record<string, IndicatorKind>> = {
  lead_hours: 'lead', handoff_wait_hours: 'lead', other_wait_hours: 'lead', work_minutes: 'work', value_added_share: 'share', coverage: 'share',
  handoffs: 'count', handoffs_per_case: 'ratio', media_breaks: 'count', rolled_complete_accurate: 'share', lost_hours_per_month: 'hours',
  open_pain_points: 'count',
};

/** The name of each indicator: the words of the Bilan of the exports, already in every language; the open pain points their own. */
const NAMES: Readonly<Record<string, string>> = {
  lead_hours: 'export.stream.totals.expected_lead', handoff_wait_hours: 'export.stream.totals.lead_handoff_wait',
  other_wait_hours: 'export.stream.totals.lead_other_wait', work_minutes: 'export.stream.totals.work_per_case',
  value_added_share: 'export.stream.totals.value_added_share', coverage: 'export.stream.totals.value_coverage', handoffs: 'export.stream.totals.handoffs',
  handoffs_per_case: 'export.stream.totals.handoffs_per_case', media_breaks: 'export.stream.totals.media_breaks',
  rolled_complete_accurate: 'export.stream.totals.rolled', lost_hours_per_month: 'export.stream.totals.lost', open_pain_points: 'snapshots.open_pain_points',
};

/** The two waits are parts of the lead time (F-105): drawn under it, set in, never added to the work. */
const PARTS: ReadonlySet<string> = new Set(['handoff_wait_hours', 'other_wait_hours']);

/** An indicator compared in words: its name, whether it is a part of the lead time, its two values and its gain (null: unknown), and which way it goes. */
export interface ChangeRow {
  readonly code: string;
  readonly name: string;
  readonly part: boolean;
  readonly before: string | null;
  readonly after: string | null;
  readonly gain: string | null;
  readonly better: boolean | null;
}

/** A value of an indicator in its unit, never negative here: the sign of a gain is written apart. */
function written(kind: IndicatorKind, value: number, writing: StreamWriting): string {
  const { language, t } = writing;
  switch (kind) {
    case 'lead': {
      const quantity = leadQuantity(value, writing);
      return `${quantity.value} ${quantity.unit}`;
    }
    case 'work': {
      const quantity = workQuantity(value, writing);
      return `${quantity.value} ${quantity.unit}`;
    }
    case 'share':
      return percentText(value, language);
    case 'count':
      return new Intl.NumberFormat(language, { maximumFractionDigits: 0 }).format(value);
    case 'ratio':
      return new Intl.NumberFormat(language, { maximumFractionDigits: 1 }).format(value);
    case 'hours':
      return `${hoursText(value, language)} ${t('stream.units.hours')}`;
  }
}

/** A gain: its sign, then its value in the unit of the indicator; nothing moved is a plain zero, whatever the unit. */
function signed(kind: IndicatorKind, gain: number, writing: StreamWriting): string {
  if (gain === 0) return '0';
  return `${gain > 0 ? '+' : '−'}${written(kind, Math.abs(gain), writing)}`;
}

/** The indicators compared by the core, in its order, each in words. */
export function changeRowsOf(changes: readonly IndicatorChange[], writing: StreamWriting): ChangeRow[] {
  return changes.map((change) => {
    const kind = KINDS[change.indicator] ?? 'ratio';
    const value = (figure: number | null) => figure === null ? null : written(kind, figure, writing);
    return {
      code: change.indicator, name: writing.t(NAMES[change.indicator] ?? change.indicator), part: PARTS.has(change.indicator),
      before: value(change.before), after: value(change.after), gain: change.gain === null ? null : signed(kind, change.gain, writing),
      better: change.better,
    };
  });
}

/** A date of a photo in the language of the interface, without its hour. */
export function snapshotDate(at: string, language: string): string {
  return new Intl.DateTimeFormat(language, { dateStyle: 'medium' }).format(new Date(at));
}
