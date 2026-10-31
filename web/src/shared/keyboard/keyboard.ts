// keyboard.ts
// The one keyboard manager of the interface: a single listener and its layers of priority, without React (spec 3.5, annex B 2.5).

/**
 * The layers below the input field, in their order of priority (S-3.5-02): a modal floating element takes every key, a
 * non-modal panel leaves the canvas its shortcuts (B-2.5-04). The input field comes first and needs no registration:
 * a key typed in a field is the field's own (B-2.5-02).
 */
export type KeyLayer = 'modal' | 'panel' | 'canvas';

/** Answers a key pressed or released; true when the key was used, which stops it there (no default action, no further handler). */
export type KeyHandler = (event: KeyboardEvent, chord: string) => boolean;

const MODIFIERS = new Set(['Control', 'Shift', 'Alt', 'Meta', 'AltGraph', 'CapsLock']);

const KEY_NAMES: Readonly<Record<string, string>> = { ' ': 'Space', Esc: 'Escape', Del: 'Delete', Left: 'ArrowLeft', Right: 'ArrowRight', Up: 'ArrowUp', Down: 'ArrowDown' };

/**
 * The chord of a key event (`Ctrl+Shift+Z`, `F2`, `ArrowLeft`, `Space`): modifiers in a fixed order, a letter in upper
 * case; `metaKey` counts as Ctrl so that the shortcuts work on a Mac (B-2.5-35). Null for a modifier pressed alone.
 */
export function chordOf(event: Pick<KeyboardEvent, 'key' | 'ctrlKey' | 'metaKey' | 'altKey' | 'shiftKey'>): string | null {
  if (MODIFIERS.has(event.key) || event.key === '' || event.key === 'Unidentified') return null;
  const key = KEY_NAMES[event.key] ?? (event.key.length === 1 ? event.key.toUpperCase() : event.key);
  const parts: string[] = [];
  if (event.ctrlKey || event.metaKey) parts.push('Ctrl');
  if (event.altKey) parts.push('Alt');
  if (event.shiftKey) parts.push('Shift');
  parts.push(key);
  return parts.join('+');
}

/** Whether the focus is in a field that takes the keys: text, area, list, editable content (B-2.5-02). */
export function isInputField(target: EventTarget | null): boolean {
  if (!(target instanceof HTMLElement)) return false;
  return ['INPUT', 'TEXTAREA', 'SELECT'].includes(target.tagName) || target.closest('[contenteditable="true"], [contenteditable=""], [contenteditable="plaintext-only"]') !== null;
}

export interface Keyboard {
  /** Registers a handler on a layer; the last one registered on a layer is asked first. Gives the function that removes it. */
  add(layer: KeyLayer, handler: KeyHandler): () => void;
  /** A chord heard everywhere, even in an input field or under a modal element (Ctrl+S, B-2.5-05), on the key pressed. */
  addGlobal(chord: string, run: () => void): () => void;
  /** Hands a key event to the layers; the listener of {@link Keyboard.attach} calls it. */
  dispatch(event: KeyboardEvent): void;
  /** Listens to a window in the capture phase, before any other listener (the canvas, B-2.5-21); gives the function that stops it. */
  attach(target: Window): () => void;
}

function stop(event: KeyboardEvent): void {
  event.preventDefault();
  event.stopPropagation();
}

/** A keyboard manager with its own layers (one per application; each test makes its own). */
export function createKeyboard(): Keyboard {
  const layers: Record<KeyLayer, KeyHandler[]> = { modal: [], panel: [], canvas: [] };
  const globals = new Map<string, () => void>();

  const remove = (list: KeyHandler[], handler: KeyHandler) => () => {
    const index = list.lastIndexOf(handler);
    if (index >= 0) list.splice(index, 1);
  };

  const ask = (handlers: readonly KeyHandler[], event: KeyboardEvent, chord: string): boolean =>
    [...handlers].reverse().some((handler) => handler(event, chord));

  const dispatch = (event: KeyboardEvent): void => {
    const chord = chordOf(event);
    if (chord === null) return;
    const global = globals.get(chord);
    if (global !== undefined) {
      if (event.type === 'keydown') {
        stop(event);
        global();
      }
      return;
    }
    if (isInputField(event.target)) return;
    const modal = layers.modal.at(-1);
    if (modal !== undefined) {
      if (modal(event, chord)) stop(event);
      return;
    }
    if (ask(layers.panel, event, chord) || ask(layers.canvas, event, chord)) stop(event);
  };

  return {
    add(layer, handler) {
      layers[layer].push(handler);
      return remove(layers[layer], handler);
    },
    addGlobal(chord, run) {
      globals.set(chord, run);
      return () => {
        if (globals.get(chord) === run) globals.delete(chord);
      };
    },
    dispatch,
    attach(target) {
      const listener = (event: KeyboardEvent) => {
        dispatch(event);
      };
      target.addEventListener('keydown', listener, true);
      target.addEventListener('keyup', listener, true);
      return () => {
        target.removeEventListener('keydown', listener, true);
        target.removeEventListener('keyup', listener, true);
      };
    },
  };
}
