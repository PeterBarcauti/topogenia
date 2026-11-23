// PresenterLaneNames.tsx
// The lanes named on the left of a presentation, beside the band of each one in the view: the name takes the lines its band holds, and is masked when the band is lower than one line (F-209), never laid over the next band.

import type { CSSProperties } from 'react';
import type { LaneBand } from '../model/mapLayout.ts';
import { laneLines } from './model/laneLines.ts';

/** The band of each lane at the scroll `y` and the `zoom` of the view; the lane of the step shown lit, the others dimmed while a step is shown. */
export function PresenterLaneNames({ lanes, active, dim, y, zoom }: { lanes: readonly LaneBand[]; active: string | null; dim: boolean; y: number; zoom: number }) {
  return (
    <div className="presenter-lanes" aria-hidden="true" data-testid="presenter-lanes">
      {lanes.map((band) => {
        const height = band.height * zoom;
        const lit = band.lane.id === active;
        const lines = laneLines(height, lit);
        const style = { top: `${String(band.top * zoom + y)}px`, height: `${String(height)}px`, '--ink': `var(--lane-${String(band.tint)}-ink)` } as CSSProperties;
        const name = ['presenter-lane', lit && 'is-active', dim && !lit && 'is-dim', lines === 0 && 'is-masked'].filter(Boolean).join(' ');
        return <div key={band.lane.id} className={name} style={style}><span style={{ '--lines': String(lines) } as CSSProperties}>{band.name}</span></div>;
      })}
    </div>
  );
}
