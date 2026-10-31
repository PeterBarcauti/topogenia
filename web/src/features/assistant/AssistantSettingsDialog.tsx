// AssistantSettingsDialog.tsx
// The dialog « Paramètres de l'IA » of the administrator: the connector chosen in the list and its fields, the classifications that may be sent, the test of the connection, saved with the version read (spec 3.0 section 4).

import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { messageOf } from '../../shared/i18n/errorMessage.ts';
import { Button } from '../../shared/ui/Button.tsx';
import { Dialog } from '../../shared/ui/Dialog.tsx';
import { notify } from '../../shared/ui/toasts.ts';
import { AwsFields, ClassificationsField, ConnectorChoice, KeyField, ModelFields, TestOutcome } from './AssistantFields.tsx';
import {
  CALLS_A_MODEL, MODELS, changed, formOf, readSettings, saveSettings, testConnection, type AssistantForm, type AssistantSettingsView,
  type AssistantTestOutcome,
} from './assistantSettings.ts';

/** The settings read when the dialog opens: the form and the version a Save sends are those of the moment. */
function useSettings() {
  const [view, setView] = useState<AssistantSettingsView | null>(null);
  const [form, setForm] = useState<AssistantForm | null>(null);
  const [failure, setFailure] = useState<string | null>(null);
  useEffect(() => {
    const controller = new AbortController();
    readSettings(controller.signal).then((read) => {
      setView(read);
      setForm(formOf(read));
    }, (error: unknown) => {
      if (!controller.signal.aborted) setFailure(messageOf(error));
    });
    return () => {
      controller.abort();
    };
  }, []);
  return { view, form, setForm, failure, setFailure };
}

/** A connector chosen: the model of the one left gives way to the first model of the new one. */
function withConnector(form: AssistantForm, connector: string): AssistantForm {
  const known = Object.values(MODELS).flat();
  const model = form.model === '' || known.includes(form.model) ? (MODELS[connector]?.[0] ?? '') : form.model;
  return { ...form, connector, model };
}

export function AssistantSettingsDialog({ onClose }: { onClose: () => void }) {
  const { t } = useTranslation();
  const { view, form, setForm, failure, setFailure } = useSettings();
  const [sending, setSending] = useState(false);
  const [outcome, setOutcome] = useState<AssistantTestOutcome | null>(null);
  const update = (change: Partial<AssistantForm>) => {
    setOutcome(null);
    setForm((current) => (current === null ? current : { ...current, ...change }));
  };
  const run = async (action: () => Promise<void>) => {
    setSending(true);
    setFailure(null);
    try {
      await action();
    } catch (error) {
      setFailure(messageOf(error));
    } finally {
      setSending(false);
    }
  };
  const save = () => run(async () => {
    if (view === null || form === null) return;
    await saveSettings(form, view.version);
    notify(t('assistant.saved'), 'success');
    onClose();
  });
  const test = () => run(async () => {
    if (form !== null) setOutcome(await testConnection(form));
  });
  const calls = form !== null && CALLS_A_MODEL.includes(form.connector);
  const footer = (
    <>
      <Button testId="assistant-cancel" variant="secondary" onClick={onClose}>{t('app.cancel')}</Button>
      <Button testId="assistant-test" variant="secondary" disabled={sending || !calls} onClick={() => void test()}>{sending ? t('assistant.testing') : t('assistant.test')}</Button>
      <Button testId="assistant-save" variant="primary" disabled={sending || view === null || form === null || !changed(view, form)} onClick={() => void save()}>
        {t('assistant.save')}
      </Button>
    </>
  );
  return (
    <Dialog title={t('assistant.title')} testId="assistant-settings" onClose={onClose} footer={footer} wide>
      <p className="muted">{t('assistant.help')}</p>
      {view === null && failure === null && <p className="muted">{t('app.loading')}</p>}
      {view !== null && form !== null && (
        <>
          <ConnectorChoice connectors={view.connectors} value={form.connector} onChange={(connector) => { update(withConnector(form, connector)); }} />
          {form.connector === 'anthropic' && <KeyField form={form} hint={view.secret_hint} onChange={update} />}
          {form.connector === 'bedrock' && <AwsFields form={form} onChange={update} />}
          {calls && <ModelFields form={form} onChange={update} />}
          {calls && <ClassificationsField allowed={form.allowed} onChange={update} />}
          {outcome !== null && <TestOutcome outcome={outcome} profile={form.awsProfile} />}
        </>
      )}
      {failure !== null && <p className="message-error" role="alert" data-testid="assistant-error">{failure}</p>}
    </Dialog>
  );
}
