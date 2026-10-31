// editCommands.ts
// The commands of the edition of the map: undo and redo, clipboard, duplicate, detail, group and ungroup, align, choose all, delete, rename, Escape, the arrows (annex B 2.1, 2.5, 2.6, 2.7, AR-154).

import { ClipboardPaste, Copy, CopyPlus, Group, PencilLine, Redo2, Scissors, SquareDashed, SquareStack, Trash2, Undo2, Ungroup, WandSparkles } from 'lucide-react';
import type { Command } from '../../../../shared/commands/commands.ts';
import { endOfMap } from '../../model/clipboard.ts';
import { cellAtPoint } from '../../model/moves.ts';
import { hasContent, isMacro } from '../../model/stepFacts.ts';
import { dissolveStep } from '../actions/dissolution.ts';
import { canReplay, replay } from '../actions/history.ts';
import { align, moveByKeys } from '../actions/layout.ts';
import { duplicate, giveUpCut, paste, takeToClipboard } from '../actions/paste.ts';
import { deleteChosen, selectAll } from '../actions/selection.ts';
import { detailStep, groupSteps } from '../actions/steps.ts';
import { isCut, stepOf, stepsOf, type MapContext } from './context.ts';

const chosen = (context: MapContext) => stepsOf(context).length > 0;

/**
 * Ctrl+V and the Paste of the ribbon (B-2.7-19 to B-2.7-21): after the one step chosen when it is not cut, in the flow;
 * else in the cell under the pointer; else at the end of the map.
 */
function pasteWhereAimed(context: MapContext): void {
  const [only, ...others] = context.state.selection.steps;
  if (only !== undefined && others.length === 0 && !isCut(context, only)) {
    void paste(context.editor, { stepId: only, position: 'after' });
    return;
  }
  const cell = (context.pointer === null ? null : cellAtPoint(context.map, context.pointer)) ?? endOfMap(context.map);
  if (cell !== null) void paste(context.editor, { cell });
}

/** One step that may be detailed: no detail yet, with content (B-2.1-28); several steps are grouped by « Grouper » (AR-154). */
function detailable(context: MapContext): boolean {
  const step = stepOf(context);
  return step !== undefined && !isMacro(step) && hasContent(step.type);
}

/** Several steps to gather in one detailed step « Sous-processus », as Grouper does in Office (B-2.1-29, AR-154). */
export const groupable = (context: MapContext): boolean => context.canEdit && stepsOf(context).length > 1;

/** One detailed step whose detail may come back into the flow, while the map has no frame open (B-2.4-16, B-2.9-15, AR-154). */
export function ungroupable(context: MapContext): boolean {
  const step = stepOf(context);
  return context.canEdit && step !== undefined && isMacro(step) && context.map.dissolution === null;
}

/** Groups the steps of the command (the steps of the menu, else the steps chosen). */
export const groupChosen = (context: MapContext): void => void groupSteps(context.editor, stepsOf(context));

/** Brings back into the flow the detail of the one step of the command. */
export function ungroupChosen(context: MapContext): void {
  const step = stepOf(context);
  if (step !== undefined) void dissolveStep(context.editor, step.id);
}

const arrow = (key: string, columns: number, lanes: number): Command<MapContext> => ({
  id: `move.${key}`, label: 'ribbon.select', shortcuts: [`Arrow${key}`],
  enabled: (context) => context.canEdit && context.state.selection.steps.length > 0,
  run: (context) => {
    void moveByKeys(context.editor, context.state.selection.steps, columns, lanes);
  },
});

/**
 * The buttons Undo and Redo follow the history of the host (B-2.1-24), in the zone Edit that only who may change the map
 * sees; the keys queue the demand behind the writes asked before, whose reply may fill the history (B-2.6-09), so they
 * only need the right to change the map, or to translate it in another language than its own (ADR 083: one Ctrl+Z takes
 * a translation by the assistant back).
 */
const replayCommands = (which: 'undo' | 'redo', icon: typeof Undo2, shortcuts: readonly string[]): Command<MapContext>[] => [
  { id: which, label: `ribbon.${which}`, icon, shortcuts, enabled: (context) => canReplay(context.state, which), run: (context) => void replay(context.editor, which) },
  { id: `${which}.keys`, label: `ribbon.${which}`, shortcuts, enabled: (context) => context.canEdit || context.state.translating, run: (context) => void replay(context.editor, which) },
];

export const EDIT_COMMANDS: readonly Command<MapContext>[] = [
  ...replayCommands('undo', Undo2, ['Ctrl+Z']),
  ...replayCommands('redo', Redo2, ['Ctrl+Y', 'Ctrl+Shift+Z']),
  { id: 'cut', label: 'menu.cut', icon: Scissors, shortcuts: ['Ctrl+X'], enabled: (context) => context.canEdit && chosen(context),
    run: (context) => {
      takeToClipboard(context.editor, stepsOf(context), 'cut');
    } },
  { id: 'copy', label: 'menu.copy', icon: Copy, shortcuts: ['Ctrl+C'], enabled: chosen,
    run: (context) => {
      takeToClipboard(context.editor, stepsOf(context), 'copy');
    } },
  { id: 'paste', label: 'ribbon.paste', icon: ClipboardPaste, shortcuts: ['Ctrl+V'], enabled: (context) => context.canEdit && context.clipboard !== null, run: pasteWhereAimed },
  { id: 'paste.keys', label: 'ribbon.paste', shortcuts: ['Ctrl+V'], enabled: (context) => context.canEdit, run: pasteWhereAimed },
  { id: 'duplicate', label: 'menu.duplicate', icon: CopyPlus, shortcuts: ['Ctrl+D'], enabled: (context) => context.canEdit && chosen(context),
    run: (context) => void duplicate(context.editor, stepsOf(context)) },
  { id: 'detail', label: 'ribbon.detail', icon: SquareStack, enabled: (context) => context.canEdit && detailable(context),
    run: (context) => {
      const step = stepOf(context);
      if (step !== undefined) void detailStep(context.editor, step.id);
    } },
  // Ctrl+G and Ctrl+Shift+G as in Office; the map takes them from the browser (find next) only when they apply (AR-154).
  { id: 'group', label: 'ribbon.group', icon: Group, shortcuts: ['Ctrl+G'], enabled: groupable, run: groupChosen },
  { id: 'ungroup', label: 'ribbon.ungroup', icon: Ungroup, shortcuts: ['Ctrl+Shift+G'], enabled: ungroupable, run: ungroupChosen },
  { id: 'align', label: 'ribbon.align', icon: WandSparkles, enabled: (context) => context.canEdit && context.map.steps.length > 0, run: (context) => void align(context.editor) },
  { id: 'selectAll', label: 'menu.select_all', icon: SquareDashed, shortcuts: ['Ctrl+A'], enabled: () => true, run: (context) => {
    selectAll(context.editor);
  } },
  { id: 'delete', label: 'menu.delete', icon: Trash2, shortcuts: ['Delete', 'Backspace'], danger: true,
    enabled: (context) => context.canEdit && context.state.selection.steps.length + context.state.selection.flows.length > 0,
    run: (context) => {
      deleteChosen(context.editor, context.state.selection.steps, context.state.selection.flows);
    } },
  { id: 'rename', label: 'menu.rename', icon: PencilLine, shortcuts: ['F2'], enabled: (context) => context.canEdit && chosen(context),
    run: (context) => {
      context.editor.store.setState({ editing: stepsOf(context)[0] ?? null });
    } },
  { id: 'escape', label: 'keys.Escape', shortcuts: ['Escape'], enabled: () => true,
    run: (context) => {
      giveUpCut();
      // A sheet folded to its strip stays: Escape is the map's then (spec 3.0 section 15.2).
      const folded = context.editor.dock.getState().folded.sheet === true;
      context.editor.store.setState({ mode: 'normal', linkSource: null, editing: null, openSheet: folded ? context.state.openSheet : null });
    } },
  arrow('Left', -1, 0),
  arrow('Right', 1, 0),
  arrow('Up', 0, -1),
  arrow('Down', 0, 1),
];
