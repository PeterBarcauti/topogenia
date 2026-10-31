// StreamCard.tsx
// What the value stream layer hangs under a step, drawn under the flows: the cartouche of four cases of a step with content, the entry of a start, the exit of an end; the formula and the sources of each case on hover, all of them on keyboard focus and for a screen reader (spec 14.5, ADR 085).

import { memo } from 'react';
import type { NodeProps } from '@xyflow/react';
import { Clock3, Hourglass, Layers, LogIn, LogOut, UserRound, type LucideIcon } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Icon } from '../../../shared/ui/Icon.tsx';
import type { CardNode } from '../canvas/flowElements.ts';
import type { CaseKind, Quantity, StreamCase } from '../model/valueStream.ts';

/** The icon of each case, as the validated mockup draws them: cases, clock, hourglass, person. */
const ICONS: Readonly<Record<CaseKind, LucideIcon>> = { throughput: Layers, work: Clock3, lead: Hourglass, fte: UserRound };

/** A number and its unit, the unit smaller; a dash for an unknown value. */
function Value({ quantity }: { quantity: Quantity | null }) {
  if (quantity === null) return <span className="stream-na">–</span>;
  return <><span className="stream-value">{quantity.value}</span><span className="stream-unit">{quantity.unit}</span></>;
}

/** The text of a case for its tooltip and a screen reader: its name, its value, its formula. */
function useCaseText() {
  const { t } = useTranslation();
  return (item: StreamCase): string => {
    const value = item.quantity === null ? t('stream.unknown') : `${item.quantity.value} ${item.quantity.unit}`;
    return [t('stream.case_value', { name: t(`stream.case.${item.kind}`), value }), ...item.formula].join('\n');
  };
}

function Case({ item, text }: { item: StreamCase; text: string }) {
  const classes = ['stream-case', item.computed && 'is-computed', item.draft && 'is-draft'].filter(Boolean).join(' ');
  return (
    <span className={classes} role="img" aria-label={text} title={text} data-testid={`stream-case-${item.kind}`}>
      <Icon icon={ICONS[item.kind]} size={11} />
      <Value quantity={item.quantity} />
    </span>
  );
}

export const StreamCardView = memo(function StreamCardView({ data }: NodeProps<CardNode>) {
  const { t } = useTranslation();
  const textOf = useCaseText();
  const { figure, code } = data;
  if (figure.kind !== 'card') {
    const text = [t(figure.kind === 'entry' ? 'stream.entry' : 'stream.exit'), ...figure.formula].join('\n');
    const missing = figure.quantity === null;
    return (
      <div className={`stream-event${missing ? ' is-missing' : ''}`} role="img" aria-label={text} title={text} data-testid={`stream-${figure.kind}-${code}`}>
        <Icon icon={figure.kind === 'entry' ? LogIn : LogOut} size={11} />
        {missing ? <span>{t(figure.kind === 'entry' ? 'stream.entry_missing' : 'stream.exit_unknown')}</span>
          : <><span>{t(figure.kind === 'entry' ? 'stream.entry' : 'stream.exit')}</span><Value quantity={figure.quantity} /></>}
      </div>
    );
  }
  const texts = figure.cases.map(textOf);
  return (
    // One stop of the keyboard a cartouche: its focus shows the four formulas, as the hover of each case shows its own.
    <div className="stream-card" role="group" tabIndex={0} aria-label={t('stream.card', { code })} data-testid={`stream-card-${code}`}>
      {figure.cases.map((item, index) => <Case key={item.kind} item={item} text={texts[index] ?? ''} />)}
      <div className="stream-card-detail" aria-hidden="true">
        {texts.map((text, index) => <p key={figure.cases[index]?.kind}>{text}</p>)}
      </div>
    </div>
  );
});
