// help.test.tsx
// The help in the header: a button « Aide » on every screen opening the Help menu under it, with the online help served beside the interface in the language of the interface (French for French, English for any other), in a new tab that is not given the window of the application, and the keyboard shortcuts; F1 opens the same help, wherever the focus is.

import { act, fireEvent, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { i18next, loadLanguage } from '../shared/i18n/i18n.ts';
import { shortcutsPanel } from '../shared/state/shortcutsPanel.ts';
import { fakeApi } from '../test/fakeApi.ts';
import { fakeSource, profileOf } from '../test/profiles.ts';
import { App } from './App.tsx';
import { HELP_URL_EN, HELP_URL_FR, helpUrl } from './help.ts';

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

/** The application on the home page in a language of the interface; gives the Help button of the header. */
function start(language = 'fr') {
  render(<App source={fakeSource([profileOf(language === 'fr' ? {} : { language, effective_language: language })]).source} />);
  return screen.findByTestId('header-help');
}

/** Opens the Help menu by its button; gives the menu. */
async function openMenu(button: HTMLElement, name = 'Menu Aide') {
  await userEvent.click(button);
  return screen.getByRole('menu', { name });
}

describe('the help of the application (manuel, aide en ligne)', () => {
  it('is a button of the header that opens the Help menu under it: the online help (F1), the keyboard shortcuts (?)', async () => {
    const button = await start();
    expect([button.tagName, button.textContent, button.getAttribute('aria-haspopup'), button.getAttribute('aria-expanded')]).toEqual(['BUTTON', 'Aide', 'menu', 'false']);
    vi.spyOn(button, 'getBoundingClientRect').mockReturnValue(new DOMRect(700, 9, 70, 30));
    const menu = await openMenu(button);
    expect([menu.style.left, menu.style.top, button.getAttribute('aria-expanded')]).toEqual(['530px', '43px', 'true']);
    expect(within(menu).getAllByRole('menuitem').map((item) => [item.querySelector('.menu-label')?.textContent, item.querySelector('kbd')?.textContent, item.getAttribute('title')]))
      .toEqual([['Aide en ligne', 'F1', 'Ouvrir l’aide dans un nouvel onglet (F1)'], ['Raccourcis clavier', '?', null]]);
  });

  it('the Help menu of the header opens the online help in a new tab in its language, as F1', async () => {
    const open = vi.spyOn(window, 'open').mockReturnValue(null);
    const menu = await openMenu(await start());
    await userEvent.click(within(menu).getByRole('menuitem', { name: 'Aide en ligne' }));
    expect([open.mock.calls, screen.queryByRole('menu'), HELP_URL_FR]).toEqual([[['/aide/index.html', '_blank', 'noopener']], null, '/aide/index.html']);
  });

  it('opens with F1, the browser keeping its own help to itself', async () => {
    const open = vi.spyOn(window, 'open').mockReturnValue(null);
    await start();
    const pressed = fireEvent.keyDown(document.body, { key: 'F1' });
    expect([open.mock.calls, pressed]).toEqual([[['/aide/index.html', '_blank', 'noopener']], false]);
  });

  it('the Help menu opens the keyboard shortcuts, the same keys out of a map; closed, the focus goes back to the button', async () => {
    const button = await start();
    await userEvent.click(within(await openMenu(button)).getByRole('menuitem', { name: 'Raccourcis clavier' }));
    const dialog = screen.getByRole('dialog', { name: 'Raccourcis clavier' });
    expect([within(dialog).getByTestId('shortcut-fit'), within(dialog).getByTestId('shortcut-help.keys')].map((line) => line.querySelector('kbd')?.textContent)).toEqual(['Maj+1', '?']);
    await userEvent.keyboard('{Escape}');
    expect([screen.queryByRole('dialog'), document.activeElement]).toEqual([null, button]);
  });

  it('leads an interface in English to the help in English, by the menu and by F1', async () => {
    await i18next.changeLanguage('en');
    const open = vi.spyOn(window, 'open').mockReturnValue(null);
    const button = await start('en');
    expect(button.textContent).toBe('Help');
    const online = within(await openMenu(button, 'Help menu')).getByRole('menuitem', { name: 'Online help' });
    expect(online).toHaveAttribute('title', 'Open the help in a new tab (F1)');
    await userEvent.click(online);
    fireEvent.keyDown(document.body, { key: 'F1' });
    expect([open.mock.calls, HELP_URL_EN]).toEqual([[['/aide/en/index.html', '_blank', 'noopener'], ['/aide/en/index.html', '_blank', 'noopener']], '/aide/en/index.html']);
  });

  it('leads any other language of the interface to the help in English, and says so', async () => {
    await loadLanguage('de');
    await i18next.changeLanguage('de');
    const open = vi.spyOn(window, 'open').mockReturnValue(null);
    const menu = await openMenu(await start('de'), i18next.t('app.help_menu'));
    const online = within(menu).getAllByRole('menuitem')[0] as HTMLElement;
    expect(online).toHaveAttribute('title', 'Hilfe (auf Englisch) in einem neuen Tab öffnen (F1)');
    await userEvent.click(online);
    expect(open.mock.calls).toEqual([['/aide/en/index.html', '_blank', 'noopener']]);
  });

  it('chooses the French help for French alone, a regional French included', () => {
    expect(['fr', 'fr-CA', 'en', 'de', 'zh', 'fy', 'pt'].map(helpUrl))
      .toEqual([HELP_URL_FR, HELP_URL_FR, HELP_URL_EN, HELP_URL_EN, HELP_URL_EN, HELP_URL_EN, HELP_URL_EN]);
  });
});
