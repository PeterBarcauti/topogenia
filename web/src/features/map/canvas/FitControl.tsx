// FitControl.tsx
// « Show all » of the map: the button next to the zoom and the way given to the editor to run it, for the command Shift+1 (lot 1 of the 3.4).

import { useEffect } from 'react';
import { ControlButton } from '@xyflow/react';
import { Maximize } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Icon } from '../../../shared/ui/Icon.tsx';
import { useShortcutLabel } from '../../../shared/ui/useShortcutLabel.ts';
import { runMapCommand, useEditor } from '../state/EditorContext.tsx';

/** Gives the editor the way to show the whole map: the bounded view of the canvas, whose largest zoom is 1. */
export function useFit(editor: ReturnType<typeof useEditor>, fit: () => void) {
  useEffect(() => {
    editor.canvas.showFit(fit);
    return () => {
      editor.canvas.showFit(null);
    };
  }, [editor, fit]);
}

/** « Show all » next to the zoom: it runs the command, as Shift+1 does. */
export function FitButton() {
  const { t } = useTranslation();
  const shortcut = useShortcutLabel();
  const editor = useEditor();
  const name = `${t('map.fit')} (${shortcut('Shift+1')})`;
  return (
    <ControlButton onClick={() => { runMapCommand(editor, 'fit'); }} title={name} aria-label={name} data-testid="map-fit">
      <Icon icon={Maximize} />
    </ControlButton>
  );
}
