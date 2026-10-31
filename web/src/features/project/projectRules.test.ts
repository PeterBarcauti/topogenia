// projectRules.test.ts
// The rules of the project screens without React: the text of a .pmap, the code typed, the members sent, the language of a new project, the project put forward, a translated name.

import { localizedName } from '../../shared/i18n/localizedName.ts';
import { focusProject, type ProjectItem } from '../home/focus.ts';
import { PROJECT_CODE, cleanContent, headerOf, typedCode } from './files/pmapText.ts';
import { bodyOf, linesOf, newMember, projectNames } from './members.ts';

const BOM = String.fromCharCode(0xfeff);

const project = (code: string, id: string): ProjectItem => ({
  id, code, name: `Projet ${code}`, source_language: 'fr', name_translations: {}, description: null, classification: 'internal', root_map_id: `map-${code}`, role: 'reader',
  step_count: 0, map_count: 1,
});

describe('the text of a .pmap opened by hand (B-1.6-06 to B-1.6-08)', () => {
  it('drops a byte order mark and the blank around', () => {
    expect(cleanContent(`${BOM}  {"a":1}\n`)).toBe('{"a":1}');
  });

  it('drops a Markdown block of code around the content, with or without its language', () => {
    expect(cleanContent('```json\n{"a":1}\n```')).toBe('{"a":1}');
    expect(cleanContent('```\n{"a":1}\n```\nmerci')).toBe('{"a":1}');
    expect(cleanContent('```')).toBe('');
  });

  it('proposes the code and name of the project of the file, only a valid code', () => {
    expect(headerOf('```json\n{"project":{"code":"VTE","name":" Ventes "}}\n```')).toEqual({ code: 'VTE', name: 'Ventes' });
    expect(headerOf('{"project":{"code":"vte","name":3}}')).toEqual({ code: '', name: '' });
    expect(headerOf('pas du JSON')).toEqual({ code: '', name: '' });
    expect(headerOf('[1]')).toEqual({ code: '', name: '' });
  });

  it('an invalid code leaves a valid name proposed, and a missing name leaves a valid code', () => {
    expect(headerOf('{"project":{"code":"ach","name":"  X  "}}')).toEqual({ code: '', name: 'X' });
    expect(headerOf('{"project":{"code":"ACH"}}')).toEqual({ code: 'ACH', name: '' });
  });
});

describe('the code of a project (B-1.4-02, B-1.4-03)', () => {
  it('keeps the letters typed, in capitals, six at most', () => {
    expect(typedCode('vte-2 ach')).toBe('VTEACH');
    expect(typedCode('abcdefgh')).toBe('ABCDEF');
  });

  it('is valid from three to six capital letters', () => {
    expect(['VTE', 'ACHATS'].map((code) => PROJECT_CODE.test(code))).toEqual([true, true]);
    expect(['VT', 'ACHATSX', 'vte', 'VT1'].map((code) => PROJECT_CODE.test(code))).toEqual([false, false, false, false]);
  });
});

describe('the members (B-1.4-10 to B-1.4-14, B-1.5-01)', () => {
  it('a new line is a contributor', () => {
    expect(newMember()).toEqual({ login: '', role: 'contributor', isSponsor: false, isKeyContributor: false });
  });

  it('the lines read keep the key contribution, and what is sent leaves out the lines without login, trimmed', () => {
    const lines = linesOf([{ user_id: 'u', login: 'FICTIF\\lea', display_name: 'Léa', role: 'facilitator', is_sponsor: true, is_key_contributor: true, display_order: 0 }]);
    expect(bodyOf([...lines, newMember(), { ...newMember(), login: '  FICTIF\\paul ', role: 'reader' }])).toEqual([
      { login: 'FICTIF\\lea', role: 'facilitator', is_sponsor: true, is_key_contributor: true },
      { login: 'FICTIF\\paul', role: 'reader', is_sponsor: false, is_key_contributor: false },
    ]);
  });
});

describe('the language a new project is entered in (B-1.4-04 to B-1.4-06)', () => {
  it('is the language of the interface when its name is filled, the others its translations', () => {
    expect(projectNames({ fr: ' Ventes ', en: 'Sales', es: '' }, 'fr', ['fr', 'en', 'es'])).toEqual({ sourceLanguage: 'fr', name: 'Ventes', translations: { en: 'Sales' } });
    expect(projectNames({ fr: 'Ventes', en: 'Sales' }, 'en', ['fr', 'en'])).toEqual({ sourceLanguage: 'en', name: 'Sales', translations: { fr: 'Ventes' } });
  });

  it('else the first language of the configuration whose name is filled', () => {
    expect(projectNames({ es: 'Ventas', en: 'Sales' }, 'fr', ['fr', 'en', 'es'])).toEqual({ sourceLanguage: 'en', name: 'Sales', translations: { es: 'Ventas' } });
  });

  it('is none without any name, or with a name only in a language not configured', () => {
    expect(projectNames({ fr: '  ' }, 'fr', ['fr', 'en'])).toBeUndefined();
    expect(projectNames({ de: 'Verkauf' }, 'de', ['fr', 'en'])).toBeUndefined();
  });
});

describe('the project put forward (AR-49, B-1.3-02, B-1.3-24)', () => {
  const projects = [project('VTE', 'b'), project('ACH', 'c'), project('RH', 'a')];

  it('is the last project opened when the user still sees it, whatever the case of its identifier', () => {
    expect(focusProject(projects, 'B')?.code).toBe('VTE');
  });

  it('else the first by code; none without a project', () => {
    expect(focusProject(projects, null)?.code).toBe('ACH');
    expect(focusProject(projects, 'gone')?.code).toBe('ACH');
    expect(focusProject([], null)).toBeUndefined();
  });
});

describe('a translated name (B-1.3-09)', () => {
  const named = { name: 'Ventes', source_language: 'fr', name_translations: { en: 'Sales' } };

  it('is the original in its own language, else its translation, else the original', () => {
    expect([localizedName(named, 'fr'), localizedName(named, 'en'), localizedName(named, 'es')]).toEqual(['Ventes', 'Sales', 'Ventes']);
  });
});
