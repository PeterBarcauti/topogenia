// streamCommands.ts
// The entries of the value stream in the menus of the map (spec 14.6, ADR 085): « Volume entrant » on a start, « Part des dossiers » on an exit of a decision or an inclusive gateway, « Effectif » on a lane of people; for who may change the structure, a reader sees the figures and enters nothing.

import { LogIn, Percent, UsersRound } from 'lucide-react';
import type { Command } from '../../../../shared/commands/commands.ts';
import type { Point } from '../../model/geometry/grid.ts';
import type { StreamEntry } from '../floating.ts';
import { stepOf, type MapContext } from './context.ts';

/** The gateways whose exits carry a share of the cases (S2). */
const SHARING = new Set(['decision', 'inclusive']);

function flowOf(context: MapContext) {
  const { target } = context;
  return target?.kind === 'flow' ? context.map.flows.find((flow) => flow.id === target.flowId) : undefined;
}

function laneOf(context: MapContext) {
  const { target } = context;
  return target?.kind === 'lane' ? context.map.lanes.find((lane) => lane.id === target.laneId) : undefined;
}

/** The field opens where the menu was opened, else at the pointer, else at the top left of the window. */
function pointOf(context: MapContext): Point {
  const { target } = context;
  const at = target?.kind === 'step' || target?.kind === 'flow' || target?.kind === 'lane' ? target.at : undefined;
  return at ?? context.pointer ?? { x: 16, y: 16 };
}

function open(context: MapContext, entry: StreamEntry): void {
  context.editor.floating.getState().open({ kind: 'stream-entry', entry, at: pointOf(context) });
}

export const STREAM_COMMANDS: readonly Command<MapContext>[] = [
  {
    id: 'step.volume', label: 'menu.step_volume', icon: LogIn, enabled: (context) => context.canEdit,
    visible: (context) => context.target?.kind === 'step' && stepOf(context)?.type === 'start',
    run: (context) => {
      const step = stepOf(context);
      if (step !== undefined) open(context, { kind: 'volume', stepId: step.id });
    },
  },
  {
    id: 'flow.share', label: 'menu.flow_share', icon: Percent, enabled: (context) => context.canEdit,
    visible: (context) => {
      const flow = flowOf(context);
      return flow !== undefined && SHARING.has(context.map.steps.find((step) => step.id === flow.source_step_id)?.type ?? '');
    },
    run: (context) => {
      const flow = flowOf(context);
      if (flow !== undefined) open(context, { kind: 'share', flowId: flow.id });
    },
  },
  {
    id: 'lane.headcount', label: 'menu.lane_headcount', icon: UsersRound, enabled: (context) => context.canEdit,
    visible: (context) => {
      const lane = laneOf(context);
      return lane !== undefined && lane.type !== 'system';
    },
    run: (context) => {
      const lane = laneOf(context);
      if (lane !== undefined) open(context, { kind: 'headcount', laneId: lane.id });
    },
  },
];
