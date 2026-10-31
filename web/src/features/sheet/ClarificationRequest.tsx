// ClarificationRequest.tsx
// The facilitator asks the author a clarification on a validated sheet: a question of 1000 characters at most, then the sheet goes « à revoir » (annex B 4).

import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button } from '../../shared/ui/Button.tsx';
import { useSheet } from './SheetContext.tsx';
import { askClarification } from './state/sheetActions.ts';

export function ClarificationRequest() {
  const { t } = useTranslation();
  const sheet = useSheet();
  const [text, setText] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  if (!sheet.view.permissions.request_clarification || sheet.view.status !== 'validated') return null;
  if (text === null) {
    return <Button testId="sheet-ask-clarification" variant="secondary" className="sheet-ask" onClick={() => { setText(''); }}>{t('sheet.ask_clarification')}</Button>;
  }
  const send = async () => {
    setBusy(true);
    if (await askClarification(sheet, text)) {
      setText(null);
      sheet.props.notify(t('sheet.clarification_sent'), 'success');
    }
    setBusy(false);
  };
  return (
    <div className="sheet-ask-form" data-testid="sheet-clarification-form">
      <div className="field">
        <label htmlFor="sheet-clarification-text" className="field-label">{t('sheet.clarification_prompt')}</label>
        <textarea id="sheet-clarification-text" className="field-control" rows={3} maxLength={1000} value={text} autoFocus data-testid="sheet-clarification-text"
          onChange={(event) => { setText(event.target.value); }} />
      </div>
      <div className="sheet-actions">
        <Button testId="sheet-clarification-cancel" variant="secondary" onClick={() => { setText(null); }}>{t('app.cancel')}</Button>
        <Button testId="sheet-clarification-send" variant="primary" disabled={text.trim() === '' || busy} onClick={() => void send()}>{t('sheet.clarification_send')}</Button>
      </div>
    </div>
  );
}
