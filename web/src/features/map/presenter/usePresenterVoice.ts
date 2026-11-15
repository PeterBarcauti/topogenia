// usePresenterVoice.ts
// The voice of a presentation (ADR 097, F-119): the sentence shown read aloud by a voice of the browser for the language shown, the clock held until it is said; the voices of the computer by default, the online ones only when the classification of the project lets its texts leave the computer (F-137) and the person allows them; the voice that reads is the one picked in its settings during the session, else the one the project keeps for the language shown, else the one the browser kept (UX-MOY-01, lot J10).

import { useCallback, useEffect, useState } from 'react';
import { useStore } from 'zustand';
import type { MapEditor } from '../state/editor.ts';
import { lineAt } from './model/timing.ts';
import { readerOf, spokenOf, voicesFor, type VoiceMark } from './model/voice.ts';
import { chooseVoice, forgetVoice, useProjectVoices } from './projectVoices.ts';
import type { Presentation } from './usePresentation.ts';

/** The speech of the browser, null where it has none (a test, an old browser). */
const speech = (): SpeechSynthesis | null => ('speechSynthesis' in window ? window.speechSynthesis : null);

/** A sentence that never says it ended (a voice that hangs) frees the clock after this long, or more for a long one. */
const LONGEST_SILENCE_MS = 8000;
const MS_A_CHARACTER = 200;

/** A browser that has told no voice after this long has none: its voices come late in Chromium, a moment after they are first asked. */
const VOICES_LATE_MS = 1000;

/**
 * The voices of the browser, read again when it tells they changed (they come late in Chromium), and whether they are
 * known: some are there, the browser told them, it has no speech, or it told none for long enough.
 */
function useVoices(): { readonly voices: readonly SpeechSynthesisVoice[]; readonly known: boolean } {
  const [voices, setVoices] = useState<readonly SpeechSynthesisVoice[]>(() => speech()?.getVoices() ?? []);
  const [told, setTold] = useState(false);
  useEffect(() => {
    const synth = speech();
    if (synth === null) return undefined;
    const changed = () => {
      setVoices(synth.getVoices());
      setTold(true);
    };
    synth.addEventListener('voiceschanged', changed);
    const late = synth.getVoices().length > 0 ? undefined : setTimeout(() => {
      setTold(true);
    }, VOICES_LATE_MS);
    return () => {
      synth.removeEventListener('voiceschanged', changed);
      clearTimeout(late);
    };
  }, []);
  return { voices, known: told || voices.length > 0 || speech() === null };
}

export interface PresenterVoice {
  /** The voices offered for the language shown, those of the computer first. */
  readonly voices: readonly SpeechSynthesisVoice[];
  /** The voice that reads, null when none is offered. */
  readonly voice: SpeechSynthesisVoice | null;
  /**
   * The voices of the computer for the language shown, and the online ones the browser has, allowed by the person or not:
   * what the question offers. None online for a project whose classification keeps its texts on the computer (F-137).
   */
  readonly local: readonly SpeechSynthesisVoice[];
  readonly natural: readonly SpeechSynthesisVoice[];
  /** The classification of the project lets the online voices be offered, as the host said when the presentation started (F-137). */
  readonly allowed: boolean;
  /** No voice can say the language shown, the voices of the browser known: the voice on, its sentences are then read on the screen (R6-04). */
  readonly silent: boolean;
  /** Chooses the voice that reads the language shown: for the session, and for the project when the person is one of its facilitators (lot J10). */
  readonly choose: (voice: VoiceMark) => void;
  /** The voice the project keeps for the language shown, null when it keeps none (lot J10). */
  readonly projectVoice: VoiceMark | null;
  /** The person may choose the voice the project keeps: one of its facilitators. */
  readonly keeps: boolean;
  /** Removes the voice the project keeps for the language shown; the voice that reads goes on for the session. */
  readonly forget: () => void;
}

/**
 * Reads the sentence shown while the voice is on and the presentation plays; silent otherwise. `allowed`: the
 * classification of the project lets the online voices be offered (F-137); never one of them otherwise.
 */
export function usePresenterVoice(editor: MapEditor, presentation: Presentation, allowed: boolean): PresenterVoice {
  const on = useStore(editor.preferences, (state) => state.presenterVoice);
  const online = useStore(editor.preferences, (state) => state.presenterOnlineVoices);
  const chosen = useStore(editor.preferences, (state) => state.presenterVoiceName);
  const playing = useStore(editor.presenter, (state) => state.player.playing && state.screen === 'map');
  const speed = useStore(editor.presenter, (state) => state.player.speed);
  const moment = useStore(editor.presenter, (state) => {
    const lines = presentation.scripts[state.player.scene]?.lines ?? [];
    return `${String(state.player.scene)} ${String(lineAt(lines, state.player.elapsed))}`;
  });
  const { voices: all, known } = useVoices();
  const voices = voicesFor(all, presentation.language, online && allowed);
  const reachable = voicesFor(all, presentation.language, allowed);
  const picked = useStore(editor.presenter, (state) => state.picked[presentation.language] ?? null);
  const kept = useStore(editor.presenter, (state) => state.projectVoices.find((one) => one.language === presentation.language) ?? null);
  const { project_id: projectId, rights } = presentation.map;
  useProjectVoices(editor, projectId);
  const voice = readerOf(voices, picked, kept, chosen);
  // The effect hangs on what is said and by whom, never on the objects of a render: the stage drawn again (a move of the mouse, the voices
  // told again by the browser, as new objects in Chromium) cut the sentence and said it again from its start (essai J2).
  const [scene, line] = moment.split(' ').map(Number) as [number, number];
  const shown = presentation.scripts[scene]?.lines[line];
  const said = shown === undefined ? undefined : spokenOf(shown, presentation.t);
  const name = voice?.name ?? null;
  // Found again by its name and where it speaks: an online voice that would bear the name of one of the computer is never taken for it.
  const local = voice?.localService ?? true;
  useEffect(() => {
    const synth = speech();
    const presenter = editor.presenter.getState();
    const by = synth?.getVoices().find((candidate) => candidate.name === name && candidate.localService === local) ?? null;
    // Only what is being said is cut: nothing is cut before a sentence that follows the end of the last one.
    if (synth !== null && (synth.speaking || synth.pending)) synth.cancel();
    if (synth === null || !on || !playing || by === null || said === undefined) {
      presenter.speak(false);
      return undefined;
    }
    let live = true;
    const done = () => {
      if (live) editor.presenter.getState().speak(false);
    };
    const utterance = new SpeechSynthesisUtterance(said);
    utterance.voice = by;
    utterance.lang = by.lang;
    utterance.rate = Math.min(2, Math.max(0.5, speed));
    utterance.onend = done;
    utterance.onerror = done;
    const guard = setTimeout(done, Math.max(LONGEST_SILENCE_MS, said.length * MS_A_CHARACTER));
    presenter.speak(true);
    synth.speak(utterance);
    return () => {
      live = false;
      clearTimeout(guard);
    };
  }, [editor, moment, said, on, playing, name, local, speed]);
  useEffect(() => () => {
    speech()?.cancel();
    editor.presenter.getState().speak(false);
  }, [editor]);
  const keeps = rights.role === 'facilitator';
  const { language } = presentation;
  const choose = useCallback((mark: VoiceMark) => {
    void chooseVoice(editor, { id: projectId, keeps }, language, mark);
  }, [editor, projectId, keeps, language]);
  const forget = useCallback(() => {
    if (name !== null) void forgetVoice(editor, { id: projectId, keeps }, language, { name, local });
  }, [editor, projectId, keeps, language, name, local]);
  return {
    voices, voice, local: reachable.filter((candidate) => candidate.localService), natural: reachable.filter((candidate) => !candidate.localService), allowed,
    silent: known && voice === null, choose, projectVoice: kept === null ? null : { name: kept.name, local: kept.local }, keeps, forget,
  };
}
