// RaciBadges.tsx
// The RACI layer under a step: a badge « A lane » a role, in the order A, S, C, I then the lanes, and « A ? » when nobody approves (annex B 2.11).

import { useTranslation } from 'react-i18next';
import type { StepRaci } from '../model/mapLayout.ts';

export function RaciBadges({ raci }: { raci: StepRaci }) {
  const { t } = useTranslation();
  if (raci.badges.length === 0 && !raci.missingApprover) return null;
  return (
    <div className="raci-badges" aria-label={t('raci.badges')} data-testid="raci-badges">
      {raci.missingApprover && <span className="raci-badge raci-badge--missing" title={t('raci.no_approver')} data-testid="raci-missing">{t('raci.missing_badge')}</span>}
      {raci.badges.map((badge) => (
        <span key={badge.laneId} className={`raci-badge raci-badge--${badge.role}`} title={t('raci.badge', { role: t(`raci.names.${badge.role}`), lane: badge.name })}>
          <b>{badge.role}</b> {badge.name}
        </span>
      ))}
    </div>
  );
}
