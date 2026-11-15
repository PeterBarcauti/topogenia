// voice.test.ts
// The voice of a presentation: the voices of the language shown, those of the computer first, the online ones only when the person allows them; the sentence as the voice says it; the clock held at the end of a sentence still being read.

import i18next from 'i18next';
import { describe, expect, it } from 'vitest';
import { advance } from './player.ts';
import { lineEndAt } from './timing.ts';
import { readerOf, spokenOf, voicesFor } from './voice.ts';

const voice = (name: string, lang: string, localService: boolean) => ({ name, lang, localService, voiceURI: name, default: false });
const VOICES = [voice('Denise Online', 'fr-FR', false), voice('Hortense', 'fr-FR', true), voice('Zira', 'en-US', true), voice('Julie', 'fr-CA', true), voice('Paul', 'FR_fr', true)];

describe('the voices of a presentation (ADR 097, F-119)', () => {
  it('are those of the language shown, the voices of the computer only unless the online ones are allowed', () => {
    expect(voicesFor(VOICES, 'fr', false).map((one) => one.name)).toEqual(['Hortense', 'Julie', 'Paul']);
    expect(voicesFor(VOICES, 'fr', true).map((one) => one.name)).toEqual(['Hortense', 'Julie', 'Paul', 'Denise Online']);
    expect(voicesFor(VOICES, 'de', true)).toEqual([]);
  });

  it('say the arrow of a handoff as a word of the language', () => {
    expect(spokenOf('Relais : Commercial → Comptabilité.', i18next.getFixedT('fr'))).toBe('Relais : Commercial vers Comptabilité.');
  });
});

describe('the voice that reads (lot J10)', () => {
  const offered = voicesFor(VOICES, 'fr', true);
  const mark = (name: string, local = true) => ({ name, local });

  it('is the one picked during the session, else the one the project keeps for the language, else the one the browser kept, else the first', () => {
    expect(readerOf(offered, mark('Paul'), mark('Julie'), 'Hortense')?.name).toBe('Paul');
    expect(readerOf(offered, null, mark('Julie'), 'Paul')?.name).toBe('Julie');
    expect(readerOf(offered, null, null, 'Paul')?.name).toBe('Paul');
    expect(readerOf(offered, null, null, null)?.name).toBe('Hortense');
    expect(readerOf([], mark('Paul'), mark('Julie'), 'Hortense')).toBeNull();
  });

  it('is never a voice that is not offered: an online one the person or the project does not allow, one this computer has not, one of the same name that speaks elsewhere', () => {
    expect(readerOf(voicesFor(VOICES, 'fr', false), null, mark('Denise Online', false), 'Julie')?.name).toBe('Julie');
    expect(readerOf(offered, null, mark('Absente'), null)?.name).toBe('Hortense');
    expect(readerOf(offered, null, mark('Denise Online', true), null)?.name).toBe('Hortense');
    expect(readerOf(offered, mark('Hortense', false), mark('Paul'), null)?.name).toBe('Paul');
  });
});

describe('the clock held by the voice', () => {
  it('knows when the sentence shown ends, the last one at the end of its scene', () => {
    expect([0, 1599, 1600, 9000].map((at) => lineEndAt(['a', 'b'], at))).toEqual([1600, 1600, 3200, 3200]);
    expect(lineEndAt([], 500)).toBe(Number.POSITIVE_INFINITY);
  });

  it('stops at the hold given, in its scene, until the voice has said the sentence', () => {
    const state = { scene: 1, elapsed: 1500, playing: true, speed: 1 };
    expect(advance(state, 500, [1000, 2000, 1000], 1599)).toEqual({ ...state, elapsed: 1599 });
    expect(advance(state, 300, [1000, 2000, 1000], 1599)).toEqual({ ...state, elapsed: 1599 });
    expect(advance(state, 300, [1000, 2000, 1000])).toEqual({ ...state, elapsed: 1800 });
  });
});
