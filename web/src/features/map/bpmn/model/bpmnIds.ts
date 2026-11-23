// bpmnIds.ts
// The identifiers of a .bpmn: each a valid xsd:ID (an NCName), read from the codes of the map so that a person finds the step, the same map always giving the same ones, never two alike in one file.

/** A text made an NCName part: the letters of ASCII, figures, `_`, `-` and `.` kept, the rest `_`. */
export const ncNamePart = (text: string): string => text.replace(/[^A-Za-z0-9_.-]/g, '_');

/** The identifiers of one file: each asked once, a second ask of the same name taking a rank (`_2`, `_3`...). */
export class BpmnIds {
  private readonly taken = new Set<string>();

  /** A new identifier from a prefix (letters) and a key read from the map (a code, an identifier of the .pmap). */
  next(prefix: string, key: string): string {
    const base = `${prefix}_${ncNamePart(key)}`;
    let id = base;
    for (let rank = 2; this.taken.has(id); rank++) id = `${base}_${String(rank)}`;
    this.taken.add(id);
    return id;
  }
}
