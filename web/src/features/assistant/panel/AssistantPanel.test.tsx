// AssistantPanel.test.tsx
// The panel of the assistant on the map: opened by its button or Ctrl+I, its tiles for each role, a request sent with its practice and its selection, the stream read into a progress then a reply, the codes shown on the map, the failures, the copy and paste, one panel at a time (spec 3.0 section 3.4).

import { act, fireEvent, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { Schema } from '../../../api/client.ts';
import { i18next } from '../../../shared/i18n/i18n.ts';
import { callsTo, fakeApi, type FakeReply, type FakeRoutes } from '../../../test/fakeApi.ts';
import { MAP_ID, sampleMap } from '../../../test/maps.ts';
import { openSheetOf, showEditor } from '../../../test/editor.tsx';

const STATUS = `GET /api/maps/${MAP_ID}/assistant`;

/** The bounds of the files of the API of Anthropic, as the host gives them. */
const FILE_BOUNDS: Schema<'AssistantFileBoundsView'> = {
  files: 5, file_bytes: 10 * 1024 * 1024, image_bytes: 7 * 1024 * 1024, total_bytes: 20 * 1024 * 1024,
  extensions: ['.pdf', '.png', '.jpg', '.jpeg', '.gif', '.webp', '.docx', '.xlsx', '.vsdx'], outdated: { '.doc': 'docx', '.xls': 'xlsx', '.vsd': 'vsdx' },
};
const EXCHANGE = `POST /api/maps/${MAP_ID}/assistant/exchanges`;

const status = (overrides: Partial<Schema<'AssistantStatusView'>> = {}): FakeReply => ({
  json: {
    mode: 'model', allowed: true, can_propose: true, empty_map: false, shows_cost: true, project_name: 'Ventes', project_code: 'VTE',
    destination: { connector: 'anthropic', model: 'claude-opus-5-5', region: null, classification: 'internal', scope: 'project', map_name: 'Ventes' },
    files: FILE_BOUNDS, ...overrides,
  },
});

const reply = (overrides: Partial<Schema<'AssistantReplyView'>> = {}): Schema<'AssistantReplyView'> => ({
  form: 'answer', message: 'La première étape est VTE-E001.', questions: [], findings: [], proposal: null, raw: '{"form":"answer"}', cost: null, duration_milliseconds: 900,
  objects: [{ code: 'VTE-E001', kind: 'step', map_id: MAP_ID, id: 's1', label: 'Demande reçue' }], ...overrides,
});

/** A stream of an exchange: its start, a progress, then the lines given. */
const stream = (...lines: object[]): FakeReply => ({
  text: [{ type: 'started', destination: null }, { type: 'progress', phase: 'thinking', thought: 'Je lis la carte.', characters: 0, elapsed_milliseconds: 1200 }, ...lines]
    .map((line) => JSON.stringify(line)).join('\n'),
});

async function open(routes: FakeRoutes = {}) {
  const sent = fakeApi({ [STATUS]: status(), ...routes });
  const shown = await showEditor(sampleMap());
  await userEvent.click(screen.getByTestId('ribbon-assistant'));
  await screen.findByTestId('assistant-tiles');
  return { sent, ...shown };
}

beforeEach(async () => {
  await i18next.changeLanguage('fr');
});

describe('the panel of the assistant on the map', () => {
  it('opens and closes by its button and Ctrl+I, reads its state and says where the data go', async () => {
    const { editor } = await open();
    expect(screen.getByTestId('assistant-scope')).toHaveTextContent('Projet entier');
    expect(screen.getByTestId('assistant-destination')).toHaveTextContent('Claude par l’API Anthropic · claude-opus-5-5 · projet Interne');
    expect(screen.getByTestId('ribbon-assistant')).toHaveAttribute('aria-pressed', 'true');
    fireEvent.keyDown(screen.getByTestId('assistant-field'), { key: 'i', ctrlKey: true });
    expect(screen.queryByTestId('assistant-panel')).toBeNull();
    act(() => {
      fireEvent.keyDown(document.body, { key: 'i', ctrlKey: true });
    });
    expect(await screen.findByTestId('assistant-panel')).toBeInTheDocument();
    expect(editor.assistant.getState().open).toBe(true);
    // The same key, and the same button, close it again.
    act(() => {
      fireEvent.keyDown(document.body, { key: 'i', ctrlKey: true });
    });
    expect(screen.queryByTestId('assistant-panel')).toBeNull();
    await userEvent.click(screen.getByTestId('ribbon-assistant'));
    await userEvent.click(screen.getByTestId('ribbon-assistant'));
    expect(screen.queryByTestId('assistant-panel')).toBeNull();
  });

  it('offers the four tiles, the two that change the map for the facilitator only, and a tile fills the field with its practice', async () => {
    await open({ [STATUS]: status({ can_propose: false }) });
    expect(screen.getByTestId('assistant-tile-create')).toBeDisabled();
    expect(screen.getByTestId('assistant-tile-modify')).toBeDisabled();
    await userEvent.click(screen.getByTestId('assistant-tile-analyze'));
    expect(screen.getByTestId('assistant-field')).toHaveValue('Où sont les pertes de temps et les ressaisies sur cette carte ?');
    expect(screen.getByTestId('assistant-intent')).toHaveTextContent('Analyser ce que je vois');
  });

  it('sends the request with its practice, the codes selected and the former turns, shows the progress then the reply, and a code shows its step', async () => {
    const { sent, editor } = await open({ [EXCHANGE]: [stream({ type: 'reply', reply: reply(), elapsed_milliseconds: 3200 }), stream({ type: 'reply', reply: reply() })] });
    act(() => {
      editor.store.setState({ selection: { steps: ['s2'], flows: [] } });
    });
    expect(await screen.findByTestId('assistant-selection')).toHaveTextContent('1 étape sélectionnée');
    await userEvent.click(screen.getByTestId('assistant-tile-ask'));
    await userEvent.clear(screen.getByTestId('assistant-field'));
    await userEvent.type(screen.getByTestId('assistant-field'), 'Quelle est la première étape ?{Enter}');
    const card = await screen.findByTestId('assistant-reply');
    expect(card).toHaveTextContent('Réponse');
    expect(card).toHaveTextContent('3 s');
    expect(callsTo(sent, 'POST', `/api/maps/${MAP_ID}/assistant/exchanges`)[0]?.body).toEqual({
      intent: 'ask', request: 'Quelle est la première étape ?', selection: ['VTE-E002'], history: [], files: [],
    });
    expect(screen.getByTestId('assistant-field')).toHaveValue('');
    await userEvent.click(within(card).getByTestId('assistant-code'));
    expect(editor.store.getState().selection.steps).toEqual(['s1']);
    await userEvent.type(screen.getByTestId('assistant-field'), 'Et la suivante ?{Enter}');
    await waitFor(() => { expect(screen.getAllByTestId('assistant-reply')).toHaveLength(2); });
    expect(callsTo(sent, 'POST', `/api/maps/${MAP_ID}/assistant/exchanges`)[1]?.body).toMatchObject({
      history: [{ request: 'Quelle est la première étape ?', reply: '{"form":"answer"}', files: [] }],
    });
  });

  it('a removed pill sends no selection, and Shift+Enter goes to the line without sending', async () => {
    const { sent, editor } = await open({ [EXCHANGE]: stream({ type: 'reply', reply: reply() }) });
    act(() => {
      editor.store.setState({ selection: { steps: ['s2', 's3'], flows: [] } });
    });
    await userEvent.click(await screen.findByTestId('assistant-selection-toggle'));
    expect(screen.getByTestId('assistant-selection')).toHaveTextContent('Sélection non envoyée');
    await userEvent.type(screen.getByTestId('assistant-field'), 'Ligne une{Shift>}{Enter}{/Shift}ligne deux');
    expect(callsTo(sent, 'POST', `/api/maps/${MAP_ID}/assistant/exchanges`)).toHaveLength(0);
    await userEvent.click(screen.getByTestId('assistant-send'));
    await screen.findByTestId('assistant-reply');
    expect(callsTo(sent, 'POST', `/api/maps/${MAP_ID}/assistant/exchanges`)[0]?.body).toMatchObject({ request: 'Ligne une\nligne deux', selection: [] });
  });

});

describe('the replies, the copy and paste and the other panels', () => {
  it('shows the findings with their kind and suggestion, the questions with their choices, a failure with its retry', async () => {
    const findings = reply({
      form: 'findings', message: 'Un constat.', objects: [{ code: 'VTE-E002', kind: 'step', map_id: MAP_ID, id: 's2', label: 'Vérifier la demande' }],
      findings: [{ kind: 're_entry', title: 'Ressaisie', detail: 'VTE-E002 ressaisit la demande.', objects: ['VTE-E002'], suggestion: 'Importer la demande.' }],
    });
    const questions = reply({ form: 'questions', message: 'Deux points.', objects: [], questions: [{ question: 'Qui valide ?', choices: ['Le responsable'] }] });
    const { editor } = await open({
      [EXCHANGE]: [stream({ type: 'reply', reply: findings }), stream({ type: 'reply', reply: questions }), stream({ type: 'failure', failure: 'limited', detail: 'slow down' })],
    });
    await userEvent.type(screen.getByTestId('assistant-field'), 'Analyse.{Enter}');
    const finding = await screen.findByTestId('assistant-finding');
    expect(finding).toHaveTextContent('Ressaisie');
    expect(finding).toHaveTextContent('Importer la demande.');
    await userEvent.click(within(finding).getByTestId('assistant-show-finding'));
    expect(editor.store.getState().selection.steps).toEqual(['s2']);
    await userEvent.type(screen.getByTestId('assistant-field'), 'Crée.{Enter}');
    await userEvent.click(await screen.findByTestId('assistant-choice'));
    expect(screen.getByTestId('assistant-field')).toHaveValue('Qui valide ? Le responsable\n');
    await userEvent.click(screen.getByTestId('assistant-send'));
    const failure = await screen.findByTestId('assistant-failure');
    expect(failure).toHaveTextContent('Trop de demandes, ou crédit épuisé');
    expect(failure).toHaveTextContent('slow down');
  });

  it('copies the prompt without a model, then reads the reply pasted from the AI of the person', async () => {
    const writeText = vi.fn(() => Promise.resolve());
    Object.defineProperty(navigator, 'clipboard', { configurable: true, value: { writeText, readText: () => Promise.reject(new Error('no')) } });
    const { sent } = await open({
      [STATUS]: status({ mode: 'copy_paste', destination: { connector: 'copy_paste', model: null, region: null, classification: 'internal', scope: 'project', map_name: 'Ventes' } }),
      [`POST /api/maps/${MAP_ID}/assistant/prompt`]: { json: { prompt: 'LA CONSIGNE' } },
      [`POST /api/maps/${MAP_ID}/assistant/replies`]: { json: { reply: reply({ message: 'Lu.' }), failure: null, detail: null } },
    });
    expect(screen.getByTestId('assistant-destination')).toHaveTextContent('Copier-coller');
    await userEvent.type(screen.getByTestId('assistant-field'), 'Résume.{Enter}');
    expect(await screen.findByTestId('assistant-copied')).toHaveTextContent('Consigne copiée (11 caractères)');
    expect(writeText).toHaveBeenCalledWith('LA CONSIGNE');
    await userEvent.type(screen.getByTestId('assistant-pasted'), '{{"form":"answer","message":"Lu."}');
    await userEvent.click(screen.getByTestId('assistant-read-pasted'));
    expect(await screen.findByTestId('assistant-reply')).toHaveTextContent('Réponse collée');
    expect(callsTo(sent, 'POST', `/api/maps/${MAP_ID}/assistant/replies`)[0]?.body).toMatchObject({ request: 'Résume.', reply: '{"form":"answer","message":"Lu."}' });
  });

  it('says the classification is refused and lets nothing be written', async () => {
    await open({ [STATUS]: status({ allowed: false, destination: { connector: 'anthropic', model: 'm', region: null, classification: 'confidential', scope: 'map', map_name: 'Ventes' } }) });
    expect(screen.getByTestId('assistant-destination')).toHaveTextContent('Projet Confidentiel : l’administrateur ne permet pas de l’envoyer à l’IA.');
    expect(screen.getByTestId('assistant-field')).toBeDisabled();
  });

  it('is one panel at a time with the sheet, and its thread lives on while it is closed', async () => {
    const { canvas, editor } = await open({ [EXCHANGE]: stream({ type: 'reply', reply: reply() }) });
    await userEvent.type(screen.getByTestId('assistant-field'), 'Bonjour{Enter}');
    await screen.findByTestId('assistant-reply');
    fakeApi({ [STATUS]: status(), 'GET /api/steps/s2/sheet': { pending: true }, [`GET /api/projects/${sampleMap().project_id}/members`]: { pending: true } });
    openSheetOf(canvas, 's2');
    await waitFor(() => { expect(screen.queryByTestId('assistant-panel')).toBeNull(); });
    act(() => {
      editor.assistant.getState().setOpen(true);
    });
    expect(await screen.findByTestId('assistant-reply')).toBeInTheDocument();
    expect(editor.store.getState().openSheet).toBeNull();
  });
});
