// ProjectList.tsx
// The projects of the home page: translated name to the root map, code, description, counts, role and classification, and the facilitator's actions (B-1.3-09 to 14, spec 4.1).

import type { MouseEvent } from 'react';
import { Footprints, Map as MapIcon, ShieldCheck, Trash2, Users } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { navigate, paths } from '../../app/router.ts';
import { useLocalizedName } from '../../shared/i18n/localizedName.ts';
import { Button } from '../../shared/ui/Button.tsx';
import { Icon } from '../../shared/ui/Icon.tsx';
import type { ProjectItem } from './focus.ts';

const follow = (path: string) => (event: MouseEvent<HTMLAnchorElement>) => {
  event.preventDefault();
  navigate(path);
};

export function ProjectList({ projects, onDelete }: { projects: readonly ProjectItem[]; onDelete: (project: ProjectItem) => void }) {
  const { t } = useTranslation();
  const nameOf = useLocalizedName();
  return (
    <ul className="project-list">
      {projects.map((project) => (
        <li key={project.id} className="project" data-testid={`project-${project.code}`}>
          <div className="project-title">
            <a href={paths.map(project.root_map_id)} data-testid={`project-${project.code}-open`} onClick={follow(paths.map(project.root_map_id))}>
              {nameOf(project)}
            </a>
            <span className="chip-code">{project.code}</span>
          </div>
          {project.description !== null && project.description !== '' && <p>{project.description}</p>}
          <div className="project-meta">
            <span><Icon icon={Footprints} size={13} />{t('home.steps', { count: project.step_count })}</span>
            <span aria-hidden="true">·</span>
            <span><Icon icon={MapIcon} size={13} />{t('home.maps', { count: project.map_count })}</span>
            <span aria-hidden="true">·</span>
            <span>{t(`roles.${project.role}`)}</span>
            <span aria-hidden="true">·</span>
            <span data-testid={`project-${project.code}-classification`}><Icon icon={ShieldCheck} size={13} />{t(`classification.${project.classification}`)}</span>
          </div>
          {project.role === 'facilitator' && (
            <div className="project-actions">
              <Button testId={`project-${project.code}-members`} icon={Users} onClick={() => {
                navigate(paths.members(project.id));
              }}>{t('home.members')}</Button>
              <Button testId={`project-${project.code}-delete`} icon={Trash2} variant="danger" onClick={() => {
                onDelete(project);
              }}>{t('home.delete')}</Button>
            </div>
          )}
        </li>
      ))}
    </ul>
  );
}
