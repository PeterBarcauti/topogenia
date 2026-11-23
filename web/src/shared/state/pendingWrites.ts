// pendingWrites.ts
// What is typed but not yet on the host, wherever it waits (the saving of a sheet, the writes of its documents): sent before the command « Quit » stops the host (spec 5.2, B-4-46), waited for before the action plan is read again (ADR 112 point 5), told to who subscribes so that the icon near the clock waits for it too (ADR 115); and, apart, the forms changed and not validated, which never leave by themselves (R11).

/** A queue of writes: whether something typed waits for the host, and the sending of what waits. */
export interface PendingWrites {
  unsaved(): boolean;
  flush(): Promise<void>;
  /** Calls the listener each time {@link unsaved} may have changed, once the queue says it truly; gives the end of it. */
  subscribe?(listener: () => void): () => void;
}

const queues = new Set<PendingWrites>();
const forms = new Set<symbol>();
const listeners = new Set<() => void>();

/** Tells each subscriber that what waits may have changed. */
function changed(): void {
  for (const listener of [...listeners]) listener();
}

/**
 * Follows a queue until the function returned is called (its panel closed); a queue still writing then is followed until
 * its last write is answered, so that what was typed just before the panel closed is still waited for.
 */
export function trackWrites(queue: PendingWrites): () => void {
  queues.add(queue);
  const unsubscribe = queue.subscribe?.(changed) ?? (() => undefined);
  changed();
  const leave = () => {
    unsubscribe();
    queues.delete(queue);
    changed();
  };
  return () => {
    if (!queue.unsaved()) {
      leave();
      return;
    }
    void queue.flush().catch(() => undefined).finally(leave);
  };
}

/** Something typed waits for the host in a queue followed. */
export function writesWaiting(): boolean {
  return [...queues].some((queue) => queue.unsaved());
}

/**
 * Follows a form open and changed, not validated (a document, a question of the facilitator, the label of a photo), until
 * the function returned is called (the form validated, given up, closed, or back as it opened). What it holds leaves only
 * when the person validates it: nothing sends it, {@link sendPendingWrites} does not wait for it (R11, ADR 115).
 */
export function trackUnsavedForm(): () => void {
  const form = Symbol('form');
  forms.add(form);
  changed();
  return () => {
    if (forms.delete(form)) changed();
  };
}

/** A form followed is changed and not validated. */
export function formsUnsaved(): boolean {
  return forms.size > 0;
}

/** Calls the listener each time {@link writesWaiting} or {@link formsUnsaved} may have changed (a queue followed, changed or left); gives the end of it. */
export function subscribeWrites(listener: () => void): () => void {
  const own = () => {
    listener();
  };
  listeners.add(own);
  return () => {
    listeners.delete(own);
  };
}

/** Sends what waits in every queue followed; true when nothing typed is left off the host. */
export async function sendPendingWrites(): Promise<boolean> {
  const followed = [...queues];
  await Promise.all(followed.map((queue) => queue.flush().catch(() => undefined)));
  return followed.every((queue) => !queue.unsaved());
}
