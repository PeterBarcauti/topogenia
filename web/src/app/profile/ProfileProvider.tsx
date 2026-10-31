// ProfileProvider.tsx
// Gives the store of the profile to the application and reads the profile when the application starts (B-0-11, B-1.2-01).

import { createContext, useContext, useEffect, useState, type ReactNode } from 'react';
import { useStore } from 'zustand';
import type { StoreApi } from 'zustand/vanilla';
import { applyLanguage, i18next } from '../../shared/i18n/i18n.ts';
import { apiProfileSource, createProfileStore, type ProfileSource, type ProfileState } from './profileStore.ts';

const ProfileContext = createContext<StoreApi<ProfileState> | null>(null);

export function ProfileProvider({ source = apiProfileSource, children }: { source?: ProfileSource; children: ReactNode }) {
  const [store] = useState(() => createProfileStore(source, applyLanguage, i18next.language));
  useEffect(() => {
    void store.getState().load();
  }, [store]);
  return <ProfileContext.Provider value={store}>{children}</ProfileContext.Provider>;
}

/** A part of the profile state, followed by the component that reads it. */
export function useProfile<T>(select: (state: ProfileState) => T): T {
  const store = useContext(ProfileContext);
  if (store === null) throw new Error('useProfile outside ProfileProvider.');
  return useStore(store, select);
}
