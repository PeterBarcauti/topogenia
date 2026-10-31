// i18n.ts
// The languages of the interface: one JSON file per language in locales/, i18next, and the language applied to the page and the API (NF12, B-0-03); a language is loaded when it is first shown, the default one with the page (ADR 083).

import i18next from 'i18next';
import { initReactI18next } from 'react-i18next';
import { setRequestLanguage } from '../../api/request.ts';
import defaults from './locales/fr.json';

type Messages = Record<string, unknown>;

const files = import.meta.glob<Messages>('./locales/*.json', { import: 'default' });

/**
 * The loader of each language whose file is in locales/ (NF12: adding a language is adding its file and its line of the
 * configuration). Eighteen languages are 1 MB of texts: each is its own file of the build, read when it is first shown.
 */
const loaders = Object.fromEntries(Object.entries(files).map(([path, load]) => [path.replace(/^.*\/(.+)\.json$/, '$1'), load]));

/** The languages written in locales/. */
export const BUNDLED_LANGUAGES: readonly string[] = Object.keys(loaders);

/** The language of the interface by default, and of every text missing in another language (D12, AR-51): its file comes with the page. */
export const DEFAULT_LANGUAGE = 'fr';

/** The first language of the browser that the interface knows, else the default one (B-1.2-04, before the profile is read). */
export function browserLanguage(preferred: readonly string[], known: readonly string[] = BUNDLED_LANGUAGES): string {
  for (const code of preferred) {
    const short = code.toLowerCase().split('-')[0] ?? '';
    if (known.includes(short)) return short;
  }
  return DEFAULT_LANGUAGE;
}

const navigatorLanguages = (): readonly string[] => (navigator.languages.length > 0 ? navigator.languages : [navigator.language]);

const productName = (defaults.app as { name?: string }).name ?? '';

/** The product name, read from the file of the default language: the same in every language (D13). */
export const PRODUCT_NAME = productName;

/** The extension of the package of exchange, the product name in lower case (Q2, D13: the extension follows the name). */
export const PACKAGE_EXTENSION = `.${productName.toLowerCase()}`;

/** A text of the interface by its key (i18next `t`), for the modules that build files: defined once for all of them (B-8.1-26). */
export type Translate = (key: string, options?: Record<string, unknown>) => string;

void i18next.use(initReactI18next).init({
  resources: { [DEFAULT_LANGUAGE]: { translation: defaults } },
  lng: DEFAULT_LANGUAGE,
  fallbackLng: DEFAULT_LANGUAGE,
  interpolation: { escapeValue: false, defaultVariables: { extension: PACKAGE_EXTENSION } },
  returnNull: false,
  initAsync: false,
});

/** Reads the texts of a language once; a language without its file keeps the texts of the default one. */
export async function loadLanguage(language: string): Promise<void> {
  const load = loaders[language];
  if (load === undefined || i18next.hasResourceBundle(language, 'translation')) return;
  i18next.addResourceBundle(language, 'translation', await load(), true, true);
}

/** Shows the page in a language, once its texts are read: texts, `lang` of the document (B-0-12) and its title (B-0-13). */
export async function showLanguage(language: string): Promise<void> {
  await loadLanguage(language);
  await i18next.changeLanguage(language);
  document.documentElement.lang = language;
  document.title = i18next.t('app.name');
}

/** Applies the language of the user: the page, and the language of the messages of the API (B-0-17, B-7-15). */
export function applyLanguage(language: string): Promise<void> {
  setRequestLanguage(language);
  return showLanguage(language);
}

/** The first language shown, the browser's: `main.tsx` waits for it before the first paint, so that no text changes under the eyes. */
export const ready: Promise<void> = showLanguage(browserLanguage(navigatorLanguages()));

export { i18next };
