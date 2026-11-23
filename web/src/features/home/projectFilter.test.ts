// projectFilter.test.ts
// The filter of the project list: a project is kept by its code, its name in the language shown or its name of origin, with or without accents, each word of several in one of them.

import type { ProjectItem } from './focus.ts';
import { filterProjects } from './projectFilter.ts';

const item = (code: string, name: string, translations: Record<string, string> = {}, language = 'fr'): ProjectItem => ({
  id: `p-${code}`, code, name, source_language: language, name_translations: translations, description: null, classification: 'internal',
  root_map_id: `m-${code}`, role: 'reader', step_count: 1, map_count: 1,
});

const PROJECTS = [
  item('ACH-01', 'Achats d’équipement', { en: 'Equipment purchasing' }),
  item('CONGES', 'Congés', { en: 'Leave' }),
  item('FAC', 'Facturation', {}),
];
const codes = (word: string, language = 'fr') => filterProjects(PROJECTS, word, language).map((project) => project.code);

describe('filterProjects', () => {
  it('keeps a project by its code, its name shown or its name of origin, with or without accents', () => {
    expect(codes('ach-0')).toEqual(['ACH-01']);
    expect(codes('CONGES')).toEqual(['CONGES']);
    expect(codes('conges')).toEqual(['CONGES']);
    expect(codes('EQUIPEMENT')).toEqual(['ACH-01']);
    expect(codes('facturation')).toEqual(['FAC']);
    // The name shown in English is searched, and so is the name of origin.
    expect(codes('purchasing', 'en')).toEqual(['ACH-01']);
    expect(codes('equipement', 'en')).toEqual(['ACH-01']);
    expect(codes('leave', 'en')).toEqual(['CONGES']);
    // A translation of a language not shown is not searched.
    expect(codes('purchasing', 'fr')).toEqual([]);
  });

  it('a word blank after the fold keeps the whole list, in its order', () => {
    expect(codes('')).toEqual(['ACH-01', 'CONGES', 'FAC']);
    expect(codes('   ')).toEqual(['ACH-01', 'CONGES', 'FAC']);
  });

  it('a word found nowhere keeps nothing', () => {
    expect(codes('zzz')).toEqual([]);
  });

  it('several words: each one, in any order, accents and capitals aside, all in one text, the code or a name (R9b)', () => {
    const list = [item('WEB-01', 'Site web Livres à jour'), item('VEN-02', 'Livres en magasin', { en: 'Web shop' }), item('MOB', 'Application mobile')];
    const kept = (word: string, language = 'fr') => filterProjects(list, word, language).map((project) => project.code);
    expect(kept('livre web')).toEqual(['WEB-01']);
    expect(kept('WEB  Lîvre')).toEqual(['WEB-01']);
    expect(kept('livre mobile')).toEqual([]);
    // In the code alone, or in the name shown alone.
    expect(kept('01 web')).toEqual(['WEB-01']);
    expect(kept('shop web', 'en')).toEqual(['VEN-02']);
    // Never spread over the code and the name, nor over the name shown and the name of origin.
    expect(kept('01 site')).toEqual([]);
    expect(kept('livre web', 'en')).toEqual(['WEB-01']);
    // A word of one character is dropped beside a longer one.
    expect(kept('z livre')).toEqual(['WEB-01', 'VEN-02']);
  });
});
