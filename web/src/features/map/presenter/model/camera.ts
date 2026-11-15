// camera.ts
// The camera of a presentation (ADR 097): the view that frames boxes in the screen less its bands, the view of now kept when it already shows them, the time of a travel; the way itself is the smooth zoom of van Wijk and Nuij that React Flow draws.

import type { Rect } from '../../model/geometry/grid.ts';
import type { Extent, View } from '../../model/geometry/viewport.ts';

/** The bands of the screen the presentation covers: the lanes on the left, the banner, the subtitles and the bar below. */
export interface Insets {
  readonly top: number;
  readonly right: number;
  readonly bottom: number;
  readonly left: number;
}

/** The zooms a frame keeps to: never so close that a step fills the screen (Prezi: a small object zoomed is a jolt), never past the floor. */
export interface ZoomBounds {
  readonly min: number;
  readonly max: number;
}

/** The room left around the boxes framed, in pixels of the screen. */
const MARGIN = 50;

/** The room a box keeps from the edge of the room when the map is held on the screen. */
const EDGE_MARGIN = 12;

/** A view of now that shows the boxes is kept if its zoom is within this ratio of the frame: no move for nothing (smart camera). */
const KEPT_ZOOM_RATIO = 1.2;

/** A travel lasts at least this, at most that, and grows with the way, as d3 and Mapbox time a flight. */
const TRAVEL_FLOOR_MS = 600;
const TRAVEL_CEILING_MS = 1800;
const TRAVEL_MS_A_SCREEN = 450;

const unionOf = (boxes: readonly Rect[]): Rect | null => {
  if (boxes.length === 0) return null;
  const left = Math.min(...boxes.map((box) => box.x));
  const top = Math.min(...boxes.map((box) => box.y));
  const right = Math.max(...boxes.map((box) => box.x + box.width));
  const bottom = Math.max(...boxes.map((box) => box.y + box.height));
  return { x: left, y: top, width: right - left, height: bottom - top };
};

/** The room of the screen the map is seen in. */
const roomOf = (screen: Extent, insets: Insets): Rect => ({
  x: insets.left, y: insets.top, width: Math.max(1, screen.width - insets.left - insets.right), height: Math.max(1, screen.height - insets.top - insets.bottom),
});

/** The view that frames the boxes at the centre of the room, at the zoom that fits them with their margin, within its bounds; null without boxes. */
export function frameOf(boxes: readonly Rect[], screen: Extent, insets: Insets, bounds: ZoomBounds): View | null {
  const union = unionOf(boxes);
  if (union === null) return null;
  const room = roomOf(screen, insets);
  const fit = Math.min((room.width - 2 * MARGIN) / Math.max(1, union.width), (room.height - 2 * MARGIN) / Math.max(1, union.height));
  const zoom = Math.min(bounds.max, Math.max(bounds.min, fit));
  return {
    x: room.x + room.width / 2 - (union.x + union.width / 2) * zoom,
    y: room.y + room.height / 2 - (union.y + union.height / 2) * zoom,
    zoom,
  };
}

/** Whether the view shows the whole union of the boxes inside the room. */
function shows(view: View, boxes: readonly Rect[], screen: Extent, insets: Insets): boolean {
  const union = unionOf(boxes);
  if (union === null) return true;
  const room = roomOf(screen, insets);
  const left = union.x * view.zoom + view.x;
  const top = union.y * view.zoom + view.y;
  return left >= room.x && top >= room.y && left + union.width * view.zoom <= room.x + room.width && top + union.height * view.zoom <= room.y + room.height;
}

/** The view of now when it already shows the boxes at a zoom close to their frame, else the frame: the camera moves only when it must. */
export function keptOrFramed(view: View, boxes: readonly Rect[], screen: Extent, insets: Insets, bounds: ZoomBounds): View | null {
  const frame = frameOf(boxes, screen, insets, bounds);
  if (frame === null) return null;
  const ratio = Math.max(view.zoom / frame.zoom, frame.zoom / view.zoom);
  return ratio <= KEPT_ZOOM_RATIO && shows(view, boxes, screen, insets) ? view : frame;
}

/**
 * The view moved so that a map larger than the room never shows the void past one of its edges, on each axis; the boxes
 * framed stay in view first, with their margin. A map smaller than the room is left where its frame put it.
 */
export function insideMap(view: View, map: Rect, boxes: readonly Rect[], screen: Extent, insets: Insets): View {
  const room = roomOf(screen, insets);
  const union = unionOf(boxes) ?? map;
  const axis = (at: number, start: number, length: number, mapStart: number, mapLength: number, boxStart: number, boxLength: number): number => {
    let kept = at;
    if (mapLength * view.zoom >= length) kept = Math.min(start - mapStart * view.zoom, Math.max(start + length - (mapStart + mapLength) * view.zoom, kept));
    // The boxes inside the room: their start after its start, their end before its end, each with a small margin.
    const low = start + EDGE_MARGIN - boxStart * view.zoom;
    const high = start + length - EDGE_MARGIN - (boxStart + boxLength) * view.zoom;
    return low <= high ? Math.min(high, Math.max(low, kept)) : kept;
  };
  return {
    x: axis(view.x, room.x, room.width, map.x, map.width, union.x, union.width),
    y: axis(view.y, room.y, room.height, map.y, map.height, union.y, union.height),
    zoom: view.zoom,
  };
}

/** The time of a travel between two views: by the way in screens, in pan and in zoom, within its floor and its ceiling; none for no way. */
export function travelMs(from: View, to: View, screen: Extent): number {
  const zoom = Math.sqrt(from.zoom * to.zoom);
  // The centre of the screen, in units of the map, before and after: the pan counted in screens at the mean zoom.
  const centre = (view: View) => ({ x: (screen.width / 2 - view.x) / view.zoom, y: (screen.height / 2 - view.y) / view.zoom });
  const [a, b] = [centre(from), centre(to)];
  const pan = (Math.hypot(b.x - a.x, b.y - a.y) * zoom) / Math.max(1, Math.hypot(screen.width, screen.height));
  const way = pan + Math.abs(Math.log2(to.zoom / from.zoom));
  if (way < 1e-6) return 0;
  return Math.round(Math.min(TRAVEL_CEILING_MS, Math.max(TRAVEL_FLOOR_MS, TRAVEL_FLOOR_MS + way * TRAVEL_MS_A_SCREEN)));
}
