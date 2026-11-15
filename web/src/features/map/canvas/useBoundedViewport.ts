// useBoundedViewport.ts
// The view of the canvas, kept by the editor and never in the browser: bounded to the map at each change, of the view, the map or the screen (annex B 2.2, B-7-09).

import { useMemo, useState } from 'react';
import { INITIAL_VIEW } from '../model/geometry/constants.ts';
import { boundView, type Extent, type View } from '../model/geometry/viewport.ts';

export interface BoundedViewport {
  /** The view shown: the one kept, bounded to the map and the screen of now (B-2.2-09). */
  readonly view: View;
  /** Takes the view the canvas moved to, bounded. */
  readonly change: (next: View) => void;
  /** Back to the start of the map at zoom 1: the first column right of the headers, the first lane at the top (B-2.1-43). */
  readonly backToStart: () => void;
}

/** The view opens at the start of the map at each opening of the editor, whatever was shown before (B-7-09). */
export function useBoundedViewport(map: Extent, screen: Extent): BoundedViewport {
  const [kept, setKept] = useState<View>(INITIAL_VIEW);
  // A new view at each move, even when the bound gives back the one kept: the canvas, which moved past the bound on its
  // own, must be given the bounded view again to come back to it.
  const take = (next: View) => {
    setKept(boundView(next, map, screen));
  };
  // The same values give the same view, so that the canvas does not set it again at each drawing.
  const view = useMemo(
    () => boundView(kept, { width: map.width, height: map.height }, { width: screen.width, height: screen.height }),
    [kept, map.width, map.height, screen.width, screen.height],
  );
  return {
    view,
    change: take,
    backToStart: () => {
      take(INITIAL_VIEW);
    },
  };
}
