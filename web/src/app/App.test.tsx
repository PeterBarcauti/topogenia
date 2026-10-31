// App.test.tsx
// The application and its shell: profile loading or its error, header, language of the interface and of the process, screens of the routes, Ctrl+S (B-0-11, annex B 1.1, 1.2, 2.5, ADR 083).

import { StrictMode, useState } from 'react';
import { act, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ApiError } from '../api/errors.ts';
import { setRequestLanguage } from '../api/request.ts';
import { i18next } from '../shared/i18n/i18n.ts';
import { openProject } from '../shared/state/openProject.ts';
import { toasts } from '../shared/ui/toasts.ts';
import { fakeSource, profileOf } from '../test/profiles.ts';
import { App, SCREENS } from './App.tsx';
import type { Profile, ProfileSource } from './profile/profileStore.ts';
import { navigate } from './router.ts';
import type { Screens } from './Shell.tsx';
import { processLanguage } from '../shared/state/processLanguage.ts';

const GUID_A = '9d708a6e-1aca-f111-afa9-985f41535c4a';
const GUID_B = '0e2f4a6c-1aca-f111-afa9-985f41535c4b';

const start = (source: ProfileSource, screens: Screens = SCREENS) =>
  render(<StrictMode><App source={source} screens={screens} /></StrictMode>);

beforeEach(async () => {
  await i18next.changeLanguage('fr');
});

afterEach(() => {
  act(() => {
    toasts.setState({ toasts: [] });
    openProject.setState({ project: null });
  });
  setRequestLanguage(null);
  window.history.replaceState(null, '', '/');
});

describe('the start (B-0-11, B-1.2-01 to B-1.2-03)', () => {
  it('reads the profile, says « Chargement… » meanwhile, then shows the header and the screen', async () => {
    let answer: (profile: Profile) => void = () => undefined;
    const source: ProfileSource = { read: vi.fn(() => new Promise<Profile>((resolve) => { answer = resolve; })), change: vi.fn(), remember: vi.fn() };
    start(source);
    expect(screen.getByTestId('page-loading')).toHaveTextContent('Chargement…');
    expect(screen.queryByTestId('app-header')).not.toBeInTheDocument();
    act(() => {
      answer(profileOf());
    });
    expect(await screen.findByTestId('app-header')).toBeInTheDocument();
    expect(screen.getByTestId('screen-home')).toBeInTheDocument();
    expect(source.read).toHaveBeenCalled();
  });

  it('shows the error and « Réessayer » when no profile could be read; trying again succeeds', async () => {
    const { source } = fakeSource([new ApiError(0, 'RESEAU', ''), new ApiError(0, 'RESEAU', ''), profileOf()]);
    start(source);
    const page = await screen.findByTestId('page-profile-error');
    expect(page).toHaveAttribute('role', 'alert');
    expect(page).toHaveTextContent(i18next.t('errors.RESEAU'));
    await userEvent.click(within(page).getByRole('button', { name: 'Réessayer' }));
    expect(await screen.findByText('Camille Bernard')).toBeInTheDocument();
  });
});

describe('the header (B-1.2-07 to B-1.2-14)', () => {
  it('brand with the logo and the name, link to the home page whose tooltip gives the version', async () => {
    window.history.replaceState(null, '', '/projects/new');
    start(fakeSource([profileOf()]).source);
    const brand = await screen.findByTestId('header-brand');
    expect(brand).toHaveTextContent(i18next.t('app.name'));
    expect(brand).toHaveAttribute('href', '/');
    expect(brand).toHaveAttribute('title', `Retour à l’accueil · ${i18next.t('app.name')} ${__APP_VERSION__}`);
    expect(brand.querySelector('svg.logo')).not.toBeNull();
    await userEvent.click(brand);
    expect(window.location.pathname).toBe('/');
    expect(screen.getByTestId('screen-home')).toBeInTheDocument();
  });

  it('the signature, the name of the user and his initials, and no « For me » button (B-1.2-10, 11, 14)', async () => {
    start(fakeSource([profileOf()]).source);
    const header = await screen.findByTestId('app-header');
    expect(within(header).getByText('Boostez vos processus avec l’IA')).toBeInTheDocument();
    expect(screen.getByTestId('header-user')).toHaveTextContent('CBCamille Bernard');
    expect(within(header).queryByRole('button', { name: /pour moi|for me/i })).not.toBeInTheDocument();
    // Without a project open, no language of the process: the account alone, its menu holding the language of the interface (ADR 083).
    expect(within(header).getAllByRole('button').map((button) => button.getAttribute('data-testid'))).toEqual(['header-user']);
  });

});

describe('the languages in the header (ADR 083)', () => {
  it('the language of the interface is chosen in the menu of the user, among the languages of the configuration, each in itself (ADR 083)', async () => {
    start(fakeSource([profileOf({ languages: ['fr', 'en', 'es'], effective_language: 'en' })]).source);
    await userEvent.click(await screen.findByTestId('header-user'));
    await userEvent.click(screen.getByRole('menuitem', { name: 'Interface language' }));
    const dialog = screen.getByRole('dialog', { name: 'Interface language' });
    const cards = within(dialog).getAllByRole('button').filter((button) => button.classList.contains('language-card'));
    expect(cards.map((card) => [card.getAttribute('data-testid'), card.getAttribute('aria-pressed')]))
      .toEqual([['interface-language-fr', 'false'], ['interface-language-en', 'true'], ['interface-language-es', 'false']]);
    expect(cards.map((card) => card.querySelector('.language-card-own')?.textContent)).toEqual(['Français', 'English', 'Español']);
    expect(document.documentElement.lang).toBe('en');
  });

  it('a language chosen applies at once and is sent to the profile (B-1.2-05)', async () => {
    const { source, sent } = fakeSource([profileOf()], [profileOf({ language: 'en', effective_language: 'en', version: 4 })]);
    start(source);
    await userEvent.click(await screen.findByTestId('header-user'));
    await userEvent.click(screen.getByRole('menuitem', { name: 'Langue de l’interface' }));
    await userEvent.click(screen.getByTestId('interface-language-en'));
    expect(await screen.findByText('Boost your processes with AI')).toBeInTheDocument();
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect([document.documentElement.lang, document.title]).toEqual(['en', i18next.t('app.name')]);
    await waitFor(() => {
      expect(sent).toEqual([[{ language: 'en' }, 3]]);
    });
  });

  it('a language refused by the profile stays applied for the session (B-1.2-06)', async () => {
    const { source } = fakeSource([profileOf()], [new ApiError(0, 'RESEAU', '')]);
    start(source);
    await userEvent.click(await screen.findByTestId('header-user'));
    await userEvent.click(screen.getByRole('menuitem', { name: 'Langue de l’interface' }));
    await userEvent.click(screen.getByTestId('interface-language-en'));
    await waitFor(() => {
      expect(source.change).toHaveBeenCalled();
    });
    expect(await screen.findByText('Boost your processes with AI')).toBeInTheDocument();
  });

  it('the language of the process: the one of origin, those translated with how far, the others; one chosen is kept for the project (ADR 083)', async () => {
    start(fakeSource([profileOf({ languages: ['fr', 'en', 'es', 'pt'] })]).source);
    await screen.findByTestId('app-header');
    const project = { id: GUID_A, code: 'VTE', breadcrumb: [], openFileMenu: vi.fn(), canEdit: true, languages: { origin: 'fr', total: 4, written: { fr: 4, pt: 3 } } };
    act(() => {
      openProject.setState({ project });
    });
    const button = screen.getByTestId('process-language');
    expect(button).toHaveTextContent('Français');
    await userEvent.click(button);
    const list = screen.getByRole('listbox', { name: 'Langue du processus' });
    expect(within(list).getAllByRole('presentation').map((group) => group.textContent)).toEqual(['Langue d’origine', 'Traduit', 'Pas encore traduit']);
    expect(within(list).getAllByRole('option').map((option) => option.getAttribute('data-testid')))
      .toEqual(['process-language-fr', 'process-language-pt', 'process-language-en', 'process-language-es']);
    expect(screen.getByTestId('process-language-pt')).toHaveTextContent(/Português.*portugais.*75\s?%/u);
    expect(screen.getByTestId('process-language-fr')).toHaveAttribute('aria-selected', 'true');
    await userEvent.click(screen.getByTestId('process-language-pt'));
    expect(screen.queryByRole('listbox')).not.toBeInTheDocument();
    expect(screen.getByTestId('process-language')).toHaveTextContent('Português');
    expect(processLanguage.getState().chosen[GUID_A]).toBe('pt');
    act(() => {
      openProject.setState({ project: null });
    });
  });

});

describe('the translations of an open project in the header (B-1.2-12)', () => {
  it('the Translations button shows only for an open project the user may translate, once its screen is declared (B-1.2-12, B-7-13)', async () => {
    start(fakeSource([profileOf()]).source);
    await screen.findByTestId('app-header');
    expect(screen.queryByTestId('header-translations')).not.toBeInTheDocument();
    const openTranslations = vi.fn();
    const project = { id: GUID_A, code: 'VTE', breadcrumb: [], openFileMenu: vi.fn(), languages: { origin: 'fr', total: 0, written: {} } };
    act(() => {
      openProject.setState({ project: { ...project, canEdit: false, openTranslations } });
    });
    expect(screen.queryByTestId('header-translations')).not.toBeInTheDocument();
    act(() => {
      openProject.setState({ project: { ...project, canEdit: true } });
    });
    expect(screen.queryByTestId('header-translations')).not.toBeInTheDocument();
    act(() => {
      openProject.setState({ project: { ...project, canEdit: true, openTranslations } });
    });
    await userEvent.click(screen.getByRole('button', { name: 'Traductions' }));
    expect(openTranslations).toHaveBeenCalledTimes(1);
  });
});

describe('the screens of the routes (B-1.1-09, B-1.1-10)', () => {
  it('an unknown address says « Impossible de charger la page » with a button to the home page', async () => {
    window.history.replaceState(null, '', '/cartes/x');
    start(fakeSource([profileOf()]).source);
    const page = await screen.findByTestId('page-not-found');
    expect(page).toHaveTextContent('Impossible de charger la page.');
    await userEvent.click(within(page).getByRole('button', { name: 'Retour à l’accueil' }));
    expect(window.location.pathname).toBe('/');
    expect(screen.getByTestId('screen-home')).toBeInTheDocument();
  });

  it('each screen gets what its route names, and the map screen is mounted again for each map', async () => {
    /** An editor that keeps the map it was mounted for, as the state of a real editor would. */
    function Editor({ mapId }: { mapId: string }) {
      const [mounted] = useState(mapId);
      return <p>{`map ${mounted}`}</p>;
    }
    const screens: Screens = { ...SCREENS, members: (projectId) => <p>{`members ${projectId}`}</p>, map: (mapId) => <Editor mapId={mapId} /> };
    window.history.replaceState(null, '', `/maps/${GUID_A.toUpperCase()}`);
    start(fakeSource([profileOf()]).source, screens);
    expect(await screen.findByText(`map ${GUID_A}`)).toBeInTheDocument();
    act(() => {
      navigate(`/maps/${GUID_B}`);
    });
    expect(screen.getByTestId('screen-map')).toHaveTextContent(`map ${GUID_B}`);
    act(() => {
      navigate(`/projects/${GUID_A}/members`);
    });
    expect(screen.getByTestId('screen-members')).toHaveTextContent(`members ${GUID_A}`);
  });
});

describe('Ctrl+S (B-2.5-05, B-1.2-20)', () => {
  it('says that everything is saved, on the page and in a field, without the window of the browser', async () => {
    start(fakeSource([profileOf()]).source, { ...SCREENS, home: () => <input aria-label="Nom" /> });
    const field = await screen.findByRole('textbox', { name: 'Nom' });
    const onPage = new KeyboardEvent('keydown', { key: 's', ctrlKey: true, bubbles: true, cancelable: true });
    act(() => {
      document.body.dispatchEvent(onPage);
    });
    expect(onPage.defaultPrevented).toBe(true);
    await userEvent.type(field, '{Control>}s{/Control}');
    const shown = within(screen.getByRole('region', { name: 'Notifications' })).getAllByRole('status');
    expect(shown.map((toast) => toast.textContent)).toEqual(Array(2).fill(i18next.t('app.autosaved')));
    expect(field).toHaveValue('');
  });
});
