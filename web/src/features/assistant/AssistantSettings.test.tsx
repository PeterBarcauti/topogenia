// AssistantSettings.test.tsx
// The gear of the settings of the assistant, for the administrator alone, and its dialog: the connector chosen in the list and its fields, the key typed and never shown, the classifications and their warnings, the test of the connection, what a Save sends (spec 3.0 sections 4, 6).

import { act, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { Schema } from '../../api/client.ts';
import { App } from '../../app/App.tsx';
import { i18next } from '../../shared/i18n/i18n.ts';
import { toasts } from '../../shared/ui/toasts.ts';
import { callsTo, fakeApi, type FakeReply, type FakeRoutes } from '../../test/fakeApi.ts';
import { fakeSource, profileOf } from '../../test/profiles.ts';

const SETTINGS = '/api/admin/assistant';
const KEY = 'sk-ant-fictive-0000';

const view = (overrides: Partial<Schema<'AssistantSettingsView'>> = {}): FakeReply => ({
  json: {
    connector: 'none', model: null, region: null, aws_profile: null, max_output_tokens: 16000, allowed_classifications: ['internal'], has_secret: false,
    secret_hint: null, version: 0, updated_at: null, connectors: ['none', 'copy_paste', 'anthropic', 'bedrock'], ...overrides,
  },
});

const outcome = (overrides: Partial<Schema<'AssistantTestOutcome'>>): FakeReply => ({
  json: { succeeded: true, model: 'claude-opus-5-5', duration_milliseconds: 1250, input_tokens: 21, output_tokens: 4, failure: null, detail: null, ...overrides },
});

/** The application on the home page for the profile given, the settings of the assistant read from the routes given. */
async function start(routes: FakeRoutes, isAdmin = true) {
  const sent = fakeApi({ 'GET /api/projects': { json: [] }, 'GET /api/trash': { json: { retention_days: 30, projects: [] } }, ...routes });
  render(<App source={fakeSource([profileOf({ is_admin: isAdmin })]).source} />);
  await screen.findByTestId('app-header');
  return sent;
}

/** The dialog opened by the gear, once the settings are read. */
async function openDialog() {
  await userEvent.click(screen.getByTestId('header-assistant'));
  await screen.findByTestId('assistant-connector');
}

const sentBodies = (sent: Parameters<typeof callsTo>[0], method: string, path: string) => callsTo(sent, method, path).map((call) => call.body);

beforeEach(async () => {
  await i18next.changeLanguage('fr');
});

afterEach(() => {
  act(() => {
    toasts.setState({ toasts: [] });
  });
  vi.unstubAllGlobals();
  window.history.replaceState(null, '', '/');
});

describe('the gear of the settings of the assistant (spec 3.0 section 4)', () => {
  it('is in the header of the administrator, with its name', async () => {
    await start({});
    expect(screen.getByTestId('header-assistant')).toHaveAccessibleName('Paramètres de l’IA');
  });

  it('is not shown to a member who is not an administrator', async () => {
    await start({}, false);
    expect(screen.getByTestId('header-brand')).toBeInTheDocument();
    expect(screen.queryByTestId('header-assistant')).not.toBeInTheDocument();
  });
});

describe('the dialog of the settings of the assistant: the connectors and their fields (spec 3.0 section 4)', () => {
  it('lists the connectors of the contract, none chosen by default, with nothing to test nor to save', async () => {
    await start({ [`GET ${SETTINGS}`]: view() });
    await openDialog();
    for (const connector of ['none', 'copy_paste', 'anthropic', 'bedrock'])
      expect(screen.getByTestId(`assistant-connector-${connector}`)).toBeInTheDocument();
    expect(screen.getByTestId('assistant-connector-none')).toBeChecked();
    // The name of a connector and what it sends, separated: « Aucun · Rien ne part vers une IA. ».
    expect(screen.getByTestId('assistant-connector-none').closest('label')).toHaveTextContent(/^Aucun · Rien ne part vers une IA\.$/u);
    expect(screen.getByTestId('assistant-test')).toBeDisabled();
    expect(screen.getByTestId('assistant-save')).toBeDisabled();
    await userEvent.click(screen.getByTestId('assistant-connector-copy_paste'));
    expect(screen.getByTestId('assistant-test')).toBeDisabled();
    expect(screen.queryByTestId('assistant-model')).not.toBeInTheDocument();
  });

  it('Anthropic chosen: the default model proposed, the key typed sent once with the version read, then the dialog closes', async () => {
    const sent = await start({ [`GET ${SETTINGS}`]: view(), [`PUT ${SETTINGS}`]: view({ connector: 'anthropic', model: 'claude-opus-5-5', has_secret: true, secret_hint: '0000', version: 1 }) });
    await openDialog();
    await userEvent.click(screen.getByTestId('assistant-connector-anthropic'));
    expect(screen.getByTestId('assistant-model')).toHaveValue('claude-opus-5-5');
    expect(screen.getByTestId('assistant-key')).toHaveAttribute('type', 'password');
    expect(screen.queryByTestId('assistant-region')).not.toBeInTheDocument();
    await userEvent.type(screen.getByTestId('assistant-key'), KEY);
    await userEvent.click(screen.getByTestId('assistant-save'));
    await waitFor(() => { expect(screen.queryByTestId('assistant-settings')).not.toBeInTheDocument(); });
    expect(callsTo(sent, 'PUT', SETTINGS).map((call) => [call.body, call.headers.get('If-Match')])).toEqual([[{
      connector: 'anthropic', model: 'claude-opus-5-5', region: null, aws_profile: null, max_output_tokens: 16000, allowed_classifications: ['internal'], secret: KEY,
    }, '"0"']]);
    expect(toasts.getState().toasts.map((toast) => toast.text)).toEqual(['Paramètres de l’IA enregistrés.']);
  });

  it('a key kept is named by its last characters and never shown; it is kept by an empty field, removed by its box', async () => {
    const kept = view({ connector: 'anthropic', model: 'claude-opus-5-5', has_secret: true, secret_hint: 'wxyz', version: 2 });
    const sent = await start({ [`GET ${SETTINGS}`]: kept, [`PUT ${SETTINGS}`]: view({ connector: 'anthropic', model: 'claude-opus-5-5', version: 3 }) });
    await openDialog();
    expect(screen.getByTestId('assistant-key')).toHaveValue('');
    expect(screen.getByText('Clé enregistrée, finissant par wxyz : laisser vide pour la garder.')).toBeInTheDocument();
    expect(screen.getByTestId('assistant-save')).toBeDisabled();
    await userEvent.click(screen.getByTestId('assistant-remove-key'));
    expect(screen.getByTestId('assistant-key')).toBeDisabled();
    await userEvent.click(screen.getByTestId('assistant-save'));
    await waitFor(() => { expect(callsTo(sent, 'PUT', SETTINGS)).toHaveLength(1); });
    expect(callsTo(sent, 'PUT', SETTINGS).map((call) => [(call.body as { secret: unknown }).secret, call.headers.get('If-Match')])).toEqual([['', '"2"']]);
  });

  it('Bedrock chosen: the first inference profile proposed, the region and the profile sent, never a key', async () => {
    const sent = await start({ [`GET ${SETTINGS}`]: view(), [`PUT ${SETTINGS}`]: view({ connector: 'bedrock', version: 1 }) });
    await openDialog();
    await userEvent.click(screen.getByTestId('assistant-connector-bedrock'));
    expect(screen.getByTestId('assistant-model')).toHaveValue('global.anthropic.claude-sonnet-5-5');
    expect(screen.queryByTestId('assistant-key')).not.toBeInTheDocument();
    await userEvent.type(screen.getByTestId('assistant-region'), 'eu-west-3');
    await userEvent.type(screen.getByTestId('assistant-profile'), 'profil-fictif');
    await userEvent.click(screen.getByTestId('assistant-save'));
    await waitFor(() => { expect(callsTo(sent, 'PUT', SETTINGS)).toHaveLength(1); });
    expect(sentBodies(sent, 'PUT', SETTINGS)).toEqual([{
      connector: 'bedrock', model: 'global.anthropic.claude-sonnet-5-5', region: 'eu-west-3', aws_profile: 'profil-fictif', max_output_tokens: 16000,
      allowed_classifications: ['internal'], secret: null,
    }]);
  });

});

describe('the dialog of the settings of the assistant: a connector changed (spec 3.0 section 4)', () => {
  it('a field typed for one connector does not go with another: the key stays behind with Anthropic, the region with Bedrock', async () => {
    const sent = await start({ [`GET ${SETTINGS}`]: view(), [`PUT ${SETTINGS}`]: view({ version: 1 }) });
    await openDialog();
    await userEvent.click(screen.getByTestId('assistant-connector-anthropic'));
    await userEvent.type(screen.getByTestId('assistant-key'), KEY);
    await userEvent.click(screen.getByTestId('assistant-connector-bedrock'));
    await userEvent.type(screen.getByTestId('assistant-region'), 'eu-west-3');
    await userEvent.click(screen.getByTestId('assistant-connector-copy_paste'));
    await userEvent.click(screen.getByTestId('assistant-save'));
    await waitFor(() => { expect(callsTo(sent, 'PUT', SETTINGS)).toHaveLength(1); });
    expect(sentBodies(sent, 'PUT', SETTINGS)).toEqual([{
      connector: 'copy_paste', model: null, region: null, aws_profile: null, max_output_tokens: 16000, allowed_classifications: ['internal'], secret: null,
    }]);
  });

  it('a model typed by hand is kept when the connector changes; a model of the list gives way to the first of the new one', async () => {
    await start({ [`GET ${SETTINGS}`]: view() });
    await openDialog();
    await userEvent.click(screen.getByTestId('assistant-connector-anthropic'));
    await userEvent.click(screen.getByTestId('assistant-connector-bedrock'));
    expect(screen.getByTestId('assistant-model')).toHaveValue('global.anthropic.claude-sonnet-5-5');
    await userEvent.clear(screen.getByTestId('assistant-model'));
    await userEvent.type(screen.getByTestId('assistant-model'), 'arn-fictif');
    await userEvent.click(screen.getByTestId('assistant-connector-anthropic'));
    expect(screen.getByTestId('assistant-model')).toHaveValue('arn-fictif');
  });

});

describe('the dialog of the settings of the assistant: the classifications, the test, the refusals (spec 3.0 sections 4, 6)', () => {
  it('the confidential projects and the strictly confidential ones each say what allowing them means', async () => {
    const sent = await start({ [`GET ${SETTINGS}`]: view({ connector: 'anthropic', model: 'claude-opus-5-5', has_secret: true, secret_hint: 'wxyz' }), [`PUT ${SETTINGS}`]: view() });
    await openDialog();
    expect(screen.getByTestId('assistant-allow-internal')).toBeChecked();
    expect(screen.queryByTestId('assistant-warning-confidential')).not.toBeInTheDocument();
    await userEvent.click(screen.getByTestId('assistant-allow-strictly_confidential'));
    await userEvent.click(screen.getByTestId('assistant-allow-confidential'));
    expect(screen.getByTestId('assistant-warning-confidential')).toHaveTextContent('contrat de l’entreprise');
    expect(screen.getByTestId('assistant-warning-strict')).toHaveTextContent('TISAX');
    await userEvent.click(screen.getByTestId('assistant-save'));
    await waitFor(() => { expect(callsTo(sent, 'PUT', SETTINGS)).toHaveLength(1); });
    expect(sentBodies(sent, 'PUT', SETTINGS).map((body) => (body as { allowed_classifications: unknown }).allowed_classifications))
      .toEqual([['internal', 'confidential', 'strictly_confidential']]);
  });

  it('the test of the connection sends the form, names the model and its time, or says what to do; a field changed clears it', async () => {
    const sent = await start({
      [`GET ${SETTINGS}`]: view(),
      [`POST ${SETTINGS}/test`]: [outcome({}), outcome({ succeeded: false, model: null, input_tokens: null, output_tokens: null, failure: 'session_expired', detail: 'The SSO session expired.' })],
    });
    await openDialog();
    await userEvent.click(screen.getByTestId('assistant-connector-bedrock'));
    await userEvent.type(screen.getByTestId('assistant-region'), 'eu-west-3');
    await userEvent.type(screen.getByTestId('assistant-profile'), 'profil-fictif');
    await userEvent.click(screen.getByTestId('assistant-test'));
    expect(await screen.findByTestId('assistant-tested')).toHaveTextContent('Connexion réussie : claude-opus-5-5 a répondu en 1,3 s (21 jetons envoyés, 4 reçus).');
    await userEvent.click(screen.getByTestId('assistant-test'));
    const failed = await screen.findByTestId('assistant-test-failed');
    expect(failed).toHaveTextContent('lancer aws sso login --profile profil-fictif, puis réessayer.');
    expect(failed).toHaveTextContent('The SSO session expired.');
    expect(sentBodies(sent, 'POST', `${SETTINGS}/test`).map((body) => (body as { aws_profile: unknown }).aws_profile)).toEqual(['profil-fictif', 'profil-fictif']);
    await userEvent.type(screen.getByTestId('assistant-region'), 'x');
    expect(screen.queryByTestId('assistant-test-failed')).not.toBeInTheDocument();
    expect(callsTo(sent, 'PUT', SETTINGS)).toEqual([]);
  });

  it('a refused Save says why and keeps the dialog open with what was typed', async () => {
    await start({ [`GET ${SETTINGS}`]: view(), [`PUT ${SETTINGS}`]: { refused: 409, code: 'CONFLIT_VERSION', details: { object: 'assistant_settings' } } });
    await openDialog();
    await userEvent.click(screen.getByTestId('assistant-connector-anthropic'));
    await userEvent.type(screen.getByTestId('assistant-key'), KEY);
    await userEvent.click(screen.getByTestId('assistant-save'));
    expect(await screen.findByTestId('assistant-error')).not.toBeEmptyDOMElement();
    expect(screen.getByTestId('assistant-settings')).toBeInTheDocument();
    expect(screen.getByTestId('assistant-key')).toHaveValue(KEY);
  });
});
