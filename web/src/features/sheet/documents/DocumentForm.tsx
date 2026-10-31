// DocumentForm.tsx
// The form of a document the step declares, an output or an input from outside: its name, its link, its wait for an input, its sharing with the group of the project (annex B 4).

import { useId, type KeyboardEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { Button } from '../../../shared/ui/Button.tsx';
import type { DocumentEntry, Direction } from '../model/documents.ts';
import { useSheet } from '../SheetContext.tsx';

export interface DocumentFormProps {
  readonly direction: Direction;
  readonly entry: DocumentEntry;
  readonly busy: boolean;
  readonly onChange: (entry: DocumentEntry) => void;
  readonly onCancel: () => void;
  readonly onSave: () => void;
}

/**
 * Name of 200 characters, link of 1000, wait before receipt in hours from 0 to 100 000 by 0.5 for an input, the box of
 * the sharing named by the setting of the project (B-4-66 to B-4-69, B-4-78); Escape cancels it before closing the panel.
 */
export function DocumentForm({ direction, entry, busy, onChange, onCancel, onSave }: DocumentFormProps) {
  const { t } = useTranslation();
  const { props } = useSheet();
  const id = useId();
  const set = (part: Partial<DocumentEntry>) => { onChange({ ...entry, ...part }); };
  const group = props.project?.sharingGroupLabel ?? null;
  const title = entry.id !== null ? t('sheet.document.edit_title', { name: entry.name }) : t(direction === 'input' ? 'sheet.document.new_external' : 'sheet.document.new_output');
  const onKeyDown = (event: KeyboardEvent<HTMLFieldSetElement>) => {
    if (event.key !== 'Escape') return;
    event.stopPropagation();
    onCancel();
  };
  return (
    <fieldset className="document-form" data-testid={`document-form-${direction}`} onKeyDown={onKeyDown}>
      <legend>{title}</legend>
      <div className="field">
        <label htmlFor={`${id}-name`} className="field-label">{t('sheet.document.name')}</label>
        <input id={`${id}-name`} className="field-control" type="text" maxLength={200} autoFocus placeholder={t('sheet.document.name_example')} value={entry.name}
          data-testid="document-form-name" onChange={(event) => { set({ name: event.target.value }); }}
          onKeyDown={(event) => {
            if (event.key !== 'Enter') return;
            event.preventDefault();
            onSave();
          }} />
      </div>
      <div className="field">
        <label htmlFor={`${id}-location`} className="field-label">{t('sheet.document.location')}</label>
        <input id={`${id}-location`} className="field-control" type="text" maxLength={1000} value={entry.location} aria-describedby={`${id}-location-help`}
          data-testid="document-form-location" onChange={(event) => { set({ location: event.target.value }); }} />
        <span id={`${id}-location-help`} className="field-hint">{t('sheet.document.location_help')}</span>
      </div>
      {direction === 'input' && (
        <div className="field">
          <label htmlFor={`${id}-wait`} className="field-label">{t('sheet.document.wait')}</label>
          <input id={`${id}-wait`} className="field-control" type="number" min={0} max={100000} step={0.5} value={entry.wait} data-testid="document-form-wait"
            onChange={(event) => { set({ wait: event.target.value }); }} />
        </div>
      )}
      <label className="check">
        <input type="checkbox" checked={entry.shared} data-testid="document-form-shared" onChange={(event) => { set({ shared: event.target.checked }); }} />
        {group === null ? t('sheet.document.shared_default') : t('sheet.document.shared', { group })}
      </label>
      <div className="sheet-actions">
        <Button testId="document-form-cancel" variant="secondary" onClick={onCancel}>{t('app.cancel')}</Button>
        <Button testId="document-form-save" variant="primary" disabled={entry.name.trim() === '' || busy} onClick={onSave}>{t('sheet.document.save')}</Button>
      </div>
    </fieldset>
  );
}
