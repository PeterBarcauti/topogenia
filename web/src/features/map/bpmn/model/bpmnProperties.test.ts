// bpmnProperties.test.ts
// The BPMN 2.0 diagram on 300 maps drawn at random (ADR 106, as the routing on its 500): no form on another, each in its lane, each flow ending on its forms, with and without the details; a sample of them valid for the official schemas; texts a person may type escaped so that a modeller reads them as written.

import { i18next, type Translate } from '../../../../shared/i18n/i18n.ts';
import { PMAP_FORMAT, type PmapDocument, type PmapStep } from '../../../../shared/pmap.ts';
import { geometryFaults, moddleRead, xsdErrors } from '../../../../test/bpmnChecks.ts';
import { randomMap, seeded } from '../../../../test/randomMaps.ts';
import { bpmnOf, type BpmnCompileOptions } from './bpmnCompile.ts';
import { bpmnXml } from './bpmnXml.ts';

const t: Translate = (key, options) => i18next.t(key, options);

const options = (withDetails: boolean): BpmnCompileOptions => ({
  language: 'fr', withSubProcesses: true, withDetails, mapTitle: 'Carte', classification: null, summary: null, exporterVersion: '3.3.0', t,
});

/** A map drawn at random made a .pmap: its lanes, its steps on their cells, its flows; a document and a pain point on one task in three. */
export function randomPmap(seed: number): PmapDocument {
  const map = randomMap(seed);
  const random = seeded(seed + 7);
  const tasks = map.steps.filter((step) => step.type === 'task');
  const steps: PmapStep[] = map.steps.map((step) => ({ id: step.id, lane: step.laneId, label: `Étape ${step.id}`, type: step.type, column: step.column, row: step.row }));
  tasks.forEach((task, index) => {
    const next = tasks[index + 1];
    if (index % 3 !== 0 || next === undefined) return;
    const producer = steps.findIndex((step) => step.id === task.id);
    const user = steps.findIndex((step) => step.id === next.id);
    const document = `${task.id}-S1`;
    steps[producer] = { ...steps[producer] as PmapStep, sheet: { outputs: [{ id: document, name: `Document ${task.id}`, medium: random() < 0.3 ? 'software' : 'file' }] } };
    steps[user] = { ...steps[user] as PmapStep, sheet: { uses: [{ document, pain_point: 'Incomplet', lost_hours_per_month: 1 }] } };
  });
  return {
    format: PMAP_FORMAT, version: '1.5', project: { name: 'Hasard', language: 'fr' },
    map: {
      lanes: [...map.lanes].sort((a, b) => a.order - b.order).map((lane) => ({ id: lane.id, name: `Couloir ${lane.id}` })),
      steps,
      flows: map.flows.map((flow) => ({ source: flow.sourceId, target: flow.targetId, ...(flow.label === null ? {} : { label: 'oui' }) })),
    },
  };
}

const SEEDS = Array.from({ length: 300 }, (_, index) => index + 1);

beforeEach(async () => {
  await i18next.changeLanguage('fr');
});

describe('the BPMN 2.0 diagram on 300 maps drawn at random', () => {
  it('puts no form on another, each in its lane, each flow ending on its forms, with the details', () => {
    const faults = SEEDS.flatMap((seed) => {
      const definitions = bpmnOf(randomPmap(seed), options(true));
      return definitions.diagrams.flatMap((diagram) => geometryFaults(definitions, diagram).map((fault) => `seed ${String(seed)}: ${fault}`));
    });
    expect(faults).toEqual([]);
  });

  it('does as much without the details', () => {
    const faults = SEEDS.flatMap((seed) => {
      const definitions = bpmnOf(randomPmap(seed), options(false));
      return definitions.diagrams.flatMap((diagram) => geometryFaults(definitions, diagram).map((fault) => `seed ${String(seed)}: ${fault}`));
    });
    expect(faults).toEqual([]);
  });

  it('gives back the band of the details in the rows that hold none: the drawings a third higher at most than without', () => {
    const height = (seed: number, withDetails: boolean) => {
      const diagram = bpmnOf(randomPmap(seed), { ...options(withDetails), withSubProcesses: false }).diagrams[0];
      return Math.max(...(diagram?.shapes ?? []).map((shape) => shape.bounds.y + shape.bounds.height));
    };
    const [high, low] = [SEEDS.reduce((sum, seed) => sum + height(seed, true), 0), SEEDS.reduce((sum, seed) => sum + height(seed, false), 0)];
    expect(high / low).toBeLessThan(4 / 3);
  });

  it('writes files valid for the official schemas and read without a warning, on one map in ten', async () => {
    for (const seed of SEEDS.filter((seed) => seed % 10 === 0)) {
      const xml = bpmnXml(bpmnOf(randomPmap(seed), options(true)));
      expect(await xsdErrors(xml), `seed ${String(seed)}`).toEqual([]);
      expect((await moddleRead(xml)).warnings, `seed ${String(seed)}`).toEqual([]);
    }
    // About 3 s alone, beyond 15 s when the whole suite runs on a loaded computer: thirty validations against the schemas.
  }, 60_000);
});

describe('the texts of a .bpmn', () => {
  const hostile = 'A & B <script>"x"</script> \'y\' ]]> \u0007 fin\nligne 2 測試 Тест Δ';
  const document: PmapDocument = {
    format: PMAP_FORMAT, version: '1.5', project: { name: hostile, language: 'fr', glossary: hostile },
    map: {
      lanes: [{ id: 'c 1', name: hostile }],
      steps: [{ id: 'é 1', lane: 'c 1', label: hostile, column: 0, comment: { text: hostile } }, { id: '1é', lane: 'c 1', label: 'Fin', type: 'end', column: 1 }],
      flows: [{ source: 'é 1', target: '1é', label: hostile }],
    },
  };

  it('are escaped for XML 1.0 and read back as written, the characters XML forbids left out', async () => {
    const xml = bpmnXml(bpmnOf(document, options(true)));
    expect(await xsdErrors(xml)).toEqual([]);
    const read = await moddleRead(xml);
    expect(read.warnings).toEqual([]);
    const written = hostile.replace('\u0007', '');
    const names = Object.values(read.elementsById).map((element) => element.name);
    expect(names).toContain(written);
    expect(names.filter((name) => name === written).length).toBeGreaterThanOrEqual(3);
  });

  it('give identifiers that are names of XML, unique in the file, from any identifier of the .pmap', async () => {
    const read = await moddleRead(bpmnXml(bpmnOf(document, options(true))));
    const ids = Object.keys(read.elementsById);
    expect(ids.every((id) => /^[A-Za-z_][A-Za-z0-9_.-]*$/.test(id))).toBe(true);
    expect(new Set(ids).size).toBe(ids.length);
  });
});
