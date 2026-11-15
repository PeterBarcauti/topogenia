// ProjectPropertiesDialog.tsx
// The properties of a project in one place: its classification, the label of its sharing group and the work time of its value stream, changed by a facilitator, read by any other member (spec 4.1, 10, 14.2).

import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { api } from '../../../api/client.ts';
import { messageOf } from '../../../shared/i18n/errorMessage.ts';
import { Button } from '../../../shared/ui/Button.tsx';
import { Dialog } from '../../../shared/ui/Dialog.tsx';
import { Field } from '../../../shared/ui/Field.tsx';
import { notify } from '../../../shared/ui/toasts.ts';
import type { Classification } from '../classification.ts';
import { ClassificationField } from '../ClassificationField.tsx';
import { changed, saveProperties, type ProjectDetail } from './projectProperties.ts';

/** The longest label of a sharing group the host keeps (`ProjectCreation.SharingGroupLabelMax`). */
const LABEL_MAX = 100;

export interface ProjectPropertiesDialogProps {
  readonly projectId: string;
  /** The code of the project, for the title before the project is read. */
  readonly code: string;
  readonly onClose: () => void;
  /** The project read at the opening, then after each change: the header shows its classification (a stable function). */
  readonly onDetail: (detail: ProjectDetail) => void;
}

/** The project read again when the dialog opens: its values and the version a Save sends are those of the moment. */
function useDetail(projectId: string, onDetail: (detail: ProjectDetail) => void) {
  const [detail, setDetail] = useState<ProjectDetail | null>(null);
  const [failure, setFailure] = useState<string | null>(null);
  useEffect(() => {
    const controller = new AbortController();
    api.get('/api/projects/{id}', { path: { id: projectId }, signal: controller.signal }).then((read) => {
      setDetail(read);
      onDetail(read);
    }, (error: unknown) => {
      if (!controller.signal.aborted) setFailure(messageOf(error));
    });
    return () => {
      controller.abort();
    };
  }, [projectId, onDetail]);
  return { detail, setDetail, failure, setFailure };
}

export function ProjectPropertiesDialog({ projectId, code, onClose, onDetail }: ProjectPropertiesDialogProps) {
  const { t } = useTranslation();
  const { detail, setDetail, failure, setFailure } = useDetail(projectId, onDetail);
  const [classification, setClassification] = useState<Classification | null>(null);
  const [label, setLabel] = useState<string | null>(null);
  const [workday, setWorkday] = useState<string | null>(null);
  const [fte, setFte] = useState<string | null>(null);
  const [sending, setSending] = useState(false);
  const choice = detail === null ? null : {
    classification: classification ?? detail.classification, label: label ?? detail.sharing_group_label ?? '',
    workday: workday ?? String(detail.workday_hours), fte: fte ?? String(detail.fte_hours_per_month),
  };
  const editable = detail?.role === 'facilitator';

  const save = async () => {
    if (detail === null || choice === null) return;
    setSending(true);
    setFailure(null);
    const step = (next: ProjectDetail) => {
      setDetail(next);
      onDetail(next);
    };
    try {
      await saveProperties(detail, choice, step);
      notify(t('project.properties_saved'), 'success');
      onClose();
    } catch (error) {
      setFailure(messageOf(error));
      setSending(false);
    }
  };

  const footer = editable
    ? (
      <>
        <Button testId="project-properties-cancel" variant="secondary" onClick={onClose}>{t('app.cancel')}</Button>
        <Button testId="project-properties-save" variant="primary" disabled={sending || choice === null || !changed(detail, choice)}
          onClick={() => void save()}>{t('project.save')}</Button>
      </>
    )
    : <Button testId="project-properties-done" variant="secondary" onClick={onClose}>{t('app.close')}</Button>;

  return (
    <Dialog title={t('project.properties_title', { code: detail?.code ?? code })} testId="project-properties" onClose={onClose} footer={footer}>
      {detail === null && failure === null && <p className="muted">{t('app.loading')}</p>}
      {choice !== null && (
        <>
          <ClassificationField value={choice.classification} testId="project-properties-classification" disabled={!editable} autoFocus={editable}
            onChange={setClassification} />
          <Field label={t('project.sharing_group')} testId="project-properties-sharing-group" hint={t('project.sharing_group_help')}>
            {(control) => <input {...control} value={choice.label} maxLength={LABEL_MAX} readOnly={!editable} onChange={(event) => { setLabel(event.target.value); }} />}
          </Field>
          <div className="two">
            <Field label={t('project.workday_hours')} testId="project-properties-workday" hint={t('project.workday_hours_help')}>
              {(control) => <input {...control} type="number" min={1} max={24} step={0.01} value={choice.workday} readOnly={!editable} onChange={(event) => { setWorkday(event.target.value); }} />}
            </Field>
            <Field label={t('project.fte_hours')} testId="project-properties-fte" hint={t('project.fte_hours_help')}>
              {(control) => <input {...control} type="number" min={1} max={744} step={0.01} value={choice.fte} readOnly={!editable} onChange={(event) => { setFte(event.target.value); }} />}
            </Field>
          </div>
          {!editable && <p className="field-hint" data-testid="project-properties-read-only">{t('project.properties_read_only')}</p>}
        </>
      )}
      {failure !== null && <p className="message-error" role="alert" data-testid="project-properties-error">{failure}</p>}
    </Dialog>
  );
}
