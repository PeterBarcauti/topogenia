// Canvas.tsx
// The canvas of the map with React Flow: lanes, steps, routed flows, bounded view scrolled by the wheel and zoomed by Ctrl and the wheel, dots, zoom controls, mini map, fixed lane headers; the value stream layer under the steps (its summary in its panel); the tools, the selection and the gestures of the edition; its state while an image is captured (annex B 2.1 to 2.4, 2.14, spec 14.5, spec 3.0 section 15.1).

import { useEffect, useMemo, useRef } from 'react';
import {
  Background, BackgroundVariant, ConnectionMode, ControlButton, Controls, MiniMap, ReactFlow, ReactFlowProvider, SelectionMode, useReactFlow, useStore as useFlowStore,
  useStoreApi, ViewportPortal,
} from '@xyflow/react';
import { ArrowLeftToLine } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useStore } from 'zustand';
import type { Schema } from '../../../api/client.ts';
import { Icon } from '../../../shared/ui/Icon.tsx';
import { LaneHeaders } from '../chrome/LaneHeaders.tsx';
import { EDGE_TYPES } from '../edges/RoutedEdge.tsx';
import { DOT_GAP, MAX_ZOOM, MIN_ZOOM, geometryCssVariables } from '../model/geometry/constants.ts';
import { layoutMap } from '../model/mapLayout.ts';
import type { StreamWriting } from '../model/valueStream.ts';
import { FrameNodeView } from '../nodes/FrameNode.tsx';
import { LaneBackground } from '../nodes/LaneBackground.tsx';
import { StepNodeView } from '../nodes/StepNode.tsx';
import { StreamCardView } from '../nodes/StreamCard.tsx';
import { useEditor, useMapState } from '../state/EditorContext.tsx';
import { ArrowMarker } from './ArrowMarker.tsx';
import { ExportCaption } from './ExportCaption.tsx';
import { flowElements, type FlowEdge, type MapNode } from './flowElements.ts';
import { useBoundedViewport } from './useBoundedViewport.ts';
import { useCanvasKeys } from './useCanvasKeys.ts';
import { useClicks, useElementStates, useMenus, useSelectionAndDrag } from './useCanvasEvents.ts';
import { capturing, useCaptureTarget } from './useExport.ts';
import { useHeaderWheel } from './useHeaderWheel.ts';
import { useTextMeasure } from './useTextMeasure.ts';

const NODE_TYPES = { step: StepNodeView, lane: LaneBackground, frame: FrameNodeView, card: StreamCardView };

/** The steps of the mini map in a token of the charter, the lanes in their body colour, the frame and the value stream left out (B-2.1-46). */
function minimapColour(node: MapNode): string {
  if (node.type === 'step') return 'var(--minimap-node)';
  return node.type === 'lane' ? `var(--lane-${node.data.band.parity})` : 'transparent';
}

/**
 * The numbers of the value stream layer in the language of the interface, with the working day of the project; null
 * while the layer is off (spec 14.5). The texts of the map follow the language they are shown in, the numbers the interface.
 */
function useStreamWriting(map: Schema<'MapView'>): StreamWriting | null {
  const { t, i18n } = useTranslation();
  const editor = useEditor();
  const on = useStore(editor.preferences, (state) => state.showValueStream);
  const workday = map.value_stream.work_time.workday_hours;
  return useMemo(() => (on ? { language: i18n.language, workdayHours: workday, t } : null), [on, i18n.language, workday, t]);
}

/** React Flow deletes nothing on its own: the host decides, and the canvas shows its answer (B-2.3-35). */
const neverDelete = () => Promise.resolve(false);

/** A turn of the wheel scrolls as many pixels of the screen as it gives, as a web page does (spec 3.0 section 15.1). */
const WHEEL_SCROLL_SPEED = 1;

export interface CanvasProps {
  readonly map: Schema<'MapView'>;
  readonly showCodes: boolean;
  readonly alwaysShowPictos: boolean;
}

/** The texts of React Flow for assistive technologies, in the language shown. */
function useFlowLabels() {
  const { t } = useTranslation();
  return {
    'controls.ariaLabel': t('map.controls'), 'controls.zoomIn.ariaLabel': t('map.zoom_in'), 'controls.zoomOut.ariaLabel': t('map.zoom_out'),
    'minimap.ariaLabel': t('map.minimap'), 'node.a11yDescription.default': t('map.step_keys_help'), 'edge.a11yDescription.default': t('map.edge_help'),
  };
}

/** The classes of the canvas: the pictograms on hover, the tool in hand, the link mode and its start chosen (AR-153), an image being captured (B-2.14-02). */
function useCanvasClass(alwaysShowPictos: boolean): { className: string; hand: boolean } {
  const tool = useMapState((state) => state.tool);
  const spaceHand = useMapState((state) => state.spaceHand);
  const linking = useMapState((state) => state.mode === 'link');
  const started = useMapState((state) => state.mode === 'link' && state.linkSource !== null);
  const exporting = useMapState(capturing);
  const hand = tool === 'hand' || spaceHand;
  const className = ['map-canvas', !alwaysShowPictos && 'pictos-on-hover', hand ? 'tool-hand' : 'tool-select', linking && 'is-linking', started && 'has-link-source', exporting && 'is-exporting']
    .filter(Boolean)
    .join(' ');
  return { className, hand };
}

function MapFlow({ map, showCodes, alwaysShowPictos }: CanvasProps) {
  const { t, i18n } = useTranslation();
  const editor = useEditor();
  const measure = useTextMeasure();
  // The map in the language it is shown in (ADR 083), the language of the interface until the screen tells it.
  const language = useMapState((state) => state.shown?.language ?? i18n.language);
  const writing = useStreamWriting(map);
  const layout = useMemo(() => layoutMap(map, language, measure, writing), [map, language, measure, writing]);
  const states = useElementStates(editor, map, showCodes);
  const { nodes, edges } = useMemo(() => flowElements(layout, states), [layout, states]);
  const root = useRef<HTMLDivElement>(null);
  const bounds = useCaptureTarget(editor, root, nodes);
  const { dragged, ...selectionAndDrag } = useSelectionAndDrag(editor, map);
  const shown = useMemo(() => (dragged.size === 0 ? nodes : nodes.map((node) => ({ ...node, position: dragged.get(node.id) ?? node.position }))), [nodes, dragged]);
  const { onPaneDoubleClick, onPointerDownCapture, ...clicks } = useClicks(editor, map);
  const { pointer, onContextMenu, onMouseDown, onMouseMove, onMouseLeave, ...menus } = useMenus(editor, map);
  useCanvasKeys(editor, pointer);
  const width = useFlowStore((state) => state.width);
  const height = useFlowStore((state) => state.height);
  const { view, change, backToStart } = useBoundedViewport({ width: layout.width, height: layout.height }, { width, height });
  useHeaderWheel(root, view, change);
  useReveal(editor);
  const labels = useFlowLabels();
  const { className, hand } = useCanvasClass(alwaysShowPictos);
  const canEdit = map.rights.structure;
  return (
    <div ref={root} className={className} style={geometryCssVariables()} role="region" aria-label={t('map.canvas')} data-testid="map-canvas"
      onPointerDownCapture={onPointerDownCapture} onDoubleClick={onPaneDoubleClick} onContextMenu={onContextMenu} onMouseDown={onMouseDown} onMouseMove={onMouseMove} onMouseLeave={onMouseLeave}>
      <ReactFlow<MapNode, FlowEdge> nodes={shown} edges={edges} nodeTypes={NODE_TYPES} edgeTypes={EDGE_TYPES} viewport={view} onViewportChange={change}
        minZoom={MIN_ZOOM} maxZoom={MAX_ZOOM} zoomOnDoubleClick={false} panOnScroll panOnScrollSpeed={WHEEL_SCROLL_SPEED} panOnDrag={hand ? true : [1, 2]} selectionOnDrag={!hand} selectionMode={SelectionMode.Full}
        multiSelectionKeyCode="Shift" panActivationKeyCode={null} deleteKeyCode={null} onBeforeDelete={neverDelete}
        nodesDraggable={canEdit} nodesConnectable={canEdit} elementsSelectable connectionMode={ConnectionMode.Loose}
        {...selectionAndDrag} {...clicks} {...menus} ariaLabelConfig={labels} proOptions={{ hideAttribution: true }}>
        {/* The arrow head lives in the view, so that the image of an export carries it with the flows. */}
        <ViewportPortal><ArrowMarker /></ViewportPortal>
        <ExportCaption bounds={bounds} />
        <Background variant={BackgroundVariant.Dots} gap={DOT_GAP} color="var(--canvas-dot)" />
        <MiniMap<MapNode> pannable zoomable nodeColor={minimapColour} className="map-minimap" />
        <Controls showFitView={false} showInteractive={false} position="bottom-right" className="map-controls">
          <ControlButton onClick={backToStart} title={t('map.back_to_start')} aria-label={t('map.back_to_start')} data-testid="map-back-to-start">
            <Icon icon={ArrowLeftToLine} />
          </ControlButton>
        </Controls>
      </ReactFlow>
      <LaneHeaders lanes={layout.lanes} view={view} showCodes={showCodes} />
      {map.steps.length === 0 && canEdit && <p className="map-empty" data-testid="map-empty">{t('map.empty')}</p>}
    </div>
  );
}

/**
 * Gives the editor the way to put steps in view (the codes of a reply of the assistant, spec 3.0 section 3.4): the first
 * step at the centre of the canvas, at the zoom of now, by React Flow, whose move the bounded view takes as any other.
 */
function useReveal(editor: ReturnType<typeof useEditor>) {
  const flow = useReactFlow<MapNode, FlowEdge>();
  const flowStore = useStoreApi<MapNode, FlowEdge>();
  useEffect(() => {
    editor.canvas.showReveal((stepIds) => {
      const node = stepIds[0] === undefined ? undefined : flow.getNode(stepIds[0]);
      if (node === undefined) return;
      const { width, height } = flowStore.getState();
      const { zoom } = flow.getViewport();
      const x = width / 2 - (node.position.x + (node.width ?? 0) / 2) * zoom;
      const y = height / 2 - (node.position.y + (node.height ?? 0) / 2) * zoom;
      void flow.setViewport({ x, y, zoom }, { duration: 200 });
    });
    return () => {
      editor.canvas.showReveal(null);
    };
  }, [editor, flow, flowStore]);
}

/** The canvas of one map; React Flow keeps its own store for it. */
export function Canvas(props: CanvasProps) {
  return (
    <ReactFlowProvider>
      <MapFlow {...props} />
    </ReactFlowProvider>
  );
}
