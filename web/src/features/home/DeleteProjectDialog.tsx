// DeleteProjectDialog.tsx
// Puts a project in the trash once its code is typed, as GitHub asks before a heavy deletion (B-1.3-21 to 23).

import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { api } from '../../api/client.ts';
import { messageOf } from '../../shared/i18n/errorMessage.ts';
import { Confirm } from '../../shared/ui/Confirm.tsx';
import { Field } from '../../shared/ui/Field.tsx';
import type { ProjectItem } from './focus.ts';

export interface DeleteProjectDialogProps {
  readonly project: ProjectItem;
  readonly name: string;
  /** The days the trash keeps a project (`trash_retention_days`, 30 by default). */
  readonly retentionDays: number;
  readonly onDeleted: () => void;
  readonly onClose: () => void;
}

export function DeleteProjectDialog({ project, name, retentionDays, onDeleted, onClose }: DeleteProjectDialogProps) {
  const { t } = useTranslation();
  const [typed, setTyped] = useState('');
  const [error, setError] = useState<string | undefined>(undefined);
  const [sending, setSending] = useState(false);

  const confirm = async () => {
    setSending(true);
    setError(undefined);
    try {
      // The deletion carries the version of the project (If-Match, required in server mode): read at the moment of it.
      const { version } = await api.get('/api/projects/{id}', { path: { id: project.id } });
      await api.delete('/api/projects/{id}', { path: { id: project.id }, query: { confirmation: typed.trim() }, version });
      onDeleted();
    } catch (failure) {
      setError(messageOf(failure));
      setSending(false);
    }
  };

  return (
    <Confirm title={t('home.delete_title', { code: project.code })} testId="delete-project" danger confirmLabel={t('home.delete_button')}
      ready={!sending && typed.trim() === project.code} onConfirm={() => void confirm()} onCancel={onClose}>
      <p>{t('home.delete_text', { name, days: retentionDays })}</p>
      <Field label={t('home.delete_typing', { code: project.code })} testId="delete-project-code" error={error}>
        {(control) => (
          <input {...control} value={typed} maxLength={6} autoComplete="off" spellCheck={false} data-autofocus
            onChange={(event) => {
              setTyped(event.target.value.toUpperCase());
            }} />
        )}
      </Field>
    </Confirm>
  );
}
