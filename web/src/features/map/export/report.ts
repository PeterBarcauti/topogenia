// report.ts
// The report « l'histoire du processus » of a map as a Markdown document (plan 3.1, L4, after a model page of the holder, not published): the texts written with the model, the diagrams, the figures and the tables written by the code from the map; what the team noted and what the model spotted kept apart.

import type { Schema } from '../../../api/client.ts';
import type { Translate } from '../../../shared/i18n/i18n.ts';
import { laneName, stepLabel } from '../model/mapLayout.ts';
import { laneRanks, readingOrder } from '../model/readingOrder.ts';
import { inertText, inline, mermaidBlock, summaryLines, tableOf } from './markdown.ts';
import { flowchartOf, journeyOf, pieOf, quadrantOf, timelineOf, type MermaidLook } from './mermaid.ts';
import { streamSheets } from './streamSheets.ts';
import { workbookSheets } from './workbook.ts';

type MapView = Schema<'MapView'>;
type Report = Schema<'AssistantReportView'>;

/** What the report says of itself: the title, the breadcrumb, the classification, the date, the model it was written with, the footer. */
export interface ReportFacts {
  readonly title: string;
  readonly breadcrumb: string;
  readonly classification: string;
  readonly date: string;
  readonly model: string;
  readonly footer: string;
}

/** What every section reads: the map, its language, the translator, the report, the colours. */
interface Parts {
  readonly map: MapView;
  readonly language: string;
  readonly t: Translate;
  readonly report: Report;
  readonly look: MermaidLook;
}

const WAVES = ['now', 'month', 'later'] as const;

/** The steps of the map by their codes, in the reading order. */
function stepsByCode(map: MapView) {
  const ranks = laneRanks(map.lanes);
  return new Map([...map.steps].sort(readingOrder(ranks)).map((step) => [step.code, step]));
}

/** The label of a step by its code, in the language shown; nothing for a code not on the map. */
const labelOf = (parts: Parts, code: string): string => {
  const step = stepsByCode(parts.map).get(code);
  return step === undefined ? '' : stepLabel(step, parts.language);
};

/** The steps the problems name: coloured as a fault on the diagrams. */
function marked(parts: Parts): Set<string> {
  const codes = stepsByCode(parts.map);
  return new Set(parts.report.problems.flatMap((problem) => problem.steps).flatMap((code) => codes.get(code)?.id ?? []));
}

const section = (title: string, ...body: readonly string[]): string => [`## ${title}`, '', ...body.filter((part) => part !== '').flatMap((part) => [part, ''])].join('\n');

/** « Les acteurs »: what the model reads of them, then the steps by lane as a pie. */
function actors(parts: Parts): string {
  const { map, language, t, report } = parts;
  const counts = map.lanes.map((lane) => ({ label: laneName(lane, language), value: map.steps.filter((step) => step.lane_id === lane.id).length }));
  return section(t('report.actors'), inertText(report.actors), mermaidBlock(pieOf(t('report.steps_by_lane'), counts, t('report.others'))));
}

/** « L'histoire »: each chapter, its text and the part of the map it tells. */
function story(parts: Parts): string {
  const { map, language, t, report, look } = parts;
  const codes = stepsByCode(map);
  const chapters = report.chapters.map((chapter, index) => {
    const only = new Set(chapter.steps.flatMap((code) => codes.get(code)?.id ?? []));
    const diagram = only.size === 0 ? '' : mermaidBlock(flowchartOf(map, language, { only, marked: marked(parts), look }));
    return [`### ${String(index + 1)}. ${inline(chapter.title)}`, '', inertText(chapter.story), '', diagram].filter((line, at, all) => line !== '' || all[at - 1] !== '').join('\n');
  });
  return section(t('report.story'), ...chapters);
}

/**
 * « Le parcours vécu »: a section of the journey per chapter, its title given by the heading above it, then the table of
 * the scores with the lane of each step (the journey draws no actor).
 */
function journey(parts: Parts): string {
  const { map, language, t, report } = parts;
  const codes = stepsByCode(map);
  const lanes = new Map(map.lanes.map((lane) => [lane.id, laneName(lane, language)]));
  const laneOf = (code: string) => { const lane = codes.get(code)?.lane_id; return lane === undefined ? '' : lanes.get(lane) ?? ''; };
  const task = (entry: Report['journey'][number]) => ({ title: entry.short, score: entry.score });
  const inChapters = new Set(report.chapters.flatMap((chapter) => chapter.steps));
  const sections = [...report.chapters.map((chapter) => ({ title: chapter.short, tasks: report.journey.filter((entry) => chapter.steps.includes(entry.step)).map(task) })),
    { title: t('report.other_steps'), tasks: report.journey.filter((entry) => !inChapters.has(entry.step)).map(task) }];
  const rows = report.journey.map((entry) => [entry.step, labelOf(parts, entry.step), laneOf(entry.step), entry.score, entry.why]);
  return section(t('report.journey'), mermaidBlock(journeyOf('', sections)), t('report.journey_scale'), tableOf({
    headers: [t('report.column_code'), t('report.column_step'), t('report.column_lane'), t('report.column_score'), t('report.column_why')], rows,
  }, language));
}

/** « Ce qui coince »: the pain points the team noted, from the sheets, then every problem with its origin. */
function problems(parts: Parts): string {
  const { language, t, report } = parts;
  const noted = report.noted.map((point) => [point.code, point.step === null ? '' : `${point.step} ${labelOf(parts, point.step)}`.trim(),
    point.description, point.lost_hours_per_month ?? '', t(`sheet.pain_point.statuses.${point.status}`)]);
  const listed = report.problems.map((problem, index) => [`#${String(index + 1)}`,
    problem.source === 'noted' ? `${t('report.noted')} (${problem.pain_point ?? ''})` : t('report.spotted'), problem.steps.join(', '),
    `${t(`assistant.panel.kinds.${problem.kind}`)} : ${problem.title}`, problem.detail]);
  return section(t('report.problems'),
    `### ${t('report.noted_title')}`, noted.length === 0 ? t('report.none')
      : tableOf({ headers: [t('report.column_code'), t('report.column_step'), t('report.column_pain_point'), t('report.column_hours'), t('report.column_status')], rows: noted }, language),
    `### ${t('report.problems_title')}`, listed.length === 0 ? t('report.none')
      : tableOf({ headers: [t('report.column_number'), t('report.column_source'), t('report.column_steps'), t('report.column_problem'), t('report.column_detail')], rows: listed }, language));
}

/** « Les actions »: the matrix by effort and impact, its points named A1, A2..., then the table of the actions. */
function actions(parts: Parts): string {
  const { language, t, report } = parts;
  if (report.actions.length === 0) return section(t('report.actions'), t('report.none'));
  const texts = {
    title: '', x: [t('report.effort_low'), t('report.effort_high')], y: [t('report.impact_low'), t('report.impact_high')],
    quadrants: [t('report.quadrant_plan'), t('report.quadrant_now'), t('report.quadrant_maybe'), t('report.quadrant_avoid')],
  } as const;
  const points = report.actions.map((action, index) => ({ name: `A${String(index + 1)}`, effort: action.effort, impact: action.impact }));
  const rows = report.actions.map((action, index) => [`A${String(index + 1)}`, action.title, action.detail, action.problems.map((number) => `#${String(number)}`).join(', '),
    action.owner, action.effort, action.impact, t(`report.wave_${action.wave}`)]);
  return section(t('report.actions'), mermaidBlock(quadrantOf(texts, points)), tableOf({
    headers: [t('report.column_number'), t('report.column_action'), t('report.column_detail'), t('report.column_treats'), t('report.column_owner'),
      t('report.column_effort'), t('report.column_impact'), t('report.column_wave')], rows,
  }, language));
}

/** « Plan d'actions »: the actions in their waves, on a timeline. */
function plan(parts: Parts): string {
  const { t, report } = parts;
  const periods = WAVES.map((wave) => ({
    title: t(`report.wave_${wave}`), events: report.actions.flatMap((action, index) => (action.wave === wave ? [`A${String(index + 1)} ${action.short}`] : [])),
  }));
  return report.actions.length === 0 ? '' : section(t('report.plan'), mermaidBlock(timelineOf('', periods)));
}

/** « Le temps »: with the layer of the value stream, its tables as the workbook gives them. */
function time(parts: Parts, valueStream: boolean): string {
  const { map, language, t } = parts;
  return valueStream ? section(t('report.time'), ...streamSheets(map, language, t).flatMap((sheet) => [`### ${sheet.name}`, tableOf(sheet, language)])) : '';
}

/** « Annexes »: the steps and the RACI matrix, as the workbook gives them. */
function annexes(parts: Parts): string {
  const { map, language, t } = parts;
  const [steps, , raci] = workbookSheets(map, language, t);
  return section(t('report.annexes'), ...[steps, raci].flatMap((sheet) => (sheet === undefined ? [] : [`### ${sheet.name}`, tableOf(sheet, language)])));
}

/** The report: its header, then its eleven sections in the order of the model page. */
export function reportMarkdown(map: MapView, language: string, t: Translate, facts: ReportFacts, report: Report, look: MermaidLook, valueStream = false): string {
  const parts: Parts = { map, language, t, report, look };
  const head = [`# ${inline(t('report.title', { title: facts.title }))}`, '', ...summaryLines(map, language), ...(facts.breadcrumb === '' ? [] : [`> ${inline(facts.breadcrumb)}`, '>']),
    `> ${facts.classification} · ${facts.date}`, '>',
    // In copy and paste, no model of the application wrote it: the AI of the person is not named.
    `> ${facts.model.trim() === '' ? t('report.written_with_pasted') : t('report.written_with', { model: facts.model })}`, ''].join('\n');
  const body = [
    section(t('report.minute'), inertText(report.summary)),
    section(t('report.overview'), mermaidBlock(flowchartOf(map, language, { marked: marked(parts), look }))),
    actors(parts), story(parts), journey(parts), problems(parts), actions(parts), plan(parts), time(parts, valueStream),
    section(t('report.conclusion'), inertText(report.conclusion)), annexes(parts),
  ].filter((part) => part !== '');
  return [head, ...body, `_${facts.footer}_`, ''].join('\n');
}
