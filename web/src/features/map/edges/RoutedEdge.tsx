// RoutedEdge.tsx
// A flow drawn on the path of the routing, whatever its kind (flow, loop, detour): stroke, arrow, clickable band, label (annex B 3.4, ADR 060).

import { BaseEdge, type EdgeProps } from '@xyflow/react';
import type { FlowEdge } from '../canvas/flowElements.ts';
import { EDGE_HIT_WIDTH } from '../model/geometry/constants.ts';
import { EdgeLabel } from './EdgeLabel.tsx';

/** The stroke and the arrow in `--edge`, the chosen flow in the colour of the selection (B-3.4-29 to B-3.4-32). */
export function RoutedEdge({ id, data, markerEnd, selected }: EdgeProps<FlowEdge>) {
  if (data === undefined) return null;
  return (
    <>
      <BaseEdge id={id} path={data.path} markerEnd={markerEnd} interactionWidth={EDGE_HIT_WIDTH} className={selected === true ? 'map-edge is-selected' : 'map-edge'} />
      {data.label !== null && <EdgeLabel id={id} box={data.label} text={data.text} untranslated={data.untranslated} documents={data.documents} share={data.share} />}
    </>
  );
}

/** The three kinds of the routing, drawn by the same component: the path comes whole from the routing (B-3.4-33). */
export const EDGE_TYPES = { flow: RoutedEdge, loop: RoutedEdge, detour: RoutedEdge };
