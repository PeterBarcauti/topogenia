// mermaid.test.ts
// The diagrams written for Mermaid (plan 3.1, L3): texts cut into lines and never cut short, signs escaped, one subgraph per lane, the shapes of the types, the flows and their labels, the steps with a problem coloured; each diagram read by Mermaid itself.

import { readdirSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import mermaid from 'mermaid';
import { flowOf, laneOf, mapOf, sampleMap, stepOf } from '../../../test/maps.ts';
import { arialMeasure, WIDE_CHARACTER } from './arialWidth.ts';
import { boxText, escaped, flowchartOf, journeyOf, LINE_MAX, lines, mermaidInit, pieOf, quadrantOf, quoted } from './mermaid.ts';

/** Mermaid reads the text: it throws on a fault of syntax. */
export async function assertReadByMermaid(diagram: string): Promise<void> {
  await expect(mermaid.parse(diagram)).resolves.toBeTruthy();
}

describe('the texts of a diagram (rendu Mermaid : aucun titre coupé)', () => {
  it('are cut into lines of 24 characters at most, at the spaces, never cut short', () => {
    const label = 'Vérifier la complétude du dossier de demande de congés avant la validation par le responsable hiérarchique direct';
    const cut = lines(label);
    expect(cut.every((line) => line.length <= LINE_MAX)).toBe(true);
    expect(cut.join(' ')).toBe(label);
    expect(lines('  deux   espaces ')).toEqual(['deux espaces']);
    expect(lines('')).toEqual([]);
  });

  it('cuts a word longer than a line with a hyphen, keeping every letter', () => {
    const word = 'Anticonstitutionnellement-et-plus-encore';
    const cut = lines(word);
    expect(cut.every((line) => line.length <= LINE_MAX)).toBe(true);
    expect(cut.map((line, index) => (index < cut.length - 1 ? line.slice(0, -1) : line)).join('')).toBe(word);
  });

  it('writes the signs Mermaid reads as their entity codes, the sign of the codes first', () => {
    expect(escaped('A "B" <C> #1 `d`')).toBe('A #quot;B#quot; #lt;C#gt; #35;1 #96;d#96;');
    expect(quoted(['VIS-E001', 'Saisir'])).toBe('"VIS-E001<br/>Saisir"');
  });
});

describe('the texts of a journey (rendu Mermaid, retour de l’essai du rapport)', () => {
  const font = { size: 13, weight: 400 };
  /** The lines a browser makes of a text at 136 px, at its spaces, after a hyphen and between two characters of a wide script. */
  const rows = (text: string) => {
    const out = [''];
    for (const piece of text.match(new RegExp(`${WIDE_CHARACTER.source}|[^ \\-${WIDE_CHARACTER.source.slice(1, -1)}]+-?|-| `, 'gu')) ?? []) {
      const line = out[out.length - 1] ?? '';
      if (line === '' || arialMeasure(`${line}${piece}`.trimEnd(), font) <= 136) out[out.length - 1] = `${line}${piece}`;
      else out.push(piece.trimStart());
    }
    return out.map((line) => line.trim());
  };

  it('keeps a title that holds in two lines of its box as it is', () => {
    for (const title of ['Déclenchement quotidien', 'Affichage tableau Gemba', 'Vérifier la complétude du', 'Prêt pour la réunion']) {
      expect(boxText(title)).toBe(title);
      expect(rows(boxText(title)).length).toBeLessThanOrEqual(2);
    }
  });

  it('cuts a word wider than the box with a hyphen, and ends a text too long with an ellipsis, never beyond two lines', () => {
    expect(boxText('Rechnungsprüfungsverfahrensordnung')).toMatch(/^Rechnungsprüfungs\S*-\S+$/u);
    const long = boxText('Vérifier la complétude du dossier de demande et des pièces justificatives jointes avant validation');
    expect(long.endsWith('…')).toBe(true);
    for (const text of [long, boxText('W'.repeat(40)), boxText('核对供应商发票与订单是否一致并记录差异原因和处理结果以及后续的跟进措施'), boxText('Проверка полноты пакета документов поставщика')]) {
      expect([text, rows(text).length <= 2, rows(text).every((line) => arialMeasure(line, font) <= 136)]).toEqual([text, true, true]);
    }
  });

  it('measures Arial as it is drawn: the letters by their width, an accent as its letter, an ideograph as one em', () => {
    expect(arialMeasure('il', font)).toBeLessThan(arialMeasure('WM', font));
    expect(arialMeasure('é', font)).toBe(arialMeasure('e', font));
    expect(arialMeasure('发票', font)).toBe(26);
  });

  it('writes a journey without actors and a section as wide as its steps, every box read by Mermaid', async () => {
    const diagram = journeyOf('', [{ title: 'Diffusion sur le terrain et suivi des écarts constatés', tasks: [{ title: 'Affichage tableau Gemba', score: 3 }, { title: 'Prêt', score: 9 }] }]);
    expect(diagram.split('\n').slice(1)).toEqual(['journey', '  section Diffusion sur le terrain et suivi des écarts constatés', '    Affichage tableau Gemba: 3', '    Prêt: 5']);
    // 13 px and lines 1.25 high whatever the page: two lines hold in a box of 50 px.
    expect(diagram.split('\n')[0]).toContain('"fontSize": "13px"}, "themeCSS": "foreignObject div { line-height: 1.25; } foreignObject div div { padding: 0 6px; }"');
    await assertReadByMermaid(diagram);
  });
});

describe('the check by Mermaid itself', () => {
  it('refuses a diagram whose syntax is broken, so that a diagram read is a proof', async () => {
    await expect(mermaid.parse('flowchart LR\n  A[`"`open --> B')).rejects.toThrow();
  });
});

describe('the map as a flowchart (plan 3.1, L3)', () => {
  it('draws a subgraph per lane in their order, the steps with their code and their shape, the flows with their labels', async () => {
    const diagram = flowchartOf(sampleMap(), 'fr');
    expect(diagram.split('\n')).toEqual([
      mermaidInit(1),
      'flowchart LR',
      '  subgraph L1["Commercial"]',
      '    S1(["VIS-E001<br/>Demande reçue"])',
      '    S2["VIS-E002<br/>Vérifier la demande"]',
      '    S3{"VIS-E003<br/>Conforme ?"}',
      '  end',
      '  subgraph L2["Comptabilité"]',
      '    S4((("VIS-E004<br/>Facture émise")))',
      '  end',
      '  S1 --> S2',
      '  S2 --> S3',
      '  S3 -->|"non"| S2',
      '  S3 -->|"oui"| S4',
    ]);
    await assertReadByMermaid(diagram);
  });

  it('shows the map in the language it is shown in, and an extract of it with only the flows between its steps', async () => {
    expect(flowchartOf(sampleMap(), 'en')).toContain('S2["VIS-E002<br/>Check the request"]');
    const extract = flowchartOf(sampleMap(), 'fr', { only: new Set(['s2', 's3']) });
    expect(extract).toContain('  S1 --> S2');
    expect(extract).not.toContain('Facture');
    expect(extract).toContain('S2 -->|"non"| S1');
    await assertReadByMermaid(extract);
  });

  it('colours the steps with a problem as the charter colours a fault, and nothing without its colours', async () => {
    const look = { problem: { fill: '#FBE9E7', stroke: '#C0362C' } };
    const diagram = flowchartOf(sampleMap(), 'fr', { marked: new Set(['s3']), look });
    expect(diagram).toContain('  classDef issue fill:#FBE9E7,stroke:#C0362C,stroke-width:2px\n  class S3 issue');
    expect(flowchartOf(sampleMap(), 'fr', { marked: new Set(['s3']), look: { problem: null } })).not.toContain('classDef');
    await assertReadByMermaid(diagram);
  });

  it('holds with the hardest texts: quotes, signs, a label of 150 characters, the shapes of every type, a detailed step', async () => {
    const long = 'Contrôler « le dossier » #3 <urgent> & `vite` '.repeat(4).slice(0, 150);
    const map = mapOf({
      lanes: [laneOf('A', 0, { name: 'Équipe "achats" <siège>' }), laneOf('B', 1)],
      steps: [
        stepOf('t', 'A', 0, { label: long }), stepOf('w', 'A', 1, { type: 'wait', label: 'Attendre' }), stepOf('p', 'B', 2, { type: 'parallel', label: '' }),
        stepOf('i', 'B', 3, { type: 'inclusive', label: 'Choisir' }), stepOf('d', 'A', 4, { sub_map_id: 'sub', label: 'Détailler' }),
      ],
      flows: [flowOf('f1', 't', 'w', { label: 'si « oui » | #ok' }), flowOf('f2', 'w', 'p'), flowOf('f3', 'p', 'i'), flowOf('f4', 'i', 'd')],
    });
    const diagram = flowchartOf(map, 'fr');
    expect(diagram).toContain('{"+<br/>');
    expect(diagram).toContain('{"O<br/>');
    expect(diagram).toMatch(/\[\["[^"]+Détailler"\]\]/);
    expect(diagram).toMatch(/>"[^"]+Attendre"\]/);
    await assertReadByMermaid(diagram);
  });
});

describe('the texts that would break or reconfigure a diagram (plan 3.1, E11)', () => {
  it('a directive written in a label stays a text: the diagram reads and keeps its header', async () => {
    const map = sampleMap();
    const steps = map.steps.map((step, index) => (index === 1 ? { ...step, label: 'Vérifier %%{init: {"theme": "dark"}}%% le dossier à 5 %' } : step));
    const diagram = flowchartOf({ ...map, steps }, 'fr');
    expect(diagram.split('\n').filter((line) => line.includes('%%'))).toEqual([mermaidInit(1)]);
    await assertReadByMermaid(diagram);
  });

  it('a title of a journey that starts with a word of the diagram, or made empty by its signs, never breaks it', async () => {
    const diagram = journeyOf('Parcours', [{ title: ':', tasks: [{ title: 'Section des achats', score: 3 }, { title: 'journey', score: 2 }] }]);
    expect(diagram).toContain('  section 1');
    expect(diagram).toContain('    «Section des achats»: 3\n    «journey»: 2');
    await assertReadByMermaid(diagram);
  });

  it('two lanes of the same name once cut stay two parts of the pie; the actions of the same scores are one point', async () => {
    const pie = pieOf('Étapes', [{ label: 'Responsable qualité site A', value: 5 }, { label: 'Responsable qualité site B', value: 3 }, { label: 'Autres', value: 1 }], 'Autres');
    expect(new Set(pie.split('\n').filter((line) => line.startsWith('  "')).map((line) => line.split('"')[1])).size).toBe(3);
    await assertReadByMermaid(pie);
    const texts = { title: 'Actions', x: ['Facile', 'Difficile'], y: ['Faible', 'Fort'], quadrants: ['Plan', 'Vite', 'Peut-être', 'Non'] } as const;
    const matrix = quadrantOf(texts, [1, 2, 3, 4, 5].map((rank) => ({ name: `A${String(rank)}`, effort: 2, impact: 4 })).concat({ name: 'A6', effort: 3, impact: 4 }));
    expect(matrix.split('\n').filter((line) => line.includes('['))).toEqual(['  "A1 A2 A3 A4 A5": [0.30, 0.72]', '  "A6": [0.55, 0.72]']);
    await assertReadByMermaid(matrix);
  });

  it('the matrix of the actions in each language of the interface: outside its quotes, ASCII only, as a Mermaid before 11.5 reads it', async () => {
    const folder = join(import.meta.dirname, '..', '..', '..', 'shared', 'i18n', 'locales');
    const languages = readdirSync(folder).filter((file) => file.endsWith('.json'));
    expect(languages.length).toBe(18);
    for (const file of languages) {
      const report = (JSON.parse(readFileSync(join(folder, file), 'utf8')) as { report: Record<string, string> }).report;
      const texts = {
        title: report.actions ?? '', x: [report.effort_low ?? '', report.effort_high ?? ''], y: [report.impact_low ?? '', report.impact_high ?? ''],
        quadrants: [report.quadrant_plan ?? '', report.quadrant_now ?? '', report.quadrant_maybe ?? '', report.quadrant_avoid ?? ''],
      } as const;
      const matrix = quadrantOf(texts, [{ name: 'A1', effort: 2, impact: 4 }, { name: 'A2', effort: 4, impact: 1 }]);
      // The title is read to the end of its line by every version: only the other lines need their quotes.
      const outside = matrix.split('\n').filter((line) => !line.startsWith('  title ')).map((line) => line.replace(/"[^"]*"/g, ''));
      expect([file, outside.filter((line) => /[^ -~]/.test(line))]).toEqual([file, []]);
      await assertReadByMermaid(matrix);
    }
  });
});