// StepBadges.tsx
// What is pinned on a step: the gauge of its sheet, the warning of the layout, the badges of a detailed step (annex B 2.10, spec 9.4).

import { Clock, Layers } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import type { Schema } from '../../../api/client.ts';
import { Icon } from '../../../shared/ui/Icon.tsx';
import type { Gauge } from '../model/stepFacts.ts';

/** The gauge of 3 px at the bottom of the step, as wide as the completeness, in the colour of the state (B-2.10-20 to B-2.10-25). */
export function SheetGauge({ gauge, completeness }: { gauge: Gauge; completeness: number }) {
  const { t } = useTranslation();
  const help = t('map.gauge', { state: t(`map.sheet_states.${gauge.status}`), completeness });
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

/** A detailed step: the « detailed » badge, and the frequency of its flow under it when its sheet has one (spec 9.4). */
export function DetailedBadges({ frequency }: { frequency: Schema<'Frequency'> }) {
  const { t } = useTranslation();
  return (
    <>
      <span className="step-detailed" data-testid="step-detailed">
        <Icon icon={Layers} size={11} strokeWidth={2.25} />
        {t('map.detailed')}
      </span>
      {frequency !== null && (
        <span className="step-frequency" title={t('map.frequency')} data-testid="step-frequency">
          <Icon icon={Clock} size={11} strokeWidth={2.25} />
          {t(`map.frequencies.${frequency}`)}
        </span>
      )}
    </>
  );
}
