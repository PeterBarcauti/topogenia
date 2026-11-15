// FoldSection.tsx
// A section of the sheet that folds: the key facts, the inputs, the procedure, the outputs or the tools; folded when the sheet opens, its summary kept in its head, so that a sheet opens light and reads at a glance (essai J10, asked by the project owner); the button that unfolds or folds them all.

import { useId, type ReactNode } from 'react';
import { ChevronDown, ChevronRight } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Icon } from '../../shared/ui/Icon.tsx';
import { SHEET_SECTIONS, useSheet, type SheetSection } from './SheetContext.tsx';

export interface FoldSectionProps {
  readonly section: SheetSection;
  readonly title: string;
  /** « 3 documents », « vide », the pills of the tools: what the section holds, shown folded or not. */
  readonly summary: ReactNode;
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

/**
 * « Tout déplier », or « Tout replier » once every section drawn is unfolded: kept for the sheets opened next, while the
 * map stays open. The reading draws only some sections and names them here (audit 3.2).
 */
export function FoldAll({ sections = SHEET_SECTIONS }: { sections?: readonly SheetSection[] }) {
  const { t } = useTranslation();
  const { props } = useSheet();
  const unfolded = sections.every((section) => !props.folded[section]);
  return (
    <button type="button" className="sheet-fold-all" data-testid="sheet-fold-all" onClick={() => { props.onFoldAll(unfolded); }}>
      {t(unfolded ? 'sheet.fold_all' : 'sheet.unfold_all')}
    </button>
  );
}
