// commands.test.ts
// Commands as data, without React: one definition read by the keyboard, the menus and the toolbars; the shortcuts shown (S-3.5-01, annex B 2.5).

import { Copy, Undo2 } from 'lucide-react';
import { createKeyboard } from '../keyboard/keyboard.ts';
import { commandOf, itemsOf, keyHandlerOf, runCommand, shortcutLabel, type Command } from './commands.ts';

interface Context {
  readonly editable: boolean;
  readonly selected: number;
  readonly done: string[];
}

const COMMANDS: readonly Command<Context>[] = [
  { id: 'undo', label: 'ribbon.undo', icon: Undo2, shortcuts: ['Ctrl+Z'], enabled: (c) => c.editable, run: (c) => c.done.push('undo') },
  { id: 'redo', label: 'ribbon.redo', shortcuts: ['Ctrl+Y', 'Ctrl+Shift+Z'], enabled: (c) => c.editable, run: (c) => c.done.push('redo') },
  { id: 'copy', label: 'ribbon.copy', icon: Copy, shortcuts: ['Ctrl+C'], enabled: (c) => c.selected > 0, run: (c) => c.done.push('copy') },
  { id: 'layer', label: 'ribbon.layer', enabled: () => true, run: (c) => c.done.push('layer') },
];

const context = (editable: boolean, selected = 0): Context => ({ editable, selected, done: [] });

const key = (init: KeyboardEventInit, type = 'keydown') => new KeyboardEvent(type, { bubbles: true, cancelable: true, ...init });

describe('a command (S-3.5-01)', () => {
  it('is found by any of its shortcuts', () => {
    expect(commandOf(COMMANDS, 'Ctrl+Shift+Z')?.id).toBe('redo');
    expect(commandOf(COMMANDS, 'Ctrl+Y')?.id).toBe('redo');
    expect(commandOf(COMMANDS, 'Ctrl+Q')).toBeUndefined();
  });

  it('runs only when its condition holds, and says whether it ran', () => {
    const reader = context(false);
    const undo = COMMANDS[0];
    if (undo === undefined) throw new Error('No command.');
    expect(runCommand(undo, reader)).toBe(false);
    const facilitator = context(true);
    expect(runCommand(undo, facilitator)).toBe(true);
    expect([reader.done, facilitator.done]).toEqual([[], ['undo']]);
  });
});

describe('the keyboard runs the same definitions (B-2.5-01)', () => {
  it('a shortcut runs its command when it is enabled, on the key pressed only', () => {
    const state = context(true, 2);
    const handler = keyHandlerOf(COMMANDS, () => state);
    expect(handler(key({ key: 'z', ctrlKey: true }), 'Ctrl+Z')).toBe(true);
    expect(handler(key({ key: 'z', ctrlKey: true, shiftKey: true }), 'Ctrl+Shift+Z')).toBe(true);
    expect(handler(key({ key: 'c', ctrlKey: true }, 'keyup'), 'Ctrl+C')).toBe(false);
    expect(state.done).toEqual(['undo', 'redo']);
  });

  it('a disabled shortcut is not used: its key keeps the action of the browser', () => {
    const keyboard = createKeyboard();
    const detach = keyboard.attach(window);
    const state = context(false, 0);
    keyboard.add('canvas', keyHandlerOf(COMMANDS, () => state));
    const event = key({ key: 'c', ctrlKey: true });
    document.body.dispatchEvent(event);
    detach();
    expect(event.defaultPrevented).toBe(false);
    expect(state.done).toEqual([]);
  });

  it('reads the context of the moment, not the one of the registration', () => {
    let state = context(false);
    const handler = keyHandlerOf(COMMANDS, () => state);
    expect(handler(key({ key: 'z', ctrlKey: true }), 'Ctrl+Z')).toBe(false);
    state = context(true);
    expect(handler(key({ key: 'z', ctrlKey: true }), 'Ctrl+Z')).toBe(true);
  });
});

describe('the menus and toolbars read the same definitions', () => {
  it('give the items in the order asked, with label, icon, first shortcut and state', () => {
    expect(itemsOf(COMMANDS, ['copy', 'undo', 'layer'], context(false, 1))).toEqual([
      { id: 'copy', label: 'ribbon.copy', icon: Copy, shortcut: 'Ctrl+C', disabled: false },
      { id: 'undo', label: 'ribbon.undo', icon: Undo2, shortcut: 'Ctrl+Z', disabled: true },
      { id: 'layer', label: 'ribbon.layer', icon: undefined, shortcut: undefined, disabled: false },
    ]);
  });

  it('refuse an unknown identifier', () => {
    expect(() => itemsOf(COMMANDS, ['paste'], context(true))).toThrow('Unknown command paste.');
  });

  it('leave out of a toolbar a command not visible in the context (B-2.1-32)', () => {
    const shown: Command<Context> = { id: 'shown', label: 'ribbon.shown', enabled: () => true, visible: (c) => c.editable, run: () => undefined };
    expect(itemsOf([...COMMANDS, shown], ['copy', 'shown'], context(false)).map((item) => item.id)).toEqual(['copy']);
    expect(itemsOf([...COMMANDS, shown], ['copy', 'shown'], context(true)).map((item) => item.id)).toEqual(['copy', 'shown']);
  });
});

describe('the shortcut shown (B-2.5-36)', () => {
  it('always writes Ctrl, and the names of the keys of the language', () => {
    expect(shortcutLabel('Ctrl+Shift+Z')).toBe('Ctrl+Shift+Z');
    expect(shortcutLabel('Ctrl+Shift+Z', { Shift: 'Maj' })).toBe('Ctrl+Maj+Z');
    expect(shortcutLabel('Delete', { Delete: 'Suppr' })).toBe('Suppr');
  });
});
