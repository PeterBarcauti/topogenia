// assistantStore.ts
// The thread of the panel of the assistant: its state for the map, the turns asked and answered, the files joined, the exchange read as a stream, the prompt copied and the reply pasted; kept by the page only, never in the browser nor the host (spec 3.0 sections 3.1-8, 3.4, 5; lot 4).

import { createStore, type StoreApi } from 'zustand/vanilla';
import { api, type Schema } from '../../../api/client.ts';
import { isApiError } from '../../../api/errors.ts';
import { sendStream } from '../../../api/request.ts';
import { messageOf } from '../../../shared/i18n/errorMessage.ts';
import type { FileBounds, FileLabel, JoinedFile } from './attachments.ts';

export type Intent = 'free' | 'create' | 'modify' | 'analyze' | 'ask' | 'translate';

/**
 * A translation of the whole process by the assistant (ADR 083): the language asked, and the root map it is asked on,
 * whose preview and application take only the translations into that language.
 */
export interface Translation {
  readonly language: string;
  readonly mapId: string;
}
export type AssistantStatus = Schema<'AssistantStatusView'>;
export type AssistantReply = Schema<'AssistantReplyView'>;
export type AssistantDestination = Schema<'AssistantDestinationView'>;
type AssistantEvent = Schema<'AssistantEvent'>;
type AssistantCost = Schema<'AssistantCostView'>;

/** A request of the person, as it was sent. */
export interface RequestTurn {
  readonly kind: 'request';
  readonly id: number;
  readonly intent: Intent;
  readonly text: string;
  readonly selection: readonly string[];
  readonly files: readonly FileLabel[];
  readonly translation?: Translation | undefined;
}

/** The model at work: its phase, the last line of its thinking, the characters written, the time since the request. */
export interface ProgressTurn {
  readonly kind: 'progress';
  readonly id: number;
  readonly phase: string;
  readonly thought: string | null;
  readonly characters: number;
  readonly elapsed: number;
  readonly destination: AssistantDestination | null;
}

export interface ReplyTurn {
  readonly kind: 'reply';
  readonly id: number;
  readonly reply: AssistantReply;
  readonly cost: AssistantCost | null;
  readonly elapsed: number;
  /** The reply came from the AI of the person, pasted (the copy and paste). */
  readonly pasted: boolean;
  /** The translation asked, whose proposal takes only the translations into its language (ADR 083). */
  readonly translation?: Translation | undefined;
}

/** An exchange that failed: the code of the failure of the provider or of the reply, or the message of a refusal of the host. */
export interface FailureTurn {
  readonly kind: 'failure';
  readonly id: number;
  readonly failure: string;
  readonly detail: string | null;
  readonly message: string | null;
  readonly retry: { readonly intent: Intent; readonly text: string; readonly files: readonly JoinedFile[]; readonly translation?: Translation | undefined } | null;
}

/** The prompt copied for the AI of the person (the copy and paste): the panel then waits for its reply, pasted. */
export interface CopiedTurn {
  readonly kind: 'copied';
  readonly id: number;
  readonly intent: Intent;
  readonly text: string;
  readonly copied: boolean;
  readonly prompt: string;
  readonly files: readonly JoinedFile[];
  readonly translation?: Translation | undefined;
}

export type Turn = RequestTurn | ProgressTurn | ReplyTurn | FailureTurn | CopiedTurn;

/** A former turn given back to the model: the request, the JSON it replied, the files joined to the request (sent again at their place). */
interface HistoryTurn {
  readonly request: string;
  readonly reply: string;
  readonly files: readonly JoinedFile[];
}

/** What the panel acts on: the map open (none for a project yet to create, from the home page) and the codes selected on it. */
export interface AssistantTarget {
  readonly mapId: () => string | null;
  readonly selection: () => readonly string[];
}

/** The longest request, in characters, as the host bounds it. */
export const REQUEST_MAX = 100_000;

/** The most former turns given back, as the host bounds them; the oldest are left out beyond. */
const TURNS_MAX = 40;

export interface AssistantState {
  /** The panel is shown (the map); the thread lives on while it is closed, with the editor of the map. */
  readonly open: boolean;
  readonly status: AssistantStatus | null;
  readonly statusError: unknown;
  readonly turns: readonly Turn[];
  readonly history: readonly HistoryTurn[];
  readonly running: boolean;
  readonly draft: string;
  /** The practice of the tile chosen, until the request is sent or the chip removed. */
  readonly intent: Intent | null;
  /** The pill of the selection was removed: the codes selected are not sent. */
  readonly selectionDropped: boolean;
  /** The files joined to the next request, kept by the page until it is sent. */
  readonly files: readonly JoinedFile[];
  /** The bounds of the files of the connector, from the state of the panel or the settings on the home page; null before them. */
  readonly bounds: FileBounds | null;
  readonly setBounds: (bounds: FileBounds | null) => void;
  readonly addFiles: (files: readonly Omit<JoinedFile, 'id'>[]) => void;
  readonly removeFile: (id: number) => void;
  /** A model called by the host, or the prompt copied for the AI of the person; from the state of the panel, or the settings on the home page. */
  readonly mode: 'model' | 'copy_paste';
  readonly setMode: (mode: 'model' | 'copy_paste') => void;
  readonly setOpen: (open: boolean) => void;
  readonly loadStatus: () => Promise<void>;
  readonly setDraft: (draft: string) => void;
  readonly choose: (intent: Intent, example: string) => void;
  readonly clearIntent: () => void;
  readonly dropSelection: (dropped: boolean) => void;
  /** Sends the request of the field with its files (or the text given, with the files given), to the model or, without a model, copies its prompt. */
  readonly send: (text?: string, intent?: Intent, files?: readonly JoinedFile[], translation?: Translation) => Promise<void>;
  readonly stop: () => void;
  /** A new thread: the turns and the history forgotten, nothing kept anywhere. */
  readonly restart: () => void;
  /** Reads the reply pasted from the AI of the person, for the prompt copied of a turn. */
  readonly paste: (turnId: number, reply: string) => Promise<void>;
}

/** What the steps of an exchange share: the state, the turns written, the body of a request, the numbers of the turns, the call that runs. */
interface Kit {
  readonly get: () => AssistantState;
  readonly set: (partial: Partial<AssistantState>) => void;
  readonly target: AssistantTarget;
  readonly next: () => number;
  readonly calls: { running: AbortController | null };
}

const push = (kit: Kit, turn: Turn) => {
  kit.set({ turns: [...kit.get().turns, turn] });
};

const replace = (kit: Kit, id: number, turn: Turn) => {
  kit.set({ turns: kit.get().turns.map((candidate) => (candidate.id === id ? turn : candidate)) });
};

const wire = (file: JoinedFile) => ({ name: file.name, data: file.data });

/** The body of a request: the practice, the text, the codes selected unless the pill was removed, the former turns with their files, the files of the request. */
const bodyOf = (kit: Kit, intent: Intent, text: string, files: readonly JoinedFile[], translation?: Translation) => ({
  intent, request: text, selection: kit.get().selectionDropped || translation !== undefined ? [] : [...kit.target.selection()],
  history: kit.get().history.slice(-TURNS_MAX).map((turn) => ({ request: turn.request, reply: turn.reply, files: turn.files.map(wire) })),
  files: files.map(wire),
  ...(translation === undefined ? {} : { language: translation.language }),
});

const remember = (kit: Kit, request: string, reply: AssistantReply, files: readonly JoinedFile[]) => {
  kit.set({ history: [...kit.get().history, { request, reply: reply.raw, files }].slice(-TURNS_MAX) });
};

/** Every file of the conversation: those of the former turns, then those joined to the next request; the bounds count them together. */
export const threadFiles = (state: Pick<AssistantState, 'history' | 'files'>): readonly JoinedFile[] => [...state.history.flatMap((turn) => turn.files), ...state.files];

/** The progress turn moved on by a line of the stream. */
function progressed(progress: ProgressTurn, event: AssistantEvent): ProgressTurn {
  if (event.type === 'started') return { ...progress, destination: event.destination ?? null };
  return {
    ...progress, phase: event.phase ?? progress.phase, thought: event.thought ?? progress.thought, characters: event.characters ?? progress.characters,
    elapsed: event.elapsed_milliseconds ?? progress.elapsed,
  };
}

/** Sends a request to the model and reads its reply as a stream: the progress turn becomes the reply, or the failure. */
async function exchange(kit: Kit, intent: Intent, text: string, files: readonly JoinedFile[], translation?: Translation): Promise<void> {
  const mapId = translation?.mapId ?? kit.target.mapId();
  const controller = new AbortController();
  kit.calls.running = controller;
  const id = kit.next();
  let progress: ProgressTurn = { kind: 'progress', id, phase: 'waiting', thought: null, characters: 0, elapsed: 0, destination: null };
  const end = { reached: false };
  push(kit, progress);
  kit.set({ running: true });
  const failed = (failure: string, message: string | null, detail: string | null = null) => {
    end.reached = true;
    replace(kit, id, { kind: 'failure', id, failure, detail, message, retry: { intent, text, files, translation } });
  };
  const onLine = (line: unknown) => {
    const event = line as AssistantEvent;
    if (event.type === 'reply' && event.reply) {
      end.reached = true;
      replace(kit, id, { kind: 'reply', id, reply: event.reply, cost: event.cost ?? null, elapsed: event.elapsed_milliseconds ?? progress.elapsed, pasted: false, translation });
      remember(kit, text, event.reply, files);
    } else if (event.type === 'failure') failed(event.failure ?? 'other', null, event.detail ?? null);
    else {
      progress = progressed(progress, event);
      replace(kit, id, progress);
    }
  };
  try {
    const route = mapId === null ? '/api/assistant/exchanges' : `/api/maps/${mapId}/assistant/exchanges`;
    await sendStream(route, { body: bodyOf(kit, intent, text, files, translation), signal: controller.signal }, onLine);
    // A stream that ends without its reply or its failure was cut on the way.
    if (!end.reached) failed('other', null);
  } catch (failure) {
    if (controller.signal.aborted) failed('cancelled', null);
    else failed(isApiError(failure) ? failure.code : 'other', messageOf(failure));
  } finally {
    if (kit.calls.running === controller) kit.calls.running = null;
    kit.set({ running: false });
  }
}

/** Without a model, the whole prompt of the request copied for the AI of the person, then its reply awaited. */
async function copyPrompt(kit: Kit, intent: Intent, text: string, files: readonly JoinedFile[], translation?: Translation): Promise<void> {
  const mapId = translation?.mapId ?? kit.target.mapId();
  const id = kit.next();
  kit.set({ running: true });
  try {
    const body = bodyOf(kit, intent, text, files, translation);
    const reply = mapId === null
      ? await api.post('/api/assistant/prompt', { body })
      : await api.post('/api/maps/{id}/assistant/prompt', { path: { id: mapId }, body });
    let copied = false;
    try {
      await navigator.clipboard.writeText(reply.prompt);
      copied = true;
    } catch {
      // The page may not write the clipboard (a browser that refuses): the prompt is copied by its button.
    }
    push(kit, { kind: 'copied', id, intent, text, copied, prompt: reply.prompt, files, translation });
  } catch (failure) {
    push(kit, { kind: 'failure', id, failure: isApiError(failure) ? failure.code : 'other', detail: null, message: messageOf(failure), retry: { intent, text, files, translation } });
  } finally {
    kit.set({ running: false });
  }
}

/** The reply of the AI of the person, pasted for a prompt copied, read by the host as the reply of a model. */
async function readPasted(kit: Kit, turnId: number, pasted: string): Promise<void> {
  const turn = kit.get().turns.find((candidate): candidate is CopiedTurn => candidate.id === turnId && candidate.kind === 'copied');
  if (turn === undefined || pasted.trim() === '') return;
  const mapId = turn.translation?.mapId ?? kit.target.mapId();
  kit.set({ running: true });
  // The files stay in the AI of the person: the host reads the reply pasted, not them again.
  const asked = bodyOf(kit, turn.intent, turn.text, [], turn.translation);
  const body = {
    intent: asked.intent, request: asked.request, selection: asked.selection, history: asked.history.map(({ request, reply }) => ({ request, reply })), reply: pasted,
    ...(turn.translation === undefined ? {} : { language: turn.translation.language }),
  };
  try {
    const read = mapId === null
      ? await api.post('/api/assistant/replies', { body })
      : await api.post('/api/maps/{id}/assistant/replies', { path: { id: mapId }, body });
    const id = kit.next();
    if (read.reply) {
      push(kit, { kind: 'reply', id, reply: read.reply, cost: null, elapsed: 0, pasted: true, translation: turn.translation });
      remember(kit, turn.text, read.reply, turn.files);
    } else push(kit, { kind: 'failure', id, failure: read.failure ?? 'invalid_reply', detail: read.detail ?? null, message: null, retry: null });
  } catch (failure) {
    push(kit, { kind: 'failure', id: kit.next(), failure: isApiError(failure) ? failure.code : 'other', detail: null, message: messageOf(failure), retry: null });
  } finally {
    kit.set({ running: false });
  }
}

/** The state of the panel for one map (made by its editor) or for a project yet to create (made by the home page). */
export function createAssistantStore(target: AssistantTarget): StoreApi<AssistantState> {
  let number = 1;
  const calls: Kit['calls'] = { running: null };
  return createStore<AssistantState>()((setState, getState) => {
    const kit: Kit = { get: getState, set: setState, target, next: () => number++, calls };
    return {
      open: false, status: null, statusError: null, turns: [], history: [], running: false, draft: '', intent: null, selectionDropped: false, mode: 'model',
      files: [], bounds: null,
      setMode: (mode) => { setState({ mode }); },
      setBounds: (bounds) => { setState({ bounds }); },
      addFiles: (added) => { setState({ files: [...getState().files, ...added.map((file) => ({ ...file, id: number++ }))] }); },
      removeFile: (id) => { setState({ files: getState().files.filter((file) => file.id !== id) }); },
      setOpen: (open) => { setState({ open }); },
      async loadStatus() {
        const mapId = target.mapId();
        if (mapId === null) return;
        try {
          const status = await api.get('/api/maps/{id}/assistant', { path: { id: mapId } });
          setState({ status, statusError: null, mode: status.mode === 'model' ? 'model' : 'copy_paste', bounds: status.files });
        } catch (failure) {
          setState({ statusError: failure });
        }
      },
      setDraft: (draft) => { setState({ draft }); },
      choose: (intent, example) => { setState({ intent, draft: example }); },
      clearIntent: () => { setState({ intent: null }); },
      dropSelection: (dropped) => { setState({ selectionDropped: dropped }); },
      async send(given, givenIntent, givenFiles, translation) {
        const state = getState();
        const text = (given ?? state.draft).trim();
        if (text === '' || state.running) return;
        const intent = givenIntent ?? state.intent ?? 'free';
        const files = given === undefined ? state.files : givenFiles ?? [];
        const labels = files.map(({ name, size, kind }) => ({ name, size, kind }));
        const selection = state.selectionDropped || translation !== undefined ? [] : [...target.selection()];
        push(kit, { kind: 'request', id: kit.next(), intent, text, selection, files: labels, translation });
        if (given === undefined) setState({ draft: '', intent: null, files: [] });
        await (state.mode === 'model' ? exchange(kit, intent, text, files, translation) : copyPrompt(kit, intent, text, files, translation));
      },
      stop: () => { calls.running?.abort(); },
      restart() {
        calls.running?.abort();
        setState({ turns: [], history: [], draft: '', intent: null, selectionDropped: false, files: [] });
      },
      paste: (turnId, pasted) => readPasted(kit, turnId, pasted),
    };
  });
}
