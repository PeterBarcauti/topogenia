// assistantCommand.ts
// The command that opens and closes the panel of the assistant: the button at the right margin of the ribbon and Ctrl+I (spec 3.0 section 3.4).

import { Sparkles } from 'lucide-react';
import type { Command } from '../../../../shared/commands/commands.ts';
import type { MapContext } from './context.ts';

/**
 * Ctrl+I, free on the map (I for the AI, « IA » in French): the same key opens and closes the panel. Every member may open
 * it: a reader asks and analyses, a facilitator also proposes (spec 3.0 section 3.3, revision 4).
 */
export const ASSISTANT_COMMAND: Command<MapContext> = {
  id: 'assistant', label: 'assistant.panel.name', icon: Sparkles, shortcuts: ['Ctrl+I'], enabled: () => true,
  checked: (context) => context.editor.assistant.getState().open,
  run: (context) => {
    const panel = context.editor.assistant.getState();
    panel.setOpen(!panel.open);
  },
};
