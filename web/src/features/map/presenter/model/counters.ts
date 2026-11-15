// counters.ts
// The counters of a presentation (ADR 097, A1 of the study): the work by case and the lead time run up to a stop along the thread of the tour, read from the core, the segments of its main path where the tour follows it, the figures of a step elsewhere; a step of a branch told aside, and a decision shown again, add nothing (lot J10); their numbers written as the sentences say them.

import type { Schema } from '../../../../api/client.ts';
import type { Tour } from './tour.ts';

export interface Counters {
  readonly workMinutes: number;
  readonly leadHours: number;
  /** Every stop counted knows its work and its lead time. */
  readonly complete: boolean;
}

/**
 * The work and the lead time of the stops of the thread of the tour up to the one given (none before the first). The
 * work is the one by case, as the core counts its main path: a step done by sessions adds its share of a session, not
 * the session. A stop told aside (a branch, another entry, a step no flow reaches) and a decision shown again are left
 * out: a case does not go through every branch, and the rule that weighs them is the core's, not the presentation's.
 */
export function countersAt(map: Schema<'MapView'>, tour: Tour, stop: number): Counters {
  const segments = new Map((map.value_stream.main_path?.segments ?? []).map((segment) => [segment.step_id, segment]));
  let workMinutes = 0;
  let leadHours = 0;
  let complete = true;
  for (const { stepId, aside, again } of tour.stops.slice(0, Math.max(0, stop + 1))) {
    if (aside === true || again === true) continue;
    const segment = segments.get(stepId);
    const step = map.value_stream.steps[stepId];
    const work = (segment === undefined ? step?.work_per_case_minutes : segment.work_minutes) ?? null;
    const lead = (segment === undefined ? step?.lead_hours : segment.lead_hours) ?? null;
    workMinutes += work ?? 0;
    leadHours += lead ?? 0;
    complete &&= work !== null && lead !== null;
  }
  return { workMinutes, leadHours, complete };
}

/** A work in the unit it is written in, as the value stream and the sentences write it: minutes under an hour, else hours. */
export const workInUnit = (minutes: number): number => (minutes < 60 ? minutes : minutes / 60);

/** A lead time in working hours, in the unit it is written in: minutes under an hour, hours under a working day, else working days. */
export const leadInUnit = (hours: number, workdayHours: number): number => (hours < 1 ? hours * 60 : hours < workdayHours ? hours : hours / workdayHours);

/**
 * A number of a counter written as the sentences of the presentation say it (`quantity` of the script): without a
 * decimal from ten in its unit, with one under, so that the counter and the subtitle show the same figure (R6-08).
 */
export const saidNumber = (value: number, language: string): string => new Intl.NumberFormat(language, { maximumFractionDigits: value >= 10 ? 0 : 1 }).format(value);
