// OtherDocumentsDialog.tsx
// « Autre… »: the outputs of the other steps of the map in a table, to tick the ones the step uses; they show in its inputs as coming from elsewhere (annex B 4).

import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { api, type Schema } from '../../../api/client.ts';
import { messageOf } from '../../../shared/i18n/errorMessage.ts';
import { Button } from '../../../shared/ui/Button.tsx';
import { Dialog } from '../../../shared/ui/Dialog.tsx';
import { useSheet } from '../SheetContext.tsx';
import { DocumentName } from './DocumentParts.tsx';
import { NO_USE, tickDocument } from './documentWrites.ts';

type AvailableOutputs = Schema<'AvailableOutputs'>;

/** The outputs offered, read once at the opening; a refusal is said and leaves the table empty. */
function useAvailable(): [AvailableOutputs[] | null, (documentId: string, used: boolean) => void] {
  const { props } = useSheet();
  const [groups, setGroups] = useState<AvailableOutputs[] | null>(null);
  const { step, notify } = props;
  useEffect(() => {
    let alive = true;
    api.get('/api/steps/{id}/documents/available', { path: { id: step.id } }).then((read) => {
      if (alive) setGroups(read);
    }, (failure: unknown) => {
      if (!alive) return;
      notify(messageOf(failure), 'error');
      setGroups([]);
    });
    return () => {
      alive = false;
    };
  }, [step.id, notify]);
  const mark = (documentId: string, used: boolean) => {
    setGroups((current) => current?.map((group) => ({
      ...group, documents: group.documents.map((offered) => (offered.document.id === documentId ? { ...offered, used } : offered)),
    })) ?? null);
  };
  return [groups, mark];
}

/** A tick uses the document with an empty use; an untick stops using it (B-4-64). */
export function OtherDocumentsDialog({ onClose }: { onClose: () => void }) {
  const { t } = useTranslation();
  const sheet = useSheet();
  const [groups, mark] = useAvailable();
  const tick = async (documentId: string, used: boolean) => {
    if (await tickDocument(sheet, documentId, used, NO_USE)) mark(documentId, used);
  };
  const rows = (groups ?? []).filter((group) => group.documents.length > 0);
  return (
    <Dialog title={t('sheet.document.other_title')} testId="other-documents" wide onClose={onClose}
      footer={<Button testId="other-documents-done" variant="primary" onClick={onClose}>{t('app.close')}</Button>}>
      <p className="field-hint">{t('sheet.document.other_help')}</p>
      {groups === null && <p className="field-hint">{t('app.loading')}</p>}
      {groups !== null && rows.length === 0 && <p className="sheet-empty" data-testid="other-documents-empty">{t('sheet.document.other_empty')}</p>}
      {rows.length > 0 && (
        <div className="table-scroll">
          <table className="documents-table" data-testid="other-documents-table">
            <thead><tr><th>{t('sheet.document.column_step')}</th><th>{t('sheet.document.column_document')}</th><th>{t('sheet.document.column_used')}</th></tr></thead>
            <tbody>
              {rows.flatMap((group) => group.documents.map((offered, index) => (
                <tr key={offered.document.id}>
                  {index === 0 && <td rowSpan={group.documents.length} className="code">{group.producer.code} · {group.producer.label}</td>}
                  <td><DocumentName document={offered.document} /></td>
                  <td className="cell-check">
                    <input type="checkbox" checked={offered.used} disabled={sheet.documentBusy} aria-label={t('sheet.document.use', { name: offered.document.name })}
                      data-testid={`other-tick-${offered.document.id}`} onChange={(event) => void tick(offered.document.id, event.target.checked)} />
                  </td>
                </tr>
              )))}
            </tbody>
          </table>
        </div>
      )}
    </Dialog>
  );
}
