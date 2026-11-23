// usePresentation.ts
// The presentation of the map shown (ADR 097): the map laid out in the language shown, its tour from the step chosen, its scenes and what each says in that language (the comments of presentation of its steps included, ADR 099; nothing for the steps, or for the ways between them, while the presenter turned their messages off, lot J10), their times, the traces of the flows.

import { useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import { useStore } from 'zustand';
import type { Schema } from '../../../api/client.ts';
import type { Translate } from '../../../shared/i18n/i18n.ts';
import { useShownTexts } from '../state/shownTexts.ts';
import { useTextMeasure } from '../canvas/useTextMeasure.ts';
import type { LaneRows } from '../model/gridShape.ts';
import { layoutMap, type MapLayout } from '../model/mapLayout.ts';
import { useEditor, useMapState } from '../state/EditorContext.tsx';
import { scenesOf, scriptOf, type Names, type Scene, type Script } from './model/script.ts';
import { sceneMs } from './model/timing.ts';
import { tourOf, type Tour } from './model/tour.ts';
import { traceOf, type Trace } from './model/route.ts';

export interface Presentation {
  readonly map: Schema<'MapView'>;
  readonly layout: MapLayout;
  readonly tour: Tour;
  readonly scenes: readonly Scene[];
  readonly scripts: readonly Script[];
  readonly durations: readonly number[];
  /** The sentences of each scene, for the presenter who moves by them. */
  readonly lines: readonly (readonly string[])[];
  readonly traces: ReadonlyMap<string, Trace>;
  /** The language the map is shown in, which the presentation speaks, and its texts. */
  readonly language: string;
  readonly t: Translate;
}

/** The names of the map as the layout writes them, in the language shown. */
function namesOf(layout: MapLayout): Names {
  const steps = new Map(layout.steps.map((figure) => [figure.step.id, { label: figure.label, laneId: figure.step.lane_id, code: figure.step.code }]));
  const lanes = new Map(layout.lanes.map((band) => [band.lane.id, { name: band.name, tint: band.tint }]));
  const flows = new Map(layout.flows.map((flow) => [flow.route.id, { text: flow.text, documents: flow.documents }]));
  return { step: (id) => steps.get(id) ?? null, lane: (id) => lanes.get(id) ?? null, flow: (id) => flows.get(id) ?? null };
}

/** No rows kept for a lane: a detail dived into (F-120). */
const NO_ROWS: LaneRows = {};

/** The presentation of the map, made again when the map, the language or the step chosen changes. */
export function usePresentation(map: Schema<'MapView'>): Presentation {
  const editor = useEditor();
  const { i18n } = useTranslation();
  const language = useMapState((state) => state.shown?.language ?? i18n.language);
  // The rows a lane keeps at least are those of the map of the editor: a detail dived into has none (F-120).
  const own = useMapState((state) => state.map?.id === map.id);
  const kept = useMapState((state) => state.rowsAtLeast);
  const rows = own ? kept : NO_ROWS;
  const from = useStore(editor.presenter, (state) => state.from);
  const comments = useStore(editor.presenter, (state) => state.comments[map.id]);
  // The messages of the steps and those of the ways between them, each said or not: the presenter may speak in their place (lot J10).
  const stepMessages = useStore(editor.preferences, (state) => state.presenterStepMessages);
  const flowMessages = useStore(editor.preferences, (state) => state.presenterFlowMessages);
  const measure = useTextMeasure();
  const t = useShownTexts(language);
  const layout = useMemo(() => layoutMap(map, language, measure, null, rows), [map, language, measure, rows]);
  const tour = useMemo(() => tourOf(map, from), [map, from]);
  const scenes = useMemo(() => scenesOf(tour), [tour]);
  const scripts = useMemo(() => {
    const context = { map, tour, names: namesOf(layout), t, language, ...(comments === undefined ? {} : { comments }) };
    const silent = (scene: Scene): boolean => (scene.kind === 'step' && !stepMessages) || (scene.kind === 'passage' && !flowMessages);
    return scenes.map((scene): Script => (silent(scene) ? { lines: [] } : scriptOf(scene, context)));
  }, [map, tour, layout, t, language, scenes, comments, stepMessages, flowMessages]);
  const traces = useMemo(() => new Map(layout.flows.map((flow) => [flow.route.id, traceOf(flow.route)])), [layout]);
  const durations = useMemo(() => scenes.map((scene, index) => {
    // The flows of a passage fly together: its time is the one of the longest (lot J10, independent review, M6).
    const lit = scene.kind === 'passage' ? tour.stops[scene.stop]?.next?.flows ?? [] : [];
    return sceneMs(scene, scripts[index]?.lines ?? [], Math.max(0, ...lit.map((flow) => traces.get(flow)?.total ?? 0)));
  }), [scenes, scripts, tour, traces]);
  const lines = useMemo(() => scripts.map((script) => script.lines), [scripts]);
  return { map, layout, tour, scenes, scripts, durations, lines, traces, language, t };
}
