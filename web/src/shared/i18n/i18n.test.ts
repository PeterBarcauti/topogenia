// i18n.test.ts
// The languages of the interface: files of locales/ with the same keys, the browser language, the page and the requests in the language, the error texts (NF12, annex B 0, 1.2, 7).

import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { ApiError } from '../../api/errors.ts';
import { send } from '../../api/request.ts';
import { messageOf } from './errorMessage.ts';
import { BUNDLED_LANGUAGES, DEFAULT_LANGUAGE, applyLanguage, browserLanguage, i18next, showLanguage } from './i18n.ts';
import en from './locales/en.json';
import fr from './locales/fr.json';

const keysOf = (node: object, prefix = ''): string[] =>
  Object.entries(node).flatMap(([key, value]) => (typeof value === 'object' && value !== null ? keysOf(value as object, `${prefix}${key}.`) : [`${prefix}${key}`]));

const valuesOf = (node: unknown): string[] =>
  typeof node === 'string' ? [node] : typeof node === 'object' && node !== null ? Object.values(node).flatMap(valuesOf) : [];

afterEach(async () => {
  vi.unstubAllGlobals();
  await i18next.changeLanguage('fr');
});

describe('the language files (NF12, B-0-03)', () => {
  it('are each file of locales/, the eighteen languages of the configuration (ADR 083)', () => {
    expect([...BUNDLED_LANGUAGES].sort()).toEqual(['cs', 'de', 'el', 'en', 'es', 'fr', 'hu', 'it', 'ja', 'nl', 'pl', 'pt', 'ro', 'ru', 'sv', 'tr', 'uk', 'zh']);
    expect(DEFAULT_LANGUAGE).toBe('fr');
  });

  it('the default one comes with the page, another is read when it is first shown (ADR 083)', async () => {
    expect(i18next.hasResourceBundle('fr', 'translation')).toBe(true);
    expect(i18next.hasResourceBundle('pt', 'translation')).toBe(false);
    await showLanguage('pt');
    expect([i18next.hasResourceBundle('pt', 'translation'), document.documentElement.lang, i18next.t('app.retry')]).toEqual([true, 'pt', 'Tentar novamente']);
  });

  it('have the same keys, none empty', () => {
    expect(keysOf(en).sort()).toEqual(keysOf(fr).sort());
    for (const messages of [fr, en]) expect(valuesOf(messages).filter((text) => text.trim() === '')).toEqual([]);
  });

  it('speak French impersonally, never with the familiar form (annex C of the 1.x)', () => {
    expect(valuesOf(fr).filter((text) => /(^|[^\p{L}])((tu|toi|ton|ta|tes|te)([^\p{L}]|$)|t['’])/iu.test(text))).toEqual([]);
  });

  it('avoid the words of annex C.5', () => {
    const text = JSON.stringify(fr).toLowerCase();
    for (const word of ['sans valeur ajoutée', 'dysfonctionnement', 'productivité', 'supprimable', 'inutile', 'performance']) expect(text).not.toContain(word);
  });

  it('name the product in one key and nest it elsewhere (D13)', async () => {
    await i18next.changeLanguage('fr');
    expect(i18next.t('errors.RESEAU')).toBe(`${fr.app.name} ne répond pas. Vérifier que ${fr.app.name} est toujours lancé (ou la connexion au réseau s’il est sur un serveur).`);
  });

  it('give the signature of the header in each language (B-1.2-10)', () => {
    expect([fr.app.signature, en.app.signature]).toEqual(['Boostez vos processus avec l’IA', 'Boost your processes with AI']);
  });
});

describe('the language before the profile (B-1.2-04)', () => {
  it('is the first language of the browser that the interface knows', () => {
    expect(browserLanguage(['en-GB', 'fr'])).toBe('en');
    expect(browserLanguage(['eo', 'FR-ca'])).toBe('fr');
    expect(browserLanguage(['pt-PT', 'fr'])).toBe('pt');
  });

  it('else the default one, French (AR-51; the 1.x took English)', () => {
    expect(browserLanguage(['eo', 'ko'])).toBe('fr');
    expect(browserLanguage([])).toBe('fr');
  });

  it('follows the languages given, those of the configuration', () => {
    expect(browserLanguage(['es-ES', 'en'], ['fr', 'es'])).toBe('es');
  });
});

describe('a language shown (B-0-12, B-0-13, B-7-15)', () => {
  it('sets the texts, the lang of the document and its title from the key of the product name', async () => {
    await showLanguage('en');
    expect([i18next.language, document.documentElement.lang, document.title]).toEqual(['en', 'en', en.app.name]);
    await showLanguage('fr');
    expect([i18next.t('app.loading'), document.documentElement.lang]).toEqual(['Chargement…', 'fr']);
  });

  it('applied as the language of the user, is sent with every request (B-0-17)', async () => {
    const fetch = vi.fn(() => Promise.resolve(new Response('{}', { status: 200 })));
    vi.stubGlobal('fetch', fetch);
    await applyLanguage('en');
    await send('get', '/api/me');
    const init = fetch.mock.calls[0] as unknown as [string, RequestInit];
    expect(new Headers(init[1].headers).get('Accept-Language')).toBe('en');
  });

  it('a language of the configuration without its file shows the texts of the default language', async () => {
    await showLanguage('eo');
    expect(i18next.t('app.retry')).toBe(fr.app.retry);
  });
});

describe('the text of an error (annex B 0)', () => {
  it('is the message of the API, already in the language of the interface', () => {
    expect(messageOf(new ApiError(409, 'CONFLIT_VERSION', 'Modifiée par Léa Petit.'))).toBe('Modifiée par Léa Petit.');
  });

  it('else the text of its code: no answer, a reply that is not JSON, a refused sign-in', () => {
    expect(messageOf(new ApiError(0, 'RESEAU', ''))).toBe(i18next.t('errors.RESEAU'));
    expect(messageOf(new ApiError(200, 'REPONSE_INVALIDE', ''))).toBe(fr.errors.REPONSE_INVALIDE);
    expect(messageOf(new ApiError(401, 'HTTP_401', ''))).toBe(fr.errors.HTTP_401);
  });

  it('else the text of an unexpected error', () => {
    expect(messageOf(new ApiError(502, 'HTTP_502', ''))).toBe(fr.errors.UNKNOWN);
    expect(messageOf(new TypeError('x'))).toBe(fr.errors.UNKNOWN);
  });
});

describe('the page (B-0-12, B-0-13)', () => {
  const html = readFileSync(join(import.meta.dirname, '../../../index.html'), 'utf8');

  it('mounts the application in #root, with the icon of the product', () => {
    expect(html).toContain('<div id="root"></div>');
    expect(html).toContain('<link rel="icon" type="image/svg+xml" href="/favicon.svg" />');
  });

  it('writes no title: it comes from the key of the product name', () => {
    expect(html).toMatch(/<title><\/title>/);
  });
});
