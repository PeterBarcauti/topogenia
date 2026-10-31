// editor.ts
// The editor of one map: its stores, the writes sent one after the other and their failures, the notifications and the way to another map (annex B 2.6, B-7-16).

import type { StoreApi } from 'zustand/vanilla';
import type { Schema } from '../../../api/client.ts';
import { isApiError } from '../../../api/errors.ts';
import { navigate as goTo } from '../../../app/router.ts';
import { messageOf } from '../../../shared/i18n/errorMessage.ts';
import { notify as toast, type Notifier } from '../../../shared/ui/toasts.ts';
import { createAssistantStore, type AssistantState } from '../../assistant/panel/assistantStore.ts';
import type { CaptureTarget } from '../export/capture.ts';
import { createDockStore, type DockState } from './dock.ts';
import { createFloatingStore, type FloatingState } from './floating.ts';
import { createMapStore, type MapSource, type MapState } from './mapStore.ts';
import { browserStorage, createPreferencesStore, type PreferencesState, type PreferenceStorage } from './preferences.ts';

type MapView = Schema<'MapView'>;
type StructureReply = Schema<'StructureReply'>;

/**
 * A write of the map: the call to the host, made with the map of the moment, or null when there is nothing to send (known at
 * once, or once what the write had to read is read).
 */
export type Write = (map: MapView) => Promise<StructureReply | null> | null;

export interface MapEditor {
  readonly store: StoreApi<MapState>;
  readonly preferences: StoreApi<PreferencesState>;
  readonly floating: StoreApi<FloatingState>;
  /**
   * Sends a write when the one before has finished (B-2.6-09: the operations of the map are serialised); the reply is
   * applied, nothing before (B-2.6-11). A refusal shows its message (B-2.6-12), a conflict reads the map again
   * (B-2.6-13); the promise then gives null, as when there was nothing to send.
   */
  readonly write: (send: Write) => Promise<StructureReply | null>;
  /**
   * Reads the map again once the writes sent before have been answered: a write of a sheet changed what the map shows
   * (the documents on the flows, the pictograms, the gauges of other steps) without giving the map back.
   */
  readonly reload: () => Promise<void>;
  readonly notify: Notifier;
  /** Opens another screen: a sub-map (B-2.3-10, B-2.4-10). */
  readonly navigate: (path: string) => void;
  /** The user is an administrator of the application: the entries [admin] of the File menu (B-2.4-53, B-2.4-55). */
  readonly admin: boolean;
  /** The languages of the configuration (AR-29): a name is translated into the others; one language alone, nothing to translate. */
  readonly languages: readonly string[];
  /** The canvas shown, given by the canvas itself to the exports that capture it (annex B 2.14). */
  readonly canvas: CanvasHandle;
  /** The panel of the assistant of the map and its thread, which lives with the editor and is never kept (spec 3.0 section 3.1-8). */
  readonly assistant: StoreApi<AssistantState>;
  /** The width and the folding of the panels docked on the right (spec 3.0 section 15.2). */
  readonly dock: StoreApi<DockState>;
}

/** Where the canvas shown is kept for the exports: given by the canvas when it shows, taken back when it leaves. */
export interface CanvasHandle {
  /** The canvas shown, null while none is. */
  readonly target: () => CaptureTarget | null;
  readonly show: (target: CaptureTarget | null) => void;
  /** Puts steps in view, the first at the centre at the zoom of now (the codes of a reply of the assistant); nothing while no canvas is shown. */
  readonly reveal: (stepIds: readonly string[]) => void;
  /** Given by the canvas when it shows, taken back when it leaves. */
  readonly showReveal: (reveal: ((stepIds: readonly string[]) => void) | null) => void;
}

function createCanvasHandle(): CanvasHandle {
  let shown: CaptureTarget | null = null;
  let revealer: ((stepIds: readonly string[]) => void) | null = null;
  return {
    target: () => shown,
    show: (target) => {
      shown = target;
    },
    reveal: (stepIds) => {
      revealer?.(stepIds);
    },
    showReveal: (reveal) => {
      revealer = reveal;
    },
  };
}

export interface EditorOptions {
  /** Where the map is read and the preferences kept: the host and the browser, fakes in the tests. */
  readonly source?: MapSource;
  readonly storage?: PreferenceStorage | null;
  readonly notify?: Notifier;
  readonly navigate?: (path: string) => void;
  readonly admin?: boolean;
  readonly languages?: readonly string[];
}

/** An editor for the map shown; a new map mounts a new editor (B-1.1-09). */
export function createMapEditor(options: EditorOptions = {}): MapEditor {
  const store = createMapStore(options.source);
  const notify = options.notify ?? toast;
  let queue: Promise<unknown> = Promise.resolve();
  const sendNow = async (send: Write): Promise<StructureReply | null> => {
    const map = store.getState().map;
    if (map === null) return null;
    try {
      const call = send(map);
      if (call === null) return null;
      const reply = await call;
      if (reply === null) return null;
      await store.getState().apply(reply);
      return reply;
    } catch (failure) {
      notify(messageOf(failure), 'error');
      if (isApiError(failure) && failure.status === 409) await store.getState().load(map.id);
      return null;
    }
  };
  const assistant = createAssistantStore({
    mapId: () => store.getState().map?.id ?? null,
    selection: () => {
      const state = store.getState();
      const chosen = new Set(state.selection.steps);
      return state.map?.steps.filter((step) => chosen.has(step.id)).map((step) => step.code) ?? [];
    },
  });
  const preferences = createPreferencesStore(options.storage);
  const floating = createFloatingStore();
  return {
    store,
    assistant,
    preferences,
    floating,
    dock: createDockStore(options.storage === undefined ? browserStorage() : options.storage, { store, assistant, preferences, floating }),
    notify,
    navigate: options.navigate ?? goTo,
    admin: options.admin ?? false,
    languages: options.languages ?? [],
    canvas: createCanvasHandle(),
    write(send) {
      const turn = queue.then(() => sendNow(send));
      queue = turn;
      return turn;
    },
    reload() {
      const turn = queue.then(async () => {
        const map = store.getState().map;
        if (map !== null) await store.getState().load(map.id);
      });
      queue = turn;
      return turn;
    },
  };
}
