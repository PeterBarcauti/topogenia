// projectVoices.ts
// The voice a project keeps for each language (lot J10): read when a presentation starts and taken before the one the browser kept, when this computer offers it to this person; the voice chosen in the settings of the voice reads for the rest of the session, and the project keeps it when a facilitator chose it, over the choice another made meanwhile.

import { useEffect } from 'react';
import { api, type Schema } from '../../../api/client.ts';
import { ApiError, isApiError, NETWORK_ERROR } from '../../../api/errors.ts';
import { messageOf } from '../../../shared/i18n/errorMessage.ts';
import type { MapEditor } from '../state/editor.ts';
import { COMMENT_SEND_MS } from './comments.ts';
import type { VoiceMark } from './model/voice.ts';

/** The voice a project keeps for a language: its name, whether it is one of the computer, the version of its line. */
export type ProjectVoice = Schema<'PresentationVoiceView'>;

/** Where the voices of a project are read and written: the host, a fake in the tests. */
export interface VoiceSource {
  /** The voice the project keeps for each language that has one. */
  readonly read: (projectId: string, signal?: AbortSignal) => Promise<readonly ProjectVoice[]>;
  /** The voice of a language written, with the version read of its line (0 without one); gives every voice of the project afterwards. */
  readonly write: (projectId: string, language: string, voice: VoiceMark, version: number, signal?: AbortSignal) => Promise<readonly ProjectVoice[]>;
  /** The voice of a language removed from the project, with the version read of its line; gives every voice of the project afterwards. */
  readonly remove: (projectId: string, language: string, version: number, signal?: AbortSignal) => Promise<readonly ProjectVoice[]>;
}

export const hostVoices: VoiceSource = {
  read: (projectId, signal) => api.get('/api/projects/{id}/presentation-voices', { path: { id: projectId }, signal }),
  write: async (projectId, language, voice, version, signal) =>
    (await api.put('/api/projects/{id}/presentation-voices/{language}', { path: { id: projectId, language }, version, body: { name: voice.name, local: voice.local }, signal })).voices,
  // An empty name removes the voice kept (ADR 103): where it was read is not asked then.
  remove: async (projectId, language, version, signal) =>
    (await api.put('/api/projects/{id}/presentation-voices/{language}', { path: { id: projectId, language }, version, body: { name: null, local: null }, signal })).voices,
};

/**
 * Reads the voices the project keeps when its presentation starts. Unreadable, nothing is said: the voice the browser
 * kept reads, as before the project kept one. An answer that comes after other voices were kept (a choice written
 * meanwhile) is older than them, and is left (independent review of the lot J10).
 */
export function useProjectVoices(editor: MapEditor, projectId: string): void {
  useEffect(() => {
    const control = new AbortController();
    const asked = editor.presenter.getState().voiceKeeps;
    editor.voices.read(projectId, control.signal).then((voices) => {
      if (!control.signal.aborted && editor.presenter.getState().voiceKeeps === asked) editor.presenter.getState().keepVoices(voices);
    }, () => undefined);
    return () => {
      control.abort();
    };
  }, [editor, projectId]);
}

/** The project of a presentation as its voice sees it: its identifier, and whether the person may write in it (a facilitator). */
export interface VoiceProject {
  readonly id: string;
  readonly keeps: boolean;
}

/** The writes of the voices of a project go one after the other, each editor its own line: the last choice is the last written. */
const queues = new WeakMap<MapEditor, Promise<void>>();

/**
 * Writes the voice picked for a language over the one the project keeps, with the version read of its line; nothing when
 * the project already keeps that very voice. The line changed meanwhile by another facilitator, the voices are read
 * again and the write sent again over it: the last who chooses is kept. Any other refusal is said in the stage, where
 * the full screen shows it; the voice picked reads all the same. A host that answers nothing within the time limit of
 * a send is given up and said too, so that the next choice is not held behind it. `forget`: the voice the project
 * keeps for the language is removed instead, by the same rules; nothing when it keeps none.
 */
async function keepNow(editor: MapEditor, projectId: string, language: string, forget: boolean): Promise<void> {
  const state = () => editor.presenter.getState();
  const control = new AbortController();
  const limit = setTimeout(() => {
    control.abort();
  }, COMMENT_SEND_MS);
  const said = (failure: unknown) => {
    state().say(messageOf(control.signal.aborted ? new ApiError(0, NETWORK_ERROR, '') : failure));
  };
  const write = async () => {
    const picked = state().picked[language];
    const kept = state().projectVoices.find((voice) => voice.language === language);
    if (forget) {
      if (kept !== undefined) state().keepVoices(await editor.voices.remove(projectId, language, kept.version, control.signal));
      return;
    }
    if (picked === undefined || (kept?.name === picked.name && kept.local === picked.local)) return;
    state().keepVoices(await editor.voices.write(projectId, language, picked, kept?.version ?? 0, control.signal));
  };
  try {
    await write();
  } catch (failure) {
    if (control.signal.aborted || !isApiError(failure) || failure.status !== 409) {
      said(failure);
      return;
    }
    try {
      state().keepVoices(await editor.voices.read(projectId, control.signal));
      await write();
    } catch (again) {
      said(again);
    }
  } finally {
    clearTimeout(limit);
  }
}

/**
 * The voice chosen in the settings for the language shown: it reads at once and until the map is left, before the one
 * of the project; the browser keeps its name (UX-MOY-01); and the project keeps it for that language when a
 * facilitator chose it, for whoever presents it next, on this computer or another that has that voice.
 */
export function chooseVoice(editor: MapEditor, project: VoiceProject, language: string, voice: VoiceMark): Promise<void> {
  const presenter = editor.presenter.getState();
  presenter.say(null);
  editor.preferences.getState().setVoiceName(voice.name);
  presenter.pick(language, voice);
  if (!project.keeps) return Promise.resolve();
  const turn = (queues.get(editor) ?? Promise.resolve()).then(() => keepNow(editor, project.id, language, false));
  queues.set(editor, turn);
  return turn;
}

/**
 * The voice of the project for the language shown, removed by a facilitator (the box of the settings unticked): the
 * project keeps none for that language any more, and whoever presents it next reads with the voice of their browser.
 * The voice shown (`shown`) goes on reading for the session, as one picked: unticking takes it from the project, not
 * from the person (third independent review of the lot J10).
 */
export function forgetVoice(editor: MapEditor, project: VoiceProject, language: string, shown: VoiceMark): Promise<void> {
  const presenter = editor.presenter.getState();
  presenter.say(null);
  presenter.pick(language, shown);
  if (!project.keeps) return Promise.resolve();
  const turn = (queues.get(editor) ?? Promise.resolve()).then(() => keepNow(editor, project.id, language, true));
  queues.set(editor, turn);
  return turn;
}
