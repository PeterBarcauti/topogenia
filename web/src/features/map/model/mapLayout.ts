// mapLayout.ts
// The map as the canvas draws it: lane bands, steps in their boxes with what they show, flows routed with their labels, the frame of a dissolution (annex B 2.9 to 2.12, 3).

import type { Schema } from '../../../api/client.ts';
import { isWrittenIn, localizedName } from '../../../shared/i18n/localizedName.ts';
import { frameBox } from './frame.ts';
import { bandOf, UNDER_STEP, streamCardTop } from './geometry/constants.ts';
import { gridOf, mapWidth, stepBox, type Rect } from './geometry/grid.ts';
import { isGateway, type RoutedFlow, type RoutingFlow, type RoutingStep } from './geometry/routing/model.ts';
import { layoutFlows } from './geometry/routing/routing.ts';
import type { UnderSide } from './geometry/routing/under.ts';
import { concernedByRaci, missingApprover, raciBadges, type RaciBadge } from './raci.ts';
import { remainderSize } from './remainder.ts';
import { gaugeOf, isMacro, tintNumber, warningsByStep, type Gauge } from './stepFacts.ts';
import { edgeLabelSize, gatewayLabelSize, type TextMeasure } from './textSize.ts';
import { flowShareOf, laneStreamOf, streamFigureOf, type FlowShareText, type LaneStreamText, type StreamFigure, type StreamWriting } from './valueStream.ts';

type MapView = Schema<'MapView'>;
type StepView = Schema<'StepView'>;
type LaneView = Schema<'LaneView'>;

/** The bodies of the lanes alternate white and grey from the first one, odd (spec 9.3). */
export type Parity = 'odd' | 'even';

/** A lane: its band of rows from the top of the map, its tint (1 to 8), its name in the language shown (B-2.12-05). */
export interface LaneBand {
  readonly lane: LaneView;
  readonly name: string;
  /** Not written in the language shown: its name of origin, shown to translate (ADR 083). */
  readonly untranslated: boolean;
  readonly tint: number;
  readonly parity: Parity;
  readonly top: number;
  readonly rows: number;
  readonly height: number;
  /** Its headcount and workload under the value stream layer (spec 14.5), null without the layer or for software. */
  readonly stream: LaneStreamText | null;
}

/** The RACI of a step concerned: its badges and whether nobody approves it (B-2.11-05 to B-2.11-07). */
export interface StepRaci {
  readonly badges: readonly RaciBadge[];
  readonly missingApprover: boolean;
}

/** A step in its box, with its label in the language shown (B-3.2-19) and what it shows besides (annex B 2.9 to 2.11, spec 9.4). */
export interface StepFigure {
  readonly step: StepView;
  readonly box: Rect;
  readonly label: string;
  /** Not written in the language shown: its label of origin, shown to translate (ADR 083). */
  readonly untranslated: boolean;
  readonly tint: number;
  readonly parity: Parity;
  readonly warnings: readonly string[];
  readonly gauge: Gauge | null;
  readonly macro: boolean;
  /**
   * Where what hangs under the step goes (label of a gateway, frequency, RACI badges): centred, or aside of the axis of
   * its bottom point when a flow leaves or reaches it there (AR-58, AR-128).
   */
  readonly under: UnderSide;
  /** Covered by the frame of a dissolution without being one of its steps: greyed (B-2.9-19, B-3.2-17). */
  readonly outside: boolean;
  /** Its RACI, null for a step that has none (B-2.11-03). */
  readonly raci: StepRaci | null;
  /** How far under its box the RACI badges start: below the label of a gateway and the frequency of a detailed step. */
  readonly raciTop: number;
  /** What the value stream layer hangs under it (spec 14.5), null without the layer or for a gateway without content. */
  readonly stream: StreamFigure | null;
  /** How far under its box the cartouche of the layer hangs: under the label of a gateway. */
  readonly streamTop: number;
}

/** The frame of the dissolution open on the map, round its steps, with the size of its remainder (B-2.9-17, B-2.9-21). */
export interface FrameFigure {
  readonly frame: Schema<'FrameView'>;
  readonly box: Rect;
  readonly remainder: number;
}

/** A flow routed, its label in the language of its source step (B-3.4-37) and the documents it carries (B-3.4-38). */
export interface FlowFigure {
  readonly route: RoutedFlow;
  readonly text: string;
  /** A label not written in the language shown, shown to translate (ADR 083). */
  readonly untranslated: boolean;
  readonly documents: readonly string[];
  /** The share of the cases of an exit of a gateway under the value stream layer, after the label (spec 14.5). */
  readonly share: FlowShareText | null;
}

export interface MapLayout {
  readonly lanes: readonly LaneBand[];
  readonly steps: readonly StepFigure[];
  readonly flows: readonly FlowFigure[];
  readonly frame: FrameFigure | null;
  /** The size of the map, free columns included (B-3.1-19), in units of the canvas. */
  readonly width: number;
  readonly height: number;
  /** The value stream layer is on: each row has the band of its cartouches under its steps (spec 14.5). */
  readonly valueStream: boolean;
}

/** The text of the label of a flow measured and drawn: its label, then the share of its cases (« non · 15 % »). */
export const labelWithShare = (text: string, share: FlowShareText | null): string => (share === null ? text : text === '' ? share.text : `${text} \u00b7 ${share.text}`);

/** The name of a lane in the language shown, else the name entered (B-2.12-05). */
export const laneName = (lane: LaneView, language: string): string =>
  localizedName({ name: lane.name, source_language: lane.source_language, name_translations: lane.translations }, language);

/** The label of a step in the language shown, else the label entered (B-3.2-19). */
export const stepLabel = (step: StepView, language: string): string =>
  localizedName({ name: step.label, source_language: step.source_language, name_translations: step.translations }, language);

/** The label of a flow is written in the language of its source step: shown as it is in that language, else translated. */
export const flowText = (flow: Schema<'FlowView'>, source: StepView, language: string): string =>
  flow.label === null ? '' : localizedName({ name: flow.label, source_language: source.source_language, name_translations: flow.translations }, language);

const laneNamed = (lane: LaneView) => ({ name: lane.name, source_language: lane.source_language, name_translations: lane.translations });
const stepNamed = (step: StepView) => ({ name: step.label, source_language: step.source_language, name_translations: step.translations });

/** A labelled flow not written in a language: its label is in the language of its source step (A-2.14-06). */
function flowUntranslated(flow: Schema<'FlowView'>, source: StepView | undefined, language: string): boolean {
  return flow.label !== null && source !== undefined && !isWrittenIn({ name: flow.label, source_language: source.source_language, name_translations: flow.translations }, language);
}

/** The texts of a map not written in a language, its lanes, steps and labelled flows: what its banner counts (ADR 083). */
export function untranslatedOn(map: MapView, language: string): number {
  const steps = new Map(map.steps.map((step) => [step.id, step]));
  return map.lanes.filter((lane) => !isWrittenIn(laneNamed(lane), language)).length
    + map.steps.filter((step) => !isWrittenIn(stepNamed(step), language)).length
    + map.flows.filter((flow) => flowUntranslated(flow, steps.get(flow.source_step_id), language)).length;
}

/** A step as the routing reads it: a gateway with the size of its label written under its shape, which no label covers (AR-57). */
export function routingStepOf(step: StepView, label: string, measure: TextMeasure): RoutingStep {
  const below = isGateway(step) ? gatewayLabelSize(label, measure) : undefined;
  return { id: step.id, laneId: step.lane_id, column: step.column, row: step.row, type: step.type, ...(below === undefined ? {} : { below }) };
}

/** The RACI badges start under the label of a gateway and under the frequency of a detailed step, then a gap (B-2.11-05). */
function raciTopOf(frequency: boolean, below: RoutingStep['below']): number {
  const label = below === undefined ? 0 : UNDER_STEP.gatewayLabel + below.height;
  return Math.max(label, frequency ? UNDER_STEP.frequencyTop + UNDER_STEP.frequencyHeight : 0) + UNDER_STEP.gap;
}

/**
 * Lays the map out for the canvas, in the language shown, the label boxes measured by the measure given; with the value
 * stream layer, written as given, each row has the band of its cartouches and each exit of a gateway its share (spec 14.5).
 */
export function layoutMap(map: MapView, language: string, measure: TextMeasure, stream: StreamWriting | null = null): MapLayout {
  const below = bandOf(stream !== null);
  const grid = gridOf(map.lanes.map((lane) => ({ id: lane.id, order: lane.sort_order })), map.steps.map((step) => ({ laneId: step.lane_id, column: step.column, row: step.row })), below);
  const lanesById = new Map(map.lanes.map((lane) => [lane.id, lane]));
  const lanes = grid.lanes.map((id, index): LaneBand => {
    const lane = lanesById.get(id) as LaneView;
    const rows = grid.rows.get(id) ?? 1;
    return {
      lane, name: laneName(lane, language), untranslated: !isWrittenIn(laneNamed(lane), language), tint: tintNumber(lane.color),
      parity: index % 2 === 0 ? 'odd' : 'even', top: grid.top.get(id) ?? 0, rows, height: rows * grid.rowHeight,
      stream: stream === null ? null : laneStreamOf(map, id, stream.language),
    };
  });
  const bands = new Map(lanes.map((band) => [band.lane.id, band]));
  const steps = map.steps.filter((step) => bands.has(step.lane_id));
  const labels = new Map(steps.map((step) => [step.id, stepLabel(step, language)]));
  const routingSteps = steps.map((step) => routingStepOf(step, labels.get(step.id) ?? '', measure));
  const labelBelow = new Map(routingSteps.map((step) => [step.id, step.below]));
  const boxes = new Map(steps.map((step) => [step.id, stepBox(grid, { laneId: step.lane_id, column: step.column, row: step.row })]));
  const frame = frameBox(map.dissolution, boxes);
  const byId = new Map(steps.map((step) => [step.id, step]));
  const texts = new Map<string, string>();
  const shares = new Map<string, FlowShareText>();
  const untranslatedFlows = new Set<string>();
  const routingFlows: RoutingFlow[] = [];
  for (const flow of map.flows) {
    const source = byId.get(flow.source_step_id);
    if (source === undefined || !byId.has(flow.target_step_id)) continue;
    const text = flowText(flow, source, language);
    const share = stream === null ? null : flowShareOf(map, flow.id, stream.language);
    texts.set(flow.id, text);
    if (share !== null) shares.set(flow.id, share);
    if (flowUntranslated(flow, source, language)) untranslatedFlows.add(flow.id);
    routingFlows.push({ id: flow.id, sourceId: flow.source_step_id, targetId: flow.target_step_id, label: edgeLabelSize(labelWithShare(text, share), flow.documents.length, measure) });
  }
  const { flows: routes, under } = layoutFlows({ lanes: grid.lanes.map((id) => ({ id, order: grid.rank.get(id) ?? 0 })), steps: routingSteps, flows: routingFlows, below });
  const documents = new Map(map.flows.map((flow) => [flow.id, flow.documents]));
  const warnings = warningsByStep(map.warnings);
  return {
    lanes,
    steps: steps.map((step): StepFigure => {
      const band = bands.get(step.lane_id) as LaneBand;
      const macro = isMacro(step);
      return {
        step, box: boxes.get(step.id) as Rect, label: labels.get(step.id) ?? step.label, untranslated: !isWrittenIn(stepNamed(step), language),
        tint: band.tint, parity: band.parity, warnings: warnings.get(step.id) ?? [], gauge: gaugeOf(step.sheet), macro,
        under: under.get(step.id) ?? 'centre', outside: frame?.outside.has(step.id) ?? false,
        raci: concernedByRaci(step) ? { badges: raciBadges(map, step, (lane) => laneName(lane, language)), missingApprover: missingApprover(map, step) } : null,
        raciTop: raciTopOf(macro && step.sheet.frequency !== null, labelBelow.get(step.id)),
        stream: stream === null ? null : streamFigureOf(map, step, stream),
        streamTop: streamCardTop(isGateway(step) ? (labelBelow.get(step.id)?.height ?? null) : null),
      };
    }),
    flows: routes.map((route) => ({
      route, text: texts.get(route.id) ?? '', untranslated: untranslatedFlows.has(route.id), documents: documents.get(route.id) ?? [], share: shares.get(route.id) ?? null,
    })),
    frame: frame === null || map.dissolution === null ? null : { frame: map.dissolution, box: frame.box, remainder: remainderSize(map.dissolution) },
    width: mapWidth(steps),
    height: grid.height,
    valueStream: stream !== null,
  };
}
