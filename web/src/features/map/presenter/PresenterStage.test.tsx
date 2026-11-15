// PresenterStage.test.tsx
// The stage of a presentation in the editor of the map: opened by « Présenter » and F5, from the step chosen by Shift+F5; the step shown lit on its map, its subtitle in the language shown, its keys, its chapters, its black screen and its subtitles; no key of the edition reaching the map under it; closed by Échap, the editor as it was.

import { act, fireEvent, screen, within } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { i18next } from '../../../shared/i18n/i18n.ts';
import { showEditor } from '../../../test/editor.tsx';
import { callsTo, fakeApi } from '../../../test/fakeApi.ts';
import { sampleMap, streamOf } from '../../../test/maps.ts';
import { fakeSpeech } from '../../../test/speech.ts';

beforeEach(async () => {
  await i18next.changeLanguage('fr');
});

afterEach(() => {
  vi.unstubAllGlobals();
  vi.useRealTimers();
});

const key = (name: string, options: Partial<KeyboardEventInit> = {}) => {
  act(() => {
    fireEvent.keyDown(window, { key: name, ...options });
  });
};

const keyUp = (name: string) => {
  act(() => {
    fireEvent.keyUp(window, { key: name });
  });
};

/** The browser tells the page that the full screen changed. */
const fullscreenChanged = () => {
  act(() => {
    document.dispatchEvent(new Event('fullscreenchange'));
  });
};

const caption = () => ({ line: screen.queryByTestId('presenter-line')?.textContent ?? null });

/** The step the stage shows: the one lit on its map (no banner names it, essai J2), null at the opening and the closing. */
const shown = () => screen.queryByTestId('presenter-canvas')?.querySelector('.react-flow__node.is-presented')?.getAttribute('data-id') ?? null;

describe('the stage of a presentation (ADR 097)', () => {
  it('opens on F5 over the editor, on the whole map, and plays once started, its name and its project in the banner', async () => {
    const { editor } = await showEditor(sampleMap());
    expect(screen.queryByTestId('presenter')).toBeNull();
    key('F5');
    key(' ');
    const stage = screen.getByTestId('presenter');
    expect([stage.getAttribute('role'), stage.getAttribute('aria-modal'), stage.getAttribute('aria-label')]).toEqual(['dialog', 'true', 'Présentation de la carte']);
    expect([shown(), caption().line, screen.queryByTestId('presenter-heading')]).toEqual([null, 'Le parcours compte 4 étapes.', null]);
    expect(editor.presenter.getState().player.playing).toBe(true);
    // The editor stays under it, its canvas as it was.
    expect(screen.getByTestId('map-canvas')).toBeInTheDocument();
  });

  it('opens by the button « Présenter » of the ribbon, named with its two shortcuts', async () => {
    await showEditor(sampleMap());
    const button = screen.getByTestId('ribbon-present');
    expect([button.getAttribute('aria-label'), button.getAttribute('title')]).toEqual(['Présenter', 'Présenter : depuis le début (F5), depuis l’étape choisie (Maj+F5)']);
    act(() => {
      button.click();
    });
    expect(screen.getByTestId('presenter')).toBeInTheDocument();
  });

  it('starts from the step chosen on Shift+F5, and moves on its keys: next, previous, start', async () => {
    const { editor } = await showEditor(sampleMap());
    act(() => {
      editor.store.setState({ selection: { steps: ['s2'], flows: [] } });
    });
    key('F5', { shiftKey: true });
    key(' ');
    expect(editor.presenter.getState().from).toBe('s2');
    key('ArrowRight');
    expect(shown()).toBe('s2');
    key('ArrowRight');
    expect(shown()).toBe('s3');
    key('ArrowLeft');
    expect(shown()).toBe('s2');
    key('Home');
    expect(shown()).toBe(null);
  });

});

describe('the keys and the controls of a presentation (ADR 097)', () => {
  it('pauses and plays on Space, blanks the screen on B and W, hides the subtitles on C', async () => {
    const { editor } = await showEditor(sampleMap());
    key('F5');
    // Paused when it opens (lot J10); Space plays it, and pauses it again.
    expect(editor.presenter.getState().player.playing).toBe(false);
    key(' ');
    expect(editor.presenter.getState().player.playing).toBe(true);
    key(' ');
    expect(editor.presenter.getState().player.playing).toBe(false);
    key('b');
    expect(screen.getByTestId('presenter-blank')).toHaveClass('presenter-blank--black');
    key('b');
    expect(screen.queryByTestId('presenter-blank')).toBeNull();
    key('w');
    expect(screen.getByTestId('presenter-blank')).toHaveClass('presenter-blank--white');
    key('c');
    expect(screen.queryByTestId('presenter-caption')).toBeNull();
    expect(editor.preferences.getState().presenterSubtitles).toBe(false);
  });

  it('goes to the stop of a chapter clicked, its chapter marked as shown, the count of the steps beside', async () => {
    await showEditor(sampleMap());
    key('F5');
    key(' ');
    act(() => {
      screen.getByTestId('presenter-chapter-2').click();
    });
    expect(shown()).toBe('s3');
    expect(screen.getByTestId('presenter-chapter-2')).toHaveAttribute('aria-current', 'step');
    expect(screen.getByTestId('presenter-progress').textContent).toBe('Étape 3 sur 4');
  });

  it('lets no key of the edition reach the map under it: Suppr deletes nothing, Ctrl+Z undoes nothing', async () => {
    const sent = fakeApi({});
    const { editor } = await showEditor(sampleMap());
    act(() => {
      editor.store.setState({ selection: { steps: ['s2'], flows: [] } });
    });
    key('F5');
    key(' ');
    key('Delete');
    key('z', { ctrlKey: true });
    expect([callsTo(sent, 'DELETE', '/api/steps/s2'), sent.length]).toEqual([[], 0]);
    expect(editor.store.getState().selection.steps).toEqual(['s2']);
  });

});

describe('the clock and the way out of a presentation (ADR 097)', () => {
  it('runs its clock while it plays: the next sentence comes once the first is read', async () => {
    vi.useFakeTimers({ toFake: ['requestAnimationFrame', 'cancelAnimationFrame', 'performance'] });
    await showEditor(sampleMap());
    key('F5');
    key(' ');
    expect(caption().line).toBe('Le parcours compte 4 étapes.');
    act(() => {
      vi.advanceTimersByTime(2600);
    });
    expect(caption().line).toBe('2 acteurs y prennent part.');
  });

  it('takes Échap before a panel left open under it: the presentation ends, the panel stays', async () => {
    const { editor } = await showEditor(sampleMap());
    act(() => {
      editor.floating.getState().open({ kind: 'raci', stepId: 's2', at: { x: 10, y: 10 } });
    });
    expect(screen.getByTestId('raci-panel')).toBeInTheDocument();
    key('F5');
    key(' ');
    key('Escape');
    expect([editor.presenter.getState().active, screen.queryByTestId('raci-panel') !== null]).toEqual([false, true]);
  });

  it('closes on Échap and by its button, the editor shown again', async () => {
    const { editor } = await showEditor(sampleMap());
    key('F5');
    key(' ');
    key('Escape');
    expect(screen.queryByTestId('presenter')).toBeNull();
    key('F5');
    key(' ');
    act(() => {
      within(screen.getByTestId('presenter-bar')).getByTestId('presenter-exit').click();
    });
    expect([screen.queryByTestId('presenter'), editor.presenter.getState().active]).toEqual([null, false]);
  });

  it('is offered to a reader too: its button enabled, F5 opens it', async () => {
    await showEditor(sampleMap({ rights: { structure: false, role: 'reader' } }));
    expect(screen.getByTestId('ribbon-present')).toBeEnabled();
    key('F5');
    key(' ');
    expect(screen.getByTestId('presenter')).toBeInTheDocument();
  });

  it('is not offered on a map without steps: its button disabled, F5 and Maj+F5 without effect (R6-12)', async () => {
    const { editor } = await showEditor(sampleMap({ steps: [], flows: [] }));
    expect(screen.getByTestId('ribbon-present')).toBeDisabled();
    key('F5');
    key(' ');
    key('F5', { shiftKey: true });
    key(' ');
    expect([screen.queryByTestId('presenter'), editor.presenter.getState().active]).toEqual([null, false]);
  });
});

describe('the moves of a presenter by a remote (audit 3.2, UX-03)', () => {
  it('moves by the sentences of the step shown on Page suivante and Page précédente, a whole step on → and ←, paused: a remote skips nothing said', async () => {
    const { editor } = await showEditor(sampleMap({ value_stream: streamOf({ steps: { s2: { work_minutes: 15.5, lead_hours: 14, work_per_case_minutes: 1.68 } as never } }) }));
    act(() => {
      editor.store.setState({ selection: { steps: ['s2'], flows: [] } });
    });
    key('F5', { shiftKey: true });
    key('ArrowRight');
    const scene = () => editor.presenter.getState().player.scene;
    const [step, first] = [scene(), caption().line];
    expect(shown()).toBe('s2');
    key('PageDown');
    expect([scene(), caption().line === first, editor.presenter.getState().player.playing]).toEqual([step, false, false]);
    key('PageUp');
    expect([scene(), caption().line]).toEqual([step, first]);
    key('Enter');
    expect([scene(), caption().line === first]).toEqual([step, false]);
    key('ArrowRight');
    expect([scene(), shown()]).toEqual([step + 2, 's3']);
    key('ArrowLeft');
    expect([scene(), caption().line]).toEqual([step, first]);
    key('ArrowDown');
    expect(shown()).toBe('s3');
    key('ArrowUp');
    expect(shown()).toBe('s2');
  });
});

describe('the exits of a decision during a presentation (ADR 097, lot J10)', () => {
  it('numbers the exits of a decision shown, the one its stop takes filled, and goes at once to the exit typed or clicked: every exit has its stop', async () => {
    const { editor } = await showEditor(sampleMap());
    key('F5');
    for (let index = 0; index < 3; index++) key('ArrowRight');
    expect(shown()).toBe('s3');
    expect([screen.getByTestId('presenter-fork-1').getAttribute('aria-label'), screen.getByTestId('presenter-fork-2').getAttribute('aria-label')]).toEqual([
      'Suivre la sortie 1 : « oui »', 'Suivre la sortie 2 : « non »',
    ]);
    // The tour tells « non » first, back to the check, then shows the decision again for « oui », the exit of its line.
    expect([screen.getByTestId('presenter-fork-1'), screen.getByTestId('presenter-fork-2')].map((button) => button.classList.contains('is-followed'))).toEqual([false, true]);
    expect(caption().line).toBe('Le récit suit « non ».');
    const scene = () => editor.presenter.getState().player.scene;
    const first = scene();
    key('1', { shiftKey: true });
    // The flight of « oui » plays at once, from the stop of the decision that takes it: the one shown again.
    expect([scene(), editor.presenter.getState().player.playing, shown()]).toEqual([first + 3, true, 's3']);
    key(' ');
    key('ArrowRight');
    expect(shown()).toBe('s4');
    key('ArrowLeft');
    expect([shown(), caption().line]).toEqual(['s3', 'Le récit suit « oui ».']);
    expect([screen.getByTestId('presenter-fork-1'), screen.getByTestId('presenter-fork-2')].map((button) => button.classList.contains('is-followed'))).toEqual([true, false]);
    act(() => {
      screen.getByTestId('presenter-fork-2').click();
    });
    // « non » is taken from the first stop of the decision: its flight back to the check, lit, and no step told again.
    expect([scene(), editor.presenter.getState().player.playing]).toEqual([first + 1, true]);
    key(' ');
    key('ArrowRight');
    expect([scene(), shown()]).toEqual([first + 2, 's3']);
    // The decision shown again has no mark of its own in the bar: the marks are the steps, each once.
    expect(screen.queryByTestId('presenter-chapter-3')).toBeNull();
    act(() => {
      screen.getByTestId('presenter-chapter-4').click();
    });
    expect([shown(), screen.getByTestId('presenter-progress').textContent]).toEqual(['s4', 'Étape 4 sur 4']);
  });
});

describe('the counters of a presentation (ADR 097, A1 of the study)', () => {
  it('count up to the figures of the step reached, in pause too, rounded as the sentences: a presenter who moves by a remote sees them', async () => {
    vi.useFakeTimers({ toFake: ['requestAnimationFrame', 'cancelAnimationFrame', 'performance'] });
    const { editor } = await showEditor(sampleMap({ value_stream: streamOf({ steps: { s2: { work_minutes: 15.5, lead_hours: 14, work_per_case_minutes: 1.68 } as never } }) }));
    act(() => {
      editor.store.setState({ selection: { steps: ['s2'], flows: [] } });
    });
    key('F5', { shiftKey: true });
    key('ArrowRight');
    expect([shown(), editor.presenter.getState().player.playing]).toEqual(['s2', false]);
    act(() => {
      vi.advanceTimersByTime(1000);
    });
    // The work by case of a step done by sessions, rounded as its sentence says it.
    expect(screen.getByTestId('presenter-counter-work')).toHaveTextContent(/1,7/);
    expect(screen.getByTestId('presenter-counter-lead')).toHaveTextContent(/2/);
  });
});

/** The host's answer read: the project of these tests lets the online voices be offered, once the presentation has asked (F-137). */
const settle = () => act(async () => {
  await Promise.resolve();
});

/** V, then the voice of the computer when the stage asks which one (essai J2 of 3.2). */
const voiceOn = () => {
  key('v');
  const local = screen.queryByTestId('presenter-voice-local');
  if (local !== null) {
    act(() => {
      local.click();
    });
  }
};

describe('the voice of a presentation (ADR 097, F-119)', () => {
  it('is off by default; V reads the sentence shown with a voice of the computer, never an online one unless allowed', async () => {
    const spoken = fakeSpeech();
    const { editor } = await showEditor(sampleMap());
    key('F5');
    key(' ');
    expect(spoken.length).toBe(0);
    voiceOn();
    expect(editor.preferences.getState().presenterVoice).toBe(true);
    expect(spoken.map(({ text, voice, rate }) => [text, voice, rate])).toEqual([['Le parcours compte 4 étapes.', 'Hortense', 1]]);
    expect(editor.presenter.getState().speaking).toBe(true);
    // In pause, the voice is silent and holds nothing.
    key(' ');
    expect([editor.presenter.getState().speaking, spoken.length]).toEqual([false, 1]);
  });

  it('holds the clock at the end of a sentence until the voice has said it', async () => {
    vi.useFakeTimers({ toFake: ['requestAnimationFrame', 'cancelAnimationFrame', 'performance'] });
    const spoken = fakeSpeech();
    await showEditor(sampleMap());
    key('F5');
    key(' ');
    voiceOn();
    // The subtitles, hidden while the voice speaks, shown again by C.
    key('c');
    act(() => {
      vi.advanceTimersByTime(6000);
    });
    expect(caption().line).toBe('Le parcours compte 4 étapes.');
    act(() => {
      spoken[0]?.end();
      vi.advanceTimersByTime(100);
    });
    expect(caption().line).toBe('2 acteurs y prennent part.');
    expect(spoken.at(-1)?.text).toBe('2 acteurs y prennent part.');
  });

});

describe('the settings of the voice of a presentation (ADR 097, F-119)', () => {
  it('offers the online voices once allowed in its settings, said for what they do with the text', async () => {
    const spoken = fakeSpeech();
    const { editor } = await showEditor(sampleMap());
    key('F5');
    key(' ');
    await settle();
    voiceOn();
    act(() => {
      screen.getByTestId('presenter-voice-settings').click();
    });
    const choice = screen.getByTestId('presenter-voice-choice');
    expect(within(choice).getAllByRole('option').map((option) => option.textContent)).toEqual(['Hortense (sur le poste)']);
    expect(screen.getByTestId('presenter-voice-panel')).toHaveTextContent('Microsoft pour Edge, Google pour Chrome');
    act(() => {
      screen.getByTestId('presenter-voice-online').click();
    });
    expect(editor.preferences.getState().presenterOnlineVoices).toBe(true);
    fireEvent.change(screen.getByTestId('presenter-voice-choice'), { target: { value: 'Denise Online' } });
    expect(spoken.at(-1)?.voice).toBe('Denise Online');
  });

  it('closes its settings by Échap, the presentation going on (out of the full screen; in it, see below), or by a click beside them (audit 3.2, BUG-06)', async () => {
    fakeSpeech();
    const { editor } = await showEditor(sampleMap());
    key('F5');
    key(' ');
    voiceOn();
    const open = () => {
      act(() => {
        screen.getByTestId('presenter-voice-settings').click();
      });
      expect(screen.getByTestId('presenter-voice-panel')).toBeInTheDocument();
    };
    open();
    key('Escape');
    expect(screen.queryByTestId('presenter-voice-panel')).toBeNull();
    expect(editor.presenter.getState().active).toBe(true);
    open();
    fireEvent.pointerDown(screen.getByTestId('presenter-canvas'));
    expect(screen.queryByTestId('presenter-voice-panel')).toBeNull();
    open();
    fireEvent.pointerDown(screen.getByTestId('presenter-voice-online'));
    expect(screen.getByTestId('presenter-voice-panel')).toBeInTheDocument();
  });
});

describe('the settings of the voice in full screen (relecture 6: R6-01, R6-12)', () => {
  it('keep Échap for themselves: held from the browser where it can be, and the presentation kept when the browser leaves the full screen on it', async () => {
    fakeSpeech();
    const lock = vi.fn(() => Promise.resolve());
    const unlock = vi.fn();
    const { editor } = await showEditor(sampleMap());
    key('F5');
    key(' ');
    voiceOn();
    const full: { element: Element | null } = { element: screen.getByTestId('presenter') };
    Object.defineProperty(document, 'fullscreenElement', { configurable: true, get: () => full.element });
    Object.defineProperty(navigator, 'keyboard', { configurable: true, value: { lock, unlock } });
    try {
      act(() => {
        screen.getByTestId('presenter-voice-settings').click();
      });
      await act(async () => {
        await Promise.resolve();
      });
      expect(lock).toHaveBeenCalledWith(['Escape']);
      // A browser that cannot hold Échap leaves the full screen on it: the presentation stays, its settings open.
      full.element = null;
      fullscreenChanged();
      expect([editor.presenter.getState().active, screen.queryByTestId('presenter-voice-panel') !== null]).toEqual([true, true]);
      // The lock is given back only once the key is up, or the browser would take its release for an Échap of its own.
      key('Escape');
      expect([editor.presenter.getState().active, screen.queryByTestId('presenter-voice-panel'), unlock.mock.calls.length]).toEqual([true, null, 0]);
      keyUp('Escape');
      expect(unlock.mock.calls.length).toBe(1);
      // The full screen left right after the Échap that closed the settings is the doing of that key: the presentation stays.
      fullscreenChanged();
      expect(editor.presenter.getState().active).toBe(true);
      // Nothing open any more: a later full screen left ends the presentation, as before.
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
});

describe('a sentence of the voice said once (essai J2 of 3.2)', () => {
  it('says a sentence once, whatever the stage draws again meanwhile: a move of the mouse, the voices told again (essai J2)', async () => {
    const spoken = fakeSpeech();
    await showEditor(sampleMap());
    key('F5');
    key(' ');
    voiceOn();
    expect(spoken.map(({ text }) => text)).toEqual(['Le parcours compte 4 étapes.']);
    act(() => {
      fireEvent.mouseMove(screen.getByTestId('presenter'));
    });
    act(() => {
      spoken.voicesChanged();
    });
    expect([spoken.length, spoken.cuts()]).toEqual([1, 0]);
    // The next sentence follows the end of the first, nothing cut before it: the voice is told to stop only while it speaks.
    act(() => {
      spoken[0]?.end();
    });
    expect([spoken.cuts(), spoken.cancels()]).toEqual([0, 0]);
  });
});

describe('the light of a passage (essai J2 of 3.2)', () => {
  it('is never drawn inside a step: its stroke and its point are born at the edge of the box they leave', async () => {
    const { editor } = await showEditor(sampleMap());
    key('F5');
    act(() => {
      editor.presenter.setState({ player: { ...editor.presenter.getState().player, scene: 2, elapsed: 0 } });
    });
    const light = screen.getByTestId('presenter-light');
    const mask = light.querySelector('mask');
    expect(mask).not.toBeNull();
    const id = mask?.getAttribute('id') ?? '';
    expect(light.querySelector('[data-testid="presenter-light-flows"]')?.getAttribute('mask')).toBe(`url(#${id})`);
    // The map shown, each step hidden (the white and the black of the presentation, from the tokens): the light shows round the boxes, never in them.
    expect(mask?.querySelectorAll('rect.presenter-light-mask-shown').length).toBe(1);
    const holes = [...(mask?.querySelectorAll('rect.presenter-light-mask-hidden') ?? [])].map((rect) => ['x', 'y', 'width', 'height'].map((name) => Number(rect.getAttribute(name))));
    // The four steps of the sample map: its start, its task, its decision and its end.
    expect(holes.length).toBe(4);
    expect(holes.every(([, , width, height]) => (width ?? 0) > 0 && (height ?? 0) > 0)).toBe(true);
  });
});

describe('the question of the voice (essai J2 of 3.2)', () => {
  it('asks once, on the first V, the voice of the computer or a natural online one, said for where the text goes; the subtitles hidden while it speaks', async () => {
    const spoken = fakeSpeech();
    const { editor } = await showEditor(sampleMap());
    key('F5');
    key(' ');
    await settle();
    key('v');
    const question = screen.getByTestId('presenter-voice-question');
    expect(question).toHaveTextContent('Microsoft');
    expect([editor.preferences.getState().presenterVoice, spoken.length]).toEqual([false, 0]);
    act(() => {
      within(question).getByTestId('presenter-voice-natural').click();
    });
    const preferences = editor.preferences.getState();
    expect([preferences.presenterVoice, preferences.presenterOnlineVoices, preferences.presenterVoiceAsked, preferences.presenterSubtitles]).toEqual([true, true, true, false]);
    expect([spoken.at(-1)?.voice, screen.queryByTestId('presenter-voice-question'), caption().line]).toEqual(['Denise Online', null, null]);
    // C shows the subtitles over the voice; V off brings them back; V on again asks nothing.
    key('c');
    expect(caption().line).toBe('Le parcours compte 4 étapes.');
    key('v');
    expect([editor.preferences.getState().presenterVoice, editor.preferences.getState().presenterSubtitles]).toEqual([false, true]);
    key('v');
    expect([screen.queryByTestId('presenter-voice-question'), editor.preferences.getState().presenterVoice]).toEqual([null, true]);
  });

  it('is left by Échap, the voice still off and the presentation going on; asks nothing where the browser has no online voice', async () => {
    fakeSpeech();
    const { editor } = await showEditor(sampleMap());
    key('F5');
    key(' ');
    await settle();
    key('v');
    key('Escape');
    expect([screen.queryByTestId('presenter-voice-question'), editor.preferences.getState().presenterVoice, editor.presenter.getState().active]).toEqual([null, false, true]);
    vi.unstubAllGlobals();
    const spoken = fakeSpeech({ online: false });
    key('Escape');
    key('F5');
    key(' ');
    key('v');
    expect([screen.queryByTestId('presenter-voice-question'), editor.preferences.getState().presenterVoice, spoken.at(-1)?.voice]).toEqual([null, true, 'Hortense']);
  });
});

describe('a decision shown again, in the bar and under the keys (lot J10, independent review, M4)', () => {
  it('marks the step of a decision shown again by the mark of its first stop, with its rank, and takes the exit typed there', async () => {
    const { editor } = await showEditor(sampleMap());
    key('F5');
    for (let index = 0; index < 4; index++) key('ArrowRight');
    const again = editor.presenter.getState().player.scene;
    expect([shown(), caption().line]).toEqual(['s3', 'Le récit suit « oui ».']);
    // One mark for the decision, the one of its first stop: current here too, and « Étape 3 sur 4 », not the rank of the last step told.
    expect(screen.getByTestId('presenter-chapter-2')).toHaveAttribute('aria-current', 'step');
    expect(screen.getByTestId('presenter-chapter-2')).toHaveClass('is-shown');
    expect(screen.getByTestId('presenter-progress').textContent).toBe('Étape 3 sur 4');
    // « non », typed from the decision shown again, goes to the stop that takes it: the first one.
    key('2', { shiftKey: true });
    expect([editor.presenter.getState().player.scene, editor.presenter.getState().player.playing]).toEqual([again - 1, true]);
  });
});

describe('the messages of the steps and of the handoffs, each turned off alone (lot J10, asked by the project owner)', () => {
  /** The sample with a figure on its check, and a handoff the core counts on « oui », from the decision to the invoice. */
  const told = () => sampleMap({
    value_stream: streamOf({
      steps: { s2: { work_minutes: 15, lead_hours: 16, work_per_case_minutes: 15 } as never },
      handoffs: { flows: ['f3'], detailed_steps: {}, count: 1, per_case: null },
    }),
  });

  it('says nothing of the steps once their messages are off, by E or its button, the handoffs still said; the opening and the summary stay', async () => {
    const { editor } = await showEditor(told());
    key('F5');
    key('ArrowRight');
    key('ArrowRight');
    // The figures keep their no-break spaces on the screen: the sentence is read by its start and its end.
    expect([shown(), caption().line?.startsWith('15'), caption().line?.endsWith('de délai.')]).toEqual(['s2', true, true]);
    key('e');
    expect([editor.preferences.getState().presenterStepMessages, shown(), caption().line]).toEqual([false, 's2', null]);
    expect(screen.getByTestId('presenter-step-messages')).toHaveAttribute('aria-pressed', 'false');
    // The decision shown again says nothing either; the way to the invoice still says its handoff.
    key('ArrowRight');
    key('ArrowRight');
    expect([shown(), caption().line]).toEqual(['s3', null]);
    key('PageDown');
    expect(caption().line).toBe('Relais : Commercial → Comptabilité.');
    // The opening and the summary are not messages of a step: they stay.
    key('Home');
    expect(caption().line).toBe('Le parcours compte 4 étapes.');
    key('End');
    expect(caption().line).toBe('Fin du parcours.');
    act(() => {
      screen.getByTestId('presenter-step-messages').click();
    });
    expect(editor.preferences.getState().presenterStepMessages).toBe(true);
  });

  it('says nothing on the ways once the messages of the handoffs are off, by R or its button, the steps still said; both off, the presenter speaks alone', async () => {
    const { editor } = await showEditor(told());
    key('F5');
    key('r');
    expect(editor.preferences.getState().presenterFlowMessages).toBe(false);
    expect(screen.getByTestId('presenter-flow-messages')).toHaveAttribute('aria-pressed', 'false');
    for (let index = 0; index < 4; index++) key('ArrowRight');
    expect([shown(), caption().line]).toEqual(['s3', 'Le récit suit « oui ».']);
    const decision = editor.presenter.getState().player.scene;
    key('PageDown');
    // The way to the invoice, its light flying, says no handoff.
    expect([editor.presenter.getState().player.scene, caption().line]).toEqual([decision + 1, null]);
    act(() => {
      screen.getByTestId('presenter-flow-messages').click();
    });
    expect(caption().line).toBe('Relais : Commercial → Comptabilité.');
    key('r');
    key('e');
    key('Home');
    key('ArrowRight');
    key('ArrowRight');
    expect([shown(), caption().line]).toEqual(['s2', null]);
  });
});
