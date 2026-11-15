// presentCommands.ts
// The commands that start a presentation (ADR 097): from the start of the tour (F5, as PowerPoint), or from the step chosen (Shift+F5, the menu of a step); a reader presents too.

import { Presentation } from 'lucide-react';
import type { Command } from '../../../../shared/commands/commands.ts';
import { stepsOf, type MapContext } from './context.ts';

const hasSteps = (context: MapContext): boolean => context.map.steps.length > 0;

export const PRESENT_COMMANDS: readonly Command<MapContext>[] = [
  {
    id: 'present', label: 'presenter.present', icon: Presentation, shortcuts: ['F5'], enabled: hasSteps,
    run: (context) => {
      context.editor.presenter.getState().start(null);
    },
  },
  {
    id: 'present.from', label: 'presenter.present_from', icon: Presentation, shortcuts: ['Shift+F5'], enabled: hasSteps,
    run: (context) => {
      context.editor.presenter.getState().start(stepsOf(context)[0] ?? null);
    },
  },
];
