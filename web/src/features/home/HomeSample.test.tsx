// HomeSample.test.tsx
// The home page and the classification: the sample project offered in desktop mode while there is no project, the level of each project on its card (spec 6.1, 4.1).

import { act, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { Schema } from '../../api/client.ts';
import { App } from '../../app/App.tsx';
import { i18next } from '../../shared/i18n/i18n.ts';
import { toasts } from '../../shared/ui/toasts.ts';
import { callsTo, fakeApi, type FakeReply } from '../../test/fakeApi.ts';
import { fakeSource, profileOf } from '../../test/profiles.ts';
import type { ProjectItem } from './focus.ts';

const CONGES: ProjectItem = {
  id: 'p-conges', code: 'CONGES', name: 'Demande de congés', source_language: 'fr', name_translations: {}, description: null, classification: 'confidential',
  root_map_id: 'm-conges', role: 'facilitator', step_count: 23, map_count: 3,
};

const DESKTOP: Partial<Schema<'ProfileView'>> = { is_admin: true, desktop: true };

function start(projects: FakeReply | FakeReply[], profile: Partial<Schema<'ProfileView'>>, sample: FakeReply = { json: {}, status: 201 }) {
  const sent = fakeApi({
    'GET /api/projects': projects, 'GET /api/trash': { json: { retention_days: 30, projects: [] } }, 'POST /api/desktop/sample-project': sample,
    'GET /api/projects/p-conges/participants': { json: [] },
  });
  render(<App source={fakeSource([profileOf(profile)]).source} />);
  return sent;
}

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

describe('the sample project of the first launch (S-6.1-10)', () => {
  it('in desktop mode, without a project, a card offers it next to the usual buttons, which stay usable', async () => {
    start({ json: [] }, DESKTOP);
    const card = await screen.findByTestId('home-sample');
    expect(within(card).getByRole('heading', { level: 3 })).toHaveTextContent('Projet d’exemple');
    expect(card).toHaveTextContent('Un projet fictif, déjà décrit, pour découvrir une carte complète');
    expect(screen.getByTestId('home-empty')).toHaveTextContent('Aucun projet pour l’instant. Créer le premier.');
    await userEvent.click(screen.getByRole('button', { name: 'Nouveau projet' }));
    expect(window.location.pathname).toBe('/projects/new');
  });

  it('« Créer le projet d’exemple » asks the host, says it, then lists the project, put forward by the main button', async () => {
    const sent = start([{ json: [] }, { json: [CONGES] }], DESKTOP, { json: { id: 'p-conges', code: 'CONGES', root_map_id: 'm-conges', trash_code: null, created_user_ids: [] }, status: 201 });
    await userEvent.click(await screen.findByRole('button', { name: 'Créer le projet d’exemple' }));
    expect(await screen.findByTestId('project-CONGES')).toHaveTextContent('Demande de congés');
    const [call] = callsTo(sent, 'POST', '/api/desktop/sample-project');
    expect([call?.body, call?.headers.get('X-Requested-With')]).toEqual([undefined, 'XMLHttpRequest']);
    expect(toasts.getState().toasts.map((toast) => [toast.kind, toast.text])).toEqual([['success', 'Projet d’exemple CONGES créé.']]);
    expect(screen.queryByTestId('home-sample')).not.toBeInTheDocument();
    await waitFor(() => {
      expect(screen.getByRole('button', { name: 'Décrire un processus' })).toBeEnabled();
    });
  });

  it('a refusal of the host is said under the card, which stays to try again', async () => {
    start({ json: [] }, DESKTOP, { refused: 409, code: 'PROJET_CODE_CORBEILLE', message: 'Le code CONGES est dans la corbeille.' });
    await userEvent.click(await screen.findByRole('button', { name: 'Créer le projet d’exemple' }));
    expect(await screen.findByTestId('home-sample-error')).toHaveTextContent('Le code CONGES est dans la corbeille.');
    expect(screen.getByRole('button', { name: 'Créer le projet d’exemple' })).toBeEnabled();
  });

  it('never in server mode, never once a project is there, never to a user who is not an administrator', async () => {
    for (const [projects, profile] of [[[], { is_admin: true }], [[CONGES], DESKTOP], [[], { desktop: true }]] as const) {
      const sent = start({ json: projects }, profile);
      await screen.findByTestId(projects.length === 0 ? 'home-empty' : 'project-CONGES');
      expect(screen.queryByTestId('home-sample')).not.toBeInTheDocument();
      expect(callsTo(sent, 'POST', '/api/desktop/sample-project')).toEqual([]);
      act(() => {
        document.body.innerHTML = '';
      });
    }
  });
});

describe('the classification on the card of a project (S-4.1-07)', () => {
  it('each project says its level after its role, in the language shown', async () => {
    start({ json: [CONGES, { ...CONGES, id: 'p-vte', code: 'VTE', classification: 'internal' }] }, {});
    const card = await screen.findByTestId('project-CONGES');
    expect(card).toHaveTextContent('23 étapes·3 cartes·Animateur·Confidentiel');
    expect(screen.getByTestId('project-VTE-classification')).toHaveTextContent('Interne');
    expect(within(card).getByTestId('project-CONGES-classification').querySelector('svg.i')).not.toBeNull();
    await act(async () => {
      await i18next.changeLanguage('en');
    });
    expect(screen.getByTestId('project-CONGES-classification')).toHaveTextContent('Confidential');
  });
});
