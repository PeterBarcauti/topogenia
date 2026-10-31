// initials.test.ts
// The initials of a person in a round badge: first and last initial, the separators of a login, two letters for one word, « ? » without a name (annex B 1.3).

import { initialsOf } from './initials.ts';

describe('the initials of a person (B-1.3-25, B-1.3-26)', () => {
  it('the first letter of the first and of the last word, in capitals', () => {
    expect([initialsOf('Isabelle Martin'), initialsOf('anne marie de la tour')]).toEqual(['IM', 'AT']);
  });

  it('the dot, the hyphen and the underscore separate the words as a space does', () => {
    expect([initialsOf('jean-paul.dupont'), initialsOf('marie_claire'), initialsOf(' .louis- ')]).toEqual(['JD', 'MC', 'LO']);
  });

  it('two letters for a name of one word, « ? » for a name empty or blank', () => {
    expect([initialsOf('joao'), initialsOf('  '), initialsOf('')]).toEqual(['JO', '?', '?']);
  });
});
