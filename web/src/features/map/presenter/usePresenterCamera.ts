// usePresenterCamera.ts
// The camera of a presentation (ADR 097): at each scene, the view that frames what it shows, kept when it shows it already; the way drawn by React Flow, the smooth zoom of van Wijk and Nuij, timed by its length and the speed; a cut under prefers-reduced-motion. It moves when the scene changes, never because the stage is drawn again: a map moved by hand stays where it was put until the next scene.

import { useEffect, useMemo, useRef, useState } from 'react';
import { useReactFlow, useStore as useFlowStore } from '@xyflow/react';
import { useStore } from 'zustand';
import { LANE_HEADER_WIDTH } from '../model/geometry/constants.ts';
import type { Rect } from '../model/geometry/grid.ts';
import type { MapEditor } from '../state/editor.ts';
import { insideMap, keptOrFramed, travelMs, type Insets, type ZoomBounds } from './model/camera.ts';
import type { Presentation } from './usePresentation.ts';

/** The bands the stage covers: the counters above, the lanes on the left, the bar and a caption of two lines over it below. */
const PRESENTER_INSETS: Insets = { top: 100, right: 28, bottom: 224, left: 196 };

/** A step and its way framed close, never past a zoom that would make a small object jump; the whole map from afar. */
const CLOSE: ZoomBounds = { min: 0.35, max: 1.25 };
const WIDE: ZoomBounds = { min: 0.08, max: 1 };

/** Whether the person asked the system for less motion (WCAG 2.3.3): the camera cuts instead of travelling. */
export function useReducedMotion(): boolean {
  const query = typeof window.matchMedia === 'function' ? window.matchMedia('(prefers-reduced-motion: reduce)') : null;
  const [reduced, setReduced] = useState(query?.matches ?? false);
  useEffect(() => {
    if (query === null) return undefined;
    const changed = () => {
      setReduced(query.matches);
    };
    query.addEventListener('change', changed);
    return () => {
      query.removeEventListener('change', changed);
    };
  }, [query]);
  return reduced;
}

/** What the camera reads of a presentation: the parts that stay the same objects while the map, its layout and its tour do. */
type Framed = Pick<Presentation, 'scenes' | 'layout' | 'tour' | 'map' | 'traces'>;

/** The box of the trace of a flow, its loop under the steps included. */
function boxOfTrace(presentation: Framed, flowId: string): Rect[] {
  const points = presentation.traces.get(flowId)?.points ?? [];
  if (points.length === 0) return [];
  const xs = points.map((point) => point.x);
  const ys = points.map((point) => point.y);
  return [{ x: Math.min(...xs), y: Math.min(...ys), width: Math.max(...xs) - Math.min(...xs), height: Math.max(...ys) - Math.min(...ys) }];
}

/**
 * What a scene shows: the whole map at the opening and the closing; a stop with the flows that leave it and the steps
 * they lead to; a passage with its flows and their ends. The stop the story goes to next is framed only when a flow lit
 * leads to it: the tour jumps from the end of a branch to the next one and comes back to a decision from afar (lot J10),
 * and framing both ends of such a way pushed the step shown out of the screen (independent review, S2). A passage
 * without a flow, a jump, frames where it lands.
 */
export function boxesOf(presentation: Framed, scene: number): Rect[] {
  const current = presentation.scenes[scene];
  const boxOf = new Map(presentation.layout.steps.map((figure) => [figure.step.id, figure.box]));
  if (current === undefined || current.kind === 'opening' || current.kind === 'closing') return [...boxOf.values()];
  const stop = presentation.tour.stops[current.stop];
  if (stop === undefined) return [];
  const lit = stop.next?.flows ?? [];
  const flows = presentation.map.flows.filter((flow) => lit.includes(flow.id) && (current.kind === 'passage' || flow.source_step_id === stop.stepId));
  const ends = flows.flatMap((flow) => [flow.source_step_id, flow.target_step_id]);
  const steps = new Set(current.kind === 'step' ? [stop.stepId, ...ends] : flows.length === 0 ? [stop.next?.to ?? stop.stepId] : ends);
  return [...[...steps].flatMap((id) => { const box = boxOf.get(id); return box === undefined ? [] : [box]; }), ...flows.flatMap((flow) => boxOfTrace(presentation, flow.id))];
}

/**
 * Moves the camera at each scene, a new run of the presentation framed at once. Its effect hangs on the scene, the size of
 * the screen and the parts of the presentation that only change with the map, its layout or its tour, never on the
 * object of the presentation, made again at each drawing of the stage, nor on the speed, read when the camera leaves:
 * the stage drawn again (a move of the mouse, a sentence) started the travel again from where it was, and brought back
 * a map moved by hand in pause (R6-03, as the voice at the essai J2).
 */
export function usePresenterCamera(editor: MapEditor, presentation: Presentation): void {
  const flow = useReactFlow();
  const width = useFlowStore((state) => state.width);
  const height = useFlowStore((state) => state.height);
  const scene = useStore(editor.presenter, (state) => state.player.scene);
  const run = useStore(editor.presenter, (state) => state.run);
  const reduced = useReducedMotion();
  const framed = useRef(false);
  const { scenes, layout, tour, map, traces } = presentation;
  const shown = useMemo<Framed>(() => ({ scenes, layout, tour, map, traces }), [scenes, layout, tour, map, traces]);
  useEffect(() => {
    framed.current = false;
  }, [run]);
  useEffect(() => {
    if (width === 0 || height === 0) return;
    const current = shown.scenes[scene];
    const bounds = current === undefined || current.kind === 'opening' || current.kind === 'closing' ? WIDE : CLOSE;
    const now = flow.getViewport();
    const screen = { width, height };
    const boxes = boxesOf(shown, scene);
    const chosen = keptOrFramed(now, boxes, screen, PRESENTER_INSETS, bounds);
    if (chosen === null || chosen === now) return;
    // The map kept on the screen: no void past an edge of a map larger than the room (the editor bounds its view the same way);
    // the lanes are drawn from the margin of their headers, under the names on the left.
    const room = { x: -LANE_HEADER_WIDTH, y: 0, width: shown.layout.width + LANE_HEADER_WIDTH, height: shown.layout.height };
    const target = insideMap(chosen, room, boxes, screen, PRESENTER_INSETS);
    const { speed } = editor.presenter.getState().player;
    const duration = reduced || !framed.current ? 0 : Math.max(250, Math.round(travelMs(now, target, screen) / speed));
    framed.current = true;
    void flow.setViewport(target, { duration });
  }, [flow, editor, shown, scene, width, height, reduced, run]);
}
