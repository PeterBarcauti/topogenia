// AccountMenu.tsx
// The user in the header: his initials and his name, and his menu: the language of the interface (ADR 083) and, in desktop mode, the command « Quit », confirmed before the host stops (spec 5.2).

import { useState, type MouseEvent } from 'react';
import { Languages, Power } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { initialsOf } from '../shared/initials.ts';
import { Button } from '../shared/ui/Button.tsx';
import { Confirm } from '../shared/ui/Confirm.tsx';
import { Menu } from '../shared/ui/Menu.tsx';
import { MENU_WIDTH } from '../shared/ui/menuPlacement.ts';
import { notify } from '../shared/ui/toasts.ts';
import { InterfaceLanguageDialog } from './InterfaceLanguageDialog.tsx';
import { useProfile } from './profile/ProfileProvider.tsx';
import { quitApplication } from './quit.ts';

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
 * could not be saved keeps the application open and says so; a failure says how to stop it otherwise.
 */
function QuitConfirm({ onClose }: { onClose: () => void }) {
  const { t } = useTranslation();
  const [sending, setSending] = useState(false);
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
    <Confirm title={t('app.quit_title')} testId="quit-confirm" confirmLabel={t('app.quit_confirm')} ready={!sending} onCancel={onClose} onConfirm={() => void quit()}>
      <p>{t('app.quit_text')}</p>
    </Confirm>
  );
}

/** « Quit » in desktop mode only: in server mode the browser tab closes the application (spec 5.2, 5.3). */
export function AccountMenu({ name }: { name: string }) {
  const { t } = useTranslation();
  const desktop = useProfile((state) => state.profile?.desktop === true);
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
        <Menu label="app.account_menu" testId="account-menu" items={desktop ? [LANGUAGE, QUIT] : [LANGUAGE]} at={menu}
          onClose={() => { setMenu(null); }}
          onRun={(id) => {
            if (id === QUIT.id) setConfirming(true);
            else setLanguage(true);
          }} />
      )}
      {confirming && <QuitConfirm onClose={() => { setConfirming(false); }} />}
      {language && <InterfaceLanguageDialog onClose={() => { setLanguage(false); }} />}
    </>
  );
}
