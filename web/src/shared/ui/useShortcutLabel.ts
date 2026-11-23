// useShortcutLabel.ts
// The shortcut of a command as the menus, the tooltips and the panel of the keyboard shortcuts write it, with the names of the keys (and of the wheel) in the language of the interface.

import { useTranslation } from 'react-i18next';
import { shortcutLabel } from '../commands/commands.ts';

const TRANSLATED_KEYS = ['Shift', 'Delete', 'Escape', 'Space', 'Enter', 'ArrowUp', 'ArrowLeft', 'ArrowRight', 'ArrowDown', 'Backspace', 'Wheel'] as const;

/** Writes a chord for the person: `Ctrl` always (B-2.5-36), `Maj` for Shift in French. */
export function useShortcutLabel(): (chord: string) => string {
  const { t } = useTranslation();
  const names = Object.fromEntries(TRANSLATED_KEYS.map((key) => [key, t(`keys.${key}`)]));
  return (chord) => shortcutLabel(chord, names);
}
