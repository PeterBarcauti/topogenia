// pendingWrites.test.ts
// What waits for the host, followed queue by queue: a subscriber learns each switch of « something waits », a closed panel still writing is followed to its last reply, a refused saving is still seen waiting (ADR 115); a form changed and not validated, followed apart (R11).

import { ApiError, NETWORK_ERROR } from '../../api/errors.ts';
import { createAutoSave } from '../../features/sheet/state/autoSave.ts';
import { sheetViewOf } from '../../test/sheets.ts';
import { formsUnsaved, sendPendingWrites, subscribeWrites, trackUnsavedForm, trackWrites, writesWaiting, type PendingWrites } from './pendingWrites.ts';

/** A queue the test sets waiting or not, which tells its subscribers each time. */
function queueOf(flush: () => Promise<void> = () => Promise.resolve()) {
  let waiting = false;
  const listeners = new Set<() => void>();
  const queue: PendingWrites & { set: (value: boolean) => void } = {
    unsaved: () => waiting,
    flush,
    subscribe(listener) {
      listeners.add(listener);
      return () => {
        listeners.delete(listener);
      };
    },
    set(value) {
      waiting = value;
      for (const listener of listeners) listener();
    },
  };
  return queue;
}

/** What each test started and ends, a test that fails included: the queues followed, the subscribers. */
const ends: (() => void)[] = [];

/** Follows the queue given, left after the test unless the test leaves it first. */
function follow(queue: PendingWrites): () => void {
  const untrack = trackWrites(queue);
  ends.push(untrack);
  return untrack;
}

/** What a subscriber read of {@link writesWaiting} at each notice, and the switches it saw (from « nothing waits »). */
function watch() {
  const seen: boolean[] = [];
  const stop = subscribeWrites(() => seen.push(writesWaiting()));
  ends.push(stop);
  const switches = () => seen.filter((value, index) => value !== (index === 0 ? false : seen[index - 1]));
  return { seen, stop, switches };
}

// Each test leaves no queue followed: a queue closed while it writes leaves once answered.
afterEach(async () => {
  for (const end of ends.splice(0)) end();
  await vi.waitFor(() => {
    expect(writesWaiting()).toBe(false);
  });
});

describe('the subscription to what waits (ADR 115)', () => {
  it('a subscriber learns each switch of a queue followed, and nothing once it stops', () => {
    const { seen, stop, switches } = watch();
    const queue = queueOf();
    const untrack = follow(queue);
    queue.set(true);
    queue.set(false);
    queue.set(true);
    queue.set(false);
    expect(switches()).toEqual([true, false, true, false]);
    stop();
    queue.set(true);
    expect(seen.at(-1)).toBe(false);
    queue.set(false);
    untrack();
  });

  it('a queue followed while it already waits is told at once, and its end too', () => {
    const { seen, stop } = watch();
    const untrack = follow({ unsaved: () => true, flush: () => Promise.resolve() });
    expect(seen).toEqual([true]);
    stop();
    untrack();
  });

  it('a panel closed while its queue still writes is followed until the reply, then the subscriber learns that nothing waits', async () => {
    let written = false;
    let answer: () => void = () => undefined;
    const queue = queueOf(() => new Promise<void>((resolve) => {
      answer = () => {
        written = true;
        resolve();
      };
    }));
    const untrack = follow({ ...queue, unsaved: () => queue.unsaved() && !written });
    queue.set(true);
    const { seen, stop } = watch();
    untrack();
    expect([writesWaiting(), seen]).toEqual([true, []]);
    // The reply changes nothing the queue tells: the subscriber learns it from the queue left.
    answer();
    await vi.waitFor(() => {
      expect(seen).toEqual([false]);
    });
    // The queue left, its own notices reach nobody any more.
    queue.set(true);
    expect([writesWaiting(), seen]).toEqual([false, [false]]);
    stop();
  });

  it('a saving refused leaves the typing waiting in the eyes of the subscriber: the queue tells it once the typing waits again', async () => {
    const save = createAutoSave({
      save: () => Promise.reject(new ApiError(0, NETWORK_ERROR, '')),
      read: () => Promise.resolve(sheetViewOf()),
    }, null);
    const untrack = follow(save);
    const { seen, stop, switches } = watch();
    save.change({ fields: {}, procedure: [], free_text: 'Facturer' });
    await save.flush();
    expect([save.unsaved(), writesWaiting(), seen.at(-1), switches()]).toEqual([true, true, true, [true]]);
    stop();
    untrack();
  });

  it('a saving answered tells the subscriber that nothing waits any more', async () => {
    const save = createAutoSave({ save: () => Promise.resolve(sheetViewOf({ version: 1 })), read: () => Promise.resolve(sheetViewOf()) }, null);
    const untrack = follow(save);
    const { stop, switches } = watch();
    save.change({ fields: {}, procedure: [], free_text: 'Facturer' });
    await save.flush();
    expect(switches()).toEqual([true, false]);
    stop();
    untrack();
  });
});

describe('the forms changed and not validated (R11, ADR 115)', () => {
  it('a form followed tells the subscriber, counts apart from the writes, and its end, given twice, tells once', async () => {
    const notices: [boolean, boolean][] = [];
    const stop = subscribeWrites(() => notices.push([writesWaiting(), formsUnsaved()]));
    ends.push(stop);
    expect(formsUnsaved()).toBe(false);
    const first = trackUnsavedForm();
    const second = trackUnsavedForm();
    ends.push(first, second);
    expect([formsUnsaved(), writesWaiting(), notices]).toEqual([true, false, [[false, true], [false, true]]]);
    first();
    first();
    expect([formsUnsaved(), notices.length]).toEqual([true, 3]);
    // Nothing to send for a form: sending what waits finds every write on the host, the form still unsaved.
    expect(await sendPendingWrites()).toBe(true);
    second();
    expect([formsUnsaved(), notices.length, notices.at(-1)]).toEqual([false, 4, [false, false]]);
    stop();
  });
});
