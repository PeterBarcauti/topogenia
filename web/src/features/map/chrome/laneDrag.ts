// laneDrag.ts
// A lane header dragged to another place: the lane aimed by the centre of the header, the new order (B-2.12-14 to B-2.12-16).

import { useState, type PointerEvent } from 'react';

/**
 * The rank aimed by a header of variable heights moved by `dy` pixels of the screen: the lane whose band holds the centre
 * of the header moved (1.x `rangCibleVariable`). Tops and heights in pixels of the screen, in the order of the lanes.
 */
export function targetRank(start: number, dy: number, tops: readonly number[], heights: readonly number[]): number {
  const centre = (tops[start] ?? 0) + (heights[start] ?? 0) / 2 + dy;
  const rank = tops.findIndex((top, index) => centre < top + (heights[index] ?? 0));
  return rank === -1 ? tops.length - 1 : Math.max(0, rank);
}

/** The list with one element taken from a place and put at another. */
export function moveInList<T>(list: readonly T[], from: number, to: number): T[] {
  const copy = [...list];
  const [moved] = copy.splice(from, 1);
  if (moved !== undefined) copy.splice(to, 0, moved);
  return copy;
}

interface Drag {
  readonly laneId: string;
  readonly start: number;
  readonly startY: number;
  readonly dy: number;
}

/**
 * The drag of the headers with the capture of the pointer (B-2.12-14): the header follows the pointer, the lane aimed is
 * known while it moves, the new order is given on release when the centre of the header reached another lane. A move of a
 * few pixels (4 px in the 1.x, B-2.12-15) never reaches it: the centre must pass half a band, 60 px at least.
 */
export function useLaneDrag(tops: readonly number[], heights: readonly number[], onOrder: (from: number, to: number) => void) {
  const [drag, setDrag] = useState<Drag | null>(null);
  return {
    dragging: drag?.laneId ?? null,
    dy: drag?.dy ?? 0,
    target: drag === null ? null : targetRank(drag.start, drag.dy, tops, heights),
    start: (event: PointerEvent<HTMLElement>, laneId: string, rank: number) => {
      if (event.button !== 0) return;
      event.currentTarget.setPointerCapture(event.pointerId);
      setDrag({ laneId, start: rank, startY: event.clientY, dy: 0 });
    },
    move: (event: PointerEvent<HTMLElement>) => {
      if (drag === null) return;
      setDrag({ ...drag, dy: event.clientY - drag.startY });
    },
    end: () => {
      if (drag === null) return;
      setDrag(null);
      const to = targetRank(drag.start, drag.dy, tops, heights);
      if (to !== drag.start) onOrder(drag.start, to);
    },
    cancel: () => {
      setDrag(null);
    },
  };
}
