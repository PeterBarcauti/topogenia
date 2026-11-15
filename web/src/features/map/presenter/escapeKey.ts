// escapeKey.ts
// Échap while something of the stage of a presentation is open (the comment written, the question or the settings of the voice): held from the browser in full screen where it can be, until the key is up; and the mark an Échap that closes something leaves, for the full screen the browser leaves on that same key to keep the presentation (essai J6 of 3.2).

import { useEffect } from 'react';
import { useKeyWatch } from '../../../shared/keyboard/KeyboardContext.tsx';

interface KeyboardLock {
  readonly lock?: (keys: string[]) => Promise<void>;
  readonly unlock?: () => void;
}

const keyboardOf = (): KeyboardLock | undefined => (navigator as Navigator & { readonly keyboard?: KeyboardLock }).keyboard;

/**
 * How long after Échap closed something a full screen left still belongs to that key: a browser leaves the full screen
 * on the key pressed or on its release, and tells the page once its window is back, after an animation on some systems.
 */
export const ESCAPE_GRACE_MS = 2000;

/** What is open in the stage that holds Échap from the browser: the keyboard is one for the page, as its lock. */
let holders = 0;

/** Échap is asked from the browser, and not given back yet. */
let held = false;

/** Échap is down, as far as the page saw it. */
let down = false;

/** When Échap last closed something of the stage, or was last seen of that same press; long ago without one. */
let takenAt = Number.NEGATIVE_INFINITY;

/**
 * Gives Échap back to the browser once nothing holds it and the key is up. Given back while the key is still down, the
 * browser takes its release for an Échap of its own and leaves the full screen: the presentation then ended on the
 * Échap that had just closed the field of a comment (essai J6).
 */
function release(): void {
  if (!held || holders > 0 || down) return;
  held = false;
  keyboardOf()?.unlock?.();
}

/**
 * While something of the stage is open that Échap must close (the comment written, the question or the settings of the
 * voice), Échap stays with the page in full screen when the browser can hold it (the keyboard lock of Chromium): it
 * closes what is open, not the full screen. Where it cannot, Échap leaves the full screen and the presentation stays
 * (`useFullscreen`).
 */
export function useEscapeHeld(): void {
  useEffect(() => {
    const keyboard = keyboardOf();
    if (document.fullscreenElement === null || keyboard?.lock === undefined) return undefined;
    holders += 1;
    held = true;
    Promise.resolve().then(() => keyboard.lock?.(['Escape'])).catch(() => undefined);
    return () => {
      holders -= 1;
      release();
    };
  }, []);
}

const fresh = (): boolean => {
  const since = performance.now() - takenAt;
  return since >= 0 && since <= ESCAPE_GRACE_MS;
};

/** Échap has just closed something of the stage, or begun to (a text on its way to the host): told by what it closed. */
export function escapeTaken(): void {
  takenAt = performance.now();
}

/**
 * Whether the full screen just left was left on the Échap that closed something of the stage: the presentation then
 * stays, in its window. Said once: the next full screen left is the doing of the person, or of another key.
 */
export function escapeLeftFullScreen(): boolean {
  const left = fresh();
  takenAt = Number.NEGATIVE_INFINITY;
  return left;
}

/** Échap seen pressed or released: whether it is down; the mark of the press that closed something lives until its release. */
function seen(event: KeyboardEvent): void {
  if (event.key !== 'Escape') return;
  down = event.type === 'keydown';
  if (fresh()) takenAt = performance.now();
  if (!down) release();
}

/** The key counts as up: a window that loses the focus never hears its release. */
function away(): void {
  down = false;
  release();
}

/**
 * Follows Échap while the stage is shown, through the one keyboard manager, which lets it see the keys typed in a
 * field too (`Keyboard.watch`): whether the key is down, for its lock to be given back once it is up; and the press
 * that closed something, whose mark lives until its release (a key held, its repeats).
 */
export function useEscapeWatch(): void {
  useKeyWatch(seen);
  useEffect(() => {
    window.addEventListener('blur', away);
    return () => {
      window.removeEventListener('blur', away);
      takenAt = Number.NEGATIVE_INFINITY;
      away();
    };
  }, []);
}
