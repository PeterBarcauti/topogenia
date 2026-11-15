// StreamEntryField.tsx
// The floating field of an entry of the value stream at the point of its menu (spec 14.2, 14.6): the incoming volume of a start and its period, the share of an exit of a gateway with the remainder of the other exit of a decision, the headcount of a lane; Enter validates, Escape or a click elsewhere gives up, an empty field clears the value.

import { useEffect, useRef, useState, type SyntheticEvent } from 'react';
import { useTranslation } from 'react-i18next';
import type { Schema } from '../../../api/client.ts';
import { setHeadcount, setShare, setVolume } from '../state/actions/valueStream.ts';
import { useEditor } from '../state/EditorContext.tsx';
import type { StreamEntry } from '../state/floating.ts';

type MapView = Schema<'MapView'>;
type VolumePeriod = Schema<'VolumePeriod'>;

const PERIODS: readonly VolumePeriod[] = ['day', 'week', 'month', 'year'];

/** The bounds of each entry, those of the core (spec 14.2): a volume up to 1 000 000, a share up to 100, a headcount up to 100 000. */
const BOUNDS = { volume: { max: 1_000_000, step: 0.01 }, share: { max: 100, step: 0.01 }, headcount: { max: 100_000, step: 1 } } as const;

/** The number typed, null for an empty field (the value cleared). */
const typed = (text: string): number | null => (text.trim() === '' ? null : Number(text));

/** The value of the entry now, as the field starts with it. */
function initialOf(map: MapView, entry: StreamEntry): { value: string; period: VolumePeriod } {
  if (entry.kind === 'volume') {
    const volume = map.steps.find((step) => step.id === entry.stepId)?.volume ?? null;
    return { value: volume === null ? '' : String(volume.quantity), period: volume?.period ?? 'month' };
  }
  const value = entry.kind === 'share' ? map.flows.find((flow) => flow.id === entry.flowId)?.share_percent : map.lanes.find((lane) => lane.id === entry.laneId)?.headcount;
  return { value: value == null ? '' : String(value), period: 'month' };
}

/**
 * The remainder of the other exit of a decision while a share is typed, when that exit is the only one without a share
 * (spec 14.6): its label, or the code of its target, and what it keeps.
 */
export function remainderOf(map: MapView, flowId: string, share: number | null): { name: string; value: number } | null {
  const flow = map.flows.find((candidate) => candidate.id === flowId);
  const source = map.steps.find((step) => step.id === flow?.source_step_id);
  if (flow === undefined || source?.type !== 'decision') return null;
  const others = map.flows.filter((candidate) => candidate.source_step_id === flow.source_step_id && candidate.id !== flowId);
  const open = others.filter((candidate) => (candidate.share_percent ?? null) === null);
  const [other] = open;
  if (open.length !== 1 || other === undefined) return null;
  const entered = others.reduce((sum, candidate) => sum + (candidate.share_percent ?? 0), 0);
  const name = other.label ?? map.steps.find((step) => step.id === other.target_step_id)?.code ?? '';
  return { name, value: Math.round((100 - (share ?? 0) - entered) * 100) / 100 };
}

export function StreamEntryField({ map, entry, at, onClose }: { map: MapView; entry: StreamEntry; at: { x: number; y: number }; onClose: () => void }) {
  const { t, i18n } = useTranslation();
  const editor = useEditor();
  const initial = initialOf(map, entry);
  const [value, setValue] = useState(initial.value);
  const [period, setPeriod] = useState<VolumePeriod>(initial.period);
  const field = useRef<HTMLInputElement>(null);
  // Opened by the person's own gesture, the menu: the field takes the focus at once.
  useEffect(() => {
    field.current?.focus();
    field.current?.select();
  }, []);
  const submit = (event: SyntheticEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (!event.currentTarget.reportValidity()) return;
    onClose();
    const number = typed(value);
    if (entry.kind === 'volume') void setVolume(editor, entry.stepId, number === null ? null : { quantity: number, period });
    else if (entry.kind === 'share') void setShare(editor, entry.flowId, number);
    else void setHeadcount(editor, entry.laneId, number);
  };
  const remainder = entry.kind === 'share' ? remainderOf(map, entry.flowId, typed(value)) : null;
  const bounds = BOUNDS[entry.kind];
  return (
    <form className="floating-field stream-entry" style={{ left: Math.max(0, Math.min(at.x, window.innerWidth - 300)), top: at.y }} data-testid={`stream-entry-${entry.kind}`}
      onSubmit={submit} onBlur={(event) => {
        if (!event.currentTarget.contains(event.relatedTarget)) onClose();
      }}
      onKeyDown={(event) => {
        if (event.key === 'Escape') onClose();
        // Enter validates from the field and from the period alike: a form of a number field is not submitted by every browser.
        if (event.key === 'Enter') {
          event.preventDefault();
          event.currentTarget.requestSubmit();
        }
      }}>
      <label>
        <span>{t(`stream.entry_form.${entry.kind}`)}</span>
        <span className="stream-entry-row">
          <input ref={field} type="number" inputMode="decimal" min={0} max={bounds.max} step={bounds.step} value={value} lang={i18n.language}
            data-testid="stream-entry-input" onChange={(event) => { setValue(event.target.value); }} />
          {entry.kind === 'volume' && (
            <select value={period} aria-label={t('stream.entry_form.period')} data-testid="stream-entry-period" onChange={(event) => { setPeriod(event.target.value as VolumePeriod); }}>
              {PERIODS.map((code) => <option key={code} value={code}>{t(`stream.entry_form.periods.${code}`)}</option>)}
            </select>
          )}
        </span>
      </label>
      {remainder !== null && <p className="stream-entry-help" data-testid="stream-entry-remainder">{t('stream.entry_form.remainder', { name: remainder.name, value: remainder.value })}</p>}
      <p className="stream-entry-help">{t('stream.entry_form.clear')}</p>
    </form>
  );
}
