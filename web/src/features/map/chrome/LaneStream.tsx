// LaneStream.tsx
// The headcount and the workload of a lane of people under the value stream layer, side by side without a ratio, in its header and in the margin the exports show (spec 14.3, 16; spec 14.5).

import { useTranslation } from 'react-i18next';
import type { LaneStreamText } from '../model/valueStream.ts';

/**
 * « 3 personnes » over « 0,70 ETP », one a line so that neither is cut; « Effectif à saisir » when the headcount is missing;
 * a workload with a step that lacks it is a minimum (« ≥ »), a dash when none is known.
 */
export function LaneStream({ stream, code }: { stream: LaneStreamText; code: string }) {
  const { t } = useTranslation();
  const headcount = stream.headcount === null ? t('stream.lane.headcount_missing') : t('stream.lane.headcount', { count: stream.headcount });
  const fte = stream.unknown ? t('stream.lane.fte_unknown') : t(stream.incomplete ? 'stream.lane.fte_minimum' : 'stream.lane.fte', { fte: stream.fte });
  return (
    <span className="lane-stream" title={t('stream.lane.label')} data-testid={`lane-stream-${code}`}>
      <span className={stream.headcount === null ? 'is-missing' : undefined}>{headcount}</span>
      <span>{fte}</span>
    </span>
  );
}
