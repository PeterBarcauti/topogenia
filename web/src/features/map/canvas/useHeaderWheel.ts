// useHeaderWheel.ts
// The wheel on the fixed lane headers, which lie over the canvas and out of React Flow: the same gestures as on the canvas, the wheel scrolls, Shift and the wheel go sideways, Ctrl and the wheel zoom on the pointer (spec 3.0 section 15.1, ADR 086).

import { useEffect, type RefObject } from 'react';
import { MAX_ZOOM, MIN_ZOOM } from '../model/geometry/constants.ts';
import type { View } from '../model/geometry/viewport.ts';

/** What the view needs of a wheel event: its deltas and their unit, its keys, the pointer from the top left of the canvas. */
export interface WheelTurn {
  readonly deltaX: number;
  readonly deltaY: number;
  /** 0 pixels, 1 lines, 2 pages, as the browser gives them. */
  readonly deltaMode: number;
  readonly ctrlKey: boolean;
  readonly shiftKey: boolean;
  readonly pointer: { readonly x: number; readonly y: number };
}

/** Pixels of a line, as React Flow counts the lines of Firefox. */
const LINE_PIXELS = 20;

/**
 * The view after a turn of the wheel, as React Flow moves the canvas: a scroll of as many pixels of the screen as the
 * turn gives, sideways with Shift; with Ctrl (and the pinch of a touchpad), a zoom kept between its bounds whose point
 * under the pointer does not move. The view is bounded after, by the canvas.
 */
export function wheelView(view: View, turn: WheelTurn): View {
  if (turn.ctrlKey) {
    const step = -turn.deltaY * (turn.deltaMode === 1 ? 0.05 : turn.deltaMode === 0 ? 0.002 : 1);
    const zoom = Math.min(MAX_ZOOM, Math.max(MIN_ZOOM, view.zoom * 2 ** step));
    const ratio = zoom / view.zoom;
    return { x: turn.pointer.x - (turn.pointer.x - view.x) * ratio, y: turn.pointer.y - (turn.pointer.y - view.y) * ratio, zoom };
  }
  const unit = turn.deltaMode === 1 ? LINE_PIXELS : 1;
  const [dx, dy] = turn.shiftKey ? [turn.deltaY * unit, 0] : [turn.deltaX * unit, turn.deltaY * unit];
  return { x: view.x - dx, y: view.y - dy, zoom: view.zoom };
}

/**
 * Listens to the wheel on the headers of the canvas given, not passive so that Ctrl and the wheel do not zoom the page;
 * a field of a header (its name typed) keeps its wheel.
 */
export function useHeaderWheel(root: RefObject<HTMLElement | null>, view: View, change: (next: View) => void): void {
  useEffect(() => {
    const element = root.current;
    if (element === null) return undefined;
    const onWheel = (event: WheelEvent) => {
      const target = event.target instanceof Element ? event.target : null;
      if (target === null || target.closest('.lane-heads') === null || target.closest('.nowheel') !== null) return;
      event.preventDefault();
      const box = element.getBoundingClientRect();
      const { deltaX, deltaY, deltaMode, ctrlKey, shiftKey } = event;
      change(wheelView(view, { deltaX, deltaY, deltaMode, ctrlKey, shiftKey, pointer: { x: event.clientX - box.left, y: event.clientY - box.top } }));
    };
    element.addEventListener('wheel', onWheel, { passive: false });
    return () => {
      element.removeEventListener('wheel', onWheel);
    };
  }, [root, view, change]);
}
