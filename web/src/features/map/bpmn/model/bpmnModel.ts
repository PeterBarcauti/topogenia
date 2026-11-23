// bpmnModel.ts
// The BPMN 2.0 diagram of a map in memory (ADR 106): the semantics a modeller reads and the geometry it draws (BPMNDI), built by the code from a .pmap, read by the view and by the writer of the .bpmn alike.

import type { Point, Rect } from '../../model/geometry/grid.ts';

/** The kinds of flow node a map gives, one per form of the symbols of the view. */
export type BpmnNodeKind =
  | 'startEvent' | 'endEvent' | 'intermediateCatchEvent'
  | 'manualTask' | 'userTask' | 'serviceTask' | 'subProcess'
  | 'exclusiveGateway' | 'parallelGateway' | 'inclusiveGateway';

export type GatewayDirection = 'Unspecified' | 'Converging' | 'Diverging' | 'Mixed';

/** A role of the RACI other than the R: its name in BPMN and the lane that holds it, by its resource. */
export interface BpmnRole {
  readonly name: string;
  readonly resourceId: string;
}

/** A reading or a writing of a document by an activity: the reference drawn near it. */
export interface BpmnDataLink {
  readonly id: string;
  readonly referenceId: string;
}

/** A flow node: a step of the map, its kind, its texts, its lane, its links, and its detail when it has one. */
export interface BpmnNode {
  readonly id: string;
  /** The identifier of its step in the .pmap: the code of the application in a copy. */
  readonly stepId: string;
  readonly kind: BpmnNodeKind;
  readonly name: string;
  /** False when the name is the original shown in another language than its own (ADR 083): the view writes it in italics. */
  readonly translated: boolean;
  readonly laneId: string;
  readonly documentation: string;
  readonly incoming: string[];
  readonly outgoing: string[];
  /** A timer: the event of a wait, with its duration in ISO 8601 when the sheet gives one. */
  readonly timer?: { readonly id: string; readonly duration: string | null };
  readonly gatewayDirection?: GatewayDirection;
  /** The resource of the lane that does the step (the R), none for a system lane. */
  readonly performerId?: string;
  readonly raciRoles: readonly BpmnRole[];
  /** The placeholder an input association needs as its target (the habit of the modellers), with the inputs. */
  readonly inputs: readonly BpmnDataLink[];
  readonly outputs: readonly BpmnDataLink[];
  /** The detail of a detailed step, when the sub-processes are carried. */
  readonly detail?: BpmnProcess;
  /** True for a detailed step: a sub-process folded under its [+], with or without its content. */
  readonly detailed: boolean;
}

export interface BpmnFlow {
  readonly id: string;
  readonly sourceId: string;
  readonly targetId: string;
  readonly name: string | null;
  readonly condition: string | null;
  readonly documentation: string;
}

export interface BpmnLane {
  readonly id: string;
  readonly laneKey: string;
  readonly name: string;
  readonly system: boolean;
  readonly documentation: string;
  readonly nodeIds: string[];
}

/** A document of the map: an object of data, or a store for a software (its store declared at the root). */
export interface BpmnData {
  readonly id: string;
  readonly name: string;
  readonly store: boolean;
}

/** A reference to a document drawn near the step that produces it, receives it from outside, or uses it. */
export interface BpmnDataReference {
  readonly id: string;
  readonly dataId: string;
  readonly store: boolean;
  readonly name: string;
  readonly nodeId: string;
}

/** The note of the open pain points of a step, linked to it. */
export interface BpmnAnnotation {
  readonly id: string;
  readonly associationId: string;
  readonly nodeId: string;
  readonly text: string;
}

/** A process: the one of the map shown, or the content of a sub-process. */
export interface BpmnProcess {
  readonly id: string;
  readonly laneSetId: string;
  readonly lanes: readonly BpmnLane[];
  readonly nodes: readonly BpmnNode[];
  readonly flows: readonly BpmnFlow[];
  /** The documents produced or received in this process (the stores are at the root). */
  readonly dataObjects: readonly BpmnData[];
  readonly references: readonly BpmnDataReference[];
  readonly annotations: readonly BpmnAnnotation[];
}

export interface BpmnShape {
  readonly id: string;
  readonly elementId: string;
  readonly bounds: Rect;
  readonly horizontal?: boolean;
  readonly expanded?: boolean;
  readonly markerVisible?: boolean;
  readonly label?: Rect;
}

export interface BpmnEdge {
  readonly id: string;
  readonly elementId: string;
  readonly waypoints: readonly Point[];
  readonly label?: Rect;
}

/** A drawing: the plane of the map shown (on the collaboration), or of a sub-process (on the sub-process). */
export interface BpmnDiagram {
  readonly id: string;
  readonly planeId: string;
  readonly elementId: string;
  readonly name: string;
  readonly shapes: readonly BpmnShape[];
  readonly edges: readonly BpmnEdge[];
}

/** The whole file: its root elements and its drawings, in the order the schema imposes. */
export interface BpmnDefinitions {
  readonly id: string;
  readonly name: string;
  readonly exporterVersion: string;
  readonly resources: readonly { readonly id: string; readonly name: string }[];
  readonly dataStores: readonly BpmnData[];
  readonly collaborationId: string;
  readonly participantId: string;
  readonly documentation: string;
  readonly process: BpmnProcess;
  readonly diagrams: readonly BpmnDiagram[];
}
