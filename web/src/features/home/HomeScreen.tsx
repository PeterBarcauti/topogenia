// HomeScreen.tsx
// The home page: banner and main button, projects and trash on two thirds, who takes part on one third, the starts (sample project, gallery, blank map, describe, open a file) of the main button without a project and of « New project » (annex B 1.3, AR-88, spec 6.1, ADR 111).

import { useMemo, useState, type MouseEvent } from 'react';
import { ArrowRight, ChevronDown, FileUp, FolderOpen, Plus, Sparkles } from 'lucide-react';
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
import { ProjectFilter } from './ProjectFilter.tsx';
import { filterProjects } from './projectFilter.ts';
import { ProjectList } from './ProjectList.tsx';
import { SampleOffer } from './SampleOffer.tsx';
import { useSampleCreation } from './sample.ts';
import { anchorOf, StartMenu, type StartAnchor } from './StartMenu.tsx';
import { TrashSection } from './TrashSection.tsx';
import { UpdateReminder } from './UpdateReminder.tsx';
import { useHome } from './useHome.ts';

type Open = 'pmap' | 'package' | 'describe' | null;

/** The main button: with no project, an administrator is offered the starts; otherwise it opens the map put forward (B-1.3-02). */
function Banner({ focus, starts, expanded, onStart }: { focus: ProjectItem | undefined; starts: boolean; expanded: boolean; onStart: (event: MouseEvent<HTMLButtonElement>) => void }) {
  const { t } = useTranslation();
  return (
    <section className="banner" aria-labelledby="home-title">
      <div className="banner-text">
        <p className="banner-eyebrow"><Icon icon={Sparkles} size={14} />{t('home.eyebrow')}</p>
        <h1 id="home-title">{t('home.title')}</h1>
      </div>
      <Button testId="home-describe" variant="primary" large icon={ArrowRight} disabled={focus === undefined && !starts} title={starts ? t('home.button_start_help') : t('home.button_help')}
        {...(starts ? { 'aria-haspopup': 'menu' as const, 'aria-expanded': expanded } : {})}
        onClick={(event) => {
          if (starts) onStart(event);
          else if (focus !== undefined) navigate(paths.map(focus.root_map_id));
        }}>
        {t('home.button')}
      </Button>
    </section>
  );
}

interface HeadProps {
  readonly projects: readonly ProjectItem[] | null;
  readonly shown: readonly ProjectItem[] | null;
  readonly filtering: boolean;
  readonly filter: string;
  readonly isAdmin: boolean;
  readonly expanded: boolean;
  readonly onFilter: (value: string) => void;
  readonly onDialog: (dialog: Exclude<Open, null>) => void;
  readonly onStart: (event: MouseEvent<HTMLButtonElement>) => void;
}

/** The header of the column of the projects: their count, the filter, and for the administrator the ways to bring a project in. */
function ProjectsHead({ projects, shown, filtering, filter, isAdmin, expanded, onFilter, onDialog, onStart }: HeadProps) {
  const { t } = useTranslation();
  const desktop = useProfile((state) => state.profile?.desktop === true);
  const links = useProfile((state) => state.profile?.links != null);
  // The tip names only the starts the menu offers: no sample outside the desktop mode, no template without the links to the site.
  const tip = desktop && links ? 'home.new_project_help' : desktop ? 'home.new_project_help_no_gallery' : links ? 'home.new_project_help_no_sample' : 'home.new_project_help_blank';
  return (
    <div className="home-col-head">
      <h2 id="projects-title">{t('home.projects')}</h2>
      {projects !== null && shown !== null && <span className="count" role="status">{filtering ? t('home.filter_count', { shown: shown.length, total: projects.length }) : projects.length}</span>}
      {projects !== null && projects.length > 0 && <ProjectFilter value={filter} onChange={onFilter} />}
      <span className="spacer" />
      {isAdmin && (
        <>
          <Button testId="home-open-pmap" icon={FolderOpen} variant="secondary" title={t('files.open_help')} onClick={() => {
            onDialog('pmap');
          }}>{t('files.open')}</Button>
          <Button testId="home-import-package" icon={FileUp} variant="secondary" title={t('package.import_help')} onClick={() => {
            onDialog('package');
          }}>{t('package.import')}</Button>
          {/* A project yet to create described with the assistant (spec 3.0 section 3.2); the main button keeps opening the map (B-1.3-02). */}
          <Button testId="home-describe-ai" icon={Sparkles} variant="secondary" title={t('assistant.panel.describe_help')} onClick={() => {
            onDialog('describe');
          }}>{t('assistant.panel.describe_button')}</Button>
          <Button testId="home-new-project" icon={Plus} variant="primary" aria-haspopup="menu" aria-expanded={expanded} title={t(tip)} onClick={onStart}>
            {t('home.new_project')}
            <Icon icon={ChevronDown} size={14} />
          </Button>
        </>
      )}
    </div>
  );
}

export function HomeScreen() {
  const { t, i18n } = useTranslation();
  const isAdmin = useProfile((state) => state.profile?.is_admin === true);
  const desktop = useProfile((state) => state.profile?.desktop === true);
  const nameOf = useLocalizedName();
  const { projects, error, trash, focus, participants, reload } = useHome();
  const sample = useSampleCreation();
  const [open, setOpen] = useState<Open>(null);
  const [starts, setStarts] = useState<StartAnchor | null>(null);
  const [toDelete, setToDelete] = useState<ProjectItem | null>(null);
  const [filter, setFilter] = useState('');
  const shown = useMemo(() => (projects === null ? null : filterProjects(projects, filter, i18n.language)), [projects, filter, i18n.language]);
  const filtering = projects !== null && shown !== null && shown.length !== projects.length;
  const close = () => {
    setOpen(null);
  };
  // No project yet: the main button, which has no map to open, offers the starts to the administrator.
  const startsFromBanner = isAdmin && projects?.length === 0;

  return (
    <div className="home" data-testid="home">
      <Banner focus={focus} starts={startsFromBanner} expanded={starts !== null} onStart={(event) => {
        setStarts(anchorOf(event, 'left', false));
      }} />
      <UpdateReminder />
      <div className="home-cols">
        <section className="home-projects" aria-labelledby="projects-title">
          <ProjectsHead projects={projects} shown={shown} filtering={filtering} filter={filter} isAdmin={isAdmin} expanded={starts !== null} onFilter={setFilter} onDialog={setOpen}
            onStart={(event) => {
              setStarts(anchorOf(event, 'right', true));
            }} />
          {error !== null && <p className="message-error" role="alert">{messageOf(error)}</p>}
          {projects === null && error === null && <p className="muted">{t('app.loading')}</p>}
          {projects?.length === 0 && <p className="empty-state" data-testid="home-empty">{isAdmin ? t('home.no_project_admin') : t('home.no_project')}</p>}
          {projects?.length === 0 && desktop && isAdmin && <SampleOffer sending={sample.sending} onCreate={sample.create} />}
          {sample.error !== null && <p className="message-error" role="alert" data-testid="home-sample-error">{sample.error}</p>}
          {shown !== null && shown.length === 0 && projects !== null && projects.length > 0 && <p className="empty-state" role="status" data-testid="home-filter-none">{t('home.filter_none')}</p>}
          {shown !== null && <ProjectList projects={shown} onDelete={setToDelete} />}
          {trash !== null && <TrashSection trash={trash} isAdmin={isAdmin} onChange={reload} />}
        </section>
        <Participants participants={participants} projectName={focus === undefined ? undefined : nameOf(focus)} />
      </div>
      {starts !== null && <StartMenu anchor={starts} onSample={sample.create} onDialog={setOpen} onClose={() => { setStarts(null); }} />}
      {open === 'pmap' && <OpenPmapDialog onClose={close} onCreated={reload} />}
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
