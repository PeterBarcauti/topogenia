// script.ts
// What a presentation says (ADR 097): its scenes, and for each its sentences, one fact a sentence, in the language shown; only what the map does not already write (essai J2: the step and its actor are read in its box, its type in its shape, an exit on its arrow); a fact unknown leaves its sentence out; the figures are those of the core; the comment of presentation of a step said instead of its sentences, in its language (ADR 099).

import type { Schema } from '../../../../api/client.ts';
import type { Translate } from '../../../../shared/i18n/i18n.ts';
import { hasContent } from '../../model/stepFacts.ts';
import { casesText, percentText } from '../../model/valueStream.ts';
import { countersAt } from './counters.ts';
import { documentsSaid } from './documentVoice.ts';
import { chunksOf } from './subtitles.ts';
import { entriesOf, type Tour } from './tour.ts';

type MapView = Schema<'MapView'>;
type StepView = Schema<'StepView'>;

/** The names of the map in the language shown, as the layout writes them on the canvas. */
export interface Names {
  readonly step: (id: string) => { readonly label: string; readonly laneId: string; readonly code: string } | null;
  readonly lane: (id: string) => { readonly name: string; readonly tint: number } | null;
  readonly flow: (id: string) => { readonly text: string; readonly documents: readonly string[] } | null;
}

export interface ScriptContext {
  readonly map: MapView;
  readonly tour: Tour;
  readonly names: Names;
  /** The texts in the language shown. */
  readonly t: Translate;
  readonly language: string;
  /**
   * The lines of the comments of presentation of the map, by the step or the flow each comments (ADR 099 revised; the
   * flows since the lot J10); none read yet, the sentences of the steps and of the ways.
   */
  readonly comments?: Readonly<Record<string, readonly CommentLine[]>>;
}

/** A line of a comment of presentation as the script reads it, of a step or of a flow: its text in a language. */
export type CommentLine = Pick<Schema<'PresentationCommentView'>, 'text' | 'language' | 'source' | 'version'>;

/** What a scene says: its sentences, cut into subtitles. No banner: it repeated the box lit on the map (essai J2). */
export interface Script {
  readonly lines: readonly string[];
}

/** A scene: the whole map before and after, a stop of the tour, the passage from a stop to the next. */
export type Scene = { readonly kind: 'opening' } | { readonly kind: 'step'; readonly stop: number } | { readonly kind: 'passage'; readonly stop: number } | { readonly kind: 'closing' };

/** The scenes of a tour: the opening, each stop and the passage after it, the closing; none for an empty tour. */
export function scenesOf(tour: Tour): Scene[] {
  if (tour.stops.length === 0) return [];
  const middle = tour.stops.flatMap((stop, index): Scene[] => (stop.next === null ? [{ kind: 'step', stop: index }] : [{ kind: 'step', stop: index }, { kind: 'passage', stop: index }]));
  return [{ kind: 'opening' }, ...middle, { kind: 'closing' }];
}

/** A quantity in words in the language shown (« 15 minutes », « 2,3 jours »): the unit long, its plural given by the browser. */
function quantity(value: number, unit: 'minute' | 'hour' | 'day', language: string): string {
  return new Intl.NumberFormat(language, { style: 'unit', unit, unitDisplay: 'long', maximumFractionDigits: value >= 10 ? 0 : 1 }).format(value);
}

/** A work: in minutes under an hour, else in hours. */
const workOf = (minutes: number, language: string): string => (minutes < 60 ? quantity(minutes, 'minute', language) : quantity(minutes / 60, 'hour', language));

/** A lead time in working hours: in minutes under an hour, in hours under a working day, else in working days (spec 14.5). */
function leadOf(hours: number, map: MapView, language: string): string {
  const workday = map.value_stream.work_time.workday_hours;
  if (hours < 1) return quantity(hours * 60, 'minute', language);
  return hours < workday ? quantity(hours, 'hour', language) : quantity(hours / workday, 'day', language);
}

/** Names or documents in a list of the language shown, three at most and the number of the others. */
function listOf(items: readonly string[], context: ScriptContext): string {
  const shown = items.length > 3 ? [...items.slice(0, 3), context.t('presenter.say.more', { count: items.length - 3 })] : [...items];
  return new Intl.ListFormat(context.language, { style: 'long', type: 'conjunction' }).format(shown);
}

const stepOf = (map: MapView, id: string): StepView | undefined => map.steps.find((step) => step.id === id);

/** A flow back: its target in a column before its source, as the routing draws a loop. */
const isBack = (map: MapView, flow: Schema<'FlowView'>): boolean =>
  (stepOf(map, flow.target_step_id)?.column ?? 0) < (stepOf(map, flow.source_step_id)?.column ?? 0);

/** The work its box shows already, in its picto of duration: the same figure to a tenth of a minute. */
const boxedWork = (step: StepView, work: number): boolean => Math.abs((step.sheet.duration_minutes ?? 0) - work) < 0.05;

/** The figures of a step: lead time by execution from the core, its work unless its box shows it, its value class, its documents, its pain points, its break of medium. */
function factsOf(step: StepView, context: ScriptContext): string[] {
  const { map, t, language } = context;
  const stream = map.value_stream.steps[step.id];
  const lead = stream?.lead_hours ?? null;
  // A step done by sessions: its work by case, the one its counter adds, differs from the one of a session its box shows.
  const perCase = stream?.work_per_case_minutes ?? null;
  const known = stream?.work_minutes ?? null;
  const sessions = known !== null && perCase !== null && workOf(perCase, language) !== workOf(known, language);
  const work = known !== null && !boxedWork(step, known) ? known : null;
  const lines: string[] = [];
  if (work !== null && lead !== null) lines.push(t('presenter.say.work_lead', { work: workOf(work, language), lead: leadOf(lead, map, language) }));
  else if (work !== null) lines.push(t('presenter.say.work', { work: workOf(work, language) }));
  else if (lead !== null) lines.push(t('presenter.say.lead', { lead: leadOf(lead, map, language) }));
  if (sessions) lines.push(t('presenter.say.per_case', { work: workOf(perCase, language) }));
  if (step.value_class !== undefined && step.value_class !== null) lines.push(t('presenter.say.value', { value: t(`menu.value.${step.value_class}`) }));
  // A document is said by its type and what it holds, never by its name as a file is named (ADR 101).
  const said = (direction: Schema<'DocumentDirection'>) => documentsSaid(step.markers.documents.filter((document) => document.direction === direction), t);
  if (said('input').length > 0) lines.push(t('presenter.say.inputs', { list: listOf(said('input'), context) }));
  if (said('output').length > 0) lines.push(t('presenter.say.outputs', { list: listOf(said('output'), context) }));
  const pains = step.markers.pain_points;
  for (const pain of pains.slice(0, 2)) lines.push(t('presenter.say.pain', { text: pain.description }));
  if (pains.length > 2) lines.push(t('presenter.say.more_pains', { count: pains.length - 2 }));
  const change = map.value_stream.media_breaks.steps.find((broken) => broken.step_id === step.id)?.changes[0];
  if (change !== undefined) lines.push(t('presenter.say.media', { from: t(`sheet.document.media.${change.from}`), to: t(`sheet.document.media.${change.to}`) }));
  return lines;
}

/** The exits of a step, the stronger share of the core first, then in the order of the map: the order they are said and numbered in. */
export function exitsOf(map: MapView, stepId: string): Schema<'FlowView'>[] {
  const share = (flowId: string) => map.value_stream.flows[flowId]?.share ?? -1;
  return map.flows.filter((flow) => flow.source_step_id === stepId).sort((a, b) => share(b.id) - share(a.id));
}

/**
 * A decision: its exits with their share of the cases from the core (the stronger first, three at most), then the one
 * the story takes from this stop; an exit without a share is read on its arrow. Shown again for its next exit (lot J10),
 * it says that exit only.
 */
function decisionOf(step: StepView, stop: number, context: ScriptContext): string[] {
  const { map, t, language, names } = context;
  const share = (flowId: string) => map.value_stream.flows[flowId]?.share ?? null;
  const exits = exitsOf(map, step.id);
  const lines: string[] = [];
  for (const exit of context.tour.stops[stop]?.again === true ? [] : exits.slice(0, 3)) {
    const label = names.flow(exit.id)?.text ?? '';
    const part = share(exit.id);
    if (label !== '' && part !== null) lines.push(t('presenter.say.decision_exit', { label, share: percentText(part, language) }));
  }
  return [...lines, ...takenOf(step, stop, context)];
}

/** The exit a decision or an inclusive gateway takes from this stop, said when it has several and this one is named on its arrow. */
function takenOf(step: StepView, stop: number, context: ScriptContext): string[] {
  const taken = context.tour.stops[stop]?.next?.flows[0];
  const flow = context.map.flows.find((candidate) => candidate.id === taken && candidate.source_step_id === step.id);
  const label = flow === undefined ? '' : context.names.flow(flow.id)?.text ?? '';
  return exitsOf(context.map, step.id).length > 1 && label !== '' ? [context.t('presenter.say.decision_followed', { label })] : [];
}

/** What a stop says by the type of its step, never its type the shape tells (a start, an end, a join, a wait); the facts of a step with content. */
function stepLines(step: StepView, stop: number, context: ScriptContext): string[] {
  const { map, t } = context;
  const forward = (flows: Schema<'FlowView'>[]) => flows.filter((flow) => !isBack(map, flow)).length;
  const exits = forward(map.flows.filter((flow) => flow.source_step_id === step.id));
  switch (step.type) {
    case 'start': {
      const volume = map.value_stream.steps[step.id]?.entry_volume ?? null;
      return volume === null ? [] : [t('presenter.say.start_volume', { value: casesText(volume, context.language) })];
    }
    case 'end':
      return [];
    case 'decision':
      return decisionOf(step, stop, context);
    // The rule of a gateway that splits, which its sign does not tell a room: all the branches, or some of them.
    case 'parallel':
      return exits > 1 ? [t('presenter.say.split', { count: exits })] : [];
    // Shown again for its next exit, it says that exit only, as a decision does (lot J10, independent review, M2).
    case 'inclusive':
      return [...(exits > 1 && context.tour.stops[stop]?.again !== true ? [t('presenter.say.inclusive')] : []), ...takenOf(step, stop, context)];
    default:
      // Several exits without a gateway: the core counts them as a parallel split, and every branch is told (lot J10).
      return [...(hasContent(step.type) ? factsOf(step, context) : []), ...(exits > 1 ? [t('presenter.say.split', { count: exits })] : [])];
  }
}

/**
 * What a passage says, flow after flow among those it lights, in their order (the exits of a split leave together, lot
 * J10): the comment of presentation of the flow in the language shown when it has one (lot J10, asked by the project
 * owner: a text of one's own on a handoff, as on a step), else the handoff the core counts there. The way back is seen
 * as the light goes back, what it carries flies beside the light.
 */
function passageLines(stop: number, context: ScriptContext): string[] {
  const { map, t, names } = context;
  const lit = context.tour.stops[stop]?.next?.flows ?? [];
  const laneOf = (stepId: string) => names.lane(names.step(stepId)?.laneId ?? '')?.name ?? '';
  const lines: string[] = [];
  for (const id of lit) {
    const typed = commentIn(id, context);
    const flow = typed === null && map.value_stream.handoffs.flows.includes(id) ? map.flows.find((candidate) => candidate.id === id) : undefined;
    const relay = flow === undefined ? null : t('presenter.say.relay', { from: laneOf(flow.source_step_id), to: laneOf(flow.target_step_id) });
    // Two flows lit together between the same two actors are one handoff said (independent review, M3).
    if (typed !== null) lines.push(...typed);
    else if (relay !== null && !lines.includes(relay)) lines.push(relay);
  }
  return lines;
}

/**
 * The opening: the steps told (each one once, a decision shown again counts for none), the entries of the map when the
 * tour tells several (lot J10), the actors met, the lead time end to end when the core knows it whole.
 */
function openingLines(context: ScriptContext): string[] {
  const { map, t, tour, language } = context;
  const told = new Set(tour.stops.map((stop) => stop.stepId));
  const actors = new Set([...told].map((id) => stepOf(map, id)?.lane_id));
  const entries = entriesOf(map).filter((id) => told.has(id)).length;
  const path = map.value_stream.main_path;
  return [
    t('presenter.say.opening_count', { count: told.size }), ...(entries > 1 ? [t('presenter.say.opening_entries', { count: entries })] : []),
    t('presenter.say.opening_actors', { count: actors.size }),
    ...(path?.lead_complete === true ? [t('presenter.say.opening_lead', { lead: leadOf(path.lead_hours, map, language) })] : []),
    t('presenter.say.opening_follow'),
  ];
}

/**
 * The lead time for the work: of the tour shown, the figures of its counters, when it is not the main path (a presentation
 * from a step, a branch chosen) and knows all its figures; of the main path end to end, when it knows them.
 */
function leadWorkLines(context: ScriptContext): string[] {
  const { map, tour, t, language } = context;
  const path = map.value_stream.main_path;
  const whole = path !== null && path.lead_complete && path.work_complete;
  const shown = countersAt(map, tour, tour.stops.length - 1);
  const figures = (lead: number, work: number) => ({ lead: leadOf(lead, map, language), work: workOf(work, language) });
  const said = (key: string, lead: number, work: number) => t(key, figures(lead, work));
  // The same figures as said: the tour is the main path, said once.
  const same = whole && JSON.stringify(figures(shown.leadHours, shown.workMinutes)) === JSON.stringify(figures(path.lead_hours, path.work_minutes));
  if (same) return [said('presenter.say.closing_lead_work', path.lead_hours, path.work_minutes)];
  return [
    ...(shown.complete ? [said('presenter.say.closing_tour_lead_work', shown.leadHours, shown.workMinutes)] : []),
    ...(whole ? [said('presenter.say.closing_path_lead_work', path.lead_hours, path.work_minutes)] : []),
  ];
}

/** The closing: the lead time for the work and the share of the work, the handoffs, the open pain points and the time they lose. */
function closingLines(context: ScriptContext): string[] {
  const { map, t, language } = context;
  const path = map.value_stream.main_path;
  const whole = path !== null && path.lead_complete && path.work_complete;
  const { totals, handoffs } = map.value_stream;
  return [
    t('presenter.say.closing_end'),
    ...leadWorkLines(context),
    ...(whole && path.work_ratio !== undefined && path.work_ratio !== null ? [t('presenter.say.closing_ratio', { share: percentText(path.work_ratio, language) })] : []),
    ...(handoffs.count > 0 ? [t('presenter.say.closing_relays', { count: handoffs.count })] : []),
    ...(totals.open_pain_points > 0 ? [t('presenter.say.closing_pains', { count: totals.open_pain_points })] : []),
    ...(totals.lost_hours_per_month > 0 ? [t('presenter.say.closing_lost', { lost: quantity(totals.lost_hours_per_month, 'hour', language) })] : []),
  ];
}

/** The lines of a comment of presentation as said: one a line written, the empty ones left out (ADR 099). */
export function commentLines(text: string): string[] {
  return text.split(/\r?\n/).map((line) => line.trim()).filter((line) => line !== '');
}

/**
 * The comment of presentation of a step in the language shown (ADR 099 revised), its original or its translation: what
 * the presenter wrote is said instead of the sentences of the step; without a line in that language, the sentences of
 * the step are said.
 */
function commentOf(step: StepView, context: ScriptContext): string[] | null {
  return commentIn(step.id, context);
}

/** The comment of a step or of a flow in the language shown, as said; null without a line in that language. */
function commentIn(subjectId: string, context: ScriptContext): string[] | null {
  const comment = context.comments?.[subjectId]?.find((line) => line.language === context.language);
  return comment === undefined ? null : commentLines(comment.text);
}

/** The sentences of a scene. */
export function scriptOf(scene: Scene, context: ScriptContext): Script {
  const said = (lines: readonly string[]): Script => ({ lines: lines.flatMap((line) => chunksOf(line)) });
  if (scene.kind === 'opening') return said(openingLines(context));
  if (scene.kind === 'closing') return said(closingLines(context));
  if (scene.kind === 'passage') return said(passageLines(scene.stop, context));
  const shown = context.tour.stops[scene.stop];
  const step = stepOf(context.map, shown?.stepId ?? '');
  // A decision shown again says the exit it now takes, not its comment a second time (lot J10).
  return said(step === undefined ? [] : (shown?.again === true ? null : commentOf(step, context)) ?? stepLines(step, scene.stop, context));
}
