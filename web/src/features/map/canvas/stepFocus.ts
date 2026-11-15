// stepFocus.ts
// The focus of the keyboard given back to a step of the canvas when the panel opened from it closes while holding the focus (NF08, WAI-ARIA).

/** The node of a step, as React Flow draws it on the canvas. */
export const stepNode = (stepId: string): HTMLElement | null => document.querySelector<HTMLElement>(`.react-flow__node-step[data-id="${stepId}"]`);

/**
 * Called as a panel closes, still in the page: when the focus is in it, the node of its step takes it, so that the
 * keyboard goes on from the step; else the focus stays where the gesture that closed the panel put it.
 */
export function focusBackOnStep(stepId: string, panel: string): void {
  if (document.activeElement?.closest(panel) != null) stepNode(stepId)?.focus();
}
