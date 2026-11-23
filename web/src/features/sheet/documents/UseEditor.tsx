// UseEditor.tsx
// The use of a ticked input: its wait, its share usable as is (%C&A) and the pain point noted where it is suffered (always offered, F-118), raised on the producing step, its status once the host holds it (ADR 116); sent on leaving a field, the fields open while the host answers (annex B 4).

import { useEffect, useRef, useState, type KeyboardEvent } from 'react';
import { Flame } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import type { Schema } from '../../../api/client.ts';
import { Icon } from '../../../shared/ui/Icon.tsx';
import { currentUse, bodyOfUse, entryOfUse, retyped, type UseEntry } from '../model/documents.ts';
import { useSheet } from '../SheetContext.tsx';
import { describeUse, setPainPointStatus } from './documentWrites.ts';
import { PainPointStatusField } from './PainPointStatusField.tsx';

/** Enter leaves the field, which sends the use (B-4-75). */
const leaveOnEnter = (event: KeyboardEvent<HTMLElement>) => {
  if (event.key === 'Enter' && event.target instanceof HTMLElement) event.target.blur();
};

/**
 * A field of the use: its value typed, open while the host answers, so that the next field takes the focus and the typing
 * (audit 3.1 V3, U1); the use leaves when the field is left, and only when something changed (B-4-75), after the writes
 * of the documents asked before it.
 */
function useFields(input: Schema<'ProposedInput'>, painPoint: Schema<'PainPoint'> | null) {
  const sheet = useSheet();
  const initial = entryOfUse(input, painPoint);
  const [entry, setEntry] = useState(initial);
  // The fields as the last use that left held them, or as the host gave them: a field that differs was typed since.
  const [sent, setSent] = useState(initial);
  // The writes of the use asked and not answered yet.
  const [writing, setWriting] = useState(0);
  // A new use from the host, once every write of the use is answered: the fields left as they were sent take it, adjusted
  // while drawing; a field typed since stays.
  const known = JSON.stringify(initial);
  const [seen, setSeen] = useState(known);
  if (known !== seen && writing === 0) {
    setSeen(known);
    setEntry(retyped(entry, sent, initial));
    setSent(initial);
  }
  const send = () => {
    const body = bodyOfUse(entry);
    // Compared with the use that left while it is not answered, else with the use of the host.
    const reference = writing > 0 ? bodyOfUse(sent) : currentUse(input, painPoint);
    if (JSON.stringify(body) === JSON.stringify(reference)) return;
    setSent(entry);
    setWriting((count) => count + 1);
    void describeUse(sheet, input.document.id, body).then((written) => {
      // Refused: the host still holds the use it held when this one left, the typing differs from it and stays.
      if (!written) setSent(initial);
      setWriting((count) => count - 1);
    });
  };
  const field = (name: keyof UseEntry) => ({
    value: entry[name], onBlur: send, 'data-testid': `use-${name}-${input.document.id}`,
    onChange: (event: { target: { value: string } }) => { setEntry((current) => ({ ...current, [name]: event.target.value })); },
  });
  return { entry, field };
}

/** The action plan of the pain point (Makigami M7): the action, who carries it, the day it is due; each optional. */
function ActionPlan({ name, field }: { name: string; field: ReturnType<typeof useFields>['field'] }) {
  const { t } = useTranslation();
  return (
    <div className="action-plan" data-testid="use-action-plan">
      <label className="field">
        <span className="field-label">{t('sheet.pain_point.action')}</span>
        <input className="field-control" type="text" maxLength={500} aria-label={t('sheet.pain_point.action_of', { name })} onKeyDown={leaveOnEnter} {...field('action')} />
      </label>
      <div className="two">
        <label className="field">
          <span className="field-label">{t('sheet.pain_point.owner')}</span>
          <input className="field-control" type="text" maxLength={150} aria-label={t('sheet.pain_point.owner_of', { name })} onKeyDown={leaveOnEnter} {...field('owner')} />
        </label>
        <label className="field">
          <span className="field-label">{t('sheet.pain_point.due')}</span>
          <input className="field-control" type="date" aria-label={t('sheet.pain_point.due_of', { name })} onKeyDown={leaveOnEnter} {...field('due')} />
        </label>
      </div>
    </div>
  );
}

export function UseEditor({ input, painPoint }: { input: Schema<'ProposedInput'>; painPoint: Schema<'PainPoint'> | null }) {
  const { t } = useTranslation();
  const sheet = useSheet();
  const { entry, field } = useFields(input, painPoint);
  const name = input.document.name;
  // Asked from the outputs of the producer (F-118): the pain point of this document takes the caret as the sheet is
  // drawn; the panel forgets the request then, taken or not (R4-10).
  const wanted = sheet.props.painFocus === input.document.id;
  const area = useRef<HTMLTextAreaElement>(null);
  useEffect(() => {
    if (wanted) area.current?.focus();
  }, [wanted]);
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
      <div className="irritant" data-testid={`use-pain-${input.document.id}`}>
        <label className="field">
          <span className="field-label"><Icon icon={Flame} size={12} />{painPoint === null ? t('sheet.pain_point.title') : t('sheet.pain_point.title_code', { code: painPoint.code })}</span>
          <textarea ref={area} className="field-control" rows={2} maxLength={2000} placeholder={t('sheet.pain_point.example')} aria-label={t('sheet.pain_point.of', { name })} {...field('painPoint')} />
        </label>
        <span className="field-hint">{t('sheet.pain_point.help', { code: input.producer?.code ?? '' })}</span>
        {entry.painPoint.trim() !== '' && (
          <>
            <label className="lost">
              {t('sheet.pain_point.lost')}
              <input className="field-control" type="number" min={0} max={99999} step={0.5} aria-label={t('sheet.pain_point.lost_of', { name })} onKeyDown={leaveOnEnter} {...field('lost')} />
              {t('sheet.pain_point.lost_unit')}
            </label>
            {painPoint !== null && (
              <PainPointStatusField painPoint={painPoint} testId={`use-status-${painPoint.id}`} write={(status) => setPainPointStatus(sheet, painPoint.id, status)} />
            )}
            <ActionPlan name={name} field={field} />
          </>
        )}
      </div>
    </div>
  );
}
