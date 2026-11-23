// bpmnXml.ts
// The text of a .bpmn (OMG BPMN 2.0.2, ISO/IEC 19510), written by the code in the order of the sequences of the official schemas, every text escaped for XML 1.0 and every character XML forbids left out (ADR 106).

import { PRODUCT_NAME } from '../../../../shared/i18n/i18n.ts';
import { PMAP_FORMAT } from '../../../../shared/pmap.ts';
import type { BpmnAnnotation, BpmnDefinitions, BpmnDiagram, BpmnFlow, BpmnLane, BpmnNode, BpmnProcess } from './bpmnModel.ts';
import { isActivity } from './bpmnMapping.ts';

const NAMESPACES = [
  'xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL"',
  'xmlns:bpmndi="http://www.omg.org/spec/BPMN/20100524/DI"',
  'xmlns:dc="http://www.omg.org/spec/DD/20100524/DC"',
  'xmlns:di="http://www.omg.org/spec/DD/20100524/DI"',
  'xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"',
].join(' ');

/** The type the modelers read an expression by (a condition, a duration): a formal expression, as they write it themselves (audit of the copy 64, P2-01). */
const FORMAL: readonly (readonly [string, string])[] = [['xsi:type', 'bpmn:tFormalExpression']];

/** The text without the characters XML 1.0 forbids, its line ends made `\n`. */
export function xmlChars(text: string): string {
  return text.replace(/\r\n?/g, '\n').replace(/[^\t\n -퟿-�\u{10000}-\u{10FFFF}]/gu, '');
}

/** A text between tags: `&`, `<` and `>` escaped. */
export const escapeText = (text: string): string => xmlChars(text).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');

/** A value of an attribute: the quotes escaped too, and the line ends and tabulations kept as character references. */
export const escapeAttribute = (text: string): string =>
  escapeText(text).replace(/"/g, '&quot;').replace(/'/g, '&apos;').replace(/\n/g, '&#10;').replace(/\t/g, '&#9;');

/** A number of a coordinate: two decimals at most, a point, never `-0`. */
const coordinate = (value: number): string => String(Math.round(value * 100) / 100 + 0);

/** The attributes of a tag, those without a value left out. */
const attributes = (pairs: readonly (readonly [string, string | null | undefined])[]): string =>
  pairs.filter((pair): pair is readonly [string, string] => pair[1] !== null && pair[1] !== undefined).map(([name, value]) => ` ${name}="${escapeAttribute(value)}"`).join('');

/** The writer of the lines of one file, indented by two spaces a level. */
class Lines {
  readonly out: string[] = [];

  add(depth: number, text: string): void {
    this.out.push(`${'  '.repeat(depth)}${text}`);
  }

  /** A tag with its children written by `inner`, or closed on itself when it writes none. */
  element(depth: number, tag: string, pairs: readonly (readonly [string, string | null | undefined])[], inner?: () => void): void {
    const before = this.out.length;
    this.add(depth, `<${tag}${attributes(pairs)}>`);
    inner?.();
    if (this.out.length === before + 1) this.out[before] = `${'  '.repeat(depth)}<${tag}${attributes(pairs)} />`;
    else this.add(depth, `</${tag}>`);
  }

  /** A tag with a text. */
  text(depth: number, tag: string, pairs: readonly (readonly [string, string | null | undefined])[], text: string): void {
    this.add(depth, `<${tag}${attributes(pairs)}>${escapeText(text)}</${tag}>`);
  }

  /** A documentation in Markdown, the way the view writes it; plain, without its format, for what modelers show as it is. */
  documentation(depth: number, text: string, plain = false): void {
    if (text.trim() !== '') this.text(depth, 'bpmn:documentation', plain ? [] : [['textFormat', 'text/markdown']], text);
  }
}

/** A flow node in the order of `tBaseElement`, `tFlowNode`, `tActivity` or `tCatchEvent`, then the content of a sub-process. */
function node(lines: Lines, depth: number, item: BpmnNode): void {
  const pairs: (readonly [string, string | null | undefined])[] = [['id', item.id], ['name', item.name], ['gatewayDirection', item.gatewayDirection]];
  lines.element(depth, `bpmn:${item.kind}`, pairs, () => {
    lines.documentation(depth + 1, item.documentation);
    item.incoming.forEach((flow) => { lines.text(depth + 1, 'bpmn:incoming', [], flow); });
    item.outgoing.forEach((flow) => { lines.text(depth + 1, 'bpmn:outgoing', [], flow); });
    if (isActivity(item.kind)) activityParts(lines, depth + 1, item);
    if (item.timer !== undefined) {
      const duration = item.timer.duration;
      lines.element(depth + 1, 'bpmn:timerEventDefinition', [['id', item.timer.id]], () => {
        if (duration !== null) lines.text(depth + 2, 'bpmn:timeDuration', FORMAL, duration);
      });
    }
    if (item.detail !== undefined) processContent(lines, depth + 1, item.detail);
  });
}

/** What an activity holds after its flows: the placeholder of its inputs, its data associations, its performer and its raciRoles. */
function activityParts(lines: Lines, depth: number, item: BpmnNode): void {
  const placeholder = `Property_${item.id}`;
  if (item.inputs.length > 0) lines.element(depth, 'bpmn:property', [['id', placeholder], ['name', '__targetRef_placeholder']]);
  item.inputs.forEach((input) => { lines.element(depth, 'bpmn:dataInputAssociation', [['id', input.id]], () => {
    lines.text(depth + 1, 'bpmn:sourceRef', [], input.referenceId);
    lines.text(depth + 1, 'bpmn:targetRef', [], placeholder);
  }); });
  item.outputs.forEach((output) => { lines.element(depth, 'bpmn:dataOutputAssociation', [['id', output.id]], () => {
    lines.text(depth + 1, 'bpmn:targetRef', [], output.referenceId);
  }); });
  if (item.performerId !== undefined)
    lines.element(depth, 'bpmn:humanPerformer', [['id', `Performer_${item.id}`]], () => { lines.text(depth + 1, 'bpmn:resourceRef', [], item.performerId ?? ''); });
  item.raciRoles.forEach((role, index) => { lines.element(depth, 'bpmn:resourceRole', [['id', `Role_${item.id}_${String(index + 1)}`], ['name', role.name]], () => {
    lines.text(depth + 1, 'bpmn:resourceRef', [], role.resourceId);
  }); });
}

function sequenceFlow(lines: Lines, depth: number, flow: BpmnFlow): void {
  lines.element(depth, 'bpmn:sequenceFlow', [['id', flow.id], ['name', flow.name], ['sourceRef', flow.sourceId], ['targetRef', flow.targetId]], () => {
    lines.documentation(depth + 1, flow.documentation);
    if (flow.condition !== null) lines.text(depth + 1, 'bpmn:conditionExpression', FORMAL, flow.condition);
  });
}

function lane(lines: Lines, depth: number, item: BpmnLane): void {
  lines.element(depth, 'bpmn:lane', [['id', item.id], ['name', item.name]], () => {
    lines.documentation(depth + 1, item.documentation);
    item.nodeIds.forEach((id) => { lines.text(depth + 1, 'bpmn:flowNodeRef', [], id); });
  });
}

function annotation(lines: Lines, depth: number, item: BpmnAnnotation): void {
  lines.element(depth, 'bpmn:textAnnotation', [['id', item.id]], () => { lines.text(depth + 1, 'bpmn:text', [], item.text); });
}

/** The content of a process or of a sub-process, in the order of the schema: its lanes, its flow elements, its artifacts. */
function processContent(lines: Lines, depth: number, process: BpmnProcess): void {
  lines.element(depth, 'bpmn:laneSet', [['id', process.laneSetId]], () => { process.lanes.forEach((item) => { lane(lines, depth + 1, item); }); });
  process.dataObjects.forEach((data) => { lines.element(depth, 'bpmn:dataObject', [['id', data.id], ['name', data.name]]); });
  process.references.forEach((reference) => { lines.element(depth, reference.store ? 'bpmn:dataStoreReference' : 'bpmn:dataObjectReference',
    [['id', reference.id], ['name', reference.name], [reference.store ? 'dataStoreRef' : 'dataObjectRef', reference.dataId]]); });
  process.nodes.forEach((item) => { node(lines, depth, item); });
  process.flows.forEach((flow) => { sequenceFlow(lines, depth, flow); });
  process.annotations.forEach((item) => { annotation(lines, depth, item); });
  process.annotations.forEach((item) => { lines.element(depth, 'bpmn:association',
    [['id', item.associationId], ['sourceRef', item.nodeId], ['targetRef', item.id], ['associationDirection', 'None']]); });
}

function diagram(lines: Lines, depth: number, item: BpmnDiagram): void {
  lines.element(depth, 'bpmndi:BPMNDiagram', [['id', item.id], ['name', item.name]], () => {
    lines.element(depth + 1, 'bpmndi:BPMNPlane', [['id', item.planeId], ['bpmnElement', item.elementId]], () => {
      for (const shape of item.shapes) {
        lines.element(depth + 2, 'bpmndi:BPMNShape', [['id', shape.id], ['bpmnElement', shape.elementId], ['isHorizontal', shape.horizontal === undefined ? null : String(shape.horizontal)],
          ['isExpanded', shape.expanded === undefined ? null : String(shape.expanded)], ['isMarkerVisible', shape.markerVisible === undefined ? null : String(shape.markerVisible)]], () => {
          bounds(lines, depth + 3, shape.bounds);
          if (shape.label !== undefined) { const label = shape.label; lines.element(depth + 3, 'bpmndi:BPMNLabel', [], () => { bounds(lines, depth + 4, label); }); }
        });
      }
      for (const edge of item.edges) {
        lines.element(depth + 2, 'bpmndi:BPMNEdge', [['id', edge.id], ['bpmnElement', edge.elementId]], () => {
          edge.waypoints.forEach((point) => { lines.element(depth + 3, 'di:waypoint', [['x', coordinate(point.x)], ['y', coordinate(point.y)]]); });
          if (edge.label !== undefined) { const label = edge.label; lines.element(depth + 3, 'bpmndi:BPMNLabel', [], () => { bounds(lines, depth + 4, label); }); }
        });
      }
    });
  });
}

function bounds(lines: Lines, depth: number, rect: { x: number; y: number; width: number; height: number }): void {
  lines.element(depth, 'dc:Bounds', [['x', coordinate(rect.x)], ['y', coordinate(rect.y)], ['width', coordinate(rect.width)], ['height', coordinate(rect.height)]]);
}

/** The text of the file: UTF-8, line ends `\n`, its root elements in the order of the schema, then its drawings. */
export function bpmnXml(definitions: BpmnDefinitions): string {
  const lines = new Lines();
  lines.add(0, '<?xml version="1.0" encoding="UTF-8"?>');
  lines.add(0, `<bpmn:definitions ${NAMESPACES}${attributes([['id', definitions.id], ['name', definitions.name], ['targetNamespace', `urn:${PMAP_FORMAT}:bpmn`],
    ['exporter', PRODUCT_NAME], ['exporterVersion', definitions.exporterVersion]])}>`);
  definitions.resources.forEach((resource) => { lines.element(1, 'bpmn:resource', [['id', resource.id], ['name', resource.name]]); });
  definitions.dataStores.forEach((store) => { lines.element(1, 'bpmn:dataStore', [['id', store.id], ['name', store.name]]); });
  lines.element(1, 'bpmn:collaboration', [['id', definitions.collaborationId]], () => {
    lines.documentation(2, definitions.documentation);
    // Written on the pool too, which a click in a modeler selects: the project, its classification, its work time are read
    // there; as plain text, which they show as it is (audit of the copy 64, P2-01).
    lines.element(2, 'bpmn:participant', [['id', definitions.participantId], ['name', definitions.name], ['processRef', definitions.process.id]], () => {
      lines.documentation(3, definitions.documentation, true);
    });
  });
  lines.element(1, 'bpmn:process', [['id', definitions.process.id], ['isExecutable', 'false']], () => { processContent(lines, 2, definitions.process); });
  definitions.diagrams.forEach((item) => { diagram(lines, 1, item); });
  lines.add(0, '</bpmn:definitions>');
  return `${lines.out.join('\n')}\n`;
}
