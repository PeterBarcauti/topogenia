// SheetForm.tsx
// The form of the sheet in the order of its rubrics: actor, purpose, inputs, procedure, outputs, tools, frequency, duration (annex B 4, B-4-21).

import { Plus, RotateCcw } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Field } from '../../../shared/ui/Field.tsx';
import { Icon } from '../../../shared/ui/Icon.tsx';
import { InputList } from '../documents/InputList.tsx';
import { OutputList } from '../documents/OutputList.tsx';
import { FoldSection } from '../FoldSection.tsx';
import { inputGroups } from '../model/documents.ts';
import { FREQUENCIES, LEAD_TIME_UNITS, offeredTools, withTool, type FrequencyCode, type LeadTimeUnit, type SheetDraft } from '../model/draft.ts';
import { useSheet } from '../SheetContext.tsx';
import { Procedure, TOOLS_LIST } from './Procedure.tsx';

/** The tools: a field of 4000 characters, and a pill « + outil » for each tool cited on the map and not here yet (B-4-24, B-4-25). */
function Tools({ set }: { set: (part: Partial<SheetDraft>) => void }) {
  const { t } = useTranslation();
  const { view, draft } = useSheet();
  const offered = offeredTools(view.suggestions.tools, draft.tools);
  return (
    <div className="sheet-tools">
      <Field label={t('sheet.tools')} hint={t('sheet.tools_help')} testId="sheet-tools">
        {(control) => <input {...control} type="text" maxLength={4000} value={draft.tools} onChange={(event) => { set({ tools: event.target.value }); }} />}
      </Field>
      {offered.length > 0 && (
        <div className="suggestions" role="group" aria-label={t('sheet.suggestion.tools')} data-testid="sheet-tool-suggestions">
          <span className="field-hint">{t('sheet.suggestion.tools')}</span>
          {offered.map((tool) => (
            <button key={tool} type="button" className="suggest" aria-label={t('sheet.suggestion.add_tool', { tool })} data-testid={`sheet-tool-suggestion-${tool}`}
              onClick={() => { set({ tools: withTool(draft.tools, tool) }); }}>
              <Icon icon={Plus} size={12} />{tool}
            </button>
          ))}
        </div>
      )}
    </div>
  );
}

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

/** The frequency in its list, and the duration per run in minutes, from 0 to 100 000 (B-4-26, B-4-28). */
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
            <input {...control} type="number" min={0} max={100000} step={1} value={draft.duration} onChange={(event) => { set({ duration: event.target.value }); }} />
            <span className="input-suffix">{t('sheet.minutes')}</span>
          </span>
        )}
      </Field>
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

export function SheetForm() {
  const { t } = useTranslation();
  const { view, draft, edit, props } = useSheet();
  const set = (part: Partial<SheetDraft>) => { edit({ ...draft, ...part }); };
  const count = (key: 'documents' | 'operations', value: number) => (value === 0 ? t('sheet.empty') : t(`sheet.${key}`, { count: value }));
  const operations = draft.procedure.filter((line) => line.text.trim() !== '').length;
  return (
    <div className="sheet-form" data-testid="sheet-form">
      <Field label={t('sheet.actor')} hint={t('sheet.actor_help')} testId="sheet-actor">
        {(control) => <input {...control} type="text" maxLength={4000} placeholder={props.step.laneName} value={draft.actor} onChange={(event) => { set({ actor: event.target.value }); }} />}
      </Field>
      <Field label={t('sheet.purpose')} hint={t('sheet.purpose_help')} testId="sheet-purpose">
        {(control) => <textarea {...control} rows={2} maxLength={4000} value={draft.purpose} onChange={(event) => { set({ purpose: event.target.value }); }} />}
      </Field>
      <div className="sheet-divider" />
      <FoldSection section="inputs" title={t('sheet.inputs')} summary={count('documents', inputGroups(view.inputs, view.permissions.fill).length)} hint={t('sheet.inputs_help')}>
        <InputList />
      </FoldSection>
      <FoldSection section="procedure" title={t('sheet.procedure')} small={t('sheet.procedure_small')} summary={count('operations', operations)} hint={t('sheet.procedure_help')}>
        <Procedure lines={draft.procedure} onChange={(procedure) => { set({ procedure }); }} />
      </FoldSection>
      <FoldSection section="outputs" title={t('sheet.outputs')} summary={count('documents', view.outputs.length)} hint={t('sheet.outputs_help')}>
        <OutputList />
      </FoldSection>
      <Tools set={set} />
      <FrequencyAndDuration set={set} />
      <LeadTime set={set} />
      <FrequencyResume set={set} />
      <datalist id={TOOLS_LIST}>{view.suggestions.tools.map((tool) => <option key={tool} value={tool} />)}</datalist>
    </div>
  );
}
