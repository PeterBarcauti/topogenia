// HomeSample.test.tsx
// The home page and the classification: the sample project offered in desktop mode (a card while there is no project, the menu of the starts always), which opens its map once created, the level of each project on its card (spec 6.1, 4.1).

import { act, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { Schema } from '../../api/client.ts';
import { App } from '../../app/App.tsx';
import { i18next } from '../../shared/i18n/i18n.ts';
import { toasts } from '../../shared/ui/toasts.ts';
import { callsTo, fakeApi, heldReply, type FakeReply } from '../../test/fakeApi.ts';
import { fakeSource, profileOf } from '../../test/profiles.ts';
import type { ProjectItem } from './focus.ts';

const CONGES: ProjectItem = {
  id: 'p-conges', code: 'CONGES', name: 'Demande de congés', source_language: 'fr', name_translations: {}, description: null, classification: 'confidential',
  root_map_id: 'm-conges', role: 'facilitator', step_count: 23, map_count: 3,
};

const created = (code: string, rootMapId: string) => ({ id: `p-${code}`, code, root_map_id: rootMapId, trash_code: null, created_user_ids: [] });

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
    await userEvent.click(screen.getByRole('menuitem', { name: 'Carte vide…' }));
    expect(window.location.pathname).toBe('/projects/new');
  });

  it('the sample created opens its map and says its code', async () => {
    const sent = start({ json: [] }, DESKTOP, { json: created('CONGES', 'm-conges'), status: 201 });
    await userEvent.click(await screen.findByRole('button', { name: 'Créer le projet d’exemple' }));
    await waitFor(() => {
      expect(window.location.pathname).toBe('/maps/m-conges');
    });
    const [call] = callsTo(sent, 'POST', '/api/desktop/sample-project');
    expect([call?.body, call?.headers.get('X-Requested-With')]).toEqual([undefined, 'XMLHttpRequest']);
    expect(toasts.getState().toasts.map((toast) => [toast.kind, toast.text])).toEqual([['success', 'Projet d’exemple CONGES créé.']]);
  });

  it('a sample created from the menu while projects exist takes the code the host gives', async () => {
    const sent = start({ json: [CONGES] }, DESKTOP, { json: created('CONGEA', 'm-conges-a'), status: 201 });
    await userEvent.click(await screen.findByRole('button', { name: 'Nouveau projet' }));
    await userEvent.click(screen.getByRole('menuitem', { name: 'Projet d’exemple' }));
    await waitFor(() => {
      expect(window.location.pathname).toBe('/maps/m-conges-a');
    });
    expect(callsTo(sent, 'POST', '/api/desktop/sample-project')).toHaveLength(1);
    expect(toasts.getState().toasts.map((toast) => toast.text)).toEqual(['Projet d’exemple CONGEA créé.']);
  });

  it('a creation of the sample under way cannot be started again from the menu or the card', async () => {
    const held = heldReply();
    const sent = start({ json: [] }, DESKTOP, held.reply);
    await userEvent.click(await screen.findByRole('button', { name: 'Nouveau projet' }));
    await userEvent.click(screen.getByRole('menuitem', { name: 'Projet d’exemple' }));
    await userEvent.click(screen.getByRole('button', { name: 'Nouveau projet' }));
    await userEvent.click(screen.getByRole('menuitem', { name: 'Projet d’exemple' }));
    await userEvent.click(screen.getByRole('button', { name: 'Décrire un processus' }));
    await userEvent.click(screen.getByRole('menuitem', { name: 'Projet d’exemple' }));
    expect(callsTo(sent, 'POST', '/api/desktop/sample-project')).toHaveLength(1);
    held.give({ json: created('CONGES', 'm-conges'), status: 201 });
    await waitFor(() => {
      expect(window.location.pathname).toBe('/maps/m-conges');
    });
  });

  it('a refusal of the host while projects exist is said on the page, which stays', async () => {
    start({ json: [CONGES] }, DESKTOP, { refused: 409, code: 'PROJET_CODE_CORBEILLE', message: 'Le code CONGES est dans la corbeille.' });
    await userEvent.click(await screen.findByRole('button', { name: 'Nouveau projet' }));
    await userEvent.click(screen.getByRole('menuitem', { name: 'Projet d’exemple' }));
    expect(await screen.findByTestId('home-sample-error')).toHaveTextContent('Le code CONGES est dans la corbeille.');
    expect(window.location.pathname).toBe('/');
  });

  it('a refusal of the host is said under the card, which stays to try again', async () => {
    start({ json: [] }, DESKTOP, { refused: 409, code: 'PROJET_CODE_CORBEILLE', message: 'Le code CONGES est dans la corbeille.' });
    await userEvent.click(await screen.findByRole('button', { name: 'Créer le projet d’exemple' }));
    expect(await screen.findByTestId('home-sample-error')).toHaveTextContent('Le code CONGES est dans la corbeille.');
    expect(screen.getByRole('button', { name: 'Créer le projet d’exemple' })).toBeEnabled();
  });

  it('the card while the list is empty, the menu always in desktop mode; neither in server mode, nor for a user who is not an administrator', async () => {
    for (const [projects, profile, menu] of [[[], { is_admin: true }, false], [[CONGES], DESKTOP, true], [[], { desktop: true }, false]] as const) {
      const sent = start({ json: projects }, profile);
      await screen.findByTestId(projects.length === 0 ? 'home-empty' : 'project-CONGES');
      expect(screen.queryByTestId('home-sample')).not.toBeInTheDocument();
      expect(callsTo(sent, 'POST', '/api/desktop/sample-project')).toEqual([]);
      if (profile.is_admin === true) await userEvent.click(screen.getByRole('button', { name: 'Nouveau projet' }));
      expect(screen.queryByRole('menuitem', { name: 'Projet d’exemple' }) !== null).toBe(menu);
      act(() => {
        document.body.innerHTML = '';
      });
    }
  });
});

describe('the classification on the card of a project (S-4.1-07)', () => {
  it('each project says its level after its role, in the language shown', async () => {
    start({ json: [CONGES, { ...CONGES, id: 'p-vis', code: 'VIS', classification: 'internal' }] }, {});
    const card = await screen.findByTestId('project-CONGES');
    expect(card).toHaveTextContent('23 étapes·3 cartes·Animateur·Confidentiel');
    expect(screen.getByTestId('project-VIS-classification')).toHaveTextContent('Interne');
    expect(within(card).getByTestId('project-CONGES-classification').querySelector('svg.i')).not.toBeNull();
    await act(async () => {
      await i18next.changeLanguage('en');
    });
    expect(screen.getByTestId('project-CONGES-classification')).toHaveTextContent('Confidential');
  });
});
