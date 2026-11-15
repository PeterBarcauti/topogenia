// SheetBody.tsx
// The body of a sheet one may fill: the person responsible, the choice between the form and the free text (both kept), the request of a clarification (annex B 4).

import { ClipboardList, PenLine } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Icon } from '../../shared/ui/Icon.tsx';
import { ClarificationRequest } from './ClarificationRequest.tsx';
import { SheetForm } from './form/SheetForm.tsx';
import { Responsible } from './Responsible.tsx';
import { useSheet } from './SheetContext.tsx';
import type { SheetMode } from './state/useSheetState.ts';

/** The free text: an area of 14 lines, 20 000 characters at most (B-4-18). */
function FreeText() {
  const { t } = useTranslation();
  const { draft, edit } = useSheet();
  return (
    <div className="field">
      <label htmlFor="sheet-free-text" className="field-label">{t('sheet.free_title')}</label>
      <textarea id="sheet-free-text" className="field-control" rows={14} maxLength={20000} value={draft.freeText} aria-describedby="sheet-free-help"
        data-testid="sheet-free-text" onChange={(event) => { edit({ ...draft, freeText: event.target.value }); }} />
      <span id="sheet-free-help" className="field-hint">{t('sheet.free_help')}</span>
    </div>
  );
}

/** « Formulaire » or « Écrire librement », a radio group; changing it changes neither content (B-4-17, B-4-20). */
function ModeSwitch({ mode, onMode }: { mode: SheetMode; onMode: (mode: SheetMode) => void }) {
  const { t } = useTranslation();
  const choices = [{ mode: 'form', icon: ClipboardList, label: t('sheet.mode_form') }, { mode: 'free', icon: PenLine, label: t('sheet.mode_free') }] as const;
  return (
    <div className="seg" role="radiogroup" aria-label={t('sheet.modes')} data-testid="sheet-modes">
      {choices.map((choice) => (
        <button key={choice.mode} type="button" role="radio" aria-checked={mode === choice.mode} data-testid={`sheet-mode-${choice.mode}`}
          onClick={() => { onMode(choice.mode); }}>
          <Icon icon={choice.icon} size={14} />
          {choice.label}
        </button>
      ))}
    </div>
  );
}

export function SheetBody({ mode, onMode }: { mode: SheetMode; onMode: (mode: SheetMode) => void }) {
  return (
    <div className="side-panel-body sheet-body" data-testid="sheet-body">
      <Responsible />
      <ModeSwitch mode={mode} onMode={onMode} />
      {mode === 'free' ? <FreeText /> : <SheetForm />}
      <ClarificationRequest />
    </div>
  );
}
