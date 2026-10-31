// KeyboardContext.tsx
// The keyboard manager given to the components: attached to the window once, and the hooks that register on its layers.

import { createContext, useContext, useEffect, useRef, useState, type ReactNode } from 'react';
import { createKeyboard, type KeyHandler, type KeyLayer, type Keyboard } from './keyboard.ts';

const KeyboardContext = createContext<Keyboard | null>(null);

/** Gives a keyboard manager to the application and listens to the window while it is shown. */
export function KeyboardProvider({ keyboard, children }: { keyboard?: Keyboard; children: ReactNode }) {
  const [manager] = useState(() => keyboard ?? createKeyboard());
  useEffect(() => manager.attach(window), [manager]);
  return <KeyboardContext.Provider value={manager}>{children}</KeyboardContext.Provider>;
}

export function useKeyboard(): Keyboard {
  const keyboard = useContext(KeyboardContext);
  if (keyboard === null) throw new Error('useKeyboard outside KeyboardProvider.');
  return keyboard;
}

/**
 * Registers a handler on a layer while the component is shown and `active` holds; the handler read is always the last
 * one rendered, so it sees the state of the moment.
 */
export function useKeyLayer(layer: KeyLayer, handler: KeyHandler, active = true): void {
  const keyboard = useKeyboard();
  const latest = useRef(handler);
  useEffect(() => {
    latest.current = handler;
  });
  useEffect(() => (active ? keyboard.add(layer, (event, chord) => latest.current(event, chord)) : undefined), [keyboard, layer, active]);
}

/** A chord heard everywhere while the component is shown (Ctrl+S, B-2.5-05). */
export function useGlobalShortcut(chord: string, run: () => void): void {
  const keyboard = useKeyboard();
  const latest = useRef(run);
  useEffect(() => {
    latest.current = run;
  });
  useEffect(() => keyboard.addGlobal(chord, () => {
    latest.current();
  }), [keyboard, chord]);
}
