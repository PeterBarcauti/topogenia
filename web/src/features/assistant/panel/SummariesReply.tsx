// SummariesReply.tsx
// The summaries written by the model, to review before they are kept (plan 3.1, lot R): one sentence per map, kept, corrected or dropped; the ones kept saved at once, with the model that wrote them; saved, the card shown again says so (audit 3.1 V2, 5.3).

import { useState } from 'react';
import { Save } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Icon } from '../../../shared/ui/Icon.tsx';
import type { AssistantReply } from './assistantStore.ts';
import type { SummaryKept, ThreadContext } from './threadContext.ts';

/** The longest summary, as the host takes it. */
const SUMMARY_MAX = 200;

interface Draft {
  readonly text: string;
  readonly kept: boolean;
}

export function SummariesReply({ reply, context, turnId }: { reply: AssistantReply; context: ThreadContext; turnId: number }) {
  const { t } = useTranslation();
  const summaries = reply.summaries ?? [];
  const [drafts, setDrafts] = useState<readonly Draft[]>(() => summaries.map((summary) => ({ text: summary.text, kept: true })));
  // Saved before the panel was closed: the card shown again says so, and offers nothing more (audit 3.1 V2, 5.3).
  const [saved, setSaved] = useState<number | null>(() => context.store.getState().summariesSaved[turnId] ?? null);
  const [saving, setSaving] = useState(false);
  if (summaries.length === 0) return null;
  const model = reply.cost?.model ?? context.status?.destination.model ?? null;
  const change = (index: number, draft: Partial<Draft>) => {
    setDrafts((before) => before.map((current, at) => (at === index ? { ...current, ...draft } : current)));
  };
  const kept: SummaryKept[] = summaries.flatMap((summary, index) => {
    const draft = drafts[index];
    return draft === undefined || !draft.kept || draft.text.trim() === '' ? [] : [{ mapId: summary.map_id, text: draft.text.trim(), version: summary.version }];
  });
  const save = async () => {
    if (context.saveSummaries === undefined) return;
    setSaving(true);
    const done = await context.saveSummaries(kept, model);
    setSaving(false);
    if (!done) return;
    context.store.getState().keepSummaries(turnId, kept.length);
    setSaved(kept.length);
  };
  return (
    <div className="reply-summaries" data-testid="assistant-summaries">
      <p className="field-hint">{t('assistant.summaries.intro')}</p>
      <ol className="summary-list">
        {summaries.map((summary, index) => {
          const draft = drafts[index] ?? { text: summary.text, kept: true };
          return (
            <li key={summary.map_id} className="summary-item" data-testid="assistant-summary">
              <label className="check">
                <input type="checkbox" checked={draft.kept} disabled={saved !== null} data-testid="assistant-summary-keep"
                  onChange={(event) => { change(index, { kept: event.target.checked }); }} />
                <span className="summary-map">{summary.name} <span className="chip-code">{summary.reference}</span></span>
              </label>
              <textarea rows={2} maxLength={SUMMARY_MAX} value={draft.text} disabled={!draft.kept || saved !== null} aria-label={summary.name}
                data-testid="assistant-summary-text" onChange={(event) => { change(index, { text: event.target.value }); }} />
              {summary.current !== null && (
                <p className="field-hint">{t('assistant.summaries.current', { text: summary.current })}</p>
              )}
            </li>
          );
        })}
      </ol>
      {context.saveSummaries !== undefined && (
        <div className="reply-actions">
          {saved === null
            ? (
              <button type="button" className="reply-action" data-testid="assistant-summaries-save" disabled={saving || kept.length === 0} onClick={() => void save()}>
                <Icon icon={Save} size={14} />{t('assistant.summaries.save', { n: kept.length })}
              </button>
            )
            : <p className="field-hint" role="status" data-testid="assistant-summaries-saved">{t('assistant.summaries.saved', { n: saved })}</p>}
        </div>
      )}
    </div>
  );
}
