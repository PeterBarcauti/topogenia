// presenterStore.ts
// The state of a presentation (ADR 097), kept by the editor of the map and never by the browser: on or off, the step it started from, its player (paused when it opens: the presenter starts the story, lot J10), a screen blanked; the map of a detail it dived into and the levels left above it (F-120); the comments of presentation read, the one written and the text typed in its field (ADR 099); what it says in its stage of a refusal or a failure.

import { createStore, type StoreApi } from 'zustand/vanilla';
import type { Schema } from '../../../api/client.ts';
import { ofFlow, type AnyComment, type CommentTyped, type FlowComment, type PresentationComment } from './comments.ts';
import { advance, PLAYER_START, playerTo, SPEEDS, type Move, type PlayerState } from './model/player.ts';
import type { Scene } from './model/script.ts';
import type { VoiceMark } from './model/voice.ts';
import type { ProjectVoice } from './projectVoices.ts';

/** The screen of the room: the map, or black or white while the presenter speaks (PowerPoint, reveal.js: B and W). */
export type PresenterScreen = 'map' | 'black' | 'white';

/** A level of a presentation left for the detail of one of its steps (F-120): its map (null, the one of the editor), where its story stood, the step. */
export interface PresenterLevel {
  readonly map: Schema<'MapView'> | null;
  readonly from: string | null;
  readonly player: PlayerState;
  readonly stepId: string;
}

export interface PresenterState {
  readonly active: boolean;
  /** The map presented: null for the one of the editor, else the detail of a step it dived into (F-120). */
  readonly map: Schema<'MapView'> | null;
  /** The levels left above the map presented, the first the one of the editor. */
  readonly levels: readonly PresenterLevel[];
  /** The step the tour starts from, null for its start. */
  readonly from: string | null;
  readonly player: PlayerState;
  readonly screen: PresenterScreen;
  /** One more at each start, so that the stage frames the map again. */
  readonly run: number;
  /** The voice is reading the sentence shown: the clock waits for it at the end of that sentence. */
  readonly speaking: boolean;
  /** The question of the voice shown: the voice of the computer or a natural online one, asked at the first V (essai J2 of 3.2). */
  readonly asking: boolean;
  /** The settings of the voice shown. While they or the question are, the screen is not blanked and no key drives the story (R6-01). */
  readonly tuning: boolean;
  /** The voice the project keeps for each language that has one (lot J10), as read when a presentation starts and after each choice of a facilitator. */
  readonly projectVoices: readonly ProjectVoice[];
  /** The voice picked in the settings of the voice, by language: it reads before the one of the project, from a presentation to the next, until the map is left. */
  readonly picked: Readonly<Record<string, VoiceMark>>;
  /** One more each time the voices of the project are kept: a reading asked before is older than what was kept since, and is left (independent review of the lot J10). */
  readonly voiceKeeps: number;
  /** One more each time the lines of a comment are kept after a write: a reading of the comments asked before may lack them, and is asked again. */
  readonly commentKeeps: number;
  /**
   * The lines of the comments of presentation read, by map then by the step or the flow each comments (ADR 099 revised;
   * the flows since the lot J10): a step, and the way along a flow, say the one of the language shown instead of their own sentences.
   */
  readonly comments: Readonly<Record<string, Readonly<Record<string, readonly AnyComment[]>>>>;
  /** The step whose comment is being written, null while none is: the story waits meanwhile (ADR 099). */
  readonly writing: string | null;
  /**
   * The flow last shown by a click, null before any: among the flows a passage lights together, the one the comment is
   * opened for (lot J10, second independent review). Kept while the map is presented; a passage that does not light it
   * takes its first flow.
   */
  readonly pointed: string | null;
  /** The text typed in the field of the comment written, null while nothing is typed: kept here, not by the field, so that every way out of the field keeps it first (ADR 099 revised, essai J6). */
  readonly typed: CommentTyped | null;
  /** The text of the field is on its way to the host: the field waits, and nothing leaves it meanwhile. */
  readonly sending: boolean;
  /** One more at each field of a comment opened: a field opened again on the step it was on takes the cursor again. */
  readonly fields: number;
  /** One more each time the field of the comment written takes the cursor back: a gesture of the pointer had taken it, and was done for nothing. */
  readonly recalled: number;
  /** A refusal or a failure said in the stage, where the full screen shows it (the notifications of the application are under it): a new object each time, null once gone; `at`, when it was said, by the clock of the page. */
  readonly alert: { readonly text: string; readonly at: number } | null;
  readonly start: (from: string | null) => void;
  /** Tells the tour again from a step clicked off it: that step at once, paused or playing as before, at the same speed (audit 3.2, BUG-04). */
  readonly jump: (from: string) => void;
  readonly stop: () => void;
  /** The scene shown starts again, playing or paused as it was: its text has just changed (its messages cut or given back, lot J10). */
  readonly restart: () => void;
  /** Play or pause; at the end, plays again from the opening. */
  readonly toggle: (durations?: readonly number[]) => void;
  /** A move of the presenter; `lines`, the sentences of each scene, for next and previous to go by them. */
  readonly move: (move: Move, scenes: readonly Scene[], lines?: readonly (readonly string[])[]) => void;
  readonly tick: (ms: number, durations: readonly number[], hold?: number) => void;
  readonly faster: () => void;
  readonly slower: () => void;
  /** Blanks the screen in black or white, or shows the map again when it is already so. */
  readonly blank: (screen: Exclude<PresenterScreen, 'map'>) => void;
  /** Goes by an exit of the decision shown: the story moves to the stop of that decision which takes it, and its flight plays at once (lot J10: every exit has its stop in the tour). */
  readonly choose: (stop: number, scenes: readonly Scene[]) => void;
  readonly speak: (speaking: boolean) => void;
  /** Shows the question of the voice, or takes it away; shown, it closes the settings and shows the map again under a screen blanked. */
  readonly ask: (asking: boolean) => void;
  /** Shows the settings of the voice, or takes them away; shown, they close the question and show the map again under a screen blanked. */
  readonly tune: (tuning: boolean) => void;
  /** Presents the map of the detail of a step, from its opening, playing or paused as before, its counters its own (F-120). */
  readonly dive: (stepId: string, map: Schema<'MapView'>) => void;
  /** Back to the level above, on the step it dived into, paused: Play goes on with its story (F-120). */
  readonly surface: () => void;
  /** The comments of a map as read: those kept before for it are replaced. */
  readonly keepComments: (mapId: string, comments: readonly PresentationComment[]) => void;
  /** The comments of the flows of a map as read: those of its flows kept before are replaced, those of its steps stay (lot J10). */
  readonly keepFlowComments: (mapId: string, comments: readonly FlowComment[]) => void;
  /** The lines of the comment of a step as they are after a write, none when the comment was removed; the scene shown, the one of that step, starts again, paused. */
  readonly keepComment: (mapId: string, stepId: string, lines: readonly AnyComment[]) => void;
  /** Opens the writing of the comment of a step, the story paused, nothing typed yet; null closes it. While it is open, the moves of the story, play, a blank screen, a dive and a way back do nothing. */
  readonly write: (stepId: string | null) => void;
  /** Keeps the flow a click has just shown, for the comment opened on its passage. */
  readonly point: (flowId: string) => void;
  /** Keeps the text typed in the field of the comment written. */
  readonly type: (typed: CommentTyped) => void;
  /** The text of the field leaves for the host, or its answer has come. */
  readonly send: (sending: boolean) => void;
  /** Gives the cursor back to the field of the comment written. */
  readonly recall: () => void;
  /** Keeps the voices of the project as the host gave them. */
  readonly keepVoices: (voices: readonly ProjectVoice[]) => void;
  /** The voice picked in the settings for a language. */
  readonly pick: (language: string, voice: VoiceMark) => void;
  /** Says a refusal or a failure in the stage; null takes it away, as the next gesture of the presenter does. */
  readonly say: (text: string | null) => void;
}

const atEnd = (player: PlayerState, durations: readonly number[]): boolean =>
  durations.length > 0 && player.scene === durations.length - 1 && player.elapsed >= (durations[durations.length - 1] ?? 0);

const speedNext = (speed: number, step: 1 | -1): number => {
  const index = SPEEDS.indexOf(speed);
  return SPEEDS[Math.min(SPEEDS.length - 1, Math.max(0, (index < 0 ? SPEEDS.indexOf(1) : index) + step))] ?? 1;
};

type Setter = StoreApi<PresenterState>['setState'];
type Getter = () => PresenterState;

/** What a gesture of the presenter leaves: the refusal or the failure the stage said before it is taken away. */
const QUIET = { alert: null } as const;

/** The question and the settings of the voice closed: a presentation that starts, stops or changes level shows neither. */
const UNASKED = { asking: false, tuning: false } as const;

/** No flow pointed at: a presentation that starts, stops or changes level is on a map whose flows are others, or told anew. */
const UNPOINTED = { pointed: null } as const;

/**
 * Whether a comment is being written: a gesture that would move the story, play it, hide it or change its level then
 * does nothing, so that the text typed is never lost unseen (R2-05, R6-11). Leaving the presentation still leaves it.
 */
const written = (get: Getter): boolean => get().writing !== null;

/** No comment written: no field, nothing typed, nothing on its way. */
const UNWRITTEN = { writing: null, typed: null, sending: false } as const;

/** The comments of presentation kept by map, and the writing of one, the story paused meanwhile (ADR 099). */
function commentActions(set: Setter, get: Getter): Pick<PresenterState, 'keepComments' | 'keepFlowComments' | 'keepComment' | 'write' | 'point' | 'type' | 'send' | 'recall'> {
  /** The comments kept for a map, those of its flows or those of its steps. */
  const keptOf = (mapId: string, flows: boolean) => Object.fromEntries(Object.entries(get().comments[mapId] ?? {}).filter(([, lines]) => lines.some(ofFlow) === flows));
  return {
    keepComments(mapId, comments) {
      const bySteps: Record<string, PresentationComment[]> = {};
      for (const comment of comments) (bySteps[comment.step_id] ??= []).push(comment);
      set({ comments: { ...get().comments, [mapId]: { ...keptOf(mapId, true), ...bySteps } } });
    },
    keepFlowComments(mapId, comments) {
      const byFlows: Record<string, FlowComment[]> = {};
      for (const comment of comments) (byFlows[comment.flow_id] ??= []).push(comment);
      set({ comments: { ...get().comments, [mapId]: { ...keptOf(mapId, false), ...byFlows } } });
    },
    keepComment(mapId, stepId, lines) {
      const others = Object.entries(get().comments[mapId] ?? {}).filter(([id]) => id !== stepId);
      // The step shown says its comment from its first sentence: the time already played is not the one of the new text (R2-04).
      set({ commentKeeps: get().commentKeeps + 1, comments: { ...get().comments, [mapId]: Object.fromEntries(lines.length === 0 ? others : [...others, [stepId, lines]]) }, player: { ...get().player, elapsed: 0, playing: false } });
    },
    write(stepId) {
      const { player, fields } = get();
      set(stepId === null ? { ...QUIET, ...UNWRITTEN }
        : { ...QUIET, ...UNASKED, ...UNWRITTEN, writing: stepId, fields: fields + 1, player: { ...player, playing: false }, screen: 'map' });
    },
    point(flowId) {
      set({ pointed: flowId });
    },
    type(typed) {
      if (written(get)) set({ typed });
    },
    send(sending) {
      if (written(get)) set({ sending });
    },
    recall() {
      if (written(get)) set({ recalled: get().recalled + 1 });
    },
  };
}

type StoryActions = Pick<PresenterState, 'start' | 'jump' | 'stop' | 'restart' | 'toggle' | 'move' | 'tick' | 'faster' | 'slower' | 'blank' | 'choose'>;

/** The story told: started and stopped, played and paused, moved by the presenter or by its clock, sped up, blanked, sent by an exit of a decision. */
function storyActions(set: Setter, get: Getter): StoryActions {
  return {
    start(from) {
      // Paused on the whole map: the presenter starts the story when the room is ready (asked by the project owner, lot J10).
      set({ ...QUIET, ...UNASKED, ...UNPOINTED, active: true, map: null, levels: [], from, player: PLAYER_START, screen: 'map', run: get().run + 1, ...UNWRITTEN });
    },
    jump(from) {
      if (written(get)) return;
      set({ ...QUIET, from, player: { ...get().player, scene: 1, elapsed: 0 }, screen: 'map' });
    },
    stop() {
      set({ ...QUIET, ...UNASKED, ...UNPOINTED, active: false, map: null, levels: [], screen: 'map', speaking: false, ...UNWRITTEN });
    },
    restart() {
      if (get().player.elapsed !== 0) set({ player: { ...get().player, elapsed: 0 } });
    },
    toggle(durations = []) {
      if (written(get)) return;
      const { player } = get();
      set({ ...QUIET, player: atEnd(player, durations) ? { ...PLAYER_START, speed: player.speed, playing: true } : { ...player, playing: !player.playing }, screen: 'map' });
    },
    move(move, scenes, lines = []) {
      if (written(get)) return;
      set({ ...QUIET, player: playerTo(get().player, move, scenes, lines), screen: 'map' });
    },
    tick(ms, durations, hold) {
      const { player, levels } = get();
      const next = advance(player, ms, durations, hold);
      if (next === player) return;
      // The end of the story of a detail: back to the step of the map above (F-120).
      if (levels.length > 0 && player.playing && !next.playing && atEnd(next, durations)) get().surface();
      else set({ player: next });
    },
    faster() {
      set({ ...QUIET, player: { ...get().player, speed: speedNext(get().player.speed, 1) } });
    },
    slower() {
      set({ ...QUIET, player: { ...get().player, speed: speedNext(get().player.speed, -1) } });
    },
    blank(screen) {
      if (written(get)) return;
      set({ ...QUIET, screen: get().screen === screen ? 'map' : screen });
    },
    choose(stop, scenes) {
      if (written(get)) return;
      const scene = scenes.findIndex((candidate) => candidate.kind === 'passage' && candidate.stop === stop);
      if (scene >= 0) set({ ...QUIET, player: { ...get().player, scene, elapsed: 0, playing: true }, screen: 'map' });
    },
  };
}

/** The levels of a presentation (F-120): into the map of the detail of a step, and back to the map above. */
function levelActions(set: Setter, get: Getter): Pick<PresenterState, 'dive' | 'surface'> {
  return {
    dive(stepId, map) {
      if (written(get)) return;
      const { player, from, levels, run } = get();
      set({
        ...QUIET, ...UNASKED, ...UNPOINTED, levels: [...levels, { map: get().map, from, player, stepId }], map, from: null,
        player: { ...PLAYER_START, playing: player.playing, speed: player.speed }, screen: 'map', run: run + 1, ...UNWRITTEN,
      });
    },
    surface() {
      const { levels, player, run } = get();
      const level = levels.at(-1);
      if (level === undefined || written(get)) return;
      set({ ...QUIET, ...UNASKED, ...UNPOINTED, levels: levels.slice(0, -1), map: level.map, from: level.from, player: { ...level.player, playing: false, speed: player.speed },
        screen: 'map', run: run + 1, ...UNWRITTEN });
    },
  };
}

/**
 * The voice of a presentation: whether it reads, its question and its settings shown or not, one of them at a time, never
 * under a screen blanked. The voice chosen is a preference of the browser (UX-MOY-01), picked for the session, and kept
 * by the project for its language when a facilitator chose it (lot J10).
 */
function voiceActions(set: Setter, get: Getter): Pick<PresenterState, 'speak' | 'ask' | 'tune' | 'keepVoices' | 'pick'> {
  return {
    keepVoices(projectVoices) {
      set({ projectVoices, voiceKeeps: get().voiceKeeps + 1 });
    },
    pick(language, voice) {
      set({ picked: { ...get().picked, [language]: voice } });
    },
    speak(speaking) {
      if (get().speaking !== speaking) set({ speaking });
    },
    ask(asking) {
      if (get().asking !== asking) set(asking ? { ...QUIET, asking, tuning: false, screen: 'map' } : { ...QUIET, asking });
    },
    tune(tuning) {
      if (get().tuning !== tuning) set(tuning ? { ...QUIET, tuning, asking: false, screen: 'map' } : { ...QUIET, tuning });
    },
  };
}

export function createPresenterStore(): StoreApi<PresenterState> {
  return createStore<PresenterState>()((set, get) => ({
    active: false,
    map: null,
    levels: [],
    from: null,
    player: PLAYER_START,
    screen: 'map',
    run: 0,
    speaking: false,
    asking: false,
    tuning: false,
    projectVoices: [],
    picked: {},
    voiceKeeps: 0,
    commentKeeps: 0,
    comments: {},
    writing: null,
    pointed: null,
    typed: null,
    sending: false,
    fields: 0,
    recalled: 0,
    alert: null,
    ...storyActions(set, get),
    ...levelActions(set, get),
    ...voiceActions(set, get),
    ...commentActions(set, get),
    say(text) {
      set({ alert: text === null ? null : { text, at: performance.now() } });
    },
  }));
}
