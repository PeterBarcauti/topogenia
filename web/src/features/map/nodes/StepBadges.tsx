// StepBadges.tsx
// What is pinned on a step: the gauge of its sheet, the warning of the layout, the badge of a detailed step and its frequency (annex B 2.10, spec 9.4).

import { Clock, Layers } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import type { Schema } from '../../../api/client.ts';
import { Icon } from '../../../shared/ui/Icon.tsx';
import type { Gauge } from '../model/stepFacts.ts';

/**
 * The gauge of 3 px at the bottom of the step, as long as the completeness, in the colour of the state (B-2.10-20 to
 * B-2.10-25). On a card it runs along the bottom edge; on a gateway, given the points of the lower contour of its
 * diamond, it runs along that contour from the left point, by the bottom point, to the right point (R14).
 */
export function SheetGauge({ gauge, completeness, contour }: { gauge: Gauge; completeness: number; contour?: { readonly box: string; readonly points: string } }) {
  const { t } = useTranslation();
  const help = t('map.gauge', { state: t(`map.sheet_states.${gauge.status}`), completeness });
  if (contour !== undefined) {
    return (
      <span className="step-gauge step-gauge--contour" role="img" aria-label={help} title={help} data-testid="step-gauge">
        <svg viewBox={contour.box} aria-hidden="true">
          <polyline className={`gauge--${gauge.status}`} points={contour.points} pathLength={100} strokeDasharray={`${String(gauge.width)} 100`} data-testid="step-gauge-contour" />
        </svg>
      </span>
    );
  }
  return (
    <span className="step-gauge" role="img" aria-label={help} title={help} data-testid="step-gauge">
      <span className={`gauge--${gauge.status}`} style={{ width: `${String(gauge.width)}%` }} />
    </span>
  );
}

/** The « ! » in the top left corner, the warnings of the layout in its tooltip, one a line (B-2.10-16 to B-2.10-19). */
export function LayoutWarning({ codes }: { codes: readonly string[] }) {
  const { t } = useTranslation();
  const texts = codes.map((code) => t(`map.warnings.${code}`));
  return (
    <span className="step-warning" role="img" aria-label={texts.join(' ')} title={texts.join('\n')} data-testid="step-warning">
      !
    </span>
  );
}

/** The « detailed » badge of a detailed step (spec 9.4), whose place the handoffs of its detail take under the layer « Analysis ». */
export function DetailedBadge() {
  const { t } = useTranslation();
  return (
    <span className="step-detailed" data-testid="step-detailed">
      <Icon icon={Layers} size={11} strokeWidth={2.25} />
      {t('map.detailed')}
    </span>
  );
}

/** The frequency of the flow of a detailed step under it when its sheet has one (spec 9.4), under the layer « Analysis » too (audit 3.1 V3, U6). */
export function FrequencyBadge({ frequency }: { frequency: Schema<'Frequency'> }) {
  const { t } = useTranslation();
  if (frequency === null) return null;
  return (
    <span className="step-frequency" title={t('map.frequency')} data-testid="step-frequency">
      <Icon icon={Clock} size={11} strokeWidth={2.25} />
      {t(`map.frequencies.${frequency}`)}
    </span>
  );
}
