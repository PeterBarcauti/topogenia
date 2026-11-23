// bpmnCommands.ts
// The command of the view in BPMN 2.0 (ADR 106): the map as a diagram read only (Alt+B, the Display zone of the ribbon, every member); its export as a .bpmn is in the File menu (fileCommands.ts).

import { Workflow } from 'lucide-react';
import type { Command } from '../../../../shared/commands/commands.ts';
import { bpmnView } from '../../bpmn/bpmnState.ts';
import type { MapContext } from './context.ts';

const hasSteps = (context: MapContext): boolean => context.map.steps.length > 0;

export const BPMN_COMMANDS: readonly Command<MapContext>[] = [
  {
    id: 'bpmn', label: 'bpmn.view', icon: Workflow, shortcuts: ['Alt+B'], enabled: hasSteps, checked: () => bpmnView.getState().open,
    run: (context) => {
      // A gesture under way stops: the link mode, the field of a name, the panel of the RACI (spec of the lot, 5.1).
      context.editor.store.setState({ mode: 'normal', linkSource: null });
      context.editor.floating.getState().close();
      bpmnView.getState().setOpen(!bpmnView.getState().open);
    },
  },
];
