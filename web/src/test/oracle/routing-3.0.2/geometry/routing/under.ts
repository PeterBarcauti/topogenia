// under.ts
// The side of its axis where what hangs under a step goes: away from the flows that leave or reach it by its bottom (AR-58, AR-128).

import { FREE_BAND, STEP_HEIGHT, UNDER_STEP } from '../constants.ts';
import type { Point } from '../grid.ts';
import { boxOf, type Context, type RoutedFlow } from './model.ts';

/**
 * Centred under the step when nothing uses its bottom point; else left aligned 10 px right of the axis, or right aligned
 * 10 px left of it when the flows of its bottom run to the right in the free band under it.
 */
export type UnderSide = 'centre' | 'left' | 'right';

/** What the side reads of a flow routed: its steps, its sides, its points (the two ends of a curve). */
export type BottomFlow = Pick<RoutedFlow, 'sourceId' | 'targetId' | 'sourceSide' | 'targetSide' | 'points'> & { readonly isCurve: boolean };

/**
 * The way a flow goes from the bottom point of a step, the points given from that point: for a curve, towards its other
 * end; else as far as it runs sideways while it stays in the free band under the step (a jog onto a track is not a way).
 * 1 to the right, -1 to the left, 0 straight down (or the free band left at once).
 */
function wayFrom(points: readonly Point[], isCurve: boolean, bandBottom: number): number {
  const [port, ...rest] = points;
  if (port === undefined) return 0;
  if (isCurve) return Math.sign((rest.at(-1)?.x ?? port.x) - port.x);
  let reach = 0;
  for (const point of rest) {
    if (point.y > bandBottom) break;
    if (Math.abs(point.x - port.x) > Math.abs(reach)) reach = point.x - port.x;
  }
  return Math.abs(reach) < UNDER_STEP.shift ? 0 : Math.sign(reach);
}

/** The side of each step whose bottom point a flow uses; a step absent from the map is centred. */
export function underSides(context: Context, flows: readonly BottomFlow[]): Map<string, UnderSide> {
  const ways = new Map<string, number[]>();
  const add = (stepId: string, points: readonly Point[], isCurve: boolean) => {
    const step = context.steps.get(stepId);
    if (step === undefined) return;
    const box = boxOf(context, step);
    ways.set(stepId, [...(ways.get(stepId) ?? []), wayFrom(points, isCurve, box.y + STEP_HEIGHT + FREE_BAND + context.grid.below)]);
  };
  for (const flow of flows) {
    if (flow.sourceSide === 'bottom') add(flow.sourceId, flow.points, flow.isCurve);
    if (flow.targetSide === 'bottom') add(flow.targetId, [...flow.points].reverse(), flow.isCurve);
  }
  const sides = new Map<string, UnderSide>();
  // Right of the axis, as AR-58 did for the label of a gateway, unless every flow that runs sideways runs to the right.
  for (const [stepId, list] of ways) sides.set(stepId, list.some((way) => way > 0) && !list.some((way) => way < 0) ? 'left' : 'right');
  return sides;
}
