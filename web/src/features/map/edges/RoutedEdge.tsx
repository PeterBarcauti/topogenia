// RoutedEdge.tsx
// A flow drawn on the path of the routing, whatever its kind (flow, loop, detour): stroke, arrow, clickable band, label; a handoff marked under the layer « Analysis », its number while its tile holds the focus (annex B 3.4, ADR 060, plan 3.1).

import { BaseEdge, type EdgeProps } from '@xyflow/react';
import type { FlowEdge } from '../canvas/flowElements.ts';
import { EDGE_HIT_WIDTH, HANDOFF_RANK } from '../model/geometry/constants.ts';
import type { Point } from '../model/geometry/grid.ts';
import { EdgeLabel } from './EdgeLabel.tsx';

/** The number of a handoff in the order of the flow, on a disc halfway along its path. */
function HandoffRank({ at, rank }: { at: Point; rank: number }) {
  return (
    <g className="handoff-rank" transform={`translate(${String(at.x)} ${String(at.y)})`} aria-hidden="true" data-testid="handoff-rank">
      <circle r={HANDOFF_RANK.radius} />
      <text textAnchor="middle" dominantBaseline="central">{rank}</text>
    </g>
  );
}

/**
 * The stroke and the arrow in `--edge`, the chosen flow in the colour of the selection (B-3.4-29 to B-3.4-32); a handoff
 * thicker in the colour of the analysis, a flow outside the focus faded.
 */
export function RoutedEdge({ id, data, markerEnd, selected }: EdgeProps<FlowEdge>) {
  if (data === undefined) return null;
  const className = ['map-edge', selected === true && 'is-selected', data.handoff !== null && 'is-handoff', data.faded && 'is-faded'].filter(Boolean).join(' ');
  return (
    <>
      <BaseEdge id={id} path={data.path} markerEnd={markerEnd} interactionWidth={EDGE_HIT_WIDTH} className={className} />
      {data.rankAt !== null && data.handoff !== null && <HandoffRank at={data.rankAt} rank={data.handoff} />}
      {data.label !== null && <EdgeLabel id={id} box={data.label} text={data.text} untranslated={data.untranslated} documents={data.documents} share={data.share} faded={data.faded} />}
    </>
  );
}

/** The three kinds of the routing, drawn by the same component: the path comes whole from the routing (B-3.4-33). */
export const EDGE_TYPES = { flow: RoutedEdge, loop: RoutedEdge, detour: RoutedEdge };
