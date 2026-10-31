// SheetPanel.tsx
// The sheet of a step, docked on the right of the canvas: its head, its form or its reading, its foot; closed by its button, by Escape or by a click on the background; focused when the keyboard opened it (annex B 4, spec 9.5, NF08).

import { useEffect, useRef, type KeyboardEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { isInputField } from '../../shared/keyboard/keyboard.ts';
import { useKeyLayer } from '../../shared/keyboard/KeyboardContext.tsx';
import { useDock } from '../../shared/ui/dock.ts';
import { fromKeyboard } from '../../shared/ui/inputModality.ts';
import { ReadView } from './read/ReadView.tsx';
import { SheetBody } from './SheetBody.tsx';
import { SheetContext, type SheetPanelProps } from './SheetContext.tsx';
import { SheetFoot } from './SheetFoot.tsx';
import { SheetHead } from './SheetHead.tsx';
import { useSheetState } from './state/useSheetState.ts';

/**
 * Escape closes the panel (B-4-04, B-8.1-34): the layer of the panels of the keyboard manager when the focus is out of a
 * field, the panel itself when it is in one of its fields (the manager leaves the keys of a field to it); the form of a
 * document and the dialog « Autre… » take it before. Out of a field, the key goes on to the layer below once the panel
 * is closed: on the map, one Escape also gives up a cut, the link mode and the name typed, as in the 1.x (P6-T10).
 */
export function SheetPanel(props: SheetPanelProps) {
  const { t } = useTranslation();
  const { value, mode, setMode } = useSheetState(props);
  const panel = useRef<HTMLElement>(null);
  // Folded to its strip, the panel leaves Escape to the map (spec 3.0 section 15.2).
  const folded = useDock()?.folded === true;
  // Opened from the keyboard (Enter on a step, an entry of its menu), the panel takes the focus; opened by the mouse, it leaves it (1.x), and never takes it from a field.
  useEffect(() => {
    if (fromKeyboard() && !isInputField(document.activeElement)) panel.current?.focus();
  }, []);
  useKeyLayer('panel', (event, chord) => {
    // The release of the key that closed a dialog above is no second Escape.
    if (event.type !== 'keydown' || chord !== 'Escape') return false;
    props.onClose();
    return false;
  }, !folded);
  const onKeyDown = (event: KeyboardEvent<HTMLElement>) => {
    if (event.key !== 'Escape' || !isInputField(event.target)) return;
    event.stopPropagation();
    props.onClose();
  };
  const title = t('sheet.title', { code: props.step.code });
  return (
    <aside ref={panel} className="side-panel sheet-panel" aria-label={title} tabIndex={-1} data-testid="sheet-panel" onKeyDown={onKeyDown}>
      {value === null ? (
        <>
          <SheetHead step={props.step} view={null} save={null} onClose={props.onClose} />
          <p className="sheet-loading" data-testid="sheet-loading">{t('sheet.loading')}</p>
        </>
      ) : (
        <SheetContext.Provider value={value}>
          <SheetHead step={props.step} view={value.view} save={value.save} onClose={props.onClose} />
          {value.view.is_draft ? <SheetBody mode={mode} onMode={setMode} /> : <ReadView />}
          {value.view.is_draft && <SheetFoot />}
        </SheetContext.Provider>
      )}
    </aside>
  );
}
