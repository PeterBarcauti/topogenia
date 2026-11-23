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

const VIS = item('VIS', 'p-vis', { role: 'facilitator', step_count: 1, map_count: 2 });
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
    start({ 'GET /api/projects': { json: [VIS, ACH] }, 'GET /api/projects/p-vis/participants': { json: [] } }, { last_project_id: 'P-VIS' });
    expect(await screen.findByRole('heading', { level: 1 })).toHaveTextContent('Cartographiez vos processus réels pour automatiser les actions répétitives et gagner en efficacité.');
    expect(screen.getByText('Pour booster vos processus avec l’IA, commencez par les décrire.')).toBeInTheDocument();
    await userEvent.click(await screen.findByRole('button', { name: 'Décrire un processus' }));
    expect(window.location.pathname).toBe('/maps/p-vis-map');
  });

  it('before any opening, the first project by code', async () => {
    start({ 'GET /api/projects': { json: [VIS, ACH] }, 'GET /api/projects/p-ach/participants': { json: [] } });
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
    start({ 'GET /api/projects': { json: [VIS, ACH] }, 'GET /api/projects/p-ach/participants': { json: [] } }, { language: 'en', effective_language: 'en' });
    expect(await screen.findByRole('heading', { level: 1 })).toHaveTextContent('Map your real processes to automate repetitive actions and work more efficiently.');
    expect(screen.getByText('To boost your processes with AI, start by describing them.')).toBeInTheDocument();
    expect(await screen.findByRole('button', { name: 'Describe a process' })).toBeInTheDocument();
  });
});

describe('the projects (B-1.3-05 to B-1.3-14)', () => {
  it('a header of section; the administrator opens a .pmap, imports a package, creates a project', async () => {
    start({ 'GET /api/projects': { json: [] } }, { is_admin: true });
    expect(await screen.findByRole('heading', { level: 2, name: 'Projets' })).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Ouvrir un fichier…' }));
    expect(screen.getByRole('dialog', { name: 'Ouvrir un fichier' })).toBeInTheDocument();
    await userEvent.click(screen.getByTestId('open-pmap-cancel'));
    await userEvent.click(screen.getByTestId('home-import-package'));
    expect(screen.getByTestId('import-package')).toBeInTheDocument();
    await userEvent.click(screen.getByTestId('import-package-cancel'));
    await userEvent.click(screen.getByRole('button', { name: 'Nouveau projet' }));
    await userEvent.click(screen.getByRole('menuitem', { name: 'Carte vide…' }));
    expect(window.location.pathname).toBe('/projects/new');
  });

  it('none of these buttons for the others', async () => {
    start({ 'GET /api/projects': { json: [] } });
    await screen.findByTestId('home-empty');
    for (const id of ['home-open-pmap', 'home-import-package', 'home-new-project']) expect(screen.queryByTestId(id)).not.toBeInTheDocument();
  });

  it('each project: name in the language shown, link to its root map, code, description, counts and role', async () => {
    start({ 'GET /api/projects': { json: [VIS, ACH] }, 'GET /api/projects/p-ach/participants': { json: [] } });
    const card = await screen.findByTestId('project-VIS');
    expect(within(card).getByRole('link', { name: 'Processus VIS' })).toHaveAttribute('href', '/maps/p-vis-map');
    expect(within(card).getByText('VIS')).toHaveClass('chip-code');
    expect(card).toHaveTextContent('Description VIS');
    expect(card).toHaveTextContent('1 étape·2 cartes·Animateur');
    expect(screen.getByTestId('project-ACH')).toHaveTextContent('3 étapes·1 carte·Contributeur');
    await act(async () => {
      await i18next.changeLanguage('en');
    });
    await userEvent.click(within(card).getByRole('link', { name: 'Process VIS' }));
    expect(window.location.pathname).toBe('/maps/p-vis-map');
  });

  it('the facilitator has Members and « Supprimer… » in red; the others none', async () => {
    start({ 'GET /api/projects': { json: [VIS, ACH] }, 'GET /api/projects/p-ach/participants': { json: [] } });
    const card = await screen.findByTestId('project-VIS');
    expect(within(card).getByRole('button', { name: 'Supprimer…' })).toHaveClass('btn--danger');
    expect(within(screen.getByTestId('project-ACH')).queryAllByRole('button')).toEqual([]);
    await userEvent.click(within(card).getByRole('button', { name: 'Membres' }));
    expect(window.location.pathname).toBe('/projects/p-vis/members');
  });

  it('a failure to read the projects is said', async () => {
    start({ 'GET /api/projects': { refused: 500, code: 'ERREUR_INTERNE', message: 'Erreur interne.' } });
    expect(await screen.findByRole('alert')).toHaveTextContent('Erreur interne.');
  });
});

describe('the participants (B-1.3-24 to B-1.3-27)', () => {
  it('those of the project put forward, with their initials and Sponsor', async () => {
    const sent = start({
      'GET /api/projects': { json: [VIS, ACH] },
      'GET /api/projects/p-vis/participants': { json: [participant('Camille Bernard', true), participant('karim', false)] },
    }, { last_project_id: 'p-vis' });
    const column = await screen.findByTestId('participants');
    await within(column).findByText('Camille Bernard');
    expect(within(column).getByRole('heading', { name: /Qui participe/ })).toHaveTextContent('Processus VIS');
    expect([...column.querySelectorAll('.initials')].map((badge) => badge.textContent)).toEqual(['CB', 'KA']);
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

describe('the filter of the projects (3.4, lot 2)', () => {
  const TEAM = new Set(['P02', 'P05', 'P09']);
  const many = Array.from({ length: 12 }, (_unused, index) => {
    const code = `P${String(index + 1).padStart(2, '0')}`;
    return item(code, `p-${code.toLowerCase()}`, { name: TEAM.has(code) ? `Équipe ${code}` : `Atelier ${code}`, name_translations: {} });
  });
  const OLD: Schema<'TrashItem'> = { id: 't-old', code: 'OLDA', name: 'Équipe ancienne', deleted_at: '2026-10-20T10:00:00Z', deleted_by: null, purge_at: '2026-11-19T10:00:00Z', step_count: 4 };
  const field = () => screen.getByRole('searchbox', { name: 'Filtrer les projets' });
  const listed = () => screen.getAllByTestId(/^project-P\d+$/).map((card) => card.getAttribute('data-testid'));

  it('the field filters the list and the count reads 3 of 12', async () => {
    start({ 'GET /api/projects': { json: many }, 'GET /api/projects/p-p01/participants': { json: [] } });
    await screen.findByTestId('project-P01');
    expect(field()).toHaveAttribute('placeholder', 'Code ou nom');
    expect(document.querySelector('.home-col-head .count')).toHaveTextContent('12');
    await userEvent.type(field(), 'equipe');
    expect(listed()).toEqual(['project-P02', 'project-P05', 'project-P09']);
    expect(document.querySelector('.home-col-head .count')).toHaveTextContent('3 sur 12');
    await userEvent.clear(field());
    await userEvent.type(field(), 'p1');
    expect(listed()).toEqual(['project-P10', 'project-P11', 'project-P12']);
  });

  it('nothing found says so, Escape empties the field and the whole list comes back', async () => {
    start({ 'GET /api/projects': { json: many }, 'GET /api/projects/p-p01/participants': { json: [] } });
    await screen.findByTestId('project-P01');
    await userEvent.type(field(), 'zzz');
    expect(screen.getByText('Aucun projet ne correspond.')).toBeInTheDocument();
    expect(screen.queryByTestId('project-P01')).not.toBeInTheDocument();
    await userEvent.keyboard('{Escape}');
    expect(field()).toHaveValue('');
    expect(screen.queryByText('Aucun projet ne correspond.')).not.toBeInTheDocument();
    expect(listed()).toHaveLength(12);
    expect(document.querySelector('.home-col-head .count')).toHaveTextContent('12');
  });

  it('the count and the message of an empty filter are announced to a screen reader', async () => {
    start({ 'GET /api/projects': { json: many }, 'GET /api/projects/p-p01/participants': { json: [] } });
    await screen.findByTestId('project-P01');
    await userEvent.type(field(), 'equipe');
    const count = screen.getByText('3 sur 12');
    expect(count).toHaveAttribute('role', 'status');
    await userEvent.clear(field());
    await userEvent.type(field(), 'zzz');
    expect(screen.getByText('Aucun projet ne correspond.')).toHaveAttribute('role', 'status');
    expect(screen.getByText('0 sur 12')).toHaveAttribute('role', 'status');
  });

  it('the field is there only when there are projects', async () => {
    start({ 'GET /api/projects': { json: [] } });
    await screen.findByTestId('home-empty');
    expect(screen.queryByRole('searchbox')).not.toBeInTheDocument();
  });

  it('the project put forward and the trash do not change', async () => {
    const sent = start({
      'GET /api/projects': { json: many }, 'GET /api/projects/p-p09/participants': { json: [participant('Camille Bernard', false)] },
      'GET /api/trash': { json: { retention_days: 30, projects: [OLD] } },
    }, { last_project_id: 'p-p09' });
    await screen.findByTestId('project-P01');
    await userEvent.type(field(), 'atelier');
    expect(listed()).toHaveLength(9);
    // The project put forward is outside the filter: the main button and the participants keep it.
    expect(screen.queryByTestId('project-P09')).not.toBeInTheDocument();
    expect(await screen.findByTestId('participants')).toHaveTextContent('Équipe P09');
    expect(callsTo(sent, 'GET', '/api/projects/p-p09/participants')).toHaveLength(1);
    // The trash is outside the filter too: « Équipe ancienne » does not hold « atelier » and stays.
    expect(screen.getByTestId('trash-OLDA')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Décrire un processus' }));
    expect(window.location.pathname).toBe('/maps/p-p09-map');
  });
});
