// PresenterLight.tsx
// The light of a passage (ADR 097): a point that runs along each flow travelled at once, the stroke lit behind it, the documents it carries beside the point of the flow followed; the flow lit whole at once under prefers-reduced-motion. Drawn over the steps, it is masked by their boxes and their rings: born at the outer frame of the step it leaves, never on it (essais J2 and J3).

import { useId } from 'react';
import { ViewportPortal } from '@xyflow/react';
import { FileText } from 'lucide-react';
import { useStore } from 'zustand';
import { Icon } from '../../../shared/ui/Icon.tsx';
import type { MapEditor } from '../state/editor.ts';
import { lightHoles, pointOn } from './model/route.ts';
import { flightMs } from './model/timing.ts';
import type { Presentation } from './usePresentation.ts';

/** Slow at the start and the end, fast in the middle, as a thing that takes off and lands. */
const easeInOut = (t: number): number => (t < 0.5 ? 4 * t ** 3 : 1 - (-2 * t + 2) ** 3 / 2);

/** The progress of the flight shown, from 0 to 1, or -1 outside a passage: the light is drawn again only while it flies. */
function useFlight(editor: MapEditor, presentation: Presentation): number {
  return useStore(editor.presenter, (state) => {
    const scene = presentation.scenes[state.player.scene];
    if (scene?.kind !== 'passage') return -1;
    // The time of the longest flow lit, as the scene is timed (`usePresentation`).
    const lit = presentation.tour.stops[scene.stop]?.next?.flows ?? [];
    const length = Math.max(0, ...lit.map((flow) => presentation.traces.get(flow)?.total ?? 0));
    return Math.round(Math.min(1, state.player.elapsed / flightMs(length)) * 400) / 400;
  });
}

/** How far round the map the mask lets the light show: the halo of a point on the edge of the map. */
const MASK_MARGIN = 64;

export function PresenterLight({ editor, presentation, still }: { editor: MapEditor; presentation: Presentation; still: boolean }) {
  const mask = `presenter-light-mask-${useId().replace(/[^a-zA-Z0-9_-]/g, '')}`;
  const progress = useFlight(editor, presentation);
  const scene = useStore(editor.presenter, (state) => presentation.scenes[state.player.scene]);
  if (progress < 0 || scene?.kind !== 'passage') return null;
  const flows = presentation.tour.stops[scene.stop]?.next?.flows ?? [];
  const at = still ? 1 : easeInOut(progress);
  // Each flow lit carries its own documents beside its own point: the exits of a split leave together (lot J10, independent review, M6).
  const cargos = flows.flatMap((id) => {
    const figure = presentation.layout.flows.find((flow) => flow.route.id === id);
    const trace = presentation.traces.get(id);
    return figure === undefined || trace === undefined || figure.documents.length === 0 ? [] : [{ id, documents: figure.documents, point: pointOn(trace, at) }];
  });
  const { layout } = presentation;
  return (
    <ViewportPortal>
      <svg className="presenter-light" aria-hidden="true" data-testid="presenter-light">
        <defs>
          <mask id={mask} maskUnits="userSpaceOnUse" x={-MASK_MARGIN} y={-MASK_MARGIN} width={layout.width + 2 * MASK_MARGIN} height={layout.height + 2 * MASK_MARGIN}>
            <rect className="presenter-light-mask-shown" x={-MASK_MARGIN} y={-MASK_MARGIN} width={layout.width + 2 * MASK_MARGIN} height={layout.height + 2 * MASK_MARGIN} />
            {lightHoles(layout.steps.map(({ box }) => box)).map((hole, index) => (
              <rect key={layout.steps[index]?.step.id ?? index} className="presenter-light-mask-hidden" x={hole.x} y={hole.y} width={hole.width} height={hole.height} />
            ))}
          </mask>
        </defs>
        <g mask={`url(#${mask})`} data-testid="presenter-light-flows">
        {flows.map((id) => {
          const figure = presentation.layout.flows.find((flow) => flow.route.id === id);
          const trace = presentation.traces.get(id);
          if (figure === undefined || trace === undefined) return null;
          const point = pointOn(trace, at);
          return (
            <g key={id}>
              <path className="presenter-trail" d={figure.route.path} pathLength={1} strokeDasharray={`${String(at)} 1`} />
              {!still && at < 1 && (
                <g className="presenter-spark" transform={`translate(${String(point.x)} ${String(point.y)})`}>
                  <circle className="presenter-spark-halo" r={13} />
                  <circle className="presenter-spark-core" r={5.5} />
                </g>
              )}
            </g>
          );
        })}
        </g>
      </svg>
      {!still && at < 1 && cargos.map((cargo) => (
        <div key={cargo.id} className="presenter-cargo" style={{ transform: `translate(${String(cargo.point.x)}px, ${String(cargo.point.y)}px)` }} data-testid="presenter-cargo">
          <Icon icon={FileText} size={13} />
          <span>{cargo.documents.join(' · ')}</span>
        </div>
      ))}
    </ViewportPortal>
  );
}
