// OutputList.tsx
// The outputs of the sheet: the documents the step produces, who uses them and how well, the pain points their consumers raised, and the way to the sheet of a consumer to note one (annex B 4, F-118).

import { useContext, useId, useState } from 'react';
import { Flame, Plus } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import type { Schema } from '../../../api/client.ts';
import { Icon } from '../../../shared/ui/Icon.tsx';
import { consumersOf, entryOf, NEW_DOCUMENT, type DocumentEntry } from '../model/documents.ts';
import { ReportTargets, useSheet } from '../SheetContext.tsx';
import { DocumentForm } from './DocumentForm.tsx';
import { DocumentLocationButtons } from './DocumentLocationButtons.tsx';
import { DocumentButtons, DocumentIcon, DocumentName, PainPointLine, useDocumentDetails } from './DocumentParts.tsx';
import { removeDocument, saveDocument } from './documentWrites.ts';

/**
 * « utilisé par CODE (x % utilisable tel quel) », or « pas encore utilisé »; the pain points of its consumers under it
 * (B-4-79, B-4-80). A pain point is noted where it is suffered: a link by consumer the map shows opens its sheet on the
 * pain point of this document (F-118); none for a step of another map, nor for a step that holds no sheet any more
 * (R4-11), nor for a sheet the person may not fill (audit 3.2), nor while nobody uses the document. Each link is named
 * by what it shows, then by its document: two outputs the same step receives give two links told apart in a list of the
 * commands (R4-12).
 */
function OutputLine({ output, onEdit }: { output: Schema<'StepOutput'>; onEdit: (entry: DocumentEntry) => void }) {
  const { t } = useTranslation();
  const sheet = useSheet();
  const details = useDocumentDetails();
  const reportable = useContext(ReportTargets);
  const id = useId();
  const consumers = consumersOf(output, sheet.view, sheet.props.codeOf);
  const named = consumers.map((consumer) => {
    const code = consumer.code ?? t('sheet.document.another_map');
    return consumer.percent === null ? code : t('sheet.document.used_by_percent', { code, percent: consumer.percent });
  });
  const use = named.length > 0 ? t('sheet.document.used_by', { steps: named.join(', ') }) : t('sheet.document.not_used');
  return (
    <li className="output" data-testid={`output-${output.document.id}`}>
      <div className="entry-row">
        <DocumentIcon document={output.document} />
        <DocumentName document={output.document} id={`${id}-name`} />
        <span className="link-btns">
          <DocumentLocationButtons document={output.document} />
          {sheet.view.permissions.fill && (
            <DocumentButtons name={output.document.name} busy={sheet.documentBusy} testId={`output-${output.document.id}`} onEdit={() => { onEdit(entryOf(output.document)); }}
              onRemove={() => void removeDocument(sheet, output.document)} />
          )}
        </span>
      </div>
      <small className="entry-details">{details(output.document, use)}</small>
      {consumers.map((consumer) => (consumer.painPoint === null ? null : <PainPointLine key={consumer.painPoint.id} painPoint={consumer.painPoint} />))}
      {sheet.props.onReport !== undefined && consumers.filter((consumer) => consumer.code !== null && reportable(consumer.stepId)).map((consumer) => (
        <button key={consumer.stepId} type="button" id={`${id}-report-${consumer.stepId}`} aria-labelledby={`${id}-report-${consumer.stepId} ${id}-name`}
          className="link-button report-pain" data-testid={`output-report-${output.document.id}-${consumer.stepId}`}
          onClick={() => { sheet.props.onReport?.(consumer.stepId, output.document.id); }}>
          <Icon icon={Flame} size={12} />{t('sheet.document.report_pain', { code: consumer.code })}
        </button>
      ))}
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
