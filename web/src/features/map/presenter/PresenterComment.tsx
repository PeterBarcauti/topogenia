// PresenterComment.tsx
// The writing of the comment of presentation of the step shown (ADR 099 revised), where the subtitles stand, in the language shown: the original in the language of origin of the step, a translation in another one, its original shown above, its characters counted under it out of 500 (UX-MOY-04); Entrée keeps it, Maj+Entrée goes to the next line, Échap closes it with its text kept (essai J6 of 3.2); an empty text gives the step its own sentences back. The comments are read again when the field opens, so that it shows the line of now.

import { useEffect, useId, useRef, type KeyboardEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { useStore } from 'zustand';
import { useUnsavedForm } from '../../../shared/state/useUnsavedForm.ts';
import { useShortcutLabel } from '../../../shared/ui/useShortcutLabel.ts';
import type { MapEditor } from '../state/editor.ts';
import { titleIn } from '../../../shared/i18n/languageNames.ts';
import { closeComment, COMMENT_MAX, escapeComment, fieldOf, lineIn, usePresenterComments } from './comments.ts';
import { useEscapeHeld } from './escapeKey.ts';
import { ARROW } from './model/voice.ts';
import type { Presentation } from './usePresentation.ts';

/**
 * The cursor of the field: in it when it opens, back in it once the host has answered a text it refused, wherever the
 * gesture that sent the text had taken it (a step clicked, a button of the bar), and back in it each time it is
 * recalled: a gesture that left nothing (a refusal just said, a conflict said again) had taken it too. Not while the
 * keyboard walks the controls of the stage (`keyed`): the button a key pressed keeps the focus, for the same key
 * pressed again to be the same gesture, which gives the text up; brought back in the field, that key would send the
 * text, over the text of another after a conflict.
 */
function useFieldCursor(sending: boolean, recalled: number, keyed: boolean) {
  const field = useRef<HTMLTextAreaElement>(null);
  const walked = useRef(false);
  const opened = useRef(false);
  const sent = useRef(false);
  useEffect(() => {
    walked.current = keyed;
  }, [keyed]);
  useEffect(() => {
    if (!opened.current || !walked.current) field.current?.focus();
    opened.current = true;
  }, [recalled]);
  useEffect(() => {
    if (sent.current && !sending && !walked.current) field.current?.focus();
    sent.current = sending;
  }, [sending]);
  return field;
}

/**
 * The field of the comment of one step, its text the line kept in the language shown. In the language of origin of the
 * step it writes the original; in another one, a translation, the original shown above it. Its text is kept by the
 * presentation, not by the field: every way out of the field keeps it first (`afterWritten`). A text left as it was is
 * not sent (R2-01); a text typed is sent with the version of the line the field showed (R2-08).
 */
function CommentField({ editor, presentation, stepId, keyed }: { editor: MapEditor; presentation: Presentation; stepId: string; keyed: boolean }) {
  const { t, i18n } = useTranslation();
  const shortcut = useShortcutLabel();
  const id = useId();
  const kept = useStore(editor.presenter, (state) => state.comments[presentation.map.id]?.[stepId]);
  const typed = useStore(editor.presenter, (state) => state.typed);
  const sending = useStore(editor.presenter, (state) => state.sending);
  const recalled = useStore(editor.presenter, (state) => state.recalled);
  const field = useFieldCursor(sending, recalled, keyed);
  // A flow is named by its two steps, and its original is in the language of origin of the step it leaves (lot J10).
  const flow = presentation.map.flows.find((candidate) => candidate.id === stepId);
  const figureOf = (id: string) => presentation.layout.steps.find((shown) => shown.step.id === id);
  const figure = figureOf(flow?.source_step_id ?? stepId);
  // A step without a label, as most gateways are, is named by its code (independent review of the lot J10).
  const named = (shown: typeof figure): string => (shown === undefined ? '' : shown.label !== '' ? shown.label : shown.step.code);
  const name = flow === undefined ? named(figure) : `${named(figure)} ${ARROW} ${named(figureOf(flow.target_step_id))}`;
  const origin = figure?.step.source_language ?? presentation.map.languages.origin;
  const original = presentation.language === origin ? undefined : lineIn(kept, origin);
  const { text, over } = fieldOf(kept, presentation.language, typed);
  // A text typed and not sent is a form not validated, for « Quit » (R12); back as the line shown, sent or given up, it is none.
  useUnsavedForm(typed !== null && text !== over.text);
  // Read again as the field opens: a line removed then written again by another has the version of the one read at the start, and another text (R1-02).
  usePresenterComments(editor, presentation.map.id);
  useEscapeHeld();
  const keyDown = (event: KeyboardEvent<HTMLTextAreaElement>) => {
    // A key of a text being composed (an input method) is the one of its composition: Échap there leaves the composition, not the field.
    if (event.nativeEvent.isComposing) return;
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault();
      // A key held acts once: its repeats would send again a text just refused, and write over the line read again after a conflict.
      if (!event.repeat) closeComment(editor, presentation, false);
    } else if (event.key === 'Escape') {
      event.preventDefault();
      // A key held acts once.
      if (!event.repeat) escapeComment(editor, presentation);
    }
  };
  return (
    <div className="presenter-comment" data-testid="presenter-comment-editor">
      <label className="presenter-comment-label" htmlFor={id}>{t('presenter.comment.label', { step: name })}</label>
      {original !== undefined && (
        <p className="presenter-comment-original" data-testid="presenter-comment-original">
          <span>{t('presenter.comment.original', { language: titleIn(origin, i18n.language) })}</span>
          <span className="presenter-comment-original-text" lang={origin}>{original.text}</span>
        </p>
      )}
      <textarea id={id} ref={field} className="presenter-comment-field" value={text} maxLength={COMMENT_MAX} rows={3} lang={presentation.language}
        placeholder={t('presenter.comment.placeholder')} readOnly={sending} aria-busy={sending} data-testid="presenter-comment-field"
        onChange={(event) => {
          editor.presenter.getState().type({ text: event.target.value, over, refused: false, conflict: typed?.conflict ?? null });
        }}
        onKeyDown={keyDown} />
      <p className="presenter-comment-hint" data-testid="presenter-comment-hint">
        <span data-testid="presenter-comment-language">{t('presenter.comment.language', { language: titleIn(presentation.language, i18n.language) })}</span>
        <span data-testid="presenter-comment-count">{t('presenter.comment.count', { n: text.length, max: COMMENT_MAX })}</span>
        <span>{t('presenter.comment.keys', { save: shortcut('Enter'), line: shortcut('Shift+Enter'), close: shortcut('Escape') })}</span>
      </p>
    </div>
  );
}

/** The writing of a comment while one is open, nothing else: a field of its own for each one opened, which takes the cursor. */
export function PresenterComment({ editor, presentation, keyed }: { editor: MapEditor; presentation: Presentation; keyed: boolean }) {
  const stepId = useStore(editor.presenter, (state) => state.writing);
  const opened = useStore(editor.presenter, (state) => state.fields);
  return stepId === null ? null : <CommentField key={`${String(opened)} ${stepId}`} editor={editor} presentation={presentation} stepId={stepId} keyed={keyed} />;
}
