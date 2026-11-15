// DuplicateDialog.tsx
// Duplicates a project under a new code: name « {name} (copie) » proposed, what the copy holds said, its root map opened (B-1.6-18 to 22).

import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { api } from '../../../api/client.ts';
import { navigate, paths } from '../../../app/router.ts';
import { messageOf } from '../../../shared/i18n/errorMessage.ts';
import { Button } from '../../../shared/ui/Button.tsx';
import { Dialog } from '../../../shared/ui/Dialog.tsx';
import { Field } from '../../../shared/ui/Field.tsx';
import { PROJECT_CODE, typedCode } from './pmapText.ts';

export function DuplicateDialog({ projectId, sourceName, onClose }: { projectId: string; sourceName: string; onClose: () => void }) {
  const { t } = useTranslation();
  const [code, setCode] = useState('');
  const [name, setName] = useState(() => t('files.copy_name', { name: sourceName }));
  const [error, setError] = useState<string | undefined>(undefined);
  const [sending, setSending] = useState(false);

  const submit = async () => {
    setSending(true);
    setError(undefined);
    try {
      const copy = await api.post('/api/projects/{id}/duplicate', { path: { id: projectId }, body: { code, name: name.trim() } });
      navigate(paths.map(copy.root_map_id));
    } catch (failure) {
      setError(messageOf(failure));
      setSending(false);
    }
  };

  const footer = (
    <>
      <Button testId="duplicate-cancel" variant="secondary" onClick={onClose}>{t('app.cancel')}</Button>
      <Button testId="duplicate-confirm" variant="primary" disabled={sending || !PROJECT_CODE.test(code) || name.trim() === ''} onClick={() => void submit()}>
        {t('files.duplicate_button')}
      </Button>
    </>
  );
  return (
    <Dialog title={t('files.duplicate_title')} testId="duplicate" onClose={onClose} footer={footer}>
      <p className="muted">{t('files.duplicate_help')}</p>
      <Field label={t('project.code')} testId="duplicate-code" hint={t('project.code_help')} error={error}>
        {(control) => <input {...control} value={code} maxLength={6} data-autofocus onChange={(event) => { setCode(typedCode(event.target.value)); }} />}
      </Field>
      <Field label={t('files.name')} testId="duplicate-name">
        {(control) => <input {...control} value={name} maxLength={200} onChange={(event) => { setName(event.target.value); }} />}
      </Field>
    </Dialog>
  );
}
