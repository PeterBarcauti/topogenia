// routing.ts
// Routes every flow of a map: sides, drawing, tracks, labels, paths; the same map always gives the same drawing (annex B 3.4, spec 9.7, S-3.5-03).

import { LOOP_DEPTH, loopDeeper, STEP_HEIGHT, STEP_WIDTH, STREAM_CARD, streamCardTop, UNDER_STEP } from '../constants.ts';
import type { Rect } from '../grid.ts';
import { placeLabels } from './labels.ts';
import { boxOf, contextOf, indexOf, isGateway, type Context, type Curve, type RoutedFlow, type RoutingFlow, type RoutingMap, type RoutingStep } from './model.ts';
import { curvePath, roundedPath } from './paths.ts';
import { draftOf, type Draft, type Load } from './routes.ts';
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
  const occupied = new Map<string, [number, number][]>();
  const load: Load = (channel, from, to) => (occupied.get(channel) ?? []).filter(([low, high]) => low < to && from < high).length;
  const drafts = flows.map((flow) => {
    const [source, target] = [context.steps.get(flow.sourceId) as RoutingStep, context.steps.get(flow.targetId) as RoutingStep];
    const draft = draftOf(context, flow, source, target, gateways.get(flow.id) ?? baseSides(context, source, target).source, load);
    draft.legs.forEach((leg, index) => {
      occupied.set(leg.channel, [...(occupied.get(leg.channel) ?? []), extentOf(draft, index, leg.horizontal, context.grid.below)]);
    });
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
