// CollectionDialog.tsx
// Opens a collection of templates (.pmaps) as projects (3.4, lot 6, ADR 114): its processes listed and ticked after a read-only preview, a code to each, then one project per ticked line, the result of each line told; nothing is written before the creation is asked.

import { useEffect, useRef } from 'react';
import { useTranslation } from 'react-i18next';
import { navigate, paths } from '../../../app/router.ts';
import { Button } from '../../../shared/ui/Button.tsx';
import { Dialog } from '../../../shared/ui/Dialog.tsx';
import { DUPLICATE_CODE, isReadable, mayCreate, readableRows, tickedRows, type CollectionRow, type LineRefusal, type Refusal } from './collection.ts';
import { useCollectionForm, type Loading } from './collectionForm.ts';

/** The codes of the host for a code already taken, by a live project or by one in the trash. */
const TAKEN_CODES = ['PROJET_CODE_EXISTANT', 'PROJET_CODE_CORBEILLE'];

/** The function of translation the component gets from its hook. */
type Text = ReturnType<typeof useTranslation>['t'];

type Fault = { readonly path: string; readonly message: string };

/** The name of a line: the one the file writes, else its rank in the collection. */
const labelOf = (row: CollectionRow, t: Text): string => (row.name !== null && row.name.trim() !== '' ? row.name : t('files.collection_unnamed', { rank: row.index + 1 }));

/** Why a line is not created: a code taken or given to another line is said by its own text, with the code tried; any other refusal by the message of the host. */
const reasonOf = (refusal: LineRefusal, t: Text): string => {
  const { code, triedCode } = refusal;
  const reason = code === DUPLICATE_CODE ? t('files.collection_code_twice', { code: triedCode })
    : code !== null && TAKEN_CODES.includes(code) ? t('files.collection_code_free', { code: triedCode }) : refusal.message;
  return t('files.collection_failed', { reason });
};

/** What a line holds, on one line: its language, its counts, its points to check. */
const summaryOf = (row: CollectionRow, t: Text): string => [
  row.language,
  row.counts === null ? null : t('files.created_summary', { steps: row.counts.steps, lanes: row.counts.lanes, flows: row.counts.flows, maps: row.counts.maps }),
  row.uncertainties.length === 0 ? null : t('files.collection_points', { count: row.uncertainties.length }),
].filter((part) => part !== null).join(' · ');

function FaultList({ faults, summary }: { faults: readonly Fault[]; summary: string }) {
  return (
    <details>
      <summary>{summary}</summary>
      <ul className="file-faults">{faults.map((fault, index) => <li key={index}>{fault.path !== '' && <code>{fault.path}</code>} {fault.message}</li>)}</ul>
    </details>
  );
}

function CodeField({ row, label, disabled, onType }: { row: CollectionRow; label: string; disabled: boolean; onType: (typed: string) => void }) {
  const { t } = useTranslation();
  const hint = `collection-code-${row.index}`;
  return (
    <>
      <span id={hint} hidden>{t('project.code_help')}</span>
      <input className="field-control" data-testid={`collection-code-input-${row.index}`} value={row.code} maxLength={6} disabled={disabled} aria-label={`${t('project.code')} (${label})`} aria-describedby={hint}
        onChange={(event) => { onType(event.target.value); }} />
    </>
  );
}

interface RowProps {
  readonly row: CollectionRow;
  readonly busy: boolean;
  readonly onType: (typed: string) => void;
}

/** A line of the list before the creation: its box, what it holds, its code; a process the host refuses is unticked, greyed, its faults folded. */
function ChooseRow({ row, busy, onTick, onType }: RowProps & { onTick: (ticked: boolean) => void }) {
  const { t } = useTranslation();
  const label = labelOf(row, t);
  const readable = isReadable(row);
  const hasDetails = (row.description ?? '') !== '' || row.uncertainties.length > 0;
  return (
    <li className={readable ? undefined : 'muted'} data-testid={`collection-row-${row.index}`}>
      <label className="check">
        <input type="checkbox" data-testid={`collection-tick-${row.index}`} checked={row.ticked && readable} disabled={!readable || busy} onChange={(event) => { onTick(event.target.checked); }} />
        <span>{label}</span>
      </label>
      {!readable && <FaultList faults={row.faults} summary={t('files.collection_invalid')} />}
      {readable && <p className="muted">{summaryOf(row, t)}</p>}
      {readable && row.remoteLocations > 0 && <p className="message-warning" data-testid={`collection-remote-${row.index}`}>{t('files.remote_locations', { count: row.remoteLocations })}</p>}
      {readable && <CodeField row={row} label={label} disabled={busy || !row.ticked} onType={onType} />}
      {readable && hasDetails && (
        <details>
          <summary>{t('files.collection_details')}</summary>
          {(row.description ?? '') !== '' && <p>{row.description}</p>}
          {row.uncertainties.length > 0 && <ul className="uncertainties">{row.uncertainties.map((text, index) => <li key={index}>{text}</li>)}</ul>}
        </details>
      )}
    </li>
  );
}

/** A line of the result: created, with the way to its map; refused, with the reason, its code to change and « Réessayer ». */
function DoneRow({ row, busy, onType, onRetry }: RowProps & { onRetry: () => void }) {
  const { t } = useTranslation();
  const label = labelOf(row, t);
  const { opened, refusal } = row;
  return (
    <li data-testid={`collection-result-${row.index}`}>
      <strong>{label}</strong>
      {row.state === 'created' && opened !== null && (
        <>
          <p role="status">{t('files.collection_created', { code: opened.code })}</p>
          <Button testId={`collection-open-${row.index}`} variant="secondary" aria-label={`${t('files.open_map')} : ${label}`} onClick={() => { navigate(paths.map(opened.root_map_id)); }}>
            {t('files.open_map')}
          </Button>
        </>
      )}
      {row.state === 'failed' && refusal !== null && (
        <>
          <p className="message-error" role="alert">{reasonOf(refusal, t)}</p>
          {refusal.faults.length > 0 && <FaultList faults={refusal.faults} summary={t('files.collection_invalid')} />}
          <CodeField row={row} label={label} disabled={busy} onType={onType} />
          <Button testId={`collection-retry-${row.index}`} variant="secondary" disabled={busy || !mayCreate([row])} aria-label={`${t('files.collection_retry')} : ${label}`} onClick={onRetry}>
            {t('files.collection_retry')}
          </Button>
        </>
      )}
    </li>
  );
}

/** The box of the header: ticks every line, or none; half-ticked when only some are. */
function AllBox({ rows, disabled, onChange }: { rows: readonly CollectionRow[]; disabled: boolean; onChange: (ticked: boolean) => void }) {
  const { t } = useTranslation();
  const box = useRef<HTMLInputElement>(null);
  const ticked = tickedRows(rows).length;
  const total = readableRows(rows).length;
  useEffect(() => {
    if (box.current !== null) box.current.indeterminate = ticked > 0 && ticked < total;
  }, [ticked, total]);
  return (
    <div className="form-row">
      <label className="check">
        <input ref={box} type="checkbox" data-testid="collection-tick-all" checked={total > 0 && ticked === total} disabled={disabled || total === 0} onChange={(event) => { onChange(event.target.checked); }} />
        <span>{t('files.collection_all')}</span>
      </label>
      <span className="muted" role="status">{t('files.collection_ticked', { count: ticked, total })}</span>
    </div>
  );
}

/** The title: the name of the collection, then once the creation is done, whether a project at least was created. */
function titleOf(loading: Loading, rows: readonly CollectionRow[], done: boolean, t: Text): string {
  if (done) return t(rows.some((row) => row.state === 'created') ? 'files.collection_done' : 'files.collection_none');
  return loading.status === 'ready' ? t('files.collection_title', { name: loading.name }) : t('files.open_title');
}

interface CollectionDialogProps {
  readonly content: string;
  readonly onClose: () => void;
  readonly onCreated?: () => void;
  /** Back to the text of the file, kept, when the host refuses the envelope: it is corrected there, as a .pmap refused. */
  readonly onBack?: (refusal: Refusal) => void;
}

export function CollectionDialog({ content, onClose, onCreated, onBack }: CollectionDialogProps) {
  const { t } = useTranslation();
  const form = useCollectionForm(content, onCreated);
  const { loading, rows, phase, busy } = form;
  const done = phase.step === 'done';
  const ready = loading.status === 'ready';
  // A creation under way is not left: the lines already sent are told when it ends.
  const close = () => {
    if (!busy) onClose();
  };
  const refused = loading.status === 'refused' ? loading.refusal : null;
  // The button pressed is gone once the creation is done: the focus goes to the way out, Escape keeps closing.
  const way = useRef<HTMLButtonElement>(null);
  useEffect(() => {
    if (done) way.current?.focus();
  }, [done]);
  const footer = done ? <Button ref={way} testId="collection-done" variant="primary" onClick={close}>{t('app.close')}</Button> : (
    <>
      <Button testId="collection-cancel" variant="secondary" disabled={busy} onClick={close}>{t('app.cancel')}</Button>
      {refused !== null && onBack !== undefined
        ? <Button testId="collection-back" variant="primary" onClick={() => { onBack(refused); }}>{t('files.collection_fix')}</Button>
        : <Button testId="collection-create" variant="primary" disabled={busy || !ready || !mayCreate(rows)} onClick={form.create}>{t('files.collection_create')}</Button>}
    </>
  );
  const title = titleOf(loading, rows, done, t);
  const results = done || busy;
  return (
    <Dialog title={title} testId="collection" onClose={close} footer={footer} wide>
      {!done && <p className="muted">{t('files.collection_help')}</p>}
      {loading.status === 'loading' && <p className="muted" role="status">{t('app.loading')}</p>}
      {loading.status === 'refused' && (
        <div className="message-error" role="alert" data-testid="collection-error">
          <p>{loading.refusal.message}</p>
          {loading.refusal.faults.length > 0 && <ul className="file-faults">{loading.refusal.faults.map((fault, index) => <li key={index}>{fault.path !== '' && <code>{fault.path}</code>} {fault.message}</li>)}</ul>}
        </div>
      )}
      {phase.step === 'creating' && <p role="status" data-testid="collection-progress">{t('files.collection_creating', { done: phase.done, total: phase.total })}</p>}
      {ready && !results && <AllBox rows={rows} disabled={busy} onChange={form.tickEvery} />}
      {ready && (
        <ul className="project-list">
          {results
            ? rows.filter((row) => row.state !== 'idle').map((row) => (
              <DoneRow key={row.index} row={row} busy={busy} onType={(typed) => { form.typeCode(row.index, typed); }} onRetry={() => { form.retry(row.index); }} />
            ))
            : rows.map((row) => (
              <ChooseRow key={row.index} row={row} busy={busy} onTick={(ticked) => { form.tick(row.index, ticked); }} onType={(typed) => { form.typeCode(row.index, typed); }} />
            ))}
        </ul>
      )}
    </Dialog>
  );
}
