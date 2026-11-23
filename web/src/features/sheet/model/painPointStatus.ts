// painPointStatus.ts
// The statuses a list offers for a pain point: five of the seven of the format, in the order of the PDCA wheel, dropped apart, and the one it has when another (ADR 116).

import type { Schema } from '../../../api/client.ts';

/** A status of the format, one of the seven. */
export type StatusCode = NonNullable<Schema<'PainPointStatus'>>;

/**
 * The statuses offered, in the order of the PDCA wheel: reported (plan), in progress (do), implemented (check), adopted
 * (act); dropped last, out of the wheel. Read by the sheet and by the panel of the action plan.
 */
export const OFFERED_STATUSES: readonly StatusCode[] = ['reported', 'in_progress', 'implemented', 'adopted', 'dropped'];

/** The status drawn apart, after a separator: dropped is not a step of the wheel. */
export const STATUS_APART: StatusCode = 'dropped';

/**
 * The statuses a list shows for a pain point: the five offered, and the one it has when another (scored or under review,
 * from a package), where the seven place it, right after reported, so that nothing is lost by opening the list.
 */
export function statusChoices(current: Schema<'PainPointStatus'>): readonly StatusCode[] {
  if (current === null || OFFERED_STATUSES.includes(current)) return OFFERED_STATUSES;
  return ['reported', current, ...OFFERED_STATUSES.filter((status) => status !== 'reported')];
}
