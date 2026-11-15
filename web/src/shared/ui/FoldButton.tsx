// FoldButton.tsx
// The button of the head of a panel docked on the right that folds it to its strip, next to its cross; nothing out of a frame (spec 3.0 section 15.2, ADR 086).

import { PanelRightClose } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useDock } from './dock.ts';
import { Icon } from './Icon.tsx';

export function FoldButton() {
  const { t } = useTranslation();
  const dock = useDock();
  if (dock === null) return null;
  return (
    <button type="button" className="icon-button" aria-label={t('dock.fold')} title={t('dock.fold')} data-testid="panel-fold" onClick={dock.fold}>
      <Icon icon={PanelRightClose} size={16} />
    </button>
  );
}
