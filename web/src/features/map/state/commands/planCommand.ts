// planCommand.ts
// The command « Plan d'actions » of the zone Display (ADR 112 point 5): a switch, checked while the panel is open, that opens and closes the action plan of the project, for every role and in the view in BPMN 2.0; no shortcut.

import { ClipboardList } from 'lucide-react';
import type { Command } from '../../../../shared/commands/commands.ts';
import { planStore } from '../../plan/planStore.ts';
import { closePlan, openPlan, planShownIn } from '../../plan/usePlan.ts';
import type { MapContext } from './context.ts';

export const PLAN_COMMAND: Command<MapContext> = {
  id: 'plan', label: 'action_plan.command', icon: ClipboardList, enabled: () => true,
  checked: (context) => planShownIn(planStore.getState(), context.map.project_id),
  run: (context) => {
    if (planShownIn(planStore.getState(), context.map.project_id)) {
      closePlan();
      return;
    }
    context.editor.dock.getState().fold('plan', false);
    openPlan(context.map);
  },
};
