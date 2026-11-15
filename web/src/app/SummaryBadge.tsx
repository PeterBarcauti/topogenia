// SummaryBadge.tsx
// The summary of the map shown, beside its title in the header: a bubble at the opening of each map, folded into an icon at the first pointer pressed or after a few seconds; the icon shows it again on hover or focus and opens its panel (plan 3.1, lot R).

import { useEffect, useState } from 'react';
import { Info } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import type { MapSummaryState } from '../shared/state/openProject.ts';
import { Icon } from '../shared/ui/Icon.tsx';

/** How long the bubble stays open at the opening of a map when nothing is done. */
export const BUBBLE_MS = 7000;

/** The sentence of a summary in a language: its translation, else its text of origin. */
export function summaryText(summary: NonNullable<MapSummaryState['summary']>, language: string): string {
  return summary.translations[language] ?? summary.text;
}

/**
 * The icon of the summary and its bubble; nothing for a reader of a map without summary. Mounted again for each map
 * (its key is the map), it opens its bubble once, at the opening, when the preference wants it; the bubble folds at the
 * first pointer pressed, or after {@link BUBBLE_MS} (the keys belong to the manager of the keyboard, and the bubble, a
 * status, holds no focus); hovering or focusing the icon shows it again, a click opens the panel of the summary.
 */
export function SummaryBadge({ state, language }: { state: MapSummaryState; language: string }) {
  const { t } = useTranslation();
  const text = state.summary === null ? null : summaryText(state.summary, language);
  const [opened, setOpened] = useState(() => text !== null && state.showOnOpen);
  const [hovered, setHovered] = useState(false);
  useEffect(() => {
    if (!opened) return undefined;
    const fold = () => {
      setOpened(false);
    };
    const timer = window.setTimeout(fold, BUBBLE_MS);
    window.addEventListener('pointerdown', fold, true);
    return () => {
      window.clearTimeout(timer);
      window.removeEventListener('pointerdown', fold, true);
    };
  }, [opened]);
  if (text === null && !state.canWrite) return null;
  const stale = state.summary?.stale === true;
  const label = text === null ? t('summary.none_help') : stale ? t('summary.stale_help') : t('summary.help');
  return (
    <span className="summary-badge">
      <button
        type="button" className={`summary-info${stale ? ' summary-info--stale' : ''}${text === null ? ' summary-info--none' : ''}`}
        data-testid="summary-info" aria-label={label} title={text === null ? label : undefined} aria-expanded={opened || hovered}
        onClick={() => { setOpened(false); state.open(); }}
        onMouseEnter={() => { setHovered(true); }} onMouseLeave={() => { setHovered(false); }}
        onFocus={() => { setHovered(true); }} onBlur={() => { setHovered(false); }}
      >
        <Icon icon={Info} size={14} />
      </button>
      {text !== null && (opened || hovered) && (
        <span role="status" className="summary-bubble" data-testid="summary-bubble">
          {text}
          {stale && <span className="summary-bubble-stale">{t('summary.stale_short')}</span>}
        </span>
      )}
    </span>
  );
}
