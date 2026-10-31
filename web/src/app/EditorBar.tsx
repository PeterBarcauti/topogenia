// EditorBar.tsx
// The bar of the editor in the header, in the place of the signature: the File button next to the brand as in Office, the breadcrumb of the map shown, the classification of the project and the Members button (annex B 2.1, spec 4.1, 9.5, AR-155).

import { Fragment, type MouseEvent } from 'react';
import { ChevronDown, ChevronRight, FolderOpen, Users } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { ProjectProperties } from '../features/project/properties/ProjectProperties.tsx';
import { localizedName } from '../shared/i18n/localizedName.ts';
import type { Crumb, OpenProject } from '../shared/state/openProject.ts';
import { Button } from '../shared/ui/Button.tsx';
import { Icon } from '../shared/ui/Icon.tsx';
import { useShownLanguage } from './ProcessLanguageMenu.tsx';
import { navigate, paths } from './router.ts';

/** The name of a map of the breadcrumb in a language, by the rule of every name (1.x `traduire`). */
export const crumbName = (crumb: Crumb, language: string): string =>
  localizedName({ name: crumb.name, source_language: crumb.sourceLanguage, name_translations: crumb.translations }, language);

/**
 * Links to the parent maps, the map shown last with `aria-current`, chevrons between them, the code of the project
 * (B-2.1-01 to B-2.1-04); each level named for the end-to-end tests by its depth from the root map (S-3.5-04).
 */
function Breadcrumb({ project }: { project: OpenProject }) {
  const { t } = useTranslation();
  // The names of the maps in the language the process is shown in (ADR 083).
  const language = useShownLanguage(project) ?? project.languages.origin;
  const last = project.breadcrumb.length - 1;
  const open = (event: MouseEvent<HTMLAnchorElement>, mapId: string) => {
    event.preventDefault();
    navigate(paths.map(mapId));
  };
  return (
    <nav className="crumbs" aria-label={t('map.breadcrumb')} data-testid="breadcrumb">
      {project.breadcrumb.map((crumb, index) => (
        <Fragment key={crumb.mapId}>
          {index > 0 && <Icon icon={ChevronRight} size={14} />}
          {index === last
            ? <span aria-current="page" data-testid={`breadcrumb-level-${String(index)}`}>{crumbName(crumb, language)}</span>
            : (
              <a href={paths.map(crumb.mapId)} data-testid={`breadcrumb-level-${String(index)}`} onClick={(event) => { open(event, crumb.mapId); }}>
                {crumbName(crumb, language)}
              </a>
            )}
        </Fragment>
      ))}
      <span className="chip-code">{project.code}</span>
    </nav>
  );
}

export function EditorBar({ project }: { project: OpenProject }) {
  const { t } = useTranslation();
  const openFile = (event: MouseEvent<HTMLButtonElement>) => {
    const box = event.currentTarget.getBoundingClientRect();
    project.openFileMenu({ x: box.left, y: box.bottom + 4 });
  };
  return (
    <>
      <Button testId="header-file" icon={FolderOpen} title={t('map.file_help')} aria-haspopup="menu" onClick={openFile}>
        {t('map.file')}
        <Icon icon={ChevronDown} size={14} />
      </Button>
      <span className="head-sep" aria-hidden="true" />
      <Breadcrumb project={project} />
      <div className="head-actions">
        <ProjectProperties projectId={project.id} code={project.code} />
        {project.canEdit && (
          <Button testId="header-members" icon={Users} onClick={() => {
            navigate(paths.members(project.id));
          }}>{t('map.members')}</Button>
        )}
      </div>
    </>
  );
}
