// ClarificationNote.tsx
// The question the facilitator asked the author, in the warning colours of the head of the sheet; the facilitator rewrites it in place or removes it (annex B 4, B-4-12, spec 3.0 section 16).

import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button } from '../../shared/ui/Button.tsx';
import { useSheet } from './SheetContext.tsx';
import { changeClarification, withdrawClarification } from './state/sheetActions.ts';

export function ClarificationNote({ text }: { text: string }) {
  const { t } = useTranslation();
  const sheet = useSheet();
  const [draft, setDraft] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const facilitator = sheet.view.permissions.request_clarification;
  /** A write of the question, then the field closed and its notification; a refusal is said by the write. */
  const run = async (write: () => Promise<boolean>, done: string) => {
    setBusy(true);
    if (await write()) {
      setDraft(null);
      sheet.props.notify(t(done), 'success');
    }
    setBusy(false);
  };
  return (
    <div className="sheet-clarification" role="note" data-testid="sheet-clarification">
      <div className="sheet-clarification-head">
        <strong>{t('sheet.clarification_title')}</strong>
        {facilitator && draft === null && (
          <span className="sheet-clarification-actions">
            <button type="button" className="sheet-clarification-action" data-testid="sheet-clarification-edit" onClick={() => { setDraft(text); }}>
              {t('sheet.clarification_edit')}
            </button>
            <button type="button" className="sheet-clarification-action" data-testid="sheet-clarification-withdraw" disabled={busy}
              onClick={() => void run(() => withdrawClarification(sheet), 'sheet.clarification_withdrawn')}>
              {t('sheet.clarification_withdraw')}
            </button>
          </span>
        )}
      </div>
      {draft === null ? <p>{text}</p> : (
        <div className="sheet-clarification-form">
          {/* Escape gives the field up and leaves the sheet open: the panel closes on an Escape that reaches it. */}
          <textarea className="field-control" rows={3} maxLength={1000} value={draft} autoFocus aria-label={t('sheet.clarification_prompt')}
            data-testid="sheet-clarification-field" onChange={(event) => { setDraft(event.target.value); }}
            onKeyDown={(event) => {
              if (event.key !== 'Escape') return;
              event.stopPropagation();
              setDraft(null);
            }} />
          <div className="sheet-actions">
            <Button testId="sheet-clarification-edit-cancel" variant="secondary" onClick={() => { setDraft(null); }}>{t('app.cancel')}</Button>
            <Button testId="sheet-clarification-save" variant="primary" disabled={draft.trim() === '' || busy}
              onClick={() => void run(() => changeClarification(sheet, draft), 'sheet.clarification_changed')}>
              {t('sheet.clarification_save')}
            </Button>
          </div>
        </div>
      )}
    </div>
  );
}
