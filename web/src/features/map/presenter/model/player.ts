// player.ts
// The reading of a presentation (ADR 097), without React: the scene shown, its time, play or pause, the speed; the clock that runs, and the moves of the presenter.

import type { Scene } from './script.ts';
import { lineMs } from './timing.ts';

export interface PlayerState {
  readonly scene: number;
  /** The time spent in the scene, at the speed 1. */
  readonly elapsed: number;
  readonly playing: boolean;
  readonly speed: number;
}

/**
 * The speeds offered, the normal one in the middle, in fine steps around it as the players of podcasts do: a voice of the
 * system is hard to follow at 0.5 and past 1.25 (essai of the project owner, 19/11/2026).
 */
export const SPEEDS: readonly number[] = [0.75, 0.9, 1, 1.1, 1.25];

/** A presentation opens paused on the whole map: the presenter starts it when the room is ready. */
export const PLAYER_START: PlayerState = { scene: 0, elapsed: 0, playing: false, speed: 1 };

/** A step shown longer than this goes back to its own start on « previous », as a player does with a track. */
const RESTART_AFTER_MS = 2000;

/**
 * The clock after a frame of `ms`: at the speed chosen, scene after scene while it plays; it stops at the end of the last.
 * A hold keeps it in its scene up to that time: the voice has not finished the sentence shown. A scene made shorter
 * than the time already played in it (its messages cut, its comment removed) ends now, and only the time of this
 * frame goes on into the next one: the time of the longer text never carries the story over the scenes after it
 * (independent review of the lot J10).
 */
export function advance(state: PlayerState, ms: number, durations: readonly number[], hold = Number.POSITIVE_INFINITY): PlayerState {
  if (!state.playing || durations.length === 0) return state;
  // A tour made shorter under the player (the map changed) brings it back to its last scene.
  let scene = Math.min(state.scene, durations.length - 1);
  const played = ms * state.speed;
  let elapsed = state.elapsed + played;
  if (elapsed > hold) return { ...state, elapsed: Math.max(state.elapsed, hold) };
  while (elapsed >= (durations[scene] ?? 0) && scene < durations.length - 1) {
    elapsed = Math.min(elapsed - (durations[scene] ?? 0), played);
    scene += 1;
  }
  const last = durations[durations.length - 1] ?? 0;
  if (scene === durations.length - 1 && elapsed >= last) return { ...state, scene, elapsed: last, playing: false };
  return { ...state, scene, elapsed };
}

/**
 * A move of the presenter: the next sentence of the scene shown, then the next scene (a remote reveals a slide so, audit 3.2,
 * UX-03), the sentence before, then the step before; a whole step at once, forward or back (the arrows of the keyboard); the
 * opening, the closing, a stop of the tour, or the passage that leaves a stop (a flow clicked, lot J10).
 */
export type Move = 'next' | 'previous' | 'nextStep' | 'previousStep' | 'start' | 'end' | { readonly stop: number } | { readonly passage: number };

/** The scene of the next step after the scene given, the flight between them left out; the closing after the last step. */
function nextStepOf(state: PlayerState, scenes: readonly Scene[]): number {
  const last = scenes.length - 1;
  for (let index = state.scene + 1; index < last; index++) if (scenes[index]?.kind === 'step') return index;
  return Math.max(state.scene, last);
}

/** The times the sentences of a scene start at, the first one at 0. */
function startsOf(lines: readonly string[]): number[] {
  const starts: number[] = [];
  let at = 0;
  for (const line of lines) {
    starts.push(at);
    at += lineMs(line);
  }
  return starts;
}

const stepScene = (scenes: readonly Scene[], stop: number): number => scenes.findIndex((scene) => scene.kind === 'step' && scene.stop === stop);

/** The scene of the step shown before the scene given: the step itself for a passage, the step before for a step. */
function previousOf(state: PlayerState, scenes: readonly Scene[]): number {
  const scene = scenes[state.scene];
  if (scene === undefined || scene.kind === 'opening') return 0;
  if (scene.kind === 'step' && state.elapsed > RESTART_AFTER_MS) return state.scene;
  for (let index = state.scene - 1; index > 0; index--) if (scenes[index]?.kind === 'step') return index;
  return 0;
}

/**
 * Where a move leads, at the start of its sentence or its scene, playing or not as before; a stop that is not in the tour leaves
 * things as they are. `lines`, the sentences of each scene: without them, next and previous go by the scenes.
 */
export function playerTo(state: PlayerState, move: Move, scenes: readonly Scene[], lines: readonly (readonly string[])[] = []): PlayerState {
  const to = (scene: number, elapsed = 0): PlayerState => ({ ...state, scene, elapsed });
  if (move === 'next' || move === 'previous') {
    const starts = startsOf(lines[state.scene] ?? []);
    const shown = starts.filter((start) => start <= state.elapsed).length - 1;
    const sentence = starts[move === 'next' ? shown + 1 : shown - 1];
    if (shown >= 0 && sentence !== undefined) return to(state.scene, sentence);
  }
  if (move === 'start') return to(0);
  if (move === 'end') return to(Math.max(0, scenes.length - 1));
  if (move === 'previous' || move === 'previousStep') return to(previousOf(state, scenes));
  if (move === 'nextStep') return state.scene >= scenes.length - 1 ? state : to(nextStepOf(state, scenes));
  if (move === 'next') return state.scene >= scenes.length - 1 ? state : to(state.scene + 1);
  const index = 'passage' in move ? scenes.findIndex((scene) => scene.kind === 'passage' && scene.stop === move.passage) : stepScene(scenes, move.stop);
  return index < 0 ? state : to(index);
}
