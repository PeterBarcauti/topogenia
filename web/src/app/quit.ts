// quit.ts
// The command « Quit » of the desktop mode: the host asked to stop, then the application stopped in the page (spec 5.2).

import { createStore } from 'zustand/vanilla';
import { api } from '../api/client.ts';
import { sendPendingWrites } from '../shared/state/pendingWrites.ts';

/** True once the host answered the command: the shell shows only that the application has stopped. */
export const stopped = createStore<{ stopped: boolean }>()(() => ({ stopped: false }));

/**
 * Sends first what is typed and still waits (a sheet), then asks the host of the desktop mode to stop
 * (`POST /api/desktop/quit`, which answers before it stops) and marks the application stopped. `unsaved` when a typing
 * could not reach the host: the host is not asked, the application keeps running. A failure of the host is thrown.
 */
export async function quitApplication(): Promise<'stopped' | 'unsaved'> {
  if (!(await sendPendingWrites())) return 'unsaved';
  await api.post('/api/desktop/quit');
  stopped.setState({ stopped: true });
  return 'stopped';
}
