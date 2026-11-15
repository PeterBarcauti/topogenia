// MembersScreen.tsx
// The members of a project for its facilitator: the editor preloaded, Save with the version read, the message in line, back to the map (annex B 1.5).

import { useEffect, useState, type SyntheticEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { api, type Schema } from '../../api/client.ts';
import { navigate, paths } from '../../app/router.ts';
import { messageOf } from '../../shared/i18n/errorMessage.ts';
import { Button } from '../../shared/ui/Button.tsx';
import { bodyOf, linesOf, type MemberLine } from './members.ts';
import { MembersEditor } from './MembersEditor.tsx';

type Detail = Schema<'ProjectDetail'>;

export function MembersScreen({ projectId }: { projectId: string }) {
  const { t } = useTranslation();
  const [project, setProject] = useState<Detail | null>(null);
  const [lines, setLines] = useState<MemberLine[]>([]);
  const [message, setMessage] = useState<{ text: string; failed: boolean } | null>(null);
  const [sending, setSending] = useState(false);

  useEffect(() => {
    const controller = new AbortController();
    api.get('/api/projects/{id}', { path: { id: projectId }, signal: controller.signal }).then((detail) => {
      setProject(detail);
      setLines(linesOf(detail.members));
    }, (failure: unknown) => {
      if (!controller.signal.aborted) setMessage({ text: messageOf(failure), failed: true });
    });
    return () => {
      controller.abort();
    };
  }, [projectId]);

  const save = async (event: SyntheticEvent) => {
    event.preventDefault();
    if (project === null) return;
    setSending(true);
    try {
      await api.put('/api/projects/{id}/members', { path: { id: projectId }, body: bodyOf(lines), version: project.version });
      // The reply gives the members; the project read again gives the version of the next change (If-Match).
      const detail = await api.get('/api/projects/{id}', { path: { id: projectId } });
      setProject(detail);
      setLines(linesOf(detail.members));
      setMessage({ text: t('project.members_saved'), failed: false });
    } catch (failure) {
      setMessage({ text: messageOf(failure), failed: true });
    } finally {
      setSending(false);
    }
  };

  return (
    <div className="page-form" data-testid="members-screen">
      <h1>{t('project.members_title', { code: project?.code ?? '' })}</h1>
      <form onSubmit={(event) => void save(event)}>
        <MembersEditor lines={lines} onChange={setLines} />
        {message !== null && (
          <p className={message.failed ? 'message-error' : 'message-success'} role={message.failed ? 'alert' : 'status'} data-testid="members-message">{message.text}</p>
        )}
        <div className="form-actions">
          {project !== null && (
            <Button testId="members-open-map" variant="secondary" onClick={() => { navigate(paths.map(project.root_map_id)); }}>{t('project.open_map')}</Button>
          )}
          <Button testId="members-save" type="submit" variant="primary" disabled={project === null || sending}>{t('project.save')}</Button>
        </div>
      </form>
    </div>
  );
}
