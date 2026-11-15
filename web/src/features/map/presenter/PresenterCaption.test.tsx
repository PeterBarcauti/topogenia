// PresenterCaption.test.tsx
// What the room reads during a presentation (relecture 6: R6-04, R6-08, R6-13): the subtitles shown whatever the setting kept when the voice is on and no voice can say the language shown; the counters rounded as the sentences say them; the subtitle told to the screen readers, not while the voice reads it.

import { act, fireEvent, screen, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { i18next } from '../../../shared/i18n/i18n.ts';
import { showEditor } from '../../../test/editor.tsx';
import { sampleMap, streamOf } from '../../../test/maps.ts';
import { fakeSpeech, type FakeVoice } from '../../../test/speech.ts';

beforeEach(async () => {
  await i18next.changeLanguage('fr');
});

afterEach(() => {
  vi.unstubAllGlobals();
  vi.useRealTimers();
});

const key = (name: string) => {
  act(() => {
    fireEvent.keyDown(window, { key: name });
  });
};

const line = () => screen.queryByTestId('presenter-line')?.textContent ?? null;

type Editor = Awaited<ReturnType<typeof showEditor>>['editor'];

/** The voice kept on and the subtitles kept off, as a presentation read aloud leaves them from one session to the next. */
const voiceKept = (editor: Editor) => {
  act(() => {
    editor.preferences.getState().set('presenterVoice', true);
    editor.preferences.getState().set('presenterSubtitles', false);
  });
};

describe('the subtitles when no voice can say the language shown (relecture 6: R6-04)', () => {
  it('are shown whatever the setting kept, the voice on and without a voice for the language: never a silent presentation without text', async () => {
    const spoken = fakeSpeech({ online: false });
    const { editor } = await showEditor(sampleMap());
    voiceKept(editor);
    // The map shown in English, on a computer that only has a French voice.
    act(() => {
      editor.store.getState().show({ language: 'en', origin: 'fr', locked: true });
    });
    key('F5');
    key(' ');
    await waitFor(() => {
      expect(line()).not.toBeNull();
    });
    expect([spoken.length, editor.preferences.getState().presenterSubtitles]).toEqual([0, false]);
    // In French the voice of the computer reads them: they stay hidden, as kept.
    key('Escape');
    act(() => {
      editor.store.getState().show({ language: 'fr', origin: 'fr', locked: true });
    });
    key('F5');
    key(' ');
    expect([line(), spoken.map(({ voice }) => voice)]).toEqual([null, ['Hortense']]);
  });

  it('wait for the browser to tell its voices, which come late in Chromium: hidden subtitles do not flash, and show once no voice has come', async () => {
    const voices: FakeVoice[] = [];
    const spoken = fakeSpeech({ voices });
    const { editor } = await showEditor(sampleMap());
    voiceKept(editor);
    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] });
    key('F5');
    key(' ');
    expect(line()).toBeNull();
    voices.push({ name: 'Hortense', lang: 'fr-FR', localService: true });
    act(() => {
      spoken.voicesChanged();
    });
    expect([line(), spoken.map(({ voice }) => voice)]).toEqual([null, ['Hortense']]);
    // A browser that never tells any voice: the subtitles come after a moment.
    key('Escape');
    voices.length = 0;
    key('F5');
    key(' ');
    expect(line()).toBeNull();
    act(() => {
      vi.advanceTimersByTime(1000);
    });
    expect(line()).toBe('Le parcours compte 4 étapes.');
  });

  it('stay hidden by C when no voice reads at all: the presenter speaks, the voice off', async () => {
    const { editor } = await showEditor(sampleMap());
    key('F5');
    key(' ');
    key('c');
    expect([line(), editor.preferences.getState().presenterVoice, editor.preferences.getState().presenterSubtitles]).toEqual([null, false, false]);
  });
});

describe('the counters rounded as the sentences say them (relecture 6: R6-08)', () => {
  /** The number a counter shows, without its unit. */
  const counted = (name: string) => screen.getByTestId(`presenter-counter-${name}`).querySelector('.presenter-counter-value')?.firstChild?.textContent ?? null;

  it('write a figure of ten or more in its unit without a decimal, as its sentence: 10,5 hours of work are « 11 », 12,5 days of lead time are « 13 »', async () => {
    vi.useFakeTimers({ toFake: ['requestAnimationFrame', 'cancelAnimationFrame', 'performance'] });
    // 630 minutes of work, and 87,5 working hours of lead time: 12,5 days of 7 hours.
    const { editor } = await showEditor(sampleMap({ value_stream: streamOf({ steps: { s2: { work_minutes: 630, lead_hours: 87.5, work_per_case_minutes: 630 } as never } }) }));
    act(() => {
      editor.store.setState({ selection: { steps: ['s2'], flows: [] } });
    });
    act(() => {
      fireEvent.keyDown(window, { key: 'F5', shiftKey: true });
    });
    key(' ');
    key('ArrowRight');
    act(() => {
      vi.advanceTimersByTime(1000);
    });
    // The browser writes an unbreakable space between a number and its unit.
    expect(line()).toMatch(/^11\sheures de travail, pour 13\sjours de délai\.$/);
    expect([counted('work'), counted('lead')]).toEqual(['11', '13']);
  });
});

describe('the subtitle told to the screen readers (relecture 6: R6-13)', () => {
  it('is told by the caption that stays, not by the sentence made again at each one; not while the voice reads it aloud', async () => {
    fakeSpeech({ online: false });
    await showEditor(sampleMap());
    key('F5');
    key(' ');
    expect([screen.getByTestId('presenter-caption').getAttribute('aria-live'), screen.getByTestId('presenter-line').getAttribute('aria-live')]).toEqual(['polite', null]);
    // The voice reads, the subtitles shown over it by C: the screen reader does not say the sentence a second time.
    key('v');
    key('c');
    expect([line(), screen.getByTestId('presenter-caption').getAttribute('aria-live')]).toEqual(['Le parcours compte 4 étapes.', 'off']);
    // In pause the voice is silent: the sentence reached by a key is told again.
    key(' ');
    expect(screen.getByTestId('presenter-caption').getAttribute('aria-live')).toBe('polite');
  });
});
