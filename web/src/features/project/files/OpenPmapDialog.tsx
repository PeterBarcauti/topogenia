// OpenPmapDialog.tsx
// Opens a .pmap as a new project: a file chosen, dropped or pasted, its code and name proposed, the faults of a refusal, the points to check; a package chosen goes to its import (annex B 1.6, AR-154), a collection to its own dialog (3.4, ADR 114).

import { useMemo, useRef, useState, type DragEvent } from 'react';
import { FileText } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { api, type Schema } from '../../../api/client.ts';
import { navigate, paths } from '../../../app/router.ts';
import { messageOf } from '../../../shared/i18n/errorMessage.ts';
import { Button } from '../../../shared/ui/Button.tsx';
import { Dialog } from '../../../shared/ui/Dialog.tsx';
import { Field } from '../../../shared/ui/Field.tsx';
import { ImportPackageDialog } from '../package/ImportPackageDialog.tsx';
import { CollectionDialog } from './CollectionDialog.tsx';
import { isCollection, OPENED_BYTES_MAX, type Refusal } from './collection.ts';
import { conversionMessage, OPENED_FILES, pmapOfFile, ZIP_SIGNATURE } from './fileConversion.ts';
import { faultsOf, type FileFault } from './fileFaults.ts';
import { PROJECT_CODE, headerOf, typedCode } from './pmapText.ts';

type Opened = Schema<'PmapOpened'>;

const openMap = (opened: Opened) => {
  navigate(paths.map(opened.root_map_id));
};

/** The project made from a file with points to check or addresses that reach another machine: what it holds, the addresses, the points, the way to its map (B-1.6-14 to 16, pre-audit 68 P3-02). */
function Created({ opened }: { opened: Opened }) {
  const { t } = useTranslation();
  const footer = <Button testId="pmap-created-open" variant="primary" onClick={() => { openMap(opened); }}>{t('files.open_map')}</Button>;
  return (
    <Dialog title={t('files.created_title')} testId="pmap-created" onClose={() => { openMap(opened); }} footer={footer}>
      <p role="status">{t('files.created_summary', { steps: opened.steps, lanes: opened.lanes, flows: opened.flows, maps: opened.maps })}</p>
      {opened.remote_locations > 0 && <p className="message-warning" data-testid="pmap-created-remote">{t('files.remote_locations', { count: opened.remote_locations })}</p>}
      {opened.uncertainties.length > 0 && (
        <>
          <p className="dialog-subtitle">{t('files.uncertainties')}</p>
          <ul className="uncertainties">{opened.uncertainties.map((text, index) => <li key={index}>{text}</li>)}</ul>
        </>
      )}
    </Dialog>
  );
}

/** The state of the dialog: the content and where it came from, the code and name proposed or typed, the refusal, the result, the package chosen instead of a .pmap. */
function usePmapForm(given: string | undefined, originMapId: string | undefined, raciLetters: string | undefined) {
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
  // A collection whose envelope the host refused, back in the text to correct: read again by « Ouvrir », not at each key typed.
  const [held, setHeld] = useState(false);

  const changeContent = (text: string, file: string | null) => {
    setContent(text);
    setFileName(file);
    setError(null);
    if (file !== null) setHeld(false);
    const header = headerOf(text);
    if (!typed.code) setCode(header.code);
    if (!typed.name) setName(header.name);
  };

  const readFile = async (file: File | undefined) => {
    if (file === undefined) return;
    // Beyond what the host reads, a file is said too large before it is read whole in the page.
    if (file.size > OPENED_BYTES_MAX) {
      setError({ message: t('files.open_too_large', { max: OPENED_BYTES_MAX / (1024 * 1024) }), faults: [] });
      return;
    }
    try {
      // A table becomes a .pmap before anything is sent (plan 3.1, L5): a workbook is a ZIP too, read by its extension first.
      const converted = await pmapOfFile(file, t, raciLetters);
      if (converted !== null) {
        changeContent(converted, file.name);
        return;
      }
      const text = await file.text();
      if (text.startsWith(ZIP_SIGNATURE)) setPackaged(file);
      else changeContent(text, file.name);
    } catch (failure) {
      setError({ message: conversionMessage(failure, t) ?? t('files.unreadable'), faults: [] });
    }
  };

  const submit = async () => {
    setSending(true);
    setError(null);
    try {
      const result = await api.post('/api/projects/import-pmap', { body: { content, code, name: name.trim(), ...(originMapId === undefined ? {} : { origin_map_id: originMapId }) } });
      if (result.uncertainties.length > 0 || result.remote_locations > 0) setOpened(result);
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
  const back = (refusal: Refusal) => { setHeld(true); setError({ message: refusal.message, faults: [...refusal.faults] }); };
  const collection = useMemo(() => isCollection(content), [content]);
  const ready = !sending && content.trim() !== '' && (collection || (PROJECT_CODE.test(code) && name.trim() !== ''));
  const confirm = () => {
    if (collection) setHeld(false);
    else void submit();
  };
  return { content, fileName, code, name, error, opened, packaged, collection, held, ready, changeContent, readFile, confirm, back, typeCode, typeName };
}

/**
 * The dialog, empty, or filled with the text of a proposal of the assistant (spec 3.0 section 3.2): its code and name to
 * confirm; a proposal made on a map names it, whose steps carry their sheets and translations into the project (ADR 082).
 * Opened from a map, it knows the letters of RACI of the installation, which a table may give (`ACI` otherwise).
 */
export function OpenPmapDialog({ onClose, onCreated, content: given, originMapId, raciLetters }: { onClose: () => void; onCreated?: () => void; content?: string; originMapId?: string; raciLetters?: string }) {
  const { t } = useTranslation();
  const { content, fileName, code, name, error, opened, packaged, collection, held, ready, changeContent, readFile, confirm, back, typeCode, typeName } = usePmapForm(given, originMapId, raciLetters);
  const chooser = useRef<HTMLInputElement>(null);
  const drop = (event: DragEvent<HTMLTextAreaElement>) => {
    event.preventDefault();
    void readFile(event.dataTransfer.files[0]);
  };

  if (opened !== null) return <Created opened={opened} />;
  // A package chosen or dropped here is not shown as text: its own import takes it, with its check (AR-154).
  if (packaged !== null) return <ImportPackageDialog chosen={packaged} onClose={onClose} />;
  // A collection chosen, dropped or pasted lists its processes, one project to each (ADR 114); a refused envelope comes back here.
  if (collection && !held) return <CollectionDialog content={content} onClose={onClose} onCreated={onCreated} onBack={back} />;
  const footer = (
    <>
      <Button testId="open-pmap-cancel" variant="secondary" onClick={onClose}>{t('app.cancel')}</Button>
      <Button testId="open-pmap-confirm" variant="primary" disabled={!ready} onClick={confirm}>{t('files.open_button')}</Button>
    </>
  );
  return (
    <Dialog title={t('files.open_title')} testId="open-pmap" onClose={onClose} footer={footer} wide>
      <p className="muted">{t('files.open_help')}</p>
      <div className="file-choice">
        <Button testId="open-pmap-choose" icon={FileText} variant="secondary" onClick={() => chooser.current?.click()}>{t('files.choose')}</Button>
        <input ref={chooser} type="file" accept={OPENED_FILES} hidden aria-label={t('files.choose')} data-testid="open-pmap-file"
          onChange={(event) => {
            // Emptied once read: the same file chosen again, corrected, is read again.
            const input = event.target;
            void readFile(input.files?.[0]);
            input.value = '';
          }} />
        {fileName !== null && <span className="file-name">{fileName}</span>}
      </div>
      <Field label={t('files.or_paste')} testId="open-pmap-content">
        {(control) => <textarea {...control} className="field-control file-content" rows={10} spellCheck={false} value={content} placeholder={t('files.area')}
          onChange={(event) => { changeContent(event.target.value, null); }} onDragOver={(event) => { event.preventDefault(); }} onDrop={drop} />}
      </Field>
      {!collection && (
        <div className="form-row">
          <Field label={t('project.code')} testId="open-pmap-code" hint={t('project.code_help')}>
            {(control) => <input {...control} value={code} maxLength={6} onChange={(event) => { typeCode(event.target.value); }} />}
          </Field>
          <Field label={t('files.name')} testId="open-pmap-name">
            {(control) => <input {...control} value={name} maxLength={200} onChange={(event) => { typeName(event.target.value); }} />}
          </Field>
        </div>
      )}
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
