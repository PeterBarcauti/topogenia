// speech.ts
// The speech of a browser in a test of the presentation (ADR 097, F-119): its voices given, what it is asked to say kept with the voice that says it and where it speaks, what it cuts counted.

import { vi } from 'vitest';

/** A voice of the fake speech: its name, its language, whether it is one of the computer. */
export interface FakeVoice {
  readonly name: string;
  readonly lang: string;
  readonly localService: boolean;
}

/** Two French voices: one of the computer, and one online unless it is left out. */
const frenchVoices = (online: boolean): FakeVoice[] => [
  ...(online ? [{ name: 'Denise Online', lang: 'fr-FR', localService: false }] : []), { name: 'Hortense', lang: 'fr-FR', localService: true },
];

/**
 * A speech of the browser that keeps what it is asked to say: two French voices by default, one of the computer and one online
 * (`online: false` leaves it out), or the voices given, as new objects at each call as Chromium does; it says whether it speaks,
 * counts what it cuts and every call to cut, and tells its voices changed when asked. Undone by `vi.unstubAllGlobals()`.
 */
export function fakeSpeech({ online = true, voices = frenchVoices(online) }: { online?: boolean; voices?: readonly FakeVoice[] } = {}) {
  const spoken: { text: string; voice: string | undefined; local: boolean | undefined; rate: number; end: () => void }[] = [];
  const heard: (() => void)[] = [];
  const synth = { speaking: false, cuts: 0, cancels: 0 };
  class Utterance {
    voice: { name: string; localService?: boolean } | null = null;
    lang = '';
    rate = 1;
    onend: (() => void) | null = null;
    onerror: (() => void) | null = null;
    constructor(readonly text: string) {}
  }
  vi.stubGlobal('SpeechSynthesisUtterance', Utterance);
  vi.stubGlobal('speechSynthesis', {
    get speaking() {
      return synth.speaking;
    },
    pending: false,
    getVoices: () => voices.map((voice) => ({ ...voice })),
    cancel: () => {
      synth.cancels++;
      if (synth.speaking) synth.cuts++;
      synth.speaking = false;
    },
    addEventListener: (_name: string, listener: () => void) => heard.push(listener),
    removeEventListener: () => undefined,
    speak: (utterance: Utterance) => {
      synth.speaking = true;
      spoken.push({ text: utterance.text, voice: utterance.voice?.name, local: utterance.voice?.localService, rate: utterance.rate, end: () => {
        synth.speaking = false;
        utterance.onend?.();
      } });
    },
  });
  return Object.assign(spoken, { cuts: () => synth.cuts, cancels: () => synth.cancels, voicesChanged: () => {
    for (const listener of heard) listener();
  } });
}
