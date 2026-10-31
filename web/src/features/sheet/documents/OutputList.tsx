// OutputList.tsx
// The outputs of the sheet: the documents the step produces, who uses them and how well, the pain points their consumers raised (annex B 4).

import { useState } from 'react';
import { FileText, Plus } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import type { Schema } from '../../../api/client.ts';
import { Icon } from '../../../shared/ui/Icon.tsx';
import { consumersOf, entryOf, NEW_DOCUMENT, type DocumentEntry } from '../model/documents.ts';
import { useSheet } from '../SheetContext.tsx';
import { DocumentForm } from './DocumentForm.tsx';
import { DocumentButtons, DocumentName, PainPointLine, useDocumentDetails } from './DocumentParts.tsx';
import { removeDocument, saveDocument } from './documentWrites.ts';

/** « utilisé par CODE (x % utilisable tel quel) », or « pas encore utilisé »; the pain points of its consumers under it (B-4-79, B-4-80). */
function OutputLine({ output, onEdit }: { output: Schema<'StepOutput'>; onEdit: (entry: DocumentEntry) => void }) {
  const { t } = useTranslation();
  const sheet = useSheet();
  const details = useDocumentDetails();
  const consumers = consumersOf(output, sheet.view, sheet.props.codeOf);
  const named = consumers.map((consumer) => {
    const code = consumer.code ?? t('sheet.document.another_map');
    return consumer.percent === null ? code : t('sheet.document.used_by_percent', { code, percent: consumer.percent });
  });
  const use = named.length > 0 ? t('sheet.document.used_by', { steps: named.join(', ') }) : t('sheet.document.not_used');
  return (
    <li className="output" data-testid={`output-${output.document.id}`}>
      <div className="entry-row">
        <Icon icon={FileText} size={14} />
        <DocumentName document={output.document} />
        {sheet.view.permissions.fill && (
          <DocumentButtons name={output.document.name} busy={sheet.documentBusy} testId={`output-${output.document.id}`} onEdit={() => { onEdit(entryOf(output.document)); }}
            onRemove={() => void removeDocument(sheet, output.document)} />
        )}
      </div>
      <small className="entry-details">{details(output.document, use)}</small>
      {consumers.map((consumer) => (consumer.painPoint === null ? null : <PainPointLine key={consumer.painPoint.id} painPoint={consumer.painPoint} />))}
    </li>
  );
}

export function OutputList() {
  const { t } = useTranslation();
  const sheet = useSheet();
  const [entry, setEntry] = useState<DocumentEntry | null>(null);
  const outputs = sheet.view.outputs;
  const save = async () => {
    if (entry === null || entry.name.trim() === '') return;
    if (await saveDocument(sheet, 'output', entry)) setEntry(null);
  };
  return (
    <div className="documents" data-testid="sheet-outputs">
      {outputs.length === 0 && entry === null && <p className="field-hint">{t('sheet.document.no_output')}</p>}
      {outputs.length > 0 && <ul className="documents-list">{outputs.map((output) => <OutputLine key={output.document.id} output={output} onEdit={setEntry} />)}</ul>}
      {entry !== null && (
        <DocumentForm direction="output" entry={entry} busy={sheet.documentBusy} onChange={setEntry} onCancel={() => { setEntry(null); }} onSave={() => void save()} />
      )}
      {entry === null && sheet.view.permissions.fill && (
        <button type="button" className="add-line" data-testid="output-add" onClick={() => { setEntry(NEW_DOCUMENT); }}>
          <Icon icon={Plus} size={14} />{t('sheet.document.add_output')}
        </button>
      )}
    </div>
  );
}
