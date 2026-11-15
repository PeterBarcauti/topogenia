// flowDom.ts
// What the canvas needs from a page and jsdom does not give: ResizeObserver, DOMMatrixReadOnly, a size for the container of React Flow, pointer capture, no canvas to measure.

/** The size of the container of React Flow in the tests, as a window of 1000 × 600 would give it. */
export const FLOW_SIZE = { width: 1000, height: 600 } as const;

class StillResizeObserver {
  observe(): void {
    // Nothing is ever resized in the tests.
  }

  unobserve(): void {
    // Nothing to stop.
  }

  disconnect(): void {
    // Nothing to stop.
  }
}

class IdentityMatrix {
  readonly m22 = 1;
}

/** React Flow measures its pane (`react-flow__renderer`), which fills its container. */
const sizeOf = (element: HTMLElement, side: 'width' | 'height'): number =>
  element.classList.contains('react-flow') || element.classList.contains('react-flow__renderer') ? FLOW_SIZE[side] : 0;

/** Installs the stand-ins once for every test file (jsdom only). */
export function installFlowDom(): void {
  if (!('ResizeObserver' in window)) Object.defineProperty(window, 'ResizeObserver', { configurable: true, writable: true, value: StillResizeObserver });
  if (!('DOMMatrixReadOnly' in window)) Object.defineProperty(window, 'DOMMatrixReadOnly', { configurable: true, writable: true, value: IdentityMatrix });
  // jsdom lays nothing out and calls every element invisible: React Flow would never measure its container.
  Object.defineProperty(Element.prototype, 'checkVisibility', { configurable: true, writable: true, value: () => true });
  Object.defineProperty(HTMLElement.prototype, 'offsetWidth', { configurable: true, get(this: HTMLElement) { return sizeOf(this, 'width'); } });
  Object.defineProperty(HTMLElement.prototype, 'offsetHeight', { configurable: true, get(this: HTMLElement) { return sizeOf(this, 'height'); } });
  // jsdom does not capture the pointer: the drag of a lane header keeps its events on the header in the tests anyway.
  Object.defineProperty(Element.prototype, 'setPointerCapture', { configurable: true, writable: true, value: () => undefined });
  Object.defineProperty(Element.prototype, 'releasePointerCapture', { configurable: true, writable: true, value: () => undefined });
  // jsdom has no canvas: the measure of the texts falls back to its estimate, without the error jsdom would print.
  Object.defineProperty(HTMLCanvasElement.prototype, 'getContext', { configurable: true, writable: true, value: () => null });
}
