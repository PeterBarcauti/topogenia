// floating.ts
// The floating element open over the map, one at a time, in a small store of the editor (B-7-19, B-8.2-09; 1.x: a union of 14 kinds in the canvas).

import { createStore, type StoreApi } from 'zustand/vanilla';
import type { Schema } from '../../../api/client.ts';
import type { ReplacedProject } from '../../project/package/ImportPackageDialog.tsx';
import type { Point } from '../model/geometry/grid.ts';

/**
 * What a menu acts on: a step (and the point of the window it was opened at, where its RACI panel opens), the steps chosen,
 * the background at a cell, a flow, a lane, the project (File menu) (annex B 2.4).
 */
export type MenuTarget =
  | { readonly kind: 'step'; readonly stepId: string; readonly at?: Point }
  | { readonly kind: 'selection'; readonly stepIds: readonly string[] }
  | { readonly kind: 'pane'; readonly point: Point }
  | { readonly kind: 'flow'; readonly flowId: string; readonly at: Point }
  | { readonly kind: 'lane'; readonly laneId: string; readonly at?: Point }
  | { readonly kind: 'file' };

export type Floating =
  /** A context menu at a point of the window. */
  | { readonly kind: 'menu'; readonly target: MenuTarget; readonly at: Point }
  /** The floating field of the label of a flow (B-2.3-33). */
  | { readonly kind: 'flow-label'; readonly flowId: string; readonly at: Point }
  /** A lane that holds steps: move them or delete them with it (B-2.4-50, B-2.4-51). */
  | { readonly kind: 'lane-delete'; readonly laneId: string }
  /** A detailed step alone: delete its map with it or give the map to another step (B-2.3-39). */
  | { readonly kind: 'macro-delete'; readonly stepId: string }
  /** A selection that holds detailed steps: their maps go too (B-2.3-40). */
  | { readonly kind: 'selection-delete'; readonly steps: readonly string[]; readonly flows: readonly string[] }
  /** Delete the detail of a detailed step (B-2.4-18). */
  | { readonly kind: 'detail-delete'; readonly stepId: string }
  /** The command bar: flows typed by the codes of their steps (B-2.13). */
  | { readonly kind: 'command' }
  /** The panel of the remainder of the open frame, not modal (B-2.9-24). */
  | { readonly kind: 'remainder' }
  /** Where « Finish » stored the remainder of the frame of a step, stored on its own or not (B-2.9-37, B-2.9-39). */
  | { readonly kind: 'arrangements'; readonly code: string; readonly arrangements: readonly Schema<'Arrangement'>[]; readonly auto: boolean }
  /** The RACI panel of a step, next to the click (B-2.3-12, B-2.11-10). */
  | { readonly kind: 'raci'; readonly stepId: string; readonly at: Point }
  /** The RACI matrix of the map (B-2.11-14). */
  | { readonly kind: 'matrix' }
  /** The dialogs of the File menu (ADR 058, AR-154): open a .pmap file, import a package, duplicate the project, export it, replace it by a package. */
  | { readonly kind: 'open-pmap' }
  | { readonly kind: 'import-package' }
  | { readonly kind: 'duplicate' }
  | { readonly kind: 'export-package' }
  | { readonly kind: 'replace'; readonly replaced: ReplacedProject }
  /** The translations of the project by a workbook, from the button of the header (annex B 5). */
  | { readonly kind: 'translations'; readonly assistant?: string }
  /** The manual translations of the label of a step or a flow, or of the name of a lane (B-5-28, spec 10). */
  | { readonly kind: 'translate'; readonly object: TranslatedObject }
  /** An entry of the value stream at the point of its menu: the volume of a start, the share of an exit, the headcount of a lane (spec 14.6). */
  | { readonly kind: 'stream-entry'; readonly entry: StreamEntry; readonly at: Point };

/** What an entry of the value stream is about (spec 14.2, S1, S2, S4). */
export type StreamEntry =
  | { readonly kind: 'volume'; readonly stepId: string }
  | { readonly kind: 'share'; readonly flowId: string }
  | { readonly kind: 'headcount'; readonly laneId: string };

/** An object whose name is translated by hand: its kind and identifier. */
export interface TranslatedObject {
  readonly kind: 'step' | 'flow' | 'lane';
  readonly id: string;
}

export interface FloatingState {
  readonly floating: Floating | null;
  readonly open: (floating: Floating) => void;
  readonly close: () => void;
}

/** One store a map editor; opening an element closes the one open. */
export function createFloatingStore(): StoreApi<FloatingState> {
  return createStore<FloatingState>()((setState) => ({
    floating: null,
    open(floating) {
      setState({ floating });
    },
    close() {
      setState({ floating: null });
    },
  }));
}
