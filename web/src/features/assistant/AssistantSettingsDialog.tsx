// AssistantSettingsDialog.tsx
// The dialog « Paramètres de l'IA » of the administrator: the connector chosen in the list and its fields, the model of each connector its own (UX-MAJ-03), the classifications that may be sent, set whatever the connector as they decide the online voices of a presentation too (F-137), the test of the connection, saved with the version read (spec 3.0 section 4).

import { useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { messageOf } from '../../shared/i18n/errorMessage.ts';
import { useUnsavedForm } from '../../shared/state/useUnsavedForm.ts';
import { Button } from '../../shared/ui/Button.tsx';
import { Dialog } from '../../shared/ui/Dialog.tsx';
import { notify } from '../../shared/ui/toasts.ts';
import { AwsFields, AzureField, ClassificationsField, ConnectorChoice, KeyField, ModelFields, PortField, TestOutcome } from './AssistantFields.tsx';
import {
  CALLS_A_MODEL, MODELS, ON_THIS_MACHINE, TAKES_A_KEY, changed, formOf, readSettings, saveSettings, testConnection, type AssistantForm, type AssistantSettingsView,
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

/**
 * A connector chosen: its model is the first one proposed for it, none without a list; the model of the one left, typed
 * or not, stays behind with it (a name of a model of one provider is never one of another, audit 3.2, UX-MAJ-03). What
 * belongs to the connector left stays behind too: the key typed and the removal asked of the key kept (a key belongs to
 * its provider and never goes to another, ADR 098), the port of a program of this machine.
 */
function withConnector(form: AssistantForm, connector: string): AssistantForm {
  if (connector === form.connector) return form;
  return { ...form, connector, model: MODELS[connector]?.[0] ?? '', key: '', removeKey: false, port: '' };
}

/**
 * The choice of a connector in the dialog: the model left with each connector is kept while the dialog is open, and
 * given back when that connector is chosen again, so that nothing typed is lost (UX-MAJ-03).
 */
function useConnectorChoice(): (form: AssistantForm, connector: string) => AssistantForm {
  const left = useRef<Partial<Record<string, string>>>({});
  return (form, connector) => {
    if (connector === form.connector) return form;
    left.current[form.connector] = form.model;
    const chosen = withConnector(form, connector);
    return { ...chosen, model: left.current[connector] ?? chosen.model };
  };
}

export function AssistantSettingsDialog({ onClose }: { onClose: () => void }) {
  const { t } = useTranslation();
  const { view, form, setForm, failure, setFailure } = useSettings();
  const choose = useConnectorChoice();
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
  // A setting changed and not saved is a form not validated, for « Quit » (R12).
  useUnsavedForm(view !== null && form !== null && changed(view, form));
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
          <ConnectorChoice connectors={view.connectors} value={form.connector} onChange={(connector) => { update(choose(form, connector)); }} />
          {TAKES_A_KEY.includes(form.connector) && <KeyField form={form} hint={view.connector === form.connector ? view.secret_hint : null} onChange={update} />}
          {form.connector === 'azure_open_ai' && <AzureField form={form} onChange={update} />}
          {ON_THIS_MACHINE.includes(form.connector) && <PortField form={form} onChange={update} />}
          {form.connector === 'bedrock' && <AwsFields form={form} onChange={update} />}
          {calls && <ModelFields form={form} onChange={update} />}
          <ClassificationsField allowed={form.allowed} onChange={update} />
          {outcome !== null && <TestOutcome outcome={outcome} profile={form.awsProfile} />}
        </>
      )}
      {failure !== null && <p className="message-error" role="alert" data-testid="assistant-error">{failure}</p>}
    </Dialog>
  );
}
