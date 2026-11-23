// updateCommand.test.tsx
// « Check for updates » in the header (ADR 111): the last line of the Help menu and the line before « Quit » in the menu of the account, for the administrator alone (the person of the computer in desktop mode), never without the links the host gives; its tooltip says the version installed and its day; it opens the page of the versions, or the page of the Store for the Store version, in a new tab.

import { act, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { Schema } from '../api/client.ts';
import { PRODUCT_NAME, i18next } from '../shared/i18n/i18n.ts';
import { formatDay } from '../shared/i18n/formatDate.ts';
import { shortcutsPanel } from '../shared/state/shortcutsPanel.ts';
import { fakeApi } from '../test/fakeApi.ts';
import { TEST_LINKS, fakeSource, profileOf } from '../test/profiles.ts';
import { App } from './App.tsx';

beforeEach(async () => {
  await i18next.changeLanguage('fr');
  fakeApi({ 'GET /api/projects': { json: [] }, 'GET /api/trash': { json: { retention_days: 30, projects: [] } } });
});

afterEach(() => {
  act(() => {
    shortcutsPanel.setState({ open: false });
  });
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
});

/** The application on the home page for a profile; gives the lines of the Help menu and of the menu of the account, each closed after it was read. */
async function menus(profile: Partial<Schema<'ProfileView'>>, names = { help: 'Menu Aide', account: 'Menu du compte' }) {
  render(<App source={fakeSource([profileOf(profile)]).source} />);
  const lines = async (button: HTMLElement, name: string) => {
    await userEvent.click(button);
    const found = within(screen.getByRole('menu', { name })).getAllByRole('menuitem').map((item) => [item.querySelector('.menu-label')?.textContent, item.getAttribute('title')]);
    await userEvent.keyboard('{Escape}');
    return found;
  };
  const help = await lines(await screen.findByTestId('header-help'), names.help);
  const account = await lines(screen.getByTestId('header-user'), names.account);
  return { help, account };
}

/** The line, with the day of the version as the page sees it at the time of the test. */
const check = () => ['Rechercher une mise à jour', `Ouvre la page des versions du site. Installée : ${__APP_VERSION__} du ${formatDay(__APP_RELEASE_DATE__, 'fr')}.`];
const ONLINE = ['Aide en ligne', 'Ouvrir l’aide dans un nouvel onglet (F1)'];
const SHORTCUTS = ['Raccourcis clavier', null];
const LANGUAGE = ['Langue de l’interface', null];
const QUIT = [`Quitter ${PRODUCT_NAME}`, null];

describe('« Check for updates » (ADR 111)', () => {
  it('the Help menu and the account menu end with Check for updates, for the administrator in server mode only, never without links', async () => {
    expect(await menus({ desktop: true, is_admin: true })).toEqual({ help: [ONLINE, SHORTCUTS, check()], account: [LANGUAGE, check(), QUIT] });
  });

  it('in server mode, for the administrator alone: the last line of both menus', async () => {
    expect(await menus({ desktop: false, is_admin: true })).toEqual({ help: [ONLINE, SHORTCUTS, check()], account: [LANGUAGE, check()] });
  });

  it('never for a user who is not administrator, who can install nothing', async () => {
    expect(await menus({ desktop: false, is_admin: false })).toEqual({ help: [ONLINE, SHORTCUTS], account: [LANGUAGE] });
  });

  it('never without the links given by the host (SiteLinks false), in either mode', async () => {
    expect(await menus({ desktop: true, is_admin: true, links: null })).toEqual({ help: [ONLINE, SHORTCUTS], account: [LANGUAGE, QUIT] });
  });

  it('opens the page of the versions in a new tab, from either menu, the version and the kind in the fragment', async () => {
    const open = vi.spyOn(window, 'open').mockReturnValue(null);
    render(<App source={fakeSource([profileOf({ desktop: false, is_admin: true })]).source} />);
    await userEvent.click(await screen.findByTestId('header-help'));
    await userEvent.click(within(screen.getByRole('menu', { name: 'Menu Aide' })).getByRole('menuitem', { name: 'Rechercher une mise à jour' }));
    expect(screen.queryByRole('menu')).toBeNull();
    await userEvent.click(screen.getByTestId('header-user'));
    await userEvent.click(within(screen.getByRole('menu', { name: 'Menu du compte' })).getByRole('menuitem', { name: 'Rechercher une mise à jour' }));
    const versions = `${TEST_LINKS.site}/versions/#v=${__APP_VERSION__}&d=server`;
    expect(open.mock.calls).toEqual([[versions, '_blank', 'noopener,noreferrer'], [versions, '_blank', 'noopener,noreferrer']]);
  });

  it('the Store version opens the page of the Store, and its tooltip says the Store installs the updates', async () => {
    const open = vi.spyOn(window, 'open').mockReturnValue(null);
    const { help } = await menus({ desktop: true, is_admin: true, store: true });
    expect(help[2]).toEqual(['Rechercher une mise à jour', 'Ouvre la fiche du Microsoft Store, qui fait les mises à jour.']);
    await userEvent.click(screen.getByTestId('header-help'));
    await userEvent.click(within(screen.getByRole('menu', { name: 'Menu Aide' })).getByRole('menuitem', { name: 'Rechercher une mise à jour' }));
    expect(open.mock.calls).toEqual([[TEST_LINKS.store_page, '_blank', 'noopener,noreferrer']]);
  });

  it('speaks the language of the interface: the label, the version and its day written as that language writes a day', async () => {
    await i18next.changeLanguage('en');
    const { help } = await menus({ desktop: true, is_admin: true, language: 'en', effective_language: 'en' }, { help: 'Help menu', account: 'Account menu' });
    expect(help[2]).toEqual(['Check for updates', `Opens the versions page of the website. Installed: ${__APP_VERSION__} of ${formatDay(__APP_RELEASE_DATE__, 'en')}.`]);
  });
});
