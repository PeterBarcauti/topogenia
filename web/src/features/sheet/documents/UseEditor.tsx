// UseEditor.tsx
// The use of a ticked input: its wait, its share usable as is (%C&A) and, under 100 %, the pain point that explains it, raised on the producing step; sent on leaving a field (annex B 4).

import { useState, type KeyboardEvent } from 'react';
import { Flame } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import type { Schema } from '../../../api/client.ts';
import { Icon } from '../../../shared/ui/Icon.tsx';
import { currentUse, showsPainPoint, bodyOfUse, entryOfUse, type UseEntry } from '../model/documents.ts';
import { useSheet } from '../SheetContext.tsx';
import { describeUse } from './documentWrites.ts';

/** Enter leaves the field, which sends the use (B-4-75). */
const leaveOnEnter = (event: KeyboardEvent<HTMLElement>) => {
  if (event.key === 'Enter' && event.target instanceof HTMLElement) event.target.blur();
};

/**
 * A field of the use: its value typed, kept while the host answers; the use leaves when the field is left, and only when
 * something changed (B-4-75).
 */
function useFields(input: Schema<'ProposedInput'>, painPoint: Schema<'PainPoint'> | null) {
  const sheet = useSheet();
  const initial = entryOfUse(input, painPoint);
  const [entry, setEntry] = useState(initial);
  // A new use from the host: the typing starts again from it, adjusted while drawing, never after a keystroke.
  const known = JSON.stringify(initial);
  const [seen, setSeen] = useState(known);
  if (known !== seen) {
    setSeen(known);
    setEntry(initial);
  }
  const send = () => {
    const body = bodyOfUse(entry);
    if (JSON.stringify(body) !== JSON.stringify(currentUse(input, painPoint))) void describeUse(sheet, input.document.id, body);
  };
  const field = (name: keyof UseEntry) => ({
    value: entry[name], disabled: sheet.documentBusy, onBlur: send, 'data-testid': `use-${name}-${input.document.id}`,
    onChange: (event: { target: { value: string } }) => { setEntry((current) => ({ ...current, [name]: event.target.value })); },
  });
  return { entry, field };
}

export function UseEditor({ input, painPoint }: { input: Schema<'ProposedInput'>; painPoint: Schema<'PainPoint'> | null }) {
  const { t } = useTranslation();
  const { entry, field } = useFields(input, painPoint);
  const name = input.document.name;
  return (
    <div className="usage" data-testid={`use-${input.document.id}`}>
      <label className="field">
        <span className="field-label">{t('sheet.document.wait_label')}</span>
        <span className="input-with-suffix">
          <input className="field-control" type="number" min={0} max={100000} step={0.5} aria-label={t('sheet.document.wait_of', { name })} onKeyDown={leaveOnEnter} {...field('wait')} />
          <span className="input-suffix">{t('sheet.document.hours')}</span>
        </span>
      </label>
      <label className="field" title={t('sheet.document.percent_help')}>
        <span className="field-label">{t('sheet.document.percent')}</span>
        <span className="input-with-suffix">
          <input className="field-control" type="number" min={0} max={100} step={5} aria-label={t('sheet.document.percent_of', { name })} onKeyDown={leaveOnEnter} {...field('percent')} />
          <span className="input-suffix">%</span>
        </span>
      </label>
      {showsPainPoint(entry, painPoint) && (
        <div className="irritant" data-testid={`use-pain-${input.document.id}`}>
          <label className="field">
            <span className="field-label"><Icon icon={Flame} size={12} />{painPoint === null ? t('sheet.pain_point.title') : t('sheet.pain_point.title_code', { code: painPoint.code })}</span>
            <textarea className="field-control" rows={2} maxLength={2000} placeholder={t('sheet.pain_point.example')} aria-label={t('sheet.pain_point.of', { name })} {...field('painPoint')} />
          </label>
          <span className="field-hint">{t('sheet.pain_point.help', { code: input.producer?.code ?? '' })}</span>
          {entry.painPoint.trim() !== '' && (
            <label className="lost">
              {t('sheet.pain_point.lost')}
              <input className="field-control" type="number" min={0} max={99999} step={0.5} aria-label={t('sheet.pain_point.lost_of', { name })} onKeyDown={leaveOnEnter} {...field('lost')} />
              {t('sheet.pain_point.lost_unit')}
            </label>
          )}
        </div>
      )}
    </div>
  );
}
