// markdown.test.ts
// The export of a map in Markdown (plan 3.1, L3): title, breadcrumb, classification and date, the diagram in a Mermaid block, then the tables of the workbook in the language shown, their pipes escaped and their numbers written as the language writes them.

import mermaid from 'mermaid';
import { i18next, PRODUCT_NAME, type Translate } from '../../../shared/i18n/i18n.ts';
import { sampleMap } from '../../../test/maps.ts';
import { mapMarkdown, mermaidBlock, tableOf } from './markdown.ts';
import { workbookSheets } from './workbook.ts';

const t: Translate = (key, options) => i18next.t(key, options);
const facts = { title: 'VIS - Visites', breadcrumb: 'Visites > Facturer', classification: 'Classification : Interne', date: '5 novembre 2026 à 22:30', footer: `Généré par ${PRODUCT_NAME}` };
const look = { problem: null };

beforeEach(async () => {
  await i18next.changeLanguage('fr');
});

describe('the Markdown of a map (plan 3.1, L3)', () => {
  it('opens on its title, its breadcrumb, its classification and its date, then its diagram in a Mermaid block', async () => {
    const text = mapMarkdown(sampleMap(), 'fr', t, facts, look);
    const head = text.split('\n').slice(0, 8);
    expect(head).toEqual(['# VIS - Visites', '', '> Visites > Facturer', '>', '> Classification : Interne · 5 novembre 2026 à 22:30', '', '## Diagramme', '']);
    const block = /```mermaid\n([\s\S]*?)\n```/.exec(text)?.[1] ?? '';
    expect(block.startsWith('%%{init:')).toBe(true);
    await expect(mermaid.parse(block)).resolves.toBeTruthy();
    expect(text.trimEnd().endsWith(`_${facts.footer}_`)).toBe(true);
  });

  it('gives each table of the workbook under its name, in the same order, with the same rows', () => {
    const text = mapMarkdown(sampleMap(), 'fr', t, facts, look);
    const sheets = workbookSheets(sampleMap(), 'fr', t);
    expect([...text.matchAll(/^## (.+)$/gm)].map((match) => match[1])).toEqual(['Diagramme', ...sheets.map((sheet) => sheet.name)]);
    for (const sheet of sheets) expect(text).toContain(tableOf(sheet, 'fr'));
  });

  it('escapes the pipes of a cell, joins its lines, writes its numbers as the language does', () => {
    const table = tableOf({ headers: ['Étape', 'Délai'], rows: [['A | B\nsuite', 1234.567], ['C \\ D', 2]] }, 'fr');
    expect(table.split('\n')).toEqual(['| Étape | Délai |', '| --- | --- |', '| A \\| B suite | 1 234,57 |', '| C \\\\ D | 2 |']);
  });

  it('the value stream tables follow when its layer is on, as in the workbook', () => {
    const on = mapMarkdown(sampleMap(), 'fr', t, facts, look, true);
    for (const sheet of workbookSheets(sampleMap(), 'fr', t, true)) expect(on).toContain(`## ${sheet.name}`);
    expect(mermaidBlock('x')).toBe('```mermaid\nx\n```');
  });
});

describe('the summary of the map under the title (plan 3.1, lot R)', () => {
  const summary = { text: 'Le commercial vérifie la *demande* puis envoie le devis.', source_language: 'fr', translations: { en: 'The clerk checks the request.' },
    origin: 'ai', model: 'm', written_at: '2026-11-06T20:00:00Z', stale: false, version: 1 } as const;

  it('follows the title in italics, its signs of Markdown escaped, in the language of the export', () => {
    const lines = mapMarkdown(sampleMap({ summary }), 'fr', t, facts, look).split('\n');
    expect(lines.slice(0, 4)).toEqual(['# VIS - Visites', '', String.raw`_Le commercial vérifie la \*demande\* puis envoie le devis._`, '']);
    expect(mapMarkdown(sampleMap({ summary }), 'en', t, facts, look).split('\n')[2]).toBe('_The clerk checks the request._');
  });

  it('is left out when the map has none, or when the map changed since it was written', () => {
    for (const map of [sampleMap(), sampleMap({ summary: null }), sampleMap({ summary: { ...summary, stale: true } })])
      expect(mapMarkdown(map, 'fr', t, facts, look).split('\n').slice(0, 3)).toEqual(['# VIS - Visites', '', '> Visites > Facturer']);
  });
});
