// bpmnCompact.test.ts
// The rows of a BPMN drawing as low as what they hold (trial of the 3.3): a cut only where nothing is drawn across the width, the clearance kept, its band at most; a row without a detail back to the height of the map, one with a document keeping its band.

import { i18next, type Translate } from '../../../../shared/i18n/i18n.ts';
import { PMAP_FORMAT, type PmapDocument } from '../../../../shared/pmap.ts';
import { CLEARANCE, compacted, cutsOf, liftOf } from './bpmnCompact.ts';
import { bpmnOf } from './bpmnCompile.ts';
import type { BpmnDiagram } from './bpmnModel.ts';

const t: Translate = (key, options) => i18next.t(key, options);

const bottomOf = (diagram: BpmnDiagram | undefined, elementId: string): number => {
  const shape = diagram?.shapes.find((candidate) => candidate.elementId === elementId);
  return shape === undefined ? Number.NaN : shape.bounds.y + shape.bounds.height;
};

/** Two lanes of one task each, the first with a document when asked. */
const twoLanes = (document: boolean): PmapDocument => ({
  format: PMAP_FORMAT, version: '1.5', project: { name: 'Essai', language: 'fr' },
  map: {
    lanes: [{ id: 'L1', name: 'Un' }, { id: 'L2', name: 'Deux' }],
    steps: [
      { id: 'E1', lane: 'L1', label: 'Préparer', column: 0, ...(document ? { sheet: { outputs: [{ id: 'E1-S1', name: 'Bon', medium: 'file' as const }] } } : {}) },
      { id: 'E2', lane: 'L2', label: 'Valider', column: 1 },
    ],
    flows: [{ source: 'E1', target: 'E2' }],
  },
});

const drawing = (pmap: PmapDocument, withDetails: boolean) =>
  bpmnOf(pmap, { language: 'fr', withSubProcesses: false, withDetails, mapTitle: 'Essai', classification: null, summary: null, exporterVersion: '3.3.0', t });

beforeEach(async () => {
  await i18next.changeLanguage('fr');
});

describe('the cuts of the rows', () => {
  it('cut the free spans only, the clearance kept round what is drawn, the band at most', () => {
    const cuts = cutsOf([{ span: { top: 0, bottom: 100 }, most: 50 }], [{ top: 10, bottom: 20 }, { top: 90, bottom: 90 }]);
    expect(cuts).toEqual([{ top: 28, bottom: 78 }]);
    expect(cutsOf([{ span: { top: 0, bottom: 100 }, most: 80 }], [{ top: 10, bottom: 20 }, { top: 90, bottom: 90 }])).toEqual([{ top: 0, bottom: 2 }, { top: 28, bottom: 82 }, { top: 98, bottom: 100 }]);
    expect(cutsOf([{ span: { top: 0, bottom: 100 }, most: 0 }], [])).toEqual([]);
  });

  it('lift what is under a cut by its height, a point within it to its top', () => {
    const lift = liftOf([{ top: 10, bottom: 30 }, { top: 50, bottom: 60 }]);
    expect([5, 10, 20, 30, 55, 70].map(lift)).toEqual([5, 10, 10, 10, 30, 40]);
  });

  it('keep two things drawn twice the clearance apart', () => {
    const [cut] = cutsOf([{ span: { top: 0, bottom: 200 }, most: 200 }], [{ top: 0, bottom: 40 }, { top: 150, bottom: 150 }]);
    const lift = liftOf(cut === undefined ? [] : [cut]);
    expect(lift(150) - lift(40)).toBe(2 * CLEARANCE);
  });

  it('change nothing without a cut', () => {
    const diagram: BpmnDiagram = { id: 'D', planeId: 'P', elementId: 'C', name: 'N', shapes: [], edges: [] };
    expect(compacted(diagram, [])).toBe(diagram);
  });
});

describe('a drawing with the details', () => {
  it('draws a map without a document or a note as low as without the details', () => {
    expect(bottomOf(drawing(twoLanes(false), true).diagrams[0], 'Lane_L2')).toBe(bottomOf(drawing(twoLanes(false), false).diagrams[0], 'Lane_L2'));
  });

  it('keeps the band of a row with a document, and gives it back in a row without', () => {
    const [low] = drawing(twoLanes(false), false).diagrams;
    const [high] = drawing(twoLanes(true), true).diagrams;
    const first = (diagram: BpmnDiagram | undefined) => bottomOf(diagram, 'Lane_L1');
    expect(first(high)).toBeGreaterThan(first(low) + 30);
    expect(bottomOf(high, 'Lane_L2') - first(high)).toBe(bottomOf(low, 'Lane_L2') - first(low));
  });
});
