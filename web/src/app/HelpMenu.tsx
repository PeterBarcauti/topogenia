// HelpMenu.tsx
// The Help button of the header and its menu, on every page: the online help in a new tab in the language of the interface (F1 too, wherever the focus is), the keyboard shortcuts (? on the map too), whose panel it shows (lot 1 of the 3.4, ADR 109), and « Check for updates » for the administrator (ADR 111).

import { useState, type MouseEvent } from 'react';
import { BookOpen, ChevronDown, CircleHelp, Keyboard } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useStore } from 'zustand';
import { useGlobalShortcut } from '../shared/keyboard/KeyboardContext.tsx';
import { shortcutsPanel } from '../shared/state/shortcutsPanel.ts';
import { Button } from '../shared/ui/Button.tsx';
import { Icon } from '../shared/ui/Icon.tsx';
import { Menu } from '../shared/ui/Menu.tsx';
import { MENU_WIDTH } from '../shared/ui/menuPlacement.ts';
import { openHelp } from './help.ts';
import { useProfile } from './profile/ProfileProvider.tsx';
import { ShortcutsDialog } from './ShortcutsDialog.tsx';
import { UPDATE_COMMAND, openUpdate, updateItem } from './siteLinks.ts';

// The tooltip of the online help says where it opens, and in English for a language of the interface but French.
const ONLINE = { id: 'online', label: 'app.help_online', icon: BookOpen, shortcut: 'F1', disabled: false, title: 'app.help_title' } as const;
const SHORTCUTS = { id: 'shortcuts', label: 'shortcuts.title', icon: Keyboard, shortcut: '?', disabled: false } as const;

export function HelpMenu() {
  const { t, i18n } = useTranslation();
  const [menu, setMenu] = useState<{ x: number; y: number } | null>(null);
  const shown = useStore(shortcutsPanel, (state) => state.open);
  const profile = useProfile((state) => state.profile);
  const update = updateItem(profile, i18n.language);
  useGlobalShortcut('F1', () => { openHelp(i18n.language); });
  // Under its button, their right sides aligned: the help stands at the right of the header, next to the account.
  const openMenu = (event: MouseEvent<HTMLButtonElement>) => {
    const box = event.currentTarget.getBoundingClientRect();
    setMenu({ x: box.right - MENU_WIDTH, y: box.bottom + 4 });
  };
  return (
    <>
      <Button testId="header-help" icon={CircleHelp} aria-haspopup="menu" aria-expanded={menu !== null} onClick={openMenu}>
        {t('app.help')}
        <Icon icon={ChevronDown} size={14} />
      </Button>
      {menu !== null && (
        <Menu label="app.help_menu" testId="help-menu" items={update === null ? [ONLINE, SHORTCUTS] : [ONLINE, SHORTCUTS, update]} at={menu} underButton
          onClose={() => { setMenu(null); }}
          onRun={(id) => {
            if (id === ONLINE.id) openHelp(i18n.language);
            else if (id === UPDATE_COMMAND) openUpdate(profile, i18n.language);
            else shortcutsPanel.getState().setOpen(true);
          }} />
      )}
      {shown && <ShortcutsDialog onClose={() => { shortcutsPanel.getState().setOpen(false); }} />}
    </>
  );
}
