// SnapshotsSection.tsx
// The photos of the indicators and the target state in the panel « Analysis », on the main map (Makigami M6, M8, plan 3.1, P3e): a photo taken, compared with now, deleted; for a target state its reference compared with now, where its source stands, its reference read again by a facilitator; a label typed and not sent is followed for « Quit » (R11).

import { Camera, GitCompareArrows, RefreshCw, Trash2 } from 'lucide-react';
import { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { api, type Schema } from '../../../api/client.ts';
import { messageOf } from '../../../shared/i18n/errorMessage.ts';
import { useUnsavedForm } from '../../../shared/state/useUnsavedForm.ts';
import { Button } from '../../../shared/ui/Button.tsx';
import { Icon } from '../../../shared/ui/Icon.tsx';
import { changeRowsOf, snapshotDate } from '../model/snapshots.ts';
import type { StreamWriting } from '../model/valueStream.ts';

type MapView = Schema<'MapView'>;
type SnapshotsView = Schema<'SnapshotsView'>;
type SnapshotView = Schema<'SnapshotView'>;
type IndicatorChange = Schema<'IndicatorChange'>;

/** The indicators compared: a row each, the waits set in under the lead time, the gain coloured by the way the core says is better. */
function Comparison({ changes, before, writing, testId }: { changes: readonly IndicatorChange[]; before: string; writing: StreamWriting; testId: string }) {
  const { t } = useTranslation();
  const rows = useMemo(() => changeRowsOf(changes, writing), [changes, writing]);
  return (
    <table className="snapshot-table" data-testid={testId}>
      <thead>
        <tr><th scope="col">{t('snapshots.indicator')}</th><th scope="col">{before}</th><th scope="col">{t('snapshots.after')}</th><th scope="col">{t('snapshots.gain')}</th></tr>
      </thead>
      <tbody>
        {rows.map((row) => (
          <tr key={row.code} className={row.part ? 'is-part' : undefined} data-testid={`${testId}-${row.code}`}>
            <th scope="row">{row.name}</th>
            <td>{row.before ?? '?'}</td>
            <td>{row.after ?? '?'}</td>
            <td className={row.better === null ? undefined : row.better ? 'is-better' : 'is-worse'}
              title={row.better === null ? undefined : t(row.better ? 'snapshots.better' : 'snapshots.worse')}>
              {row.gain ?? '?'}
            </td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}

/** A label typed and sent: the form of a new photo, or of the reference read again. */
function LabelForm({ initial, action, testId, onSend, onCancel }: {
  initial: string; action: string; testId: string; onSend: (label: string) => Promise<void>; onCancel: () => void;
}) {
  const { t } = useTranslation();
  const [label, setLabel] = useState(initial);
  const [sending, setSending] = useState(false);
  useUnsavedForm(label !== initial);
  const send = async () => {
    setSending(true);
    // A refusal is said by the section; the form stays with what was typed.
    await onSend(label.trim()).catch(() => undefined);
    setSending(false);
  };
  return (
    <form className="snapshot-form" data-testid={testId} onSubmit={(event) => { event.preventDefault(); void send(); }}>
      <input className="field-control" value={label} maxLength={100} aria-label={t('snapshots.label')} placeholder={t('snapshots.label_placeholder')} data-autofocus
        data-testid={`${testId}-label`} onChange={(event) => { setLabel(event.target.value); }} onKeyDown={(event) => { if (event.key === 'Escape') onCancel(); }} />
      <Button testId={`${testId}-send`} variant="primary" type="submit" disabled={sending || label.trim() === ''}>{action}</Button>
      <Button testId={`${testId}-cancel`} variant="secondary" onClick={onCancel}>{t('app.cancel')}</Button>
    </form>
  );
}

/** The reference of a target state: its label and date, where its source stands, the comparison, its refresh by a facilitator who sees the source. */
function TargetState({ view, facilitator, writing, onRefresh }: {
  view: SnapshotsView; facilitator: boolean; writing: StreamWriting; onRefresh: (label: string) => Promise<void>;
}) {
  const { t } = useTranslation();
  const [refreshing, setRefreshing] = useState(false);
  const baseline = view.baseline;
  const state = view.source?.state ?? 'missing';
  const reason = state === 'hidden' ? t('target.source_hidden') : state === 'missing' ? t('target.source_missing') : !facilitator ? t('target.refresh_role') : null;
  return (
    <section className="stream-section" data-testid="target-state">
      <h3>{t('target.title')}</h3>
      {baseline !== null && <p className="stream-basis" data-testid="target-reference">{t('target.reference', { label: baseline.label, date: snapshotDate(baseline.created_at, writing.language) })}</p>}
      {state === 'visible' && view.source?.code != null && <p className="stream-basis" data-testid="target-source">{t('target.source', { code: view.source.code })}</p>}
      {state !== 'visible' && <p className="analysis-ask" data-testid="target-source-reason">{reason}</p>}
      {view.comparison !== null && <Comparison changes={view.comparison} before={t('target.before')} writing={writing} testId="target-comparison" />}
      {refreshing
        ? <LabelForm initial={baseline?.label ?? ''} action={t('target.refresh_button')} testId="target-refresh-form"
          onSend={async (label) => { await onRefresh(label); setRefreshing(false); }} onCancel={() => { setRefreshing(false); }} />
        : (
          <Button testId="target-refresh" icon={RefreshCw} disabled={reason !== null} title={reason ?? t('target.refresh_help')} onClick={() => { setRefreshing(true); }}>
            {t('target.refresh')}
          </Button>
        )}
    </section>
  );
}

/** A photo: its label and date, its comparison with now on demand, its removal by a facilitator. */
function Photo({ photo, facilitator, writing, onDelete }: { photo: SnapshotView; facilitator: boolean; writing: StreamWriting; onDelete: () => void }) {
  const { t } = useTranslation();
  const [open, setOpen] = useState(false);
  return (
    <li className="snapshot" data-testid={`snapshot-${photo.id}`}>
      <span className="snapshot-head">
        <span className="snapshot-label">{photo.label}</span>
        <span className="snapshot-date">{snapshotDate(photo.created_at, writing.language)}</span>
        <button type="button" className="icon-button" aria-pressed={open} aria-label={t('snapshots.compare')} title={t('snapshots.compare')}
          data-testid={`snapshot-compare-${photo.id}`} onClick={() => { setOpen(!open); }}>
          <Icon icon={GitCompareArrows} size={14} />
        </button>
        {facilitator && (
          <button type="button" className="icon-button" aria-label={t('snapshots.delete', { label: photo.label })} title={t('snapshots.delete', { label: photo.label })}
            data-testid={`snapshot-delete-${photo.id}`} onClick={onDelete}>
            <Icon icon={Trash2} size={14} />
          </button>
        )}
      </span>
      {open && <Comparison changes={photo.changes} before={t('snapshots.before')} writing={writing} testId={`snapshot-comparison-${photo.id}`} />}
    </li>
  );
}

/**
 * The photos of the project and, for a target state, its reference: read when the main map is shown and again when it
 * changes, since the indicators of now move with it. A refusal of the host is said in the section.
 */
export function SnapshotsSection({ map, writing }: { map: MapView; writing: StreamWriting }) {
  const { t } = useTranslation();
  const [view, setView] = useState<SnapshotsView | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [taking, setTaking] = useState(false);
  const projectId = map.project_id;
  const facilitator = map.rights.role === 'facilitator';
  useEffect(() => {
    let alive = true;
    api.get('/api/projects/{id}/snapshots', { path: { id: projectId } }).then((read) => { if (alive) setView(read); }, (failure: unknown) => {
      if (alive) setError(messageOf(failure));
    });
    return () => {
      alive = false;
    };
  }, [projectId, map.version, map.structure_version]);
  const act = async (write: () => Promise<SnapshotsView>) => {
    setError(null);
    try {
      setView(await write());
    } catch (failure) {
      setError(messageOf(failure));
      throw failure;
    }
  };
  const take = (label: string) => act(() => api.post('/api/projects/{id}/snapshots', { path: { id: projectId }, body: { label } }));
  const refresh = (label: string) => act(() => api.post('/api/projects/{id}/baseline', { path: { id: projectId }, body: { label } }));
  const remove = (photo: SnapshotView) => {
    void act(() => api.delete('/api/projects/{id}/snapshots/{snapshotId}', { path: { id: projectId, snapshotId: photo.id } })).catch(() => undefined);
  };
  if (view === null) return error === null ? null : <p className="message-error" role="alert" data-testid="snapshots-error">{error}</p>;
  return (
    <>
      {/* A target state imported where its source was not found keeps its reference: shown, its source said missing. */}
      {(view.baseline !== null || view.baseline_project_id !== null) && <TargetState view={view} facilitator={facilitator} writing={writing} onRefresh={refresh} />}
      <section className="stream-section" data-testid="snapshots">
        <h3>{t('snapshots.title')}</h3>
        <p className="stream-basis">{t('snapshots.help')}</p>
        {view.photos.length === 0
          ? <p className="stream-none" data-testid="snapshots-none">{t('snapshots.none')}</p>
          : (
            <ul className="snapshots">
              {view.photos.map((photo) => <Photo key={photo.id} photo={photo} facilitator={facilitator} writing={writing} onDelete={() => { remove(photo); }} />)}
            </ul>
          )}
        {facilitator && (taking
          ? <LabelForm initial="" action={t('snapshots.take_button')} testId="snapshot-take-form"
            onSend={async (label) => { await take(label); setTaking(false); }} onCancel={() => { setTaking(false); }} />
          : <Button testId="snapshot-take" icon={Camera} onClick={() => { setTaking(true); }}>{t('snapshots.take')}</Button>)}
        {error !== null && <p className="message-error" role="alert" data-testid="snapshots-error">{error}</p>}
      </section>
    </>
  );
}
