// shortcutSheet.ts
// The sheet of the keyboard shortcuts, by use: the key commands of the map, read from their list, and the keys that are not commands (Space held, the wheel, Enter and Shift+Enter in the search, Enter, Shift+F10, a character typed, F1, Ctrl+S), for the panel « Raccourcis clavier » (lot 1 of the 3.4, ADR 109 and 110).

import { KEY_COMMANDS } from './mapCommands.ts';

/** A line of key commands: their identifiers, a label of its own when the command's does not say the effect, the keys shown (all their shortcuts by default). */
export interface CommandLine {
  readonly ids: readonly string[];
  readonly label?: string;
  /** Fewer keys than the commands have: a double left out (Shift+V for V, Shift+! for Shift+1). */
  readonly keys?: readonly string[];
  /** The i18n key of a note under the label: the condition of the keys (a single step chosen takes them as its name). */
  readonly note?: string;
}

/** A key heard by its own handler, not by a command: each has its test (shortcutSheet.test.ts). */
export type OtherKey = 'space' | 'wheel' | 'zoom' | 'search' | 'enter' | 'menu' | 'type' | 'online' | 'saved';

export interface OtherLine {
  readonly other: OtherKey;
  readonly label: string;
  readonly keys: readonly string[];
  /** The i18n key of a text shown in place of the keys, when a key name would read as the letter itself. */
  readonly shown?: string;
}

export type SheetLine = CommandLine | OtherLine;

export interface SheetSection {
  /** The name of the section, `shortcuts.sections.<id>`. */
  readonly id: 'move' | 'choose' | 'edit' | 'display' | 'present' | 'help';
  /** The i18n key of a note under the name: who may use these keys. */
  readonly note?: string;
  readonly lines: readonly SheetLine[];
}

const keys = (...ids: string[]): CommandLine => ({ ids });

/**
 * The sections and their lines. Every key command of the map is on one line and one only, and every key shown is a
 * shortcut of its command (shortcutSheet.test.ts): a command added to the keyboard and not here fails the test.
 */
export const SHORTCUT_SHEET: readonly SheetSection[] = [
  {
    id: 'move',
    lines: [
      keys('level.up'),
      { ids: ['tool.hand'], keys: ['H'], note: 'shortcuts.alone_note' },
      { other: 'space', label: 'shortcuts.space_held', keys: ['Space'] },
      { other: 'wheel', label: 'shortcuts.wheel', keys: ['Wheel', 'Shift+Wheel'] },
      { other: 'zoom', label: 'shortcuts.zoom', keys: ['Ctrl+Wheel'] },
      { ids: ['fit'], keys: ['Shift+1'], note: 'shortcuts.alone_note' },
      keys('find'),
      { other: 'search', label: 'shortcuts.search_keys', keys: ['Enter', 'Shift+Enter'] },
    ],
  },
  {
    id: 'choose',
    lines: [
      { ids: ['tool.select'], keys: ['V'], note: 'shortcuts.alone_note' },
      keys('selectAll'),
      { other: 'enter', label: 'shortcuts.enter', keys: ['Enter'] },
      { other: 'menu', label: 'shortcuts.menu_key', keys: ['Shift+F10'] },
      keys('copy'),
      { ids: ['escape'], label: 'shortcuts.escape' },
    ],
  },
  {
    id: 'edit',
    note: 'shortcuts.edit_note',
    lines: [
      { other: 'type', label: 'shortcuts.type', keys: ['A'], shown: 'shortcuts.type_key' },
      keys('rename'),
      keys('undo.keys'),
      keys('redo.keys'),
      keys('cut'),
      keys('paste.keys'),
      keys('duplicate'),
      keys('group'),
      keys('ungroup'),
      keys('delete'),
      { ids: ['move.Left', 'move.Right', 'move.Up', 'move.Down'], label: 'shortcuts.move_selection' },
      keys('command'),
    ],
  },
  { id: 'display', lines: [keys('ribbon'), { ids: ['bpmn'], label: 'bpmn.toolbar' }, keys('assistant')] },
  { id: 'present', lines: [keys('present'), keys('present.from')] },
  {
    id: 'help',
    lines: [
      { ids: ['help.keys'], keys: ['?'], note: 'shortcuts.alone_note' },
      { other: 'online', label: 'app.help_online', keys: ['F1'] },
      { other: 'saved', label: 'shortcuts.saved', keys: ['Ctrl+S'] },
    ],
  },
];

const commandOf = (id: string) => KEY_COMMANDS.find((command) => command.id === id);

/** The identifier of a line: its first command, or its other key. */
export const lineId = (line: SheetLine): string => ('other' in line ? line.other : line.ids[0] ?? '');

/** The keys a line shows: its own, else every shortcut of its commands, in their order, each once. */
export function keysOf(line: SheetLine): readonly string[] {
  if (line.keys !== undefined) return line.keys;
  const all = 'ids' in line ? line.ids.flatMap((id) => commandOf(id)?.shortcuts ?? []) : [];
  return [...new Set(all)];
}

/** The i18n key of the label of a line: its own, else the label of its first command. */
export function labelOf(line: SheetLine): string {
  if (line.label !== undefined) return line.label;
  return 'ids' in line ? commandOf(line.ids[0] ?? '')?.label ?? '' : '';
}
