// clipboard.ts
// The clipboard of the browser, written by a command of the interface: true when the text is there, false when the browser refuses.

/** Copies a text to the clipboard; false when the browser refuses. */
export async function copyText(text: string): Promise<boolean> {
  try {
    await navigator.clipboard.writeText(text);
    return true;
  } catch {
    return false;
  }
}
