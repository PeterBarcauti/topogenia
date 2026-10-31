// frame.ts
// The frame of a dissolution: the box around the steps of the frame still on the map, with its margins, and the steps of other flows it covers (annex B 2.9, B-3.1-12).

import type { Schema } from '../../../api/client.ts';
import { FRAME_MARGIN, FRAME_MARGIN_TOP } from './geometry/constants.ts';
import type { Rect } from './geometry/grid.ts';

/** The box of the frame and the steps it covers without holding them: greyed (B-2.9-19, B-3.2-17). */
export interface FrameBox {
  readonly box: Rect;
  readonly outside: ReadonlySet<string>;
}

const overlaps = (a: Rect, b: Rect): boolean => a.x < b.x + b.width && a.x + a.width > b.x && a.y < b.y + b.height && a.y + a.height > b.y;

/**
 * The frame around its steps, 14 units off their boxes and 26 above for its label (B-2.9-17, B-2.9-18); null without an
 * open frame or when none of its steps is on the map.
 */
export function frameBox(frame: Pick<Schema<'FrameView'>, 'frame_step_ids'> | null, boxes: ReadonlyMap<string, Rect>): FrameBox | null {
  if (frame === null) return null;
  const members = new Set(frame.frame_step_ids);
  const inside = [...boxes].filter(([id]) => members.has(id)).map(([, box]) => box);
  if (inside.length === 0) return null;
  const x = Math.min(...inside.map((box) => box.x)) - FRAME_MARGIN;
  const y = Math.min(...inside.map((box) => box.y)) - FRAME_MARGIN_TOP;
  const right = Math.max(...inside.map((box) => box.x + box.width)) + FRAME_MARGIN;
  const bottom = Math.max(...inside.map((box) => box.y + box.height)) + FRAME_MARGIN;
  const box = { x, y, width: right - x, height: bottom - y };
  const outside = new Set([...boxes].filter(([id, step]) => !members.has(id) && overlaps(step, box)).map(([id]) => id));
  return { box, outside };
}
