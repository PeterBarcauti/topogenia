// localizedName.test.ts
// The name shown in a language (1.x `traduire()`): the original in its language of origin, else the translation, else the original (B-5-25, B-5-26).

import { localizedName } from './localizedName.ts';

describe('the name shown in a language (B-5-25, B-5-26)', () => {
  const named = { name: 'Valider la commande', source_language: 'fr', name_translations: { fr: 'Commande validée', en: 'Approve the order' } };

  it('the language asked is the language of origin: the original, even beside a translation written in that language', () => {
    expect(localizedName(named, 'fr')).toBe('Valider la commande');
  });

  it('another language: its translation when there is one, else the original', () => {
    expect([localizedName(named, 'en'), localizedName(named, 'es')]).toEqual(['Approve the order', 'Valider la commande']);
    expect(localizedName({ ...named, source_language: 'en' }, 'fr')).toBe('Commande validée');
  });
});
