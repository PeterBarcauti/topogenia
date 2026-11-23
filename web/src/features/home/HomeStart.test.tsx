// HomeStart.test.tsx
// The starts of the home page (ADR 111): the main button of an administrator without a project and the button « New project » open the same menu; the gallery opens the site in the browser; the others keep what they had.

import { act, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { Schema } from '../../api/client.ts';
import { App } from '../../app/App.tsx';
import { i18next } from '../../shared/i18n/i18n.ts';
import { fakeApi } from '../../test/fakeApi.ts';
import { TEST_LINKS, fakeSource, profileOf } from '../../test/profiles.ts';
import type { ProjectItem } from './focus.ts';

const VIS: ProjectItem = {
  id: 'p-vis', code: 'VIS', name: 'Visites', source_language: 'fr', name_translations: {}, description: null, classification: 'internal', root_map_id: 'm-vis', role: 'facilitator',
  step_count: 2, map_count: 1,
};

function start(projects: ProjectItem[], profile: Partial<Schema<'ProfileView'>> = { is_admin: true, desktop: true }) {
  fakeApi({ 'GET /api/projects': { json: projects }, 'GET /api/trash': { json: { retention_days: 30, projects: [] } }, 'GET /api/projects/p-vis/participants': { json: [] } });
  render(<App source={fakeSource([profileOf(profile)]).source} />);
}

const names = () => within(screen.getByRole('menu')).getAllByRole('menuitem').map((entry) => entry.textContent);

beforeEach(async () => {
  await i18next.changeLanguage('fr');
});

afterEach(() => {
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
  act(() => {
    document.body.innerHTML = '';
  });
  window.history.replaceState(null, '', '/');
});

describe('the starts of the home page', () => {
  it('without a project, the main button of the administrator opens the starts and Escape gives the focus back', async () => {
    start([]);
    const button = await screen.findByRole('button', { name: 'Décrire un processus' });
    await waitFor(() => {
      expect(button).toBeEnabled();
    });
    expect([button.getAttribute('aria-haspopup'), button.getAttribute('title')]).toEqual(['menu', 'Choisir un point de départ.']);
    expect(button).toHaveAttribute('aria-expanded', 'false');
    await userEvent.click(button);
    expect(button).toHaveAttribute('aria-expanded', 'true');
    expect(screen.getByRole('menu', { name: 'Commencer un projet' })).toBeInTheDocument();
    expect(names()).toEqual(['Projet d’exemple', 'Galerie de modèles', 'Carte vide…', 'Décrire avec l’IA', 'Ouvrir un fichier…']);
    await userEvent.keyboard('{Escape}');
    expect(screen.queryByRole('menu')).toBeNull();
    expect(button).toHaveFocus();
  });

  it('with projects, the main button opens the map put forward', async () => {
    start([VIS]);
    const button = await screen.findByRole('button', { name: 'Décrire un processus' });
    await waitFor(() => {
      expect(button).toBeEnabled();
    });
    expect(button).not.toHaveAttribute('aria-haspopup');
    await userEvent.click(button);
    expect(screen.queryByRole('menu')).toBeNull();
    expect(window.location.pathname).toBe('/maps/m-vis');
  });

  it('New project opens the same starts, Blank map first, which Enter chooses', async () => {
    start([VIS]);
    const button = await screen.findByRole('button', { name: 'Nouveau projet' });
    expect(button).toHaveAttribute('aria-haspopup', 'menu');
    expect(button).toHaveAttribute('aria-expanded', 'false');
    await userEvent.click(button);
    expect(button).toHaveAttribute('aria-expanded', 'true');
    expect(names()).toEqual(['Carte vide…', 'Projet d’exemple', 'Galerie de modèles', 'Décrire avec l’IA', 'Ouvrir un fichier…']);
    expect(screen.getByRole('menuitem', { name: 'Carte vide…' })).toHaveFocus();
    await userEvent.keyboard('{Enter}');
    expect(window.location.pathname).toBe('/projects/new');
  });

  it('the starts that open a dialog open it: describe with the assistant, open a file', async () => {
    start([]);
    await userEvent.click(await screen.findByRole('button', { name: 'Nouveau projet' }));
    await userEvent.click(screen.getByRole('menuitem', { name: 'Ouvrir un fichier…' }));
    expect(await screen.findByRole('dialog', { name: 'Ouvrir un fichier' })).toBeInTheDocument();
    await userEvent.click(screen.getByTestId('open-pmap-cancel'));
    await userEvent.click(screen.getByRole('button', { name: 'Nouveau projet' }));
    await userEvent.click(screen.getByRole('menuitem', { name: 'Décrire avec l’IA' }));
    expect(await screen.findByTestId('assistant-field')).toBeInTheDocument();
  });

  it('the gallery opens the website in a new tab, in the language of the interface, and nothing else', async () => {
    const open = vi.spyOn(window, 'open').mockReturnValue(null);
    start([VIS]);
    await userEvent.click(await screen.findByRole('button', { name: 'Nouveau projet' }));
    expect(screen.getByRole('menuitem', { name: 'Galerie de modèles' })).toHaveAttribute('title', 'S’ouvre dans le navigateur.');
    await userEvent.click(screen.getByRole('menuitem', { name: 'Galerie de modèles' }));
    expect(open.mock.calls).toEqual([[`${TEST_LINKS.site}/modeles/`, '_blank', 'noopener,noreferrer']]);
    expect(window.location.pathname).toBe('/');
  });

  it('no gallery when the host gives no link, no sample in server mode', async () => {
    start([], { is_admin: true, desktop: false, links: null });
    await userEvent.click(await screen.findByRole('button', { name: 'Nouveau projet' }));
    expect(names()).toEqual(['Carte vide…', 'Décrire avec l’IA', 'Ouvrir un fichier…']);
  });

  it('a user who is not administrator keeps a greyed main button and no start', async () => {
    start([], { is_admin: false });
    const button = await screen.findByRole('button', { name: 'Décrire un processus' });
    expect(button).toBeDisabled();
    await userEvent.click(button);
    expect(screen.queryByRole('menu')).toBeNull();
    expect(screen.queryByTestId('home-new-project')).toBeNull();
    expect(screen.getByTestId('home-empty')).toHaveTextContent('L’animateur du projet enverra une invitation.');
  });
});

describe('the tip of New project', () => {
  it('the tip of New project promises only what its menu offers', async () => {
    const tip = async (profile: Partial<Schema<'ProfileView'>>) => {
      start([VIS], profile);
      const button = await screen.findByRole('button', { name: 'Nouveau projet' });
      const text = button.getAttribute('title');
      act(() => {
        document.body.innerHTML = '';
      });
      return text;
    };
    expect(await tip({ is_admin: true, desktop: true })).toBe('Partir d’une carte vide, d’un exemple ou d’un modèle.');
    expect(await tip({ is_admin: true, desktop: false })).toBe('Partir d’une carte vide ou d’un modèle.');
    expect(await tip({ is_admin: true, desktop: true, links: null })).toBe('Partir d’une carte vide ou d’un exemple.');
    expect(await tip({ is_admin: true, desktop: false, links: null })).toBe('Partir d’une carte vide.');
  });
});
