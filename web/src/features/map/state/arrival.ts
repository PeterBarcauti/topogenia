// arrival.ts
// The object a map chooses and puts in view once it is shown: a mention of the control that leads to a detailed map names its step or its lane (F-64).

import type { Schema } from '../../../api/client.ts';

/** What a mention names on the map it leads to: a step, or a lane by its steps. */
export interface ArrivalSubject {
  readonly mapId: string;
  readonly stepId: string | null;
  readonly laneId: string | null;
}

let pending: ArrivalSubject | null = null;

/** Kept until the map named is shown; another mention replaces it, a mention that names nothing forgets it. */
export function focusOnArrival(subject: ArrivalSubject): void {
  pending = subject.stepId === null && subject.laneId === null ? null : subject;
}

/** The subject waiting for this map, given once and forgotten; null for any other map. */
export function takeArrival(mapId: string): ArrivalSubject | null {
  if (pending?.mapId !== mapId) return null;
  const subject = pending;
  pending = null;
  return subject;
}

/** The steps a subject names on its map: its step, or the steps of its lane; none when neither is there. */
export function stepsNamed(map: Schema<'MapView'>, subject: Pick<ArrivalSubject, 'stepId' | 'laneId'>): string[] {
  if (subject.stepId !== null) return map.steps.some((step) => step.id === subject.stepId) ? [subject.stepId] : [];
  return subject.laneId === null ? [] : map.steps.filter((step) => step.lane_id === subject.laneId).map((step) => step.id);
}
