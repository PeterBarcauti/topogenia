// Composer.test.tsx
// The field of the request of the assistant and the panel of the home page: a text file read where the caret is, a file too long refused, copy, clear, a paste refused by the browser, a request stopped; « Décrire un processus » up to « Créer le projet » (spec 3.0 sections 3.2, 3.4).

import { act, fireEvent, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { App } from '../../../app/App.tsx';
import { i18next } from '../../../shared/i18n/i18n.ts';
import { toasts } from '../../../shared/ui/toasts.ts';
import { callsTo, fakeApi } from '../../../test/fakeApi.ts';
import { fakeSource, profileOf } from '../../../test/profiles.ts';
import { createAssistantStore, REQUEST_MAX } from './assistantStore.ts';
import { Composer } from './Composer.tsx';

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

const notes = () => toasts.getState().toasts.map((toast) => toast.text);

function field() {
  const store = createAssistantStore({ mapId: () => 'map', selection: () => [] });
  render(<Composer store={store} placeholder="Votre demande" />);
  return { store, input: screen.getByTestId('assistant-field') };
}

describe('the field of the request', () => {
  it('reads a text file where the caret is, without its mark of byte order, and refuses a file too long', async () => {
    const { store, input } = field();
    await userEvent.type(input, 'Avant  après');
    act(() => {
      (input as HTMLTextAreaElement).setSelectionRange(6, 6);
    });
    const prompt = new File([`${String.fromCharCode(0xfeff)}le récit`], 'prompt.txt', { type: 'text/plain' });
    await userEvent.upload(screen.getByTestId('assistant-file'), prompt);
    await waitFor(() => { expect(store.getState().draft).toBe('Avant le récit après'); });
    expect(notes()).toContain('« prompt.txt » lu dans le champ.');
    await userEvent.upload(screen.getByTestId('assistant-file'), new File(['x'.repeat(REQUEST_MAX + 1)], 'long.md', { type: 'text/markdown' }));
    await waitFor(() => { expect(notes()).toContain('Fichier trop long : 100000 caractères au plus.'); });
    expect(store.getState().draft).toBe('Avant le récit après');
  });

  it('copies its text, clears it, and says when the browser refuses to read the clipboard', async () => {
    const writeText = vi.fn(() => Promise.resolve());
    Object.defineProperty(navigator, 'clipboard', { configurable: true, value: { writeText, readText: () => Promise.reject(new Error('refused')) } });
    const { store, input } = field();
    expect(screen.getByTestId('assistant-copy')).toBeDisabled();
    expect(screen.getByTestId('assistant-send')).toBeDisabled();
    await userEvent.type(input, 'Un texte');
    await userEvent.click(screen.getByTestId('assistant-copy'));
    expect(writeText).toHaveBeenCalledWith('Un texte');
    await userEvent.click(screen.getByTestId('assistant-paste'));
    await waitFor(() => { expect(notes()).toContain('Le navigateur refuse de lire le presse-papiers : collez avec Ctrl+V dans le champ.'); });
    await userEvent.click(screen.getByTestId('assistant-clear'));
    expect(store.getState().draft).toBe('');
    expect(input).toHaveAttribute('rows', '4');
  });

  it('pastes the clipboard where the caret is', async () => {
    Object.defineProperty(navigator, 'clipboard', { configurable: true, value: { writeText: vi.fn(), readText: () => Promise.resolve('collé') } });
    const { store, input } = field();
    await userEvent.type(input, 'Texte ');
    await userEvent.click(screen.getByTestId('assistant-paste'));
    await waitFor(() => { expect(store.getState().draft).toBe('Texte collé'); });
  });

  it('stops a request running: the exchange becomes a quiet failure that may be tried again', async () => {
    // A call that waits for its reply, as the network does, until the person stops it.
    vi.stubGlobal('fetch', vi.fn((_: string, init?: RequestInit) => new Promise((_resolve, reject) => {
      init?.signal?.addEventListener('abort', () => { reject(new DOMException('The call was stopped.', 'AbortError')); });
    })));
    const store = createAssistantStore({ mapId: () => 'map', selection: () => [] });
    render(<Composer store={store} placeholder="Votre demande" />);
    await userEvent.type(screen.getByTestId('assistant-field'), 'Bonjour{Enter}');
    await userEvent.click(await screen.findByTestId('assistant-stop'));
    await waitFor(() => { expect(store.getState().turns.at(-1)).toMatchObject({ kind: 'failure', failure: 'cancelled', retry: { text: 'Bonjour' } }); });
    expect(screen.getByTestId('assistant-send')).toBeInTheDocument();
  });
});

describe('« Décrire un processus » from the home page', () => {
  it('opens the panel for an administrator, sends a project yet to create, and its proposal opens as a new project to confirm', async () => {
    const proposal = {
      form: 'proposal', message: 'Une carte.', questions: [], findings: [], objects: [], raw: '{}', cost: null, duration_milliseconds: 0,
      proposal: {
        pmap: '{"version":"1.1","project":{"code":"ACH","name":"Demande d’achat"},"map":{"lanes":[]}}', project_code: 'ACH', project_name: 'Demande d’achat',
        maps: 1, lanes: 3, steps: 6, flows: 5, sheets: 1, uncertainties: ['Le seuil est à confirmer.'], faults: [],
      },
    };
    const sent = fakeApi({
      'GET /api/projects': { json: [] }, 'GET /api/trash': { json: { retention_days: 30, projects: [] } },
      'GET /api/admin/assistant': {
        json: {
          connector: 'anthropic', model: 'claude-opus-5-5', region: null, aws_profile: null, max_output_tokens: 16000, allowed_classifications: ['internal'], has_secret: true,
          secret_hint: 'abcd', version: 1, updated_at: null, connectors: ['none', 'copy_paste', 'anthropic', 'bedrock'],
        },
      },
      'POST /api/assistant/exchanges': { text: [{ type: 'started', destination: null }, { type: 'reply', reply: proposal }].map((line) => JSON.stringify(line)).join('\n') },
    });
    render(<App source={fakeSource([profileOf({ is_admin: true })]).source} />);
    await userEvent.click(await screen.findByTestId('home-describe-ai'));
    expect(await screen.findByTestId('assistant-destination')).toHaveTextContent('Envoyé à Claude par l’API Anthropic · claude-opus-5-5 · projet Interne');
    fireEvent.change(screen.getByTestId('assistant-field'), { target: { value: 'Le demandeur saisit sa demande, le responsable la valide.' } });
    await userEvent.click(screen.getByTestId('assistant-send'));
    const card = await screen.findByTestId('assistant-proposal');
    // Each count under its figure: the name of the count, then the figure, in the order of the page.
    expect(card).toHaveTextContent(/étapes\s*6/);
    expect(card).toHaveTextContent('Le seuil est à confirmer.');
    expect(callsTo(sent, 'POST', '/api/assistant/exchanges')[0]?.body).toMatchObject({ intent: 'free', request: 'Le demandeur saisit sa demande, le responsable la valide.' });
    await userEvent.click(screen.getByTestId('assistant-create-project'));
    expect(await screen.findByTestId('open-pmap')).toBeInTheDocument();
    expect(screen.getByTestId('open-pmap-content')).toHaveValue(proposal.proposal.pmap);
  });

  it('is offered to an administrator only, the main button keeping the way to the map of the project in focus (B-1.3-02)', async () => {
    fakeApi({ 'GET /api/projects': { json: [] }, 'GET /api/trash': { json: { retention_days: 30, projects: [] } } });
    render(<App source={fakeSource([profileOf({ is_admin: false })]).source} />);
    expect(await screen.findByTestId('home-describe')).toBeDisabled();
    expect(screen.queryByTestId('home-describe-ai')).toBeNull();
  });
});
