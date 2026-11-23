// InsertPmapDialog.tsx
// Inserts a .pmap file as a new detailed step of the map shown, or replaces the detail of a step by one (3.3, F-208): the file chosen, dropped or pasted, what it holds before anything is written, the place and the label of the step, the sheets on option; one operation, undone by one Ctrl+Z.

import { useTranslation } from 'react-i18next';
import type { Schema } from '../../../api/client.ts';
import { Button } from '../../../shared/ui/Button.tsx';
import { Dialog } from '../../../shared/ui/Dialog.tsx';
import { Field } from '../../../shared/ui/Field.tsx';
import type { GridCell } from '../model/geometry/grid.ts';
import { useEditor, useMapLanguage } from '../state/EditorContext.tsx';
import { useInsertForm } from './insertPmapForm.ts';
import { FileChoice, InsertionErrorBox, PlaceFields, PreviewSummary, stepName } from './InsertPmapParts.tsx';

export interface InsertPmapDialogProps {
  readonly map: Schema<'MapView'>;
  /** The step whose detail the file replaces; none for a new step. */
  readonly stepId?: string;
  /** The cell the menu of the map was opened at: the new step goes there. */
  readonly cell?: GridCell;
  /** The flow of the menu of a flow: the new step goes on it. */
  readonly flowId?: string;
  readonly onClose: () => void;
}

export function InsertPmapDialog({ map, stepId, cell, flowId, onClose }: InsertPmapDialogProps) {
  const { t } = useTranslation();
  const editor = useEditor();
  const language = useMapLanguage();
  const form = useInsertForm(editor, map, { stepId, cell, flowId }, onClose);
  const replaced = stepId === undefined ? undefined : map.steps.find((step) => step.id === stepId);
  const footer = (
    <>
      <Button testId="insert-pmap-cancel" variant="secondary" onClick={onClose}>{t('app.cancel')}</Button>
      <Button testId="insert-pmap-confirm" variant="primary" disabled={!form.ready} onClick={() => void form.submit()}>
        {t(stepId === undefined ? 'files.insert_button' : 'files.replace_button')}
      </Button>
    </>
  );
  return (
    <Dialog title={t(stepId === undefined ? 'files.insert_title' : 'files.replace_title')} testId="insert-pmap" onClose={onClose} footer={footer} wide>
      <p className="muted">{replaced === undefined ? t('files.insert_help') : t('files.replace_help', { label: stepName(replaced, language) })}</p>
      <FileChoice content={form.content} fileName={form.fileName} onText={(text) => { form.changeContent(text, null); }} onFile={(file) => void form.readFile(file)} />
      {form.preview !== null && <PreviewSummary preview={form.preview} />}
      {form.preview !== null && stepId === undefined && (
        <>
          <Field label={t('files.insert_label')} testId="insert-pmap-label">
            {(control) => <input {...control} value={form.label} maxLength={150} onChange={(event) => { form.setLabel(event.target.value); }} />}
          </Field>
          {cell === undefined && <PlaceFields map={map} language={language} place={form.place} offeredFlow={form.offeredFlow} onChange={form.setPlace} />}
        </>
      )}
      {form.preview !== null && (
        <>
          <label className="check">
            <input type="checkbox" checked={form.withSheets} data-testid="insert-pmap-sheets" aria-describedby="insert-pmap-sheets-help"
              onChange={(event) => { form.setWithSheets(event.target.checked); }} />
            <span>{t('files.insert_sheets')}</span>
          </label>
          <p id="insert-pmap-sheets-help" className="field-hint">{t('files.insert_sheets_help')}</p>
        </>
      )}
      {form.error !== null && <InsertionErrorBox error={form.error} />}
    </Dialog>
  );
}
