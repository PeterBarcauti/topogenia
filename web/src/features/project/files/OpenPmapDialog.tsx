// OpenPmapDialog.tsx
// Opens a .pmap as a new project: a file chosen, dropped or pasted, its code and name proposed, the faults of a refusal, the points to check; a package chosen goes to its import (annex B 1.6, AR-154).

import { useRef, useState, type DragEvent } from 'react';
import { FileText } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { api, type Schema } from '../../../api/client.ts';
import { isApiError } from '../../../api/errors.ts';
import { navigate, paths } from '../../../app/router.ts';
import { messageOf } from '../../../shared/i18n/errorMessage.ts';
import { Button } from '../../../shared/ui/Button.tsx';
import { Dialog } from '../../../shared/ui/Dialog.tsx';
import { Field } from '../../../shared/ui/Field.tsx';
import { ImportPackageDialog } from '../package/ImportPackageDialog.tsx';
import { PROJECT_CODE, headerOf, typedCode } from './pmapText.ts';

type Opened = Schema<'PmapOpened'>;

/** The first bytes of a ZIP archive, which a package of exchange is: read as text, never the start of a .pmap (AR-154). */
const ZIP_SIGNATURE = 'PK\u0003\u0004';

/** A fault of the file as the host lists it: its place (`map.steps[2]`), its code, its message (A-1.2-19). */
interface FileFault {
  readonly path: string;
  readonly message: string;
}

function faultsOf(failure: unknown): FileFault[] {
  const list = isApiError(failure) ? failure.details.errors : undefined;
  if (!Array.isArray(list)) return [];
  return list.filter((item): item is FileFault => typeof item === 'object' && item !== null && typeof (item as FileFault).message === 'string');
}

const openMap = (opened: Opened) => {
  navigate(paths.map(opened.root_map_id));
};

/** The project made from a file with points to check: what it holds, the points, the way to its map (B-1.6-14 to 16). */
function Created({ opened }: { opened: Opened }) {
  const { t } = useTranslation();
  const footer = <Button testId="pmap-created-open" variant="primary" onClick={() => { openMap(opened); }}>{t('files.open_map')}</Button>;
  return (
    <Dialog title={t('files.created_title')} testId="pmap-created" onClose={() => { openMap(opened); }} footer={footer}>
      <p role="status">{t('files.created_summary', { steps: opened.steps, lanes: opened.lanes, flows: opened.flows, maps: opened.maps })}</p>
      <p className="dialog-subtitle">{t('files.uncertainties')}</p>
      <ul className="uncertainties">{opened.uncertainties.map((text, index) => <li key={index}>{text}</li>)}</ul>
    </Dialog>
  );
}

/** The state of the dialog: the content and where it came from, the code and name proposed or typed, the refusal, the result, the package chosen instead of a .pmap. */
function usePmapForm(given: string | undefined, originMapId: string | undefined) {
  const { t } = useTranslation();
  const [content, setContent] = useState(given ?? '');
  const [fileName, setFileName] = useState<string | null>(null);
  const [code, setCode] = useState(() => (given === undefined ? '' : headerOf(given).code));
  const [name, setName] = useState(() => (given === undefined ? '' : headerOf(given).name));
  // The code and the name follow the content until the person changes them (B-1.6-09).
  const [typed, setTyped] = useState({ code: false, name: false });
  const [error, setError] = useState<{ message: string; faults: FileFault[] } | null>(null);
  const [sending, setSending] = useState(false);
  const [opened, setOpened] = useState<Opened | null>(null);
  const [packaged, setPackaged] = useState<File | null>(null);

  const changeContent = (text: string, file: string | null) => {
    setContent(text);
    setFileName(file);
    setError(null);
    const header = headerOf(text);
    if (!typed.code) setCode(header.code);
    if (!typed.name) setName(header.name);
  };

  const readFile = async (file: File | undefined) => {
    if (file === undefined) return;
    try {
      const text = await file.text();
      if (text.startsWith(ZIP_SIGNATURE)) setPackaged(file);
      else changeContent(text, file.name);
    } catch {
      setError({ message: t('files.unreadable'), faults: [] });
    }
  };

  const submit = async () => {
    setSending(true);
    setError(null);
    try {
      const result = await api.post('/api/projects/import-pmap', { body: { content, code, name: name.trim(), ...(originMapId === undefined ? {} : { origin_map_id: originMapId }) } });
      if (result.uncertainties.length > 0) setOpened(result);
      else openMap(result);
    } catch (failure) {
      setError({ message: messageOf(failure), faults: faultsOf(failure) });
    } finally {
      setSending(false);
    }
  };

  const typeCode = (value: string) => {
    setCode(typedCode(value));
    setTyped((state) => ({ ...state, code: true }));
  };
  const typeName = (value: string) => {
    setName(value);
    setTyped((state) => ({ ...state, name: true }));
  };
  const ready = !sending && content.trim() !== '' && PROJECT_CODE.test(code) && name.trim() !== '';
  return { content, fileName, code, name, error, opened, packaged, ready, changeContent, readFile, submit, typeCode, typeName };
}

/**
 * The dialog, empty, or filled with the text of a proposal of the assistant (spec 3.0 section 3.2): its code and name to
 * confirm; a proposal made on a map names it, whose steps carry their sheets and translations into the project (ADR 082).
 */
export function OpenPmapDialog({ onClose, content: given, originMapId }: { onClose: () => void; content?: string; originMapId?: string }) {
  const { t } = useTranslation();
  const { content, fileName, code, name, error, opened, packaged, ready, changeContent, readFile, submit, typeCode, typeName } = usePmapForm(given, originMapId);
  const chooser = useRef<HTMLInputElement>(null);
  const drop = (event: DragEvent<HTMLTextAreaElement>) => {
    event.preventDefault();
    void readFile(event.dataTransfer.files[0]);
  };

  if (opened !== null) return <Created opened={opened} />;
  // A package chosen or dropped here is not shown as text: its own import takes it, with its check (AR-154).
  if (packaged !== null) return <ImportPackageDialog chosen={packaged} onClose={onClose} />;
  const footer = (
    <>
      <Button testId="open-pmap-cancel" variant="secondary" onClick={onClose}>{t('app.cancel')}</Button>
      <Button testId="open-pmap-confirm" variant="primary" disabled={!ready} onClick={() => void submit()}>{t('files.open_button')}</Button>
    </>
  );
  return (
    <Dialog title={t('files.open_title')} testId="open-pmap" onClose={onClose} footer={footer} wide>
      <p className="muted">{t('files.open_help')}</p>
      <div className="file-choice">
        <Button testId="open-pmap-choose" icon={FileText} variant="secondary" onClick={() => chooser.current?.click()}>{t('files.choose')}</Button>
        <input ref={chooser} type="file" accept=".pmap,application/json" hidden aria-label={t('files.choose')} data-testid="open-pmap-file"
          onChange={(event) => void readFile(event.target.files?.[0])} />
        {fileName !== null && <span className="file-name">{fileName}</span>}
      </div>
      <Field label={t('files.or_paste')} testId="open-pmap-content">
        {(control) => <textarea {...control} className="field-control file-content" rows={10} spellCheck={false} value={content} placeholder={t('files.area')}
          onChange={(event) => { changeContent(event.target.value, null); }} onDragOver={(event) => { event.preventDefault(); }} onDrop={drop} />}
      </Field>
      <div className="form-row">
        <Field label={t('project.code')} testId="open-pmap-code" hint={t('project.code_help')}>
          {(control) => <input {...control} value={code} maxLength={6} onChange={(event) => { typeCode(event.target.value); }} />}
        </Field>
        <Field label={t('files.name')} testId="open-pmap-name">
          {(control) => <input {...control} value={name} maxLength={200} onChange={(event) => { typeName(event.target.value); }} />}
        </Field>
      </div>
      {error !== null && (
        <div className="message-error" role="alert" data-testid="open-pmap-error">
          <p>{error.message}</p>
          {error.faults.length > 0 && (
            <ul className="file-faults">{error.faults.map((fault, index) => <li key={index}>{fault.path !== '' && <code>{fault.path}</code>} {fault.message}</li>)}</ul>
          )}
        </div>
      )}
    </Dialog>
  );
}
