// help.test.tsx
// The help in the header: a link « Aide » on every screen, to the help served beside the interface in the language of the interface (French for French, English for any other), in a new tab that is not given the window of the application; F1 opens the same help, wherever the focus is.

import { fireEvent, render, screen } from '@testing-library/react';
import { i18next, loadLanguage } from '../shared/i18n/i18n.ts';
import { fakeSource, profileOf } from '../test/profiles.ts';
import { App } from './App.tsx';
import { HELP_URL_EN, HELP_URL_FR, helpUrl } from './help.ts';

beforeEach(async () => {
  await i18next.changeLanguage('fr');
});

afterEach(() => {
  vi.restoreAllMocks();
});

describe('the help of the application (manuel, aide en ligne)', () => {
  it('is a link of the header to the help in French beside the interface, in a new tab without the window of the application', async () => {
    render(<App source={fakeSource([profileOf({})]).source} />);
    const link = await screen.findByTestId('header-help');
    expect([link.textContent, link.getAttribute('href'), link.getAttribute('target'), link.getAttribute('rel'), HELP_URL_FR])
      .toEqual(['Aide', '/aide/index.html', '_blank', 'noopener', '/aide/index.html']);
    expect(link).toHaveAttribute('title', 'Ouvrir l’aide dans un nouvel onglet (F1)');
  });

  it('opens with F1, the browser keeping its own help to itself', async () => {
    const open = vi.spyOn(window, 'open').mockReturnValue(null);
    render(<App source={fakeSource([profileOf({})]).source} />);
    await screen.findByTestId('header-help');
    const pressed = fireEvent.keyDown(document.body, { key: 'F1' });
    expect([open.mock.calls, pressed]).toEqual([[['/aide/index.html', '_blank', 'noopener']], false]);
  });

  it('leads an interface in English to the help in English, by the link and by F1', async () => {
    await i18next.changeLanguage('en');
    const open = vi.spyOn(window, 'open').mockReturnValue(null);
    render(<App source={fakeSource([profileOf({ language: 'en', effective_language: 'en' })]).source} />);
    const link = await screen.findByTestId('header-help');
    expect([link.textContent, link.getAttribute('href'), link.getAttribute('title'), HELP_URL_EN])
      .toEqual(['Help', '/aide/en/index.html', 'Open the help in a new tab (F1)', '/aide/en/index.html']);
    fireEvent.keyDown(document.body, { key: 'F1' });
    expect(open.mock.calls).toEqual([['/aide/en/index.html', '_blank', 'noopener']]);
  });

  it('leads any other language of the interface to the help in English, and says so', async () => {
    await loadLanguage('de');
    await i18next.changeLanguage('de');
    render(<App source={fakeSource([profileOf({ language: 'de', effective_language: 'de' })]).source} />);
    const link = await screen.findByTestId('header-help');
    expect([link.getAttribute('href'), link.getAttribute('title')]).toEqual(['/aide/en/index.html', 'Hilfe (auf Englisch) in einem neuen Tab öffnen (F1)']);
  });

  it('chooses the French help for French alone, a regional French included', () => {
    expect(['fr', 'fr-CA', 'en', 'de', 'zh', 'fy', 'pt'].map(helpUrl))
      .toEqual([HELP_URL_FR, HELP_URL_FR, HELP_URL_EN, HELP_URL_EN, HELP_URL_EN, HELP_URL_EN, HELP_URL_EN]);
  });
});
