// bpmnCompile.test.ts
// A .pmap made a BPMN 2.0 diagram (ADR 106): valid against the official schemas of the OMG and read by the meta-model of the modellers without a warning, each step of the kind its rules say, the documentation whole and without a location, the drawing of the map without a form on another, the same file every time.

import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { i18next, loadLanguage, type Translate } from '../../../../shared/i18n/i18n.ts';
import { PMAP_FORMAT, type PmapDocument } from '../../../../shared/pmap.ts';
import { geometryFaults, moddleRead, xsdErrors } from '../../../../test/bpmnChecks.ts';
import { bpmnOf, type BpmnCompileOptions } from './bpmnCompile.ts';
import type { BpmnNode } from './bpmnModel.ts';
import { bpmnXml } from './bpmnXml.ts';

const t: Translate = (key, options) => i18next.t(key, options);

const options = (overrides: Partial<BpmnCompileOptions> = {}): BpmnCompileOptions => ({
  language: 'fr', withSubProcesses: true, withDetails: true, mapTitle: 'Clôture', classification: 'Interne', summary: null, exporterVersion: '3.3.0', t, ...overrides,
});

/** A fictional monthly closing: every type of step, a system lane, a detailed step, documents, pain points open and dropped, comments. */
export const closing: PmapDocument = {
  format: PMAP_FORMAT, version: '1.5',
  project: { code: 'CLO', name: 'Clôture', language: 'fr', glossary: 'balance = trial balance', workday_hours: 7 },
  map: {
    lanes: [{ id: 'CLO-C01', name: 'Comptable' }, { id: 'CLO-C02', name: 'Contrôleur', translations: { en: 'Controller' } }, { id: 'CLO-C03', name: 'ERP', type: 'system' }],
    steps: [
      { id: 'CLO-E001', lane: 'CLO-C01', label: 'Fin du mois', type: 'start', column: 0, volume: { quantity: 1, period: 'month' } },
      {
        id: 'CLO-E002', lane: 'CLO-C01', label: 'Extraire la balance', column: 1, comment: { text: 'Tout part de la balance.' },
        sheet: {
          tools: 'ERP', procedure: [{ text: 'Lancer l’extraction', tool: 'ERP', nature: 'extract' }],
          outputs: [{ id: 'CLO-E002-S1', name: 'Balance', medium: 'spreadsheet', location: '\\\\serveur\\partage\\balance.xlsx' }],
        },
      },
      { id: 'CLO-E003', lane: 'CLO-C02', label: 'Attendre les écritures', type: 'wait', column: 2, sheet: { lead_time: { value: 2, unit: 'day' } } },
      { id: 'CLO-E004', lane: 'CLO-C02', label: 'Écarts acceptables ?', type: 'decision', column: 3, raci: { 'CLO-C01': 'C' } },
      {
        id: 'CLO-E005', lane: 'CLO-C02', label: 'Contrôler', column: 4, raci: { 'CLO-C01': 'A' },
        sheet: { uses: [{ document: 'CLO-E002-S1', pain_point: 'Balance incomplète', pain_point_code: 'CLO-I001', pain_point_status: 'reported', lost_hours_per_month: 3 }] },
      },
      {
        id: 'CLO-E006', lane: 'CLO-C03', label: 'Publier le reporting', column: 5, automation: 'automated',
        sheet: {
          outputs: [{ id: 'CLO-E006-S1', name: 'Reporting', medium: 'software' }],
          uses: [{ document: 'CLO-E002-S1', pain_point: 'Format à reprendre', pain_point_code: 'CLO-I002', pain_point_status: 'dropped' }],
        },
      },
      {
        id: 'CLO-E007', lane: 'CLO-C01', label: 'Corriger', column: 4, row: 0,
        sub_map: {
          lanes: [{ id: 'CLO-C04', name: 'Comptable' }],
          steps: [{ id: 'CLO-E009', lane: 'CLO-C04', label: 'Passer l’écriture', column: 0 }, { id: 'CLO-E010', lane: 'CLO-C04', label: 'Corrigé', type: 'end', column: 1 }],
          flows: [{ source: 'CLO-E009', target: 'CLO-E010' }],
        },
      },
      { id: 'CLO-E008', lane: 'CLO-C02', label: 'Clôture faite', type: 'end', column: 6 },
    ],
    flows: [
      { source: 'CLO-E001', target: 'CLO-E002' }, { source: 'CLO-E002', target: 'CLO-E003' }, { source: 'CLO-E003', target: 'CLO-E004' },
      { source: 'CLO-E004', target: 'CLO-E005', label: 'oui', share: 80, translations: { en: 'yes' } },
      { source: 'CLO-E004', target: 'CLO-E007', label: 'non', share: 20, comment: { text: 'On reprend.' } },
      { source: 'CLO-E007', target: 'CLO-E004' }, { source: 'CLO-E005', target: 'CLO-E006' }, { source: 'CLO-E006', target: 'CLO-E008' },
    ],
  },
};

const nodeOf = (definitions: ReturnType<typeof bpmnOf>, stepId: string): BpmnNode => {
  const node = definitions.process.nodes.find((candidate) => candidate.stepId === stepId);
  if (node === undefined) throw new Error(`no node for ${stepId}`);
  return node;
};

beforeAll(async () => {
  await loadLanguage('en');
});

beforeEach(async () => {
  await i18next.changeLanguage('fr');
});

describe('the BPMN 2.0 diagram of a map', () => {
  it('is valid against the five official schemas of the OMG, with and without its details and its sub-processes', async () => {
    for (const asked of [options(), options({ withDetails: false }), options({ withSubProcesses: false }), options({ withDetails: false, withSubProcesses: false })])
      expect(await xsdErrors(bpmnXml(bpmnOf(closing, asked)))).toEqual([]);
  });

  it('is read by the meta-model of the modellers without a warning, a drawing for the map and one for its sub-process', async () => {
    const read = await moddleRead(bpmnXml(bpmnOf(closing, options())));
    expect(read.warnings.map((warning) => warning.message)).toEqual([]);
    const types = Object.values(read.elementsById).map((element) => element.$type);
    expect(types.filter((type) => type === 'bpmndi:BPMNDiagram')).toHaveLength(2);
    expect(types.filter((type) => type === 'bpmn:Lane')).toHaveLength(4);
  });

  it('gives each step the kind its type, its automation, its lane, its tools and its detail say', () => {
    const definitions = bpmnOf(closing, options());
    const kinds = Object.fromEntries(definitions.process.nodes.map((node) => [node.stepId, node.kind]));
    expect(kinds).toEqual({
      'CLO-E001': 'startEvent', 'CLO-E002': 'userTask', 'CLO-E003': 'intermediateCatchEvent', 'CLO-E004': 'exclusiveGateway',
      'CLO-E005': 'manualTask', 'CLO-E006': 'serviceTask', 'CLO-E007': 'subProcess', 'CLO-E008': 'endEvent',
    });
    expect(nodeOf(definitions, 'CLO-E003').timer?.duration).toBe('P2D');
    expect(nodeOf(definitions, 'CLO-E004').gatewayDirection).toBe('Mixed');
    expect(nodeOf(definitions, 'CLO-E006').performerId).toBeUndefined();
    expect(nodeOf(definitions, 'CLO-E005').raciRoles.map((role) => role.name)).toEqual(['Approuve (A)']);
    expect(nodeOf(definitions, 'CLO-E007').detail?.nodes.map((node) => node.kind)).toEqual(['manualTask', 'endEvent']);
    expect(bpmnOf(closing, options({ withSubProcesses: false })).process.nodes.find((node) => node.stepId === 'CLO-E007')?.detail).toBeUndefined();
  });

  it('names the flows with their share, gives a condition to the exits of a decision only, and the comment of a flow its documentation', () => {
    const flows = bpmnOf(closing, options()).process.flows;
    expect(flows.map((flow) => [flow.name, flow.condition])).toEqual([
      [null, null], [null, null], [null, null], ['oui · 80 %', 'oui · 80 %'], ['non · 20 %', 'non · 20 %'], [null, null], [null, null], [null, null],
    ]);
    expect(flows[4]?.documentation).toBe('Commentaire de relais : On reprend.');
  });

  it('draws the documents and a note of the open pain points only, a store for a software, never a location', () => {
    const definitions = bpmnOf(closing, options());
    expect(definitions.dataStores.map((store) => store.name)).toEqual(['Reporting']);
    expect(definitions.process.annotations.map((note) => [note.nodeId, note.text])).toEqual([[nodeOf(definitions, 'CLO-E002').id, '[CLO-I001] Balance incomplète (3 h perdues par mois)']]);
    const xml = bpmnXml(definitions);
    expect(xml).not.toContain('serveur');
    expect(xml).toContain('Format à reprendre');
    expect(xml).toContain('Glossaire du projet : \nbalance = trial balance');
  });

  it('writes the texts in the language shown, an untranslated one as its original said untranslated', async () => {
    await i18next.changeLanguage('en');
    const definitions = bpmnOf(closing, options({ language: 'en' }));
    expect(definitions.process.lanes.map((lane) => lane.name)).toEqual(['Comptable', 'Controller', 'ERP']);
    expect(nodeOf(definitions, 'CLO-E002').translated).toBe(false);
    expect(definitions.process.flows[3]?.name).toBe('yes · 80 %');
    expect(nodeOf(definitions, 'CLO-E005').documentation).toContain('Responsible: Controller ; Accountable: Comptable');
  });

  it('lays the map out as the map, no form on another, each in its lane, each flow ending on its forms', () => {
    for (const asked of [options(), options({ withDetails: false })]) {
      const definitions = bpmnOf(closing, asked);
      expect(definitions.diagrams.flatMap((diagram) => geometryFaults(definitions, diagram))).toEqual([]);
    }
  });

  it('writes the same file for the same map', () => {
    expect(bpmnXml(bpmnOf(closing, options()))).toBe(bpmnXml(bpmnOf(closing, options())));
  });

  it('opens the copy of the sample of the repository', async () => {
    const sample = JSON.parse(readFileSync(join(import.meta.dirname, '..', '..', '..', '..', '..', '..', 'samples', 'cloture-mensuelle.pmap'), 'utf8')) as PmapDocument;
    const definitions = bpmnOf(sample, options({ mapTitle: sample.project.name }));
    const xml = bpmnXml(definitions);
    expect(await xsdErrors(xml)).toEqual([]);
    expect((await moddleRead(xml)).warnings).toEqual([]);
    expect(definitions.diagrams.flatMap((diagram) => geometryFaults(definitions, diagram))).toEqual([]);
  });
});

describe('what the .bpmn holds for the modellers (audit of the copy 64, P2-01)', () => {
  it('holds only the documents and notes it draws, its project on the pool too, its expressions typed as the modelers read them', async () => {
    // Audit of the copy 64, P2-01: what a modeler lists is drawn; a click on the pool shows the project.
    for (const asked of [options(), options({ withDetails: false })]) {
      const definitions = bpmnOf(closing, asked);
      const drawn = new Set(definitions.diagrams.flatMap((diagram) => diagram.shapes.map((shape) => shape.elementId)));
      const walk = (process: typeof definitions.process): string[] => [
        ...process.references.map((reference) => reference.id), ...process.annotations.map((note) => note.id),
        ...process.nodes.flatMap((node) => (node.detail === undefined ? [] : walk(node.detail))),
      ];
      expect(walk(definitions.process).filter((id) => !drawn.has(id))).toEqual([]);
    }
    const bare = bpmnXml(bpmnOf(closing, options({ withDetails: false })));
    expect(['bpmn:dataObject', 'bpmn:dataStore', 'bpmn:textAnnotation', 'bpmn:association', 'bpmn:dataInputAssociation', 'bpmn:dataOutputAssociation'].filter((tag) => bare.includes(`<${tag} `)))
      .toEqual([]);
    expect(bare).toContain('Format à reprendre');
    const whole = bpmnXml(bpmnOf(closing, options()));
    expect(/<bpmn:participant [^>]*>\s*<bpmn:documentation>[^<]*Classification : Interne/.test(whole)).toBe(true);
    const read = await moddleRead(whole);
    expect(read.warnings.map((warning) => warning.message)).toEqual([]);
    const conditions = Object.values(read.elementsById).filter((element) => element.$type === 'bpmn:SequenceFlow')
      .map((flow) => (flow as { conditionExpression?: { $type: string } }).conditionExpression?.$type).filter((type) => type !== undefined);
    expect(conditions).toEqual(['bpmn:FormalExpression', 'bpmn:FormalExpression']);
  });
});
