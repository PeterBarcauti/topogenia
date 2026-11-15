// report.test.ts
// The report « l'histoire du processus » assembled (plan 3.1, L4): its header says it was written with the AI and by which model, its eleven sections in their order, what the team noted apart from what the AI spotted, the actions named A1, A2... on their matrix and their plan; every diagram read by Mermaid.

import mermaid from 'mermaid';
import { i18next, PRODUCT_NAME, type Translate } from '../../../shared/i18n/i18n.ts';
import { sampleMap } from '../../../test/maps.ts';
import { hardReport, sampleReport } from '../../../test/reports.ts';
import { reportMarkdown } from './report.ts';

const t: Translate = (key, options) => i18next.t(key, options);
const facts = { title: 'VIS - Visites', breadcrumb: 'Visites', classification: 'Classification : Interne', date: '6 novembre 2026', model: 'claude-opus-5-5', footer: `Généré par ${PRODUCT_NAME}` };
const look = { problem: { fill: '#FBE9E7', stroke: '#C0362C' } };
const blocks = (text: string) => [...text.matchAll(/```mermaid\n([\s\S]*?)\n```/g)].map((match) => match[1] ?? '');

beforeEach(async () => {
  await i18next.changeLanguage('fr');
});

describe('the report of a map (plan 3.1, L4)', () => {
  it('says it was written with the AI and by which model, then gives its sections in the order of the model', () => {
    const text = reportMarkdown(sampleMap(), 'fr', t, facts, sampleReport(), look);
    expect(text.startsWith('# Rapport : VIS - Visites\n\n> Visites\n>\n> Classification : Interne · 6 novembre 2026\n>\n> Rédigé avec l’IA (claude-opus-5-5) : à relire avant de le diffuser.')).toBe(true);
    expect([...text.matchAll(/^## (.+)$/gm)].map((match) => match[1])).toEqual([
      'En une minute', 'Vue d’ensemble', 'Les acteurs', 'L’histoire, chapitre par chapitre', 'Le parcours vécu', 'Ce qui coince', 'Les actions', 'Plan d’actions',
      'Pour conclure', 'Annexes',
    ]);
    expect(text.trimEnd().endsWith(`_${facts.footer}_`)).toBe(true);
  });

  it('keeps what the team noted, from the sheets, apart from what the AI spotted', () => {
    const text = reportMarkdown(sampleMap(), 'fr', t, facts, sampleReport(), look);
    expect(text).toContain('### Irritants relevés par l’équipe');
    expect(text).toContain('| VIS-I001 | VIS-E002 Vérifier la demande | Pièces ressaisies à la main | 6 | signalé |');
    expect(text).toContain('| #1 | Relevé par l’équipe (VIS-I001) | VIS-E002 | Ressaisie : Ressaisie des pièces | 6 h perdues par mois. |');
    expect(text).toContain('| #2 | Repéré par l’IA | VIS-E003, VIS-E002 | Boucle : Boucle de reprise |');
  });

  it('names the actions A1, A2... on the matrix by effort and impact, in the table and on the plan of their waves', () => {
    const text = reportMarkdown(sampleMap(), 'fr', t, facts, sampleReport(), look);
    expect(text).toContain('  "A1": [0.30, 0.84]');
    expect(text).toContain('  "A2": [0.10, 0.60]');
    expect(text).toContain('| A1 | Importer les pièces | Un import depuis le courriel. | #1 | Commercial | 2 | 5 | Tout de suite |');
    expect(text).toContain('  Tout de suite : A1 Importer\n  Dans le mois : A2 Compléter');
  });

  it('draws the journey without actors, the lane of each step in its table; no diagram repeats the heading above it', () => {
    const text = reportMarkdown(sampleMap(), 'fr', t, facts, sampleReport(), look);
    expect(text).toContain('| Code | Étape | Couloir | Note | Pourquoi |');
    expect(text).toContain('| VIS-E002 | Vérifier la demande | Commercial | 2 | Ressaisie des pièces. |');
    const journey = blocks(text).find((diagram) => diagram.includes('\njourney\n')) ?? '';
    expect(journey.split('\n').filter((line) => line.startsWith('    '))).toEqual(['    Recevoir: 4', '    Vérifier: 2', '    Décider: 3', '    Facturer: 5']);
    expect(blocks(text).flatMap((diagram) => diagram.split('\n').filter((line) => line.startsWith('  title ')))).toEqual(['  title Étapes par couloir']);
  });

  it('tells each chapter with the part of the map it covers, the steps of the problems coloured', () => {
    const text = reportMarkdown(sampleMap(), 'fr', t, facts, sampleReport(), look);
    expect(text).toContain('### 1. Recevoir et vérifier\n\nLa demande arrive, le commercial la vérifie.\n\n```mermaid');
    const [overview] = blocks(text);
    expect(overview).toContain('class S2,S3 issue');
  });

  it('gives the tables of the value stream with its layer on, and every diagram reads in Mermaid, the hard report too', async () => {
    expect(reportMarkdown(sampleMap(), 'fr', t, facts, sampleReport(), look, true)).toContain('## Le temps');
    for (const report of [sampleReport(), hardReport(), sampleReport({ actions: [], problems: [], chapters: [] })]) {
      const diagrams = blocks(reportMarkdown(sampleMap(), 'fr', t, facts, report, look));
      for (const diagram of diagrams) await expect(mermaid.parse(diagram)).resolves.toBeTruthy();
    }
  });
});

describe('the texts of the model stay texts (plan 3.1, E11)', () => {
  it('a text of the model writes no heading, no image, no link, no HTML and no block of code into the report', async () => {
    const report = sampleReport({
      summary: 'Résumé.\n\n# Titre injecté\n\n![p](exfil.png)\n\n<!--',
      chapters: [{ title: 'Recevoir\n\n## Faux chapitre', short: 'Recevoir', steps: ['VIS-E001'], story: 'Texte\n```\nreste\n    code\n1. liste\n---' }],
    });
    const text = reportMarkdown(sampleMap(), 'fr', t, facts, report, { problem: null });
    const lines = text.split('\n');
    expect(lines.filter((line) => /^#{1,6} /.test(line)).some((line) => /injecté|Faux chapitre/.test(line) && !line.startsWith('### 1. '))).toBe(false);
    expect([/(?<!\\)!\[p\]/.test(text), /(?<!\\)<!--/.test(text), lines.filter((line) => line.startsWith('```') && line !== '```' && line !== '```mermaid')]).toEqual([false, false, []]);
    expect(lines.filter((line) => line.startsWith('```')).length % 2).toBe(0);
    for (const diagram of [...text.matchAll(/```mermaid\n([\s\S]*?)```/g)].map((block) => block[1] ?? '')) await expect(mermaid.parse(diagram)).resolves.toBeTruthy();
  });

  it('names no model when the reply was pasted from the AI of the person', () => {
    const text = reportMarkdown(sampleMap(), 'fr', t, { ...facts, model: '' }, sampleReport(), { problem: null });
    expect(text).toContain('> Rédigé avec l’IA : à relire avant de le diffuser.');
    expect(text).not.toContain('()');
  });
});