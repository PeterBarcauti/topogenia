// bpmnState.ts
// The view in BPMN 2.0 of a map (ADR 106) on or off for the session, so that it stays on when the person dives into a detailed step or climbs back (each map mounts its own editor); the details it draws are a preference of the map (`showBpmnDetails`).

import { createStore, type StoreApi } from 'zustand/vanilla';

export interface BpmnViewState {
  /** The view in BPMN 2.0 replaces the canvas of the map. */
  readonly open: boolean;
  readonly setOpen: (open: boolean) => void;
}

/** The view of the application: on or off whatever map is shown. */
export const bpmnView: StoreApi<BpmnViewState> = createStore<BpmnViewState>((set) => ({
  open: false,
  setOpen: (open) => { set({ open }); },
}));
