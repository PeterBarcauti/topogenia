// LaneBackground.tsx
// The body of a lane behind its steps, white or grey in alternation, and its name in a margin hidden under the fixed headers, with its headcount and workload under the value stream layer (annex B 2.12, 3.1, spec 9.3, 14.5).

import { memo, type CSSProperties } from 'react';
import type { NodeProps } from '@xyflow/react';
import { useTranslation } from 'react-i18next';
import type { LaneNode } from '../canvas/flowElements.ts';
import { LaneStream } from '../chrome/LaneStream.tsx';

/**
 * The margin repeats the name and the type of the lane in its pastel: on the page the fixed headers cover it, the exports
 * show it (B-2.12-25, P6-T09).
 */
export const LaneBackground = memo(function LaneBackground({ data }: NodeProps<LaneNode>) {
  const { t } = useTranslation();
  const { band } = data;
  const style = { '--pastel': `var(--lane-${String(band.tint)}-pastel)`, '--ink': `var(--lane-${String(band.tint)}-ink)` } as CSSProperties;
  return (
    <div className={`lane-body lane-body--${band.parity}`} style={style} data-testid={`lane-body-${band.lane.code}`}>
      <div className="lane-body-margin" aria-hidden="true">
        <span className="lane-body-name">{band.name}</span>
        <span className="lane-body-type">{t(`map.lane_types.${band.lane.type}`)}</span>
        {band.stream !== null && <LaneStream stream={band.stream} code={`body-${band.lane.code}`} />}
      </div>
    </div>
  );
});
