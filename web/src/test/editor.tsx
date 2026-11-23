// editor.tsx
// The editor of a map shown in a test: a map answered at once, preferences held in memory, the online voices allowed unless said otherwise, under the keyboard manager of the application.

import { act, fireEvent, render, within } from '@testing-library/react';
import type { Schema } from '../api/client.ts';
import { ApiError } from '../api/errors.ts';
import { MapScreen, type MapScreenProps } from '../features/map/MapScreen.tsx';
import { createMapEditor } from '../features/map/state/editor.ts';
import type { CommentSource, FlowComment } from '../features/map/presenter/comments.ts';
import type { OnlineVoiceSource } from '../features/map/presenter/onlineVoices.ts';
import type { ProjectVoice, VoiceSource } from '../features/map/presenter/projectVoices.ts';
import type { MapSource } from '../features/map/state/mapStore.ts';
import type { PreferenceStorage } from '../features/map/state/preferences.ts';
import { KeyboardProvider } from '../shared/keyboard/KeyboardContext.tsx';
import { MAP_ID } from './maps.ts';

/** A storage of the preferences held in memory, with the values given, and what the editor wrote in it. */
export function memoryStorage(values: Record<string, string> = {}): PreferenceStorage & { written: Map<string, string> } {
  const written = new Map<string, string>();
  return {
    written,
    getItem: (key) => values[key] ?? null,
    setItem: (key, value) => {
      written.set(key, value);
    },
  };
}

/** The screen of a map under the keyboard manager, as the application shows it. */
export function renderMapScreen(props: MapScreenProps) {
  return render(<KeyboardProvider><MapScreen {...props} /></KeyboardProvider>);
}

/** A source that gives the maps given, one a reading, the last one repeated. */
export function sourceOf(...maps: Schema<'MapView'>[]): MapSource {
  let reads = 0;
  return () => Promise.resolve({ map: maps[Math.min(reads++, maps.length - 1)] as Schema<'MapView'>, history: { undo: [], redo: [] } });
}

/** Shows a map in the editor and waits for its canvas; the next readings give the maps given after it. */
export async function showMap(map: Schema<'MapView'>, storage: PreferenceStorage | null = memoryStorage(), ...next: Schema<'MapView'>[]): Promise<HTMLElement> {
  const { container } = renderMapScreen({ mapId: MAP_ID, source: sourceOf(map, ...next), storage });
  return within(container).findByTestId('map-canvas');
}

/** A write of a comment of presentation the editor sent (ADR 099). */
export interface CommentWrite {
  readonly stepId: string;
  readonly text: string;
  readonly language: string;
  readonly version: number;
}

/** The refusal of the host for a line changed since it was read (`CONFLIT_VERSION`, 409), in the words of a fake host. */
const COMMENT_CONFLICT = 'Ce commentaire a été modifié entre-temps.';

const VOICE_CONFLICT = 'Cette voix a été modifiée entre-temps.';

/**
 * Comments of presentation held in memory (ADR 099 revised): the lines given, read for any map; each write kept with the
 * next version of its line as a text typed, an empty text removing the line; the lines of the step given back. The
 * original is written in <paramref name="origin"/>: emptied, it takes the translations of its step with it. A failure
 * given refuses the next writes; a write whose version is not the one of its line (0 without one) is refused as the
 * host refuses it, by a conflict.
 */
export function memoryComments(given: readonly Schema<'PresentationCommentView'>[] = [], origin = 'fr') {
  const keyOf = (stepId: string, language: string) => `${stepId}|${language}`;
  const kept = new Map(given.map((comment) => [keyOf(comment.step_id, comment.language), comment]));
  const linesOf = (stepId: string) => [...kept.values()].filter((comment) => comment.step_id === stepId);
  const reads: string[] = [];
  const writes: CommentWrite[] = [];
  const state: { failure: Error | null } = { failure: null };
  // Those of the flows (lot J10): kept by flow and language, read and written by their own routes, by the same rules.
  const flowKept = new Map<string, FlowComment>();
  const flowLinesOf = (flowId: string) => [...flowKept.values()].filter((comment) => comment.flow_id === flowId);
  const flowWrites: { flowId: string; text: string; language: string; version: number }[] = [];
  const source: CommentSource = {
    readFlows: () => Promise.resolve([...flowKept.values()]),
    writeFlow: (flowId, text, language, version) => {
      flowWrites.push({ flowId, text, language, version });
      if (state.failure !== null) return Promise.reject(state.failure);
      if (version !== (flowKept.get(keyOf(flowId, language))?.version ?? 0)) return Promise.reject(new ApiError(409, 'CONFLIT_VERSION', COMMENT_CONFLICT));
      if (text === '') {
        for (const line of flowLinesOf(flowId)) if (language === origin || line.language === language) flowKept.delete(keyOf(flowId, line.language));
        return Promise.resolve(flowLinesOf(flowId));
      }
      flowKept.set(keyOf(flowId, language), { flow_id: flowId, text, language, source: 'manual', version: version + 1 });
      return Promise.resolve(flowLinesOf(flowId));
    },
    read: (mapId) => {
      reads.push(mapId);
      return Promise.resolve([...kept.values()]);
    },
    write: (stepId, text, language, version) => {
      writes.push({ stepId, text, language, version });
      if (state.failure !== null) return Promise.reject(state.failure);
      if (version !== (kept.get(keyOf(stepId, language))?.version ?? 0)) return Promise.reject(new ApiError(409, 'CONFLIT_VERSION', COMMENT_CONFLICT));
      if (text === '') {
        for (const line of linesOf(stepId)) if (language === origin || line.language === language) kept.delete(keyOf(stepId, line.language));
        return Promise.resolve(linesOf(stepId));
      }
      kept.set(keyOf(stepId, language), { step_id: stepId, text, language, source: 'manual', version: version + 1 });
      return Promise.resolve(linesOf(stepId));
    },
  };
  /** Puts a line of the comment of a flow in the memory, as the host would have it. */
  const giveFlow = (comment: FlowComment) => {
    flowKept.set(keyOf(comment.flow_id, comment.language), comment);
  };
  return { source, reads, writes, state, flowWrites, giveFlow };
}

/**
 * The voices of a project held in memory (lot J10): those given, read for any project; each write kept with the next
 * version of its line, refused by a conflict when its version is not the one of the line (0 without one), as the host
 * refuses it; the failure given refuses the next writes.
 */
export function memoryVoices(given: readonly ProjectVoice[] = []) {
  const kept = new Map(given.map((voice) => [voice.language, voice]));
  const reads: string[] = [];
  const writes: { projectId: string; language: string; name: string; local: boolean; version: number }[] = [];
  const removals: { projectId: string; language: string; version: number }[] = [];
  const state: { failure: Error | null } = { failure: null };
  const source: VoiceSource = {
    read: (projectId) => {
      reads.push(projectId);
      return Promise.resolve([...kept.values()]);
    },
    write: (projectId, language, voice, version) => {
      writes.push({ projectId, language, name: voice.name, local: voice.local, version });
      if (state.failure !== null) return Promise.reject(state.failure);
      if (version !== (kept.get(language)?.version ?? 0)) return Promise.reject(new ApiError(409, 'CONFLIT_VERSION', VOICE_CONFLICT));
      kept.set(language, { language, name: voice.name, local: voice.local, version: version + 1 });
      return Promise.resolve([...kept.values()]);
    },
    remove: (projectId, language, version) => {
      removals.push({ projectId, language, version });
      if (state.failure !== null) return Promise.reject(state.failure);
      if (version !== (kept.get(language)?.version ?? 0)) return Promise.reject(new ApiError(409, 'CONFLIT_VERSION', VOICE_CONFLICT));
      kept.delete(language);
      return Promise.resolve([...kept.values()]);
    },
  };
  /** Puts the voice of a language in the memory, as the host would have it after another facilitator chose it. */
  const give = (voice: ProjectVoice) => {
    kept.set(voice.language, voice);
  };
  return { source, reads, writes, removals, state, give };
}

/** A map shown in an editor the test holds: its state read and changed by the test, the notifications and screens opened kept. */
export async function showEditor(map: Schema<'MapView'>, ...next: Schema<'MapView'>[]) {
  return showEditorWith({}, map, ...next);
}

/**
 * What a test gives the editor instead of the host and the browser: the comments of presentation (ADR 099), the storage of
 * the preferences, whether the classification of the project lets the online voices be offered (F-137), yes by default,
 * and the voices the project keeps (lot J10), none by default.
 */
export interface EditorFakes {
  readonly comments?: CommentSource;
  readonly voices?: VoiceSource;
  readonly storage?: PreferenceStorage;
  readonly onlineVoices?: OnlineVoiceSource;
}

/** The same, with the fakes given. */
export async function showEditorWith(options: EditorFakes, map: Schema<'MapView'>, ...next: Schema<'MapView'>[]) {
  const notes: [string, string | undefined, string?][] = [];
  const opened: string[] = [];
  const editor = createMapEditor({ source: sourceOf(map, ...next), storage: options.storage ?? memoryStorage(), notify: (text, kind, _action, key) => notes.push(key === undefined ? [text, kind] : [text, kind, key]),
    navigate: (path) => opened.push(path), comments: options.comments ?? memoryComments().source, onlineVoices: options.onlineVoices ?? (() => Promise.resolve(true)),
    voices: options.voices ?? memoryVoices().source });
  const { container } = renderMapScreen({ mapId: MAP_ID, editor });
  const canvas = await within(container).findByTestId('map-canvas');
  return { editor, canvas, notes, opened };
}

/** Opens the sheet of a step as the user does (AR-151): a click chooses the step, then Enter on it opens its sheet. */
export function openSheetOf(canvas: HTMLElement, stepId: string): void {
  const step = canvas.querySelector<HTMLElement>(`.react-flow__node[data-id="${stepId}"]`);
  if (step === null) throw new Error(`No step ${stepId} on the canvas`);
  fireEvent.click(step);
  act(() => {
    step.focus();
  });
  fireEvent.keyDown(step, { key: 'Enter' });
}
