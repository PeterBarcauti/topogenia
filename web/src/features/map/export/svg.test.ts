// svg.test.ts
// The image SVG of a map (plan 3.1, L7): every lane, step, flow and head drawn from the layout, the labels, codes, pictograms and badges as the preferences ask, the value stream under the steps, the line of the classification; nothing a reader of Word or PowerPoint would not draw, every number finite; the copy read back whole; the same layout giving the same text.

import type { Schema } from '../../../api/client.ts';
import { i18next, type Translate } from '../../../shared/i18n/i18n.ts';
import { flowOf, laneOf, mapOf, sampleMap, stepOf, streamOf } from '../../../test/maps.ts';
import { copyMetadata, pmapOfSvg } from '../../project/files/svgCopy.ts';
import { FORMAT } from '../../project/files/tableImport.ts';
import { layoutMap } from '../model/mapLayout.ts';
import { estimateMeasure } from '../model/textSize.ts';
import { mapSvg, svgSize, type SvgLook } from './svg.ts';
import { svgIcons, type SvgIcons } from './svgIcons.ts';

const t: Translate = (key, options) => i18next.t(key, options);

/** Colours of the tests, each one apart so that a piece is found by its colour. */
const look: SvgLook = {
  surface: '#ffffff', laneOdd: '#fefefe', laneEven: '#f5f5f5', laneSeparator: '#e8e8ec', border: '#e3e4e8', text: '#1d1d1f', textMuted: '#6b6e76',
  accentStrong: '#4343b8', edge: '#80858e', frameLabelBg: '#eeeefb', dangerMuted: '#b4554d', warningSoft: '#fff3dd', warningText: '#8a5300', warningGraphic: '#c27400',
  raci: { A: '#a00001', S: '#a00002', C: '#a00003', I: '#a00004' },
  pastels: ['#f00001', '#f00002', '#f00003', '#f00004', '#f00005', '#f00006', '#f00007', '#f00008'],
  inks: ['#100001', '#100002', '#100003', '#100004', '#100005', '#100006', '#100007', '#100008'],
};

let icons: SvgIcons;

beforeAll(async () => {
  icons = await svgIcons();
});

beforeEach(async () => {
  await i18next.changeLanguage('fr');
});

const pen = (overrides: Partial<Parameters<typeof mapSvg>[1]> = {}) => ({
  look, t, measure: estimateMeasure, icons, codes: true, pictos: true, raci: false, stream: false, title: 'VIS - Visites', caption: 'Classification : Interne', fonts: null, copy: null, ...overrides,
});

/** The image read back as a document of its own. */
function documentOf(svg: string): Document {
  const document = new DOMParser().parseFromString(svg, 'image/svg+xml');
  expect(document.getElementsByTagName('parsererror')).toHaveLength(0);
  return document;
}

const count = (document: Document, selector: string): number => document.querySelectorAll(selector).length;
const texts = (document: Document): string[] => [...document.querySelectorAll('text')].map((text) => text.textContent);

describe('the image SVG of a map (plan 3.1, L7)', () => {
  it('draws each lane, step, flow and its head, each label, and the line of the classification, nothing a reader of Office would not draw', () => {
    const layout = layoutMap(sampleMap(), 'fr', estimateMeasure);
    const svg = mapSvg(layout, pen());
    const document = documentOf(svg);
    expect([count(document, 'g.lane'), count(document, 'g.step'), count(document, 'g.flow'), count(document, 'path.head'), count(document, 'g.flow-label')]).toEqual([2, 4, 4, 4, 3]);
    expect(texts(document)).toEqual(expect.arrayContaining(['Commercial', 'Comptabilité', 'Demande reçue', 'Vérifier la demande', 'Conforme ?', 'Facture émise', 'VIS-E001', 'oui', 'non', 'Classification : Interne']));
    expect([document.documentElement.getAttribute('width'), document.documentElement.getAttribute('height')]).toEqual([String(svgSize(layout).width), String(svgSize(layout).height)]);
    expect(document.querySelector('title')?.textContent).toBe('VIS - Visites');
    expect([count(document, 'foreignObject'), count(document, 'script'), count(document, 'image'), count(document, 'use'), count(document, 'marker')]).toEqual([0, 0, 0, 0, 0]);
    expect(svg).not.toMatch(/var\(|url\(|NaN|Infinity|undefined|\bon\w+=/);
  });

  it('writes the codes, the pictograms, the RACI badges and the badges of a detailed step as the preferences ask', () => {
    const map = sampleMap({ raci: [{ step_id: 's2', lane_id: 'l2', role: 'A' }] });
    const steps = map.steps.map((step) => (step.id === 's2' ? { ...step, sub_map_id: 'm2', sub_map_empty: false, sheet: { ...step.sheet, duration_minutes: 45, frequency: 'daily' as const },
      markers: { documents: [{ name: 'Devis', direction: 'output' as const }], pain_points: [{ code: 'VIS-P01', description: 'Lent' }] } } : step));
    const document = documentOf(mapSvg(layoutMap({ ...map, steps } as typeof map, 'fr', estimateMeasure), pen({ raci: true })));
    expect(texts(document)).toEqual(expect.arrayContaining([t('map.detailed'), t('map.frequencies.daily'), '45 min', 'A Comptabilité', '1']));
    const bare = documentOf(mapSvg(layoutMap({ ...map, steps } as typeof map, 'fr', estimateMeasure), pen({ codes: false, pictos: false })));
    expect(texts(bare).filter((text) => /^VIS-E\d+$/.test(text) || text === '45 min' || text.startsWith('A '))).toEqual([]);
  });

  it('draws the value stream under the steps with its layer on, and the headcount of the lanes', () => {
    const stream = { language: 'fr', workdayHours: 7, t };
    const figures: Schema<'StepStream'> = {
      step_id: '', throughput: 120, entry: null, entry_volume: null, inflows: [], opened_by: null, executions: null, basis: 'none', work_minutes: null, work_origin: 'none',
      lead_hours: null, lead_origin: 'none', expected_lead_hours: null, work_per_case_minutes: null, charge_hours: null, fte: null, human: true, from_draft: false,
    };
    const steps = Object.fromEntries(['s1', 's2', 's4'].map((id) => [id, { ...figures, step_id: id }]));
    const lanes = { l1: { lane_id: 'l1', headcount: null, human: true, charge_hours: 3, fte: 0.02, charge_complete: true } };
    const layout = layoutMap(sampleMap({ value_stream: streamOf({ steps, lanes }) }), 'fr', estimateMeasure, stream);
    const document = documentOf(mapSvg(layout, pen({ stream: true })));
    expect(count(document, 'g.stream')).toBe(3);
    expect(texts(document)).toEqual(expect.arrayContaining([t('stream.entry_missing'), t('stream.exit')]));
    expect(texts(document)).toEqual(expect.arrayContaining([t('stream.lane.headcount_missing')]));
  });

  it('draws every type of step, a loop and labels of every length with finite numbers only', () => {
    const map = mapOf({
      lanes: [laneOf('l1', 0, { name: 'Une équipe au nom bien plus long que la marge ne le permet sur une ligne' }), laneOf('l2', 1)],
      steps: ['start', 'task', 'decision', 'parallel', 'inclusive', 'wait', 'end'].map((type, column) =>
        stepOf(`s${String(column)}`, column % 2 === 0 ? 'l1' : 'l2', column, { type: type as 'task', label: `${type} ${'mot '.repeat(column * 4)}`.trim() })),
      flows: [flowOf('f1', 's0', 's1'), flowOf('f2', 's1', 's2'), flowOf('f3', 's2', 's1', { label: 'non, reprendre la demande depuis le début' }), flowOf('f4', 's2', 's3', { label: 'oui' }),
        flowOf('f5', 's3', 's4'), flowOf('f6', 's4', 's5'), flowOf('f7', 's5', 's6'), flowOf('f8', 's4', 's4')],
    });
    const svg = mapSvg(layoutMap(map, 'fr', estimateMeasure), pen({ raci: true }));
    const document = documentOf(svg);
    expect([count(document, 'g.step'), count(document, 'polygon')]).toEqual([7, 3]);
    expect(count(document, 'path.head')).toBe(count(document, 'g.flow'));
    const numbers = [...document.querySelectorAll('*')].flatMap((element) => ['x', 'y', 'width', 'height', 'rx', 'ry', 'cx', 'cy', 'r'].map((name) => element.getAttribute(name)).filter((value) => value !== null));
    expect(numbers.filter((value) => !Number.isFinite(Number(value)))).toEqual([]);
    // The paths too: the flow of a step to itself (f8), which the host never keeps, is left out rather than written broken.
    expect([...document.querySelectorAll('path')].map((path) => path.getAttribute('d') ?? '').filter((d) => /NaN|Infinity/.test(d))).toEqual([]);
    expect(count(document, 'g.flow')).toBe(7);
    expect(texts(document).some((text) => text.endsWith('…'))).toBe(true);
  });

  it('carries the copy of the map in its metadata, read back whole by the opening, and gives the same text for the same layout', () => {
    const layout = layoutMap(sampleMap(), 'fr', estimateMeasure);
    const pmap = JSON.stringify({ format: FORMAT, version: '1.3', project: { code: 'VIS', name: 'Visites « été »' } });
    const svg = mapSvg(layout, pen({ copy: copyMetadata(pmap) }));
    expect(pmapOfSvg(svg)).toBe(pmap);
    expect(mapSvg(layout, pen())).toBe(mapSvg(layoutMap(sampleMap(), 'fr', estimateMeasure), pen()));
    expect(() => pmapOfSvg(mapSvg(layout, pen()))).toThrow('files.svg.no_copy');
  });

  it('matches its reference for the sample map', async () => {
    await expect(mapSvg(layoutMap(sampleMap(), 'fr', estimateMeasure), pen({ raci: true }))).toMatchFileSnapshot('./references/sample-map.svg');
  });
});

describe('the marks of the layer « Analysis » (plan 3.1, Makigami M1, M3, M9)', () => {
  const analysisLook = { handoff: '#c20001', badgeBg: '#c20002', badgeFg: '#c20003', mediaBreak: '#c20004', classes: { value_added: '#c20005', necessary: '#c20006', waste: '#c20007' } };

  it('draws a handoff in its colour and thickness, the class of a step by its band, a break of medium by its dashes, the handoffs of a detail in the place of « Détaillée »', () => {
    const map = sampleMap();
    const steps = map.steps.map((step) => (step.id === 's2' ? { ...step, sub_map_id: 'm2', sub_map_empty: false } : step));
    const layout = layoutMap({ ...map, steps }, 'fr', estimateMeasure);
    const marks = { handoffs: new Map([['f3', 1]]), detailed: { s2: 3 }, classes: { s2: 'waste' as const, s3: 'necessary' as const }, breaks: new Set(['s3']) };
    const document = documentOf(mapSvg(layout, pen({ analysis: { marks, look: analysisLook } })));
    const strokes = [...document.querySelectorAll('g.flow > path:not(.head)')].map((path) => `${path.getAttribute('stroke') ?? ''} ${path.getAttribute('stroke-width') ?? ''}`);
    expect(strokes.filter((stroke) => stroke.startsWith('#c20001'))).toEqual(['#c20001 3']);
    expect(strokes.filter((stroke) => stroke === `${look.edge} 1.6`)).toHaveLength(3);
    expect(count(document, 'path.head[fill="#c20001"]')).toBe(1);
    expect([...document.querySelectorAll('path.value')].map((band) => band.getAttribute('fill'))).toEqual(['#c20007', '#c20006']);
    expect(count(document, 'rect[stroke="#c20004"][stroke-dasharray]')).toBe(1);
    expect(count(document, 'rect[fill="#c20002"]')).toBe(1);
    expect(texts(document)).toContain('3');
    expect(texts(document)).not.toContain(t('map.detailed'));
  });

  it('keeps the frequency of a detailed step under it, where it stands without the layer, when its handoffs take the place of « Détaillée » (audit 3.1 V3, U6)', () => {
    const map = sampleMap();
    const steps = map.steps.map((step) => (step.id === 's2' ? { ...step, sub_map_id: 'm2', sub_map_empty: false, sheet: { ...step.sheet, frequency: 'daily' as const } } : step));
    const layout = layoutMap({ ...map, steps }, 'fr', estimateMeasure);
    const marks = { handoffs: new Map<string, number>(), detailed: { s2: 3 }, classes: {}, breaks: new Set<string>() };
    const marked = documentOf(mapSvg(layout, pen({ analysis: { marks, look: analysisLook } })));
    const frequency = (document: Document) => [...document.querySelectorAll('text')].find((text) => text.textContent === t('map.frequencies.daily'))?.outerHTML;
    expect([texts(marked).includes('3'), texts(marked).includes(t('map.detailed'))]).toEqual([true, false]);
    expect(frequency(marked)).toBeDefined();
    expect(frequency(marked)).toBe(frequency(documentOf(mapSvg(layout, pen()))));
  });

  it('leaves the image as it was without them', () => {
    const layout = layoutMap(sampleMap(), 'fr', estimateMeasure);
    const plain = mapSvg(layout, pen());
    expect(mapSvg(layout, pen({ analysis: null }))).toBe(plain);
    expect(plain).not.toContain('#c2000');
    const none = { handoffs: new Map<string, number>(), detailed: {}, classes: {}, breaks: new Set<string>() };
    expect(mapSvg(layout, pen({ analysis: { marks: none, look: analysisLook } }))).toBe(plain);
  });
});
