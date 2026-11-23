// bpmnPlacement.ts
// The column of each step of a .pmap: the one it gives, or, for a file that gives none, the rule of the opening (`AutomaticColumns`, format section 5), so that a hand-written file draws as it would open.

import type { PmapMap } from '../../../../shared/pmap.ts';

/**
 * The columns of a map: those of the file when every step gives one; otherwise a step goes one column after the most
 * advanced of its predecessors, column 0 without one, pushed right by a cell taken in its lane and row, the order of the
 * file deciding a tie, a loop broken at the first step left in the file.
 */
export function placedColumns(map: PmapMap): Map<string, number> {
  const steps = map.steps ?? [];
  if (steps.every((step) => step.column !== undefined)) return new Map(steps.map((step) => [step.id, step.column ?? 0]));
  const predecessors = new Map(steps.map((step) => [step.id, (map.flows ?? []).filter((flow) => flow.target === step.id && flow.source !== step.id).map((flow) => flow.source)]));
  const placed = new Map<string, number>();
  const taken = new Set<string>();
  const remaining = [...steps];
  while (remaining.length > 0) {
    const ready = remaining.findIndex((step) => (predecessors.get(step.id) ?? []).every((id) => placed.has(id)));
    const [step] = remaining.splice(ready < 0 ? 0 : ready, 1);
    if (step === undefined) break;
    let column = Math.max(0, ...(predecessors.get(step.id) ?? []).flatMap((id) => { const at = placed.get(id); return at === undefined ? [] : [at + 1]; }));
    while (taken.has(`${step.lane}:${String(step.row ?? 0)}:${String(column)}`)) column++;
    taken.add(`${step.lane}:${String(step.row ?? 0)}:${String(column)}`);
    placed.set(step.id, column);
  }
  return placed;
}
