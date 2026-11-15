// voiceSwitch.ts
// The voice of a presentation turned on and off (essai J2 of 3.2): asked once which voice where the browser has online ones and the classification of the project lets its texts leave (the voice of the computer, nothing leaves; a natural one, the text leaves for the maker of the browser; F-137); the subtitles hidden while a voice reads them, shown again when it stops.

import type { MapEditor } from '../state/editor.ts';
import type { PresenterVoice } from './usePresenterVoice.ts';

/** The natural voice offered first: one said « Natural » (Edge), else one of the country of the language (`fr-FR`), else the first. */
export function naturalOf(voices: readonly SpeechSynthesisVoice[], language: string): SpeechSynthesisVoice | null {
  const home = `${language.toLowerCase()}-${language.toUpperCase()}`.toLowerCase();
  return voices.find((voice) => /natural/i.test(voice.name)) ?? voices.find((voice) => voice.lang.toLowerCase() === home) ?? voices[0] ?? null;
}

/**
 * Turns the voice on, `natural` the answer to the question (null when none was asked): the answer kept, the voice chosen
 * kept by the browser (forgotten for the voice of the computer, UX-MOY-01), and the subtitles hidden while a voice reads
 * them; kept where no voice can read them. A natural voice is one the classification of the project allows (F-137).
 */
export function turnVoiceOn(editor: MapEditor, voice: PresenterVoice, language: string, natural: boolean | null): void {
  const preferences = editor.preferences.getState();
  const presenter = editor.presenter.getState();
  if (natural !== null) {
    preferences.set('presenterOnlineVoices', natural);
    preferences.set('presenterVoiceAsked', true);
    const first = natural ? naturalOf(voice.natural, language) : null;
    preferences.setVoiceName(first?.name ?? null);
    // The natural voice answered is what the person chose for this session: it reads before the voice the project keeps, which never
    // takes its place once the person agreed to it (independent review of the lot J10). The project keeps its own: nothing is written.
    // The answer says a kind of voice, not a voice: when the project keeps an online one this computer offers, that one reads, the
    // person having just agreed to its kind (third independent review).
    const kept = voice.projectVoice;
    const ofProject = kept !== null && !kept.local && voice.natural.some((one) => one.name === kept.name);
    if (first !== null && !ofProject) presenter.pick(language, { name: first.name, local: first.localService });
  }
  presenter.ask(false);
  const readers = natural === null ? voice.voices : natural ? voice.natural : voice.local;
  preferences.set('presenterVoice', true);
  if (readers.length > 0) preferences.set('presenterSubtitles', false);
}

/** V and the button of the voice: off, the subtitles back and its settings closed; on, the question asked once where online voices are offered (`natural`, F-137). */
export function switchVoice(editor: MapEditor, voice: PresenterVoice, language: string): void {
  const preferences = editor.preferences.getState();
  const presenter = editor.presenter.getState();
  if (preferences.presenterVoice) {
    preferences.set('presenterVoice', false);
    preferences.set('presenterSubtitles', true);
    presenter.tune(false);
    return;
  }
  if (!preferences.presenterVoiceAsked && voice.natural.length > 0) presenter.ask(!presenter.asking);
  else turnVoiceOn(editor, voice, language, null);
}
