// RowBand.tsx
// Rows a drop or a pull would give a lane, while the gesture runs: a band in the pastel of the lane, dashed in its ink (ADR 096).

import { memo, type CSSProperties } from 'react';
import type { NodeProps } from '@xyflow/react';
import type { RowBandNode } from '../canvas/flowElements.ts';

export const RowBandView = memo(function RowBandView({ data }: NodeProps<RowBandNode>) {
  const style = { '--pastel': `var(--lane-${String(data.tint)}-pastel)`, '--ink': `var(--lane-${String(data.tint)}-ink)` } as CSSProperties;
  return <div className="row-band" style={style} data-testid={`row-band-${data.code}`} />;
});
