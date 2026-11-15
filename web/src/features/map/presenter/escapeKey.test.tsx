// escapeKey.test.tsx
// The keyboard lock on Échap and the mark of an Échap that closed something in the stage of a presentation: asked only in full screen, given back once the key is up, and the full screen left on that key told apart.

import { act, fireEvent, renderHook } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { KeyboardProvider } from '../../../shared/keyboard/KeyboardContext.tsx';
import { ESCAPE_GRACE_MS, escapeLeftFullScreen, escapeTaken, useEscapeHeld, useEscapeWatch } from './escapeKey.ts';

const lock = vi.fn(() => Promise.resolve());
const unlock = vi.fn();

/** What the browser shows full screen, and whether it can hold the keyboard. */
const browser = (options: { full: boolean; keyboard: boolean }) => {
  Object.defineProperty(document, 'fullscreenElement', { configurable: true, get: () => (options.full ? document.body : null) });
  if (options.keyboard) Object.defineProperty(navigator, 'keyboard', { configurable: true, value: { lock, unlock } });
  else Reflect.deleteProperty(navigator, 'keyboard');
};

beforeEach(() => {
  lock.mockClear();
  unlock.mockClear();
  escapeLeftFullScreen();
  browser({ full: true, keyboard: true });
});

afterEach(() => {
  Reflect.deleteProperty(document, 'fullscreenElement');
  Reflect.deleteProperty(navigator, 'keyboard');
  vi.useRealTimers();
});

const press = (type: 'keydown' | 'keyup', key = 'Escape') => {
  act(() => {
    fireEvent(window, new KeyboardEvent(type, { key }));
  });
};

/** The page has run the promise that asks the lock. */
const asked = () => act(async () => {
  await Promise.resolve();
});

describe('the keyboard lock on Échap (essai J6 of 3.2)', () => {
  it('asks the lock only in full screen and where the keyboard can be locked', async () => {
    browser({ full: false, keyboard: true });
    renderHook(() => { useEscapeHeld(); }).unmount();
    browser({ full: true, keyboard: false });
    renderHook(() => { useEscapeHeld(); }).unmount();
    await asked();
    expect(lock).not.toHaveBeenCalled();
    browser({ full: true, keyboard: true });
    const held = renderHook(() => { useEscapeHeld(); });
    await asked();
    expect(lock.mock.calls).toEqual([[['Escape']]]);
    held.unmount();
  });

  it('gives Échap back at once when the key is up as the holder goes', () => {
    renderHook(() => { useEscapeWatch(); }, { wrapper: KeyboardProvider });
    const held = renderHook(() => { useEscapeHeld(); });
    held.unmount();
    expect(unlock).toHaveBeenCalledTimes(1);
  });

  it('gives Échap back only once the key is up when the holder goes while the key is down', () => {
    renderHook(() => { useEscapeWatch(); }, { wrapper: KeyboardProvider });
    const held = renderHook(() => { useEscapeHeld(); });
    press('keydown');
    held.unmount();
    expect(unlock).not.toHaveBeenCalled();
    press('keyup', 'a');
    expect(unlock).not.toHaveBeenCalled();
    press('keyup');
    expect(unlock).toHaveBeenCalledTimes(1);
    press('keyup');
    expect(unlock).toHaveBeenCalledTimes(1);
  });

  it('gives Échap back after the last of two holders only', () => {
    renderHook(() => { useEscapeWatch(); }, { wrapper: KeyboardProvider });
    const first = renderHook(() => { useEscapeHeld(); });
    const second = renderHook(() => { useEscapeHeld(); });
    first.unmount();
    expect(unlock).not.toHaveBeenCalled();
    second.unmount();
    expect(unlock).toHaveBeenCalledTimes(1);
  });

  it('counts the key as up when the window loses the focus: its release is never heard', () => {
    renderHook(() => { useEscapeWatch(); }, { wrapper: KeyboardProvider });
    const held = renderHook(() => { useEscapeHeld(); });
    press('keydown');
    held.unmount();
    expect(unlock).not.toHaveBeenCalled();
    act(() => {
      window.dispatchEvent(new Event('blur'));
    });
    expect(unlock).toHaveBeenCalledTimes(1);
  });
});

describe('the full screen left on the Échap that closed something (essai J6 of 3.2)', () => {
  beforeEach(() => {
    vi.useFakeTimers({ toFake: ['performance'] });
  });

  it('is told once within the grace after the mark, and not after it', () => {
    escapeTaken();
    vi.advanceTimersByTime(ESCAPE_GRACE_MS - 1);
    expect([escapeLeftFullScreen(), escapeLeftFullScreen()]).toEqual([true, false]);
    escapeTaken();
    vi.advanceTimersByTime(ESCAPE_GRACE_MS + 1);
    expect(escapeLeftFullScreen()).toBe(false);
  });

  it('is told for no one without a mark', () => {
    expect(escapeLeftFullScreen()).toBe(false);
  });

  it('has its grace renewed by an Échap seen meanwhile, a key held or its release', () => {
    renderHook(() => { useEscapeWatch(); }, { wrapper: KeyboardProvider });
    escapeTaken();
    vi.advanceTimersByTime(ESCAPE_GRACE_MS - 500);
    press('keyup');
    vi.advanceTimersByTime(ESCAPE_GRACE_MS - 500);
    expect(escapeLeftFullScreen()).toBe(true);
  });

  it('is not renewed by another key, nor by an Échap seen after the grace is over', () => {
    renderHook(() => { useEscapeWatch(); }, { wrapper: KeyboardProvider });
    escapeTaken();
    vi.advanceTimersByTime(ESCAPE_GRACE_MS - 500);
    press('keyup', 'a');
    vi.advanceTimersByTime(1000);
    press('keyup');
    expect(escapeLeftFullScreen()).toBe(false);
  });

  it('is forgotten when the stage goes', () => {
    const watch = renderHook(() => { useEscapeWatch(); }, { wrapper: KeyboardProvider });
    escapeTaken();
    watch.unmount();
    expect(escapeLeftFullScreen()).toBe(false);
  });
});
