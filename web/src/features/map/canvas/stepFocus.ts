// stepFocus.ts
// The focus of the keyboard given back to a step of the canvas, or to its form in the view in BPMN 2.0, when the panel opened from it closes while holding the focus (NF08, WAI-ARIA).

/** The node of a step, as React Flow draws it on the canvas. */
export const stepNode = (stepId: string): HTMLElement | null => document.querySelector<HTMLElement>(`.react-flow__node-step[data-id="${stepId}"]`);

/** The form of a step in the view in BPMN 2.0, named there by its code (ADR 106). */
const bpmnNode = (code: string): HTMLElement | null => document.querySelector<HTMLElement>(`[data-testid="bpmn-node"][data-step="${code.replace(/["\\]/g, '\\$&')}"]`);

/**
 * Called as a panel closes, still in the page: when the focus is in it, the node of its step takes it, so that the
 * keyboard goes on from the step, its form in the view in BPMN 2.0 when that view is shown (audit of the copy 64, P2-02);
 * else the focus stays where the gesture that closed the panel put it.
 */
export function focusBackOnStep(stepId: string, panel: string, code?: string): void {
  if (document.activeElement?.closest(panel) == null) return;
  (stepNode(stepId) ?? (code === undefined ? null : bpmnNode(code)))?.focus();
}
