// streamPdf.ts
// The pages of the value stream in the PDF of a map, with its layer on (spec 3.0 section 15.4, ADR 086): the summary and its base, the main path a row a step, the check grouped by code with the object of each mention, the texts of the panel.

import type { Schema } from '../../../api/client.ts';
import type { Translate } from '../../../shared/i18n/i18n.ts';
import { streamPanelOf } from '../model/streamPanel.ts';
import type { StreamPdfContent, StreamPdfLine } from './pdf.ts';

/** The content of the pages: the numbers in the language of the interface, the steps named in the language shown. */
export function streamPdfOf(map: Schema<'MapView'>, shown: string, mapTitle: string, language: string, t: Translate): StreamPdfContent {
  const figure = streamPanelOf(map, shown, { language, workdayHours: map.value_stream.work_time.workday_hours, t });
  const summary = figure.tiles.flatMap((tile): StreamPdfLine[] => [
    { text: t(`stream.panel.tiles.${tile.key}`), figures: [tile.value] },
    ...(tile.note === null ? [] : [{ text: tile.note, grey: true, indent: true }]),
  ]);
  const path: StreamPdfLine[] = figure.path === null ? [{ text: t('stream.timeline.none'), grey: true }] : [
    { text: t('stream.timeline.lead'), figures: [figure.path.lead] },
    { text: t('stream.timeline.work'), figures: [figure.path.work] },
    { text: t('stream.panel.ratio'), figures: [figure.path.ratio] },
    { text: t('stream.panel.step'), figures: [t('stream.timeline.lead'), t('stream.timeline.work')], grey: true },
    ...figure.path.rows.map((row) => ({ text: `${row.code} ${row.label}`, figures: [row.lead, row.work] })),
  ];
  const checks: StreamPdfLine[] = figure.checks.length === 0 ? [{ text: t('stream.check.none') }] : figure.checks.flatMap((group) => [
    { text: group.label, figures: [String(group.subjects.length)], bold: group.severity === 'warning', grey: group.severity !== 'warning' },
    ...group.subjects.map((subject) => ({ text: subject.name === '' ? subject.code : subject.name, indent: true })),
  ]);
  return {
    title: `${t('ribbon.value_stream')} · ${mapTitle}`,
    subtitle: `${figure.quantified} · ${figure.basis}`,
    sections: [
      { title: t('stream.panel.summary'), lines: summary },
      { title: t('stream.timeline.title'), lines: path },
      { title: t('stream.check.title'), lines: checks },
    ],
  };
}
