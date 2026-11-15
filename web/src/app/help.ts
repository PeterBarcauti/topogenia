// help.ts
// The help of the application: the user manual written for the screen, pages of their own served beside the interface (public/aide, made from the manual), in the default language of the interface (French) for an interface in it and in English for any other language, opened in a new tab by the button of the header or F1.

import { DEFAULT_LANGUAGE } from '../shared/i18n/i18n.ts';

/** The address of the help in the default language, French, the one the manual is written in, under the base of the interface. */
export const HELP_URL_FR = `${import.meta.env.BASE_URL}aide/index.html`;

/** The address of the help in English, the one of every language of the interface but French. */
export const HELP_URL_EN = `${import.meta.env.BASE_URL}aide/en/index.html`;

/** The help to open for a language of the interface: French for French (« fr », « fr-CA »), English for any other. */
export function helpUrl(language: string): string {
  return language === DEFAULT_LANGUAGE || language.startsWith(`${DEFAULT_LANGUAGE}-`) ? HELP_URL_FR : HELP_URL_EN;
}

/** Opens the help of a language of the interface in a new tab, without giving it the window of the application. */
export function openHelp(language: string): void {
  window.open(helpUrl(language), '_blank', 'noopener');
}
