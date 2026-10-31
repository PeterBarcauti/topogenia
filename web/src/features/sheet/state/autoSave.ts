// autoSave.ts
// The saving of the sheet as typed: one send 1200 ms after the last change, one at a time with the version known, the typing never lost, a conflict read again (annex B 4).

import { createStore, type StoreApi } from 'zustand/vanilla';
import type { Schema } from '../../../api/client.ts';
import { isApiError } from '../../../api/errors.ts';

type SheetView = Schema<'SheetView'>;
type SaveSheetBody = Schema<'SaveSheetBody'>;

/** The delay between the last change and its sending (B-4-39; 1.x `DELAI_ENREGISTREMENT`, US-13: two seconds at most). */
export const SAVE_DELAY = 1200;

/** « Enregistré », « Modifications en attente… », « Enregistrement… », « Non enregistré » (B-4-42). */
export type SaveStatus = 'saved' | 'pending' | 'saving' | 'failed';

/** How the queue reaches the host. */
export interface AutoSaveCalls {
  /** Sends the draft with the version known in `If-Match`; gives the sheet read after the write. */
  readonly save: (body: SaveSheetBody, version: number) => Promise<SheetView>;
  /** Reads the sheet again, after a conflict. */
  readonly read: () => Promise<SheetView>;
}

/** What the queue tells the panel. */
export interface AutoSaveListener {
  /** A sheet the queue received: what the panel shows of it, the typing left alone (B-4-41). */
  readonly onView: (view: SheetView) => void;
  /** A send refused or lost: the panel says it. */
  readonly onFailure: (error: unknown) => void;
}

export interface AutoSave {
  readonly state: StoreApi<{ readonly status: SaveStatus }>;
  /** Who hears the sheets received and the failures, replaced by the next one. */
  listen(listener: AutoSaveListener): void;
  /** The typing changed: it replaces what waited and leaves {@link SAVE_DELAY} after the last change. */
  change(body: SaveSheetBody): void;
  /** Sends at once what waits; resolves once it is answered (closing, another step, validating: B-4-45, B-4-53). */
  flush(): Promise<void>;
  /** A sheet read elsewhere (validated, documents): its version is the one to send next. */
  know(view: SheetView): void;
  /** The version for `If-Match`: 0 while the sheet was never saved (ADR 045). */
  version(): number;
  /** Something typed is not on the host yet: it waits, or a send has not answered (B-4-46); a failure leaves it waiting. */
  unsaved(): boolean;
  failed(): boolean;
}

/** A queue for the sheet of one step, from the version of the sheet read (null: never saved). */
export function createAutoSave(calls: AutoSaveCalls, initialVersion: number | null): AutoSave {
  let hooks: AutoSaveListener = { onView: () => undefined, onFailure: () => undefined };
  const state = createStore<{ readonly status: SaveStatus }>()(() => ({ status: 'saved' }));
  let version = initialVersion ?? 0;
  /** The last typing given, and the one not sent yet. */
  let latest: SaveSheetBody | null = null;
  let waiting: SaveSheetBody | null = null;
  /** The sends asked and not answered yet. */
  let sends = 0;
  let lastFailed = false;
  let timer: ReturnType<typeof setTimeout> | undefined;
  let queue: Promise<void> = Promise.resolve();

  const know = (view: SheetView) => {
    version = view.version ?? 0;
  };
  const readAgain = async () => {
    try {
      const view = await calls.read();
      know(view);
      hooks.onView(view);
    } catch {
      // The message of the conflict is already shown.
    }
  };
  const send = async (body: SaveSheetBody) => {
    try {
      const view = await calls.save(body, version);
      sends -= 1;
      know(view);
      lastFailed = false;
      hooks.onView(view);
      state.setState({ status: waiting !== null ? 'pending' : sends > 0 ? 'saving' : 'saved' });
    } catch (failure) {
      sends -= 1;
      lastFailed = true;
      state.setState({ status: 'failed' });
      hooks.onFailure(failure);
      // Never lost: it leaves again at the next send, unless a newer typing waits or is already on its way (B-4-43).
      if (waiting === null && latest === body) waiting = body;
      // Changed by someone else: the version of the host for the next send, the typing kept under the eyes (B-4-44).
      if (isApiError(failure) && failure.status === 409) await readAgain();
    }
  };
  const flush = (): Promise<void> => {
    clearTimeout(timer);
    const body = waiting;
    if (body === null) return queue;
    waiting = null;
    sends += 1;
    state.setState({ status: 'saving' });
    queue = queue.then(() => send(body));
    return queue;
  };
  return {
    state,
    listen(listener) {
      hooks = listener;
    },
    change(body) {
      latest = body;
      waiting = body;
      state.setState({ status: 'pending' });
      clearTimeout(timer);
      timer = setTimeout(() => void flush(), SAVE_DELAY);
    },
    flush,
    know,
    version: () => version,
    unsaved: () => waiting !== null || sends > 0,
    failed: () => lastFailed,
  };
}
