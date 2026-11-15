// voice.ts
// The voice of a presentation (ADR 097, F-119): the voices of the browser for the language shown, those of the computer first, the online ones (Edge, Chrome: the text leaves for the maker of the browser) only when the person allows them; the voice that reads among them, the one picked during the session, else the one the project keeps, else the one the browser kept (lot J10); the sentence as the voice says it.

import type { Translate } from '../../../../shared/i18n/i18n.ts';

/** What a presentation reads of a voice of the browser (`SpeechSynthesisVoice`). */
export type VoiceOption = Pick<SpeechSynthesisVoice, 'name' | 'lang' | 'localService'>;

/** The language of a voice, its first part (`fr-FR`, `fr_CA`, `FR` give `fr`). */
const languageOf = (voice: VoiceOption): string => voice.lang.toLowerCase().split(/[-_]/)[0] ?? '';

/** The voices of a language: those of the computer, then the online ones when they are allowed, each group in the order of the browser. */
export function voicesFor<Voice extends VoiceOption>(voices: readonly Voice[], language: string, online: boolean): Voice[] {
  const spoken = voices.filter((voice) => languageOf(voice) === language.toLowerCase());
  return [...spoken.filter((voice) => voice.localService), ...(online ? spoken.filter((voice) => !voice.localService) : [])];
}

/** A voice named, and where it speaks: what a project keeps for a language, what a person picks during a session. */
export interface VoiceMark {
  readonly name: string;
  readonly local: boolean;
}

/**
 * The voice that reads among those offered (lot J10): the one picked in the settings during the session, else the one
 * the project keeps for the language shown, else the one the browser kept by its name, else the first. A voice marked
 * is found by its name and where it speaks, among the voices offered only: an online voice the person or the
 * classification of the project does not allow is not offered, so never taken (F-137), nor one this computer has not.
 */
export function readerOf<Voice extends VoiceOption>(offered: readonly Voice[], picked: VoiceMark | null, project: VoiceMark | null, kept: string | null): Voice | null {
  const marked = (mark: VoiceMark | null) => (mark === null ? undefined : offered.find((voice) => voice.name === mark.name && voice.localService === mark.local));
  return marked(picked) ?? marked(project) ?? offered.find((voice) => voice.name === kept) ?? offered[0] ?? null;
}

/** The arrow of a handoff in the sentences (« Relais : Commercial → Comptabilité »), by its code point: no sign is written in a script. */
export const ARROW = String.fromCodePoint(0x2192);

/** A sentence as the voice says it: the arrow of a handoff said as a word of the language. */
export const spokenOf = (line: string, t: Translate): string => line.replaceAll(ARROW, t('presenter.say.arrow'));
