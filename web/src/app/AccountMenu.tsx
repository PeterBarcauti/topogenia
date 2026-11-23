// AccountMenu.tsx
// The user in the header: his initials and his name, and his menu: the language of the interface (ADR 083), « Check for updates » for the administrator (ADR 111) and, in desktop mode, the command « Quit », confirmed before the host stops, the confirmation saying a form changed and not validated (spec 5.2, R11).

import { useState, useSyncExternalStore, type MouseEvent } from 'react';
import { Languages, Power } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { initialsOf } from '../shared/initials.ts';
import { formsUnsaved, subscribeWrites } from '../shared/state/pendingWrites.ts';
import { Button } from '../shared/ui/Button.tsx';
import { Confirm } from '../shared/ui/Confirm.tsx';
import { Menu } from '../shared/ui/Menu.tsx';
import { MENU_WIDTH } from '../shared/ui/menuPlacement.ts';
import { notify } from '../shared/ui/toasts.ts';
import { InterfaceLanguageDialog } from './InterfaceLanguageDialog.tsx';
import { useProfile } from './profile/ProfileProvider.tsx';
import { quitApplication } from './quit.ts';
import { UPDATE_COMMAND, openUpdate, updateItem } from './siteLinks.ts';

const LANGUAGE = { id: 'language', label: 'app.language', icon: Languages, shortcut: undefined, disabled: false } as const;
const QUIT = { id: 'quit', label: 'app.quit', icon: Power, shortcut: undefined, disabled: false } as const;

function User({ name }: { name: string }) {
  return (
    <>
      <span className="avatar" aria-hidden="true">{initialsOf(name)}</span>
      {name}
    </>
  );
}

/**
 * The command « Quit » asked, then confirmed: what is typed leaves first, then the host answers and stops; a typing that
 * could not be saved keeps the application open and says so; a failure says how to stop it otherwise. A form changed and
 * not validated never leaves by itself: the confirmation says that its typing is lost and asks « Quit anyway », Cancel
 * keeping the focus first, as the icon near the clock asks, No by default (R11, ADR 115).
 */
function QuitConfirm({ onClose }: { onClose: () => void }) {
  const { t } = useTranslation();
  const [sending, setSending] = useState(false);
  const unsaved = useSyncExternalStore(subscribeWrites, formsUnsaved);
  const quit = async () => {
    setSending(true);
    try {
      if ((await quitApplication()) === 'unsaved') {
        notify(t('app.quit_unsaved'), 'error');
        onClose();
      }
    } catch {
      notify(t('app.quit_failed'), 'error');
      onClose();
    }
  };
  return (
    <Confirm title={t('app.quit_title')} testId="quit-confirm" confirmLabel={t(unsaved ? 'app.quit_anyway' : 'app.quit_confirm')} danger={unsaved} ready={!sending}
      onCancel={onClose} onConfirm={() => void quit()}>
      <p>{t('app.quit_text')}</p>
      {unsaved && <p className="field-error" data-testid="quit-confirm-unsaved">{t('app.quit_text_unsaved')}</p>}
    </Confirm>
  );
}

/** « Quit » in desktop mode only: in server mode the browser tab closes the application (spec 5.2, 5.3). */
export function AccountMenu({ name }: { name: string }) {
  const { t, i18n } = useTranslation();
  const profile = useProfile((state) => state.profile);
  const desktop = profile?.desktop === true;
  // « Check for updates » before « Quit », the last command of the menu.
  const update = updateItem(profile, i18n.language);
  const items = [LANGUAGE, ...(update === null ? [] : [update]), ...(desktop ? [QUIT] : [])];
  const [menu, setMenu] = useState<{ x: number; y: number } | null>(null);
  const [confirming, setConfirming] = useState(false);
  const [language, setLanguage] = useState(false);
  // Under its button, their right sides aligned: the account stands at the right end of the header.
  const openMenu = (event: MouseEvent<HTMLButtonElement>) => {
    const box = event.currentTarget.getBoundingClientRect();
    setMenu({ x: box.right - MENU_WIDTH, y: box.bottom + 4 });
  };
  return (
    <>
      <Button testId="header-user" className="user" title={t('app.account_help')} aria-haspopup="menu" aria-expanded={menu !== null} onClick={openMenu}>
        <User name={name} />
      </Button>
      {menu !== null && (
        <Menu label="app.account_menu" testId="account-menu" items={items} at={menu} underButton
          onClose={() => { setMenu(null); }}
          onRun={(id) => {
            if (id === QUIT.id) setConfirming(true);
            else if (id === UPDATE_COMMAND) openUpdate(profile, i18n.language);
            else setLanguage(true);
          }} />
      )}
      {confirming && <QuitConfirm onClose={() => { setConfirming(false); }} />}
      {language && <InterfaceLanguageDialog onClose={() => { setLanguage(false); }} />}
    </>
  );
}
