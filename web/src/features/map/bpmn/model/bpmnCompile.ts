// bpmnCompile.ts
// A .pmap made a BPMN 2.0 diagram (ADR 106): its semantics, then a drawing for the map shown on the collaboration and one for each sub-process carried; the same model read by the view and written as a .bpmn.

import type { PmapDocument } from '../../../../shared/pmap.ts';
import { diagramOf } from './bpmnLayout.ts';
import { BpmnMapper, type BpmnOptions, type MappedProcess } from './bpmnMapping.ts';
import type { BpmnDefinitions, BpmnDiagram, BpmnNode, BpmnProcess } from './bpmnModel.ts';

/** What the diagram is asked, the details drawn or not and the version of the application that writes it. */
export interface BpmnCompileOptions extends BpmnOptions {
  readonly withDetails: boolean;
  readonly exporterVersion: string;
}

/** The identifier of the drawing of an element: its own identifier, which no other element of the file starts with `DI_`. */
const drawingId = (elementId: string): string => `DI_${elementId}`;

/** The drawings of the sub-processes carried below a process, each on the plane of its sub-process, every level. */
function detailDiagrams(mapper: BpmnMapper, parent: MappedProcess, withDetails: boolean): BpmnDiagram[] {
  return parent.details.flatMap((child) => {
    const owner = parent.process.nodes.find((node) => node.detail === child.process);
    if (owner === undefined) return [];
    const plane = { id: mapper.ids.next('Diagram', owner.stepId), planeId: mapper.ids.next('Plane', owner.stepId), elementId: owner.id, name: owner.name, poolId: null };
    return [diagramOf(child, plane, withDetails, drawingId), ...detailDiagrams(mapper, child, withDetails)];
  });
}

/**
 * A process with only the documents and the notes its drawings show, every level: a modeler lists what the file holds and
 * finds nowhere on the diagram what is not drawn (audit of the copy 64, P2-01); what a step uses or makes stays written in
 * its documentation. The associations to what is left out go with it, and the data objects no reference names.
 */
function drawnOnly(process: BpmnProcess, drawn: ReadonlySet<string>): BpmnProcess {
  const references = process.references.filter((reference) => drawn.has(reference.id));
  const kept = new Set(references.map((reference) => reference.id));
  const named = new Set(references.map((reference) => reference.dataId));
  return {
    ...process,
    dataObjects: process.dataObjects.filter((data) => named.has(data.id)),
    references,
    annotations: process.annotations.filter((note) => drawn.has(note.id)),
    nodes: process.nodes.map((node): BpmnNode => ({
      ...node,
      inputs: node.inputs.filter((link) => kept.has(link.referenceId)),
      outputs: node.outputs.filter((link) => kept.has(link.referenceId)),
      ...(node.detail === undefined ? {} : { detail: drawnOnly(node.detail, drawn) }),
    })),
  };
}

/** The data stores a reference of a process names, every level. */
function storesNamed(process: BpmnProcess, into: Set<string>): Set<string> {
  process.references.forEach((reference) => { if (reference.store) into.add(reference.dataId); });
  process.nodes.forEach((node) => { if (node.detail !== undefined) storesNamed(node.detail, into); });
  return into;
}

/** The BPMN 2.0 diagram of the map of a .pmap: the root map of the document is the map shown. */
export function bpmnOf(document: PmapDocument, options: BpmnCompileOptions): BpmnDefinitions {
  const mapper = new BpmnMapper(document, options);
  const key = document.project.code ?? 'Map';
  const definitionsId = mapper.ids.next('Definitions', key);
  const collaborationId = mapper.ids.next('Collaboration', key);
  const participantId = mapper.ids.next('Participant', key);
  const root = mapper.process(document.map, key);
  const plane = { id: mapper.ids.next('Diagram', key), planeId: mapper.ids.next('Plane', key), elementId: collaborationId, name: options.mapTitle, poolId: participantId };
  const diagrams = [diagramOf(root, plane, options.withDetails, drawingId), ...detailDiagrams(mapper, root, options.withDetails)];
  const drawn = new Set(diagrams.flatMap((diagram) => diagram.shapes.map((shape) => shape.elementId)));
  const process = drawnOnly(root.process, drawn);
  const stores = storesNamed(process, new Set());
  return {
    id: definitionsId, name: options.mapTitle, exporterVersion: options.exporterVersion, resources: mapper.resources,
    dataStores: mapper.dataStores.filter((store) => stores.has(store.id)), collaborationId, participantId, documentation: mapper.projectDocumentation(), process, diagrams,
  };
}
