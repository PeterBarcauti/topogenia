// viewCommands.ts
// The commands of the view: Show all, the whole map in the window, by the button next to the zoom and by Shift+1; the panel of the keyboard shortcuts by ?; the search in the project by Ctrl+F (ADR 110); for every role (lots 1 and 2 of the 3.4).

import { Keyboard, Maximize, Search } from 'lucide-react';
import type { Command } from '../../../../shared/commands/commands.ts';
import { shortcutsPanel } from '../../../../shared/state/shortcutsPanel.ts';
import { askOf, openSearch } from '../../search/useSearch.ts';
import type { MapContext } from './context.ts';

export const VIEW_COMMANDS: readonly Command<MapContext>[] = [
  {
    // Shift+1 reads « 1 » on a French keyboard and « ! » on most others: the keyboard manager reads the character (same as the digits of the presentation).
    id: 'fit', label: 'map.fit', icon: Maximize, shortcuts: ['Shift+1', 'Shift+!'], enabled: () => true,
    run: (context) => {
      context.editor.canvas.fit();
    },
  },
  {
    // « ? » is Shift+, on a French keyboard and Shift+/ on an English one; with one step alone chosen, it begins its name (AR-151).
    id: 'help.keys', label: 'shortcuts.title', icon: Keyboard, shortcuts: ['Shift+?', '?'], enabled: () => true,
    run: () => {
      shortcutsPanel.getState().setOpen(true);
    },
  },
  {
    // Not a character typed: it works with one step alone chosen too. In a field (the search field included) and under a
    // dialog the keyboard manager leaves it to the browser, whose own search a second Ctrl+F opens (ADR 110 point 6).
    id: 'find', label: 'search.command_help', icon: Search, shortcuts: ['Ctrl+F'], enabled: () => true,
    run: (context) => {
      const ask = askOf(context.editor);
      if (ask === null) return;
      context.editor.dock.getState().fold('search', false);
      openSearch(ask);
    },
  },
];
