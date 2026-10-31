// Header.tsx
// The permanent header: brand to the home page with the version, signature or the bar of the editor of the map shown, the language of the open process and its translations, the gear of the settings of the assistant for the administrator, user and his menu with the language of the interface (annex B 1.2, 2.1, spec 5.2, spec 3.0 section 4, ADR 083).

import { useState, type MouseEvent } from 'react';
import { Languages, Settings } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useStore } from 'zustand';
import { AssistantSettingsDialog } from '../features/assistant/AssistantSettingsDialog.tsx';
import { openProject } from '../shared/state/openProject.ts';
import { Button } from '../shared/ui/Button.tsx';
import { Logo } from '../shared/ui/Logo.tsx';
import { AccountMenu } from './AccountMenu.tsx';
import { EditorBar } from './EditorBar.tsx';
import { ProcessLanguageMenu } from './ProcessLanguageMenu.tsx';
import { useProfile } from './profile/ProfileProvider.tsx';
import { navigate, paths } from './router.ts';

export function Header() {
  const { t } = useTranslation();
  const name = useProfile((state) => state.profile?.display_name ?? '');
  const isAdmin = useProfile((state) => state.profile?.is_admin === true);
  const project = useStore(openProject, (state) => state.project);
  const [assistant, setAssistant] = useState(false);
  const goHome = (event: MouseEvent<HTMLAnchorElement>) => {
    event.preventDefault();
    navigate(paths.home);
  };
  return (
    <header className="app-header" data-testid="app-header">
      <a className="brand" href={paths.home} title={`${t('app.home')} · ${t('app.name')} ${__APP_VERSION__}`} data-testid="header-brand" onClick={goHome}>
        <Logo />
        {t('app.name')}
      </a>
      <span className="head-sep" aria-hidden="true" />
      {project === null ? <span className="brand-sign">{t('app.signature')}</span> : <EditorBar project={project} />}
      <div className="head-right">
        {project !== null && <ProcessLanguageMenu project={project} />}
        {project?.canEdit === true && project.openTranslations !== undefined && (
          <Button testId="header-translations" icon={Languages} title={t('app.translations_help')} onClick={project.openTranslations}>
            {t('app.translations')}
          </Button>
        )}
        {isAdmin && (
          <Button testId="header-assistant" icon={Settings} title={t('assistant.title')} aria-label={t('assistant.title')} onClick={() => { setAssistant(true); }} />
        )}
        <span className="head-sep" aria-hidden="true" />
        <AccountMenu name={name} />
      </div>
      {assistant && <AssistantSettingsDialog onClose={() => { setAssistant(false); }} />}
    </header>
  );
}
