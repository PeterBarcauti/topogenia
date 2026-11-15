// sides.ts
// The sides a flow leaves and reaches, the detours, and the points of the gateways shared out among their exits (annex B 3.4 rules 1 to 4, AR-24).

import { columnFree, indexOf, isGateway, lineOf, lineTaken, type Context, type RoutingFlow, type RoutingStep, type Side } from './model.ts';

export interface Sides {
  readonly source: Side;
  readonly target: Side;
}

/**
 * The sides of a flow by the places of its steps (B-3.4-01 to B-3.4-04): same column on another line, a vertical (down:
 * bottom to top, up: top to bottom); a target on the left, a loop bottom to bottom; else right to left.
 */
export function baseSides(context: Context, source: RoutingStep, target: RoutingStep): Sides {
  if (target.column === source.column && lineOf(target) !== lineOf(source)) {
    return indexOf(context, target) > indexOf(context, source) ? { source: 'bottom', target: 'top' } : { source: 'top', target: 'bottom' };
  }
  if (target.column < source.column) return { source: 'bottom', target: 'bottom' };
  return { source: 'right', target: 'left' };
}

/**
 * A flow to the right goes round the steps in its way (B-3.4-15, B-3.4-16): on its own line when a step stands between,
 * between two lines when both are taken between the columns.
 */
export function needsDetour(context: Context, source: RoutingStep, target: RoutingStep): boolean {
  if (target.column <= source.column) return false;
  const sourceTaken = lineTaken(context, source, source.column, target.column);
  if (lineOf(source) === lineOf(target)) return sourceTaken;
  return sourceTaken && lineTaken(context, target, source.column, target.column);
}

/** « Voie libre » (B-3.4-12): the column of the gateway free up to the line of the target, and that line free up to it. */
const wayFree = (context: Context, source: RoutingStep, target: RoutingStep): boolean =>
  !lineTaken(context, target, source.column, target.column) && columnFree(context, source.column, source, target, true);

const byTargetColumn = (steps: ReadonlyMap<string, RoutingStep>) => (a: RoutingFlow, b: RoutingFlow): number =>
  (steps.get(a.targetId)?.column ?? 0) - (steps.get(b.targetId)?.column ?? 0);

/**
 * The point the rules give each exit of a gateway of two exits or more (B-3.4-05 to B-3.4-13), one point per exit while
 * there is one left among right, top and bottom (the left point is the entry): the imposed exits first (verticals and
 * loops), then the right point towards the nearest target of the same line without detour, then the others; a second
 * exit imposed on a point already taken, and any exit beyond the three points, leaves from the right point, on a track of
 * its own (AR-24: never a point shared, the bottom one included). The exits are given in their reading order; the points
 * free are the three, or those the choices of the user leave (ADR 091).
 */
function ruledExits(context: Context, source: RoutingStep, list: readonly RoutingFlow[], points: readonly Side[] = ['right', 'top', 'bottom']): Map<string, Side> {
  const sides = new Map<string, Side>();
  const free = new Set<Side>(points);
  const take = (flow: RoutingFlow, choices: readonly Side[]) => {
    const side = choices.find((choice) => free.has(choice)) ?? 'right';
    free.delete(side);
    sides.set(flow.id, side);
  };
  const target = (flow: RoutingFlow) => context.steps.get(flow.targetId) as RoutingStep;
  for (const flow of list) {
    const imposed = baseSides(context, source, target(flow)).source;
    if (imposed !== 'right') take(flow, [imposed]);
  }
  const rest = list.filter((flow) => !sides.has(flow.id)).sort(byTargetColumn(context.steps));
  for (const flow of rest.filter((item) => lineOf(target(item)) === lineOf(source) && !needsDetour(context, source, target(item)))) take(flow, ['right']);
  for (const flow of rest.filter((item) => !sides.has(item.id))) {
    const goal = target(flow);
    if (lineOf(goal) === lineOf(source)) take(flow, ['top', 'right']);
    else if (indexOf(context, goal) < indexOf(context, source)) take(flow, wayFree(context, source, goal) ? ['right', 'top'] : ['right']);
    else take(flow, wayFree(context, source, goal) ? ['right', 'bottom'] : ['right']);
  }
  return sides;
}

/**
 * The points chosen by the user on the exits of one gateway, over the points of the rules (ADR 091): one exit per point
 * in the reading order, never the entry. A point chosen that the rules already give moves no exit, so that the exits that
 * share the right point in the drawing still share it; the exits whose point another one chose take a point by the rules
 * among those left free (AR-24).
 */
function chosenExits(context: Context, source: RoutingStep, list: readonly RoutingFlow[], ruled: ReadonlyMap<string, Side>): Map<string, Side> {
  const won = new Map<string, Side>();
  for (const flow of list) {
    const side = flow.route?.source;
    if (side !== undefined && side !== 'left' && ![...won.values()].includes(side)) won.set(flow.id, side);
  }
  const sides = new Map<string, Side>(won);
  const takenBy = new Map<Side, string>([...won].filter(([id, side]) => ruled.get(id) !== side).map(([id, side]) => [side, id]));
  const displaced = list.filter((flow) => !won.has(flow.id) && takenBy.has(ruled.get(flow.id) ?? 'right'));
  for (const flow of list) if (!won.has(flow.id) && !displaced.includes(flow)) sides.set(flow.id, ruled.get(flow.id) ?? 'right');
  const used = new Set(sides.values());
  const free = (['right', 'top', 'bottom'] as const).filter((side) => !used.has(side));
  for (const [id, side] of ruledExits(context, source, displaced, free)) sides.set(id, side);
  return sides;
}

/**
 * The point each exit of a gateway of two exits or more leaves from: the points of the rules, then the points chosen by
 * the user over them (ADR 091). Without any choice, the drawing of 3.0.2. The exits are given in their reading order.
 */
export function gatewaySides(context: Context, flows: readonly RoutingFlow[]): Map<string, Side> {
  const sides = new Map<string, Side>();
  const exits = new Map<string, RoutingFlow[]>();
  for (const flow of flows) {
    const source = context.steps.get(flow.sourceId);
    if (source === undefined || !isGateway(source) || !context.steps.has(flow.targetId)) continue;
    exits.set(source.id, [...(exits.get(source.id) ?? []), flow]);
  }
  for (const [sourceId, list] of exits) {
    const source = context.steps.get(sourceId);
    if (list.length < 2 || source === undefined) continue;
    const ruled = ruledExits(context, source, list);
    const chosen = list.some((flow) => flow.route?.source !== undefined) ? chosenExits(context, source, list, ruled) : ruled;
    for (const [id, side] of chosen) sides.set(id, side);
  }
  return sides;
}
