// comments.ts
// The comments of presentation of the steps (ADR 099 revised): every line of them read with the map presented, the one of the language shown written by those who may change the map (the original in the language of origin of the step, a translation in another), kept by the presentation while it runs; the text typed in the field kept before the field is left, by Entrée, Échap, the pencil, the way out or a step shown by the pointer, which the field then follows (essai J6 of 3.2).

import { useEffect } from 'react';
import { api, type Schema } from '../../../api/client.ts';
import { ApiError, isApiError, NETWORK_ERROR } from '../../../api/errors.ts';
import { messageOf } from '../../../shared/i18n/errorMessage.ts';
import type { MapEditor } from '../state/editor.ts';
import { escapeTaken } from './escapeKey.ts';
import { scenesOf } from './model/script.ts';
import { tourOf, type Tour } from './model/tour.ts';
import type { Presentation } from './usePresentation.ts';

export type PresentationComment = Schema<'PresentationCommentView'>;

/** A line of the comment of a flow (lot J10): as the one of a step, its flow in the place of its step. */
export type FlowComment = Schema<'FlowPresentationCommentView'>;

/** A line of a comment of presentation, of a step or of a flow. */
export type AnyComment = PresentationComment | FlowComment;

/** Whether a line is the one of a flow. */
export const ofFlow = (line: AnyComment): line is FlowComment => 'flow_id' in line;

/** What a field shows of the line of its language: its text and its version, nothing and 0 without a line. */
export interface ShownLine {
  readonly text: string;
  readonly version: number;
}

/**
 * The text typed in the field of a comment, over the line that field showed; `refused`, the host has just refused that
 * very text: a gesture that leaves the field then gives it up. `conflict`, what the stage said when the line changed
 * under the field, until a text is kept: a gesture that leaves says it again, and never writes over the text of another.
 */
export interface CommentTyped {
  readonly text: string;
  readonly over: ShownLine;
  readonly refused: boolean;
  readonly conflict: string | null;
}

/** The longest comment: one to three sentences, read aloud in a few seconds (`PresentationCommentRules.TextMax`). */
export const COMMENT_MAX = 500;

/**
 * The longest a text waits for the answer of the host: beyond, its field takes its hand back and the lack of answer is said,
 * as a refusal is. A save takes well under a second; a host or a network that never answers must hold nobody in the field.
 */
export const COMMENT_SEND_MS = 10_000;

/**
 * How long a refusal must have been shown before a gesture that leaves the field gives its text up: the time to see it.
 * Sooner, the gesture is the second click of a double click, or a key pressed twice by reflex: nothing moves.
 */
export const REFUSAL_READ_MS = 1000;

/** Where the comments are read and written: the host, a fake in the tests. */
export interface CommentSource {
  /** Every line of the comments of the live steps of a map: the original of each and its translations. */
  readonly read: (mapId: string, signal: AbortSignal) => Promise<readonly PresentationComment[]>;
  /**
   * The line of the comment of a step written in a language, with the version read of that line (0 without one); an
   * empty text removes it (the original takes its translations with it). Gives every line of the step afterwards.
   * `signal`, the send given up: its field waits for it no longer.
   */
  readonly write: (stepId: string, text: string, language: string, version: number, signal: AbortSignal) => Promise<readonly PresentationComment[]>;
  /** Every line of the comments of the live flows of a map (lot J10): the original of each, in the language of origin of the step it leaves, and its translations. */
  readonly readFlows: (mapId: string, signal: AbortSignal) => Promise<readonly FlowComment[]>;
  /** The line of the comment of a flow written in a language, as the one of a step is; gives every line of the flow afterwards. */
  readonly writeFlow: (flowId: string, text: string, language: string, version: number, signal: AbortSignal) => Promise<readonly FlowComment[]>;
}

export const hostComments: CommentSource = {
  read: (mapId, signal) => api.get('/api/maps/{id}/presentation-comments', { path: { id: mapId }, signal }),
  write: async (stepId, text, language, version, signal) =>
    (await api.put('/api/steps/{id}/presentation-comment', { path: { id: stepId }, version, body: { text, language }, signal })).comments,
  readFlows: (mapId, signal) => api.get('/api/maps/{id}/flow-presentation-comments', { path: { id: mapId }, signal }),
  writeFlow: async (flowId, text, language, version, signal) =>
    (await api.put('/api/flows/{id}/presentation-comment', { path: { id: flowId }, version, body: { text, language }, signal })).comments,
};

/** The line of a comment in a language, among the lines of its step or of its flow; none when it has no comment in that language. */
export function lineIn(lines: readonly AnyComment[] | undefined, language: string): AnyComment | undefined {
  return lines?.find((line) => line.language === language);
}

const shownOf = (line: AnyComment | undefined): ShownLine => ({ text: line?.text ?? '', version: line?.version ?? 0 });

/**
 * The text of the field of a comment, and the line it is written over. While nothing is typed, the field is the line of
 * now: comments read after it opened are shown (R2-08), and so is the line read again when it opens (R1-02). Once a
 * text is typed, it stays, over the line the field showed: in its version of now while that line still has the text
 * shown, in the version shown once it has another text, which the host then refuses.
 */
export function fieldOf(kept: readonly AnyComment[] | undefined, language: string, typed: CommentTyped | null): { text: string; over: ShownLine } {
  const now = shownOf(lineIn(kept, language));
  const over = typed === null || typed.over.text === now.text ? now : typed.over;
  return { text: typed?.text ?? over.text, over };
}

/**
 * What the scene shown may take a comment for: the step of a stop; on a passage, a flow it lights (lot J10: a flow
 * takes a comment as a step does), or the step it leaves when it is a jump without a flow; null at the opening and the
 * closing, and for a person who may not change the map (a reader, a contributor: ADR 099). Among the flows a passage
 * lights together, the one last shown by a click (`pointed`), else the first: a flow clicked is the one written for,
 * whichever it is (second independent review of the lot J10).
 */
export function commentableAt(presentation: Presentation, scene: number, pointed: string | null = null): string | null {
  const shown = presentation.scenes[scene];
  if (presentation.map.rights.role !== 'facilitator' || (shown?.kind !== 'step' && shown?.kind !== 'passage')) return null;
  const stop = presentation.tour.stops[shown.stop];
  const flows = shown.kind === 'passage' ? stop?.next?.flows ?? [] : [];
  return (pointed !== null && flows.includes(pointed) ? pointed : flows[0]) ?? stop?.stepId ?? null;
}

/** Whether what a comment is written for is a flow of the map presented. */
export const isFlow = (presentation: Pick<Presentation, 'map'>, subjectId: string): boolean => presentation.map.flows.some((flow) => flow.id === subjectId);

/** The stop of a tour whose passage lights a flow, -1 when none does. */
const lighting = (tour: Tour, flowId: string): number => tour.stops.findIndex((candidate) => candidate.next?.flows.includes(flowId) === true);

/**
 * Where a flow is shown: on the tour told when one of its passages lights it; else on the tour told again from the
 * step the flow leaves (`from`), which lights every flow that leaves it: a story told from a step leaves out what
 * comes before it. Null for a flow no tour lights.
 */
function passageOf(presentation: Presentation, flowId: string): { readonly tour: Tour; readonly stop: number; readonly from: string | null } | null {
  const told = lighting(presentation.tour, flowId);
  if (told >= 0) return { tour: presentation.tour, stop: told, from: null };
  const source = presentation.map.flows.find((flow) => flow.id === flowId)?.source_step_id;
  if (source === undefined) return null;
  const again = tourOf(presentation.map, source);
  const stop = lighting(again, flowId);
  return stop >= 0 ? { tour: again, stop, from: source } : null;
}

/** Whether a click on a flow shows it: the field of a comment being written then follows it. */
export const showsFlow = (presentation: Presentation, flowId: string): boolean => passageOf(presentation, flowId) !== null;

/**
 * Shows a flow clicked (lot J10): the passage that lights it, paused or playing as before. A flow the story leaves
 * out is shown by the story told again from the step it leaves, as a step clicked off the story is (independent
 * review of the lot J10): every flow of the map is reached, and commented, by a click.
 */
export function showFlow(editor: MapEditor, presentation: Presentation, flowId: string): void {
  const shown = passageOf(presentation, flowId);
  if (shown === null) return;
  if (shown.from !== null) editor.presenter.getState().jump(shown.from);
  editor.presenter.getState().move({ passage: shown.stop }, shown.from === null ? presentation.scenes : scenesOf(shown.tour));
  // The flow pointed at: T, or the pencil, opens its field, among the flows this passage lights together.
  editor.presenter.getState().point(flowId);
}

/**
 * Opens the comment the scene shown takes, the story paused there: the one of the step shown; on a passage, the one of
 * its first flow, the story staying on the passage (lot J10), or, for a jump without a flow, the one of the step it
 * leaves, to which the story goes back. A decision shown again for its next exit says that exit only: the story goes
 * back to its first stop, the one that says its comment, so that the text kept is heard where it is said (independent
 * review, M7).
 */
export function openComment(editor: MapEditor, presentation: Presentation): void {
  const presenter = editor.presenter.getState();
  const stepId = commentableAt(presentation, presenter.player.scene, presenter.pointed);
  if (stepId === null) return;
  const shown = presentation.scenes[presenter.player.scene];
  const again = shown !== undefined && (shown.kind === 'step' || shown.kind === 'passage') && presentation.tour.stops[shown.stop]?.again === true;
  if (isFlow(presentation, stepId)) {
    editor.presenter.getState().write(stepId);
    return;
  }
  if (again) presenter.move({ stop: presentation.tour.stops.findIndex((candidate) => candidate.stepId === stepId) }, presentation.scenes);
  else if (shown?.kind === 'passage') presenter.move({ stop: shown.stop }, presentation.scenes);
  editor.presenter.getState().write(stepId);
}

/**
 * The comments of a map read for the presentation, again when it changes map (a detail dived into); a failure is said in
 * the stage, where the full screen shows it, and the sentences of the steps stay.
 */
export function usePresenterComments(editor: MapEditor, mapId: string): void {
  useEffect(() => {
    const control = new AbortController();
    const failed = (failure: unknown) => {
      if (!control.signal.aborted) editor.presenter.getState().say(messageOf(failure));
    };
    // An answer that comes after a comment was kept may be older than it, and lack it: the comments are asked again,
    // never taken as they are (independent review of the lot J10).
    const read = <Line,>(ask: () => Promise<readonly Line[]>, keep: (comments: readonly Line[]) => void) => {
      const asked = editor.presenter.getState().commentKeeps;
      ask().then((comments) => {
        if (control.signal.aborted) return;
        if (editor.presenter.getState().commentKeeps === asked) keep(comments);
        else read(ask, keep);
      }, failed);
    };
    read(() => editor.comments.read(mapId, control.signal), (comments) => {
      editor.presenter.getState().keepComments(mapId, comments);
    });
    // Those of the flows, by their own route (lot J10).
    read(() => editor.comments.readFlows(mapId, control.signal), (comments) => {
      editor.presenter.getState().keepFlowComments(mapId, comments);
    });
    return () => {
      control.abort();
    };
  }, [editor, mapId]);
}

/**
 * What became of a comment sent: kept by the host; refused because its line changed meanwhile (`conflict`) or for another
 * reason, with what the stage said of it; `silent`, nothing heard of the host within the time limit of a send.
 */
type CommentSaved = { readonly outcome: 'kept' } | { readonly outcome: 'silent' } | { readonly outcome: 'conflict' | 'refused'; readonly said: string };

const SILENT: CommentSaved = { outcome: 'silent' };

/** The line of a comment as its field sends it: its text, its language, and the version of the line the field showed (0 without one). */
interface CommentSent {
  readonly text: string;
  readonly language: string;
  readonly version: number;
}

/**
 * Writes the line of the comment of a step of the map presented, with the version of the line its field showed; kept,
 * the presentation keeps the lines of the step the host gave back. Refused, the refusal is said in the stage and the
 * text stays to be sent again; a conflict reads the comments of the map again, for the field to send the version of now.
 * A send given up (`signal`) takes nothing of an answer that comes later: nothing is said, and the story stays as it is.
 */
export async function saveComment(editor: MapEditor, mapId: string, stepId: string, sent: CommentSent, signal: AbortSignal, flow = false): Promise<CommentSaved> {
  // A new try: what the stage said of the one before is gone.
  editor.presenter.getState().say(null);
  // Asked anew after each wait: a send is given up while it waits.
  const givenUp = (): boolean => signal.aborted;
  try {
    // The line of a flow goes by the route of the flows (lot J10); `stepId` is then its flow.
    const written = await (flow ? editor.comments.writeFlow : editor.comments.write)(stepId, sent.text.trim(), sent.language, sent.version, signal);
    if (givenUp()) return SILENT;
    editor.presenter.getState().keepComment(mapId, stepId, written);
    return { outcome: 'kept' };
  } catch (failure) {
    if (givenUp()) return SILENT;
    const said = messageOf(failure);
    editor.presenter.getState().say(said);
    if (!isApiError(failure) || failure.status !== 409) return { outcome: 'refused', said };
    await (flow
      ? editor.comments.readFlows(mapId, signal).then((comments) => {
        if (!givenUp()) editor.presenter.getState().keepFlowComments(mapId, comments);
      }, () => undefined)
      : editor.comments.read(mapId, signal).then((comments) => {
        if (!givenUp()) editor.presenter.getState().keepComments(mapId, comments);
      }, () => undefined));
    return givenUp() ? SILENT : { outcome: 'conflict', said };
  }
}

/** The map and the language of the presentation a field writes in. */
type Written = Pick<Presentation, 'map' | 'language'>;

/**
 * What became of a text sent; `silent` once the time limit has passed without an answer. The send is then given up
 * (`signal`): the request is cancelled where it can be, and an answer that comes all the same is taken for nothing.
 */
async function answered(saving: (signal: AbortSignal) => Promise<CommentSaved>): Promise<CommentSaved> {
  const control = new AbortController();
  let timer: ReturnType<typeof setTimeout> | undefined;
  const silent = new Promise<CommentSaved>((resolve) => {
    timer = setTimeout(() => {
      resolve(SILENT);
      control.abort();
    }, COMMENT_SEND_MS);
  });
  const outcome = await Promise.race([saving(control.signal), silent]);
  clearTimeout(timer);
  return outcome;
}

/**
 * Sends the text of the field of a step, the field waiting meanwhile; says whether the host kept it. Refused, the text
 * stays in the field, marked as refused, over the line read again after a conflict, whose version the next try sends.
 * A host that has not answered within the time limit counts as a refusal, said as a lack of answer: its field holds
 * nobody (should the text have reached the host all the same, the next try is refused as a conflict, which reads the
 * line again). The answer is for the field that sent the text: one left meanwhile, or another opened since on the same
 * step, takes nothing of it.
 */
async function sendWritten(editor: MapEditor, { map, language }: Written, stepId: string, text: string, over: ShownLine): Promise<boolean> {
  const { fields, typed } = editor.presenter.getState();
  editor.presenter.getState().send(true);
  const saved = await answered((signal) => saveComment(editor, map.id, stepId, { text, language, version: over.version }, signal, isFlow({ map }, stepId)));
  const now = editor.presenter.getState();
  if (now.writing !== stepId || now.fields !== fields) return false;
  now.send(false);
  if (saved.outcome === 'kept') return true;
  // Said as the field takes its hand back: the lack of an answer; and a refusal again, for the one of a conflict may have
  // gone while the comments were read again.
  now.say(saved.outcome === 'silent' ? messageOf(new ApiError(0, NETWORK_ERROR, '')) : saved.said);
  const conflict = saved.outcome === 'conflict';
  now.type({ text, over: conflict ? shownOf(lineIn(now.comments[map.id]?.[stepId], language)) : over, refused: true, conflict: conflict ? saved.said : typed?.conflict ?? null });
  return false;
}

/**
 * Does what a gesture asks once the field of the comment written may be left, its text kept first when it changed (ADR
 * 099 revised, essai J6): Entrée, Échap, the pencil, the way out and a step shown by the pointer all keep the text
 * typed. At once without a field, or for a text left as it was: nothing is written, the line of another language and an
 * automatic translation stay as they are (R2-01). After the host has kept the text otherwise. Never while a text is on
 * its way, nor when the host refuses it: the refusal is said in the stage and the text stays in the field. A gesture
 * that leaves the field (`leaving`: all but Entrée, which sends again) on a text the host has just refused gives that
 * text up: the person reads the refusal, and no field holds anybody in it. Only while that refusal is still shown: once
 * it has gone, the gesture sends the text again first, so that a refusal long past gives no text up unseen. After a
 * conflict, a gesture that leaves sends nothing: the line read again is the text of another, which only Entrée, pressed
 * knowing it, writes over (ADR 099, point 5); the conflict is said again, and the same gesture then gives the text up.
 * Giving a text up takes a refusal shown long enough to be seen (`REFUSAL_READ_MS`): sooner, the gesture is the second
 * click of a double click or a key pressed twice, and nothing moves. A gesture done for nothing gives the cursor back to
 * the field, which a button clicked had taken.
 */
export function afterWritten(editor: MapEditor, presentation: Written, leaving: boolean, then: () => void): void {
  const presenter = editor.presenter.getState();
  const stepId = presenter.writing;
  if (stepId === null) {
    then();
    return;
  }
  if (presenter.sending) return;
  const { typed, alert } = presenter;
  const { text, over } = fieldOf(presenter.comments[presentation.map.id]?.[stepId], presentation.language, typed);
  if (text.trim() === over.text.trim()) {
    then();
    return;
  }
  if (leaving && typed?.refused === true && alert !== null) {
    if (performance.now() - alert.at >= REFUSAL_READ_MS) then();
    else presenter.recall();
    return;
  }
  if (leaving && typed !== null && typed.conflict !== null) {
    presenter.say(typed.conflict);
    presenter.type({ ...typed, refused: true });
    presenter.recall();
    return;
  }
  void sendWritten(editor, presentation, stepId, text, over).then((kept) => {
    if (kept) then();
  });
}

/** Closes the field of the comment written, its text kept first: Entrée, which sends again a text refused; the pencil, which leaves. */
export function closeComment(editor: MapEditor, presentation: Written, leaving: boolean): void {
  afterWritten(editor, presentation, leaving, () => {
    editor.presenter.getState().write(null);
  });
}

/**
 * Échap while a comment is written, in its field or elsewhere in the stage: the field closes, its text kept, and the key
 * is marked as taken, for the full screen a browser leaves on it to keep the presentation (`useFullscreen`).
 */
export function escapeComment(editor: MapEditor, presentation: Written): void {
  escapeTaken();
  closeComment(editor, presentation, true);
}

/**
 * The way out of the presentation: the text of a comment written is kept first, and a text the host refuses keeps the
 * presentation, which says why. While a text is on its way, the way out waits for its answer as every gesture does, the
 * time limit of a send at most: left at once, the presentation would say to nobody a refusal that came after, and the
 * text would be lost unseen (second critical reading of 13/11/2026).
 */
export function leavePresentation(editor: MapEditor, presentation: Written): void {
  afterWritten(editor, presentation, true, () => {
    editor.presenter.getState().stop();
  });
}

/**
 * A move of the story asked by the pointer: a step clicked on the map, a chapter, a step before or after or the start
 * by the bar. While a comment is written, its field follows the step, as the notes of a slide follow it: the text is
 * kept first, the story moves, and the field opens on the step reached, the cursor in it; it closes where the scene
 * reached has no step (the opening, the closing). A text the host refuses keeps the story and the field where they are.
 * `reached`, the step a click names, which a story told again from it shows at once. The step written starts again
 * before the move, as it does after a text kept: « Étape précédente » then leaves it at once, whatever it had played.
 */
export function moveWritten(editor: MapEditor, presentation: Presentation, move: () => void, reached?: string): void {
  const writing = editor.presenter.getState().writing !== null;
  afterWritten(editor, presentation, true, () => {
    if (!writing) {
      move();
      return;
    }
    const written = presentation.scenes[editor.presenter.getState().player.scene];
    editor.presenter.getState().write(null);
    if (written?.kind === 'step') editor.presenter.getState().move({ stop: written.stop }, presentation.scenes);
    else if (written?.kind === 'passage') editor.presenter.getState().move({ passage: written.stop }, presentation.scenes);
    move();
    const stepId = reached ?? commentableAt(presentation, editor.presenter.getState().player.scene, editor.presenter.getState().pointed);
    if (stepId !== null) editor.presenter.getState().write(stepId);
  });
}
