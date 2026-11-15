// routing.ts
// Routes every flow of a map: sides, drawing, tracks, labels, paths; the same map always gives the same drawing (annex B 3.4, spec 9.7, S-3.5-03).

import { LOOP_DEPTH, loopDeeper, STEP_HEIGHT, STEP_WIDTH, STREAM_CARD, streamCardTop, UNDER_STEP } from '../constants.ts';
import type { Rect } from '../grid.ts';
import { placeLabels } from './labels.ts';
import { boxOf, contextOf, indexOf, isGateway, type Context, type Curve, type RoutedFlow, type RoutingFlow, type RoutingMap, type RoutingStep, type Side } from './model.ts';
import { curvePath, roundedPath } from './paths.ts';
import { draftOf, type Draft, type Load } from './routes.ts';
import { chosenDraft, passageHolds } from './chosen.ts';
import { baseSides, gatewaySides } from './sides.ts';
import { extentOf, placeOnTracks } from './tracks.ts';
import { underSides, type UnderSide } from './under.ts';

/** The space between the shape of a gateway and its label written under it (AR-57). */
const LABEL_GAP = 3;

const compare = (a: number | string, b: number | string): number => (a < b ? -1 : a > b ? 1 : 0);

/** The flows in the reading order of their source then of their target: the drawing does not depend on the order received. */
function readingOrder(context: Context, flows: readonly RoutingFlow[]): RoutingFlow[] {
  const key = (step: RoutingStep | undefined) => [step?.column ?? 0, step === undefined ? 0 : indexOf(context, step)] as const;
  return [...flows].sort((a, b) => {
    const [sa, sb, ta, tb] = [key(context.steps.get(a.sourceId)), key(context.steps.get(b.sourceId)), key(context.steps.get(a.targetId)), key(context.steps.get(b.targetId))];
    return compare(sa[0], sb[0]) || compare(sa[1], sb[1]) || compare(ta[0], tb[0]) || compare(ta[1], tb[1]) || compare(a.id, b.id);
  });
}

/** A flow without its route: its sides then given to the search, its passage left to the drawing. */
const sidesOnly = (flow: RoutingFlow): RoutingFlow => ({ id: flow.id, sourceId: flow.sourceId, targetId: flow.targetId, label: flow.label });

/**
 * The drawing of a flow with a route chosen by the user (ADR 091): the sides chosen, the others of the drawing; a departure
 * chosen on a point that another exit of its gateway chose first, or on its entry, is left to the drawing (AR-24). Without a passage and
 * with the sides of the drawing, the drawing as it is; else the path of the sides and of the passage chosen. A middle of
 * a column or of a row closed by steps all along keeps the sides chosen and leaves the passage to the drawing.
 */
function withChosenRoute(context: Context, flow: RoutingFlow, source: RoutingStep, target: RoutingStep, automatic: Draft, gateways: ReadonlyMap<string, Side>, load: Load): Draft {
  const route = flow.route ?? {};
  const chosen = route.source;
  const entry = chosen === 'left' && isGateway(source);
  const sourceSide = chosen !== undefined && !entry && (!gateways.has(flow.id) || gateways.get(flow.id) === chosen) ? chosen : automatic.sourceSide;
  const targetSide = route.target ?? automatic.targetSide;
  if (!passageHolds(context, route.via) && sourceSide === automatic.sourceSide && targetSide === automatic.targetSide) return automatic;
  const through = chosenDraft(context, flow, source, target, sourceSide, targetSide, load);
  if (through !== null) return through;
  if (sourceSide === automatic.sourceSide && targetSide === automatic.targetSide) return automatic;
  const sides = chosenDraft(context, sidesOnly(flow), source, target, sourceSide, targetSide, load);
  return sides === null ? automatic : { ...sides, flow };
}

/** The x of what hangs under a step, of a width: centred, or 10 px aside of the axis of its bottom point (AR-58, AR-128). */
function underX(box: Rect, side: UnderSide, width: number): number {
  const axis = box.x + STEP_WIDTH / 2;
  if (side === 'right') return axis + UNDER_STEP.shift;
  if (side === 'left') return axis - UNDER_STEP.shift - width;
  return axis - width / 2;
}

/**
 * The rectangles no label covers: the steps, and the label of each gateway under its shape (AR-57), centred, or aside of
 * the axis when a flow uses the bottom point (AR-58, AR-128); with the band of the value stream layer, the cartouche
 * under each step, centred, below the label of a gateway (spec 14.5).
 */
function obstaclesOf(context: Context, under: ReadonlyMap<string, UnderSide>): Rect[] {
  const obstacles: Rect[] = [];
  for (const step of context.steps.values()) {
    const box = boxOf(context, step);
    const side = under.get(step.id) ?? 'centre';
    obstacles.push(box);
    const label = isGateway(step) && step.below !== undefined ? step.below : null;
    if (label !== null)
      obstacles.push({ x: underX(box, side, label.width), y: box.y + STEP_HEIGHT + LABEL_GAP, width: label.width, height: label.height });
    if (context.grid.below > 0) {
      const top = streamCardTop(label === null ? null : label.height);
      obstacles.push({ x: underX(box, 'centre', STREAM_CARD.width), y: box.y + STEP_HEIGHT, width: STREAM_CARD.width, height: top + STREAM_CARD.height });
    }
  }
  return obstacles;
}

/** The curve of a loop under the steps of its line, deeper with the band under the steps (spec 14.5). */
const curveOf = (draft: Draft, below: number): Curve | null => {
  if (!draft.curve) return null;
  const [start, end] = [draft.points[0], draft.points.at(-1)];
  if (start === undefined || end === undefined) return null;
  return { start, end, low: Math.max(start.y, end.y) + LOOP_DEPTH + loopDeeper(below) };
};

/** The flows routed, and the side of what hangs under each step whose bottom point a flow uses (AR-128). */
export interface FlowsLayout {
  readonly flows: RoutedFlow[];
  readonly under: ReadonlyMap<string, UnderSide>;
}

/** The drawing of the flows of a map, and where what hangs under its steps goes so that no flow runs under it. */
export function layoutFlows(map: RoutingMap): FlowsLayout {
  const context = contextOf(map);
  const flows = readingOrder(context, map.flows).filter((flow) => context.steps.has(flow.sourceId) && context.steps.has(flow.targetId));
  const gateways = gatewaySides(context, flows);
  const anyRoute = flows.some((flow) => flow.route !== undefined);
  // The points the rules give the exits of a gateway, the choices of the user aside: the drawing of an exit whose point
  // the user chose starts from the point of the rules, which its shape supposes (ADR 091).
  const ruled = anyRoute
    ? gatewaySides(context, flows.map((flow) => (flow.route === undefined ? flow : { id: flow.id, sourceId: flow.sourceId, targetId: flow.targetId, label: flow.label })))
    : gateways;
  type Occupied = Map<string, [number, number][]>;
  const occupy = (occupied: Occupied, draft: Draft) => {
    draft.legs.forEach((leg, at) => {
      occupied.set(leg.channel, [...(occupied.get(leg.channel) ?? []), extentOf(draft, at, leg.horizontal, context.grid.below)]);
    });
  };
  const loadOf = (occupied: Occupied): Load => (channel, from, to) => (occupied.get(channel) ?? []).filter(([low, high]) => low < to && from < high).length;
  const stepsOf = (flow: RoutingFlow) => [context.steps.get(flow.sourceId) as RoutingStep, context.steps.get(flow.targetId) as RoutingStep] as const;
  // First the drawing of every flow, in the reading order, as if no route were chosen (ADR 091): a flow without a choice
  // is drawn as before, whatever its neighbours chose.
  const drawn: Occupied = new Map();
  const automatic = flows.map((flow) => {
    const [source, target] = stepsOf(flow);
    const pointChosen = flow.route?.source !== undefined && gateways.get(flow.id) === flow.route.source;
    const draft = draftOf(context, flow, source, target, (pointChosen ? ruled : gateways).get(flow.id) ?? baseSides(context, source, target).source, loadOf(drawn));
    occupy(drawn, draft);
    return draft;
  });
  // Then the flows with a route chosen, in the reading order: each takes its passage where the others leave the most room.
  // Without any, the drawing of 3.0.2 as it is, at its cost.
  const kept: Occupied = new Map();
  if (anyRoute) flows.forEach((flow, index) => {
    if (flow.route === undefined) occupy(kept, automatic[index] as Draft);
  });
  const drafts = !anyRoute ? automatic : flows.map((flow, index) => {
    const own = automatic[index] as Draft;
    if (flow.route === undefined) return own;
    const [source, target] = stepsOf(flow);
    const draft = withChosenRoute(context, flow, source, target, own, gateways, loadOf(kept));
    occupy(kept, draft);
    return draft;
  });
  placeOnTracks(drafts, context.boundaries, context.grid.below);
  const under = underSides(context, drafts.map((draft) => ({
    sourceId: draft.flow.sourceId, targetId: draft.flow.targetId, sourceSide: draft.sourceSide, targetSide: draft.targetSide, points: draft.points, isCurve: draft.curve,
  })));
  const labels = placeLabels(
    drafts.filter((draft) => draft.flow.label !== null)
      .map((draft) => ({ id: draft.flow.id, points: draft.points, curve: draft.curve, size: draft.flow.label ?? { width: 0, height: 0 }, below: context.grid.below })),
    obstaclesOf(context, under),
  );
  const routed = drafts.map((draft): RoutedFlow => {
    const curve = curveOf(draft, context.grid.below);
    return {
      id: draft.flow.id, sourceId: draft.flow.sourceId, targetId: draft.flow.targetId, kind: draft.kind,
      sourceSide: draft.sourceSide, targetSide: draft.targetSide, points: draft.points, curve,
      path: curve === null ? roundedPath(draft.points) : curvePath(curve), label: labels.get(draft.flow.id) ?? null,
    };
  });
  return { flows: routed, under };
}
