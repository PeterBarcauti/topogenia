// ProjectProperties.tsx
// The classification of the open project in the bar of the editor, which opens the properties of the project, and their dialog (spec 4.1, 10).

import { useEffect } from 'react';
import { ShieldCheck } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useStore } from 'zustand';
import { Button } from '../../../shared/ui/Button.tsx';
import { ProjectPropertiesDialog } from './ProjectPropertiesDialog.tsx';
import { closeProjectProperties, followProject, openProjectProperties, projectProperties, showDetail } from './projectProperties.ts';

/**
 * The level of the project next to its code (S-4.1-07), as a button: every member opens the properties, which only a
 * facilitator changes. Nothing is shown while the project is read, or when it could not be.
 */
export function ProjectProperties({ projectId, code }: { projectId: string; code: string }) {
  const { t } = useTranslation();
  const detail = useStore(projectProperties, (state) => state.detail);
  const open = useStore(projectProperties, (state) => state.open);
  useEffect(() => followProject(projectId), [projectId]);
  const level = detail?.id.toLowerCase() === projectId.toLowerCase() ? t(`classification.${detail.classification}`) : null;
  const help = level === null ? '' : t('project.classification_button', { level });
  return (
    <>
      {level !== null && (
        <Button testId="header-classification" icon={ShieldCheck} title={help} aria-label={help} onClick={openProjectProperties}>{level}</Button>
      )}
      {open && (
        <ProjectPropertiesDialog projectId={projectId} code={code} onClose={closeProjectProperties} onDetail={showDetail} />
      )}
    </>
  );
}
