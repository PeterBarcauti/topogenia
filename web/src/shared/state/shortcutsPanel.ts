// shortcutsPanel.ts
// The panel of the keyboard shortcuts open or not, on every page: opened by ? on the map and by the Help menu of the header, shown by the header (lot 1 of the 3.4, ADR 109).

import { createStore, type StoreApi } from 'zustand/vanilla';

export interface ShortcutsPanelState {
  /** The panel of the keyboard shortcuts is shown. */
  readonly open: boolean;
  readonly setOpen: (open: boolean) => void;
}

/** The panel of the application: one, whatever page is shown. */
export const shortcutsPanel: StoreApi<ShortcutsPanelState> = createStore<ShortcutsPanelState>((set) => ({
  open: false,
  setOpen: (open) => { set({ open }); },
}));
