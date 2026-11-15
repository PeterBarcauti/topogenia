// PresenterFlow.tsx
// The map of a presentation (ADR 097): its own React Flow, read only (nothing moved, chosen nor deleted), the scene lit on it, the camera that frames it, the light of a passage, the lanes named on the left and a small map « you are here »; a step clicked becomes the stop shown, and the field of a comment written follows it, its text kept first (ADR 099 revised, essai J6 of 3.2).

import { useMemo, type CSSProperties } from 'react';
import { ConnectionMode, MiniMap, ReactFlow, useStore as useFlowStore, ViewportPortal } from '@xyflow/react';
import { useTranslation } from 'react-i18next';
import { useStore } from 'zustand';
import { ArrowMarker } from '../canvas/ArrowMarker.tsx';
import { flowElements, VIEW_ONLY, type FlowEdge, type MapNode } from '../canvas/flowElements.ts';
import { EDGE_TYPES } from '../edges/RoutedEdge.tsx';
import { geometryCssVariables, MAX_ZOOM } from '../model/geometry/constants.ts';
import type { LaneBand } from '../model/mapLayout.ts';
import { LaneBackground } from '../nodes/LaneBackground.tsx';
import { StepNodeView } from '../nodes/StepNode.tsx';
import type { MapEditor } from '../state/editor.ts';
import { moveWritten, showFlow, showsFlow } from './comments.ts';
import { diveInto, showStep } from './dive.ts';
import { spotOf, spotlit } from './model/spotlight.ts';
import { PresenterForks } from './PresenterForks.tsx';
import { PresenterLight } from './PresenterLight.tsx';
import { usePresenterCamera } from './usePresenterCamera.ts';
import type { Presentation } from './usePresentation.ts';

const NODE_TYPES = { step: StepNodeView, lane: LaneBackground };

/** The lowest zoom of the stage: the whole of a large map at the opening. */
const PRESENTER_MIN_ZOOM = 0.05;

/** The lanes named on the left of the screen, beside the band of each one in the view; the lane of the step shown lit. */
function PresenterLanes({ lanes, active, dim }: { lanes: readonly LaneBand[]; active: string | null; dim: boolean }) {
  const transform = useFlowStore((state) => state.transform);
  const [, y, zoom] = transform;
  return (
    <div className="presenter-lanes" aria-hidden="true" data-testid="presenter-lanes">
      {lanes.map((band) => {
        const style = { top: `${String(band.top * zoom + y)}px`, height: `${String(band.height * zoom)}px`, '--ink': `var(--lane-${String(band.tint)}-ink)` } as CSSProperties;
        const name = ['presenter-lane', band.lane.id === active && 'is-active', dim && band.lane.id !== active && 'is-dim'].filter(Boolean).join(' ');
        return <div key={band.lane.id} className={name} style={style}><span>{band.name}</span></div>;
      })}
    </div>
  );
}

/** The step of the small map in the colour of the charter, the one shown in the colour of the accent. */
const minimapColour = (presented: string | null) => (node: MapNode): string =>
  node.type !== 'step' ? 'transparent' : node.id === presented ? 'var(--accent)' : 'var(--minimap-node)';

export function PresenterFlow({ editor, presentation, still }: { editor: MapEditor; presentation: Presentation; still: boolean }) {
  const { t } = useTranslation();
  const showCodes = useStore(editor.preferences, (state) => state.showCodes);
  const scene = useStore(editor.presenter, (state) => state.player.scene);
  // The map as the room sees it: the frame of a dissolution is a tool of the edition, left out (ADR 097).
  const base = useMemo(() => {
    const drawn = flowElements(presentation.layout, { ...VIEW_ONLY, showCodes });
    return { nodes: drawn.nodes.filter((node) => node.type !== 'frame'), edges: drawn.edges };
  }, [presentation.layout, showCodes]);
  const spot = useMemo(() => spotOf(presentation.scenes[scene], presentation.tour, base.edges), [presentation, scene, base.edges]);
  const { nodes, edges } = useMemo(() => spotlit(base.nodes, base.edges, spot), [base, spot]);
  usePresenterCamera(editor, presentation);
  const active = presentation.map.steps.find((step) => step.id === spot.presented)?.lane_id ?? null;
  return (
    <div className="map-canvas presenter-flow" style={geometryCssVariables()} data-testid="presenter-canvas">
      <ReactFlow<MapNode, FlowEdge> nodes={nodes} edges={edges} nodeTypes={NODE_TYPES} edgeTypes={EDGE_TYPES} minZoom={PRESENTER_MIN_ZOOM} maxZoom={MAX_ZOOM}
        nodesDraggable={false} nodesConnectable={false} elementsSelectable={false} nodesFocusable={false} edgesFocusable={false} zoomOnDoubleClick={false}
        connectionMode={ConnectionMode.Loose}
        deleteKeyCode={null} selectionKeyCode={null} multiSelectionKeyCode={null} panActivationKeyCode={null} zoomActivationKeyCode={null}
        onNodeClick={(_, node) => {
          if (node.type !== 'step') return;
          moveWritten(editor, presentation, () => {
            showStep(editor, presentation, node.id);
          }, node.id);
        }}
        onEdgeClick={(_, edge) => {
          // A flow clicked shows its passage, and the field of a comment written follows it (lot J10).
          moveWritten(editor, presentation, () => {
            showFlow(editor, presentation, edge.id);
          }, showsFlow(presentation, edge.id) ? edge.id : undefined);
        }}
        onNodeDoubleClick={(_, node) => {
          if (node.type === 'step') diveInto(editor, presentation, node.id);
        }}
        ariaLabelConfig={{ 'minimap.ariaLabel': t('map.minimap') }} proOptions={{ hideAttribution: true }}>
        <ViewportPortal><ArrowMarker /></ViewportPortal>
        <PresenterLight editor={editor} presentation={presentation} still={still} />
        <PresenterForks editor={editor} presentation={presentation} />
        {spot.presented !== null && <MiniMap<MapNode> nodeColor={minimapColour(spot.presented)} className="presenter-minimap" position="bottom-right" />}
      </ReactFlow>
      <PresenterLanes lanes={presentation.layout.lanes} active={active} dim={spot.presented !== null} />
    </div>
  );
}
