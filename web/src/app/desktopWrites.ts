// desktopWrites.ts
// The tab of the desktop mode tells its host whether it still has writes to send and whether a form is changed and not validated (`POST /api/desktop/writes`), one declaration at a time, the page left included, so that « Quit » by the icon near the clock waits for the writes and asks at once for the form (ADR 115, R11).

import { api } from '../api/client.ts';
import { formsUnsaved, subscribeWrites, writesWaiting } from '../shared/state/pendingWrites.ts';

/** What the tab says: writes that will leave by themselves, a form that will never leave by itself. */
interface Declared {
  readonly waiting: boolean;
  readonly unsaved: boolean;
}

const NOTHING: Declared = { waiting: false, unsaved: false };

const same = (one: Declared, other: Declared) => one.waiting === other.waiting && one.unsaved === other.unsaved;

/**
 * Declares to the host of the desktop mode, under the name of the tab (letters, digits and dashes), each change of
 * {@link writesWaiting} and of {@link formsUnsaved}: one declaration at a time, the next once the one before is answered
 * and only when it says something new; `unsaved` is sent only when it holds (absent, the host reads false). A refusal or a
 * failure is kept silent (the icon then stops at once, as before). The page left lifts the declaration at once, with
 * `keepalive`, and nothing more is declared until the page is shown again. The function returned ends it and lifts what
 * was declared. Never started in server mode.
 */
export function declareWrites(tab: string = crypto.randomUUID()): () => void {
  /** What the host was last told, or is being told. */
  let declared = NOTHING;
  let sending = false;
  let hidden = false;
  let ended = false;
  const post = (state: Declared, keepalive: boolean): Promise<void> => {
    const body = state.unsaved ? { tab, waiting: state.waiting, unsaved: true } : { tab, waiting: state.waiting };
    return api.post('/api/desktop/writes', { body, keepalive }).then(() => undefined, () => undefined);
  };
  const wanted = (): Declared => (ended ? NOTHING : { waiting: writesWaiting(), unsaved: formsUnsaved() });
  const follow = async () => {
    if (sending) return;
    sending = true;
    try {
      for (let next = wanted(); !hidden && !same(next, declared); next = wanted()) {
        declared = next;
        await post(next, false);
      }
    } finally {
      sending = false;
    }
  };
  const run = () => {
    void follow();
  };
  const leave = () => {
    hidden = true;
    if (same(declared, NOTHING)) return;
    declared = NOTHING;
    void post(NOTHING, true);
  };
  const show = () => {
    hidden = false;
    run();
  };
  const unsubscribe = subscribeWrites(run);
  window.addEventListener('pagehide', leave);
  window.addEventListener('pageshow', show);
  run();
  return () => {
    ended = true;
    unsubscribe();
    window.removeEventListener('pagehide', leave);
    window.removeEventListener('pageshow', show);
    run();
  };
}
