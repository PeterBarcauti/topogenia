// bpmnModdle.d.ts
// What the tests read of bpmn-moddle (MIT, a dependency of the tests only, never served): the reading of a .bpmn by the meta-model of BPMN 2.0 that the modellers use, its warnings and the elements by identifier.

declare module 'bpmn-moddle' {
  export interface ModdleElement {
    readonly $type: string;
    readonly id?: string;
    readonly name?: string;
    readonly [property: string]: unknown;
  }

  export interface ModdleResult {
    readonly rootElement: ModdleElement;
    readonly warnings: readonly { readonly message: string }[];
    readonly elementsById: Readonly<Record<string, ModdleElement>>;
  }

  export interface Moddle {
    fromXML(xml: string, type?: string): Promise<ModdleResult>;
  }

  /** The meta-model of BPMN 2.0, with its packages (the library exports a factory under this name). */
  export function BpmnModdle(): Moddle;
}
