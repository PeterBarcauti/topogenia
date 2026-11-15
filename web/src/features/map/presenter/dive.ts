// dive.ts
// The dive of a presentation into a detailed step (F-120, spec of the 10/11/2026): the step shown first, as a click shows it, then the map of its detail read without leaving the editor and presented from its opening; a presentation left or moved meanwhile takes nothing; a map that cannot be read is said.

import { messageOf } from '../../../shared/i18n/errorMessage.ts';
import { isMacro } from '../model/stepFacts.ts';
import type { MapEditor } from '../state/editor.ts';
import type { PresenterState } from './presenterStore.ts';
import type { Presentation } from './usePresentation.ts';

/**
 * Shows a step clicked: its stop when the story passes there, else the story told again from it (audit 3.2, BUG-04).
 * A decision has a stop for each of its exits (lot J10): the one shown is kept when it is a stop of that step, so that
 * a detail opened from a decision shown again comes back to it, not to its first stop (independent review, M1).
 */
export function showStep(editor: MapEditor, presentation: Presentation, stepId: string): void {
  const presenter = editor.presenter.getState();
  const shown = presentation.scenes[presenter.player.scene];
  const here = shown?.kind === 'step' && presentation.tour.stops[shown.stop]?.stepId === stepId ? shown.stop : -1;
  const stop = here >= 0 ? here : presentation.tour.stops.findIndex((candidate) => candidate.stepId === stepId);
  if (stop >= 0) presenter.move({ stop }, presentation.scenes);
  else presenter.jump(stepId);
}

/** The detailed step a scene shows, null for another scene or a step without detail: what « Voir le détail » and D open. */
export function detailedAt(presentation: Presentation, scene: number): string | null {
  const shown = presentation.scenes[scene];
  if (shown?.kind !== 'step') return null;
  const stepId = presentation.tour.stops[shown.stop]?.stepId;
  const step = presentation.map.steps.find((candidate) => candidate.id === stepId);
  return step !== undefined && isMacro(step) ? step.id : null;
}

/**
 * Whether the presentation still shows the step it asked the detail of: still on, in the same run (a start, a dive and
 * a way back each begin a new one), its story told from the same step, at the same scene. Left, started again or moved
 * meanwhile, by the presenter or by its clock, it takes nothing of a map that comes late: the way back from a detail
 * is then always on the step dived into (R6-10).
 */
const unmoved = (asked: PresenterState, now: PresenterState): boolean =>
  now.active && now.run === asked.run && now.from === asked.from && now.player.scene === asked.player.scene;

/** Presents the map of the detail of a step: the step shown at once, its map once read; nothing for a step without detail. */
export function diveInto(editor: MapEditor, presentation: Presentation, stepId: string): void {
  const step = presentation.map.steps.find((candidate) => candidate.id === stepId);
  if (step === undefined || !isMacro(step) || step.sub_map_id === null) return;
  showStep(editor, presentation, stepId);
  const before = editor.presenter.getState();
  editor.read(step.sub_map_id, new AbortController().signal).then((reply) => {
    const now = editor.presenter.getState();
    if (unmoved(before, now)) now.dive(stepId, reply.map);
  }, (failure: unknown) => {
    // Said in the stage, where the full screen shows it, to the presentation that asked for it.
    const now = editor.presenter.getState();
    if (now.active && now.run === before.run) now.say(messageOf(failure));
  });
}
