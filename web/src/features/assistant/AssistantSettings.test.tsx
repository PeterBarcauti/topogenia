// AssistantSettings.test.tsx
// The gear of the settings of the assistant, for the administrator alone, and its dialog: the connectors by family and where their data go, the fields of the one chosen, its model its own and the ceiling typed as text (audit 3.2), the key typed and never shown, the classifications and their warnings, set whatever the connector (F-137), the test of the connection, what a Save sends (spec 3.0 sections 4, 6, ADR 098).

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
    secret_hint: null, version: 0, updated_at: null, connectors: ['none', 'ollama', 'lm_studio', 'bedrock', 'azure_open_ai', 'anthropic', 'open_ai', 'gemini', 'grok', 'mistral', 'deep_seek', 'kimi', 'qwen'], ...overrides,
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
  it('lists every connector of the contract by family, each family saying where its data go, none chosen by default, with nothing to test nor to save', async () => {
    await start({ [`GET ${SETTINGS}`]: view() });
    await openDialog();
    for (const connector of ['none', 'ollama', 'lm_studio', 'bedrock', 'azure_open_ai', 'anthropic', 'open_ai', 'gemini', 'grok', 'mistral', 'deep_seek', 'kimi', 'qwen'])
      expect(screen.getByTestId(`assistant-connector-${connector}`)).toBeInTheDocument();
    const families = ['none', 'machine', 'cloud', 'american', 'european', 'chinese'].map((family) => screen.getByTestId(`assistant-family-${family}`));
    expect(families.map((family) => [...family.querySelectorAll('input')].map((input) => input.value))).toEqual([
      ['none'], ['ollama', 'lm_studio'], ['bedrock', 'azure_open_ai'], ['anthropic', 'open_ai', 'gemini', 'grok'], ['mistral'], ['deep_seek', 'kimi', 'qwen'],
    ]);
    expect(families[5]).toHaveTextContent('Éditeurs chinois · Les données partent chez un éditeur chinois.');
    expect(families[1]).toHaveTextContent('rien ne sort');
    // A « cloud » model of Ollama is computed at its maker: the family says so, and the clouds do not promise a region.
    expect(families[1]).toHaveTextContent('sauf avec un modèle « cloud » d’Ollama');
    expect(families[2]).toHaveTextContent('la région de traitement dépend du profil ou du déploiement choisi');
    expect(screen.getByTestId('assistant-connector-none')).toBeChecked();
    // The name of a connector and what it does, separated. One choice without a connection (lot J10): it says that the
    // prompt is copied into an AI and its reply pasted back, what « Copier-coller », offered no more, said.
    expect(screen.getByTestId('assistant-connector-none').closest('label'))
      .toHaveTextContent(/^Aucun · La consigne se copie dans l’IA de son choix, puis sa réponse se colle ici : rien ne part de \S+\.$/u);
    expect(screen.queryByTestId('assistant-connector-copy_paste')).not.toBeInTheDocument();
    expect(screen.getByTestId('assistant-test')).toBeDisabled();
    expect(screen.getByTestId('assistant-save')).toBeDisabled();
    expect(screen.queryByTestId('assistant-model')).not.toBeInTheDocument();
  });

});

describe('the dialog of the settings of the assistant: one choice without a connection (lot J10)', () => {
  it('opens on « Aucun » a setting kept as the copy and paste, a host that still lists it offering it no more, with nothing to save', async () => {
    const kept = view({ connector: 'copy_paste', version: 3, connectors: ['none', 'copy_paste', 'ollama', 'anthropic'] });
    await start({ [`GET ${SETTINGS}`]: kept });
    await openDialog();
    expect(screen.getByTestId('assistant-connector-none')).toBeChecked();
    expect(screen.queryByTestId('assistant-connector-copy_paste')).not.toBeInTheDocument();
    expect(screen.queryByTestId('assistant-family-other')).not.toBeInTheDocument();
    expect(screen.getByTestId('assistant-save')).toBeDisabled();
  });
});

describe('the dialog of the settings of the assistant: a connector that calls a model (spec 3.0 section 4)', () => {
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
      resource: null, port: null,
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
    await userEvent.type(screen.getByTestId('assistant-region'), 'eu-central-1');
    await userEvent.type(screen.getByTestId('assistant-profile'), 'profil-fictif');
    await userEvent.click(screen.getByTestId('assistant-save'));
    await waitFor(() => { expect(callsTo(sent, 'PUT', SETTINGS)).toHaveLength(1); });
    expect(sentBodies(sent, 'PUT', SETTINGS)).toEqual([{
      connector: 'bedrock', model: 'global.anthropic.claude-sonnet-5-5', region: 'eu-central-1', aws_profile: 'profil-fictif', max_output_tokens: 16000,
      allowed_classifications: ['internal'], secret: null, resource: null, port: null,
    }]);
  });
});

describe('the dialog of the settings of the assistant: the providers of ADR 098', () => {
  it('OpenAI chosen: its first model proposed; Azure: its key typed and sent, the name of its resource', async () => {
    const sent = await start({ [`GET ${SETTINGS}`]: view(), [`PUT ${SETTINGS}`]: view({ version: 1 }) });
    await openDialog();
    await userEvent.click(screen.getByTestId('assistant-connector-open_ai'));
    expect(screen.getByTestId('assistant-model')).toHaveValue('gpt-5.6-sol');
    expect(screen.getByTestId('assistant-connector-help')).toHaveTextContent('api.openai.com');
    await userEvent.click(screen.getByTestId('assistant-connector-azure_open_ai'));
    expect(screen.getByTestId('assistant-model')).toHaveValue('');
    await userEvent.type(screen.getByTestId('assistant-key'), KEY);
    await userEvent.type(screen.getByTestId('assistant-resource'), 'contoso');
    await userEvent.type(screen.getByTestId('assistant-model'), 'processus-gpt');
    await userEvent.click(screen.getByTestId('assistant-save'));
    await waitFor(() => { expect(callsTo(sent, 'PUT', SETTINGS)).toHaveLength(1); });
    expect(sentBodies(sent, 'PUT', SETTINGS)).toEqual([{
      connector: 'azure_open_ai', model: 'processus-gpt', region: null, aws_profile: null, max_output_tokens: 16000, allowed_classifications: ['internal'],
      secret: KEY, resource: 'contoso', port: null,
    }]);
  });

  it('a model of this machine takes its port, never a key; a key kept for another provider is not offered to the one chosen', async () => {
    const sent = await start({ [`GET ${SETTINGS}`]: view({ connector: 'anthropic', model: 'claude-opus-5-5', has_secret: true, secret_hint: 'wxyz' }), [`PUT ${SETTINGS}`]: view({ version: 1 }) });
    await openDialog();
    await userEvent.click(screen.getByTestId('assistant-connector-mistral'));
    expect(screen.getByText('Aucune clé enregistrée.')).toBeInTheDocument();
    await userEvent.click(screen.getByTestId('assistant-connector-ollama'));
    expect(screen.queryByTestId('assistant-key')).not.toBeInTheDocument();
    await userEvent.type(screen.getByTestId('assistant-port'), '11500');
    await userEvent.type(screen.getByTestId('assistant-model'), 'llama3.1:8b');
    await userEvent.click(screen.getByTestId('assistant-save'));
    await waitFor(() => { expect(callsTo(sent, 'PUT', SETTINGS)).toHaveLength(1); });
    expect(sentBodies(sent, 'PUT', SETTINGS)).toEqual([{
      connector: 'ollama', model: 'llama3.1:8b', region: null, aws_profile: null, max_output_tokens: 16000, allowed_classifications: ['internal'],
      secret: null, resource: null, port: 11500,
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
    await userEvent.type(screen.getByTestId('assistant-region'), 'eu-central-1');
    await userEvent.click(screen.getByTestId('assistant-connector-ollama'));
    await userEvent.click(screen.getByTestId('assistant-connector-none'));
    await userEvent.click(screen.getByTestId('assistant-allow-confidential'));
    await userEvent.click(screen.getByTestId('assistant-save'));
    await waitFor(() => { expect(callsTo(sent, 'PUT', SETTINGS)).toHaveLength(1); });
    expect(sentBodies(sent, 'PUT', SETTINGS)).toEqual([{
      connector: 'none', model: null, region: null, aws_profile: null, max_output_tokens: 16000, allowed_classifications: ['internal', 'confidential'], secret: null,
      resource: null, port: null,
    }]);
  });

  it('a key typed for one provider never goes to another: the field is emptied by the choice of another connector, the test and the Save send none', async () => {
    const sent = await start({ [`GET ${SETTINGS}`]: view(), [`PUT ${SETTINGS}`]: view({ version: 1 }), [`POST ${SETTINGS}/test`]: outcome({}) });
    await openDialog();
    await userEvent.click(screen.getByTestId('assistant-connector-open_ai'));
    await userEvent.type(screen.getByTestId('assistant-key'), KEY);
    await userEvent.click(screen.getByTestId('assistant-connector-deep_seek'));
    expect(screen.getByTestId('assistant-key')).toHaveValue('');
    await userEvent.click(screen.getByTestId('assistant-test'));
    await screen.findByTestId('assistant-tested');
    await userEvent.click(screen.getByTestId('assistant-save'));
    await waitFor(() => { expect(callsTo(sent, 'PUT', SETTINGS)).toHaveLength(1); });
    const secrets = (path: string, method: string) => sentBodies(sent, method, path).map((body) => (body as { connector: unknown; secret: unknown }));
    expect(secrets(`${SETTINGS}/test`, 'POST').map((body) => [body.connector, body.secret])).toEqual([['deep_seek', null]]);
    expect(secrets(SETTINGS, 'PUT').map((body) => [body.connector, body.secret])).toEqual([['deep_seek', null]]);
  });

  it('coming back to the first provider does not bring its key back: what was typed is typed again', async () => {
    await start({ [`GET ${SETTINGS}`]: view() });
    await openDialog();
    await userEvent.click(screen.getByTestId('assistant-connector-open_ai'));
    await userEvent.type(screen.getByTestId('assistant-key'), KEY);
    await userEvent.click(screen.getByTestId('assistant-connector-mistral'));
    await userEvent.click(screen.getByTestId('assistant-connector-open_ai'));
    expect(screen.getByTestId('assistant-key')).toHaveValue('');
  });

  it('the removal asked of the key kept does not follow to another provider: its field takes a key', async () => {
    const sent = await start({ [`GET ${SETTINGS}`]: view({ connector: 'anthropic', model: 'claude-opus-5-5', has_secret: true, secret_hint: 'wxyz' }), [`PUT ${SETTINGS}`]: view({ version: 1 }) });
    await openDialog();
    await userEvent.click(screen.getByTestId('assistant-remove-key'));
    await userEvent.click(screen.getByTestId('assistant-connector-open_ai'));
    expect(screen.getByTestId('assistant-key')).toBeEnabled();
    await userEvent.type(screen.getByTestId('assistant-key'), KEY);
    await userEvent.click(screen.getByTestId('assistant-save'));
    await waitFor(() => { expect(callsTo(sent, 'PUT', SETTINGS)).toHaveLength(1); });
    expect(sentBodies(sent, 'PUT', SETTINGS).map((body) => (body as { secret: unknown }).secret)).toEqual([KEY]);
  });

  it('the port typed for one program of this machine does not follow to the other', async () => {
    await start({ [`GET ${SETTINGS}`]: view() });
    await openDialog();
    await userEvent.click(screen.getByTestId('assistant-connector-ollama'));
    await userEvent.type(screen.getByTestId('assistant-port'), '11500');
    await userEvent.click(screen.getByTestId('assistant-connector-lm_studio'));
    expect(screen.getByTestId('assistant-port')).toHaveValue(null);
  });
});

describe('the dialog of the settings of the assistant: the model and the ceiling of each connector (audit 3.2)', () => {
  it('a connector chosen takes the first model of its list, or none: a model typed for another provider never follows it (audit 3.2, UX-MAJ-03)', async () => {
    await start({ [`GET ${SETTINGS}`]: view() });
    await openDialog();
    const model = () => screen.getByTestId('assistant-model');
    await userEvent.click(screen.getByTestId('assistant-connector-bedrock'));
    expect(model()).toHaveValue('global.anthropic.claude-sonnet-5-5');
    await userEvent.clear(model());
    await userEvent.type(model(), 'arn-fictif');
    await userEvent.click(screen.getByTestId('assistant-connector-anthropic'));
    expect(model()).toHaveValue('claude-opus-5-5');
    await userEvent.click(screen.getByTestId('assistant-connector-ollama'));
    expect(model()).toHaveValue('');
    await userEvent.type(model(), 'llama3.2');
    await userEvent.click(screen.getByTestId('assistant-connector-anthropic'));
    expect(model()).toHaveValue('claude-opus-5-5');
  });

  it('coming back to a connector while the dialog is open gives back the model left with it, typed or read', async () => {
    await start({ [`GET ${SETTINGS}`]: view({ connector: 'anthropic', model: 'claude-sonnet-5-5', has_secret: true, secret_hint: 'wxyz' }) });
    await openDialog();
    const model = () => screen.getByTestId('assistant-model');
    await userEvent.click(screen.getByTestId('assistant-connector-bedrock'));
    await userEvent.clear(model());
    await userEvent.type(model(), 'arn-fictif');
    await userEvent.click(screen.getByTestId('assistant-connector-ollama'));
    await userEvent.type(model(), 'llama3.2');
    await userEvent.click(screen.getByTestId('assistant-connector-bedrock'));
    expect(model()).toHaveValue('arn-fictif');
    await userEvent.click(screen.getByTestId('assistant-connector-ollama'));
    expect(model()).toHaveValue('llama3.2');
    await userEvent.click(screen.getByTestId('assistant-connector-anthropic'));
    expect(model()).toHaveValue('claude-sonnet-5-5');
  });

  it('the ceiling of a reply is typed as the port: emptied, the field is empty, never « 0 »; sent as a number, or null for the default (audit 3.2, UX-MOY-05)', async () => {
    const sent = await start({ [`GET ${SETTINGS}`]: view({ connector: 'anthropic', model: 'claude-opus-5-5', has_secret: true, secret_hint: 'wxyz' }), [`PUT ${SETTINGS}`]: view({ version: 1 }) });
    await openDialog();
    const ceiling = screen.getByTestId('assistant-ceiling');
    expect(document.getElementById(ceiling.getAttribute('aria-describedby') ?? '')).toHaveTextContent('vide : 64 000, la valeur par défaut');
    await userEvent.clear(ceiling);
    expect(ceiling).toHaveValue(null);
    await userEvent.type(ceiling, '8000');
    expect(ceiling).toHaveValue(8000);
    await userEvent.click(screen.getByTestId('assistant-save'));
    await waitFor(() => { expect(callsTo(sent, 'PUT', SETTINGS)).toHaveLength(1); });
    expect(sentBodies(sent, 'PUT', SETTINGS).map((body) => (body as { max_output_tokens: unknown }).max_output_tokens)).toEqual([8000]);
  });

  it('an emptied ceiling is sent as null, for the default of the host, never as 0', async () => {
    const sent = await start({ [`GET ${SETTINGS}`]: view({ connector: 'anthropic', model: 'claude-opus-5-5', has_secret: true, secret_hint: 'wxyz' }), [`PUT ${SETTINGS}`]: view({ version: 1 }) });
    await openDialog();
    await userEvent.clear(screen.getByTestId('assistant-ceiling'));
    await userEvent.click(screen.getByTestId('assistant-save'));
    await waitFor(() => { expect(callsTo(sent, 'PUT', SETTINGS)).toHaveLength(1); });
    expect(sentBodies(sent, 'PUT', SETTINGS).map((body) => (body as { max_output_tokens: unknown }).max_output_tokens)).toEqual([null]);
  });
});

describe('the dialog of the settings of the assistant: the classifications, whatever the connector (F-137)', () => {
  it('the classifications are set whatever the connector, none too, and say they hold for the online voices of a presentation (F-137)', async () => {
    const sent = await start({ [`GET ${SETTINGS}`]: view(), [`PUT ${SETTINGS}`]: view({ version: 1 }) });
    await openDialog();
    expect(screen.getByTestId('assistant-classifications')).toHaveTextContent('Cette liste vaut aussi pour les voix en ligne de la présentation, quel que soit le connecteur.');
    await userEvent.click(screen.getByTestId('assistant-allow-confidential'));
    await userEvent.click(screen.getByTestId('assistant-connector-ollama'));
    expect(screen.getByTestId('assistant-allow-confidential')).toBeChecked();
    await userEvent.click(screen.getByTestId('assistant-connector-none'));
    await userEvent.click(screen.getByTestId('assistant-save'));
    await waitFor(() => { expect(callsTo(sent, 'PUT', SETTINGS)).toHaveLength(1); });
    expect(sentBodies(sent, 'PUT', SETTINGS)).toEqual([{
      connector: 'none', model: null, region: null, aws_profile: null, max_output_tokens: 16000, allowed_classifications: ['internal', 'confidential'], secret: null,
      resource: null, port: null,
    }]);
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
    expect(screen.getByTestId('assistant-warning-strict')).toHaveTextContent('responsable de la sécurité de l’information');
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
    await userEvent.type(screen.getByTestId('assistant-region'), 'eu-central-1');
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

  it.each([
    ['ceiling_refused', 'Le fournisseur refuse ce plafond de réponse : le baisser.'],
    ['invalid_reply', 'Le service a répondu, mais pas comme un modèle : vérifier le connecteur, le port et le modèle.'],
    ['declined', 'Le modèle a refusé de répondre à l’essai.'],
  ])('the failure %s of the test of the connection has its own text, and the detail of the provider under it', async (failure, text) => {
    await start({
      [`GET ${SETTINGS}`]: view({ connector: 'deep_seek', model: 'deepseek-chat', has_secret: true, secret_hint: 'wxyz' }),
      [`POST ${SETTINGS}/test`]: outcome({ succeeded: false, model: null, input_tokens: null, output_tokens: null, failure, detail: 'Detail given by the provider.' }),
    });
    await openDialog();
    await userEvent.click(screen.getByTestId('assistant-test'));
    const failed = await screen.findByTestId('assistant-test-failed');
    expect(failed).toHaveTextContent(text);
    expect(failed).toHaveTextContent('Detail given by the provider.');
  });

  it('a failure of the test the interface has no text for is said as a refusal of the provider, never by its code', async () => {
    await start({
      [`GET ${SETTINGS}`]: view({ connector: 'open_ai', model: 'gpt-5.6-sol', has_secret: true, secret_hint: 'wxyz' }),
      [`POST ${SETTINGS}/test`]: outcome({ succeeded: false, model: null, input_tokens: null, output_tokens: null, failure: 'a_failure_of_tomorrow', detail: null }),
    });
    await openDialog();
    await userEvent.click(screen.getByTestId('assistant-test'));
    const failed = await screen.findByTestId('assistant-test-failed');
    expect(failed).toHaveTextContent('Le fournisseur a refusé la demande.');
    expect(failed).not.toHaveTextContent('assistant.failures');
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
