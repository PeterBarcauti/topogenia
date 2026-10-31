// AssistantFields.tsx
// The parts of the dialog of the settings of the assistant: the list of the connectors, the fields of each one, the classifications that may be sent with their warnings, the account of a test (spec 3.0 sections 4, 6).

import { useId } from 'react';
import { useTranslation } from 'react-i18next';
import { Field } from '../../shared/ui/Field.tsx';
import { CLASSIFICATIONS, type Classification } from '../project/classification.ts';
import { MODELS, type AssistantForm, type AssistantTestOutcome } from './assistantSettings.ts';

type Change = (change: Partial<AssistantForm>) => void;

/** The list of the connectors, as radio buttons with what each one sends out. */
export function ConnectorChoice({ connectors, value, onChange }: { connectors: readonly string[]; value: string; onChange: (connector: string) => void }) {
  const { t } = useTranslation();
  const name = useId();
  return (
    <fieldset className="field choice-list" data-testid="assistant-connector">
      <legend className="field-label">{t('assistant.connector')}</legend>
      {connectors.map((connector) => (
        <label key={connector} className="check">
          <input type="radio" name={name} value={connector} checked={value === connector} data-testid={`assistant-connector-${connector}`}
            onChange={() => { onChange(connector); }} />
          <span>
            <strong>{t(`assistant.connectors.${connector}`)}</strong>{' '}
            <span className="field-hint">· {t(`assistant.connector_help.${connector}`)}</span>
          </span>
        </label>
      ))}
    </fieldset>
  );
}

/** The key of the API of Anthropic, typed only: the one kept is named by its last characters, never shown. */
export function KeyField({ form, hint, onChange }: { form: AssistantForm; hint: string | null; onChange: Change }) {
  const { t } = useTranslation();
  return (
    <>
      <Field label={t('assistant.key')} testId="assistant-key" hint={hint === null ? t('assistant.key_none') : t('assistant.key_kept', { hint })}>
        {(control) => <input {...control} type="password" autoComplete="off" spellCheck={false} value={form.key} disabled={form.removeKey}
          onChange={(event) => { onChange({ key: event.target.value }); }} />}
      </Field>
      {hint !== null && (
        <label className="check">
          <input type="checkbox" checked={form.removeKey} data-testid="assistant-remove-key" onChange={(event) => { onChange({ removeKey: event.target.checked, key: '' }); }} />
          <span>{t('assistant.remove_key')}</span>
        </label>
      )}
    </>
  );
}

/** The region and the named profile of AWS (Bedrock): the profile of the SSO of the machine, or none on a server. */
export function AwsFields({ form, onChange }: { form: AssistantForm; onChange: Change }) {
  const { t } = useTranslation();
  return (
    <div className="form-row">
      <Field label={t('assistant.region')} testId="assistant-region" hint={t('assistant.region_help')}>
        {(control) => <input {...control} value={form.region} spellCheck={false} maxLength={40} onChange={(event) => { onChange({ region: event.target.value }); }} />}
      </Field>
      <Field label={t('assistant.profile')} testId="assistant-profile" hint={t('assistant.profile_help')}>
        {(control) => <input {...control} value={form.awsProfile} spellCheck={false} maxLength={100} onChange={(event) => { onChange({ awsProfile: event.target.value }); }} />}
      </Field>
    </div>
  );
}

/** The model, proposed from the list of the connector, any other identifier typed; the ceiling of a reply. */
export function ModelFields({ form, onChange }: { form: AssistantForm; onChange: Change }) {
  const { t } = useTranslation();
  const list = useId();
  return (
    <div className="form-row">
      <Field label={t('assistant.model')} testId="assistant-model" hint={t(`assistant.model_help.${form.connector}`)}>
        {(control) => <input {...control} list={list} value={form.model} spellCheck={false} maxLength={200} onChange={(event) => { onChange({ model: event.target.value }); }} />}
      </Field>
      <datalist id={list}>{(MODELS[form.connector] ?? []).map((model) => <option key={model} value={model} />)}</datalist>
      <Field label={t('assistant.ceiling')} testId="assistant-ceiling" hint={t('assistant.ceiling_help')}>
        {(control) => <input {...control} type="number" min={256} max={128000} step={256} value={form.maxOutputTokens}
          onChange={(event) => { onChange({ maxOutputTokens: Number(event.target.value) }); }} />}
      </Field>
    </div>
  );
}

/** The classifications of the projects that may be sent to the model; the more protected ones say what it means (spec 3.0 section 6). */
export function ClassificationsField({ allowed, onChange }: { allowed: readonly Classification[]; onChange: Change }) {
  const { t } = useTranslation();
  const toggle = (level: Classification, on: boolean) => {
    onChange({ allowed: CLASSIFICATIONS.filter((item) => (item === level ? on : allowed.includes(item))) });
  };
  return (
    <fieldset className="field choice-list" data-testid="assistant-classifications">
      <legend className="field-label">{t('assistant.classifications')}</legend>
      {CLASSIFICATIONS.map((level) => (
        <label key={level} className="check">
          <input type="checkbox" checked={allowed.includes(level)} data-testid={`assistant-allow-${level}`} onChange={(event) => { toggle(level, event.target.checked); }} />
          <span>{t(`classification.${level}`)}</span>
        </label>
      ))}
      {allowed.includes('confidential') && <p className="message-warning" data-testid="assistant-warning-confidential">{t('assistant.warning_confidential')}</p>}
      {allowed.includes('strictly_confidential') && <p className="message-warning" data-testid="assistant-warning-strict">{t('assistant.warning_strict')}</p>}
    </fieldset>
  );
}

/** The account of a test: the model and its time, or the reason it failed and what the provider said. */
export function TestOutcome({ outcome, profile }: { outcome: AssistantTestOutcome; profile: string }) {
  const { t, i18n } = useTranslation();
  if (outcome.succeeded) {
    const seconds = new Intl.NumberFormat(i18n.language, { minimumFractionDigits: 1, maximumFractionDigits: 1 }).format(outcome.duration_milliseconds / 1000);
    return (
      <p className="message-success" role="status" data-testid="assistant-tested">
        {t('assistant.tested', { model: outcome.model ?? '', seconds, input: outcome.input_tokens ?? 0, output: outcome.output_tokens ?? 0 })}
      </p>
    );
  }
  return (
    <div className="message-error" role="alert" data-testid="assistant-test-failed">
      <p>{t(`assistant.failures.${outcome.failure ?? 'other'}`, { profile })}</p>
      {outcome.detail !== null && <p className="muted">{outcome.detail}</p>}
    </div>
  );
}
