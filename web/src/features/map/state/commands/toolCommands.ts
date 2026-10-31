// toolCommands.ts
// The commands of the level, the tools, the shapes of the next steps, the links, the command bar and the display: radio choices and switches of the ribbon, the RACI matrix, the value stream layer (annex B 2.1, 2.5, 2.11, spec 14.5, AR-90, AR-155).

import { CornerLeftUp, GitBranchPlus, Hand, Hash, MousePointer2, PanelTopClose, Shapes, Spline, SquareTerminal, Table2, UsersRound } from 'lucide-react';
import type { Schema } from '../../../../api/client.ts';
import { paths } from '../../../../app/router.ts';
import type { Command } from '../../../../shared/commands/commands.ts';
import { SHAPE_ICONS, VALUE_STREAM_ICON } from '../../chrome/shapeIcons.ts';
import { hasContent } from '../../model/stepFacts.ts';
import { insertStep } from '../actions/steps.ts';
import type { PreferenceName } from '../preferences.ts';
import { stepOf, type MapContext } from './context.ts';

type StepType = Schema<'StepType'>;

/** The shapes of the zone Shapes and the diamonds of the zone Gateways (B-2.1-14, B-2.1-16). */
export const SHAPES: readonly StepType[] = ['task', 'wait', 'start', 'end'];
export const GATEWAYS: readonly StepType[] = ['decision', 'parallel', 'inclusive'];

const tool = (id: 'select' | 'hand', icon: typeof Hand, key: string): Command<MapContext> => ({
  id: `tool.${id}`, label: `ribbon.${id}`, icon, shortcuts: [key, `Shift+${key}`], enabled: () => true,
  checked: (context) => context.state.tool === id,
  run: (context) => {
    context.editor.store.setState({ tool: id });
  },
});

/** The type of the next step only, without effect on the steps chosen (B-2.1-15, B-2.1-17, AR-152). */
const shape = (type: StepType): Command<MapContext> => ({
  id: `shape.${type}`, label: `map.step_types.${type}`, icon: SHAPE_ICONS[type], enabled: (context) => context.canEdit,
  checked: (context) => context.state.newStepType === type,
  run: (context) => {
    context.editor.store.setState({ newStepType: type });
  },
});

const toggle = (id: string, label: string, icon: typeof Hash, name: PreferenceName, shortcuts?: readonly string[]): Command<MapContext> => ({
  id, label, icon, ...(shortcuts === undefined ? {} : { shortcuts }), enabled: () => true,
  checked: (context) => context.preferences[name],
  run: (context) => {
    context.editor.preferences.getState().set(name, !context.preferences[name]);
  },
});

/** The map above the one shown, read from its breadcrumb; none for the root map. */
const parentMap = (context: MapContext): string | undefined => context.map.breadcrumb.at(-2)?.map_id;

/** The step that may receive a branch: one step with content, not an event nor a joining gateway (B-2.1-18, B-2.1-19). */
const branchable = (context: MapContext) => {
  const step = stepOf(context);
  return step !== undefined && hasContent(step.type) ? step : undefined;
};

export const TOOL_COMMANDS: readonly Command<MapContext>[] = [
  // Back from a detailed map to the map above, for every reader; Alt+Up as the folder above in the Explorer of Windows (AR-155).
  { id: 'level.up', label: 'ribbon.level_up', icon: CornerLeftUp, shortcuts: ['Alt+ArrowUp'], enabled: (context) => parentMap(context) !== undefined,
    run: (context) => {
      const parent = parentMap(context);
      if (parent !== undefined) context.editor.navigate(paths.map(parent));
    } },
  tool('select', MousePointer2, 'V'),
  tool('hand', Hand, 'H'),
  ...SHAPES.map(shape),
  ...GATEWAYS.map(shape),
  {
    id: 'branch', label: 'menu.insert_parallel', icon: GitBranchPlus,
    enabled: (context) => context.canEdit && branchable(context) !== undefined,
    visible: (context) => context.target === null || branchable(context) !== undefined,
    run: (context) => {
      const step = branchable(context);
      if (step !== undefined) void insertStep(context.editor, step.id, 'parallel');
    },
  },
  {
    id: 'link', label: 'ribbon.link', icon: Spline, enabled: (context) => context.canEdit, checked: (context) => context.state.mode === 'link',
    run: (context) => {
      context.editor.store.setState({ mode: context.state.mode === 'link' ? 'normal' : 'link', linkSource: null });
    },
  },
  {
    id: 'command', label: 'ribbon.command', icon: SquareTerminal, shortcuts: ['Ctrl+K'], enabled: (context) => context.canEdit,
    run: (context) => {
      context.editor.floating.getState().open({ kind: 'command' });
    },
  },
  toggle('codes', 'ribbon.codes', Hash, 'showCodes'),
  toggle('pictos', 'ribbon.pictos', Shapes, 'alwaysShowPictos'),
  toggle('raci', 'ribbon.raci', UsersRound, 'showRaci'),
  /** The value stream layer, next to the RACI one, which it turns off (spec 14.5). */
  toggle('stream', 'ribbon.value_stream', VALUE_STREAM_ICON, 'showValueStream'),
  /** The matrix of the map, offered with the RACI layer only (B-2.1-32, B-2.11-14). */
  {
    id: 'matrix', label: 'ribbon.matrix', icon: Table2, enabled: () => true, visible: (context) => context.preferences.showRaci,
    run: (context) => {
      context.editor.floating.getState().open({ kind: 'matrix' });
    },
  },
  { ...toggle('ribbon', 'ribbon.fold', PanelTopClose, 'ribbonCollapsed', ['Ctrl+F1']), checked: undefined },
];
