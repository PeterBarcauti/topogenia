// dock.ts
// The panels docked on the right of the map (spec 3.0 section 15.2, ADR 086): one width for all, chosen by the handle and kept in the browser; each panel folded for itself for the session, shown unfolded when it is opened.

import { createStore, type StoreApi } from 'zustand/vanilla';
import type { AssistantState } from '../../assistant/panel/assistantStore.ts';
import type { FloatingState } from './floating.ts';
import type { MapState } from './mapStore.ts';
import type { PreferencesState, PreferenceStorage } from './preferences.ts';

/** The panels of the right, in the order they cover each other: the remainder of a frame, the sheet, the assistant, the value stream. */
export type DockPanel = 'remainder' | 'sheet' | 'assistant' | 'stream';

/**
 * The width of the panels in pixels: at first (the token `--side-panel-width`), its least, its share of the window at
 * most, the step of an arrow key; the width of the strip of a panel folded.
 */
export const DOCK = { width: 440, min: 320, maxShare: 0.7, step: 16, strip: 40 } as const;

/** The key of the local storage, neutral as the preferences (AR-28: `app.*`). */
export const DOCK_WIDTH_KEY = 'app.sidePanelWidth';

export interface DockState {
  /** The width chosen, in pixels; a narrow window shows it narrower ({@link shownWidth}). */
  readonly width: number;
  /** The panels folded to their strip. */
  readonly folded: Readonly<Partial<Record<DockPanel, true>>>;
  /** A new width, the least at least; written to the browser when `keep` holds (the end of a drag, a key). */
  readonly setWidth: (width: number, keep: boolean) => void;
  readonly fold: (panel: DockPanel, folded: boolean) => void;
}

/** The width shown in a window of the width given: the width chosen, the share of the window at most, the least at least. */
export function shownWidth(width: number, windowWidth: number): number {
  return Math.round(Math.max(DOCK.min, Math.min(width, windowWidth * DOCK.maxShare)));
}

/** The width kept: a whole number of pixels, the least at least; else, or where the storage throws, the first width. */
function readWidth(storage: PreferenceStorage | null): number {
  try {
    const value = Number(storage?.getItem(DOCK_WIDTH_KEY) ?? Number.NaN);
    return Number.isInteger(value) && value >= DOCK.min ? value : DOCK.width;
  } catch {
    return DOCK.width;
  }
}

/** The stores whose openings show a panel unfolded. */
export interface DockSources {
  readonly store: StoreApi<MapState>;
  readonly assistant: StoreApi<AssistantState>;
  readonly preferences: StoreApi<PreferencesState>;
  readonly floating: StoreApi<FloatingState>;
}

/**
 * The panels of an editor: a sheet opened (another step), the assistant opened, the layer turned on or the remainder of
 * a frame opened shows its panel unfolded (one panel at a time: `watchSidePanel`).
 */
export function createDockStore(storage: PreferenceStorage | null, sources: DockSources): StoreApi<DockState> {
  const dock = createStore<DockState>()((setState) => ({
    width: readWidth(storage),
    folded: {},
    setWidth(width, keep) {
      const kept = Math.round(Math.max(DOCK.min, width));
      setState({ width: kept });
      if (!keep) return;
      try {
        storage?.setItem(DOCK_WIDTH_KEY, String(kept));
      } catch {
        // The storage is full or blocked: the width holds for the session only.
      }
    },
    fold(panel, folded) {
      setState((state) => {
        const others = Object.fromEntries(Object.entries(state.folded).filter(([name]) => name !== panel));
        return { folded: folded ? { ...others, [panel]: true } : others };
      });
    },
  }));
  const unfold = (panel: DockPanel) => {
    dock.getState().fold(panel, false);
  };
  sources.store.subscribe((state, previous) => {
    if (state.openSheet !== null && state.openSheet !== previous.openSheet) unfold('sheet');
  });
  sources.assistant.subscribe((state, previous) => {
    if (state.open && !previous.open) unfold('assistant');
  });
  sources.preferences.subscribe((state, previous) => {
    if (state.showValueStream && !previous.showValueStream) unfold('stream');
  });
  sources.floating.subscribe((state, previous) => {
    if (state.floating?.kind === 'remainder' && previous.floating?.kind !== 'remainder') unfold('remainder');
  });
  return dock;
}
