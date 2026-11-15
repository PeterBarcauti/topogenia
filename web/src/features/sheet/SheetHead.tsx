// SheetHead.tsx
// The sticky head of the sheet: « CODE · libellé », the lane, the state of the sheet in its colour, the saving, the completeness gauge, the clarification asked, which the facilitator rewrites or removes (annex B 4, mockup, spec 3.0 section 16).

import { Check, CircleAlert, X } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useStore } from 'zustand';
import type { Schema } from '../../api/client.ts';
import { FoldButton } from '../../shared/ui/FoldButton.tsx';
import { Icon } from '../../shared/ui/Icon.tsx';
import type { SheetStep } from './SheetContext.tsx';
import { ClarificationNote } from './ClarificationNote.tsx';
import type { AutoSave } from './state/autoSave.ts';

/** « Enregistré », « Modifications en attente… », « Enregistrement… », « Non enregistré » in red (B-4-42). */
function SaveIndicator({ save }: { save: AutoSave }) {
  const { t } = useTranslation();
  const status = useStore(save.state, (state) => state.status);
  return (
    <span className={`sheet-saving sheet-saving--${status}`} role="status" data-testid="sheet-saving">
      {status === 'saved' && <Icon icon={Check} size={14} strokeWidth={2.5} />}
      {status === 'failed' && <Icon icon={CircleAlert} size={14} />}
      {t(`sheet.saving.${status}`)}
    </span>
  );
}

/** The gauge computed by the host and its percentage; no field is required (B-4-10, B-4-11). */
function Completeness({ view }: { view: Schema<'SheetView'> }) {
  const { t } = useTranslation();
  return (
    <div className="sheet-completeness" title={t('sheet.completeness_help')}>
      <span>{t('sheet.completeness')}</span>
      <div className={`sheet-completeness-bar sheet-completeness-bar--${view.status}`} role="progressbar" aria-label={t('sheet.completeness_label')}
        aria-valuemin={0} aria-valuemax={100} aria-valuenow={view.completeness} data-testid="sheet-completeness">
        <span style={{ width: `${String(view.completeness)}%` }} />
      </div>
      <b>{view.completeness} %</b>
    </div>
  );
}

export function SheetHead({ step, view, save, onClose }: { step: SheetStep; view: Schema<'SheetView'> | null; save: AutoSave | null; onClose: () => void }) {
  const { t } = useTranslation();
  return (
    <header className="side-panel-head sheet-head">
      <div className="sheet-title-row">
        <h2 className="sheet-title" data-testid="sheet-title"><code>{step.code}</code> · {step.label}</h2>
        <FoldButton />
        <button type="button" className="icon-button" aria-label={t('sheet.close')} title={t('sheet.close')} data-testid="sheet-close" onClick={onClose}>
          <Icon icon={X} size={16} />
        </button>
      </div>
      <div className="sheet-meta" data-testid="sheet-meta">
        {step.laneName !== '' && <span>{t('sheet.lane', { name: step.laneName })}</span>}
        {view !== null && (
          <>
            {step.laneName !== '' && <span aria-hidden="true">·</span>}
            <span>{t('sheet.state')}</span>
            <span className={`state state--${view.status}`} data-testid="sheet-state">{t(`map.sheet_states.${view.status}`)}</span>
            {save !== null && view.permissions.fill && <SaveIndicator save={save} />}
          </>
        )}
      </div>
      {view !== null && <Completeness view={view} />}
      {view?.clarification_request != null && <ClarificationNote text={view.clarification_request} />}
    </header>
  );
}
