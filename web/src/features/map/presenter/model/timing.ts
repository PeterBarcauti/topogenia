// timing.ts
// The times of a presentation (ADR 097): a sentence shown the time to read it, as subtitles are timed; a scene its sentences one after the other; a flight by the length of its flow.

import type { Scene } from './script.ts';
import { widthOf } from './subtitles.ts';

/** Characters read in a second: under the 17 of adult subtitles, a room reads a screen from afar while it listens. */
const CHARACTERS_A_SECOND = 15;

/** The time the eye takes to find a new sentence, added to its reading. */
const LEAD_IN_MS = 300;

/** A sentence stays at least this long, so that a short one is not a flash, and at most this long. */
const LINE_FLOOR_MS = 1600;
const LINE_CEILING_MS = 7000;

/** A scene without a sentence (a step without anything known, or the text off with nothing to say) stays long enough to be seen. */
export const STEP_BARE_MS = 2200;

/** A flight: its start and the time a pixel of the flow takes, between its floor and its ceiling. */
const FLIGHT_BASE_MS = 800;
const FLIGHT_MS_A_PIXEL = 1.1;
const FLIGHT_FLOOR_MS = 1200;
const FLIGHT_CEILING_MS = 3000;

const within = (value: number, floor: number, ceiling: number): number => Math.min(ceiling, Math.max(floor, value));

/** The time a sentence is shown, at the speed 1: by its columns, so that a wide character (Chinese, Japanese, Korean) counts for two. */
export const lineMs = (line: string): number => Math.round(within(LEAD_IN_MS + (widthOf(line) * 1000) / CHARACTERS_A_SECOND, LINE_FLOOR_MS, LINE_CEILING_MS));

/** The time of the flight of a light along a flow of this length, in units of the canvas. */
export const flightMs = (length: number): number => Math.round(within(FLIGHT_BASE_MS + length * FLIGHT_MS_A_PIXEL, FLIGHT_FLOOR_MS, FLIGHT_CEILING_MS));

/** The time of a scene: its sentences one after the other; a passage, its flight at least. */
export function sceneMs(scene: Scene, lines: readonly string[], flowLength = 0): number {
  const reading = lines.reduce((total, line) => total + lineMs(line), 0);
  if (scene.kind === 'passage') return Math.max(flightMs(flowLength), reading);
  return lines.length === 0 ? STEP_BARE_MS : reading;
}

/** The time the sentence shown at a time of its scene ends, the last one at the end of its scene; none without sentences. */
export function lineEndAt(lines: readonly string[], elapsed: number): number {
  let end = 0;
  for (const line of lines) {
    end += lineMs(line);
    if (elapsed < end) return end;
  }
  return lines.length === 0 ? Number.POSITIVE_INFINITY : end;
}

/** The sentence shown at a time of its scene: the one whose time it is, the last one until the scene ends; -1 without sentences. */
export function lineAt(lines: readonly string[], elapsed: number): number {
  let end = 0;
  for (const [index, line] of lines.entries()) {
    end += lineMs(line);
    if (elapsed < end) return index;
  }
  return lines.length - 1;
}
