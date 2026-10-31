// DeletionDialogs.tsx
// The questions asked before a deletion: a lane that holds steps, a detailed step alone, a selection with detailed steps, a detail (annex B 2.3-39, 2.3-40, 2.4-18, 2.4-50, 2.4-51).

import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { Schema } from '../../../api/client.ts';
import { localizedName } from '../../../shared/i18n/localizedName.ts';
import { Button } from '../../../shared/ui/Button.tsx';
import { Confirm } from '../../../shared/ui/Confirm.tsx';
import { Dialog } from '../../../shared/ui/Dialog.tsx';
import { Field } from '../../../shared/ui/Field.tsx';
import { lanesInOrder } from '../model/readingOrder.ts';
import { hasContent, isMacro } from '../model/stepFacts.ts';
import { deleteLaneWith } from '../state/actions/lanes.ts';
import { deleteSelection } from '../state/actions/selection.ts';
import { deleteDetail, deleteMacro } from '../state/actions/steps.ts';
import { useEditor, useMapLanguage } from '../state/EditorContext.tsx';

type MapView = Schema<'MapView'>;

const laneName = (lane: Schema<'LaneView'>, language: string) =>
  localizedName({ name: lane.name, source_language: lane.source_language, name_translations: lane.translations }, language);

/** A lane that holds steps: move them to another lane, or delete them with it (B-2.4-50, B-2.4-51). */
export function LaneDialog({ map, laneId, onClose }: { map: MapView; laneId: string; onClose: () => void }) {
  const { t } = useTranslation();
  const language = useMapLanguage();
  const editor = useEditor();
  const lanes = new Map(map.lanes.map((lane) => [lane.id, lane]));
  const others = lanesInOrder(map.lanes).filter((id) => id !== laneId);
  const [to, setTo] = useState(others[0] ?? '');
  const lane = lanes.get(laneId);
  if (lane === undefined) return null;
  const finish = (steps: 'move' | 'delete') => {
    onClose();
    void deleteLaneWith(editor, laneId, steps, steps === 'move' ? to : undefined);
  };
  const footer = (
    <>
      <Button testId="lane-dialog-cancel" variant="secondary" onClick={onClose}>{t('app.cancel')}</Button>
      <Button testId="lane-dialog-delete" variant="danger" onClick={() => { finish('delete'); }}>{t('deletion.delete_steps')}</Button>
      <Button testId="lane-dialog-move" variant="primary" disabled={to === ''} onClick={() => { finish('move'); }}>{t('deletion.move')}</Button>
    </>
  );
  const count = map.steps.filter((step) => step.lane_id === laneId).length;
  return (
    <Dialog title={t('deletion.lane_title', { name: laneName(lane, language), count })} testId="lane-dialog" onClose={onClose} footer={footer}>
      <Field label={t('deletion.move_to')} testId="lane-dialog-to">
        {(control) => (
          <select {...control} value={to} onChange={(event) => { setTo(event.target.value); }}>
            {others.map((id) => {
              const other = lanes.get(id);
              return other === undefined ? null : <option key={id} value={id}>{laneName(other, language)}</option>;
            })}
          </select>
        )}
      </Field>
      <p className="dialog-note">{t('deletion.undoable')}</p>
    </Dialog>
  );
}

/** A detailed step alone: give its map to another step, or delete the map with it (B-2.3-39). */
export function MacroDialog({ map, stepId, onClose }: { map: MapView; stepId: string; onClose: () => void }) {
  const { t } = useTranslation();
  const language = useMapLanguage();
  const editor = useEditor();
  const [to, setTo] = useState('');
  const step = map.steps.find((candidate) => candidate.id === stepId);
  if (step === undefined) return null;
  const candidates = map.steps.filter((candidate) => candidate.id !== stepId && !isMacro(candidate) && hasContent(candidate.type));
  const label = (candidate: Schema<'StepView'>) => localizedName({ name: candidate.label, source_language: candidate.source_language, name_translations: candidate.translations }, language);
  const finish = (choice: 'delete' | 'reattach') => {
    onClose();
    void deleteMacro(editor, stepId, choice, choice === 'reattach' ? to : undefined);
  };
  const footer = (
    <>
      <Button testId="macro-dialog-cancel" variant="secondary" onClick={onClose}>{t('app.cancel')}</Button>
      <Button testId="macro-dialog-reattach" variant="secondary" disabled={to === ''} onClick={() => { finish('reattach'); }}>{t('deletion.macro_give')}</Button>
      <Button testId="macro-dialog-delete" variant="danger" onClick={() => { finish('delete'); }}>{t('deletion.macro_delete')}</Button>
    </>
  );
  return (
    <Dialog title={t('deletion.macro_title', { label: label(step) })} testId="macro-dialog" onClose={onClose} footer={footer}>
      <Field label={t('deletion.macro_reattach')} testId="macro-dialog-to">
        {(control) => (
          <select {...control} value={to} onChange={(event) => { setTo(event.target.value); }}>
            <option value="">{t('deletion.macro_choose')}</option>
            {candidates.map((candidate) => <option key={candidate.id} value={candidate.id}>{`${candidate.code} · ${label(candidate)}`}</option>)}
          </select>
        )}
      </Field>
      <p className="dialog-note">{t('deletion.undoable')}</p>
    </Dialog>
  );
}

/** A selection that holds detailed steps: their maps go with them (B-2.3-40). */
export function SelectionDeleteDialog({ steps, flows, onClose }: { steps: readonly string[]; flows: readonly string[]; onClose: () => void }) {
  const { t } = useTranslation();
  const editor = useEditor();
  return (
    <Confirm title={t('menu.delete')} testId="selection-delete" danger confirmLabel={t('deletion.confirm')} onCancel={onClose}
      onConfirm={() => {
        onClose();
        void deleteSelection(editor, steps, flows, true);
      }}>
      <p>{t('deletion.selection_macro')}</p>
      <p className="dialog-note">{t('deletion.undoable')}</p>
    </Confirm>
  );
}

/** The detail of a detailed step deleted with its whole descent (B-2.4-18). */
export function DetailDeleteDialog({ map, stepId, onClose }: { map: MapView; stepId: string; onClose: () => void }) {
  const { t } = useTranslation();
  const editor = useEditor();
  const step = map.steps.find((candidate) => candidate.id === stepId);
  if (step === undefined) return null;
  return (
    <Confirm title={t('menu.delete_detail')} testId="detail-delete" danger confirmLabel={t('deletion.confirm')} onCancel={onClose}
      onConfirm={() => {
        onClose();
        void deleteDetail(editor, stepId);
      }}>
      <p>{t('deletion.detail_confirm', { label: step.label })}</p>
      <p className="dialog-note">{t('deletion.undoable')}</p>
    </Confirm>
  );
}
