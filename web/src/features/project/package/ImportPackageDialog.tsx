// ImportPackageDialog.tsx
// Imports a package of exchange as a new project (administrator) or in the place of a project (facilitator), always through its preview; a package chosen in the dialog of a .pmap comes here (spec 7.3, S-7.3-03, AR-154).

import { useRef, useState } from 'react';
import { FileUp } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { api } from '../../../api/client.ts';
import { navigate, paths } from '../../../app/router.ts';
import { messageOf } from '../../../shared/i18n/errorMessage.ts';
import { PACKAGE_EXTENSION } from '../../../shared/i18n/i18n.ts';
import { Button } from '../../../shared/ui/Button.tsx';
import { Dialog } from '../../../shared/ui/Dialog.tsx';
import { Field } from '../../../shared/ui/Field.tsx';
import { PROJECT_CODE, typedCode } from '../files/pmapText.ts';
import { PackagePreview, type Preview } from './PackagePreview.tsx';

/** The project a package replaces: its identifier, code and version (If-Match), and how long the trash keeps the old one. */
export interface ReplacedProject {
  readonly id: string;
  readonly code: string;
  readonly version: number;
  readonly retentionDays: number;
}

/** The choices a preview was made for: the import runs only on the same ones (the preview is the account of the import). */
interface Choices {
  readonly file: File;
  readonly withMembers: boolean;
  readonly code: string;
}

const sameChoices = (a: Choices, b: Choices) => a.file === b.file && a.withMembers === b.withMembers && a.code === b.code;

/** The dialog; `chosen` is the package the person chose in the dialog of a .pmap, said and kept as the file to import. */
export function ImportPackageDialog({ replaced, chosen, onClose }: { replaced?: ReplacedProject; chosen?: File; onClose: () => void }) {
  const { t } = useTranslation();
  const [file, setFile] = useState<File | null>(chosen ?? null);
  const [withMembers, setWithMembers] = useState(false);
  const [code, setCode] = useState('');
  const [preview, setPreview] = useState<{ choices: Choices; result: Preview } | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [sending, setSending] = useState(false);
  const chooser = useRef<HTMLInputElement>(null);
  const choices = file === null ? null : { file, withMembers, code };
  const codeQuery = code === '' ? undefined : code;

  const run = async (step: (current: Choices) => Promise<void>) => {
    if (choices === null) return;
    setSending(true);
    setError(null);
    try {
      await step(choices);
    } catch (failure) {
      setError(messageOf(failure));
    } finally {
      setSending(false);
    }
  };

  const check = () => run(async (current) => {
    const result = replaced === undefined
      ? await api.post('/api/projects/import/preview', { body: current.file, query: { with_members: String(current.withMembers), code: codeQuery } })
      : await api.post('/api/projects/{id}/import/preview', { path: { id: replaced.id }, body: current.file, query: { with_members: String(current.withMembers) } });
    setPreview({ choices: current, result });
  });

  const importIt = () => run(async (current) => {
    const imported = replaced === undefined
      ? await api.post('/api/projects/import', { body: current.file, query: { with_members: String(current.withMembers), code: codeQuery } })
      : await api.post('/api/projects/{id}/import', { path: { id: replaced.id }, body: current.file, query: { with_members: String(current.withMembers) }, version: replaced.version });
    navigate(paths.map(imported.root_map_id));
  });

  const current = preview !== null && choices !== null && sameChoices(preview.choices, choices) ? preview.result : null;
  const codeNeeded = replaced === undefined && preview !== null && preview.result.code_state !== 'free';
  const importable = current !== null && (replaced !== undefined || current.code_state === 'free');
  const footer = (
    <>
      <Button testId="import-package-cancel" variant="secondary" onClick={onClose}>{t('app.cancel')}</Button>
      <Button testId="import-package-check" variant={importable ? 'secondary' : 'primary'} disabled={sending || choices === null || (codeNeeded && !PROJECT_CODE.test(code))}
        onClick={() => void check()}>{t('package.check')}</Button>
      <Button testId="import-package-confirm" variant={replaced === undefined ? 'primary' : 'danger'} disabled={sending || !importable} onClick={() => void importIt()}>
        {replaced === undefined ? t('package.import_button') : t('package.replace_button')}
      </Button>
    </>
  );
  return (
    <Dialog title={replaced === undefined ? t('package.import_title') : t('package.replace_title', { code: replaced.code })} testId="import-package" onClose={onClose} footer={footer} wide>
      {chosen !== undefined && <p role="status" data-testid="import-package-recognized">{t('package.recognized', { name: chosen.name, extension: PACKAGE_EXTENSION })}</p>}
      <p className="muted">{replaced === undefined ? t('package.import_help') : t('package.replace_help', { days: replaced.retentionDays })}</p>
      <div className="file-choice">
        <Button testId="import-package-choose" icon={FileUp} variant="secondary" onClick={() => chooser.current?.click()}>{t('package.choose')}</Button>
        <input ref={chooser} type="file" accept={PACKAGE_EXTENSION} hidden aria-label={t('package.choose')} data-testid="import-package-file"
          onChange={(event) => { setFile(event.target.files?.[0] ?? null); }} />
        {file !== null && <span className="file-name">{file.name}</span>}
      </div>
      <label className="check">
        <input type="checkbox" checked={withMembers} data-testid="import-package-with-members" onChange={(event) => { setWithMembers(event.target.checked); }} />
        <span>{t('package.with_members')}</span>
      </label>
      {codeNeeded && (
        <Field label={t('package.new_code')} testId="import-package-code" hint={t('project.code_help')}>
          {(control) => <input {...control} value={code} maxLength={6} onChange={(event) => { setCode(typedCode(event.target.value)); }} />}
        </Field>
      )}
      {current !== null && <PackagePreview preview={current} replacing={replaced !== undefined} />}
      {error !== null && <p className="message-error" role="alert" data-testid="import-package-error">{error}</p>}
    </Dialog>
  );
}
