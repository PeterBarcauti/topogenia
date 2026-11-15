// presenterStage.tsx
// What the tests of the comment of presentation share: the stage opened on the second step of the sample, the keys and the field, the full screen as a browser tells it, the comments read late.

import { act, fireEvent, screen, waitFor } from '@testing-library/react';
import { onTestFinished, vi } from 'vitest';
import { REFUSAL_READ_MS } from '../features/map/presenter/comments.ts';
import { memoryComments, showEditorWith } from './editor.tsx';
import { MAP_ID, sampleMap } from './maps.ts';

export const key = (name: string, options: Partial<KeyboardEventInit> = {}) => {
  act(() => {
    fireEvent.keyDown(window, { key: name, ...options });
  });
};

export const line = () => screen.queryByTestId('presenter-line')?.textContent ?? null;

export const field = () => screen.getByTestId<HTMLTextAreaElement>('presenter-comment-field');

/** Waits until the field of the comment is closed. */
export const closed = () => waitFor(() => {
  expect(screen.queryByTestId('presenter-comment-editor')).toBeNull();
});

const clock = { ahead: 0, running: false };

/**
 * Lets the time pass that a refusal must have been shown before a gesture gives its text up (`REFUSAL_READ_MS`): from
 * now on the clock of the page says that much more, until the test ends.
 */
export function afterReading(): void {
  if (!clock.running) {
    const real = performance.now.bind(performance);
    const spy = vi.spyOn(performance, 'now').mockImplementation(() => real() + clock.ahead);
    clock.running = true;
    onTestFinished(() => {
      spy.mockRestore();
      clock.ahead = 0;
      clock.running = false;
    });
  }
  clock.ahead += REFUSAL_READ_MS;
}

/** The full screen of the document, as a browser tells it: entered on the stage, left with its event. */
export function fullScreen() {
  const full: { element: Element | null } = { element: screen.getByTestId('presenter') };
  Object.defineProperty(document, 'fullscreenElement', { configurable: true, get: () => full.element });
  return {
    enter: () => {
      full.element = screen.getByTestId('presenter');
    },
    leave: () => {
      full.element = null;
      act(() => {
        document.dispatchEvent(new Event('fullscreenchange'));
      });
    },
    restore: () => {
      Reflect.deleteProperty(document, 'fullscreenElement');
    },
  };
}

/** Types in the field of the comment, then a key on it: the keys of the field stay with it. */
export const typeIn = (text: string, end = 'Enter', options: Partial<KeyboardEventInit> = {}) => {
  act(() => {
    fireEvent.change(field(), { target: { value: text } });
  });
  act(() => {
    fireEvent.keyDown(field(), { key: end, ...options });
  });
};

/** The stage open on the step s2 of the sample, paused, its comments read; the map shown in a language when one is given. */
export async function onTheSecondStep(comments = memoryComments(), map = sampleMap(), language?: string) {
  const shown = await showEditorWith({ comments: comments.source }, map);
  if (language !== undefined) {
    act(() => {
      shown.editor.store.getState().show({ language, origin: map.languages.origin, locked: true });
    });
  }
  key('F5');
  await waitFor(() => {
    expect(comments.reads).toEqual([MAP_ID]);
  });
  key('ArrowRight');
  key('ArrowRight');
  return { ...shown, comments };
}

/** Comments whose reading waits until the test lets it go, after the first readings said to answer at once: a slow link (R2-08, R1-02). */
export function lateComments(given: Parameters<typeof memoryComments>[0], atOnce = 0) {
  const comments = memoryComments(given);
  const gate: { open: () => void; asked: number } = { open: () => undefined, asked: 0 };
  const opened = new Promise<void>((resolve) => {
    gate.open = resolve;
  });
  const read: typeof comments.source.read = async (mapId, signal) => {
    if (gate.asked++ >= atOnce) await opened;
    return comments.source.read(mapId, signal);
  };
  return { ...comments, source: { ...comments.source, read }, release: gate.open };
}
