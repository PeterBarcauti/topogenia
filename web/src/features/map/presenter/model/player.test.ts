// player.test.ts
// The reading of a presentation: the time of a sentence and of a scene, the clock that runs at the speed chosen, the moves of the presenter (next, previous, start, end, a stop chosen).

import { describe, expect, it } from 'vitest';
import { flightMs, lineMs, lineAt, sceneMs, STEP_BARE_MS } from './timing.ts';
import { advance, PLAYER_START, playerTo, type PlayerState } from './player.ts';
import type { Scene } from './script.ts';

describe('the time of a sentence (subtitles, ADR 097)', () => {
  it('lasts the time to read it at fifteen characters a second, never less than its floor nor more than its ceiling', () => {
    expect(lineMs('Fin.')).toBe(1600);
    expect(lineMs('x'.repeat(60))).toBe(4000 + 300);
    expect(lineMs('x'.repeat(400))).toBe(7000);
  });

  it('a scene lasts its sentences one after the other, a step without any the time to be seen', () => {
    expect(sceneMs({ kind: 'step', stop: 0 }, ['a', 'b'])).toBe(3200);
    expect(sceneMs({ kind: 'step', stop: 0 }, [])).toBe(STEP_BARE_MS);
    expect(sceneMs({ kind: 'opening' }, [])).toBe(STEP_BARE_MS);
  });

  it('a passage lasts its flight, by the length of the flow, or the time of its sentences when longer', () => {
    expect(flightMs(100)).toBe(1200);
    expect(flightMs(2000)).toBe(3000);
    expect(sceneMs({ kind: 'passage', stop: 0 }, [], 2000)).toBe(3000);
    expect(sceneMs({ kind: 'passage', stop: 0 }, ['a', 'b'], 100)).toBe(3200);
  });

  it('shows at each moment the sentence of its time, the last one until the scene ends', () => {
    const lines = ['a', 'b'];
    expect([0, 1599, 1600, 3199, 9000].map((at) => lineAt(lines, at))).toEqual([0, 0, 1, 1, 1]);
    expect(lineAt([], 500)).toBe(-1);
  });
});

const SCENES: Scene[] = [{ kind: 'opening' }, { kind: 'step', stop: 0 }, { kind: 'passage', stop: 0 }, { kind: 'step', stop: 1 }, { kind: 'closing' }];
const DURATIONS = [1000, 2000, 1500, 2000, 1000];
const at = (scene: number, elapsed = 0, playing = true, speed = 1): PlayerState => ({ scene, elapsed, playing, speed });

describe('the clock of a presentation', () => {
  it('runs at the speed chosen and goes from scene to scene while it plays', () => {
    expect(advance(at(0), 400, DURATIONS)).toEqual(at(0, 400));
    expect(advance(at(0, 0, true, 2), 400, DURATIONS)).toEqual(at(0, 800, true, 2));
    expect(advance(at(0, 900), 300, DURATIONS)).toEqual(at(1, 200));
    expect(advance(at(1, 1900), 1700, DURATIONS)).toEqual(at(3, 100));
  });

  it('ends now a scene made shorter than the time already played in it, and carries only the time of the frame into the next one', () => {
    // The second scene was 14 s long and is now 2 s: 12 s played in it must not run over the scenes after it.
    expect(advance(at(1, 12_000), 100, DURATIONS)).toEqual(at(2, 100));
    expect(advance(at(1, 12_000, true, 2), 100, DURATIONS)).toEqual(at(2, 200, true, 2));
    // A long frame still crosses the short scenes it covers.
    expect(advance(at(1, 12_000), 1700, DURATIONS)).toEqual(at(3, 200));
  });

  it('stands still in pause, and stops at the end of the closing', () => {
    expect(advance(at(1, 500, false), 1000, DURATIONS)).toEqual(at(1, 500, false));
    expect(advance(at(4, 900), 500, DURATIONS)).toEqual(at(4, 1000, false));
  });

  it('starts paused on the opening', () => {
    expect(PLAYER_START).toEqual({ scene: 0, elapsed: 0, playing: false, speed: 1 });
  });
});

describe('the moves of the presenter', () => {
  it('next: from a step to the flight that leaves it, from a flight to the step it reaches, the opening to the first step', () => {
    expect(playerTo(at(1, 700), 'next', SCENES)).toEqual(at(2));
    expect(playerTo(at(2, 700), 'next', SCENES)).toEqual(at(3));
    expect(playerTo(at(0, 300), 'next', SCENES)).toEqual(at(1));
    expect(playerTo(at(4, 300), 'next', SCENES)).toEqual(at(4, 300));
  });

  it('previous: back to the start of the step shown once it has run a while, else to the step before', () => {
    expect(playerTo(at(3, 2500), 'previous', SCENES)).toEqual(at(3));
    expect(playerTo(at(3, 500), 'previous', SCENES)).toEqual(at(1));
    expect(playerTo(at(2, 500), 'previous', SCENES)).toEqual(at(1));
    expect(playerTo(at(4, 100), 'previous', SCENES)).toEqual(at(3));
    expect(playerTo(at(1, 100), 'previous', SCENES)).toEqual(at(0));
  });

  it('start and end: the opening and the closing, the play kept', () => {
    expect(playerTo(at(3, 500, false), 'start', SCENES)).toEqual(at(0, 0, false));
    expect(playerTo(at(1, 500), 'end', SCENES)).toEqual(at(4));
  });

  it('next and previous by the sentences of the scene shown, then by the scenes, as a remote reveals a slide (audit 3.2, UX-03)', () => {
    const lines = [[], ['Une phrase.', 'Une autre.', 'La dernière.'], [], [], []];
    const second = lineMs('Une phrase.');
    const third = second + lineMs('Une autre.');
    expect(playerTo(at(1, 100, false), 'next', SCENES, lines)).toEqual(at(1, second, false));
    expect(playerTo(at(1, second + 10, false), 'next', SCENES, lines)).toEqual(at(1, third, false));
    expect(playerTo(at(1, third + 10, false), 'next', SCENES, lines)).toEqual(at(2, 0, false));
    expect(playerTo(at(1, third + 10, false), 'previous', SCENES, lines)).toEqual(at(1, second, false));
    expect(playerTo(at(1, second, false), 'previous', SCENES, lines)).toEqual(at(1, 0, false));
  });

  it('next step and previous step: a whole step at once, the flight between them left out, the closing after the last (the arrows of the keyboard)', () => {
    expect(playerTo(at(0, 300), 'nextStep', SCENES)).toEqual(at(1));
    expect(playerTo(at(1, 300), 'nextStep', SCENES)).toEqual(at(3));
    expect(playerTo(at(2, 300), 'nextStep', SCENES)).toEqual(at(3));
    expect(playerTo(at(3, 300), 'nextStep', SCENES)).toEqual(at(4));
    expect(playerTo(at(4, 300), 'nextStep', SCENES)).toEqual(at(4, 300));
    expect(playerTo(at(3, 500, false), 'previousStep', SCENES)).toEqual(at(1, 0, false));
    expect(playerTo(at(3, 2500), 'previousStep', SCENES)).toEqual(at(3));
  });

  it('a stop chosen: its step, at its start', () => {
    expect(playerTo(at(0, 500), { stop: 1 }, SCENES)).toEqual(at(3));
    expect(playerTo(at(2, 500), { stop: 7 }, SCENES)).toEqual(at(2, 500));
  });
});

describe('a tour that got shorter under the player (the map changed in server mode)', () => {
  it('comes back to the last scene instead of staying past the end', () => {
    expect(advance(at(7, 100), 50, DURATIONS)).toEqual(at(4, 150));
    expect(advance(at(7, 5000), 50, DURATIONS)).toEqual(at(4, 1000, false));
  });
});
