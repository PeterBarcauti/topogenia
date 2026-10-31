// dock.ts
// What a panel docked on the right knows of its frame (spec 3.0 section 15.2, ADR 086): folded or not, and how to fold it; null for a panel shown out of a frame.

import { createContext, useContext } from 'react';

export interface DockControl {
  /** The panel is folded to its strip: shown no more, kept as it is. */
  readonly folded: boolean;
  readonly fold: () => void;
}

export const DockContext = createContext<DockControl | null>(null);

/** The frame of the panel shown, null out of one. */
export const useDock = (): DockControl | null => useContext(DockContext);
