// router.ts
// Navigation by the history of the browser and the four screens of the application (annex B 1.1, AR-30).

import { useSyncExternalStore } from 'react';

/** The event that tells the subscribers that the address changed by {@link navigate} (`popstate` covers the browser). */
export const NAVIGATION_EVENT = 'app:navigation';

/** Goes to a path of the application; nothing when it is the current one, the entry is replaced when asked (B-1.1-02, 03). */
export function navigate(path: string, replace = false): void {
  if (path === window.location.pathname) return;
  if (replace) window.history.replaceState(null, '', path);
  else window.history.pushState(null, '', path);
  window.dispatchEvent(new Event(NAVIGATION_EVENT));
}

function subscribe(onChange: () => void): () => void {
  window.addEventListener('popstate', onChange);
  window.addEventListener(NAVIGATION_EVENT, onChange);
  return () => {
    window.removeEventListener('popstate', onChange);
    window.removeEventListener(NAVIGATION_EVENT, onChange);
  };
}

/** The current path, followed by the component that reads it (B-1.1-01). */
export function usePath(): string {
  return useSyncExternalStore(subscribe, () => window.location.pathname);
}

/** A screen of the application and what its path names; no route for the trash, files, sheet, translations, profile (B-1.1-11). */
export type Route =
  | { readonly screen: 'home' }
  | { readonly screen: 'new-project' }
  | { readonly screen: 'members'; readonly projectId: string }
  | { readonly screen: 'map'; readonly mapId: string }
  | { readonly screen: 'not-found' };

const GUID = '[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}';
const MEMBERS = new RegExp(`^/projects/(${GUID})/members$`);
const MAP = new RegExp(`^/maps/(${GUID})$`);

/** The route of a path: final slash tolerated, identifier in lower case (B-1.1-04 to 10). */
export function routeOf(path: string): Route {
  const clean = path.replace(/\/+$/, '') || '/';
  if (clean === '/') return { screen: 'home' };
  if (clean === '/projects/new') return { screen: 'new-project' };
  const members = MEMBERS.exec(clean)?.[1];
  if (members !== undefined) return { screen: 'members', projectId: members.toLowerCase() };
  const map = MAP.exec(clean)?.[1];
  if (map !== undefined) return { screen: 'map', mapId: map.toLowerCase() };
  return { screen: 'not-found' };
}

/** The paths of the screens that name an object, for the links and buttons that lead to them. */
export const paths = {
  home: '/',
  newProject: '/projects/new',
  members: (projectId: string): string => `/projects/${projectId}/members`,
  map: (mapId: string): string => `/maps/${mapId}`,
};
