// streamPanel.ts
// What the panel of the value stream shows (spec 3.0 section 15.3, ADR 086): the summary in tiles, the base of the computation, the main path a row a step, the completeness check grouped by code with the object of each mention; the mentions written whole for the workbook and the PDF. Nothing is computed here: the core gives every figure.

import type { Schema } from '../../../api/client.ts';
import { stepLabel } from './mapLayout.ts';
import { casesText, fteText, hoursText, leadQuantity, percentText, workQuantity, type Quantity, type StreamWriting } from './valueStream.ts';

type MapView = Schema<'MapView'>;

/** The tiles of the summary, in their order. */
export type TileKey = 'entry' | 'expected_lead' | 'work_per_case' | 'charge' | 'rolled' | 'lost';

/** A tile: its figure (a dash when unknown, « ≥ » before a minimum), a precision under it, or none. */
export interface StreamTile {
  readonly key: TileKey;
  readonly value: string;
  readonly note: string | null;
}

/** A step of the main path: its lead time and its work by case, a dash when unknown. */
export interface PathRow {
  readonly stepId: string;
  readonly code: string;
  readonly label: string;
  readonly lead: string;
  readonly work: string;
}

/** The main path: its totals (« ≥ » when a value lacks), the work over the lead time, its rows. */
export interface PathFigure {
  readonly lead: string;
  readonly work: string;
  readonly ratio: string;
  readonly rows: readonly PathRow[];
}

/** The object of a mention: its code (the code of the project for the root map), its code and label, where it leads. */
export interface CheckSubject {
  readonly code: string;
  readonly name: string;
  readonly mapId: string;
  readonly stepId: string | null;
  readonly laneId: string | null;
}

/** The mentions of one code: its gravity, its short label, their objects in the order of the check. */
export interface CheckGroup {
  readonly code: string;
  readonly severity: Schema<'StreamSeverity'>;
  readonly label: string;
  readonly subjects: readonly CheckSubject[];
}

/** A mention written whole, « object : label », with its code and gravity (the workbook, the PDF). */
export interface CheckLine {
  readonly code: string;
  readonly severity: Schema<'StreamSeverity'>;
  readonly text: string;
}

export interface StreamPanelFigure {
  /** « n/m steps quantified » (spec 14.4). */
  readonly quantified: string;
  readonly tiles: readonly StreamTile[];
  /** The settings of the project as entered: the base of every figure. */
  readonly basis: string;
  readonly path: PathFigure | null;
  readonly checks: readonly CheckGroup[];
  /** The mentions of gravity « warning », counted on the strip of the panel folded. */
  readonly warnings: number;
}

const DASH = '–';

const joined = (quantity: Quantity): string => `${quantity.value} ${quantity.unit}`;

/** A setting of the project as entered: two decimals at most. */
const settingText = (hours: number, language: string): string => new Intl.NumberFormat(language, { maximumFractionDigits: 2 }).format(hours);

function tilesOf(map: MapView, writing: StreamWriting): StreamTile[] {
  const { t, language } = writing;
  const totals = map.value_stream.totals;
  const perMonth = t('stream.units.per_month');
  const minimum = (value: string, complete: boolean) => (complete ? value : t('stream.minimum', { value }));
  return [
    { key: 'entry', value: totals.entry_volume === null ? DASH : `${casesText(totals.entry_volume, language)} ${perMonth}`, note: null },
    { key: 'expected_lead', value: totals.expected_lead_hours === null ? DASH : joined(leadQuantity(totals.expected_lead_hours, writing)), note: null },
    { key: 'work_per_case', value: totals.work_per_case_minutes === null ? DASH : joined(workQuantity(totals.work_per_case_minutes, writing)), note: null },
    {
      key: 'charge', value: minimum(`${hoursText(totals.charge_hours, language)} ${t('stream.units.hours')}${perMonth}`, totals.charge_complete),
      note: minimum(t('stream.lane.fte', { fte: fteText(totals.fte, language) }), totals.charge_complete),
    },
    {
      key: 'rolled', value: totals.rolled_complete_accurate === null ? DASH : percentText(totals.rolled_complete_accurate, language),
      note: t('stream.panel.receptions', { rated: totals.rated_receptions, receptions: totals.receptions }),
    },
    {
      key: 'lost', value: `${hoursText(totals.lost_hours_per_month, language)} ${t('stream.units.hours')}${perMonth}`,
      note: t('stream.panel.pain_points', { count: totals.open_pain_points }),
    },
  ];
}

function pathOf(map: MapView, shown: string, writing: StreamWriting): PathFigure | null {
  const path = map.value_stream.main_path;
  if (path === null) return null;
  const steps = new Map(map.steps.map((step) => [step.id, step]));
  const minimum = (value: string, complete: boolean) => (complete ? value : writing.t('stream.minimum', { value }));
  return {
    lead: minimum(joined(leadQuantity(path.lead_hours, writing)), path.lead_complete),
    work: minimum(joined(workQuantity(path.work_minutes, writing)), path.work_complete),
    ratio: path.work_ratio == null ? DASH : percentText(path.work_ratio, writing.language),
    rows: path.segments.flatMap((segment): PathRow[] => {
      const step = steps.get(segment.step_id);
      return step === undefined ? [] : [{
        stepId: step.id, code: step.code, label: stepLabel(step, shown),
        lead: segment.lead_hours === null ? DASH : joined(leadQuantity(segment.lead_hours, writing)),
        work: segment.work_minutes === null ? DASH : joined(workQuantity(segment.work_minutes, writing)),
      }];
    }),
  };
}

/** The object of each mention: a step or a lane by its code and label, a detailed map by the step it details (spec 14.5). */
function subjectOf(map: MapView, issue: Schema<'StreamIssue'>): CheckSubject {
  const [stepId, laneId] = [issue.step_id ?? null, issue.lane_id ?? null];
  const subject = map.stream_subjects[stepId ?? laneId ?? issue.map_id];
  const code = subject?.code ?? '';
  return { code, name: subject === undefined ? '' : `${subject.code} ${subject.label}`, mapId: issue.map_id, stepId, laneId };
}

/** The mentions of the check grouped by code, in the order of the check: the warnings first, then the reading order (spec 14.4). */
export function checkGroupsOf(map: MapView, writing: StreamWriting): CheckGroup[] {
  const groups = new Map<string, { severity: Schema<'StreamSeverity'>; subjects: CheckSubject[] }>();
  for (const issue of map.value_stream.issues) {
    const group = groups.get(issue.code) ?? { severity: issue.severity, subjects: [] };
    group.subjects.push(subjectOf(map, issue));
    groups.set(issue.code, group);
  }
  return [...groups].map(([code, group]) => ({ code, severity: group.severity, label: writing.t(`stream.issues.${code}`), subjects: group.subjects }));
}

/** Each mention of the check written whole, « object : label », for the workbook and the PDF (spec 3.0 section 15.4). */
export function checkLinesOf(map: MapView, writing: StreamWriting): CheckLine[] {
  return map.value_stream.issues.map((issue) => ({
    code: issue.code, severity: issue.severity,
    text: writing.t('stream.check.mention', { object: subjectOf(map, issue).name, message: writing.t(`stream.issues.${issue.code}`) }),
  }));
}

/** The panel of the value stream of a map: its numbers in the language of the interface, its steps named in the language shown. */
export function streamPanelOf(map: MapView, shown: string, writing: StreamWriting): StreamPanelFigure {
  const { t, language } = writing;
  const totals = map.value_stream.totals;
  const work = map.value_stream.work_time;
  return {
    quantified: t('stream.check.quantified', { quantified: totals.steps_quantified, count: totals.steps_with_content }),
    tiles: tilesOf(map, writing),
    // The settings as entered, two decimals at most (spec 14.2, S6), not rounded as a computed figure.
    basis: t('stream.panel.basis', { workday: settingText(work.workday_hours, language), fte_hours: settingText(work.fte_hours_per_month, language) }),
    path: pathOf(map, shown, writing),
    checks: checkGroupsOf(map, writing),
    warnings: map.value_stream.issues.filter((issue) => issue.severity === 'warning').length,
  };
}
