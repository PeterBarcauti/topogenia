// mermaidImport.test.ts
// A Mermaid flowchart opened as a process (plan 3.1, L6): its shapes, its links and their labels, its chains, its subgraphs as lanes, its styles set aside and said; the flowchart and the Markdown the application exports read back as they were; texts at random that never raise but a refusal, read in a bounded time; every file written valid for the published schema of the .pmap 1.2.

import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import Ajv2020 from 'ajv/dist/2020.js';
import { mapMarkdown } from '../../map/export/markdown.ts';
import { flowchartOf } from '../../map/export/mermaid.ts';
import { i18next, PRODUCT_NAME, type Translate } from '../../../shared/i18n/i18n.ts';
import { flowOf, laneOf, mapOf, sampleMap, stepOf } from '../../../test/maps.ts';
import { diagramOf, mermaidText, mermaidToPmap } from './mermaidImport.ts';
import { plainText, TableError } from './tableImport.ts';

const t: Translate = (key, options) => i18next.t(key, options);
const schema = JSON.parse(readFileSync(join(import.meta.dirname, '..', '..', '..', '..', '..', 'schemas', 'pmap-1.2.schema.json'), 'utf8')) as object;
const validate = new Ajv2020({ allErrors: true, strictTypes: false }).compile(schema);

interface Pmap {
  readonly project: { readonly name: string };
  readonly map: {
    readonly lanes: readonly { id: string; name: string }[];
    readonly steps: readonly { id: string; lane: string; label: string; type?: string }[];
    readonly flows: readonly { source: string; target: string; label?: string }[];
  };
  readonly uncertainties?: readonly string[];
}

/** The diagram read as a .pmap, checked against the published schema first. */
function open(text: string, markdown = false, name = 'Achats'): Pmap {
  const file = JSON.parse(mermaidToPmap(text, markdown, name, t)) as Pmap;
  expect(validate(file), JSON.stringify(validate.errors)).toBe(true);
  return file;
}

const label = (file: Pmap, id: string) => file.map.steps.find((step) => step.id === id)?.label;
const laneName = (file: Pmap, id: string) => file.map.lanes.find((lane) => lane.id === id)?.name;
const flowsOf = (file: Pmap) => file.map.flows.map((flow) => `${label(file, flow.source) ?? ''}>${label(file, flow.target) ?? ''}${flow.label === undefined ? '' : ` (${flow.label})`}`);
const stepsOf = (file: Pmap) => file.map.steps.map((step) => [step.label, step.type ?? 'task', laneName(file, step.lane)]);

beforeEach(async () => {
  await i18next.changeLanguage('fr');
});

describe('the shapes and the links of a flowchart (plan 3.1, L6)', () => {
  it('reads the shapes as types: a stadium or a circle a start without flow in, an end without flow out; a double circle an end', () => {
    const file = open([
      'flowchart LR', '  A([Début]) --> B[Saisir] --> C{Conforme ?}', '  C -->|oui| D(((Classé)))', '  C -- non --> E>Attendre le retour]',
      '  E --> B', '  F((Rond seul))', '  B --> G((Sortie))', '  H[(Base)] --- B', '  I{{Préparer}} ==> B',
    ].join('\n'));
    expect(stepsOf(file)).toEqual([
      ['Début', 'start', 'Processus'], ['Saisir', 'task', 'Processus'], ['Conforme ?', 'decision', 'Processus'], ['Classé', 'end', 'Processus'],
      ['Attendre le retour', 'wait', 'Processus'], ['Rond seul', 'start', 'Processus'], ['Sortie', 'end', 'Processus'], ['Base', 'task', 'Processus'],
      ['Préparer', 'task', 'Processus'],
    ]);
    expect(flowsOf(file)).toEqual([
      'Début>Saisir', 'Saisir>Conforme ?', 'Conforme ?>Classé (oui)', 'Conforme ?>Attendre le retour (non)', 'Attendre le retour>Saisir',
      'Saisir>Sortie', 'Base>Saisir', 'Préparer>Saisir',
    ]);
    expect(file.uncertainties).toBeUndefined();
    expect(file.project.name).toBe('Achats');
  });

  it('reads the arrows dotted, thick and long, the labels in pipes, in quotes or between the halves, the chains and the groups of « & »', () => {
    const file = open('graph TD;\nA-->B;B-.->C\nC ==>|"Vu, signé"| D & E\nA & B ----> F\nD -. relance .-> A\nE == urgent ==> F');
    expect(flowsOf(file)).toEqual(['A>B', 'B>C', 'C>D (Vu, signé)', 'C>E (Vu, signé)', 'A>F', 'B>F', 'D>A (relance)', 'E>F (urgent)']);
  });

  it('reads the shapes of Mermaid 11, a label in Markdown, the entity codes in one pass, the tags of HTML left out', () => {
    const file = open([
      'flowchart LR', 'A@{ shape: diam, label: "Conforme ?" } --> B@{ shape: dbl-circ }', 'C@{ shape: delay, label: "Attendre, puis voir" }',
      'D["`**Valider** le bon`"]', 'E["#quot;net#quot; #35;quot; #9829; #9999999;"]', 'G["<b>Gras</b> et a<b"]',
    ].join('\n'));
    expect(stepsOf(file).map(([text, type]) => [text, type])).toEqual([
      ['Conforme ?', 'decision'], ['B', 'end'], ['Attendre, puis voir', 'wait'], ['**Valider** le bon', 'task'], ['"net" #quot; \u2665 #9999999;', 'task'], ['Gras et a< b', 'task'],
    ]);
    expect([mermaidText('"a<br/>b<br>c"'), mermaidText('#60;b#62;'), plainText('a\u0000b\tc\u0085')]).toEqual(['a\nb\nc', '<b>', 'ab\tc']);
  });
});

describe('the subgraphs, the comments and what is set aside (plan 3.1, L6)', () => {
  it('makes a lane of each subgraph, the innermost one for a node, the lane « Processus » for the nodes outside one', () => {
    const file = open([
      'flowchart TB', '  subgraph V["Visites"]', '    direction LR', '    A[Saisir]', '    subgraph X [Contrôle]', '      B[Vérifier]', '    end', '    C[Facturer]', '  end',
      '  subgraph "Service client"', '    D[Répondre]', '  end', '  subgraph Achats', '  end', '  A --> B --> C --> D --> E[Classer]',
    ].join('\n'));
    expect(stepsOf(file)).toEqual([['Saisir', 'task', 'Visites'], ['Vérifier', 'task', 'Contrôle'], ['Facturer', 'task', 'Visites'], ['Répondre', 'task', 'Service client'], ['Classer', 'task', 'Processus']]);
    expect(file.map.lanes.map((lane) => lane.name)).toEqual(['Visites', 'Contrôle', 'Service client', 'Processus']);
  });

  it('leaves the comments out, but not the signs %% of a label; reads the front matter and the directives as nothing', () => {
    const file = open(['---', 'title: Achats', 'config:', '  theme: neutral', '---', '%%{', '  init: {"theme": "neutral"}', '}%%', 'flowchart LR', '  %% une note', '  A["Remise 50 %% max"] --> B %% fin de ligne', '  B --> C; %% C --> D'].join('\n'));
    expect(flowsOf(file)).toEqual(['Remise 50 %% max>B', 'B>C']);
  });

  it('reads a node whose name begins with a keyword as a node, not as the keyword (audit 3.1 V2, 5.2)', () => {
    const file = open(['flowchart LR', '  class1[Saisir] --> style_2[Vérifier]', '  direction1[Classer] --> subgraph1[Archiver]', '  style_2 --> direction1',
      '  classé[Payer] --> linkStyle3[Clore]', '  subgraph G [Groupe]', '    click4[Envoyer]', '  end'].join('\n'));
    expect(file.map.steps.map((step) => step.label)).toEqual(['Saisir', 'Vérifier', 'Classer', 'Archiver', 'Payer', 'Clore', 'Envoyer']);
    expect(flowsOf(file)).toEqual(['Saisir>Vérifier', 'Classer>Archiver', 'Vérifier>Classer', 'Payer>Clore']);
    expect(file.uncertainties ?? []).toEqual([]);
    expect(file.map.lanes.map((lane) => lane.name)).toContain('Groupe');
  });

  it('skips the header by its place: a node named flowchart-1 or graph-2 is a node, flowchart-elk still a header (audit 3.1 V3, U10)', () => {
    const file = open(['flowchart LR', '  flowchart-1[Étape] --> B', '  graph-2[Contrôle] --> B'].join('\n'));
    expect([file.map.steps.map((step) => step.label), flowsOf(file), file.uncertainties ?? []]).toEqual([['Étape', 'B', 'Contrôle'], ['Étape>B', 'Contrôle>B'], []]);
    const elk = open(['flowchart-elk TD', '  A[Saisir] --> B[Vérifier]'].join('\n'));
    expect([elk.map.steps.map((step) => step.label), flowsOf(elk), elk.uncertainties ?? []]).toEqual([['Saisir', 'Vérifier'], ['Saisir>Vérifier'], []]);
  });

  it('reads a keyword glued to a shape, or direction without a direction, as a node; the directives stay set aside (audit 3.1 V4, A2)', () => {
    const file = open(['flowchart LR', '  direction[Direction générale] --> style[Style]', '  click(Clic) --> accTitle{Titre ?}', '  direction --> subgraph1',
      '  subgraph G [Groupe]', '    direction TB', '    class[Classe]', '  end', '  accTitle: Les achats', '  click direction callback', '  style click fill:#f00'].join('\n'));
    expect(file.map.steps.map((step) => step.label)).toEqual(['Direction générale', 'Style', 'Clic', 'Titre ?', 'subgraph1', 'Classe']);
    expect(flowsOf(file)).toEqual(['Direction générale>Style', 'Clic>Titre ?', 'Direction générale>subgraph1']);
    expect(file.map.lanes.map((lane) => lane.name)).toContain('Groupe');
    expect(file.uncertainties).toEqual([
      'Ligne 9 : « accTitle » ne fait que la mise en forme, il est laissé de côté.',
      'Ligne 10 : « click » ne fait que la mise en forme, il est laissé de côté.',
      'Ligne 11 : « style » ne fait que la mise en forme, il est laissé de côté.',
    ]);
  });

  it('says once each word of style and the line of each statement not understood, of each link hidden, both ways or to a whole subgraph', () => {
    const file = open([
      'flowchart LR', '  classDef issue fill:#f00', '  A:::issue --> B', '  class A issue', '  classDef other fill:#0f0', '  style B fill:#ccc', '  A <--> C', '  A ~~~ D',
      '  subgraph G [Groupe]', '    E[[Détaillée]]', '  end', '  G --> A', '  A --> ??', '  click A callback', '  F[jamais fermé --> A',
    ].join('\n'));
    expect(file.map.steps.map((step) => step.label)).toEqual(['A', 'B', 'C', 'D', 'Détaillée']);
    expect(flowsOf(file)).toEqual(['A>B', 'A>C']);
    expect(file.uncertainties).toEqual([
      'Ligne 2 : « classDef » ne fait que la mise en forme, il est laissé de côté.',
      'Ligne 3 : « ::: » ne fait que la mise en forme, il est laissé de côté.',
      'Ligne 4 : « class » ne fait que la mise en forme, il est laissé de côté.',
      'Ligne 6 : « style » ne fait que la mise en forme, il est laissé de côté.',
      'Ligne 13 : instruction non comprise, elle est laissée de côté.',
      'Ligne 14 : « click » ne fait que la mise en forme, il est laissé de côté.',
      'Ligne 15 : instruction non comprise, elle est laissée de côté.',
      'Ligne 7 : flèche à double sens gardée dans un seul sens.',
      'Ligne 8 : lien invisible laissé de côté.',
      'Ligne 12 : lien vers un couloir entier laissé de côté.',
      'Le nœud E était un sous-processus : il devient une étape simple, sans carte détaillée.',
    ]);
  });

  it('cuts a label, a lane or a label of flow past the bounds of the format, and says it', () => {
    const file = open(`flowchart LR\nsubgraph S ["${'c'.repeat(160)}"]\nA["${'x'.repeat(160)}"] -->|${'y'.repeat(70)}| B\nC --> A\nend`);
    expect([file.map.steps[0]?.label.length, file.map.lanes[0]?.name.length, file.map.flows[0]?.label?.length]).toEqual([150, 150, 60]);
    expect(file.uncertainties).toEqual(['Ligne 3 : le libellé de la flèche est coupé à 60 caractères.', 'Le libellé du nœud A est coupé à 150 caractères.', 'Le nom d’un couloir est coupé à 150 caractères.']);
  });
});

describe('the files refused (plan 3.1, L6)', () => {
  it('refuses another diagram, a flowchart without a node, a Markdown without a flowchart, plainly', () => {
    expect(() => mermaidToPmap('sequenceDiagram\n  A->>B: Bonjour', false, 'x', t)).toThrow(new TableError('files.mermaid.not_flowchart'));
    expect(() => mermaidToPmap('flowchart LR\n%% rien', false, 'x', t)).toThrow(new TableError('files.mermaid.empty'));
    expect(() => mermaidToPmap('# Titre\n\n```mermaid\npie\n  "A" : 1\n```\n', true, 'x', t)).toThrow(new TableError('files.mermaid.no_block'));
    expect(t('files.mermaid.not_flowchart')).toBe('Le diagramme Mermaid doit être un logigramme (flowchart ou graph).');
  });

  it('refuses a diagram past its bounds: its lines, its nodes, its links', () => {
    expect(() => mermaidToPmap(`flowchart LR\n${'A\n'.repeat(20_000)}`, false, 'x', t)).toThrow(new TableError('files.mermaid.too_long', { lines: 20_000, links: 20_000 }));
    expect(() => mermaidToPmap(`flowchart LR\n${Array.from({ length: 5001 }, (_, index) => `N${String(index)}`).join('\n')}`, false, 'x', t)).toThrow(new TableError('files.table.too_many', { max: 5000 }));
    const many = Array.from({ length: 150 }, (_, index) => `N${String(index)}`).join(' & ');
    expect(() => mermaidToPmap(`flowchart LR\n${many} --> ${many.replace(/N/g, 'M')}`, false, 'x', t)).toThrow(new TableError('files.mermaid.too_long', { lines: 20_000, links: 20_000 }));
    expect(t('files.mermaid.too_long', { lines: 20_000, links: 20_000 })).toContain('20');
  });

  it('takes the first block of a Markdown that is a flowchart, and counts its lines from the top of the document', () => {
    const text = '# Rapport\n\n```mermaid\npie\n  "A" : 1\n```\n\nTexte.\n\n```mermaid\n%% vue\nflowchart LR\n  A --> B\n  style A fill:#fff\n```\n';
    expect(diagramOf(text, true).offset).toBe(10);
    expect(open(text, true).uncertainties).toEqual(['Ligne 14 : « style » ne fait que la mise en forme, il est laissé de côté.']);
  });
});

/** A map of every type of step, a detailed one, long words and lanes, the signs Mermaid reads, labels of flow in lines. */
function richMap() {
  return mapOf({
    lanes: [laneOf('l1', 0, { name: 'Commercial' }), laneOf('l2', 1, { name: 'Direction administrative et financière du groupe' }), laneOf('l3', 2, { name: 'Vide' })],
    steps: [
      stepOf('s1', 'l1', 0, { type: 'start', label: 'Demande reçue' }),
      stepOf('s2', 'l1', 1, { label: 'Anticonstitutionnellement-vérifier la demande du client' }),
      stepOf('s3', 'l1', 2, { type: 'decision', label: 'Prix "net" #5 > 3 & a<3 ; x' }),
      stepOf('s4', 'l2', 3, { type: 'parallel', label: 'Lancer' }),
      stepOf('s5', 'l2', 4, { type: 'wait', label: 'Attendre la signature' }),
      stepOf('s6', 'l2', 4, { row: 1, label: 'Instruire le dossier', sub_map_id: 'm2', sub_map_empty: false }),
      stepOf('s7', 'l2', 5, { type: 'inclusive', label: 'Joindre' }),
      stepOf('s8', 'l2', 6, { type: 'end', label: 'Facture émise' }),
    ],
    flows: [
      flowOf('f1', 's1', 's2'), flowOf('f2', 's2', 's3'), flowOf('f3', 's3', 's4', { label: 'oui' }), flowOf('f4', 's3', 's2', { label: 'non, à reprendre' }),
      flowOf('f5', 's4', 's5'), flowOf('f6', 's4', 's6', { label: 'Après validation complète par le responsable' }), flowOf('f7', 's5', 's7'),
      flowOf('f8', 's6', 's7'), flowOf('f9', 's7', 's8'),
    ],
  });
}

describe('the flowchart the application exports, read back (plan 3.1, L3 and L6)', () => {
  it('gives back its lanes, its steps, their types and labels, its flows and their labels', () => {
    const file = open(flowchartOf(richMap(), 'fr'));
    expect(file.map.lanes.map((lane) => lane.name)).toEqual(['Commercial', 'Direction administrative et financière du groupe']);
    expect(stepsOf(file)).toEqual([
      ['Demande reçue', 'start', 'Commercial'], ['Anticonstitutionnellement-vérifier la demande du client', 'task', 'Commercial'],
      ['Prix "net" #5 > 3 & a<3 ; x', 'decision', 'Commercial'], ['Lancer', 'parallel', 'Direction administrative et financière du groupe'],
      ['Attendre la signature', 'wait', 'Direction administrative et financière du groupe'], ['Instruire le dossier', 'task', 'Direction administrative et financière du groupe'],
      ['Joindre', 'inclusive', 'Direction administrative et financière du groupe'], ['Facture émise', 'end', 'Direction administrative et financière du groupe'],
    ]);
    expect(flowsOf(file).sort()).toEqual([
      'Anticonstitutionnellement-vérifier la demande du client>Prix "net" #5 > 3 & a<3 ; x', 'Attendre la signature>Joindre', 'Demande reçue>Anticonstitutionnellement-vérifier la demande du client',
      'Instruire le dossier>Joindre', 'Joindre>Facture émise', 'Lancer>Attendre la signature', 'Lancer>Instruire le dossier (Après validation complète par le responsable)',
      'Prix "net" #5 > 3 & a<3 ; x>Anticonstitutionnellement-vérifier la demande du client (non, à reprendre)', 'Prix "net" #5 > 3 & a<3 ; x>Lancer (oui)',
    ]);
    expect(file.uncertainties).toEqual(['Le nœud S6 était un sous-processus : il devient une étape simple, sans carte détaillée.']);
  });

  it('reads back the first diagram of the Markdown of a map, the colours of its steps with a problem said as a style', () => {
    const facts = { title: 'VIS - Visites', breadcrumb: 'Visites', classification: 'Classification : Interne', date: '5 novembre 2026', footer: `Généré par ${PRODUCT_NAME}` };
    const text = mapMarkdown(sampleMap(), 'fr', t, facts, { problem: { fill: '#fde2e1', stroke: '#c0392b' } });
    const plain = open(text, true);
    expect(stepsOf(plain)).toEqual([['Demande reçue', 'start', 'Commercial'], ['Vérifier la demande', 'task', 'Commercial'], ['Conforme ?', 'decision', 'Commercial'], ['Facture émise', 'end', 'Comptabilité']]);
    expect(flowsOf(plain)).toEqual(['Demande reçue>Vérifier la demande', 'Vérifier la demande>Conforme ?', 'Conforme ?>Vérifier la demande (non)', 'Conforme ?>Facture émise (oui)']);
  });
});

/** A generator of numbers at random, seeded, so that a failure comes back the same. */
function seeded(seed: number): () => number {
  let state = seed;
  return () => {
    state = (state + 0x6d2b79f5) | 0;
    let mixed = Math.imul(state ^ (state >>> 15), 1 | state);
    mixed = (mixed + Math.imul(mixed ^ (mixed >>> 7), 61 | mixed)) ^ mixed;
    return ((mixed ^ (mixed >>> 14)) >>> 0) / 4_294_967_296;
  };
}

const TOKENS = [
  'A', 'B1', 'é_2', 'C', 'D[Saisir]', 'E{Conforme ?}', 'F(("Début"))', 'G>Attendre]', 'H([Fin])', 'A --> B1', 'C -->|oui| D', ' ', ' ', '\n', '\n',
  ';', '-->', '---', '-.->', '==>', '<-->', '~~~', '--o', '|', '"', '[', ']', '(', ')', '{', '}', '((', '))', '([', '>', '&',
  'subgraph ', 'end', '%%', ':::x', '@{', 'shape: diam', 'label: "z"', '#quot;', '#9999999;', '#55296;', '<b>', '<br/>', '\u{1F600}', '\u0000', '\u0085', '-- oui -->',
  'classDef', 'style', '`', '\\', '/', 'direction LR', 'flowchart', 'x'.repeat(200),
];

/** Every text of a value, its keys left out. */
const textsOf = (value: unknown): string[] =>
  typeof value === 'string' ? [value] : Array.isArray(value) ? value.flatMap(textsOf) : typeof value === 'object' && value !== null ? Object.values(value).flatMap(textsOf) : [];

describe('texts at random (plan 3.1, L6)', () => {
  it('never raises but a refusal, writes only files valid for the schema and the host, in a bounded time', () => {
    const random = seeded(31);
    const started = performance.now();
    let opened = 0;
    for (let round = 0; round < 1500; round++) {
      const tokens = Array.from({ length: Math.floor(random() * 80) }, () => TOKENS[Math.floor(random() * TOKENS.length)] ?? '');
      const text = `${round % 3 === 0 ? '' : 'flowchart LR\n'}${tokens.join('')}`;
      try {
        const file = JSON.parse(mermaidToPmap(round % 7 === 0 ? `# Essai\n\n${'`'.repeat(3)}mermaid\n${text}\n${'`'.repeat(3)}\n` : text, round % 7 === 0, 'Essai', t)) as Pmap;
        expect(validate(file), `${JSON.stringify(text)} ${JSON.stringify(validate.errors)}`).toBe(true);
        expect(textsOf(file).filter((value) => /(?![\t\n\r])\p{Cc}|[\uD800-\uDFFF]/u.test(value.replace(/[\uD800-\uDBFF][\uDC00-\uDFFF]/g, ''))), JSON.stringify(text)).toEqual([]);
        expect(file.map.steps.every((step) => step.label.length <= 150) && file.map.flows.every((flow) => (flow.label?.length ?? 0) <= 60), JSON.stringify(text)).toBe(true);
        opened++;
      } catch (failure) {
        if (!(failure instanceof TableError)) throw new Error(`${JSON.stringify(text)}: ${String(failure)}`, { cause: failure });
      }
    }
    expect(opened).toBeGreaterThan(500);
    expect(performance.now() - started).toBeLessThan(15_000);
  });

  it('reads in a bounded time the texts made to make a reading come back: spaces without end, brackets, quotes, links', () => {
    const texts = [
      `flowchart LR\nA -- ${' '.repeat(1990)}x`, `flowchart LR\n${'['.repeat(2000)}`, `flowchart LR\nA["${'"'.repeat(1990)}`,
      `flowchart LR\n${Array.from({ length: 600 }, (_, index) => `N${String(index)}`).join(' --> ')}`, `flowchart LR\n${'A -- x '.repeat(280)}`,
      `flowchart LR\n${`A -- ${'- '.repeat(990)}\n`.repeat(200)}`, `flowchart LR\nA@{ ${'"a"'.repeat(600)}`,
    ];
    for (const text of texts) {
      const started = performance.now();
      try {
        mermaidToPmap(text, false, 'x', t);
      } catch (failure) {
        expect(failure).toBeInstanceOf(TableError);
      }
      expect(performance.now() - started, text.slice(0, 40)).toBeLessThan(1500);
    }
  });
});

describe('the texts made to freeze a reading, and the bounds of the host (plan 3.1, E11)', () => {
  it('reads in a time bounded by its length a text of directives never closed, of fences on one line, of fields without end', () => {
    const texts: [string, boolean][] = [
      [`flowchart LR\n${'%%{'.repeat(100_000)}\nA --> B`, false],
      [`${'```mermaid'.repeat(40_000)}\n`, true],
      [`flowchart LR\n${`A@{${'a'.repeat(1990)}}\n`.repeat(2000)}`, false],
    ];
    for (const [text, markdown] of texts) {
      const started = performance.now();
      try {
        mermaidToPmap(text, markdown, 'x', t);
      } catch (failure) {
        expect(failure).toBeInstanceOf(TableError);
      }
      expect(performance.now() - started, text.slice(0, 20)).toBeLessThan(3000);
    }
  });

  it('refuses more lanes than a map holds, and cuts what an uncertainty quotes', () => {
    const many = Array.from({ length: 201 }, (_, index) => `subgraph G${String(index)} [Couloir ${String(index)}]\nN${String(index)}\nend`).join('\n');
    expect(() => mermaidToPmap(`flowchart LR\n${many}`, false, 'x', t)).toThrow(new TableError('files.mermaid.too_many_lanes', { max: 200 }));
    const file = open(`flowchart LR\n${'K'.repeat(1500)}[[Détail]]`);
    expect(file.uncertainties?.every((text) => text.length <= 500)).toBe(true);
  });
});