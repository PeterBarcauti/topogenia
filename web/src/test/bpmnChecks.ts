// bpmnChecks.ts
// The checks of a .bpmn for the tests: its validity against the five official schemas of the OMG (BPMN 2.0.2, kept beside the tests, never served), its reading by bpmn-moddle, and the geometry of its drawings.

import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { BpmnModdle, type ModdleResult } from 'bpmn-moddle';
import { validateXML } from 'xmllint-wasm';
import type { BpmnDefinitions, BpmnDiagram } from '../features/map/bpmn/model/bpmnModel.ts';
import type { Rect } from '../features/map/model/geometry/grid.ts';

const XSD_FOLDER = join(import.meta.dirname, 'oracle', 'bpmn-2.0.2');
const xsd = (fileName: string) => ({ fileName, contents: readFileSync(join(XSD_FOLDER, fileName), 'utf8') });

/** The faults of a text against the official schemas: none for a valid file. */
export async function xsdErrors(xml: string): Promise<string[]> {
  const result = await validateXML({
    xml: { fileName: 'diagram.bpmn', contents: xml },
    schema: xsd('BPMN20.xsd'),
    preload: [xsd('Semantic.xsd'), xsd('BPMNDI.xsd'), xsd('DI.xsd'), xsd('DC.xsd')],
  });
  return result.valid ? [] : result.errors.map((error) => error.rawMessage);
}

/** The file read by the meta-model of the modellers. */
export const moddleRead = (xml: string): Promise<ModdleResult> => BpmnModdle().fromXML(xml);

const inside = (inner: Rect, outer: Rect, slack = 0.5): boolean =>
  inner.x >= outer.x - slack && inner.y >= outer.y - slack && inner.x + inner.width <= outer.x + outer.width + slack && inner.y + inner.height <= outer.y + outer.height + slack;

const overlapping = (a: Rect, b: Rect): boolean => a.x < b.x + b.width - 0.5 && b.x < a.x + a.width - 0.5 && a.y < b.y + b.height - 0.5 && b.y < a.y + a.height - 0.5;

/** A point on the border of a rectangle, at one pixel. */
function onBorder(point: { x: number; y: number }, rect: Rect): boolean {
  const within = (value: number, low: number, high: number) => value >= low - 1 && value <= high + 1;
  const sideX = Math.abs(point.x - rect.x) <= 1 || Math.abs(point.x - rect.x - rect.width) <= 1;
  const sideY = Math.abs(point.y - rect.y) <= 1 || Math.abs(point.y - rect.y - rect.height) <= 1;
  return (sideX && within(point.y, rect.y, rect.y + rect.height)) || (sideY && within(point.x, rect.x, rect.x + rect.width));
}

/** The faults of the geometry of one drawing, written for a reader: a number not finite, a form out of its lane, two forms on each other, a flow whose end is off its form. */
export function geometryFaults(definitions: BpmnDefinitions, diagram: BpmnDiagram): string[] {
  const faults: string[] = [];
  const lanes = new Set(collectLanes(definitions));
  const frames = diagram.shapes.filter((shape) => lanes.has(shape.elementId) || shape.elementId === definitions.participantId);
  const forms = diagram.shapes.filter((shape) => !frames.includes(shape));
  for (const shape of diagram.shapes)
    if (![shape.bounds.x, shape.bounds.y, shape.bounds.width, shape.bounds.height].every(Number.isFinite)) faults.push(`${shape.elementId}: not finite`);
  const laneFrames = frames.filter((frame) => lanes.has(frame.elementId));
  for (const form of forms)
    if (!laneFrames.some((lane) => inside(form.bounds, lane.bounds))) faults.push(`${form.elementId}: out of its lane`);
  forms.forEach((form, index) => { forms.slice(index + 1).forEach((other) => {
    if (overlapping(form.bounds, other.bounds)) faults.push(`${form.elementId} on ${other.elementId}`);
  }); });
  const byElement = new Map(forms.map((form) => [form.elementId, form.bounds]));
  for (const flow of collectFlows(definitions)) {
    const edge = diagram.edges.find((candidate) => candidate.elementId === flow.id);
    const [source, target] = [byElement.get(flow.sourceId), byElement.get(flow.targetId)];
    if (edge === undefined || source === undefined || target === undefined) continue;
    const [first, last] = [edge.waypoints[0], edge.waypoints.at(-1)];
    if (first === undefined || last === undefined || !onBorder(first, source) || !onBorder(last, target)) faults.push(`${flow.id}: an end off its form`);
    if (!edge.waypoints.every((point) => Number.isFinite(point.x) && Number.isFinite(point.y))) faults.push(`${flow.id}: not finite`);
  }
  return faults;
}

function collectLanes(definitions: BpmnDefinitions): string[] {
  const ids: string[] = [];
  const walk = (process: BpmnDefinitions['process']) => {
    process.lanes.forEach((lane) => ids.push(lane.id));
    process.nodes.forEach((node) => { if (node.detail !== undefined) walk(node.detail); });
  };
  walk(definitions.process);
  return ids;
}

function collectFlows(definitions: BpmnDefinitions): BpmnDefinitions['process']['flows'] {
  const flows: BpmnDefinitions['process']['flows'][number][] = [];
  const walk = (process: BpmnDefinitions['process']) => {
    flows.push(...process.flows);
    process.nodes.forEach((node) => { if (node.detail !== undefined) walk(node.detail); });
  };
  walk(definitions.process);
  return flows;
}
