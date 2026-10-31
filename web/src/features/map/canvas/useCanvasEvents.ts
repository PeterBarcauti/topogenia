// useCanvasEvents.ts
// What the pointer does on the canvas: choose, drag and drop, click and double click, a second click that names, link, the context menus, the point aimed (annex B 2.3, 2.4, AR-151).

import { useEffect, useMemo, useState, type MouseEvent, type PointerEvent, type RefObject } from 'react';
import { useReactFlow, type Connection, type EdgeChange, type NodeChange, type OnConnectEnd, type XYPosition } from '@xyflow/react';
import { useStore } from 'zustand';
import type { Schema } from '../../../api/client.ts';
import { paths } from '../../../app/router.ts';
import type { Point } from '../model/geometry/grid.ts';
import { cellAtPoint, dropCells } from '../model/moves.ts';
import { hasContent, isMacro } from '../model/stepFacts.ts';
import { addFlow } from '../state/actions/flows.ts';
import { moveSteps } from '../state/actions/layout.ts';
import { activateStep } from '../state/actions/stepActivation.ts';
import { createStep } from '../state/actions/steps.ts';
import { clipboard, cutSteps } from '../state/clipboard.ts';
import type { MapEditor } from '../state/editor.ts';
import { useMapState } from '../state/EditorContext.tsx';
import type { MenuTarget } from '../state/floating.ts';
import type { ElementStates, FlowEdge, MapNode } from './flowElements.ts';
import { capturing } from './useExport.ts';

type MapView = Schema<'MapView'>;

/** The states of the elements: what is chosen, cut, linked, typed; the quick add offered, out of the layers (annex B 3.2, B-2.3-46 to B-2.3-48, spec 14.5). */
export function useElementStates(editor: MapEditor, map: MapView, showCodes: boolean): ElementStates {
  const selection = useMapState((state) => state.selection);
  const linkSource = useMapState((state) => state.linkSource);
  const editing = useMapState((state) => state.editing);
  const linking = useMapState((state) => state.mode === 'link');
  const showRaci = useStore(editor.preferences, (state) => state.showRaci);
  const showStream = useStore(editor.preferences, (state) => state.showValueStream);
  const content = useStore(clipboard, (state) => state.content);
  const exporting = useMapState(capturing);
  const marks = useMapState((state) => state.marks);
  return useMemo(() => ({
    showCodes, canEdit: map.rights.structure, selection, cut: cutSteps(content, map.id), linkSource, editing,
    quickAdd: map.rights.structure && !linking && !showRaci && !showStream, showRaci, showStream, exporting, marks,
  }), [showCodes, map.rights.structure, map.id, selection, content, linkSource, editing, linking, showRaci, showStream, exporting, marks]);
}

/** The identifiers given with the choices of React Flow applied: added when chosen, taken out when no more. */
export function withChoices(ids: readonly string[], changes: readonly { readonly id: string; readonly selected: boolean }[]): string[] {
  const next = new Set(ids);
  for (const change of changes) {
    if (change.selected) next.add(change.id);
    else next.delete(change.id);
  }
  return [...next];
}

const choices = (changes: readonly (NodeChange<MapNode> | EdgeChange<FlowEdge>)[]) =>
  changes.flatMap((change) => (change.type === 'select' ? [{ id: change.id, selected: change.selected }] : []));

/**
 * The selection of React Flow (click, Shift+click, rectangle) kept in the store, and the steps dragged kept where the
 * pointer holds them until the host answers: a reading of the map during a drag does not put them back (B-2.3-27).
 */
export function useSelectionAndDrag(editor: MapEditor, map: MapView) {
  const [dragged, setDragged] = useState<ReadonlyMap<string, XYPosition>>(new Map());
  // The same handlers while the map does not change: React Flow is not given new ones at each move of the view.
  const handlers = useMemo(() => dragHandlers(editor, map, setDragged), [editor, map]);
  return { dragged, ...handlers };
}

function dragHandlers(editor: MapEditor, map: MapView, setDragged: (update: (before: ReadonlyMap<string, XYPosition>) => ReadonlyMap<string, XYPosition>) => void) {
  const onNodesChange = (changes: NodeChange<MapNode>[]) => {
    const chosen = choices(changes);
    if (chosen.length > 0) editor.store.setState((state) => ({ selection: { ...state.selection, steps: withChoices(state.selection.steps, chosen) } }));
    const moved = changes.flatMap((change) => (change.type === 'position' && change.position !== undefined ? [[change.id, change.position] as const] : []));
    if (moved.length > 0) setDragged((before) => new Map([...before, ...moved]));
  };
  const onEdgesChange = (changes: EdgeChange<FlowEdge>[]) => {
    const chosen = choices(changes);
    if (chosen.length > 0) editor.store.setState((state) => ({ selection: { ...state.selection, flows: withChoices(state.selection.flows, chosen) } }));
  };
  const onNodeDragStop = (_event: unknown, _node: MapNode, nodes: MapNode[]) => {
    const moves = dropCells(map, nodes.filter((node) => node.type === 'step').map((node) => ({ id: node.id, position: node.position })));
    void moveSteps(editor, moves).finally(() => {
      setDragged(() => new Map());
    });
  };
  return { onNodesChange, onEdgesChange, onNodeDragStop };
}

/** The time a double click is waited for before a second click names the step, the default of Windows (AR-151). */
export const RENAME_DELAY_MS = 500;

/** The step chosen alone when the button went down, where, and whether by a finger; the naming waited for, if any (AR-151). */
interface Press {
  chosen: string | null;
  at: Point | null;
  touch: boolean;
  naming: ReturnType<typeof setTimeout> | undefined;
}

/**
 * The clicks (B-2.3-09 to B-2.3-21, B-2.3-33, AR-151): a click chooses a step and opens nothing, a sheet already open
 * following the step; a second click on the step chosen alone names it in place, once no double click came; a tap of a
 * finger, which has no Enter, opens the sheet as in the 1.x (S-NF-03); in the link mode a click chooses the source then
 * the target, and with the RACI layer on and the right to change the map it opens the RACI panel at (+12, −20) of the
 * pointer (B-2.3-12); a double click names a step, opens a detailed map, creates a step on the background, types the
 * label of a flow; a line drawn from a handle links two steps, dropped on a handle or on the body.
 */
export function useClicks(editor: MapEditor, map: MapView) {
  const flow = useReactFlow<MapNode, FlowEdge>();
  // Written by the handlers and read by them, never drawn: kept by the canvas for its whole life.
  const [press] = useState<Press>(() => ({ chosen: null, at: null, touch: false, naming: undefined }));
  useEffect(() => () => {
    clearTimeout(press.naming);
  }, [press]);
  return useMemo(() => clickHandlers(editor, map, flow.screenToFlowPosition, press), [editor, map, flow, press]);
}

/** Whether the focus is still on the step: a key, a menu or a panel that took it in the meantime calls the naming off. */
const focusedOn = (stepId: string): boolean => document.activeElement?.closest('.react-flow__node')?.getAttribute('data-id') === stepId;

/**
 * A click that chooses a step: the sheet open follows it; on the step already chosen alone, its name typed in place
 * after the time of a double click, if the step still has the focus (Enter at once opens its sheet instead).
 */
function chooseStep(editor: MapEditor, step: Schema<'StepView'>, event: MouseEvent, press: Press, canEdit: boolean): void {
  if (event.shiftKey || event.ctrlKey || event.metaKey || event.altKey) return;
  const { openSheet } = editor.store.getState();
  if (openSheet !== null && openSheet !== step.id) editor.store.setState({ openSheet: hasContent(step.type) ? step.id : null });
  const still = press.at !== null && Math.hypot(event.clientX - press.at.x, event.clientY - press.at.y) <= 3;
  if (!canEdit || !still || press.chosen !== step.id) return;
  press.naming = setTimeout(() => {
    const state = editor.store.getState();
    if (state.mode === 'normal' && state.editing === null && state.selection.steps.length === 1 && state.selection.steps[0] === step.id && focusedOn(step.id)) {
      editor.store.setState({ editing: step.id, editingSeed: null });
    }
  }, RENAME_DELAY_MS);
}

function clickHandlers(editor: MapEditor, map: MapView, toFlow: (point: Point) => Point, press: Press) {
  const canEdit = map.rights.structure;
  const stepOf = (id: string) => map.steps.find((step) => step.id === id);
  return {
    /** Before React Flow changes the selection: what was chosen alone, and where the button went down. */
    onPointerDownCapture: (event: PointerEvent) => {
      clearTimeout(press.naming);
      const { selection } = editor.store.getState();
      press.chosen = selection.steps.length === 1 && selection.flows.length === 0 ? (selection.steps[0] ?? null) : null;
      press.at = { x: event.clientX, y: event.clientY };
      press.touch = event.pointerType === 'touch';
    },
    onNodeClick: (event: MouseEvent, node: MapNode) => {
      const step = node.type === 'step' ? stepOf(node.id) : undefined;
      if (step === undefined) return;
      const { mode } = editor.store.getState();
      if (mode === 'link' || press.touch || (canEdit && editor.preferences.getState().showRaci)) activateStep(editor, step.id, { x: event.clientX, y: event.clientY });
      else chooseStep(editor, step, event, press, canEdit);
    },
    onNodeDoubleClick: (_event: MouseEvent, node: MapNode) => {
      clearTimeout(press.naming);
      const step = node.type === 'step' ? stepOf(node.id) : undefined;
      if (step !== undefined && isMacro(step) && step.sub_map_id !== null) editor.navigate(paths.map(step.sub_map_id));
      else if (step !== undefined && (canEdit || editor.store.getState().translating)) editor.store.setState({ editing: step.id, editingSeed: null });
    },
    onPaneClick: () => {
      editor.store.setState({ openSheet: null });
    },
    onPaneDoubleClick: (event: MouseEvent) => {
      if (!canEdit || !(event.target instanceof Element) || !event.target.classList.contains('react-flow__pane')) return;
      const cell = cellAtPoint(map, toFlow({ x: event.clientX, y: event.clientY }));
      if (cell !== null) void createStep(editor, cell);
    },
    onEdgeDoubleClick: (event: MouseEvent, edge: FlowEdge) => {
      // A label is typed in the language shown: a facilitator in another language than the one of origin translates it (ADR 083).
      if (canEdit || (editor.store.getState().translating && edge.data?.text !== '')) {
        editor.floating.getState().open({ kind: 'flow-label', flowId: edge.id, at: { x: event.clientX, y: event.clientY } });
      }
    },
    onConnect: (connection: Connection) => {
      void addFlow(editor, connection.source, connection.target);
    },
    onConnectEnd: ((event, state) => {
      if (state.isValid === true || state.fromNode === null) return;
      const point = 'changedTouches' in event ? event.changedTouches[0] : event;
      const target = point === undefined ? null : document.elementFromPoint(point.clientX, point.clientY)?.closest('.react-flow__node-step')?.getAttribute('data-id');
      if (target != null) void addFlow(editor, state.fromNode.id, target);
    }) satisfies OnConnectEnd,
  };
}

/** The context menus of the steps, the selection, the flows and the background, and the point under the pointer (annex B 2.4, B-2.7-20). */
export function useMenus(editor: MapEditor, map: MapView) {
  const flow = useReactFlow<MapNode, FlowEdge>();
  // Two places the handlers write and the keyboard reads, never drawn: kept by the canvas for its whole life.
  const [places] = useState(() => ({ pointer: { current: null as Point | null }, rightPress: { current: null as Point | null } }));
  return useMemo(() => menuHandlers(editor, map, flow.screenToFlowPosition, places), [editor, map, flow, places]);
}

function menuHandlers(editor: MapEditor, map: MapView, toFlow: (point: Point) => Point, refs: { pointer: RefObject<Point | null>; rightPress: RefObject<Point | null> }) {
  const { pointer, rightPress } = refs;
  const open = (target: MenuTarget, event: MouseEvent) => {
    editor.floating.getState().open({ kind: 'menu', target, at: { x: event.clientX, y: event.clientY } });
  };
  return {
    pointer,
    onNodeContextMenu: (event: MouseEvent, node: MapNode) => {
      event.preventDefault();
      if (node.type !== 'step') return;
      const chosen = editor.store.getState().selection.steps;
      open(chosen.length > 1 && chosen.includes(node.id) ? { kind: 'selection', stepIds: chosen } : { kind: 'step', stepId: node.id, at: { x: event.clientX, y: event.clientY } }, event);
    },
    onSelectionContextMenu: (event: MouseEvent, nodes: MapNode[]) => {
      event.preventDefault();
      const stepIds = nodes.filter((node) => node.type === 'step').map((node) => node.id);
      if (stepIds.length > 0) open({ kind: 'selection', stepIds }, event);
    },
    onEdgeContextMenu: (event: MouseEvent, edge: FlowEdge) => {
      event.preventDefault();
      if (map.rights.structure) open({ kind: 'flow', flowId: edge.id, at: { x: event.clientX, y: event.clientY } }, event);
    },
    /** The menu of the background, unless the right button moved the view by more than 5 px (B-2.4-41). */
    onContextMenu: (event: MouseEvent) => {
      if (!(event.target instanceof Element) || !event.target.classList.contains('react-flow__pane')) return;
      event.preventDefault();
      const start = rightPress.current;
      if (start !== null && Math.hypot(event.clientX - start.x, event.clientY - start.y) > 5) return;
      open({ kind: 'pane', point: toFlow({ x: event.clientX, y: event.clientY }) }, event);
    },
    onMouseDown: (event: MouseEvent) => {
      rightPress.current = event.button === 2 ? { x: event.clientX, y: event.clientY } : null;
    },
    onMouseMove: (event: MouseEvent) => {
      pointer.current = toFlow({ x: event.clientX, y: event.clientY });
    },
    onMouseLeave: () => {
      pointer.current = null;
    },
  };
}
