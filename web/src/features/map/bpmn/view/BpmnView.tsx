// BpmnView.tsx
// The map shown as a diagram BPMN 2.0, read only (ADR 106): its copy read without any event, its diagram compiled in the language shown, drawn on a canvas of its own (moved and zoomed, nothing changed) opened where the map showed its steps, a step clicked opens its sheet read only, a detailed step dives into its detail in the same view; Alt+B and the button of its bar come back to the map.

import { useEffect, useLayoutEffect, useMemo, useRef, useState } from 'react';
import { getViewportForBounds, ReactFlow, ReactFlowProvider, useStore as useFlowStore, type Node, type NodeProps } from '@xyflow/react';
import { useTranslation } from 'react-i18next';
import { useStore } from 'zustand';
import type { Schema } from '../../../../api/client.ts';
import { paths } from '../../../../app/router.ts';
import { useShownTexts } from '../../state/shownTexts.ts';
import { useKeyLayer } from '../../../../shared/keyboard/KeyboardContext.tsx';
import type { PmapDocument } from '../../../../shared/pmap.ts';
import { MAX_ZOOM, MIN_ZOOM, TOP_MARGIN } from '../../model/geometry/constants.ts';
import type { View } from '../../model/geometry/viewport.ts';
import { hasContent } from '../../model/stepFacts.ts';
import { bpmnKeyHandler } from '../../state/commands/mapCommands.ts';
import { useEditor, useMapLanguage } from '../../state/EditorContext.tsx';
import type { MapEditor } from '../../state/editor.ts';
import { handedPlace, useTellView, type PlacedStep } from '../../state/viewHandover.ts';
import { diagramOfMap, readCopy, withDetailsOfMap } from '../bpmnSource.ts';
import { bpmnView } from '../bpmnState.ts';
import type { BpmnDefinitions, BpmnDiagram } from '../model/bpmnModel.ts';
import { BpmnDrawing, extentOf, type BpmnActions } from './BpmnDrawing.tsx';
import { BpmnToolbar } from './BpmnToolbar.tsx';

type DiagramNode = Node<{ definitions: BpmnDefinitions; diagram: BpmnDiagram; actions: BpmnActions }, 'diagram'>;

/** The whole drawing, one node of the canvas: the canvas frames it, moves it and zooms it. */
function DiagramNodeView({ data }: NodeProps<DiagramNode>) {
  return <BpmnDrawing definitions={data.definitions} diagram={data.diagram} actions={data.actions} />;
}

const NODE_TYPES = { diagram: DiagramNodeView };

/**
 * What the steps of the drawing do: open the sheet of the step, read only, for a step that has one (a start, an end, a
 * gateway has none: nothing opens, as on the map; audit of the copy 64, P2-02); dive into its detail, the view kept.
 */
function actionsOf(editor: MapEditor, map: Schema<'MapView'>): BpmnActions {
  const stepOf = (code: string) => map.steps.find((step) => step.code === code);
  return {
    onOpen: (code) => {
      const step = stepOf(code);
      editor.store.setState({ openSheet: step !== undefined && hasContent(step.type) ? step.id : null });
    },
    onDive: (code) => {
      const detail = stepOf(code)?.sub_map_id;
      if (detail != null) editor.navigate(paths.map(detail));
    },
  };
}

/**
 * The copy of the map, read again when what the map holds changes: not when only the rights of the viewer do, as they
 * are read with the project just after the opening; null while it is read, `failed` when it could not be.
 */
function useCopy(map: Schema<'MapView'>): PmapDocument | null | 'failed' {
  const content = useMemo(() => JSON.stringify({ ...map, rights: null }), [map]);
  const [read, setRead] = useState<{ content: string; document: PmapDocument | 'failed' } | null>(null);
  const mapId = map.id;
  useEffect(() => {
    let live = true;
    readCopy(mapId, 'view').then((document) => { if (live) setRead({ content, document }); }, () => { if (live) setRead({ content, document: 'failed' }); });
    return () => {
      live = false;
    };
  }, [mapId, content]);
  return read?.content === content ? read.document : null;
}

/** The view at the opening of a map: the top left of the drawing at zoom 1, as the map opens at its start (B-2.1-43). */
const START: View = { x: 16, y: TOP_MARGIN, zoom: 1 };

/** The room left around the drawing by « Show all »: a tenth of the window, as `fitView` of React Flow leaves it. */
const FIT_PADDING = 0.1;

/** The steps of the drawing, by their code and their form, so that a switch from the map or to it keeps them in sight. */
function placedSteps(definitions: BpmnDefinitions, diagram: BpmnDiagram): PlacedStep[] {
  const codes = new Map(definitions.process.nodes.map((node) => [node.id, node.stepId]));
  return diagram.shapes.flatMap((shape) => {
    const code = codes.get(shape.elementId);
    return code === undefined ? [] : [{ code, box: shape.bounds }];
  });
}

/**
 * « Show all », as the map has it (arbitration of the lot 1 of the 3.4): the whole drawing in the window, never past 100 %; the
 * button of the bar and Shift+1 run the same, given to the editor while the canvas is shown. Computed here, as the map does,
 * and not by `fitView` of React Flow, which needs the nodes measured by the browser.
 */
function useFit(nodes: readonly DiagramNode[], width: number, height: number, setView: (view: View) => void) {
  const editor = useEditor();
  useEffect(() => {
    const extent = nodes[0] === undefined ? null : { width: nodes[0].width ?? 0, height: nodes[0].height ?? 0 };
    if (extent === null || width <= 0 || height <= 0) return undefined;
    editor.canvas.showFit(() => { setView(getViewportForBounds({ x: 0, y: 0, ...extent }, width, height, MIN_ZOOM, 1, FIT_PADDING)); });
    return () => { editor.canvas.showFit(null); };
  }, [editor, nodes, width, height, setView]);
}

/**
 * The canvas of the drawing: opened where the map showed its steps, at its zoom, when the person switches from it (trial
 * of the 3.3), else at its start; moved and zoomed by the person, framed whole by « Show all ».
 */
function BpmnCanvas({ mapId, nodes, steps }: { mapId: string; nodes: DiagramNode[]; steps: readonly PlacedStep[] }) {
  const root = useRef<HTMLDivElement>(null);
  const [view, setView] = useState<View>(START);
  const width = useFlowStore((state) => state.width);
  const height = useFlowStore((state) => state.height);
  const placed = useRef(false);
  useLayoutEffect(() => {
    if (placed.current) return;
    placed.current = true;
    const handed = handedPlace(mapId, 'bpmn', steps, root.current);
    if (handed !== null) setView(handed);
  }, [mapId, steps]);
  useTellView(mapId, 'bpmn', root, steps, view, width, height);
  useFit(nodes, width, height, setView);
  return (
    <div ref={root} className="map-canvas bpmn-canvas" data-testid="bpmn-canvas">
      <ReactFlow<DiagramNode> nodes={nodes} edges={[]} nodeTypes={NODE_TYPES} viewport={view} onViewportChange={setView} minZoom={MIN_ZOOM} maxZoom={MAX_ZOOM}
        nodesDraggable={false} nodesConnectable={false} elementsSelectable={false} nodesFocusable={false} zoomOnDoubleClick={false} panOnScroll
        deleteKeyCode={null} selectionKeyCode={null} multiSelectionKeyCode={null} panActivationKeyCode={null} zoomActivationKeyCode={null}
        proOptions={{ hideAttribution: true }} />
    </div>
  );
}

function BpmnScreen({ map }: { map: Schema<'MapView'> }) {
  const { t: translate } = useTranslation();
  const editor = useEditor();
  const language = useMapLanguage();
  const details = useStore(editor.preferences, (state) => state.showBpmnDetails);
  const copy = useCopy(map);
  // The words of the drawing are of the language the map is shown in, as its names (ADR 106; audit of the copy 64, P2-02).
  const texts = useShownTexts(language);
  const close = () => { bpmnView.getState().setOpen(false); };
  // The keys that change nothing keep working here, the canvas of the map being taken away: up a level, the ribbon, the
  // assistant, the presentation (audit of the copy 64, P1-03).
  const keys = useMemo(() => bpmnKeyHandler(editor), [editor]);
  useKeyLayer('canvas', (event, chord) => {
    // Escape is left to the sheet and to the dialogs, which close first: one Escape never closes the view with them.
    if (chord !== 'Alt+B') return keys(event, chord);
    // On the press only: the release of the Alt+B that opened the view, Alt still held, is no new Alt+B (it is kept).
    if (event.type === 'keydown') close();
    return true;
  });
  const definitions = useMemo(() => (copy === null || copy === 'failed' ? null : diagramOfMap(map, withDetailsOfMap(copy, map), { language, withDetails: details, withSubProcesses: false, classification: null, t: texts })), [map, copy, language, details, texts]);
  const { nodes, steps } = useMemo((): { nodes: DiagramNode[]; steps: PlacedStep[] } => {
    const diagram = definitions?.diagrams[0];
    if (definitions === null || diagram === undefined) return { nodes: [], steps: [] };
    const extent = extentOf(diagram);
    return {
      nodes: [{ id: 'diagram', type: 'diagram', position: { x: 0, y: 0 }, width: extent.width, height: extent.height, draggable: false, selectable: false, focusable: false,
        data: { definitions, diagram, actions: actionsOf(editor, map) } }],
      steps: placedSteps(definitions, diagram),
    };
  }, [definitions, editor, map]);
  const hasDrawing = nodes.length > 0;
  return (
    <div className="bpmn-view" data-testid="bpmn-view">
      <BpmnToolbar onExport={() => { editor.floating.getState().open({ kind: 'export-bpmn' }); }} onClose={close}
        onFit={hasDrawing ? () => { editor.canvas.fit(); } : null} />
      {copy === 'failed' && <p className="map-banner map-banner--warning" role="alert" data-testid="bpmn-failed">{translate('bpmn.failed')}</p>}
      {copy === null && <p className="page-loading" data-testid="bpmn-loading">{translate('bpmn.loading')}</p>}
      {nodes.length > 0 && <BpmnCanvas key={map.id} mapId={map.id} nodes={nodes} steps={steps} />}
    </div>
  );
}

/** The view and its own canvas, whose « Show all » the bar asks of it. */
export function BpmnView({ map }: { map: Schema<'MapView'> }) {
  return (
    <ReactFlowProvider>
      <BpmnScreen map={map} />
    </ReactFlowProvider>
  );
}
