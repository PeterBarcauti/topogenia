// PresenterKeys.test.tsx
// The keyboard and the focus of the stage of a presentation, a modal dialog (relecture 6: R6-01, R6-05): the focus enters it and stays in it; the question and the settings of the voice are never hidden, take the focus and let no key through to the story, the page under it nor the browser; Tab walks its controls, Entrée and Espace then press the one focused; Ctrl+R and Ctrl+F5 stay those of the browser.

import { act, fireEvent, screen } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { i18next } from '../../../shared/i18n/i18n.ts';
import { showEditor } from '../../../test/editor.tsx';
import { sampleMap } from '../../../test/maps.ts';
import { fakeSpeech } from '../../../test/speech.ts';

beforeEach(async () => {
  await i18next.changeLanguage('fr');
});

afterEach(() => {
  vi.unstubAllGlobals();
  vi.useRealTimers();
});

/** A key pressed where the focus is, as a keyboard sends it; says whether the page stopped it there: no default action of the browser. */
const press = (name: string, options: Partial<KeyboardEventInit> = {}): boolean => {
  let stopped = false;
  act(() => {
    stopped = !fireEvent.keyDown(document.activeElement ?? document.body, { key: name, ...options });
  });
  return stopped;
};

/** A key released where the focus is. */
const release = (name: string) => {
  act(() => {
    fireEvent.keyUp(document.activeElement ?? document.body, { key: name });
  });
};

/** The browser tells the page that the full screen changed: `full.element` is what it now shows full screen. */
const fullscreenChanged = () => {
  act(() => {
    document.dispatchEvent(new Event('fullscreenchange'));
  });
};

const click = (testId: string) => {
  act(() => {
    screen.getByTestId(testId).click();
  });
};

/** The host's answer read: the project of these tests lets the online voices be offered, once the presentation has asked (F-137). */
const settle = () => act(async () => {
  await Promise.resolve();
});

describe('the question and the settings of the voice, never hidden, never letting a key through (relecture 6: R6-01)', () => {
  it('shows the question asked by V in a bar that stays visible and over no blank screen, the focus in it', async () => {
    fakeSpeech();
    const { editor } = await showEditor(sampleMap());
    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] });
    press('F5');
    press(' ');
    await settle();
    const stage = screen.getByTestId('presenter');
    act(() => {
      vi.advanceTimersByTime(3500);
    });
    expect(stage).toHaveClass('is-idle');
    press('v');
    expect([stage.classList.contains('is-idle'), document.activeElement]).toEqual([false, screen.getByTestId('presenter-voice-question')]);
    act(() => {
      vi.advanceTimersByTime(3500);
    });
    expect(stage).not.toHaveClass('is-idle');
    press('Escape');
    expect([screen.queryByTestId('presenter-voice-question'), editor.presenter.getState().active]).toEqual([null, true]);
    press('b');
    expect(screen.getByTestId('presenter-blank')).toBeInTheDocument();
    press('v');
    expect([screen.queryByTestId('presenter-blank'), screen.queryByTestId('presenter-voice-question') !== null]).toEqual([null, true]);
    press('b');
    expect(screen.queryByTestId('presenter-blank')).toBeNull();
  });

  it('stops the keys of the presentation while the question is open: none drives the story, none reaches the page under it nor the browser', async () => {
    fakeSpeech();
    const { editor } = await showEditor(sampleMap());
    press('F5');
    press(' ');
    await settle();
    press('v');
    const before = editor.presenter.getState();
    expect([' ', 'Enter', 'ArrowRight', 'PageDown', 'b', 'F5'].map((name) => press(name))).toEqual([true, true, true, true, true, true]);
    const after = editor.presenter.getState();
    expect([after.player.playing, after.player.scene, after.run, after.screen, after.asking]).toEqual([true, 0, before.run, 'map', true]);
    // Entrée and Espace answered nothing by chance: the voice is still off, and nothing may leave for the maker of the browser.
    expect([editor.preferences.getState().presenterVoice, editor.preferences.getState().presenterOnlineVoices]).toEqual([false, false]);
  });

  it('gives the focus to the stage when the presentation starts, so that Entrée and Espace never press again a button of the editor under it, and gives it back at the end', async () => {
    await showEditor(sampleMap());
    const button = screen.getByTestId('ribbon-present');
    act(() => {
      button.focus();
      button.click();
    });
    expect(document.activeElement).toBe(screen.getByTestId('presenter'));
    expect([press('Enter'), press(' ')]).toEqual([true, true]);
    press('Escape');
    expect([screen.queryByTestId('presenter'), document.activeElement]).toEqual([null, button]);
  });
});

describe('Échap and the keys typed in the fields, while the voice asks its question or shows its settings (relecture 6: R6-01)', () => {
  it('holds Échap for the question where the browser can, and keeps the presentation when the full screen is left while the question is open', async () => {
    fakeSpeech();
    const lock = vi.fn(() => Promise.resolve());
    const unlock = vi.fn();
    const { editor } = await showEditor(sampleMap());
    press('F5');
    press(' ');
    await settle();
    const full: { element: Element | null } = { element: screen.getByTestId('presenter') };
    Object.defineProperty(document, 'fullscreenElement', { configurable: true, get: () => full.element });
    Object.defineProperty(navigator, 'keyboard', { configurable: true, value: { lock, unlock } });
    try {
      press('v');
      await act(async () => {
        await Promise.resolve();
      });
      expect(lock).toHaveBeenCalledWith(['Escape']);
      // A browser that does not hold Échap leaves the full screen on it: the presentation stays, its question open.
      full.element = null;
      fullscreenChanged();
      expect([editor.presenter.getState().active, screen.queryByTestId('presenter-voice-question') !== null]).toEqual([true, true]);
      // Échap closes the question; the lock is given back only once the key is up, or the browser would take its release for an Échap of its own.
      press('Escape');
      expect([editor.presenter.getState().active, screen.queryByTestId('presenter-voice-question'), unlock.mock.calls.length]).toEqual([true, null, 0]);
      release('Escape');
      expect(unlock.mock.calls.length).toBe(1);
      // The full screen left right after that Échap is the doing of that key: the presentation stays.
      fullscreenChanged();
      expect(editor.presenter.getState().active).toBe(true);
      // Nothing open any more: a later full screen left ends the presentation.
      full.element = screen.getByTestId('presenter');
      fullscreenChanged();
      full.element = null;
      fullscreenChanged();
      expect(editor.presenter.getState().active).toBe(false);
    } finally {
      Reflect.deleteProperty(document, 'fullscreenElement');
      Reflect.deleteProperty(navigator, 'keyboard');
    }
  });

  it('keeps the keys typed in the settings of the voice from the story and from the browser: F5 reloads nothing, Échap closes the settings alone, the focus back on their button', async () => {
    fakeSpeech();
    const { editor } = await showEditor(sampleMap());
    press('F5');
    press(' ');
    await settle();
    press('v');
    click('presenter-voice-local');
    click('presenter-voice-settings');
    expect(document.activeElement).toBe(screen.getByTestId('presenter-voice-panel'));
    const { run } = editor.presenter.getState();
    expect([press(' '), press('ArrowRight'), press('F5')]).toEqual([true, true, true]);
    act(() => {
      screen.getByTestId('presenter-voice-choice').focus();
    });
    expect([press('F5'), editor.presenter.getState().run, editor.presenter.getState().player.playing]).toEqual([true, run, true]);
    press('Escape');
    expect([screen.queryByTestId('presenter-voice-panel'), editor.presenter.getState().active]).toEqual([null, true]);
    expect(document.activeElement).toBe(screen.getByTestId('presenter-voice-settings'));
  });
});

describe('the settings of the voice closed with the voice (relecture 6: R6-01)', () => {
  it('close when the voice is turned off by its button: the keys drive the story again', async () => {
    fakeSpeech();
    const { editor } = await showEditor(sampleMap());
    press('F5');
    press(' ');
    await settle();
    press('v');
    click('presenter-voice-local');
    click('presenter-voice-settings');
    expect(press(' ')).toBe(true);
    expect([editor.presenter.getState().tuning, editor.presenter.getState().player.playing]).toEqual([true, true]);
    click('presenter-voice');
    expect([screen.queryByTestId('presenter-voice-panel'), editor.presenter.getState().tuning, editor.preferences.getState().presenterVoice]).toEqual([null, false, false]);
    press(' ');
    expect(editor.presenter.getState().player.playing).toBe(false);
  });
});

describe('the stage of a presentation, a modal dialog walked by Tab (relecture 6: R6-05)', () => {
  /** Tab pressed until a control has the focus, a round of the stage at most. */
  const walkTo = (testId: string) => {
    for (let presses = 0; presses < 40 && document.activeElement !== screen.getByTestId(testId); presses++) press('Tab');
  };

  it('walks its controls on Tab and Maj+Tab, round and round, and never leaves for the editor under it', async () => {
    await showEditor(sampleMap());
    press('F5');
    press(' ');
    const stage = screen.getByTestId('presenter');
    expect([press('Tab'), document.activeElement, stage.classList.contains('is-keyed')]).toEqual([true, screen.getByTestId('presenter-restart'), true]);
    expect([press('Tab', { shiftKey: true }), document.activeElement]).toEqual([true, screen.getByTestId('presenter-exit')]);
    press('Tab');
    expect(document.activeElement).toBe(screen.getByTestId('presenter-restart'));
    const inside = Array.from({ length: 40 }, () => {
      press('Tab');
      return document.activeElement !== stage && stage.contains(document.activeElement);
    });
    expect(inside.every(Boolean)).toBe(true);
  });

  it('keeps Tab and Maj+Tab in the stage from the field of a comment too', async () => {
    await showEditor(sampleMap());
    press('F5');
    press('ArrowRight');
    press('ArrowRight');
    press('t');
    const stage = screen.getByTestId('presenter');
    expect(document.activeElement).toBe(screen.getByTestId('presenter-comment-field'));
    expect([press('Tab', { shiftKey: true }), document.activeElement !== screen.getByTestId('presenter-comment-field'), stage.contains(document.activeElement)]).toEqual([true, true, true]);
    walkTo('presenter-comment-field');
    expect([press('Tab'), document.activeElement]).toEqual([true, screen.getByTestId('presenter-restart')]);
  });

  it('presses the control Tab walked to on Entrée or Espace, not « next sentence » nor « pause »; after a press of the pointer the keys are those of the story again', async () => {
    const { editor } = await showEditor(sampleMap());
    press('F5');
    walkTo('presenter-next');
    // « Étape suivante » goes to the first step; the next sentence would have stayed in the opening.
    expect([press('Enter'), editor.presenter.getState().player.scene]).toEqual([true, 1]);
    walkTo('presenter-subtitles');
    expect([press(' '), editor.preferences.getState().presenterSubtitles, editor.presenter.getState().player.playing]).toEqual([true, false, false]);
    fireEvent.pointerDown(screen.getByTestId('presenter-subtitles'));
    press(' ');
    expect([editor.preferences.getState().presenterSubtitles, editor.presenter.getState().player.playing, screen.getByTestId('presenter').classList.contains('is-keyed')]).toEqual([false, true, false]);
  });

  it('leaves Ctrl+R and Ctrl+F5 to the browser, which reloads the page; F5 alone starts the presentation again and reloads nothing', async () => {
    const { editor } = await showEditor(sampleMap());
    press('F5');
    press(' ');
    const { run } = editor.presenter.getState();
    expect([press('r', { ctrlKey: true }), press('F5', { ctrlKey: true }), editor.presenter.getState().run]).toEqual([false, false, run]);
    expect([press('F5'), editor.presenter.getState().run]).toEqual([true, run + 1]);
  });
});
