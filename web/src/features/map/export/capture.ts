// capture.ts
// The image of the whole map for the PNG and PDF exports: the bounds of its nodes without the frame, the margin, the pixel ratio, the elements left out, the wait for the steps drawn, the fonts of the page, the capture by html-to-image loaded on demand (annex B 2.14, S-5.4-04, ADR 066).

import type { MapNode } from '../canvas/flowElements.ts';

/** A box on the map, in its own coordinates. */
export interface Bounds {
  readonly x: number;
  readonly y: number;
  readonly width: number;
  readonly height: number;
}

/** The margin round the map in the image, in pixels (B-2.14-05). */
export const CAPTURE_MARGIN = 24;

/** The height added under the map, as the 1.x added it (B-2.14-05). */
export const EXTRA_HEIGHT = 20;

/** The line of the classification under the map of a PNG (S-4.1-08, AR-127). */
export const CAPTION_HEIGHT = 24;

/** The longest wait for the steps to be drawn, then the pause before the capture, in milliseconds (B-2.14-03). */
export const STEPS_WAIT = 3_000;
export const SETTLE_WAIT = 50;

/** What the canvas gives the exports while it is shown: its element and the bounds of its nodes. */
export interface CaptureTarget {
  readonly element: HTMLElement;
  readonly bounds: Bounds | null;
}

/** The box of the nodes drawn, the frame left out (B-2.9-20): the lanes from the margin of their headers, the steps; null without any. */
export function mapBounds(nodes: readonly MapNode[]): Bounds | null {
  const drawn = nodes.filter((node) => node.type !== 'frame');
  if (drawn.length === 0) return null;
  const left = Math.min(...drawn.map((node) => node.position.x));
  const top = Math.min(...drawn.map((node) => node.position.y));
  const right = Math.max(...drawn.map((node) => node.position.x + (node.width ?? 0)));
  const bottom = Math.max(...drawn.map((node) => node.position.y + (node.height ?? 0)));
  return { x: left, y: top, width: right - left, height: bottom - top };
}

/** The size of the image and the move of the view that brings the bounds into it past the margin; the caption adds its line. */
export interface CaptureFrame {
  readonly width: number;
  readonly height: number;
  readonly transform: string;
}

/** The frame of the image: the bounds, a margin of 24 round them, 20 more pixels in height (B-2.14-05). */
export function captureFrame(bounds: Bounds, caption = false): CaptureFrame {
  return {
    width: Math.ceil(bounds.width + 2 * CAPTURE_MARGIN),
    height: Math.ceil(bounds.height + 2 * CAPTURE_MARGIN + EXTRA_HEIGHT + (caption ? CAPTION_HEIGHT : 0)),
    transform: `translate(${String(CAPTURE_MARGIN - bounds.x)}px, ${String(CAPTURE_MARGIN - bounds.y)}px) scale(1)`,
  };
}

/** Where the line of the classification stands: under the map, from its left edge (S-4.1-08). */
export const captionPosition = (bounds: Bounds): { readonly x: number; readonly y: number } => ({ x: bounds.x, y: bounds.y + bounds.height + EXTRA_HEIGHT });

/** Twice the resolution, bounded to stay under the limit of the canvases of the browsers (B-2.14-06). */
export function pixelRatio(width: number, height: number, target = 2, maximum = 12_000): number {
  return Math.max(0.5, Math.min(target, maximum / width, maximum / height));
}

/** The elements left out of the image: the « + » and their hover zone, the handles, the gauges, the frame (B-2.14-07, B-2.10-26, B-2.9-20). */
export const LEFT_OUT: readonly string[] = ['quick-add', 'quick-add-bridge', 'react-flow__handle', 'step-gauge', 'react-flow__node-frame'];

/** Whether an element of the view goes into the image. */
export const keptInCapture = (node: Node): boolean => !(node instanceof HTMLElement && LEFT_OUT.some((name) => node.classList.contains(name)));

/** The clock of the wait: the page's by default, the tests give theirs. */
export interface WaitClock {
  readonly now: () => number;
  readonly frame: () => Promise<void>;
  readonly pause: (milliseconds: number) => Promise<void>;
}

const PAGE_CLOCK: WaitClock = {
  now: () => Date.now(),
  frame: () => new Promise((resolve) => {
    requestAnimationFrame(() => {
      resolve();
    });
  }),
  pause: (milliseconds) => new Promise((resolve) => {
    setTimeout(resolve, milliseconds);
  }),
};

/** Waits a frame at a time, 3 s at most, for every step to be drawn in the state of the export, then 50 ms (B-2.14-03). */
export async function waitForSteps(root: ParentNode, count: number, clock: WaitClock = PAGE_CLOCK): Promise<void> {
  const start = clock.now();
  while (clock.now() - start < STEPS_WAIT) {
    await clock.frame();
    if (root.querySelectorAll('.react-flow__node-step').length >= count) break;
  }
  await clock.pause(SETTLE_WAIT);
}

/** The image captured: the canvas drawn at its pixel ratio, and its size before it. */
export interface Captured {
  readonly canvas: HTMLCanvasElement;
  readonly width: number;
  readonly height: number;
}

/** The bytes turned into text a slice at a time, under the limit of the arguments of a call. */
const SLICE = 0x8000;

/** A file of the application as a data address, read by `fetch` (`connect-src 'self'`) and written in base 64. */
async function dataUrlOf(address: string, read: typeof fetch): Promise<string> {
  const reply = await read(address);
  if (!reply.ok) throw new Error(`The file ${address} could not be read.`);
  const bytes = new Uint8Array(await reply.arrayBuffer());
  let binary = '';
  for (let start = 0; start < bytes.length; start += SLICE) binary += String.fromCharCode(...bytes.subarray(start, start + SLICE));
  return `data:${reply.headers.get('Content-Type') ?? 'application/octet-stream'};base64,${btoa(binary)}`;
}

/** A face of a font with the files it names written in data addresses; null when one of them cannot be read (the text falls back). */
async function embeddedFace(css: string, base: string, files: Map<string, Promise<string>>, read: typeof fetch): Promise<string | null> {
  let embedded = css;
  try {
    for (const match of css.matchAll(/url\(\s*(['"]?)([^'")]+)\1\s*\)/g)) {
      const address = new URL(match[2] ?? '', base).href;
      if (address.startsWith('data:')) continue;
      if (!files.has(address)) files.set(address, dataUrlOf(address, read));
      embedded = embedded.replace(match[0], `url("${await (files.get(address) ?? '')}")`);
    }
  } catch {
    return null;
  }
  return embedded;
}

/**
 * The fonts of the page as CSS whose files are data addresses: the image is an SVG drawn as a picture, which loads no file
 * of its own. html-to-image would read them itself by setting a base address in a document of its own, which the policy
 * of the host refuses (`base-uri 'none'`): they are read here, each address resolved against its style sheet by `URL`.
 */
export async function fontFaces(sheets: Iterable<CSSStyleSheet> = document.styleSheets, read: typeof fetch = fetch): Promise<string> {
  const faces: { readonly css: string; readonly base: string }[] = [];
  for (const sheet of sheets) {
    let rules: CSSRuleList;
    try {
      rules = sheet.cssRules;
    } catch {
      // A sheet of another origin cannot be read; the application serves none.
      continue;
    }
    for (const rule of rules) if (rule instanceof CSSFontFaceRule) faces.push({ css: rule.cssText, base: sheet.href ?? document.baseURI });
  }
  const files = new Map<string, Promise<string>>();
  const embedded = await Promise.all(faces.map((face) => embeddedFace(face.css, face.base, files, read)));
  return embedded.filter((face): face is string => face !== null).join('\n');
}

/** What the capture needs besides the view: the background of the charter (`--surface`), the fonts of the page, whether the caption has its line. */
export interface CaptureOptions {
  readonly background: string;
  readonly fonts: string;
  readonly caption: boolean;
}

/**
 * The view of React Flow drawn whole into a canvas by html-to-image loaded on demand (B-0-04): on the background of the
 * charter, framed on the bounds, at twice the resolution at most, without the elements left out, with the fonts read by
 * {@link fontFaces} (B-2.14-04 to B-2.14-07, S-5.4-04).
 */
export async function captureView(view: HTMLElement, bounds: Bounds, options: CaptureOptions): Promise<Captured> {
  const frame = captureFrame(bounds, options.caption);
  const { toCanvas } = await import('html-to-image');
  const canvas = await toCanvas(view, {
    ...(options.background.trim() === '' ? {} : { backgroundColor: options.background.trim() }),
    width: frame.width,
    height: frame.height,
    pixelRatio: pixelRatio(frame.width, frame.height),
    style: { width: `${String(frame.width)}px`, height: `${String(frame.height)}px`, transform: frame.transform },
    filter: keptInCapture,
    fontEmbedCSS: options.fonts,
  });
  return { canvas, width: frame.width, height: frame.height };
}

/** The PNG of a canvas, as a file to save (B-2.14-12). */
export function pngOf(canvas: HTMLCanvasElement): Promise<Blob> {
  return new Promise((resolve, reject) => {
    canvas.toBlob((blob) => {
      if (blob === null) reject(new Error('The canvas gave no image.'));
      else resolve(blob);
    }, 'image/png');
  });
}
