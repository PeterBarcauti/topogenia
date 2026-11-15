// usePresenterFocus.ts
// The focus of the stage of a presentation, a modal dialog (ADR 097; relecture 6: R6-01, R6-05): it enters the stage when the presentation starts, so that no key presses a control of the editor left focused under it, and goes back where it was when the presentation ends; Tab and Maj+Tab walk the controls of the stage and never leave it; while the keyboard walks them, Entrée and Espace press the one focused.

import { useEffect, useState, type RefObject } from 'react';

/** What takes the focus in the stage, as in a dialog of the application (`shared/ui/Dialog.tsx`). */
const FOCUSABLE = 'button:not([disabled]), [href], input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])';

export interface StageFocus {
  /** The keyboard walks the controls of the stage: Tab gave the focus last, no press of the pointer since. */
  readonly keyed: boolean;
  /** Tab, or Maj+Tab back: the control of the stage after the one focused, or before it, round and round; never one out of the stage. */
  readonly tab: (back: boolean) => void;
  /**
   * The button Entrée and Espace press: the one focused in the stage while the keyboard walks its controls, null
   * otherwise. A button focused by a click, or given the focus back (the pencil of a comment closed), is not pressed:
   * the keys stay those of the story, Espace play and pause, Entrée the next sentence.
   */
  readonly pressed: () => HTMLButtonElement | null;
  /** A press of the pointer in the stage: the keyboard no longer walks its controls. */
  readonly pointed: () => void;
}

/**
 * The control Tab goes to from the element focused, in the order of the page: the first after it, the last before it
 * on the way back, round and round; from the stage itself, or from out of it, its first control, or its last.
 */
function controlAfter(stage: HTMLElement, back: boolean): HTMLElement | null {
  const controls = [...stage.querySelectorAll<HTMLElement>(FOCUSABLE)];
  const active = document.activeElement;
  const edge = (back ? controls.at(-1) : controls[0]) ?? null;
  if (active === null || active === stage || !stage.contains(active)) return edge;
  const placed = (control: HTMLElement, where: number): boolean => (active.compareDocumentPosition(control) & where) !== 0;
  const next = back
    ? controls.findLast((control) => placed(control, Node.DOCUMENT_POSITION_PRECEDING) && !control.contains(active))
    : controls.find((control) => placed(control, Node.DOCUMENT_POSITION_FOLLOWING));
  return next ?? edge;
}

/** Gives the focus to the stage while it is shown, and back to what had it before; keeps Tab in the stage. */
export function useStageFocus(root: RefObject<HTMLDivElement | null>): StageFocus {
  const [keyed, setKeyed] = useState(false);
  useEffect(() => {
    const before = document.activeElement;
    root.current?.focus();
    return () => {
      if (before instanceof HTMLElement) before.focus();
    };
  }, [root]);
  const tab = (back: boolean) => {
    if (root.current === null) return;
    controlAfter(root.current, back)?.focus();
    setKeyed(true);
  };
  const pointed = () => {
    setKeyed(false);
  };
  const pressed = () => {
    const active = document.activeElement;
    return keyed && active instanceof HTMLButtonElement && root.current?.contains(active) === true ? active : null;
  };
  return { keyed, tab, pressed, pointed };
}
