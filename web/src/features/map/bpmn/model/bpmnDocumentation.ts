// bpmnDocumentation.ts
// The documentation a .bpmn carries in its own tags (ADR 106): the context of the project on the collaboration, the whole sheet of a step on its node, in Markdown and in the language shown, so that the file says everything without any other file; never the location of a document.

import type { Translate } from '../../../../shared/i18n/i18n.ts';
import type { PmapDocument, PmapDocumentEntry, PmapSheet, PmapStep, PmapUse } from '../../../../shared/pmap.ts';
import { plainNumber } from './bpmnText.ts';

/** A pain point of a step: raised on it by a use of one of its outputs (it is written where it is suffered, shown where the document is made). */
export interface StepPainPoint {
  readonly use: PmapUse;
  readonly document: string;
  readonly open: boolean;
}

/** What the documentation of a step needs beyond the step: the names of the lanes and the labels shown. */
export interface StepContext {
  readonly laneName: (laneId: string) => string;
  readonly label: string;
  readonly comment: string | null;
  readonly documentName: (documentId: string) => string;
  readonly producerCode: (documentId: string) => string | null;
  readonly painPoints: readonly StepPainPoint[];
}

/** What the documentation of the project says besides the .pmap: the map shown and the classification, from the interface. */
export interface ProjectContext {
  readonly mapTitle: string;
  readonly classification: string | null;
  readonly language: string;
  readonly summary: string | null;
}

/** A label and its value, as the language writes them (« Durée : 10 minutes », « Duration: 10 minutes »). */
const field = (t: Translate, label: string, value: string): string => t('bpmn.doc.field', { label, value });

const line = (t: Translate, label: string, value: string | undefined | null): string | null =>
  value === undefined || value === null || value.trim() === '' ? null : field(t, label, value);

const join = (lines: readonly (string | null)[]): string => lines.filter((text): text is string => text !== null).join('\n');

/** The documentation of the collaboration: the project, the map shown, its classification, its work time, its summary, its glossary. */
export function projectDocumentation(document: PmapDocument, context: ProjectContext, t: Translate): string {
  const project = document.project;
  return join([
    `# ${project.name}${project.code === undefined ? '' : ` (${project.code})`}`,
    t('bpmn.doc.map', { name: context.mapTitle }),
    context.classification === null ? null : t('bpmn.doc.classification', { value: context.classification }),
    t('bpmn.doc.language', { language: context.language }),
    t('bpmn.doc.work_time', { workday: plainNumber(project.workday_hours ?? 7), fte: plainNumber(project.fte_hours_per_month ?? 151.67) }),
    line(t, t('summary.title'), context.summary),
    line(t, t('bpmn.doc.description'), project.description),
    project.glossary === undefined ? null : field(t, t('translations.glossary'), `\n${project.glossary}`),
  ]);
}

/** A document: its name, its medium, its waiting time; never its location (a path of a computer says nothing to a reader). */
function entry(document: PmapDocumentEntry, t: Translate): string {
  const parts = [
    document.medium === undefined ? null : t(`sheet.document.media.${document.medium}`),
    document.wait_hours === undefined ? null : t('bpmn.doc.wait', { hours: plainNumber(document.wait_hours) }),
  ].filter((part): part is string => part !== null);
  return parts.length === 0 ? document.name : `${document.name} (${parts.join(', ')})`;
}

/** A use of an output of another step: its name, the step that makes it, its wait, its part usable as is. */
function used(use: PmapUse, context: StepContext, t: Translate): string {
  const producer = context.producerCode(use.document);
  const parts = [
    producer === null ? null : t('bpmn.doc.from', { code: producer }),
    use.wait_hours === undefined ? null : t('bpmn.doc.wait', { hours: plainNumber(use.wait_hours) }),
    use.percent_complete_accurate === undefined ? null : t('sheet.document.percent_short', { percent: use.percent_complete_accurate }),
  ].filter((part): part is string => part !== null);
  const name = context.documentName(use.document);
  return parts.length === 0 ? name : `${name} (${parts.join(', ')})`;
}

/** The durations and the frequency of a sheet, on one line each. */
function timing(sheet: PmapSheet, t: Translate): (string | null)[] {
  const bounds = sheet.duration_min_minutes !== undefined && sheet.duration_max_minutes !== undefined
    ? t('sheet.duration_between', { count: sheet.duration_max_minutes, min: plainNumber(sheet.duration_min_minutes), max: plainNumber(sheet.duration_max_minutes) })
    : sheet.duration_min_minutes !== undefined ? t('sheet.duration_at_least', { count: sheet.duration_min_minutes, min: plainNumber(sheet.duration_min_minutes) })
      : sheet.duration_max_minutes !== undefined ? t('sheet.duration_at_most', { count: sheet.duration_max_minutes, max: plainNumber(sheet.duration_max_minutes) }) : null;
  const duration = sheet.duration_minutes === undefined ? null : t('sheet.duration_value', { count: sheet.duration_minutes });
  return [
    duration === null && bounds === null ? null : field(t, t('sheet.duration'), [duration, bounds].filter((part) => part !== null).join(', ')),
    sheet.lead_time === undefined ? null : field(t, t('sheet.lead_time'), t(`sheet.lead_time_value.${sheet.lead_time.unit}`, { count: sheet.lead_time.value })),
    sheet.frequency === undefined ? null : field(t, t('sheet.frequency'), t(`map.frequencies.${sheet.frequency}`)),
  ];
}

/** The lines of the procedure, numbered, each with its tool and its nature. */
function procedure(sheet: PmapSheet, t: Translate): string | null {
  const tasks = (sheet.procedure ?? []).filter((task) => task.text !== undefined && task.text.trim() !== '');
  if (tasks.length === 0) return null;
  const lines = tasks.map((task, index) => {
    const tags = [task.tool, task.nature === undefined ? undefined : t(`sheet.natures.${task.nature}`)].filter((tag): tag is string => tag !== undefined && tag !== '');
    return `${String(index + 1)}. ${task.text ?? ''}${tags.length === 0 ? '' : ` [${tags.join(' · ')}]`}`;
  });
  return field(t, t('sheet.procedure'), `\n${lines.join('\n')}`);
}

/** A pain point raised on the step: its code, its text, its hours, its status and its action plan. */
function painPoint(point: StepPainPoint, t: Translate): string {
  const use = point.use;
  const title = use.pain_point_code === undefined ? t('sheet.pain_point.title') : t('sheet.pain_point.title_code', { code: use.pain_point_code });
  const parts = [
    field(t, t('bpmn.doc.quote', { text: point.document }), use.pain_point ?? ''),
    use.lost_hours_per_month === undefined ? null : t('sheet.pain_point.lost_short', { hours: plainNumber(use.lost_hours_per_month) }),
    use.pain_point_status === undefined ? null : t(`sheet.pain_point.statuses.${use.pain_point_status}`),
    use.action === undefined ? null : t('sheet.pain_point.plan_action', { action: use.action }),
    use.owner === undefined ? null : t('sheet.pain_point.plan_owner', { owner: use.owner }),
    use.due_date === undefined ? null : t('sheet.pain_point.plan_due', { date: use.due_date }),
  ].filter((part): part is string => part !== null);
  return field(t, title, parts.join(' ; '));
}

/** The roles of the step: its R, the lane of the step, then the other lanes by their letter. */
function roles(step: PmapStep, context: StepContext, t: Translate): string {
  const others = Object.entries(step.raci ?? {}).filter(([, letter]) => letter !== 'R').map(([lane, letter]) => field(t, t(`raci.names.${letter}`), context.laneName(lane)));
  return [field(t, t('raci.names.R'), context.laneName(step.lane)), ...others].join(' ; ');
}

/** The documentation of a step: its code and label, its roles, its value, its sheet, its documents, its pain points, its comment. */
export function stepDocumentation(step: PmapStep, context: StepContext, t: Translate): string {
  const sheet = step.sheet ?? {};
  return join([
    `**${step.id}** ${context.label}`,
    roles(step, context, t),
    step.value_class === undefined ? null : t(`menu.value.${step.value_class}`),
    step.volume === undefined ? null : t('bpmn.doc.volume', { quantity: plainNumber(step.volume.quantity), period: t(`bpmn.doc.periods.${step.volume.period}`) }),
    ...timing(sheet, t),
    line(t, t('sheet.actor'), sheet.actor),
    line(t, t('sheet.tools'), sheet.tools),
    line(t, t('sheet.purpose'), sheet.purpose),
    procedure(sheet, t),
    sheet.free_text === undefined ? null : field(t, t('sheet.free_title'), `\n${sheet.free_text}`),
    line(t, t('sheet.inputs'), [...(sheet.inputs ?? []).map((input) => entry(input, t)), ...(sheet.uses ?? []).map((use) => used(use, context, t))].join(' ; ')),
    line(t, t('sheet.outputs'), (sheet.outputs ?? []).map((output) => entry(output, t)).join(' ; ')),
    ...context.painPoints.map((point) => painPoint(point, t)),
    line(t, t('translations.types.presentation_comment'), context.comment),
  ]);
}
