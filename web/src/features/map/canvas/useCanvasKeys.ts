// useCanvasKeys.ts
// The keys of the canvas on the layer of the one keyboard manager: the commands of the map, the Space bar held for the Hand, Enter on a step, a character typed on the step chosen alone (annex B 2.3, 2.5, NF08, AR-151).

import { useEffect, useMemo, type RefObject } from 'react';
import type { KeyHandler } from '../../../shared/keyboard/keyboard.ts';
import { useKeyLayer } from '../../../shared/keyboard/KeyboardContext.tsx';
import { noteKeyboard } from '../../../shared/ui/inputModality.ts';
import type { Point } from '../model/geometry/grid.ts';
import { activateStep } from '../state/actions/stepActivation.ts';
import { mapKeyHandler } from '../state/commands/mapCommands.ts';
import type { MapEditor } from '../state/editor.ts';

/** The elements that Space activates: a button, a link, a step or a flow focused keep their key (accessibility). */
const ACTIVATED_BY_SPACE = 'button, a, [role="button"], [role="menuitem"], .react-flow__node, .react-flow__edge';

/** Space held gives the Hand for the time being, released with the key (B-2.3-06, B-2.5-17). */
function spaceHandler(editor: MapEditor): KeyHandler {
  return (event) => {
    const held = editor.store.getState().spaceHand;
    if (event.type === 'keyup') {
      if (held) editor.store.setState({ spaceHand: false });
      return held;
    }
    if (event.target instanceof Element && event.target.closest(ACTIVATED_BY_SPACE) !== null) return false;
    if (!held) editor.store.setState({ spaceHand: true });
    return true;
  };
}

/**
 * Enter on a step focused (NF08, S-NF-12, AR-151): it chooses the step alone, then opens its sheet, its RACI panel next
 * to it on the layer, or takes it as the source or the target of the link mode, as a click does in these two modes. The key stops there: the
 * control of the panel that takes the focus does not receive it as well (a letter of the RACI panel would be given).
 */
function enterHandler(editor: MapEditor): KeyHandler {
  return (event) => {
    const node = event.type === 'keydown' && event.target instanceof Element ? event.target.closest('.react-flow__node-step') : null;
    const stepId = node?.getAttribute('data-id');
    if (node == null || stepId == null) return false;
    const box = node.getBoundingClientRect();
    // The panel opened takes the focus: noted before it is drawn, which may come before the other listeners of the key.
    noteKeyboard();
    editor.store.setState({ selection: { steps: [stepId], flows: [] } });
    activateStep(editor, stepId, { x: box.left + box.width / 2, y: box.top + box.height / 2 });
    return true;
  };
}

/**
 * A character typed on the canvas while one step alone is chosen begins its name in place, from that character, as in a
 * spreadsheet (AR-151); V and H choose a tool only when no step alone is chosen. Ctrl and Alt keep their shortcuts; AltGr,
 * which Windows gives as Ctrl and Alt together, types (€, @).
 */
function typeHandler(editor: MapEditor): KeyHandler {
  return (event) => {
    const altGr = event.ctrlKey && event.altKey && !event.metaKey;
    if (event.type !== 'keydown' || event.key.length !== 1 || event.key === ' ' || event.isComposing || ((event.ctrlKey || event.metaKey || event.altKey) && !altGr)) return false;
    if (!(event.target instanceof Element) || event.target.closest('.map-canvas') === null) return false;
    const { map, mode, selection, editing, translating } = editor.store.getState();
    const stepId = selection.steps.length === 1 && selection.flows.length === 0 ? selection.steps[0] : undefined;
    const typing = map !== null && (map.rights.structure || translating);
    if (map === null || !typing || mode !== 'normal' || editing !== null || stepId === undefined || !map.steps.some((step) => step.id === stepId)) return false;
    event.preventDefault();
    editor.store.setState({ editing: stepId, editingSeed: { stepId, text: event.key } });
    return true;
  };
}

/** Registers the keys of the canvas while it is shown; the Hand held is released when the window loses the focus (B-2.3-07). */
export function useCanvasKeys(editor: MapEditor, pointer: RefObject<Point | null>): void {
  const space = useMemo(() => spaceHandler(editor), [editor]);
  const enter = useMemo(() => enterHandler(editor), [editor]);
  const type = useMemo(() => typeHandler(editor), [editor]);
  const commands = useMemo(() => mapKeyHandler(editor, () => pointer.current), [editor, pointer]);
  useKeyLayer('canvas', (event, chord) => (chord === 'Space' ? space(event, chord) : chord === 'Enter' ? enter(event, chord) : type(event, chord) || commands(event, chord)));
  useEffect(() => {
    const release = () => {
      editor.store.setState({ spaceHand: false });
    };
    window.addEventListener('blur', release);
    return () => {
      window.removeEventListener('blur', release);
    };
  }, [editor]);
}
