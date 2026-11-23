// CommentsReply.tsx
// The comments of presentation written by the model, to review before they are kept (3.3, lot 3, F-130): one per step or flow of the map open and of its detailed maps, each map named when there are several, kept, corrected or dropped; one that would replace a comment typed by hand is unticked and says so, kept only when the person ticks it; the ones kept saved at once; saved, the card shown again says so.

import { useState } from 'react';
import { Save } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Icon } from '../../../shared/ui/Icon.tsx';
import type { AssistantReply } from './assistantStore.ts';
import type { CommentKept, ThreadContext } from './threadContext.ts';

/** The longest comment, as the host takes it. */
const COMMENT_MAX = 500;

interface Draft {
  readonly text: string;
  readonly kept: boolean;
}

type CommentDraft = NonNullable<AssistantReply['comment_drafts']>[number];

/** A comment that would replace one typed by a person: the person decides, never the AI. */
const replacesTyped = (draft: CommentDraft): boolean => draft.current !== null && draft.current_source === 'manual';

export function CommentsReply({ reply, context, turnId }: { reply: AssistantReply; context: ThreadContext; turnId: number }) {
  const { t } = useTranslation();
  const proposed = reply.comment_drafts ?? [];
  const [drafts, setDrafts] = useState<readonly Draft[]>(() => proposed.map((comment) => ({ text: comment.text, kept: !replacesTyped(comment) })));
  const [saved, setSaved] = useState<number | null>(() => context.store.getState().commentsSaved[turnId] ?? null);
  const [saving, setSaving] = useState(false);
  if (proposed.length === 0) return null;
  // The map open and its detailed maps (essay of the 18/11/2026): each map named before its first comment when the reply covers several.
  const several = new Set(proposed.map((comment) => comment.map)).size > 1;
  const change = (index: number, draft: Partial<Draft>) => {
    setDrafts((before) => before.map((current, at) => (at === index ? { ...current, ...draft } : current)));
  };
  const kept: CommentKept[] = proposed.flatMap((comment, index) => {
    const draft = drafts[index];
    return draft === undefined || !draft.kept || draft.text.trim() === ''
      ? []
      : [{ stepId: comment.step_id, flowId: comment.flow_id, text: draft.text.trim(), version: comment.version, replace: replacesTyped(comment) }];
  });
  const save = async () => {
    if (context.saveComments === undefined) return;
    setSaving(true);
    const done = await context.saveComments(kept);
    setSaving(false);
    if (!done) return;
    context.store.getState().keepComments(turnId, kept.length);
    setSaved(kept.length);
  };
  return (
    <div className="reply-summaries" data-testid="assistant-comments">
      <p className="field-hint">{t('assistant.comments.intro')}</p>
      <ol className="summary-list">
        {proposed.map((comment, index) => {
          const draft = drafts[index] ?? { text: comment.text, kept: true };
          const typed = replacesTyped(comment);
          return (
            <li key={comment.step_id ?? comment.flow_id ?? comment.reference} className="summary-item" data-testid="assistant-comment">
              {several && proposed[index - 1]?.map !== comment.map && (
                <p className="field-hint" data-testid="assistant-comments-map">{t('assistant.comments.map', { name: comment.map })}</p>
              )}
              <label className="check">
                <input type="checkbox" checked={draft.kept} disabled={saved !== null} data-testid="assistant-comment-keep"
                  onChange={(event) => { change(index, { kept: event.target.checked }); }} />
                <span className="summary-map">
                  {comment.flow_id !== null ? t('assistant.comments.flow', { labels: comment.context }) : comment.context}{' '}
                  <span className="chip-code">{comment.reference}</span>
                </span>
              </label>
              <textarea rows={3} maxLength={COMMENT_MAX} value={draft.text} disabled={!draft.kept || saved !== null} aria-label={comment.context}
                data-testid="assistant-comment-text" onChange={(event) => { change(index, { text: event.target.value }); }} />
              {comment.current !== null && (
                <p className={`field-hint${typed ? ' is-warning' : ''}`} data-testid={typed ? 'assistant-comment-typed' : undefined}>
                  {t(typed ? 'assistant.comments.typed' : 'assistant.comments.current', { text: comment.current })}
                </p>
              )}
            </li>
          );
        })}
      </ol>
      {context.saveComments !== undefined && (
        <div className="reply-actions">
          {saved === null
            ? (
              <button type="button" className="reply-action" data-testid="assistant-comments-save" disabled={saving || kept.length === 0} onClick={() => void save()}>
                <Icon icon={Save} size={14} />{t('assistant.comments.save', { n: kept.length })}
              </button>
            )
            : <p className="field-hint" role="status" data-testid="assistant-comments-saved">{t('assistant.comments.saved', { n: saved })}</p>}
        </div>
      )}
    </div>
  );
}
