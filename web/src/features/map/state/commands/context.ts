// context.ts
// What a command of the map reads: the editor, the map, the rights, the clipboard, the preferences, the object of a menu and the pointer (spec 3.5).

import type { Schema } from '../../../../api/client.ts';
import type { ClipboardContent } from '../../model/clipboard.ts';
import type { Point } from '../../model/geometry/grid.ts';
import { clipboard, cutSteps } from '../clipboard.ts';
import type { MapEditor } from '../editor.ts';
import type { MenuTarget } from '../floating.ts';
import type { MapState } from '../mapStore.ts';
import type { DisplayPreferences } from '../preferences.ts';

type MapView = Schema<'MapView'>;
type StepView = Schema<'StepView'>;

export interface MapContext {
  readonly editor: MapEditor;
  readonly map: MapView;
  readonly state: MapState;
  /** The structure may be changed: facilitator of the project or administrator (B-2.1-35). */
  readonly canEdit: boolean;
  readonly clipboard: ClipboardContent | null;
  readonly preferences: DisplayPreferences;
  /** The object of the context menu that runs the command; null for the ribbon and the keyboard. */
  readonly target: MenuTarget | null;
  /** The last point of the canvas under the pointer, for Ctrl+V (B-2.7-20); null when the pointer left the canvas. */
  readonly pointer: Point | null;
}

/** The context of the moment, null while the map is not read. */
export function contextOf(editor: MapEditor, target: MenuTarget | null = null, pointer: Point | null = null): MapContext | null {
  const state = editor.store.getState();
  if (state.map === null) return null;
  return {
    editor, map: state.map, state, canEdit: state.map.rights.structure, clipboard: clipboard.getState().content,
    preferences: editor.preferences.getState(), target, pointer,
  };
}

/** The steps a command acts on: the step of the menu, the steps of the menu of a selection, else the steps chosen. */
export function stepsOf(context: MapContext): readonly string[] {
  const { target } = context;
  if (target?.kind === 'step') return [target.stepId];
  if (target?.kind === 'selection') return target.stepIds;
  return context.state.selection.steps;
}

/** The one step a command acts on: the step of the menu, else the only step chosen. */
export function stepOf(context: MapContext): StepView | undefined {
  const ids = stepsOf(context);
  return ids.length === 1 ? context.map.steps.find((step) => step.id === ids[0]) : undefined;
}

/** Whether a step is cut and waits for its paste (B-2.4-24). */
export const isCut = (context: MapContext, stepId: string): boolean => cutSteps(context.clipboard, context.map.id).has(stepId);
