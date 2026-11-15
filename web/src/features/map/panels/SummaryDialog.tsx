// SummaryDialog.tsx
// The panel of the summary of the map shown (plan 3.1, lot R): its sentence in the language shown, who wrote it and when, whether the map changed since; written by hand, erased, asked again of the AI; the bubble at the opening turned on or off.

import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useStore } from 'zustand';
import type { Schema } from '../../../api/client.ts';
import { summaryText } from '../../../app/SummaryBadge.tsx';
import { formatDate } from '../../../shared/i18n/formatDate.ts';
import { Button } from '../../../shared/ui/Button.tsx';
import { Dialog } from '../../../shared/ui/Dialog.tsx';
import { askSummaries, SUMMARY_MAX, writeSummary } from '../state/actions/summaries.ts';
import { useEditor, useMapState } from '../state/EditorContext.tsx';

export interface SummaryDialogProps {
  readonly map: Schema<'MapView'>;
  readonly onClose: () => void;
}

/** Who wrote the summary and when, and whether the map changed since. */
function Written({ summary, language }: { summary: Schema<'MapSummaryView'>; language: string }) {
  const { t } = useTranslation();
  const date = formatDate(summary.written_at, language);
  return (
    <>
      <p className="field-hint" data-testid="summary-written">
        {summary.origin === 'ai' ? t('summary.by_ai', { date }) : t('summary.by_hand', { date })}
        {summary.origin === 'ai' && summary.model !== null && <span className="summary-model"> · {summary.model}</span>}
      </p>
      {summary.stale && <p className="field-hint field-hint--warning" data-testid="summary-stale">{t('summary.stale_help')}</p>}
    </>
  );
}

export function SummaryDialog({ map, onClose }: SummaryDialogProps) {
  const { t, i18n } = useTranslation();
  const editor = useEditor();
  const language = useMapState((state) => state.shown?.language) ?? map.languages.origin;
  const showOnOpen = useStore(editor.preferences, (state) => state.showSummaryOnOpen);
  const summary = map.summary ?? null;
  const canWrite = map.rights.structure;
  const [typed, setDraft] = useState<string | null>(null);
  // A map without summary opens on the field to write it, for who may write it; after an erasure as well.
  const draft = typed ?? (summary === null && canWrite ? '' : null);
  const [saving, setSaving] = useState(false);

  const save = async (text: string) => {
    setSaving(true);
    const written = await writeSummary(editor, text);
    setSaving(false);
    if (written) setDraft(null);
  };
  const askAgain = () => {
    onClose();
    void askSummaries(editor, [map.id]);
  };

  const footer = (
    <>
      {canWrite && <Button testId="summary-ask" variant="secondary" onClick={askAgain}>{summary === null ? t('summary.ask') : t('summary.ask_again')}</Button>}
      {canWrite && summary !== null && draft === null && (
        <>
          <Button testId="summary-erase" variant="secondary" disabled={saving} onClick={() => void save('')}>{t('summary.erase')}</Button>
          <Button testId="summary-edit" variant="secondary" onClick={() => { setDraft(summary.text); }}>{t('summary.edit')}</Button>
        </>
      )}
      {draft !== null && (
        <Button testId="summary-save" variant="primary" disabled={saving || draft.trim() === '' || draft.length > SUMMARY_MAX} onClick={() => void save(draft)}>
          {t('summary.save')}
        </Button>
      )}
      <Button testId="summary-done" variant={draft === null ? 'primary' : 'secondary'} onClick={onClose}>{t('app.close')}</Button>
    </>
  );
  return (
    <Dialog title={t('summary.title')} testId="summary" onClose={onClose} footer={footer}>
      {summary !== null && draft === null && (
        <>
          <p className="summary-text" lang={summary.translations[language] === undefined ? summary.source_language : language} data-testid="summary-text">
            {summaryText(summary, language)}
          </p>
          <Written summary={summary} language={i18n.language} />
        </>
      )}
      {summary === null && draft === null && <p className="muted" data-testid="summary-none">{t('summary.none')}</p>}
      {draft !== null && (
        <label className="field">
          <span>{t('summary.sentence')}</span>
          <textarea data-testid="summary-draft" rows={3} maxLength={SUMMARY_MAX} value={draft} onChange={(event) => { setDraft(event.target.value); }} />
          <span className="field-hint">{t('summary.count', { n: draft.length, max: SUMMARY_MAX })}</span>
        </label>
      )}
      <label className="check">
        <input type="checkbox" checked={showOnOpen} data-testid="summary-on-open"
          onChange={(event) => { editor.preferences.getState().set('showSummaryOnOpen', event.target.checked); }} />
        <span>{t('summary.on_open')}</span>
      </label>
    </Dialog>
  );
}
