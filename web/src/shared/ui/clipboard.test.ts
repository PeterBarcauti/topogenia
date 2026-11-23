// clipboard.test.ts
// The image written to the clipboard: one item image/png in the turn of the call, its image given later; the finished image when the browser refuses a promise; false without ClipboardItem, or when the browser refuses (lot 1 of the 3.4, ADR 109).

import { copyImage } from './clipboard.ts';

afterEach(() => {
  vi.unstubAllGlobals();
});

const PNG = new Blob(['png'], { type: 'image/png' });

type Part = Blob | Promise<Blob>;

/** An item of the clipboard as the browser builds it, its parts as given; a browser that takes no promise throws here. */
function itemClass(takesPromises = true) {
  return class FakeItem {
    readonly parts: Record<string, Part>;
    constructor(parts: Record<string, Part>) {
      if (!takesPromises && Object.values(parts).some((part) => part instanceof Promise)) throw new TypeError('A promise is not a blob.');
      this.parts = parts;
    }
  };
}

/** A clipboard that waits for the parts of its items, as the browser does, and keeps the images written; refuses when told. */
function clipboardOf(refuses = false) {
  const written: Blob[] = [];
  const write = vi.fn(async (items: readonly { parts: Record<string, Part> }[]) => {
    if (refuses) throw new DOMException('Refused.', 'NotAllowedError');
    for (const item of items) written.push(await (item.parts['image/png'] ?? Promise.reject(new Error('No image.'))));
  });
  vi.stubGlobal('navigator', { clipboard: { write } });
  return { written, write };
}

describe('copyImage', () => {
  it('copyImage writes one image/png item in the turn of the call, its image given later', async () => {
    vi.stubGlobal('ClipboardItem', itemClass());
    const { written, write } = clipboardOf();
    let give: (blob: Blob) => void = () => undefined;
    const image = new Promise<Blob>((resolve) => {
      give = resolve;
    });
    const copied = copyImage(() => image);
    // Nothing awaited yet: the browser sees the write in the gesture of the person.
    expect(write).toHaveBeenCalledTimes(1);
    const items = write.mock.calls[0]?.[0] ?? [];
    expect([items.length, Object.keys(items[0]?.parts ?? {}), items[0]?.parts['image/png']]).toEqual([1, ['image/png'], image]);
    expect(written).toEqual([]);
    give(PNG);
    expect(await copied).toBe(true);
    expect(written).toEqual([PNG]);
  });

  it('copyImage writes the finished image when the browser refuses a promise', async () => {
    vi.stubGlobal('ClipboardItem', itemClass(false));
    const { written, write } = clipboardOf();
    const make = vi.fn(() => Promise.resolve(PNG));
    expect(await copyImage(make)).toBe(true);
    expect([written, make.mock.calls.length, write.mock.calls[0]?.[0][0]?.parts['image/png']]).toEqual([[PNG], 1, PNG]);
    // A browser that builds the item but whose writing refuses a promise: the finished image is written after it.
    vi.stubGlobal('ClipboardItem', itemClass());
    const later = { written: [] as Blob[] };
    const write2 = vi.fn((items: readonly { parts: Record<string, Part> }[]) => {
      const part = items[0]?.parts['image/png'];
      if (!(part instanceof Blob)) return Promise.reject(new TypeError('A promise is not a blob.'));
      later.written.push(part);
      return Promise.resolve();
    });
    vi.stubGlobal('navigator', { clipboard: { write: write2 } });
    expect(await copyImage(() => Promise.resolve(PNG))).toBe(true);
    expect([later.written, write2.mock.calls.length]).toEqual([[PNG], 2]);
  });

  it('copyImage says false without ClipboardItem, or when the browser refuses', async () => {
    vi.stubGlobal('ClipboardItem', undefined);
    const without = clipboardOf();
    const make = vi.fn(() => Promise.resolve(PNG));
    expect(await copyImage(make)).toBe(false);
    // Without a way to write an image, no image is made.
    expect([without.write.mock.calls.length, make.mock.calls.length]).toEqual([0, 0]);
    vi.stubGlobal('ClipboardItem', itemClass());
    vi.stubGlobal('navigator', {});
    expect(await copyImage(make)).toBe(false);
    const refused = clipboardOf(true);
    expect(await copyImage(() => Promise.resolve(PNG))).toBe(false);
    expect(refused.written).toEqual([]);
    // The image that could not be made: refused, never thrown.
    const failed = clipboardOf();
    expect(await copyImage(() => Promise.reject(new Error('The map has nothing to draw.')))).toBe(false);
    expect(failed.written).toEqual([]);
  });
});
