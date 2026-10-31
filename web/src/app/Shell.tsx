// Shell.tsx
// The envelope of every screen: profile loading or its error, header, the screen of the address, notifications, Ctrl+S, the project opened remembered, the application stopped (annex B 1.2, B-2.5-05, AR-49, spec 5.2).

import { useEffect, type ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { useStore } from 'zustand';
import { messageOf } from '../shared/i18n/errorMessage.ts';
import { useGlobalShortcut } from '../shared/keyboard/KeyboardContext.tsx';
import { openProject } from '../shared/state/openProject.ts';
import { notify } from '../shared/ui/toasts.ts';
import { Toaster } from '../shared/ui/Toaster.tsx';
import { ErrorPage } from './ErrorPage.tsx';
import { Header } from './Header.tsx';
import { useProfile } from './profile/ProfileProvider.tsx';
import { stopped } from './quit.ts';
import { navigate, paths, routeOf, usePath, type Route } from './router.ts';

/** What each screen of a route shows; the page of an unknown address is the shell's own. */
export interface Screens {
  readonly home: () => ReactNode;
  readonly newProject: () => ReactNode;
  readonly members: (projectId: string) => ReactNode;
  readonly map: (mapId: string) => ReactNode;
}

function NotFound() {
  const { t } = useTranslation();
  return <ErrorPage testId="page-not-found" message={t('app.page_error')} actionLabel={t('app.home')} onAction={() => {
    navigate(paths.home);
  }} />;
}

/** The page left once the command « Quit » stopped the host of the desktop mode: nothing else answers any more (spec 5.2). */
function Stopped() {
  const { t } = useTranslation();
  return (
    <div className="page-error" role="status" data-testid="page-stopped">
      <p>{t('app.stopped')}</p>
      <p className="muted">{t('app.stopped_help')}</p>
    </div>
  );
}

/** What makes a screen new: the object its route names, so that the editor is mounted again for each map (B-1.1-09). */
const screenKey = (route: Route): string =>
  route.screen === 'map' ? `map:${route.mapId}` : route.screen === 'members' ? `members:${route.projectId}` : route.screen;

function screenOf(route: Route, screens: Screens): ReactNode {
  switch (route.screen) {
    case 'home':
      return screens.home();
    case 'new-project':
      return screens.newProject();
    case 'members':
      return screens.members(route.projectId);
    case 'map':
      return screens.map(route.mapId);
    case 'not-found':
      return <NotFound />;
  }
}

export function Shell({ screens }: { screens: Screens }) {
  const { t } = useTranslation();
  const route = routeOf(usePath());
  const profile = useProfile((state) => state.profile);
  const error = useProfile((state) => state.error);
  const load = useProfile((state) => state.load);
  const rememberProject = useProfile((state) => state.rememberProject);
  const projectId = useStore(openProject, (state) => state.project?.id ?? null);
  const halted = useStore(stopped, (state) => state.stopped);
  useGlobalShortcut('Ctrl+S', () => {
    notify(t('app.autosaved'));
  });
  // The project of the map opened becomes the last one opened, put forward by the home page (AR-49, B-1.3-02, B-1.3-24).
  useEffect(() => {
    if (projectId !== null) void rememberProject(projectId);
  }, [projectId, rememberProject]);
  if (halted) return <Stopped />;
  if (profile === null && error !== null)
    return <ErrorPage testId="page-profile-error" message={messageOf(error)} actionLabel={t('app.retry')} onAction={() => void load()} />;
  if (profile === null) return <p className="page-loading" data-testid="page-loading">{t('app.loading')}</p>;
  return (
    <>
      <Header />
      <main key={screenKey(route)} className="screen" data-testid={`screen-${route.screen}`}>{screenOf(route, screens)}</main>
      <Toaster />
    </>
  );
}
