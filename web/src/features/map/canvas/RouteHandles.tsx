// RouteHandles.tsx
// The handles of the route of a flow chosen alone, for who may change the map (ADR 091): a point at each end, dragged onto a side of its step, and a bar on each segment that may move, dragged into another aisle or band or onto the middle of a column or of a row; the flow drawn as it would be meanwhile, Escape giving up.

import { useReactFlow, ViewportPortal } from '@xyflow/react';
import { useEffect, useState, type PointerEvent } from 'react';
import { useTranslation } from 'react-i18next';
import type { Schema } from '../../../api/client.ts';
import { withPassage, withSide } from '../model/flowRoutes.ts';
import type { Point, Rect } from '../model/geometry/grid.ts';
import type { ChosenRoute, RoutedFlow, Side } from '../model/geometry/routing/model.ts';
import type { MapLayout } from '../model/mapLayout.ts';
import { allowedSides, drawnSidesOf, endsOf, nearestSide, passageNearX, passageNearY, segmentHandles, SIDES, sidePoints, type SegmentHandle } from '../model/routeGestures.ts';
import { previewRoute, routeShown, setFlowRoute, type RouteChange } from '../state/actions/flows.ts';
import { useEditor, useMapState } from '../state/EditorContext.tsx';
import type { MapEditor } from '../state/editor.ts';
import { capturing } from './useExport.ts';

type End = 'source' | 'target';

/** How far the pointer goes, in pixels of the screen, before a press on a handle becomes a gesture: a click writes nothing. */
export const DRAG_START = 4;

/** The handles of a flow: its two ends and its segments that may move. */
interface Handles {
  readonly ends: { readonly source: Point; readonly target: Point };
  readonly segments: readonly SegmentHandle[];
}

/**
 * A gesture under way: the handle held (an end, or a segment by its place), the handles as they were when it started, so
 * that the one held stays while the flow is drawn anew, the route before it, where the press was on the screen, whether
 * the pointer went far enough from it to move the route, and where the pointer is.
 */
interface Gesture {
  readonly held: End | number;
  readonly handles: Handles;
  readonly horizontal: boolean;
  readonly before: ChosenRoute | undefined;
  readonly origin: Point;
  readonly moved: boolean;
  readonly pointer: Point | null;
}

const at = (point: Point) => ({ transform: `translate(${String(point.x)}px, ${String(point.y)}px)` });

const unchanged: RouteChange = (route) => route;

function handlesOf(route: RoutedFlow): Handles | null {
  const ends = endsOf(route);
  return ends === null ? null : { ends, segments: segmentHandles(route) };
}

/**
 * The gestures of the handles of one flow: their start, the route each move gives, the write at the end. The route
 * drawn meanwhile is the one of the map store: Escape, a command of the map, takes it away, and the gesture then ends
 * without writing anything. A press that does not move, another button than the main one, a gesture cancelled by the
 * browser, or the handles gone (another choice) write nothing either.
 */
function useRouteGesture(editor: MapEditor, map: Schema<'MapView'>, layout: MapLayout, flowId: string) {
  const { screenToFlowPosition } = useReactFlow();
  const [gesture, setGesture] = useState<Gesture | null>(null);
  const active = gesture !== null;
  useEffect(() => {
    if (!active) return undefined;
    return () => {
      const { routeDraft } = editor.store.getState();
      if (routeDraft?.flowId === flowId && !routeDraft.sent) previewRoute(editor, null);
    };
  }, [active, editor, flowId]);
  const boxOf = (end: End): Rect | null => {
    const flow = map.flows.find((item) => item.id === flowId);
    const stepId = end === 'source' ? flow?.source_step_id : flow?.target_step_id;
    return layout.steps.find((figure) => figure.step.id === stepId)?.box ?? null;
  };
  /** The change a gesture gives with the pointer there: its side, or its passage; none when nothing is near. */
  const changeAt = (current: Gesture, pointer: Point): RouteChange => {
    if (typeof current.held !== 'number') {
      const [end, box] = [current.held, boxOf(current.held)];
      const side = box === null ? null : nearestSide(box, pointer, allowedSides(map, flowId, end, drawnSidesOf(layout.flows)));
      return side === null ? unchanged : (route) => withSide(route, end, side);
    }
    const lastColumn = layout.steps.reduce((last, figure) => Math.max(last, figure.step.column), -1);
    const passage = current.horizontal ? passageNearY(layout.lanes, pointer.y) : passageNearX(pointer.x, lastColumn);
    return passage === null ? unchanged : (route) => withPassage(route, passage);
  };
  /** The gesture still running: none once Escape took its route away. */
  const running = (): Gesture | null => {
    if (gesture === null || editor.store.getState().routeDraft?.flowId === flowId) return gesture;
    setGesture(null);
    return null;
  };
  const start = (event: PointerEvent<HTMLElement>, handles: Handles, held: End | number, horizontal: boolean) => {
    if (event.button !== 0) return;
    event.stopPropagation();
    event.preventDefault();
    event.currentTarget.setPointerCapture(event.pointerId);
    const before = routeShown(editor, flowId);
    previewRoute(editor, flowId, before);
    setGesture({ held, handles, horizontal, before, origin: { x: event.clientX, y: event.clientY }, moved: false, pointer: null });
  };
  const move = (event: PointerEvent<HTMLElement>) => {
    const current = running();
    if (current === null) return;
    if (!current.moved && Math.hypot(event.clientX - current.origin.x, event.clientY - current.origin.y) < DRAG_START) return;
    const pointer = screenToFlowPosition({ x: event.clientX, y: event.clientY });
    setGesture({ ...current, moved: true, pointer });
    previewRoute(editor, flowId, changeAt(current, pointer)(current.before));
  };
  const finish = (event: PointerEvent<HTMLElement>) => {
    event.currentTarget.releasePointerCapture(event.pointerId);
    const current = running();
    if (current === null) return;
    setGesture(null);
    if (current.moved) void setFlowRoute(editor, flowId, changeAt(current, screenToFlowPosition({ x: event.clientX, y: event.clientY })));
  };
  const cancel = (event: PointerEvent<HTMLElement>) => {
    event.currentTarget.releasePointerCapture(event.pointerId);
    setGesture(null);
  };
  return { gesture, start, move, finish, cancel, boxOf };
}

/** The handles of the flow chosen alone; nothing for a reader, in the link mode, during an export, or with steps chosen too. */
export function RouteHandles({ map, layout }: { readonly map: Schema<'MapView'>; readonly layout: MapLayout }) {
  const selection = useMapState((state) => state.selection);
  const exporting = useMapState(capturing);
  const mode = useMapState((state) => state.mode);
  const flowId = selection.flows.length === 1 && selection.steps.length === 0 ? selection.flows[0] : undefined;
  if (!map.rights.structure || exporting || mode !== 'normal' || flowId === undefined) return null;
  return <FlowHandles map={map} layout={layout} flowId={flowId} />;
}

/** The points of the step of the end held, a point another exit of its gateway chose greyed. */
function SidePoints({ box, allowed }: { readonly box: Rect; readonly allowed: readonly Side[] }) {
  return (
    <>
      {Object.entries(sidePoints(box)).map(([side, point]) => (
        <span key={side} className={allowed.includes(side as Side) ? 'route-point' : 'route-point is-taken'} style={at(point)} data-testid={`route-point-${side}`} />
      ))}
    </>
  );
}

function FlowHandles({ map, layout, flowId }: { readonly map: Schema<'MapView'>; readonly layout: MapLayout; readonly flowId: string }) {
  const { t } = useTranslation();
  const editor = useEditor();
  const { gesture, start, move, finish, cancel, boxOf } = useRouteGesture(editor, map, layout, flowId);
  const figure = layout.flows.find((item) => item.route.id === flowId);
  const drawn = figure === undefined ? null : handlesOf(figure.route);
  const handles = gesture?.handles ?? drawn;
  if (handles === null) return null;
  const held = gesture?.held;
  const box = held === undefined || typeof held === 'number' ? null : boxOf(held);
  const allowed = held === undefined || typeof held === 'number' ? SIDES : allowedSides(map, flowId, held, drawnSidesOf(layout.flows));
  // A held end snaps to the point of its step it would take; out of reach it stays on its point, where a release changes
  // nothing: it never seems to leave for another step (audit 3.1.0 3.7, F-81). A held segment follows the pointer.
  const endAt = (pointer: Point, point: Point): Point => {
    const side = box === null ? null : nearestSide(box, pointer, allowed);
    return box !== null && side !== null ? sidePoints(box)[side] : point;
  };
  const place = (handle: End | number, point: Point) =>
    at(held !== handle || !gesture?.pointer ? point : typeof handle === 'number' ? gesture.pointer : endAt(gesture.pointer, point));
  const handlers = { onPointerMove: move, onPointerUp: finish, onPointerCancel: cancel };
  return (
    <ViewportPortal>
      <div className="route-handles nodrag nopan" data-testid="route-handles">
        {box !== null && <SidePoints box={box} allowed={allowed} />}
        {(['source', 'target'] as const).map((end) => (
          <button key={end} type="button" className="route-end nodrag nopan" style={place(end, handles.ends[end])} aria-label={t(end === 'source' ? 'map.route_departure' : 'map.route_arrival')}
            title={t('map.route_end_help')} data-testid={`route-end-${end}`} data-flow-id={flowId} tabIndex={-1} onPointerDown={(event) => { start(event, handles, end, false); }} {...handlers} />
        ))}
        {handles.segments.map((segment) => (
          <button key={segment.index} type="button" className={segment.horizontal ? 'route-segment is-horizontal nodrag nopan' : 'route-segment is-vertical nodrag nopan'}
            style={place(segment.index, segment.middle)} aria-label={t('map.route_passage')} title={t('map.route_passage_help')} data-testid={`route-segment-${String(segment.index)}`} data-flow-id={flowId} tabIndex={-1}
            onPointerDown={(event) => { start(event, handles, segment.index, segment.horizontal); }} {...handlers} />
        ))}
      </div>
    </ViewportPortal>
  );
}
