// BpmnToolbar.tsx
// The bar of the view in BPMN 2.0 (ADR 106): what is shown (a diagram read only), the switch of the details drawn under the steps, the whole diagram framed, the export of the .bpmn, the way back to the map.

import { FileCode, Map as MapIcon, Maximize } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useStore } from 'zustand';
import { Button } from '../../../../shared/ui/Button.tsx';
import { useShortcutLabel } from '../../../../shared/ui/useShortcutLabel.ts';
import { useEditor } from '../../state/EditorContext.tsx';

/** `onFit` frames the whole diagram; null while it is not drawn. */
export function BpmnToolbar({ onExport, onClose, onFit }: { onExport: () => void; onClose: () => void; onFit: (() => void) | null }) {
  const { t } = useTranslation();
  const shortcut = useShortcutLabel();
  const editor = useEditor();
  const details = useStore(editor.preferences, (state) => state.showBpmnDetails);
  return (
    <div className="bpmn-toolbar" role="toolbar" aria-label={t('bpmn.toolbar')} data-testid="bpmn-toolbar">
      <span className="bpmn-badge" data-testid="bpmn-badge">{t('bpmn.badge')}</span>
      <label className="bpmn-switch">
        <input type="checkbox" role="switch" checked={details} aria-checked={details} data-testid="bpmn-toggle-details"
          onChange={(event) => { editor.preferences.getState().set('showBpmnDetails', event.target.checked); }} />
        <span>{t('bpmn.details')}</span>
      </label>
      <Button testId="bpmn-fit" icon={Maximize} variant="secondary" disabled={onFit === null} onClick={() => { onFit?.(); }} title={`${t('bpmn.fit_help')} (${shortcut('Shift+1')})`}>{t('bpmn.fit')}</Button>
      <span className="bpmn-toolbar-gap" />
      <Button testId="bpmn-export-open" icon={FileCode} onClick={onExport}>{t('bpmn.export')}</Button>
      <Button testId="bpmn-close" icon={MapIcon} variant="secondary" onClick={onClose} title={t('bpmn.close_help')}>{t('bpmn.close')}</Button>
    </div>
  );
}
