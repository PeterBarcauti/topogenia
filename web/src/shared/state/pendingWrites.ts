// pendingWrites.ts
// What is typed but not yet on the host, wherever it waits (the saving of a sheet): sent before the command « Quit » stops the host (spec 5.2, B-4-46).

/** A queue of writes: whether something typed waits for the host, and the sending of what waits. */
export interface PendingWrites {
  unsaved(): boolean;
  flush(): Promise<void>;
}

const queues = new Set<PendingWrites>();

/** Follows a queue until the function returned is called (its panel closed). */
export function trackWrites(queue: PendingWrites): () => void {
  queues.add(queue);
  return () => {
    queues.delete(queue);
  };
}

/** Sends what waits in every queue followed; true when nothing typed is left off the host. */
export async function sendPendingWrites(): Promise<boolean> {
  const followed = [...queues];
  await Promise.all(followed.map((queue) => queue.flush().catch(() => undefined)));
  return followed.every((queue) => !queue.unsaved());
}
