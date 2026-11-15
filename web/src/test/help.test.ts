// help.test.ts
// The help served beside the interface (public/aide in French, public/aide/en in English, made from the user manual by Manuel/outils/aide.py) holds together: each page is in its language, both are made from one manual (same chapters, same sections, same names of anchors) and lead to each other, every link inside them leads somewhere, every file they name is there, nothing comes from elsewhere, no script nor style is written inline (the policy of the host refuses them), their charter is the one of the application, word for word.

import { existsSync, readFileSync, readdirSync } from 'node:fs';
import { join } from 'node:path';

const web = join(import.meta.dirname, '..', '..');
const help = join(web, 'public', 'aide');
const script = readFileSync(join(help, 'aide.js'), 'utf8');
const sheet = readFileSync(join(help, 'aide.css'), 'utf8');

/** The help of a language: its folder and its page. */
const PAGES = {
  fr: { folder: help, page: readFileSync(join(help, 'index.html'), 'utf8') },
  en: { folder: join(help, 'en'), page: readFileSync(join(help, 'en', 'index.html'), 'utf8') },
};
const LANGUAGES = ['fr', 'en'] as const;

/** The values of an attribute in a page. */
const values = (page: string, name: string): string[] => [...page.matchAll(new RegExp(`\\s${name}="([^"]*)"`, 'g'))].map((match) => match[1] ?? '');

describe('the help beside the interface (aide en ligne)', () => {
  it.each(LANGUAGES)('is in its language (%s), with its summary of every chapter and section, each link inside it leading to its heading', (language) => {
    const { page } = PAGES[language];
    expect(page).toMatch(new RegExp(`^<!DOCTYPE html>\\n<html lang="${language}">`));
    const ids = new Set(values(page, 'id'));
    const inside = values(page, 'href').filter((href) => href.startsWith('#')).map((href) => href.slice(1));
    expect(inside.filter((id) => !ids.has(id))).toEqual([]);
    expect([...page.matchAll(/<li class="toc-chapter">/g)]).toHaveLength(16);
    expect(inside.filter((id) => /^s\d+-\d+$/.test(id)).length).toBeGreaterThanOrEqual(60);
  });

  it.each(LANGUAGES)('leads each chapter or section it cites to its heading (%s), a line ending between the word and the name too (audit 3.1 V4, A4)', (language) => {
    const outside = PAGES[language].page.replace(/<a\b[^>]*>[\s\S]*?<\/a>/g, '');
    expect(outside.match(language === 'fr' ? /(?:chapitre|voir)\s+«/g : /(?:chapter|see)\s+“/g)).toBeNull();
  });

  it('is made from one manual in both languages: the same anchors in the same order, the same captures, each page leading to the other', () => {
    const anchors = (page: string) => values(page, 'id').filter((id) => /^s\d+(-\d+)?$/.test(id));
    expect(anchors(PAGES.en.page)).toEqual(anchors(PAGES.fr.page));
    expect(readdirSync(join(PAGES.en.folder, 'images'))).toEqual(readdirSync(join(PAGES.fr.folder, 'images')));
    const other = (page: string) => /<a class="lang-switch" href="([^"]+)" hreflang="([a-z]+)" lang="\2"/.exec(page)?.slice(1);
    expect([other(PAGES.fr.page), other(PAGES.en.page)]).toEqual([['en/index.html', 'en'], ['../index.html', 'fr']]);
    // The sentence of a search that finds nothing is the one of the page, given to the script in its markup.
    expect(LANGUAGES.map((language) => values(PAGES[language].page, 'data-empty')))
      .toEqual([['Aucune section ne contient « {q} ». Essayez un autre mot, ou parcourez le sommaire.'], ['No section contains “{q}”. Try another word, or browse the contents.']]);
    expect([script.includes('list.dataset.empty'), /Aucune section|No section/.test(script)]).toEqual([true, false]);
  });

  it.each(LANGUAGES)('names only files that are there (%s), beside it, nothing taken from another address', (language) => {
    const { folder, page } = PAGES[language];
    const files = [...values(page, 'src'), ...values(page, 'href').filter((href) => !href.startsWith('#'))];
    expect(files.filter((file) => /^(https?:)?\/\//.test(file) || file.startsWith('/'))).toEqual([]);
    expect(files.filter((file) => !existsSync(join(folder, file)))).toEqual([]);
    expect(readdirSync(join(folder, 'images')).length).toBe(files.filter((file) => file.startsWith('images/')).length - 2);
    const sheets = [...sheet.matchAll(/url\('([^']+)'\)/g)].map((match) => match[1] ?? '');
    expect(sheets.filter((file) => /^(https?:)?\/\//.test(file) || file.startsWith('/') || !existsSync(join(help, file)))).toEqual([]);
  });

  it.each(LANGUAGES)('writes no script nor style inline and no handler in its markup (%s): the policy of the host would refuse them', (language) => {
    const { page } = PAGES[language];
    expect(page.match(/<script(?![^>]*\ssrc=)[^>]*>/g)).toBeNull();
    expect(page.match(/<style\b|\sstyle="|\son[a-z]+="/g)).toBeNull();
    // Its script builds the search with nodes: it never writes HTML nor evaluates a text.
    expect(script.match(/innerHTML|outerHTML|insertAdjacentHTML|document\.write|\beval\(|new Function/g)).toBeNull();
  });

  it('takes the charter of the application as it is: the tokens of the interface, word for word', () => {
    expect(readFileSync(join(help, 'tokens.css'), 'utf8')).toBe(readFileSync(join(web, 'src', 'styles', 'tokens.css'), 'utf8'));
    expect(sheet.startsWith('/* aide.css */')).toBe(true);
    expect(sheet).toContain("@import url('tokens.css');");
    // Every colour of the help comes from a token or is mixed from them.
    expect(sheet.match(/#[0-9a-fA-F]{3,8}\b|rgba?\(|hsla?\(/g)).toBeNull();
  });
});
