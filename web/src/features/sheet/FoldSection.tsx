// FoldSection.tsx
// A section of the sheet that folds, the inputs, the procedure or the outputs: open by default, its summary kept in its head when folded, its state kept by the browser (AR-91).

import { useId, type ReactNode } from 'react';
import { ChevronDown, ChevronRight } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Icon } from '../../shared/ui/Icon.tsx';
import { useSheet, type SheetSection } from './SheetContext.tsx';

export interface FoldSectionProps {
  readonly section: SheetSection;
  readonly title: string;
  /** « 3 documents », « vide »: what the section holds, shown folded or not. */
  readonly summary: string;
  readonly small?: string;
  readonly hint?: string;
  readonly children: ReactNode;
}

export function FoldSection({ section, title, summary, small, hint, children }: FoldSectionProps) {
  const { t } = useTranslation();
  const { props } = useSheet();
  const id = useId();
  const folded = props.folded[section];
  return (
    <section className={`sheet-section${folded ? ' is-folded' : ''}`} aria-labelledby={`${id}-title`} data-testid={`sheet-section-${section}`}>
      <h3 className="sheet-section-head">
        <button type="button" aria-expanded={!folded} aria-controls={`${id}-body`} title={t(folded ? 'sheet.unfold' : 'sheet.fold', { section: title })}
          data-testid={`sheet-fold-${section}`} onClick={() => { props.onFold(section, !folded); }}>
          <Icon icon={folded ? ChevronRight : ChevronDown} size={14} />
          <span id={`${id}-title`}>{title}</span>
          {small !== undefined && <small>{small}</small>}
          <span className="count" data-testid={`sheet-summary-${section}`}>{summary}</span>
        </button>
      </h3>
      {!folded && (
        <div id={`${id}-body`} className="sheet-section-body">
          {hint !== undefined && <span id={`${id}-hint`} className="field-hint">{hint}</span>}
          {children}
        </div>
      )}
    </section>
  );
}
