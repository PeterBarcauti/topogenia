// ExportBpmnDialog.tsx
// Exports the map shown in BPMN 2.0 (ADR 106): its detailed steps as sub-processes carried, ticked when the map has one, and its documents and pain points drawn as in the view; the documentation of the file says everything whatever is drawn.

import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useStore } from 'zustand';
import type { Schema } from '../../../../api/client.ts';
import { Button } from '../../../../shared/ui/Button.tsx';
import { Dialog } from '../../../../shared/ui/Dialog.tsx';
import { useEditor } from '../../state/EditorContext.tsx';
import { exportBpmn } from './exportBpmn.ts';

export interface ExportBpmnDialogProps {
  readonly map: Schema<'MapView'>;
  readonly onClose: () => void;
}

export function ExportBpmnDialog({ map, onClose }: ExportBpmnDialogProps) {
  const { t } = useTranslation();
  const editor = useEditor();
  const detailed = map.steps.some((step) => step.sub_map_id !== null);
  const viewDetails = useStore(editor.preferences, (state) => state.showBpmnDetails);
  const [withSubProcesses, setWithSubProcesses] = useState(detailed);
  const [withDetails, setWithDetails] = useState(viewDetails);

  const submit = () => {
    onClose();
    void exportBpmn(editor, { withSubProcesses: detailed && withSubProcesses, withDetails });
  };

  const footer = (
    <>
      <Button testId="export-bpmn-cancel" variant="secondary" onClick={onClose}>{t('app.cancel')}</Button>
      <Button testId="export-bpmn-confirm" variant="primary" onClick={submit}>{t('bpmn.export_button')}</Button>
    </>
  );
  return (
    <Dialog title={t('bpmn.export_title')} testId="export-bpmn" onClose={onClose} footer={footer}>
      <p className="muted">{t('bpmn.export_help')}</p>
      <label className="check">
        <input type="checkbox" checked={detailed && withSubProcesses} disabled={!detailed} data-testid="export-bpmn-subprocesses" aria-describedby="export-bpmn-subprocesses-help"
          onChange={(event) => { setWithSubProcesses(event.target.checked); }} />
        <span>{t('bpmn.export_subprocesses')}</span>
      </label>
      <p id="export-bpmn-subprocesses-help" className="field-hint">{detailed ? t('bpmn.export_subprocesses_help') : t('bpmn.export_subprocesses_none')}</p>
      <label className="check">
        <input type="checkbox" checked={withDetails} data-testid="export-bpmn-details" aria-describedby="export-bpmn-details-help"
          onChange={(event) => { setWithDetails(event.target.checked); }} />
        <span>{t('bpmn.export_details')}</span>
      </label>
      <p id="export-bpmn-details-help" className="field-hint">{t('bpmn.export_details_help')}</p>
    </Dialog>
  );
}
