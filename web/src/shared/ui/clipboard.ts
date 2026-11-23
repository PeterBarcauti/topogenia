// clipboard.ts
// The clipboard of the browser, written by a command of the interface: a text, or an image PNG written in the gesture of the person while it is still being made; true when it is there, false when the browser refuses.

/** Copies a text to the clipboard; false when the browser refuses. */
export async function copyText(text: string): Promise<boolean> {
  try {
    await navigator.clipboard.writeText(text);
    return true;
  } catch {
    return false;
  }
}

/** The type of the image written. */
const PNG_TYPE = 'image/png';

/**
 * Copies an image PNG to the clipboard (ADR 109). The image is asked for and the writing sent before anything is awaited:
 * the browser sees the writing in the gesture of the person, the image given to it as a promise while it is made (Firefox
 * accepts a writing only a few seconds after the click). A browser that refuses a promise is given the finished image.
 * False without the means to write an image (no image is then made), when the browser refuses, or when the image fails.
 */
export async function copyImage(make: () => Promise<Blob>): Promise<boolean> {
  // Out of a secure context the browser gives no clipboard; an older one has no ClipboardItem.
  const clipboard = (navigator as Partial<Navigator>).clipboard;
  if (typeof ClipboardItem === 'undefined' || clipboard === undefined) return false;
  const image = make();
  try {
    await clipboard.write([new ClipboardItem({ [PNG_TYPE]: image })]);
    return true;
  } catch {
    try {
      const finished = await image;
      await clipboard.write([new ClipboardItem({ [PNG_TYPE]: finished })]);
      return true;
    } catch {
      return false;
    }
  }
}
