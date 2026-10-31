// commands.ts
// Commands as data: one definition per action, read by the ribbon, the menus and the keyboard, testable without React (spec 3.5).

import type { LucideIcon } from 'lucide-react';
import type { KeyHandler } from '../keyboard/keyboard.ts';

/**
 * One action of the interface (S-3.5-01): its identifier, the i18n key of its label, its icon, its shortcuts (chords of
 * {@link chordOf}, the first one shown), the condition that enables it and what it does, all read from what the
 * context of its screen gives (a map, a selection, the rights).
 */
export interface Command<Context> {
  readonly id: string;
  readonly label: string;
  readonly icon?: LucideIcon;
  readonly shortcuts?: readonly string[];
  readonly enabled: (context: Context) => boolean;
  readonly run: (context: Context) => void;
  /** Whether the command is offered at all (an entry of a menu only for a detailed step); offered by default. */
  readonly visible?: (context: Context) => boolean;
  /** The value of a choice or a switch: the item is a radio or a pressed button, checked when true (B-2.4-05). */
  readonly checked?: (context: Context) => boolean;
  /** An action that removes, shown in red (B-2.4-07). */
  readonly danger?: boolean;
  /** The values of the label (`Copier le code {{code}}`, a count for the plural). */
  readonly labelValues?: (context: Context) => Readonly<Record<string, string | number>>;
  /** The colour a choice shows in a dot before its label (the colour of a lane, B-2.4-48). */
  readonly swatch?: string;
}

/** A command as a menu or a toolbar shows it: label key, icon, shortcut shown, and whether it can be chosen. */
export interface CommandItem {
  readonly id: string;
  readonly label: string;
  readonly icon: LucideIcon | undefined;
  readonly shortcut: string | undefined;
  readonly disabled: boolean;
  /** Undefined for an action, else whether the choice or the switch is on. */
  readonly checked?: boolean | undefined;
  readonly danger?: boolean;
  readonly labelValues?: Readonly<Record<string, string | number>> | undefined;
  readonly swatch?: string;
  /** A thin line before the item: it opens another group of its menu (B-2.4-08). */
  readonly separatorBefore?: boolean;
}

/** The command a chord runs, among a list (the first that declares it). */
export function commandOf<Context>(commands: readonly Command<Context>[], chord: string): Command<Context> | undefined {
  return commands.find((command) => command.shortcuts?.includes(chord) === true);
}

/**
 * Runs a command when it is enabled; false when it is not, so that the caller leaves the key or the click alone (a
 * disabled shortcut keeps the default action of the browser, as in the 1.x).
 */
export function runCommand<Context>(command: Command<Context>, context: Context): boolean {
  if (!command.enabled(context)) return false;
  command.run(context);
  return true;
}

/** The handler of a layer of the keyboard that runs the commands of a list on the key pressed (B-2.5-01: one manager). */
export function keyHandlerOf<Context>(commands: readonly Command<Context>[], contextOf: () => Context): KeyHandler {
  return (event, chord) => {
    if (event.type !== 'keydown') return false;
    const command = commandOf(commands, chord);
    return command !== undefined && runCommand(command, contextOf());
  };
}

/**
 * The items of a toolbar, in the order of the identifiers given, without the commands not visible (the RACI matrix without
 * its layer, B-2.1-32); an unknown identifier is a programming error.
 */
export function itemsOf<Context>(commands: readonly Command<Context>[], ids: readonly string[], context: Context): CommandItem[] {
  return ids.map((id) => commandById(commands, id)).filter((command) => command.visible?.(context) !== false).map((command) => itemOf(command, context));
}

function commandById<Context>(commands: readonly Command<Context>[], id: string): Command<Context> {
  const command = commands.find((candidate) => candidate.id === id);
  if (command === undefined) throw new Error(`Unknown command ${id}.`);
  return command;
}

function itemOf<Context>(command: Command<Context>, context: Context): CommandItem {
  return {
    id: command.id, label: command.label, icon: command.icon, shortcut: command.shortcuts?.[0], disabled: !command.enabled(context),
    checked: command.checked?.(context), labelValues: command.labelValues?.(context), ...(command.danger === true ? { danger: true } : {}), ...(command.swatch === undefined ? {} : { swatch: command.swatch }),
  };
}

/** A line of a menu: its command, its group (a line between two groups), and when the menu offers it besides the command's own condition. */
export interface MenuLine<Context> {
  readonly id: string;
  readonly group: number;
  readonly when?: (context: Context) => boolean;
}

/**
 * The items of a menu, in the order of its lines: only the lines offered and whose command is visible, a separator
 * before the first item of each group after the first one (B-2.4-08).
 */
export function menuItemsOf<Context>(commands: readonly Command<Context>[], lines: readonly MenuLine<Context>[], context: Context): CommandItem[] {
  const shown = lines.filter((line) => line.when?.(context) !== false && commandById(commands, line.id).visible?.(context) !== false);
  return shown.map((line, index) => ({ ...itemOf(commandById(commands, line.id), context), separatorBefore: index > 0 && shown[index - 1]?.group !== line.group }));
}

/**
 * The shortcut as the menus write it: always `Ctrl`, on a Mac too (B-2.5-36); the names of the keys that differ by
 * language (Shift is `Maj` in French) come from the translations given.
 */
export function shortcutLabel(chord: string, keyNames: Readonly<Record<string, string>> = {}): string {
  return chord.split('+').map((part) => keyNames[part] ?? part).join('+');
}
