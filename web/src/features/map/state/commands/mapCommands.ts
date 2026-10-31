// mapCommands.ts
// The commands of the map in one list, and what reads them: the keyboard, the groups of the ribbon, the lines of each context menu (spec 3.5, B-8.2-06, B-8.2-07).

import { itemsOf, keyHandlerOf, type Command, type MenuLine } from '../../../../shared/commands/commands.ts';
import type { KeyHandler } from '../../../../shared/keyboard/keyboard.ts';
import { LANE_TINTS } from '../../../../shared/theme/laneTints.ts';
import type { MenuGroup } from '../../../../shared/ui/Menu.tsx';
import type { ToolbarGroup } from '../../../../shared/ui/Toolbar.tsx';
import type { Point } from '../../model/geometry/grid.ts';
import type { MapEditor } from '../editor.ts';
import { contextOf, type MapContext } from './context.ts';
import { EDIT_COMMANDS } from './editCommands.ts';
import { FILE_COMMANDS } from './fileCommands.ts';
import { LANE_TYPES, MENU_COMMANDS, STEP_TYPES } from './menuCommands.ts';
import { STREAM_COMMANDS } from './streamCommands.ts';
import { GATEWAYS, SHAPES, TOOL_COMMANDS } from './toolCommands.ts';
import { TRANSLATE_COMMANDS } from './translateCommands.ts';
import { ASSISTANT_COMMAND } from './assistantCommand.ts';

/** Every command of the map: one definition an action, for the ribbon, the menus and the keyboard. */
export const MAP_COMMANDS: readonly Command<MapContext>[] = [
  ...TOOL_COMMANDS, ...EDIT_COMMANDS, ...MENU_COMMANDS, ...STREAM_COMMANDS, ...TRANSLATE_COMMANDS, ...FILE_COMMANDS, ASSISTANT_COMMAND,
];

/**
 * The commands the keyboard runs, in the order they are looked for (B-2.5-06 to B-2.5-27, Ctrl+G and Ctrl+Shift+G of AR-154): Ctrl+V pastes by its own rule,
 * the Paste of the ribbon only being offered when there is something to paste; Ctrl+Z and Ctrl+Y wait for the writes
 * before them, the buttons follow the history.
 */
const KEY_IDS = [
  'level.up', 'tool.select', 'tool.hand', 'ribbon', 'command', 'assistant', 'undo.keys', 'redo.keys', 'cut', 'copy', 'paste.keys', 'duplicate', 'group', 'ungroup', 'selectAll', 'delete', 'rename', 'escape',
  'move.Left', 'move.Right', 'move.Up', 'move.Down',
];

export const KEY_COMMANDS: readonly Command<MapContext>[] = KEY_IDS.map((id) => MAP_COMMANDS.find((command) => command.id === id) as Command<MapContext>);

/** The handler of the layer of the canvas: the commands of the keyboard with the context of the moment, nothing while the map is not read. */
export function mapKeyHandler(editor: MapEditor, pointer: () => Point | null = () => null): KeyHandler {
  return (event, chord) => {
    const context = contextOf(editor, null, pointer());
    return context !== null && keyHandlerOf(KEY_COMMANDS, () => context)(event, chord);
  };
}

/** A zone of the ribbon: its name, the choice its radio buttons make, its commands, and whether a reader sees it (B-2.1-08). */
export interface RibbonZone {
  readonly id: string;
  readonly label: string;
  readonly choice?: string;
  readonly ids: readonly string[];
  /** The commands before which a thin separator stands. */
  readonly breaks?: readonly string[];
  readonly forEditors: boolean;
}

/**
 * The zones Level, Shapes, Gateways, Links, Edit, Display and Tools (B-2.1-08 to B-2.1-32): « Remonter d'un niveau » first
 * and the tools last, next to the display, by the choice of the owner of the product (AR-155); Grouper and Dissocier next to
 * Détailler (AR-154).
 */
export const RIBBON: readonly RibbonZone[] = [
  { id: 'level', label: 'ribbon.level', ids: ['level.up'], forEditors: false },
  { id: 'shapes', label: 'ribbon.shapes', choice: 'ribbon.shape_choice', ids: SHAPES.map((type) => `shape.${type}`), forEditors: true },
  { id: 'gateways', label: 'ribbon.gateways', choice: 'ribbon.gateway_choice', ids: [...GATEWAYS.map((type) => `shape.${type}`), 'branch'], forEditors: true },
  { id: 'links', label: 'ribbon.links', ids: ['link', 'command'], forEditors: true },
  { id: 'edit', label: 'ribbon.edit', ids: ['undo', 'redo', 'cut', 'copy', 'paste', 'detail', 'group', 'ungroup', 'align'], breaks: ['cut', 'detail'], forEditors: true },
  { id: 'display', label: 'ribbon.display', ids: ['codes', 'pictos', 'raci', 'stream', 'matrix'], forEditors: false },
  { id: 'tools', label: 'ribbon.tools', choice: 'ribbon.tool_choice', ids: ['tool.select', 'tool.hand'], forEditors: false },
];

const editor = (context: MapContext) => context.canEdit;
const reader = (context: MapContext) => !context.canEdit;
const line = (id: string, group: number, when?: (context: MapContext) => boolean): MenuLine<MapContext> => (when === undefined ? { id, group } : { id, group, when });

/** The menu of a step, in the order of annex B 2.4-10 to 2.4-29; its sheet and its RACI roles come after its detailed map (NF08). */
export const STEP_MENU: readonly MenuLine<MapContext>[] = [
  line('step.open', 1), line('step.sheet', 1), line('step.raci', 1), line('rename', 1, editor), line('step.translate', 1), line('step.volume', 1, editor),
  line('step.insert-before', 1, editor), line('step.insert-after', 1, editor), line('branch', 1, editor),
  line('step.detail', 1, editor), line('step.dissolve', 1, editor), line('step.delete-detail', 1, editor),
  line('cut', 2, editor), line('copy', 2, editor), line('duplicate', 2, editor), line('paste.before', 2, editor), line('paste.after', 2, editor),
  ...STEP_TYPES.map((type) => line(`step.type.${type}`, 3, editor)),
  line('step.copy-code', 4), line('copy', 4, reader), line('step.delete', 4, editor),
];

/** The menu of several steps chosen (B-2.4-31 to B-2.4-37), « Grouper en sous-processus » in the place of « Détailler la sélection » (AR-154). */
export const SELECTION_MENU: readonly MenuLine<MapContext>[] = [
  line('cut', 1, editor), line('copy', 1), line('duplicate', 1, editor), line('selection.group', 2, editor), line('selection.delete', 3, editor),
];

/** The menu of the background (B-2.4-38 to B-2.4-40). */
export const PANE_MENU: readonly MenuLine<MapContext>[] = [line('paste.here', 1, editor), line('selectAll', 1)];

/** The menu of a flow, only for who may change the map (B-2.4-42 to B-2.4-44), « Inverser le sens » before the deletion (AR-155). */
export const FLOW_MENU: readonly MenuLine<MapContext>[] = [line('flow.label', 1), line('flow.translate', 1), line('flow.share', 1), line('flow.reverse', 1), line('flow.delete', 1)];

/** The menu of a lane, only for who may change the map (B-2.4-45 to B-2.4-49). */
export const LANE_MENU: readonly MenuLine<MapContext>[] = [
  line('lane.rename', 1), line('lane.translate', 1), line('lane.headcount', 1), ...LANE_TYPES.map((type) => line(`lane.type.${type}`, 2)), ...LANE_TINTS.map((tint) => line(`lane.colour.${tint.key}`, 3)), line('lane.delete', 4),
];

/**
 * The choices of the menus drawn as the validated mock-up draws them (C11 of the gate P6, ARIA 1.2: one choice checked a
 * group): the seven types of a step after a radio, their shape on the right; the type of a lane under its title, its icon
 * then its radio; its colour under its title, eight swatches with the name and the rank of the one chosen.
 */
export const STEP_MENU_GROUPS: readonly MenuGroup[] = [{ ids: STEP_TYPES.map((type) => `step.type.${type}`), name: 'menu.step_type_group', look: 'radio-icon' }];

export const LANE_MENU_GROUPS: readonly MenuGroup[] = [
  {
    ids: LANE_TYPES.map((type) => `lane.type.${type}`), name: 'menu.lane_type_group', titled: true, look: 'icon-radio',
    texts: Object.fromEntries(LANE_TYPES.map((type) => [`lane.type.${type}`, `map.lane_types.${type}`])),
  },
  {
    ids: LANE_TINTS.map((tint) => `lane.colour.${tint.key}`), name: 'menu.lane_colour_group', titled: true, look: 'swatches', caption: 'menu.tint_of',
    texts: Object.fromEntries(LANE_TINTS.map((tint) => [`lane.colour.${tint.key}`, `map.tints.${tint.key}`])),
    inks: Object.fromEntries(LANE_TINTS.map((tint, index) => [`lane.colour.${tint.key}`, `var(--lane-${String(index + 1)}-ink)`])),
  },
];

/**
 * The File menu, opened under its button in the header: the files that make a project (.pmap, package, as on the home
 * page, AR-154), the copy, the exports of the map, the package of exchange, then the properties of the project
 * (B-2.4-52 to B-2.4-58, ADR 058, spec 4.1).
 */
export const FILE_MENU: readonly MenuLine<MapContext>[] = [
  line('file.open', 1), line('file.import', 1), line('file.download', 1), line('file.duplicate', 1), line('file.pdf', 2), line('file.png', 2), line('file.xlsx', 2),
  line('file.export', 3), line('file.replace', 3), line('file.properties', 4),
];

/** The groups of the ribbon for the context of the moment: a reader sees only Tools and Display (B-2.1-09, B-2.1-14, B-2.1-31). */
export function ribbonGroups(context: MapContext): ToolbarGroup[] {
  return RIBBON.filter((zone) => !zone.forEditors || context.canEdit).map((zone) => ({
    id: zone.id, label: zone.label, ...(zone.choice === undefined ? {} : { choice: zone.choice }),
    items: itemsOf(MAP_COMMANDS, zone.ids, context).map((item) => (zone.breaks?.includes(item.id) === true ? { ...item, separatorBefore: true } : item)),
  }));
}
