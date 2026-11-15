// viewport.ts
// The view hooked to the top of the map: bounded above, below, left and right, the zoom kept (annex B 2.2, AR-19).

import { BOTTOM_MARGIN, INITIAL_VIEW, RIGHT_MARGIN, TOP_MARGIN } from './constants.ts';

/** A view of the canvas: its shift in pixels of the screen and its zoom. */
export interface View {
  readonly x: number;
  readonly y: number;
  readonly zoom: number;
}

/** A size: of the map in units of the canvas, of the screen in pixels. */
export interface Extent {
  readonly width: number;
  readonly height: number;
}

/**
 * The view bounded to the map, as Visio and Lucidchart hook lanes: the first lane never goes down under the top margin
 * (B-2.2-04); the view goes up past the last lane only when the map is taller than the screen (B-2.2-05); never left of
 * the column 0 (B-2.2-06); right, up to the end of the map, free columns included (B-2.2-07). The zoom is kept (B-2.2-08).
 * A screen not measured yet (0 × 0) leaves the view as it is.
 */
export function boundView(view: View, map: Extent, screen: Extent): View {
  if (screen.width <= 0 || screen.height <= 0) return view;
  const { zoom } = view;
  const yMax = TOP_MARGIN;
  const yMin = Math.min(yMax, screen.height - map.height * zoom - BOTTOM_MARGIN);
  const xMax = INITIAL_VIEW.x;
  const xMin = Math.min(xMax, screen.width - map.width * zoom - RIGHT_MARGIN);
  return { x: Math.min(xMax, Math.max(xMin, view.x)), y: Math.min(yMax, Math.max(yMin, view.y)), zoom };
}
