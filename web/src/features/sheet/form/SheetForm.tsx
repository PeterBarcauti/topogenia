// SheetForm.tsx
// The form of the sheet in its sections, each folded when the sheet opens with its summary in its head (essai J10, asked by the project owner; before, annex B 4, B-4-21): the key facts (actor, purpose, frequency, duration and its bounds, lead time, Makigami M5), the inputs, the procedure, the outputs, the pain points of the step without a document (R10), the tools as pills.

import { RotateCcw } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Field } from '../../../shared/ui/Field.tsx';
import { Icon } from '../../../shared/ui/Icon.tsx';
import { InputList } from '../documents/InputList.tsx';
import { OutputList } from '../documents/OutputList.tsx';
import { hasStepPainPoints, StepPainPoints } from '../documents/StepPainPoints.tsx';
import { FoldAll, FoldSection } from '../FoldSection.tsx';
import { inputGroups } from '../model/documents.ts';
import { boundOutOfOrder, citedTools, FREQUENCIES, LEAD_TIME_UNITS, numberOf, type FrequencyCode, type LeadTimeUnit, type SheetDraft } from '../model/draft.ts';
import { factsText } from '../model/durationText.ts';
import { SHEET_SECTIONS, useSheet } from '../SheetContext.tsx';
import { ToolPills } from '../ToolPills.tsx';
import { Procedure, TOOLS_LIST } from './Procedure.tsx';
import { ToolsField } from './ToolsField.tsx';

/**
 * The lead time of a run, after its duration: a number from 0 to 100 000 and its unit, minutes, hours or working days;
 * it does not count in the completeness of the sheet (spec 14.2, S3).
 */
function LeadTime({ set }: { set: (part: Partial<SheetDraft>) => void }) {
  const { t } = useTranslation();
  const { draft } = useSheet();
  return (
    <Field label={t('sheet.lead_time')} hint={t('sheet.lead_time_help')} testId="sheet-lead-time">
      {(control) => (
        <span className="input-with-suffix">
          <input {...control} type="number" min={0} max={100000} step={0.01} value={draft.leadTime} onChange={(event) => { set({ leadTime: event.target.value }); }} />
          <select value={draft.leadTimeUnit} aria-label={t('sheet.lead_time_unit')} data-testid="sheet-lead-time-unit"
            onChange={(event) => { set({ leadTimeUnit: event.target.value as LeadTimeUnit }); }}>
            {LEAD_TIME_UNITS.map((unit) => <option key={unit} value={unit}>{t(`sheet.lead_time_units.${unit}`)}</option>)}
          </select>
        </span>
      )}
    </Field>
  );
}

/**
 * The frequency in its list, and the duration per run in minutes, from 0 to 100 000 (B-4-26, B-4-28): any step, so that the
 * decimals the core keeps are valid, the arrows still by 1 (audit 3.1 V4, A1).
 */
function FrequencyAndDuration({ set }: { set: (part: Partial<SheetDraft>) => void }) {
  const { t } = useTranslation();
  const { draft } = useSheet();
  return (
    <div className="two">
      <Field label={t('sheet.frequency')} testId="sheet-frequency">
        {(control) => (
          <select {...control} value={draft.frequency ?? ''} onChange={(event) => { set({ frequency: event.target.value === '' ? null : event.target.value as FrequencyCode }); }}>
            <option value="">{t('sheet.frequency_none')}</option>
            {FREQUENCIES.map((frequency) => <option key={frequency} value={frequency}>{t(`map.frequencies.${frequency}`)}</option>)}
          </select>
        )}
      </Field>
      <Field label={t('sheet.duration')} testId="sheet-duration">
        {(control) => (
          <span className="input-with-suffix">
            <input {...control} type="number" min={0} max={100000} step="any" value={draft.duration} onChange={(event) => { set({ duration: event.target.value }); }} />
            <span className="input-suffix">{t('sheet.minutes')}</span>
          </span>
        )}
      </Field>
    </div>
  );
}

/**
 * The shortest and the longest duration of a run around its duration, in minutes, each optional (Makigami M5); a bound
 * out of order is said on its field before the host refuses it. They do not count in the completeness; any step, as the duration.
 */
function DurationBounds({ set }: { set: (part: Partial<SheetDraft>) => void }) {
  const { t } = useTranslation();
  const { draft } = useSheet();
  const fault = boundOutOfOrder(draft);
  const bound = (key: 'durationMin' | 'durationMax', label: string, testId: string, side: 'min' | 'max') => (
    <Field label={label} testId={testId} error={fault === side ? t('sheet.duration_bounds_order') : undefined}>
      {(control) => (
        <span className="input-with-suffix">
          <input {...control} type="number" min={0} max={100000} step="any" value={draft[key]} onChange={(event) => { set({ [key]: event.target.value }); }} />
          <span className="input-suffix">{t('sheet.minutes')}</span>
        </span>
      )}
    </Field>
  );
  return (
    <div className="two" data-testid="sheet-duration-bounds">
      {bound('durationMin', t('sheet.duration_min'), 'sheet-duration-min', 'min')}
      {bound('durationMax', t('sheet.duration_max'), 'sheet-duration-max', 'max')}
    </div>
  );
}

/** « Reprendre « X » (étape CODE) » while the frequency is empty (B-4-27). */
function FrequencyResume({ set }: { set: (part: Partial<SheetDraft>) => void }) {
  const { t } = useTranslation();
  const { view, draft } = useSheet();
  const offered = view.suggestions.frequency;
  if (offered === null || draft.frequency !== null) return null;
  return (
    <button type="button" className="resume" data-testid="sheet-frequency-resume" onClick={() => { set({ frequency: offered.frequency }); }}>
      <Icon icon={RotateCcw} size={13} />
      {t('sheet.suggestion.frequency', { frequency: t(`map.frequencies.${offered.frequency}`), code: offered.step_code })}
    </button>
  );
}

/** The key facts typed, in a line for the head of their section: the frequency, the duration and the lead time, those entered. */
function useFactsSummary(draft: SheetDraft): string | null {
  const { t, i18n } = useTranslation();
  const lead = numberOf(draft.leadTime);
  return factsText({ frequency: draft.frequency, duration_minutes: numberOf(draft.duration), lead_time: lead === null ? null : { value: lead, unit: draft.leadTimeUnit } }, t, i18n.language);
}

export function SheetForm() {
  const { t } = useTranslation();
  const { view, draft, edit, props } = useSheet();
  const set = (part: Partial<SheetDraft>) => { edit({ ...draft, ...part }); };
  const count = (key: 'documents' | 'operations', value: number) => (value === 0 ? t('sheet.empty') : t(`sheet.${key}`, { count: value }));
  const operations = draft.procedure.filter((line) => line.text.trim() !== '').length;
  const facts = useFactsSummary(draft);
  const tools = citedTools(draft.tools);
  // « Tout déplier » acts on the sections drawn: the pain points of the step only when it holds one.
  const sections = SHEET_SECTIONS.filter((section) => section !== 'pain_points' || hasStepPainPoints(view));
  return (
    <div className="sheet-form" data-testid="sheet-form">
      <FoldAll sections={sections} />
      <FoldSection section="facts" title={t('sheet.facts')} summary={facts ?? t('sheet.empty')}>
        <Field label={t('sheet.actor')} hint={t('sheet.actor_help')} testId="sheet-actor">
          {(control) => <input {...control} type="text" maxLength={4000} placeholder={props.step.laneName} value={draft.actor} onChange={(event) => { set({ actor: event.target.value }); }} />}
        </Field>
        <Field label={t('sheet.purpose')} hint={t('sheet.purpose_help')} testId="sheet-purpose">
          {(control) => <textarea {...control} rows={2} maxLength={4000} value={draft.purpose} onChange={(event) => { set({ purpose: event.target.value }); }} />}
        </Field>
        <FrequencyAndDuration set={set} />
        <DurationBounds set={set} />
        <LeadTime set={set} />
        <FrequencyResume set={set} />
      </FoldSection>
      <FoldSection section="inputs" title={t('sheet.inputs')} summary={count('documents', inputGroups(view.inputs, view.permissions.fill).length)} hint={t('sheet.inputs_help')}>
        <InputList />
      </FoldSection>
      <FoldSection section="procedure" title={t('sheet.procedure')} small={t('sheet.procedure_small')} summary={count('operations', operations)} hint={t('sheet.procedure_help')}>
        <Procedure lines={draft.procedure} onChange={(procedure) => { set({ procedure }); }} />
      </FoldSection>
      <FoldSection section="outputs" title={t('sheet.outputs')} summary={count('documents', view.outputs.length)} hint={t('sheet.outputs_help')}>
        <OutputList />
      </FoldSection>
      <StepPainPoints />
      <FoldSection section="tools" title={t('sheet.tools')} summary={tools.length === 0 ? t('sheet.empty') : <ToolPills tools={tools} />} hint={t('sheet.tools_help')}>
        <ToolsField value={draft.tools} suggestions={view.suggestions.tools} onChange={(value) => { set({ tools: value }); }} />
      </FoldSection>
      <datalist id={TOOLS_LIST}>{view.suggestions.tools.map((tool) => <option key={tool} value={tool} />)}</datalist>
    </div>
  );
}
