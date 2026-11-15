// SheetFoot.tsx
// The sticky foot of a sheet one may fill: what the team sees of it, and « Je valide ma fiche » (annex B 4).

import { useState } from 'react';
import { CheckCheck } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useStore } from 'zustand';
import { formatDate } from '../../shared/i18n/formatDate.ts';
import { Button } from '../../shared/ui/Button.tsx';
import { useSheet } from './SheetContext.tsx';
import { validateSheet } from './state/sheetActions.ts';

/** « déjà validée le… par… » on a validated sheet saved; « l'équipe voit encore la version validée du… » on a draft over it (B-4-54, B-4-55). */
function useFootText(): string | null {
  const { t, i18n } = useTranslation();
  const { view, save, nameOf } = useSheet();
  const status = useStore(save.state, (state) => state.status);
  const validated = view.validated;
  if (view.status === 'draft' && validated !== null) return t('sheet.draft_over_validated', { date: formatDate(validated.validated_at, i18n.language) });
  if (view.status === 'validated' && validated !== null && status === 'saved') {
    return t('sheet.already_validated', { date: formatDate(validated.validated_at, i18n.language), name: nameOf(validated.validated_by) });
  }
  return view.permissions.validate && view.status !== 'validated' ? t('sheet.validate_help') : null;
}

/**
 * The validation, for who may validate: open on a draft or a sheet to review, or while something typed is not saved
 * (B-4-51, B-4-52); it saves what waits first (B-4-53).
 */
export function SheetFoot() {
  const { t } = useTranslation();
  const sheet = useSheet();
  const { view, save, props } = sheet;
  const status = useStore(save.state, (state) => state.status);
  const [busy, setBusy] = useState(false);
  const text = useFootText();
  const open = view.status === 'draft' || view.status === 'needs_review' || status !== 'saved';
  const validate = async () => {
    setBusy(true);
    if (await validateSheet(sheet)) props.notify(t('sheet.validated'), 'success');
    setBusy(false);
  };
  return (
    <footer className="side-panel-foot sheet-foot">
      <small data-testid="sheet-foot-text">{text}</small>
      {view.permissions.validate && (
        <Button testId="sheet-validate" variant="primary" icon={CheckCheck} disabled={!open || busy} onClick={() => void validate()}>{t('sheet.validate')}</Button>
      )}
    </footer>
  );
}
