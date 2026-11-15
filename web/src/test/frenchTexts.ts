// frenchTexts.ts
// The texts of the interface in French read straight from their file, for the tests that run outside a page (the rendering of the diagrams in Chromium): a key by its path, its values put in place.

import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import type { Translate } from '../shared/i18n/i18n.ts';

type Texts = { readonly [key: string]: string | Texts };

const french = JSON.parse(readFileSync(join(import.meta.dirname, '..', 'shared', 'i18n', 'locales', 'fr.json'), 'utf8')) as Texts;

/** The text of a key in French, its `{{value}}` replaced; the key itself when it has none. */
export const frenchText: Translate = (key, options) => {
  let found: string | Texts | undefined = french;
  for (const part of key.split('.')) found = typeof found === 'object' ? found[part] : undefined;
  const text = typeof found === 'string' ? found : key;
  return text.replace(/\{\{(\w+)\}\}/g, (_, name: string) => {
    const value: unknown = options?.[name];
    return typeof value === 'string' || typeof value === 'number' ? String(value) : '';
  });
};
