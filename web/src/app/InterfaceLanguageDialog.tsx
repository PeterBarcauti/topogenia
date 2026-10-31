// InterfaceLanguageDialog.tsx
// « Language of the interface », from the menu of the user (ADR 083): the languages the application is written in, each in itself; the one chosen is kept in the profile.

import { Check } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { BUNDLED_LANGUAGES } from '../shared/i18n/i18n.ts';
import { ownName, titleIn } from '../shared/i18n/languageNames.ts';
import { Dialog } from '../shared/ui/Dialog.tsx';
import { Icon } from '../shared/ui/Icon.tsx';
import { useProfile } from './profile/ProfileProvider.tsx';

export function InterfaceLanguageDialog({ onClose }: { onClose: () => void }) {
  const { t, i18n } = useTranslation();
  const configured = useProfile((state) => state.profile?.languages ?? []);
  const current = useProfile((state) => state.language);
  const changeLanguage = useProfile((state) => state.changeLanguage);
  // A language of the interface needs its file: one configured for the translations only is not offered.
  const languages = configured.filter((code) => BUNDLED_LANGUAGES.includes(code));
  return (
    <Dialog title={t('app.language')} testId="interface-language" onClose={onClose} wide>
      <p className="language-lead">{t('interface_language.help')}</p>
      <div className="language-grid" role="group" aria-label={t('app.language')}>
        {languages.map((code) => (
          <button key={code} type="button" className="language-card" aria-pressed={code === current} data-testid={`interface-language-${code}`}
            onClick={() => {
              void changeLanguage(code);
              onClose();
            }}>
            <span className="language-card-own" lang={code}>{ownName(code)}</span>
            <span className="language-card-in">{titleIn(code, i18n.language)}</span>
            {code === current && <span className="language-card-check"><Icon icon={Check} size={16} /></span>}
          </button>
        ))}
      </div>
    </Dialog>
  );
}
