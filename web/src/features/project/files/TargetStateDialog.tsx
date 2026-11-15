// TargetStateDialog.tsx
// Creates the target state of a project (Makigami M6, P3e): a new project, complete copy of this one linked to it, under a new code; its name « {name} (état cible) » and the label of its reference proposed; its main map opened.

import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { api } from '../../../api/client.ts';
import { navigate, paths } from '../../../app/router.ts';
import { messageOf } from '../../../shared/i18n/errorMessage.ts';
import { Button } from '../../../shared/ui/Button.tsx';
import { Dialog } from '../../../shared/ui/Dialog.tsx';
import { Field } from '../../../shared/ui/Field.tsx';
import { PROJECT_CODE, typedCode } from './pmapText.ts';

export function TargetStateDialog({ projectId, sourceName, onClose }: { projectId: string; sourceName: string; onClose: () => void }) {
  const { t } = useTranslation();
  const [code, setCode] = useState('');
  const [name, setName] = useState(() => t('files.target_name', { name: sourceName }));
  const [label, setLabel] = useState(() => t('files.target_label_default'));
  const [error, setError] = useState<string | undefined>(undefined);
  const [sending, setSending] = useState(false);

  const submit = async () => {
    setSending(true);
    setError(undefined);
    try {
      const target = await api.post('/api/projects/{id}/target', { path: { id: projectId }, body: { code, name: name.trim(), label: label.trim() } });
      navigate(paths.map(target.root_map_id));
    } catch (failure) {
      setError(messageOf(failure));
      setSending(false);
    }
  };

  const footer = (
    <>
      <Button testId="target-cancel" variant="secondary" onClick={onClose}>{t('app.cancel')}</Button>
      <Button testId="target-confirm" variant="primary" disabled={sending || !PROJECT_CODE.test(code) || name.trim() === '' || label.trim() === ''}
        onClick={() => void submit()}>
        {t('files.target_button')}
      </Button>
    </>
  );
  return (
    <Dialog title={t('files.target_title')} testId="target" onClose={onClose} footer={footer}>
      <p className="muted">{t('files.target_help')}</p>
      <Field label={t('project.code')} testId="target-code" hint={t('project.code_help')} error={error}>
        {(control) => <input {...control} value={code} maxLength={6} data-autofocus onChange={(event) => { setCode(typedCode(event.target.value)); }} />}
      </Field>
      <Field label={t('files.name')} testId="target-name">
        {(control) => <input {...control} value={name} maxLength={200} onChange={(event) => { setName(event.target.value); }} />}
      </Field>
      <Field label={t('files.target_label')} testId="target-label">
        {(control) => <input {...control} value={label} maxLength={100} onChange={(event) => { setLabel(event.target.value); }} />}
      </Field>
    </Dialog>
  );
}
