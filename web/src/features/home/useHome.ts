// useHome.ts
// What the home page reads: the projects, the trash (optional), the participants of the project put forward (annex B 1.3).

import { useCallback, useEffect, useState } from 'react';
import { api, type Schema } from '../../api/client.ts';
import { useProfile } from '../../app/profile/ProfileProvider.tsx';
import { focusProject, type ProjectItem } from './focus.ts';

export interface HomeData {
  /** The projects the user sees, null while they are read. */
  readonly projects: readonly ProjectItem[] | null;
  /** The failure of the reading of the projects. */
  readonly error: unknown;
  /** The trash, null while read or when it could not be read (B-1.3-16). */
  readonly trash: Schema<'TrashView'> | null;
  /** The project of the main button and of the participants (AR-49). */
  readonly focus: ProjectItem | undefined;
  readonly participants: readonly Schema<'MemberView'>[];
  /** Reads the projects and the trash again (after a deletion, a restoration). */
  readonly reload: () => void;
}

export function useHome(): HomeData {
  const lastProjectId = useProfile((state) => state.profile?.last_project_id ?? null);
  const [projects, setProjects] = useState<readonly ProjectItem[] | null>(null);
  const [error, setError] = useState<unknown>(null);
  const [trash, setTrash] = useState<Schema<'TrashView'> | null>(null);
  const [participants, setParticipants] = useState<readonly Schema<'MemberView'>[]>([]);
  const [round, setRound] = useState(0);

  useEffect(() => {
    const controller = new AbortController();
    api.get('/api/projects', { signal: controller.signal }).then((list) => {
      setProjects(list);
      setError(null);
    }, (failure: unknown) => {
      if (!controller.signal.aborted) setError(failure);
    });
    // The trash completes the page: without it, the home page stays usable.
    api.get('/api/trash', { signal: controller.signal }).then(setTrash, () => undefined);
    return () => {
      controller.abort();
    };
  }, [round]);

  const focus = projects === null ? undefined : focusProject(projects, lastProjectId);
  const focusId = focus?.id;
  useEffect(() => {
    if (focusId === undefined) return undefined;
    const controller = new AbortController();
    api.get('/api/projects/{id}/participants', { path: { id: focusId }, signal: controller.signal }).then(setParticipants, () => undefined);
    return () => {
      controller.abort();
    };
  }, [focusId]);

  const reload = useCallback(() => {
    setRound((value) => value + 1);
  }, []);
  return { projects, error, trash, focus, participants, reload };
}
