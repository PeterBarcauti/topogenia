// HomeScreen.test.tsx
// The home page: banner and main button, projects, trash, deletion by the code, who takes part, the layout (annex B 1.3, AR-49, AR-88).

import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { act, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { Schema } from '../../api/client.ts';
import { App } from '../../app/App.tsx';
import { i18next } from '../../shared/i18n/i18n.ts';
import { toasts } from '../../shared/ui/toasts.ts';
import { callsTo, fakeApi, type FakeRoutes } from '../../test/fakeApi.ts';
import { fakeSource, profileOf } from '../../test/profiles.ts';
import type { ProjectItem } from './focus.ts';

const item = (code: string, id: string, overrides: Partial<ProjectItem> = {}): ProjectItem => ({
  id, code, name: `Processus ${code}`, source_language: 'fr', name_translations: { en: `Process ${code}` }, description: `Description ${code}`,
  classification: 'internal', root_map_id: `${id}-map`, role: 'contributor', step_count: 3, map_count: 1, ...overrides,
});

const VTE = item('VTE', 'p-vte', { role: 'facilitator', step_count: 1, map_count: 2 });
const ACH = item('ACH', 'p-ach', { description: null });

const participant = (name: string, sponsor: boolean): Schema<'MemberView'> =>
  ({ user_id: name, login: `FICTIF\\${name}`, display_name: name, role: 'reader', is_sponsor: sponsor, is_key_contributor: false, display_order: 0 });

function start(routes: FakeRoutes, profile: Partial<Schema<'ProfileView'>> = {}) {
  const sent = fakeApi({ 'GET /api/trash': { json: { retention_days: 30, projects: [] } }, ...routes });
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

describe('the banner (B-1.3-01 to B-1.3-03, AR-88)', () => {
  it('says what the tool is for, and « Décrire un processus » opens the map of the last project opened (AR-49)', async () => {
    start({ 'GET /api/projects': { json: [VTE, ACH] }, 'GET /api/projects/p-vte/participants': { json: [] } }, { last_project_id: 'P-VTE' });
    expect(await screen.findByRole('heading', { level: 1 })).toHaveTextContent('Cartographiez vos processus réels pour automatiser les actions répétitives et gagner en efficacité.');
    expect(screen.getByText('Pour booster vos processus avec l’IA, commencez par les décrire.')).toBeInTheDocument();
    await userEvent.click(await screen.findByRole('button', { name: 'Décrire un processus' }));
    expect(window.location.pathname).toBe('/maps/p-vte-map');
  });

  it('before any opening, the first project by code', async () => {
    start({ 'GET /api/projects': { json: [VTE, ACH] }, 'GET /api/projects/p-ach/participants': { json: [] } });
    const button = await screen.findByRole('button', { name: 'Décrire un processus' });
    await waitFor(() => {
      expect(button).toBeEnabled();
    });
    await userEvent.click(button);
    expect(window.location.pathname).toBe('/maps/p-ach-map');
  });

  it('is disabled without a project, and the empty message differs for the administrator (B-1.3-08)', async () => {
    start({ 'GET /api/projects': { json: [] } });
    expect(await screen.findByTestId('home-empty')).toHaveTextContent('L’animateur du projet enverra une invitation.');
    expect(screen.getByRole('button', { name: 'Décrire un processus' })).toBeDisabled();
  });

  it('the empty message of the administrator invites to create the first project', async () => {
    start({ 'GET /api/projects': { json: [] } }, { is_admin: true });
    expect(await screen.findByTestId('home-empty')).toHaveTextContent('Aucun projet pour l’instant. Créer le premier.');
  });

  it('in English, the texts fixed with AR-88 word for word: the title, the line above it and the main button', async () => {
    start({ 'GET /api/projects': { json: [VTE, ACH] }, 'GET /api/projects/p-ach/participants': { json: [] } }, { language: 'en', effective_language: 'en' });
    expect(await screen.findByRole('heading', { level: 1 })).toHaveTextContent('Map your real processes to automate repetitive actions and work more efficiently.');
    expect(screen.getByText('To boost your processes with AI, start by describing them.')).toBeInTheDocument();
    expect(await screen.findByRole('button', { name: 'Describe a process' })).toBeInTheDocument();
  });
});

describe('the projects (B-1.3-05 to B-1.3-14)', () => {
  it('a header of section; the administrator opens a .pmap, imports a package, creates a project', async () => {
    start({ 'GET /api/projects': { json: [] } }, { is_admin: true });
    expect(await screen.findByRole('heading', { level: 2, name: 'Projets' })).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Ouvrir un fichier .pmap…' }));
    expect(screen.getByRole('dialog', { name: 'Ouvrir un fichier .pmap' })).toBeInTheDocument();
    await userEvent.click(screen.getByTestId('open-pmap-cancel'));
    await userEvent.click(screen.getByTestId('home-import-package'));
    expect(screen.getByTestId('import-package')).toBeInTheDocument();
    await userEvent.click(screen.getByTestId('import-package-cancel'));
    await userEvent.click(screen.getByRole('button', { name: 'Nouveau projet' }));
    expect(window.location.pathname).toBe('/projects/new');
  });

  it('none of these buttons for the others', async () => {
    start({ 'GET /api/projects': { json: [] } });
    await screen.findByTestId('home-empty');
    for (const id of ['home-open-pmap', 'home-import-package', 'home-new-project']) expect(screen.queryByTestId(id)).not.toBeInTheDocument();
  });

  it('each project: name in the language shown, link to its root map, code, description, counts and role', async () => {
    start({ 'GET /api/projects': { json: [VTE, ACH] }, 'GET /api/projects/p-ach/participants': { json: [] } });
    const card = await screen.findByTestId('project-VTE');
    expect(within(card).getByRole('link', { name: 'Processus VTE' })).toHaveAttribute('href', '/maps/p-vte-map');
    expect(within(card).getByText('VTE')).toHaveClass('chip-code');
    expect(card).toHaveTextContent('Description VTE');
    expect(card).toHaveTextContent('1 étape·2 cartes·Animateur');
    expect(screen.getByTestId('project-ACH')).toHaveTextContent('3 étapes·1 carte·Contributeur');
    await act(async () => {
      await i18next.changeLanguage('en');
    });
    await userEvent.click(within(card).getByRole('link', { name: 'Process VTE' }));
    expect(window.location.pathname).toBe('/maps/p-vte-map');
  });

  it('the facilitator has Members and « Supprimer… » in red; the others none', async () => {
    start({ 'GET /api/projects': { json: [VTE, ACH] }, 'GET /api/projects/p-ach/participants': { json: [] } });
    const card = await screen.findByTestId('project-VTE');
    expect(within(card).getByRole('button', { name: 'Supprimer…' })).toHaveClass('btn--danger');
    expect(within(screen.getByTestId('project-ACH')).queryAllByRole('button')).toEqual([]);
    await userEvent.click(within(card).getByRole('button', { name: 'Membres' }));
    expect(window.location.pathname).toBe('/projects/p-vte/members');
  });

  it('a failure to read the projects is said', async () => {
    start({ 'GET /api/projects': { refused: 500, code: 'ERREUR_INTERNE', message: 'Erreur interne.' } });
    expect(await screen.findByRole('alert')).toHaveTextContent('Erreur interne.');
  });
});

describe('the participants (B-1.3-24 to B-1.3-27)', () => {
  it('those of the project put forward, with their initials and Sponsor', async () => {
    const sent = start({
      'GET /api/projects': { json: [VTE, ACH] },
      'GET /api/projects/p-vte/participants': { json: [participant('Camille Bernard', true), participant('joao', false)] },
    }, { last_project_id: 'p-vte' });
    const column = await screen.findByTestId('participants');
    await within(column).findByText('Camille Bernard');
    expect(within(column).getByRole('heading', { name: /Qui participe/ })).toHaveTextContent('Processus VTE');
    expect([...column.querySelectorAll('.initials')].map((badge) => badge.textContent)).toEqual(['CB', 'JO']);
    expect(within(column).getAllByText('Sponsor')).toHaveLength(1);
    expect(callsTo(sent, 'GET', '/api/projects/p-ach/participants')).toEqual([]);
  });
});

describe('the layout (B-1.3-04, B-1.3-28)', () => {
  const css = readFileSync(join(import.meta.dirname, '../../styles/pages.css'), 'utf8');

  it('projects on two thirds, participants on one third; one column below 900 px', () => {
    expect(css).toMatch(/\.home-cols \{[^}]*grid-template-columns: 2fr 1fr;/);
    expect(css).toMatch(/@media \(max-width: 899px\) \{[^@]*\.home-cols \{ grid-template-columns: 1fr; \}/);
  });
});
