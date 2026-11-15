// PresenterVoiceProject.test.tsx
// The voice a project keeps for a language (lot J10): read when the presentation starts and taken before the one the browser kept, when this computer offers it to this person, never an online one the person or the classification of the project does not allow (F-137); the voice a facilitator chooses kept by the project for the language shown, over the choice of another when it changed meanwhile; the one a reader chooses kept for the session only.

import { act, fireEvent, screen, within } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiError, NETWORK_ERROR } from '../../../api/errors.ts';
import { messageOf } from '../../../shared/i18n/errorMessage.ts';
import { i18next } from '../../../shared/i18n/i18n.ts';
import { memoryStorage, memoryVoices, showEditorWith } from '../../../test/editor.tsx';
import { PROJECT_ID, sampleMap } from '../../../test/maps.ts';
import { fakeSpeech } from '../../../test/speech.ts';

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

/** What the host answered taken before the next gesture. */
const settle = () => act(async () => {
  await Promise.resolve();
  await Promise.resolve();
  await Promise.resolve();
});

/** The presentation started, the voices of the project read, then played: the presenter starts the story once the room is ready. */
const present = async () => {
  key('F5');
  await settle();
  key(' ');
  await settle();
};

const choose = async (name: string) => {
  if (screen.queryByTestId('presenter-voice-panel') === null) {
    act(() => {
      screen.getByTestId('presenter-voice-settings').click();
    });
  }
  act(() => {
    fireEvent.change(screen.getByTestId('presenter-voice-choice'), { target: { value: name } });
  });
  await settle();
};

/** Three French voices and an English one: two of the computer and one online for the language shown. */
const VOICES = [
  { name: 'Hortense', lang: 'fr-FR', localService: true }, { name: 'Julie', lang: 'fr-FR', localService: true },
  { name: 'Denise Online', lang: 'fr-FR', localService: false }, { name: 'Zira', lang: 'en-US', localService: true },
];

/** The voice kept on by the browser, its question answered, the online voices allowed by the person. */
const ON = { 'app.presenterVoice': '1', 'app.presenterOnlineVoices': '1', 'app.presenterVoiceAsked': '1', 'app.presenterSubtitles': '0' };

const french = (name: string, local = true, version = 1) => ({ language: 'fr', name, local, version });

const reader = () => ({ ...sampleMap(), rights: { structure: false, role: 'reader' as const } });

describe('the voice a project keeps for the language shown (lot J10)', () => {
  it('is read when the presentation starts and reads before the one the browser kept; the voice of another language is left', async () => {
    const spoken = fakeSpeech({ voices: VOICES });
    const voices = memoryVoices([french('Julie'), { language: 'en', name: 'Zira', local: true, version: 1 }]);
    await showEditorWith({ voices: voices.source, storage: memoryStorage({ ...ON, 'app.presenterVoiceName': 'Hortense' }) }, sampleMap());
    await present();
    expect(voices.reads).toEqual([PROJECT_ID]);
    expect(spoken.map(({ voice }) => voice)).toEqual(['Julie']);
    act(() => {
      screen.getByTestId('presenter-voice-settings').click();
    });
    expect(screen.getByTestId<HTMLSelectElement>('presenter-voice-choice').value).toBe('Julie');
  });

  it('leaves the voice of the browser when the project keeps none for the language, or one this computer has not', async () => {
    const spoken = fakeSpeech({ voices: VOICES });
    const voices = memoryVoices([french('Absente')]);
    await showEditorWith({ voices: voices.source, storage: memoryStorage({ ...ON, 'app.presenterVoiceName': 'Julie' }) }, sampleMap());
    await present();
    expect(spoken.map(({ voice }) => voice)).toEqual(['Julie']);
  });

  it.each([
    ['the classification of the project keeps its texts on the computer', { ...ON }, false, 'Hortense'],
    ['the person did not allow the online voices', { ...ON, 'app.presenterOnlineVoices': '0' }, true, 'Hortense'],
    ['the person and the project allow them', { ...ON }, true, 'Denise Online'],
  ])('is an online voice only when both allow it: %s', async (_, kept, allowed, read) => {
    const spoken = fakeSpeech({ voices: VOICES });
    const voices = memoryVoices([french('Denise Online', false)]);
    await showEditorWith({ voices: voices.source, storage: memoryStorage(kept), onlineVoices: () => Promise.resolve(allowed) }, sampleMap());
    await present();
    expect(spoken.at(-1)?.voice).toBe(read);
  });

  it('is unreadable without a word: the voice of the browser reads, and nothing is said in the stage', async () => {
    const spoken = fakeSpeech({ voices: VOICES });
    const source = { ...memoryVoices().source, read: () => Promise.reject(new Error('host unreachable')) };
    await showEditorWith({ voices: source, storage: memoryStorage({ ...ON, 'app.presenterVoiceName': 'Julie' }) }, sampleMap());
    await present();
    expect([spoken.map(({ voice }) => voice), within(screen.getByTestId('presenter')).queryByRole('alert')]).toEqual([['Julie'], null]);
  });
});

describe('the voice chosen in the settings of the voice (lot J10)', () => {
  it('is kept by the project for the language shown when a facilitator chooses it, each choice over the version of the one before', async () => {
    const spoken = fakeSpeech({ voices: VOICES });
    const voices = memoryVoices();
    const { editor } = await showEditorWith({ voices: voices.source, storage: memoryStorage(ON) }, sampleMap());
    await present();
    await choose('Julie');
    await choose('Denise Online');
    expect(voices.writes).toEqual([
      { projectId: PROJECT_ID, language: 'fr', name: 'Julie', local: true, version: 0 },
      { projectId: PROJECT_ID, language: 'fr', name: 'Denise Online', local: false, version: 1 },
    ]);
    expect([spoken.at(-1)?.voice, editor.preferences.getState().presenterVoiceName]).toEqual(['Denise Online', 'Denise Online']);
    expect(within(screen.getByTestId('presenter')).queryByRole('alert')).toBeNull();
  });

  it('is kept for the session only when a reader chooses it: nothing is written, and it reads again when the presentation starts again', async () => {
    const spoken = fakeSpeech({ voices: VOICES });
    const voices = memoryVoices([french('Hortense')]);
    await showEditorWith({ voices: voices.source, storage: memoryStorage(ON) }, reader());
    await present();
    await choose('Julie');
    expect([voices.writes, spoken.at(-1)?.voice]).toEqual([[], 'Julie']);
    key('Escape');
    key('Escape');
    expect(screen.queryByTestId('presenter')).toBeNull();
    await present();
    // The stage is new and read the voices of the project again: it keeps Hortense, and the voice picked reads all the same.
    expect([voices.reads.length, spoken.at(-1)?.voice]).toEqual([2, 'Julie']);
  });

  it('is written over the choice another facilitator made meanwhile: the voices read again, then the write sent again', async () => {
    fakeSpeech({ voices: VOICES });
    const voices = memoryVoices([french('Hortense')]);
    await showEditorWith({ voices: voices.source, storage: memoryStorage(ON) }, sampleMap());
    await present();
    voices.give(french('Denise Online', false, 4));
    await choose('Julie');
    expect(voices.writes.map(({ name, version }) => [name, version])).toEqual([['Julie', 1], ['Julie', 4]]);
    expect([voices.reads.length, within(screen.getByTestId('presenter')).queryByRole('alert')]).toEqual([2, null]);
    await choose('Hortense');
    expect(voices.writes.at(-1)).toEqual({ projectId: PROJECT_ID, language: 'fr', name: 'Hortense', local: true, version: 5 });
  });

  it('says in the stage that the project did not keep it when the host refuses, and reads with it all the same', async () => {
    const spoken = fakeSpeech({ voices: VOICES });
    const voices = memoryVoices();
    voices.state.failure = new ApiError(403, 'ACCES_REFUSE', 'Vous n’avez pas le droit de faire cela.');
    await showEditorWith({ voices: voices.source, storage: memoryStorage(ON) }, sampleMap());
    await present();
    await choose('Julie');
    expect(within(screen.getByTestId('presenter')).getByRole('alert')).toHaveTextContent('Vous n’avez pas le droit de faire cela.');
    expect(spoken.at(-1)?.voice).toBe('Julie');
  });
});

describe('the voice of the project at its edges (lot J10, independent review)', () => {
  it('leaves what the host answered last when the first reading comes after a choice: the next choice is written over it', async () => {
    fakeSpeech({ voices: VOICES });
    const voices = memoryVoices([french('Hortense')]);
    const gate: { open: () => void; asked: number } = { open: () => undefined, asked: 0 };
    const opened = new Promise<void>((resolve) => {
      gate.open = resolve;
    });
    // The first reading is answered with what the host had when it was asked, once the test lets it go.
    const read = async (projectId: string) => {
      const answer = await voices.source.read(projectId);
      if (gate.asked++ === 0) await opened;
      return answer;
    };
    const { editor } = await showEditorWith({ voices: { ...voices.source, read }, storage: memoryStorage(ON) }, sampleMap());
    await present();
    await choose('Julie');
    gate.open();
    await settle();
    expect(editor.presenter.getState().projectVoices).toEqual([french('Julie', true, 2)]);
    await choose('Hortense');
    expect(voices.writes.map(({ name, version }) => [name, version])).toEqual([['Julie', 0], ['Julie', 1], ['Hortense', 2]]);
  });

  it('gives way to the natural voice answered to the question of the first use, for the session: the person agreed to it, and nothing is written', async () => {
    const spoken = fakeSpeech({ voices: VOICES });
    const voices = memoryVoices([french('Julie')]);
    await showEditorWith({ voices: voices.source, storage: memoryStorage({}) }, sampleMap());
    await present();
    key('v');
    act(() => {
      screen.getByTestId('presenter-voice-natural').click();
    });
    await settle();
    expect([spoken.at(-1)?.voice, voices.writes]).toEqual(['Denise Online', []]);
  });

  it('reads with the voice of the project when the answer is the voice of the computer', async () => {
    const spoken = fakeSpeech({ voices: VOICES });
    const voices = memoryVoices([french('Julie')]);
    await showEditorWith({ voices: voices.source, storage: memoryStorage({}) }, sampleMap());
    await present();
    key('v');
    act(() => {
      screen.getByTestId('presenter-voice-local').click();
    });
    await settle();
    expect(spoken.at(-1)?.voice).toBe('Julie');
  });

  it('gives up a write the host never answers, says it in the stage, and writes the next choice', async () => {
    fakeSpeech({ voices: VOICES });
    const voices = memoryVoices();
    let mute = true;
    const write: typeof voices.source.write = (projectId, language, voice, version, signal) => (mute
      ? new Promise((_, reject) => {
        signal?.addEventListener('abort', () => {
          reject(new DOMException('aborted', 'AbortError'));
        });
      })
      : voices.source.write(projectId, language, voice, version, signal));
    await showEditorWith({ voices: { ...voices.source, write }, storage: memoryStorage(ON) }, sampleMap());
    await present();
    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] });
    await choose('Julie');
    expect(within(screen.getByTestId('presenter')).queryByRole('alert')).toBeNull();
    await act(async () => {
      await vi.advanceTimersByTimeAsync(10_000);
    });
    expect(within(screen.getByTestId('presenter')).getByRole('alert')).toHaveTextContent(messageOf(new ApiError(0, NETWORK_ERROR, '')));
    mute = false;
    await choose('Hortense');
    expect(voices.writes).toEqual([{ projectId: PROJECT_ID, language: 'fr', name: 'Hortense', local: true, version: 0 }]);
  });
});

describe('the box of the voice of the project, in the settings of the voice (lot J10, third independent review)', () => {
  /** The settings of the voice opened, and the box that says the project keeps the voice shown. */
  const projectBox = () => {
    if (screen.queryByTestId('presenter-voice-panel') === null) {
      act(() => {
        screen.getByTestId('presenter-voice-settings').click();
      });
    }
    return screen.queryByTestId<HTMLInputElement>('presenter-voice-project');
  };

  it('is the voice already shown in the list once a facilitator ticks its box: no other voice need be chosen first (third independent review)', async () => {
    fakeSpeech({ voices: VOICES });
    const voices = memoryVoices();
    const { editor } = await showEditorWith({ voices: voices.source, storage: memoryStorage({ ...ON, 'app.presenterVoiceName': 'Julie' }) }, sampleMap());
    await present();
    expect([screen.queryByTestId('presenter-voice-panel'), projectBox()?.checked]).toEqual([null, false]);
    expect(screen.getByTestId<HTMLSelectElement>('presenter-voice-choice').value).toBe('Julie');
    expect(screen.getByLabelText('Voix du projet pour cette langue')).toBe(projectBox());
    act(() => {
      projectBox()?.click();
    });
    await settle();
    expect(voices.writes).toEqual([{ projectId: PROJECT_ID, language: 'fr', name: 'Julie', local: true, version: 0 }]);
    expect([projectBox()?.checked, editor.presenter.getState().projectVoices]).toEqual([true, [french('Julie')]]);
  });

  it('is removed from the project when its box is unticked, the version read sent; it goes on reading for the session', async () => {
    const spoken = fakeSpeech({ voices: VOICES });
    const voices = memoryVoices([french('Julie')]);
    const { editor } = await showEditorWith({ voices: voices.source, storage: memoryStorage(ON) }, sampleMap());
    await present();
    expect(projectBox()?.checked).toBe(true);
    act(() => {
      projectBox()?.click();
    });
    await settle();
    expect([voices.removals, voices.writes, editor.presenter.getState().projectVoices, projectBox()?.checked])
      .toEqual([[{ projectId: PROJECT_ID, language: 'fr', version: 1 }], [], [], false]);
    // The voice that read still reads, and the list still shows it: unticking takes it from the project, not from the session.
    expect(screen.getByTestId<HTMLSelectElement>('presenter-voice-choice').value).toBe('Julie');
    key('ArrowRight');
    key(' ');
    await settle();
    expect(spoken.at(-1)?.voice).toBe('Julie');
  });

  it('follows the voice shown: unticked while the project keeps another one, ticked once a facilitator has chosen in the list, which the project keeps at once', async () => {
    fakeSpeech({ voices: VOICES });
    const voices = memoryVoices([french('Julie')]);
    const { editor } = await showEditorWith({ voices: voices.source, storage: memoryStorage(ON) }, sampleMap());
    await present();
    // Another facilitator chose Hortense meanwhile: the list still shows the voice that reads here, which the project keeps no more.
    act(() => {
      editor.presenter.getState().pick('fr', { name: 'Julie', local: true });
      editor.presenter.getState().keepVoices([french('Hortense', true, 2)]);
    });
    expect([screen.queryByTestId('presenter-voice-panel'), projectBox()?.checked]).toEqual([null, false]);
    await choose('Hortense');
    expect(projectBox()?.checked).toBe(true);
    await choose('Julie');
    expect([voices.writes.map(({ name }) => name), projectBox()?.checked]).toEqual([['Julie'], true]);
  });

  it('is offered to a facilitator only: a reader has no box, and chooses for the session', async () => {
    fakeSpeech({ voices: VOICES });
    await showEditorWith({ voices: memoryVoices([french('Julie')]).source, storage: memoryStorage(ON) }, reader());
    await present();
    expect(projectBox()).toBeNull();
    expect(screen.getByTestId('presenter-voice-choice')).toBeEnabled();
  });
});

describe('the natural voice answered at first use, on a project that keeps an online voice (lot J10, third independent review)', () => {
  it('reads with the online voice the project keeps when the natural voice is answered at first use: the answer says a kind of voice, the project names one', async () => {
    const online = [{ name: 'Denise Online (Natural)', lang: 'fr-FR', localService: false }, { name: 'Henri Online (Natural)', lang: 'fr-FR', localService: false }];
    const spoken = fakeSpeech({ voices: [...VOICES.filter((voice) => voice.localService), ...online] });
    const voices = memoryVoices([french('Henri Online (Natural)', false)]);
    await showEditorWith({ voices: voices.source, storage: memoryStorage({}) }, sampleMap());
    await present();
    key('v');
    act(() => {
      screen.getByTestId('presenter-voice-natural').click();
    });
    await settle();
    expect([spoken.at(-1)?.voice, voices.writes]).toEqual(['Henri Online (Natural)', []]);
  });
});
