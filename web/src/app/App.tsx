// App.tsx
// The application: keyboard manager, profile provider, then the shell with the screen of the address (B-0-11).

import { HomeScreen } from '../features/home/HomeScreen.tsx';
import { MapScreen } from '../features/map/MapScreen.tsx';
import { MembersScreen } from '../features/project/MembersScreen.tsx';
import { NewProjectScreen } from '../features/project/NewProjectScreen.tsx';
import { KeyboardProvider } from '../shared/keyboard/KeyboardContext.tsx';
import { ProfileProvider, useProfile } from './profile/ProfileProvider.tsx';
import type { ProfileSource } from './profile/profileStore.ts';
import { Shell, type Screens } from './Shell.tsx';

/**
 * The map of a route, its editor told whether the user is an administrator (the File menu, B-2.4-53, B-2.4-55) and the
 * languages of the configuration (the translations, annex B 5).
 */
function MapRoute({ mapId }: { mapId: string }) {
  const admin = useProfile((state) => state.profile?.is_admin === true);
  const languages = useProfile((state) => state.profile?.languages);
  return <MapScreen mapId={mapId} admin={admin} languages={languages ?? []} />;
}

/** The screens of the features: the home page, the creation of a project and its members, the map (mounted again for each map: B-1.1-09). */
export const SCREENS: Screens = {
  home: () => <HomeScreen />,
  newProject: () => <NewProjectScreen />,
  members: (projectId) => <MembersScreen projectId={projectId} />,
  map: (mapId) => <MapRoute mapId={mapId} />,
};

export function App({ source, screens = SCREENS }: { source?: ProfileSource; screens?: Screens }) {
  return (
    <KeyboardProvider>
      <ProfileProvider source={source}>
        <Shell screens={screens} />
      </ProfileProvider>
    </KeyboardProvider>
  );
}
