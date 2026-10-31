// streamSheets.ts
// The two sheets of the value stream in the workbook of a map, with the layer on (spec 14.9, ADR 085): « Flux de valeur », a row a step with content, its figures in numbers at full precision and the origin of each; « Bilan », the totals, the settings and the completeness check.

import type { Schema } from '../../../api/client.ts';
import type { Translate } from '../../../shared/i18n/i18n.ts';
import { laneName, stepLabel } from '../model/mapLayout.ts';
import { laneRanks, readingOrder } from '../model/readingOrder.ts';
import { hasContent } from '../model/stepFacts.ts';
import { checkLinesOf } from '../model/streamPanel.ts';
import type { Sheet } from './workbook.ts';

type MapView = Schema<'MapView'>;

/** « Flux de valeur »: a row a step with content in the reading order, numbers at full precision, an unknown value empty (spec 14.9). */
function streamSheet(map: MapView, language: string, t: Translate): Sheet {
  const lanes = new Map(map.lanes.map((lane) => [lane.id, lane]));
  const order = readingOrder(laneRanks(map.lanes));
  const steps = [...map.steps].filter((step) => hasContent(step.type)).sort(order);
  const value = (number: number | null | undefined): number | string => number ?? '';
  return {
    name: t('export.sheet_stream'),
    headers: [
      t('raci.column_code'), t('raci.column_step'), t('export.stream.lane'), t('export.stream.throughput'), t('export.stream.executions'), t('export.stream.basis'),
      t('export.stream.work'), t('export.stream.work_origin'), t('export.stream.lead'), t('export.stream.lead_origin'), t('export.stream.work_per_case'),
      t('export.stream.charge'), t('export.stream.fte'), t('export.stream.sheet'), t('export.stream.draft'),
    ],
    rows: steps.flatMap((step) => {
      const stream = map.value_stream.steps[step.id];
      if (stream === undefined) return [];
      const lane = lanes.get(step.lane_id);
      return [[
        step.code, stepLabel(step, language), lane === undefined ? '' : laneName(lane, language), value(stream.throughput), value(stream.executions),
        t(`export.stream.bases.${stream.basis}`), value(stream.work_minutes), t(`export.stream.origins.${stream.work_origin}`), value(stream.lead_hours),
        t(`export.stream.origins.${stream.lead_origin}`), value(stream.work_per_case_minutes), stream.human ? value(stream.charge_hours) : 0, stream.human ? value(stream.fte) : 0,
        t(`map.sheet_states.${step.sheet.status}`), stream.from_draft ? t('export.yes') : '',
      ]];
    }),
    widths: [12, 36, 22, 14, 14, 26, 14, 22, 14, 22, 14, 14, 10, 14, 12],
  };
}

/** « Bilan »: each total with its unit, the settings, then a row a mention of the completeness check (spec 14.4, 14.9). */
function summarySheet(map: MapView, language: string, t: Translate): Sheet {
  const { totals, work_time: work, main_path: path } = map.value_stream;
  const line = (key: string, number: number | null | undefined, unit: string): (number | string)[] => [t(`export.stream.totals.${key}`), number ?? '', unit];
  const complete = (done: boolean) => t(done ? 'export.stream.complete' : 'export.stream.minimum');
  const rows: (number | string)[][] = [
    line('entry', totals.entry_volume, t('export.stream.units.per_month')),
    line('path_lead', path?.lead_hours, `${t('export.stream.units.working_hours')} · ${path === null ? '' : complete(path.lead_complete)}`),
    line('path_work', path?.work_minutes, `${t('export.stream.units.minutes')} · ${path === null ? '' : complete(path.work_complete)}`),
    line('ratio', path?.work_ratio, t('export.stream.units.share')),
    line('expected_lead', totals.expected_lead_hours, t('export.stream.units.working_hours')),
    line('work_per_case', totals.work_per_case_minutes, t('export.stream.units.minutes')),
    line('charge', totals.charge_hours, `${t('export.stream.units.hours_per_month')} · ${complete(totals.charge_complete)}`),
    line('fte', totals.fte, t('export.stream.units.fte')),
    line('rolled', totals.rolled_complete_accurate, t('export.stream.units.rated', { rated: totals.rated_receptions, receptions: totals.receptions })),
    line('lost', totals.lost_hours_per_month, t('export.stream.units.lost', { count: totals.open_pain_points })),
    line('quantified', totals.steps_quantified, t('export.stream.units.quantified', { count: totals.steps_with_content })),
    line('workday', work.workday_hours, t('export.stream.units.hours')),
    line('fte_hours', work.fte_hours_per_month, t('export.stream.units.hours')),
    ...checkLinesOf(map, { language, workdayHours: work.workday_hours, t })
      .map((check) => [t('export.stream.check', { severity: t(`stream.check.${check.severity}`) }), check.code, check.text]),
  ];
  return { name: t('export.sheet_summary'), headers: [t('export.stream.item'), t('export.stream.value'), t('export.stream.unit')], rows, widths: [36, 26, 70] };
}

/** The two sheets of the value stream, after the three of the map. */
export const streamSheets = (map: MapView, language: string, t: Translate): Sheet[] => [streamSheet(map, language, t), summarySheet(map, language, t)];
