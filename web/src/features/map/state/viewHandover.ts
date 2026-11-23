// viewHandover.ts
// The place of the view handed between the canvas of a map and its view in BPMN 2.0 (trial of the 3.3): the step nearest the middle of the screen, the point of the page where it stands and the zoom, so that a switch keeps the steps in sight at the same size; told by the view shown, read by the other view of the same map as it opens.

import { useEffect, type RefObject } from 'react';
import type { Point, Rect } from '../model/geometry/grid.ts';
import type { Extent, View } from '../model/geometry/viewport.ts';

/** The two views of a map: its canvas, and its diagram in BPMN 2.0. */
export type ViewKind = 'map' | 'bpmn';

/** Where a step stands on the screen: its code, the point of the page where its centre is, the zoom. */
export interface ViewAnchor {
  readonly code: string;
  readonly page: Point;
  readonly zoom: number;
}

/** A step as a view draws it: its code and its box, in the units of its drawing. */
export interface PlacedStep {
  readonly code: string;
  readonly box: Rect;
}

interface Told {
  readonly mapId: string;
  readonly from: ViewKind;
  readonly anchor: () => ViewAnchor | null;
}

let told: Told | null = null;

/** The view shown tells where it is, at each move; the last view told is the one a switch starts from. */
export function tellView(mapId: string, from: ViewKind, anchor: () => ViewAnchor | null): void {
  told = { mapId, from, anchor };
}

/**
 * Where the other view of this map stood, when it was the last one shown: a switch between the two views; null at the
 * opening of a map, which starts at its start (B-7-09), or after a view of another map.
 */
export function handedView(mapId: string, into: ViewKind): ViewAnchor | null {
  return told !== null && told.mapId === mapId && told.from !== into ? told.anchor() : null;
}

const centreOf = (box: Rect): Point => ({ x: box.x + box.width / 2, y: box.y + box.height / 2 });

/** The step whose centre is nearest the middle of a screen at a view, the top left of the screen at `origin` on the page. */
export function anchorOf(steps: readonly PlacedStep[], view: View, screen: Extent, origin: Point): ViewAnchor | null {
  if (screen.width <= 0 || screen.height <= 0) return null;
  const middle = { x: (screen.width / 2 - view.x) / view.zoom, y: (screen.height / 2 - view.y) / view.zoom };
  let nearest: { step: PlacedStep; distance: number } | null = null;
  for (const step of steps) {
    const centre = centreOf(step.box);
    const distance = Math.hypot(centre.x - middle.x, centre.y - middle.y);
    if (nearest === null || distance < nearest.distance) nearest = { step, distance };
  }
  if (nearest === null) return null;
  const centre = centreOf(nearest.step.box);
  return { code: nearest.step.code, page: { x: origin.x + view.x + centre.x * view.zoom, y: origin.y + view.y + centre.y * view.zoom }, zoom: view.zoom };
}

/** The view that puts a step where an anchor stood on the page, at its zoom, the top left of the screen at `origin`. */
export function viewAt(anchor: ViewAnchor, box: Rect, origin: Point): View {
  const centre = centreOf(box);
  return { x: anchor.page.x - origin.x - centre.x * anchor.zoom, y: anchor.page.y - origin.y - centre.y * anchor.zoom, zoom: anchor.zoom };
}

/** The view of a step handed by the other view of the map, placed on the screen of `root`; null without one. */
export function handedPlace(mapId: string, into: ViewKind, steps: readonly PlacedStep[], root: HTMLElement | null): View | null {
  const anchor = handedView(mapId, into);
  const step = anchor === null ? undefined : steps.find((candidate) => candidate.code === anchor.code);
  if (anchor === null || step === undefined || root === null) return null;
  const { left, top } = root.getBoundingClientRect();
  return viewAt(anchor, step.box, { x: left, y: top });
}

/** Tells where the view shown is at each move, its screen at `root` on the page. */
export function useTellView(mapId: string, from: ViewKind, root: RefObject<HTMLElement | null>, steps: readonly PlacedStep[], view: View, width: number, height: number): void {
  useEffect(() => {
    const element = root.current;
    if (element === null) return;
    const { left, top } = element.getBoundingClientRect();
    tellView(mapId, from, () => anchorOf(steps, view, { width, height }, { x: left, y: top }));
  }, [mapId, from, root, steps, view, width, height]);
}
