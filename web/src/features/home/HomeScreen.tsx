// HomeScreen.tsx
// The home page: banner and main button, projects and trash on two thirds, who takes part on one third, the sample project of the desktop mode (annex B 1.3, AR-88, spec 6.1).

import { useState } from 'react';
import { ArrowRight, FileUp, FolderOpen, Plus, Sparkles } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useProfile } from '../../app/profile/ProfileProvider.tsx';
import { navigate, paths } from '../../app/router.ts';
import { messageOf } from '../../shared/i18n/errorMessage.ts';
import { useLocalizedName } from '../../shared/i18n/localizedName.ts';
import { Button } from '../../shared/ui/Button.tsx';
import { Icon } from '../../shared/ui/Icon.tsx';
import { notify } from '../../shared/ui/toasts.ts';
import { OpenPmapDialog } from '../project/files/OpenPmapDialog.tsx';
import { DescribeDialog } from '../assistant/panel/DescribeDialog.tsx';
import { ImportPackageDialog } from '../project/package/ImportPackageDialog.tsx';
import { DeleteProjectDialog } from './DeleteProjectDialog.tsx';
import type { ProjectItem } from './focus.ts';
import { Participants } from './Participants.tsx';
import { ProjectList } from './ProjectList.tsx';
import { SampleOffer } from './SampleOffer.tsx';
import { TrashSection } from './TrashSection.tsx';
import { useHome } from './useHome.ts';

type Open = 'pmap' | 'package' | 'describe' | null;

function Banner({ focus }: { focus: ProjectItem | undefined }) {
  const { t } = useTranslation();
  return (
    <section className="banner" aria-labelledby="home-title">
      <div className="banner-text">
        <p className="banner-eyebrow"><Icon icon={Sparkles} size={14} />{t('home.eyebrow')}</p>
        <h1 id="home-title">{t('home.title')}</h1>
      </div>
      <Button testId="home-describe" variant="primary" large icon={ArrowRight} disabled={focus === undefined} title={t('home.button_help')}
        onClick={() => {
          if (focus !== undefined) navigate(paths.map(focus.root_map_id));
        }}>
        {t('home.button')}
      </Button>
    </section>
  );
}

export function HomeScreen() {
  const { t } = useTranslation();
  const isAdmin = useProfile((state) => state.profile?.is_admin === true);
  const desktop = useProfile((state) => state.profile?.desktop === true);
  const nameOf = useLocalizedName();
  const { projects, error, trash, focus, participants, reload } = useHome();
  const [open, setOpen] = useState<Open>(null);
  const [toDelete, setToDelete] = useState<ProjectItem | null>(null);
  const close = () => {
    setOpen(null);
  };

  return (
    <div className="home" data-testid="home">
      <Banner focus={focus} />
      <div className="home-cols">
        <section className="home-projects" aria-labelledby="projects-title">
          <div className="home-col-head">
            <h2 id="projects-title">{t('home.projects')}</h2>
            {projects !== null && <span className="count">{projects.length}</span>}
            <span className="spacer" />
            {isAdmin && (
              <>
                <Button testId="home-open-pmap" icon={FolderOpen} variant="secondary" title={t('files.open_help')} onClick={() => {
                  setOpen('pmap');
                }}>{t('files.open')}</Button>
                <Button testId="home-import-package" icon={FileUp} variant="secondary" title={t('package.import_help')} onClick={() => {
                  setOpen('package');
                }}>{t('package.import')}</Button>
                {/* A project yet to create described with the assistant (spec 3.0 section 3.2); the main button keeps opening the map (B-1.3-02). */}
                <Button testId="home-describe-ai" icon={Sparkles} variant="secondary" title={t('assistant.panel.describe_help')} onClick={() => {
                  setOpen('describe');
                }}>{t('assistant.panel.describe_button')}</Button>
                <Button testId="home-new-project" icon={Plus} variant="primary" onClick={() => {
                  navigate(paths.newProject);
                }}>{t('home.new_project')}</Button>
              </>
            )}
          </div>
          {error !== null && <p className="message-error" role="alert">{messageOf(error)}</p>}
          {projects === null && error === null && <p className="muted">{t('app.loading')}</p>}
          {projects?.length === 0 && <p className="empty-state" data-testid="home-empty">{isAdmin ? t('home.no_project_admin') : t('home.no_project')}</p>}
          {projects?.length === 0 && desktop && isAdmin && <SampleOffer onCreated={reload} />}
          {projects !== null && <ProjectList projects={projects} onDelete={setToDelete} />}
          {trash !== null && <TrashSection trash={trash} isAdmin={isAdmin} onChange={reload} />}
        </section>
        <Participants participants={participants} projectName={focus === undefined ? undefined : nameOf(focus)} />
      </div>
      {open === 'pmap' && <OpenPmapDialog onClose={close} />}
      {open === 'describe' && <DescribeDialog onClose={close} />}
      {open === 'package' && <ImportPackageDialog onClose={close} />}
      {toDelete !== null && (
        <DeleteProjectDialog project={toDelete} name={nameOf(toDelete)} retentionDays={trash?.retention_days ?? 30}
          onClose={() => {
            setToDelete(null);
          }}
          onDeleted={() => {
            notify(t('home.deleted', { code: toDelete.code }), 'success');
            setToDelete(null);
            reload();
          }} />
      )}
    </div>
  );
}
