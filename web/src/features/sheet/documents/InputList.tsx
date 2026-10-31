// InputList.tsx
// The inputs of the sheet: the outputs of the steps linked by an incoming flow offered unticked, those ticked from elsewhere in their colour, the inputs from outside the map (annex B 4).

import { useState } from 'react';
import { Plus } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import type { Schema } from '../../../api/client.ts';
import { Icon } from '../../../shared/ui/Icon.tsx';
import { currentUse, entryOf, inputGroups, NEW_DOCUMENT, painPointOf, type DocumentEntry } from '../model/documents.ts';
import { useSheet } from '../SheetContext.tsx';
import { DocumentForm } from './DocumentForm.tsx';
import { DocumentButtons, DocumentName, PainPointLine, useDocumentDetails } from './DocumentParts.tsx';
import { removeDocument, saveDocument, tickDocument } from './documentWrites.ts';
import { OtherDocumentsDialog } from './OtherDocumentsDialog.tsx';
import { UseEditor } from './UseEditor.tsx';

type ProposedInput = Schema<'ProposedInput'>;

/**
 * An input: ticked to use it (B-4-63), its producer by its code, « d'ailleurs » for one ticked from elsewhere (B-4-65);
 * an input from outside, changed or removed by its buttons (B-4-70); in reading, its wait, its %C&A and its pain point (B-4-77).
 */
function InputLine({ input, onEdit }: { input: ProposedInput; onEdit: (entry: DocumentEntry) => void }) {
  const { t } = useTranslation();
  const sheet = useSheet();
  const details = useDocumentDetails();
  const fill = sheet.view.permissions.fill;
  const painPoint = painPointOf(sheet.view, input.use?.pain_point_id ?? null);
  const producer = input.producer;
  const wait = producer === null ? input.document.wait_hours : input.use?.wait_hours ?? null;
  const percent = input.use?.percent_complete_accurate ?? null;
  const classes = ['entry', producer !== null && !input.near ? 'entry--other' : '', producer !== null && input.used ? 'entry--used' : ''].filter((name) => name !== '').join(' ');
  return (
    <li className={classes} data-testid={`input-${input.document.id}`}>
      <div className="entry-row">
        {producer !== null && fill ? (
          <label className="entry-check">
            <input type="checkbox" checked={input.used} disabled={sheet.documentBusy} aria-label={t('sheet.document.use', { name: input.document.name })}
              data-testid={`input-tick-${input.document.id}`} onChange={(event) => void tickDocument(sheet, input.document.id, event.target.checked, currentUse(input, painPoint))} />
            <DocumentName document={input.document} />
          </label>
        ) : <DocumentName document={input.document} />}
        {producer !== null && <span className="from" title={producer.label}>{input.near ? producer.code : t('sheet.document.from_elsewhere', { code: producer.code })}</span>}
        {producer === null && fill && (
          <DocumentButtons name={input.document.name} busy={sheet.documentBusy} testId={`input-${input.document.id}`} onEdit={() => { onEdit(entryOf(input.document)); }}
            onRemove={() => void removeDocument(sheet, input.document)} />
        )}
      </div>
      <small className="entry-details">
        {details(input.document, producer === null ? t('sheet.document.external') : null, wait !== null && input.used ? t('sheet.document.wait_short', { hours: wait }) : null,
          percent !== null && input.used && !fill ? t('sheet.document.percent_short', { percent }) : null)}
      </small>
      {painPoint !== null && !fill && <PainPointLine painPoint={painPoint} />}
      {producer !== null && input.used && fill && <UseEditor input={input} painPoint={painPoint} />}
    </li>
  );
}

export function InputList() {
  const { t } = useTranslation();
  const sheet = useSheet();
  const [entry, setEntry] = useState<DocumentEntry | null>(null);
  const [other, setOther] = useState(false);
  const fill = sheet.view.permissions.fill;
  const inputs = inputGroups(sheet.view.inputs, fill);
  const save = async () => {
    if (entry === null || entry.name.trim() === '') return;
    if (await saveDocument(sheet, 'input', entry)) setEntry(null);
  };
  return (
    <div className="documents" data-testid="sheet-inputs">
      {inputs.length === 0 && entry === null && <p className="field-hint">{t(fill ? 'sheet.document.no_input_offered' : 'sheet.document.no_input')}</p>}
      {inputs.length > 0 && <ul className="documents-list">{inputs.map((input) => <InputLine key={input.document.id} input={input} onEdit={setEntry} />)}</ul>}
      {entry !== null && (
        <DocumentForm direction="input" entry={entry} busy={sheet.documentBusy} onChange={setEntry} onCancel={() => { setEntry(null); }} onSave={() => void save()} />
      )}
      {entry === null && fill && (
        <div className="documents-actions">
          <button type="button" className="add-line" data-testid="input-other" onClick={() => { setOther(true); }}>{t('sheet.document.other')}</button>
          <button type="button" className="add-line" data-testid="input-add-external" onClick={() => { setEntry(NEW_DOCUMENT); }}>
            <Icon icon={Plus} size={14} />{t('sheet.document.add_external')}
          </button>
        </div>
      )}
      {other && <OtherDocumentsDialog onClose={() => { setOther(false); }} />}
    </div>
  );
}
