// remainder.ts
// The remainder of a frame: its size, the steps it may be assigned to in two groups, the moment it is stored on its own, and what « Finish » did with each element (annex B 2.9).

import type { Schema } from '../../../api/client.ts';
import { laneRanks, readingOrder } from './readingOrder.ts';
import { hasContent } from './stepFacts.ts';

type FrameView = Schema<'FrameView'>;
type StepView = Schema<'StepView'>;

/** What the dissolved step still carries: documents, documents it used, pain points (B-2.9-21). */
export const remainderSize = (frame: Pick<FrameView, 'documents' | 'usages' | 'pain_points'>): number =>
  frame.documents.length + frame.usages.length + frame.pain_points.length;

/** The steps an element may go to: those of the frame, then the others. */
export interface Candidates {
  readonly inFrame: readonly StepView[];
  readonly others: readonly StepView[];
}

/** In the reading order (B-8.1-29; 1.x: its own sort), without start, end, parallel nor inclusive (B-2.9-29, B-2.9-30). */
export function assignCandidates(map: Pick<Schema<'MapView'>, 'steps' | 'lanes'>, frame: Pick<FrameView, 'frame_step_ids'>): Candidates {
  const members = new Set(frame.frame_step_ids);
  const steps = map.steps.filter((step) => hasContent(step.type)).sort(readingOrder(laneRanks(map.lanes)));
  return { inFrame: steps.filter((step) => members.has(step.id)), others: steps.filter((step) => !members.has(step.id)) };
}

/** One step or none left in the frame: what remains is stored on its own, once a dissolution (B-2.9-38). */
export const storedOnItsOwn = (frame: Pick<FrameView, 'frame_step_ids'>): boolean => frame.frame_step_ids.length <= 1;

/** What « Finish » did with an element: stored on a step, given to the map, deleted, or dropped as already received (B-2.9-37). */
export type ArrangementOutcome = 'to-step' | 'to-map' | 'deleted' | 'duplicate';

export function arrangementOutcome(arrangement: Pick<Schema<'Arrangement'>, 'kind' | 'step_id' | 'step_code'>): ArrangementOutcome {
  if (arrangement.kind === 'usage' && arrangement.step_id === null && arrangement.step_code !== null) return 'duplicate';
  if (arrangement.step_code !== null) return 'to-step';
  return arrangement.kind === 'pain_point' ? 'to-map' : 'deleted';
}
