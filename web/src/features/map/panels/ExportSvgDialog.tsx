// ExportSvgDialog.tsx
// Exports the map shown as an image SVG (plan 3.1, L7): the copy of the map inside it on option, unticked by default (as draw.io offers it), never for a project « Strictement confidentiel », whose classification is read when the dialog opens.

import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { api } from '../../../api/client.ts';
import { PRODUCT_NAME } from '../../../shared/i18n/i18n.ts';
import { Button } from '../../../shared/ui/Button.tsx';
import { Dialog } from '../../../shared/ui/Dialog.tsx';
import { exportMap } from '../state/actions/exports.ts';
import { useEditor } from '../state/EditorContext.tsx';

export interface ExportSvgDialogProps {
  readonly projectId: string;
  readonly onClose: () => void;
}

/** The classification of the project, read once: null while it is read, or when it could not be. */
function useClassification(projectId: string): string | null {
  const [classification, setClassification] = useState<string | null>(null);
  useEffect(() => {
    let live = true;
    api.get('/api/projects/{id}', { path: { id: projectId } }).then((project) => {
      if (live) setClassification(project.classification);
    }, () => undefined);
    return () => {
      live = false;
    };
  }, [projectId]);
  return classification;
}

export function ExportSvgDialog({ projectId, onClose }: ExportSvgDialogProps) {
  const { t } = useTranslation();
  const editor = useEditor();
  const classification = useClassification(projectId);
  const [editable, setEditable] = useState(false);
  const refused = classification === 'strictly_confidential';
  const offered = classification !== null && !refused;

  const submit = () => {
    onClose();
    void exportMap(editor, 'svg', { editable: offered && editable });
  };

  const footer = (
    <>
      <Button testId="export-svg-cancel" variant="secondary" onClick={onClose}>{t('app.cancel')}</Button>
      <Button testId="export-svg-confirm" variant="primary" onClick={submit}>{t('export.svg_button')}</Button>
    </>
  );
  return (
    <Dialog title={t('export.svg_title')} testId="export-svg" onClose={onClose} footer={footer}>
      <p className="muted">{t('export.svg_help')}</p>
      <label className="check">
        <input type="checkbox" checked={offered && editable} disabled={!offered} data-testid="export-svg-editable" aria-describedby="export-svg-editable-help"
          onChange={(event) => { setEditable(event.target.checked); }} />
        <span>{t('export.svg_editable')}</span>
      </label>
      <p id="export-svg-editable-help" className="field-hint">{refused ? t('export.svg_editable_refused') : t('export.svg_editable_help', { name: PRODUCT_NAME })}</p>
    </Dialog>
  );
}
