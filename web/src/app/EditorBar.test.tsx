// EditorBar.test.tsx
// The bar of the editor in the header: File, breadcrumb and Members, in the place of the signature; the File menu of the map by role; the project opened remembered (annex B 2.1, 2.4, AR-49, AR-155).

import { act, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { Schema } from '../api/client.ts';
import { PACKAGE_EXTENSION, i18next } from '../shared/i18n/i18n.ts';
import { openProject, type OpenProject } from '../shared/state/openProject.ts';
import { toasts } from '../shared/ui/toasts.ts';
import { fakeApi, type FakeRoutes } from '../test/fakeApi.ts';
import { MAP_ID, PROJECT_ID, sampleMap } from '../test/maps.ts';
import { fakeSource, profileOf } from '../test/profiles.ts';
import { App } from './App.tsx';

const ROOT = '0190a1b2-0000-7000-8000-00000000a000';
const DEEP = '0190a1b2-0000-7000-8000-00000000a002';

/** A sub-map of the project VIS, under its root map named as the project. */
const subMap = (overrides: Partial<Schema<'MapView'>> = {}) => sampleMap({
  name: 'Traiter la commande', depth: 2,
  breadcrumb: [{ map_id: ROOT, name: 'Visites', source_language: 'fr', translations: { en: 'Visits' } }, { map_id: MAP_ID, name: 'Traiter la commande', source_language: 'fr', translations: {} }],
  ...overrides,
});

/** The application on a map read from the fake API; gives the calls sent and the projects the profile remembered. */
function startOnMap(map: Schema<'MapView'>, profile: Partial<Schema<'ProfileView'>> = {}, routes: FakeRoutes = {}) {
  window.history.replaceState(null, '', `/maps/${map.id}`);
  const sent = fakeApi({ [`GET /api/maps/${map.id}`]: { json: { map, history: { undo: [], redo: [] } } }, ...routes });
  const { source, remembered } = fakeSource([profileOf(profile)]);
  render(<App source={source} />);
  return { sent, remembered };
}

/** The application on the home page, the open project declared by hand. */
async function startWith(project: Partial<OpenProject> = {}) {
  fakeApi({ 'GET /api/projects': { json: [] }, 'GET /api/trash': { json: { retention_days: 30, projects: [] } } });
  render(<App source={fakeSource([profileOf()]).source} />);
  await screen.findByTestId('app-header');
  const declared: OpenProject = {
    id: PROJECT_ID, code: 'VIS', canEdit: true, openFileMenu: vi.fn(), languages: { origin: 'fr', total: 3, written: { fr: 3, en: 2 } },
    breadcrumb: [{ mapId: ROOT, name: 'Visites', sourceLanguage: 'fr', translations: { en: 'Visits' } }, { mapId: MAP_ID, name: 'Traiter la commande', sourceLanguage: 'fr', translations: { en: 'Handle the order' } }, { mapId: DEEP, name: 'Contrôler', sourceLanguage: 'fr', translations: {} }],
    ...project,
  };
  act(() => {
    openProject.setState({ project: declared });
  });
  return declared;
}

beforeEach(async () => {
  await i18next.changeLanguage('fr');
});

afterEach(() => {
  act(() => {
    toasts.setState({ toasts: [] });
    openProject.setState({ project: null });
  });
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
  window.history.replaceState(null, '', '/');
});

describe('the bar of the editor in the header (B-2.1-01 to B-2.1-06)', () => {
  it('on a map, the header of 48 px holds the breadcrumb in the place of the signature, which comes back on the home page (B-2.1-04)', async () => {
    startOnMap(subMap());
    const header = await screen.findByTestId('app-header');
    const crumbs = await within(header).findByRole('navigation', { name: 'Fil d’Ariane' });
    expect(crumbs).toHaveClass('crumbs');
    expect(within(header).queryByText('Boostez vos processus avec l’IA')).toBeNull();
    await userEvent.click(within(crumbs).getByRole('link', { name: 'Visites' }));
    await waitFor(() => {
      expect(window.location.pathname).toBe(`/maps/${ROOT}`);
    });
    act(() => {
      window.history.pushState(null, '', '/');
      window.dispatchEvent(new PopStateEvent('popstate'));
    });
    expect(await within(header).findByText('Boostez vos processus avec l’IA')).toBeInTheDocument();
    expect(within(header).queryByRole('navigation')).toBeNull();
  });

  it('links to the parent maps, the map shown last with aria-current, a chevron between two, the code of the project; names in the language shown', async () => {
    await startWith();
    const crumbs = screen.getByTestId('breadcrumb');
    expect(within(crumbs).getAllByRole('link').map((link) => [link.textContent, link.getAttribute('href')])).toEqual([
      ['Visites', `/maps/${ROOT}`], ['Traiter la commande', `/maps/${MAP_ID}`],
    ]);
    const current = within(crumbs).getByText('Contrôler');
    expect([current.tagName, current.getAttribute('aria-current')]).toEqual(['SPAN', 'page']);
    expect(crumbs.querySelectorAll('svg.i[aria-hidden="true"]')).toHaveLength(2);
    expect(crumbs.querySelector('.chip-code')).toHaveTextContent('VIS');
    await userEvent.click(within(crumbs).getByRole('link', { name: 'Traiter la commande' }));
    expect(window.location.pathname).toBe(`/maps/${MAP_ID}`);
    await act(async () => {
      await i18next.changeLanguage('en');
    });
    expect(within(crumbs).getAllByRole('link').map((link) => link.textContent)).toEqual(['Visits', 'Handle the order']);
  });

  it('each level of the breadcrumb carries a stable identifier by its depth, link or map shown (S-3.5-04)', async () => {
    await startWith();
    const crumbs = screen.getByTestId('breadcrumb');
    expect(['breadcrumb-level-0', 'breadcrumb-level-1', 'breadcrumb-level-2'].map((id) => {
      const level = within(crumbs).getByTestId(id);
      return [level.tagName, level.textContent, level.getAttribute('href')];
    })).toEqual([['A', 'Visites', `/maps/${ROOT}`], ['A', 'Traiter la commande', `/maps/${MAP_ID}`], ['SPAN', 'Contrôler', null]]);
  });

  it('Fichier stands next to the brand, before the breadcrumb; Membres after it (AR-155)', async () => {
    await startWith();
    const header = screen.getByTestId('app-header');
    const order = [...header.querySelectorAll('[data-testid]')].map((element) => element.getAttribute('data-testid'))
      .filter((id) => ['header-brand', 'header-file', 'breadcrumb', 'header-members'].includes(id ?? ''));
    expect(order).toEqual(['header-brand', 'header-file', 'breadcrumb', 'header-members']);
  });

  it('Fichier opens the File menu under its button (B-2.1-05, B-2.4-52)', async () => {
    const project = await startWith();
    const file = screen.getByTestId('header-file');
    expect([file.textContent, file.getAttribute('aria-haspopup'), file.getAttribute('title'), file.querySelectorAll('svg.i').length])
      .toEqual(['Fichier', 'menu', 'Ouvrir un fichier, télécharger une copie, dupliquer, exporter', 2]);
    vi.spyOn(file, 'getBoundingClientRect').mockReturnValue(new DOMRect(120, 9, 90, 30));
    await userEvent.click(file);
    expect(project.openFileMenu).toHaveBeenCalledWith({ x: 120, y: 43 });
  });

  it('Membres shows for who may change the structure, and opens the members of the project (B-2.1-06)', async () => {
    await startWith({ canEdit: false });
    expect(screen.queryByTestId('header-members')).toBeNull();
    act(() => {
      const project = openProject.getState().project;
      if (project !== null) openProject.setState({ project: { ...project, canEdit: true } });
    });
    await userEvent.click(screen.getByRole('button', { name: 'Membres' }));
    expect(window.location.pathname).toBe(`/projects/${PROJECT_ID}/members`);
  });
});

describe('the File menu of the map (B-2.4-52 to B-2.4-58, ADR 058)', () => {
  // The rows of the File menu in cascade (ADR 117): three entries at a click, four groups after a separator, the properties after another.
  const AT_A_CLICK = ['Enregistrer la carte ouverte (.pmap)', 'Copier la carte en image'];
  const PROPERTIES = '| Propriétés du projet…';
  const entries = () => within(screen.getByRole('menu', { name: 'Menu Fichier' })).getAllByRole('menuitem')
    .map((item) => `${item.classList.contains('menu-item--group') ? '| ' : ''}${item.textContent}`);
  /** The entries of a group of the menu, its short labels, once it is opened by a click on its row. */
  const group = async (name: string) => {
    await userEvent.click(within(screen.getByRole('menu', { name: 'Menu Fichier' })).getByRole('menuitem', { name }));
    return within(screen.getByRole('menu', { name })).getAllByRole('menuitem').map((item) => `${item.classList.contains('menu-item--group') ? '| ' : ''}${item.textContent}`);
  };
  const EXPORTS = ['PDF (A3 paysage)', 'Image PNG', 'Image SVG…', 'Excel', 'Markdown (Mermaid)', 'BPMN 2.0 (.bpmn)…', '| Imprimer en grand…'];

  it('an administrator who leads the project: the .pmap files, the exports of the map, then the package of exchange; the menu under its button', async () => {
    startOnMap(subMap(), { is_admin: true });
    const file = await screen.findByTestId('header-file');
    vi.spyOn(file, 'getBoundingClientRect').mockReturnValue(new DOMRect(120, 9, 90, 30));
    await userEvent.click(file);
    const menu = screen.getByRole('menu', { name: 'Menu Fichier' });
    expect([menu.style.left, menu.style.top]).toEqual(['120px', '43px']);
    expect(entries()).toEqual(['Ouvrir un fichier…', ...AT_A_CLICK, '| Exporter la carte', 'Projet', 'Paquet d’échange', 'Assistant IA', PROPERTIES]);
    expect(await group('Exporter la carte')).toEqual(EXPORTS);
    // The target state is made by an administrator, as a duplicate (ADR 095 point 10).
    expect(await group('Projet')).toEqual(['Tout le projet en PDF…', 'Télécharger une copie (.pmap)', 'Insérer un fichier comme étape détaillée…', 'Dupliquer le projet…', 'Créer l’état cible…']);
    expect(await group('Paquet d’échange')).toEqual([`Exporter le projet (${PACKAGE_EXTENSION})…`, 'Importer un paquet…', 'Remplacer par un paquet…']);
    expect(await group('Assistant IA')).toEqual(['Rapport raconté (Markdown)', 'Résumer les cartes', 'Commentaires de présentation…']);
  });

  it('a header menu too long for the window stays under its button and scrolls', async () => {
    const height = window.innerHeight;
    Object.defineProperty(window, 'innerHeight', { configurable: true, value: 400 });
    try {
      startOnMap(subMap(), { is_admin: true });
      const file = await screen.findByTestId('header-file');
      vi.spyOn(file, 'getBoundingClientRect').mockReturnValue(new DOMRect(120, 9, 90, 30));
      // Drawn, the menu of 21 entries is higher than the window: it does not go up on its button.
      vi.spyOn(HTMLElement.prototype, 'getBoundingClientRect').mockImplementation(function box(this: HTMLElement) {
        return this.classList.contains('menu') ? new DOMRect(120, 43, 240, 760) : new DOMRect();
      });
      await userEvent.click(file);
      const menu = screen.getByRole('menu', { name: 'Menu Fichier' });
      expect([menu.style.left, menu.style.top, menu.style.maxHeight]).toEqual(['120px', '43px', '349px']);
    } finally {
      Object.defineProperty(window, 'innerHeight', { configurable: true, value: height });
    }
  });

  it('a facilitator who is not an administrator: the copy, the exports, then the package of exchange, without the files of the administrator', async () => {
    startOnMap(subMap());
    await userEvent.click(await screen.findByTestId('header-file'));
    expect(entries()).toEqual([...AT_A_CLICK, '| Exporter la carte', 'Projet', 'Paquet d’échange', 'Assistant IA', PROPERTIES]);
    expect(await group('Projet')).toEqual(['Tout le projet en PDF…', 'Télécharger une copie (.pmap)', 'Insérer un fichier comme étape détaillée…']);
    expect(await group('Paquet d’échange')).toEqual([`Exporter le projet (${PACKAGE_EXTENSION})…`, 'Remplacer par un paquet…']);
    expect(await group('Assistant IA')).toEqual(['Rapport raconté (Markdown)', 'Résumer les cartes', 'Commentaires de présentation…']);
  });

  it('a reader who is not an administrator: the copy and the exports of the map, no group of the package of exchange, and no Members button', async () => {
    startOnMap(subMap({ rights: { structure: false, role: 'reader' } }));
    await userEvent.click(await screen.findByTestId('header-file'));
    expect(screen.queryByTestId('header-members')).toBeNull();
    expect(entries()).toEqual([...AT_A_CLICK, '| Exporter la carte', 'Projet', 'Assistant IA', PROPERTIES]);
    expect(await group('Projet')).toEqual(['Tout le projet en PDF…', 'Télécharger une copie (.pmap)']);
    expect(await group('Assistant IA')).toEqual(['Rapport raconté (Markdown)']);
  });

  it('« Propriétés du projet… » opens the properties of the project, for a reader too (spec 4.1)', async () => {
    startOnMap(subMap({ rights: { structure: false, role: 'reader' } }));
    await userEvent.click(await screen.findByTestId('header-file'));
    await userEvent.click(within(screen.getByRole('menu', { name: 'Menu Fichier' })).getByRole('menuitem', { name: 'Propriétés du projet…' }));
    expect(await screen.findByRole('dialog', { name: 'Propriétés du projet VIS' })).toBeInTheDocument();
    expect(screen.queryByRole('menu')).toBeNull();
  });
});

describe('the project opened remembered (AR-49, B-1.3-02, B-1.3-24, B-8.1-42, B-8.1-43)', () => {
  const project = (code: string, id: string, rootMapId: string) => ({
    id, code, name: `Processus ${code}`, source_language: 'fr', name_translations: {}, description: null, root_map_id: rootMapId, role: 'facilitator',
    step_count: 1, map_count: 1,
  });

  it('the map of a project opened makes it the last one, put forward by the home page; opening it again sends nothing', async () => {
    const { remembered } = startOnMap(subMap(), {}, {
      'GET /api/projects': { json: [project('ACH', 'p-ach', 'ach-map'), project('VIS', PROJECT_ID, ROOT)] },
      'GET /api/trash': { json: { retention_days: 30, projects: [] } },
      [`GET /api/projects/${PROJECT_ID}/participants`]: { json: [] },
      'GET /api/projects/p-ach/participants': { json: [] },
      [`GET /api/maps/${ROOT}`]: { json: { map: sampleMap({ id: ROOT }), history: { undo: [], redo: [] } } },
    });
    await screen.findByTestId('breadcrumb');
    await waitFor(() => {
      expect(remembered).toEqual([PROJECT_ID]);
    });
    await userEvent.click(screen.getByRole('link', { name: 'Visites' }));
    await screen.findByTestId('map-canvas');
    await userEvent.click(screen.getByTestId('header-brand'));
    const describe = await screen.findByRole('button', { name: 'Décrire un processus' });
    await waitFor(() => {
      expect(describe).toBeEnabled();
    });
    await userEvent.click(describe);
    expect(window.location.pathname).toBe(`/maps/${ROOT}`);
    expect(remembered).toEqual([PROJECT_ID]);
  });
});
