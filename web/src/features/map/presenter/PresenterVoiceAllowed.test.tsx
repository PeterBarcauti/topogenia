// PresenterVoiceAllowed.test.tsx
// The online voices of a presentation offered only for a project whose classification lets its texts leave the computer, the rule of the assistant read from the host (F-137, audit 3.2 SEC-CRIT-02): the question, the settings and the voice read; the voice chosen kept by the browser and taken again after a reload, if it is still there and still allowed (UX-MOY-01).

import { act, fireEvent, screen, within } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { i18next } from '../../../shared/i18n/i18n.ts';
import { memoryStorage, showEditorWith } from '../../../test/editor.tsx';
import { sampleMap } from '../../../test/maps.ts';
import { fakeSpeech } from '../../../test/speech.ts';

beforeEach(async () => {
  await i18next.changeLanguage('fr');
});

afterEach(() => {
  vi.unstubAllGlobals();
});

const key = (name: string) => {
  act(() => {
    fireEvent.keyDown(window, { key: name });
  });
};

const click = (testId: string) => {
  act(() => {
    screen.getByTestId(testId).click();
  });
};

/** The answer of the host read before the next gesture, as a presenter who starts then reaches for V. */
const settle = () => act(async () => {
  await Promise.resolve();
});

const refused = () => Promise.resolve(false);

/** The voice kept on by the browser, its question answered, the online voices allowed by the person: the voice of the last session. */
const KEPT = { 'app.presenterVoice': '1', 'app.presenterOnlineVoices': '1', 'app.presenterVoiceAsked': '1', 'app.presenterSubtitles': '0' };

describe('the online voices for a project whose classification does not let its texts leave the computer (F-137)', () => {
  it.each([
    ['refused by the host', refused],
    ['unreadable', () => Promise.reject(new Error('host unreachable'))],
    ['not read yet', () => new Promise<boolean>(() => undefined)],
  ])('are never offered when the answer is %s: V asks nothing and reads with the voice of the computer', async (_, onlineVoices) => {
    const spoken = fakeSpeech();
    const { editor } = await showEditorWith({ onlineVoices }, sampleMap());
    key('F5');
    key(' ');
    await settle();
    key('v');
    const preferences = editor.preferences.getState();
    expect([screen.queryByTestId('presenter-voice-question'), screen.queryByTestId('presenter-voice-natural')]).toEqual([null, null]);
    expect([preferences.presenterVoice, preferences.presenterOnlineVoices, spoken.map(({ voice }) => voice)]).toEqual([true, false, ['Hortense']]);
  });

  it('are left out of the voice read and of its settings, the box of the online voices off and saying why, even when the browser kept them allowed and chosen', async () => {
    const spoken = fakeSpeech();
    const storage = memoryStorage({ ...KEPT, 'app.presenterVoiceName': 'Denise Online' });
    const { editor } = await showEditorWith({ onlineVoices: refused, storage }, sampleMap());
    key('F5');
    key(' ');
    await settle();
    expect(spoken.map(({ voice }) => voice)).toEqual(['Hortense']);
    click('presenter-voice-settings');
    expect(within(screen.getByTestId('presenter-voice-choice')).getAllByRole('option').map((option) => option.textContent)).toEqual(['Hortense (sur le poste)']);
    const box = screen.getByTestId('presenter-voice-online');
    expect([box.hasAttribute('disabled'), (box as HTMLInputElement).checked]).toEqual([true, false]);
    expect(screen.getByTestId('presenter-voice-online-refused')).toHaveTextContent('La classification de ce projet ne permet pas les voix en ligne.');
    // The choice of the person is not changed: a project that allows them gives them back.
    expect(editor.preferences.getState().presenterOnlineVoices).toBe(true);
  });

  it('are never taken for a voice of the computer of the same name: the voice read is found again by its name and where it speaks', async () => {
    const spoken = fakeSpeech({ voices: [{ name: 'Hortense', lang: 'fr-FR', localService: false }, { name: 'Hortense', lang: 'fr-FR', localService: true }] });
    await showEditorWith({ onlineVoices: refused, storage: memoryStorage({ ...KEPT, 'app.presenterVoiceName': 'Hortense' }) }, sampleMap());
    key('F5');
    key(' ');
    await settle();
    expect(spoken.map(({ voice, local }) => [voice, local])).toEqual([['Hortense', true]]);
  });

  it('are offered for a project that allows them, once the host has said so: the question offers the natural voice, the box is on', async () => {
    const spoken = fakeSpeech();
    const reads: string[] = [];
    await showEditorWith({ onlineVoices: (mapId) => { reads.push(mapId); return Promise.resolve(true); } }, sampleMap());
    key('F5');
    key(' ');
    await settle();
    key('v');
    click('presenter-voice-natural');
    expect(spoken.at(-1)?.voice).toBe('Denise Online');
    click('presenter-voice-settings');
    expect([screen.getByTestId('presenter-voice-online').hasAttribute('disabled'), screen.queryByTestId('presenter-voice-online-refused')]).toEqual([false, null]);
    expect(reads).toEqual([sampleMap().id]);
  });
});

describe('the voice chosen, kept by the browser (audit 3.2, UX-MOY-01)', () => {
  it('is taken again after a reload, by its name, when it is still there and the project allows it', async () => {
    const spoken = fakeSpeech();
    await showEditorWith({ storage: memoryStorage({ ...KEPT, 'app.presenterVoiceName': 'Denise Online' }) }, sampleMap());
    key('F5');
    key(' ');
    await settle();
    expect(spoken.at(-1)?.voice).toBe('Denise Online');
  });

  it('gives way to the first voice of the language when it is gone', async () => {
    const spoken = fakeSpeech();
    await showEditorWith({ storage: memoryStorage({ ...KEPT, 'app.presenterVoiceName': 'Une voix disparue' }) }, sampleMap());
    key('F5');
    key(' ');
    await settle();
    expect(spoken.map(({ voice }) => voice)).toEqual(['Hortense']);
  });

  it('is written when the question is answered and when the settings change it', async () => {
    fakeSpeech();
    const storage = memoryStorage();
    await showEditorWith({ storage }, sampleMap());
    key('F5');
    key(' ');
    await settle();
    key('v');
    click('presenter-voice-natural');
    expect(storage.written.get('app.presenterVoiceName')).toBe('Denise Online');
    click('presenter-voice-settings');
    fireEvent.change(screen.getByTestId('presenter-voice-choice'), { target: { value: 'Hortense' } });
    expect(storage.written.get('app.presenterVoiceName')).toBe('Hortense');
  });

  it('is forgotten when the voice of the computer is answered to the question', async () => {
    const spoken = fakeSpeech();
    const storage = memoryStorage({ 'app.presenterVoiceName': 'Denise Online' });
    await showEditorWith({ storage }, sampleMap());
    key('F5');
    key(' ');
    await settle();
    key('v');
    click('presenter-voice-local');
    expect([storage.written.get('app.presenterVoiceName'), spoken.at(-1)?.voice]).toEqual(['', 'Hortense']);
  });
});
