// bpmnMapping.ts
// The semantics of a map in BPMN 2.0 (ADR 106), from its .pmap: a lane per lane, a node per step of the kind its type, its automation, its lane and its tools say, a sequence flow per flow, the RACI as raciRoles, the documents as data, the open pain points as notes; the detail of a step as the content of its sub-process.

import type { Translate } from '../../../../shared/i18n/i18n.ts';
import type { PmapDocument, PmapFlow, PmapLane, PmapMap, PmapStep, PmapUse } from '../../../../shared/pmap.ts';
import { projectDocumentation, stepDocumentation, type StepPainPoint } from './bpmnDocumentation.ts';
import { BpmnIds } from './bpmnIds.ts';
import type { BpmnAnnotation, BpmnData, BpmnDataLink, BpmnDataReference, BpmnFlow, BpmnLane, BpmnNode, BpmnNodeKind, BpmnProcess, GatewayDirection } from './bpmnModel.ts';
import { flowName, isoDuration, shown } from './bpmnText.ts';

/** What the diagram is asked: the language shown, the sub-processes carried or not, the context the .pmap does not say. */
export interface BpmnOptions {
  readonly language: string;
  readonly withSubProcesses: boolean;
  readonly mapTitle: string;
  readonly classification: string | null;
  readonly summary: string | null;
  readonly t: Translate;
}

/** A process with what its drawing needs: its map, and the identifier given to each lane, step and flow. */
export interface MappedProcess {
  readonly process: BpmnProcess;
  readonly map: PmapMap;
  readonly laneIds: ReadonlyMap<string, string>;
  readonly nodeIds: ReadonlyMap<string, string>;
  readonly flowIds: readonly string[];
  /** The maps of its sub-processes carried, each drawn on its own plane. */
  readonly details: readonly MappedProcess[];
}

/** The statuses of a pain point dealt with: the map no longer shows it (`MapMarkers.IsOpen`, `PmapUse.DealtWith`). */
const DEALT_WITH = new Set(['implemented', 'adopted', 'dropped']);

const ACTIVITIES = new Set<BpmnNodeKind>(['manualTask', 'userTask', 'serviceTask', 'subProcess']);

export const isActivity = (kind: BpmnNodeKind): boolean => ACTIVITIES.has(kind);

/** The direction of a gateway by its flows: one in and several out diverges, several in and one out converges. */
function directionOf(incoming: number, outgoing: number): GatewayDirection {
  if (incoming <= 1 && outgoing > 1) return 'Diverging';
  if (incoming > 1 && outgoing <= 1) return 'Converging';
  return incoming > 1 && outgoing > 1 ? 'Mixed' : 'Unspecified';
}

/** The kind of a step: its type, then for a task its detail, its automation, its lane and its tools (spec of the lot, 4.1). */
export function kindOf(step: PmapStep, lane: PmapLane | undefined, incoming: number): BpmnNodeKind {
  switch (step.type ?? 'task') {
    case 'start': return 'startEvent';
    case 'end': return 'endEvent';
    case 'decision': return 'exclusiveGateway';
    case 'parallel': return 'parallelGateway';
    case 'inclusive': return 'inclusiveGateway';
    case 'wait': return step.sub_map !== undefined ? 'subProcess' : incoming > 0 ? 'intermediateCatchEvent' : 'startEvent';
    default: break;
  }
  if (step.sub_map !== undefined) return 'subProcess';
  if (step.automation === 'automated' || lane?.type === 'system') return 'serviceTask';
  const tools = (step.sheet?.tools ?? '').trim() !== '' || (step.sheet?.procedure ?? []).some((task) => (task.tool ?? '').trim() !== '');
  return tools ? 'userTask' : 'manualTask';
}

/** The builder of the semantics of one file: its identifiers, its resources and its stores are shared by every map of it. */
export class BpmnMapper {
  readonly ids = new BpmnIds();
  readonly resources: { id: string; name: string }[] = [];
  readonly dataStores: BpmnData[] = [];
  private readonly resourceOf = new Map<string, string>();
  private readonly origin: string;

  constructor(private readonly document: PmapDocument, private readonly options: BpmnOptions) {
    this.origin = document.project.language ?? options.language;
  }

  /** The documentation of the collaboration: the context of the project. */
  projectDocumentation(): string {
    return projectDocumentation(this.document, { mapTitle: this.options.mapTitle, classification: this.options.classification, language: this.options.language, summary: this.options.summary }, this.options.t);
  }

  private text(original: string, translations: PmapStep['translations']) {
    return shown(original, translations, this.origin, this.options.language);
  }

  /** The process of a map: its lanes, its nodes, its flows, its documents and its notes, and those of its details carried. */
  process(map: PmapMap, key: string): MappedProcess {
    const laneIds = new Map<string, string>();
    const lanes: BpmnLane[] = map.lanes.map((lane) => this.lane(lane, laneIds));
    const steps = map.steps ?? [];
    const flows = map.flows ?? [];
    const incoming = (id: string) => flows.filter((flow) => flow.target === id).length;
    const nodeIds = new Map(steps.map((step) => [step.id, this.ids.next('Step', step.id)]));
    const flowIds = flows.map((flow) => this.ids.next('Flow', `${flow.source}_to_${flow.target}`));
    const data = new DataBuilder(this, steps);
    const details: MappedProcess[] = [];
    const nodes = steps.map((step) => {
      const lane = map.lanes.find((candidate) => candidate.id === step.lane);
      const node = { ...this.node(step, lane, nodeIds, flows, flowIds, data, incoming(step.id)), laneId: laneIds.get(step.lane) ?? '' };
      const child = node.kind === 'subProcess' && this.options.withSubProcesses && step.sub_map !== undefined ? this.process(step.sub_map, step.id) : null;
      if (child !== null) details.push(child);
      lanes.find((candidate) => candidate.id === laneIds.get(step.lane))?.nodeIds.push(node.id);
      return child === null ? node : { ...node, detail: child.process };
    });
    const sequence = flows.map((flow, index) => this.flow(flow, flowIds[index] ?? '', nodeIds, steps));
    const process: BpmnProcess = {
      id: this.ids.next('Process', key), laneSetId: this.ids.next('LaneSet', key), lanes, nodes, flows: sequence,
      dataObjects: data.objects, references: data.references, annotations: this.annotations(steps, nodeIds, data),
    };
    return { process, map, laneIds, nodeIds, flowIds, details };
  }

  private lane(lane: PmapLane, laneIds: Map<string, string>): BpmnLane {
    const id = this.ids.next('Lane', lane.id);
    laneIds.set(lane.id, id);
    const name = this.text(lane.name, lane.translations).text;
    if (!this.resourceOf.has(lane.id)) {
      const resource = this.ids.next('Resource', lane.id);
      this.resourceOf.set(lane.id, resource);
      this.resources.push({ id: resource, name });
    }
    const t = this.options.t;
    const documentation = [lane.type === 'system' ? t('bpmn.doc.lane_system') : null, lane.headcount === undefined ? null : t('bpmn.doc.headcount', { number: lane.headcount })]
      .filter((part): part is string => part !== null).join('\n');
    return { id, laneKey: lane.id, name, system: lane.type === 'system', documentation, nodeIds: [] };
  }

  /** A node of a step: its kind, its names, its links, its raciRoles, its timer and its documentation. */
  private node(step: PmapStep, lane: PmapLane | undefined, nodeIds: ReadonlyMap<string, string>, flows: readonly PmapFlow[], flowIds: readonly string[],
    data: DataBuilder, incoming: number): BpmnNode {
    const id = nodeIds.get(step.id) ?? '';
    const kind = kindOf(step, lane, incoming);
    const name = this.text(step.label, step.translations);
    const inbound = flows.flatMap((flow, index) => (flow.target === step.id ? [flowIds[index] ?? ''] : []));
    const outbound = flows.flatMap((flow, index) => (flow.source === step.id ? [flowIds[index] ?? ''] : []));
    const t = this.options.t;
    const raciRoles = Object.entries(step.raci ?? {}).filter(([, letter]) => letter !== 'R')
      .flatMap(([laneKey, letter]) => { const resourceId = this.resourceOf.get(laneKey); return resourceId === undefined ? [] : [{ name: `${t(`raci.names.${letter}`)} (${letter})`, resourceId }]; });
    const links = isActivity(kind) ? data.linksOf(step, id) : { inputs: [], outputs: [] };
    const comment = step.comment === undefined ? null : this.text(step.comment.text ?? '', step.comment.translations).text;
    const documentation = stepDocumentation(step, {
      laneName: (laneKey) => this.laneName(laneKey), label: name.text, comment: comment === '' ? null : comment, documentName: (document) => data.nameOf(document),
      producerCode: (document) => data.producerOf(document), painPoints: data.painPointsOn(step.id),
    }, t);
    const timed = kind === 'intermediateCatchEvent' || (kind === 'startEvent' && step.type === 'wait');
    return {
      id, stepId: step.id, kind, name: name.text, translated: name.translated, laneId: '', documentation, incoming: inbound, outgoing: outbound,
      ...(timed ? { timer: { id: this.ids.next('Timer', step.id), duration: isoDuration(step.sheet) } } : {}),
      ...(kind.endsWith('Gateway') ? { gatewayDirection: directionOf(inbound.length, outbound.length) } : {}),
      ...(lane !== undefined && lane.type !== 'system' && isActivity(kind) ? { performerId: this.resourceOf.get(lane.id) ?? '' } : {}),
      raciRoles: isActivity(kind) ? raciRoles : [], inputs: links.inputs, outputs: links.outputs, detailed: step.sub_map !== undefined,
    };
  }

  private laneName(laneKey: string): string {
    return this.resources.find((resource) => resource.id === this.resourceOf.get(laneKey))?.name ?? laneKey;
  }

  /** A sequence flow: its label and share as its name, a condition out of a decision or an inclusive gateway. */
  private flow(flow: PmapFlow, id: string, nodeIds: ReadonlyMap<string, string>, steps: readonly PmapStep[]): BpmnFlow {
    const source = steps.find((step) => step.id === flow.source);
    const name = flowName(flow.label === undefined ? '' : this.text(flow.label, flow.translations).text, flow.share);
    const branching = source?.type === 'decision' || source?.type === 'inclusive';
    const comment = flow.comment === undefined ? '' : this.text(flow.comment.text ?? '', flow.comment.translations).text;
    const documentation = comment === '' ? '' : this.options.t('bpmn.doc.field', { label: this.options.t('translations.types.flow_presentation_comment'), value: comment });
    return { id, sourceId: nodeIds.get(flow.source) ?? '', targetId: nodeIds.get(flow.target) ?? '', name, condition: branching ? name : null, documentation };
  }

  /** A note per step that bears open pain points: their codes, texts and hours, linked to the step. */
  private annotations(steps: readonly PmapStep[], nodeIds: ReadonlyMap<string, string>, data: DataBuilder): BpmnAnnotation[] {
    return steps.flatMap((step) => {
      const open = data.painPointsOn(step.id).filter((point) => point.open);
      if (open.length === 0) return [];
      const t = this.options.t;
      const text = open.map((point) => {
        const code = point.use.pain_point_code === undefined ? '' : `[${point.use.pain_point_code}] `;
        const hours = point.use.lost_hours_per_month === undefined ? '' : ` (${t('sheet.pain_point.lost_short', { hours: point.use.lost_hours_per_month })})`;
        return `${code}${point.use.pain_point ?? ''}${hours}`;
      }).join('\n');
      return [{ id: this.ids.next('Annotation', step.id), associationId: this.ids.next('Association', step.id), nodeId: nodeIds.get(step.id) ?? '', text }];
    });
  }

  /** A store for a software, declared once at the root of the file. */
  store(key: string, name: string): BpmnData {
    const store = { id: this.ids.next('DataStore', key), name, store: true };
    this.dataStores.push(store);
    return store;
  }
}

/**
 * The documents of one map: an object (or a store) per output and per external input, a reference near each step that
 * produces, receives or uses one, and the pain points each use raises on the step that makes its document.
 */
class DataBuilder {
  readonly objects: BpmnData[] = [];
  readonly references: BpmnDataReference[] = [];
  private readonly outputData = new Map<string, BpmnData>();
  private readonly outputName = new Map<string, string>();
  private readonly outputSoftware = new Map<string, boolean>();
  private readonly producer = new Map<string, string>();
  private readonly raised = new Map<string, StepPainPoint[]>();

  constructor(private readonly mapper: BpmnMapper, steps: readonly PmapStep[]) {
    for (const step of steps)
      (step.sheet?.outputs ?? []).forEach((output, index) => {
        const key = output.id ?? `${step.id}_out_${String(index + 1)}`;
        this.outputName.set(key, output.name);
        this.outputSoftware.set(key, output.medium === 'software');
        this.producer.set(key, step.id);
      });
    for (const step of steps)
      for (const use of step.sheet?.uses ?? []) this.raise(use);
  }

  private raise(use: PmapUse): void {
    const producer = this.producer.get(use.document);
    if (producer === undefined || use.pain_point === undefined) return;
    const points = this.raised.get(producer) ?? [];
    points.push({ use, document: this.nameOf(use.document), open: !DEALT_WITH.has(use.pain_point_status ?? 'reported') });
    this.raised.set(producer, points);
  }

  nameOf = (document: string): string => this.outputName.get(document) ?? document;

  producerOf = (document: string): string | null => this.producer.get(document) ?? null;

  painPointsOn = (stepId: string): readonly StepPainPoint[] => this.raised.get(stepId) ?? [];

  private data(key: string, name: string, software: boolean): BpmnData {
    if (software) return this.mapper.store(key, name);
    const object = { id: this.mapper.ids.next('DataObject', key), name, store: false };
    this.objects.push(object);
    return object;
  }

  private reference(data: BpmnData, nodeId: string, key: string): BpmnDataReference {
    const reference = { id: this.mapper.ids.next('DataRef', key), dataId: data.id, store: data.store, name: data.name, nodeId };
    this.references.push(reference);
    return reference;
  }

  /** The documents an activity writes and reads: its outputs, its external inputs, the outputs of other steps it uses. */
  linksOf(step: PmapStep, nodeId: string): { inputs: BpmnDataLink[]; outputs: BpmnDataLink[] } {
    const sheet = step.sheet ?? {};
    const outputs = (sheet.outputs ?? []).map((output, index) => {
      const key = output.id ?? `${step.id}_out_${String(index + 1)}`;
      return { id: this.mapper.ids.next('DataOutput', key), referenceId: this.reference(this.outputOf(key), nodeId, key).id };
    });
    const external = (sheet.inputs ?? []).map((input, index) => {
      const key = `${step.id}_in_${String(index + 1)}`;
      return { id: this.mapper.ids.next('DataInput', key), referenceId: this.reference(this.data(key, input.name, input.medium === 'software'), nodeId, key).id };
    });
    const uses = (sheet.uses ?? []).map((use) => {
      const key = `${step.id}_uses_${use.document}`;
      return { id: this.mapper.ids.next('DataInput', key), referenceId: this.reference(this.outputOf(use.document), nodeId, key).id };
    });
    return { inputs: [...external, ...uses], outputs };
  }

  /** The object (or store) of an output, made once whoever reads it first, its producer or a step that uses it. */
  private outputOf(key: string): BpmnData {
    const known = this.outputData.get(key);
    if (known !== undefined) return known;
    const data = this.data(key, this.nameOf(key), this.outputSoftware.get(key) ?? false);
    this.outputData.set(key, data);
    return data;
  }
}
