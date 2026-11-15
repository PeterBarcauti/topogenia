// ExportPackageDialog.tsx
// Exports a project as a package of exchange, without the people by default (spec 5.4: the option is ticked for every export, and can be unticked).

import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { api } from '../../../api/client.ts';
import { download, exportFileName } from '../../../shared/download.ts';
import { messageOf } from '../../../shared/i18n/errorMessage.ts';
import { PACKAGE_EXTENSION } from '../../../shared/i18n/i18n.ts';
import { Button } from '../../../shared/ui/Button.tsx';
import { Dialog } from '../../../shared/ui/Dialog.tsx';
import { notify } from '../../../shared/ui/toasts.ts';

export interface ExportPackageDialogProps {
  readonly project: { readonly id: string; readonly code: string; readonly name: string };
  readonly onClose: () => void;
}

export function ExportPackageDialog({ project, onClose }: ExportPackageDialogProps) {
  const { t } = useTranslation();
  const [withoutPeople, setWithoutPeople] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [sending, setSending] = useState(false);

  const submit = async () => {
    setSending(true);
    setError(null);
    try {
      const archive = await api.post('/api/projects/{id}/export', { path: { id: project.id }, body: { without_people: withoutPeople } });
      const file = exportFileName(project.code, project.name, PACKAGE_EXTENSION);
      download(archive, file);
      notify(t('package.exported', { file }), 'success');
      onClose();
    } catch (failure) {
      setError(messageOf(failure));
      setSending(false);
    }
  };

  const footer = (
    <>
      <Button testId="export-package-cancel" variant="secondary" onClick={onClose}>{t('app.cancel')}</Button>
      <Button testId="export-package-confirm" variant="primary" disabled={sending} onClick={() => void submit()}>{t('package.export_button')}</Button>
    </>
  );
  return (
    <Dialog title={t('package.export_title', { code: project.code })} testId="export-package" onClose={onClose} footer={footer}>
      <p className="muted">{t('package.export_help')}</p>
      <label className="check">
        <input type="checkbox" checked={withoutPeople} data-testid="export-package-without-people" aria-describedby="export-without-people-help"
          onChange={(event) => { setWithoutPeople(event.target.checked); }} />
        <span>{t('package.without_people')}</span>
      </label>
      <p id="export-without-people-help" className="field-hint">{t('package.without_people_help')}</p>
      {error !== null && <p className="message-error" role="alert">{error}</p>}
    </Dialog>
  );
}
