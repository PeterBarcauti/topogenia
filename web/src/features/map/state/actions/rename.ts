// rename.ts
// A name typed on the map is written in the language the map is shown in (ADR 083): the name itself in its own language, else its translation into the language shown; the version of origin never changes from another language.

import type { Schema } from '../../../../api/client.ts';
import { typedFrom, type NamedObject } from '../../../../shared/i18n/localizedName.ts';
import type { MapEditor } from '../editor.ts';
import { relabelFlow } from './flows.ts';
import { updateLane } from './lanes.ts';
import { updateStep } from './steps.ts';
import { translateObject } from './translations.ts';

type MapView = Schema<'MapView'>;

/** The language a name of the map is typed in: the language shown, the language of the name itself before the screen tells it. */
const typedIn = (editor: MapEditor, own: string): string => editor.store.getState().shown?.language ?? own;

const stepNamed = (step: Schema<'StepView'>): NamedObject => ({ name: step.label, source_language: step.source_language, name_translations: step.translations });
const laneNamed = (lane: Schema<'LaneView'>): NamedObject => ({ name: lane.name, source_language: lane.source_language, name_translations: lane.translations });

/** A labelled flow as a name: its label is in the language of its source step (A-2.14-06); null without a label nor its source. */
function flowNamed(map: MapView, flowId: string): NamedObject | null {
  const flow = map.flows.find((candidate) => candidate.id === flowId);
  const source = map.steps.find((step) => step.id === flow?.source_step_id);
  return flow === undefined || source === undefined ? null : { name: flow.label ?? '', source_language: source.source_language, name_translations: flow.translations };
}

/** The text the name of a step is typed from, in the language shown. */
export const stepTyped = (editor: MapEditor, step: Schema<'StepView'>): string => typedFrom(stepNamed(step), typedIn(editor, step.source_language));

/** The text the name of a lane is typed from, in the language shown. */
export const laneTyped = (editor: MapEditor, lane: Schema<'LaneView'>): string => typedFrom(laneNamed(lane), typedIn(editor, lane.source_language));

/** The text the label of a flow is typed from, in the language shown. */
export function flowTyped(editor: MapEditor, map: MapView, flowId: string): string {
  const named = flowNamed(map, flowId);
  return named === null ? '' : typedFrom(named, typedIn(editor, named.source_language));
}

/** The name of a step typed: its label in its own language, else its translation into the language shown. */
export async function renameStep(editor: MapEditor, step: Schema<'StepView'>, text: string): Promise<void> {
  const language = typedIn(editor, step.source_language);
  if (language === step.source_language) await updateStep(editor, step.id, { label: text });
  else await translateObject(editor, { kind: 'step', id: step.id }, { [language]: text });
}

/** The name of a lane typed: its name in its own language, else its translation into the language shown. */
export async function renameLane(editor: MapEditor, lane: Schema<'LaneView'>, text: string): Promise<void> {
  const language = typedIn(editor, lane.source_language);
  if (language === lane.source_language) await updateLane(editor, lane.id, { name: text });
  else await translateObject(editor, { kind: 'lane', id: lane.id }, { [language]: text });
}

/**
 * The label of a flow typed: its label in the language of its source step, else its translation into the language shown
 * (a flow without a label has nothing to translate: its label is then written as it is).
 */
export async function relabelFlowIn(editor: MapEditor, flowId: string, text: string): Promise<void> {
  const map = editor.store.getState().map;
  const named = map === null ? null : flowNamed(map, flowId);
  const language = named === null ? null : typedIn(editor, named.source_language);
  if (named === null || language === named.source_language || named.name === '') await relabelFlow(editor, flowId, text);
  else await translateObject(editor, { kind: 'flow', id: flowId }, { [language ?? named.source_language]: text });
}
