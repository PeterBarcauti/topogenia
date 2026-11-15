// menuCommands.ts
// The commands offered only by the context menus: on a step, a selection, the background, a flow, a lane (annex B 2.4).

import {
  ArrowDown, ArrowLeft, ArrowLeftFromLine, ArrowLeftRight, ArrowRight, ArrowRightFromLine, ArrowUp, BetweenHorizontalEnd, BetweenHorizontalStart, ClipboardPaste, ExternalLink,
  FoldVertical, Group, Hash, PanelRightOpen, Pencil, PencilLine, Route, RouteOff, SquareStack, SquareX, Trash2, Ungroup, UsersRound,
} from 'lucide-react';
import type { Schema } from '../../../../api/client.ts';
import { paths } from '../../../../app/router.ts';
import type { Command } from '../../../../shared/commands/commands.ts';
import { i18next } from '../../../../shared/i18n/i18n.ts';
import { LANE_TINTS } from '../../../../shared/theme/laneTints.ts';
import { LANE_ICONS, SHAPE_ICONS } from '../../chrome/shapeIcons.ts';
import { isEmpty, ROUTE_CHOICES, withSide } from '../../model/flowRoutes.ts';
import { allowedSides } from '../../model/routeGestures.ts';
import { roomOf, shrinking, type LaneEnd } from '../../model/laneRows.ts';
import { hasContent, isMacro } from '../../model/stepFacts.ts';
import { deleteLane, growLane, shrinkLane, updateLane } from '../actions/lanes.ts';
import { autoRoutes, reverseFlow, routeShown, setFlowRoute } from '../actions/flows.ts';
import { paste } from '../actions/paste.ts';
import { deleteChosen, deleteSelection } from '../actions/selection.ts';
import { raciPanelPoint } from '../actions/stepActivation.ts';
import { deleteDetail, detailStep, insertStep, updateStep, type InsertPosition } from '../actions/steps.ts';
import { cellAimed, flowsOf, isCut, stepOf, stepsOf, type MapContext } from './context.ts';
import { groupable, groupChosen, ungroupable, ungroupChosen } from './editCommands.ts';

type StepType = Schema<'StepType'>;

/** The seven types of a step, in the order of the menu (B-2.4-25). */
export const STEP_TYPES: readonly StepType[] = ['task', 'wait', 'start', 'end', 'decision', 'parallel', 'inclusive'];

/** The value classes a step takes in the menu, then « Not classified » (Makigami M1, plan 3.1). */
export const VALUE_CHOICES = ['value_added', 'necessary', 'waste', 'none'] as const;
export type ValueChoice = (typeof VALUE_CHOICES)[number];
export const LANE_TYPES: readonly Schema<'LaneType'>[] = ['person', 'department', 'system'];

const editing = (context: MapContext) => context.canEdit;
const targetStep = (context: MapContext) => (context.target?.kind === 'step' ? stepOf(context) : undefined);
const targetLane = (context: MapContext) => {
  const { target } = context;
  return target?.kind === 'lane' ? context.map.lanes.find((lane) => lane.id === target.laneId) : undefined;
};

const insert = (position: InsertPosition, label: string): Command<MapContext> => ({
  id: `step.insert-${position}`, label, icon: position === 'before' ? ArrowLeftFromLine : ArrowRightFromLine, enabled: editing,
  run: (context) => {
    const step = targetStep(context);
    if (step !== undefined) void insertStep(context.editor, step.id, position);
  },
});

/** Paste before or after the step of the menu, by an anchor of the flow; not when it is the step cut (B-2.4-22 to B-2.4-24). */
const pasteBy = (position: 'before' | 'after'): Command<MapContext> => ({
  id: `paste.${position}`, label: `menu.paste_${position}`, icon: ClipboardPaste, ...(position === 'after' ? { shortcuts: ['Ctrl+V'] } : {}),
  enabled: (context) => context.canEdit && context.clipboard !== null && targetStep(context) !== undefined && !isCut(context, targetStep(context)?.id ?? ''),
  run: (context) => {
    const step = targetStep(context);
    if (step !== undefined) void paste(context.editor, { stepId: step.id, position });
  },
});

/** « Changer le type : X », the type of the moment marked; a detailed step keeps a type with content (B-2.4-25, B-2.4-26). */
const changeType = (type: StepType): Command<MapContext> => ({
  id: `step.type.${type}`, label: 'menu.change_type', icon: SHAPE_ICONS[type], labelValues: () => ({ type: i18next.t(`map.step_types.${type}`) }),
  checked: (context) => targetStep(context)?.type === type,
  enabled: (context) => {
    const step = targetStep(context);
    return context.canEdit && step !== undefined && !(isMacro(step) && !hasContent(type));
  },
  run: (context) => {
    const step = targetStep(context);
    if (step !== undefined) void updateStep(context.editor, step.id, { type });
  },
});

/**
 * « Valeur : X » under the layer « Analysis » (Makigami M1): a step with content that is not detailed, whose value comes
 * from its detail; the class checked, or « Not classified ».
 */
const classify = (choice: ValueChoice): Command<MapContext> => ({
  id: `step.value.${choice}`, label: `menu.value.${choice}`,
  checked: (context) => (targetStep(context)?.value_class ?? 'none') === choice,
  enabled: (context) => context.canEdit,
  visible: (context) => {
    const step = targetStep(context);
    return context.preferences.showAnalysis && step !== undefined && hasContent(step.type) && !isMacro(step);
  },
  run: (context) => {
    const step = targetStep(context);
    if (step !== undefined) void updateStep(context.editor, step.id, { valueClass: choice === 'none' ? null : choice });
  },
});

/** A step whose sheet the menu offers: one with content (B-2.3-13); its RACI roles too, for who may change the map, on the layer (B-2.3-12, NF08). */
const withSheet = (context: MapContext) => {
  const step = targetStep(context);
  return step !== undefined && hasContent(step.type);
};

const STEP_COMMANDS: readonly Command<MapContext>[] = [
  { id: 'step.open', label: 'menu.open_detail', icon: ExternalLink, enabled: () => true, visible: (context) => targetStep(context) !== undefined && isMacro(targetStep(context) as Schema<'StepView'>),
    run: (context) => {
      const subMap = targetStep(context)?.sub_map_id;
      if (subMap != null) context.editor.navigate(paths.map(subMap));
    } },
  // Enter on the step chosen opens its sheet too: shown in front of the entry, as F2 in front of Renommer (AR-151, AR-155).
  { id: 'step.sheet', label: 'menu.open_sheet', icon: PanelRightOpen, shortcuts: ['Enter'], enabled: () => true, visible: withSheet,
    run: (context) => {
      context.editor.store.setState({ openSheet: targetStep(context)?.id ?? null });
    } },
  { id: 'step.raci', label: 'menu.raci_roles', icon: UsersRound, enabled: editing, visible: (context) => context.canEdit && context.preferences.showRaci && withSheet(context),
    run: (context) => {
      const { target } = context;
      if (target?.kind === 'step') context.editor.floating.getState().open({ kind: 'raci', stepId: target.stepId, at: raciPanelPoint(target.at ?? { x: 0, y: 0 }) });
    } },
  insert('before', 'menu.insert_before'),
  insert('after', 'menu.insert_after'),
  { id: 'step.detail', label: 'menu.detail', icon: SquareStack, enabled: editing,
    visible: (context) => {
      const step = targetStep(context);
      return step !== undefined && !isMacro(step) && hasContent(step.type);
    },
    run: (context) => {
      const step = targetStep(context);
      if (step !== undefined) void detailStep(context.editor, step.id);
    } },
  // « Dissocier le sous-processus », the inverse of « Grouper en sous-processus » (AR-154): the detail comes back into the flow.
  { id: 'step.dissolve', label: 'menu.dissolve', icon: Ungroup, shortcuts: ['Ctrl+Shift+G'], enabled: editing, visible: ungroupable, run: ungroupChosen },
  { id: 'step.delete-detail', label: 'menu.delete_detail', icon: SquareX, danger: true, enabled: editing, visible: (context) => targetStep(context)?.sub_map_id != null,
    run: (context) => {
      const step = targetStep(context);
      if (step === undefined) return;
      if (isMacro(step)) context.editor.floating.getState().open({ kind: 'detail-delete', stepId: step.id });
      else void deleteDetail(context.editor, step.id);
    } },
  pasteBy('before'),
  pasteBy('after'),
  ...STEP_TYPES.map(changeType),
  ...VALUE_CHOICES.map(classify),
  { id: 'step.copy-code', label: 'menu.copy_code', icon: Hash, labelValues: (context) => ({ code: targetStep(context)?.code ?? '' }), enabled: () => true,
    run: (context) => {
      const code = targetStep(context)?.code ?? '';
      void navigator.clipboard.writeText(code).then(() => {
        context.editor.notify(i18next.t('map.code_copied', { code }), 'success');
      });
    } },
  { id: 'step.delete', label: 'menu.delete', icon: Trash2, shortcuts: ['Delete'], danger: true, enabled: editing,
    run: (context) => {
      deleteChosen(context.editor, stepsOf(context), []);
    } },
];

const SELECTION_COMMANDS: readonly Command<MapContext>[] = [
  { id: 'selection.group', label: 'menu.group_selection', icon: Group, shortcuts: ['Ctrl+G'], enabled: groupable, run: groupChosen },
  { id: 'selection.route.auto', label: 'menu.route_auto_selection', icon: RouteOff,
    enabled: (context) => editing(context) && routedOf(context).length > 0, run: (context) => void autoRoutes(context.editor, routedOf(context)) },
  { id: 'selection.delete', label: 'menu.delete_steps', labelValues: (context) => ({ count: stepsOf(context).length }), icon: Trash2, shortcuts: ['Delete'], danger: true,
    enabled: editing, run: (context) => {
      deleteChosen(context.editor, stepsOf(context), []);
    } },
  { id: 'paste.here', label: 'menu.paste_here', icon: ClipboardPaste, shortcuts: ['Ctrl+V'],
    enabled: (context) => context.canEdit && context.clipboard !== null && context.target?.kind === 'pane' && cellAimed(context, context.target.point) !== null,
    run: (context) => {
      const cell = context.target?.kind === 'pane' ? cellAimed(context, context.target.point) : null;
      if (cell !== null) void paste(context.editor, { cell });
    } },
  { id: 'flow.label', label: 'menu.flow_label', icon: Pencil, enabled: editing,
    run: (context) => {
      if (context.target?.kind === 'flow') context.editor.floating.getState().open({ kind: 'flow-label', flowId: context.target.flowId, at: context.target.at });
    } },
  { id: 'flow.reverse', label: 'menu.reverse_flow', icon: ArrowLeftRight, enabled: editing,
    run: (context) => {
      if (context.target?.kind === 'flow') void reverseFlow(context.editor, context.target.flowId);
    } },
  { id: 'flow.delete', label: 'menu.delete_flow', icon: Trash2, danger: true, enabled: editing,
    run: (context) => {
      if (context.target?.kind === 'flow') void deleteSelection(context.editor, [], [context.target.flowId]);
    } },
];

/** The arrow of each choice of side of a route, the drawing as a route of its own (ADR 091). */
const SIDE_ICONS = { auto: Route, top: ArrowUp, bottom: ArrowDown, left: ArrowLeft, right: ArrowRight } as const;

const flowTargeted = (context: MapContext): string | null => (context.target?.kind === 'flow' ? context.target.flowId : null);

/** The flows of the selection that carry a choice of route: those « Tracé automatique des flèches » gives back to the drawing. */
const routedOf = (context: MapContext): readonly string[] =>
  flowsOf(context).filter((id) => context.map.flows.some((flow) => flow.id === id && flow.route != null));

/**
 * The departure and the arrival of a flow, side by side, and « Tracé automatique » (ADR 091): each choice one operation;
 * on a gateway, a point another exit chose is not offered (AR-24).
 */
const ROUTE_COMMANDS: readonly Command<MapContext>[] = [
  ...(['from', 'to'] as const).flatMap((end) => ROUTE_CHOICES.map((side): Command<MapContext> => {
    const at = end === 'from' ? 'source' : 'target';
    return {
      id: `flow.${end}.${side}`, label: end === 'from' ? 'menu.route_departure' : 'menu.route_arrival', icon: SIDE_ICONS[side],
      labelValues: () => ({ side: i18next.t(`map.route_sides.${side}`) }),
      enabled: (context) => {
        const flowId = flowTargeted(context);
        return editing(context) && flowId !== null && (side === 'auto' || allowedSides(context.map, flowId, at, context.editor.canvas.drawn()).includes(side));
      },
      checked: (context) => {
        const flowId = flowTargeted(context);
        return flowId !== null && (routeShown(context.editor, flowId)?.[at] ?? 'auto') === side;
      },
      run: (context) => {
        const flowId = flowTargeted(context);
        if (flowId !== null) void setFlowRoute(context.editor, flowId, (route) => withSide(route, at, side === 'auto' ? null : side));
      },
    };
  })),
  { id: 'flow.route.auto', label: 'menu.route_auto', icon: RouteOff,
    enabled: (context) => {
      const flowId = flowTargeted(context);
      return editing(context) && flowId !== null && !isEmpty(routeShown(context.editor, flowId));
    },
    run: (context) => {
      const flowId = flowTargeted(context);
      if (flowId !== null) void setFlowRoute(context.editor, flowId, () => undefined);
    } },
];

/**
 * A row more at the top or the bottom of a lane, what a pull of its border gives, and its empty first or last row taken
 * out, what a double click on its border does, each by a click (ADR 096; NF08, WCAG 2.5.7: a way without dragging): not
 * past the row 50, never a row that holds something nor the only row of a lane.
 */
const LANE_ENDS: readonly LaneEnd[] = ['top', 'bottom'];
const laneRow = (end: LaneEnd): Command<MapContext> => ({
  id: `lane.row.${end}`, label: `menu.lane_row_${end}`, icon: end === 'top' ? BetweenHorizontalStart : BetweenHorizontalEnd,
  enabled: (context) => {
    const lane = targetLane(context);
    return context.canEdit && lane !== undefined && roomOf(context.map, lane.id, context.editor.store.getState().rowsAtLeast) > 0;
  },
  run: (context) => {
    const lane = targetLane(context);
    if (lane !== undefined) void growLane(context.editor, lane.id, end, 1);
  },
});
const laneRowOut = (end: LaneEnd): Command<MapContext> => ({
  id: `lane.row.remove_${end}`, label: `menu.lane_row_remove_${end}`, icon: FoldVertical,
  enabled: (context) => {
    const lane = targetLane(context);
    return context.canEdit && lane !== undefined && shrinking(context.map, context.editor.store.getState().rowsAtLeast, lane.id, end) !== null;
  },
  run: (context) => {
    const lane = targetLane(context);
    if (lane !== undefined) void shrinkLane(context.editor, lane.id, end);
  },
});

const LANE_COMMANDS: readonly Command<MapContext>[] = [
  { id: 'lane.rename', label: 'menu.rename', icon: PencilLine, enabled: editing, run: (context) => {
    const lane = targetLane(context);
    if (lane !== undefined) context.editor.store.setState({ editing: `lane:${lane.id}` });
  } },
  ...LANE_ENDS.map(laneRow),
  ...LANE_ENDS.map(laneRowOut),
  ...LANE_TYPES.map((type): Command<MapContext> => ({
    id: `lane.type.${type}`, label: 'menu.lane_type', icon: LANE_ICONS[type], labelValues: () => ({ type: i18next.t(`map.lane_types.${type}`) }), enabled: editing,
    checked: (context) => targetLane(context)?.type === type,
    run: (context) => {
      const lane = targetLane(context);
      if (lane !== undefined) void updateLane(context.editor, lane.id, { type });
    },
  })),
  ...LANE_TINTS.map((tint, index): Command<MapContext> => ({
    id: `lane.colour.${tint.key}`, label: 'menu.lane_colour', labelValues: () => ({ tint: i18next.t(`map.tints.${tint.key}`) }), enabled: editing,
    swatch: `var(--lane-${String(index + 1)}-pastel)`, checked: (context) => targetLane(context)?.color.toUpperCase() === tint.pastel.toUpperCase(),
    run: (context) => {
      const lane = targetLane(context);
      if (lane !== undefined) void updateLane(context.editor, lane.id, { color: tint.pastel });
    },
  })),
  { id: 'lane.delete', label: 'menu.delete_lane', icon: Trash2, danger: true, enabled: (context) => context.canEdit && context.map.lanes.length > 1,
    run: (context) => {
      const lane = targetLane(context);
      if (lane !== undefined) deleteLane(context.editor, lane.id);
    } },
];

export const MENU_COMMANDS: readonly Command<MapContext>[] = [...STEP_COMMANDS, ...SELECTION_COMMANDS, ...ROUTE_COMMANDS, ...LANE_COMMANDS];
